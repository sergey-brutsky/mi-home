using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway;

namespace MiHomeLib.Devices;

/// <summary>LLKZMK11LM dual relay, supported by Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraDualWirelessRelayCN : ProtocolMappedDeviceBase, IProtocolMappedManageableDevice
{
    public const string MARKET_MODEL = "LLKZMK11LM";
    public const string LUMI_MQTT_MODEL = "lumi.relay.c2acn01";
    public enum ChannelState
    {
        Unknown = -1,
        On = 1,
        Off = 0,
    }
    public enum PowerMemoryState
    {
        PowerOff = 0,
        Previous = 1,
    }
    public enum InterlockState
    {
        Unknown = -1,
        Enabled = 1,
        Disabled = 0,
    }
    private const string CHANNEL1_RES_NAME = "4.1.85";
    private const string CHANNEL2_RES_NAME = "4.2.85";
    private const string VOLTAGE_RES_NAME = "0.11.85";
    private const string LOAD_POWER_RES_NAME = "0.12.85";
    private const string ENERGY_RES_NAME = "0.13.85";
    private const string LOAD_CURRENT_RES_NAME = "0.14.85";
    private const string POWER_MEMORY_RES_NAME = "8.0.2030";
    private const string INTERLOCK_RES_NAME = "4.9.85";
    public ChannelState Channel1 { get; private set; } = ChannelState.Unknown;
    public ChannelState Channel2 { get; private set; } = ChannelState.Unknown;
    /// <summary>
    /// Voltage in volts
    /// </summary>
    public float Voltage { get; private set; }
    /// <summary>
    /// Load power in watt
    /// </summary>
    public float LoadPower { get; private set; }
    /// <summary>
    /// Consumed energy as reported by the gateway
    /// </summary>
    public float Energy { get; private set; }
    /// <summary>
    /// Load current in milliamperes
    /// </summary>
    public float Current { get; private set; }
    public InterlockState Interlock { get; private set; } = InterlockState.Unknown;
    /// <summary>Raised only when the channel state changes. The new state is in Channel1.</summary>
    public event Func<Task> OnChannel1StateChangeAsync = () => Task.CompletedTask;
    /// <summary>Raised only when the channel state changes. The new state is in Channel2.</summary>
    public event Func<Task> OnChannel2StateChangeAsync = () => Task.CompletedTask;
    /// <summary>Raised on every report, including unchanged values; passes the previous value. The new value is in LoadPower.</summary>
    public event Func<float, Task> OnLoadPowerChangeAsync = (_) => Task.CompletedTask;
    /// <summary>Raised on every report, including unchanged values; passes the previous value. The new value is in Voltage.</summary>
    public event Func<float, Task> OnVoltageChangeAsync = (_) => Task.CompletedTask;
    /// <summary>Raised on every report, including unchanged values; passes the previous value. The new value is in Energy.</summary>
    public event Func<float, Task> OnEnergyChangeAsync = (_) => Task.CompletedTask;
    /// <summary>Raised on every report, including unchanged values; passes the previous value. The new value is in Current.</summary>
    public event Func<float, Task> OnCurrentChangeAsync = (_) => Task.CompletedTask;
    private ZigBeeTransport _zigBeeTransport;
    public override string DeviceId { get; }
    public int ShortId { get; }
    public byte LinkQuality { get; private set; }
    public byte ChipTemperature { get; private set; }

    /// <summary>Raised on every report; passes the previous value. The new value is in LinkQuality.</summary>
    public event Func<byte, Task> OnLinkQualityChangeAsync = _ => Task.CompletedTask;
    /// <summary>Raised on every report; passes the previous value. The new value is in ChipTemperature.</summary>
    public event Func<byte, Task> OnChipTemperatureChangeAsync = _ => Task.CompletedTask;

    public AqaraDualWirelessRelayCN(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    void IProtocolMappedManageableDevice.SetMqttTransport(IMqttTransport mqttTransport) =>
        _zigBeeTransport = new ZigBeeTransport(mqttTransport);

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiMqtt(CHANNEL1_RES_NAME, "channel_0",
            value => UpdateChannel1(ToChannelState(value.ReadInt32())),
            value => Channel1 = StringToState(value.GetValue<string>()));
        map.LumiMqtt(CHANNEL2_RES_NAME, "channel_1",
            value => UpdateChannel2(ToChannelState(value.ReadInt32())),
            value => Channel2 = StringToState(value.GetValue<string>()));
        map.LumiMqtt(VOLTAGE_RES_NAME, "load_voltage",
            value => SetAndRaise(Voltage, () => Voltage = value.ReadInt32() / 1000f, OnVoltageChangeAsync),
            value => Voltage = value.GetValue<int>() / 1000f);
        map.LumiMqtt(LOAD_POWER_RES_NAME, "load_power",
            value => SetAndRaise(LoadPower, () => LoadPower = value.ReadSingle(), OnLoadPowerChangeAsync),
            value => LoadPower = value.ReadSingle());
        map.LumiMqtt(INTERLOCK_RES_NAME, "enable_motor_mode",
            value => Interlock = ToInterlockState(value.ReadInt32()),
            value => Interlock = ToInterlockState(value.GetValue<int>()));
        map.LumiMqtt("8.0.2007", "lqi",
            value => SetAndRaise(LinkQuality, () => LinkQuality = value.ReadByte(), OnLinkQualityChangeAsync),
            value => LinkQuality = checked((byte)value.GetValue<int>()));
        map.LumiMqtt("8.0.2006", "chip_temperature",
            value => SetAndRaise(ChipTemperature, () => ChipTemperature = value.ReadByte(), OnChipTemperatureChangeAsync),
            value => ChipTemperature = checked((byte)value.GetValue<int>()));
        map.LumiMqtt(ENERGY_RES_NAME, value => SetAndRaise(Energy, () => Energy = value.ReadSingle(), OnEnergyChangeAsync));
        map.LumiMqtt(LOAD_CURRENT_RES_NAME, value => SetAndRaise(Current, () => Current = value.ReadSingle(), OnCurrentChangeAsync));
        // Acknowledgement of a setting that is not exposed as a reported property.
        map.LumiMqtt(POWER_MEMORY_RES_NAME, _ => { });
    }

    private static ChannelState ToChannelState(int value) =>
        value == 1 ? ChannelState.On : value == 0 ? ChannelState.Off : ChannelState.Unknown;

    private static InterlockState ToInterlockState(int value) =>
        value == 1 ? InterlockState.Enabled : value == 0 ? InterlockState.Disabled : InterlockState.Unknown;

    private void UpdateChannel1(ChannelState value)
    {
        if (Channel1 == value) return;
        Channel1 = value;
        RaiseEvent(OnChannel1StateChangeAsync);
    }

    private void UpdateChannel2(ChannelState value)
    {
        if (Channel2 == value) return;
        Channel2 = value;
        RaiseEvent(OnChannel2StateChangeAsync);
    }







    private void SendWriteCommand(string resource, int value)
    {
        if (_zigBeeTransport is null)
            throw new NotSupportedException($"Device '{DeviceId}' has not been connected to a gateway, it cannot accept commands.");
        _zigBeeTransport.SendWriteCommand(DeviceId, resource, value);
    }

    private ChannelState StringToState(string state)
    {
        return state switch
        {
            "on" => ChannelState.On,
            "off" => ChannelState.Off,
            _ => ChannelState.Unknown
        };
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, LUMI_MQTT_MODEL) +
            $"Channel 1: {Channel1}, " +
            $"Channel 2: {Channel2}, " +
            $"Voltage: {Voltage}V, " +
            $"Load Power: {LoadPower}W, " +
            $"Interlock state: {Interlock}, " +
            $"ChipTemperature: {ChipTemperature}°C, LinkQuality: {LinkQuality}";
    }
    /// <summary>Sets channel 1 through Lumi MQTT and waits for a matching device write_ack. A timeout means the outcome is unknown.</summary>
    public Task SetChannel1StateConfirmedAsync(ChannelState state) => SetChannelStateConfirmedAsync(CHANNEL1_RES_NAME, state);

    /// <inheritdoc cref="SetChannel1StateConfirmedAsync"/>
    public void SetChannel1StateConfirmed(ChannelState state) => RunSync(() => SetChannel1StateConfirmedAsync(state));

    /// <summary>Sets channel 2 through Lumi MQTT and waits for a matching device write_ack. A timeout means the outcome is unknown.</summary>
    public Task SetChannel2StateConfirmedAsync(ChannelState state) => SetChannelStateConfirmedAsync(CHANNEL2_RES_NAME, state);

    /// <inheritdoc cref="SetChannel2StateConfirmedAsync"/>
    public void SetChannel2StateConfirmed(ChannelState state) => RunSync(() => SetChannel2StateConfirmedAsync(state));

    private Task SetChannelStateConfirmedAsync(string resource, ChannelState state)
    {
        if (state is not (ChannelState.On or ChannelState.Off))
            throw new ArgumentOutOfRangeException(nameof(state));
        if (_zigBeeTransport is null)
            throw new NotSupportedException($"Device '{DeviceId}' has not been connected to a gateway, it cannot accept commands.");
        return _zigBeeTransport.SendConfirmedWriteCommandAsync(DeviceId, resource, (int)state);
    }

    public void Channel1PowerOn() => RunSync(() => Channel1PowerOnAsync());

    /// <inheritdoc cref="Channel1PowerOn"/>
    public Task Channel1PowerOnAsync()
    {
        try
        {
            SendWriteCommand(CHANNEL1_RES_NAME, 1);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void Channel1PowerOff() => RunSync(() => Channel1PowerOffAsync());

    /// <inheritdoc cref="Channel1PowerOff"/>
    public Task Channel1PowerOffAsync()
    {
        try
        {
            SendWriteCommand(CHANNEL1_RES_NAME, 0);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void Channel1ToggleState() => RunSync(() => Channel1ToggleStateAsync());

    /// <inheritdoc cref="Channel1ToggleState"/>
    public Task Channel1ToggleStateAsync()
    {
        try
        {
            switch (Channel1)
            {
                case ChannelState.Off:
                    SendWriteCommand(CHANNEL1_RES_NAME, 1);
                    break;
                case ChannelState.On:
                    SendWriteCommand(CHANNEL1_RES_NAME, 0);
                    break;
            };
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void Channel2PowerOn() => RunSync(() => Channel2PowerOnAsync());

    /// <inheritdoc cref="Channel2PowerOn"/>
    public Task Channel2PowerOnAsync()
    {
        try
        {
            SendWriteCommand(CHANNEL2_RES_NAME, 1);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void Channel2PowerOff() => RunSync(() => Channel2PowerOffAsync());

    /// <inheritdoc cref="Channel2PowerOff"/>
    public Task Channel2PowerOffAsync()
    {
        try
        {
            SendWriteCommand(CHANNEL2_RES_NAME, 0);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void Channel2ToggleState() => RunSync(() => Channel2ToggleStateAsync());

    /// <inheritdoc cref="Channel2ToggleState"/>
    public Task Channel2ToggleStateAsync()
    {
        try
        {
            switch (Channel2)
            {
                case ChannelState.Off:
                    SendWriteCommand(CHANNEL2_RES_NAME, 1);
                    break;
                case ChannelState.On:
                    SendWriteCommand(CHANNEL2_RES_NAME, 0);
                    break;
            };
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    /// <summary>
    /// Restore or not previous state after electricity was disrupted and then appeared again
    /// </summary>
    public void SetPowerMemoryState(PowerMemoryState state) => RunSync(() => SetPowerMemoryStateAsync(state));

    /// <inheritdoc cref="SetPowerMemoryState"/>
    public Task SetPowerMemoryStateAsync(PowerMemoryState state)
    {
        try
        {
            SendWriteCommand(POWER_MEMORY_RES_NAME, (int)state);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    /// <summary>
    /// This function enables/disabled "only one channel can be enabled at a time" logic.
    /// If we enable channel1, channel2 will be automatically turned off off and vice versa
    /// </summary>
    public void SetInterlock(InterlockState state) => RunSync(() => SetInterlockAsync(state));

    /// <inheritdoc cref="SetInterlock"/>
    public Task SetInterlockAsync(InterlockState state)
    {
        try
        {
            SendWriteCommand(INTERLOCK_RES_NAME, (int)state);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
}
