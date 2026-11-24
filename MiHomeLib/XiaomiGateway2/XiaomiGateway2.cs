using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.Helpers;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.XiaomiGateway2;

/// <summary>
/// Xiaomi Gateway (CN) DGNWG02LM
/// </summary>
public partial class XiaomiGateway2 : MiioDevice, IDisposable, IGateway2Writer
{
    public const string MARKET_MODEL = "DGNWG02LM";
    public const string MODEL = "lumi.gateway.v3";
    // The model the gateway reports itself with in the UDP protocol, next to its sub-devices
    private const string GATEWAY_SUB_DEVICE_MODEL = "gateway";

    /// <summary>
    /// Id of the gateway in its UDP protocol, known after the discovery
    /// </summary>
    public string Sid { get; private set; }

    /// <summary>
    /// Illumination measured by the gateway, 0 until the first report
    /// </summary>
    public int Illumination { get; private set; }

    /// <summary>
    /// Current color and brightness of the gateway light as the gateway reports it: brightness &lt;&lt; 24 | r &lt;&lt; 16 | g &lt;&lt; 8 | b
    /// </summary>
    public int Rgb { get; private set; }

    public string ProtoVersion { get; private set; }

    /// <summary>
    /// IP address the gateway reports in its heartbeat
    /// </summary>
    public string Ip { get; private set; }

    /// <summary>
    /// Raised on every report of the illumination, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Illumination"/>
    /// </summary>
    public event Func<int, Task> OnIlluminationChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the light color, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Rgb"/>
    /// </summary>
    public event Func<int, Task> OnRgbChangeAsync = _ => Task.CompletedTask;
    private readonly string _gatewaySid;
    private readonly ILoggerFactory _gatewayLoggerFactory;
    private readonly ILogger _logger;
    private bool _subscribed;
    private readonly SemaphoreSlim _discoveryLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly ConcurrentQueue<(string Text, DateTime ReceivedAt, int Bytes)> _messages = new();
    private int _messageBytes;
    private Exception _messageBufferFailure;
    internal int MaxPendingMessages { get; set; } = 256;
    internal int MaxPendingMessageBytes { get; set; } = 8 * 1024 * 1024;
    private readonly SemaphoreSlim _messagesAvailable = new(0);
    private readonly object _messagesLock = new();
    private Task _messageLoop = Task.CompletedTask;
    internal Task MessageProcessingCompletion => _messageLoop;
    private int _disposed;
    // Requested on the first write command and cached, null until then: reading devices,
    // the light, sounds and arming do not need the developer mode of the gateway
    private string _developerKey;
    private readonly SemaphoreSlim _developerKeyLock = new(1, 1);

    // Set while DiscoverDevicesAsync waits for the gateway to answer, null otherwise
    // its result is the task reading the devices, started once the gateway has answered
    private TaskCompletionSource<Task> _asyncDiscovery;
    internal TimeSpan DiscoveryTimeout { get; set; } = TimeSpan.FromSeconds(10);
    private readonly IGateway2Transport _messageTransport;
    private readonly GatewayDevices _devices = new();

