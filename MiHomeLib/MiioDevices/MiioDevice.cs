using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public abstract class MiioDevice
{
    private int _initialId = 0;
    private readonly JsonSerializerOptions _serializerSettings;
    protected readonly IMiioTransport _miioTransport;
    public MiioDevice(IMiioTransport miioTransport, int initialIdExternal = 0)
    {
        _miioTransport = miioTransport;
        _initialId = initialIdExternal;

        _serializerSettings = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
    protected void CheckMessage(string response, string errorMessage)
    {
        if (response != $"{{\"result\":[\"ok\"],\"id\":{_initialId}}}")
        {
            throw new Exception($"{errorMessage}, miio protocol error --> {response}");
        }
    }
    protected int GetInteger(string response, string msg)
    {
        var result = JsonNode.Parse(response)["result"][0].ToString();

        if (!int.TryParse(result, out int number))
        {
            throw new Exception($"{msg}, value '{result}' seems to be corrupted");
        }

        return number;
    }
    protected string BuildParamsArray(string method, params object[] methodParams)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>(){
            { "id", Interlocked.Increment(ref _initialId) },
            { "method", method},
            { "params", methodParams}
        }, _serializerSettings);
    }
    protected string BuildParamsObject(string method, object methodParams)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>(){
            { "id", Interlocked.Increment(ref _initialId) },
            { "method", method},
            { "params", methodParams }
        }, _serializerSettings);
    }
    protected string BuildSidProp(string method, string sid, string prop, int value)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>(){
            { "id", Interlocked.Increment(ref _initialId) },
            { "method", method},
            { "params", new Dictionary<string, object>
                {
                    { "sid", sid },
                    { prop, value },
                }
            }
        }, _serializerSettings);
    }
    protected string RepeatMessageIfTimeout(Func<string, string> func, string msg, int times = 3)
    {
        var error = string.Empty;

        for (int i = 0; i < times; i++)
        {
            try
            {
                var response = func(msg);
                var json = JsonNode.Parse(response).AsObject();

                // it's okay that there are timeouts for some requests
                if (json["error"] != null && json["error"]["code"] != null)
                {
                    error = json["error"].ToString();
                    Interlocked.Increment(ref _initialId);
                    var newMsg = JsonObject.Parse(msg)["id"];
                    newMsg["id"] = _initialId;
                    msg = newMsg.ToString();
                    continue;
                }

                return response;

            }
            catch (Exception) { }
        }

        throw new Exception($"No response for msg -> '{msg}' after {times} attempts, error '{error}'");
    }
    protected string[] GetProps(params string[] props)
    {
        return ResultProps(_miioTransport.SendMessage(BuildParamsArray("get_prop", props)));
    }
    protected async Task<string[]> GetPropsAsync(params string[] props)
    {
        return ResultProps(await _miioTransport.SendMessageAsync(BuildParamsArray("get_prop", props)));
    }
    private static string[] ResultProps(string response) => JsonNode
                        .Parse(response)["result"]
                        .AsArray()
                        .Select(x => x.ToString())
                        .ToArray();
    public void Dispose() => _miioTransport?.Dispose();
}