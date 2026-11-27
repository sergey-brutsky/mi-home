using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiHomeLib.Devices;
using MiHomeLib.MqttGateway;
using MiHomeLib.XiaomiGateway2;

namespace MiHomeWorkerService;

public sealed class GatewayHostedService : BackgroundService
{
    private readonly MiHomeOptions _options;
    private readonly GatewayRegistry _registry;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GatewayHostedService> _logger;

    public GatewayHostedService(IOptions<MiHomeOptions> options, GatewayRegistry registry,
        ILoggerFactory loggerFactory, ILogger<GatewayHostedService> logger)
    {
        _options = options.Value;
        _registry = registry;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startups = _options.Gateways.Select(gateway =>
            Task.Run(() => InitializeGatewayAsync(gateway, stoppingToken)));
        await Task.WhenAll(startups).ConfigureAwait(false);
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(_registry.GetAllGateways()
            .Select(gateway => gateway.DisposeAsync().AsTask())).ConfigureAwait(false);
    }

    private async Task InitializeGatewayAsync(GatewayOptions options, CancellationToken stoppingToken)
    {
        IAsyncDisposable gateway = null;
        try
        {
            stoppingToken.ThrowIfCancellationRequested();
            gateway = CreateGateway(options);
            SubscribeToEvents(options.Name, gateway);
            await DiscoverDevicesAsync(gateway, stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            _registry.Add(options.Name, gateway);
            gateway = null;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            if (gateway is not null) await gateway.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to initialize gateway '{GatewayName}'.", options.Name);
            if (gateway is not null)
            {
                try { await gateway.DisposeAsync().ConfigureAwait(false); }
                catch (Exception disposalException)
                {
                    _logger.LogError(disposalException, "Failed to dispose gateway '{GatewayName}' after initialization failed.", options.Name);
                }
            }
        }
    }

    private IAsyncDisposable CreateGateway(GatewayOptions options) =>
        options.Kind switch
        {
            GatewayKind.XiaomiGateway2 =>
                new XiaomiGateway2(options.Ip, options.Token, options.GatewaySid, _loggerFactory),
            GatewayKind.MultimodeGateway =>
                new MultimodeGateway(options.Ip, options.Token, options.Did, options.MqttPort, _loggerFactory),
            GatewayKind.MultimodeGateway2China =>
                new MultimodeGateway2China(options.Ip, options.Token, options.Did, options.MqttPort, _loggerFactory),
            GatewayKind.MultimodeGateway2Global =>
                new MultimodeGateway2Global(options.Ip, options.Token, options.Did, options.MqttPort, _loggerFactory),
            GatewayKind.AqaraHubE1China =>
                new AqaraHubE1China(options.Ip, options.Token, options.Did, options.MqttPort, _loggerFactory),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Kind), options.Kind, "Unsupported gateway kind."),
        };

    private void SubscribeToEvents(string name, IAsyncDisposable gateway)
    {
        if (gateway is XiaomiGateway2 gateway2)
        {
            gateway2.OnDeviceDiscoveredAsync += LogDiscoveredDeviceAsync;
            gateway2.OnDeviceEventAsync += deviceEvent => _registry.ForwardDeviceEventAsync(name, deviceEvent);
        }
        else if (gateway is MqttGatewayBase mqttGateway)
        {
            mqttGateway.OnDeviceDiscoveredAsync += LogDiscoveredDeviceAsync;
            mqttGateway.OnDeviceEventAsync += deviceEvent => _registry.ForwardDeviceEventAsync(name, deviceEvent);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported gateway implementation '{gateway.GetType().FullName}'.");
        }
    }

    private Task LogDiscoveredDeviceAsync(GatewaySubDeviceBase device)
    {
        _logger.LogInformation("Discovered {Device}", device.ToString());
        return Task.CompletedTask;
    }

    private static Task DiscoverDevicesAsync(IAsyncDisposable gateway, CancellationToken cancellationToken) =>
        gateway switch
        {
            XiaomiGateway2 gateway2 => gateway2.DiscoverDevicesAsync(cancellationToken),
            MqttGatewayBase mqttGateway => mqttGateway.DiscoverDevicesAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported gateway implementation '{gateway.GetType().FullName}'."),
        };
}
