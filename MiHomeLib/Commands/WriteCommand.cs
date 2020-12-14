using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MiHomeLib.Commands
{
    public class WriteCommand: Command
    {
        private readonly string _sid;
        private readonly string _type;
        private readonly string _data;

        public WriteCommand(string sid, string type, string key, Command data)
        {
            _sid = sid;
            _type = type;
            
            var jObject = JObject.Parse(data.ToString());
            jObject["key"] = key;

            _data = JsonConvert.SerializeObject(jObject.ToString(Formatting.None));
        }

        public override string ToString()
        {
            return $"{{\"cmd\":\"write\",\"model\":\"{_type}\",\"sid\":\"{_sid}\", \"data\":{_data}}}";
        }
    }
}