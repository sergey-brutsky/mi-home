using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MiHomeLib.MiioDevices;

public class MiioDevice
{
    protected int _clientId;
    protected readonly IMiioTransport _miioTransport;
    private readonly JsonSerializerSettings _serializerSettings = new();

    public MiioDevice(IMiioTransport miioTransport, int? initialClientId = null)
    {
        _miioTransport = miioTransport;
        _clientId = initialClientId is null ? new Random().Next(1, 255) : initialClientId.Value;
        _serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();

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
        return (string)GetResult(response);
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
    protected static JToken GetResult(string response)
    {
        return (JObject.Parse(response)["result"] as JArray)[0];
    }

    protected string BuildParamsArray(string method, params object[] methodParams)
    {
        return $"{{\"id\": {Interlocked.Increment(ref _clientId)}, \"method\": \"{method}\", \"params\": {JsonConvert.SerializeObject(methodParams, _serializerSettings)}}}";
    }

    protected string BuildParamsObject(string method, object methodParams)
    {
        return $"{{\"id\": {Interlocked.Increment(ref _clientId)}, \"method\": \"{method}\", \"params\": {JsonConvert.SerializeObject(methodParams, _serializerSettings)}}}";
    }

    protected string BuildSidProp(string method, string sid, string prop, int value)
    {
        return $"{{\"id\": {Interlocked.Increment(ref _clientId)}, \"method\": \"{method}\", \"params\": {{\"sid\":\"{sid}\", \"{prop}\":{value}}}}}";
    }

    protected string[] GetProps(params string[] props)
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("get_prop", props));
        var values = JObject.Parse(response)["result"] as JArray;
        return values.Select(x => x.ToString()).ToArray();
    }

    protected async Task<string[]> GetPropsAsync(params string[] props)
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("get_prop", props));
        var values = JObject.Parse(response)["result"] as JArray;
        return values.Select(x => x.ToString()).ToArray();
    }

    public int GetClientId() => _clientId;
    public void Dispose()
    {
        _miioTransport?.Dispose();
    }
}