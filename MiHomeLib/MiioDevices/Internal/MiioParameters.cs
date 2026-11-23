using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiHomeLib.Helpers;

namespace MiHomeLib.MiioDevices.Internal;

internal static class MiioParameters
{
    internal static JsonNode ToNode(object value)
    {
        switch (value)
        {
            case null: return null;
            case JsonNode node: return node;
            case string v: return JsonValue.Create(v);
            case bool v: return JsonValue.Create(v);
            case int v: return JsonValue.Create(v);
            case long v: return JsonValue.Create(v);
            case uint v: return JsonValue.Create(v);
            case ushort v: return JsonValue.Create(v);
            case byte v: return JsonValue.Create(v);
            case short v: return JsonValue.Create(v);
            case float v: return JsonValue.Create(v);
            case double v: return JsonValue.Create(v);
            case decimal v: return JsonValue.Create(v);
            case Enum v: return v.GetTypeCode() == TypeCode.UInt64
                ? JsonValue.Create(Convert.ToUInt64(v, CultureInfo.InvariantCulture))
                : JsonValue.Create(Convert.ToInt64(v, CultureInfo.InvariantCulture));
            case IDictionary<string, object> dictionary:
                var obj = new JsonObject();
                foreach (var item in dictionary) obj.Add(item.Key, ToNode(item.Value)?.DeepClone());
                return obj;
            case IEnumerable sequence:
                var array = new JsonArray();
                foreach (var item in sequence) array.Add(ToNode(item)?.DeepClone());
                return array;
            default:
                var type = value.GetType();
                var info = MiHomeJsonContext.Default.GetTypeInfo(type);
                if (info is not null) return JsonSerializer.SerializeToNode(value, info);
                throw new NotSupportedException($"miio parameter of type {type} is not supported");
        }
    }
}
