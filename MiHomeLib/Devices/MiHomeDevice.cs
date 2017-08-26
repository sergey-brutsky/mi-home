using MiHomeLib.Commands;

namespace MiHomeLib.Devices
{
    public abstract class MiHomeDevice
    {
        public string Sid { get; }

        protected MiHomeDevice(string sid)
        {
            Sid = sid;
        }

        public abstract void ParseData(string command);
    }
}