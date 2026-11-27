using MiHomeLib.Helpers;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using MiHomeLib.MqttGateway.Discovery;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices;
using MiHomeLib.MqttGateway.ActionProcessors;


namespace MiHomeLib.MqttGateway;

public abstract class MqttGatewayBase : MiotGenericDevice, IDisposable, IAsyncDisposable, IEventTaskTracker
{

    /// <summary>
    /// Factory the gateway was created with, also used by all its devices
    /// </summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Logger of this gateway, its category is the concrete gateway type
    /// </summary>
    protected ILogger Logger { get; }
    protected IDevicesDiscoverer _devicesDiscoverer;
    private readonly IMqttTransport _mqttTransport;
    private readonly Dictionary<string, IActionProcessor> _supportedActionProcessors;
    private readonly GatewayDevices _devices = new();
    private readonly ConcurrentDictionary<Task, byte> _pendingEvents = new();
    private bool _subscribed;
    private readonly object _initializationLock = new();
    private readonly Dictionary<string, Queue<(IActionProcessor Processor, JsonElement Message, DateTime ReceivedAt, int Bytes)>> _initializingDevices = new();
    internal int InitializingDeviceCount
    {
        get { lock (_initializationLock) return _initializingDevices.Count; }
    }
    private TaskCompletionSource<Exception> _initializationFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _initializingMessages;
    private int _initializingBytes;
    internal int MaxInitializationMessages { get; set; } = 256;
    internal int MaxInitializationBytes { get; set; } = 8 * 1024 * 1024;
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private CancellationTokenSource _discoveryCancellation;
    private int _disposed;
    private Task _availabilityLoop = Task.CompletedTask;
    internal Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;
    internal TimeSpan AvailabilityCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public event Func<GatewaySubDeviceBase, Task> OnDeviceDiscoveredAsync = (_) => Task.CompletedTask;
    /// <summary>
    /// Raised for every event of every sub-device after it has been discovered.
    /// Handlers of the device itself and of this event run concurrently.
    /// </summary>
    public event Func<DeviceEvent, Task> OnDeviceEventAsync = _ => Task.CompletedTask;
    /// <summary>Raised when the MQTT connection to the gateway is lost; the library reconnects by itself</summary>
    public event Func<Task> OnConnectionLostAsync = () => Task.CompletedTask;
    /// <summary>Raised when the MQTT connection to the gateway is restored after a loss</summary>
    public event Func<Task> OnConnectionRestoredAsync = () => Task.CompletedTask;
    protected static readonly string _zigbeeCommandsTopic = "zigbee/recv";
    protected static readonly string[] _zigbeeTopics = ["zigbee/send"];
    protected static readonly string[] _bleTopics = ["central/report"];
    protected MqttGatewayBase(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer)
        : this(did, miioTransport, mqttTransport, devicesDiscoverer, null) { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by SetLoggerFactory</param>
    /// <param name="devicesDiscoverer">Gateway device discovery provider.</param>
    /// <param name="did">Gateway device identifier.</param>
    /// <param name="miioTransport">Transport for miIO commands.</param>
    /// <param name="mqttTransport">Transport for MQTT messages.</param>
    protected MqttGatewayBase(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory) : base(did, miioTransport, 0, () => ResourceCleanup.DisposeAll(mqttTransport.Dispose, miioTransport.Dispose))
    {
        _lifetimeToken = _lifetime.Token;
        LoggerFactory = ResolveLoggerFactory(loggerFactory);
        Logger = LoggerFactory.CreateLogger(GetType());

        _supportedActionProcessors = new()
        {
            { ZigbeeReportCommandProcessor.ACTION, new ZigbeeReportCommandProcessor(_devices, LoggerFactory) },
            { ZigbeeHeartBeatCommandProcessor.ACTION, new ZigbeeHeartBeatCommandProcessor(_devices, LoggerFactory) },
            { ZigbeeReadResponseCommandProcessor.ACTION, new ZigbeeReadResponseCommandProcessor(_devices, LoggerFactory) },
            { AsyncBleEventMethodProcessor.ACTION, new AsyncBleEventMethodProcessor(_devices, LoggerFactory) },
            { EventOccuredMethodProcessor.ACTION, new EventOccuredMethodProcessor(_devices, LoggerFactory) },
            { PropertiesChangedMethodProcessor.ACTION, new PropertiesChangedMethodProcessor(_devices, LoggerFactory) },
        };

        _mqttTransport = mqttTransport;
        _devicesDiscoverer = devicesDiscoverer;
        if (mqttTransport is IMqttConnectionEvents events)
        {
            events.ConnectionLost += OnMqttConnectionLost;
            events.ConnectionRestored += OnMqttConnectionRestored;
        }
    }

    private void OnMqttConnectionLost() =>
        TrackEvent(AsyncEvents.InvokeAsync(OnConnectionLostAsync, handler => handler(),
            exception => Logger.LogError(exception, "Connection event subscriber failed."), this));

    private void OnMqttConnectionRestored() =>
        TrackEvent(AsyncEvents.InvokeAsync(OnConnectionRestoredAsync, handler => handler(),
            exception => Logger.LogError(exception, "Connection event subscriber failed."), this));

    private Task RaiseDeviceEventAsync(DeviceEvent deviceEvent) =>
        TrackAndReturn(AsyncEvents.InvokeAsync(OnDeviceEventAsync, handler => handler(deviceEvent),
            exception => Logger.LogError(exception, "Device event subscriber failed."), this));

    private void UnsubscribeConnectionEvents()
    {
        if (_mqttTransport is IMqttConnectionEvents events)
        {
            events.ConnectionLost -= OnMqttConnectionLost;
            events.ConnectionRestored -= OnMqttConnectionRestored;
        }
    }
    /// <summary>
    /// Same as <see cref="DiscoverDevicesAsync()"/>, blocks until the discovery has finished.
    /// Exceptions are thrown as they are, not wrapped into an AggregateException
    /// </summary>
    public void DiscoverDevices() => RunSync(DiscoverDevicesAsync);

    public Task DiscoverDevicesAsync() => DiscoverDevicesAsync(CancellationToken.None);

    /// <summary>Discovers sub-devices, or stops waiting when the supplied token is canceled.</summary>
    public async Task DiscoverDevicesAsync(CancellationToken cancellationToken)
    {
        using var discoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken, cancellationToken);
        var acquired = false;
        try
        {
            await _discoveryLock.WaitAsync(discoveryCancellation.Token).ConfigureAwait(false);
            acquired = true;
            Volatile.Write(ref _discoveryCancellation, discoveryCancellation);
            discoveryCancellation.Token.ThrowIfCancellationRequested();
            lock (_initializationLock)
                _initializationFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
            // The subscription and the found devices are kept for the gateway lifetime, so a repeated
            // discovery neither doubles every incoming message nor fails on devices it already knows
            if (!_subscribed)
            {
                _mqttTransport.OnMessageReceived += OnMqttMessageReceived;
                _subscribed = true;
                _availabilityLoop = Task.Run(() => RunAvailabilityLoopAsync(_lifetimeToken));
            }

            await DiscoverZigbeeDevices().ConfigureAwait(false);
            ThrowIfInitializationFailed();
            discoveryCancellation.Token.ThrowIfCancellationRequested();
            await DiscoverBleDevices().ConfigureAwait(false);
            discoveryCancellation.Token.ThrowIfCancellationRequested();
            await DiscoverMeshDevices().ConfigureAwait(false);
            ThrowIfInitializationFailed();
            discoveryCancellation.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            if (acquired)
            {
                lock (_initializationLock) ClearInitializationBuffers();
                Volatile.Write(ref _discoveryCancellation, null);
                _discoveryLock.Release();
            }
        }
    }

