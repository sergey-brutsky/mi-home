using AutoFixture;
using FluentAssertions;
using MiHomeLib.MqttGateway.Devices;
using System.Threading.Tasks;
using Xunit;
using static MiHomeLib.MqttGateway.Devices.AqaraWirelessMiniSwitchCN;

namespace MiHomeUnitTests.MultimodeGatewaySubDevicesTests;

public class AqaraWirelessMiniSwitchCNTests : MqttGatewayDeviceTests
{
    [Theory]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":1}]", ClickArg.SingleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":2}]", ClickArg.DoubleClick)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":16}]", ClickArg.LongPressHold)]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":17}]", ClickArg.LongPressRelease)]
    public void Check_Switch_OnClick_Event(string data, ClickArg evt)
    {
        // Arrange
        var sw = _fixture.Create<AqaraWirelessMiniSwitchCN>();
        var eventRaised = false;

        sw.OnClickAsync += (clickArgs) =>
        {
            eventRaised = clickArgs == evt;
            return Task.CompletedTask;
        };

        // Act
        sw.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_ToString()
    {
        var sw = _fixture.Create<AqaraWirelessMiniSwitchCN>();

        var result = sw.ToString();

        result.Should().Contain(MARKET_MODEL);
        result.Should().Contain(MODEL);
    }
}
