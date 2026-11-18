using MiHomeLib.Protocols;
using System.Collections.Generic;
using FluentAssertions;
using MiHomeLib;
using Xunit;

namespace MiHomeUnitTests.Devices.Gateway2;

public class DeviceDescriptionTests : Gateway2DeviceTestBase
{
    public static IEnumerable<object[]> Gateway2Models()
    {
        foreach (var model in ProtocolDeviceRegistry.Find(DeviceProtocol.XiaomiGateway2))
            yield return new object[] { model };
    }

    [Theory]
    [MemberData(nameof(Gateway2Models))]
    public void Description_Includes_Gateway2_Model_Between_Market_Model_And_DeviceId(string model)
    {
        const string deviceId = "158d000ad60071";
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.XiaomiGateway2, model, deviceId, 0,
            _loggerFactory, out var device).Should().BeTrue();
        var marketModel = device.GetType().GetField("MARKET_MODEL").GetRawConstantValue();

        device.ToString().Should().StartWith($"Device: {marketModel} {model} {deviceId}, ");
    }
}
