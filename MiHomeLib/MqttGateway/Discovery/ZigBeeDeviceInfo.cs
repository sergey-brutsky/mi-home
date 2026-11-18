namespace MiHomeLib.MqttGateway.Discovery;

public class ZigBeeDeviceInfo
{
    public ZigBeeDeviceInfo(string did, string model)
    {
        Did = did;
        Model = model;
    }

    public string Did { get; }
    public string Model { get; }
}
