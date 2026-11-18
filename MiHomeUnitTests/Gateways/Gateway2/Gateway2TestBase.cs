using MiHomeUnitTests.Devices.Gateway2;
using MiHomeUnitTests.Infrastructure;
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

public abstract class Gateway2TestBase: Gateway2DeviceTestBase, IAsyncLifetime
{
    protected readonly string _gatewaySid;
    protected readonly XiaomiGateway2 _gateway;

    private readonly List<Task> _pendingDiscoveries = [];
    protected void StartDiscovery() => _pendingDiscoveries.Add(_gateway.DiscoverDevicesAsync());

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _gateway.Dispose();
        try
        {
            await _gateway.MessageProcessingCompletion.WaitAsync(TimeSpan.FromSeconds(2));
            foreach (var discovery in _pendingDiscoveries)
            {
                try { await discovery.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch (OperationCanceledException) { } // Disposal cancels unfinished discovery.
            }
        }
        finally { base.Dispose(); }
    }

    // Reports enter an asynchronous queue, so wait for processing before checking the original expectations.
    protected static async Task WaitForMessageAsync(Func<bool> processed)
    {
        using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!processed()) await Task.Delay(1, timeout.Token);
    }

    protected static string ToRadioListJson(int[] radioChannelIds)
    {
        var list = radioChannelIds
            .Select(x => new { id = x, type = 0, url = $"http://192.168.1.1/radio{x}.m3u8"});

        return JsonSerializer.Serialize(new { result = new { chs = list }});
    }

    protected Gateway2TestBase()
    {
        _gatewaySid = _fixture.Create<string>()[..12];

        _miioTransport = new Mock<IMiioTransport>();
        SendResultMethod(string.Empty, ["f40e1b285fes68cd"]);

        _gateway = new XiaomiGateway2(_miioTransport.Object, _messageTransport.Object, _gatewaySid, 0, _loggerFactory)
        {
            // keep tests fast: unanswered reads are repeated after a short wait
            ReadDeviceInterval = TimeSpan.FromMilliseconds(10),
            ReadAnswerTimeout = TimeSpan.FromMilliseconds(50)
        };
    }

    protected int DeveloperKeyRequests() => _miioTransport.Invocations.Count(i =>
        i.Method.Name == nameof(IMiioTransport.SendMessageAsync) && ((string)i.Arguments[0]).Contains("get_lumi_dpf_aes_key"));

    protected sealed class NonPumpingContext : System.Threading.SynchronizationContext
    {
        public int Posts;
        public override void Post(System.Threading.SendOrPostCallback callback, object state) =>
            System.Threading.Interlocked.Increment(ref Posts);
    }

    protected void RaiseGatewayMessage(string cmd, string sid, string data, string token = null) =>
        _messageTransport.Raise(x => x.OnMessageReceived += null,
            new Gw2Response { Cmd = cmd, Sid = sid, Model = "gateway", Token = token, Data = data }.ToString());

    protected string ReadAck(string sid, string model, string data) =>
        new Gw2Response { Cmd = "read_ack", Sid = sid, Model = model, Data = data }.ToString();

    protected void SetupIdList(params string[] sids) =>
        _messageTransport
            .Setup(x => x.SendDiscover())
            .Raises(x => x.OnMessageReceived += null, new Gw2Response
            {
                Cmd = "get_id_list_ack",
                Sid = _gatewaySid,
                Token = _fixture.Create<string>(),
                Data = JsonSerializer.Serialize(sids)
            }.ToString());

    // Answers the read of the sid only on the given attempts, like a gateway losing some requests
    protected void SetupReadAnswers(string sid, string ack, params int[] answeredAttempts)
    {
        var attempt = 0;
        _messageTransport
            .Setup(x => x.SendRead(sid))
            .Callback(() =>
            {
                if (answeredAttempts.Contains(++attempt))
                    _messageTransport.Raise(x => x.OnMessageReceived += null, ack);
            });
    }

    protected void VerifyReads(string sid, int times) =>
        _messageTransport.Verify(x => x.SendRead(sid), Times.Exactly(times));

    public static TheoryData<bool, string, int, Func<XiaomiGateway2, bool, Task<int>>, object[]> IntGetters => new()
    {
        { false, "wait time", 15, (gw, a) => Call(a, gw.GetArmingWaitTime, gw.GetArmingWaitTimeAsync), ["get_arm_wait_time", Array.Empty<object>()] },
        { true, "wait time", 30, (gw, a) => Call(a, gw.GetArmingWaitTime, gw.GetArmingWaitTimeAsync), ["get_arm_wait_time", Array.Empty<object>()] },
        { false, "off time", 15, (gw, a) => Call(a, gw.GetArmingOffTime, gw.GetArmingOffTimeAsync), ["get_device_prop", new object[] { "lumi.0", "alarm_time_len" }] },
        { true, "off time", 30, (gw, a) => Call(a, gw.GetArmingOffTime, gw.GetArmingOffTimeAsync), ["get_device_prop", new object[] { "lumi.0", "alarm_time_len" }] },
        { false, "blinking time", 15, (gw, a) => Call(a, gw.GetArmingBlinkingTime, gw.GetArmingBlinkingTimeAsync), ["get_device_prop", new object[] { "lumi.0", "en_alarm_light" }] },
        { true, "blinking time", 30, (gw, a) => Call(a, gw.GetArmingBlinkingTime, gw.GetArmingBlinkingTimeAsync), ["get_device_prop", new object[] { "lumi.0", "en_alarm_light" }] },
        { false, "volume", 10, (gw, a) => Call(a, gw.GetArmingVolume, gw.GetArmingVolumeAsync), ["get_alarming_volume", Array.Empty<object>()] },
        { true, "volume", 20, (gw, a) => Call(a, gw.GetArmingVolume, gw.GetArmingVolumeAsync), ["get_alarming_volume", Array.Empty<object>()] },
        { false, "last triggered", 1609150074, (gw, a) => Call(a, gw.GetArmingLastTimeTriggeredTimestamp, gw.GetArmingLastTimeTriggeredTimestampAsync), ["get_arming_time", Array.Empty<object>()] },
        { true, "last triggered", 1609150074, (gw, a) => Call(a, gw.GetArmingLastTimeTriggeredTimestamp, gw.GetArmingLastTimeTriggeredTimestampAsync), ["get_arming_time", Array.Empty<object>()] },
    };

    protected void VerifySetDeviceProp(object @params) =>
        _miioTransport.Verify(x => x.SendMessageAsync(new { id = 1, method = "set_device_prop", @params }.ToJson()), Times.Once());

    protected static readonly int[] DefaultChannels = [1025, 1026, 1027];

    protected void SetupRadioChannels(params int[] ids) =>
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_channels"))))
            .Returns(Task.FromResult(ToRadioListJson(ids)));

    protected static object Channel(int id, string url = null) => new { id, url = url ?? $"http://192.168.1.1/radio{id}.m3u8", type = 0 };

    protected void VerifySentSecond(string method, object @params) =>
        _miioTransport.Verify(x => x.SendMessageAsync(new { id = 2, method, @params }.ToJson()), Times.Once());

}
