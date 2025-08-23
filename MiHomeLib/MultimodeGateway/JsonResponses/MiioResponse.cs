using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiHomeLib.MultimodeGateway.JsonResponses;

public abstract class MiioResponse
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public override string ToString()
    {
        return JsonSerializer.Serialize(Convert.ChangeType(this, GetType()), _options);
    }
}
