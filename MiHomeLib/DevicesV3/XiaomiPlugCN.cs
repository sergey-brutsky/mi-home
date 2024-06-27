using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.DevicesV3;

public class XiaomiPlugCN: ZigBeeManageableDevice
{   
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
    public const string MARKET_MODEL = "ZNCZ02LM";
    public const string MODEL = "lumi.plug";    
    private const string STATE_RES_NAME = "4.1.85";
    private const string LOAD_POWER_RES_NAME = "0.12.85";
    private const string POWER_ON_STATE_RES_NAME = "8.0.2030";
    private const string CHARGE_PROTECTION_RES_NAME = "8.0.2031";
    private const string LED_STATE_RES_NAME = "8.0.2032";
    public PlugState State { get; set; } 
    public float LoadPower { get; set; } 
    public int MaxPower { get; set; } 
    public event Action OnStateChange;
    public event Action<float> OnLoadPowerChange;
    public XiaomiPlugCN(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : base(did, mqttTransport, loggerFactory)
    {
        ResNamesToActions.Add(STATE_RES_NAME, x =>
        {
            var state = x.GetInt32() == 1 ? PlugState.On : PlugState.Off;

            if(state == State) return; // No need to emit event when state is already actual

            State = state;
            OnStateChange?.Invoke();
        });

        ResNamesToActions.Add(LOAD_POWER_RES_NAME, x =>
        {
            var oldValue = LoadPower;
            LoadPower = (float)x.GetDouble();
            OnLoadPowerChange?.Invoke(oldValue);
        });
    }
    protected internal override string[] GetProps()
    {
        // order of properties does matter ! don't change it
        return ["neutral_0", "load_power", "max_power", .. base.GetProps()];
    }
    protected internal override void SetProps(JsonNode[] props)
    {
        State = props[0].GetValue<string>() == "on" ? PlugState.On : PlugState.Off;
        LoadPower = props[1].GetValue<float>();
        MaxPower = props[2].GetValue<int>();
        base.SetProps(props.Skip(3).ToArray());
    }
    public void PowerOn() => SendWriteCommand(STATE_RES_NAME, 1);
    public void PowerOff() => SendWriteCommand(STATE_RES_NAME, 0);
    public void ToggleState()
    {
        switch (State)
        {
            case PlugState.Off:
                SendWriteCommand(STATE_RES_NAME, 1);
                break;
            case PlugState.On:
                SendWriteCommand(STATE_RES_NAME, 0);
                break;
        };
    }
    public void SetPowerMemoryState(PowerMemoryState state)
    {
        SendWriteCommand(POWER_ON_STATE_RES_NAME, (int)state);
    }
    public void SetChargeProtection(ChargeProtect state)
    {
        SendWriteCommand(CHARGE_PROTECTION_RES_NAME, (int)state);
    }
    public void SetLedState(LedState state)
    {
        SendWriteCommand(LED_STATE_RES_NAME, (int)state);
    }
    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"State: {State}, " +
            $"Load Power: {LoadPower}W, " +
            $"Max Power: {MaxPower}W, " + base.ToString();
    }
}

