using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway2.Devices;

public class AqaraCubeSensor : BatteryXiaomiGateway2SubDevice
{
    public const string MARKET_MODEL = "MFKZQ01LM";
    public const string MODEL = "sensor_cube.aqgl01";
    public enum CubeStatus
    {
        Undefined,
        Rotated,
        Move,
        Flip90,
        Flip180,
        TapTwice,
        ShakeAir,
    }
    public CubeStatus Status { get; private set; }
    public CubeRotate Rotate { get; private set; }

    public AqaraCubeSensor(string sid, int shortId, ILoggerFactory loggerFactory) : base(sid, shortId, loggerFactory)
    {
        Rotate = new CubeRotate();

        Status = CubeStatus.Undefined;

        Actions.Add("status", async x => 
        {            
            Status = x.GetString() switch
            {
                "move" => CubeStatus.Move,
                "flip90" => CubeStatus.Flip90,
                "flip180" => CubeStatus.Flip180,
                "tap_twice" => CubeStatus.TapTwice,
                "shake_air" => CubeStatus.ShakeAir,
                _ => CubeStatus.Undefined,
            };  

            if(OnStatusChangedAsync is not null) await OnStatusChangedAsync.Invoke(Status);
        });

        Actions.Add("rotate", async x => 
        {
            var array = x.GetString().Split(',').Select(short.Parse).ToArray();
            Rotate = new CubeRotate(array[0], array[1]);;
            Status = CubeStatus.Rotated;
            await OnStatusChangedAsync(Status);
        });
    }

    public event Func<CubeStatus, Task> OnStatusChangedAsync = (_) => Task.CompletedTask;

    public override string ToString()
    {
        return base.ToString() +  $", Rotate: [{Rotate}], Status: {Status}";
    }

    public readonly struct CubeRotate(short shift, short value)
    {
        public short Shift { get; } = shift;

        public short Value { get; } = value;

        public override string ToString()
        {
            return $"{Shift},{Value}";
        }
    }    
}





