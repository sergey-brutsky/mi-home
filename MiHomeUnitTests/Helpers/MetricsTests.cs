using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib;
using MiHomeLib.Devices;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices.Internal;
using MiHomeLib.XiaomiGateway2;
using MiHomeUnitTests.Gateways.Gateway2;
using Xunit;
using static MiHomeLib.MiioDevices.Internal.MiioTransport;

namespace MiHomeUnitTests.Helpers;

[CollectionDefinition("Metrics", DisableParallelization = true)]
public sealed class MetricsCollection { }

[Collection("Metrics")]
public sealed class MetricsTests : Gateway2TestBase
{
    private const string Token = "d5e589986c860a6d814ac8aa47240646";
    private const string Hello = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";

    [Fact]
    public async Task Successful_Miio_Request_Records_Duration_And_Method()
    {
        using var metrics = new MetricsRecorder();
        await using var device = new MiioResponder(Token);
        device.DropNextRequests = 1;
        using var transport = new MiioTransport("127.0.0.1", Token, device.Port)
        {
            ResponseTimeout = TimeSpan.FromSeconds(2),
            AttemptTimeout = TimeSpan.FromMilliseconds(150)
        };

        await transport.RequestAsync("metrics_success", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat);

        var measurement = metrics.GetSingle("mihome.miio.request.duration");
        measurement.Tags["method"].Should().Be("metrics_success");
        measurement.Tags["outcome"].Should().Be("success");
        measurement.Value.Should().BeGreaterThan(0);
        var retries = metrics.GetSingle("mihome.miio.request.retries");
        retries.Tags["method"].Should().Be("metrics_success");
        retries.Value.Should().Be(1);
    }

    [Fact]
    public async Task Timed_Out_Miio_Request_Records_Timeout_Outcome()
    {
        using var metrics = new MetricsRecorder();
        using var silentDevice = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)silentDevice.Client.LocalEndPoint).Port;
        using var transport = new MiioTransport("127.0.0.1", Token, port)
        {
            ResponseTimeout = TimeSpan.FromMilliseconds(150),
            AttemptTimeout = TimeSpan.FromMilliseconds(50)
        };

        var act = () => transport.RequestAsync("metrics_timeout", Array.Empty<object>(), MiioRetryPolicy.SafeToRepeat);

        await act.Should().ThrowAsync<TimeoutException>();
        var measurement = metrics.GetSingle("mihome.miio.request.duration");
        measurement.Tags["method"].Should().Be("metrics_timeout");
        measurement.Tags["outcome"].Should().Be("timeout");
    }

    [Fact]
    public async Task Gateway2_Buffer_Overflow_Records_Dropped_Message_Reason()
    {
        using var metrics = new MetricsRecorder();
        _gateway.MaxPendingMessages = 0;
        var discovery = _gateway.DiscoverDevicesAsync();

        RaiseGatewayMessage("report", _gatewaySid, """{"rgb":1}""");

        Func<Task> wait = async () => await discovery;
        await wait.Should().ThrowAsync<System.IO.IOException>();
        var measurement = metrics.GetSingle("mihome.messages.dropped");
        measurement.Tags["gateway"].Should().Be("XiaomiGateway2");
        measurement.Tags["reason"].Should().Be("buffer_full");
    }

    [Fact]
    public void Device_Availability_Transition_Records_Available_State()
    {
        using var metrics = new MetricsRecorder();
        using var device = new XiaomiTemperatureHumiditySensor("metrics-availability", 0, _loggerFactory)
        {
            AvailabilityTimeout = TimeSpan.FromSeconds(1)
        };
        var now = DateTime.UtcNow;
        device.MarkAdded(now);

        device.CheckAvailability(now.AddSeconds(2));

        var measurement = metrics.GetSingle("mihome.devices.availability.changed");
        measurement.Tags["available"].Should().Be("False");
    }

    private sealed class MetricsRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<(string Name, double Value, Dictionary<string, string> Tags)> _measurements = new();

        public MetricsRecorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MiHomeDiagnostics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>(Record);
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Record(instrument, value, tags, null));
            _listener.Start();
        }

        public (string Name, double Value, Dictionary<string, string> Tags) GetSingle(string name) =>
            _measurements.Where(item => item.Name == name).Should().ContainSingle().Which;

        public void Dispose() => _listener.Dispose();

        private void Record<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
            where T : struct, IConvertible
        {
            var tagValues = new Dictionary<string, string>();
            foreach (var tag in tags)
                tagValues[tag.Key] = Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture);

            _measurements.Enqueue((instrument.Name, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture), tagValues));
        }
    }

    private sealed class MiioResponder : IAsyncDisposable
    {
        private readonly UdpClient _device = new(new IPEndPoint(IPAddress.Loopback, 0));
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _server;
        private int _dropNextRequests;

        public MiioResponder(string token) => _server = RunAsync(token);

        public int Port => ((IPEndPoint)_device.Client.LocalEndPoint).Port;
        public int DropNextRequests
        {
            get => Volatile.Read(ref _dropNextRequests);
            set => Volatile.Write(ref _dropNextRequests, value);
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _device.Dispose();
            try { await _server.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            finally { _stop.Dispose(); }
        }

        private async Task RunAsync(string token)
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

                var request = JsonNode.Parse(new MiioPacket(received.Buffer).GetResponseData(token));
                if (Interlocked.CompareExchange(ref _dropNextRequests, 0, 0) > 0)
                {
                    Interlocked.Decrement(ref _dropNextRequests);
                    continue;
                }
                var response = new JsonObject
                {
                    ["id"] = request["id"].GetValue<int>(),
                    ["result"] = new JsonArray("ok")
                };
                var packet = new MiioPacket(Hello).BuildMessage(response.ToJsonString(), token).ToByteArray();
                await _device.SendAsync(packet, received.RemoteEndPoint);
            }
        }
    }
}
