using System;

namespace MiHomeLib.Events
{
    public class WallSwitchEventArgs: EventArgs
    {
        public WallSwitchEventArgs(string state)
        {
            State = state;
        }

        public string State { get; }
    }
}