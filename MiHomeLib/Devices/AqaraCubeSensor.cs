using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Devices
{
    public class AqaraCubeSensor : MiHomeDevice
    {
        private CubeStatus _status;
        public const string SensorKey = "sensor_cube.aqgl01";

        public override string Type => SensorKey;

        public event EventHandler<CubeStatusEventArgs> OnStatusChanged;

        public AqaraCubeSensor(string sid) : base(sid)
        {
        }

        public float? Voltage { get; private set; }

        public CubeRotate Rotate { get; private set; }

        public CubeStatus Status
        {
            get => _status;
            private set
            {
                _status = value;
                OnStatusChanged?.Invoke(this, new CubeStatusEventArgs(value));
            }
        }
        public override void ParseData(string command)
        {
            Rotate = new CubeRotate();
            _status = CubeStatus.Undefined;

            var jObject = JObject.Parse(command);

            if (jObject["status"] != null)
            {
                Status = ParseStatus(jObject["status"].ToString());
            }

            if (jObject["rotate"] != null)
            {
                Rotate = ParseRotate(jObject["rotate"].ToString());
                Status = CubeStatus.Rotated;
            }

            if (jObject["voltage"] != null && float.TryParse(jObject["voltage"].ToString(), out var v))
            {
                Voltage = v / 1000;
            }

            Debug.WriteLine($"Sid: {Sid}, Type: {GetType().Name}, Command: {command}, Sensor: {this}");
        }

        public override string ToString()
        {
            return $"{nameof(Rotate)}: [{Rotate}], {nameof(Status)}: {Status}, {nameof(Voltage)}: {Voltage}V";
        }

        private static CubeRotate ParseRotate(string rotate)
        {
            var array = rotate.Split(',').Select(short.Parse).ToArray();
            return new CubeRotate(array[0], array[1]);
        }

        private static CubeStatus ParseStatus(string status)
        {
            switch (status)
            {
                case "move": return CubeStatus.Move;
                case "flip90": return CubeStatus.Flip90;
                case "flip180": return CubeStatus.Flip180;
                case "tap_twice": return CubeStatus.TapTwice;
                case "shake_air": return CubeStatus.ShakeAir;

                // some times got
                // case "iam": return AqaraCubeStatus.iam;
                // case "swing": return AqaraCubeStatus.swing;
                // case "alert": return AqaraCubeStatus.alert;
            }

            Debug.WriteLine($"Warning! Undefined Cube status:{status}");

            return CubeStatus.Undefined;
        }
    }

    public class CubeStatusEventArgs : EventArgs
    {
        public CubeStatus Status { get; }

        public CubeStatusEventArgs(CubeStatus status)
        {
            Status = status;
        }
    }

    public struct CubeRotate
    {
        public CubeRotate(short shift, short value)
        {
            Shift = shift;
            Value = value;
        }

        public short Shift { get; }

        public short Value { get; }

        public override string ToString()
        {
            return $"{Shift},{Value}";
        }
    }

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
}