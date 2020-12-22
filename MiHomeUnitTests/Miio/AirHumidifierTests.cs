using System.Threading.Tasks;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests
{
    public class AirHumidifierTests
    {
        [Fact]
        public void PowerOn_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            airHumidifier.PowerOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_power\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public async Task PowerOnAsync_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            await airHumidifier.PowerOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_power\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public void PowerOff_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            airHumidifier.PowerOff();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_power\", \"params\": [\"off\"]}"), Times.Once());
        }

        [Fact]
        public async Task PowerOffAsync_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            await airHumidifier.PowerOffAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_power\", \"params\": [\"off\"]}"), Times.Once());
        }

        [Fact]
        public void SetMode_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            airHumidifier.SetMode(AirHumidifier.Mode.High);

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_mode\", \"params\": [\"high\"]}"), Times.Once());
        }

        [Fact]
        public async Task SetModeAsync_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            await airHumidifier.SetModeAsync(AirHumidifier.Mode.Medium);

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_mode\", \"params\": [\"medium\"]}"), Times.Once());
        }

        [Fact]
        public void IsTurnedOn_Returns_State_Power()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"on\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var power = airHumidifier.IsTurnedOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"power\"]}"), Times.Once());
            Assert.True(power);
        }

        [Fact]
        public async void IsTurnedOnAsync_Returns_State_Power()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"on\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var power = await airHumidifier.IsTurnedOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"power\"]}"), Times.Once());
            Assert.True(power);
        }

        [Fact]
        public void GetDeviceMode_Returns_Valid_Mode()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"high\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var mode = airHumidifier.GetDeviceMode();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"mode\"]}"), Times.Once());
            Assert.Equal(AirHumidifier.Mode.High, mode);
        }

        [Fact]
        public async void GetDeviceModeAsync_Returns_Valid_Mode()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"medium\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var mode = await airHumidifier.GetDeviceModeAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"mode\"]}"), Times.Once());
            Assert.Equal(AirHumidifier.Mode.Medium, mode);
        }

        [Fact]
        public void GetTemperature_Returns_Valid_Temperature()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"323\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var temperature = airHumidifier.GetTemperature();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"temp_dec\"]}"), Times.Once());
            Assert.Equal(32.3f, temperature);
        }

        [Fact]
        public async Task GetTemperatureAsync_Returns_Valid_Temperature()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"323\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var temperature = await airHumidifier.GetTemperatureAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"temp_dec\"]}"), Times.Once());
            Assert.Equal(32.3f, temperature);
        }

        [Fact]
        public void GetHumidity_Returns_Valid_Humidity()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"45\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var humidity = airHumidifier.GetHumidity();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"humidity\"]}"), Times.Once());
            Assert.Equal(45, humidity);
        }

        [Fact]
        public async Task GetHumidityAsync_Returns_Valid_Humidity()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"41\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var humidity = await airHumidifier.GetHumidityAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"humidity\"]}"), Times.Once());
            Assert.Equal(41, humidity);
        }

        [Fact]
        public void GetBrightness_Returns_Valid_Brightness()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"0\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var brightness = airHumidifier.GetBrightness();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"led_b\"]}"), Times.Once());
            Assert.Equal(AirHumidifier.Brightness.Bright, brightness);
        }

        [Fact]
        public async void GetBrightnessAsync_Returns_Valid_Brightness()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"2\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var brightness = await airHumidifier.GetBrightnessAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"led_b\"]}"), Times.Once());
            Assert.Equal(AirHumidifier.Brightness.Off, brightness);
        }

        [Fact]
        public void SetBrightness_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            airHumidifier.SetBrightness(AirHumidifier.Brightness.Bright);

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_led_b\", \"params\": [0]}"), Times.Once());
        }

        [Fact]
        public async Task SetBrightnessAsync_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            await airHumidifier.SetBrightnessAsync(AirHumidifier.Brightness.Off);

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_led_b\", \"params\": [2]}"), Times.Once());
        }

        [Fact]
        public void GetTargetHumidity_Returns_Valid_TargetHumidity()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"50\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var targetHumidity = airHumidifier.GetTargetHumidity();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"limit_hum\"]}"), Times.Once());
            Assert.Equal(50, targetHumidity);
        }

        [Fact]
        public async void GetTargetHumidityAsync_Returns_Valid_TargetHumidity()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"50\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var targetHumidity = await airHumidifier.GetTargetHumidityAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"limit_hum\"]}"), Times.Once());
            Assert.Equal(50, targetHumidity);
        }

        [Fact]
        public void IsBuzzerOn_Returns_Valid_BuzzerState()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"on\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var buzzer = airHumidifier.IsBuzzerOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"buzzer\"]}"), Times.Once());
            Assert.True(buzzer);
        }

        [Fact]
        public async void IsBuzzerOnAsync_Returns_Valid_BuzzerState()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"off\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var buzzer = await airHumidifier.IsBuzzerOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"buzzer\"]}"), Times.Once());
            Assert.False(buzzer);
        }

        [Fact]
        public void BuzzerOn_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            airHumidifier.BuzzerOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_buzzer\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public async Task BuzzerOnAsync_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            await airHumidifier.BuzzerOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_buzzer\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public void IsChildLockOn_Returns_Valid_ChildLockState()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"on\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var childLock = airHumidifier.IsChildLockOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"child_lock\"]}"), Times.Once());
            Assert.True(childLock);
        }

        [Fact]
        public async void IsChildLockOnAsync_Returns_Valid_ChildLockState()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"off\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var childLock = await airHumidifier.IsChildLockOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"child_lock\"]}"), Times.Once());
            Assert.False(childLock);
        }

        [Fact]
        public void ChildLockOn_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            airHumidifier.ChildLockOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_child_lock\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public async Task ChildLockOnAsync_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            await airHumidifier.ChildLockOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_child_lock\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public void ToString_Returns_Valid_State()
        {
            // Arrange
            var miioDevice = new Mock<IMiioDevice>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>()))
                .Returns("{\"result\": [\"on\", \"high\", \"323\", \"45\", \"0\", \"on\", \"off\", \"50\"], \"id\": 1}");
            var airHumidifier = new AirHumidifier(miioDevice.Object);

            // Act
            var str = airHumidifier.ToString();

            // Assert            
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_prop\", \"params\": [\"power\",\"mode\",\"temp_dec\",\"humidity\",\"led_b\",\"buzzer\",\"child_lock\",\"limit_hum\"]}"), Times.Once());
            Assert.Equal("Power: on\nMode: high\nTemperature: 32.3 °C\nHumidity: 45%\nLED brightness: bright\nBuzzer: on\nChild lock: off\nTarget humidity: 50%\nModel: zhimi.humidifier.v1\nIP Address:\nToken: ", str);
        }
    }
}