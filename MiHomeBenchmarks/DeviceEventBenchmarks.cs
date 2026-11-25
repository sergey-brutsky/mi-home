using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using System.Threading.Tasks;

namespace MiHomeBenchmarks;

[MemoryDiagnoser]
public class DeviceEventBenchmarks
{
    private XiaomiTemperatureHumiditySensor _sensorWithoutSubscribers;
    private XiaomiTemperatureHumiditySensor _sensorWithSubscribers;
    private int _temperature;

    [GlobalSetup]
    public void Setup()
    {
        _sensorWithoutSubscribers = CreateSensor("benchmark-device-0");
        _sensorWithSubscribers = CreateSensor("benchmark-device-3");
        _sensorWithSubscribers.OnTemperatureChangeAsync += _ => Task.CompletedTask;
        _sensorWithSubscribers.OnTemperatureChangeAsync += _ => Task.CompletedTask;
        _sensorWithSubscribers.OnTemperatureChangeAsync += _ => Task.CompletedTask;
    }

    [Benchmark(Baseline = true)]
    public void ChangeWithNoDeviceSubscribers() =>
        _sensorWithoutSubscribers.UpdateTemperature((++_temperature % 1000) / 10f);

    [Benchmark]
    public void ChangeWithThreeDeviceSubscribers() =>
        _sensorWithSubscribers.UpdateTemperature((++_temperature % 1000) / 10f);

    [GlobalCleanup]
    public void Cleanup()
    {
        _sensorWithoutSubscribers.Dispose();
        _sensorWithSubscribers.Dispose();
    }

    private static XiaomiTemperatureHumiditySensor CreateSensor(string deviceId)
    {
        var sensor = new XiaomiTemperatureHumiditySensor(deviceId, 1, NullLoggerFactory.Instance);
        sensor.InitializeProtocol(DeviceProtocol.XiaomiGateway2, XiaomiTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL);
        sensor.EventSink = _ => Task.CompletedTask;
        return sensor;
    }
}
