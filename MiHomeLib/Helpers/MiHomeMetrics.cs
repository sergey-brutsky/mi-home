using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace MiHomeLib.Helpers;

internal static class MiHomeMetrics
{
    private static readonly Meter Meter = new(
        MiHomeDiagnostics.MeterName,
        typeof(MiHomeDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    private static readonly Histogram<double> MiioRequestDuration =
        Meter.CreateHistogram<double>("mihome.miio.request.duration", "ms");
    private static readonly Counter<long> MiioRequestRetries =
        Meter.CreateCounter<long>("mihome.miio.request.retries", "{retry}");
    private static readonly Counter<long> MqttConnectionLost =
        Meter.CreateCounter<long>("mihome.mqtt.connection.lost", "{event}");
    private static readonly Counter<long> MqttConnectionRestored =
        Meter.CreateCounter<long>("mihome.mqtt.connection.restored", "{event}");
    private static readonly Counter<long> MessagesReceived =
        Meter.CreateCounter<long>("mihome.messages.received", "{message}");
    private static readonly Counter<long> MessagesDropped =
        Meter.CreateCounter<long>("mihome.messages.dropped", "{message}");
    private static readonly Counter<long> DeviceAvailabilityChanged =
        Meter.CreateCounter<long>("mihome.devices.availability.changed", "{change}");

    public static void RecordMiioRequestDuration(double milliseconds, string method, string outcome) =>
        MiioRequestDuration.Record(milliseconds,
            new KeyValuePair<string, object>("method", method),
            new KeyValuePair<string, object>("outcome", outcome));

    public static void RecordMiioRetry(string method) =>
        MiioRequestRetries.Add(1, new KeyValuePair<string, object>("method", method));

    public static void RecordMqttConnectionLost() => MqttConnectionLost.Add(1);

    public static void RecordMqttConnectionRestored() => MqttConnectionRestored.Add(1);

    public static void RecordMessageReceived(string gateway) =>
        MessagesReceived.Add(1, new KeyValuePair<string, object>("gateway", gateway));

    public static void RecordMessageDropped(string gateway, string reason) =>
        MessagesDropped.Add(1,
            new KeyValuePair<string, object>("gateway", gateway),
            new KeyValuePair<string, object>("reason", reason));

    public static void RecordDeviceAvailabilityChanged(bool available) =>
        DeviceAvailabilityChanged.Add(1, new KeyValuePair<string, object>("available", available));
}