    private void OnMqttMessageReceived(string topic, string msg)
    {
        MiHomeMetrics.RecordMessageReceived("Mqtt");
        Logger.LogDebug("{Topic} --> {Message}", topic, msg);

        string actionField;

        switch (topic)
        {
            case var _ when _zigbeeTopics.Contains(topic):
                actionField = "cmd";
                break;
            case var _ when _bleTopics.Contains(topic):
                actionField = "method";
                break;
            default:
                MiHomeMetrics.RecordMessageDropped("Mqtt", "unsupported_topic");
                Logger.LogWarning("Topic '{Topic}' is not supported. Please contribute to support.", topic);
                return;
        }

        if (!MqttJson.TryRead(() => JsonDocument.Parse(msg), Logger, topic, out var document)) return;
        using (document)
        {
            if (!MqttJson.TryRead(() => MqttJson.String(MqttJson.Object(document.RootElement).GetProperty(actionField)),
                    Logger, topic, out var action)) return;
            if (!_supportedActionProcessors.TryGetValue(action, out var processor))
            {
                if (action is "write_ack" or "write_rsp") return;
                MiHomeMetrics.RecordMessageDropped("Mqtt", "malformed");
                Logger.LogWarning("Command/Method '{Action}' is unknown. Please contribute to support.", action);
                return;
            }

            // Discovery adds devices from the caller's thread while processors read them on the MQTT thread
            lock (_initializationLock)
            {
                if (_lifetimeToken.IsCancellationRequested) return;
                var root = document.RootElement;
                string did = null;
                if (root.TryGetProperty("did", out var id) && id.ValueKind == JsonValueKind.String)
                    did = id.GetString();
                else if (action == ZigbeeHeartBeatCommandProcessor.ACTION &&
                    root.TryGetProperty("params", out var items) && items.ValueKind == JsonValueKind.Array &&
                    items.GetArrayLength() == 1 && items[0].ValueKind == JsonValueKind.Object &&
                    items[0].TryGetProperty("did", out id) && id.ValueKind == JsonValueKind.String)
                    did = id.GetString();
                if (did is not null && _initializingDevices.TryGetValue(did, out var pending))
                {
                    if (_initializationFailure.Task.IsCompleted) return;
                    var bytes = Encoding.UTF8.GetByteCount(msg);
                    if (_initializingMessages >= MaxInitializationMessages || bytes > MaxInitializationBytes - _initializingBytes)
                    {
                        MiHomeMetrics.RecordMessageDropped("Mqtt", "buffer_full");
                        _initializationFailure.TrySetResult(new IOException("MQTT discovery buffer is full. Reports were lost; retry discovery."));
                        foreach (var queue in _initializingDevices.Values) queue.Clear();
                        _initializingMessages = 0;
                        _initializingBytes = 0;
                        return;
                    }
                    pending.Enqueue((processor, root.Clone(), MessageReception.UtcNow, bytes));
                    _initializingMessages++;
                    _initializingBytes += bytes;
                }
                else
                    processor.ProcessMessage(root);
            }
        }
    }

