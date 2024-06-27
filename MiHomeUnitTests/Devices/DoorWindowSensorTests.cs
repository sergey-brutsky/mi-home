using System.Collections.Generic;
using MiHomeLib;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests
{
    public class DoorWindowSensorTests : IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _deviceFactory;

        public DoorWindowSensorTests(MiHomeDeviceFactoryFixture deviceFactory)
        {
            _deviceFactory = deviceFactory;
        }

        [Fact]
        public void Check_DoorWindowSensor_Hearbeat_Data()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2985 },
                        { "status", "open" },
                    });

            // Act
            DoorWindowSensor device = _deviceFactory.GetDeviceByCommand<DoorWindowSensor>(cmd);

            // Assert
            Assert.Equal("magnet", device.Type);
            Assert.Equal("158d0001233529", device.Sid);
            Assert.Equal(2.985f, device.Voltage);
            Assert.Equal("open", device.Status);
        }

        [Fact]
        public void Check_DoorWindowSensor_Report_Data()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("report", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2985 },
                        { "status", "open" },
                    });

            // Act
            DoorWindowSensor device = _deviceFactory.GetDeviceByCommand<DoorWindowSensor>(cmd);

            // Assert
            Assert.Equal("magnet", device.Type);
            Assert.Equal("158d0001233529", device.Sid);
            Assert.Equal(2.985f, device.Voltage);
            Assert.Equal("open", device.Status);
        }

        [Fact]
        public void Check_DoorWindowSensor_Raised_Open_Event()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2985 }
                    });

            // Act
            DoorWindowSensor device = _deviceFactory.GetDeviceByCommand<DoorWindowSensor>(cmd);

            bool openRaised = false;

            device.OnOpen += (_, __) => openRaised = true;

            cmd = Helpers
                .CreateCommand("report", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "status", "open" }
                    });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(openRaised);
        }

        [Fact]
        public void Check_DoorWindowSensor_Raised_Closed_Event()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2985 }
                    });

            // Act
            DoorWindowSensor device = _deviceFactory.GetDeviceByCommand<DoorWindowSensor>(cmd);

            bool closedRaised = false;

            device.OnClose += (_, __) => closedRaised = true;

            cmd = Helpers
                .CreateCommand("report", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "status", "close" }
                    });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(closedRaised);
        }

        [Fact]
        public void Check_DoorWindowSensor_Raised_NotClosedFor1Minute_Event()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2985 }
                    });

            // Act
            DoorWindowSensor device = _deviceFactory.GetDeviceByCommand<DoorWindowSensor>(cmd);

            bool notClosedRaised = false;

            device.NotClosedFor1Minute += (_, __) => notClosedRaised = true;

            cmd = Helpers
                .CreateCommand("report", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "no_close", "60" }
                    });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(notClosedRaised);
        }

        [Fact]
        public void Check_DoorWindowSensor_Raised_NotClosedFor5Minutes_Event()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2985 }
                    });

            // Act
            DoorWindowSensor device = _deviceFactory.GetDeviceByCommand<DoorWindowSensor>(cmd);

            bool notClosedRaised = false;

            device.NotClosedFor5Minutes += (_, __) => notClosedRaised = true;

            cmd = Helpers
                .CreateCommand("report", "magnet", "158d0001233529", 64996,
                    new Dictionary<string, object>
                    {
                        { "no_close", "300" }
                    });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(notClosedRaised);
        }
    }
}