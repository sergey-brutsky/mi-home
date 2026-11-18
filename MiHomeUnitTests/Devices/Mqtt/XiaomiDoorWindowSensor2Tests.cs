using MiHomeLib.Protocols;
using Xunit;
using AutoFixture;
using FluentAssertions;
using System.Threading.Tasks;
using MiHomeLib.Devices;

namespace MiHomeUnitTests.Devices.Mqtt;

public class XiaomiDoorWindowSensor2Tests: ProtocolDeviceTestBase
{
    private readonly XiaomiDoorWindowSensor2 _dwSensor2;

    public XiaomiDoorWindowSensor2Tests()
    {
        _dwSensor2 = CreateLumiBleProtocolDevice<XiaomiDoorWindowSensor2>(XiaomiDoorWindowSensor2.PDID);
    }

    [Theory]
    [InlineData(4121, "00", XiaomiDoorWindowSensor2.ContactState.Unknown)]
    [InlineData(4121, "01", XiaomiDoorWindowSensor2.ContactState.Open)]
    [InlineData(4121, "02", XiaomiDoorWindowSensor2.ContactState.Unknown)]
    public void Check_OnContactChange_Event(int eid, string edata, XiaomiDoorWindowSensor2.ContactState oldState)
    {
        // Arrange
        var eventRaised = false;

        if (oldState != XiaomiDoorWindowSensor2.ContactState.Unknown)
            _dwSensor2.ParseData(SetupBleAsyncEventParams(4121, ((int)oldState).ToString("x2")).ToString());

        _dwSensor2.OnContactChangeAsync += (oldValue) =>
         {
            eventRaised = oldValue == oldState;
            _dwSensor2.Contact.Should().Be((XiaomiDoorWindowSensor2.ContactState)int.Parse(edata));
            return Task.CompletedTask;
        };

        // Act
        _dwSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory]
    [InlineData(4120, "00", XiaomiDoorWindowSensor2.LightState.LightDiscovered)]
    [InlineData(4120, "01", XiaomiDoorWindowSensor2.LightState.NoLight)]
    public void Check_OnLightChange_Event(int eid, string edata, XiaomiDoorWindowSensor2.LightState oldState)
    {
        // Arrange
        var eventRaised = false;

        if (oldState != XiaomiDoorWindowSensor2.LightState.Unknown)
            _dwSensor2.ParseData(SetupBleAsyncEventParams(4120, ((int)oldState).ToString("x2")).ToString());

        _dwSensor2.OnLightChangeAsync += (oldValue) =>
         {
            eventRaised = oldValue == oldState;
            _dwSensor2.Light.Should().Be((XiaomiDoorWindowSensor2.LightState)int.Parse(edata));
            return Task.CompletedTask;
        };

        // Act
        _dwSensor2.ParseData(SetupBleAsyncEventParams(eid, edata).ToString());

        // Assert
        eventRaised.Should().BeTrue();
    }
    [Theory]
    [InlineData(4121, "00")]
    [InlineData(4121, "01")]
    [InlineData(4121, "02")]
    [InlineData(4120, "00")]
    [InlineData(4120, "01")]
    public void Repeated_State_Reports_Are_Suppressed(int eid, string data)
    {
        var count = 0;
        _dwSensor2.OnContactChangeAsync += _ => { count++; return Task.CompletedTask; };
        _dwSensor2.OnLightChangeAsync += _ => { count++; return Task.CompletedTask; };
        var report = SetupBleAsyncEventParams(eid, data).ToString();
        _dwSensor2.ParseData(report);
        _dwSensor2.ParseData(report);
        count.Should().Be(1);
    }

    [Fact]
    public void Battery_Hex_Is_Decoded_And_Previous_Value_Is_Reported_On_Every_Report()
    {
        var previous = new System.Collections.Generic.List<byte>();
        _dwSensor2.OnBatteryPercentChangeAsync += old =>
        {
            _dwSensor2.BatteryPercent.Should().Be(100);
            previous.Add(old);
            return Task.CompletedTask;
        };
        var report = SetupBleAsyncEventParams(4106, "64").ToString();
        _dwSensor2.ParseData(report);
        _dwSensor2.ParseData(report);
        previous.Should().Equal(0, 100);
    }

