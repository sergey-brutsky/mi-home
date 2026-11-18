using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib;
using Xunit;
using System.Linq;
using Moq;

namespace MiHomeUnitTests.Protocols;

public class ProtocolDeviceRegistryTests
{
    [Theory]
    [InlineData("LumiMqtt", false)]
    [InlineData("LumiMqtt", true)]
    [InlineData("LumiBle", false)]
    [InlineData("LumiBle", true)]
    [InlineData("LumiMesh", false)]
    [InlineData("LumiMesh", true)]
    public void Initialization_Failure_Releases_The_Device_And_Preserves_The_Original_Error(string protocolName, bool cleanupFails)
    {
        using var logs = new CleanupFactory();
        var cleanupFailure = new InvalidOperationException("cleanup failed");
        if (cleanupFails) logs.Resource.Setup(value => value.Dispose()).Throws(cleanupFailure);
        var protocol = Enum.Parse<DeviceProtocol>(protocolName);
        var registration = ProtocolDeviceRegistry.ValidateRegistrations([typeof(InitializationFailureDevice)])
            .Single(value => value.Metadata.Protocol == protocol);

        Action create = () => registration.Create("test", 0, logs);

        var error = create.Should().ThrowExactly<InvalidOperationException>().Which;
        error.Should().BeSameAs(logs.InitializationFailure);
        logs.Resource.Verify(value => value.Dispose(), Times.Once);
        if (cleanupFails) error.Data["DeviceCleanupException"].Should().BeSameAs(cleanupFailure);
    }

    [Fact]
    public void Successful_Initialization_Transfers_Resource_Ownership_To_The_Caller()
    {
        using var logs = new CleanupFactory { FailInitialization = false };
        var registration = ProtocolDeviceRegistry.ValidateRegistrations([typeof(InitializationFailureDevice)])
            .Single(value => value.Metadata.Protocol == DeviceProtocol.LumiMqtt);
        using (var device = registration.Create("test", 0, logs))
        {
            logs.Resource.Verify(value => value.Dispose(), Times.Never);
            ((ProtocolMappedDeviceBase)device).Model.Should().Be("initialization-test");
        }
        logs.Resource.Verify(value => value.Dispose(), Times.Once);
    }

