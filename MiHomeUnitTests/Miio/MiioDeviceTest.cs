using System.Threading.Tasks;
using MiHomeLib;
using Moq;

namespace MiHomeUnitTests
{
    public class MiioDeviceTest
    {
        protected static Mock<IMiioTransport> GetMiioDevice(string method, string response)
        {
            var miioDevice = new Mock<IMiioTransport>();

            miioDevice
                .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains(method))))
                .Returns(response);

            return miioDevice;
        }

        protected static Mock<IMiioTransport> GetMiioDevice(string method, int id = 1)
        {
            var miioDevice = new Mock<IMiioTransport>();

            miioDevice
                .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains(method))))
                .Returns($"{{\"result\":[\"ok\"],\"id\":{id}}}");

            return miioDevice;
        }

        protected static Mock<IMiioTransport> GetMiioDeviceAsync(string method, int id = 1)
        {
            var miioDevice = new Mock<IMiioTransport>();

            miioDevice
                .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains(method))))
                .Returns(Task.FromResult($"{{\"result\":[\"ok\"],\"id\":{id}}}"));

            return miioDevice;
        }
    }
}