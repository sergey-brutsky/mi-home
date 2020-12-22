using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MiHomeLib
{
    public static class Helpers
    {
        public static async Task<byte[]> ReceiveBytesAsync(this Socket socket, int bufferSize = 4096)
        {
            var buffer = new ArraySegment<byte>(new byte[bufferSize]);
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None).ConfigureAwait(false);

            if (received > bufferSize)
            {
                throw new Exception("Data received is greater than buffer size");
            }

            return buffer.Take(received).ToArray();
        }

        public static byte[] ReceiveBytes(this Socket socket, int bufferSize = 4096)
        {
            var buffer = new byte[bufferSize];
            
            var recevied = socket.Receive(buffer);
            
            if(recevied > bufferSize)
            {
                throw new Exception("Data received is greater than buffer size");
            }

            return new ArraySegment<byte>(buffer, 0, recevied).ToArray();
        }

        public static ArraySegment<byte> ToArraySegment(this string hex)
        {
            return new ArraySegment<byte>(hex.ToByteArray());
        }

        public static byte[] ToByteArray(this string hex)
        {
            return Enumerable
                .Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        public static string ToHex(this byte[] byteArray)
        {
            return BitConverter.ToString(byteArray).Replace("-", string.Empty).ToLower();
        }

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
