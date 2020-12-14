using System.Collections.Generic;
using MiHomeLib;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests
{
    public class ThSensorTests: IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _fixture;

        public ThSensorTests(MiHomeDeviceFactoryFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Check_ThSensor_Raised_Temperature_And_Humdity_Change()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("heartbeat", "sensor_ht", "158d000156a94b", 20911,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2995 },
                        { "temperature", "1601" },
                        { "humidity", "7214" },
                    });

            ThSensor device = _fixture.GetDeviceByCommand<ThSensor>(cmd);

            bool temperatureEventRaised = false;
            bool humidityEventRaised = false;
            float newTemperature = 0;
            float newHumidity = 0;

            device.OnTemperatureChange += (_, args) =>
            {
                temperatureEventRaised = true;
                newTemperature = args.Temperature;
            };

            device.OnHumidityChange += (_, args) =>
            {
                humidityEventRaised = true;
                newHumidity = args.Humidity;
            };

            // Act
            cmd = JObjectHelpers
                .CreateCommand("heartbeat", "sensor_ht", "158d000156a94b", 20911,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2995 },
                        { "temperature", "1901" },
                        { "humidity", "7502" },
                    });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(temperatureEventRaised);
            Assert.True(humidityEventRaised);
            Assert.Equal(19.01f, newTemperature);
            Assert.Equal(75.02f, newHumidity);
        }

        [Fact]
        public void Check_ThSensor_Humidity_Report_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("report", "sensor_ht", "158d000156a94b", 20911,
                    new Dictionary<string, object>
                    {
                        { "humidity", "7159" }
                    });

            // Act
            ThSensor device = _fixture.GetDeviceByCommand<ThSensor>(cmd);

            // Assert
            Assert.Equal("sensor_ht", device.Type);
            Assert.Equal("158d000156a94b", device.Sid);
            Assert.Equal(71.59f, device.Humidity);
        }

        [Fact]
        public void Check_ThSensor_Temperature_Report_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("report", "sensor_ht", "158d000156a94b", 20911,
                    new Dictionary<string, object>
                    {
                        { "temperature", "1602" }
                    });

            // Act
            ThSensor device = _fixture.GetDeviceByCommand<ThSensor>(cmd);

            // Assert
            Assert.Equal("sensor_ht", device.Type);
            Assert.Equal("158d000156a94b", device.Sid);
            Assert.Equal(16.02f, device.Temperature);
        }

        
        [Fact]
        public void Check_ThSensor_Hearbeat_Data()
        {
            // Arrange
            var cmd = JObjectHelpers
                .CreateCommand("heartbeat", "sensor_ht", "158d000156a94b", 20911,
                    new Dictionary<string, object>
                    {
                        { "voltage", 2995 },
                        { "temperature", "1601" },
                        { "humidity", "7214" },
                    });

            // Act
            ThSensor device = _fixture.GetDeviceByCommand<ThSensor>(cmd);

            // Assert
            Assert.Equal("sensor_ht", device.Type);
            Assert.Equal("158d000156a94b", device.Sid);
            Assert.Equal(2.995f, device.Voltage);
            Assert.Equal(16.01f, device.Temperature);
            Assert.Equal(72.14f, device.Humidity);
        }
    }
}