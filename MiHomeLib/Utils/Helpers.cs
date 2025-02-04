using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiHomeLib.Utils;
public static class Helpers
{
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
    public static float ToBleFloat(this string hex)
    {
        // hex string is little endian !
        var arr = hex.ToByteArray();
        arr.Reverse();
        return BitConverter.ToInt16(arr, 0)/10f;
    }
    public static byte ToBleByte(this string hex)
    {
        return hex.ToByteArray()[0];
    }

    public static int ToBleInt256(this string hex)
    {
        var res = 0;
        var start = 0;
        
        foreach (var val in hex.ToByteArray()) res += val*(int)Math.Pow(256, start++);
        
        return res;
    }

    public static DateTime UnixSecondsToDateTime(this double unixTimeStamp)
    {
        return (unixTimeStamp * 1000).UnixMilliSecondsToDateTime(); 
    }

    public static DateTime UnixMilliSecondsToDateTime(this double unixTimeStamp)
    {
        DateTime dateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }

    public static string CreateCommand(string cmd, string model, string sid, int short_id, Dictionary<string, object> data)
    {
        var dict = new Dictionary<string, object>()
        {
            { "cmd", cmd},
            { "model", model},
            { "sid", sid},
            { "short_id", short_id},
            { "data", JsonSerializer.Serialize(data) },
        };

        return JsonSerializer.Serialize(dict);
    }
    public static bool ParseString(this JsonObject jObject, string key, out string s)
    {
        if (jObject[key] != null)
        {
            s = jObject[key].ToString();
            return true;
        }

        s = null;
        return false;
    }
    public static bool ParseInt(this JsonObject jObject, string key, out int i)
    {
        if (jObject[key] != null && int.TryParse(jObject[key].ToString(), out int f1))
        {
            i = f1;
            return true;
        }

        i = 0;
        return false;
    }
    
    public static bool ParseFloat(this JsonObject jObject, string key, out float f)
    {
        if (jObject[key] != null && float.TryParse(jObject[key].ToString(), out float f1))
        {
            f = f1;
            return true;
        }

        f = 0;
        return false;
    }
    
    public static float? ParseVoltage(this JsonObject jObject)
    {
        if (jObject.ParseFloat("voltage", out float v))
        {
            return v / 1000;
        }

        return null;
    }
    public static string DecodeMacAddress(this string mac)
    {
        var chunks = Enumerable
            .Range(0, mac.Length / 2)
            .Select(i => mac.Substring(i * 2, 2))
            .Reverse();

        return string.Join(":", chunks);
    }

    public static IEnumerable<int> EnumToIntegers<T>()
    {
        return ((T[])Enum.GetValues(typeof(T))).Select(x => Convert.ToInt32(x));
    }
}
