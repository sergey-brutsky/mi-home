using System;

namespace MiHomeLib.Events;

public class DiscoverEventArgs(string ip, string type, string serial, string token) : EventArgs
{
    public string Ip { get; } = ip;
    public string Type { get; } = type;
    public string Serial { get; } = serial;
    public string Token { get; } = token;
}