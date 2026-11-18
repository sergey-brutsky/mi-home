using MiHomeLib.MiioDevices.Internal;
using System.Linq;
using MiHomeLib.Helpers;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.MiioDevices;
using Xunit;
using static MiHomeLib.MiioDevices.Internal.MiioTransport;

namespace MiHomeUnitTests.Transports.Miio;

/// <summary>
/// A fake miio device on localhost that loses and delays datagrams like a poor Wi-Fi or VPN link
/// </summary>
[Xunit.Trait("Category", "Integration")]
public sealed class MiioTransportRetryTests : IAsyncLifetime
{
    private const string TOKEN = "d5e589986c860a6d814ac8aa47240646";
    private const string HELLO = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";

    private readonly UdpClient _device = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<int> _requestIds = new();
    private readonly Task _deviceLoop;
    private readonly MiioTransport _transport;
    private int _hellos;
    private bool _loseFirstHello;
    private string _helloResponse = HELLO;
    private Action _onHello = () => { };
    private readonly ConcurrentQueue<uint> _stamps = new();
    private readonly TaskCompletionSource<bool> _repeatedRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentBag<Task> _replies = new();

    // Decides how the device answers a request: returns the ids to answer with, in order (empty: request lost)
    private Func<int, int, int[]> _answer = (attempt, id) => [id];
    private Func<UdpReceiveResult, Task> _beforeReply = _ => Task.CompletedTask;

    public MiioTransportRetryTests()
    {
        _transport = new MiioTransport("127.0.0.1", TOKEN, ((IPEndPoint)_device.Client.LocalEndPoint).Port)
        {
            ResponseTimeout = TimeSpan.FromSeconds(3),
            AttemptTimeout = TimeSpan.FromMilliseconds(200)
        };
        _deviceLoop = RunDevice();
    }

