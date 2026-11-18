using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraSmartHumanBodySensorTests : ProtocolDeviceTestBase
{
    [Fact]
    public void Is_Registered_For_Lumi_Mqtt_And_Reads_Battery_Properties()
    {
        var sensor = CreateLumiProtocolDevice<AqaraSmartHumanBodySensor>(AqaraSmartHumanBodySensor.LUMI_MQTT_MODEL);

        sensor.GetInitialPropertyNames().Should().Equal("voltage", "battery", "lqi", "chip_temperature");
        sensor.SetInitialProperties([JsonValue.Create(3005), JsonValue.Create(95), JsonValue.Create(120), JsonValue.Create(25)]);

        sensor.BatteryPercent.Should().Be(95);
        sensor.ToString().Should().Contain("lumi.sensor_motion.aq2").And.Contain("BatteryPercent: 95%");
    }

    [Fact]
    public void Motion_Raises_OnMotion_And_Times_No_Motion_Periods()
    {
        var timers = new Dictionary<int, Mock<ITimer>>();
        var sensor = new AqaraSmartHumanBodySensor(_fixture.Create<string>(), 0, _loggerFactory, period =>
        {
            var timer = new Mock<ITimer>();
            if (period == TimeSpan.FromSeconds(300))
                timer.Setup(x => x.Start(Moq.It.IsAny<Action>())).Callback<Action>(elapsed => elapsed());
            timers[(int)period.TotalSeconds] = timer;
            return timer.Object;
        });
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        var motion = 0;
        var noMotion = new List<int>();
        sensor.OnMotionAsync += () => { motion++; return Task.CompletedTask; };
        sensor.OnNoMotionAsync += x => { noMotion.Add(x); return Task.CompletedTask; };

        sensor.ParseData("[{\"res_name\":\"3.1.85\",\"value\":1}]");

        motion.Should().Be(1);
        noMotion.Should().Equal(300);
        sensor.MotionDate.Should().NotBeNull();
        timers.Keys.Should().Equal(MotionSensorBase.NoMotionPeriods);
    }

    [Fact]
    public void Illuminance_Report_Updates_Lux_And_Passes_Old_Value()
    {
        var sensor = CreateLumiProtocolDevice<AqaraSmartHumanBodySensor>(AqaraSmartHumanBodySensor.LUMI_MQTT_MODEL);
        sensor.ParseData("[{\"res_name\":\"0.3.85\",\"value\":15}]");
        int? oldLux = null;
        sensor.OnLuxChangeAsync += x => { oldLux = x; return Task.CompletedTask; };

        sensor.ParseData("[{\"res_name\":\"0.3.85\",\"value\":320}]");

        sensor.Lux.Should().Be(320);
        oldLux.Should().Be(15);
        sensor.ToString().Should().EndWith("Lux: 320");
    }
}
