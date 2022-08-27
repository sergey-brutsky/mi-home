using System.Threading.Tasks;
using MiHomeLib.Devices;
using Moq;
using Xunit;
using System.Threading;
using System.Globalization;

namespace MiHomeUnitTests
{
    public class MiRobotV1Tests: MiioDeviceTest
    {
        [Fact]
        public void ToString_Returns_Valid_State()
        {
		    Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            // Arrange
            var response = "{\"result\":[{\"msg_ver\":8,\"msg_seq\":54,\"state\":8,\"battery\":100,\"clean_time\":729,\"clean_area\":9795000,\"error_code\":0,\"map_present\":1,\"in_cleaning\":0,\"fan_power\":60,\"dnd_enabled\":0}],\"id\":6}\0";
            var miioDevice = GetMiioDevice("get_status", response);

            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            var str = miRobot.ToString();

            // Assert            
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"get_status\", \"params\": [\"\"]}"), Times.Once());

            Assert.Equal($"Model: rockrobo.vacuum.v1\nState: Charging\n" +
                $"Battery: 100 %\nFanspeed: 60 %\n" +
                $"Cleaning since: 729 seconds\n" +
                $"Cleaned area: 9.795 m²\n" +
                $"IP Address: \nToken: ", str);
        }

        [Fact]
        public void FindMe_Should_Now_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = GetMiioDevice("find_me");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            miRobot.FindMe();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"find_me\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public async Task FindMeAsync_Should_Now_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = GetMiioDeviceAsync("find_me");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            await miRobot.FindMeAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"find_me\", \"params\": [\"\"]}"), Times.Once());
        }

       
        [Fact]
        public void Home_Should_Now_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = GetMiioDevice("app_pause");

            miioDevice
               .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("app_charge"))))
               .Returns($"{{\"result\":[\"ok\"],\"id\":2}}");

            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            miRobot.Home();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"app_pause\", \"params\": [\"\"]}"), Times.Once());
            miioDevice.Verify(x => x.SendMessage("{\"id\": 2, \"method\": \"app_charge\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public async Task HomeAsync_Should_Now_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = GetMiioDeviceAsync("app_pause");

            miioDevice
               .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("app_charge"))))
               .Returns(Task.FromResult($"{{\"result\":[\"ok\"],\"id\":2}}"));

            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            await miRobot.HomeAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"app_pause\", \"params\": [\"\"]}"), Times.Once());
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 2, \"method\": \"app_charge\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public void Start_Should_Now_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = GetMiioDevice("app_start");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            miRobot.Start();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"app_start\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public async Task StartAsync_Should_Now_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = GetMiioDeviceAsync("app_start");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            await miRobot.StartAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"app_start\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public void Stop_Should_Now_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = GetMiioDevice("app_stop");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            miRobot.Stop();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"app_stop\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public async Task StopAsync_Should_Now_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = GetMiioDeviceAsync("app_stop");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            await miRobot.StopAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"app_stop\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public void Pause_Should_Now_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = GetMiioDevice("app_pause");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            miRobot.Pause();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"app_pause\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public async Task PauseAsync_Should_Now_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = GetMiioDeviceAsync("app_pause");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            await miRobot.PauseAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"app_pause\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public void Spot_Should_Now_Throw_Exceptions()
        {
            // Arrange
            var miioDevice = GetMiioDevice("app_spot");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            miRobot.Spot();

            // Assert
            miioDevice.Verify(x => x.SendMessage("{\"id\": 1, \"method\": \"app_spot\", \"params\": [\"\"]}"), Times.Once());
        }

        [Fact]
        public async Task SpotAsync_Should_Now_Throw_ExceptionsAsync()
        {
            // Arrange
            var miioDevice = GetMiioDeviceAsync("app_spot");
            var miRobot = new MiRobotV1(miioDevice.Object);

            // Act
            await miRobot.SpotAsync();

            // Assert
            miioDevice.Verify(x => x.SendMessageAsync("{\"id\": 1, \"method\": \"app_spot\", \"params\": [\"\"]}"), Times.Once());
        }
    }
}
