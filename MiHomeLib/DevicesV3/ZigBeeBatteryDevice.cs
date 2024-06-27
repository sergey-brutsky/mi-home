using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MiHomeLib.DevicesV3;

namespace MiHomeLib;

public abstract class ZigBeeBatteryDevice : ZigBeeDevice
{
    private const string BATTERY_RES_NAME = "8.0.2001";
    private const string VOLTAGE_RES_NAME = "8.0.2008";    
    public float Voltage { get; set; }
    public byte BatteryPercent { get; set; }
    /// <summary>
    /// Old value voltage 0-3.5 (0.01 step) passed as an argument
    /// </summary>    
    public event Action<float> OnVoltageChange;
    /// <summary>
    /// Old value battery percent 0-100% (1 step) passed as an argument
    /// </summary>
    public event Action<byte> OnBatteryPercentChange;    
    public ZigBeeBatteryDevice(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        ResNamesToActions.Add(VOLTAGE_RES_NAME, x => 
        {
            var oldVal = Voltage;
            Voltage = x.GetInt32()/1000f;
            OnVoltageChange?.Invoke(oldVal);
        });
        
        ResNamesToActions.Add(BATTERY_RES_NAME, x => 
        {
            var oldVal = BatteryPercent;
            BatteryPercent = (byte)x.GetInt32();
            OnBatteryPercentChange?.Invoke(oldVal);
        });
    }
    protected internal override string[] GetProps()
    {
        // order of properties does matter ! don't change it
        return ["voltage", "battery", .. base.GetProps()];
    }
    protected internal override void SetProps(JsonNode[] props)
    {
        Voltage = props[0].GetValue<int>()/1000f;
        BatteryPercent = (byte)props[1].GetValue<int>();
        base.SetProps(props.Skip(2).ToArray());
    }

    public override string ToString() => $"Voltage: {Voltage}V, "+ $"BatteryPercent: {BatteryPercent}%, "+ base.ToString();
}
