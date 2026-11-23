using System.Text.Json;

namespace MiHomeUnitTests.Infrastructure;

internal static class JsonExtensions
{
    public static string ToJson(this object data)
    {
        return JsonSerializer.Serialize(data);
    }
}
