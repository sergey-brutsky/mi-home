using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using MiHomeLib.MqttGateway;
using MiHomeUnitTests.Devices.Mqtt;
using MiHomeUnitTests.Helpers;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Gateways.Mqtt;

[Collection(nameof(LoggingTests))]
public class MqttGatewayAsyncApiTests : MqttDeviceTestBase
{
    private const string Did = "gateway-api";

    public static IEnumerable<object[]> Reads =>
    [
        [1, "NightModeEnabled", 6, 1, 1, true],
        [1, "NightModeEffectiveHours", 6, 2, "23:05-08:10", ((byte)23, (byte)5, (byte)8, (byte)10)],
        [1, "HotspotModeEnabled", 19, 1, true, true],
        [1, "HotspotName", 19, 2, "ssid", "ssid"],
        [1, "HotspotPassword", 19, 3, "password", "password"],
        [1, "HotspotClients", 19, 4, "[]", Array.Empty<(string, string, string)>()],
        [2, "LedEnabled", 6, 6, 1, true],
        [2, "ArmingMode", 3, 1, 3, MultimodeGateway.ArmingModeValue.Sleep],
        [2, "AlarmState", 3, 22, 2, MultimodeGateway.AlarmStateValue.NonSecurityAlarm],
        [3, "AccessMode", 2, 1, 2, MultimodeGateway2Base.AccessModeValue.Wireless5G],
        [3, "DoNotDisturbModeEnabled", 6, 1, 1, true],
        [3, "DoNotDisturbEffectiveTime", 6, 2, "23:05-08:10", ((ushort)23, (ushort)5, (ushort)8, (ushort)10)],
        [3, "LedIndicatorBrightness", 6, 3, 75, (ushort)75],
        [2, "AccidentalDeletionEnabled", 7, 1, 1, true]
    ];

    public static IEnumerable<object[]> Writes =>
    [
        [1, "NightModeEnabled", 6, 1, false, 0],
        [1, "NightModeEffectiveHours", 6, 2, ((byte)23, (byte)5, (byte)8, (byte)10), "23:05-08:10"],
        [1, "HotspotModeEnabled", 19, 1, true, true],
        [1, "HotspotName", 19, 2, "ssid", "ssid"],
        [1, "HotspotPassword", 19, 3, "password", "password"],
        [2, "LedEnabled", 6, 6, true, 1],
        [2, "ArmingMode", 3, 1, MultimodeGateway.ArmingModeValue.Sleep, MultimodeGateway.ArmingModeValue.Sleep],
        [2, "AlarmState", 3, 22, MultimodeGateway.AlarmStateValue.NoAlarm, MultimodeGateway.AlarmStateValue.NoAlarm],
        [3, "AccessMode", 2, 1, MultimodeGateway2Base.AccessModeValue.LAN, MultimodeGateway2Base.AccessModeValue.LAN],
        [3, "DoNotDisturbModeEnabled", 6, 1, false, 0],
        [3, "DoNotDisturbEffectiveTime", 6, 2, ((ushort)23, (ushort)5, (ushort)8, (ushort)10), "23:05-08:10"],
        [3, "LedIndicatorBrightness", 6, 3, (ushort)75, (ushort)75],
        [2, "AccidentalDeletionEnabled", 7, 1, false, 0]
    ];

    [Theory]
    [MemberData(nameof(Reads))]
    public async Task Async_Getters_Use_The_Expected_Property(int kind, string name, int siid, int piid, object value, object expected)
    {
        SetupGetProperties((siid, piid, Did, value));
        using var gateway = CreateGateway(kind);
        var task = (Task)gateway.GetType().GetMethod("Get" + name + "Async").Invoke(gateway, null);
        await task;
        task.GetType().GetProperty("Result").GetValue(task).Should().BeEquivalentTo(expected);
        VerifyGetProperties(siid, piid, Did);
    }

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task Async_Setters_Use_The_Expected_Property(int kind, string name, int siid, int piid, object value, object expected)
    {
        SetupSetProperties();
        using var gateway = CreateGateway(kind);
        await (Task)gateway.GetType().GetMethod("Set" + name + "Async").Invoke(gateway, [value]);
        VerifySetProperties(siid, piid, Did, expected);
    }

    [Theory]
    [InlineData(typeof(MqttGatewayBase))]
    [InlineData(typeof(AqaraHubE1China))]
    [InlineData(typeof(MultimodeGateway))]
    [InlineData(typeof(MultimodeGateway2Base))]
    public void Network_Operations_Have_Async_Pairs_And_No_Network_Properties(Type type)
    {
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Should().BeEmpty();
        var localReads = new[] { "GetDevices", "GetDeviceById", "GetDeviceByDid" };
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => (method.Name.StartsWith("Get") || method.Name.StartsWith("Set")) &&
                !method.Name.EndsWith("Async") && !localReads.Contains(method.Name)))
        {
            var asyncMethod = type.GetMethod(method.Name + "Async", method.GetParameters().Select(p => p.ParameterType).ToArray());
            asyncMethod.Should().NotBeNull();
            asyncMethod.ReturnType.Should().Be(method.ReturnType == typeof(void)
                ? typeof(Task) : typeof(Task<>).MakeGenericType(method.ReturnType));
        }
    }

    [Fact]
    public async Task Async_Setter_Waits_For_Response_And_Propagates_Timeout()
    {
        using var gateway = CreateGateway(1);
        var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _miioTransport.Setup(transport => transport.SendMessageAsync(It.Is<string>(message => message.Contains("set_properties"))))
            .Returns(response.Task);
        var pending = ((AqaraHubE1China)gateway).SetHotspotNameAsync("ssid");
        pending.IsCompleted.Should().BeFalse();
        response.SetException(new TimeoutException("gateway timeout"));
        Func<Task> finish = () => pending;
        await finish.Should().ThrowAsync<TimeoutException>();
    }

    private MqttGatewayBase CreateGateway(int kind) => kind switch
    {
        1 => new AqaraHubE1China(Did, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object, _loggerFactory),
        2 => new MultimodeGateway(Did, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object, _loggerFactory),
        _ => new Mock<MultimodeGateway2Base>(Did, _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object) { CallBase = true }.Object
    };
}
