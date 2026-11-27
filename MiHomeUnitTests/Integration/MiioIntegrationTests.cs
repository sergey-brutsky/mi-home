using System;
using System.Threading.Tasks;
using MiHomeEmulator;
using MiHomeLib.MiioDevices.Internal;
using Xunit;

namespace MiHomeUnitTests.Integration;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MiioIntegrationTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    [Fact(Timeout = 30_000)]
    public async Task Safe_request_retries_but_non_idempotent_request_is_sent_once()
    {
        await using var emulator = new MiioEmulator(Token);
        using var transport = new MiioTransport("127.0.0.1", Token, emulator.Port)
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(50),
            ResponseTimeout = TimeSpan.FromMilliseconds(350),
        };

        emulator.DropNextRequests = 2;
        var response = await transport.RequestAsync("get_prop", new[] { "power" }, MiioRetryPolicy.SafeToRepeat);
        Assert.Contains("\"result\"", response);
        Assert.Equal(3, emulator.ReceivedRequests.Count);

        emulator.DropNextRequests = 1;
        var before = emulator.ReceivedRequests.Count;
        await Assert.ThrowsAsync<TimeoutException>(() =>
            transport.RequestAsync("set_properties", new[] { "power", "on" }, MiioRetryPolicy.DoNotRepeat));
        Assert.Equal(before + 1, emulator.ReceivedRequests.Count);
    }
}
