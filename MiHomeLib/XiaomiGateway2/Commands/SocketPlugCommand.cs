using System;

namespace MiHomeLib.XiaomiGateway2.Commands;

public class SocketPlugCommand: Command
{
    private readonly string _status;

    public SocketPlugCommand(string status = "on")
    {
        if(status != "on" && status != "off") throw new ArgumentException("Status must be on/off");

        _status = status;
    }

    public override string ToString() => $"{{\"status\":\"{_status}\"}}";
}