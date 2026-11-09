using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Xunit;
using static MiHomeLib.MiioDevices.XiaomiAirHumidifierV1;

namespace MiHomeUnitTests.MiioDevicesTests;

public class XiaomiAirHumidifierV1Tests: MiioDeviceBase
{
    private readonly XiaomiAirHumidifierV1 _airHumidifier;
    
    public XiaomiAirHumidifierV1Tests()
    {        
        _airHumidifier = new XiaomiAirHumidifierV1(_miioTransport.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PowerOn_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "set_power", "ok");

        await Call(isAsync, _airHumidifier.PowerOn, _airHumidifier.PowerOnAsync);

        VerifySent(isAsync, "set_power", "on");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PowerOff_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "set_power", "ok");

        await Call(isAsync, _airHumidifier.PowerOff, _airHumidifier.PowerOffAsync);

        VerifySent(isAsync, "set_power", "off");
    }

    [Theory]
    [InlineData(false, Mode.High, "high")]
    [InlineData(true, Mode.Medium, "medium")]
    public async Task SetMode_Sends_Command(bool isAsync, Mode mode, string expected)
    {
        SetupResult(isAsync, "set_mode", "ok");

        await Call(isAsync, () => _airHumidifier.SetMode(mode), () => _airHumidifier.SetModeAsync(mode));

        VerifySent(isAsync, "set_mode", expected);
    }

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "on", true)]
    public async Task IsTurnedOn_Returns_Power_State(bool isAsync, string reported, bool expected)
    {
        SetupResult(isAsync, "get_prop", reported);

        var power = await Call(isAsync, _airHumidifier.IsTurnedOn, _airHumidifier.IsTurnedOnAsync);

        VerifySent(isAsync, "get_prop", "power");
        power.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "high", Mode.High)]
    [InlineData(true, "medium", Mode.Medium)]
    public async Task GetDeviceMode_Returns_Mode(bool isAsync, string reported, Mode expected)
    {
        SetupResult(isAsync, "get_prop", reported);

        var mode = await Call(isAsync, _airHumidifier.GetDeviceMode, _airHumidifier.GetDeviceModeAsync);

        VerifySent(isAsync, "get_prop", "mode");
        mode.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "323", 32.3f)]
    [InlineData(true, "325", 32.5f)]
    public async Task GetTemperature_Returns_Temperature(bool isAsync, string reported, float expected)
    {
        SetupResult(isAsync, "get_prop", reported);

        var temperature = await Call(isAsync, _airHumidifier.GetTemperature, _airHumidifier.GetTemperatureAsync);

        VerifySent(isAsync, "get_prop", "temp_dec");
        temperature.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "45", 45)]
    [InlineData(true, "41", 41)]
    public async Task GetHumidity_Returns_Humidity(bool isAsync, string reported, int expected)
    {
        SetupResult(isAsync, "get_prop", reported);

        var humidity = await Call(isAsync, _airHumidifier.GetHumidity, _airHumidifier.GetHumidityAsync);

        VerifySent(isAsync, "get_prop", "humidity");
        humidity.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "0", Brightness.Bright)]
    [InlineData(true, "2", Brightness.Off)]
    public async Task GetBrightness_Returns_Brightness(bool isAsync, string reported, Brightness expected)
    {
        SetupResult(isAsync, "get_prop", reported);

        var brightness = await Call(isAsync, _airHumidifier.GetBrightness, _airHumidifier.GetBrightnessAsync);

        VerifySent(isAsync, "get_prop", "led_b");
        brightness.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, Brightness.Bright, 0)]
    [InlineData(true, Brightness.Off, 2)]
    public async Task SetBrightness_Sends_Command(bool isAsync, Brightness brightness, int expected)
    {
        SetupResult(isAsync, "set_led_b", "ok");

        await Call(isAsync, () => _airHumidifier.SetBrightness(brightness), () => _airHumidifier.SetBrightnessAsync(brightness));

        VerifySent(isAsync, "set_led_b", expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetTargetHumidity_Returns_TargetHumidity(bool isAsync)
    {
        SetupResult(isAsync, "get_prop", "50");

        var targetHumidity = await Call(isAsync, _airHumidifier.GetTargetHumidity, _airHumidifier.GetTargetHumidityAsync);

        VerifySent(isAsync, "get_prop", "limit_hum");
        targetHumidity.Should().Be(50);
    }

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "off", false)]
    public async Task IsBuzzerOn_Returns_Buzzer_State(bool isAsync, string reported, bool expected)
    {
        SetupResult(isAsync, "get_prop", reported);

        var buzzer = await Call(isAsync, _airHumidifier.IsBuzzerOn, _airHumidifier.IsBuzzerOnAsync);

        VerifySent(isAsync, "get_prop", "buzzer");
        buzzer.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuzzerOn_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "set_buzzer", "ok");

        await Call(isAsync, _airHumidifier.BuzzerOn, _airHumidifier.BuzzerOnAsync);

        VerifySent(isAsync, "set_buzzer", "on");
    }

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "off", false)]
    public async Task IsChildLockOn_Returns_ChildLock_State(bool isAsync, string reported, bool expected)
    {
        SetupResult(isAsync, "get_prop", reported);

        var childLock = await Call(isAsync, _airHumidifier.IsChildLockOn, _airHumidifier.IsChildLockOnAsync);

        VerifySent(isAsync, "get_prop", "child_lock");
        childLock.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChildLockOn_Sends_Command(bool isAsync)
    {
        SetupResult(isAsync, "set_child_lock", "ok");

        await Call(isAsync, _airHumidifier.ChildLockOn, _airHumidifier.ChildLockOnAsync);

        VerifySent(isAsync, "set_child_lock", "on");
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
