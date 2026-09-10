using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MiHomeLib.XiaomiGateway2.Commands;

public class ResponseCommand
{
    public string RawCommand { get; set; }
    public ResponseCommandType Command { get; private set; }
    public string Model { get; set; }
    public string Sid { get; set; }
    public int ShortId { get; set; }
    public string Token { get; set; }
    public string Data { get; set; }

    private static readonly Dictionary<string, ResponseCommandType> commandTypeMap = new Dictionary<string, ResponseCommandType>
    {
        { "get_id_list_ack", ResponseCommandType.GetIdListAck},
        { "report", ResponseCommandType.Report},
        { "heartbeat", ResponseCommandType.Hearbeat},
        { "read_ack", ResponseCommandType.ReadAck},
    };

    public static ResponseCommand FromString(string data)
    {
        try
        {
            var  json = JsonSerializer.Deserialize<Dictionary<string, object>>(data);

            var cmd = json["cmd"].ToString();

            if (commandTypeMap.TryGetValue(cmd, out var commandType))
            {
                return new ResponseCommand
                {
                    RawCommand = cmd,
                    Command = commandType,
                    Model = json.TryGetValue("model", out var model) ? model.ToString() : null,
                    Sid = json["sid"].ToString(),
                    ShortId = json.TryGetValue("short_id", out var shortId) ? int.Parse(shortId.ToString()) : 0,
                    Token = json.TryGetValue("token", out var token) ? token.ToString() : null,
                    Data = json["data"].ToString(),
                };
            }
            else
            {
                return new ResponseCommand
                {
                    RawCommand = cmd,
                    Command = ResponseCommandType.Unknown
                };
            }
        }
        catch (Exception e) {
            throw new Exception("Parsing response command failed", e);
        }
    }
    
    public override string ToString()
    {
        return JsonSerializer.Serialize(Convert.ChangeType(this, GetType()));
    }
}