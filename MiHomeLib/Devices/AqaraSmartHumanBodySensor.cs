using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>RTCGQ11LM motion and illuminance sensor shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraSmartHumanBodySensor : MotionSensorBase
{
    public const string MARKET_MODEL = "RTCGQ11LM";
    public const string XIAOMI_GATEWAY2_MODEL = "sensor_motion.aq2";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_motion.aq2";

    /// <summary>
    /// Illuminance in lux, 0 until the first report
    /// </summary>
    public int Lux { get; private set; }

    /// <summary>
    /// Raised on every report of the illuminance, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Lux"/>
    /// </summary>
    public event Func<int, Task> OnLuxChangeAsync = _ => Task.CompletedTask;

    public AqaraSmartHumanBodySensor(string deviceId, int shortId, ILoggerFactory loggerFactory)
        : this(deviceId, shortId, loggerFactory, period => new SimpleTimer(period)) { }

    internal AqaraSmartHumanBodySensor(string deviceId, int shortId, ILoggerFactory loggerFactory, Func<TimeSpan, ITimer> createTimer)
        : base(deviceId, shortId, loggerFactory, createTimer) { }

    private protected override string MarketModel => MARKET_MODEL;
    private protected override string XiaomiGateway2Model => XIAOMI_GATEWAY2_MODEL;
    private protected override string LumiMqttModel => LUMI_MQTT_MODEL;
    private protected override string ModelInfo => $", Lux: {Lux}";

    private protected override void ConfigureModelProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("lux", value =>
        {
            if (TryGetGateway2Int(value, out var lux)) UpdateLux(lux);
        });

        // Lux as is, the resource used by the AlexxIT/XiaomiGateway3 integration
        map.LumiMqtt("0.3.85", value => UpdateLux(value.ReadInt32()));
    }

    private void UpdateLux(int value) => SetAndRaise(nameof(OnLuxChangeAsync), Lux, () => Lux = value, OnLuxChangeAsync);
}