    protected virtual async Task DiscoverZigbeeDevices()
    {
        var cancellationToken = GetDiscoveryToken();
        var zigbeeDeviceList = await (_devicesDiscoverer is IAsyncDevicesDiscoverer asyncDiscoverer
            ? asyncDiscoverer.DiscoverZigBeeDevicesAsync(cancellationToken)
            : RunBlockingAsync(_devicesDiscoverer.DiscoverZigBeeDevices, cancellationToken)).ConfigureAwait(false);

        if (zigbeeDeviceList.Count == 0)
        {
            Logger.LogWarning("Gateway '{Ip}' has no connected zigbee devices", _miioTransport.Ip);
            return;
        }

        lock (_initializationLock)
        {
            foreach (var device in zigbeeDeviceList)
                if (!IsKnownDevice(device.Did) && ProtocolDeviceRegistry.IsSupported(DeviceProtocol.LumiMqtt, device.Model))
                    _initializingDevices[device.Did] = new();
        }
        foreach (var device in zigbeeDeviceList)
        {
            var model = device.Model;
            var did = device.Did;
            if (IsKnownDevice(did)) continue; // already discovered by a previous run

            if (!ProtocolDeviceRegistry.TryCreate(
                DeviceProtocol.LumiMqtt, model, did, 0, LoggerFactory, out var mihomeDevice))
            {
                MiHomeMetrics.RecordMessageDropped("Mqtt", "unsupported_model");
                Logger.LogWarning("Device '{Model}' is not supported yet. Please contribute to support", model);
                continue;
            }

            Logger.LogInformation("Device '{Model}' with did '{Did}' has been discovered", model, did);

            if (mihomeDevice is not ProtocolMappedDeviceBase mappedDevice)
                throw new InvalidOperationException($"Device model '{model}' has no protocol mapping.");
            if (mihomeDevice is IProtocolMappedManageableDevice manageableDevice)
                manageableDevice.SetMqttTransport(_mqttTransport);
            var props = mappedDevice.GetInitialPropertyNames();
            try
            {
                if (props.Length > 0)
                    await ReadInitialDevicePropertiesAsync(did, props, mappedDevice.SetInitialProperty, cancellationToken).ConfigureAwait(false);
                ThrowIfInitializationFailed();
            }
            catch (Exception failure)
            {
                ResourceCleanup.PreserveFailure(failure, mihomeDevice.Dispose);
                throw;
            }

            mihomeDevice.EventSink = RaiseDeviceEventAsync;
            mihomeDevice.MarkAdded(MessageReception.UtcNow);
            AddDevice(did, mihomeDevice);

            await NotifyDiscoveredAsync(mihomeDevice, cancellationToken).ConfigureAwait(false);
            lock (_initializationLock)
            {
                if (_initializingDevices.TryGetValue(did, out var pending))
                {
                    while (!_lifetimeToken.IsCancellationRequested && pending.Count > 0)
                    {
                        var message = pending.Dequeue();
                        _initializingMessages--;
                        _initializingBytes -= message.Bytes;
                        MessageReception.Run(message.ReceivedAt, () => message.Processor.ProcessMessage(message.Message));
                    }
                    _initializingDevices.Remove(did);
                }
            }
        }
    }

