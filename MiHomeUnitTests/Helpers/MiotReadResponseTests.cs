using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using MiHomeUnitTests.Infrastructure;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Helpers;

public class MiotReadResponseTests : MiioDeviceTestBase
{
    public MiotReadResponseTests() => SetupMiioInfo();

    private static readonly string[] InvalidResponses =
    [
        "not-json", "null", "[]", "{}", "{\"error\":{\"code\":-1}}",
        "{\"result\":null}", "{\"result\":{}}", "{\"result\":[]}",
        "{\"result\":[null,17,{}]}",
        "{\"result\":[{\"did\":17,\"code\":0,\"value\":true}]}",
        "{\"result\":[{\"did\":\"other\",\"code\":0,\"value\":true}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":0,\"value\":true},{\"did\":\"probe\",\"code\":0,\"value\":false}]}",
        "{\"result\":[{\"did\":\"probe\",\"value\":true}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":null,\"value\":true}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":\"0\",\"value\":true}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":0.5,\"value\":true}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":2147483648,\"value\":true}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":-4003,\"value\":true}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":0}]}",
        "{\"result\":[{\"did\":\"probe\",\"code\":0,\"value\":null}]}"
    ];

    public static IEnumerable<object[]> InvalidCases =>
        from response in InvalidResponses
        from isAsync in new[] { false, true }
        select new object[] { isAsync, response };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task Invalid_Result_Identifies_Requested_Property_And_Is_Not_Retried(bool isAsync, string response)
    {
        using var device = new Probe(_miioTransport.Object);
        SetupReadResponse(response);
        Func<Task> read = () => Call(isAsync, device.ReadOne, device.ReadOneAsync);

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2/1*probe*");

        VerifyGetProperties(2, 1, "probe");
    }

    public static IEnumerable<object[]> InvalidIdentifierCases =>
        from identifier in new[] { "siid", "piid" }
        from raw in new[] { "null", "\"2\"", "true", "{}", "2.5", "2147483648", "99" }
        from isAsync in new[] { false, true }
        select new object[] { isAsync, identifier, raw };

    [Theory]
    [MemberData(nameof(InvalidIdentifierCases))]
    public async Task Invalid_Or_Mismatched_Identifier_Cannot_Match_The_Requested_Property(bool isAsync, string identifier, string raw)
    {
        using var device = new Probe(_miioTransport.Object);
        SetupReadResponse($"{{\"result\":[{{\"did\":\"probe\",\"{identifier}\":{raw},\"code\":0,\"value\":true}}]}}");
        Func<Task> read = () => Call(isAsync, device.ReadOne, device.ReadOneAsync);

        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2/1*probe*matching result*");

        VerifyGetProperties(2, 1, "probe");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Results_Sharing_Did_Are_Matched_By_Service_And_Property_In_Request_Order(bool isAsync)
    {
        using var device = new Probe(_miioTransport.Object);
        SetupReadResponse("""
            {"result":[
              null, 17, {}, {"did":null}, {"did":17},
              {"did":"other","siid":2,"piid":1,"code":0,"value":"wrong device"},
              {"did":"shared","siid":3,"piid":2,"code":0,"value":42},
              {"did":"shared","siid":2,"piid":99,"code":0,"value":"wrong property"},
              {"did":"shared","siid":99,"piid":1,"code":0,"value":"wrong service"},
              {"did":"shared","siid":2,"piid":1,"code":0,"value":"on"}
            ]}
            """);

        var values = await Call(isAsync, device.ReadShared, device.ReadSharedAsync);

        values.Should().Equal("on", "42");
        VerifyGetProperties(2, (2, 1, "shared"), (3, 2, "shared"));
    }

    [Theory]
    [InlineData(false, "", "false", "False")]
    [InlineData(true, "", "false", "False")]
    [InlineData(false, ",\"siid\":2", "0", "0")]
    [InlineData(true, ",\"siid\":2", "0", "0")]
    [InlineData(false, ",\"piid\":1", "\"\"", "")]
    [InlineData(true, ",\"piid\":1", "\"\"", "")]
    public async Task Optional_Identifiers_And_Falsy_Values_Are_Accepted(bool isAsync, string identifiers, string rawValue, string expected)
    {
        using var device = new Probe(_miioTransport.Object);
        SetupReadResponse($"{{\"result\":[{{\"did\":\"probe\"{identifiers},\"code\":0,\"value\":{rawValue}}}]}}");

        (await Call(isAsync, device.ReadOne, device.ReadOneAsync)).Should().Equal(expected);
        VerifyGetProperties(2, 1, "probe");
    }

    private void SetupReadResponse(string response) => _miioTransport
        .Setup(transport => transport.SendMessageAsync(It.Is<string>(message =>
            System.Text.Json.Nodes.JsonNode.Parse(message)["method"].GetValue<string>() == "get_properties")))
        .ReturnsAsync(response);

    private sealed class Probe(IMiioTransport transport) : MiotGenericDevice("test", transport, 0)
    {
        public string[] ReadOne() => GetMiotProperties([(2, 1, "probe")]);
        public Task<string[]> ReadOneAsync() => GetMiotPropertiesAsync([(2, 1, "probe")]);
        public string[] ReadShared() => GetMiotProperties([(2, 1, "shared"), (3, 2, "shared")]);
        public Task<string[]> ReadSharedAsync() => GetMiotPropertiesAsync([(2, 1, "shared"), (3, 2, "shared")]);
    }
}
