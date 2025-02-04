using System;

namespace MiHomeLib.Events;

public class DensityEventArgs(float t) : EventArgs
{
    public float Density { get; } = t;
}