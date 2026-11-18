using MiHomeUnitTests.Infrastructure;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Xunit;
using static MiHomeLib.MiioDevices.XiaomiAirHumidifierV1;

namespace MiHomeUnitTests.Devices.Miio;

public class XiaomiAirHumidifierV1Tests: MiioDeviceTestBase
{
    [Theory]
    [InlineData("0", "0.0")]
    [InlineData("5", "0.5")]
    [InlineData("95", "9.5")]
    [InlineData("100", "10.0")]
    [InlineData("-5", "-0.5")]
    [InlineData("-95", "-9.5")]
    [InlineData("1000", "100.0")]
    public void Description_Formats_Temperature_Numerically(string raw, string expected)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
            SendResultMethod("get_prop", "on", "high", raw, "45", "0", "on", "off", "50");
            _airHumidifier.ToString().Should().Contain($"Temperature: {expected} °C");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    private readonly XiaomiAirHumidifierV1 _airHumidifier;

    public XiaomiAirHumidifierV1Tests()
    {
        _airHumidifier = new XiaomiAirHumidifierV1(_miioTransport.Object);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disabled_Buzzer_And_ChildLock_Send_Off(bool isAsync)
    {
        SendResultMethod("set_buzzer", "ok");
        SendResultMethod("set_child_lock", "ok");
        await Call(isAsync, () => _airHumidifier.SetBuzzerEnabled(false), () => _airHumidifier.SetBuzzerEnabledAsync(false));
        await Call(isAsync, () => _airHumidifier.SetChildLockEnabled(false), () => _airHumidifier.SetChildLockEnabledAsync(false));
        VerifyMethod("set_buzzer", "off");
        if (isAsync) _miioTransport.Verify(x => x.SendMessageAsync("{\"id\":2,\"method\":\"set_child_lock\",\"params\":[\"off\"]}"), Moq.Times.Once);
        else VerifyMethodWithId("set_child_lock", 2, "off");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TurnOn_Sends_Command(bool isAsync)
    {
        SendResultMethod("set_power", "ok");

        await Call(isAsync, _airHumidifier.TurnOn, _airHumidifier.TurnOnAsync);

        VerifyMethod("set_power", "on");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TurnOff_Sends_Command(bool isAsync)
    {
        SendResultMethod("set_power", "ok");

        await Call(isAsync, _airHumidifier.TurnOff, _airHumidifier.TurnOffAsync);

        VerifyMethod("set_power", "off");
    }

    [Theory]
    [InlineData(false, Mode.High, "high")]
    [InlineData(true, Mode.Medium, "medium")]
    public async Task SetMode_Sends_Command(bool isAsync, Mode mode, string expected)
    {
        SendResultMethod("set_mode", "ok");

        await Call(isAsync, () => _airHumidifier.SetMode(mode), () => _airHumidifier.SetModeAsync(mode));

        VerifyMethod("set_mode", expected);
    }

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "on", true)]
    public async Task GetPowerState_Returns_Power_State(bool isAsync, string reported, bool expected)
    {
        SendResultMethod("get_prop", reported);

        var power = await Call(isAsync, _airHumidifier.GetPowerState, _airHumidifier.GetPowerStateAsync);

        VerifyMethod("get_prop", "power");
        power.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "high", Mode.High)]
    [InlineData(true, "medium", Mode.Medium)]
    public async Task GetMode_Returns_Mode(bool isAsync, string reported, Mode expected)
    {
        SendResultMethod("get_prop", reported);

        var mode = await Call(isAsync, _airHumidifier.GetMode, _airHumidifier.GetModeAsync);

        VerifyMethod("get_prop", "mode");
        mode.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "323", 32.3f)]
    [InlineData(true, "325", 32.5f)]
    public async Task GetTemperature_Returns_Temperature(bool isAsync, string reported, float expected)
    {
        SendResultMethod("get_prop", reported);

        var temperature = await Call(isAsync, _airHumidifier.GetTemperature, _airHumidifier.GetTemperatureAsync);

        VerifyMethod("get_prop", "temp_dec");
        temperature.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "45", 45)]
    [InlineData(true, "41", 41)]
    public async Task GetHumidity_Returns_Humidity(bool isAsync, string reported, int expected)
    {
        SendResultMethod("get_prop", reported);

        var humidity = await Call(isAsync, _airHumidifier.GetHumidity, _airHumidifier.GetHumidityAsync);

        VerifyMethod("get_prop", "humidity");
        humidity.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, "0", Brightness.Bright)]
    [InlineData(true, "2", Brightness.Off)]
    public async Task GetBrightness_Returns_Brightness(bool isAsync, string reported, Brightness expected)
    {
        SendResultMethod("get_prop", reported);

        var brightness = await Call(isAsync, _airHumidifier.GetBrightness, _airHumidifier.GetBrightnessAsync);

        VerifyMethod("get_prop", "led_b");
        brightness.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, Brightness.Bright, 0)]
    [InlineData(true, Brightness.Off, 2)]
    public async Task SetBrightness_Sends_Command(bool isAsync, Brightness brightness, int expected)
    {
        SendResultMethod("set_led_b", "ok");

        await Call(isAsync, () => _airHumidifier.SetBrightness(brightness), () => _airHumidifier.SetBrightnessAsync(brightness));

        VerifyMethod("set_led_b", expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetTargetHumidity_Returns_TargetHumidity(bool isAsync)
    {
        SendResultMethod("get_prop", "50");

        var targetHumidity = await Call(isAsync, _airHumidifier.GetTargetHumidity, _airHumidifier.GetTargetHumidityAsync);

        VerifyMethod("get_prop", "limit_hum");
        targetHumidity.Should().Be(50);
    }

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "off", false)]
    public async Task GetBuzzerEnabled_Returns_Buzzer_State(bool isAsync, string reported, bool expected)
    {
        SendResultMethod("get_prop", reported);

        var buzzer = await Call(isAsync, _airHumidifier.GetBuzzerEnabled, _airHumidifier.GetBuzzerEnabledAsync);

        VerifyMethod("get_prop", "buzzer");
        buzzer.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enabled_Buzzer_Sends_Command(bool isAsync)
    {
        SendResultMethod("set_buzzer", "ok");

        await Call(isAsync, () => _airHumidifier.SetBuzzerEnabled(true), () => _airHumidifier.SetBuzzerEnabledAsync(true));

        VerifyMethod("set_buzzer", "on");
    }

    [Theory]
    [InlineData(false, "on", true)]
    [InlineData(true, "off", false)]
    public async Task GetChildLockEnabled_Returns_ChildLock_State(bool isAsync, string reported, bool expected)
    {
        SendResultMethod("get_prop", reported);

        var childLock = await Call(isAsync, _airHumidifier.GetChildLockEnabled, _airHumidifier.GetChildLockEnabledAsync);

        VerifyMethod("get_prop", "child_lock");
        childLock.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enabled_ChildLock_Sends_Command(bool isAsync)
    {
        SendResultMethod("set_child_lock", "ok");

        await Call(isAsync, () => _airHumidifier.SetChildLockEnabled(true), () => _airHumidifier.SetChildLockEnabledAsync(true));

        VerifyMethod("set_child_lock", "on");
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
        str.Should().Be("Power: on\nMode: high\nTemperature: 32.3 °C\nHumidity: 45%\nLED brightness: bright\nBuzzer: on\nChild lock: off\nTarget humidity: 50%\nModel: zhimi.humidifier.v1\nIP Address:");
    }
}
