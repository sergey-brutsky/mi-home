using MiHomeLib.Protocols;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

public abstract class ProtocolMappedDeviceBase : GatewaySubDeviceBase
{
    private DeviceProtocol _protocol;
    private ProtocolDeviceMap _map;

    protected ProtocolMappedDeviceBase(ILoggerFactory loggerFactory) : base(loggerFactory) { }

    private protected abstract void ConfigureProtocolMap(ProtocolDeviceMap map);

    /// <summary>
    /// Model the device reported itself with on its gateway, e.g. "lumi.sensor_magnet";
    /// null until the device is created by a gateway
    /// </summary>
    public string Model { get; private set; }

    internal void InitializeProtocol(DeviceProtocol protocol, string model = null)
    {
        _protocol = protocol;
        Model = model;
        _map = new ProtocolDeviceMap();
        ConfigureProtocolMap(_map);
    }

    internal string[] GetInitialPropertyNames() => _map.GetInitialPropertyNames();

    internal void SetInitialProperties(JsonNode[] values) => _map.SetInitialProperties(values);

    internal void SetInitialProperty(string name, JsonNode value) => _map.SetInitialProperty(name, value);

    internal bool IsInitializedFor(DeviceProtocol protocol) => _map is not null && _protocol == protocol;

    internal sealed override void ParseData(string data)
    {
        using var document = JsonDocument.Parse(data);
        ParseData(document.RootElement);
    }

    internal sealed override void ParseData(JsonElement data) =>
        Apply(ProtocolMessageReader.Read(_protocol, _map?.UsesMiSpecBle ?? false, data, _logger));

    // read_rsp supplies its valid resources as one message without rebuilding a JSON array.
    internal sealed override void ParseData(IEnumerable<JsonElement> data) =>
        Apply(ProtocolMessageReader.ReadItems(_protocol, _map?.UsesMiSpecBle ?? false, data, _logger));

    private void Apply(IEnumerable<ProtocolEntry> entries)
    {
        if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
        if (_map is null)
            throw new InvalidOperationException("The device has not been initialized for a gateway protocol.");

        OnMessageParsing();
        var malformed = false;
        foreach (var entry in entries)
        {
            if (entry.IsMalformed)
            {
                malformed = true;
                continue;
            }
            try
            {
                var applied = entry.Key is not null && _map.Apply(_protocol, entry.Key, entry.Value) ||
                              entry.FallbackKey is not null && _map.Apply(_protocol, entry.FallbackKey, entry.Value);
                if (!applied)
                    LogUnsupportedProperty(entry.Key ?? "unknown");
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or JsonException)
            {
                malformed = true;
                _logger.LogWarning("Invalid {Protocol} value for '{Property}' on device '{DeviceId}': {Reason}. Processing is skipped.",
                    _protocol, entry.Key, DeviceId, exception.Message);
            }
        }
        // Independent values are still applied, but incomplete reports cannot form a composite event.
        if (!malformed) OnMessageParsed();
    }

    /// <summary>
    /// Reads a number Xiaomi Gateway 2 may send either as a string ("2985") or as a number, logs a warning when it is neither
    /// </summary>
    private protected bool TryGetGateway2Int(JsonElement value, out int number)
    {
        number = 0;
        var parsed = value.ValueKind == JsonValueKind.Number
            ? value.TryGetInt32(out number)
            : value.ValueKind == JsonValueKind.String &&
              int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

        if (!parsed)
            _logger.LogWarning("Value '{Value}' is not a number", value.GetRawText());

        return parsed;
    }

    /// <inheritdoc cref="TryGetGateway2Int"/>
    private protected bool TryGetGateway2Float(JsonElement value, out float number)
    {
        number = 0;
        var parsed = value.ValueKind == JsonValueKind.Number
            ? value.TryGetSingle(out number)
            : value.ValueKind == JsonValueKind.String &&
              float.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);

        parsed = parsed && !float.IsNaN(number) && !float.IsInfinity(number);
        if (!parsed)
        {
            number = 0;
            _logger.LogWarning("Value '{Value}' is not a finite Single number", value.GetRawText());
        }

        return parsed;
    }

    /// <summary>Reset temporary per-message values, including values left by an incomplete report.</summary>
    private protected virtual void OnMessageParsing() { }

    /// <summary>
    /// Called after all values of one message are applied, for values that only make sense together
    /// </summary>
    private protected virtual void OnMessageParsed() { }

    private void LogUnsupportedProperty(string property) =>
        _logger.LogWarning("Property '{Property}' is not supported for this device yet. Please contribute to support.", property);
}
