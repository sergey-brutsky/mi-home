using System;

namespace MiHomeLib.Events;

public class NoMotionEventArgs(int seconds) : EventArgs
{
    public int Seconds { get; } = seconds;
}
