using FluentAssertions;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeHardwareTests;

[Collection("Hardware")]
public sealed class DeviceReadTests(HardwareFixture fixture)
{
#pragma warning disable CS0618 // Also verify clients using the preserved Opple aliases.
    [SkippableFact]
    public void AqaraCubeSensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraCubeSensor>();
        Skip.If(device is null, "No AqaraCubeSensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraDualWallSwitch_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraDualWallSwitch>();
        Skip.If(device is null, "No AqaraDualWallSwitch found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraDualWallWirelessSwitch_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraDualWallWirelessSwitch>();
        Skip.If(device is null, "No AqaraDualWallWirelessSwitch found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraDualWirelessRelayCN_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraDualWirelessRelayCN>();
        Skip.If(device is null, "No AqaraDualWirelessRelayCN found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraOppleFourButtonsWirelesSwitch_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraOppleFourButtonsWirelesSwitch>();
        Skip.If(device is null, "No AqaraOppleFourButtonsWirelesSwitch found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraOppleFourButtonsWirelessSwitch_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraOppleFourButtonsWirelessSwitch>();
        Skip.If(device is null, "No AqaraOppleFourButtonsWirelessSwitch found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraOppleTwoButtonsWirelesSwitch_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraOppleTwoButtonsWirelesSwitch>();
        Skip.If(device is null, "No AqaraOppleTwoButtonsWirelesSwitch found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraOppleTwoButtonsWirelessSwitch_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraOppleTwoButtonsWirelessSwitch>();
        Skip.If(device is null, "No AqaraOppleTwoButtonsWirelessSwitch found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraSmartHumanBodySensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraSmartHumanBodySensor>();
        Skip.If(device is null, "No AqaraSmartHumanBodySensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraT1WithNeutral_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraT1WithNeutral>();
        Skip.If(device is null, "No AqaraT1WithNeutral found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraTemperatureHumiditySensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraTemperatureHumiditySensor>();
        Skip.If(device is null, "No AqaraTemperatureHumiditySensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraVibrationSensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraVibrationSensor>();
        Skip.If(device is null, "No AqaraVibrationSensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraWaterLeakSensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraWaterLeakSensor>();
        Skip.If(device is null, "No AqaraWaterLeakSensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraWindowDoorSensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraWindowDoorSensor>();
        Skip.If(device is null, "No AqaraWindowDoorSensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraWirelessMiniSwitchCN_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraWirelessMiniSwitchCN>();
        Skip.If(device is null, "No AqaraWirelessMiniSwitchCN found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void AqaraWirelessMiniSwitchCN2_Reports_Plausible_Values()
    {
        var device = fixture.Find<AqaraWirelessMiniSwitchCN2>();
        Skip.If(device is null, "No AqaraWirelessMiniSwitchCN2 found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void PtxWirelessSwitchBle_Reports_Plausible_Values()
    {
        var device = fixture.Find<PtxWirelessSwitchBle>();
        Skip.If(device is null, "No PtxWirelessSwitchBle found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiBluetoothHygrothermograph2_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiBluetoothHygrothermograph2>();
        Skip.If(device is null, "No XiaomiBluetoothHygrothermograph2 found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiBluetoothHygrothermograph3_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiBluetoothHygrothermograph3>();
        Skip.If(device is null, "No XiaomiBluetoothHygrothermograph3 found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiDoorWindowSensor2_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiDoorWindowSensor2>();
        Skip.If(device is null, "No XiaomiDoorWindowSensor2 found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiHoneywellSmokeDetector_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiHoneywellSmokeDetector>();
        Skip.If(device is null, "No XiaomiHoneywellSmokeDetector found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiMeshWallSwitchSingleOpen_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiMeshWallSwitchSingleOpen>();
        Skip.If(device is null, "No XiaomiMeshWallSwitchSingleOpen found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiMiSmartPowerPlugCN_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiMiSmartPowerPlugCN>();
        Skip.If(device is null, "No XiaomiMiSmartPowerPlugCN found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiSmartHumanBodySensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiSmartHumanBodySensor>();
        Skip.If(device is null, "No XiaomiSmartHumanBodySensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiSmartHumanBodySensor2_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiSmartHumanBodySensor2>();
        Skip.If(device is null, "No XiaomiSmartHumanBodySensor2 found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiSmartWirelessSwitch_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiSmartWirelessSwitch>();
        Skip.If(device is null, "No XiaomiSmartWirelessSwitch found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiSmokeSensorGuard_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiSmokeSensorGuard>();
        Skip.If(device is null, "No XiaomiSmokeSensorGuard found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiTemperatureHumiditySensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiTemperatureHumiditySensor>();
        Skip.If(device is null, "No XiaomiTemperatureHumiditySensor found on the configured gateways");
        Check(device!);
    }

    [SkippableFact]
    public void XiaomiWindowDoorSensor_Reports_Plausible_Values()
    {
        var device = fixture.Find<XiaomiWindowDoorSensor>();
        Skip.If(device is null, "No XiaomiWindowDoorSensor found on the configured gateways");
        Check(device!);
    }

#pragma warning restore CS0618
    private static void Check(GatewaySubDeviceBase device)
    {
        device.DeviceId.Should().NotBeNullOrEmpty();
        var description = device.ToString()!;
        description.Should().Contain(device.DeviceId);
        // Public numeric properties can retain zero before a BLE report. Unknown display values
        // are deliberately not interpreted as real measurements, and no initial report is required.
        foreach (var (name, label, min, max) in new[]
        {
            ("Voltage", "Voltage", 2.0, 3.5), ("BatteryPercent", "Battery Percent", 0.0, 100.0),
            ("Temperature", "Temperature", -40.0, 85.0), ("Humidity", "Humidity", 0.0, 100.0)
        })
        {
            var property = device.GetType().GetProperty(name);
            if (property is null || description.Contains(label + ": Unknown", StringComparison.OrdinalIgnoreCase)) continue;
            var value = Convert.ToDouble(property.GetValue(device), System.Globalization.CultureInfo.InvariantCulture);
            if (name == "Voltage")
            {
                // The plug's voltage field is not a battery reading.
                if (device is XiaomiMiSmartPowerPlugCN || value == 0) continue;
            }
            value.Should().BeInRange(min, max, "reported {0} must be plausible", name);
        }
    }
}
