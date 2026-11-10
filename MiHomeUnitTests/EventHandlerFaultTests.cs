using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MiHomeLib.MqttGateway.Devices;
using Moq;
using MiHomeUnitTests.MultimodeGatewaySubDevicesTests;
using Xunit;
using Gw2 = MiHomeLib.XiaomiGateway2.Devices;

namespace MiHomeUnitTests;

/// <summary>
/// A user handler that throws must not escape the library. Before the fix such exceptions
/// went to the thread pool as unobserved async void failures and terminated the process
/// </summary>
public class EventHandlerFaultTests : MqttGatewayDeviceTests
{
    private readonly List<Exception> _escaped = [];

    // Async void continuations post to the current SynchronizationContext. Capturing them here makes
    // an escaped handler exception visible to the test instead of crashing the test host
    private void RunCapturingEscapes(Action action)
    {
        var previous = SynchronizationContext.Current;
        var context = new CapturingContext(_escaped);
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            action();
            context.Drain();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static Task Throwing() => Task.FromException(new InvalidOperationException("user handler failed"));

    private void ShouldHaveLoggedHandlerFailure() =>
        _loggerFactory.TakeErrors().Should().ContainSingle().Which.Message.Should().Be("user handler failed");

    [Fact]
    public void Ble_Device_Handler_Exception_Does_Not_Escape()
    {
        var th = _fixture.Build<XiaomiBluetoothHygrothermograph2>().Create();
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData(SetupBleAsyncEventParams(4100, "d400").ToString()));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(21.2f, "the value is updated before the handler runs");
    }

    [Fact]
    public void Lumi_ZigBee_Device_Handler_Exception_Does_Not_Escape()
    {
        var th = _fixture.Create<XiaomiTemperatureHumiditySensor>();
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData("[{\"res_name\":\"0.1.85\",\"value\":2515}]"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(25.15f);
    }

    [Fact]
    public void MiSpec_ZigBee_Device_Handler_Exception_Does_Not_Escape()
    {
        var sw = _fixture.Create<AqaraWirelessMiniSwitchCN2>();
        sw.OnClickAsync += _ => Throwing();

        RunCapturingEscapes(() => sw.ParseData("[{\"siid\":2,\"eiid\":1,\"arguments\":[]}]"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
    }

    [Fact]
    public void MiSpec_Ble_Device_Handler_Exception_Does_Not_Escape()
    {
        var th = _fixture.Build<XiaomiBluetoothHygrothermograph3>().Create();
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData("{\"siid\":3,\"piid\":1001,\"value\":21.5}"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(21.5f);
    }

    [Fact]
    public void Gateway2_Sub_Device_Handler_Exception_Does_Not_Escape()
    {
        var th = new Gw2.XiaomiTemperatureHumiditySensor("158d000156a94b", 1, _loggerFactory);
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData("{\"temperature\":\"2515\"}"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(25.15f);
    }

    [Fact]
    public void Timer_Handler_Exception_Does_Not_Escape()
    {
        var timer1 = new Mock<MiHomeLib.Contracts.ITimer>();
        var timer2 = new Mock<MiHomeLib.Contracts.ITimer>();
        timer1.Setup(x => x.Start()).Raises(x => x.Elapsed += null, null, null);

        var sensor = new XiaomiSmartHumanBodySensor("did", _loggerFactory, timer1.Object, timer2.Object);
        sensor.OnNoMotionDetectedAsync += _ => Throwing();

        RunCapturingEscapes(() => sensor.ParseData("[{\"res_name\":\"3.1.85\",\"value\":1}]"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        timer1.Verify(x => x.Stop(), Times.AtLeast(2), "the one-minute timer is still stopped after the handler failed");
    }

    [Fact]
    public void Handler_Is_Awaited_Before_ParseData_Returns()
    {
        // Existing behaviour that must be kept: a synchronous handler has finished when ParseData returns
        var th = _fixture.Build<XiaomiBluetoothHygrothermograph2>().Create();
        var calls = 0;
        th.OnTemperatureChangeAsync += _ => { calls++; return Task.CompletedTask; };
        th.OnTemperatureChangeAsync += _ => { calls++; return Task.CompletedTask; };

        th.ParseData(SetupBleAsyncEventParams(4100, "d400").ToString());

        calls.Should().Be(2);
    }

    private sealed class CapturingContext(List<Exception> escaped) : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback, object)> _work = new();

        public override void Post(SendOrPostCallback d, object state) => _work.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object state) => d(state);

        public void Drain()
        {
            while (_work.Count > 0)
            {
                var (callback, state) = _work.Dequeue();

                try { callback(state); }
                catch (Exception e) { escaped.Add(e); }
            }
        }
    }
}
