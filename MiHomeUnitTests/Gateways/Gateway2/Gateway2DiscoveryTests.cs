using MiHomeUnitTests.Devices.Gateway2;
using MiHomeLib.Protocols;
using MiHomeLib.MiioDevices;
using MiHomeLib.Helpers;
using FluentAssertions;
using Moq;
using Xunit;
using AutoFixture;
using MiHomeLib.XiaomiGateway2;
using System.Threading;
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

public class Gateway2DiscoveryTests : Gateway2TestBase
{
    [Fact]
    public async Task Invalid_First_Report_Does_Not_Prevent_Later_Discovery()
    {
        SetupIdList();
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        await _gateway.DiscoverDevicesAsync();
        var found = 0;
        _gateway.OnDeviceDiscoveredAsync += _ => { found++; return Task.CompletedTask; };
        _messageTransport.Raise(x => x.OnMessageReceived += null, ReadAck("retry-device", "sensor_ht", "{"));
        _messageTransport.Raise(x => x.OnMessageReceived += null, ReadAck("retry-device", "sensor_ht", "{\"temperature\":\"2246\"}"));
        await WaitForMessageAsync(() => found == 1);
        _loggerFactory.TakeErrors().Should().ContainSingle();
        found.Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_Discovery_Acknowledgement_Is_Ignored()
    {
        var ack = new Gw2Response { Cmd = "get_id_list_ack", Sid = _gatewaySid, Token = "token", Data = "[]" }.ToString();
        _messageTransport.Setup(x => x.SendDiscover()).Callback(() =>
        {
            _messageTransport.Raise(x => x.OnMessageReceived += null, ack);
            _messageTransport.Raise(x => x.OnMessageReceived += null, ack);
        });
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        await _gateway.DiscoverDevicesAsync();
        VerifyReads(_gatewaySid, 1);
    }

    [Fact]
    public async Task Parallel_Discovery_And_Duplicate_Acks_Do_Not_Duplicate_Reads()
    {
        SetupIdList();
        _messageTransport.Setup(x => x.SendRead(_gatewaySid)).Callback(() =>
            _messageTransport.Raise(x => x.OnMessageReceived += null, ReadAck(_gatewaySid, "gateway", "{}")));
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _gateway.DiscoverDevicesAsync()));
        VerifyReads(_gatewaySid, 4);
    }

    [Fact]
    public async Task Dispose_Cancels_Pending_Discovery()
    {
        var discovery = _gateway.DiscoverDevicesAsync();
        _gateway.Dispose();
        Func<Task> wait = async () => await discovery.WaitAsync(TimeSpan.FromSeconds(1));
        await wait.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Slow_Discovery_Subscriber_Allows_New_Reports_And_Disposal()
    {
        SetupIdList("slow-sensor");
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{}"), 1);
        SetupReadAnswers("slow-sensor", ReadAck("slow-sensor", "sensor_ht", "{\"temperature\":\"2100\"}"), 1);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        UnifiedTemperatureHumiditySensor sensor = null;
        _gateway.OnDeviceDiscoveredAsync += async device =>
        {
            calls++;
            sensor = (UnifiedTemperatureHumiditySensor)device;
            entered.SetResult(true);
            await release.Task;
        };
        var discovery = _gateway.DiscoverDevicesAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            _messageTransport.Raise(x => x.OnMessageReceived += null,
                ReadAck("slow-sensor", "sensor_ht", "{\"temperature\":\"2250\"}").Replace("read_ack", "report"));
            await WaitForMessageAsync(() => sensor.Temperature == 22.5f);
            sensor.Temperature.Should().Be(22.5f);
            calls.Should().Be(1);
            _gateway.Dispose();
            Func<Task> wait = async () => await discovery.WaitAsync(TimeSpan.FromSeconds(1));
            await wait.Should().ThrowAsync<OperationCanceledException>();
        }
        finally { release.TrySetResult(true); }
    }

    [Fact]
    public void Discovery_Fills_Gateway_Properties_And_Does_Not_Report_Gateway_As_Device()
    {
        var rgb = (int)_fixture.Create<byte>();
        var illumination = (int)_fixture.Create<byte>();

        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack", Sid = _gatewaySid, Token = _fixture.Create<string>(), Data = "[]"
            }.ToString());
        _messageTransport
            .Setup(x => x.SendRead(It.IsAny<string>()))
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "read_ack", Sid = _gatewaySid, Model = "gateway",
                Data = $"{{\"rgb\":{rgb},\"illumination\":{illumination},\"proto_version\":\"1.1.1\"}}"
            }.ToString());
        var reported = 0;
        _gateway.OnDeviceDiscoveredAsync += _ => { reported++; return Task.CompletedTask; };

        _gateway.DiscoverDevices();

        reported.Should().Be(0, "the gateway is not its own sub-device");
        _gateway.Sid.Should().Be(_gatewaySid);
        _gateway.Rgb.Should().Be(rgb);
        _gateway.Illumination.Should().Be(illumination);
        _gateway.ProtoVersion.Should().Be("1.1.1");
    }

    [Fact]
    public void DiscoverDevices_Called_Twice_Handles_Each_Message_Once()
    {
        var cmd = new Gw2Response()
        {
            Cmd = "get_id_list_ack",
            Sid = _gatewaySid,
            Token = _fixture.Create<string>(),
            Data = "[]"
        };

        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, cmd.ToString());

        _gateway.ReadAttempts = 1;
        _gateway.DiscoverDevices();
        _gateway.DiscoverDevices();

        _messageTransport.Verify(x => x.SendRead(_gatewaySid), Times.Exactly(2));
    }

    [Fact]
    public void OnDeviceDiscovered_DiscoversChildrenSubDevices()
    {
        var waterLeakSid = _fixture.Create<string>();
        var thSensorSid =  _fixture.Create<string>();

        var cmd1 = new Gw2Response()
        {
            Cmd = "get_id_list_ack",
            Sid = _gatewaySid,
            Token = _fixture.Create<string>(),
            Data = $"[\"{waterLeakSid}\",\"{thSensorSid}\"]"
        };

        var cmd2 = new Gw2Response()
        {
            Cmd = "read_ack",
            Sid = waterLeakSid,
            Model = "sensor_wleak.aq1",
            Data = $"{{\"voltage\":3015}}"
        };

        var cmd3 = new Gw2Response()
        {
            Cmd = "read_ack",
            Sid = thSensorSid,
            Model = "sensor_ht",
            Data = $"{{\"voltage\":3025,\"temperature\":\"2363\",\"humidity\":\"2902\"}}"
        };

        var waterLeakSensorDiscovered = false;
        var thSensorDiscovered = false;
        var thTemperature = 0f;
        var thHumidity = 0f;

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            if(device is UnifiedAqaraWaterLeakSensor) waterLeakSensorDiscovered = true;
            if(device is UnifiedTemperatureHumiditySensor sensor)
            {
                thSensorDiscovered = true;
                thTemperature = sensor.Temperature;
                thHumidity = sensor.Humidity;
            }
            return Task.CompletedTask;
        };

        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, cmd1.ToString());

        _messageTransport
            .Setup(x => x.SendRead(waterLeakSid))
            .Raises(x => x.OnMessageReceived += null, cmd2.ToString());

        _messageTransport
            .Setup(x => x.SendRead(thSensorSid))
            .Raises(x => x.OnMessageReceived += null, cmd3.ToString());

        _gateway.DiscoverDevices();

        waterLeakSensorDiscovered.Should().BeTrue();
        thSensorDiscovered.Should().BeTrue();
        thTemperature.Should().Be(23.63f);
        thHumidity.Should().Be(29.02f);
    }

    [Fact]
    public void OnDeviceDiscovered_DiscoversUnifiedAqaraTemperatureHumiditySensor()
    {
        var sensorSid = _fixture.Create<string>();
        var discoveredSensor = (UnifiedAqaraTemperatureHumiditySensor)null;

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            discoveredSensor = device as UnifiedAqaraTemperatureHumiditySensor;
            return Task.CompletedTask;
        };

        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack",
                Sid = _gatewaySid,
                Token = _fixture.Create<string>(),
                Data = $"[\"{sensorSid}\"]"
            }.ToString());

        _messageTransport
            .Setup(x => x.SendRead(sensorSid))
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "read_ack",
                Sid = sensorSid,
                Model = UnifiedAqaraTemperatureHumiditySensor.XIAOMI_GATEWAY2_MODEL,
                Data = "{\"temperature\":\"2363\",\"humidity\":\"2902\",\"pressure\":\"9912\"}"
            }.ToString());

        _gateway.DiscoverDevices();

        discoveredSensor.Should().NotBeNull();
        discoveredSensor.Temperature.Should().Be(23.63f);
        discoveredSensor.Humidity.Should().Be(29.02f);
        discoveredSensor.Pressure.Should().Be(991.2f);
    }

    [Fact]
    public async Task OnDeviceDiscovered_DiscoversUnifiedWirelessSwitch()
    {
        var switchSid = _fixture.Create<string>();
        UnifiedXiaomiSmartWirelessSwitch discoveredSwitch = null;
        var doubleClickRaised = false;

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            discoveredSwitch = device as UnifiedXiaomiSmartWirelessSwitch;
            if (discoveredSwitch is not null)
                discoveredSwitch.OnClickAsync += click =>
                {
                    doubleClickRaised = click == UnifiedXiaomiSmartWirelessSwitch.ClickArg.DoubleClick;
                    return Task.CompletedTask;
                };
            return Task.CompletedTask;
        };

        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack",
                Sid = _gatewaySid,
                Token = _fixture.Create<string>(),
                Data = $"[\"{switchSid}\"]"
            }.ToString());

        _messageTransport
            .Setup(x => x.SendRead(switchSid))
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "read_ack",
                Sid = switchSid,
                Model = UnifiedXiaomiSmartWirelessSwitch.XIAOMI_GATEWAY2_MODEL,
                Data = "{\"voltage\":3032,\"status\":\"click\"}"
            }.ToString());

        _gateway.DiscoverDevices();

        _messageTransport.Raise(x => x.OnMessageReceived += null, new Gw2Response
        {
            Cmd = "report",
            Sid = switchSid,
            Model = UnifiedXiaomiSmartWirelessSwitch.XIAOMI_GATEWAY2_MODEL,
            Data = "{\"status\":\"double_click\"}"
        }.ToString());

        await WaitForMessageAsync(() => doubleClickRaised);
        discoveredSwitch.Should().NotBeNull();
        discoveredSwitch.Voltage.Should().Be(3.032f);
        discoveredSwitch.Status.Should().Be("double_click");
        doubleClickRaised.Should().BeTrue();
    }

    [Fact]
    public void LYWSD03MMC_Is_Not_Registered_For_XiaomiGateway2()
    {
        ProtocolDeviceRegistry.Find(DeviceProtocol.XiaomiGateway2)
            .Should()
            .NotContain(UnifiedXiaomiBluetoothHygrothermograph2.MODEL);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Discovers_Children_SubDevices()
    {
        var waterLeakSid = _fixture.Create<string>();
        var thSensorSid = _fixture.Create<string>();
        var discovered = new System.Collections.Concurrent.ConcurrentBag<string>();

        _gateway.OnDeviceDiscoveredAsync += device =>
        {
            discovered.Add(device.DeviceId);
            return Task.CompletedTask;
        };

        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack",
                Sid = _gatewaySid,
                Token = _fixture.Create<string>(),
                Data = $"[\"{waterLeakSid}\",\"{thSensorSid}\"]"
            }.ToString());

        _messageTransport
            .Setup(x => x.SendRead(waterLeakSid))
            .Raises(x => x.OnMessageReceived += null, new Gw2Response { Cmd = "read_ack", Sid = waterLeakSid, Model = "sensor_wleak.aq1", Data = "{\"voltage\":3015}" }.ToString());

        _messageTransport
            .Setup(x => x.SendRead(thSensorSid))
            .Raises(x => x.OnMessageReceived += null, new Gw2Response { Cmd = "read_ack", Sid = thSensorSid, Model = "sensor_ht", Data = "{\"voltage\":3025}" }.ToString());

        await _gateway.DiscoverDevicesAsync();

        discovered.Should().BeEquivalentTo([waterLeakSid, thSensorSid]);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Throws_When_Gateway_Does_Not_Answer()
    {
        _gateway.DiscoveryTimeout = TimeSpan.FromMilliseconds(200);

        var act = () => _gateway.DiscoverDevicesAsync();

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public void DiscoverDevices_Throws_When_Gateway_Does_Not_Answer()
    {
        _gateway.DiscoveryTimeout = TimeSpan.FromMilliseconds(200);

        var act = () => _gateway.DiscoverDevices();

        act.Should().Throw<TimeoutException>()
            .WithMessage("Gateway did not answer the discovery request*");
    }

    [Fact]
    public async Task Discovered_Plug_Sends_Commands_With_Gateway_Password()
    {
        var plugSid = _fixture.Create<string>();
        SetupIdList(plugSid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{\"rgb\":0}"), 1);
        SetupReadAnswers(plugSid, ReadAck(plugSid, "plug", "{\"status\":\"off\"}"), 1);
        MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN plug = null;
        _gateway.OnDeviceDiscoveredAsync += d => { plug ??= d as MiHomeLib.Devices.XiaomiMiSmartPowerPlugCN; return Task.CompletedTask; };

        await _gateway.DiscoverDevicesAsync();
        plug.PowerOn();

        _messageTransport.Verify(x => x.SendWrite(plugSid, "plug", "f40e1b285fes68cd",
            It.Is<(string Key, string Value)[]>(d => d.Length == 1 && d[0].Key == "status" && d[0].Value == "on")), Times.Once());
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Does_Not_Repeat_Reads_When_All_Devices_Answer()
    {
        var sensorSid = _fixture.Create<string>();
        SetupIdList(sensorSid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{\"rgb\":0}"), 1);
        SetupReadAnswers(sensorSid, ReadAck(sensorSid, "sensor_wleak.aq1", "{\"voltage\":3015}"), 1);

        await _gateway.DiscoverDevicesAsync();

        VerifyReads(_gatewaySid, 1);
        VerifyReads(sensorSid, 1);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Repeats_Read_For_Device_That_Did_Not_Answer()
    {
        var lostSid = _fixture.Create<string>();
        var answeringSid = _fixture.Create<string>();
        var discovered = new List<string>();
        _gateway.OnDeviceDiscoveredAsync += d => { discovered.Add(d.DeviceId); return Task.CompletedTask; };

        var firstWaitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstWaitExpired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRepliesProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.ReadAnswerDelay = (_, cancellationToken) => firstWaitStarted.TrySetResult()
            ? firstWaitExpired.Task.WaitAsync(cancellationToken)
            : Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        _gateway.OnIlluminationChangeAsync += _ =>
        {
            firstRepliesProcessed.TrySetResult();
            return Task.CompletedTask;
        };

        SetupIdList(lostSid, answeringSid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{\"rgb\":0}"), 1);
        SetupReadAnswers(lostSid, ReadAck(lostSid, "sensor_wleak.aq1", "{\"voltage\":3015}"), 2);
        SetupReadAnswers(answeringSid, ReadAck(answeringSid, "sensor_ht", "{\"voltage\":3025}"), 1);

        var testTimeout = TimeSpan.FromSeconds(5);
        var discovery = _gateway.DiscoverDevicesAsync();
        await firstWaitStarted.Task.WaitAsync(testTimeout);

        // Queue the report behind the first replies so their acknowledgements are recorded before expiring the wait.
        RaiseGatewayMessage("report", _gatewaySid, "{\"illumination\":1}");
        await firstRepliesProcessed.Task.WaitAsync(testTimeout);
        firstWaitExpired.SetResult();
        await discovery.WaitAsync(testTimeout);

        discovered.Should().Contain([lostSid, answeringSid], "the device that missed the first read is found by the repeated one");
        VerifyReads(lostSid, 2);
        VerifyReads(answeringSid, 1);
        VerifyReads(_gatewaySid, 1);
    }

    [Fact]
    public void DiscoverDevices_Gives_Up_After_ReadAttempts_And_Logs_Warning()
    {
        var silentSid = _fixture.Create<string>();
        SetupIdList(silentSid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{\"rgb\":0}"), 1);
        SetupReadAnswers(silentSid, ReadAck(silentSid, "sensor_wleak.aq1", "{\"voltage\":3015}"));

        _gateway.Invoking(x => x.DiscoverDevices()).Should().NotThrow();

        VerifyReads(silentSid, 3);
        _loggerFactory.Entries.Should().Contain(x =>
            x.Level == Microsoft.Extensions.Logging.LogLevel.Warning &&
            x.Message.Contains($"'{silentSid}' did not answer the read request after 3 attempts"));
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Completes_After_The_Last_Answered_Device_Is_Reported()
    {
        var sensorSid = _fixture.Create<string>();
        var discovered = false;
        _gateway.OnDeviceDiscoveredAsync += d => { if (d.DeviceId == sensorSid) discovered = true; return Task.CompletedTask; };

        SetupIdList(sensorSid);
        SetupReadAnswers(_gatewaySid, ReadAck(_gatewaySid, "gateway", "{\"rgb\":0}"), 1);
        SetupReadAnswers(sensorSid, ReadAck(sensorSid, "sensor_wleak.aq1", "{\"voltage\":3015}"), 1);

        await _gateway.DiscoverDevicesAsync();

        discovered.Should().BeTrue();
    }

    [Fact]
    public async Task DiscoverDevicesAsync_Does_Not_Block_A_Thread_During_Pauses()
    {
        var sids = Enumerable.Range(0, 3).Select(_ => _fixture.Create<string>()).ToArray();

        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack",
                Sid = _gatewaySid,
                Token = _fixture.Create<string>(),
                Data = JsonSerializer.Serialize(sids)
            }.ToString());

        _gateway.ReadAttempts = 1;
        _gateway.ReadDeviceInterval = TimeSpan.FromMilliseconds(100);

        // Three 100 ms pauses: a blocking wait would hold the caller for at least 300 ms
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var discovery = _gateway.DiscoverDevicesAsync();
        var returnedAfter = sw.ElapsedMilliseconds;

        await discovery;

        returnedAfter.Should().BeLessThan(250, "DiscoverDevicesAsync must return to the caller before the pauses run");
        _messageTransport.Verify(x => x.SendRead(It.IsAny<string>()), Times.Exactly(sids.Length + 1));
    }
}
