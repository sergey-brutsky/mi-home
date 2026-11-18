using MiHomeLib.Protocols;
using MiHomeLib.MiioDevices;
using MiHomeLib.Helpers;
using FluentAssertions;
using Moq;
using Xunit;
using AutoFixture;
using MiHomeLib.XiaomiGateway2;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using MiHomeLib;
using UnifiedTemperatureHumiditySensor = MiHomeLib.Devices.XiaomiTemperatureHumiditySensor;
using UnifiedAqaraTemperatureHumiditySensor = MiHomeLib.Devices.AqaraTemperatureHumiditySensor;
using UnifiedXiaomiSmartWirelessSwitch = MiHomeLib.Devices.XiaomiSmartWirelessSwitch;
using UnifiedXiaomiBluetoothHygrothermograph2 = MiHomeLib.Devices.XiaomiBluetoothHygrothermograph2;
using UnifiedAqaraWaterLeakSensor = MiHomeLib.Devices.AqaraWaterLeakSensor;

namespace MiHomeUnitTests.Gateways.Gateway2;

public class Gateway2MessageProcessingTests : Gateway2TestBase
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Write_Ack_Snapshot_Does_Not_Overwrite_Device_Reports(bool late, bool omitModel)
    {
        SetupIdList("ack-plug");
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers("ack-plug", ReadAck("ack-plug", "plug", "{\"status\":\"on\"}"), 1);
        await _gateway.DiscoverDevicesAsync();
        var plug = _gateway.GetDeviceById<MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN>("ack-plug");
        var changes = 0;
        plug.OnStateChangeAsync += () => { changes++; return Task.CompletedTask; };
        var ack = System.Text.Json.Nodes.JsonNode.Parse(ReadAck("ack-plug", "plug", "{\"status\":\"on\",\"load_power\":\"999\"}"));
        ack["cmd"] = "write_ack";
        if (omitModel) ack.AsObject().Remove("model");
        void Acknowledge() => _messageTransport.Raise(x => x.OnMessageReceived += null, ack.ToJsonString());
        if (!late) Acknowledge();
        _messageTransport.Raise(x => x.OnMessageReceived += null,
            ReadAck("ack-plug", "plug", "{\"status\":\"off\",\"load_power\":\"0\"}").Replace("read_ack", "report"));
        if (late) Acknowledge();
        _messageTransport.Raise(x => x.OnMessageReceived += null, ReadAck("ack-plug", "plug", "{\"status\":\"unknown\"}"));
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnRgbChangeAsync += _ => { processed.TrySetResult(true); return Task.CompletedTask; };
        RaiseGatewayMessage("report", _gatewaySid, "{\"rgb\":101}");
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        plug.State.Should().Be(MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN.PlugState.Off);
        plug.LoadPower.Should().Be(0);
        changes.Should().Be(1);
        _loggerFactory.Entries.Should().NotContain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning && entry.Message.Contains("write_ack"));
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Debug && entry.Message.Contains("ack-plug") && entry.Message.Contains("acknowledgement"));
    }

    [Fact]
    public async Task Write_Ack_Error_Without_Model_Is_Logged_With_Sid()
    {
        StartDiscovery();
        _messageTransport.Raise(x => x.OnMessageReceived += null,
            """{"cmd":"write_ack","sid":"failed-plug","data":"{\"error\":\"Invalid key\"}"}""");
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnRgbChangeAsync += _ => { processed.TrySetResult(true); return Task.CompletedTask; };
        RaiseGatewayMessage("report", _gatewaySid, "{\"rgb\":102}");
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _loggerFactory.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error &&
            entry.Message.Contains("failed-plug") && entry.Message.Contains("Invalid key"));
        _gateway.GetDevices().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Message_Queue_Overflow_Fails_Discovery_And_Processing_Recovers(bool byteLimit, bool readingDevices)
    {
        using var release = new System.Threading.ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnIlluminationChangeAsync += _ =>
        {
            entered.TrySetResult(true);
            release.Wait();
            return Task.CompletedTask;
        };
        _gateway.ReadAnswerTimeout = TimeSpan.FromSeconds(10);
        var discovery = _gateway.DiscoverDevicesAsync();
        if (readingDevices) RaiseGatewayMessage("get_id_list_ack", _gatewaySid, "[]", "1234567890123456");
        RaiseGatewayMessage("report", _gatewaySid, "{\"illumination\":1}");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            _gateway.MaxPendingMessages = byteLimit ? 256 : 1;
            _gateway.MaxPendingMessageBytes = byteLimit ? 1 : 8 * 1024 * 1024;
            RaiseGatewayMessage("report", _gatewaySid, "{\"rgb\":2}");
            RaiseGatewayMessage("report", _gatewaySid, "{\"rgb\":3}");
            Func<Task> finish = async () => await discovery.WaitAsync(TimeSpan.FromSeconds(2));
            await finish.Should().ThrowAsync<System.IO.IOException>().WithMessage("*buffer*full*");
            _loggerFactory.Entries.Should().Contain(entry => entry.Message.Contains("buffer is full"));
        }
        finally { release.Set(); }
        _gateway.MaxPendingMessages = 256;
        _gateway.MaxPendingMessageBytes = 8 * 1024 * 1024;
        // Drain the old queue before checking delivery of a fresh report.
        if (!byteLimit) await WaitForMessageAsync(() => _gateway.Rgb == 2);
        RaiseGatewayMessage("report", _gatewaySid, "{\"rgb\":4}");
        await WaitForMessageAsync(() => _gateway.Rgb == 4);
        _messageTransport.Setup(transport => transport.SendRead(It.IsAny<string>()))
            .Callback<string>(sid => RaiseGatewayMessage("read_ack", sid, "{}"));
        var retry = _gateway.DiscoverDevicesAsync();
        RaiseGatewayMessage("get_id_list_ack", _gatewaySid, "[]", "1234567890123456");
        await retry.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Slow_Gateway_Event_Handler_Does_Not_Block_The_Next_Report()
    {
        StartDiscovery();
        var secondProcessed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnIlluminationChangeAsync += _ => secondProcessed.Task;
        _gateway.OnRgbChangeAsync += _ =>
        {
            secondProcessed.TrySetResult(true);
            return Task.CompletedTask;
        };

        try
        {
            RaiseGatewayMessage("report", _gatewaySid, "{\"illumination\":100}");
            RaiseGatewayMessage("report", _gatewaySid, "{\"rgb\":42}");
            await secondProcessed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            // Release the waiting handler even when the regression is reproduced.
            secondProcessed.TrySetResult(true);
        }
    }

    [Fact]
    public async Task Consecutive_Reports_Are_Applied_In_Receive_Order()
    {
        SetupIdList("ordered-sensor");
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers("ordered-sensor", ReadAck("ordered-sensor", "sensor_ht", "{\"temperature\":\"2000\"}"), 1);
        await _gateway.DiscoverDevicesAsync();
        var sensor = _gateway.GetDeviceById<UnifiedTemperatureHumiditySensor>("ordered-sensor");
        var received = new List<(float OldValue, float NewValue)>();
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        sensor.OnTemperatureChangeAsync += previous =>
        {
            received.Add((previous, sensor.Temperature));
            if (received.Count == 2) processed.SetResult(true);
            return Task.CompletedTask;
        };

        _messageTransport.Raise(transport => transport.OnMessageReceived += null,
            ReadAck("ordered-sensor", "sensor_ht", "{\"temperature\":\"2100\"}").Replace("read_ack", "report"));
        _messageTransport.Raise(transport => transport.OnMessageReceived += null,
            ReadAck("ordered-sensor", "sensor_ht", "{\"temperature\":\"2200\"}").Replace("read_ack", "report"));

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        received.Should().Equal((20f, 21f), (21f, 22f));
        sensor.Temperature.Should().Be(22f);
    }

    [Fact]
    public async Task Device_Event_Can_Synchronously_Change_Developer_Key()
    {
        SetupIdList("event-plug");
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers("event-plug", ReadAck("event-plug", "plug", "{\"status\":\"off\"}"), 1);
        MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN plug = null;
        _gateway.OnDeviceDiscoveredAsync += device => { plug = (MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN)device; return Task.CompletedTask; };
        await _gateway.DiscoverDevicesAsync();
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("set_lumi_dpf_aes_key"))))
            .Returns(() => Task.Run(async () => { await Task.Delay(20); return "{\"result\":[\"ok\"]}"; }));
        var keyChanged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        plug.OnStateChangeAsync += () =>
        {
            _gateway.SetDeveloperKey("0123456789abcdef");
            keyChanged.SetResult(true);
            return Task.CompletedTask;
        };
        await Task.Run(() => _messageTransport.Raise(x => x.OnMessageReceived += null,
            ReadAck("event-plug", "plug", "{\"status\":\"on\"}"))).WaitAsync(TimeSpan.FromSeconds(2));
        await keyChanged.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await plug.PowerOnAsync();
        _messageTransport.Verify(x => x.SendWrite("event-plug", "plug", "0123456789abcdef", It.IsAny<(string, string)[]>()), Times.Once);
    }

    [Fact]
    public async Task Gateway_Report_Raises_Events_With_Old_Values()
    {
        StartDiscovery(); // subscribes to messages
        var initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnRgbChangeAsync += _ => { initialized.TrySetResult(true); return Task.CompletedTask; };
        RaiseGatewayMessage("report", _gatewaySid, "{\"illumination\":400,\"rgb\":10}");
        await initialized.Task.WaitAsync(TimeSpan.FromSeconds(2));
        int? oldIllumination = null, oldRgb = null;
        _gateway.OnIlluminationChangeAsync += x => { oldIllumination = x; return Task.CompletedTask; };
        _gateway.OnRgbChangeAsync += x => { oldRgb = x; return Task.CompletedTask; };

        RaiseGatewayMessage("report", _gatewaySid, "{\"illumination\":495,\"rgb\":0}");

        await WaitForMessageAsync(() => oldRgb.HasValue);
        _gateway.Illumination.Should().Be(495);
        _gateway.Rgb.Should().Be(0);
        oldIllumination.Should().Be(400);
        oldRgb.Should().Be(10);
    }

    [Fact]
    public async Task Gateway_Heartbeat_Updates_Ip_And_Token()
    {
        StartDiscovery();

        RaiseGatewayMessage("heartbeat", _gatewaySid, "{\"ip\":\"192.168.1.1\"}", token: "newtoken");

        await WaitForMessageAsync(() => _gateway.Ip == "192.168.1.1");
        _gateway.Ip.Should().Be("192.168.1.1");
        _messageTransport.Object.Token.Should().Be("newtoken");
    }

    [Fact]
    public async Task Messages_Of_Another_Gateway_Are_Ignored()
    {
        StartDiscovery();

        int? previous = null;
        _gateway.OnIlluminationChangeAsync += value => { previous = value; return Task.CompletedTask; };
        RaiseGatewayMessage("report", "another-gateway", "{\"illumination\":777}");
        RaiseGatewayMessage("report", _gatewaySid, "{\"illumination\":0}");
        await WaitForMessageAsync(() => previous.HasValue);
        previous.Should().Be(0);
        _gateway.Illumination.Should().Be(0);
    }

    [Fact]
    public async Task Failing_Gateway_Event_Handler_Is_Logged()
    {
        StartDiscovery();
        _gateway.OnIlluminationChangeAsync += _ => throw new InvalidOperationException("handler failed");
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.OnIlluminationChangeAsync += _ => { processed.SetResult(true); return Task.CompletedTask; };

        RaiseGatewayMessage("report", _gatewaySid, "{\"illumination\":10}");

        await processed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _gateway.Illumination.Should().Be(10);
        _loggerFactory.TakeErrors().Should().ContainSingle().Which.Message.Should().Be("handler failed");
    }

    [Fact]
    public void SetDeveloperKey_Should_SendMessage_Successfully()
    {
        // Arrange
        var key = _fixture.Create<string>()[..16]; // exactly 16 characters

        SendResultMethod("set_lumi_dpf_aes_key", "ok");

        // Act
        _gateway.SetDeveloperKey(key);

        // Assert
        VerifyMethod("set_lumi_dpf_aes_key", key);
    }

    [Fact]
    public async Task EnableLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 1677786880;
        var brightness = 100;

        SendResultMethod("set_rgb", "ok");

        // Act
        await _gateway.EnableLightAsync(0, 255, 0, brightness);

        // Assert
        VerifyMethod("set_rgb", rgb, brightness);
    }

    [Fact]
    public void DisableLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 0;
        var brightness = 0;

        SendResultMethod("set_rgb", "ok");

        // Act
        _ = _gateway.DisableLightAsync();

        // Assert
        VerifyMethod("set_rgb", rgb, brightness);
    }

    [Fact]
    public void EnableNightLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 1677786880;
        var brightness = 100;

        SendResultMethod("set_night_light_rgb", "ok");

        // Act
        _ = _gateway.EnableNightLightAsync(0, 255, 0, brightness);

        // Assert
        VerifyMethod("set_night_light_rgb", rgb, brightness);
    }

    [Fact]
    public void DisableNightLight_Should_SendMessage_Successfully()
    {
        // Arrange
        var rgb = 0;
        var brightness = 0;

        SendResultMethod("set_night_light_rgb", "ok");

        // Act
        _ = _gateway.DisableNightLightAsync();

        // Assert
        VerifyMethod("set_night_light_rgb", rgb, brightness);
    }
}