    internal TimeSpan ReadDeviceInterval { get; set; } = TimeSpan.FromMilliseconds(25);
    internal TimeSpan ReadAnswerTimeout { get; set; } = TimeSpan.FromSeconds(1);
    internal Func<TimeSpan, CancellationToken, Task> ReadAnswerDelay { get; set; } = Task.Delay;
    internal int ReadAttempts { get; set; } = 3;
    // Sids whose read request has not been answered yet
    private readonly ConcurrentDictionary<string, byte> _awaitedReads = new();
    private TaskCompletionSource<bool> _allReadsAnswered;
    public enum Sound
    {
        PoliceСar1 = 0,
        PoliceСar2 = 1,
        Accident = 2,
        Countdown = 3,
        Ghost = 4,
        SniperRifle = 5,
        Battle = 6,
        AirRaid = 7,
        Bark = 8,
        Doorbell = 10,
        KnockAtDoor = 11,
        Amuse = 12,
        AlarmClock = 13,
        Mimix = 20,
        Enthusiastic = 21,
        GuitarClassic = 22,
        IceWorldPiano = 23,
        LeisureTime = 24,
        ChildHood = 25,
        MorningStreamList = 26,
        MusicBox = 27,
        Orange = 28,
        Thinker = 29,
        Custom1 = 10001,
        Custom2 = 10002,
        Custom3 = 10003,
    }
    public event Func<GatewaySubDeviceBase, Task> OnDeviceDiscoveredAsync = (_) => Task.CompletedTask;
    public XiaomiGateway2(string ip, string token, string gatewaySid = null)
        : this(ip, token, gatewaySid, null) { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by MiHomeLogging.SetLoggerFactory</param>
    /// <param name="gatewaySid">Optional gateway SID used to select this hub among multicast responses.</param>
    /// <param name="ip">Gateway IP address.</param>
    /// <param name="token">Gateway miIO token.</param>
    public XiaomiGateway2(string ip, string token, string gatewaySid, ILoggerFactory loggerFactory)
        : this(ResourceFactory.CreatePair<IMiioTransport, IGateway2Transport>(
            () => new MiioTransport(ip, token),
            () => new UdpTransport(loggerFactory: MiHomeLogging.Resolve(loggerFactory))), gatewaySid, loggerFactory) { }

    private XiaomiGateway2((IMiioTransport First, IGateway2Transport Second) transports, string gatewaySid, ILoggerFactory loggerFactory)
        : this(transports.First, transports.Second, gatewaySid, 0, loggerFactory) { }

    internal XiaomiGateway2(IMiioTransport miioTransport, IGateway2Transport messageTransport, string gatewaySid, int externalId = 0, ILoggerFactory loggerFactory = null) : base(miioTransport, externalId)
    {
        _lifetimeToken = _lifetime.Token;
        _gatewayLoggerFactory = MiHomeLogging.Resolve(loggerFactory);
        _logger = _gatewayLoggerFactory.CreateLogger(GetType());
        _messageTransport = messageTransport;
        _gatewaySid = gatewaySid;

    }

    /// <summary>
    /// Sets the logger factory for gateways created afterwards without a factory of their own
    /// </summary>
    public static void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        if (loggerFactory is not null) MiHomeLogging.SetLoggerFactory(loggerFactory);
    }

    /// <summary>
    /// Sub-devices found so far
    /// </summary>
    public List<GatewaySubDeviceBase> GetDevices() => _devices.ToList();

    /// <summary>
    /// The sub-device with this id (sid) if it has been found and is of type <typeparamref name="T"/>, otherwise null
    /// </summary>
    public T GetDeviceById<T>(string deviceId) where T : GatewaySubDeviceBase => _devices.Get<T>(deviceId);

    async Task IGateway2Writer.WriteAsync(string sid, string model, params (string Key, string Value)[] data)
    {
        await _developerKeyLock.WaitAsync(_lifetimeToken).ConfigureAwait(false);
        try
        {
            // Keep key changes out until the UDP command using this key has been sent.
            _developerKey ??= await GetDeveloperKeyAsync().ConfigureAwait(false);
            _lifetimeToken.ThrowIfCancellationRequested();
            _messageTransport.SendWrite(sid, model, _developerKey, data);
        }
        finally { _developerKeyLock.Release(); }
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        ResourceCleanup.DisposeAll(
            StopMessageProcessing,
            _lifetime.Dispose,
            _messageTransport.Dispose,
            _devices.DisposeAll,
            () => base.Dispose());
    }

    private void StopMessageProcessing()
    {
        lock (_messagesLock)
        {
            ResourceCleanup.DisposeAll(
                _lifetime.Cancel,
                () => _messageTransport.OnMessageReceived -= OnMessageReceived,
                () =>
                {
                    _ = _messageLoop.ContinueWith(completed =>
                    {
                        lock (_messagesLock)
                        {
                            while (_messages.TryDequeue(out _)) { }
                            _messageBytes = 0;
                        }
                        _messagesAvailable.Dispose();
                    }, TaskScheduler.Default);
                });
        }
    }

    public override string ToString() =>
        $"Model: {MARKET_MODEL} {MODEL}, Sid: {Sid}, Illumination: {Illumination}, Rgb: {Rgb}, ProtoVersion: {ProtoVersion}";
}
