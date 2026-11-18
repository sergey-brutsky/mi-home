using MiHomeLib.Protocols;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class XiaomiMiSmartPowerPlugCNTests : Gateway2DeviceTestBase
{
    private readonly string _sid = "158d00015dc332";
    private readonly Mock<MiHomeLib.XiaomiGateway2.IGateway2Writer> _writer = new();
    private readonly int _shortId = 10624;
    private readonly XiaomiMiSmartPowerPlugCN _socketPlug;

    public XiaomiMiSmartPowerPlugCNTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2, XiaomiMiSmartPowerPlugCN.XIAOMI_GATEWAY2_MODEL, _sid, _shortId, _loggerFactory, out var device));
        _socketPlug = Assert.IsType<XiaomiMiSmartPowerPlugCN>(device);
        ((IProtocolMappedGateway2ManageableDevice)_socketPlug).SetGateway2Writer(_writer.Object);
    }

    private void Parse(string cmd, Dictionary<string, object> data) =>
        _socketPlug.ParseData(Gateway2Message.Parse(
            CreateCommand(cmd, XiaomiMiSmartPowerPlugCN.XIAOMI_GATEWAY2_MODEL, _sid, _shortId, data)).Data);

    [Theory]
    [InlineData("on")]
    [InlineData("off")]
    public void Unknown_Read_Status_Preserves_State_While_Other_Values_Update(string initial)
    {
        Parse("report", new() { { "status", initial } });
        var state = _socketPlug.State;
        var changes = 0;
        _socketPlug.OnStateChangeAsync += () => { changes++; return Task.CompletedTask; };
        Parse("read_ack", new() { { "status", "unknown" }, { "load_power", "1.25" } });
        _socketPlug.State.Should().Be(state);
        _socketPlug.LoadPower.Should().Be(1.25f);
        changes.Should().Be(0);
    }

    [Fact]
    public void Check_SocketPlug_Hearbeat_Data()
    {
        Parse("heartbeat", new()
        {
            { "voltage", 3600 },
            { "status", "on" },
            { "inuse", "1" },
            { "power_consumed", "39009" },
            { "load_power", "3.20" },
        });

        _socketPlug.DeviceId.Should().Be(_sid);
        _socketPlug.Voltage.Should().Be(3.6f);
        _socketPlug.State.Should().Be(XiaomiMiSmartPowerPlugCN.PlugState.On);
        _socketPlug.Inuse.Should().Be(1);
        _socketPlug.PowerConsumed.Should().Be(39009);
        _socketPlug.LoadPower.Should().Be(3.2f);
    }

    [Fact]
    public void Status_Raises_OnStateChange_Only_When_State_Changes()
    {
        var raised = 0;
        _socketPlug.OnStateChangeAsync += () => { raised++; return Task.CompletedTask; };

        Parse("report", new() { { "status", "on" } });
        Parse("report", new() { { "status", "on" } });
        Parse("report", new() { { "status", "off" } });

        raised.Should().Be(2);
        _socketPlug.State.Should().Be(XiaomiMiSmartPowerPlugCN.PlugState.Off);
    }

    [Fact]
    public void Load_Power_Passes_Old_Value()
    {
        Parse("report", new() { { "load_power", "3.20" } });
        float? oldValue = null;
        _socketPlug.OnLoadPowerChangeAsync += x => { oldValue = x; return Task.CompletedTask; };

        Parse("report", new() { { "load_power", "82.59" } });

        _socketPlug.LoadPower.Should().Be(82.59f);
        oldValue.Should().Be(3.2f);
    }

    [Theory]
    [InlineData(true, "on")]
    [InlineData(false, "off")]
    public void PowerOn_And_PowerOff_Send_Gateway2_Write_Command(bool on, string status)
    {
        if (on) _socketPlug.PowerOn(); else _socketPlug.PowerOff();

        _writer.Verify(x => x.WriteAsync(_sid, XiaomiMiSmartPowerPlugCN.XIAOMI_GATEWAY2_MODEL,
            It.Is<(string Key, string Value)[]>(d => d.Length == 1 && d[0].Key == "status" && d[0].Value == status)), Times.Once());
    }

    [Fact]
    public void ToggleState_Turns_On_A_Plug_That_Is_Off()
    {
        Parse("report", new() { { "status", "off" } });

        _socketPlug.ToggleState();

        _writer.Verify(x => x.WriteAsync(_sid, "plug",
            It.Is<(string Key, string Value)[]>(d => d.Length == 1 && d[0].Key == "status" && d[0].Value == "on")), Times.Once());
    }

    [Fact]
    public void Mqtt_Only_Commands_Are_Not_Supported_On_Gateway2()
    {
        _socketPlug.Invoking(x => x.SetLedState(XiaomiMiSmartPowerPlugCN.LedState.AlwaysOn))
            .Should().Throw<NotSupportedException>().WithMessage("*Lumi MQTT gateways*");
    }
    [Theory]
    [InlineData(true, "on")]
    [InlineData(false, "off")]
    public async Task PowerOn_And_PowerOff_Send_Gateway2_Write_Command_Async(bool on, string status)
    {
        if (on) await _socketPlug.PowerOnAsync(); else await _socketPlug.PowerOffAsync();

        _writer.Verify(x => x.WriteAsync(_sid, XiaomiMiSmartPowerPlugCN.XIAOMI_GATEWAY2_MODEL,
            It.Is<(string Key, string Value)[]>(d => d.Length == 1 && d[0].Key == "status" && d[0].Value == status)), Times.Once());
    }
}
