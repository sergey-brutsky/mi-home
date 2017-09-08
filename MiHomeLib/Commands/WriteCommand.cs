namespace MiHomeLib.Commands
{
    internal class WriteCommand: Command
    {
        private readonly string _sid;
        private readonly string _data;

        public WriteCommand(string sid, string data)
        {
            _sid = sid;
            _data = data;
        }

        public override string ToString()
        {
            return $"{{\"cmd\":\"write\",\"sid\":\"{_sid}\", \"data\":{_data}}}";
        }
    }
}