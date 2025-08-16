using System.Text.Json;

namespace MiHomeLib.XiaomiGateway3.JsonResponses;

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
