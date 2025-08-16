using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;
using MiHomeUnitTests.XiaomiGateway2SubDevicesTests;
using Xunit;

namespace MiHomeUnitTests.XiaomiGateway2SubDevices;

public class AqaraWaterLeakSensorTests : Gw2DeviceTests
{
    private readonly string _sid = "158d0001d561e2";
    private readonly int _shortId = 12345;
    private readonly AqaraWaterLeakSensor _waterLeakSensor;

    public AqaraWaterLeakSensorTests()
    {
        _waterLeakSensor = new AqaraWaterLeakSensor(_sid, _shortId, _loggerFactory);
    }

    [Fact]
    public void Check_WaterLeakSensor_Report_Data()
    {
        // Arrange
        var cmd = CreateCommand("report", AqaraWaterLeakSensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "status", "no_leak" }
                });

        // Act
        _waterLeakSensor.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _waterLeakSensor.Sid);
        Assert.Equal("no_leak", _waterLeakSensor.Status);
    }

    [Fact]
    public void Check_WaterLeakSensor_Heartbeat_Data()
    {
        // Arrange
        var cmd = CreateCommand("heartbeat", AqaraWaterLeakSensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "voltage", 3005 }
                });

        // Act
        _waterLeakSensor.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.Equal(_sid, _waterLeakSensor.Sid);
        Assert.Equal(3.005f, _waterLeakSensor.Voltage);
    }

    [Fact]
    public void Check_WaterLeakSensor_Raised_Leak()
    {
        // Arrange
        bool leakRaised = false;

        _waterLeakSensor.OnLeakAsync += () =>
        {
            leakRaised = true;
            return Task.CompletedTask;
        };
        
        // Act
        var cmd = CreateCommand("report", AqaraWaterLeakSensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "status", "leak" }
                });

        _waterLeakSensor.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.True(leakRaised);
    }

    [Fact]
    public void Check_WaterLeakSensor_Raised_NoLeak()
    {
        // Arrange
        bool noleakRaised = false;

        _waterLeakSensor.OnNoLeakAsync += () =>
        {
            noleakRaised = true;
            return Task.CompletedTask;
        };

        // Act
        var cmd = CreateCommand("report", AqaraWaterLeakSensor.MODEL, _sid, _shortId,
                new Dictionary<string, object>
                {
                    { "status", "no_leak" }
                });

        _waterLeakSensor.ParseData(ResponseCommand.FromString(cmd).Data);

        // Assert
        Assert.True(noleakRaised);
    }
}