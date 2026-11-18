using MiHomeLib.Protocols;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiMiSmartPowerPlugCNTests : MqttDeviceTestBase
{
    [Fact]
    public async Task Confirmed_Power_Waits_For_Transport_Acknowledgement()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = _mqttTransport.As<MiHomeLib.MqttGateway.IMqttCommandTransport>();
        var plug = CreatePlug();
        commands.Setup(x => x.SendCommandAsync(It.Is<string>(message =>
            message.Contains(plug.DeviceId) && message.Contains("4.1.85"))))
            .Returns(completion.Task);
        var pending = plug.SetStateConfirmedAsync(XiaomiMiSmartPowerPlugCN.PlugState.On);
        pending.IsCompleted.Should().BeFalse();
        completion.SetResult(true);
        await pending;
        commands.Verify(x => x.SendCommandAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Confirmed_Power_Requires_Transport_Support_And_Valid_State()
    {
        var plug = CreatePlug();
        System.Action unsupported = () => plug.SetStateConfirmed(XiaomiMiSmartPowerPlugCN.PlugState.On);
        unsupported.Should().Throw<System.NotSupportedException>();
        System.Action invalid = () => plug.SetStateConfirmed((XiaomiMiSmartPowerPlugCN.PlugState)99);
        invalid.Should().Throw<System.ArgumentOutOfRangeException>();
    }

    private XiaomiMiSmartPowerPlugCN CreatePlug()
    {
        var plug = CreateLumiProtocolDevice<XiaomiMiSmartPowerPlugCN>(XiaomiMiSmartPowerPlugCN.LUMI_MQTT_MODEL);
        ((IProtocolMappedManageableDevice)plug).SetMqttTransport(_mqttTransport.Object);
        return plug;
    }

    private string WriteCommand(string did, string resource, int value) =>
        $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"{resource}\",\"value\":{value}}}]}}";

    [Fact]
    public void Initial_Properties_Are_Read_In_Order()
    {
        var plug = CreatePlug();
        plug.GetInitialPropertyNames().Should().Equal("neutral_0", "load_power", "max_power", "lqi", "chip_temperature");

        plug.SetInitialProperties([JsonValue.Create("on"), JsonValue.Create(12.5f), JsonValue.Create(1800), JsonValue.Create(132), JsonValue.Create(35)]);

        plug.State.Should().Be(XiaomiMiSmartPowerPlugCN.PlugState.On);
        plug.LoadPower.Should().Be(12.5f);
        plug.MaxPower.Should().Be(1800);
        plug.LinkQuality.Should().Be(132);
        plug.ChipTemperature.Should().Be(35);
    }

    [Fact]
    public void Check_OnStateChange_Event_Only_On_Real_Change()
    {
        var plug = CreatePlug();
        var raised = 0;
        plug.OnStateChangeAsync += () => { raised++; return Task.CompletedTask; };

        plug.ParseData("[{\"res_name\":\"4.1.85\",\"value\":1}]");
        plug.ParseData("[{\"res_name\":\"4.1.85\",\"value\":1}]");

        raised.Should().Be(1);
        plug.State.Should().Be(XiaomiMiSmartPowerPlugCN.PlugState.On);
    }

    [Fact]
    public void Check_OnLoadPowerChange_Event_Passes_Old_Value()
    {
        var plug = CreatePlug();
        plug.ParseData("[{\"res_name\":\"0.12.85\",\"value\":10}]");
        float? oldValue = null;
        plug.OnLoadPowerChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        plug.ParseData("[{\"res_name\":\"0.12.85\",\"value\":42.2}]");

        plug.LoadPower.Should().Be(42.2f);
        oldValue.Should().Be(10f);
    }

    [Fact]
    public void Check_PowerOn_And_PowerOff_Work()
    {
        var plug = CreatePlug();

        plug.PowerOn();
        plug.PowerOff();

        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "4.1.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "4.1.85", 0)), Times.Once);
    }

    [Theory]
    [InlineData(XiaomiMiSmartPowerPlugCN.PlugState.Off, 1)]
    [InlineData(XiaomiMiSmartPowerPlugCN.PlugState.On, 0)]
    public void Check_Toggle_Works(XiaomiMiSmartPowerPlugCN.PlugState state, int value)
    {
        var plug = CreatePlug();
        plug.ParseData($"[{{\"res_name\":\"4.1.85\",\"value\":{(int)state}}}]");

        plug.ToggleState();

        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "4.1.85", value)), Times.Once);
    }

    [Fact]
    public void Check_Settings_Commands_Work()
    {
        var plug = CreatePlug();

        plug.SetPowerMemoryState(XiaomiMiSmartPowerPlugCN.PowerMemoryState.Previous);
        plug.SetChargeProtection(XiaomiMiSmartPowerPlugCN.ChargeProtect.On);
        plug.SetLedState(XiaomiMiSmartPowerPlugCN.LedState.TurnOffAtNightTime);

        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "8.0.2030", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "8.0.2031", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "8.0.2032", 0)), Times.Once);
    }
    [Fact]
    public async Task Check_PowerOn_And_PowerOff_Work_Async()
    {
        var plug = CreatePlug();

        await plug.PowerOnAsync();
        await plug.PowerOffAsync();

        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "4.1.85", 1)), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(WriteCommand(plug.DeviceId, "4.1.85", 0)), Times.Once);
    }
}
