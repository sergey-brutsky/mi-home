using MiHomeUnitTests.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class DiagnosticReportsTests : ProtocolDeviceTestBase
{
    public static IEnumerable<object[]> Cases()
    {
        var diagnostics = new[] { "Voltage", "BatteryPercent", "LinkQuality", "ChipTemperature" };
        foreach (var row in SupportedDevicesTests.Expected.Where(row => row.Protocol == DeviceProtocol.LumiMqtt))
            foreach (var name in diagnostics)
                if (row.Type.GetProperty(name) is not null && row.Type.GetEvent("On" + name + "ChangeAsync") is not null)
                    yield return new object[] { row.Model, name };
    }

    public static IEnumerable<object[]> Models() => SupportedDevicesTests.Expected
        .Where(row => row.Protocol == DeviceProtocol.LumiMqtt).Select(row => new object[] { row.Model });

    [Theory]
    [MemberData(nameof(Models))]
    public void Description_Identifies_Model_And_Device(string model)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model, "diagnostic-device", 42, _loggerFactory, out var device).Should().BeTrue();
        using var lifetime = device;
        ((ProtocolMappedDeviceBase)device).Model.Should().Be(model);
        device.ToString().Should().Contain("diagnostic-device").And.Contain(model);
        var shortId = device.GetType().GetProperty("ShortId");
        shortId.Should().NotBeNull();
        shortId.GetValue(device).Should().Be(42);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Diagnostic_Report_Updates_Property_And_Passes_Previous_Value_Even_When_Repeated(string model, string name)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model, "test", 0, _loggerFactory, out var device).Should().BeTrue();
        using var lifetime = device;
        var resource = name == "Voltage" && model == AqaraDualWirelessRelayCN.LUMI_MQTT_MODEL ? "0.11.85" : name switch { "Voltage" => "8.0.2008", "BatteryPercent" => "8.0.2001", "LinkQuality" => "8.0.2007", _ => "8.0.2006" };
        var firstRaw = name == "Voltage" ? 3015 : 70;
        var secondRaw = name == "Voltage" ? 2980 : 65;
        var first = name == "Voltage" ? firstRaw / 1000f : firstRaw;
        var second = name == "Voltage" ? secondRaw / 1000f : secondRaw;
        string Report(int value) => model == AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL
            ? $"[{{\"siid\":3,\"piid\":2,\"value\":{value}}}]"
            : $"[{{\"res_name\":\"{resource}\",\"value\":{value}}}]";
        device.ParseData(Report(firstRaw));
        var property = device.GetType().GetProperty(name);
        var oldValues = new List<float>();
        Task OnValue(float old)
        {
            oldValues.Add(old);
            Convert.ToSingle(property.GetValue(device)).Should().BeApproximately(second, 0.00001f);
            return Task.CompletedTask;
        }
        var evt = device.GetType().GetEvent("On" + name + "ChangeAsync");
        Delegate handler = name == "Voltage" ? new Func<float, Task>(OnValue) : new Func<byte, Task>(old => OnValue(old));
        evt.AddEventHandler(device, handler);
        device.ParseData(Report(secondRaw));
        device.ParseData(Report(secondRaw));
        oldValues.Should().HaveCount(2);
        oldValues[0].Should().BeApproximately(first, 0.00001f);
        oldValues[1].Should().BeApproximately(second, 0.00001f);
        Convert.ToSingle(property.GetValue(device)).Should().BeApproximately(second, 0.00001f);
        ((ProtocolMappedDeviceBase)device).Model.Should().Be(model);
        device.ToString().Should().Contain(model);
        _loggerFactory.Entries.Should().NotContain(x => x.Level >= Microsoft.Extensions.Logging.LogLevel.Warning);
    }
}
