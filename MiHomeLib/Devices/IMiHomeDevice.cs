using System;
using System.Collections.Generic;
using System.Text;

namespace MiHomeLib.Devices
{
    public interface IMiHomeDevice
    {
        IObservable<IMiHomeDevice> Changes { get; }

        string Sid { get; }
        string Name { get; set; }
        string Type { get; }

        void ParseData(string command);
    }

    public interface IMiHomeDevice<T> : IMiHomeDevice where T : class, IMiHomeDevice<T>
    {
    }
}
