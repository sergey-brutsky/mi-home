using System;

namespace MiHomeLib.Helpers;

internal static class UnixTimeExtensions
{
    public static DateTime UnixSecondsToDateTime(this double unixTimeStamp)
    {
        return UnixMilliSecondsToDateTime(unixTimeStamp * 1000);
    }

    public static DateTime UnixMilliSecondsToDateTime(this double unixTimeStamp)
    {
        DateTime dateTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
}
