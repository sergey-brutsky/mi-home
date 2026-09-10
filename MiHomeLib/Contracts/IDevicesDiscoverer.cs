using System.Collections.Generic;

namespace MiHomeLib.Contracts;

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

public interface IDevicesDiscoverer
{
    List<BleDeviceInfo> DiscoverBleDevices();
    List<ZigBeeDeviceInfo> DiscoverZigBeeDevices();
}