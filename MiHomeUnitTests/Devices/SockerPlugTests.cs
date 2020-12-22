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
    public class SockerPlugTests : IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _deviceFactory;

        public SockerPlugTests(MiHomeDeviceFactoryFixture deviceFactory)
        {
            _deviceFactory = deviceFactory;
        }

        [Fact]
        public void Check_SocketPlug_Hearbeat_Data()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "plug", "158d00015dc332", 18916,
                    new Dictionary<string, object>
                    {
                        { "voltage", 3600 },
                        { "status", "on" },
                        { "inuse", "1" },
                        { "power_consumed", "39009" },
                        { "load_power", "3.20" },
                    });

            // Act
            SocketPlug device = _deviceFactory.GetDeviceByCommand<SocketPlug>(cmd);

            // Assert
            Assert.Equal("plug", device.Type);
            Assert.Equal("158d00015dc332", device.Sid);
            Assert.Equal(3.6f, device.Voltage);
            Assert.Equal("on", device.Status);
            Assert.Equal(1, device.Inuse);
            Assert.Equal(39009, device.PowerConsumed);
            Assert.Equal(3.2f, device.LoadPower);
        }

        [Fact]
        public void Check_SocketPlug_Report_Data()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("report", "plug", "158d00015dc332", 18916,
                    new Dictionary<string, object>
                    {
                        { "status", "off" }
                    });

            // Act
            SocketPlug device = _deviceFactory.GetDeviceByCommand<SocketPlug>(cmd);

            // Assert
            Assert.Equal("plug", device.Type);
            Assert.Equal("158d00015dc332", device.Sid);
            Assert.Equal("off", device.Status);
        }

        [Fact]
        public void Check_SocketPlug_TurnOn_Command()
        {
            // Arrange
            var transport = new Mock<IMessageTransport>();
            var command = JsonConvert.SerializeObject(new { status = "on" });
            var socketPlug = new SocketPlug("158d00015dc332", transport.Object);

            // Act
            socketPlug.TurnOn();
            
            // Assert
            transport.Verify(x => x
                .SendWriteCommand("158d00015dc332", "plug",
                    It.Is<SocketPlugCommand>(c => c.ToString() == command)), Times.Once());
        }

        [Fact]
        public void Check_SocketPlug_TurnOff_Command()
        {
            // Arrange
            var transport = new Mock<IMessageTransport>();
            var command = JsonConvert.SerializeObject(new { status = "off" });
            var socketPlug = new SocketPlug("158d00015dc332", transport.Object);

            // Act
            socketPlug.TurnOff();

            // Assert
            transport.Verify(x => x
                .SendWriteCommand("158d00015dc332", "plug",
                    It.Is<SocketPlugCommand>(c => c.ToString() == command)), Times.Once());
        }
    }
}