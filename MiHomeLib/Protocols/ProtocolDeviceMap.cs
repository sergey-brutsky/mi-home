using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Protocols;

internal sealed class ProtocolDeviceMap
{
    private sealed class Mapping(string propertyName, Action<JsonElement> update, Action<JsonNode> initialize)
    {
        public string PropertyName { get; } = propertyName;
        public Action<JsonElement> Update { get; } = update;
        public Action<JsonNode> Initialize { get; } = initialize;
    }

    private readonly Dictionary<DeviceProtocol, Dictionary<string, Mapping>> _mappings = [];
    private readonly List<Mapping> _initialProperties = [];
    private readonly HashSet<string> _initialPropertyNames = new(StringComparer.Ordinal);

    public void XiaomiGateway2(string field, Action<JsonElement> update) =>
        Add(DeviceProtocol.XiaomiGateway2, field, null, update, null);

    public void LumiMqtt(string resource, Action<JsonElement> update) =>
        Add(DeviceProtocol.LumiMqtt, resource, null, update, null);

    public static string MiSpecKey(int siid, int piid) =>
        $"mi:{siid.ToString(CultureInfo.InvariantCulture)}:{piid.ToString(CultureInfo.InvariantCulture)}";

    public static string MiSpecEventKey(int siid, int eiid) =>
        $"event:{siid.ToString(CultureInfo.InvariantCulture)}:{eiid.ToString(CultureInfo.InvariantCulture)}";

    public void LumiMqttMiSpecEvent(int siid, int eiid, Action<JsonElement> update) =>
        Add(DeviceProtocol.LumiMqtt, MiSpecEventKey(siid, eiid), null, update, null);

    public void LumiMqttMiSpec(int siid, int piid, Action<JsonElement> update) =>
        Add(DeviceProtocol.LumiMqtt, MiSpecKey(siid, piid), null, update, null);

    public void LumiMeshMiSpec(int siid, int piid, Action<JsonElement> update) =>
        Add(DeviceProtocol.LumiMesh, MiSpecKey(siid, piid), null, update, null);

    public void LumiMeshMiSpecEvent(int siid, int eiid, Action<JsonElement> update) =>
        Add(DeviceProtocol.LumiMesh, MiSpecEventKey(siid, eiid), null, update, null);

    public bool UsesMiSpecBle { get; private set; }

    public void LumiBleMiSpec(int siid, int piid, Action<JsonElement> update)
    {
        UsesMiSpecBle = true;
        Add(DeviceProtocol.LumiBle, MiSpecKey(siid, piid), null, update, null);
    }

    public void LumiBleMiSpecEvent(int siid, int eiid, Action<JsonElement> update)
    {
        UsesMiSpecBle = true;
        Add(DeviceProtocol.LumiBle, MiSpecEventKey(siid, eiid), null, update, null);
    }

    public static string MiSpecServiceEventKey(int siid) =>
        $"event:{siid.ToString(CultureInfo.InvariantCulture)}:*";

    /// <summary>Handles every event of a service whose event identifiers are not distinguished.</summary>
    public void LumiBleMiSpecServiceEvent(int siid, Action<JsonElement> update)
    {
        UsesMiSpecBle = true;
        Add(DeviceProtocol.LumiBle, MiSpecServiceEventKey(siid), null, update, null);
    }

    public void LumiBle(int eid, Action<JsonElement> update) =>
        Add(DeviceProtocol.LumiBle, eid.ToString(CultureInfo.InvariantCulture), null, update, null);

    public void LumiMqtt(
        string resource,
        string propertyName,
        Action<JsonElement> update,
        Action<JsonNode> initialize)
    {
        ValidateInitialPropertyName(propertyName);
        if (initialize is null)
            throw new ArgumentNullException(nameof(initialize));

        var mapping = Add(DeviceProtocol.LumiMqtt, resource, propertyName, update, initialize);
        _initialProperties.Add(mapping);
        _initialPropertyNames.Add(propertyName);
    }

    /// <summary>
    /// A property read at discovery that never comes with reports
    /// </summary>
    public void LumiMqttInitial(string propertyName, Action<JsonNode> initialize)
    {
        ValidateInitialPropertyName(propertyName);
        if (initialize is null) throw new ArgumentNullException(nameof(initialize));
        _initialProperties.Add(new Mapping(propertyName, _ => { }, initialize));
        _initialPropertyNames.Add(propertyName);
    }

    private void ValidateInitialPropertyName(string propertyName)
    {
        if (propertyName is null) throw new ArgumentNullException(nameof(propertyName));
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("Initial property name must not be empty.", nameof(propertyName));
        if (_initialPropertyNames.Contains(propertyName))
            throw new InvalidOperationException($"Initial property '{propertyName}' is mapped more than once.");
    }

    public bool Apply(DeviceProtocol protocol, string key, JsonElement value)
    {
        if (_mappings.TryGetValue(protocol, out var protocolMappings) &&
            protocolMappings.TryGetValue(key, out var mapping))
        {
            mapping.Update(value);
            return true;
        }

        return false;
    }

    public string[] GetInitialPropertyNames() => _initialProperties.Select(x => x.PropertyName).ToArray();

    public void SetInitialProperties(JsonNode[] values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        if (values.Length != _initialProperties.Count)
            throw new ArgumentException($"Expected {_initialProperties.Count} initial properties but received {values.Length}.", nameof(values));

        for (var i = 0; i < values.Length; i++)
            _initialProperties[i].Initialize(values[i]);
    }

    public void SetInitialProperty(string name, JsonNode value)
    {
        var mapping = _initialProperties.FirstOrDefault(property => property.PropertyName == name)
            ?? throw new ArgumentException($"Initial property '{name}' is not mapped.", nameof(name));
        mapping.Initialize(value);
    }

    private Mapping Add(
        DeviceProtocol protocol,
        string key,
        string propertyName,
        Action<JsonElement> update,
        Action<JsonNode> initialize)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Protocol field must not be empty.", nameof(key));
        if (update is null)
            throw new ArgumentNullException(nameof(update));

        var mapping = new Mapping(propertyName, update, initialize);
        if (!_mappings.TryGetValue(protocol, out var protocolMappings))
        {
            protocolMappings = new Dictionary<string, Mapping>(StringComparer.Ordinal);
            _mappings.Add(protocol, protocolMappings);
        }

        if (protocolMappings.ContainsKey(key))
            throw new InvalidOperationException($"Protocol field '{key}' is mapped more than once for {protocol}.");
        protocolMappings.Add(key, mapping);
        return mapping;
    }
}
