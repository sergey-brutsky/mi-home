using Microsoft.Extensions.Logging;
using MiHomeLib.Protocols;

namespace MiHomeLib.Devices;

#pragma warning disable CS0618
/// <summary>Opple wireless switch with four buttons, supported by Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraOppleFourButtonsWirelessSwitch : AqaraOppleFourButtonsWirelesSwitch
{
    public AqaraOppleFourButtonsWirelessSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory)
        : base(deviceId, shortId, loggerFactory) { }
}
#pragma warning restore CS0618
