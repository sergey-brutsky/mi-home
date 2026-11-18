using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using MiHomeLib.Protocols;
using Xunit;

namespace MiHomeUnitTests.Protocols;

public class ProtocolDeviceMapTests
{
    [Fact]
    public void Named_Initialization_Updates_Only_The_Returned_Property()
    {
        var map = new ProtocolDeviceMap();
        var first = 10;
        var second = 20;
        map.LumiMqttInitial("first", value => first = value.GetValue<int>());
        map.LumiMqttInitial("second", value => second = value.GetValue<int>());
        map.SetInitialProperty("second", JsonValue.Create(30));
        first.Should().Be(10);
        second.Should().Be(30);
        Action unsupported = () => map.SetInitialProperty("missing", JsonValue.Create(40));
        unsupported.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_Resource_Key_Is_Rejected(string key)
    {
        var map = new ProtocolDeviceMap();
        Action register = () => map.LumiMqtt(key, _ => { });
        register.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Empty_Initial_Property_Name_Is_Rejected(string name)
    {
        var map = new ProtocolDeviceMap();
        Action register = () => map.LumiMqttInitial(name, _ => { });
        register.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("propertyName");
        map.GetInitialPropertyNames().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Duplicate_Initial_Name_Is_Rejected_Without_Adding_A_Partial_Mapping(bool firstInitialOnly, bool secondInitialOnly)
    {
        var map = new ProtocolDeviceMap();
        if (firstInitialOnly) map.LumiMqttInitial("battery", _ => { });
        else map.LumiMqtt("first", "battery", _ => { }, _ => { });
        Action duplicate = () =>
        {
            if (secondInitialOnly) map.LumiMqttInitial("battery", _ => { });
            else map.LumiMqtt("second", "battery", _ => { }, _ => { });
        };

        duplicate.Should().ThrowExactly<InvalidOperationException>().WithMessage("*battery*");

        map.GetInitialPropertyNames().Should().Equal("battery");
        using var value = JsonDocument.Parse("75");
        map.Apply(DeviceProtocol.LumiMqtt, "second", value.RootElement).Should().BeFalse();
    }

    [Fact]
    public void Null_Initial_Values_Identify_The_Argument()
    {
        var map = new ProtocolDeviceMap();
        Action initialize = () => map.SetInitialProperties(null);
        initialize.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("values");
    }

    [Fact]
    public void Wrong_Count_Does_Not_Initialize_Any_Property_And_Valid_Values_Keep_Order()
    {
        var map = new ProtocolDeviceMap();
        var received = new System.Collections.Generic.List<int>();
        map.LumiMqttInitial("first", value => received.Add(value.GetValue<int>()));
        map.LumiMqttInitial("second", value => received.Add(value.GetValue<int>()));
        Action initialize = () => map.SetInitialProperties([JsonValue.Create(1)]);
        initialize.Should().Throw<ArgumentException>();
        received.Should().BeEmpty();
        map.SetInitialProperties([JsonValue.Create(1), JsonValue.Create(2)]);
        received.Should().Equal(1, 2);
    }
}
