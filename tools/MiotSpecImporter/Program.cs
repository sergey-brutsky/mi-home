using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/MiotSpecImporter -- <path-to-instance.json>");
    return 2;
}

Console.Write(MiotSpecImporter.GenerateAttributes(File.ReadAllText(args[0])));
return 0;

public static class MiotSpecImporter
{
    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.Ordinal)
    {
        ["bool"] = "bool",
        ["uint8"] = "byte",
        ["uint16"] = "ushort",
        ["int16"] = "short",
        ["uint32"] = "uint",
        ["int32"] = "int",
        ["float"] = "float",
        ["string"] = "string"
    };

    public static string GenerateAttributes(string json)
    {
        using var document = JsonDocument.Parse(json);
        var properties = new List<PropertySpec>();
        foreach (var service in document.RootElement.GetProperty("services").EnumerateArray())
        {
            var serviceId = service.GetProperty("iid").GetInt32();
            var serviceName = service.TryGetProperty("description", out var serviceDescription)
                ? ToPascalCase(serviceDescription.GetString())
                : string.Empty;
            if (string.IsNullOrEmpty(serviceName)) serviceName = $"Service{serviceId}";

            if (!service.TryGetProperty("properties", out var serviceProperties)) continue;
            foreach (var property in serviceProperties.EnumerateArray())
            {
                var propertyId = property.GetProperty("iid").GetInt32();
                var description = property.GetProperty("description").GetString() ?? string.Empty;
                var format = property.TryGetProperty("format", out var formatElement)
                    ? formatElement.GetString() ?? string.Empty
                    : string.Empty;
                var access = property.TryGetProperty("access", out var accessElement)
                    ? ReadAccess(accessElement)
                    : [];
                properties.Add(new PropertySpec(
                    serviceId,
                    propertyId,
                    serviceName,
                    ToPascalCase(description),
                    description,
                    format,
                    access,
                    property.TryGetProperty("value-list", out var valueList) ? valueList : default));
            }
        }

        var duplicateNames = properties.GroupBy(property => property.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var output = new StringBuilder();
        foreach (var property in properties)
        {
            var name = duplicateNames.Contains(property.Name)
                ? property.ServiceName + property.Name
                : property.Name;
            if (!usedNames.Add(name))
            {
                var suffix = 2;
                var uniqueName = $"{name}{suffix}";
                while (!usedNames.Add(uniqueName)) uniqueName = $"{name}{++suffix}";
                name = uniqueName;
            }

            AppendComments(output, property);
            if (!TypeNames.TryGetValue(property.Format, out var typeName)) continue;

            var writable = property.Access.Contains("write", StringComparer.OrdinalIgnoreCase);
            output.Append("[MiotProperty(\"").Append(EscapeString(name)).Append("\", ")
                .Append(property.ServiceId).Append(", ").Append(property.PropertyId)
                .Append(", typeof(").Append(typeName).Append("), Writable = ")
                .Append(writable ? "true" : "false")
                .Append(", Summary = \"").Append(EscapeString(property.Description)).AppendLine("\")]");
        }

        return output.ToString();
    }

    private static void AppendComments(StringBuilder output, PropertySpec property)
    {
        if (property.ValueList.ValueKind == JsonValueKind.Array)
        {
            var values = property.ValueList.EnumerateArray().Select(value =>
            {
                var valueText = value.TryGetProperty("value", out var rawValue) ? rawValue.ToString() : "?";
                var description = value.TryGetProperty("description", out var rawDescription)
                    ? rawDescription.GetString() ?? string.Empty
                    : string.Empty;
                return $"{SingleLine(valueText)}={SingleLine(description)}";
            });
            output.Append("// TODO enum ").Append(property.ServiceId).Append('/').Append(property.PropertyId)
                .Append(": ").AppendLine(string.Join(", ", values));
        }

        if (!TypeNames.ContainsKey(property.Format))
            output.Append("// TODO unsupported format ").Append(SingleLine(property.Format)).Append(": ")
                .AppendLine(SingleLine(property.Description));

        if (!property.Access.Contains("read", StringComparer.OrdinalIgnoreCase))
            output.AppendLine("// TODO write-only");
    }

    private static string[] ReadAccess(JsonElement access) =>
        access.ValueKind switch
        {
            JsonValueKind.Array => access.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray(),
            JsonValueKind.String => [access.GetString() ?? string.Empty],
            _ => []
        };

    private static string ToPascalCase(string value)
    {
        var output = new StringBuilder(value.Length);
        var capitalize = true;
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                capitalize = true;
                continue;
            }

            output.Append(capitalize ? char.ToUpperInvariant(character) : char.ToLowerInvariant(character));
            capitalize = false;
        }

        if (output.Length == 0) return "Property";
        if (char.IsDigit(output[0])) output.Insert(0, "Property");
        return output.ToString();
    }

    private static string EscapeString(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            escaped.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                _ when char.IsControl(character) => $"\\u{(int)character:x4}",
                _ => character.ToString()
            });
        }

        return escaped.ToString();
    }

    private static string SingleLine(string value) =>
        value.Replace("\r", " ").Replace("\n", " ").Replace("//", "/ /");

    private sealed record PropertySpec(
        int ServiceId,
        int PropertyId,
        string ServiceName,
        string Name,
        string Description,
        string Format,
        string[] Access,
        JsonElement ValueList);
}
