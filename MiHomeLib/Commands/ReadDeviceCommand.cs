namespace MiHomeLib.Commands
{
    internal class ReadDeviceCommand: Command
    {
        private readonly string _sid;
        
        public ReadDeviceCommand(string sid)
        {
            _sid = sid;
        }

        public override string ToString()
        {
            return $"{{\"cmd\":\"read\",\"sid\":\"{_sid}\"}}";
        }
    }
}
