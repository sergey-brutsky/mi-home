using MiHomeLib.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using MiHomeLib;
using Xunit;
using FluentAssertions;
using static MiHomeLib.Devices.AqaraOppleWirelesSwitch;
using System.Threading.Tasks;
using MiHomeLib.Devices;

namespace MiHomeUnitTests.Devices.Mqtt;

/// <summary>
/// Covers both Opple switches: both models share diagnostic properties and click semantics
/// </summary>
public class AqaraOppleSwitchTests: ProtocolDeviceTestBase
{
    private AqaraOppleWirelesSwitch Create(string model)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model, "opple", 0, _loggerFactory, out var device)
            .Should().BeTrue();
        return (AqaraOppleWirelesSwitch)device;
    }

    [Theory]
    [InlineData(AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    [InlineData(AqaraOppleFourButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    public void Initial_Properties_And_Reports_Use_Common_Diagnostics(string model)
    {
        var sw = Create(model);
        sw.GetInitialPropertyNames().Should().Equal("voltage", "battery", "lqi", "chip_temperature");
        var batteries = new List<byte>();
        sw.OnBatteryPercentChangeAsync += old => { batteries.Add(old); return Task.CompletedTask; };
        sw.SetInitialProperties(System.Text.Json.Nodes.JsonNode.Parse("[3000,90,120,25]").AsArray().ToArray());
        sw.Voltage.Should().Be(3);
        sw.BatteryPercent.Should().Be(90);
        sw.LinkQuality.Should().Be(120);
        sw.ChipTemperature.Should().Be(25);
        batteries.Should().BeEmpty();
        var report = "[{\"res_name\":\"8.0.2001\",\"value\":80}]";
        sw.ParseData(report);
        sw.ParseData(report);
        batteries.Should().Equal(90, 80);
        sw.BatteryPercent.Should().Be(80);
        sw.ToString().Should().Contain(model).And.Contain("opple").And.Contain("LinkQuality");
    }

    [Theory]
    [InlineData(AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    [InlineData(AqaraOppleFourButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    public void Unknown_Click_Is_Ignored_And_Valid_Reports_Are_Not_Deduplicated(string model)
    {
        var sw = Create(model);
        var count = 0;
        Func<ClickArg, Task> handler = _ => { count++; return Task.CompletedTask; };
        if (sw is AqaraOppleTwoButtonsWirelessSwitch two) two.OnButton1ClickAsync += handler;
        else ((AqaraOppleFourButtonsWirelessSwitch)sw).OnButton1ClickAsync += handler;
        foreach (var value in new[] { 99, 1, 1 })
            sw.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "13.1.85", value } }));
        count.Should().Be(2);
    }

    private static readonly Dictionary<int, Action<AqaraOppleFourButtonsWirelessSwitch, Func<ClickArg, Task>>> Subscribe = new()
    {
        [1] = (sw, h) => sw.OnButton1ClickAsync += h,
        [2] = (sw, h) => sw.OnButton2ClickAsync += h,
        [3] = (sw, h) => sw.OnButton3ClickAsync += h,
        [4] = (sw, h) => sw.OnButton4ClickAsync += h,
    };

    private static readonly (int value, ClickArg arg)[] Clicks =
    [
        (1, ClickArg.SingleClick),
        (2, ClickArg.DoubleClick),
        (3, ClickArg.TripleClick),
        (16, ClickArg.LongPressHold),
        (17, ClickArg.LongPressRelease),
    ];

    private static void SubscribeButton(AqaraOppleWirelesSwitch sw, int button, Func<ClickArg, Task> handler)
    {
        if (sw is AqaraOppleFourButtonsWirelessSwitch four) Subscribe[button](four, handler);
        else if (button == 1) ((AqaraOppleTwoButtonsWirelessSwitch)sw).OnButton1ClickAsync += handler;
        else ((AqaraOppleTwoButtonsWirelessSwitch)sw).OnButton2ClickAsync += handler;
    }

    public static IEnumerable<object[]> AllButtonClicks =>
        from model in new[] { AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL, AqaraOppleFourButtonsWirelessSwitch.LUMI_MQTT_MODEL }
        from button in Enumerable.Range(1, model == AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL ? 2 : 4)
        from click in Clicks
        select new object[] { model, button, click.value, click.arg };

    [Theory]
    [MemberData(nameof(AllButtonClicks))]
    public void Every_Click_Reaches_Only_Its_Button(string model, int button, int value, ClickArg expected)
    {
        var sw = Create(model);
        var received = new List<(int, ClickArg)>();
        var buttons = sw is AqaraOppleTwoButtonsWirelessSwitch ? 2 : 4;
        for (var n = 1; n <= buttons; n++)
        {
            var subscribed = n;
            SubscribeButton(sw, n, click => { received.Add((subscribed, click)); return Task.CompletedTask; });
        }
        foreach (var code in new[] { -1, 0, 99, value, value })
            sw.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = $"13.{button}.85", value = code } }));
        received.Should().Equal((button, expected), (button, expected));
    }

    [Theory]
    [InlineData(AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    [InlineData(AqaraOppleFourButtonsWirelessSwitch.LUMI_MQTT_MODEL)]
    public void All_Diagnostics_Update_Before_Events_And_Report_Previous_Values(string model)
    {
        var sw = Create(model);
        var voltages = new List<float>();
        var batteries = new List<byte>();
        var qualities = new List<byte>();
        var temperatures = new List<byte>();
        sw.OnVoltageChangeAsync += old => { sw.Voltage.Should().Be(2.85f); voltages.Add(old); return Task.CompletedTask; };
        sw.OnBatteryPercentChangeAsync += old => { sw.BatteryPercent.Should().Be(80); batteries.Add(old); return Task.CompletedTask; };
        sw.OnLinkQualityChangeAsync += old => { sw.LinkQuality.Should().Be(100); qualities.Add(old); return Task.CompletedTask; };
        sw.OnChipTemperatureChangeAsync += old => { sw.ChipTemperature.Should().Be(30); temperatures.Add(old); return Task.CompletedTask; };
        sw.SetInitialProperties(System.Text.Json.Nodes.JsonNode.Parse("[3000,90,120,25]").AsArray().ToArray());
        voltages.Should().BeEmpty();
        batteries.Should().BeEmpty();
        qualities.Should().BeEmpty();
        temperatures.Should().BeEmpty();
        var report = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { res_name = "8.0.2008", value = 2850 },
            new { res_name = "8.0.2001", value = 80 },
            new { res_name = "8.0.2007", value = 100 },
            new { res_name = "8.0.2006", value = 30 }
        });
        sw.ParseData(report);
        sw.ParseData(report);
        voltages.Should().Equal(3f, 2.85f);
        batteries.Should().Equal(90, 80);
        qualities.Should().Equal(120, 100);
        temperatures.Should().Equal(25, 30);
    }

    [Fact]
    public void Two_Button_Model_Ignores_Absent_Buttons_And_Continues_Parsing()
    {
        var sw = Create(AqaraOppleTwoButtonsWirelessSwitch.LUMI_MQTT_MODEL);
        var clicks = new List<ClickArg>();
        SubscribeButton(sw, 1, click => { clicks.Add(click); return Task.CompletedTask; });
        sw.ParseData(System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { res_name = "13.3.85", value = 1 },
            new { res_name = "13.4.85", value = 1 },
            new { res_name = "13.1.85", value = 2 }
        }));
        clicks.Should().Equal(ClickArg.DoubleClick);
    }
}
