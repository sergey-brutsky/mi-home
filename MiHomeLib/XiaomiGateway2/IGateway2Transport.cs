using System;

namespace MiHomeLib.XiaomiGateway2;

/// <summary>
/// UDP multicast protocol of Xiaomi Gateway 2
/// </summary>
internal interface IGateway2Transport : IDisposable
{
    /// <summary>
    /// Asks the gateway for the list of its sub-devices: {"cmd":"get_id_list"}
    /// </summary>
    void SendDiscover();

    /// <summary>
    /// Asks a sub-device for its state: {"cmd":"read","sid":...}
    /// </summary>
    void SendRead(string sid);

    /// <summary>
    /// Writes values to a sub-device, the key is built from <see cref="Token"/> and the gateway password
    /// </summary>
    void SendWrite(string sid, string model, string gatewayPassword, params (string Key, string Value)[] data);

    event Action<string> OnMessageReceived;

    string Token { get; set; }
}
