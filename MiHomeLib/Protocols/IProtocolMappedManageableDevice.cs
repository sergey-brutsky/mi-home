using MiHomeLib.MqttGateway;

namespace MiHomeLib.Protocols;

/// <summary>
/// A unified device that accepts commands. Lumi MQTT gateways hand it their transport after creating it
/// </summary>
internal interface IProtocolMappedManageableDevice
{
    void SetMqttTransport(IMqttTransport mqttTransport);
}
