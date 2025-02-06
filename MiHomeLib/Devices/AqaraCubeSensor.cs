using System;
using System.Linq;
using System.Text.Json.Nodes;
using MiHomeLib.Utils;

namespace MiHomeLib.Devices;

public class AqaraCubeSensor(string sid) : MiHomeDevice(sid, TypeKey)
{
    private CubeStatus _status;

    public const string TypeKey = "sensor_cube.aqgl01";

    public event EventHandler<CubeStatusEventArgs> OnStatusChanged;

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

        var jObject = JsonNode.Parse(command).AsObject();

        if (jObject.ParseString("status", out string status))
        {
            Status = ParseStatus(status);
        }

        if (jObject.ParseString("rotate", out string rotate))
        {
            Rotate = ParseRotate(rotate);
            Status = CubeStatus.Rotated;
        }

        Voltage = jObject.ParseVoltage();
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
        return status switch
        {
            "move" => CubeStatus.Move,
            "flip90" => CubeStatus.Flip90,
            "flip180" => CubeStatus.Flip180,
            "tap_twice" => CubeStatus.TapTwice,
            "shake_air" => CubeStatus.ShakeAir,
            _ => CubeStatus.Undefined,
        };
    }
}

public class CubeStatusEventArgs(CubeStatus status) : EventArgs
{
    public CubeStatus Status { get; } = status;
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