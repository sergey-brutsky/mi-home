using System.Collections.Generic;

namespace MiHomeLib;

public interface IDevicesDiscoverer
{
    List<(string did, int pdid, string mac)> DiscoverBleDevices();
    List<(string did, string model)> DiscoverZigBeeDevices();
}