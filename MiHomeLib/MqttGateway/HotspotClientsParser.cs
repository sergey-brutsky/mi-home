using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MiHomeLib.MqttGateway;

internal static class HotspotClientsParser
{
    public static (string ip, string mac, string name)[] Parse(string text)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(text); }
        catch (JsonException) { document = JsonDocument.Parse(NormalizeQuotes(text)); }
        using (document)
        {
            var clients = new List<(string, string, string)>();
            foreach (var client in document.RootElement.EnumerateArray())
                clients.Add((ReadString(client, "ip"), ReadString(client, "mac"), ReadString(client, "name")));
            return clients.ToArray();
        }
    }

    private static string ReadString(JsonElement item, string name) =>
        item.GetProperty(name).GetString() ?? throw new FormatException($"Hotspot client field '{name}' is null.");

    // Some firmware returns single-quoted strings. Only string delimiters are converted.
    private static string NormalizeQuotes(string text)
    {
        var json = new StringBuilder();
        char quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (quote == '\0')
            {
                if (current is '\'' or '"')
                {
                    quote = current;
                    json.Append('"');
                }
                else json.Append(current);
            }
            else if (current == '\\' && i + 1 < text.Length)
            {
                var escaped = text[++i];
                if (quote == '\'' && escaped == '\'') json.Append('\'');
                else json.Append('\\').Append(escaped);
            }
            else if (current == quote)
            {
                json.Append('"');
                quote = '\0';
            }
            else if (quote == '\'' && current == '"') json.Append("\\\"");
            else json.Append(current);
        }
        return json.ToString();
    }
}
