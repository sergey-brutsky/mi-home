using MiHomeUnitTests.Infrastructure;
using System;
using System.Reflection;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Xunit;
using Moq;

namespace MiHomeUnitTests.Helpers;

public class MiotFailureTests : MiioDeviceTestBase
{
    public MiotFailureTests() => SetupMiioInfo();

    [Theory]
    [InlineData(typeof(MijiaSmartSocket2China), MijiaSmartSocket2China.MARKET_MODEL, MijiaSmartSocket2China.MODEL)]
    [InlineData(typeof(XiaomiSmartPlug2Euro), XiaomiSmartPlug2Euro.MARKET_MODEL, XiaomiSmartPlug2Euro.MODEL)]
    [InlineData(typeof(XiaomiSmartAirPurifier4Lite), XiaomiSmartAirPurifier4Lite.MARKET_MODEL, XiaomiSmartAirPurifier4Lite.MODEL)]
    [InlineData(typeof(XiaomiRobotVacuumMop3C), XiaomiRobotVacuumMop3C.MARKET_MODEL, XiaomiRobotVacuumMop3C.MODEL)]
    [InlineData(typeof(XiaomiRobotVacuumX20Plus), XiaomiRobotVacuumX20Plus.MARKET_MODEL, XiaomiRobotVacuumX20Plus.MODEL)]
    public void Device_Descriptions_Use_Common_Fields_From_Initialization(Type deviceType, string marketModel, string model)
    {
        var constructor = deviceType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(IMiioTransport), typeof(int)], null);
        using var device = (MiotGenericDevice)constructor.Invoke([_miioTransport.Object, 0]);

        device.ToString().Should().Be($"Model: {marketModel} {model}," +
            " Uptime: 502175 seconds, Miio Version: 0.0.9, Mac: 34:EF:44:49:BC:63," +
            " Firmware Version: 1.0.5_0008, Hardware: Linux, SSID: ssid1, BSSID: bssid1," +
            " RSSI: -35, Freq: 2437, Ip: 192.168.1.100, Mask: 255.255.255.0, Gateway: 192.168.1.1");
        _miioTransport.Verify(transport => transport.SendMessageAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Failed_Info_Disposes_Transport_Of_Ordinary_Device()
    {
        _miioTransport.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Throws(new TimeoutException());
        Action create = () => new Probe(_miioTransport.Object);
        create.Should().Throw<TimeoutException>();
        _miioTransport.Verify(x => x.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async System.Threading.Tasks.Task Info_Accepts_Objects_And_Encoded_Objects(bool encoded)
    {
        var info = System.Text.Json.Nodes.JsonNode.Parse(await _miioTransport.Object.SendMessageAsync("{\"method\":\"miIO.info\"}"));
        if (encoded)
        {
            info["result"]["ap"] = info["result"]["ap"].ToJsonString();
            info["result"]["netif"] = info["result"]["netif"].ToJsonString();
        }
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("miIO.info")))).ReturnsAsync(info.ToJsonString());
        var device = new Probe(_miioTransport.Object);
        device.Network.Ip.Should().Be("192.168.1.100");
        device.Wifi.Rssi.Should().Be(-35);
        device.Wifi.Freq.Should().Be(2437);
        device.UptimeSeconds.Should().Be(502175);
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":-4003,\"message\":\"unsupported\"}}")]
    [InlineData("{\"result\":[]}")]
    [InlineData("{\"result\":[{}]}")]
    [InlineData("{\"result\":[{\"code\":\"bad\"}]}")]
    [InlineData("not-json")]
    public void Invalid_Write_Response_Is_Contextual_And_Is_Not_Retried(string response)
    {
        var device = new Probe(_miioTransport.Object);
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("set_properties")))).ReturnsAsync(response);
        Action write = device.Write;
        write.Should().Throw<InvalidOperationException>().WithMessage("*2/1*");
        _miioTransport.Verify(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("set_properties"))), Times.Once);
    }

    [Theory]
    [InlineData("{\"result\":[]}")]
    [InlineData("{\"result\":{}}")]
    [InlineData("{\"result\":{\"code\":\"bad\"}}")]
    [InlineData("{\"error\":{\"code\":-4003}}")]
    public void Invalid_Action_Response_Is_Contextual_And_Is_Not_Retried(string response)
    {
        var device = new Probe(_miioTransport.Object);
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("action")))).ReturnsAsync(response);
        Action act = device.Act;
        act.Should().Throw<InvalidOperationException>().WithMessage("*siid: 2, aiid: 3*");
        _miioTransport.Verify(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("action"))), Times.Once);
    }

    [Fact]
    public void Failed_Property_Write_Reports_Operation_Failure()
    {
        SetupSetProperties();
        SendResultMethod("set_properties", new { code = -4003 });
        var device = new Probe(_miioTransport.Object);
        Action write = device.Write;
        write.Should().Throw<InvalidOperationException>().WithMessage("*2/1*");
    }

    [Fact]
    public void Failed_Action_Reports_Address()
    {
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("action")))).ReturnsAsync("{\"result\":{\"code\":-4003}}");
        var device = new Probe(_miioTransport.Object);
        Action action = device.Act;
        action.Should().Throw<InvalidOperationException>().WithMessage("*siid: 2, aiid: 3*");
    }

    private sealed class Probe(IMiioTransport transport) : MiotGenericDevice("test", transport, 0)
    {
        public void Write() => SetMiotProperty(2, 1, true);
        public void Act() => CallMiotAction(2, 3);
    }
}
