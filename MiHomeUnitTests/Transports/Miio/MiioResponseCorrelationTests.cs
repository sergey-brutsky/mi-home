using MiHomeLib.MiioDevices.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices;
using Xunit;
using static MiHomeLib.MiioDevices.Internal.MiioTransport;

namespace MiHomeUnitTests.Transports.Miio;

[Trait("Category", "Integration")]
public sealed class MiioResponseCorrelationTests : IAsyncLifetime
{
    private const string Token = "d5e589986c860a6d814ac8aa47240646";
    private const string Hello = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";
    private static readonly TimeSpan ExchangeTimeout = TimeSpan.FromSeconds(5);
    private readonly UdpClient _device = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private readonly MiioTransport _transport;
    private readonly Task _server;
    private Func<int?, IEnumerable<string>> _responses = _ => [];
    private int _commands;

    public MiioResponseCorrelationTests()
    {
        _transport = new MiioTransport("127.0.0.1", Token, ((IPEndPoint)_device.Client.LocalEndPoint).Port)
        {
            // The loopback peer and the transport both need time to run on a busy CI runner.
            ResponseTimeout = ExchangeTimeout,
            AttemptTimeout = ExchangeTimeout
        };
        _server = RunDeviceAsync();
    }

    [Fact]
    public async Task Gateway_Device_Id_Is_Read_From_The_Hello_Header()
    {
        _transport.DeviceId.Should().BeNull();
        _responses = id => ["{\"id\":" + id + ",\"result\":[\"ok\"]}"];
        await _transport.RequestAsync("miIO.info", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat);
        _transport.DeviceId.Should().Be(0x0404a850u);
    }

    public static IEnumerable<object[]> InvalidResponses =>
        from response in new[]
        {
            "{\"result\":[\"stale\"]}",
            "{\"id\":null,\"result\":[\"stale\"]}",
            "{\"id\":\"42\",\"result\":[\"stale\"]}",
            "{\"id\":42.5,\"result\":[\"stale\"]}",
            "{\"id\":2147483648,\"result\":[\"stale\"]}",
            "{\"id\":true,\"result\":[\"stale\"]}",
            "{\"id\":{},\"result\":[\"stale\"]}",
            "{\"id\":[],\"result\":[\"stale\"]}",
            "{\"id\":41,\"result\":[\"stale\"]}",
            "{\"id\":41,\"id\":42,\"result\":[\"stale\"]}",
            "{\"id\":42,\"id\":41,\"result\":[\"stale\"]}",
            "not-json", "null", "[]", "17"
        }
        from isAsync in new[] { false, true }
        select new object[] { response, isAsync };

    [Fact]
    public async Task Gateway2_Format_Uses_Small_Ids_And_Still_Rejects_A_Stale_Write_Response()
    {
        _transport.UseGateway2RequestFormat = true;
        var ids = new List<int?>();
        _responses = id =>
        {
            ids.Add(id);
            return ["{\"id\":0,\"result\":[\"stale\"]}",
                new JsonObject { ["id"] = id, ["result"] = new JsonArray("ok") }.ToJsonString()];
        };
        await _transport.RequestAsync("miIO.info", Array.Empty<object>(), MiioRetryPolicy.DoNotRepeat);
        var response = await _transport.RequestAsync("set_lumi_dpf_aes_key", new[] { "" }, MiioRetryPolicy.DoNotRepeat);
        ids.Should().Equal(new int?[] { 1, 2 });
        JsonNode.Parse(response)["id"].GetValue<int>().Should().Be(2);
        JsonNode.Parse(response)["result"][0].GetValue<string>().Should().Be("ok");
        Volatile.Read(ref _commands).Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public async Task Numbered_Request_Ignores_Invalid_Or_Unmatched_Response_And_Waits_For_Its_Own(string invalid, bool isAsync)
    {
        const string valid = "{\"id\":42,\"result\":[\"current\"]}";
        _responses = _ => [invalid, valid];
        const string request = "{\"id\":42,\"method\":\"get_prop\",\"params\":[]}";

        var response = isAsync ? await _transport.SendMessageAsync(request)
            : await Task.Run(() => _transport.SendMessage(request));

        response.Should().Be(valid);
        Volatile.Read(ref _commands).Should().Be(1);
    }

    [Fact]
    public async Task Canceled_Request_Does_Not_Consume_A_Late_Response_For_The_Next_Request()
    {
        int? canceledRequestId = null;
        var requestObserved = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _responses = id =>
        {
            canceledRequestId = id;
            requestObserved.TrySetResult(id);
            return [];
        };
        using var cancellation = new CancellationTokenSource();

        var canceledRequest = _transport.RequestAsync(
            "cancelled_request", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat, cancellation.Token);
        await requestObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();
        Func<Task> wait = async () => await canceledRequest;
        await wait.Should().ThrowAsync<OperationCanceledException>();
        canceledRequestId.Should().NotBeNull();

        _responses = id =>
        [
            "{\"id\":" + canceledRequestId + ",\"result\":[\"late\"]}",
            "{\"id\":" + id + ",\"result\":[\"current\"]}",
        ];
        var response = await _transport.RequestAsync("next_request", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat);

        JsonNode.Parse(response)["result"][0].GetValue<string>().Should().Be("current");
    }

    [Fact]
    public async Task Already_Canceled_Request_Is_Not_Sent()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> request = async () => await _transport.RequestAsync(
            "must_not_be_sent", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat, cancellation.Token);
        await request.Should().ThrowAsync<OperationCanceledException>();
        Volatile.Read(ref _commands).Should().Be(0);
    }

