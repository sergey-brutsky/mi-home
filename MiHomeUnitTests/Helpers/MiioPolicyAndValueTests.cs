using MiHomeLib.MiioDevices.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices;
using MiHomeUnitTests.Infrastructure;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class MiioPolicyAndValueTests : MiioDeviceTestBase
{
    public MiioPolicyAndValueTests() => SetupMiioInfo();

    public static IEnumerable<object[]> EnumCases =>
        from name in new[] { "humidifier-mode", "humidifier-brightness", "plug-power", "plug-cycle", "purifier-mode", "purifier-screen", "vacuum-mode", "vacuum-sweep", "vacuum-start" }
        from value in new[] { -1, 999 }
        from isAsync in new[] { false, true }
        select new object[] { name, value, isAsync };

    [Theory]
    [MemberData(nameof(EnumCases))]
    public async Task Unknown_Enum_Argument_Is_Rejected_Before_Sending(string name, int invalid, bool isAsync)
    {
        var item = CreateEnumCase(name, invalid);
        using var device = item.Device;
        _miioTransport.Invocations.Clear();

        Func<Task> invoke = () => Call(isAsync, item.Sync, item.Async);
        var error = await invoke.Should().ThrowExactlyAsync<ArgumentOutOfRangeException>();

        error.Which.ParamName.Should().Be(item.Parameter);
        _miioTransport.Verify(t => t.SendMessageAsync(It.IsAny<string>()), Times.Never);
    }

    private (MiioDevice Device, Action Sync, Func<Task> Async, string Parameter) CreateEnumCase(string name, int value)
    {
        switch (name)
        {
            case "humidifier-mode":
            {
                var d = new XiaomiAirHumidifierV1(_miioTransport.Object);
                var v = (XiaomiAirHumidifierV1.Mode)value;
                return (d, () => d.SetMode(v), () => d.SetModeAsync(v), "mode");
            }
            case "humidifier-brightness":
            {
                var d = new XiaomiAirHumidifierV1(_miioTransport.Object);
                var v = (XiaomiAirHumidifierV1.Brightness)value;
                return (d, () => d.SetBrightness(v), () => d.SetBrightnessAsync(v), "brightness");
            }
            case "plug-power":
            {
                var d = new XiaomiSmartPlug2Euro(_miioTransport.Object);
                var v = (XiaomiSmartPlug2Euro.PowerOnState)value;
                return (d, () => d.SetDefaultPowerOnState(v), () => d.SetDefaultPowerOnStateAsync(v), "value");
            }
            case "plug-cycle":
            {
                var d = new XiaomiSmartPlug2Euro(_miioTransport.Object);
                var v = ((ushort)20, (ushort)30, (XiaomiSmartPlug2Euro.CycleState)value, (byte)1);
                return (d, () => d.SetCycleData(v), () => d.SetCycleDataAsync(v), "value");
            }
            case "purifier-mode":
            {
                var d = new XiaomiSmartAirPurifier4Lite(_miioTransport.Object);
                var v = (OperationMode)value;
                return (d, () => d.SetMode(v), () => d.SetModeAsync(v), "value");
            }
            case "purifier-screen":
            {
                var d = new XiaomiSmartAirPurifier4Lite(_miioTransport.Object);
                var v = (ScreenBrightness)value;
                return (d, () => d.SetScreenBrightness(v), () => d.SetScreenBrightnessAsync(v), "value");
            }
            default:
            {
                var d = new XiaomiRobotVacuumMop3C(_miioTransport.Object);
                if (name == "vacuum-mode")
                    return (d, () => d.SetCleaningMode((XiaomiRobotVacuumMop3C.CleaningMode)value),
                        () => d.SetCleaningModeAsync((XiaomiRobotVacuumMop3C.CleaningMode)value), "cleaningMode");
                if (name == "vacuum-sweep")
                    return (d, () => d.SetSweepType((XiaomiRobotVacuumMop3C.SweepType)value),
                        () => d.SetSweepTypeAsync((XiaomiRobotVacuumMop3C.SweepType)value), "sweepType");
                return (d, () => d.StartCleaning((XiaomiRobotVacuumMop3C.CleaningType)value),
                    () => d.StartCleaningAsync((XiaomiRobotVacuumMop3C.CleaningType)value), "ct");
            }
        }
    }

    [Theory]
    [InlineData("socket-toggle", false)]
    [InlineData("vacuum-find", false)]
    [InlineData("plug-toggle", false)]
    [InlineData("purifier-toggle", false)]
    [InlineData("socket-on", true)]
    [InlineData("humidifier-on", true)]
    public async Task Device_Chooses_Retry_Policy_For_The_Operation(string operation, bool repeatable)
    {
        var requests = _miioTransport.As<IMiioRequests>();
        var sent = new List<(string Method, MiioRetryPolicy Policy)>();
        requests.Setup(t => t.RequestAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<MiioRetryPolicy>()))
            .Returns<string, object, MiioRetryPolicy>((method, parameters, policy) =>
            {
                sent.Add((method, policy));
                if (method == "miIO.info") return _miioTransport.Object.SendMessageAsync(new { id = 1, method, @params = parameters }.ToJson());
                return Task.FromResult(method == "action" ? "{\"result\":{\"code\":0}}" :
                    method == "set_properties" ? "{\"result\":[{\"code\":0}]}" : "{\"result\":[\"ok\"]}");
            });
        MiioDevice device;
        Func<Task> call;
        switch (operation)
        {
            case "socket-toggle":
                var socket = new MijiaSmartSocket2China(_miioTransport.Object); device = socket; call = socket.TogglePowerAsync; break;
            case "socket-on":
                var on = new MijiaSmartSocket2China(_miioTransport.Object); device = on; call = on.TurnOnAsync; break;
            case "vacuum-find":
                var vacuum = new XiaomiRobotVacuumMop3C(_miioTransport.Object); device = vacuum; call = vacuum.FindMeAsync; break;
            case "plug-toggle":
                var plug = new XiaomiSmartPlug2Euro(_miioTransport.Object); device = plug; call = plug.TogglePowerAsync; break;
            case "purifier-toggle":
                var purifier = new XiaomiSmartAirPurifier4Lite(_miioTransport.Object); device = purifier; call = purifier.TogglePowerAsync; break;
            default:
                var humidifier = new XiaomiAirHumidifierV1(_miioTransport.Object); device = humidifier; call = humidifier.TurnOnAsync; break;
        }
        using (device)
        {
            sent.Clear();
            await call();
            sent.Should().ContainSingle().Which.Policy.Should().Be(repeatable ? MiioRetryPolicy.SafeToRepeat : MiioRetryPolicy.DoNotRepeat);
        }
    }

    [Theory]
    [InlineData("bool", "bad", "2/1")]
    [InlineData("byte", "256", "2/6")]
    [InlineData("ushort", "-1", "5/2")]
    [InlineData("uint", "4294967296", "5/6")]
    [InlineData("float", "bad", "3/7")]
    [InlineData("enum", "40000", "2/4")]
    public async Task Invalid_Scalar_Reports_The_MiSpec_Address_In_Both_Apis(string kind, string value, string address)
    {
        MiotGenericDevice device;
        Action sync;
        Func<Task> asyncRead;
        if (kind == "float")
        {
            var d = new XiaomiSmartAirPurifier4Lite(_miioTransport.Object); device = d;
            sync = () => _ = d.GetTemperature(); asyncRead = () => d.GetTemperatureAsync();
        }
        else if (kind == "enum")
        {
            var d = new XiaomiRobotVacuumMop3C(_miioTransport.Object); device = d;
            sync = () => d.GetCleaningMode(); asyncRead = () => d.GetCleaningModeAsync();
        }
        else
        {
            var d = new MijiaSmartSocket2China(_miioTransport.Object); device = d;
            (Action Sync, Func<Task> Async) readers = kind switch
            {
                "bool" => (() => _ = d.GetPowerState(), () => d.GetPowerStateAsync()),
                "byte" => (() => _ = d.GetTemperature(), () => d.GetTemperatureAsync()),
                "ushort" => (() => _ = d.GetElectricCurrent(), () => d.GetElectricCurrentAsync()),
                _ => (() => _ = d.GetElectricPower(), () => d.GetElectricPowerAsync())
            };
            (sync, asyncRead) = readers;
        }
        using (device)
        {
            var parts = address.Split('/');
            SetupGetProperties((int.Parse(parts[0]), int.Parse(parts[1]), value));
            sync.Should().ThrowExactly<InvalidOperationException>().WithMessage($"*{address}*");
            var error = await asyncRead.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage($"*{address}*");
            error.Which.InnerException.Should().Match<Exception>(e => e is FormatException || e is OverflowException);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_Numeric_Enum_From_Firmware_Remains_Readable(bool isAsync)
    {
        using var plug = new XiaomiSmartPlug2Euro(_miioTransport.Object);
        SetupGetProperties((2, 2, 99));
        var state = await Call(isAsync, () => plug.GetDefaultPowerOnState(), plug.GetDefaultPowerOnStateAsync);
        ((int)state).Should().Be(99);
    }

    [Theory]
    [InlineData("power")]
    [InlineData("buzzer")]
    [InlineData("child_lock")]
    [InlineData("mode")]
    [InlineData("temp_dec")]
    [InlineData("humidity")]
    [InlineData("led_b")]
    [InlineData("limit_hum")]
    public async Task Invalid_Legacy_Value_Reports_The_Requested_Property(string property)
    {
        using var device = new XiaomiAirHumidifierV1(_miioTransport.Object);
        SendResultMethod("get_prop", "bad");
        Func<Task> read = property switch
        {
            "power" => () => device.GetPowerStateAsync(),
            "buzzer" => () => device.GetBuzzerEnabledAsync(),
            "child_lock" => () => device.GetChildLockEnabledAsync(),
            "mode" => () => device.GetModeAsync(),
            "temp_dec" => () => device.GetTemperatureAsync(),
            "humidity" => () => device.GetHumidityAsync(),
            "led_b" => () => device.GetBrightnessAsync(),
            _ => () => device.GetTargetHumidityAsync()
        };
        var error = await read.Should().ThrowExactlyAsync<InvalidOperationException>().WithMessage($"*{property}*");
        error.Which.InnerException.Should().BeOfType<FormatException>();
    }

    [Fact]
    public async Task Scalar_Numbers_Are_Parsed_Independently_Of_Current_Culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.GetCultureInfo("ru-RU").Clone();
            culture.NumberFormat.NegativeSign = "~";
            System.Globalization.CultureInfo.CurrentCulture = culture;
            using var purifier = new XiaomiSmartAirPurifier4Lite(_miioTransport.Object);
            SetupGetProperties((3, 7, "-12.5"));
            (await purifier.GetTemperatureAsync()).Should().Be(-12.5f);
            using var vacuum = new XiaomiRobotVacuumMop3C(_miioTransport.Object);
            SetupGetProperties((2, 4, "-1"));
            ((int)await vacuum.GetCleaningModeAsync()).Should().Be(-1);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }
}
