using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Helpers;
using Moq;
using Xunit;
using static MiHomeLib.MiioDevices.XiaomiRobotVacuumX20Plus;

namespace MiHomeUnitTests.Devices.Miio;

public partial class XiaomiRobotVacuumX20PlusTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Status_Reads_All_Fields_In_One_Request(bool isAsync)
    {
        SetupGetProperties((2, 1, 13), (2, 2, 0), (3, 1, 100), (3, 2, 1), (4, 1, 14),
            (4, 7, 0), (4, 2, 28), (4, 3, 12), (4, 25, 0), (2, 5, 120));

        var result = await Call(isAsync, _miRobot.GetStatus, _miRobot.GetStatusAsync);

        result.Should().Be(new DeviceStatus(VacuumState.ChargingCompleted, 0, 100,
            ChargingState.Charging, 14, 0, 28, 12, 0, 120));
        VerifyGetProperties(2, (2, 1), (2, 2), (3, 1), (3, 2), (4, 1), (4, 7), (4, 2), (4, 3), (4, 25), (2, 5));
    }

    [Fact]
    public async Task Status_Preserves_Unknown_Codes_And_Unsigned_Counters()
    {
        SetupGetProperties((2, 1, 255), (2, 2, 255), (3, 1, 0), (3, 2, 255), (4, 1, uint.MaxValue),
            (4, 7, uint.MaxValue), (4, 2, uint.MaxValue), (4, 3, uint.MaxValue), (4, 25, 255), (2, 5, ushort.MaxValue));

        var result = await _miRobot.GetStatusAsync();

        ((int)result.State).Should().Be(255);
        ((int)result.Charging).Should().Be(255);
        result.WorkMode.Should().Be(uint.MaxValue);
        result.CleaningArea.Should().Be(uint.MaxValue);
        result.DryingMinutesLeft.Should().Be(ushort.MaxValue);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("256")]
    [InlineData("-1")]
    public async Task Status_Reports_Malformed_Values_With_Context(string state)
    {
        SetupGetProperties((2, 1, state), (2, 2, 0), (3, 1, 100), (3, 2, 1), (4, 1, 14),
            (4, 7, 0), (4, 2, 28), (4, 3, 12), (4, 25, 0), (2, 5, 0));

        Func<Task> read = () => _miRobot.GetStatusAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*X20+ status*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consumables_Map_Filter_And_Mop_Hours_And_Percent_Correctly(bool isAsync)
    {
        SetupGetProperties((9, 1, 238), (9, 2, 79), (10, 1, 138), (10, 2, 69),
            (11, 1, 58), (11, 2, 88), (18, 1, 80), (18, 2, 64));

        var result = await Call(isAsync, _miRobot.GetConsumables, _miRobot.GetConsumablesAsync);

        result.Should().Be(new ConsumablesInfo(new Consumable(238, 79), new Consumable(138, 69),
            new Consumable(88, 58), new Consumable(64, 80)));
        VerifyGetProperties(2, (9, 1), (9, 2), (10, 1), (10, 2), (11, 2), (11, 1), (18, 2), (18, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Statistics_Preserve_Raw_Unsigned_Counters(bool isAsync)
    {
        SetupGetProperties((12, 1, uint.MaxValue), (12, 2, 3195), (12, 3, 133), (12, 4, 2041));

        var result = await Call(isAsync, _miRobot.GetCleaningStatistics, _miRobot.GetCleaningStatisticsAsync);

        result.Should().Be(new CleaningStatistics(uint.MaxValue, 3195, 133, 2041));
        VerifyGetProperties(2, (12, 1), (12, 2), (12, 3), (12, 4));
    }

    [Fact]
    public async Task Consumables_Reject_Out_Of_Range_Lifetime_With_Context()
    {
        SetupGetProperties((9, 1, -1), (9, 2, 79), (10, 1, 138), (10, 2, 69),
            (11, 1, 58), (11, 2, 88), (18, 1, 80), (18, 2, 64));
        Func<Task> read = () => _miRobot.GetConsumablesAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*X20+ consumables*");
    }

    [Fact]
    public async Task Statistics_Reject_Out_Of_Range_Counter_With_Context()
    {
        SetupGetProperties((12, 1, 4294967296L), (12, 2, 3195), (12, 3, 133), (12, 4, 2041));
        Func<Task> read = () => _miRobot.GetCleaningStatisticsAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*X20+ cleaning statistics*");
    }

    [Fact]
    public async Task Settings_Reject_Unknown_Boolean_Switch_With_Context()
    {
        SetupGetProperties((2, 3, 3), (4, 5, 3), (4, 11, 2), (4, 12, 0), (4, 27, 1),
            (7, 1, 100), (5, 1, true), (5, 2, "22:30"), (5, 3, "08:00"), (8, 1, "Asia/Shanghai"));
        Func<Task> read = () => _miRobot.GetSettingsAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*X20+ settings*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Settings_Read_Typed_Values_And_Device_Time_Zone(bool isAsync)
    {
        SetupGetProperties((2, 3, 3), (4, 5, 3), (4, 11, 1), (4, 12, 0), (4, 27, 1),
            (7, 1, 100), (5, 1, true), (5, 2, "22:30"), (5, 3, "08:00"), (8, 1, "Asia/Shanghai"));

        var result = await Call(isAsync, _miRobot.GetSettings, _miRobot.GetSettingsAsync);

        result.Should().Be(new DeviceSettings(SuctionMode.Max, WaterLevel.High, true, false, true, 100,
            new DndSettings(true, new TimeSpan(22, 30, 0), new TimeSpan(8, 0, 0)), "Asia/Shanghai"));
        VerifyGetProperties(2, (2, 3), (4, 5), (4, 11), (4, 12), (4, 27), (7, 1), (5, 1), (5, 2), (5, 3), (8, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dnd_Reads_Device_Local_Times(bool isAsync)
    {
        SetupGetProperties((5, 1, false), (5, 2, "00:00"), (5, 3, "23:59"));

        var result = await Call(isAsync, _miRobot.GetDndSettings, _miRobot.GetDndSettingsAsync);

        result.Should().Be(new DndSettings(false, TimeSpan.Zero, new TimeSpan(23, 59, 0)));
        VerifyGetProperties(2, (5, 1), (5, 2), (5, 3));
    }

    [Theory]
    [InlineData("24:00")]
    [InlineData("8:00")]
    [InlineData("invalid")]
    public async Task Dnd_Reports_Malformed_Time_With_Context(string time)
    {
        SetupGetProperties((5, 1, true), (5, 2, time), (5, 3, "08:00"));

        Func<Task> read = () => _miRobot.GetDndSettingsAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DND settings*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Schedules_Preserve_All_Fields_And_Entries(bool isAsync)
    {
        const string data = "1-0-20:38-0000000-0-2-3-32-2;2-2-08:30-1111100-1-2-1-2-3,4-extra";
        SetupGetProperties((8, 2, data), (8, 1, "Asia/Shanghai"));

        var result = await Call(isAsync, _miRobot.GetCleaningSchedule, _miRobot.GetCleaningScheduleAsync);

        result.Should().Be(new CleaningSchedule(data, "Asia/Shanghai"));
        VerifyGetProperties(2, (8, 2), (8, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Diagnostics_Batch_Requests_And_Preserve_Firmware_Value_Types(bool isAsync)
    {
        SetupDiagnosticResponses();

        var result = await Call(isAsync, _miRobot.GetDiagnostics, _miRobot.GetDiagnosticsAsync);

        result.Should().HaveCount(38).And.OnlyContain(property => property.IsAvailable);
        result.Select(property => (property.ServiceId, property.PropertyId)).Should().OnlyHaveUniqueItems();
        result.Single(property => property.PropertyId == 48).Value.Should().Be("[]");
        result.Single(property => property.PropertyId == 37).Value.Should().Be("2");
        var requests = GetPropertyRequests();
        requests.Select(request => request.GetProperty("params").GetArrayLength()).Should().Equal(10, 10, 10, 8);
    }

    [Fact]
    public async Task Diagnostics_Retain_Successes_When_One_Property_Is_Unsupported()
    {
        SetupDiagnosticResponses(rejectProperty: 21);

        var result = await _miRobot.GetDiagnosticsAsync();

        result.Should().HaveCount(38);
        result.Count(property => property.IsAvailable).Should().Be(37);
        var unsupported = result.Single(property => !property.IsAvailable);
        unsupported.PropertyId.Should().Be(21);
        unsupported.Error.Should().Contain("-1");
        unsupported.Value.Should().BeNull();
        GetPropertyRequests().Should().HaveCount(14);
    }

    [Fact]
    public async Task Diagnostics_Propagate_Timeout_Without_Retrying_Every_Property()
    {
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("get_properties"))))
            .ThrowsAsync(new TimeoutException("offline"));

        Func<Task> read = () => _miRobot.GetDiagnosticsAsync();

        await read.Should().ThrowAsync<TimeoutException>();
        GetPropertyRequests().Should().HaveCount(1);
    }

    private void SetupDiagnosticResponses(int? rejectProperty = null)
    {
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("get_properties"))))
            .ReturnsAsync((string message) =>
            {
                using var request = JsonDocument.Parse(message);
                var properties = request.RootElement.GetProperty("params").EnumerateArray().Select(property => new
                {
                    did = property.GetProperty("did").GetString(),
                    code = property.GetProperty("piid").GetInt32() == rejectProperty ? -1 : 0,
                    value = property.GetProperty("piid").GetInt32() == 48 ? (object)"[]" : 2
                }).ToArray();
                return new { result = properties }.ToJson();
            });
    }

    private JsonElement[] GetPropertyRequests() => _miioTransport.Invocations
        .Where(invocation => invocation.Method.Name == "SendMessageAsync")
        .Select(invocation => JsonSerializer.Deserialize<JsonElement>((string)invocation.Arguments[0]))
        .Where(request => request.GetProperty("method").GetString() == "get_properties")
        .ToArray();
}
