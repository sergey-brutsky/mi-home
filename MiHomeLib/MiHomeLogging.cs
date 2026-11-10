using System;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway;

namespace MiHomeLib;

/// <summary>
/// Logging setup for the whole library. Nothing is logged until a logger factory is provided,
/// either here or through a gateway constructor
/// </summary>
public static class MiHomeLogging
{
    /// <summary>
    /// Sets the logger factory for all gateways created afterwards without a factory of their own.
    /// A gateway keeps the factory it was created with and passes it to its devices
    /// </summary>
    public static void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        if (loggerFactory is null) throw new ArgumentNullException(nameof(loggerFactory));

        MqttGatewayBase.SetLoggerFactory(loggerFactory);
        XiaomiGateway2.XiaomiGateway2.SetLoggerFactory(loggerFactory);
    }
}
