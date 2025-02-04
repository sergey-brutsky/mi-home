using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices
{
    public class AqaraVirationSensor : MiHomeDevice
    {
        public const string TypeKey = "vibration";
        private readonly Dictionary<string, Action> _sensorStatuses;
        public AqaraVirationSensor(string sid) : base(sid, TypeKey)
        {
            _sensorStatuses = new()
            {
                {"vibrate", () => OnVibration?.Invoke(this, EventArgs.Empty)},
                {"tilt", () => OnTilt?.Invoke(this, EventArgs.Empty)},
                {"free_fall", () => OnFreeFall?.Invoke(this, EventArgs.Empty)},
            };
        }
        public float? Voltage { get; set; }
        public event EventHandler OnVibration;
        public event EventHandler OnTilt;
        public event EventHandler OnFreeFall;
        public string LastStatus { get; private set; }
        public event EventHandler OnFinalTiltAngle;
        public int FinalTiltAngle { get; private set; }
        public event EventHandler OnCoordinations;
        public (int X, int Y, int Z) Coordinations { get; private set; }
        public event EventHandler OnBedActivity;
        public int BedActivity { get; private set; }
        public override void ParseData(string command)
        {
            var jObject = JsonNode.Parse(command).AsObject();
            
            if (jObject.ParseString("status", out string status) && _sensorStatuses.Keys.Contains(status))
            {
                LastStatus = status;
                _sensorStatuses[status]();
            }
            else if (jObject.ParseInt("final_tilt_angle", out int final_tilt_angle))
            {
                FinalTiltAngle = final_tilt_angle;
                OnFinalTiltAngle?.Invoke(this, EventArgs.Empty);
            }
            else if (jObject.ParseString("coordination", out string coordination))
            {
                var coords = coordination.Split(',').Select(int.Parse).ToArray();
                Coordinations = (coords[0], coords[1], coords[2]);
                OnCoordinations?.Invoke(this, EventArgs.Empty);
            }
            else if (jObject.ParseInt("bed_activity", out int bed_activity))
            {
                BedActivity = bed_activity;
                OnBedActivity?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Voltage = jObject.ParseVoltage();
            }
        }
        public override string ToString()
        {
            return $"{nameof(Voltage)}: {Voltage}V, Last status: {LastStatus}, Final tilt angle: {FinalTiltAngle}, Coordinations: '{Coordinations.X},{Coordinations.Y},{Coordinations.Z}', Bed Activity: {BedActivity}";
        }
    }
}
