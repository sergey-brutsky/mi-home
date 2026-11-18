using System.Collections.Generic;

namespace MiHomeLib.MqttGateway.Discovery;

public interface IDevicesDiscoverer
{
    List<BleDeviceInfo> DiscoverBleDevices();
    List<MeshDeviceInfo> DiscoverMeshDevices();
    List<ZigBeeDeviceInfo> DiscoverZigBeeDevices();
}
