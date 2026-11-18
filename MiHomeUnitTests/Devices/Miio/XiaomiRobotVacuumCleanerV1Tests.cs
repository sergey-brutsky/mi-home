using MiHomeUnitTests.Infrastructure;
using MiHomeLib.Helpers;
using System.Threading.Tasks;
using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using System.Threading;
using System.Globalization;
using MiHomeLib;

namespace MiHomeUnitTests.Devices.Miio;

public class XiaomiRobotVacuumCleanerV1Tests: MiioDeviceTestBase
{
    [Theory]
    [InlineData("{\"error\":{\"code\":-1}}")]
    [InlineData("{\"result\":[]}")]
    [InlineData("{\"result\":[null]}")]
    [InlineData("{\"result\":[{}]}")]
    [InlineData("{\"result\":[{\"state\":\"bad\"}]}")]
    [InlineData("not-json")]
    public void Invalid_Status_Response_Reports_The_Operation(string response)
    {
        _miioTransport.Setup(value => value.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response);
        System.Action describe = () => _miRobot.ToString();
        describe.Should().Throw<System.InvalidOperationException>().WithMessage("*status*");
    }

    [Theory]
    [InlineData("battery", "null")]
    [InlineData("fan_power", "\"bad\"")]
    [InlineData("clean_time", "{}")]
    [InlineData("clean_area", "2147483648")]
    [InlineData("state", "[]")]
    public void Invalid_Status_Value_Identifies_The_Field(string field, string invalidValue)
    {
        var status = System.Text.Json.Nodes.JsonNode.Parse("{\"state\":8,\"battery\":100,\"fan_power\":60,\"clean_time\":729,\"clean_area\":9795000}");
        status[field] = System.Text.Json.Nodes.JsonNode.Parse(invalidValue);
        var response = new System.Text.Json.Nodes.JsonObject { ["result"] = new System.Text.Json.Nodes.JsonArray(status) };
        _miioTransport.Setup(value => value.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response.ToJsonString());
        System.Action describe = () => _miRobot.ToString();

        describe.Should().Throw<System.InvalidOperationException>().WithMessage($"*status*{field}*");
    }

    [Fact]
    public void Status_Accepts_Numeric_Strings()
    {
        _miioTransport.Setup(value => value.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(
            "{\"result\":[{\"state\":\"8\",\"battery\":\"100\",\"fan_power\":\"60\",\"clean_time\":\"729\",\"clean_area\":\"9795000\"}]}");

        _miRobot.ToString().Should().Contain("State: Charging").And.Contain("Battery: 100 %");
    }

    private readonly XiaomiRobotVacuumCleanerV1 _miRobot;

    public XiaomiRobotVacuumCleanerV1Tests()
    {
        _miRobot = new XiaomiRobotVacuumCleanerV1(_miioTransport.Object);
    }

    [Fact]
    public void ToString_Returns_Valid_State()
    {
		Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

        // Arrange
        SendResultMethod("get_status", new
        {
            msg_ver = 8,
            msg_seq = 54,
            state = 8,
            battery = 100,
            clean_time = 729,
            clean_area = 9795000,
            error_code = 0,
            map_present = 1,
            in_cleaning = 0,
            fan_power = 60,
            dnd_enabled = 0
        });

        // // Act
        var str = _miRobot.ToString();

        // Assert
        VerifyMethod("get_status", string.Empty);

        str.Should().Be($"Model: rockrobo.vacuum.v1\nState: Charging\n" +
            $"Battery: 100 %\nFanspeed: 60 %\n" +
            $"Cleaning since: 729 seconds\n" +
            $"Cleaned area: 9.795 m²\n" +
            $"IP Address: ");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindMe_Sends_Command(bool isAsync)
    {
        SendResultMethod("find_me", "ok");

        await Call(isAsync, _miRobot.FindMe, _miRobot.FindMeAsync);

        VerifyMethod("find_me", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Home_Pauses_Then_Sends_To_Charging(bool isAsync)
    {
        // Arrange
        SendResultMethod("app_pause", "ok");
        var charge = new { result = new[] { "ok" }, id = 2 }.ToJson();
        var expectedCharge = new { id = 2, method = "app_charge", @params = new[] { string.Empty } }.ToJson();

        if (isAsync)
            _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("app_charge")))).Returns(Task.FromResult(charge));
        else
            _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("app_charge")))).ReturnsAsync(charge);

        // Act
        await Call(isAsync, _miRobot.GoHome, _miRobot.GoHomeAsync);

        // Assert
        VerifyMethod("app_pause", string.Empty);

        if (isAsync)
            _miioTransport.Verify(x => x.SendMessageAsync(expectedCharge), Times.Once());
        else
            _miioTransport.Verify(x => x.SendMessageAsync(expectedCharge), Times.Once());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_Sends_Command(bool isAsync)
    {
        SendResultMethod("app_start", "ok");

        await Call(isAsync, _miRobot.StartCleaning, _miRobot.StartCleaningAsync);

        VerifyMethod("app_start", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_Sends_Command(bool isAsync)
    {
        SendResultMethod("app_stop", "ok");

        await Call(isAsync, _miRobot.StopCleaning, _miRobot.StopCleaningAsync);

        VerifyMethod("app_stop", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pause_Sends_Command(bool isAsync)
    {
        SendResultMethod("app_pause", "ok");

        await Call(isAsync, _miRobot.Pause, _miRobot.PauseAsync);

        VerifyMethod("app_pause", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Spot_Sends_Command(bool isAsync)
    {
        SendResultMethod("app_spot", "ok");

        await Call(isAsync, _miRobot.StartSpotCleaning, _miRobot.StartSpotCleaningAsync);

        VerifyMethod("app_spot", string.Empty);
    }
}
