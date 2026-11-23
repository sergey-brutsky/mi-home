using MiHomeLib.Devices;
using MiHomeLib.MqttGateway;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeHardwareTests;

[CollectionDefinition("Hardware", DisableParallelization = true)]
public sealed class HardwareCollection : ICollectionFixture<HardwareFixture> { }

public sealed class HardwareFixture : IAsyncLifetime
{
    internal HardwareConfig Config { get; } = new();
    public XiaomiGateway2? Gateway2 { get; private set; }
    public MqttGatewayBase? Mqtt { get; private set; }
    private HardwareLoggerFactory? _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<GatewaySubDeviceBase, byte> _stateReports = new();
    internal bool HasStateReport(GatewaySubDeviceBase device) => _stateReports.ContainsKey(device);
    private Task Observe(GatewaySubDeviceBase device)
    {
        if (device is XiaomiMeshWallSwitchSingleOpen mesh)
            mesh.OnStateChangeAsync += _ => { _stateReports[device] = 1; return Task.CompletedTask; };
        return Task.CompletedTask;
    }
    public async Task InitializeAsync()
    {
        _logger = new HardwareLoggerFactory(Config);
        try
        {
            if (Config.HasGateway2)
            {
                Gateway2 = new XiaomiGateway2(Config.Gateway2Ip!, Config.Gateway2Token!, null, _logger);
                Gateway2.OnDeviceDiscoveredAsync += Observe;
                await Gateway2.DiscoverDevicesAsync();
            }
            if (Config.HasMqtt)
            {
                if (Config.MqttKind != "MultimodeGateway" && Config.MqttDid is null)
                    throw new InvalidOperationException("MIHOME_MQTT_DID is required for the selected gateway kind.");
                Mqtt = Config.MqttKind switch
                {
                    "MultimodeGateway" => new MultimodeGateway(Config.MqttIp!, Config.MqttToken!, Config.MqttDid ?? "", 1883, _logger),
                    "MultimodeGateway2China" => new MultimodeGateway2China(Config.MqttIp!, Config.MqttToken!, Config.MqttDid!, 1883, _logger),
                    "MultimodeGateway2Global" => new MultimodeGateway2Global(Config.MqttIp!, Config.MqttToken!, Config.MqttDid!, 1883, _logger),
                    "AqaraHubE1China" => new AqaraHubE1China(Config.MqttIp!, Config.MqttToken!, Config.MqttDid!, 1883, _logger),
                    _ => throw new InvalidOperationException("Unsupported MIHOME_MQTT_KIND.")
                };
                Mqtt.OnDeviceDiscoveredAsync += Observe;
                await Mqtt.DiscoverDevicesAsync();
            }
        }
        catch
        {
            await DisposeAsync();
            // Exceptions from a transport can contain credentials or raw packets; keep fixture output generic.
            throw new InvalidOperationException("Configured hardware initialization failed. Check connectivity and credentials locally.");
        }
    }
    public IEnumerable<GatewaySubDeviceBase> Devices => (Gateway2?.GetDevices() ?? []).Concat(Mqtt?.GetDevices() ?? []);
    public T? Find<T>() where T : GatewaySubDeviceBase => Devices.OfType<T>().FirstOrDefault();
    internal T? FindWritable<T>() where T : GatewaySubDeviceBase => Devices.OfType<T>().FirstOrDefault(d => Config.Writable.Contains(d.DeviceId));
    public Task DisposeAsync()
    {
        try { Mqtt?.Dispose(); }
        finally { try { Gateway2?.Dispose(); } finally { _logger?.Dispose(); } }
        Mqtt = null; Gateway2 = null; _logger = null;
        return Task.CompletedTask;
    }
}
