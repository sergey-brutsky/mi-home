using System;
using System.Collections.Generic;
using System.Linq;

namespace MiHomeLib.Devices;

/// <summary>One event raised by a gateway sub-device.</summary>
public sealed class DeviceEvent
{
    internal DeviceEvent(GatewaySubDeviceBase device, string eventName, IReadOnlyList<object> arguments, DateTime timestampUtc)
    {
        Device = device;
        EventName = eventName;
        Arguments = Array.AsReadOnly(arguments.ToArray());
        TimestampUtc = timestampUtc;
    }

    /// <summary>The device that raised the event.</summary>
    public GatewaySubDeviceBase Device { get; }

    /// <summary>Name of the device event member, e.g. "OnVoltageChangeAsync".</summary>
    public string EventName { get; }

    /// <summary>
    /// Arguments passed to the device event handlers. For value changes this is the previous value;
    /// the new value is available from the device property.
    /// </summary>
    public IReadOnlyList<object> Arguments { get; }

    /// <summary>UTC time the library received the message that caused the event.</summary>
    public DateTime TimestampUtc { get; }

    public override string ToString() => $"{Device.DeviceId} {EventName}({string.Join(", ", Arguments)})";
}