    [Theory]
    [InlineData(4121)]
    [InlineData(4120)]
    public void Unknown_Hex_State_Maps_To_Unknown(int eid)
    {
        _dwSensor2.ParseData(SetupBleAsyncEventParams(eid, "01").ToString());
        var count = 0;
        _dwSensor2.OnContactChangeAsync += _ => { count++; return Task.CompletedTask; };
        _dwSensor2.OnLightChangeAsync += _ => { count++; return Task.CompletedTask; };
        _dwSensor2.ParseData(SetupBleAsyncEventParams(eid, "FF").ToString());
        count.Should().Be(1);
        if (eid == 4121) _dwSensor2.Contact.Should().Be(XiaomiDoorWindowSensor2.ContactState.Unknown);
        else _dwSensor2.Light.Should().Be(XiaomiDoorWindowSensor2.LightState.Unknown);
    }

    [Theory]
    [InlineData(4121)]
    [InlineData(4120)]
    [InlineData(4106)]
    public void Invalid_Payload_Length_Does_Not_Update_State(int eid)
    {
        var report = SetupBleAsyncEventParams(eid, "0001").ToString();
        System.Action parse = () => _dwSensor2.ParseData(report);
        parse.Should().NotThrow();
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning && entry.Message.Contains(eid.ToString()));
        _dwSensor2.Contact.Should().Be(XiaomiDoorWindowSensor2.ContactState.Unknown);
        _dwSensor2.Light.Should().Be(XiaomiDoorWindowSensor2.LightState.Unknown);
        _dwSensor2.BatteryPercent.Should().Be(0);
    }

    [Fact]
    public void Battery_Description_Stays_Unknown_Until_A_Valid_Battery_Report()
    {
        _dwSensor2.ToString().Should().Contain("Battery Percent: Unknown");
        _dwSensor2.ParseData(SetupBleAsyncEventParams(4121, "00").ToString());
        _dwSensor2.ParseData(SetupBleAsyncEventParams(4120, "01").ToString());
        System.Action invalidReport = () => _dwSensor2.ParseData(SetupBleAsyncEventParams(4106, "0001").ToString());
        invalidReport.Should().NotThrow();
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning && entry.Message.Contains("4106"));
        _dwSensor2.ToString().Should().Contain("Battery Percent: Unknown");
    }

    [Theory]
    [InlineData("00", "0%")]
    [InlineData("64", "100%")]
    public void Battery_Description_Shows_Received_Value_Before_Event(string data, string expected)
    {
        var eventRaised = false;
        _dwSensor2.OnBatteryPercentChangeAsync += _ =>
        {
            _dwSensor2.ToString().Should().Contain($"Battery Percent: {expected}");
            eventRaised = true;
            return Task.CompletedTask;
        };

        _dwSensor2.ParseData(SetupBleAsyncEventParams(4106, data).ToString());

        eventRaised.Should().BeTrue();
        _dwSensor2.ToString().Should().Contain($"Battery Percent: {expected}");
    }

    [Fact]
    public void Description_Includes_Device_Model_Id_Mac_And_State()
    {
        ((MiHomeLib.Protocols.IProtocolMappedBleDevice)_dwSensor2).SetMacAddress("aa:bb:cc:dd:ee:ff");
        _dwSensor2.ToString().Should().Contain(XiaomiDoorWindowSensor2.MARKET_MODEL)
            .And.Contain(XiaomiDoorWindowSensor2.MODEL).And.Contain(_dwSensor2.DeviceId)
            .And.Contain(_dwSensor2.Mac).And.Contain("Contact: Unknown").And.Contain("Light: Unknown");
    }
}
