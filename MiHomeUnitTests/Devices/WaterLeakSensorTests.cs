using System.Collections.Generic;
using MiHomeLib;
using MiHomeLib.Commands;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeUnitTests
{
    public class WaterLeakSensorTests : IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private readonly MiHomeDeviceFactoryFixture _deviceFactory;

        public WaterLeakSensorTests(MiHomeDeviceFactoryFixture deviceFactory)
        {
            _deviceFactory = deviceFactory;
        }

        [Fact]
        public void Check_WaterLeakSensor_Report_Data()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("report", "sensor_wleak.aq1", "158d0001d561e2", 18101,
                    new Dictionary<string, object>
                    {
                        { "status", "no_leak" }
                    });

            // Act
            WaterLeakSensor device = _deviceFactory.GetDeviceByCommand<WaterLeakSensor>(cmd);

            // Assert
            Assert.Equal("sensor_wleak.aq1", device.Type);
            Assert.Equal("158d0001d561e2", device.Sid);
            Assert.Equal("no_leak", device.Status);
        }

        [Fact]
        public void Check_WaterLeakSensor_Heartbeat_Data()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "sensor_wleak.aq1", "158d0001d561e2", 18101,
                    new Dictionary<string, object>
                    {
                        { "voltage", "3005" }
                    });

            // Act
            WaterLeakSensor device = _deviceFactory.GetDeviceByCommand<WaterLeakSensor>(cmd);

            // Assert
            Assert.Equal("sensor_wleak.aq1", device.Type);
            Assert.Equal("158d0001d561e2", device.Sid);
            Assert.Equal(3.005f, device.Voltage);
        }

        [Fact]
        public void Check_WaterLeakSensor_Raised_Leak()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "sensor_wleak.aq1", "158d0001d561e2", 18101,
                    new Dictionary<string, object>
                    {
                        { "voltage", "3005" }
                    });

            // Act
            WaterLeakSensor device = _deviceFactory.GetDeviceByCommand<WaterLeakSensor>(cmd);

            bool leakRaised = false;

            device.OnLeak += (_, __) => leakRaised = true; ;

            cmd = Helpers
                .CreateCommand("report", "sensor_wleak.aq1", "158d0001d561e2", 18101,
                    new Dictionary<string, object>
                    {
                        { "status", "leak" }
                    });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(leakRaised);
        }

        [Fact]
        public void Check_WaterLeakSensor_Raised_NoLeak()
        {
            // Arrange
            var cmd = Helpers
                .CreateCommand("heartbeat", "sensor_wleak.aq1", "158d0001d561e2", 18101,
                    new Dictionary<string, object>
                    {
                        { "voltage", "3005" }
                    });

            // Act
            WaterLeakSensor device = _deviceFactory.GetDeviceByCommand<WaterLeakSensor>(cmd);

            bool noleakRaised = false;

            device.OnNoLeak += (_, __) => noleakRaised = true; ;

            cmd = Helpers
                .CreateCommand("report", "sensor_wleak.aq1", "158d0001d561e2", 18101,
                    new Dictionary<string, object>
                    {
                        { "status", "no_leak" }
                    });

            device.ParseData(ResponseCommand.FromString(cmd).Data);

            // Assert
            Assert.True(noleakRaised);
        }
    }
}