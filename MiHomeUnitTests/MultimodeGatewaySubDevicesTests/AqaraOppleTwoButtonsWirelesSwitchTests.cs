using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using Xunit;
using FluentAssertions;
using static MiHomeLib.MqttGateway.Devices.AqaraOppleWirelesSwitch;
using System.Threading.Tasks;
using MiHomeLib.MqttGateway.Devices;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

/// <summary>
/// Covers both Opple switches: the four-button model inherits buttons 1 and 2 and adds 3 and 4
/// </summary>
public class AqaraOppleTwoButtonsWirelesSwitchTests: MqttGatewayDeviceTests
{
    private static readonly Dictionary<int, Action<AqaraOppleFourButtonsWirelesSwitch, Func<ClickArg, Task>>> Subscribe = new()
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

    public static IEnumerable<object[]> ButtonClicks =>
        from button in Subscribe.Keys
        from click in Clicks
        select new object[] { button, click.value, click.arg };

    [Theory]
    [MemberData(nameof(ButtonClicks))]
    public void Button_Click_Raises_Event_With_ClickArg(int button, int value, ClickArg expected)
    {
        // Arrange
        var sw = _fixture.Create<AqaraOppleFourButtonsWirelesSwitch>();
        ClickArg? actual = null;
        Subscribe[button](sw, clickArgs => { actual = clickArgs; return Task.CompletedTask; });

        // Act
        sw.ParseData($"[{{\"res_name\":\"13.{button}.85\",\"value\":{value}}}]");

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Two_Buttons_Switch_Raises_Its_Buttons(int button)
    {
        var sw = _fixture.Create<AqaraOppleTwoButtonsWirelesSwitch>();
        ClickArg? actual = null;

        if (button == 1) sw.OnButton1ClickAsync += a => { actual = a; return Task.CompletedTask; };
        else sw.OnButton2ClickAsync += a => { actual = a; return Task.CompletedTask; };

        sw.ParseData($"[{{\"res_name\":\"13.{button}.85\",\"value\":1}}]");

        actual.Should().Be(ClickArg.SingleClick);
    }
}
