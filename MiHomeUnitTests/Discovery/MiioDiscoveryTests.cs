using MiHomeLib.MiioDevices.Internal;
using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices;
using Xunit;

namespace MiHomeUnitTests.Discovery;

public class MiioDiscoveryTests
{
    [Fact]
    public void Missing_Device_Id_Reports_An_Invalid_Operation()
    {
        Action find = () => MiioDevice.GetDeviceIdByIp("127.0.0.1", []);
        find.Should().ThrowExactly<InvalidOperationException>().WithMessage("Cannot get device id for 127.0.0.1*");
    }
    [Theory]
    [InlineData("8000", "0000", "2147483648")]
    [InlineData("ffff", "ffff", "4294967295")]
    [InlineData("0404", "a850", "67414096")]
    public void Device_Id_Uses_Unsigned_32_Bit_Range(string type, string serial, string expected)
    {
        MiioDevice.GetDeviceIdByIp("127.0.0.1", [("127.0.0.1", type, serial, "token")]).Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Discovery_Sends_Hello_And_Returns_Responding_Device_Metadata(bool isAsync)
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        const string hello = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";
        var packet = new MiioTransport.MiioPacket(hello);
        var destination = (IPEndPoint)device.Client.LocalEndPoint;
        var discover = isAsync
            ? MiioTransport.SendDiscoverMessageAsync(destination, TimeSpan.FromMilliseconds(500))
            : Task.Run(() => MiioTransport.SendDiscoverMessage(destination, 500));
        var request = await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        request.Buffer.ToHex().Should().Be("21310020ffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        await device.SendAsync(new byte[] { 0, 1, 2 }, 3, request.RemoteEndPoint);
        var response = hello.ToByteArray();
        await device.SendAsync(response, response.Length, request.RemoteEndPoint);
        var results = await discover.WaitAsync(TimeSpan.FromSeconds(2));
        results.Should().ContainSingle().Which.Should().Be(("127.0.0.1", packet.GetDeviceType(), packet.GetSerial(), packet.GetChecksum()));
    }

    [Fact]
    public async Task Typed_Discovery_Returns_Hello_Checksum_As_Token()
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        const string hello = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";
        var packet = new MiioTransport.MiioPacket(hello);
        var discover = MiioDiscovery.DiscoverAsync((IPEndPoint)device.Client.LocalEndPoint, TimeSpan.FromMilliseconds(100));
        var request = await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await device.SendAsync(hello.ToByteArray(), request.RemoteEndPoint);
        var devices = await discover.WaitAsync(TimeSpan.FromSeconds(2));
        devices.Should().ContainSingle().Which.Should().Be(new MiioDiscovery.DiscoveredDevice(
            "127.0.0.1", packet.GetDeviceType(), packet.GetSerial(), packet.GetChecksum()));
    }

    [Fact]
    public async Task Incoming_Packets_Do_Not_Extend_The_Total_Discovery_Timeout()
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var stop = new CancellationTokenSource();
        var discover = MiioTransport.SendDiscoverMessageAsync((IPEndPoint)device.Client.LocalEndPoint, TimeSpan.FromMilliseconds(150));
        var request = await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var sender = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                await device.SendAsync(new byte[] { 1, 2 }, request.RemoteEndPoint);
                await Task.Delay(10, stop.Token);
            }
        });
        var elapsed = Stopwatch.StartNew();
        try
        {
            (await discover.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeEmpty();
            elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }
        finally
        {
            stop.Cancel();
            try { await sender; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public void Discovery_Without_Responses_Returns_Empty_List()
    {
        using var silent = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        MiioTransport.SendDiscoverMessage((IPEndPoint)silent.Client.LocalEndPoint, 30).Should().BeEmpty();
    }

    [Fact]
    public async Task Discovery_Can_Be_Canceled_And_Closes_Its_Socket()
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var cancellation = new CancellationTokenSource();
        var discover = MiioDiscovery.DiscoverAsync(
            (IPEndPoint)device.Client.LocalEndPoint, TimeSpan.FromSeconds(5), cancellation.Token);
        await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();

        Func<Task> wait = async () => await discover.WaitAsync(TimeSpan.FromSeconds(1));
        await wait.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Nonpositive_Discovery_Timeout_Is_Rejected(int milliseconds)
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        Func<Task> discover = () => MiioDiscovery.DiscoverAsync(
            (IPEndPoint)device.Client.LocalEndPoint, TimeSpan.FromMilliseconds(milliseconds));
        await discover.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task All_Valid_Responses_Are_Kept_When_Invalid_Packets_Are_Interleaved(bool invalidBetween)
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var discover = MiioDiscovery.DiscoverAsync(
            (IPEndPoint)device.Client.LocalEndPoint, TimeSpan.FromMilliseconds(150));
        var request = await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await device.SendAsync("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77".ToByteArray(), request.RemoteEndPoint);
        if (invalidBetween) await device.SendAsync(new byte[] { 1, 2 }, request.RemoteEndPoint);
        await device.SendAsync("213100200000000005c5a851002e58735d3a2f018c90097a850558c35c953b77".ToByteArray(), request.RemoteEndPoint);

        var results = await discover.WaitAsync(TimeSpan.FromSeconds(2));

        results.Should().Equal(
            new MiioDiscovery.DiscoveredDevice("127.0.0.1", "0404", "a850", "5d3a2f018c90097a850558c35c953b77"),
            new MiioDiscovery.DiscoveredDevice("127.0.0.1", "05c5", "a851", "5d3a2f018c90097a850558c35c953b77"));
    }

    [Fact]
    public async Task Consecutive_Discovery_Does_Not_Reuse_Previous_Results()
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var destination = (IPEndPoint)device.Client.LocalEndPoint;
        var first = MiioDiscovery.DiscoverAsync(destination, TimeSpan.FromMilliseconds(100));
        var request = await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await device.SendAsync("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77".ToByteArray(), request.RemoteEndPoint);
        (await first.WaitAsync(TimeSpan.FromSeconds(2))).Should().ContainSingle();

        var second = MiioDiscovery.DiscoverAsync(destination, TimeSpan.FromMilliseconds(50));
        await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        (await second.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_Hello_Responses_Are_Returned_Once()
    {
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var discover = MiioDiscovery.DiscoverAsync((IPEndPoint)device.Client.LocalEndPoint, TimeSpan.FromMilliseconds(100));
        var request = await device.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var hello = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77".ToByteArray();
        await device.SendAsync(hello, request.RemoteEndPoint);
        await device.SendAsync(hello, request.RemoteEndPoint);
        (await discover.WaitAsync(TimeSpan.FromSeconds(2))).Should().ContainSingle();
    }
}
