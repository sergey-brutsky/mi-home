using Microsoft.Extensions.Logging;
using MiHomeLib;
using MiHomeLib.Contracts;
using Moq;
using Xunit;

namespace MiHomeUnitTests
{
    public class MiHomeUnitTests
    {
        private readonly Mock<IMessageTransport> _transport;
        private readonly Mock<ILogger<MiHome>> _logger;

        public MiHomeUnitTests()
        {
            _transport = new Mock<IMessageTransport>();
            _logger = new Mock<ILogger<MiHome>>();
            _logger.MockLog(LogLevel.Warning);
        }

        [Fact]
        public void Add_WellKnown_Device_Should_Not_Throw_Exception_And_Warning()
        {
            var data = "{\"cmd\":\"report\",\"model\":\"sensor_ht\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{}\"}";

            _transport.Setup(x => x.ReceiveAsync()).ReturnsAsync(data);

            var miHome = new MiHome(_transport.Object, _logger.Object);
            miHome.Dispose();
            _logger.Verify(LogLevel.Warning, Times.Never());
        }

        [Fact]
        public void Add_Unknown_Device_Should_Not_Throw_Exception_But_Show_Warning()
        {
            var data = "{\"cmd\":\"report\",\"model\":\"ctrl_xxx.aq1\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{}\"}";

            _transport.Setup(x => x.ReceiveAsync()).ReturnsAsync(data);

            var miHome = new MiHome(_transport.Object, _logger.Object);
            miHome.Dispose();
            _logger.Verify(LogLevel.Warning, Times.Once());
        }
    }
}