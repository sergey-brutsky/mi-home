using System.Threading.Tasks;
using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using System.Threading;
using System.Globalization;
using MiHomeLib;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindMe_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "find_me", "ok");

        await Call(isAsync, _miRobot.FindMe, _miRobot.FindMeAsync);

        VerifySent(isAsync, "find_me", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Home_Pauses_Then_Sends_To_Charging(bool isAsync)
    {
        // Arrange
        SetupResult(isAsync, "app_pause", "ok");
        var charge = new { result = new[] { "ok" }, id = 2 }.ToJson();
        var expectedCharge = new { id = 2, method = "app_charge", @params = new[] { string.Empty } }.ToJson();

        if (isAsync)
            _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("app_charge")))).Returns(Task.FromResult(charge));
        else
            _miioTransport.Setup(x => x.SendMessage(It.Is<string>(s => s.Contains("app_charge")))).Returns(charge);

        // Act
        await Call(isAsync, _miRobot.Home, _miRobot.HomeAsync);

        // Assert
        VerifySent(isAsync, "app_pause", string.Empty);

        if (isAsync)
            _miioTransport.Verify(x => x.SendMessageAsync(expectedCharge), Times.Once());
        else
            _miioTransport.Verify(x => x.SendMessage(expectedCharge), Times.Once());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "app_start", "ok");

        await Call(isAsync, _miRobot.Start, _miRobot.StartAsync);

        VerifySent(isAsync, "app_start", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "app_stop", "ok");

        await Call(isAsync, _miRobot.Stop, _miRobot.StopAsync);

        VerifySent(isAsync, "app_stop", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pause_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "app_pause", "ok");

        await Call(isAsync, _miRobot.Pause, _miRobot.PauseAsync);

        VerifySent(isAsync, "app_pause", string.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Spot_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "app_spot", "ok");

        await Call(isAsync, _miRobot.Spot, _miRobot.SpotAsync);

        VerifySent(isAsync, "app_spot", string.Empty);
    }
}
