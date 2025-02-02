using AutoFixture;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests.Devices
{
    public class AqaraVibrationSensorTests : MiHomeDeviceFactoryFixture
    {
        private readonly AqaraVirationSensor _device; 
        public AqaraVibrationSensorTests()
        {
            _device = _fixture.Create<AqaraVirationSensor>();
        }
        
        [Fact]
        public void Check_Vibration_Raised()
        {
            // Arrange
            bool eventRaised = false;
            var expected = "vibrate";

            _device.OnVibration += (_, __) => eventRaised = true;
            
            // Act
            _device.ParseData($"{{\"status\":\"vibrate\"}}");

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(expected, _device.LastStatus);
        }

        [Fact]
        public void Check_FreeFall_Raised()
        {
            // Arrange
            bool eventRaised = false;
            var expected = "free_fall";

            _device.OnFreeFall += (_, __) => eventRaised = true;
            
            // Act
            _device.ParseData($"{{\"status\":\"free_fall\"}}");

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(expected, _device.LastStatus);
        }

        [Fact]
        public void Check_Tilt_Raised()
        {
            // Arrange
            bool eventRaised = false;
            var expected = "tilt";

            _device.OnTilt += (_, __) => eventRaised = true;
            
            // Act
            _device.ParseData($"{{\"status\":\"tilt\"}}");

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(expected, _device.LastStatus);
        }
        
        [Fact]
        public void Check_FinalTiltAngle_Raised()
        {
            // Arrange
            bool eventRaised = false;
            var expectedAngle = 170;

            _device.OnFinalTiltAngle += (_, __) => eventRaised = true;
            
            // Act                        
            _device.ParseData($"{{\"final_tilt_angle\":\"{expectedAngle}\"}}");

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(expectedAngle, _device.FinalTiltAngle);
        }

        [Fact]
        public void Check_Coordination_Raised()
        {
            // Arrange
            bool eventRaised = false;
            var (x, y, z) = (1, 2, 3);

            _device.OnCoordinations += (_, __) => eventRaised = true;
            
            // Act                        
            _device.ParseData($"{{\"coordination\":\"{x},{y},{z}\"}}");

            // Assert
            Assert.True(eventRaised);
            Assert.Equal((x,y,z), _device.Coordinations);
        }

        [Fact]
        public void Check_BedActivity_Raised()
        {
            // Arrange
            bool eventRaised = false;
            var expected = 200;

            _device.OnBedActivity += (_, __) => eventRaised = true;
            
            // Act                        
            _device.ParseData($"{{\"bed_activity\":\"{expected}\"}}");

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(expected, _device.BedActivity);
        }

        [Fact]
        public void Check_Voltage()
        {
            // Arrange
            float? expected = 3.005f;

            // Act                        
            _device.ParseData($"{{\"voltage\":\"3005\"}}");

            // Assert
            Assert.Equal(expected, _device.Voltage);
        }
    }
}