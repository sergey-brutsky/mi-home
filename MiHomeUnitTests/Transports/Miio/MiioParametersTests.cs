using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices.Internal;
using Xunit;

namespace MiHomeUnitTests.Transports.Miio;

public class MiioParametersTests
{
    private enum Example : long { Negative = -2 }
    private enum Large : ulong { Maximum = ulong.MaxValue }
    public static IEnumerable<object[]> Parameters()
    {
        object[] values = ["text", true, 42, -5L, uint.MaxValue, ushort.MaxValue, byte.MaxValue,
            (short)-10, 1.25f, 1.5d, 2.25m, Example.Negative, Large.Maximum,
            new object[] { 1, "two", false, null }, new Dictionary<string, object> { ["x"] = new object[] { "a", 2 } }];
        foreach (var value in values) yield return [value];
    }

    [Theory]
    [MemberData(nameof(Parameters))]
    public void Supported_Values_Preserve_The_Previous_Serializer_Json(object value) =>
        MiioParameters.ToNode(value).ToJsonString().Should().Be(JsonSerializer.Serialize(value, MiioJson.Options));

    [Fact]
    public void Null_And_Borrowed_Nodes_Have_Explicit_Semantics()
    {
        MiioParameters.ToNode(null).Should().BeNull();
        var node = JsonNode.Parse("{\"a\":1}");
        MiioParameters.ToNode(node).Should().BeSameAs(node);
        var first = MiioParameters.ToNode(new object[] { node, node });
        first.ToJsonString().Should().Be("[{\"a\":1},{\"a\":1}]");
        node.Parent.Should().BeNull();
    }

    [Fact]
    public void Unregistered_Object_Fails_Instead_Of_Falling_Back_To_Reflection()
    {
        Action convert = () => MiioParameters.ToNode(new { name = "not registered" });
        convert.Should().Throw<NotSupportedException>().WithMessage("miio parameter of type * is not supported");
    }

    [Fact]
    public void Registered_Models_Preserve_Wire_Spelling_And_Property_Order()
    {
        MiioParameters.ToNode(new MiotReadProperty("test", 2, 3)).ToJsonString()
            .Should().Be("{\"did\":\"test\",\"siid\":2,\"piid\":3}");
        MiioParameters.ToNode(new MiotWriteProperty("test", 2, 3, JsonValue.Create(true))).ToJsonString()
            .Should().Be("{\"did\":\"test\",\"siid\":2,\"piid\":3,\"value\":true}");
        JsonSerializer.Serialize(new RemoteControlPayload(1, 2, "false", 3), MiHomeJsonContext.Default.RemoteControlPayload)
            .Should().Be("{\"spdv\":1,\"spdw\":2,\"audio\":\"false\",\"random\":3}");
        JsonSerializer.Serialize(new SettingPayload("key", JsonValue.Create(1)), MiHomeJsonContext.Default.SettingPayload)
            .Should().Be("{\"k\":\"key\",\"v\":1}");
        JsonSerializer.Serialize(new UploadPayload("id", "url", "md5", 5), MiHomeJsonContext.Default.UploadPayload)
            .Should().Be("{\"id\":\"id\",\"url\":\"url\",\"md5\":\"md5\",\"size\":5}");
    }
}
