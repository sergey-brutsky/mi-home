using System.Threading.Tasks;
using MiHomeLib.Devices;
using Moq;
using Xunit;

namespace MiHomeUnitTests
{
    public class MiioGatewayTests
    {
        [Fact]
        public void IsArmingOn_Returns_Arming_State()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"on\"],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var arming = miioGateway.IsArmingOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_arming\", \"params\": []}"), Times.Once());
            Assert.True(arming);
        }

        [Fact]
        public async Task IsArmingOnAsync_Returns_Arming_StateAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"off\"],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var arming = await miioGateway.IsArmingOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_arming\", \"params\": []}"), Times.Once());
            Assert.False(arming);
        }

        [Fact]
        public void SetArmingOn_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            miioGateway.SetArmingOn();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_arming\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public async Task SetArmingOnAsync_Should_Not_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            await miioGateway.SetArmingOnAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_arming\", \"params\": [\"on\"]}"), Times.Once());
        }

        [Fact]
        public void SetArmingOff_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            miioGateway.SetArmingOff();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_arming\", \"params\": [\"off\"]}"), Times.Once());
        }

        [Fact]
        public async Task SetArmingOffAsync_Should_Not_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            await miioGateway.SetArmingOffAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_arming\", \"params\": [\"off\"]}"), Times.Once());
        }

        [Fact]
        public void GetArmingWaitTime_Returns_Integer()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[15],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingWaitTime = miioGateway.GetArmingWaitTime();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_arm_wait_time\", \"params\": []}"), Times.Once());
            Assert.Equal(15, armingWaitTime);
        }

        [Fact]
        public async Task GetArmingWaitTimeAsync_Returns_IntegerAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[30],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingWaitTime = await miioGateway.GetArmingWaitTimeAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_arm_wait_time\", \"params\": []}"), Times.Once());
            Assert.Equal(30, armingWaitTime);
        }

        [Fact]
        public void SetArmingWaitTime_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            miioGateway.SetArmingWaitTime(20);

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_arm_wait_time\", \"params\": [20]}"), Times.Once());
        }

        [Fact]
        public async Task SetWaitTimeAsync_Should_Not_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            await miioGateway.SetArmingWaitTimeAsync(30);

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_arm_wait_time\", \"params\": [30]}"), Times.Once());
        }

        [Fact]
        public void GetArmingOffTime_Returns_Integer()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[15],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingOffTime = miioGateway.GetArmingOffTime();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_device_prop\", \"params\": [\"lumi.0\",\"alarm_time_len\"]}"), Times.Once());
            Assert.Equal(15, armingOffTime);
        }

        [Fact]
        public async Task GetArmingOffTimeAsync_Returns_IntegerAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[30],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingOffTime = await miioGateway.GetArmingOffTimeAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_device_prop\", \"params\": [\"lumi.0\",\"alarm_time_len\"]}"), Times.Once());
            Assert.Equal(30, armingOffTime);
        }

        [Fact]
        public void SetArmingOffTime_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            miioGateway.SetArmingOffTime(40);

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_device_prop\", \"params\": {\"sid\":\"lumi.0\", \"alarm_time_len\":40}}"), Times.Once());
        }

        [Fact]
        public async Task SetArmingOffTimeAsync_Should_Not_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            await miioGateway.SetArmingOffTimeAsync(45);

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_device_prop\", \"params\": {\"sid\":\"lumi.0\", \"alarm_time_len\":45}}"), Times.Once());
        }

        [Fact]
        public void GetArmingBlinkingTime_Returns_Integer()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[15],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingBlinkingTime = miioGateway.GetArmingBlinkingTime();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_device_prop\", \"params\": [\"lumi.0\",\"en_alarm_light\"]}"), Times.Once());
            Assert.Equal(15, armingBlinkingTime);
        }

        [Fact]
        public async Task GetArmingBlinkingTimeAsync_Returns_IntegerAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[30],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingBlinkingTime = await miioGateway.GetArmingBlinkingTimeAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_device_prop\", \"params\": [\"lumi.0\",\"en_alarm_light\"]}"), Times.Once());
            Assert.Equal(30, armingBlinkingTime);
        }

        [Fact]
        public void SetArmingBlinkingTime_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            miioGateway.SetArmingBlinkingTime(40);

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_device_prop\", \"params\": {\"sid\":\"lumi.0\", \"en_alarm_light\":40}}"), Times.Once());
        }

        [Fact]
        public async Task SetArmingBlinkingTimeAsync_Should_Not_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            await miioGateway.SetArmingBlinkingTimeAsync(45);

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_device_prop\", \"params\": {\"sid\":\"lumi.0\", \"en_alarm_light\":45}}"), Times.Once());
        }

        [Fact]
        public void GetArmingVolume_Returns_Integer()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[10],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingVolume = miioGateway.GetArmingVolume();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_alarming_volume\", \"params\": []}"), Times.Once());
            Assert.Equal(10, armingVolume);
        }

        [Fact]
        public async Task GetArmingVolumeAsync_Returns_IntegerAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[20],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var armingVolume = await miioGateway.GetArmingVolumeAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_alarming_volume\", \"params\": []}"), Times.Once());
            Assert.Equal(20, armingVolume);
        }

        [Fact]
        public void SetArmingVolume_Should_Not_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[\"ok\"],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            miioGateway.SetArmingVolume(15);

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"set_alarming_volume\", \"params\": [15]}"), Times.Once());
        }

        [Fact]
        public async Task SetArmingVolumeAsync_Should_Not_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[\"ok\"],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            await miioGateway.SetArmingVolumeAsync(30);

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"set_alarming_volume\", \"params\": [30]}"), Times.Once());
        }

        [Fact]
        public void GetArmingLastTimeTriggeredTimestamp_Returns_Integer()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessage(It.IsAny<string>())).Returns("{\"result\":[1609150074],\"id\":1}");
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var timestamp = miioGateway.GetArmingLastTimeTriggeredTimestamp();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_arming_time\", \"params\": []}"), Times.Once());
            Assert.Equal(1609150074, timestamp);
        }

        [Fact]
        public async Task GetArmingLastTimeTriggeredTimestampAsync_Returns_IntegerAsync()
        {
            // Arrange
            var miioDevice = new Mock<IMiioTransport>();
            miioDevice.Setup(x => x.SendMessageAsync(It.IsAny<string>())).Returns(Task.FromResult("{\"result\":[1609150074],\"id\":1}"));
            var miioGateway = new MiioGateway(miioDevice.Object);

            // Act
            var timestamp = await miioGateway.GetArmingLastTimeTriggeredTimestampAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"get_arming_time\", \"params\": []}"), Times.Once());
            Assert.Equal(1609150074, timestamp);
        }
    }
}