using System;
using Microsoft.Extensions.Logging;
using MiHomeLib.Devices;

namespace MiHomeLib.Protocols;

internal sealed record ProtocolDeviceEntry(
    Type DeviceType,
    Func<string, int, ILoggerFactory, GatewaySubDeviceBase> Create,
    Func<string, ILoggerFactory, GatewaySubDeviceBase> CreateByPdid);
