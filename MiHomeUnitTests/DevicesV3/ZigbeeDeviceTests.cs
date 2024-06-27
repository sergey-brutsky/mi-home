using AutoFixture;
using Xunit;
using FluentAssertions;
using MiHomeLib.DevicesV3;

namespace MiHomeUnitTests.DevicesV3;

public class ZigbeeDeviceTests: MiHome3DeviceTests
{
    [Theory, InlineData("[{\"res_name\":\"8.0.2008\",\"value\":3025}]")]
    public void Check_OnVoltageChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<float>();
        var th = _fixture
                    .Build<XiaomiThSensor>()
                    .With(x => x.Voltage, oldValue)
                    .Create();
        
        var eventRaised = false;

        th.OnVoltageChange += (x) => 
        { 
            eventRaised = x == oldValue;
            th.Voltage.Should().Be(DataToZigbeeResource(data)[0].Value/1000f);
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"8.0.2001\",\"value\":95}]")]
    public void Check_OnBatteryPercentChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<byte>();
        var th = _fixture
                    .Build<XiaomiThSensor>()
                    .With(x => x.BatteryPercent, oldValue)
                    .Create();
        
        var eventRaised = false;

        th.OnBatteryPercentChange += (x) => 
        { 
            eventRaised = x == oldValue;
            th.BatteryPercent.Should().Be((byte)DataToZigbeeResource(data)[0].Value);
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"8.0.2007\",\"value\":181}]")]
    public void Check_OnLinqQualityChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<byte>();

        var th = _fixture
                    .Build<XiaomiThSensor>()
                    .With(x => x.LinqQuality, oldValue)
                    .Create();
        
        var eventRaised = false;

        th.OnLinkQualityChange += (x) => 
        { 
            eventRaised = x == oldValue;
            th.LinqQuality.Should().Be((byte)DataToZigbeeResource(data)[0].Value);
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Theory, InlineData("[{\"res_name\":\"8.0.2006\",\"value\":81}]")]
    public void Check_OnChipTemperatureChange_Event(string data)
    {
        // Arrange  
        var oldValue = _fixture.Create<byte>();
        var th = _fixture
                    .Build<XiaomiThSensor>()
                    .With(x => x.ChipTemperature, oldValue)
                    .Create();
        
        var eventRaised = false;

        th.OnChipTemperatureChange += (x) => 
        { 
            eventRaised = x == oldValue;
            th.ChipTemperature.Should().Be((byte)DataToZigbeeResource(data)[0].Value);
        };

        // Act
        th.ParseData(data);

        // Assert
        eventRaised.Should().BeTrue();
    }
}
