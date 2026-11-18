using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.Helpers;
using MiHomeLib.MqttGateway.Discovery;
// Partial support for this device has been implemented on top of https://home.miot-spec.com/s/lumi.gateway.aqcn02
// Your contributions are appreciated
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.MqttGateway;

/// <summary>
/// Aqara Hub E1 (China) ZHWG16LM
/// </summary>
public class AqaraHubE1China : MqttGatewayBase
{
    private const int NIGHT_MODE_SERVICE = 6;
    private const int NIGHT_MODE_ENABLED = 1;
    private const int NIGHT_MODE_HOURS = 2;
    private const int HOTSPOT_SERVICE = 19;
    private const int HOTSPOT_ENABLED = 1;
    private const int HOTSPOT_NAME = 2;
    private const int HOTSPOT_PASSWORD = 3;
    private const int HOTSPOT_CLIENTS = 4;

    public const string MARKET_MODEL = "ZHWG16LM";
    public const string MODEL = "lumi.gateway.aqcn02";
    public AqaraHubE1China(string ip, string token, string did, int port = 1883) : this(ip, token, did, port, null) { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by MiHomeLogging.SetLoggerFactory</param>
    public AqaraHubE1China(string ip, string token, string did, int port, ILoggerFactory loggerFactory) :
        this(
                did,
                ResourceFactory.CreatePair<IMiioTransport, IMqttTransport>(
                    () => new MiioTransport(ip, token),
                    () => new MqttTransport(ip, port, [.. _zigbeeTopics], _zigbeeCommandsTopic, ResolveLoggerFactory(loggerFactory))),
                new CommonDevicesDiscoverer(ip, 23, "root", "/ # ", "/data/zigbee/device.info", ""),
                loggerFactory
        )
    { }

    private AqaraHubE1China(string did, (IMiioTransport First, IMqttTransport Second) transports,
        IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory)
        : this(did, transports.First, transports.Second, devicesDiscoverer, loggerFactory) { }

    internal AqaraHubE1China(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory = null)
        : base(did, miioTransport, mqttTransport, devicesDiscoverer, loggerFactory)
    {
    }

    // This gateway doesn't suppport BLE devices, no need to discover them
    protected sealed override Task DiscoverBleDevices() => Task.CompletedTask;

    // Mesh devices are not supported by this gateway either
    protected sealed override Task DiscoverMeshDevices() => Task.CompletedTask;

    /// <summary>
    /// Enable night mode (at night hours led will be turned off)
    /// </summary>
    public bool GetNightModeEnabled() => RunSync(GetNightModeEnabledAsync);

    /// <inheritdoc cref="GetNightModeEnabled"/>
    public async Task<bool> GetNightModeEnabledAsync()
    {
        return short.Parse(await GetMiotPropertyAsync(NIGHT_MODE_SERVICE, NIGHT_MODE_ENABLED, _did).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>Sets NightModeEnabled on the gateway.</summary>
    public void SetNightModeEnabled(bool value) => RunSync(() => SetNightModeEnabledAsync(value));

    /// <inheritdoc cref="SetNightModeEnabled"/>
    public Task SetNightModeEnabledAsync(bool value)
    {
        return SetMiotPropertyAsync(NIGHT_MODE_SERVICE, NIGHT_MODE_ENABLED, _did, value ? 1 : 0);
    }

    /// <summary>
    /// Setup night mode hours in the format hh:mm-hh:mm
    /// </summary>
    public (byte startHour, byte startMinute, byte endHour, byte endMinute) GetNightModeEffectiveHours() => RunSync(GetNightModeEffectiveHoursAsync);

    /// <inheritdoc cref="GetNightModeEffectiveHours"/>
    public async Task<(byte startHour, byte startMinute, byte endHour, byte endMinute)> GetNightModeEffectiveHoursAsync()
    {
        var time = GatewayTimeRange.Parse(await GetMiotPropertyAsync(NIGHT_MODE_SERVICE, NIGHT_MODE_HOURS, _did).ConfigureAwait(false));
        return ((byte)time.startHour, (byte)time.startMinute, (byte)time.endHour, (byte)time.endMinute);
    }

    /// <summary>Sets NightModeEffectiveHours on the gateway.</summary>
    public void SetNightModeEffectiveHours((byte startHour, byte startMinute, byte endHour, byte endMinute) value) => RunSync(() => SetNightModeEffectiveHoursAsync(value));

    /// <inheritdoc cref="SetNightModeEffectiveHours"/>
    public Task SetNightModeEffectiveHoursAsync((byte startHour, byte startMinute, byte endHour, byte endMinute) value)
    {
        var hours = GatewayTimeRange.Format(value.startHour, value.startMinute, value.endHour, value.endMinute);
        return SetMiotPropertyAsync(NIGHT_MODE_SERVICE, NIGHT_MODE_HOURS, _did, hours);
    }

    /// <summary>
    /// Enable hotspot mode (act as a wifi repiter)
    /// </summary>
    public bool GetHotspotModeEnabled() => RunSync(GetHotspotModeEnabledAsync);

    /// <inheritdoc cref="GetHotspotModeEnabled"/>
    public async Task<bool> GetHotspotModeEnabledAsync()
    {
        return bool.Parse(await GetMiotPropertyAsync(HOTSPOT_SERVICE, HOTSPOT_ENABLED, _did).ConfigureAwait(false));
    }

    /// <summary>Sets HotspotModeEnabled on the gateway.</summary>
    public void SetHotspotModeEnabled(bool value) => RunSync(() => SetHotspotModeEnabledAsync(value));

    /// <inheritdoc cref="SetHotspotModeEnabled"/>
    public Task SetHotspotModeEnabledAsync(bool value)
    {
        return SetMiotPropertyAsync(HOTSPOT_SERVICE, HOTSPOT_ENABLED, _did, value);
    }

    /// <summary>
    /// Change hotspot name (SSID)
    /// </summary>
    public string GetHotspotName() => RunSync(GetHotspotNameAsync);

    /// <inheritdoc cref="GetHotspotName"/>
    public async Task<string> GetHotspotNameAsync()
    {
        return await GetMiotPropertyAsync(HOTSPOT_SERVICE, HOTSPOT_NAME, _did).ConfigureAwait(false);
    }

    /// <summary>Sets HotspotName on the gateway.</summary>
    public void SetHotspotName(string value) => RunSync(() => SetHotspotNameAsync(value));

    /// <inheritdoc cref="SetHotspotName"/>
    public Task SetHotspotNameAsync(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (value.Length > 32) throw new ArgumentOutOfRangeException(nameof(value), value, "Length must be within 0-32 characters.");
        return SetMiotPropertyAsync(HOTSPOT_SERVICE, HOTSPOT_NAME, _did, value);
    }

    /// <summary>
    /// Change hotspot password 0-50 symbols
    /// </summary>
    public string GetHotspotPassword() => RunSync(GetHotspotPasswordAsync);

    /// <inheritdoc cref="GetHotspotPassword"/>
    public async Task<string> GetHotspotPasswordAsync()
    {
        return await GetMiotPropertyAsync(HOTSPOT_SERVICE, HOTSPOT_PASSWORD, _did).ConfigureAwait(false);
    }

    /// <summary>Sets HotspotPassword on the gateway.</summary>
    public void SetHotspotPassword(string value) => RunSync(() => SetHotspotPasswordAsync(value));

    /// <inheritdoc cref="SetHotspotPassword"/>
    public Task SetHotspotPasswordAsync(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (value.Length > 50) throw new ArgumentOutOfRangeException(nameof(value), value, "Length must be within 0-50 characters.");
        return SetMiotPropertyAsync(HOTSPOT_SERVICE, HOTSPOT_PASSWORD, _did, value);
    }

    /// <summary>
    /// Hotspot clients list in the format [(ip, mac, name)]
    /// </summary>
    public (string ip, string mac, string name)[] GetHotspotClients() => RunSync(GetHotspotClientsAsync);

    /// <inheritdoc cref="GetHotspotClients"/>
    public async Task<(string ip, string mac, string name)[]> GetHotspotClientsAsync() =>
        HotspotClientsParser.Parse(await GetMiotPropertyAsync(HOTSPOT_SERVICE, HOTSPOT_CLIENTS, _did).ConfigureAwait(false));

    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
