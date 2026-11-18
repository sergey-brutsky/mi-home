using MiHomeLib.MiioDevices.Internal;
using System.IO;
using FluentAssertions;
using Xunit;
using static MiHomeLib.MiioDevices.Internal.MiioTransport;


namespace MiHomeUnitTests.Transports.Miio;

public class MiioPacketTests
{
    [Theory]
    [InlineData("")]
    [InlineData("2131")]
    [InlineData("00310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77")]
    [InlineData("21310021000000000404a850002e58735d3a2f018c90097a850558c35c953b77")]
    [InlineData("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b7z")]
    public void Invalid_Packet_Header_Is_Rejected(string hex)
    {
        System.Action parse = () => new MiioPacket(hex);
        parse.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void MiioPacket_Returns_Valid_Props()
    {
        // Arrange
        // hello reponse for zhimi.humidifier.v1 with serial number a850
        var hex = "21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77";

        // Act
        var miioPacket = new MiioPacket(hex);

        // Assert
        miioPacket.GetDeviceType().Should().Be("0404");
        miioPacket.GetSerial().Should().Be("a850");
        miioPacket.GetChecksum().Should().Be("5d3a2f018c90097a850558c35c953b77");
    }

    [Fact]
    public void BuildMessage_Returns_Valid_Hex_String()
    {
        // Arrange
        var miioPacket = new MiioPacket("21310020000000000404a850002e58735d3a2f018c90097a850558c35c123b77");

        // Act
        var packet = miioPacket.BuildMessage("{\"id\": 1, \"method\": \"set_power\", \"params\": [\"on\"]}", "5d3a2f018c90097a850558c35c953b77");

        // Assert
        packet.Should().Be("21310060000000000404a850002e58734a31baf3a300c3bb4179fe1b910bd69d2fa405db0b664dc87c543b16c63c69e5429f4825298687cf6194c23fdb0fbed9ed7846dbd1dd4e258218e3f922b632c3ee62af7240530a0757d9d5ec8febcd7b");
    }

    [Fact]
    public void GetResponseData_Returns_Valid_Response()
    {
        // Arrange
        var miioPacket = new MiioPacket("21310040000000000404a850002e5b830afd260f87e33eed4788eb2c5f1b0237da93c178df54ec4a95ee3e952f5c8a73a0d9ad1a8fec934087977f497a8f7eb5");

        // Act
        var packet = miioPacket.GetResponseData("5d3a2f018c90097a850558c35c953b77");

        // Assert
        packet.Should().Be("{\"result\":[\"ok\"],\"id\":1}");
    }

    [Fact]
    public void GetResponseData_WithWrongToken_ExplainsHowToDiagnose()
    {
        var packet = new MiioPacket("21310040000000000404a850002e5b830afd260f87e33eed4788eb2c5f1b0237da93c178df54ec4a95ee3e952f5c8a73a0d9ad1a8fec934087977f497a8f7eb5");

        var act = () => packet.GetResponseData("00000000000000000000000000000000");

        act.Should()
            .Throw<InvalidDataException>()
            .WithMessage("*token may be incorrect*");
    }

    [Fact]
    public void GetResponseData_WithoutEncryptedPayload_ExplainsHowToDiagnose()
    {
        var packet = new MiioPacket("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77");

        var act = () => packet.GetResponseData("5d3a2f018c90097a850558c35c953b77");

        act.Should()
            .Throw<InvalidDataException>()
            .WithMessage("*no encrypted payload*");
    }

    [Fact]
    public void Stamp_Is_Read_From_The_Header()
    {
        new MiioPacket("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77").Stamp.Should().Be(0x002e5873);
    }

    [Fact]
    public void BuildMessage_Writes_The_Given_Stamp_And_Stays_Decryptable()
    {
        var hello = new MiioPacket("21310020000000000404a850002e58735d3a2f018c90097a850558c35c953b77");
        var cipher = new MiHomeLib.MiioDevices.Internal.MiioCipher("5d3a2f018c90097a850558c35c953b77");

        var request = new MiioPacket(hello.BuildMessage("{\"id\":1}", cipher, 0x002e5880));

        request.Stamp.Should().Be(0x002e5880u, "requests carry the device clock moved forward by the time passed since its last answer");
        request.GetResponseData(cipher).Should().Be("{\"id\":1}");
        request.GetDeviceType().Should().Be("0404");
        request.GetSerial().Should().Be("a850");
    }
}
