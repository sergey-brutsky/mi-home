// Partial support for this device has been implemented on top of https://home.miot-spec.com/s/lumi.gateway.aqcn02
// Your contributions are appreciated
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MiHomeLib.Contracts;
using MiHomeLib.MiioDevices;
using MiHomeLib.Transport;

namespace MiHomeLib.MqttGateway;

/// <summary>
/// Aqara Hub E1 (China) ZHWG16LM
/// </summary>
public class AqaraHubE1China : MqttGatewayBase, IDisposable
{
    public const string MARKET_MODEL = "ZHWG16LM";
    public const string MODEL = "lumi.gateway.aqcn02";
    public AqaraHubE1China(string ip, string token, string did, int port = 1883) :
        this(
                did,
                new MiioTransport(ip, token),
                new MqttDotNetTransport(ip, port, [.. _zigbeeTopics], _zigbeeCommandsTopic, _loggerFactory),
                new CommonDevicesDiscoverer(ip, 23, "root", "/ # ", "/data/zigbee/device.info", "")
        )
    { }
    internal AqaraHubE1China(string did, IMiioTransport miioTransport, IMqttTransport mqttTransport, IDevicesDiscoverer devicesDiscoverer) : base(did, miioTransport, mqttTransport, devicesDiscoverer)
    {        
    }

    // This gateway doesn't suppport BLE devices, no need to discover them
    protected sealed override Task DiscoverBleDevices() => Task.CompletedTask; 

    /// <summary>
    /// Enable night mode (at night hours led will be turned off)
    /// </summary>
    public bool NightModeEnabled
    {
        get => short.Parse(GetMiotProperty(6, 1, _did)) == 1;

        set => SetMiotProperty(6, 1, _did, value ? 1 : 0);
    }

    /// <summary>
    /// Setup night mode hours in the format hh:mm-hh:mm
    /// </summary>
    public (byte startHour, byte startMinute, byte endHour, byte endMinute) NigtModeEffectiveHours
    {
        get
        {
            var (start, end) = GetMiotProperty(6, 2, _did).Split('-') switch { var x => (x[0], x[1]) };
            var (startHour, startMinute) = start.Split(':') switch { var x => (byte.Parse(x[0]), byte.Parse(x[1])) };
            var (endHour, endMinute) = end.Split(':') switch { var x => (byte.Parse(x[0]), byte.Parse(x[1])) };

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
    /// Enable hotspot mode (act as a wifi repiter)
    /// </summary>
    public bool HotspotModeEnabled
    {
        get => bool.Parse(GetMiotProperty(19, 1, _did));

        set => SetMiotProperty(19, 1, _did, value);
    }

    /// <summary>
    /// Change hotspot name (SSID)
    /// </summary>
    public string HotspotName
    {
        get => GetMiotProperty(19, 2, _did);

        set
        {
            if (value.Length > 32) throw new ArgumentOutOfRangeException("SSID name should be within range 0-32 symbols");
            
            SetMiotProperty(19, 2, _did, value);
        }
    }

    /// <summary>
    /// Change hotspot password 0-50 symbols
    /// </summary>
    public string HotspotPassword
    {
        get => GetMiotProperty(19, 3, _did);

        set
        {
            if (value.Length > 50) throw new ArgumentOutOfRangeException("Hotspot password should be within range 0-50 symbols");
            
            SetMiotProperty(19, 3, _did, value);
        }
    }

    /// <summary>
    /// Hotspot clients list in the format [(ip, mac, name)]
    /// </summary>
    public (string ip, string mac, string name)[] HotspotClients
    {
        get
        {
            var validJson = GetMiotProperty(19, 4, _did).Replace('\'', '\"');

            return [.. JsonNode
                .Parse(validJson)
                .AsArray()
                .AsEnumerable()
                .Select(x => (x["ip"].ToString(), x["mac"].ToString(), x["name"].ToString()))];
        }
    }

    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}
