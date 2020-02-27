using System.Collections.Generic;
using Newtonsoft.Json;

namespace MiHomeLib.Commands
{
    public class ResponseCommand
    {
        public ResponseCommandType Command { get; private set; }
        public string Model { get; set; }
        public string Sid { get; set; }
        public int ShortId { get; set; }
        public string Token { get; set; }
        public string Data { get; set; }

        public static ResponseCommand FromString(string data)
        {
            var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
            
            return new ResponseCommand
            {
                Command = ResponseCommandType.GetIdListAck,
                Model = json["model"],
                Sid = json["sid"],
                ShortId = int.Parse(json["short_id"]),
                //Token = json["token"],
                Data =  json["data"],
            };

        }

        
    }
}