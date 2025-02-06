namespace MiHomeLib.Commands;

internal class ReadDeviceCommand(string sid) : Command
{
    private readonly string _sid = sid;

    public override string ToString() => $"{{\"cmd\":\"read\",\"sid\":\"{_sid}\"}}";
}
