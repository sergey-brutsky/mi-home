using System;

namespace MiHomeLib.Events;

public class PressureEventArgs(float p) : EventArgs
{
    public float Pressure { get; } = p;
}