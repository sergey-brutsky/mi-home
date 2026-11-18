using MiHomeLib.XiaomiGateway2;

namespace MiHomeLib.Protocols;

/// <summary>
/// A unified device that accepts commands on Xiaomi Gateway 2. The gateway hands itself as the writer after creating it
/// </summary>
internal interface IProtocolMappedGateway2ManageableDevice
{
    void SetGateway2Writer(IGateway2Writer writer);
}
