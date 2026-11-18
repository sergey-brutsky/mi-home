using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiSmartHumanBodySensorTests: ProtocolDeviceTestBase
{
    private const string MOTION = "[{\"res_name\":\"3.1.85\",\"value\":1}]";

    // One mock timer per no motion period, keyed by seconds
    private readonly Dictionary<int, Mock<ITimer>> _timers = [];

    private XiaomiSmartHumanBodySensor CreateWithTimers(params int[] elapsingPeriods)
    {
        var sensor = new XiaomiSmartHumanBodySensor(_fixture.Create<string>(), 0, _loggerFactory, period =>
        {
            var seconds = (int)period.TotalSeconds;
            var timer = new Mock<ITimer>();
            if (elapsingPeriods.Contains(seconds))
                timer.Setup(x => x.Start(Moq.It.IsAny<Action>())).Callback<Action>(elapsed => elapsed());
            _timers[seconds] = timer;
            return timer.Object;
        });
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        return sensor;
    }

    [Theory]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":1}]")]
    [InlineData("[{\"res_name\":\"3.1.85\",\"value\":0}]")]
    public void Check_OnMotion_Event(string data)
    {
        var motionSensor = CreateLumiProtocolDevice<XiaomiSmartHumanBodySensor>(XiaomiSmartHumanBodySensor.LUMI_MQTT_MODEL);
        var eventRaised = false;
        motionSensor.OnMotionAsync += () => { eventRaised = true; return Task.CompletedTask; };

        motionSensor.ParseData(data);

        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void A_Timer_Is_Created_For_Every_Gateway2_No_Motion_Period()
    {
        CreateWithTimers();

        _timers.Keys.Should().Equal(60, 120, 180, 300, 600, 1800);
        XiaomiSmartHumanBodySensor.NoMotionPeriods.Should().Equal(_timers.Keys);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(180)]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(1800)]
    public void Elapsed_Timer_Raises_OnNoMotion_With_Its_Period(int period)
    {
        var motionSensor = CreateWithTimers(period);
        var reported = new List<int>();
        motionSensor.OnNoMotionAsync += seconds => { reported.Add(seconds); return Task.CompletedTask; };

        motionSensor.ParseData(MOTION);

        reported.Should().Equal(period);
        _timers[period].Verify(x => x.Stop(), Times.Exactly(2), "stopped by the motion and once more after firing, so it fires once");
    }

    [Fact]
    public void Motion_Restarts_Every_No_Motion_Timer()
    {
        var motionSensor = CreateWithTimers();

        motionSensor.ParseData(MOTION);
        motionSensor.ParseData(MOTION);

        foreach (var timer in _timers.Values)
        {
            timer.Verify(x => x.Stop(), Times.Exactly(2));
            timer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Times.Exactly(2));
        }
    }

    [Fact]
    public void Parses_Lumi_Battery_Properties_And_Requests_Them_In_Order()
    {
        var sensor = CreateLumiProtocolDevice<XiaomiSmartHumanBodySensor>(XiaomiSmartHumanBodySensor.LUMI_MQTT_MODEL);
        sensor.GetInitialPropertyNames().Should().Equal("voltage", "battery", "lqi", "chip_temperature");

        sensor.SetInitialProperties([JsonValue.Create(3005), JsonValue.Create(95), JsonValue.Create(120), JsonValue.Create(25)]);

        sensor.Voltage.Should().Be(3.005f);
        sensor.BatteryPercent.Should().Be(95);
        sensor.LinkQuality.Should().Be(120);
        sensor.ChipTemperature.Should().Be(25);
    }

    [Fact]
    public void Voltage_Report_Updates_Value_And_Passes_Old_Value()
    {
        var sensor = CreateLumiProtocolDevice<XiaomiSmartHumanBodySensor>(XiaomiSmartHumanBodySensor.LUMI_MQTT_MODEL);
        sensor.SetInitialProperties([JsonValue.Create(3005), JsonValue.Create(95), JsonValue.Create(120), JsonValue.Create(25)]);
        float? oldVoltage = null;
        sensor.OnVoltageChangeAsync += x => { oldVoltage = x; return Task.CompletedTask; };

        sensor.ParseData("[{\"res_name\":\"8.0.2008\",\"value\":2955}]");

        sensor.Voltage.Should().Be(2.955f);
        oldVoltage.Should().Be(3.005f);
    }
}