    [Theory]
    [InlineData("{\"result\":[\"stale\"]}")]
    [InlineData("{\"id\":\"42\",\"result\":[\"stale\"]}")]
    [InlineData("not-json")]
    public async Task No_Matching_Response_Times_Out_Without_Replaying_An_Action(string invalid)
    {
        // Complete the hello before applying the short timeout to the action itself.
        _responses = id => ["{\"id\":" + id + ",\"result\":[\"ok\"]}"];
        await _transport.RequestAsync("miIO.info", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat);
        var commandsBeforeAction = Volatile.Read(ref _commands);

        _responses = _ => [invalid];
        _transport.ResponseTimeout = TimeSpan.FromMilliseconds(500);
        Func<Task> send = () => _transport.SendMessageAsync("{\"id\":42,\"method\":\"action\",\"params\":{}}");

        await send.Should().ThrowAsync<TimeoutException>().WithMessage("*outcome is unknown*");

        Volatile.Read(ref _commands).Should().Be(commandsBeforeAction + 1);
    }

    [Fact]
    public async Task Transport_Generated_Id_Also_Requires_A_Matching_Response()
    {
        int? generatedId = null;
        _responses = id =>
        {
            generatedId = id;
            return new[] { "{\"result\":[\"stale\"]}", new JsonObject { ["id"] = id, ["result"] = new JsonArray("current") }.ToJsonString() };
        };

        var response = await _transport.RequestAsync("get_prop", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat);

        generatedId.Should().BeGreaterThan(100_000);
        JsonNode.Parse(response)["id"].GetValue<int>().Should().Be(generatedId.Value);
        JsonNode.Parse(response)["result"][0].GetValue<string>().Should().Be("current");
    }

    [Theory]
    [InlineData("{\"result\":[\"current\"]}")]
    [InlineData("{\"id\":42,\"result\":[\"current\"]}")]
    public async Task Explicitly_Unnumbered_Raw_Request_Retains_Compatibility_With_Object_Responses(string valid)
    {
        _responses = _ => [valid];

        var response = await _transport.SendMessageAsync("{\"method\":\"get_prop\",\"params\":[]}");

        response.Should().Be(valid);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"id\":null,\"result\":[\"stale\"]}")]
    [InlineData("{\"id\":\"42\",\"result\":[\"stale\"]}")]
    public async Task Unnumbered_Raw_Request_Does_Not_Accept_Malformed_Responses(string invalid)
    {
        const string valid = "{\"result\":[\"current\"]}";
        _responses = _ => [invalid, valid];

        var response = await _transport.SendMessageAsync("{\"method\":\"get_prop\",\"params\":[]}");

        response.Should().Be(valid);
    }

    private async Task RunDeviceAsync()
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
                await _device.SendAsync(Hello.ToByteArray(), received.RemoteEndPoint);
                continue;
            }
            Interlocked.Increment(ref _commands);
            var request = JsonNode.Parse(new MiioPacket(received.Buffer).GetResponseData(Token));
            var id = request["id"]?.GetValue<int>();
            foreach (var response in _responses(id))
                await _device.SendAsync(new MiioPacket(Hello).BuildMessage(response, Token).ToByteArray(), received.RemoteEndPoint);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _stop.Cancel();
        _transport.Dispose();
        try { await _server.WaitAsync(TimeSpan.FromSeconds(2)); }
        finally { _device.Dispose(); _stop.Dispose(); }
    }
}
