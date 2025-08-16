namespace MiHomeLib.XiaomiGateway2.Commands;

internal class ReadDeviceCommand(string sid) : Command
{
    public string Sid { get; } = sid;

    public override string ToString() => $"{{\"cmd\":\"read\",\"sid\":\"{Sid}\"}}";
}
