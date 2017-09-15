namespace MiHomeLib.Devices
{
    public abstract class MiHomeDevice
    {
        public string Sid { get; }
        public string Name { get; set; }
        public string Type { get; }

        protected MiHomeDevice(string sid, string type)
        {
            Sid = sid;
            Type = type;
        }

        public abstract void ParseData(string command);
    }
}