// Support for this device has been implemented on top of https://home.miot-spec.com/spec?type=urn%3Amiot-spec-v2%3Adevice%3Aswitch%3A0000A003%3Azimi-dhkg01%3A2%3A0000C808
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// Xiaomi Smart Wall Switch Single Open DHKG01CM, Bluetooth Mesh
/// </summary>
public class XiaomiMeshWallSwitchSingleOpen : MeshDevice
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

    public XiaomiMeshWallSwitchSingleOpen(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        Properties.Add((SWITCH_SIID, ON_PIID), x => UpdateState(x.GetValue<bool>()));
        Properties.Add((INDICATOR_SIID, INDICATOR_PIID), x => UpdateIndicatorLight(x.GetValue<bool>()));
        Events.Add((CLICK_SIID, CLICK_EIID), () => RaiseEvent(() => OnClickAsync()));
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
    public bool IsTurnedOn { get; internal set; }

    /// <summary>
    /// Old value of the switch state passed as an argument
    /// </summary>
    public event Func<bool, Task> OnStateChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Physical click on the switch button
    /// </summary>
    public event Func<Task> OnClickAsync = () => Task.CompletedTask;

    /// <summary>
    /// Indicator light state, updated from properties_changed reports. Unknown until the device reports it
    /// </summary>
    public bool IsIndicatorLightEnabled { get; internal set; }

    /// <summary>
    /// Old value of the indicator light state passed as an argument
    /// </summary>
    public event Func<bool, Task> OnIndicatorLightChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Turn the switch on
    /// </summary>
    public void TurnOn() => SetProperty(SWITCH_SIID, ON_PIID, true);

    /// <summary>
    /// Turn the switch off
    /// </summary>
    public void TurnOff() => SetProperty(SWITCH_SIID, ON_PIID, false);

    /// <summary>
    /// Toggle the switch state through the toggle service (write-only property, value 1)
    /// </summary>
    public void Toggle() => SetProperty(TOGGLE_SIID, TOGGLE_PIID, TOGGLE_ON_VALUE);

    /// <summary>
    /// Wired and wireless mode: wireless only disables the physical wall switch
    /// </summary>
    public void SetMode(WirelessMode mode) => SetProperty(SWITCH_SIID, MODE_PIID, (int)mode);

    /// <summary>
    /// Turn the indicator light on or off
    /// </summary>
    public void SetIndicatorLight(bool enabled) => SetProperty(INDICATOR_SIID, INDICATOR_PIID, enabled);

    /// <summary>
    /// Set the value of the power-change service (write-only)
    /// </summary>
    public void SetPowerChangeValue(PowerChangeValue value) => SetProperty(POWER_CHANGE_SIID, POWER_CHANGE_PIID, (int)value);

    private void UpdateState(bool value)
    {
        var oldValue = IsTurnedOn;
        IsTurnedOn = value;
        RaiseEvent(() => OnStateChangeAsync(oldValue));
    }

    private void UpdateIndicatorLight(bool value)
    {
        var oldValue = IsIndicatorLightEnabled;
        IsIndicatorLightEnabled = value;
        RaiseEvent(() => OnIndicatorLightChangeAsync(oldValue));
    }

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + $"On: {IsTurnedOn}";
}
