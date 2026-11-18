using MiHomeUnitTests.Devices.Mqtt;
using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Helpers;

/// <summary>
/// A user handler that throws must not escape the library. Before the fix such exceptions
/// went to the thread pool as unobserved async void failures and terminated the process
/// </summary>
public class EventHandlerFaultTests : MqttDeviceTestBase
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
        var th = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph2>(XiaomiBluetoothHygrothermograph2.PDID);
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData(SetupBleAsyncEventParams(4100, "d400").ToString()));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(21.2f, "the value is updated before the handler runs");
    }

    [Fact]
    public void Lumi_ZigBee_Device_Handler_Exception_Does_Not_Escape()
    {
        var th = CreateLumiProtocolDevice<XiaomiTemperatureHumiditySensor>(
            XiaomiTemperatureHumiditySensor.LUMI_MQTT_MODEL);
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData("[{\"res_name\":\"0.1.85\",\"value\":2515}]"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(25.15f);
    }

    [Fact]
    public void MiSpec_ZigBee_Device_Handler_Exception_Does_Not_Escape()
    {
        var sw = CreateLumiProtocolDevice<AqaraWirelessMiniSwitchCN2>(AqaraWirelessMiniSwitchCN2.LUMI_MQTT_MODEL);
        sw.OnClickAsync += _ => Throwing();

        RunCapturingEscapes(() => sw.ParseData("[{\"siid\":2,\"eiid\":1,\"arguments\":[]}]"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
    }

    [Theory]
    [InlineData(2, 1012)]
    [InlineData(3, 1)]
    public void Ptx_Handler_Exception_Is_Logged_Without_Escaping(int siid, int eiid)
    {
        var button = CreateLumiBleProtocolDevice<PtxWirelessSwitchBle>(PtxWirelessSwitchBle.PDID);
        button.OnClickAsync += _ => Throwing();
        button.OnLowBatteryAsync += Throwing;
        RunCapturingEscapes(() => button.ParseData(System.Text.Json.JsonSerializer.Serialize(new { siid, eiid })));
        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
    }

    [Fact]
    public void Mesh_Handler_Exception_Is_Logged_And_Following_Properties_Are_Parsed()
    {
        ProtocolDeviceRegistry.TryCreateMesh(XiaomiMeshWallSwitchSingleOpen.PDID, "mesh", _loggerFactory, out var device).Should().BeTrue();
        var sw = (XiaomiMeshWallSwitchSingleOpen)device;
        sw.OnStateChangeAsync += _ => Throwing();
        RunCapturingEscapes(() => sw.ParseData("[{\"siid\":2,\"piid\":1,\"value\":true},{\"siid\":10,\"piid\":1,\"value\":true}]"));
        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        sw.IsTurnedOn.Should().BeTrue();
        sw.IsIndicatorLightEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(4121)]
    [InlineData(4120)]
    [InlineData(4106)]
    public void DoorWindowSensor2_Handler_Exception_Is_Logged(int eid)
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiDoorWindowSensor2>(XiaomiDoorWindowSensor2.PDID);
        sensor.OnContactChangeAsync += _ => Throwing();
        sensor.OnLightChangeAsync += _ => Throwing();
        sensor.OnBatteryPercentChangeAsync += _ => Throwing();
        RunCapturingEscapes(() => sensor.ParseData(SetupBleAsyncEventParams(eid, "01").ToString()));
        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
    }

    [Theory]
    [InlineData(4117)]
    [InlineData(4106)]
    public void SmokeGuard_Handler_Exception_Is_Logged_After_State_Update(int eid)
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiSmokeSensorGuard>(XiaomiSmokeSensorGuard.PDID);
        sensor.OnSmokeChangeAsync += _ => Throwing();
        sensor.OnBatteryPercentChangeAsync += _ => Throwing();
        RunCapturingEscapes(() => sensor.ParseData(SetupBleAsyncEventParams(eid, "01").ToString()));
        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        if (eid == 4117) sensor.Smoke.Should().Be(XiaomiSmokeSensorGuard.SmokeState.SmokeDetected);
        else sensor.BatteryPercent.Should().Be(1);
    }

    [Theory]
    [InlineData(15, "000100")]
    [InlineData(4120, "01")]
    [InlineData(4119, "78000000")]
    [InlineData(4106, "64")]
    public void MotionSensor2_Handler_Exception_Is_Logged(int eid, string data)
    {
        var sensor = CreateLumiBleProtocolDevice<XiaomiSmartHumanBodySensor2>(XiaomiSmartHumanBodySensor2.PDID);
        sensor.OnMotionDetectedAsync += _ => Throwing();
        sensor.OnLightChangeAsync += _ => Throwing();
        sensor.OnNoMotionDetectedAsync += _ => Throwing();
        sensor.OnBatteryPercentChangeAsync += _ => Throwing();
        RunCapturingEscapes(() => sensor.ParseData(SetupBleAsyncEventParams(eid, data).ToString()));
        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
    }

    [Fact]
    public void MiSpec_Ble_Device_Handler_Exception_Does_Not_Escape()
    {
        var th = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph3>(XiaomiBluetoothHygrothermograph3.PDID);
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData("{\"siid\":3,\"piid\":1001,\"value\":21.5}"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(21.5f);
    }

    [Fact]
    public void Gateway2_Sub_Device_Handler_Exception_Does_Not_Escape()
    {
        ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2,
            XiaomiTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL,
            "158d000156a94b",
            1,
            _loggerFactory,
            out var device).Should().BeTrue();
        var th = (XiaomiTemperatureHumiditySensor)device;
        th.OnTemperatureChangeAsync += _ => Throwing();

        RunCapturingEscapes(() => th.ParseData("{\"temperature\":\"2515\"}"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        th.Temperature.Should().Be(25.15f);
    }

    [Fact]
    public void Timer_Handler_Exception_Does_Not_Escape()
    {
        var timer1 = new Mock<MiHomeLib.Devices.Timing.ITimer>();
        timer1.Setup(x => x.Start(Moq.It.IsAny<Action>())).Callback<Action>(elapsed => elapsed());

        // Only the one minute timer elapses, the others stay silent
        var sensor = new XiaomiSmartHumanBodySensor("did", 0, _loggerFactory,
            period => period == TimeSpan.FromMinutes(1) ? timer1.Object : new Mock<MiHomeLib.Devices.Timing.ITimer>().Object);
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        sensor.OnNoMotionAsync += _ => Throwing();

        RunCapturingEscapes(() => sensor.ParseData("[{\"res_name\":\"3.1.85\",\"value\":1}]"));

        _escaped.Should().BeEmpty();
        ShouldHaveLoggedHandlerFailure();
        timer1.Verify(x => x.Stop(), Times.AtLeast(2), "the one-minute timer is still stopped after the handler failed");
    }

    [Fact]
    public void Handler_Is_Awaited_Before_ParseData_Returns()
    {
        // Existing behaviour that must be kept: a synchronous handler has finished when ParseData returns
        var th = CreateLumiBleProtocolDevice<XiaomiBluetoothHygrothermograph2>(XiaomiBluetoothHygrothermograph2.PDID);
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
