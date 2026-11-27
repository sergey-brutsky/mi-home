using MiHomeLib.Protocols;
using MiHomeLib.XiaomiGateway2;
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway;

namespace MiHomeLib.Devices;

/// <summary>ZNCZ02LM smart plug shared by Xiaomi Gateway 2 and Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.XiaomiGateway2, XIAOMI_GATEWAY2_MODEL)]
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class XiaomiMiSmartPowerPlugCN : ProtocolMappedDeviceBase, IProtocolMappedManageableDevice, IProtocolMappedGateway2ManageableDevice, ILinkQualityReporter, IPowerMeter, ISwitchable
{
    public const string MARKET_MODEL = "ZNCZ02LM";
    public const string XIAOMI_GATEWAY2_MODEL = "plug";
    public const string LUMI_MQTT_MODEL = "lumi.plug";

    public enum PlugState
    {
        Off = 0,
        On = 1,
    }

    public enum PowerMemoryState
    {
        PowerOff = 0,
        Previous = 1,
    }

    public enum ChargeProtect
    {
        Off = 0,
        On = 1, // when load is less then 2w for half an hour, plug will turn off automatically
    }

    public enum LedState
    {
        TurnOffAtNightTime = 0, // from mihome app night time is from 21:00 till 09:00
        AlwaysOn = 1,
    }

    private const string STATE_RES_NAME = "4.1.85";
    private const string POWER_ON_STATE_RES_NAME = "8.0.2030";
    private const string CHARGE_PROTECTION_RES_NAME = "8.0.2031";
    private const string LED_STATE_RES_NAME = "8.0.2032";

    private ZigBeeTransport _zigBeeTransport;
    private IGateway2Writer _gateway2;
    private const PlugState UnknownState = (PlugState)(-1);
    private volatile PlugState _state = UnknownState;

    public override string DeviceId { get; }
    public int ShortId { get; }
    public PlugState State => _state == PlugState.On ? PlugState.On : PlugState.Off;
    bool? ISwitchable.IsOn => HasKnownState ? State == PlugState.On : null;

    Task ISwitchable.TurnOnAsync() => PowerOnAsync();
    Task ISwitchable.TurnOffAsync() => PowerOffAsync();
    Task ISwitchable.ToggleAsync() => ToggleStateAsync();

    /// <summary>True after the gateway returns or reports a valid on/off state.</summary>
    public bool HasKnownState => _state != UnknownState;

    /// <summary>
    /// Load power in watts
    /// </summary>
    public float LoadPower { get; private set; }

    /// <summary>
    /// Read by Lumi MQTT gateways at discovery, always 0 on Xiaomi Gateway 2
    /// </summary>
    public int MaxPower { get; private set; }

    /// <summary>
    /// Consumed energy as Xiaomi Gateway 2 reports it, always 0 on Lumi MQTT gateways
    /// </summary>
    public int PowerConsumed { get; private set; }

    /// <summary>
    /// 1 when a load is connected, as Xiaomi Gateway 2 reports it, always 0 on Lumi MQTT gateways
    /// </summary>
    public int Inuse { get; private set; }

    /// <summary>
    /// Reported by Xiaomi Gateway 2 only, always 0 on Lumi MQTT gateways
    /// </summary>
    public float Voltage { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte LinkQuality { get; private set; }

    /// <summary>
    /// Reported by Lumi MQTT gateways only, always 0 on Xiaomi Gateway 2
    /// </summary>
    public byte ChipTemperature { get; private set; }

    /// <summary>
    /// Raised when the plug is turned on or off, only when the state really changes. The new state is in <see cref="State"/>
    /// </summary>
    public event Func<Task> OnStateChangeAsync = () => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the load power, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="LoadPower"/>
    /// </summary>
    public event Func<float, Task> OnLoadPowerChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the link quality, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="LinkQuality"/>
    /// </summary>
    public event Func<byte, Task> OnLinkQualityChangeAsync = _ => Task.CompletedTask;

    /// <summary>
    /// Raised on every report of the chip temperature, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="ChipTemperature"/>
    /// </summary>
    public event Func<byte, Task> OnChipTemperatureChangeAsync = _ => Task.CompletedTask;

    public XiaomiMiSmartPowerPlugCN(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    void IProtocolMappedManageableDevice.SetMqttTransport(IMqttTransport mqttTransport) => _zigBeeTransport = new ZigBeeTransport(mqttTransport);

    void IProtocolMappedGateway2ManageableDevice.SetGateway2Writer(IGateway2Writer writer) => _gateway2 = writer;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("status", value =>
        {
            switch (value.ReadString())
            {
                case "on": UpdateState(PlugState.On); break;
                case "off": UpdateState(PlugState.Off); break;
            }
        });
        map.XiaomiGateway2("load_power", value => { if (TryGetGateway2Float(value, out var power)) UpdateLoadPower(power); });
        map.XiaomiGateway2("power_consumed", value => { if (TryGetGateway2Float(value, out var consumed)) PowerConsumed = (int)consumed; });
        map.XiaomiGateway2("inuse", value => { if (TryGetGateway2Float(value, out var inuse)) Inuse = (int)inuse; });
        map.XiaomiGateway2("voltage", value => Voltage = value.ReadInt32() / 1000f);

        // Order of the initial properties matters, the gateway answers in the same order
        map.LumiMqtt(STATE_RES_NAME, "neutral_0",
            value =>
            {
                switch (value.ReadInt32())
                {
                    case 0: UpdateState(PlugState.Off); break;
                    case 1: UpdateState(PlugState.On); break;
                }
            },
            value =>
            {
                if (value is not JsonValue json || !json.TryGetValue<string>(out var status)) return;
                switch (status)
                {
                    case "off": _state = PlugState.Off; break;
                    case "on": _state = PlugState.On; break;
                }
            });
        map.LumiMqtt("0.12.85", "load_power",
            value => UpdateLoadPower(value.ReadSingle()),
            value => LoadPower = value.ReadSingle());
        map.LumiMqttInitial("max_power", value => MaxPower = value.GetValue<int>());
        map.LumiMqtt("8.0.2007", "lqi",
            value => SetAndRaise(nameof(OnLinkQualityChangeAsync), LinkQuality, () => LinkQuality = value.ReadByte(), OnLinkQualityChangeAsync),
            value => LinkQuality = checked((byte)value.GetValue<int>()));
        map.LumiMqtt("8.0.2006", "chip_temperature",
            value => SetAndRaise(nameof(OnChipTemperatureChangeAsync), ChipTemperature, () => ChipTemperature = value.ReadByte(), OnChipTemperatureChangeAsync),
            value => ChipTemperature = checked((byte)value.GetValue<int>()));
    }

    public void PowerOn() => RunSync(() => PowerOnAsync());

    /// <inheritdoc cref="PowerOn"/>
    public Task PowerOnAsync() => SetStateAsync(PlugState.On);

    /// <summary>Sets power through Lumi MQTT and waits for a matching device write_ack. A timeout means the outcome is unknown.</summary>
    public Task SetStateConfirmedAsync(PlugState state)
    {
        if (state is not (PlugState.On or PlugState.Off))
            throw new ArgumentOutOfRangeException(nameof(state));
        if (_zigBeeTransport is null)
            throw new NotSupportedException("Confirmed power commands require a Lumi MQTT gateway.");
        return _zigBeeTransport.SendConfirmedWriteCommandAsync(DeviceId, STATE_RES_NAME, (int)state);
    }

    /// <inheritdoc cref="SetStateConfirmedAsync"/>
    public void SetStateConfirmed(PlugState state) => RunSync(() => SetStateConfirmedAsync(state));

    public void PowerOff() => RunSync(() => PowerOffAsync());

    /// <inheritdoc cref="PowerOff"/>
    public Task PowerOffAsync() => SetStateAsync(PlugState.Off);

    public void ToggleState() => RunSync(() => ToggleStateAsync());

    /// <inheritdoc cref="ToggleState"/>
    public Task ToggleStateAsync() => SetStateAsync(State == PlugState.On ? PlugState.Off : PlugState.On);

    /// <summary>
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    public void SetPowerMemoryState(PowerMemoryState state) => RunSync(() => SetPowerMemoryStateAsync(state));

    /// <inheritdoc cref="SetPowerMemoryState"/>
    public Task SetPowerMemoryStateAsync(PowerMemoryState state)
    {
        try
        {
            SendMqttWriteCommand(POWER_ON_STATE_RES_NAME, (int)state);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    public void SetChargeProtection(ChargeProtect state) => RunSync(() => SetChargeProtectionAsync(state));

    /// <inheritdoc cref="SetChargeProtection"/>
    public Task SetChargeProtectionAsync(ChargeProtect state)
    {
        try
        {
            SendMqttWriteCommand(CHARGE_PROTECTION_RES_NAME, (int)state);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Supported by Lumi MQTT gateways only
    /// </summary>
    public void SetLedState(LedState state) => RunSync(() => SetLedStateAsync(state));

    /// <inheritdoc cref="SetLedState"/>
    public Task SetLedStateAsync(LedState state)
    {
        try
        {
            SendMqttWriteCommand(LED_STATE_RES_NAME, (int)state);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    private async Task SetStateAsync(PlugState state)
    {
        if (_gateway2 is not null)
        {
            await _gateway2.WriteAsync(DeviceId, XIAOMI_GATEWAY2_MODEL, ("status", state == PlugState.On ? "on" : "off")).ConfigureAwait(false);
            return;
        }

        SendMqttWriteCommand(STATE_RES_NAME, (int)state);
    }

    private void SendMqttWriteCommand(string resource, int value)
    {
        if (_zigBeeTransport is null)
            throw new NotSupportedException(_gateway2 is null
                ? $"Device '{DeviceId}' has not been connected to a gateway, it cannot accept commands."
                : $"This command of device '{DeviceId}' is supported only on Lumi MQTT gateways (Multimode Gateway, Aqara Hub E1).");

        _zigBeeTransport.SendWriteCommand(DeviceId, resource, value);
    }


    private void UpdateState(PlugState state)
    {
        var previous = State;
        _state = state;
        if (state != previous) RaiseEvent(nameof(OnStateChangeAsync), OnStateChangeAsync);
    }

    private void UpdateLoadPower(float value) => SetAndRaise(nameof(OnLoadPowerChangeAsync), LoadPower, () => LoadPower = value, OnLoadPowerChangeAsync);



    public override string ToString()
    {
        if (IsInitializedFor(DeviceProtocol.XiaomiGateway2))
            return $"Device: {MARKET_MODEL} {Model ?? XIAOMI_GATEWAY2_MODEL} {DeviceId}, State: {State}, Inuse: {Inuse}, " +
                   $"Load Power: {LoadPower}W, Power Consumed: {PowerConsumed}, Voltage: {Voltage}V";

        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return $"Device: {MARKET_MODEL} {Model ?? LUMI_MQTT_MODEL} {DeviceId}, State: {State}, Load Power: {LoadPower}W, " +
                   $"Max Power: {MaxPower}W, ChipTemperature: {ChipTemperature}°C, LinkQuality: {LinkQuality}";

        return $"Device: {MARKET_MODEL} {DeviceId}";
    }
}
