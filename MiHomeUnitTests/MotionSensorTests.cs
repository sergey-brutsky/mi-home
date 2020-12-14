using System;
using System.Collections.Generic;
using MiHomeLib;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests
{
    public class MotionSensorTests: IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _fixture;

        public MotionSensorTests(MiHomeDeviceFactoryFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Check_MotionSensor_ReadAck_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("read_ack", "motion", "158d0001214e1f", 19149,
                    new Dictionary<string, object>
                    {
                        { "voltage", "2985" }
                    });

            // Act
            MotionSensor device = _fixture.GetDeviceByCommand<MotionSensor>(cmd);

            // Assert
            Assert.Equal("motion", device.Type);
            Assert.Equal("158d0001214e1f", device.Sid);
            Assert.Equal(2.985f, device.Voltage);
        }

        [Fact]
        public void Check_MotionSensor_Reports_NoMotion_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("report", "motion", "158d00011c0", 52754,
                    new Dictionary<string, object>
                    {
                        { "no_motion", "120" }
                    });

            // Act
            MotionSensor device = _fixture.GetDeviceByCommand<MotionSensor>(cmd);

            var noMotionRaised = false;
            var moMotionSeconds = 0;

            device.OnNoMotion += (_, args) =>
            {
                noMotionRaised = true;
                moMotionSeconds = args.Seconds;
            };

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.Equal("motion", device.Type);
            Assert.Equal("158d00011c0", device.Sid);
            Assert.Equal("no motion", device.Status);
            Assert.True(noMotionRaised);
            Assert.Equal(120, device.NoMotion);
        }

        [Fact]
        public void Check_MotionSensor_Reports_Motion_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("report", "motion", "158d00011c0", 52754,
                    new Dictionary<string, object>
                    {
                        { "status", "motion" }
                    });

            // Act
            MotionSensor device = _fixture.GetDeviceByCommand<MotionSensor>(cmd);

            var motionRaised = false;

            device.OnMotion += (_, args) =>
            {
                motionRaised = true;
            };

            var timeDiff = DateTime.Now - device.MotionDate.Value;

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.Equal("motion", device.Type);
            Assert.Equal("158d00011c0", device.Sid);
            Assert.Equal("motion", device.Status);
            Assert.True(timeDiff <= TimeSpan.FromSeconds(1));
            Assert.True(motionRaised);
        }
    }
}

