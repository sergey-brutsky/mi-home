namespace MiHomeLib.Commands
{
    internal class WriteCommand: Command
    {
        private readonly string _sid;
        private readonly string _type;
        private readonly string _data;

        public WriteCommand(string sid, string type, string data)
        {
            _sid = sid;
            _type = type;
            _data = data;
        }

        public override string ToString()
        {
            return $"{{\"cmd\":\"write\",\"model\":\"{_type}\",\"sid\":\"{_sid}\", \"data\":{_data}}}";
        }
    }
}