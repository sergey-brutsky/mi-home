using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;
public abstract class AqaraOppleWirelesSwitch(string did, ILoggerFactory loggerFactory) : ZigBeeBatteryDevice(did, loggerFactory)
{
    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        TripleClick = 3,
        LongPressHold = 16,
        LongPressRelease = 17,
    }
    protected readonly IEnumerable<int> _validClickValues = Helpers.EnumToIntegers<ClickArg>();
}
