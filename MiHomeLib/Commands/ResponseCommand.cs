using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MiHomeLib.Commands
{
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
                var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);

                var cmd = json["cmd"];

                if (commandTypeMap.ContainsKey(cmd))
                {
                    return new ResponseCommand
                    {
                        RawCommand = cmd,
                        Command = commandTypeMap[cmd],
                        Model = json.ContainsKey("model") ? json["model"] : null,
                        Sid = json["sid"],
                        ShortId = json.ContainsKey("short_id") ? int.Parse(json["short_id"]) : 0,
                        Token = json.ContainsKey("token") ? json["token"] : null,
                        Data = json["data"],
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
                throw new ResponseCommandException("Parsing response command failed", e);
            }
        }
        
    }
}