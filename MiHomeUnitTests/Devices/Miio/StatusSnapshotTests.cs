using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices;
using MiHomeUnitTests.Infrastructure;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Miio;

public class StatusSnapshotTests : MiioDeviceTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Humidifier_Status_Is_One_Request_And_Each_Call_Returns_A_New_Snapshot(bool isAsync)
    {
        using var device = new XiaomiAirHumidifierV1(_miioTransport.Object);
        SendResultMethod("get_prop", "on", "medium", "-125", "45", "1", "off", "on", "50");

        var first = await Call(isAsync, device.GetStatus, device.GetStatusAsync);

        first.Should().Be(new XiaomiAirHumidifierV1.DeviceStatus(true, XiaomiAirHumidifierV1.Mode.Medium,
            -12.5f, 45, XiaomiAirHumidifierV1.Brightness.Dim, false, true, 50));
        VerifyMethod("get_prop", "power", "mode", "temp_dec", "humidity", "led_b", "buzzer", "child_lock", "limit_hum");
        SendResultMethod("get_prop", "off", "high", "241", "55", "2", "on", "off", "60");

        var second = await Call(isAsync, device.GetStatus, device.GetStatusAsync);

        second.Should().Be(new XiaomiAirHumidifierV1.DeviceStatus(false, XiaomiAirHumidifierV1.Mode.High,
            24.1f, 55, XiaomiAirHumidifierV1.Brightness.Off, true, false, 60));
        first.Temperature.Should().Be(-12.5f);
        VerifyMethodWithId("get_prop", 2, "power", "mode", "temp_dec", "humidity", "led_b", "buzzer", "child_lock", "limit_hum");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Vacuum_Status_Preserves_Units_And_Does_Not_Cache_The_Result(bool isAsync)
    {
        using var device = new XiaomiRobotVacuumCleanerV1(_miioTransport.Object);
        SendResultMethod("get_status", new { state = 8, battery = 100, fan_power = 60, clean_time = 729, clean_area = 9795000 });

        var first = await Call(isAsync, device.GetStatus, device.GetStatusAsync);

        first.Should().Be(new XiaomiRobotVacuumCleanerV1.DeviceStatus(XiaomiRobotVacuumCleanerV1.Status.Charging,
            100, 60, 729, 9.795f));
        VerifyMethod("get_status", "");
        SendResultMethod("get_status", new { state = 5, battery = 90, fan_power = 70, clean_time = 800, clean_area = 10000000 });

        var second = await Call(isAsync, device.GetStatus, device.GetStatusAsync);

        second.Should().Be(new XiaomiRobotVacuumCleanerV1.DeviceStatus(XiaomiRobotVacuumCleanerV1.Status.Cleaning,
            90, 70, 800, 10f));
        first.BatteryPercent.Should().Be(100);
        VerifyMethodWithId("get_status", 2, "");
    }

    [Theory]
    [InlineData(0, "unknown", "power")]
    [InlineData(1, "unknown", "mode")]
    [InlineData(2, "bad", "temp_dec")]
    [InlineData(3, "bad", "humidity")]
    [InlineData(4, "bad", "led_b")]
    [InlineData(5, "unknown", "buzzer")]
    [InlineData(6, "unknown", "child_lock")]
    [InlineData(7, "bad", "limit_hum")]
    public async Task Malformed_Humidifier_Status_Identifies_The_Field(int index, string invalid, string fieldName)
    {
        using var device = new XiaomiAirHumidifierV1(_miioTransport.Object);
        object[] values = ["on", "medium", "250", "45", "1", "off", "on", "50"];
        values[index] = invalid;
        SendResultMethod("get_prop", values);
        Func<Task> read = () => device.GetStatusAsync();
        await read.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage($"*humidifier status*{fieldName}*");
    }
}
