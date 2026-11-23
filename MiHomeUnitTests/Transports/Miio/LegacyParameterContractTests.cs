using System;
using FluentAssertions;
using MiHomeLib.MiioDevices;
using MiHomeUnitTests.Infrastructure;
using Xunit;

namespace MiHomeUnitTests.Transports.Miio;

public class LegacyParameterContractTests : MiioDeviceTestBase
{
    private enum Example { Valid = 1 }
    private sealed class Probe(IMiioTransport transport) : MiotGenericDevice("test", transport, 0)
    {
        public void Set(object value) => SetMiotProperty(2, 3, value);
        public void Action(object[] args) => CallMiotAction(2, 1, args);
    }

    [Fact]
    public void External_Subclasses_Can_Still_Send_Arbitrary_Objects()
    {
        SetupMiioInfo();
        SetupSetProperties();
        using var probe = new Probe(_miioTransport.Object);
        probe.Set(new { CustomValue = 7 });
        VerifyMethodWithId("set_properties", 2, new { did = "set-2-3", siid = 2, piid = 3, value = new { customValue = 7 } });
    }

    [Fact]
    public void External_Subclass_Property_Still_Rejects_An_Undefined_Enum()
    {
        SetupMiioInfo();
        using var probe = new Probe(_miioTransport.Object);
        Action set = () => probe.Set((Example)999);
        set.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void External_Subclass_Action_Preserves_Arbitrary_And_Null_Arguments(bool nullArguments)
    {
        SetupMiioInfo();
        SetupCallAction();
        using var probe = new Probe(_miioTransport.Object);
        object[] args = nullArguments ? null : [new { CustomValue = 7 }];
        probe.Action(args);
        var expected = new { id = 2, method = "action", @params = new { did = "call-2-1", siid = 2, aiid = 1, @in = args } };
        _miioTransport.Verify(x => x.SendMessageAsync(System.Text.Json.JsonSerializer.Serialize(expected,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })), Moq.Times.Once);
    }
}
