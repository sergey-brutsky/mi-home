using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiHomeLib.XiaomiGateway2.Commands;

public class WriteCommand: Command
{
    private readonly string _sid;
    private readonly string _type;
    private readonly string _data;

    public WriteCommand(string sid, string type, string key, Command data)
    {
        _sid = sid;
        _type = type;
        
        var jObject = JsonNode.Parse(data.ToString());
        jObject["key"] = key;
        _data = JsonSerializer.Serialize(jObject);
    }

    public override string ToString()
    {
        return $"{{\"cmd\":\"write\",\"model\":\"{_type}\",\"sid\":\"{_sid}\", \"data\":{_data}}}";
    }
}