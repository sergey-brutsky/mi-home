using System;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class LegacyMiioResponseTests
{
    [Theory]
    [InlineData("{\"result\":[]}")]
    [InlineData("{\"result\":[\"on\"]}")]
    [InlineData("{\"result\":[\"on\",null]}")]
    [InlineData("{\"result\":[\"on\",45,100]}")]
    [InlineData("{\"result\":{}}")]
    [InlineData("{\"error\":{\"code\":-1}}")]
    [InlineData("not-json")]
    public async Task Invalid_Property_Responses_Identify_Requested_Properties_In_Both_APIs(string response)
    {
        var transport = new Mock<IMiioTransport>();
        transport.Setup(value => value.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response);
        using var device = new Probe(transport.Object);
        Action read = () => device.Read("power", "humidity");
        Func<Task> readAsync = () => device.ReadAsync("power", "humidity");

        read.Should().Throw<InvalidOperationException>().WithMessage("*power*humidity*");
        (await readAsync.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*power*humidity*");
        // The synchronous API wraps the asynchronous one, both send through SendMessageAsync
        transport.Verify(value => value.SendMessage(It.IsAny<string>()), Times.Never);
        transport.Verify(value => value.SendMessageAsync(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Valid_Property_Responses_Retain_Order_And_Value_Representation()
    {
        var transport = new Mock<IMiioTransport>();
        const string response = "{\"result\":[\"on\",45,true]}";
        transport.Setup(value => value.SendMessageAsync(It.IsAny<string>())).ReturnsAsync(response);
        using var device = new Probe(transport.Object);
        device.Read("power", "humidity", "enabled").Should().Equal("on", "45", "True");
        (await device.ReadAsync("power", "humidity", "enabled")).Should().Equal("on", "45", "True");
        device.Read().Should().HaveCount(3);
    }

    private sealed class Probe(IMiioTransport transport) : MiioDevice(transport)
    {
        public string[] Read(params string[] properties) => GetProps(properties);
        public Task<string[]> ReadAsync(params string[] properties) => GetPropsAsync(properties);
    }
}
