using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.Exceptions;
using Xunit;

namespace MiHomeUnitTests
{

    public class MiHomeDeviceFactoryTests: IClassFixture<MiHomeDeviceFactoryFixture>
    {
        private MiHomeDeviceFactory _mihomeDeviceFactory;

        public MiHomeDeviceFactoryTests(MiHomeDeviceFactoryFixture fixture)
        {
            _mihomeDeviceFactory = fixture.MiHomeDeviceFactory;
        }

        [Theory]
        [InlineData("sensor_cube.aqgl01", "34ce0088db36", typeof(AqaraCubeSensor))]
        [InlineData("sensor_motion.aq2", "34ce0088db36", typeof(AqaraMotionSensor))]
        [InlineData("sensor_magnet.aq2", "34ce0088db36", typeof(AqaraOpenCloseSensor))]
        [InlineData("magnet", "34ce0088db36", typeof(DoorWindowSensor))]
        [InlineData("motion", "34ce0088db36", typeof(MotionSensor))]
        [InlineData("smoke", "34ce0088db36", typeof(SmokeSensor))]
        [InlineData("plug", "34ce0088db36", typeof(SocketPlug))]
        [InlineData("switch", "34ce0088db36", typeof(Switch))]
        [InlineData("sensor_ht", "34ce0088db36", typeof(ThSensor))]
        [InlineData("sensor_wleak.aq1", "34ce0088db36", typeof(WaterLeakSensor))]
        [InlineData("weather.v1", "34ce0088db36", typeof(WeatherSensor))]
        [InlineData("ctrl_neutral2", "34ce0088db36", typeof(WiredDualWallSwitch))]
        [InlineData("remote.b286acn01", "34ce0088db36", typeof(WirelessDualWallSwitch))]
        public void CheckCreateByModelMethod(string model, string sid, System.Type type)
        {
            // Act 
            var device = _mihomeDeviceFactory.CreateByModel(model, sid);

            // Assert
            Assert.IsType(type, device);
        }

        [Fact]
        public void CreateByModel_With_UnknownModel_ShouldThrowException()
        {
            Assert.Throws<ModelNotSupportedException>(() => _mihomeDeviceFactory.CreateByModel("aaa", "bbb"));
        }
    }
}