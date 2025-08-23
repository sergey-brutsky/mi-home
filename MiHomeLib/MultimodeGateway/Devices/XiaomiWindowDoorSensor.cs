using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MultimodeGateway.Devices;

public class XiaomiWindowDoorSensor : ZigBeeBatteryDevice
{   
    public const string MARKET_MODEL = "MCCGQ01LM";
    public const string MODEL = "lumi.sensor_magnet"; 
    public enum DoorWindowContactState
    {
        Closed = 0,
        Opened = 1,
    }    
    private const string CONTACT_RES_NAME = "3.1.85";
    public DoorWindowContactState Contact { get; set; }
    public event Func<Task> OnContactChangedAsync = () => Task.CompletedTask;
    private readonly IEnumerable<int> _validContactValues = Helpers.EnumToIntegers<DoorWindowContactState>();
    public XiaomiWindowDoorSensor(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        Actions.Add(CONTACT_RES_NAME, async x =>
        {
            var val = x.GetInt32();
            if(!_validContactValues.Contains(val)) return;

            Contact = (DoorWindowContactState)val;
            await OnContactChangedAsync();
        });
    }

    protected string GetBaseToString() => base.ToString();

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + $"Contact: {Contact}, " + base.ToString();
}
