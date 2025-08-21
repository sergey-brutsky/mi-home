using System.Threading.Tasks;
using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using System.Threading;
using System.Globalization;

namespace MiHomeUnitTests.MiioDevicesTests;

public class XiaomiRobotVacuumCleanerV1Tests: MiioDeviceBase
{
    private readonly XiaomiRobotVacuumCleanerV1 _miRobot;

    public XiaomiRobotVacuumCleanerV1Tests()
    {
        _miRobot = new XiaomiRobotVacuumCleanerV1(_miioTransport.Object);
    }

    [Fact]
    public void ToString_Returns_Valid_State()
    {
		Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

        // Arrange        
        SendResultMethod("get_status", new 
        { 
            msg_ver = 8, 
            msg_seq = 54, 
            state = 8, 
            battery = 100, 
            clean_time = 729, 
            clean_area = 9795000, 
            error_code = 0, 
            map_present = 1, 
            in_cleaning = 0, 
            fan_power = 60, 
            dnd_enabled = 0 
        });

        // // Act
        var str = _miRobot.ToString();

        // Assert            
        VerifyMethod("get_status", string.Empty);

        str.Should().Be($"Model: rockrobo.vacuum.v1\nState: Charging\n" +
            $"Battery: 100 %\nFanspeed: 60 %\n" +
            $"Cleaning since: 729 seconds\n" +
            $"Cleaned area: 9.795 m²\n" +
            $"IP Address: \nToken: ");
    }

    [Fact]
    public void FindMe_Should_Now_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("find_me", "ok");
        
        // Act
        _miRobot.FindMe();

        // Assert
        VerifyMethod("find_me", string.Empty);
    }

    [Fact]
    public async Task FindMeAsync_Should_Now_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync("find_me", "ok");
        
        // Act
        await _miRobot.FindMeAsync();

        // Assert
        VerifyMethodAsync("find_me", string.Empty);
    }

    [Fact]
    public void Home_Should_Now_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("app_pause", "ok");

        _miioTransport
            .Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("app_charge"))))
            .Returns(ToJson(new { result = new[] { "ok" }, id = 2 }));    
        
        // Act
        _miRobot.Home();

        // Assert
        VerifyMethod("app_pause", string.Empty);
        
        _miioTransport
            .Verify(x => x.SendMessage(ToJson(new { id = 2, method = "app_charge", @params = new[] {string.Empty} })), Times.Once());
    }

    [Fact]
    public async Task HomeAsync_Should_Now_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync("app_pause", "ok");

        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("app_charge"))))
            .Returns(Task.FromResult(ToJson(new { result = new[] { "ok" }, id = 2 })));
        
        // Act
        await _miRobot.HomeAsync();

        // Assert
        VerifyMethodAsync("app_pause", string.Empty);

        _miioTransport
            .Verify(x => x.SendMessageAsync(ToJson(new { id = 2, method = "app_charge", @params = new[] {string.Empty}})), Times.Once());
    }

    [Fact]
    public void Start_Should_Now_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("app_start", "ok");
        
        // Act
        _miRobot.Start();

        // Assert
        VerifyMethod("app_start", string.Empty);
    }

    [Fact]
    public async Task StartAsync_Should_Now_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync("app_start", "ok");
        
        // Act
        await _miRobot.StartAsync();

        // Assert
        VerifyMethodAsync("app_start", string.Empty);
    }

    [Fact]
    public void Stop_Should_Now_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("app_stop", "ok");
        
        // Act
        _miRobot.Stop();

        // Assert
        VerifyMethod("app_stop", string.Empty);
    }

    [Fact]
    public async Task StopAsync_Should_Now_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync("app_stop", "ok");
        
        // Act
        await _miRobot.StopAsync();

        // Assert
        VerifyMethodAsync("app_stop", string.Empty);
    }

    [Fact]
    public void Pause_Should_Now_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("app_pause", "ok");
        
        // Act
        _miRobot.Pause();

        // Assert
        VerifyMethod("app_pause", string.Empty);
    }

    [Fact]
    public async Task PauseAsync_Should_Now_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync("app_pause", "ok");
        
        // Act
        await _miRobot.PauseAsync();

        // Assert
        VerifyMethodAsync("app_pause", string.Empty);
    }

    [Fact]
    public void Spot_Should_Now_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("app_spot", "ok");
        
        // Act
        _miRobot.Spot();

        // Assert
        VerifyMethod("app_spot", string.Empty);
    }

    [Fact]
    public async Task SpotAsync_Should_Now_Throw_ExceptionsAsync()
    {
        // Arrange
        SendResultMethodAsync("app_spot", "ok");
        
        // Act
        await _miRobot.SpotAsync();

        // Assert
        VerifyMethodAsync("app_spot", string.Empty);
    }
}
