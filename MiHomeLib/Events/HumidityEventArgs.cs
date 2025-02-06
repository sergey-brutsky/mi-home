using System;

namespace MiHomeLib.Events;

public class HumidityEventArgs(float t) : EventArgs
{
    public float Humidity { get; } = t;
}