using MiHomeLib;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;
using MiHomeLib.Devices;
using Moq;

namespace MiHomeUnitTests
{
    public class MiHomeDeviceFactoryFixture
    {
        public MiHomeDeviceFactory MiHomeDeviceFactory { get; private set; }

        public MiHomeDeviceFactoryFixture()
        {
            MiHomeDeviceFactory = new MiHomeDeviceFactory(new Mock<IMessageTransport>().Object);
        }

        public T GetDeviceByCommand<T>(string cmd) where T : MiHomeDevice
        {
            var respCmd = ResponseCommand.FromString(cmd);
            T device = MiHomeDeviceFactory.CreateByModel(respCmd.Model, respCmd.Sid) as T;
            device.ParseData(respCmd.Data);
            return device;
        }
    }
}