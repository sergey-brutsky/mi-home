using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.Helpers;
using MiHomeLib.MqttGateway.Discovery;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.MqttGateway;

public abstract class MultimodeGateway2Base : MqttGatewayBase
{
    public MultimodeGateway2Base(string ip, string token, string did, int port, string endMarker) : this(ip, token, did, port, endMarker, null) { }

    /// <param name="loggerFactory">Used by the gateway and all its devices; null takes the factory set by MiHomeLogging.SetLoggerFactory</param>
    public MultimodeGateway2Base(string ip, string token, string did, int port, string endMarker, ILoggerFactory loggerFactory) :
        this(
                did,
                ResourceFactory.CreatePair<IMiioTransport, IMqttTransport>(
                    () => new MiioTransport(ip, token),
                    () => new MqttTransport(ip, port, [.. _zigbeeTopics, .. _bleTopics], _zigbeeCommandsTopic, ResolveLoggerFactory(loggerFactory))),
                new CommonDevicesDiscoverer(ip, 23, "root", endMarker, "/data/zigbee/device.info", "/data/local/miio_bt/mible_local.db"),
                loggerFactory
        )
    { }

    private MultimodeGateway2Base(string did, (IMiioTransport First, IMqttTransport Second) transports,
        IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory)
        : this(did, transports.First, transports.Second, devicesDiscoverer, loggerFactory) { }

    // For unit tests only
    protected MultimodeGateway2Base(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer) : base(did, miioTransport, mqttTransport, devicesDiscoverer) { }
    private protected MultimodeGateway2Base(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer, ILoggerFactory loggerFactory)
        : base(did, miioTransport, mqttTransport, devicesDiscoverer, loggerFactory) { }
    
    public enum AccessModeValue
    {
        LAN = 0,
        Wireless2G = 1,
        Wireless5G = 2,
    }

    /// <summary>
    /// Gateway connection type: LAN, Wireless 2G, Wireless 5G
    /// </summary>
    public AccessModeValue GetAccessMode() => RunSync(GetAccessModeAsync);

    /// <inheritdoc cref="GetAccessMode"/>
    public async Task<AccessModeValue> GetAccessModeAsync()
    {
        return (AccessModeValue)ushort.Parse(await GetMiotPropertyAsync(2, 1, _did).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    /// <summary>Sets AccessMode on the gateway.</summary>
    public void SetAccessMode(AccessModeValue value) => RunSync(() => SetAccessModeAsync(value));

    /// <inheritdoc cref="SetAccessMode"/>
    public Task SetAccessModeAsync(AccessModeValue value)
    {
        return SetMiotPropertyAsync(2, 1, _did, value);
    }

    /// <summary>
    /// Do not distirb mode enable/disable, affects only led indicator glow in specific hours
    /// </summary>
    public bool GetDoNotDisturbModeEnabled() => RunSync(GetDoNotDisturbModeEnabledAsync);

    /// <inheritdoc cref="GetDoNotDisturbModeEnabled"/>
    public async Task<bool> GetDoNotDisturbModeEnabledAsync()
    {
        return int.Parse(await GetMiotPropertyAsync(6, 1, _did).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }

    /// <summary>Sets DoNotDisturbModeEnabled on the gateway.</summary>
    public void SetDoNotDisturbModeEnabled(bool value) => RunSync(() => SetDoNotDisturbModeEnabledAsync(value));

    /// <inheritdoc cref="SetDoNotDisturbModeEnabled"/>
    public Task SetDoNotDisturbModeEnabledAsync(bool value)
    {
        return SetMiotPropertyAsync(6, 1, _did, value ? 1 : 0);
    }

    /// <summary>
    /// Time when led indicator will be off when do not disturb mode enabled
    /// </summary>
    public (ushort startHour, ushort startMinute, ushort endHour, ushort endMinute) GetDoNotDisturbEffectiveTime() => RunSync(GetDoNotDisturbEffectiveTimeAsync);

    /// <inheritdoc cref="GetDoNotDisturbEffectiveTime"/>
    public async Task<(ushort startHour, ushort startMinute, ushort endHour, ushort endMinute)> GetDoNotDisturbEffectiveTimeAsync()
    {
        return GatewayTimeRange.Parse(await GetMiotPropertyAsync(6, 2, _did).ConfigureAwait(false));
    }

    /// <summary>Sets DoNotDisturbEffectiveTime on the gateway.</summary>
    public void SetDoNotDisturbEffectiveTime((ushort startHour, ushort startMinute, ushort endHour, ushort endMinute) value) => RunSync(() => SetDoNotDisturbEffectiveTimeAsync(value));

    /// <inheritdoc cref="SetDoNotDisturbEffectiveTime"/>
    public Task SetDoNotDisturbEffectiveTimeAsync((ushort startHour, ushort startMinute, ushort endHour, ushort endMinute) value)
    {
        var hours = GatewayTimeRange.Format(value.startHour, value.startMinute, value.endHour, value.endMinute);
        return SetMiotPropertyAsync(6, 2, _did, hours);
    }

    /// <summary>
    /// Led indicator brightness is in range 0 - 100
    /// </summary>
    public ushort GetLedIndicatorBrightness() => RunSync(GetLedIndicatorBrightnessAsync);

    /// <inheritdoc cref="GetLedIndicatorBrightness"/>
    public async Task<ushort> GetLedIndicatorBrightnessAsync()
    {
        return ushort.Parse(await GetMiotPropertyAsync(6, 3, _did).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    /// <summary>Sets LedIndicatorBrightness on the gateway.</summary>
    public void SetLedIndicatorBrightness(ushort value) => RunSync(() => SetLedIndicatorBrightnessAsync(value));

    /// <inheritdoc cref="SetLedIndicatorBrightness"/>
    public Task SetLedIndicatorBrightnessAsync(ushort value)
    {
        if (value > 100) throw new ArgumentOutOfRangeException(nameof(value), value, "Brightness must be within 0-100.");
        return SetMiotPropertyAsync(6, 3, _did, value);
    }
}
