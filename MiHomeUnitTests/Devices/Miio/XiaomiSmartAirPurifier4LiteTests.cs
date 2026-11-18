using MiHomeUnitTests.Infrastructure;
using Xunit;
using MiHomeLib.MiioDevices;
using Moq;
using FluentAssertions;
using MiHomeLib;
using System;
using System.Threading.Tasks;

namespace MiHomeUnitTests.Devices.Miio;

public class XiaomiSmartAirPurifier4LiteTests : MiioDeviceTestBase
{
    private readonly XiaomiSmartAirPurifier4Lite _purifier;

    public XiaomiSmartAirPurifier4LiteTests()
    {
        SetupMiioInfo();

        _purifier = new XiaomiSmartAirPurifier4Lite(_miioTransport.Object);
    }

    [Fact]
    public void Description_Contains_Model_And_Initialized_Network()
    {
        _purifier.ToString().Should().Contain(XiaomiSmartAirPurifier4Lite.MARKET_MODEL)
            .And.Contain("192.168.1.100").And.Contain("ssid1").And.Contain("1.0.5_0008");
    }

    [Fact]
    public void GetPowerState_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 1, true));

        // Act
        var turnedOn = _purifier.GetPowerState();

        // Assert
        VerifyGetProperties(2, 1, "2-1");
        turnedOn.Should().BeTrue();
    }

    [Fact]
    public void TurnOn_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.TurnOn();

        // Assert
        VerifySetProperties(2, 1, true);
    }

    [Fact]
    public void TurnOff_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.TurnOff();

        // Assert
        VerifySetProperties(2, 1, false);
    }

    [Fact]
    public void TogglePower_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _purifier.TogglePower();

        // Assert
        VerifyCallAction(2, 1);
    }

    [Fact]
    public void Get_DeviceFaultState_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 2, (byte)0));

        // Act
        var faultState = _purifier.GetDeviceFaultState();

        // Assert
        VerifyGetProperties(2, 2);
        faultState.Should().Be(FaultState.NoFaults);
    }

    [Fact]
    public void Get_Mode_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((2, 4, (byte)0));

        // Act
        var mode = _purifier.GetMode();

        // Assert
        VerifyGetProperties(2, 4);
        mode.Should().Be(OperationMode.Auto);
    }

    [Fact]
    public void Set_Mode_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.SetMode(OperationMode.Sleep);

        // Assert
        VerifySetProperties(2, 4, OperationMode.Sleep);
    }

    [Fact]
    public void Get_Humidity_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 1, (byte)55));

        // Act
        var humidity = _purifier.GetHumidity();

        // Assert
        VerifyGetProperties(3, 1);
        humidity.Should().Be(55);
    }

    [Fact]
    public void Get_Pm25Density_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 4, (ushort)23));

        // Act
        var pm25 = _purifier.GetPm25Density();

        // Assert
        VerifyGetProperties(3, 4);
        pm25.Should().Be(23);
    }

    [Fact]
    public void Get_Temperature_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 7, 22.5f));

        // Act
        var temperature = _purifier.GetTemperature();

        // Assert
        VerifyGetProperties(3, 7);
        temperature.Should().Be(22.5f);
    }

    [Fact]
    public void Get_AirQuality_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((3, 8, (byte)0));

        // Act
        var airQuality = _purifier.GetAirQuality();

        // Assert
        VerifyGetProperties(3, 8);
        airQuality.Should().Be(AirQuality.Excellent);
    }

    [Fact]
    public void Get_FilterLifeLevel_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((4, 1, (byte)85));

        // Act
        var filterLifeLevel = _purifier.GetFilterLifeLevel();

        // Assert
        VerifyGetProperties(4, 1);
        filterLifeLevel.Should().Be(85);
    }

    [Fact]
    public void Get_FilterUsedTime_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((4, 3, (ushort)1200));

        // Act
        var filterUsedTime = _purifier.GetFilterUsedTime();

        // Assert
        VerifyGetProperties(4, 3);
        filterUsedTime.Should().Be(1200);
    }

    [Fact]
    public void Get_FilterLeftTime_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((4, 4, (ushort)180));

        // Act
        var filterLeftTime = _purifier.GetFilterLeftTime();

        // Assert
        VerifyGetProperties(4, 4);
        filterLeftTime.Should().Be(180);
    }

    [Fact]
    public void ResetFilterLife_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _purifier.ResetFilterLife();

        // Assert
        VerifyCallAction(4, 1);
    }

    [Fact]
    public void Get_BuzzerEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((6, 1, true));

        // Act
        var buzzerEnabled = _purifier.GetBuzzerEnabled();

        // Assert
        VerifyGetProperties(6, 1);
        buzzerEnabled.Should().BeTrue();
    }

    [Fact]
    public void Set_BuzzerEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.SetBuzzerEnabled(false);

        // Assert
        VerifySetProperties(6, 1, false);
    }

    [Fact]
    public void Get_ScreenBrightness_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((7, 2, (byte)1));

        // Act
        var screenBrightness = _purifier.GetScreenBrightness();

        // Assert
        VerifyGetProperties(7, 2);
        screenBrightness.Should().Be(ScreenBrightness.Bright);
    }

    [Fact]
    public void Set_ScreenBrightness_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.SetScreenBrightness(ScreenBrightness.Off);

        // Assert
        VerifySetProperties(7, 2, ScreenBrightness.Off);
    }

    [Fact]
    public void Get_ChildLockEnabled_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((8, 1, false));

        // Act
        var childLockEnabled = _purifier.GetChildLockEnabled();

        // Assert
        VerifyGetProperties(8, 1);
        childLockEnabled.Should().BeFalse();
    }

    [Fact]
    public void Set_ChildLockEnabled_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.SetChildLockEnabled(true);

        // Assert
        VerifySetProperties(8, 1, true);
    }

    [Fact]
    public void Get_MotorSpeedRpm_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((9, 1, (ushort)800));

        // Act
        var motorSpeedRpm = _purifier.GetMotorSpeedRpm();

        // Assert
        VerifyGetProperties(9, 1);
        motorSpeedRpm.Should().Be(800);
    }

    [Fact]
    public void ToggleMode_Should_Work_As_Expected()
    {
        // Arrange
        SetupCallAction();

        // Act
        _purifier.ToggleMode();

        // Assert
        VerifyCallAction(9, 1);
    }

    [Fact]
    public void Get_FanLevel_Should_Return_Correct_Value()
    {
        // Arrange
        SetupGetProperties((11, 1, (byte)5));

        // Act
        var fanLevel = _purifier.GetFanLevel();

        // Assert
        VerifyGetProperties(11, 1);
        fanLevel.Should().Be(5);
    }

    [Fact]
    public void Set_FanLevel_Should_Work_As_Expected()
    {
        // Arrange
        SetupSetProperties();

        // Act
        _purifier.SetFanLevel(10);

        // Assert
        VerifySetProperties(11, 1, (byte)10);
    }

    [Fact]
    public void Set_FanLevel_Above_Range_Should_Throw_Exception()
    {
        // Act & Assert
        _purifier
            .Invoking(x => x.SetFanLevel(15))
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Status_Reads_Power_Mode_Environment_And_Motor_In_One_Request(bool isAsync)
    {
        SetupStatus();

        var result = await Call(isAsync, _purifier.GetStatus, _purifier.GetStatusAsync);

        result.Should().Be(new XiaomiSmartAirPurifier4Lite.DeviceStatus(true, FaultState.NoFaults,
            OperationMode.Favorite, 55, 23, 22.5f, AirQuality.Good, 800));
        VerifyGetProperties(2, (2, 1), (2, 2), (2, 4), (3, 1), (3, 4), (3, 7), (3, 8), (9, 1));
    }

    [Theory]
    [InlineData(0, "on")]
    [InlineData(1, "256")]
    [InlineData(3, "-1")]
    [InlineData(4, "65536")]
    [InlineData(5, "invalid")]
    public async Task Status_Reports_Malformed_Fields_With_Context(int index, string value)
    {
        SetupStatus(index, value);
        Func<Task> read = () => _purifier.GetStatusAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Air Purifier 4 Lite status*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public async Task Status_Preserves_Unknown_Firmware_Enum_Values(int index)
    {
        SetupStatus(index, 255);

        var result = await _purifier.GetStatusAsync();

        var value = index switch
        {
            1 => (int)result.DeviceFaultState,
            2 => (int)result.Mode,
            6 => (int)result.AirQuality,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
        value.Should().Be(255);
    }

    [Fact]
    public async Task Status_Reflects_Each_New_Response()
    {
        SetupStatus();
        var first = await _purifier.GetStatusAsync();
        SetupStatus(5, -5.5f);

        var second = await _purifier.GetStatusAsync();

        first.Temperature.Should().Be(22.5f);
        second.Temperature.Should().Be(-5.5f);
        VerifyGetProperties(2, (2, 1), (2, 2), (2, 4), (3, 1), (3, 4), (3, 7), (3, 8), (9, 1));
        VerifyGetProperties(3, (2, 1), (2, 2), (2, 4), (3, 1), (3, 4), (3, 7), (3, 8), (9, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Settings_Read_All_Five_Properties_In_One_Request(bool isAsync)
    {
        SetupGetProperties((2, 4, 1), (6, 1, false), (7, 2, 2), (8, 1, true), (11, 1, 14));

        var result = await Call(isAsync, _purifier.GetSettings, _purifier.GetSettingsAsync);

        result.Should().Be(new XiaomiSmartAirPurifier4Lite.DeviceSettings(OperationMode.Sleep, false,
            ScreenBrightness.Brightest, true, 14));
        VerifyGetProperties(2, (2, 4), (6, 1), (7, 2), (8, 1), (11, 1));
    }

    [Fact]
    public async Task Settings_Reject_Invalid_Switch_With_Context()
    {
        SetupGetProperties((2, 4, 1), (6, 1, "invalid"), (7, 2, 2), (8, 1, true), (11, 1, 14));
        Func<Task> read = () => _purifier.GetSettingsAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Air Purifier 4 Lite settings*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Filter_Info_Maps_Percent_Hours_And_Days(bool isAsync)
    {
        SetupGetProperties((4, 1, 85), (4, 3, 1200), (4, 4, 180));

        var result = await Call(isAsync, _purifier.GetFilterInfo, _purifier.GetFilterInfoAsync);

        result.Should().Be(new XiaomiSmartAirPurifier4Lite.FilterInfo(85, 1200, 180));
        VerifyGetProperties(2, (4, 1), (4, 3), (4, 4));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task Filter_Info_Rejects_Invalid_Used_Time(int hours)
    {
        SetupGetProperties((4, 1, 85), (4, 3, hours), (4, 4, 180));
        Func<Task> read = () => _purifier.GetFilterInfoAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Air Purifier 4 Lite filter info*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Device_Information_Maps_All_Five_Properties(bool isAsync)
    {
        SetupGetProperties((1, 1, "zhimi"), (1, 2, XiaomiSmartAirPurifier4Lite.MODEL),
            (1, 3, "test-device-id"), (1, 4, "1.0.0"), (1, 5, "test-serial"));

        var result = await Call(isAsync, _purifier.GetDeviceInformation, _purifier.GetDeviceInformationAsync);

        result.Should().Be(new XiaomiSmartAirPurifier4Lite.DeviceInformation("zhimi",
            XiaomiSmartAirPurifier4Lite.MODEL, "test-device-id", "1.0.0", "test-serial"));
        VerifyGetProperties(2, (1, 1), (1, 2), (1, 3), (1, 4), (1, 5));
    }

    [Fact]
    public async Task Device_Information_Preserves_Firmware_Placeholders()
    {
        SetupGetProperties((1, 1, "hello"), (1, 2, "hello"), (1, 3, "hello"), (1, 4, "hello"), (1, 5, "hello"));

        var result = await _purifier.GetDeviceInformationAsync();

        result.Should().Be(new XiaomiSmartAirPurifier4Lite.DeviceInformation("hello", "hello", "hello", "hello", "hello"));
    }

    [Theory]
    [InlineData(false, 9, 2)]
    [InlineData(true, 9, 2)]
    [InlineData(false, 10, 1)]
    [InlineData(true, 10, 1)]
    public async Task Service_Property_Reads_Expected_Address(bool isAsync, int service, int property)
    {
        SetupGetProperties((service, property, ushort.MaxValue));

        var result = service == 9
            ? await Call(isAsync, () => _purifier.GetFilterUsedTimeDebug(), _purifier.GetFilterUsedTimeDebugAsync)
            : await Call(isAsync, () => _purifier.GetAqiUpdateHeartbeat(), _purifier.GetAqiUpdateHeartbeatAsync);

        result.Should().Be(ushort.MaxValue);
        VerifyGetProperties(service, property);
    }

    [Theory]
    [InlineData(false, 9, 2, 0)]
    [InlineData(true, 9, 2, 3500)]
    [InlineData(false, 10, 1, 0)]
    [InlineData(true, 10, 1, 65535)]
    public async Task Service_Property_Writes_Expected_Address_And_Accepts_Bounds(bool isAsync, int service, int property, int value)
    {
        SetupSetProperties();

        if (service == 9)
            await Call(isAsync, () => _purifier.SetFilterUsedTimeDebug((ushort)value), () => _purifier.SetFilterUsedTimeDebugAsync((ushort)value));
        else
            await Call(isAsync, () => _purifier.SetAqiUpdateHeartbeat((ushort)value), () => _purifier.SetAqiUpdateHeartbeatAsync((ushort)value));

        VerifySetProperties(service, property, (ushort)value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Debug_Counter_Rejects_Values_Above_Range_Before_Request(bool isAsync)
    {
        Func<Task> write = () => Call(isAsync, () => _purifier.SetFilterUsedTimeDebug(3501),
            () => _purifier.SetFilterUsedTimeDebugAsync(3501));

        var error = await write.Should().ThrowAsync<ArgumentOutOfRangeException>();

        error.Which.ParamName.Should().Be("value");
        error.Which.ActualValue.Should().Be((ushort)3501);
        _miioTransport.Invocations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(9, 2, "-1")]
    [InlineData(10, 1, "65536")]
    public async Task Invalid_Service_Value_Reports_Property_Address(int service, int property, string value)
    {
        SetupGetProperties((service, property, value));
        Func<Task> read = () => service == 9 ? _purifier.GetFilterUsedTimeDebugAsync() : _purifier.GetAqiUpdateHeartbeatAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{service}/{property}*");
    }

    [Fact]
    public async Task Rejected_Service_Write_Propagates_Device_Error()
    {
        SendResultMethod("set_properties", new { code = -1 });
        Func<Task> write = () => _purifier.SetAqiUpdateHeartbeatAsync(100);

        await write.Should().ThrowAsync<InvalidOperationException>().WithMessage("*10/1*-1*");
    }

    [Fact]
    public async Task Snapshot_Rejects_Missing_Property_Instead_Of_Returning_Default()
    {
        SetupGetProperties((4, 1, 85), (4, 3, 1200));
        Func<Task> read = () => _purifier.GetFilterInfoAsync();

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*4/4*");
    }

    private void SetupStatus(int changedIndex = -1, object value = null)
    {
        (int siid, int piid, object value)[] properties = [(2, 1, true), (2, 2, 0), (2, 4, 2), (3, 1, 55),
            (3, 4, 23), (3, 7, 22.5f), (3, 8, 1), (9, 1, 800)];
        if (changedIndex >= 0)
            properties[changedIndex] = (properties[changedIndex].siid, properties[changedIndex].piid, value);
        SetupGetProperties(properties);
    }
}
