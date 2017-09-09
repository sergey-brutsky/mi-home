using System;

namespace MiHomeLib.Events
{
    public class NoMotionEventArgs: EventArgs
    {
        public NoMotionEventArgs(int seconds)
        {
            Seconds = seconds;
        }

        public int Seconds { get; }
    }
}
