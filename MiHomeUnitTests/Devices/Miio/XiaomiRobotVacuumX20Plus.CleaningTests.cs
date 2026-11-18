using MiHomeLib.MiioDevices.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Moq;
using Xunit;
using static MiHomeLib.MiioDevices.XiaomiRobotVacuumX20Plus;

namespace MiHomeUnitTests.Devices.Miio;

public partial class XiaomiRobotVacuumX20PlusTests
{
    public static IEnumerable<object[]> ActionCases()
    {
        foreach (var isAsync in new[] { false, true })
        {
            yield return [isAsync, "dust", 2, 4];
            yield return [isAsync, "wash", 2, 6];
            yield return [isAsync, "dry", 2, 8];
            yield return [isAsync, "stop-dry", 2, 9];
            yield return [isAsync, "eject", 2, 10];
            yield return [isAsync, "sound", 7, 2];
            yield return [isAsync, "main-brush", 9, 1];
            yield return [isAsync, "side-brush", 10, 1];
            yield return [isAsync, "filter", 11, 1];
            yield return [isAsync, "mop", 18, 1];
        }
    }

    [Theory]
    [MemberData(nameof(ActionCases))]
    public async Task Dock_Audio_And_Consumable_Actions_Use_Expected_Identifiers(bool isAsync, string action, int service, int actionId)
    {
        SetupCallAction();
        (Action Sync, Func<Task> Async) call = action switch
        {
            "dust" => (_miRobot.StartDustCollection, _miRobot.StartDustCollectionAsync),
            "wash" => (_miRobot.StartMopWashing, _miRobot.StartMopWashingAsync),
            "dry" => (_miRobot.StartDrying, _miRobot.StartDryingAsync),
            "stop-dry" => (_miRobot.StopDrying, _miRobot.StopDryingAsync),
            "eject" => (_miRobot.EjectFromDock, _miRobot.EjectFromDockAsync),
            "sound" => (_miRobot.PlaySound, _miRobot.PlaySoundAsync),
            _ => (() => _miRobot.ResetConsumable((ConsumableType)service), () => _miRobot.ResetConsumableAsync((ConsumableType)service))
        };

        await Call(isAsync, call.Sync, call.Async);

        VerifyCallAction(service, actionId);
    }

    [Fact]
    public async Task Unknown_Consumable_Is_Rejected_Before_Request()
    {
        Func<Task> reset = () => _miRobot.ResetConsumableAsync((ConsumableType)12);

        await reset.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(false, 0, 0, false)]
    [InlineData(true, 100, -50, true)]
    public async Task Remote_Control_Uses_Firmware_Payload_And_Random_Nonce(bool isAsync, int velocity, int rotation, bool sound)
    {
        SetupSetProperties();

        await Call(isAsync, () => _miRobot.RemoteControlMoveStep(velocity, rotation, sound),
            () => _miRobot.RemoteControlMoveStepAsync(velocity, rotation, sound));

        var message = JsonSerializer.Deserialize<JsonElement>((string)_miioTransport.Invocations.Last().Arguments[0]);
        var property = message.GetProperty("params")[0];
        property.GetProperty("did").GetString().Should().Be("set-4-15");
        property.GetProperty("siid").GetInt32().Should().Be(4);
        property.GetProperty("piid").GetInt32().Should().Be(15);
        using var payload = JsonDocument.Parse(property.GetProperty("value").GetString());
        payload.RootElement.GetProperty("spdv").GetInt32().Should().Be(velocity);
        payload.RootElement.GetProperty("spdw").GetInt32().Should().Be(rotation);
        payload.RootElement.GetProperty("audio").GetString().Should().Be(sound ? "true" : "false");
        payload.RootElement.GetProperty("random").GetInt32().Should().BeInRange(0, 65534);
    }

    [Theory]
    [InlineData("remote", "set_properties", false)]
    [InlineData("voice", "set_properties", false)]
    [InlineData("room", "action", false)]
    [InlineData("volume", "set_properties", true)]
    public async Task Commands_Select_Expected_Transport_Retry_Policy(string operation, string method, bool repeatable)
    {
        _miioTransport = new Mock<IMiioTransport>();
        var requests = _miioTransport.As<IMiioRequests>();
        SetupMiioInfo();
        requests.Setup(transport => transport.RequestAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<MiioRetryPolicy>()))
            .Returns<string, object, MiioRetryPolicy>((name, parameters, _) => name == "miIO.info"
                ? _miioTransport.Object.SendMessageAsync(JsonSerializer.Serialize(new { id = 1, method = name, @params = parameters }))
                : Task.FromResult(name == "action" ? "{\"result\":{\"code\":0}}" : "{\"result\":[{\"code\":0}]}"));
        using var robot = new XiaomiRobotVacuumX20Plus(_miioTransport.Object);

        switch (operation)
        {
            case "remote": await robot.RemoteControlMoveStepAsync(0, 0); break;
            case "voice": await robot.InstallVoicePackageAsync("20", "http://192.0.2.1/voice.pkg", "0123456789abcdef0123456789abcdef", 1024); break;
            case "room": await robot.StartRoomCleaningAsync([new RoomSelection(1)]); break;
            case "volume": await robot.SetVolumeAsync(50); break;
        }

