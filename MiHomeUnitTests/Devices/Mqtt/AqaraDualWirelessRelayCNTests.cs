using MiHomeLib.Protocols;
using AutoFixture;
using Xunit;
using FluentAssertions;
using Moq;
using System.Threading.Tasks;
using MiHomeLib.Devices;
using MiHomeLib;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraDualWirelessRelayCNTests: MqttDeviceTestBase
{
    [Theory]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(2, 0, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    [InlineData(2, 0, true)]
    [InlineData(2, 1, true)]
    public async Task Confirmed_Commands_Target_Their_Channel(int channel, int value, bool synchronous)
    {
        var commands = _mqttTransport.As<MiHomeLib.MqttGateway.IMqttCommandTransport>();
        commands.Setup(x => x.SendCommandAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        using var relay = CreateRelay("confirmed-relay");
        var state = (AqaraDualWirelessRelayCN.ChannelState)value;
        if (synchronous)
        {
            if (channel == 1) relay.SetChannel1StateConfirmed(state);
            else relay.SetChannel2StateConfirmed(state);
        }
        else if (channel == 1) await relay.SetChannel1StateConfirmedAsync(state);
        else await relay.SetChannel2StateConfirmedAsync(state);
        var expected = $"{{\"cmd\":\"write\",\"did\":\"confirmed-relay\",\"params\":[{{\"res_name\":\"4.{channel}.85\",\"value\":{value}}}]}}";
        commands.Verify(x => x.SendCommandAsync(expected), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Confirmed_Command_Waits_And_Propagates_Failure(int channel)
    {
        var commands = _mqttTransport.As<MiHomeLib.MqttGateway.IMqttCommandTransport>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.Setup(x => x.SendCommandAsync(It.IsAny<string>())).Returns(completion.Task);
        using var relay = CreateRelay("confirmed-relay");
        var pending = channel == 1
            ? relay.SetChannel1StateConfirmedAsync(AqaraDualWirelessRelayCN.ChannelState.On)
            : relay.SetChannel2StateConfirmedAsync(AqaraDualWirelessRelayCN.ChannelState.On);
        pending.IsCompleted.Should().BeFalse();
        completion.SetException(new System.TimeoutException("outcome unknown"));
        System.Func<Task> finish = () => pending;
        await finish.Should().ThrowAsync<System.TimeoutException>().WithMessage("outcome unknown");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Confirmed_Command_Rejects_Invalid_State_And_Unsupported_Transport(int channel)
    {
        using var relay = CreateRelay("confirmed-relay");
        void Set(AqaraDualWirelessRelayCN.ChannelState state)
        {
            if (channel == 1) relay.SetChannel1StateConfirmed(state);
            else relay.SetChannel2StateConfirmed(state);
        }
        System.Action invalid = () => Set(AqaraDualWirelessRelayCN.ChannelState.Unknown);
        invalid.Should().Throw<System.ArgumentOutOfRangeException>();
        System.Action unsupported = () => Set(AqaraDualWirelessRelayCN.ChannelState.On);
        unsupported.Should().Throw<System.NotSupportedException>();
    }

    private AqaraDualWirelessRelayCN CreateRelay(string id)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, AqaraDualWirelessRelayCN.LUMI_MQTT_MODEL,
            id, 0, _loggerFactory, out var device).Should().BeTrue();
        var relay = (AqaraDualWirelessRelayCN)device;
        ((IProtocolMappedManageableDevice)relay).SetMqttTransport(_mqttTransport.Object);
        return relay;
    }

    [Fact]
    public void Both_Channel_Off_Commands_Target_Their_Channel()
    {
        var relay = CreateRelay("off-relay");
        relay.Channel1PowerOff();
        relay.Channel2PowerOff();
        _mqttTransport.Verify(x => x.SendMessage("{\"cmd\":\"write\",\"did\":\"off-relay\",\"params\":[{\"res_name\":\"4.1.85\",\"value\":0}]}"), Times.Once);
        _mqttTransport.Verify(x => x.SendMessage("{\"cmd\":\"write\",\"did\":\"off-relay\",\"params\":[{\"res_name\":\"4.2.85\",\"value\":0}]}"), Times.Once);
    }

    [Fact]
    public void Discovery_Initializes_Properties_Without_Events()
    {
        var relay = CreateRelay("relay");
        relay.GetInitialPropertyNames().Should().Equal("channel_0", "channel_1", "load_voltage",
            "load_power", "enable_motor_mode", "lqi", "chip_temperature");
        var events = 0;
        relay.OnChannel1StateChangeAsync += () => { events++; return Task.CompletedTask; };
        relay.OnVoltageChangeAsync += _ => { events++; return Task.CompletedTask; };
        var values = System.Text.Json.Nodes.JsonNode.Parse("[\"on\",\"off\",230000,42.5,1,120,35]").AsArray();
        relay.SetInitialProperties(System.Linq.Enumerable.ToArray(values));
        relay.DeviceId.Should().Be("relay");
        relay.Channel1.Should().Be(AqaraDualWirelessRelayCN.ChannelState.On);
        relay.Channel2.Should().Be(AqaraDualWirelessRelayCN.ChannelState.Off);
        relay.Voltage.Should().Be(230);
        relay.LoadPower.Should().Be(42.5f);
        relay.Interlock.Should().Be(AqaraDualWirelessRelayCN.InterlockState.Enabled);
        relay.LinkQuality.Should().Be(120);
        relay.ChipTemperature.Should().Be(35);
        events.Should().Be(0);
    }

    [Theory]
    [InlineData("8.0.2007")]
    [InlineData("8.0.2006")]
    public void Diagnostics_Report_Previous_Value_Even_When_Unchanged(string resource)
    {
        var relay = CreateRelay("relay");
        var previous = new System.Collections.Generic.List<byte>();
        if (resource == "8.0.2007")
            relay.OnLinkQualityChangeAsync += value => { previous.Add(value); return Task.CompletedTask; };
        else
            relay.OnChipTemperatureChangeAsync += value => { previous.Add(value); return Task.CompletedTask; };
        var report = System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = resource, value = 35 } });
        relay.ParseData(report);
        relay.ParseData(report);
        previous.Should().Equal(0, 35);
    }

    [Theory, InlineData("[{\"res_name\":\"4.1.85\",\"value\":1}]")]
    public void Check_OnChannel1StateChange_Event(string data)
    {
        // Arrange
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "4.1.85", value = (int)(AqaraDualWirelessRelayCN.ChannelState.Off) } }));

        var eventRaised = false;
        var counter = 0;

        relay.OnChannel1StateChangeAsync += () =>
        {
            counter++;
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);
        relay.ParseData(data); // this is intentionally

        // Assert
        eventRaised.Should().BeTrue();
        counter.Should().Be(1);
    }

    [Theory, InlineData("[{\"res_name\":\"4.2.85\",\"value\":1}]")]
    public void Check_OnChannel2StateChange_Event(string data)
    {
        // Arrange
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "4.2.85", value = (int)(AqaraDualWirelessRelayCN.ChannelState.Off) } }));

        var eventRaised = false;
        var counter = 0;

        relay.OnChannel2StateChangeAsync += () =>
        {
            counter++;
            eventRaised = true;
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);
        relay.ParseData(data); // this is intentionally

        // Assert
        eventRaised.Should().BeTrue();
        counter.Should().Be(1);
    }

    [Theory, InlineData("[{\"res_name\":\"0.11.85\",\"value\":212512}]")]
    public void Check_OnVoltageChange_Event(string data)
    {
        // Arrange
        var oldValue = _fixture.Create<float>();
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "0.11.85", value = (oldValue) * 1000f } }));

        var eventRaised = false;

        relay.OnVoltageChangeAsync += (x) =>
        {
            eventRaised = x == oldValue;
            relay.Voltage.Should().Be(GetMiSpecValue<int>(data)/1000f);
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"0.12.85\",\"value\":42.2}]")]
    public void Check_OnLoadPowerChange_Event(string data)
    {
        // Arrange
        var oldValue = _fixture.Create<float>();
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "0.12.85", value = oldValue } }));

        var eventRaised = false;

        relay.OnLoadPowerChangeAsync += (x) =>
        {
            eventRaised = x == oldValue;
            relay.LoadPower.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"0.13.85\",\"value\":3163.13}]")]
    public void Check_OnEnergyChange_Event(string data)
    {
        // Arrange
        var oldValue = _fixture.Create<float>();
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "0.13.85", value = oldValue } }));

        var eventRaised = false;

        relay.OnEnergyChangeAsync += (x) =>
        {
            eventRaised = x == oldValue;
            relay.Energy.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"0.14.85\",\"value\":153.24}]")]
    public void Check_OnCurrentChange_Event(string data)
    {
        // Arrange
        var oldValue = _fixture.Create<float>();
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "0.14.85", value = oldValue } }));

        var eventRaised = false;

        relay.OnCurrentChangeAsync += (x) =>
        {
            eventRaised = x == oldValue;
            relay.Current.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_Channel1PowerOn_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}";
        var relay = CreateRelay(did);

        // Act
        relay.Channel1PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void Check_Channel2PowerOn_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.2.85\",\"value\":1}}]}}";
        var relay = CreateRelay(did);

        // Act
        relay.Channel2PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.Off, 1)]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.On, 0)]
    public void Check_Channel1ToggleState_Works(AqaraDualWirelessRelayCN.ChannelState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":{value}}}]}}";
        var relay = CreateRelay(did);
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "4.1.85", value = (int)(state) } }));

        // Act
        relay.Channel1ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.Off, 1)]
    [InlineData(AqaraDualWirelessRelayCN.ChannelState.On, 0)]
    public void Check_Channel2ToggleState_Works(AqaraDualWirelessRelayCN.ChannelState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.2.85\",\"value\":{value}}}]}}";
        var relay = CreateRelay(did);
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { res_name = "4.2.85", value = (int)(state) } }));

        // Act
        relay.Channel2ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.PowerMemoryState.PowerOff, 0)]
    [InlineData(AqaraDualWirelessRelayCN.PowerMemoryState.Previous, 1)]
    public void Check_SetPowerMemoryState_Works(AqaraDualWirelessRelayCN.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"8.0.2030\",\"value\":{value}}}]}}";
        var relay = CreateRelay(did);

        // Act
        relay.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraDualWirelessRelayCN.InterlockState.Disabled, 0)]
    [InlineData(AqaraDualWirelessRelayCN.InterlockState.Enabled, 1)]
    public void Check_SetInterlock_Works(AqaraDualWirelessRelayCN.InterlockState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.9.85\",\"value\":{value}}}]}}";
        var relay = CreateRelay(did);

        // Act
        relay.SetInterlock(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(1, AqaraDualWirelessRelayCN.InterlockState.Enabled)]
    [InlineData(0, AqaraDualWirelessRelayCN.InterlockState.Disabled)]
    [InlineData(7, AqaraDualWirelessRelayCN.InterlockState.Unknown)]
    public void Interlock_Report_Updates_State(int value, AqaraDualWirelessRelayCN.InterlockState expected)
    {
        var relay = CreateRelay(_fixture.Create<string>());

        relay.ParseData($"[{{\"res_name\":\"4.9.85\",\"value\":{value}}}]");

        relay.Interlock.Should().Be(expected);
    }
    [Fact]
    public async Task Check_Channel1PowerOn_Works_Async()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"params\":[{{\"res_name\":\"4.1.85\",\"value\":1}}]}}";
        var relay = CreateRelay(did);

        // Act
        await relay.Channel1PowerOnAsync();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
