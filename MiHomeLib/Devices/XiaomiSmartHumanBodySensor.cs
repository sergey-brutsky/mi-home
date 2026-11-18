using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>RTCGQ01LM motion sensor shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class XiaomiSmartHumanBodySensor : MotionSensorBase
{
    public const string MARKET_MODEL = "RTCGQ01LM";
    public const string XIAOMI_GATEWAY2_MODEL = "motion";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_motion";

    public XiaomiSmartHumanBodySensor(string deviceId, int shortId, ILoggerFactory loggerFactory)
        : this(deviceId, shortId, loggerFactory, period => new SimpleTimer(period)) { }

    internal XiaomiSmartHumanBodySensor(string deviceId, int shortId, ILoggerFactory loggerFactory, Func<TimeSpan, ITimer> createTimer)
        : base(deviceId, shortId, loggerFactory, createTimer) { }

    private protected override string MarketModel => MARKET_MODEL;
    private protected override string XiaomiGateway2Model => XIAOMI_GATEWAY2_MODEL;
    private protected override string LumiMqttModel => LUMI_MQTT_MODEL;
}
