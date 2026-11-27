using MiHomeLib.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// Common base class for all gateway sub-devices (both XiaomiGateway2 and MqttGateway hierarchies).
/// Contains shared properties and behavior: logging, last message timestamp, device identification.
/// </summary>
public abstract class GatewaySubDeviceBase : IDisposable, IEventTaskTracker
{
    private int _disposed;
    private readonly ConcurrentDictionary<Task, byte> _pendingEvents = new();
    private readonly object _availabilityLock = new();
    private TimeSpan? _availabilityTimeout;
    private bool _availabilityTimeoutSet;
    private DateTime? _addedUtc;
    private int _isAvailable = 1;
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

    internal Func<DeviceEvent, Task> EventSink { get; set; }
    internal object EventOwner { get; set; }

    internal Task WhenEventsCompleted() => Task.WhenAll(_pendingEvents.Keys.ToArray());

    /// <summary>
    /// False after no message has come from the device for <see cref="AvailabilityTimeout"/>;
    /// true again with its next message. True for a device that has not been checked yet.
    /// </summary>
    public bool IsAvailable => Volatile.Read(ref _isAvailable) != 0;

    /// <summary>
    /// How long the device may stay silent before it is reported unavailable; null turns the check off.
    /// Defaults to three hours for Zigbee devices and null for BLE and Mesh devices.
    /// </summary>
    public TimeSpan? AvailabilityTimeout
    {
        get { lock (_availabilityLock) return _availabilityTimeout; }
        set
        {
            if (value is { } timeout && timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Availability timeout must be greater than zero.");
            lock (_availabilityLock)
            {
                _availabilityTimeout = value;
                _availabilityTimeoutSet = true;
            }
        }
    }

    /// <summary>Raised when <see cref="IsAvailable"/> changes; the new value is passed as an argument.</summary>
    public event Func<bool, Task> OnAvailabilityChangedAsync = _ => Task.CompletedTask;

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

    internal void InitializeAvailabilityTimeout(TimeSpan? defaultTimeout)
    {
        lock (_availabilityLock)
        {
            if (_availabilityTimeoutSet) return;
            _availabilityTimeout = defaultTimeout;
            _availabilityTimeoutSet = true;
        }
    }

    internal void MarkMessageReceived(DateTime receivedUtc) => MarkMessageReceived(receivedUtc, null, updateSourceTime: false);

    internal void MarkMessageReceived(DateTime receivedUtc, DateTime? sourceUtc) =>
        MarkMessageReceived(receivedUtc, sourceUtc, updateSourceTime: true);

    private void MarkMessageReceived(DateTime receivedUtc, DateTime? sourceUtc, bool updateSourceTime)
    {
        var becameAvailable = false;
        lock (_availabilityLock)
        {
            LastTimeMessageReceived = receivedUtc;
            if (updateSourceTime) LastMessageSourceTimeUtc = sourceUtc;
            becameAvailable = Interlocked.CompareExchange(ref _isAvailable, 1, 0) == 0;
        }

        if (!becameAvailable) return;
        MiHomeMetrics.RecordDeviceAvailabilityChanged(true);
        RaiseEvent(nameof(OnAvailabilityChangedAsync), OnAvailabilityChangedAsync, true);
    }

    internal void MarkAdded(DateTime utc)
    {
        lock (_availabilityLock) _addedUtc = utc;
    }

    internal void CheckAvailability(DateTime nowUtc)
    {
        var becameUnavailable = false;
        lock (_availabilityLock)
        {
            if (_availabilityTimeout is not { } timeout || Volatile.Read(ref _isAvailable) == 0)
                return;

            var lastMessageUtc = LastTimeMessageReceived ?? _addedUtc;
            if (lastMessageUtc is null || nowUtc - lastMessageUtc.Value <= timeout)
                return;

            becameUnavailable = Interlocked.CompareExchange(ref _isAvailable, 0, 1) == 1;
        }

        if (!becameUnavailable) return;
        MiHomeMetrics.RecordDeviceAvailabilityChanged(false);
        RaiseEvent(nameof(OnAvailabilityChangedAsync), OnAvailabilityChangedAsync, false);
    }

    /// <summary>
    /// Format base device info string with market model, model and device identifier
    /// </summary>
    protected virtual string GetBaseInfo(string marketModel, string model) => $"Device: {marketModel} {model} {DeviceId}, ";

    /// <summary>
    /// Assigns the value through <paramref name="assign"/> and raises the change event with the previous value
    /// </summary>
    private protected void SetAndRaise<T>(string eventName, T oldValue, Action assign, Func<T, Task> handlers)
    {
        assign();
        RaiseEvent(eventName, handlers, oldValue);
    }

    private protected void RaiseEvent(string eventName, Func<Task> handlers) => TrackEvent(RaiseEventAsync(eventName, handlers));
    private protected void RaiseEvent<T>(string eventName, Func<T, Task> handlers, T value) => TrackEvent(RaiseEventAsync(eventName, handlers, value));
    private protected void RaiseEvent<T1, T2>(string eventName, Func<T1, T2, Task> handlers, T1 first, T2 second) =>
        TrackEvent(RaiseEventAsync(eventName, handlers, first, second));

    private protected Task RaiseEventAsync(string eventName, Func<Task> handlers) =>
        RaiseEventAsync(eventName, Array.Empty<object>(), () => AsyncEvents.InvokeAsync(handlers, handler => handler(), LogEventError, EventOwner ?? this));
    private protected Task RaiseEventAsync<T>(string eventName, Func<T, Task> handlers, T value) =>
        RaiseEventAsync(eventName, new object[] { value }, () => AsyncEvents.InvokeAsync(handlers, handler => handler(value), LogEventError, EventOwner ?? this));
    private protected Task RaiseEventAsync<T1, T2>(string eventName, Func<T1, T2, Task> handlers, T1 first, T2 second) =>
        RaiseEventAsync(eventName, new object[] { first, second }, () =>
            AsyncEvents.InvokeAsync(handlers, handler => handler(first, second), LogEventError, EventOwner ?? this));

    protected void RaiseEvent(Func<Task> handlers) => TrackEvent(RaiseEventAsync(handlers));
    protected void RaiseEvent<T>(Func<T, Task> handlers, T value) => TrackEvent(RaiseEventAsync(handlers, value));
    protected void RaiseEvent<T1, T2>(Func<T1, T2, Task> handlers, T1 first, T2 second) =>
        TrackEvent(RaiseEventAsync(handlers, first, second));

    protected Task RaiseEventAsync(Func<Task> handlers) =>
        IsDisposed ? Task.CompletedTask : AsyncEvents.InvokeAsync(handlers, handler => handler(), LogEventError, EventOwner ?? this);
    protected Task RaiseEventAsync<T>(Func<T, Task> handlers, T value) =>
        IsDisposed ? Task.CompletedTask : AsyncEvents.InvokeAsync(handlers, handler => handler(value), LogEventError, EventOwner ?? this);
    protected Task RaiseEventAsync<T1, T2>(Func<T1, T2, Task> handlers, T1 first, T2 second) =>
        IsDisposed ? Task.CompletedTask : AsyncEvents.InvokeAsync(handlers, handler => handler(first, second), LogEventError, EventOwner ?? this);

    private Task RaiseEventAsync(string eventName, IReadOnlyList<object> arguments, Func<Task> invokeDeviceHandlers)
    {
        if (IsDisposed) return Task.CompletedTask;

        var deviceNotification = invokeDeviceHandlers();
        if (EventSink is not { } eventSink) return deviceNotification;

        var deviceEvent = new DeviceEvent(this, eventName, arguments, MessageReception.UtcNow);
        var gatewayNotification = AsyncEvents.InvokeAsync(eventSink, handler => handler(deviceEvent), LogEventError, EventOwner ?? this);
        return Task.WhenAll(deviceNotification, gatewayNotification);
    }

    void IEventTaskTracker.TrackEvent(Task task) => TrackEvent(task);

    private void TrackEvent(Task task)
    {
        if (task.IsCompleted) return;
        _pendingEvents.TryAdd(task, 0);
        _ = task.ContinueWith(completed => _pendingEvents.TryRemove(completed, out _),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void LogEventError(Exception e) => _logger.LogError(e, "Processing an event of device '{DeviceId}' failed", DeviceId);
}
