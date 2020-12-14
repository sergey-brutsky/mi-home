using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MiHomeLib
{
    public static class JObjectHelpers
    {
        public static string CreateCommand(string cmd, string model, string sid, int short_id, Dictionary<string, object> data)
        {
            var dict = new Dictionary<string, object>()
            {
                { "cmd", cmd},
                { "model", model},
                { "sid", sid},
                { "short_id", short_id},
                { "data", JsonConvert.SerializeObject(data) },
            };

            return JsonConvert.SerializeObject(dict);
        }

        public static bool ParseString(this JObject jObject, string key, out string s)
        {
            if (jObject[key] != null)
            {
                s = jObject[key].ToString();
                return true;
            }

            s = null;
            return false;
        }

        public static bool ParseInt(this JObject jObject, string key, out int f)
        {
            if (jObject[key] != null && int.TryParse(jObject[key].ToString(), out int f1))
            {
                f = f1;
                return true;
            }

            f = 0;
            return false;
        }

        public static bool ParseFloat(this JObject jObject, string key, out float f)
        {
            if (jObject[key] != null && float.TryParse(jObject[key].ToString(), out float f1))
            {
                f = f1;
                return true;
            }   

            f = 0;
            return false;
        }

        public static float? ParseVoltage(this JObject jObject)
        {
            if(jObject.ParseFloat("voltage", out float v))
            {
                return v / 1000;
            }

            return null;
        }
    }
}
