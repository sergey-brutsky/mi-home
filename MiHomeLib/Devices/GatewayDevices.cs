using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiHomeLib.Helpers;

namespace MiHomeLib.Devices;

/// <summary>
/// Sub-devices of one gateway, shared by its discovery and its message processing threads
/// </summary>
internal sealed class GatewayDevices
{
    private readonly Dictionary<string, GatewaySubDeviceBase> _devices = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public bool Contains(string deviceId)
    {
        lock (_lock) return _devices.ContainsKey(deviceId);
    }

    public bool TryGet(string deviceId, out GatewaySubDeviceBase device)
    {
        lock (_lock) return _devices.TryGetValue(deviceId, out device);
    }

    public void Add(string deviceId, GatewaySubDeviceBase device)
    {
        lock (_lock) _devices.Add(deviceId, device);
    }

    /// <summary>
    /// Adds the device unless the gateway is being disposed; then the device is disposed and the call throws
    /// </summary>
    public void Add(string deviceId, GatewaySubDeviceBase device, CancellationToken gatewayLifetime)
    {
        lock (_lock)
        {
            if (gatewayLifetime.IsCancellationRequested)
            {
                device.Dispose();
                gatewayLifetime.ThrowIfCancellationRequested();
            }

            _devices.Add(deviceId, device);
        }
    }

    public List<GatewaySubDeviceBase> ToList()
    {
        lock (_lock) return [.. _devices.Values];
    }

    public T Get<T>(string deviceId) where T : GatewaySubDeviceBase =>
        TryGet(deviceId, out var device) ? device as T : null;

    public void DisposeAll()
    {
        // User-defined disposal runs outside the collection lock.
        ResourceCleanup.DisposeAll(ToList().Select(device => (Action)device.Dispose).ToArray());
    }
}
