using MiHomeLib.Protocols;
using System.Collections.Generic;
using System.Threading.Tasks;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.XiaomiGateway2;
using MiHomeUnitTests.Devices.Gateway2;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class AqaraWaterLeakSensorTests : Gateway2DeviceTestBase
{
    private const string Sid = "158d0001d561e2";
    private const int ShortId = 12345;
    private readonly AqaraWaterLeakSensor _waterLeakSensor;

    public AqaraWaterLeakSensorTests()
    {
        Assert.True(ProtocolDeviceRegistry.TryCreate(
            DeviceProtocol.XiaomiGateway2,
            AqaraWaterLeakSensor.XIAOMI_GATEWAY2_MODEL,
            Sid,
            ShortId,
            _loggerFactory,
            out var device));
        _waterLeakSensor = (AqaraWaterLeakSensor)device;
    }

    [Fact]
    public void Parses_Gateway2_Status_And_Voltage()
    {
        var command = CreateCommand("heartbeat", AqaraWaterLeakSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["status"] = "leak", ["voltage"] = 3005 });

        _waterLeakSensor.ParseData(Gateway2Message.Parse(command).Data);

        Assert.Equal(Sid, _waterLeakSensor.DeviceId);
        Assert.Equal(ShortId, _waterLeakSensor.ShortId);
        Assert.True(_waterLeakSensor.Moisture);
        Assert.Equal(3.005f, _waterLeakSensor.Voltage);
        Assert.Contains("Voltage:", _waterLeakSensor.ToString());
        Assert.DoesNotContain("BatteryPercent:", _waterLeakSensor.ToString());
    }

    [Theory]
    [InlineData("leak", true)]
    [InlineData("no_leak", false)]
    public void Gateway2_Status_Raises_Moisture_Event(string status, bool expectedMoisture)
    {
        bool? eventValue = null;
        _waterLeakSensor.OnMoistureChangeAsync += value =>
        {
            eventValue = value;
            return Task.CompletedTask;
        };
        var command = CreateCommand("report", AqaraWaterLeakSensor.XIAOMI_GATEWAY2_MODEL, Sid, ShortId,
            new Dictionary<string, object> { ["status"] = status });

        _waterLeakSensor.ParseData(Gateway2Message.Parse(command).Data);

        Assert.Equal(expectedMoisture, eventValue);
    }
}
