using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.MqttGateway.Discovery;

internal interface IAsyncDevicesDiscoverer
{
    Task<List<ZigBeeDeviceInfo>> DiscoverZigBeeDevicesAsync(CancellationToken cancellationToken);
    Task<List<BleDeviceInfo>> DiscoverBleDevicesAsync(CancellationToken cancellationToken);
    Task<List<MeshDeviceInfo>> DiscoverMeshDevicesAsync(CancellationToken cancellationToken);
}
