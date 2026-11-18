using System;
using System.Threading;

namespace MiHomeLib.Helpers;

// Carries socket reception time through synchronous parsers and delayed discovery delivery.
internal static class MessageReception
{
    private static readonly AsyncLocal<DateTime?> _time = new();
    public static DateTime UtcNow => _time.Value ?? DateTime.UtcNow;

    public static void Run(DateTime receivedAt, Action process)
    {
        var previous = _time.Value;
        _time.Value = receivedAt;
        try { process(); }
        finally { _time.Value = previous; }
    }
}
