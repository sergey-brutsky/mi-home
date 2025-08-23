using System;

namespace MiHomeLib.MultimodeGateway;

[Obsolete("Please use MultimodeGateway instead, this one will be removed in next releases")]
public class XiaomiGateway3(string ip, string token, int port = 1883) : MultimodeGateway(ip, token, port)
{
}
