using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.Protocols;
using MiHomeLib.MqttGateway;
// Support for this device has been implemented on top of https://home.miot-spec.com/spec?type=urn%3Amiot-spec-v2%3Adevice%3Aswitch%3A0000A003%3Azimi-dhkg01%3A2%3A0000C808
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// Xiaomi Smart Wall Switch Single Open DHKG01CM, Bluetooth Mesh
/// </summary>
[ProtocolDeviceModel(DeviceProtocol.LumiMesh, MODEL, PDID)]
public sealed class XiaomiMeshWallSwitchSingleOpen : ProtocolMappedDeviceBase, IProtocolMappedMeshDevice
{
    public const string MARKET_MODEL = "DHKG01CM";
    public const string MODEL = "zimi.switch.dhkg01";
    public const int PDID = 1945;

    private const int SWITCH_SIID = 2;
    private const int ON_PIID = 1;
    private const int MODE_PIID = 2;
    private const int TOGGLE_SIID = 5;
    private const int TOGGLE_PIID = 1;
    private const int TOGGLE_ON_VALUE = 1;
    private const int INDICATOR_SIID = 10;
    private const int INDICATOR_PIID = 1;
    private const int POWER_CHANGE_SIID = 7;
    private const int POWER_CHANGE_PIID = 1;
    private const int CLICK_SIID = 8;
    private const int CLICK_EIID = 1;

    private IMqttTransport _mqttTransport;
    public override string DeviceId { get; }
    public string Mac { get; private set; }

    public XiaomiMeshWallSwitchSingleOpen(string deviceId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
    }

    void IProtocolMappedBleDevice.SetMacAddress(string mac) => Mac = mac;
    void IProtocolMappedManageableDevice.SetMqttTransport(IMqttTransport mqttTransport) => _mqttTransport = mqttTransport;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiMeshMiSpec(SWITCH_SIID, ON_PIID, value => SetAndRaise(IsTurnedOn, () => IsTurnedOn = value.ReadBoolean(), OnStateChangeAsync));
        map.LumiMeshMiSpec(INDICATOR_SIID, INDICATOR_PIID,
            value => SetAndRaise(IsIndicatorLightEnabled, () => IsIndicatorLightEnabled = value.ReadBoolean(), OnIndicatorLightChangeAsync));
        map.LumiMeshMiSpecEvent(CLICK_SIID, CLICK_EIID, _ => RaiseEvent(OnClickAsync));
    }

    private void SetProperty(int siid, int piid, object value)
    {
        if (_mqttTransport is null)
            throw new InvalidOperationException($"Device '{DeviceId}' is not connected to the gateway transport");
        _mqttTransport.Publish("miio/command", JsonSerializer.Serialize(new MeshCommand("set_properties",
            [new MiotWriteProperty(DeviceId, siid, piid, MiioParameters.ToNode(value))]), MiHomeJsonContext.Default.MeshCommand));
    }

    public enum WirelessMode
    {
        WiredAndWireless = 0,
        WirelessOnly = 1,
    }

    /// <summary>
    /// Values of the power-change service. The spec labels the property "scan window value" and gives no units
    /// </summary>
    public enum PowerChangeValue
    {
        Value15 = 0,
        Value20 = 1,
        Value25 = 2,
    }

    /// <summary>
    /// Current state of the switch, updated from properties_changed reports
    /// </summary>
    public bool IsTurnedOn { get; private set; }

    /// <summary>
    /// Raised on every state report, including unchanged values. Passes the previous state; the new state is in IsTurnedOn.
    /// </summary>
    public event Func<bool, Task> OnStateChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Physical click on the switch button
    /// </summary>
    public event Func<Task> OnClickAsync = () => Task.CompletedTask;

    /// <summary>
    /// Indicator light state, updated from properties_changed reports. False until the device reports it
    /// </summary>
    public bool IsIndicatorLightEnabled { get; private set; }

    /// <summary>
    /// Raised on every indicator report, including unchanged values. Passes the previous state; the new state is in IsIndicatorLightEnabled.
    /// </summary>
    public event Func<bool, Task> OnIndicatorLightChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Turn the switch on
    /// </summary>
    public void TurnOn() => RunSync(() => TurnOnAsync());

    /// <inheritdoc cref="TurnOn"/>
    public Task TurnOnAsync()
    {
        try
        {
            SetProperty(SWITCH_SIID, ON_PIID, true);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Turn the switch off
    /// </summary>
    public void TurnOff() => RunSync(() => TurnOffAsync());

    /// <inheritdoc cref="TurnOff"/>
    public Task TurnOffAsync()
    {
        try
        {
            SetProperty(SWITCH_SIID, ON_PIID, false);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Toggle the switch state through the toggle service (write-only property, value 1)
    /// </summary>
    public void Toggle() => RunSync(() => ToggleAsync());

    /// <inheritdoc cref="Toggle"/>
    public Task ToggleAsync()
    {
        try
        {
            SetProperty(TOGGLE_SIID, TOGGLE_PIID, TOGGLE_ON_VALUE);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Wired and wireless mode: wireless only disables the physical wall switch
    /// </summary>
    public void SetMode(WirelessMode mode) => RunSync(() => SetModeAsync(mode));

    /// <inheritdoc cref="SetMode"/>
    public Task SetModeAsync(WirelessMode mode)
    {
        try
        {
            SetProperty(SWITCH_SIID, MODE_PIID, (int)mode);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Turn the indicator light on or off
    /// </summary>
    public void SetIndicatorLight(bool enabled) => RunSync(() => SetIndicatorLightAsync(enabled));

    /// <inheritdoc cref="SetIndicatorLight"/>
    public Task SetIndicatorLightAsync(bool enabled)
    {
        try
        {
            SetProperty(INDICATOR_SIID, INDICATOR_PIID, enabled);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }

    /// <summary>
    /// Set the value of the power-change service (write-only)
    /// </summary>
    public void SetPowerChangeValue(PowerChangeValue value) => RunSync(() => SetPowerChangeValueAsync(value));

    /// <inheritdoc cref="SetPowerChangeValue"/>
    public Task SetPowerChangeValueAsync(PowerChangeValue value)
    {
        try
        {
            SetProperty(POWER_CHANGE_SIID, POWER_CHANGE_PIID, (int)value);
            return Task.CompletedTask;
        }
        catch (Exception exception) { return Task.FromException(exception); }
    }



    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + $"Mac: {Mac}, On: {IsTurnedOn}, Indicator light: {IsIndicatorLightEnabled}";
}
