using System;
using MiHomeLib.Transport;

namespace MiHomeLib.MultimodeGateway;

public abstract class MultimodeGateway2 : MultimodeGatewayBase
{
    public MultimodeGateway2(string ip, string token, string did, int port) : base(ip, token, did, port) { }

    // For unit tests only
    internal MultimodeGateway2(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer) : base(did, miioTransport, mqttTransport, devicesDiscoverer)
    {
        _did = did;
    }

    public enum AccessModeValue
    {
        LAN = 0,
        Wireless2G = 1,
        Wireless5G = 2,
    }

    /// <summary>
    /// Gateway connection type: LAN, Wireless 2G, Wireless 5G
    /// </summary>
    public AccessModeValue AccessMode
    {
        get => (AccessModeValue)ushort.Parse(GetMiotProperty(2, 1, _did));
        set => SetMiotProperty(2, 1, _did, value);
    }

    /// <summary>
    /// Do not distirb mode enable/disable, affects only led indicator glow in specific hours
    /// </summary>
    public bool DoNotDisturbModeEnabled
    {
        get => int.Parse(GetMiotProperty(6, 1, _did)) == 1;

        set => SetMiotProperty(6, 1, _did, value == true ? 1 : 0);
    }

    /// <summary>
    /// Time when led indicator will be off when do not disturb mode enabled
    /// </summary>
    public (ushort startHour, ushort startMinute, ushort endHour, ushort endMinute) DoNotDisturbEffectiveTime
    {
        get
        {
            var (start, end) = GetMiotProperty(6, 2, _did).Split('-') switch { var x => (x[0], x[1]) };
            var (startHour, startMinute) = start.Split(':') switch { var x => (ushort.Parse(x[0]), ushort.Parse(x[1])) };
            var (endHour, endMinute) = end.Split(':') switch { var x => (ushort.Parse(x[0]), ushort.Parse(x[1])) };

            return (startHour, startMinute, endHour, endMinute);
        }
        set
        {
            if (value.startHour > 23) throw new ArgumentOutOfRangeException("Starting hour should be in range 00-23");
            if (value.startMinute > 59) throw new ArgumentOutOfRangeException("Starting minute should be in range 00-59");
            if (value.endHour > 23) throw new ArgumentOutOfRangeException("Ending hour should be in range 00-23");
            if (value.endMinute > 59) throw new ArgumentOutOfRangeException("Ending minute should be in range 00-59");

            var str = $"{value.startHour:D2}:{value.startMinute:D2}-{value.endHour:D2}:{value.endMinute:D2}";

            SetMiotProperty(6, 2, _did, str);
        }
    }

    /// <summary>
    /// Led indicator brightness is in range 0 - 100
    /// </summary>
    public ushort LedIndicatorBrightness
    {
        get => ushort.Parse(GetMiotProperty(6, 3, _did));

        set
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException("Led indicator brightness should be in range 0 - 100");

            SetMiotProperty(6, 3, _did, value);
        }
    }
}