    private sealed class CleanupFactory : ILoggerFactory
    {
        public Mock<IDisposable> Resource { get; } = new();
        public InvalidOperationException InitializationFailure { get; } = new("map configuration failed");
        public bool FailInitialization { get; set; } = true;
        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    [ProtocolDeviceModel(DeviceProtocol.LumiMqtt, "initialization-test")]
    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "initialization-test-ble", 99001)]
    [ProtocolDeviceModel(DeviceProtocol.LumiMesh, "initialization-test-mesh", 99002)]
    public sealed class InitializationFailureDevice : ProtocolMappedDeviceBase
    {
        private readonly CleanupFactory _factory;
        public InitializationFailureDevice(string id, ILoggerFactory factory) : base(factory) => _factory = (CleanupFactory)factory;
        public InitializationFailureDevice(string id, int shortId, ILoggerFactory factory) : this(id, factory) { }
        public override string DeviceId => "test";
        private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
        {
            if (_factory.FailInitialization) throw _factory.InitializationFailure;
        }
        public override void Dispose()
        {
            base.Dispose();
            _factory.Resource.Object.Dispose();
        }
    }

    [Theory]
    [InlineData("LumiBle", "miaomiaoce.sensor_ht", typeof(XiaomiBluetoothHygrothermograph2))]
    [InlineData("LumiMesh", "zimi.switch.dhkg01", typeof(XiaomiMeshWallSwitchSingleOpen))]
    public void Common_Factory_Creates_Ble_And_Mesh_By_Model(string protocolName, string model, Type expectedType)
    {
        var protocol = Enum.Parse<DeviceProtocol>(protocolName);
        ProtocolDeviceRegistry.TryCreate(protocol, model, "test", 0, NullLoggerFactory.Instance, out var device)
            .Should().BeTrue();
        using (device)
        {
            device.Should().BeOfType(expectedType);
            ((ProtocolMappedDeviceBase)device).Model.Should().Be(model);
            ((ProtocolMappedDeviceBase)device).IsInitializedFor(protocol).Should().BeTrue();
        }
        ProtocolDeviceRegistry.TryCreate(protocol, "unsupported", "test", 0, NullLoggerFactory.Instance, out var missing)
            .Should().BeFalse();
        missing.Should().BeNull();
    }

    private static readonly InvalidOperationException ConstructorFailure = new("device constructor failed");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Compiled_Factory_Preserves_The_Constructor_Exception(bool ble)
    {
        var type = ble ? typeof(ThrowingBleDevice) : typeof(ThrowingZigbeeDevice);
        var registration = ProtocolDeviceRegistry.ValidateRegistrations([type])[0];
        Action create = () =>
        {
            if (ble) registration.CreateByPdid("test", NullLoggerFactory.Instance);
            else registration.CreateByModel("test", 0, NullLoggerFactory.Instance);
        };
        create.Should().ThrowExactly<InvalidOperationException>().Which.Should().BeSameAs(ConstructorFailure);
        Action initialize = () => registration.Create("test", 0, NullLoggerFactory.Instance);
        initialize.Should().ThrowExactly<InvalidOperationException>().Which.Should().BeSameAs(ConstructorFailure);
    }

    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "throwing-ble", 997)]
    public sealed class ThrowingBleDevice : ProtocolMappedDeviceBase
    {
        public ThrowingBleDevice(string id, ILoggerFactory logs) : base(logs) => throw ConstructorFailure;
        public override string DeviceId => "test";
        private protected override void ConfigureProtocolMap(ProtocolDeviceMap map) { }
    }

    [ProtocolDeviceModel(DeviceProtocol.LumiMqtt, "throwing-zigbee")]
    public sealed class ThrowingZigbeeDevice : ProtocolMappedDeviceBase
    {
        public ThrowingZigbeeDevice(string id, int shortId, ILoggerFactory logs) : base(logs) => throw ConstructorFailure;
        public override string DeviceId => "test";
        private protected override void ConfigureProtocolMap(ProtocolDeviceMap map) { }
    }

    [Fact]
    public void Support_Check_Uses_The_Selected_Protocol()
    {
        ProtocolDeviceRegistry.IsSupported(DeviceProtocol.XiaomiGateway2, AqaraCubeSensor.XIAOMI_GATEWAY2_MODEL).Should().BeTrue();
        ProtocolDeviceRegistry.IsSupported(DeviceProtocol.LumiMqtt, AqaraCubeSensor.XIAOMI_GATEWAY2_MODEL).Should().BeFalse();
        ProtocolDeviceRegistry.IsSupported(DeviceProtocol.XiaomiGateway2, "unsupported").Should().BeFalse();
        ProtocolDeviceRegistry.IsSupported(DeviceProtocol.XiaomiGateway2, null).Should().BeFalse();
    }
    [Theory]
    [InlineData("lumi.sensor_cube")]
    [InlineData("lumi.sensor_cube.aqgl01")]
    public void Registered_Mqtt_Model_Is_Preserved_In_The_Device_And_Description(string model)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model, "cube", 0,
            NullLoggerFactory.Instance, out var device).Should().BeTrue();
        using (device)
        {
            device.ParseData("[{\"res_name\":\"8.0.2008\",\"value\":3000}]");
            ((ProtocolMappedDeviceBase)device).Model.Should().Be(model);
            device.ToString().Should().StartWith($"Device: {AqaraCubeSensor.MARKET_MODEL} {model} cube,");
        }
    }

    [Fact]
    public void Registered_Ble_Model_Is_Preserved_When_Created_By_Pdid()
    {
        ProtocolDeviceRegistry.TryCreateBle(XiaomiBluetoothHygrothermograph2.PDID, "ble",
            NullLoggerFactory.Instance, out var device).Should().BeTrue();
        using (device)
        {
            ((ProtocolMappedDeviceBase)device).Model.Should().Be(XiaomiBluetoothHygrothermograph2.MODEL);
            device.ToString().Should().Contain(XiaomiBluetoothHygrothermograph2.MODEL);
        }
    }

    [Theory]
    [InlineData(AqaraDualWallWirelessSwitch.LUMI_MQTT_MODEL)]
    [InlineData(AqaraDualWallWirelessSwitch.LUMI_MQTT_MODEL_86SW2)]
    [InlineData(AqaraDualWallWirelessSwitch.LUMI_MQTT_MODEL_86SW2_ES1)]
    public void Every_Wireless_Wall_Switch_Alias_Is_Preserved(string model)
    {
        ProtocolDeviceRegistry.TryCreate(DeviceProtocol.LumiMqtt, model, "switch", 0,
            NullLoggerFactory.Instance, out var device).Should().BeTrue();
        using (device)
        {
            device.ParseData("[{\"res_name\":\"8.0.2008\",\"value\":3000}]");
            ((ProtocolMappedDeviceBase)device).Model.Should().Be(model);
            device.ToString().Should().StartWith($"Device: {AqaraDualWallWirelessSwitch.MARKET_MODEL} {model} switch,");
        }
    }

    [Fact]
    public void Device_Created_Without_A_Gateway_Has_No_Reported_Model()
    {
        using var device = new AqaraCubeSensor("cube", 0, NullLoggerFactory.Instance);
        device.Model.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(InvalidBase))]
    [InlineData(typeof(MissingConstructor))]
    [InlineData(typeof(DuplicateModel))]
    [InlineData(typeof(DuplicatePdid))]
    public void Invalid_Registration_Is_Rejected(Type type)
    {
        Action validate = () => ProtocolDeviceRegistry.ValidateRegistrations([type]);
        validate.Should().Throw<InvalidOperationException>();
    }

    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "invalid", 999)]
    public class InvalidBase { }

    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "missing", 999)]
    public sealed class MissingConstructor : ProtocolMappedDeviceBase
    {
        private MissingConstructor(ILoggerFactory logs) : base(logs) { }
        public override string DeviceId => "test";
        private protected override void ConfigureProtocolMap(ProtocolDeviceMap map) { }
    }

    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "duplicate", 998)]
    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "duplicate", 999)]
    public sealed class DuplicateModel(string id, ILoggerFactory logs) : ProtocolMappedDeviceBase(logs)
    {
        public override string DeviceId => id;
        private protected override void ConfigureProtocolMap(ProtocolDeviceMap map) { }
    }

    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "one", 999)]
    [ProtocolDeviceModel(DeviceProtocol.LumiBle, "two", 999)]
    public sealed class DuplicatePdid(string id, ILoggerFactory logs) : ProtocolMappedDeviceBase(logs)
    {
        public override string DeviceId => id;
        private protected override void ConfigureProtocolMap(ProtocolDeviceMap map) { }
    }
}
