using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// WXKG20LM E1 lumi.remote.acn007 wireless mini switch (CN revision)
/// </summary>
public class AqaraWirelessMiniSwitchCN2 : MiSpecZigBeeDevice
{
    public const string MARKET_MODEL = "WXKG20LM";
    public const string MODEL = "lumi.remote.acn007";
    private const int CLICK_SIID = 2; // click
    private const int BATTERY_SIID = 3; // battery & percentage
    private const int BATTERY_STATUS_PIID = 1; // state
    private const int BATTERY_VOLTAGE_PIID = 2; // voltage
    
    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        LongPressClick = 3 // press & hold more than 3 seconds
    }

    public enum BatteryStatusType
    {
        Normal = 1,
        LowBattery = 2
    }

    public AqaraWirelessMiniSwitchCN2(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        Events.Add((CLICK_SIID, (int)ClickArg.SingleClick), async _ => await OnClickAsync(ClickArg.SingleClick));
        Events.Add((CLICK_SIID, (int)ClickArg.DoubleClick), async _ => await OnClickAsync(ClickArg.DoubleClick));
        Events.Add((CLICK_SIID, (int)ClickArg.LongPressClick), async _ => await OnClickAsync(ClickArg.LongPressClick));
        
        Properties.Add((BATTERY_SIID, BATTERY_VOLTAGE_PIID), async x => await OnBatteryVoltageAsync(x.GetValue<int>()/1000f));
        Properties.Add((BATTERY_SIID, BATTERY_STATUS_PIID), async x => await OnBatteryStatusAsync((BatteryStatusType)x.GetValue<int>()));
    }
    
    /// <summary>
    /// Value of click type passed as a parameter
    /// </summary>
    public event Func<ClickArg, Task> OnClickAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Value of voltage passed as a parameter
    /// </summary>
    public event Func<float, Task> OnBatteryVoltageAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Battery status (Normal/Low Battery) passed as a parameter 
    /// </summary>
    public event Func<BatteryStatusType, Task> OnBatteryStatusAsync = (_) => Task.CompletedTask;

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + base.ToString();
}
