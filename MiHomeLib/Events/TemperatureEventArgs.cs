using System;

namespace MiHomeLib.Events
{
    public class TemperatureEventArgs : EventArgs
    {
        public TemperatureEventArgs(float t)
        {
            Temperature = t;
        }

        public float Temperature { get; }
    }
}