using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway;
using MiHomeLib.XiaomiGateway2;

namespace MiHomeWorkerService;

public sealed class GatewayRegistry
{
    private readonly ConcurrentDictionary<string, IAsyncDisposable> _gateways = new(StringComparer.Ordinal);

    public event Func<string, DeviceEvent, Task> OnDeviceEventAsync;
    public IReadOnlyCollection<string> Names => _gateways.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();

    internal void Add(string name, IAsyncDisposable gateway)
    {
        if (!_gateways.TryAdd(name, gateway))
            throw new InvalidOperationException($"A gateway named '{name}' is already registered.");
    }

    public XiaomiGateway2 GetXiaomiGateway2(string name)
    {
        var gateway = GetGateway(name);
        return gateway as XiaomiGateway2 ??
            throw new InvalidOperationException($"Gateway '{name}' is not a Xiaomi Gateway 2.");
    }

    public MqttGatewayBase GetMqttGateway(string name)
    {
        var gateway = GetGateway(name);
        return gateway as MqttGatewayBase ??
            throw new InvalidOperationException($"Gateway '{name}' is not an MQTT gateway.");
    }

    internal IAsyncDisposable[] GetAllGateways() => _gateways.Values.ToArray();

    internal Task ForwardDeviceEventAsync(string name, DeviceEvent deviceEvent)
    {
        var handlers = OnDeviceEventAsync;
        if (handlers is null) return Task.CompletedTask;
        return Task.WhenAll(handlers.GetInvocationList()
            .Cast<Func<string, DeviceEvent, Task>>()
            .Select(handler => handler(name, deviceEvent)));
    }

    private IAsyncDisposable GetGateway(string name)
    {
        if (name is null || !_gateways.TryGetValue(name, out var gateway))
            throw new KeyNotFoundException($"Gateway '{name}' was not found.");
        return gateway;
    }
}
