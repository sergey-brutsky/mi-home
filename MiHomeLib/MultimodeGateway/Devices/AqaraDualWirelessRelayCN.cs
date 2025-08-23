using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.MultimodeGateway.Devices;

public class AqaraDualWirelessRelayCN : ZigBeeManageableDevice
{
    public const string MARKET_MODEL = "LLKZMK11LM";
    public const string MODEL = "lumi.relay.c2acn01";
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
    public ChannelState Channel1 { get; internal set; } = ChannelState.Unknown;
    public ChannelState Channel2 { get; internal set; } = ChannelState.Unknown;
    /// <summary>
    /// Voltage in volts
    /// </summary>
    public float Voltage { get; internal set; }
    /// <summary>
    /// Load power in watt
    /// </summary>
    public float LoadPower { get; internal set; }
    /// <summary>
    /// Energy in watt
    /// </summary>
    public float Energy { get; internal set; }
    /// <summary>
    /// Load current in milliampers
    /// </summary>
    public float Current { get; internal set; }    
    public InterlockState Interlock { get; internal set; } = InterlockState.Unknown;
    public event Func<Task> OnChannel1StateChangeAsync = () => Task.CompletedTask;  
    public event Func<Task> OnChannel2StateChangeAsync = () => Task.CompletedTask;
    public event Func<float, Task> OnLoadPowerChangeAsync = (_) => Task.CompletedTask;
    public event Func<float, Task> OnVoltageChangeAsync = (_) => Task.CompletedTask;
    public event Func<float, Task> OnEnergyChangeAsync = (_) => Task.CompletedTask;
    public event Func<float, Task> OnCurrentChangeAsync = (_) => Task.CompletedTask;
    public AqaraDualWirelessRelayCN(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : base(did, mqttTransport, loggerFactory)
    {
        Actions.Add(CHANNEL1_RES_NAME, async x =>
        {
            var state = x.GetInt32();

            if(state == (int)Channel1) return; // No need to emit event when state is already actual

            Channel1 = state == 1 ? ChannelState.On : state == 0 ? ChannelState.Off : ChannelState.Unknown;
            await OnChannel1StateChangeAsync();
        });

        Actions.Add(CHANNEL2_RES_NAME, async x =>
        {
            var state = x.GetInt32();

            if(state == (int)Channel2) return; // No need to emit event when state is already actual

            Channel2 = state == 1 ? ChannelState.On : state == 0 ? ChannelState.Off : ChannelState.Unknown;
            await OnChannel2StateChangeAsync();
        });

        Actions.Add(VOLTAGE_RES_NAME, async x =>
        {
            (var oldValue, Voltage) = (Voltage, x.GetInt32()/1000f);
            await OnVoltageChangeAsync(oldValue);
        });

        Actions.Add(LOAD_POWER_RES_NAME, async x =>
        {
            (var oldValue, LoadPower) = (LoadPower, (float)x.GetDouble());
            await OnLoadPowerChangeAsync(oldValue);
        });

        Actions.Add(ENERGY_RES_NAME, async x =>
        {
            (var oldValue, Energy) = (Energy, (float)x.GetDouble());
            await OnEnergyChangeAsync(oldValue);
        });

        Actions.Add(LOAD_CURRENT_RES_NAME, async x =>
        {
            (var oldValue, Current) = (Current, (float)x.GetDouble());
            await OnCurrentChangeAsync(oldValue);
        });
    }
    protected internal override string[] GetProps()
    {
        // order of properties does matter ! don't change it
        return ["channel_0", "channel_1", "load_voltage", "load_power", "enable_motor_mode", .. base.GetProps()];
    }
    protected internal override void SetProps(JsonNode[] props)
    {
        Channel1 = StringToState(props[0].GetValue<string>());
        Channel2 = StringToState(props[1].GetValue<string>());
        Voltage = props[2].GetValue<int>() / 1000f;
        LoadPower = props[3].GetValue<float>();        
        Interlock = (InterlockState)props[4].GetValue<int>();
        base.SetProps(props.Skip(5).ToArray());
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
        return GetBaseInfo(MARKET_MODEL, MODEL) +
            $"Channel 1: {Channel1}, " +
            $"Channel 2: {Channel2}, " +
            $"Voltage: {Voltage}V, " +
            $"Load Power: {LoadPower}W, " +
            $"Interlock state: {Interlock}, " +
            base.ToString();
    }
    public void Channel1PowerOn() => SendWriteCommand(CHANNEL1_RES_NAME, 1);
    public void Channel1PowerOff() => SendWriteCommand(CHANNEL1_RES_NAME, 0);
    public void Channel1ToggleState()
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
    }
    public void Channel2PowerOn() => SendWriteCommand(CHANNEL2_RES_NAME, 1);
    public void Channel2PowerOff() => SendWriteCommand(CHANNEL2_RES_NAME, 0);
    public void Channel2ToggleState()
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
    }
    /// <summary>
    /// Restore or not previous state after electricity was disrupted and then appeared again
    /// </summary>    
    public void SetPowerMemoryState(PowerMemoryState state) => SendWriteCommand(POWER_MEMORY_RES_NAME, (int)state);
    /// <summary>
    /// This function enables/disabled "only one channel can be enabled at a time" logic.
    /// If we enable channel1, channel2 will be automatically turned off off and vice versa
    /// </summary>
    public void SetInterlock(InterlockState state) => SendWriteCommand(INTERLOCK_RES_NAME, (int)state);
}
