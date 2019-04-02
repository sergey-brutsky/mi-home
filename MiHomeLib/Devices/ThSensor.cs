using System;
using MiHomeLib.Events;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class ThSensor : ThSensorAbstract<ThSensor>
    {
        public static string IdString => "sensor_ht";

        public override string Type => IdString;
        
        public ThSensor(string sid) : base(sid) { }

        public override void ParseData(string command)
        {
            if (ParseDataInternal(command))
                _changes.OnNext(this);
        }
    }
}