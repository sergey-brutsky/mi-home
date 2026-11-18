using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Devices;
using Xunit;
using static MiHomeLib.Devices.AqaraCubeSensor;

namespace MiHomeUnitTests.Devices.Mqtt;

public class AqaraCubeSensorTests : ProtocolDeviceTestBase
{
    public static TheoryData<string> Models => new() { LUMI_MQTT_MODEL, LUMI_MQTT_MODEL_LEGACY };

    [Theory]
    [MemberData(nameof(Models))]
    public void Both_Models_Are_Registered_And_Read_Battery(string model)
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(model);
        cube.GetInitialPropertyNames().Should().Equal("voltage", "battery");

        cube.SetInitialProperties([JsonValue.Create(3005), JsonValue.Create(90)]);

        cube.Voltage.Should().Be(3.005f);
        cube.BatteryPercent.Should().Be(90);
    }

    [Theory]
    [InlineData(0, CubeStatus.Flip90)]
    [InlineData(1, CubeStatus.Flip180)]
    [InlineData(2, CubeStatus.Move)]
    [InlineData(3, CubeStatus.TapTwice)]
    [InlineData(20, CubeStatus.ShakeAir)]
    public void Action_Raises_OnStatusChanged(int code, CubeStatus expected)
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(LUMI_MQTT_MODEL);
        var raised = new List<CubeStatus>();
        cube.OnStatusChangedAsync += x => { raised.Add(x); return Task.CompletedTask; };

        cube.ParseData($"[{{\"res_name\":\"13.1.85\",\"value\":{code}}}]");

        raised.Should().Equal(expected);
    }

    [Theory]
    [InlineData("[{\"res_name\":\"13.1.85\",\"value\":16},{\"res_name\":\"0.3.85\",\"value\":-45300},{\"res_name\":\"0.2.85\",\"value\":500}]")]
    [InlineData("[{\"res_name\":\"0.3.85\",\"value\":-45300},{\"res_name\":\"0.2.85\",\"value\":500},{\"res_name\":\"13.1.85\",\"value\":16}]")]
    public void Rotation_Is_Raised_Once_With_Angle_And_Duration_In_Any_Order(string data)
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(LUMI_MQTT_MODEL);
        var raised = new List<(CubeStatus, CubeRotate)>();
        cube.OnStatusChangedAsync += x => { raised.Add((x, cube.Rotate)); return Task.CompletedTask; };

        cube.ParseData(data);

        raised.Should().ContainSingle();
        raised[0].Item1.Should().Be(CubeStatus.Rotated);
        raised[0].Item2.Shift.Should().Be(-45);
        raised[0].Item2.Value.Should().Be(500);
    }

    [Fact]
    public void Report_Without_Action_Does_Not_Raise_Status()
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(LUMI_MQTT_MODEL);
        var raised = false;
        cube.OnStatusChangedAsync += _ => { raised = true; return Task.CompletedTask; };

        cube.ParseData("[{\"res_name\":\"8.0.2008\",\"value\":2995}]");

        raised.Should().BeFalse();
        cube.Voltage.Should().Be(2.995f);
    }

    [Fact]
    public void Incomplete_Report_Does_Not_Leak_Action_Or_Angle_Into_Next_Message()
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(LUMI_MQTT_MODEL);
        var raised = new List<CubeStatus>();
        cube.OnStatusChangedAsync += x => { raised.Add(x); return Task.CompletedTask; };

        System.Action incomplete = () => cube.ParseData("""[{"res_name":"13.1.85","value":16},{"res_name":"0.3.85","value":45000},{"res_name":"0.2.85","value":"bad"}]""");
        incomplete.Should().NotThrow();
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning && entry.Message.Contains("0.2.85"));
        cube.ParseData("""[{"res_name":"8.0.2008","value":2995}]""");
        raised.Should().BeEmpty();

        incomplete.Should().NotThrow();
        cube.ParseData("""[{"res_name":"13.1.85","value":16}]""");
        raised.Should().Equal(CubeStatus.Rotated);
        cube.Rotate.Shift.Should().Be(0);
        cube.Rotate.Value.Should().Be(0);
    }

    [Fact]
    public void Unknown_Action_Code_Is_Logged_And_Reported_As_Undefined()
    {
        var cube = CreateLumiProtocolDevice<AqaraCubeSensor>(LUMI_MQTT_MODEL);
        CubeStatus? raised = null;
        cube.OnStatusChangedAsync += x => { raised = x; return Task.CompletedTask; };

        cube.ParseData("[{\"res_name\":\"13.1.85\",\"value\":99}]");

        raised.Should().Be(CubeStatus.Undefined);
        _loggerFactory.Entries.Should().Contain(x => x.Message.Contains("'99'"));
    }
}
