using FluentAssertions;
using MiHomeLib.Devices;
using MiHomeUnitTests.Infrastructure;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class BleBatteryReportsTests : ProtocolDeviceTestBase
{
    [Theory]
    [InlineData("5f", "63", 95, 99)]
    [InlineData("29", "62", 41, 98)]
    public void Battery_Reports_Pass_Previous_Value_After_Updating_Property_Even_When_Repeated(
        string oldData, string edata, byte oldBatteryPercent, byte expectedBatteryPercent)
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph2>(XiaomiBluetoothHygrothermograph2.PDID);
        sensor.ParseData(SetupBleAsyncEventParams(4106, oldData).ToString());
        var changes = new ValueChangeRecorder<byte>(() => sensor.BatteryPercent);
        sensor.OnBatteryPercentChangeAsync += changes.Handle;

        var report = SetupBleAsyncEventParams(4106, edata).ToString();
        sensor.ParseData(report);
        sensor.ParseData(report);

        changes.Changes.Should().Equal(
            (oldBatteryPercent, expectedBatteryPercent),
            (expectedBatteryPercent, expectedBatteryPercent));
        sensor.BatteryPercent.Should().Be(expectedBatteryPercent);
    }
}
