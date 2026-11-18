namespace MiHomeLib.MqttGateway.Discovery;

public class BleDeviceInfo
{
    public BleDeviceInfo(string did, int pdid, string mac)
    {
        Did = did;
        Pdid = pdid;
        Mac = mac;
    }

    public string Did { get; }
    public int Pdid { get; }
    public string Mac { get; }
}
