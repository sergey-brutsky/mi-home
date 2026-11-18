using MiHomeLib.Protocols;
using AutoFixture;
using Xunit;
using FluentAssertions;
using Moq;
using System;
using System.Threading.Tasks;
using MiHomeLib.Devices;
using MiHomeLib;

namespace MiHomeUnitTests.Devices.Mqtt;
public class AqaraT1WithNeutralTests: MqttDeviceTestBase
{
    private AqaraT1WithNeutral CreateRelay(string id)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, AqaraT1WithNeutral.LUMI_MQTT_MODEL,
            id, 0, _loggerFactory, out var device).Should().BeTrue();
        var relay = (AqaraT1WithNeutral)device;
        ((IProtocolMappedManageableDevice)relay).SetMqttTransport(_mqttTransport.Object);
        return relay;
    }

    [Theory, InlineData("[{\"siid\":2,\"piid\":1,\"value\":true}]")]
    public void Check_OnChannelStateChange_Event(string data)
    {
        // Arrange
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { siid = 2, piid = 1, value = (AqaraT1WithNeutral.RelayState.Off) == AqaraT1WithNeutral.RelayState.On } }));

        var eventRaised = false;
        var counter = 0;

        relay.OnStateChangeAsync += () =>
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

    [Theory, InlineData("[{\"siid\":3,\"piid\":2,\"value\":42.20}]")]
    public void Check_OnLoadPowerChange_Event(string data)
    {
        // Arrange
        var oldValue = _fixture.Create<float>();

        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { siid = 3, piid = 2, value = oldValue } }));

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

    [Theory, InlineData("[{\"siid\":3,\"piid\":1,\"value\":1.30}]")]
    public void Check_OnPowerConsumptionChange_Event(string data)
    {
        // Arrange
        var oldValue = _fixture.Create<float>();
        var relay = CreateRelay(_fixture.Create<string>());
        relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { siid = 3, piid = 1, value = oldValue } }));

        var eventRaised = false;

        relay.OnPowerConsumptionChangeAsync += (x) =>
        {
            eventRaised = x == oldValue;
            relay.PowerConsumption.Should().Be(GetMiSpecValue<float>(data));
            return Task.CompletedTask;
        };

        // Act
        relay.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void Check_PowerOn_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":1}}]}}";
        var relay = CreateRelay(did);

        // Act
        relay.PowerOn();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void Check_PowerOff_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":0}}]}}";
        var relay = CreateRelay(did);

        // Act
        relay.PowerOff();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
    [Theory]
    [InlineData(AqaraT1WithNeutral.RelayState.Off, 1)]
    [InlineData(AqaraT1WithNeutral.RelayState.On, 0)]
    public void Check_Toggle_Works(AqaraT1WithNeutral.RelayState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":{value}}}]}}";
        var plug = CreateRelay(did);
        plug.ParseData(System.Text.Json.JsonSerializer.Serialize(new[] { new { siid = 2, piid = 1, value = (state) == AqaraT1WithNeutral.RelayState.On } }));

        // Act
        plug.ToggleState();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraT1WithNeutral.PowerMemoryState.PowerOff, 0)]
    [InlineData(AqaraT1WithNeutral.PowerMemoryState.Previous, 1)]
    public void Check_SetPowerMemoryState_Works(AqaraT1WithNeutral.PowerMemoryState state, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":5,\"piid\":1,\"value\":{value}}}]}}";
        var plug = CreateRelay(did);

        // Act
        plug.SetPowerMemoryState(state);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Theory]
    [InlineData(AqaraT1WithNeutral.PowerMode.Momentary, 1)]
    [InlineData(AqaraT1WithNeutral.PowerMode.Toggle, 2)]
    public void Check_SetPowerMode_Works(AqaraT1WithNeutral.PowerMode mode, int value)
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":7,\"piid\":2,\"value\":{value}}}]}}";
        var plug = CreateRelay(did);

        // Act
        plug.SetPowerMode(mode);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void SetPowerOverloadThreshold_When_ValidArgument_Works()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var value = 100;
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":5,\"piid\":6,\"value\":{value}}}]}}";
        var plug = CreateRelay(did);

        // Act
        plug.SetPowerOverloadThreshold(value);

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }

    [Fact]
    public void SetPowerOverloadThreshold_When_InvalidArgument_ThrowsException()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var value = 5000; // invalid argument
        var plug = CreateRelay(did);

        // Act
        Action code = () => { plug.SetPowerOverloadThreshold(value); };

        // Assert
        code
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>()
            .WithParameterName("threshold")
            .Which.ActualValue.Should().Be(value);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Numeric_Reports_Pass_Previous_Values_Even_When_Unchanged(int piid)
    {
        var relay = CreateRelay("relay");
        var previous = new System.Collections.Generic.List<float>();
        Func<float, Task> handler = old => { previous.Add(old); return Task.CompletedTask; };
        if (piid == 1) relay.OnPowerConsumptionChangeAsync += handler;
        else relay.OnLoadPowerChangeAsync += handler;
        var data = System.Text.Json.JsonSerializer.Serialize(new[] { new { siid = 3, piid, value = 42.5 } });
        relay.ParseData(data);
        relay.ParseData(data);
        previous.Should().Equal(0, 42.5f);
    }

    [Fact]
    public void Unknown_Properties_Do_Not_Prevent_State_Updates()
    {
        var relay = CreateRelay("relay");
        relay.State.Should().Be(AqaraT1WithNeutral.RelayState.Unknown);
        relay.GetInitialPropertyNames().Should().BeEmpty();
        var count = 0;
        relay.OnStateChangeAsync += () => { count++; return Task.CompletedTask; };
        foreach (var state in new[] { false, false, true, true, false })
            relay.ParseData(System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new { siid = 99, piid = 1, value = state },
                new { siid = 2, piid = 1, value = state }
            }));
        count.Should().Be(3);
        relay.State.Should().Be(AqaraT1WithNeutral.RelayState.Off);
    }

    [Fact]
    public void Toggle_Unknown_State_Does_Not_Send_Command()
    {
        CreateRelay("relay").ToggleState();
        _mqttTransport.Verify(x => x.SendMessage(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2200)]
    public void Overload_Threshold_Boundaries_Are_Accepted(int threshold)
    {
        CreateRelay("relay").SetPowerOverloadThreshold(threshold);
        _mqttTransport.Verify(x => x.SendMessage(It.Is<string>(s => s.Contains("\"value\":" + threshold))), Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2201)]
    public void Invalid_Threshold_Does_Not_Send_Command(int threshold)
    {
        var relay = CreateRelay("relay");
        Action act = () => relay.SetPowerOverloadThreshold(threshold);
        act.Should().Throw<ArgumentOutOfRangeException>();
        _mqttTransport.Verify(x => x.SendMessage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Commands_Require_Connected_Transport()
    {
        var relay = new AqaraT1WithNeutral("relay", 0, _loggerFactory);
        Action act = relay.PowerOn;
        act.Should().Throw<NotSupportedException>();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failing_Handler_Does_Not_Stop_The_Remaining_Properties(bool faultedTask)
    {
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();
        var factory = new Mock<Microsoft.Extensions.Logging.ILoggerFactory>();
        factory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, AqaraT1WithNeutral.LUMI_MQTT_MODEL,
            "relay", 0, factory.Object, out var device).Should().BeTrue();
        var relay = (AqaraT1WithNeutral)device;
        relay.OnStateChangeAsync += () => faultedTask
            ? Task.FromException(new InvalidOperationException("handler failure"))
            : throw new InvalidOperationException("handler failure");
        Action parse = () => relay.ParseData("[{\"siid\":2,\"piid\":1,\"value\":true},{\"siid\":3,\"piid\":2,\"value\":42.5}]");
        parse.Should().NotThrow();
        relay.State.Should().Be(AqaraT1WithNeutral.RelayState.On);
        relay.LoadPower.Should().Be(42.5f);
        logger.Verify(x => x.Log(Microsoft.Extensions.Logging.LogLevel.Error, It.IsAny<Microsoft.Extensions.Logging.EventId>(),
            It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }
    [Fact]
    public async Task Check_PowerOn_Works_Async()
    {
        // Arrange
        var did = _fixture.Create<string>();
        var cmd = $"{{\"cmd\":\"write\",\"did\":\"{did}\",\"mi_spec\":[{{\"siid\":2,\"piid\":1,\"value\":1}}]}}";
        var relay = CreateRelay(did);

        // Act
        await relay.PowerOnAsync();

        // Assert
        _mqttTransport.Verify(x => x.SendMessage(cmd), Times.Once);
    }
}
