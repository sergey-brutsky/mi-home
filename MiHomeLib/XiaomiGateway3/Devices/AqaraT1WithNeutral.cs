using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.XiaomiGateway3.Devices;

public class AqaraT1WithNeutral : ZigBeeManageableDevice
{
    public const string MARKET_MODEL = "SSM-U01";
    public const string MODEL = "lumi.switch.n0agl1";
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
    private (int siid, int piid) STATE_RES = (2, 1);
    private (int siid, int piid) LOAD_POWER_RES = (3, 2);
    private (int siid, int piid) POWER_CONSUMPTION_RES = (3, 1);
    private (int siid, int piid) POWER_MEMORY_RES = (5, 1);
    private (int siid, int piid) POWER_MODE_RES = (7, 2);
    private (int siid, int piid) POWER_OVERLOAD_RES = (5, 6);    
    private readonly Dictionary<(int siid, int piid), Action<JsonNode>> _actions;
    public AqaraT1WithNeutral(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : base(did, mqttTransport, loggerFactory)
    {
        _actions = new()
        {
            {LOAD_POWER_RES, x => // load power changed
                {
                    var oldValue = LoadPower;
                    LoadPower = x.GetValue<float>();
                    OnLoadPowerChangeAsync?.Invoke(oldValue);
                }
            }, 
            {STATE_RES, x => // channel state changed
                {
                    var state = x.GetValue<bool>() ? 1 : 0;

                    if(state  == (int)State) return; // No need to emit event when state is already actual

                    State = state == 1 ? RelayState.On : RelayState.Off;
                    OnStateChangeAsync?.Invoke();
                }
            },
            {POWER_CONSUMPTION_RES, x => // electricity consumption changed
                {
                    var oldValue = PowerConsumption;
                    PowerConsumption = x.GetValue<float>();
                    OnPowerConsumptionChangeAsync?.Invoke(oldValue);
                }
            }, 
        };
    }
    public float LoadPower { get; internal set; }
    public float PowerConsumption { get; internal set; }
    public RelayState State { get; internal set; } = RelayState.Unknown;
    public event Func<Task> OnStateChangeAsync = () => Task.CompletedTask;
    /// <summary>
    /// Old value passed as an argument in W
    /// </summary>        
    public event Func<float, Task> OnLoadPowerChangeAsync = (_) => Task.CompletedTask;
    /// <summary>
    /// Old value passed as an argument in kWh
    /// </summary>        
    public event Func<float, Task> OnPowerConsumptionChangeAsync = (_) => Task.CompletedTask;
    protected internal override void ParseData(string data)
    {
        var listProps = JsonSerializer.Deserialize<List<JsonNode>>(data);

        foreach (var prop in listProps)
        {
            var key = (prop["siid"].GetValue<int>(), prop["piid"].GetValue<int>());
            
            if(_actions.ContainsKey(key))
                 _actions[key](prop["value"]);
        }
    }
    public void PowerOn() => SendWriteCommand(STATE_RES, 1);
    public void PowerOff() => SendWriteCommand(STATE_RES, 0);
    public void ToggleState()
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
    }
    public void SetPowerMemoryState(PowerMemoryState state) => SendWriteCommand(POWER_MEMORY_RES, (int)state);
    public void SetPowerMode(PowerMode mode) => SendWriteCommand(POWER_MODE_RES, (int)mode);
    /// <summary>
    /// Warning ! Be very careful with this function. 
    /// If you set low threshold and it's reached, device will fall into "protection" mode and stop working
    /// To reset this mode you need an external intervention (press button on the device or on external switch)
    /// </summary>
    public void SetPowerOverloadThreshold(int threshold)
    {
        if(threshold < 0 || threshold > 2200) 
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, $"Power overload threshold should be within range 1-2200 watt");

        SendWriteCommand(POWER_OVERLOAD_RES, threshold);
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"Load Power: {LoadPower}W, " +
            $"Channel State: {State}";
    }
}
