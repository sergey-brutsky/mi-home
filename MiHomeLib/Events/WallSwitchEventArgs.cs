using System;

namespace MiHomeLib.Events;

public class WallSwitchEventArgs(string state) : EventArgs
{
    public string State { get; } = state;
}