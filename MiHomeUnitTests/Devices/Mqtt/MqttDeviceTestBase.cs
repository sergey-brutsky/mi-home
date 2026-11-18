using MiHomeLib.Protocols;
using MiHomeLib.MqttGateway.Discovery;
using MiHomeLib.MqttGateway;
using MiHomeLib.MiioDevices;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoFixture;
using Microsoft.Extensions.Logging;
using MiHomeLib;
using MiHomeLib.Devices;

using Moq;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse;
using static MiHomeUnitTests.Infrastructure.Messages.BleAsyncEventResponse.BleAsyncEventParams;

namespace MiHomeUnitTests.Devices.Mqtt;

public abstract class MqttDeviceTestBase : ProtocolDeviceTestBase
{
    protected readonly Mock<IDevicesDiscoverer> _devicesDiscoverer = new();
    protected readonly Mock<IMqttTransport> _mqttTransport = new();

    protected MqttDeviceTestBase() => SetupMiioInfo();
}
