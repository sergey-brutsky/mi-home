using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;

namespace MiHomeWorkerService;

public sealed class LeakNotifier : BackgroundService
{
    private readonly GatewayRegistry _registry;
    private readonly ILogger<LeakNotifier> _logger;

    public LeakNotifier(GatewayRegistry registry, ILogger<LeakNotifier> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _registry.OnDeviceEventAsync += HandleDeviceEventAsync;
        try { await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { _registry.OnDeviceEventAsync -= HandleDeviceEventAsync; }
    }

    private Task HandleDeviceEventAsync(string gateway, DeviceEvent deviceEvent)
    {
        if (deviceEvent.Device is AqaraWaterLeakSensor sensor &&
            deviceEvent.EventName == nameof(AqaraWaterLeakSensor.OnMoistureChangeAsync) &&
            sensor.Moisture)
        {
            _logger.LogWarning("Water leak detected by {DeviceId} on gateway {Gateway}", sensor.DeviceId, gateway);
        }
        else if (deviceEvent.EventName == nameof(GatewaySubDeviceBase.OnAvailabilityChangedAsync) &&
                 deviceEvent.Arguments.LastOrDefault() is false)
        {
            _logger.LogWarning("Device {DeviceId} is unavailable on gateway {Gateway}",
                deviceEvent.Device.DeviceId, gateway);
        }

        // Replace the log call with your notification (Telegram, e-mail, MQTT).
        return Task.CompletedTask;
    }
}
