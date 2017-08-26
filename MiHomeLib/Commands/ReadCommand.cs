using MiHomeLib.Devices;

namespace MiHomeLib.Commands
{
    internal class ReadCommand
    {
        private readonly MiHomeDevice _device;

        public ReadCommand(MiHomeDevice device)
        {
            _device = device;
        }

        public override string ToString()
        {
            return $"{{\"cmd\":\"read\",\"sid\":\"{_device.Sid}\"}}";
        }
    }
}
