using System;
using System.Text.Json;

namespace MiHomeLib.JsonResponses;

public abstract class BleResponse
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override string ToString()
    {
        return JsonSerializer.Serialize(Convert.ChangeType(this, GetType()), _options);
    }
}
