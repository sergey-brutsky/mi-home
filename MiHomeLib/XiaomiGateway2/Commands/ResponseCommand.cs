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

            if (commandTypeMap.ContainsKey(cmd))
            {
                return new ResponseCommand
                {
                    RawCommand = cmd,
                    Command = commandTypeMap[cmd],
                    Model = json.ContainsKey("model") ? json["model"].ToString() : null,
                    Sid = json["sid"].ToString(),
                    ShortId = json.ContainsKey("short_id") ? int.Parse(json["short_id"].ToString()) : 0,
                    Token = json.ContainsKey("token") ? json["token"].ToString() : null,
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