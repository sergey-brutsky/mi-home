using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Protocols;

/// <summary>
/// One value of a gateway message: the key a device maps, e.g. "voltage", "8.0.2008" or "mi:2:1".
/// <see cref="FallbackKey"/> is tried when the device has no mapping for <see cref="Key"/>
/// </summary>
internal readonly struct ProtocolEntry(string key, JsonElement value, string fallbackKey = null, bool isMalformed = false)
{
    public string Key { get; } = key;
    public JsonElement Value { get; } = value;
    public string FallbackKey { get; } = fallbackKey;
    public bool IsMalformed { get; } = isMalformed;
    public static ProtocolEntry Malformed => new(null, default, isMalformed: true);
}

/// <summary>
/// Turns the payload of a gateway message into keys and values, so devices do not depend on the wire format:
/// a Xiaomi Gateway 2 "data" object, a BLE "evt" array, Lumi "res_name" resources or MI-spec siid/piid/eiid items
/// </summary>
internal static class ProtocolMessageReader
{
    public static IEnumerable<ProtocolEntry> Read(DeviceProtocol protocol, bool miSpecBle, JsonElement data, ILogger logger)
    {
        if (protocol == DeviceProtocol.XiaomiGateway2)
            return ReadGateway2(data, logger);

        if (protocol == DeviceProtocol.LumiBle && !miSpecBle)
            return ReadMiBeacon(data, logger);

        return ReadItems(protocol, miSpecBle,
            data.ValueKind == JsonValueKind.Array ? data.EnumerateArray() : Enumerable.Repeat(data, 1), logger);
    }

    /// <summary>
    /// Items of Lumi MQTT, MI-spec BLE and mesh messages: {"res_name":..,"value":..}, {"siid":..,"piid":..,"value":..}
    /// or an event {"siid":..,"eiid":..,"arguments":..}
    /// </summary>
    public static IEnumerable<ProtocolEntry> ReadItems(DeviceProtocol protocol, bool miSpecBle, IEnumerable<JsonElement> items, ILogger logger)
    {
        var miSpecOnly = protocol == DeviceProtocol.LumiMesh || protocol == DeviceProtocol.LumiBle && miSpecBle;

        var index = 0;
        foreach (var item in items)
        {
            yield return TryReadItem(protocol, miSpecOnly, item, out var entry, out var reason)
                ? entry : Invalid(protocol, index, reason, logger);
            index++;
        }
    }

    private static bool TryReadItem(DeviceProtocol protocol, bool miSpecOnly, JsonElement item,
        out ProtocolEntry entry, out string reason)
    {
        entry = default;
        reason = "expected an object";
        if (item.ValueKind != JsonValueKind.Object) return false;
        if (!miSpecOnly && item.TryGetProperty("res_name", out var name))
        {
            reason = "missing or invalid res_name";
            if (name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())) return false;
            reason = "missing or null value";
            if (!TryValue(item, out var value)) return false;
            entry = new ProtocolEntry(name.GetString(), value);
            return true;
        }
        reason = "missing or invalid siid";
        if (!TryInt(item, "siid", out var siid)) return false;
        if (item.TryGetProperty("eiid", out var eventId))
        {
            reason = "invalid eiid";
            if (eventId.ValueKind != JsonValueKind.Number || !eventId.TryGetInt32(out var eiid)) return false;
            // Events are passed whole; arguments remain available to device-specific handlers.
            entry = new ProtocolEntry(ProtocolDeviceMap.MiSpecEventKey(siid, eiid), item,
                protocol == DeviceProtocol.LumiBle ? ProtocolDeviceMap.MiSpecServiceEventKey(siid) : null);
            return true;
        }
        reason = "missing or invalid piid";
        if (!TryInt(item, "piid", out var piid)) return false;
        reason = "missing or null value";
        if (!TryValue(item, out var propertyValue)) return false;
        entry = new ProtocolEntry(ProtocolDeviceMap.MiSpecKey(siid, piid), propertyValue);
        return true;
    }

    private static IEnumerable<ProtocolEntry> ReadGateway2(JsonElement data, ILogger logger)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            yield return Invalid(DeviceProtocol.XiaomiGateway2, -1, "expected an object", logger);
            yield break;
        }
        foreach (var property in data.EnumerateObject())
            yield return new ProtocolEntry(property.Name, property.Value);
    }

    private static IEnumerable<ProtocolEntry> ReadMiBeacon(JsonElement data, ILogger logger)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("evt", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            yield return Invalid(DeviceProtocol.LumiBle, -1, "missing or invalid evt array", logger);
            yield break;
        }
        var index = 0;
        foreach (var item in events.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                yield return Invalid(DeviceProtocol.LumiBle, index, "expected an object", logger);
            else if (!TryInt(item, "eid", out var eid))
                yield return Invalid(DeviceProtocol.LumiBle, index, "missing or invalid eid", logger);
            else if (!item.TryGetProperty("edata", out var dataValue) || dataValue.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(dataValue.GetString()))
                yield return Invalid(DeviceProtocol.LumiBle, index, "missing or invalid edata", logger);
            else
                yield return new ProtocolEntry(eid.ToString(CultureInfo.InvariantCulture), dataValue);
            index++;
        }
    }

    private static bool TryInt(JsonElement item, string field, out int number)
    {
        number = 0;
        return item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number);
    }

    private static bool TryValue(JsonElement item, out JsonElement value) =>
        item.TryGetProperty("value", out value) && value.ValueKind != JsonValueKind.Null;

    private static ProtocolEntry Invalid(DeviceProtocol protocol, int index, string reason, ILogger logger)
    {
        if (index < 0)
            logger.LogWarning("Invalid {Protocol} message: {Reason}. Processing is skipped.", protocol, reason);
        else
            logger.LogWarning("Invalid {Protocol} message item {Index}: {Reason}. Processing is skipped.", protocol, index, reason);
        return ProtocolEntry.Malformed;
    }
}
