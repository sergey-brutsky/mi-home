using System;

namespace MiHomeLib.Events;

public class TemperatureEventArgs(float t) : EventArgs
{
    public float Temperature { get; } = t;
}