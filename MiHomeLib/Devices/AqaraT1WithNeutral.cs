using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway;

namespace MiHomeLib.Devices;

/// <summary>SSM-U01 relay with neutral, supported by Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.LumiMqtt, LUMI_MQTT_MODEL)]
public sealed class AqaraT1WithNeutral : ProtocolMappedDeviceBase, IProtocolMappedManageableDevice, IPowerMeter, ISwitchable
{
    public const string MARKET_MODEL = "SSM-U01";
    public const string LUMI_MQTT_MODEL = "lumi.switch.n0agl1";
    public enum RelayState
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
    public enum PowerMode
    {
        Momentary = 1,
        Toggle = 2,
    }
    private static readonly (int siid, int piid) STATE_RES = (2, 1);
    private static readonly (int siid, int piid) LOAD_POWER_RES = (3, 2);
    private static readonly (int siid, int piid) POWER_CONSUMPTION_RES = (3, 1);
    private static readonly (int siid, int piid) POWER_MEMORY_RES = (5, 1);
    private static readonly (int siid, int piid) POWER_MODE_RES = (7, 2);
    private static readonly (int siid, int piid) POWER_OVERLOAD_RES = (5, 6);
    private ZigBeeTransport _zigBeeTransport;
    public override string DeviceId { get; }
    public int ShortId { get; }

    public AqaraT1WithNeutral(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    void IProtocolMappedManageableDevice.SetMqttTransport(IMqttTransport mqttTransport) =>
        _zigBeeTransport = new ZigBeeTransport(mqttTransport);

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiMqttMiSpec(STATE_RES.siid, STATE_RES.piid, value =>
        {
            var state = value.ReadBoolean() ? RelayState.On : RelayState.Off;
            if (State == state) return;
            State = state;
            RaiseEvent(nameof(OnStateChangeAsync), OnStateChangeAsync);
        });
        map.LumiMqttMiSpec(LOAD_POWER_RES.siid, LOAD_POWER_RES.piid,
            value => SetAndRaise(nameof(OnLoadPowerChangeAsync), LoadPower, () => LoadPower = value.ReadSingle(), OnLoadPowerChangeAsync));
        map.LumiMqttMiSpec(POWER_CONSUMPTION_RES.siid, POWER_CONSUMPTION_RES.piid,
            value => SetAndRaise(nameof(OnPowerConsumptionChangeAsync), PowerConsumption, () => PowerConsumption = value.ReadSingle(), OnPowerConsumptionChangeAsync));
    }

    private void SendWriteCommand((int siid, int piid) resource, int value)
    {
        if (_zigBeeTransport is null)
            throw new NotSupportedException($"Device '{DeviceId}' has not been connected to a gateway, it cannot accept commands.");
        _zigBeeTransport.SendWriteCommand(DeviceId, resource, value);
    }

    public float LoadPower { get; private set; }
    public float PowerConsumption { get; private set; }
    public RelayState State { get; private set; } = RelayState.Unknown;
    bool? ISwitchable.IsOn => State switch
    {
        RelayState.On => true,
        RelayState.Off => false,
        _ => null,
    };

    Task ISwitchable.TurnOnAsync() => PowerOnAsync();
    Task ISwitchable.TurnOffAsync() => PowerOffAsync();
    Task ISwitchable.ToggleAsync() => ToggleStateAsync();
    /// <summary>Raised only when the relay state changes. The new state is in State.</summary>
    public event Func<Task> OnStateChangeAsync = () => Task.CompletedTask;
    /// <summary>
    /// Raised on every report, including unchanged values. Passes the previous value in watts; the new value is in LoadPower.
    /// </summary>
    public event Func<float, Task> OnLoadPowerChangeAsync = (_) => Task.CompletedTask;
    /// <summary>
    /// Raised on every report, including unchanged values. Passes the previous value in kWh; the new value is in PowerConsumption.
    /// </summary>
    public event Func<float, Task> OnPowerConsumptionChangeAsync = (_) => Task.CompletedTask;
    public void PowerOn() => RunSync(() => PowerOnAsync());

    /// <inheritdoc cref="PowerOn"/>
    public Task PowerOnAsync()
    {
        try
        {
            SendWriteCommand(STATE_RES, 1);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void PowerOff() => RunSync(() => PowerOffAsync());

    /// <inheritdoc cref="PowerOff"/>
    public Task PowerOffAsync()
    {
        try
        {
            SendWriteCommand(STATE_RES, 0);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void ToggleState() => RunSync(() => ToggleStateAsync());

    /// <inheritdoc cref="ToggleState"/>
    public Task ToggleStateAsync()
    {
        try
        {
            switch (State)
            {
                case RelayState.Off:
                    SendWriteCommand(STATE_RES, 1);
                    break;
                case RelayState.On:
                    SendWriteCommand(STATE_RES, 0);
                    break;
            };
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void SetPowerMemoryState(PowerMemoryState state) => RunSync(() => SetPowerMemoryStateAsync(state));

    /// <inheritdoc cref="SetPowerMemoryState"/>
    public Task SetPowerMemoryStateAsync(PowerMemoryState state)
    {
        try
        {
            SendWriteCommand(POWER_MEMORY_RES, (int)state);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public void SetPowerMode(PowerMode mode) => RunSync(() => SetPowerModeAsync(mode));

    /// <inheritdoc cref="SetPowerMode"/>
    public Task SetPowerModeAsync(PowerMode mode)
    {
        try
        {
            SendWriteCommand(POWER_MODE_RES, (int)mode);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    /// <summary>
    /// Warning ! Be very careful with this function.
    /// If you set low threshold and it's reached, device will fall into "protection" mode and stop working
    /// To reset this mode you need an external intervention (press button on the device or on external switch)
    /// </summary>
    public void SetPowerOverloadThreshold(int threshold) => RunSync(() => SetPowerOverloadThresholdAsync(threshold));

    /// <inheritdoc cref="SetPowerOverloadThreshold"/>
    public Task SetPowerOverloadThresholdAsync(int threshold)
    {
        try
        {
            if(threshold < 0 || threshold > 2200)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold, $"Power overload threshold should be within range 0-2200 watts");

            SendWriteCommand(POWER_OVERLOAD_RES, threshold);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, LUMI_MQTT_MODEL) +
            $"Load Power: {LoadPower}W, " +
            $"Power Consumption: {PowerConsumption}kWh, Channel State: {State}";
    }
}
