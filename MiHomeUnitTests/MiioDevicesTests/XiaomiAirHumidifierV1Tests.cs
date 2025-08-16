using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.MiioDevicesTests;

public class XiaomiAirHumidifierV1Tests: MiioDeviceBase
{
    private readonly XiaomiAirHumidifierV1 _airHumidifier;
    
    public XiaomiAirHumidifierV1Tests()
    {        
        _airHumidifier = new XiaomiAirHumidifierV1(_miioTransport.Object);
    }

    [Fact]
    public void PowerOn_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("set_power", "ok");

        // Act
        _airHumidifier.PowerOn();

        // Assert
        VerifyMethod("set_power", "on");
    }

    [Fact]
    public async Task PowerOnAsync_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethodAsync("set_power", "ok");
        
        // Act
        await _airHumidifier.PowerOnAsync();

        // Assert
        VerifyMethodAsync("set_power", "on");
    }

    [Fact]
    public void PowerOff_Should_Not_Throw_Exceptions()
    {
        // Arrange
        _miioTransport
            .Setup(x => x.SendMessage(It.IsAny<string>()))
            .Returns(ResultOkJson());
        
        // Act
        _airHumidifier.PowerOff();

        // Assert
        VerifyMethod("set_power", "off");
    }

    [Fact]
    public async Task PowerOffAsync_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethodAsync("set_power", "ok");

        // Act
        await _airHumidifier.PowerOffAsync();

        // Assert
        VerifyMethodAsync("set_power", "off");
    }

    [Fact]
    public void SetMode_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("set_mode", "ok");

        // Act
        _airHumidifier.SetMode(XiaomiAirHumidifierV1.Mode.High);

        // Assert
        VerifyMethod("set_mode", "high");
    }

    [Fact]
    public async Task SetModeAsync_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethodAsync("set_mode", "ok");

        // Act
        await _airHumidifier.SetModeAsync(XiaomiAirHumidifierV1.Mode.Medium);

        // Assert
        VerifyMethodAsync("set_mode", "medium");
    }

    [Fact]
    public void IsTurnedOn_Returns_State_Power()
    {
        // Arrange
        SendResultMethod("get_prop", "on");

        // Act
        var power = _airHumidifier.IsTurnedOn();

        // Assert
        VerifyMethod("get_prop", "power");        
        power.Should().BeTrue();
    }

    [Fact]
    public async Task IsTurnedOnAsync_Returns_State_Power()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "on");

        // Act
        var power = await _airHumidifier.IsTurnedOnAsync();

        // Assert
        VerifyMethodAsync("get_prop", "power");        
        power.Should().BeTrue();
    }

    [Fact]
    public void GetDeviceMode_Returns_Valid_Mode()
    {
        // Arrange
        SendResultMethod("get_prop", "high");

        // Act
        var mode = _airHumidifier.GetDeviceMode();

        // Assert
        VerifyMethod("get_prop", "mode");
        mode.Should().Be(XiaomiAirHumidifierV1.Mode.High);
    }

    [Fact]
    public async Task GetDeviceModeAsync_Returns_Valid_Mode()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "medium");
 
        // Act
        var mode = await _airHumidifier.GetDeviceModeAsync();

        // Assert
        VerifyMethodAsync("get_prop", "mode");
        mode.Should().Be(XiaomiAirHumidifierV1.Mode.Medium);
    }

    [Fact]
    public void GetTemperature_Returns_Valid_Temperature()
    {
        // Arrange
        SendResultMethod("get_prop", "323");

        // Act
        var temperature = _airHumidifier.GetTemperature();

        // Assert
        VerifyMethod("get_prop", "temp_dec");

        temperature.Should().Be(32.3f);
    }

    [Fact]
    public async Task GetTemperatureAsync_Returns_Valid_Temperature()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "325");
        
        // Act
        var temperature = await _airHumidifier.GetTemperatureAsync();

        // Assert
        VerifyMethodAsync("get_prop", "temp_dec");

        temperature.Should().Be(32.5f);
    }

    [Fact]
    public void GetHumidity_Returns_Valid_Humidity()
    {
        // Arrange
        SendResultMethod("get_prop", "45");

        // Act
        var humidity = _airHumidifier.GetHumidity();

        // Assert
        VerifyMethod("get_prop", "humidity");        
        humidity.Should().Be(45);
    }

    [Fact]
    public async Task GetHumidityAsync_Returns_Valid_Humidity()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "41");
        
        // Act
        var humidity = await _airHumidifier.GetHumidityAsync();

        // Assert
        VerifyMethodAsync("get_prop", "humidity");
        humidity.Should().Be(41);
    }

    [Fact]
    public void GetBrightness_Returns_Valid_Brightness()
    {
        // Arrange
        SendResultMethod("get_prop", "0");

        // Act
        var brightness = _airHumidifier.GetBrightness();

        // Assert
        VerifyMethod("get_prop", "led_b");
        brightness.Should().Be(XiaomiAirHumidifierV1.Brightness.Bright);
    }

    [Fact]
    public async Task GetBrightnessAsync_Returns_Valid_Brightness()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "2");

        // Act
        var brightness = await _airHumidifier.GetBrightnessAsync();

        // Assert
        VerifyMethodAsync("get_prop", "led_b");
        brightness.Should().Be(XiaomiAirHumidifierV1.Brightness.Off);
    }

    [Fact]
    public void SetBrightness_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("set_led_b", "ok");
        
        // Act
        _airHumidifier.SetBrightness(XiaomiAirHumidifierV1.Brightness.Bright);

        // Assert
        VerifyMethod("set_led_b", 0);
    }

    [Fact]
    public async Task SetBrightnessAsync_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethodAsync("set_led_b", "ok");

        // Act
        await _airHumidifier.SetBrightnessAsync(XiaomiAirHumidifierV1.Brightness.Off);

        // Assert
        VerifyMethodAsync("set_led_b", 2);
    }

    [Fact]
    public void GetTargetHumidity_Returns_Valid_TargetHumidity()
    {
        // Arrange
        SendResultMethod("get_prop", "50");

        // Act
        var targetHumidity = _airHumidifier.GetTargetHumidity();

        // Assert
        VerifyMethod("get_prop", "limit_hum");
        targetHumidity.Should().Be(50);
    }

    [Fact]
    public async Task GetTargetHumidityAsync_Returns_Valid_TargetHumidity()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "50");

        // Act
        var targetHumidity = await _airHumidifier.GetTargetHumidityAsync();

        // Assert
        VerifyMethodAsync("get_prop", "limit_hum");
        targetHumidity.Should().Be(50);
    }

    [Fact]
    public void IsBuzzerOn_Returns_Valid_BuzzerState()
    {
        // Arrange
        SendResultMethod("get_prop", "on");

        // Act
        var buzzer = _airHumidifier.IsBuzzerOn();

        // Assert
        VerifyMethod("get_prop", "buzzer");
        buzzer.Should().BeTrue();
    }

    [Fact]
    public async Task IsBuzzerOnAsync_Returns_Valid_BuzzerState()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "off");

        // Act
        var buzzer = await _airHumidifier.IsBuzzerOnAsync();

        // Assert
        VerifyMethodAsync("get_prop", "buzzer");
        buzzer.Should().BeFalse();
    }

    [Fact]
    public void BuzzerOn_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("set_buzzer", "ok");

        // Act
        _airHumidifier.BuzzerOn();

        // Assert
        VerifyMethod("set_buzzer", "on");
    }

    [Fact]
    public async Task BuzzerOnAsync_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethodAsync("set_buzzer", "ok");

        // Act
        await _airHumidifier.BuzzerOnAsync();

        // Assert
        VerifyMethodAsync("set_buzzer", "on");
    }

    [Fact]
    public void IsChildLockOn_Returns_Valid_ChildLockState()
    {
        // Arrange
        SendResultMethod("get_prop", "on");

        // Act
        var childLock = _airHumidifier.IsChildLockOn();

        // Assert
        VerifyMethod("get_prop", "child_lock");
        childLock.Should().BeTrue();
    }

    [Fact]
    public async Task IsChildLockOnAsync_Returns_Valid_ChildLockState()
    {
        // Arrange
        SendResultMethodAsync("get_prop", "off");
        
        // Act
        var childLock = await _airHumidifier.IsChildLockOnAsync();

        // Assert
        VerifyMethodAsync("get_prop", "child_lock");
        childLock.Should().BeFalse();
    }

    [Fact]
    public void ChildLockOn_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethod("set_child_lock", "ok");

        // Act
        _airHumidifier.ChildLockOn();

        // Assert
        VerifyMethod("set_child_lock", "on");
    }

    [Fact]
    public async Task ChildLockOnAsync_Should_Not_Throw_Exceptions()
    {
        // Arrange
        SendResultMethodAsync("set_child_lock", "ok");

        // Act
        await _airHumidifier.ChildLockOnAsync();

        // Assert
        VerifyMethodAsync("set_child_lock", "on");
    }

    [Fact]
    public void ToString_Returns_Valid_State()
    {
        // Arrange
        SendResultMethod("get_prop", "on", "high", "323", "45", "0", "on", "off", "50");

        // Act
        var str = _airHumidifier.ToString();

        // Assert
        VerifyMethod("get_prop", "power", "mode", "temp_dec", "humidity", "led_b", "buzzer", "child_lock", "limit_hum");
        str.Should().Be("Power: on\nMode: high\nTemperature: 32.3 °C\nHumidity: 45%\nLED brightness: bright\nBuzzer: on\nChild lock: off\nTarget humidity: 50%\nModel: zhimi.humidifier.v1\nIP Address:\nToken: ");
    }
}