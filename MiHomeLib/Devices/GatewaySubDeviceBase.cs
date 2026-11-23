using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// Common base class for all gateway sub-devices (both XiaomiGateway2 and MqttGateway hierarchies).
/// Contains shared properties and behavior: logging, last message timestamp, device identification.
/// </summary>
public abstract class GatewaySubDeviceBase : IDisposable
{
    private int _disposed;
    protected bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    protected bool BeginDispose() => Interlocked.Exchange(ref _disposed, 1) == 0;
    public virtual void Dispose() => BeginDispose();

    // Category is the concrete device type, so logging can be tuned per device
    protected readonly ILogger _logger;

    protected GatewaySubDeviceBase(ILoggerFactory loggerFactory) => _logger = loggerFactory.CreateLogger(GetType());

    /// <summary>
    /// Primary device identifier (Did for MqttGateway devices, Sid for XiaomiGateway2 devices)
    /// </summary>
    public abstract string DeviceId { get; }

    /// <summary>
    /// UTC time when the library received the last message from this device, null until the first message.
    /// </summary>
    public DateTime? LastTimeMessageReceived { get; internal set; }

    /// <summary>UTC time supplied by the source of the last message, null when the message has no source timestamp.</summary>
    public DateTime? LastMessageSourceTimeUtc { get; internal set; }

    /// <summary>
    /// Parse incoming data payload from the gateway
    /// </summary>
    internal abstract void ParseData(string command);

    private protected static void RunSync(Func<Task> asyncFunc) =>
        asyncFunc().ConfigureAwait(false).GetAwaiter().GetResult();

    // Borrowed elements are consumed synchronously while the gateway's JsonDocument is alive.
    // The string fallback preserves the contract of other GatewaySubDeviceBase implementations.
    internal virtual void ParseData(JsonElement data) => ParseData(data.GetRawText());

    internal virtual void ParseData(IEnumerable<JsonElement> data) => ParseData(JsonSerializer.Serialize(System.Linq.Enumerable.ToArray(data), MiHomeJsonContext.Default.JsonElementArray));

    /// <summary>
    /// Format base device info string with market model, model and device identifier
    /// </summary>
    protected virtual string GetBaseInfo(string marketModel, string model) => $"Device: {marketModel} {model} {DeviceId}, ";

    /// <summary>
    /// Assigns the value through <paramref name="assign"/> and raises the change event with the previous value
    /// </summary>
    private protected void SetAndRaise<T>(T oldValue, Action assign, Func<T, Task> handlers)
    {
        assign();
        RaiseEvent(handlers, oldValue);
    }

    protected void RaiseEvent(Func<Task> handlers) => _ = RaiseEventAsync(handlers);
    protected void RaiseEvent<T>(Func<T, Task> handlers, T value) => _ = RaiseEventAsync(handlers, value);
    protected void RaiseEvent<T1, T2>(Func<T1, T2, Task> handlers, T1 first, T2 second) =>
        _ = RaiseEventAsync(handlers, first, second);

    protected Task RaiseEventAsync(Func<Task> handlers) =>
        IsDisposed ? Task.CompletedTask : AsyncEvents.InvokeAsync(handlers, handler => handler(), LogEventError);
    protected Task RaiseEventAsync<T>(Func<T, Task> handlers, T value) =>
        IsDisposed ? Task.CompletedTask : AsyncEvents.InvokeAsync(handlers, handler => handler(value), LogEventError);
    protected Task RaiseEventAsync<T1, T2>(Func<T1, T2, Task> handlers, T1 first, T2 second) =>
        IsDisposed ? Task.CompletedTask : AsyncEvents.InvokeAsync(handlers, handler => handler(first, second), LogEventError);

    private void LogEventError(Exception e) => _logger.LogError(e, "Processing an event of device '{DeviceId}' failed", DeviceId);
}
