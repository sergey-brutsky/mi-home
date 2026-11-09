using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib;

/// <summary>
/// Common base class for all gateway sub-devices (both XiaomiGateway2 and MqttGateway hierarchies).
/// Contains shared properties and behavior: logging, last message timestamp, device identification.
/// </summary>
public abstract class GatewaySubDeviceBase
{
    // Category is the concrete device type, so logging can be tuned per device
    protected readonly ILogger _logger;

    protected GatewaySubDeviceBase(ILoggerFactory loggerFactory) => _logger = loggerFactory.CreateLogger(GetType());

    /// <summary>
    /// Primary device identifier (Did for MqttGateway devices, Sid for XiaomiGateway2 devices)
    /// </summary>
    public abstract string DeviceId { get; }

    /// <summary>
    /// Timestamp of the last received message from this device
    /// </summary>
    public DateTime LastTimeMessageReceived { get; internal set; }

    /// <summary>
    /// Parse incoming data payload from the gateway
    /// </summary>
    protected internal abstract void ParseData(string command);

    /// <summary>
    /// Format base device info string with market model, model and device identifier
    /// </summary>
    protected virtual string GetBaseInfo(string marketModel, string model) => $"Device: {marketModel} {model} {DeviceId}, ";

    /// <summary>
    /// Invokes a user event handler so that its exception is logged instead of escaping the library.
    /// Handlers used to be awaited from async void callbacks, where an exception terminates the process.
    /// The handler still starts synchronously, so ParseData returns at the same point as before
    /// </summary>
    protected void RaiseEvent(Func<Task> invoke)
    {
        Task task;

        try
        {
            task = invoke();
        }
        catch (Exception e)
        {
            LogEventError(e);
            return;
        }

        if (task is null || task.IsCanceled) return;

        if (task.IsFaulted)
        {
            LogEventError(task.Exception.GetBaseException());
            return;
        }

        if (!task.IsCompleted)
        {
            task.ContinueWith(
                t => LogEventError(t.Exception.GetBaseException()),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Same as <see cref="RaiseEvent"/>, for code that must continue only after the handler has finished
    /// </summary>
    protected async Task RaiseEventAsync(Func<Task> invoke)
    {
        try
        {
            var task = invoke();
            if (task is not null) await task.ConfigureAwait(false);
        }
        catch (Exception e)
        {
            LogEventError(e);
        }
    }

    private void LogEventError(Exception e) => _logger.LogError(e, "Processing an event of device '{DeviceId}' failed", DeviceId);
}