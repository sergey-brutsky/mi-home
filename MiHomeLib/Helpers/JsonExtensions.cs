using System.Text.Json;

namespace MiHomeLib.Helpers;

internal static class JsonExtensions
{
    public static string ToJson(this object data)
    {
        return JsonSerializer.Serialize(data);
    }
}
