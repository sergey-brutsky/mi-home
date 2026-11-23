using Microsoft.Extensions.Logging;
using MiHomeLib.Protocols;

namespace MiHomeLib.Devices;

#pragma warning disable CS0618
/// <summary>Opple wireless switch with two buttons, supported by Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraOppleTwoButtonsWirelessSwitch : AqaraOppleTwoButtonsWirelesSwitch
{
    public AqaraOppleTwoButtonsWirelessSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory)
        : base(deviceId, shortId, loggerFactory) { }
}
#pragma warning restore CS0618
