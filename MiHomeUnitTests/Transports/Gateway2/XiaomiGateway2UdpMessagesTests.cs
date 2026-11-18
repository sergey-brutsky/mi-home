using FluentAssertions;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Transports.Gateway2;

/// <summary>
/// The messages are the same the removed command classes used to build
/// </summary>
public class XiaomiGateway2UdpMessagesTests
{
    [Fact]
    public void Discover_Message() =>
        UdpTransport.DiscoverMessage().ToJsonString().Should().Be("{\"cmd\":\"get_id_list\"}");

    [Fact]
    public void Read_Message() =>
        UdpTransport.ReadMessage("158d0001826509").ToJsonString().Should().Be("{\"cmd\":\"read\",\"sid\":\"158d0001826509\"}");

    [Fact]
    public void Write_Message_Puts_Key_After_The_Values() =>
        UdpTransport.WriteMessage("158d0001826509", "plug", "ABCDEF", ("status", "on")).ToJsonString()
            .Should().Be("{\"cmd\":\"write\",\"model\":\"plug\",\"sid\":\"158d0001826509\",\"data\":{\"status\":\"on\",\"key\":\"ABCDEF\"}}");
}