    [Fact]
    public async Task Repeated_Send_Count_Limits_Datagram_Attempts()
    {
        // Complete the hello first, then lose the command's only permitted attempt.
        await _transport.SendMessageAsync("{\"id\":60,\"method\":\"warmup\",\"params\":[]}");
        var before = _requestIds.Count;
        _answer = (_, _) => [];
        Action send = () => _transport.SendMessageRepeated("{\"id\":61,\"method\":\"get_prop\",\"params\":[]}", 1);
        send.Should().Throw<TimeoutException>();
        _requestIds.Count.Should().Be(before + 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Datagrams_From_Another_Port_Are_Ignored_Before_Parsing(bool hello)
    {
        var injected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var otherSender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        _beforeReply = async received =>
        {
            if ((received.Buffer.Length == 32) != hello) return;
            await otherSender.SendAsync(new byte[] { 1, 2, 3 }, received.RemoteEndPoint);
            injected.TrySetResult(true);
            await release.Task;
        };
        var send = _transport.SendMessageAsync("{\"id\":50,\"method\":\"get_prop\",\"params\":[]}");
        try
        {
            await injected.Task.WaitAsync(TimeSpan.FromSeconds(2));
            // Hold the real response until the foreign datagram has had time to be received.
            (await Task.WhenAny(send, Task.Delay(50))).Should().NotBe(send);
        }
        finally { release.TrySetResult(true); }
        var response = await send.WaitAsync(TimeSpan.FromSeconds(2));
        JsonNode.Parse(response)["result"][0].GetValue<string>().Should().Be("ok");
    }

    [Fact]
    public async Task Dispose_Cancels_Active_And_Queued_Commands()
    {
        _answer = (_, _) => [];
        var first = _transport.SendMessageAsync("{\"id\":40,\"method\":\"get_prop\",\"params\":[]}");
        var second = _transport.SendMessageAsync("{\"id\":41,\"method\":\"get_prop\",\"params\":[]}");
        _transport.Dispose();
        Func<Task> active = async () => await first.WaitAsync(TimeSpan.FromSeconds(1));
        Func<Task> queued = async () => await second.WaitAsync(TimeSpan.FromSeconds(1));
        await active.Should().ThrowAsync<OperationCanceledException>();
        await queued.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Parallel_Commands_Receive_Their_Own_Responses()
    {
        var responses = await Task.WhenAll(System.Linq.Enumerable.Range(100, 8)
            .Select(id => _transport.SendMessageAsync($"{{\"id\":{id},\"method\":\"get_prop\",\"params\":[]}}")));
        for (var i = 0; i < responses.Length; i++)
            JsonNode.Parse(responses[i])["id"].GetValue<int>().Should().Be(100 + i);
    }

    [Fact]
    public void Synchronous_Repeated_Send_Uses_Real_Transport()
    {
        _transport.Ip.Should().Be("127.0.0.1");
        _transport.Token.Should().Be(TOKEN);
        var response = _transport.SendMessageRepeated("{\"id\":15,\"method\":\"get_prop\",\"params\":[]}", 2);
        JsonNode.Parse(response)["result"][0].GetValue<string>().Should().Be("ok");
    }

    private async Task RunDevice()
    {
        while (!_stop.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try { received = await _device.ReceiveAsync(_stop.Token); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { return; }
            catch (ObjectDisposedException) when (_stop.IsCancellationRequested) { return; }
            catch (SocketException) when (_stop.IsCancellationRequested) { return; }

            if (received.Buffer.Length == 32)
            {
                if (Interlocked.Increment(ref _hellos) == 1 && _loseFirstHello) continue; // the first hello is lost
                await _beforeReply(received);
                _onHello();
                await _device.SendAsync(_helloResponse.ToByteArray(), 32, received.RemoteEndPoint);
                continue;
            }

            var packet = new MiioPacket(received.Buffer);
            _stamps.Enqueue(packet.Stamp);
            var request = JsonNode.Parse(packet.GetResponseData(TOKEN));
            var id = request["id"].GetValue<int>();
            _requestIds.Enqueue(id);
            var attempt = _requestIds.Count;
            if (attempt == 2) _repeatedRequest.TrySetResult(true);
            _replies.Add(Reply(received, attempt, id));
        }

        async Task Reply(UdpReceiveResult received, int attempt, int id)
        {
            await _beforeReply(received).WaitAsync(_stop.Token);
            foreach (var answerId in _answer(attempt, id))
            {
                var response = new JsonObject { ["id"] = answerId, ["result"] = new JsonArray("ok") }.ToJsonString();
                var bytes = new MiioPacket(_helloResponse).BuildMessage(response, TOKEN).ToByteArray();
                await _device.SendAsync(bytes, bytes.Length, received.RemoteEndPoint);
            }
        }
    }

    [Fact]
    public async Task Lost_Hello_And_Lost_Request_Are_Repeated_With_A_New_Id()
    {
        _loseFirstHello = true;
        _answer = (attempt, id) => attempt == 1 ? [] : [id];

        var response = await _transport.SendMessageAsync("{\"id\":7,\"method\":\"get_prop\",\"params\":[]}");

        _hellos.Should().BeGreaterThan(1, "the lost hello is sent again");
        _requestIds.Should().HaveCount(2);
        _requestIds.Should().OnlyHaveUniqueItems("some devices ignore a request id they have already seen");
        _requestIds.Should().StartWith(7);
        JsonNode.Parse(response)["id"].GetValue<int>().Should().Be(_requestIds.ToArray()[1]);
    }

    [Fact]
    public void Answer_To_Another_Request_Is_Skipped()
    {
        // A late answer to an earlier request arrives before the right one
        _answer = (attempt, id) => [id - 1, id];

        var response = _transport.SendMessage("{\"id\":42,\"method\":\"get_prop\",\"params\":[]}");

        JsonNode.Parse(response)["id"].GetValue<int>().Should().Be(42);
        _requestIds.Should().ContainSingle();
    }

    [Fact]
    public async Task Late_Answer_To_An_Earlier_Attempt_Is_Accepted()
    {
        // The device answers only the first attempt, after the transport has already repeated the request
        _beforeReply = received => received.Buffer.Length != 32 && _requestIds.Count == 1
            ? _repeatedRequest.Task.WaitAsync(_stop.Token) : Task.CompletedTask;
        _answer = (attempt, id) => attempt == 1 ? [id] : [];

        var response = await _transport.SendMessageAsync("{\"id\":5,\"method\":\"get_prop\",\"params\":[]}");

        JsonNode.Parse(response)["id"].GetValue<int>().Should().Be(5);

        _requestIds.Count.Should().BeGreaterThan(1, "the request was repeated while the first answer was delayed");
    }

    [Fact]
    public async Task Gives_Up_After_ResponseTimeout_With_Number_Of_Attempts()
    {
        _transport.ResponseTimeout = TimeSpan.FromMilliseconds(900);
        await _transport.SendMessageAsync("{\"id\":1,\"method\":\"warm_up\",\"params\":[]}");
        _answer = (_, _) => [];

        var act = () => _transport.SendMessageAsync("{\"id\":2,\"method\":\"get_prop\",\"params\":[]}");

        (await act.Should().ThrowAsync<TimeoutException>()).WithMessage("*127.0.0.1*attempts*");
    }

    [Fact]
    public async Task Time_Spent_In_Queue_Does_Not_Consume_The_Next_Commands_Timeout()
    {
        _transport.ResponseTimeout = TimeSpan.FromMilliseconds(400);
        _transport.AttemptTimeout = TimeSpan.FromMilliseconds(400);
        _beforeReply = received => received.Buffer.Length == 32
            ? Task.CompletedTask : Task.Delay(250, _stop.Token);

        var responses = await Task.WhenAll(Enumerable.Range(200, 3).Select(id =>
            _transport.SendMessageAsync($"{{\"id\":{id},\"method\":\"get_prop\",\"params\":[]}}")))
            .WaitAsync(TimeSpan.FromSeconds(3));

        responses.Select(response => JsonNode.Parse(response)["id"].GetValue<int>()).Should().Equal(200, 201, 202);
        _requestIds.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("action")]
    [InlineData("set_properties")]
    [InlineData("vendor_unknown")]
    public async Task Lost_Nonrepeatable_Response_Does_Not_Execute_The_Command_Twice(string method)
    {
        _transport.ResponseTimeout = TimeSpan.FromMilliseconds(250);
        _transport.AttemptTimeout = TimeSpan.FromMilliseconds(50);
        var power = false;
        _answer = (_, _) => { power = !power; return []; };
        object parameters = method == "set_properties"
            ? new[] { new { did = "set-4-6", siid = 4, piid = 6, value = true } }
            : new { siid = 2, aiid = 1 };
        Func<Task> send = () => _transport.SendMessageAsync(new { id = 10, method, @params = parameters }.ToJson());

        await send.Should().ThrowAsync<TimeoutException>().WithMessage("*outcome is unknown*");

        power.Should().BeTrue();
        _requestIds.Should().ContainSingle();
        _answer = (_, id) => [id];
        await _transport.SendMessageAsync("{\"id\":20,\"method\":\"get_prop\",\"params\":[]}");
        _requestIds.Should().Equal(new[] { 10, 20 }, "the failed action must not be replayed during recovery");
        _hellos.Should().Be(2);
        power.Should().BeTrue();
    }

    [Fact]
    public async Task Next_Request_After_Reboot_Timeout_Refreshes_Hello_And_Device_Clock()
    {
        await _transport.SendMessageAsync("{\"id\":10,\"method\":\"get_prop\",\"params\":[]}");
        var acceptsCommands = false;
        _helloResponse = "21310020000000000404a850000000015d3a2f018c90097a850558c35c953b77";
        _onHello = () => acceptsCommands = true;
        _answer = (_, id) => acceptsCommands ? [id] : [];
        _transport.ResponseTimeout = TimeSpan.FromMilliseconds(250);
        _transport.AttemptTimeout = TimeSpan.FromMilliseconds(50);
        Func<Task> failed = () => _transport.SendMessageAsync("{\"id\":20,\"method\":\"get_prop\",\"params\":[]}");
        await failed.Should().ThrowAsync<TimeoutException>();
        _hellos.Should().Be(1, "hello is refreshed on the next request, not by replaying the failed operation");

        var response = await _transport.SendMessageAsync("{\"id\":30,\"method\":\"get_prop\",\"params\":[]}");

        JsonNode.Parse(response)["id"].GetValue<int>().Should().Be(30);
        _hellos.Should().Be(2);
        _stamps.Last().Should().BeInRange(1u, 3u);
        await _transport.SendMessageAsync("{\"id\":40,\"method\":\"get_prop\",\"params\":[]}");
        _stamps.Last().Should().BeInRange(1u, 3u);
        _hellos.Should().Be(2, "a successful refresh is reused by subsequent requests");
    }

    [Fact]
    public async Task Setting_A_Known_Value_Can_Be_Repeated_When_The_Response_Is_Lost()
    {
        _transport.ResponseTimeout = TimeSpan.FromMilliseconds(400);
        _transport.AttemptTimeout = TimeSpan.FromMilliseconds(50);
        var power = false;
        _answer = (attempt, id) => { power = true; return attempt == 1 ? [] : [id]; };

        await _transport.SendMessageAsync("{\"id\":10,\"method\":\"set_power\",\"params\":[\"on\"]}");

        power.Should().BeTrue();
        _requestIds.Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _stop.Cancel();
        _transport.Dispose();
        try
        {
            await _deviceLoop.WaitAsync(TimeSpan.FromSeconds(2));
            try { await Task.WhenAll(_replies).WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
        finally
        {
            _device.Dispose();
            _stop.Dispose();
        }
    }
}
