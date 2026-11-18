using MiHomeUnitTests.Devices.Mqtt;
using MiHomeUnitTests.Infrastructure;
using MiHomeLib.MqttGateway.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib;
using MiHomeLib.MqttGateway;
using XiaomiSmartWirelessSwitch = MiHomeLib.Devices.XiaomiSmartWirelessSwitch;
using Moq;
using Xunit;

namespace MiHomeUnitTests.Helpers;

// The default factory is process-wide, these tests must not run in parallel with each other
[CollectionDefinition(nameof(LoggingTests), DisableParallelization = true)]
public class LoggingTestsCollection { }

[Collection(nameof(LoggingTests))]
public class LoggingTests : MqttDeviceTestBase
{
    private MultimodeGateway CreateGateway(ILoggerFactory loggerFactory)
    {
        _devicesDiscoverer.Setup(x => x.DiscoverZigBeeDevices()).Returns([new ZigBeeDeviceInfo("switch-did", XiaomiSmartWirelessSwitch.LUMI_MQTT_MODEL)]);
        _devicesDiscoverer.Setup(x => x.DiscoverBleDevices()).Returns([]);
        _devicesDiscoverer.Setup(x => x.DiscoverMeshDevices()).Returns([]);
        _miioTransport
            .Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("get_device_prop"))))
            .ReturnsAsync("{\"code\":0,\"result\":[3032,100,192,88]}");

        return new MultimodeGateway("gw-did", _miioTransport.Object, _mqttTransport.Object, _devicesDiscoverer.Object, loggerFactory);
    }

    private void RaiseUnknownDidReport() =>
        _mqttTransport.Raise(x => x.OnMessageReceived += null, "zigbee/send",
            "{\"cmd\":\"report\",\"did\":\"unknown-did\",\"time\":1,\"params\":[]}");

    [Fact]
    public void Gateway_Factory_Is_Used_By_Gateway_Processors_And_Devices()
    {
        using var gw = CreateGateway(_loggerFactory);
        gw.DiscoverDevices();

        gw.GetDeviceByDid<XiaomiSmartWirelessSwitch>("switch-did").ParseData("[{\"res_name\":\"unknown.res\",\"value\":1}]");
        RaiseUnknownDidReport();

        var categories = _loggerFactory.Entries.Select(x => x.Category).Distinct().ToList();
        categories.Should().Contain(typeof(MultimodeGateway).FullName, "the gateway logs under its own type");
        categories.Should().Contain(typeof(XiaomiSmartWirelessSwitch).FullName, "devices log under their own type");
        categories.Should().Contain(typeof(MiHomeLib.MqttGateway.ActionProcessors.ZigbeeReportCommandProcessor).FullName);
    }

    [Fact]
    public void Incoming_Mqtt_Message_Is_Logged_At_Debug()
    {
        using var gw = CreateGateway(_loggerFactory);
        gw.DiscoverDevices();

        RaiseUnknownDidReport();

        _loggerFactory.Entries
            .Where(x => x.Category == typeof(MultimodeGateway).FullName && x.Message.StartsWith("zigbee/send -->"))
            .Should().ContainSingle().Which.Level.Should().Be(LogLevel.Debug);
    }

    [Fact]
    public void Log_Messages_Are_Templates_With_Values_Filled_In()
    {
        using var gw = CreateGateway(_loggerFactory);
        gw.DiscoverDevices();

        RaiseUnknownDidReport();

        var entry = _loggerFactory.Entries.Should().ContainSingle(x =>
            x.Level == LogLevel.Warning && x.Message == "Did 'unknown-did' is unknown. Cannot process 'report' command for this device.").Which;
        entry.OriginalFormat.Should().Be("Did '{Did}' is unknown. Cannot process '{Action}' command for this device.");
        entry.Properties.Should().Contain(new KeyValuePair<string, object>("Did", "unknown-did"))
            .And.Contain(new KeyValuePair<string, object>("Action", "report"));
    }

    [Fact]
    public void Default_Factory_Applies_To_Gateways_Created_Afterwards_Only()
    {
        using var gatewayBefore = CreateGateway(null);
        using var global = new ErrorRecordingLoggerFactory();
        var previousFactory = MiHomeLogging.Default;

        try
        {
            MiHomeLogging.SetLoggerFactory(global);
            using var gatewayAfter = CreateGateway(null);

            gatewayAfter.DiscoverDevices();
            global.Entries.Should().Contain(x => x.Category == typeof(MultimodeGateway).FullName);

            var countAfterFirst = global.Entries.Count;
            gatewayBefore.DiscoverDevices();
            global.Entries.Should().HaveCount(countAfterFirst, "a gateway keeps the factory it was created with");
        }
        finally
        {
            MiHomeLogging.SetLoggerFactory(previousFactory);
        }
    }

    [Fact]
    public void SetLoggerFactory_Rejects_Null()
    {
        var act = () => MiHomeLogging.SetLoggerFactory(null);

        act.Should().Throw<ArgumentNullException>();
    }
}
