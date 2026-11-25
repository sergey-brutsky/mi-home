using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MiHomeLib.MqttGateway.Discovery;
using Xunit;

namespace MiHomeUnitTests.Integration;

[CollectionDefinition("Network integration", DisableParallelization = true)]
public sealed class IntegrationCollection
{
    public const string Name = "Network integration";
}

internal sealed class FixedDevicesDiscoverer(
    List<ZigBeeDeviceInfo> zigbeeDevices = null) : IDevicesDiscoverer
{
    public List<BleDeviceInfo> DiscoverBleDevices() => [];
    public List<MeshDeviceInfo> DiscoverMeshDevices() => [];
    public List<ZigBeeDeviceInfo> DiscoverZigBeeDevices() => zigbeeDevices ?? [];
}

internal sealed class ErrorRecordingLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentQueue<string> _errors = new();
    public IReadOnlyCollection<string> Errors => _errors.ToArray();

    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();
    public ILogger CreateLogger(string categoryName) => new ErrorRecordingLogger(categoryName, _errors);
    public void Dispose() { }

    private sealed class ErrorRecordingLogger(string categoryName, ConcurrentQueue<string> errors) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                errors.Enqueue($"{categoryName}: {formatter(state, exception)}");
        }
    }
}
