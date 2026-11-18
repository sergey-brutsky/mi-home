using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MiHomeLib;

/// <summary>
/// Logging setup for the whole library. Nothing is logged until a logger factory is provided,
/// either here or through a gateway constructor
/// </summary>
public static class MiHomeLogging
{
    private static volatile ILoggerFactory _default = NullLoggerFactory.Instance;

    /// <summary>
    /// Factory used by gateways created without a factory of their own
    /// </summary>
    internal static ILoggerFactory Default => _default;

    internal static ILoggerFactory Resolve(ILoggerFactory loggerFactory) => loggerFactory ?? _default;

    /// <summary>
    /// Sets the logger factory for all gateways created afterwards without a factory of their own.
    /// A gateway keeps the factory it was created with and passes it to its devices
    /// </summary>
    public static void SetLoggerFactory(ILoggerFactory loggerFactory) =>
        _default = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
}
