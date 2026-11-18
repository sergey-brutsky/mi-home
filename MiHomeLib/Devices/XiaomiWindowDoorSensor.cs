using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using System;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>MCCGQ01LM window and door sensor shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class XiaomiWindowDoorSensor : WindowDoorSensorBase
{
    public const string MARKET_MODEL = "MCCGQ01LM";
    public const string XIAOMI_GATEWAY2_MODEL = "magnet";
    public const string LUMI_MQTT_MODEL = "lumi.sensor_magnet";

    public XiaomiWindowDoorSensor(string deviceId, int shortId, ILoggerFactory loggerFactory)
        : this(deviceId, shortId, loggerFactory, new SimpleTimer(TimeSpan.FromMinutes(1)), new SimpleTimer(TimeSpan.FromMinutes(5))) { }

    internal XiaomiWindowDoorSensor(string deviceId, int shortId, ILoggerFactory loggerFactory, ITimer oneMinuteTimer, ITimer fiveMinutesTimer)
        : base(deviceId, shortId, loggerFactory, oneMinuteTimer, fiveMinutesTimer) { }

    private protected override string MarketModel => MARKET_MODEL;
    private protected override string XiaomiGateway2Model => XIAOMI_GATEWAY2_MODEL;
    private protected override string LumiMqttModel => LUMI_MQTT_MODEL;
}
