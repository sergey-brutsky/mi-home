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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices;
using MiHomeLib.MqttGateway.ActionProcessors;


namespace MiHomeLib.MqttGateway;

public abstract class MqttGatewayBase : MiotGenericDevice, IDisposable
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
    private bool _subscribed;
    private readonly object _initializationLock = new();
    private readonly Dictionary<string, Queue<(IActionProcessor Processor, JsonElement Message, DateTime ReceivedAt, int Bytes)>> _initializingDevices = new();
    private TaskCompletionSource<Exception> _initializationFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _initializingMessages;
    private int _initializingBytes;
    internal int MaxInitializationMessages { get; set; } = 256;
    internal int MaxInitializationBytes { get; set; } = 8 * 1024 * 1024;
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private int _disposed;
    public event Func<GatewaySubDeviceBase, Task> OnDeviceDiscoveredAsync = (_) => Task.CompletedTask;
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
        _ = AsyncEvents.InvokeAsync(OnConnectionLostAsync, handler => handler(),
            exception => Logger.LogError(exception, "Connection event subscriber failed."));

    private void OnMqttConnectionRestored() =>
        _ = AsyncEvents.InvokeAsync(OnConnectionRestoredAsync, handler => handler(),
            exception => Logger.LogError(exception, "Connection event subscriber failed."));

    private void UnsubscribeConnectionEvents()
    {
        if (_mqttTransport is IMqttConnectionEvents events)
        {
            events.ConnectionLost -= OnMqttConnectionLost;
            events.ConnectionRestored -= OnMqttConnectionRestored;
        }
    }
    /// <summary>
    /// Same as <see cref="DiscoverDevicesAsync"/>, blocks until the discovery has finished.
    /// Exceptions are thrown as they are, not wrapped into an AggregateException
    /// </summary>
    public void DiscoverDevices() => RunSync(DiscoverDevicesAsync);

    public async Task DiscoverDevicesAsync()
    {
        await _discoveryLock.WaitAsync(_lifetimeToken).ConfigureAwait(false);
        try
        {
            _lifetimeToken.ThrowIfCancellationRequested();
            lock (_initializationLock)
                _initializationFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
            // The subscription and the found devices are kept for the gateway lifetime, so a repeated
            // discovery neither doubles every incoming message nor fails on devices it already knows
            if (!_subscribed)
            {
                _mqttTransport.OnMessageReceived += OnMqttMessageReceived;
                _subscribed = true;
            }

            await DiscoverZigbeeDevices().ConfigureAwait(false);
            ThrowIfInitializationFailed();
            _lifetimeToken.ThrowIfCancellationRequested();
            await DiscoverBleDevices().ConfigureAwait(false);
            _lifetimeToken.ThrowIfCancellationRequested();
            await DiscoverMeshDevices().ConfigureAwait(false);
            ThrowIfInitializationFailed();
        }
        finally
        {
            lock (_initializationLock) ClearInitializationBuffers();
            _discoveryLock.Release();
        }
    }

    private void OnMqttMessageReceived(string topic, string msg)
    {
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
        var zigbeeDeviceList = await (_devicesDiscoverer is IAsyncDevicesDiscoverer asyncDiscoverer
            ? asyncDiscoverer.DiscoverZigBeeDevicesAsync(_lifetimeToken)
            : RunBlockingAsync(_devicesDiscoverer.DiscoverZigBeeDevices)).ConfigureAwait(false);

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
                    await ReadInitialDevicePropertiesAsync(did, props, mappedDevice.SetInitialProperty).ConfigureAwait(false);
                ThrowIfInitializationFailed();
            }
            catch (Exception failure)
            {
                ResourceCleanup.PreserveFailure(failure, mihomeDevice.Dispose);
                throw;
            }

            AddDevice(did, mihomeDevice);

            await NotifyDiscoveredAsync(mihomeDevice).ConfigureAwait(false);
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

    private async Task NotifyDiscoveredAsync(GatewaySubDeviceBase device)
    {
        var failure = _initializationFailure.Task;
        var handlers = OnDeviceDiscoveredAsync;
        // A subscriber can block before returning its Task. Keep the discovery failure wait independent.
        var notification = Task.Run(() => AsyncEvents.InvokeAsync(handlers, handler => handler(device),
            exception => Logger.LogError(exception, "Device discovery subscriber failed."), _lifetimeToken), _lifetimeToken);
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
    private Task<T> RunBlockingAsync<T>(Func<T> read) => Task.Run(read, _lifetimeToken);

    private async Task ReadInitialDevicePropertiesAsync(string did, string[] properties, Action<string, JsonNode> setProperty)
    {
        object[] request = [did, .. properties];
        var response = await SendPropertyRequestAsync(did, request).ConfigureAwait(false);
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
            ThrowIfInitializationFailed();
            response = await SendPropertyRequestAsync(did, [did, properties[i]]).ConfigureAwait(false);
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
    private async Task<JsonNode> SendPropertyRequestAsync(string did, object[] parameters)
    {
        try
        {
            return JsonNode.Parse(await RequestArrayAsync("get_device_prop", parameters).ConfigureAwait(false));
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
        foreach (var device in await (_devicesDiscoverer is IAsyncDevicesDiscoverer asyncDiscoverer
            ? asyncDiscoverer.DiscoverBleDevicesAsync(_lifetimeToken)
            : RunBlockingAsync(_devicesDiscoverer.DiscoverBleDevices)).ConfigureAwait(false))
        {
            if (IsKnownDevice(device.Did)) continue;

            if (!ProtocolDeviceRegistry.TryCreateBle(device.Pdid, device.Did, LoggerFactory, out var protocolDevice))
            {
                Logger.LogWarning("Device with pdid '{Pdid}' is not supported yet. Please contribute to support.", device.Pdid);
                continue;
            }

            if (protocolDevice is not IProtocolMappedBleDevice bleDevice)
                throw new InvalidOperationException($"BLE device with pdid '{device.Pdid}' has no MAC address mapping.");

            bleDevice.SetMacAddress(DecodeMacAddress(device.Mac));
            AddDevice(device.Did, protocolDevice);
            Logger.LogInformation("Device '{Model}' with did '{Did}' has been successfully discovered", protocolDevice.GetType().Name, device.Did);
            await NotifyDiscoveredAsync(protocolDevice).ConfigureAwait(false);
        }
    }
    protected virtual async Task DiscoverMeshDevices()
    {
        foreach (var device in await (_devicesDiscoverer is IAsyncDevicesDiscoverer asyncDiscoverer
            ? asyncDiscoverer.DiscoverMeshDevicesAsync(_lifetimeToken)
            : RunBlockingAsync(_devicesDiscoverer.DiscoverMeshDevices)).ConfigureAwait(false))
        {
            if (IsKnownDevice(device.Did)) continue;

            if (!ProtocolDeviceRegistry.TryCreateMesh(device.Pdid, device.Did, LoggerFactory, out var protocolDevice))
            {
                Logger.LogWarning("Mesh device with pdid '{Pdid}' is not supported yet. Please contribute to support.", device.Pdid);
                continue;
            }

            if (protocolDevice is not IProtocolMappedMeshDevice meshDevice)
                throw new InvalidOperationException($"Mesh device with pdid '{device.Pdid}' has no transport mapping.");

            // mesh_device_v3 stores MAC already as aa:bb:cc:dd:ee:ff, unlike gateway_authed_table
            meshDevice.SetMacAddress(device.Mac);
            meshDevice.SetMqttTransport(_mqttTransport);
            AddDevice(device.Did, protocolDevice);
            Logger.LogInformation("Mesh device '{Model}' with did '{Did}' has been successfully discovered", protocolDevice.GetType().Name, device.Did);
            await NotifyDiscoveredAsync(protocolDevice).ConfigureAwait(false);
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
    private void AddDevice(string did, GatewaySubDeviceBase device) => _devices.Add(did, device, _lifetimeToken);

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
}