    private async Task NotifyDiscoveredAsync(GatewaySubDeviceBase device, CancellationToken cancellationToken)
    {
        var failure = _initializationFailure.Task;
        var handlers = OnDeviceDiscoveredAsync;
        // A subscriber can block before returning its Task. Keep the discovery failure wait independent.
        var notification = Task.Run(() => AsyncEvents.InvokeAsync(handlers, handler => handler(device),
            exception => Logger.LogError(exception, "Device discovery subscriber failed."), cancellationToken, this), cancellationToken);
        await Task.WhenAny(notification, failure).ConfigureAwait(false);
        ThrowIfInitializationFailed();
        await notification.ConfigureAwait(false);
    }

    private void ThrowIfInitializationFailed()
    {
        if (_initializationFailure.Task.IsCompleted) throw _initializationFailure.Task.Result;
    }

    // Caller holds _initializationLock.
    private void ClearInitializationBuffers()
    {
        _initializingDevices.Clear();
        _initializingMessages = 0;
        _initializingBytes = 0;
    }

    // Custom synchronous discoverers run on the thread pool to keep the caller responsive.
    private CancellationToken GetDiscoveryToken() =>
        Volatile.Read(ref _discoveryCancellation)?.Token ?? _lifetimeToken;

    private Task<T> RunBlockingAsync<T>(Func<T> read, CancellationToken cancellationToken) => Task.Run(read, cancellationToken);

    private async Task ReadInitialDevicePropertiesAsync(string did, string[] properties, Action<string, JsonNode> setProperty,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object[] request = [did, .. properties];
        var response = await SendPropertyRequestAsync(did, request, cancellationToken).ConfigureAwait(false);
        if (response is null) return;

        if (!IsSuccessfulMiioResponse(response))
        {
            Logger.LogError("Device '{Did}' is not responding on get_device_prop [{Properties}] or doesn't support miio protocol",
                did, string.Join(", ", properties));
            return;
        }

        if (TryGetMiioResultArray(response, out var results) && results.Count == properties.Length)
        {
            for (var i = 0; i < properties.Length; i++) setProperty(properties[i], results[i]);
            return;
        }

        Logger.LogWarning(
            "Device '{Did}' did not return all requested properties in one response; retrying properties individually",
            did);

        for (var i = 0; i < properties.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfInitializationFailed();
            response = await SendPropertyRequestAsync(did, [did, properties[i]], cancellationToken).ConfigureAwait(false);
            if (response is not null && TryGetMiioResultArray(response, out results) && results.Count > 0)
            {
                setProperty(properties[i], results[0]);
            }
            else
            {
                Logger.LogWarning("Cannot get property '{Property}' for device did '{Did}'", properties[i], did);
            }
        }

    }

    // The transport repeats lost requests itself, one call is enough. A device whose properties cannot be read
    // is still added: its values arrive with its next report, one silent device must not stop the discovery
    // null when the gateway did not answer
    private async Task<JsonNode> SendPropertyRequestAsync(string did, object[] parameters, CancellationToken cancellationToken)
    {
        try
        {
            return JsonNode.Parse(await RequestArrayAsync("get_device_prop", cancellationToken, parameters).ConfigureAwait(false));
        }
        catch (TimeoutException e)
        {
            Logger.LogWarning(
                "Gateway did not answer the request for the initial properties of device '{Did}', the device is added without them " +
                "and gets its values with its next report. {Reason}", did, e.Message);
            return null;
        }
    }

    private static bool IsSuccessfulMiioResponse(JsonNode response) =>
        response is JsonObject responseObject &&
        !responseObject.ContainsKey("error") &&
        responseObject.TryGetPropertyValue("code", out var codeNode) &&
        codeNode is JsonValue codeValue &&
        codeValue.TryGetValue<int>(out var code) &&
        code == 0;

