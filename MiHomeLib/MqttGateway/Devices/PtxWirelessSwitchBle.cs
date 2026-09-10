using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// PTX_YK1_QMIMB PTX Wireless Switch (Bluetooth version) BLE 5.0
/// </summary>
public class PtxWirelessSwitchBle : BleDevice
{
    public const string MARKET_MODEL = "PTX-YK1-QMIMB";
    public const string MODEL = "090615.remote.btsw1";
    public const int PDID = 14523;

    public enum ClickArg
    {
        SingleClick = 1012,
        DoubleClick = 1013,
        LongPressClick = 1014, // press & hold more than 5 seconds
    }

    private const int CLICK_SIID = 2;
    
    // low battery event, only event
    // unfortunately percentage is not available for this device :(
    private const int LOW_BATTERY_SIID = 3; 

    public PtxWirelessSwitchBle(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(CLICK_SIID, async x => await OnClickAsync((ClickArg)int.Parse(x)));
        EidToActions.Add(LOW_BATTERY_SIID, async _ => await OnLowBatteryAsync());
    }

    protected internal override void ParseData(string data)
    {
        JsonObject @params = JsonNode.Parse(data).AsObject();

        var siid = @params["siid"].GetValue<int>();
        var eiid = @params["eiid"].GetValue<int>();

        if (
            (siid == CLICK_SIID || siid == LOW_BATTERY_SIID)
            && EidToActions.TryGetValue(siid, out var action) && Enum.IsDefined(typeof(ClickArg), eiid))
        {
            action(eiid.ToString());
        }
    }
    /// <summary>
    /// Value of click type passed as a parameter
    /// </summary>
    public event Func<ClickArg, Task> OnClickAsync = (_) => Task.CompletedTask;
    
    /// <summary>
    /// Low battery event
    /// </summary>
    public event Func<Task> OnLowBatteryAsync = () => Task.CompletedTask;

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + base.ToString();
}