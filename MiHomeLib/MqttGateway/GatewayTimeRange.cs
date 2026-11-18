using System;
using System.Globalization;

namespace MiHomeLib.MqttGateway;

internal static class GatewayTimeRange
{
    public static (ushort startHour, ushort startMinute, ushort endHour, ushort endMinute) Parse(string value)
    {
        var parts = value.Split('-', ':');
        if (parts.Length != 4) throw new FormatException("Expected hh:mm-hh:mm.");
        var result = (ushort.Parse(parts[0], CultureInfo.InvariantCulture), ushort.Parse(parts[1], CultureInfo.InvariantCulture),
            ushort.Parse(parts[2], CultureInfo.InvariantCulture), ushort.Parse(parts[3], CultureInfo.InvariantCulture));
        Format(result.Item1, result.Item2, result.Item3, result.Item4);
        return result;
    }

    public static string Format(ushort startHour, ushort startMinute, ushort endHour, ushort endMinute)
    {
        if (startHour > 23) throw new ArgumentOutOfRangeException(nameof(startHour));
        if (startMinute > 59) throw new ArgumentOutOfRangeException(nameof(startMinute));
        if (endHour > 23) throw new ArgumentOutOfRangeException(nameof(endHour));
        if (endMinute > 59) throw new ArgumentOutOfRangeException(nameof(endMinute));
        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}-{2:D2}:{3:D2}", startHour, startMinute, endHour, endMinute);
    }
}
