namespace MiHomeLib.Devices
{
    public abstract class MiHomeDevice
    {
        public string Sid { get; }
        public string Did { get; }
        public string Name { get; set; }
        public string Type { get; }

        protected MiHomeDevice(string sid, string type)
        {
            Sid = sid;
            Type = type;
        }

        protected MiHomeDevice(string did)
        {
            Did = did;
        }

        public abstract void ParseData(string command);

        public override string ToString()
        {
            return $"Name: {Name}, Sid: {Sid}, Type: {Type}";
        }
    }
}