        var policy = repeatable ? MiioRetryPolicy.SafeToRepeat : MiioRetryPolicy.DoNotRepeat;
        requests.Verify(transport => transport.RequestAsync(method, It.IsAny<object>(), policy), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Room_Cleaning_Sends_Work_Mode_And_Ordered_Selections(bool isAsync)
    {
        SetupCallAction();
        RoomSelection[] rooms = [new(7, 2, SuctionMode.Max, WaterLevel.High), new(3)];

        await Call(isAsync, () => _miRobot.StartRoomCleaning(rooms), () => _miRobot.StartRoomCleaningAsync(rooms));

        var parameters = GetActionParameters();
        parameters.GetProperty("siid").GetInt32().Should().Be(4);
        parameters.GetProperty("aiid").GetInt32().Should().Be(1);
        var inputs = parameters.GetProperty("in");
        inputs.GetArrayLength().Should().Be(2);
        inputs[0].GetProperty("piid").GetInt32().Should().Be(1);
        inputs[0].GetProperty("value").GetInt32().Should().Be(18);
        inputs[1].GetProperty("piid").GetInt32().Should().Be(10);
        using var payload = JsonDocument.Parse(inputs[1].GetProperty("value").GetString());
        var selections = payload.RootElement.GetProperty("selects");
        selections[0].EnumerateArray().Select(value => value.GetInt32()).Should().Equal(7, 2, 3, 3, 1);
        selections[1].EnumerateArray().Select(value => value.GetInt32()).Should().Equal(3, 1, 1, 2, 2);
        _miioTransport.Invocations.Should().HaveCount(2);
    }

    public static IEnumerable<object[]> InvalidRooms()
    {
        yield return [null];
        yield return [Array.Empty<RoomSelection>()];
        yield return [new RoomSelection[] { null }];
        yield return [new[] { new RoomSelection(0) }];
        yield return [new[] { new RoomSelection(-1) }];
        yield return [new[] { new RoomSelection(1, 0) }];
        yield return [new[] { new RoomSelection(1), new RoomSelection(1) }];
        yield return [new[] { new RoomSelection(1, Suction: (SuctionMode)4) }];
        yield return [new[] { new RoomSelection(1, Water: (WaterLevel)0) }];
    }

    [Theory]
    [MemberData(nameof(InvalidRooms))]
    public async Task Invalid_Room_Selections_Do_Not_Send_Requests(RoomSelection[] rooms)
    {
        Func<Task> clean = () => _miRobot.StartRoomCleaningAsync(rooms);

        await clean.Should().ThrowAsync<ArgumentException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Custom_Cleaning_Preserves_Firmware_Payload(bool isAsync)
    {
        SetupCallAction();
        const string data = "{ \"areas\": [[1,2,3,4]], \"future\": true }";

        await Call(isAsync, () => _miRobot.StartCustomCleaning(19, data), () => _miRobot.StartCustomCleaningAsync(19, data));

        var inputs = GetActionParameters().GetProperty("in");
        inputs[0].GetProperty("value").GetInt32().Should().Be(19);
        inputs[1].GetProperty("value").GetString().Should().Be(data);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    public async Task Invalid_Custom_Work_Mode_Does_Not_Send_Request(int mode)
    {
        Func<Task> clean = () => _miRobot.StartCustomCleaningAsync(mode, "{}");

        await clean.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task Custom_Cleaning_Requires_JSON_Object(string data)
    {
        Func<Task> clean = () => _miRobot.StartCustomCleaningAsync(18, data);

        await clean.Should().ThrowAsync<ArgumentException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public async Task Custom_Cleaning_Rejects_Malformed_JSON()
    {
        Func<Task> clean = () => _miRobot.StartCustomCleaningAsync(18, "bad");

        await clean.Should().ThrowAsync<JsonException>();
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_Schedule_Uses_Timer_Id_Input(bool isAsync)
    {
        SetupCallAction();

        await Call(isAsync, () => _miRobot.DeleteCleaningSchedule("2"), () => _miRobot.DeleteCleaningScheduleAsync("2"));

        var parameters = GetActionParameters();
        parameters.GetProperty("siid").GetInt32().Should().Be(8);
        parameters.GetProperty("aiid").GetInt32().Should().Be(1);
        var input = parameters.GetProperty("in")[0];
        input.GetProperty("piid").GetInt32().Should().Be(3);
        input.GetProperty("value").GetString().Should().Be("2");
    }

    [Fact]
    public async Task Rejected_Action_Propagates_Device_Error()
    {
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("\"action\""))))
            .ReturnsAsync("{\"result\":{\"code\":-1}}");

        Func<Task> start = () => _miRobot.StartDryingAsync();

        await start.Should().ThrowAsync<InvalidOperationException>().WithMessage("*-1*");
    }

    private JsonElement GetActionParameters()
    {
        var message = _miioTransport.Invocations
            .Where(invocation => invocation.Method.Name == "SendMessageAsync")
            .Select(invocation => JsonSerializer.Deserialize<JsonElement>((string)invocation.Arguments[0]))
            .Single(request => request.GetProperty("method").GetString() == "action");
        return message.GetProperty("params");
    }
}
