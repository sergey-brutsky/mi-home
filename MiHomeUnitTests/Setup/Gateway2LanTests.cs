using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.MiioDevices.Internal;
using MiHomeSetup;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Setup;

public class Gateway2LanTests
{
    private const string Key = "abcdefghijklmnop";

    [Fact]
    public async Task Empty_Key_Is_Replaced_And_Verified_Without_Disclosing_It()
    {
        var transport = CreateTransport("");
        using var output = new StringWriter();
        await Gateway2Lan.EnsureEnabledAsync(transport.Object, null, CancellationToken.None, output);
        var write = Assert.Single(transport.Invocations.Where(x => x.Method.Name == "RequestAsync" && (string)x.Arguments[0] == "set_lumi_dpf_aes_key"));
        var generated = Assert.Single((string[])write.Arguments[1]);
        Assert.Equal(16, generated.Length);
        Assert.All(generated, c => Assert.True(char.IsAsciiLetterOrDigit(c)));
        Assert.Equal(MiioRetryPolicy.DoNotRepeat, write.Arguments[2]);
        Assert.Equal(3, transport.Invocations.Count);
        Assert.DoesNotContain(generated, output.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Key)]
    public async Task Existing_Key_Is_Preserved(string requested)
    {
        var transport = CreateTransport(Key);
        await Gateway2Lan.EnsureEnabledAsync(transport.Object, requested, CancellationToken.None);
        Assert.Single(transport.Invocations);
    }

    [Fact]
    public async Task Explicit_Key_Replaces_The_Old_Key()
    {
        var transport = CreateTransport("0123456789abcdef");
        await Gateway2Lan.EnsureEnabledAsync(transport.Object, Key, CancellationToken.None);
        transport.Verify(x => x.RequestAsync("set_lumi_dpf_aes_key", It.Is<string[]>(p => p.SequenceEqual(new[] { Key })), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Disable_Sends_One_Empty_String_And_Verifies_It()
    {
        var transport = CreateTransport(Key);
        await Gateway2Lan.DisableAsync(transport.Object, CancellationToken.None);
        transport.Verify(x => x.RequestAsync("set_lumi_dpf_aes_key", It.Is<string[]>(p => p.Length == 1 && p[0] == ""), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, transport.Invocations.Count);
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":-1}}")]
    [InlineData("{\"result\":[]}")]
    [InlineData("{\"result\":[null]}")]
    [InlineData("{\"result\":[\"\",\"\"]}")]
    public async Task Unknown_Read_State_Prevents_A_Write(string response)
    {
        var transport = CreateTransport("");
        transport.Setup(x => x.RequestAsync("get_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => Gateway2Lan.EnsureEnabledAsync(transport.Object, null, CancellationToken.None));
        Assert.Single(transport.Invocations);
    }

    [Fact]
    public async Task Lost_Write_Acknowledgement_Is_Verified_Without_Repeating_The_Write()
    {
        var transport = CreateTransport("");
        transport.Setup(x => x.RequestAsync("set_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        transport.SetupSequence(x => x.RequestAsync("get_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"result\":[\"\"]}").ReturnsAsync(JsonSerializer.Serialize(new { result = new[] { Key } }));
        await Gateway2Lan.EnsureEnabledAsync(transport.Object, Key, CancellationToken.None);
        transport.Verify(x => x.RequestAsync("set_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Readback_Mismatch_Is_A_Failure()
    {
        var transport = CreateTransport(Key);
        transport.Setup(x => x.RequestAsync("set_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>())).ReturnsAsync("{\"result\":[\"ok\"]}");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Gateway2Lan.DisableAsync(transport.Object, CancellationToken.None,
            verificationDelay: TimeSpan.Zero));
        Assert.Contains("5 readback checks", error.Message);
        Assert.Contains("returned string length: 16", error.Message);
        Assert.Contains("disable could not be confirmed", error.Message);
        Assert.DoesNotContain(Key, error.Message);
        transport.Verify(x => x.RequestAsync("set_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delayed_Empty_Readback_Is_Verified_Without_Repeating_The_Write()
    {
        var transport = CreateTransport(Key);
        transport.SetupSequence(x => x.RequestAsync("get_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new { result = new[] { Key } }))
            .ReturnsAsync("{\"result\":[\"\"]}");
        await Gateway2Lan.DisableAsync(transport.Object, CancellationToken.None, verificationDelay: TimeSpan.Zero);
        transport.Verify(x => x.RequestAsync("set_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>()), Times.Once);
        transport.Verify(x => x.RequestAsync("get_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Rejected_Write_Is_A_Failure()
    {
        var transport = CreateTransport(Key);
        transport.Setup(x => x.RequestAsync("set_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>())).ReturnsAsync("{\"error\":{\"code\":-1}}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Gateway2Lan.DisableAsync(transport.Object, CancellationToken.None));
        Assert.Single(transport.Invocations);
    }

    [Fact]
    public async Task Invalid_Key_And_Cancellation_Are_Rejected_Before_Sending()
    {
        var transport = CreateTransport("");
        await Assert.ThrowsAsync<ArgumentException>(() => Gateway2Lan.EnsureEnabledAsync(transport.Object, "short", CancellationToken.None));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Gateway2Lan.DisableAsync(transport.Object, new CancellationToken(true)));
        Assert.Empty(transport.Invocations);
    }

    private static Mock<IMiioRequests> CreateTransport(string key)
    {
        var transport = new Mock<IMiioRequests>(MockBehavior.Strict);
        transport.Setup(x => x.RequestAsync("get_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.SafeToRepeat, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => JsonSerializer.Serialize(new { result = new[] { key } }));
        transport.Setup(x => x.RequestAsync("set_lumi_dpf_aes_key", It.IsAny<object>(), MiioRetryPolicy.DoNotRepeat, It.IsAny<CancellationToken>()))
            .Callback<string, object, MiioRetryPolicy, CancellationToken>((_, parameters, _, _) => key = ((string[])parameters)[0])
            .ReturnsAsync("{\"result\":[\"ok\"]}");
        return transport;
    }
}
