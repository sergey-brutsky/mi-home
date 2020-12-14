using System.Collections.Generic;
using MiHomeLib;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests
{
    public class SmokeSensorTests: IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _fixture;

        public SmokeSensorTests(MiHomeDeviceFactoryFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Check_SmokeSensor_NoAlarm_Event_Raised()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("report", "smoke", "158d0001d8f8f7", 25885,
                    new Dictionary<string, object>
                    {
                        { "alarm", "1" },
                    });

            // Act
            SmokeSensor device = _fixture.GetDeviceByCommand<SmokeSensor>(cmd);

            var alarmStoppedEventRaised = false;

            device.OnAlarmStopped += (_, args) =>
            {
                alarmStoppedEventRaised = true;
            };

            var cmd1 = JObjectHelpers
                .CreateCommand("report", "smoke", "158d0001d8f8f7", 25885,
                    new Dictionary<string, object>
                    {
                        { "alarm", "0" },
                    });

            device.ParseData(ResponseCommand.FromString(cmd1).Data);

            // Assert
            Assert.False(device.Alarm);
            Assert.True(alarmStoppedEventRaised);
        }

        [Fact]
        public void Check_SmokeSensor_Alarm_Event_Raised()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("report", "smoke", "158d0001d8f8f7", 25885,
                    new Dictionary<string, object>
                    {
                        { "alarm", "0" },
                    });

            // Act
            SmokeSensor device = _fixture.GetDeviceByCommand<SmokeSensor>(cmd);

            var alarmEventRaised = false;

            device.OnAlarm += (_, args) =>
            {
                alarmEventRaised = true;
            };

            var cmd1 = JObjectHelpers
                .CreateCommand("report", "smoke", "158d0001d8f8f7", 25885,
                    new Dictionary<string, object>
                    {
                        { "alarm", "1" },
                    });

            device.ParseData(ResponseCommand.FromString(cmd1).Data);

            // Assert
            Assert.True(device.Alarm);
            Assert.True(alarmEventRaised);
        }

        [Fact]
        public void Check_SmokeSensor_Heartbeat_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("heartbeat", "smoke", "158d0001d8f8f7", 25885,
                    new Dictionary<string, object>
                    {
                        { "voltage", "3065" },
                        { "alarm", "0" },
                    });

            // Act
            SmokeSensor device = _fixture.GetDeviceByCommand<SmokeSensor>(cmd);

            // Assert
            Assert.Equal("smoke", device.Type);
            Assert.Equal("158d0001d8f8f7", device.Sid);
            Assert.Equal(3.065f, device.Voltage);
            Assert.False(device.Alarm);
        }

        [Fact]
        public void Check_SmokeSensor_Report_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("report", "smoke", "158d0001d8f8f7", 25885,
                    new Dictionary<string, object>
                    {
                        { "density", "12" }
                    });

            // Act
            SmokeSensor device = _fixture.GetDeviceByCommand<SmokeSensor>(cmd);

            // Assert
            Assert.Equal("smoke", device.Type);
            Assert.Equal("158d0001d8f8f7", device.Sid);
            Assert.Equal(0.12f, device.Density);
        }

        [Fact]
        public void Check_SmokeSensor_ReadAck_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("read_ack", "smoke", "158d0001d8f8f7", 25885,
                    new Dictionary<string, object>
                    {
                        { "voltage", "3055" },
                        { "alarm", "0" }
                    });

            // Act
            SmokeSensor device = _fixture.GetDeviceByCommand<SmokeSensor>(cmd);

            // Assert
            Assert.Equal("smoke", device.Type);
            Assert.Equal("158d0001d8f8f7", device.Sid);
            Assert.Equal(3.055f, device.Voltage);
            Assert.False(device.Alarm);
        }
    }
}

