using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.DevicesV3;

public class XiaomiDoorWindowSensor : ZigBeeBatteryDevice
{    
    public enum DoorWindowContactState
    {
        Closed = 0,
        Opened = 1,
    }
    public const string MARKET_MODEL = "MCCGQ01LM";
    public const string MODEL = "lumi.sensor_magnet";
    private const string CONTACT_RES_NAME = "3.1.85";
    public DoorWindowContactState Contact { get; set; }
    public event Action OnContactChanged;
    private readonly IEnumerable<int> _validContactValues = Helpers.EnumToIntegers<DoorWindowContactState>();
    public XiaomiDoorWindowSensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        ResNamesToActions.Add(CONTACT_RES_NAME, x =>
        {
            var val = x.GetInt32();
            if(!_validContactValues.Contains(val)) return;

            Contact = (DoorWindowContactState)val;
            OnContactChanged?.Invoke();
        });
    }

    protected string GetBaseToString() => base.ToString();

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + $"Contact: {Contact}, " + base.ToString();
}
