using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public class MiioDevice
{
    protected int _clientId;
    private JsonSerializerOptions _serializerSettings;
    protected readonly IMiioTransport _miioTransport;

    public MiioDevice(IMiioTransport miioTransport, int? initialClientId = null)
    {
        _miioTransport = miioTransport;
        _clientId = initialClientId is null ? new Random().Next(1, 255) : initialClientId.Value;
        _serializerSettings = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    protected void CheckMessage(string response, string errorMessage)
    {
        if (response.TrimEnd('\0') != $"{{\"result\":[\"ok\"],\"id\":{_clientId}}}")
        {
            throw new Exception($"{errorMessage}, miio protocol error --> {response}");
        }
    }
    protected string GetString(string response)
    {
        return System.Text.Json.Nodes.JsonNode.Parse(response)["result"][0].ToString();
    }

    protected int GetInteger(string response, string msg)
    {
        var result = GetString(response);

        if (!int.TryParse(result, out int number))
        {
            throw new Exception($"{msg}, value '{result}' seems to be corrupted");
        }

        return number;
    }
    
    protected string BuildParamsArray(string method, params object[] methodParams)
    {
        var params1 = JsonSerializer.Serialize(methodParams, _serializerSettings);
        return $"{{\"id\": {Interlocked.Increment(ref _clientId)}, \"method\": \"{method}\", \"params\": {params1}}}";
    }

    protected string BuildParamsObject(string method, object methodParams)
    {
        var params1 = JsonSerializer.Serialize(methodParams, _serializerSettings);
        return $"{{\"id\": {Interlocked.Increment(ref _clientId)}, \"method\": \"{method}\", \"params\": {params1}}}";
    }

    protected string BuildSidProp(string method, string sid, string prop, int value)
    {
        return $"{{\"id\": {Interlocked.Increment(ref _clientId)}, \"method\": \"{method}\", \"params\": {{\"sid\":\"{sid}\", \"{prop}\":{value}}}}}";
    }

    protected string[] GetProps(params string[] props)
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("get_prop", props));
        var values = System.Text.Json.Nodes.JsonNode.Parse(response)["result"].AsArray();
        return values.Select(x => x.ToString()).ToArray();
        // var values = JObject.Parse(response)["result"] as JArray;
        // return values.Select(x => x.ToString()).ToArray();
    }

    protected async Task<string[]> GetPropsAsync(params string[] props)
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("get_prop", props));
        var values = System.Text.Json.Nodes.JsonNode.Parse(response)["result"].AsArray();
        return values.Select(x => x.ToString()).ToArray();
        // var values = JObject.Parse(response)["result"] as JArray;
        // return values.Select(x => x.ToString()).ToArray();
    }

    public int GetClientId() => _clientId;
    public void Dispose() => _miioTransport?.Dispose();
}