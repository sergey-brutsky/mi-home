using System;

namespace MiHomeLib.Events
{
    public class PressureEventArgs : EventArgs
    {
        public PressureEventArgs(float p)
        {
            Pressure = p;
        }

        public float Pressure { get; }
    }
}