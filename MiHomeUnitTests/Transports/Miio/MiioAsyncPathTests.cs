using MiHomeLib.MiioDevices.Internal;
using System;
using System.Collections.Concurrent;
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

/// <summary>
/// Devices on top of the real MiioTransport and a fake miio device on localhost.
/// </summary>
[Xunit.Trait("Category", "Integration")]
public sealed class MiioAsyncPathTests : IAsyncLifetime
{
    private const string TOKEN = "d5e589986c860a6d814ac8aa47240646";
    private const string HELLO = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";

    private readonly UdpClient _device = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<JsonNode> _requests = new();
    private readonly Task _deviceLoop;
    private readonly MiioTransport _transport;

    public MiioAsyncPathTests()
    {
        _transport = new MiioTransport("127.0.0.1", TOKEN, ((IPEndPoint)_device.Client.LocalEndPoint).Port);
        _deviceLoop = RunDevice();
    }

    private async Task RunDevice()
    {
        var hello = new MiioPacket(HELLO);
        while (!_stop.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try { received = await _device.ReceiveAsync(_stop.Token); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { return; }
            catch (ObjectDisposedException) when (_stop.IsCancellationRequested) { return; }
            catch (SocketException) when (_stop.IsCancellationRequested) { return; }

            if (received.Buffer.Length == 32)
            {
                await _device.SendAsync(HELLO.ToByteArray(), 32, received.RemoteEndPoint);
                continue;
            }

            var request = JsonNode.Parse(new MiioPacket(received.Buffer).GetResponseData(TOKEN));
            _requests.Enqueue(request);
            var response = new JsonObject { ["id"] = request["id"].GetValue<int>(), ["result"] = new JsonArray(request["method"].GetValue<string>() == "get_prop" ? "off" : "ok") }.ToJsonString();
            var bytes = hello.BuildMessage(response, TOKEN).ToByteArray();
            await _device.SendAsync(bytes, bytes.Length, received.RemoteEndPoint);
        }
    }

    [Fact]
    public async Task Transport_Numbers_Requests_And_Builds_The_Same_Request()
    {
        using var humidifier = new XiaomiAirHumidifierV1(_transport);

        await humidifier.TurnOnAsync();
        humidifier.TurnOff();

        var requests = _requests.ToArray();
        requests.Select(x => x["method"].GetValue<string>()).Should().Equal("set_power", "set_power");
        requests.Select(x => x["params"].ToJsonString()).Should().Equal("[\"on\"]", "[\"off\"]");
        var ids = requests.Select(x => x["id"].GetValue<int>()).ToArray();
        ids[0].Should().BeGreaterThan(100_000, "ids of requests numbered by the transport start far from the ids devices use");
        ids[1].Should().Be(ids[0] + 1);
    }

    [Fact]
    public void Synchronous_Methods_Do_Not_Deadlock_In_A_Single_Threaded_Synchronization_Context()
    {
        using var humidifier = new XiaomiAirHumidifierV1(_transport);
        using var context = new SingleThreadContext();
        Exception failure = null;

        // Like a UI thread: a continuation posted back to this context would wait for the blocked thread forever
        var finished = context.Run(() =>
        {
            try
            {
                humidifier.TurnOn();
                humidifier.GetPowerState().Should().BeFalse("the fake device answers \"off\"");
            }
            catch (Exception e) { failure = e; }
        }, TimeSpan.FromSeconds(10));

        finished.Should().BeTrue("a missing ConfigureAwait(false) inside the library would deadlock here");
        failure.Should().BeNull();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _stop.Cancel();
        _transport.Dispose();
        try { await _deviceLoop.WaitAsync(TimeSpan.FromSeconds(2)); }
        finally
        {
            _device.Dispose();
            _stop.Dispose();
        }
    }

    /// <summary>
    /// A synchronization context with one thread, like the one of a UI application
    /// </summary>
    private sealed class SingleThreadContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback, object)> _queue = new();
        private readonly Thread _thread;

        public SingleThreadContext()
        {
            _thread = new Thread(() =>
            {
                SetSynchronizationContext(this);
                foreach (var (callback, state) in _queue.GetConsumingEnumerable()) callback(state);
            }) { IsBackground = true };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback callback, object state) => _queue.Add((callback, state));

        public bool Run(Action action, TimeSpan timeout)
        {
            using var done = new ManualResetEventSlim();
            Post(_ => { action(); done.Set(); }, null);
            return done.Wait(timeout);
        }

        public void Dispose() => _queue.CompleteAdding();
    }
}
