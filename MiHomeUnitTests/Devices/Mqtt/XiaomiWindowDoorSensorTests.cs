using System;
using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using Moq;
using Xunit;
using ContactState = MiHomeLib.Devices.WindowDoorSensorBase.DoorWindowContactState;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiWindowDoorSensorTests : ProtocolDeviceTestBase
{
    private const string OPENED = "[{\"res_name\":\"3.1.85\",\"value\":1}]";
    private const string CLOSED = "[{\"res_name\":\"3.1.85\",\"value\":0}]";

    private readonly Mock<ITimer> _oneMinuteTimer = new();
    private readonly Mock<ITimer> _fiveMinutesTimer = new();

    public static TheoryData<string> Models => new() { XiaomiWindowDoorSensor.LUMI_MQTT_MODEL, AqaraWindowDoorSensor.LUMI_MQTT_MODEL };

    private XiaomiWindowDoorSensor CreateWithTimers()
    {
        var sensor = new XiaomiWindowDoorSensor(_fixture.Create<string>(), 0, _loggerFactory, _oneMinuteTimer.Object, _fiveMinutesTimer.Object);
        sensor.InitializeProtocol(DeviceProtocol.LumiMqtt);
        return sensor;
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Both_Window_Door_Models_Are_Registered_For_Lumi_Mqtt(string model)
    {
        var sensor = CreateLumiProtocolDevice<WindowDoorSensorBase>(model);

        sensor.Should().BeAssignableTo<WindowDoorSensorBase>();
        sensor.ToString().Should().Contain(model);
    }

    [Theory]
    [InlineData(XiaomiWindowDoorSensor.LUMI_MQTT_MODEL, 1, ContactState.Opened, "open")]
    [InlineData(XiaomiWindowDoorSensor.LUMI_MQTT_MODEL, 0, ContactState.Closed, "close")]
    [InlineData(AqaraWindowDoorSensor.LUMI_MQTT_MODEL, 1, ContactState.Opened, "open")]
    [InlineData(AqaraWindowDoorSensor.LUMI_MQTT_MODEL, 0, ContactState.Closed, "close")]
    public void Lumi_Contact_Raises_Every_Contact_Event(string model, int value, ContactState expected, string expectedStatus)
    {
        var sensor = CreateLumiProtocolDevice<WindowDoorSensorBase>(model);
        var raised = new List<string>();
        sensor.OnContactChangedAsync += () => { raised.Add("contact"); return Task.CompletedTask; };
        sensor.OnOpenAsync += () => { raised.Add("open"); return Task.CompletedTask; };
        sensor.OnCloseAsync += () => { raised.Add("close"); return Task.CompletedTask; };

        sensor.ParseData($"[{{\"res_name\":\"3.1.85\",\"value\":{value}}}]");

        raised.Should().Equal(expectedStatus, "contact");
        sensor.Contact.Should().Be(expected);
        sensor.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public void Lumi_Initial_Properties_Are_Read_In_Battery_Device_Order()
    {
        var sensor = CreateLumiProtocolDevice<XiaomiWindowDoorSensor>(XiaomiWindowDoorSensor.LUMI_MQTT_MODEL);
        sensor.GetInitialPropertyNames().Should().Equal("voltage", "battery", "lqi", "chip_temperature");

        sensor.SetInitialProperties(
        [
            System.Text.Json.Nodes.JsonValue.Create(3005),
            System.Text.Json.Nodes.JsonValue.Create(95),
            System.Text.Json.Nodes.JsonValue.Create(132),
            System.Text.Json.Nodes.JsonValue.Create(36)
        ]);

        sensor.Voltage.Should().Be(3.005f);
        sensor.BatteryPercent.Should().Be(95);
        sensor.LinkQuality.Should().Be(132);
        sensor.ChipTemperature.Should().Be(36);
    }

    [Fact]
    public void Opening_Starts_Not_Closed_Timers_And_Closing_Stops_Them()
    {
        var sensor = CreateWithTimers();

        sensor.ParseData(OPENED);
        _oneMinuteTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Times.Once);
        _fiveMinutesTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Times.Once);

        sensor.ParseData(CLOSED);
        _oneMinuteTimer.Verify(x => x.Stop(), Times.Once);
        _fiveMinutesTimer.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public void Repeated_Opened_Report_Does_Not_Restart_The_Countdown()
    {
        var sensor = CreateWithTimers();

        sensor.ParseData(OPENED);
        sensor.ParseData(OPENED);

        _oneMinuteTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Times.Once);
        _fiveMinutesTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Times.Once);
        _oneMinuteTimer.Verify(x => x.Stop(), Times.Never);
    }

    [Fact]
    public void One_Minute_Timer_Raises_NotClosedFor1Minute_Once()
    {
        _oneMinuteTimer.Setup(x => x.Start(Moq.It.IsAny<Action>())).Callback<Action>(elapsed => elapsed());
        var sensor = CreateWithTimers();
        var oneMinute = 0;
        var fiveMinutes = 0;
        sensor.NotClosedFor1MinuteAsync += () => { oneMinute++; return Task.CompletedTask; };
        sensor.NotClosedFor5MinutesAsync += () => { fiveMinutes++; return Task.CompletedTask; };

        sensor.ParseData(OPENED);

        oneMinute.Should().Be(1);
        fiveMinutes.Should().Be(0);
        _oneMinuteTimer.Verify(x => x.Stop(), Times.Once, "the timer fires once per opening");
    }

    [Fact]
    public void Five_Minutes_Timer_Raises_NotClosedFor5Minutes()
    {
        _fiveMinutesTimer.Setup(x => x.Start(Moq.It.IsAny<Action>())).Callback<Action>(elapsed => elapsed());
        var sensor = CreateWithTimers();
        var fiveMinutes = 0;
        sensor.NotClosedFor5MinutesAsync += () => { fiveMinutes++; return Task.CompletedTask; };

        sensor.ParseData(OPENED);

        fiveMinutes.Should().Be(1);
    }

    [Fact]
    public void Reopening_After_Close_Starts_The_Countdown_Again()
    {
        var sensor = CreateWithTimers();

        sensor.ParseData(OPENED);
        sensor.ParseData(CLOSED);
        sensor.ParseData(OPENED);

        _oneMinuteTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Times.Exactly(2));
        _fiveMinutesTimer.Verify(x => x.Start(Moq.It.IsAny<Action>()), Times.Exactly(2));
    }
}
