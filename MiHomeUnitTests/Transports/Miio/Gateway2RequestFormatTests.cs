using System.Text.Json.Nodes;
using FluentAssertions;
using MiHomeLib.MiioDevices.Internal;
using Xunit;

namespace MiHomeUnitTests.Transports.Miio;

public class Gateway2RequestFormatTests
{
    [Fact]
    public void Gateway2_Request_Ids_Start_At_One_And_Wrap_Below_9999()
    {
        var counter = 0;
        MiioTransport.NextGateway2RequestId(ref counter).Should().Be(1);
        MiioTransport.NextGateway2RequestId(ref counter).Should().Be(2);
        counter = 9997;
        MiioTransport.NextGateway2RequestId(ref counter).Should().Be(9998);
        MiioTransport.NextGateway2RequestId(ref counter).Should().Be(1);
    }

    [Theory]
    [InlineData("{\"id\":1,\"method\":\"miIO.info\",\"params\":[]}", "{\"id\": 1, \"method\": \"miIO.info\", \"params\": []}")]
    [InlineData("{\"id\":2,\"method\":\"set_lumi_dpf_aes_key\",\"params\":[\"\"]}", "{\"id\": 2, \"method\": \"set_lumi_dpf_aes_key\", \"params\": [\"\"]}")]
    [InlineData("{\"id\":3,\"method\":\"get_lumi_dpf_aes_key\",\"params\":[]}", "{\"id\": 3, \"method\": \"get_lumi_dpf_aes_key\", \"params\": []}")]
    public void Gateway2_Uses_The_Reference_Miiocli_Json_Format(string compact, string expected)
    {
        MiioTransport.FormatGateway2Request(JsonNode.Parse(compact)).Should().Be(expected);
    }

    [Fact]
    public void Formatting_Preserves_Whitespace_And_Punctuation_Inside_Values()
    {
        var request = new JsonObject { ["id"] = 2, ["method"] = "test",
            ["params"] = new JsonArray("a, b: [c]", "a\"b\\c", null, 5, true) };
        var formatted = MiioTransport.FormatGateway2Request(request);
        JsonNode.DeepEquals(request, JsonNode.Parse(formatted)).Should().BeTrue();
    }
}
