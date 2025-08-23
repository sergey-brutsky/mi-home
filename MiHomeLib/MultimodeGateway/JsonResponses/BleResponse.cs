using System.Text.Json;

namespace MiHomeLib.MultimodeGateway.JsonResponses;

public abstract class BleResponse
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override string ToString()
    {
        return JsonSerializer.Serialize(System.Convert.ChangeType(this, GetType()), _options);
    }
}
