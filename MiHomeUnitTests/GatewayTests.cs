using System.Collections.Generic;
using MiHomeLib;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;
using MiHomeLib.Devices;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace MiHomeUnitTests
{
    public class GatewayTests : IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _deviceFactory;

        public GatewayTests(MiHomeDeviceFactoryFixture deviceFactory)
        {
            _deviceFactory = deviceFactory;
        }

        [Fact]
        public void Check_Gateway_Hearbeat_Data()
        {
            // Arrange
            Gateway device = new Gateway("34ce1188db36", new Mock<IMessageTransport>().Object);

            var cmd = JObjectHelpers
                .CreateCommand("heartbeat", "gateway", "34ce1188db36", 0,
                    new Dictionary<string, object>
                    {
                        { "ip", "192.168.1.1" }
                    });

            // Act
            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.Equal("gateway", device.Type);
            Assert.Equal("34ce1188db36", device.Sid);
            Assert.Equal("192.168.1.1", device.Ip);
        }

        [Fact]
        public void Check_Gateway_Report_Data()
        {
            // Arrange
            Gateway device = new Gateway("34ce1188db36", new Mock<IMessageTransport>().Object);

            var cmd = JObjectHelpers
                .CreateCommand("report", "gateway", "34ce1188db36", 0,
                    new Dictionary<string, object>
                    {
                        { "rgb", 0 },
                        { "illumination", 495 }
                    });

            // Act
            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.Equal("gateway", device.Type);
            Assert.Equal("34ce1188db36", device.Sid);
            Assert.Equal(0, device.Rgb);
            Assert.Equal(495, device.Illumination);
        }

        [Fact]
        public void Check_Gateway_EnableLight_Command()
        {
            // Arrange
            var transport = new Mock<IMessageTransport>();
            var command = JsonConvert.SerializeObject(new { rgb = 1694433280 });
            var gateway = new Gateway("34ce1188db36", transport.Object);

            // Act
            gateway.EnableLight(255, 0, 0, 100);

            // Assert
            transport.Verify(x => x
                .SendWriteCommand("34ce1188db36", "gateway",
                    It.Is<GatewayLightCommand>(c => c.ToString() == command)), Times.Once());
        }

        [Fact]
        public void Check_Gateway_DisableLight_Command()
        {
            // Arrange
            var transport = new Mock<IMessageTransport>();
            var command = JsonConvert.SerializeObject(new { rgb = 0 });
            var gateway = new Gateway("34ce1188db36", transport.Object);

            // Act
            gateway.DisableLight();

            // Assert
            transport.Verify(x => x
                .SendWriteCommand("34ce1188db36", "gateway",
                    It.Is<GatewayLightCommand>(c => c.ToString() == command)), Times.Once());
        }

        [Fact]
        public void Check_Gateway_StartPlayMusic_Command()
        {
            // Arrange
            var transport = new Mock<IMessageTransport>();
            var command = JsonConvert.SerializeObject(new { mid = 1 });
            var gateway = new Gateway("34ce1188db36", transport.Object);

            // Act
            gateway.StartPlayMusic(1);

            // Assert
            transport.Verify(x => x
                .SendWriteCommand("34ce1188db36", "gateway",
                    It.Is<GatewayMusicCommand>(c => c.ToString() == command)), Times.Once());
        }

        [Fact]
        public void Check_Gateway_StopPlayMusic_Command()
        {
            // Arrange
            var transport = new Mock<IMessageTransport>();
            var command = JsonConvert.SerializeObject(new { mid = 1000 });
            var gateway = new Gateway("34ce1188db36", transport.Object);

            // Act
            gateway.StopPlayMusic();

            // Assert
            transport.Verify(x => x
                .SendWriteCommand("34ce1188db36", "gateway",
                    It.Is<GatewayMusicCommand>(c => c.ToString() == command)), Times.Once());
        }
    }
}