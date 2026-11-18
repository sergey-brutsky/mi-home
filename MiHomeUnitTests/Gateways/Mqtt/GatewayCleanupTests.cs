using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.MiioDevices;
using MiHomeLib.MqttGateway;
using MiHomeLib.XiaomiGateway2;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Gateways.Mqtt;

public class GatewayCleanupTests : MqttGatewayTestBase
{
    [Fact]
    public void Device_Cleanup_Attempts_Every_Device_And_Collects_Errors()
    {
        var devices = new GatewayDevices();
        var first = new ProbeDevice("first", new Exception("first failed"));
        var second = new ProbeDevice("second", new Exception("second failed"));
        var last = new ProbeDevice("last");
        devices.Add(first.DeviceId, first);
        devices.Add(second.DeviceId, second);
        devices.Add(last.DeviceId, last);

        Action dispose = devices.DisposeAll;

        dispose.Should().ThrowExactly<AggregateException>().Which.Flatten().InnerExceptions
            .Should().Equal(first.Failure, second.Failure);
        first.DisposeCalls.Should().Be(1);
        second.DisposeCalls.Should().Be(1);
        last.DisposeCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Gateway_Cleanup_Attempts_All_Devices_And_Both_Transports(bool gateway2)
    {
        var transportFailure = new Exception("transport failed");
        var miioFailure = new Exception("miio failed");
        var first = new ProbeDevice("first", new Exception("device failed"));
        var last = new ProbeDevice("last");
        var messages = new Mock<IGateway2Transport>();
        IDisposable gateway = gateway2
            ? new XiaomiGateway2(_miioTransport.Object, messages.Object, "sid", 0, _loggerFactory)
            : Gateway;
        var owner = gateway2 ? typeof(XiaomiGateway2) : typeof(MqttGatewayBase);
        var devices = (GatewayDevices)owner.GetField("_devices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gateway);
        devices.Add(first.DeviceId, first);
        devices.Add(last.DeviceId, last);
        _miioTransport.Setup(transport => transport.Dispose()).Throws(miioFailure);
        if (gateway2) messages.Setup(transport => transport.Dispose()).Throws(transportFailure);
        else _mqttTransport.Setup(transport => transport.Dispose()).Throws(transportFailure);

        Action dispose = gateway.Dispose;

        dispose.Should().ThrowExactly<AggregateException>().Which.Flatten().InnerExceptions
            .Should().BeEquivalentTo(new[] { first.Failure, transportFailure, miioFailure });
        first.DisposeCalls.Should().Be(1);
        last.DisposeCalls.Should().Be(1);
        _miioTransport.Verify(transport => transport.Dispose(), Times.Once);
        if (gateway2) messages.Verify(transport => transport.Dispose(), Times.Once);
        else _mqttTransport.Verify(transport => transport.Dispose(), Times.Once);
        dispose.Should().NotThrow();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initialization_Failure_Is_Preserved_When_Transport_Cleanup_Fails(bool mqttGateway)
    {
        var failure = new TimeoutException("initialization failed");
        var miioFailure = new Exception("miio cleanup failed");
        var mqttFailure = new Exception("mqtt cleanup failed");
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.IsAny<string>())).Throws(failure);
        _miioTransport.Setup(transport => transport.Dispose()).Throws(miioFailure);
        _mqttTransport.Setup(transport => transport.Dispose()).Throws(mqttFailure);
        Action create = () =>
        {
            if (mqttGateway)
                _ = new MultimodeGateway("failed", _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object, _loggerFactory);
            else
                _ = new MiotGenericDevice("failed", _miioTransport.Object, 0);
        };

        var error = create.Should().ThrowExactly<TimeoutException>().Which;

        error.Should().BeSameAs(failure);
        _miioTransport.Verify(transport => transport.Dispose(), Times.Once);
        if (mqttGateway)
        {
            _mqttTransport.Verify(transport => transport.Dispose(), Times.Once);
            ((AggregateException)error.Data["ResourceCleanupException"]).Flatten().InnerExceptions
                .Should().BeEquivalentTo(new[] { miioFailure, mqttFailure });
        }
        else error.Data["ResourceCleanupException"].Should().BeSameAs(miioFailure);
    }

    private sealed class ProbeDevice(string id, Exception failure = null) : GatewaySubDeviceBase(NullLoggerFactory.Instance)
    {
        public override string DeviceId => id;
        public Exception Failure => failure;
        public int DisposeCalls { get; private set; }
        internal override void ParseData(string command) { }
        public override void Dispose()
        {
            if (!BeginDispose()) return;
            DisposeCalls++;
            if (failure is not null) throw failure;
        }
    }
}