    private static bool TryGetMiioResultArray(JsonNode response, out JsonArray results)
    {
        results = null;
        if (!IsSuccessfulMiioResponse(response) ||
            response is not JsonObject responseObject ||
            !responseObject.TryGetPropertyValue("result", out var resultNode) ||
            resultNode is not JsonArray resultArray ||
            resultArray.Any(value => value is null))
            return false;

        results = resultArray;
        return true;
    }

    protected virtual async Task DiscoverBleDevices()
    {
        var cancellationToken = GetDiscoveryToken();
        foreach (var device in await (_devicesDiscoverer is IAsyncDevicesDiscoverer asyncDiscoverer
            ? asyncDiscoverer.DiscoverBleDevicesAsync(cancellationToken)
            : RunBlockingAsync(_devicesDiscoverer.DiscoverBleDevices, cancellationToken)).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsKnownDevice(device.Did)) continue;

            if (!ProtocolDeviceRegistry.TryCreateBle(device.Pdid, device.Did, LoggerFactory, out var protocolDevice))
            {
                MiHomeMetrics.RecordMessageDropped("Mqtt", "unsupported_model");
                Logger.LogWarning("Device with pdid '{Pdid}' is not supported yet. Please contribute to support.", device.Pdid);
                continue;
            }

            if (protocolDevice is not IProtocolMappedBleDevice bleDevice)
                throw new InvalidOperationException($"BLE device with pdid '{device.Pdid}' has no MAC address mapping.");

            bleDevice.SetMacAddress(DecodeMacAddress(device.Mac));
            protocolDevice.EventSink = RaiseDeviceEventAsync;
            protocolDevice.MarkAdded(MessageReception.UtcNow);
            AddDevice(device.Did, protocolDevice);
            Logger.LogInformation("Device '{Model}' with did '{Did}' has been successfully discovered", protocolDevice.GetType().Name, device.Did);
            await NotifyDiscoveredAsync(protocolDevice, cancellationToken).ConfigureAwait(false);
        }
    }
    protected virtual async Task DiscoverMeshDevices()
    {
        var cancellationToken = GetDiscoveryToken();
        foreach (var device in await (_devicesDiscoverer is IAsyncDevicesDiscoverer asyncDiscoverer
            ? asyncDiscoverer.DiscoverMeshDevicesAsync(cancellationToken)
            : RunBlockingAsync(_devicesDiscoverer.DiscoverMeshDevices, cancellationToken)).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsKnownDevice(device.Did)) continue;

            if (!ProtocolDeviceRegistry.TryCreateMesh(device.Pdid, device.Did, LoggerFactory, out var protocolDevice))
            {
                MiHomeMetrics.RecordMessageDropped("Mqtt", "unsupported_model");
                Logger.LogWarning("Mesh device with pdid '{Pdid}' is not supported yet. Please contribute to support.", device.Pdid);
                continue;
            }

            if (protocolDevice is not IProtocolMappedMeshDevice meshDevice)
                throw new InvalidOperationException($"Mesh device with pdid '{device.Pdid}' has no transport mapping.");

            // mesh_device_v3 stores MAC already as aa:bb:cc:dd:ee:ff, unlike gateway_authed_table
            meshDevice.SetMacAddress(device.Mac);
            meshDevice.SetMqttTransport(_mqttTransport);
            protocolDevice.EventSink = RaiseDeviceEventAsync;
            protocolDevice.MarkAdded(MessageReception.UtcNow);
            AddDevice(device.Did, protocolDevice);
            Logger.LogInformation("Mesh device '{Model}' with did '{Did}' has been successfully discovered", protocolDevice.GetType().Name, device.Did);
            await NotifyDiscoveredAsync(protocolDevice, cancellationToken).ConfigureAwait(false);
        }
    }
    public string DecodeMacAddress(string mac)
    {
        if (mac is null) throw new ArgumentNullException(nameof(mac));
        if (mac.Length != 12 || mac.Any(character => !Uri.IsHexDigit(character)))
            throw new FormatException("Encoded BLE MAC address must contain exactly 12 hexadecimal characters.");
        var chunks = Enumerable
            .Range(0, mac.Length / 2)
            .Select(i => mac.Substring(i * 2, 2))
            .Reverse();

        return string.Join(":", chunks);
    }

    /// <summary>
    /// Enable lock to prevent accidental gateway deletion from you smart home system
    /// </summary>
    public bool GetAccidentalDeletionEnabled() => RunSync(GetAccidentalDeletionEnabledAsync);

    /// <summary>Sets the gateway's accidental-deletion lock.</summary>
    public void SetAccidentalDeletionEnabled(bool value) => RunSync(() => SetAccidentalDeletionEnabledAsync(value));

    /// <inheritdoc cref="GetAccidentalDeletionEnabled"/>
    public async Task<bool> GetAccidentalDeletionEnabledAsync() =>
        short.Parse(await GetMiotPropertyAsync(7, 1, _did).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;

    /// <inheritdoc cref="SetAccidentalDeletionEnabled"/>
    public Task SetAccidentalDeletionEnabledAsync(bool value) => SetMiotPropertyAsync(7, 1, _did, value ? 1 : 0);

    /// <summary>
    /// Sets the logger factory for gateways created afterwards without a factory of their own
    /// </summary>
    public static void SetLoggerFactory(ILoggerFactory value)
    {
        if (value is not null) MiHomeLogging.SetLoggerFactory(value);
    }
    private protected static ILoggerFactory ResolveLoggerFactory(ILoggerFactory loggerFactory) => MiHomeLogging.Resolve(loggerFactory);
    private bool IsKnownDevice(string did) => _devices.Contains(did);
    private void AddDevice(string did, GatewaySubDeviceBase device)
    {
        device.EventOwner = this;
        _devices.Add(did, device, _lifetimeToken);
    }

    internal void CheckAvailability()
    {
        if (_lifetimeToken.IsCancellationRequested) return;
        foreach (var device in _devices.ToList())
        {
            try { device.CheckAvailability(UtcNow()); }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Failed to check availability for device '{DeviceId}'.", device.DeviceId);
            }
        }
    }

    private async Task RunAvailabilityLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(AvailabilityCheckInterval, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                CheckAvailability();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    /// <summary>
    /// Sub-devices found so far
    /// </summary>
    public List<GatewaySubDeviceBase> GetDevices() => _devices.ToList();

    /// <summary>
    /// The sub-device with this id if it has been found and is of type <typeparamref name="T"/>, otherwise null
    /// </summary>
    public T GetDeviceById<T>(string deviceId) where T : GatewaySubDeviceBase => _devices.Get<T>(deviceId);

    /// <summary>
    /// Same as <see cref="GetDeviceById{T}"/>
    /// </summary>
    public T GetDeviceByDid<T>(string did) where T : GatewaySubDeviceBase => GetDeviceById<T>(did);
    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        ResourceCleanup.DisposeAll(
            _lifetime.Cancel,
            () => { lock (_initializationLock) ClearInitializationBuffers(); },
            () => _mqttTransport.OnMessageReceived -= OnMqttMessageReceived,
            UnsubscribeConnectionEvents,
            _devices.DisposeAll,
            _lifetime.Dispose,
            _mqttTransport.Dispose,
            () => base.Dispose());
    }

    /// <summary>
    /// Disposes the gateway and completes when its background work has stopped and running event handlers have returned;
    /// no event handler is invoked after this method completes. Called from inside an event handler of this gateway or
    /// one of its devices, it does not wait for handlers.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var devices = _devices.ToList();
        Dispose();

        var backgroundTasks = new List<Task> { _availabilityLoop };
        if (_mqttTransport is MqttTransport mqttTransport)
            backgroundTasks.Add(mqttTransport.Completion);
        await WaitForTasksAsync(backgroundTasks).ConfigureAwait(false);

        if (AsyncEvents.IsInsideEventHandlerFor(this)) return;
        await WaitForTasksAsync(_pendingEvents.Keys.ToArray().Concat(devices.Select(device => device.WhenEventsCompleted())))
            .ConfigureAwait(false);
    }

    void IEventTaskTracker.TrackEvent(Task task) => TrackEvent(task);

    private void TrackEvent(Task task)
    {
        if (task.IsCompleted) return;
        _pendingEvents.TryAdd(task, 0);
        _ = task.ContinueWith(completed => _pendingEvents.TryRemove(completed, out _),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private Task TrackAndReturn(Task task)
    {
        TrackEvent(task);
        return task;
    }

    private async Task WaitForTasksAsync(IEnumerable<Task> tasks)
    {
        try { await Task.WhenAll(tasks).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Logger.LogDebug(exception, "A gateway background task failed during disposal."); }
    }
}
