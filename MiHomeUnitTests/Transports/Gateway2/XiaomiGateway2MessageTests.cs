using System;
using System.Text.Json;
using FluentAssertions;
using MiHomeLib.XiaomiGateway2;
using Xunit;

namespace MiHomeUnitTests.Transports.Gateway2;

public class XiaomiGateway2MessageTests
{
    [Theory]
    // The type is internal, so the expected value is passed by name
    [InlineData("report", nameof(Gateway2MessageType.Report))]
    [InlineData("heartbeat", nameof(Gateway2MessageType.Heartbeat))]
    [InlineData("read_ack", nameof(Gateway2MessageType.ReadAck))]
    [InlineData("get_id_list_ack", nameof(Gateway2MessageType.GetIdListAck))]
    [InlineData("write_ack", "WriteAck")]
    [InlineData("aaa", nameof(Gateway2MessageType.Unknown))]
    public void Parses_Message_Type(string cmd, string expected)
    {
        var str = "{\"cmd\":\"" + cmd + "\",\"model\":\"sensor_ht\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}";

        var message = Gateway2Message.Parse(str);

        message.Type.ToString().Should().Be(expected);
        message.Command.Should().Be(cmd);
    }

    [Fact]
    public void Write_Ack_Without_Model_Preserves_Sid_And_Error_Data()
    {
        var message = Gateway2Message.Parse("""{"cmd":"write_ack","sid":"plug","data":"{\"error\":\"Invalid key\"}"}""");
        message.Type.ToString().Should().Be("WriteAck");
        message.Sid.Should().Be("plug");
        message.Model.Should().BeNull();
        message.Data.Should().Be("""{"error":"Invalid key"}""");
    }

    [Theory]
    [InlineData("{\"cmd\":\"report\",\"sid\":null,\"model\":\"plug\",\"data\":\"{}\"}")]
    [InlineData("{\"cmd\":\"report\",\"sid\":\"one\",\"data\":\"{}\"}")]
    [InlineData("{\"cmd\":\"read_ack\",\"sid\":\"one\",\"model\":\"plug\"}")]
    [InlineData("{\"cmd\":\"get_id_list_ack\",\"sid\":\"\",\"data\":\"[]\"}")]
    public void Missing_Required_Fields_Are_Rejected(string json)
    {
        Action parse = () => Gateway2Message.Parse(json);
        parse.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parses_Model_Sid_ShortId_And_Data()
    {
        var message = Gateway2Message.Parse(
            "{\"cmd\":\"report\",\"model\":\"plug\",\"sid\":\"158d0001826509\",\"short_id\":11109,\"data\":\"{\\\"temperature\\\":\\\"2246\\\"}\"}");

        message.Model.Should().Be("plug");
        message.Sid.Should().Be("158d0001826509");
        message.ShortId.Should().Be(11109);
        message.Data.Should().Be("{\"temperature\":\"2246\"}");
        message.Token.Should().BeNull();
    }

    [Fact]
    public void Gateway_Heartbeat_Has_Token_And_String_Short_Id()
    {
        var message = Gateway2Message.Parse(
            "{\"cmd\":\"heartbeat\",\"model\":\"gateway\",\"sid\":\"34ce0088db36\",\"short_id\":\"0\",\"token\":\"VPNvuBhwmeWHCbyG\",\"data\":\"{\\\"ip\\\":\\\"192.168.1.12\\\"}\"}");

        message.Token.Should().Be("VPNvuBhwmeWHCbyG");
        message.ShortId.Should().Be(0);
    }

    [Fact]
    public void Broken_Message_Throws_JsonException_With_The_Message()
    {
        var act = () => Gateway2Message.Parse("{\"cmd\":\"report\"}");

        act.Should().Throw<JsonException>().WithMessage("*{\"cmd\":\"report\"}*");
    }
}
