using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// PTX_YK1_QMIMB PTX Wireless Switch (Bluetooth version)
/// BLE 5.0, CR2032 battery powered, works with Mijia
/// </summary>
public class PtxWirelessSwitchBle : BleBatteryDevice
{
    public const string MARKET_MODEL = "PTX-YK1-QMIMB";
    public const string MODEL = "090615.remote.bleykt1";
    public const int PDID = 3051;

    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        LongPress = 3,
    }

    private const int BUTTON_EVENT_EID = 4097;

    private readonly IEnumerable<int> _validClickValues = Helpers.EnumToIntegers<ClickArg>();

    /// <summary>
    /// Old value of click type passed as a parameter
    /// </summary>
    public event Func<ClickArg, Task> OnClickAsync = (_) => Task.CompletedTask;

    public PtxWirelessSwitchBle(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(BUTTON_EVENT_EID, async x =>
        {
            var val = ToBleInt256(x);

            if (_validClickValues.Contains(val))
            {
                await OnClickAsync((ClickArg)val);
            }
        });
    }

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + base.ToString();
}