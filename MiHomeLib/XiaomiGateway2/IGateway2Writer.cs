using System.Threading.Tasks;

namespace MiHomeLib.XiaomiGateway2;

/// <summary>
/// Everything a sub-device of Xiaomi Gateway 2 needs to accept commands. The gateway implements it
/// and adds the write key itself, so the developer key is requested only when a command is really sent
/// </summary>
internal interface IGateway2Writer
{
    Task WriteAsync(string sid, string model, params (string Key, string Value)[] data);
}
