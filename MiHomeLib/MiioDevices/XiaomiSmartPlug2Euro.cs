// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/cuco.plug.v2eur
// Your contributions are appreciated
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public class XiaomiSmartPlug2Euro : MiotGenericDevice
{
    public const string MARKET_MODEL = "ZNCZ302KK";
    public const string MODEL = "cuco.plug.v2eur";
    public enum PowerOnState
    {
        RestoreLast = 0,
        TurnOnPower = 1,
        TurnOffPower = 2
    }
    public enum CycleState
    {
        TurnOff = 0,
        TurnOn = 1,
        RestoreLast = 2
    }
    public enum FaultStateValue
    {
        NoFaults = 0,
        OverTemperature = 1,
        Overloaded = 2
    } 
    private readonly int _uptimeSeconds;
    private readonly string _miioVersion;
    private readonly string _mac;
    private readonly string _firmwareVersion;
    private readonly string _hardware;
    private readonly WifiSettings _wifiSettings = new();
    private readonly NetifSettings _netifSettings = new();

    public XiaomiSmartPlug2Euro(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiSmartPlug2Euro(IMiioTransport transport, int initialIdExternal = 0) : base(transport, initialIdExternal)
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("miIO.info", string.Empty));
        var values = JsonNode
            .Parse(response)["result"]
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        _uptimeSeconds = int.Parse(values["life"]);
        _miioVersion = values["miio_ver"].ToString();
        _mac = values["mac"].ToString();
        _firmwareVersion = values["fw_ver"].ToString();
        _hardware = values["hw_ver"].ToString();

        var apValues = JsonNode
            .Parse(values["ap"].ToString())
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        _wifiSettings = new WifiSettings()
        {
            Ssid = apValues["ssid"].ToString(),
            Bssid = apValues["bssid"].ToString(),
            Rssi = int.Parse(apValues["rssi"]),
            Primary = int.Parse(apValues["primary"]),
        };

        var netifValues = JsonNode
            .Parse(values["netif"].ToString())
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        _netifSettings = new NetifSettings()
        {
            Ip = netifValues["localIp"].ToString(),
            Mask = netifValues["mask"].ToString(),
            Gateway = netifValues["gw"].ToString(),
        };
    }

    /// <summary>
    /// Get power state on/off
    /// </summary>
    public bool IsTurnedOn { get { return bool.Parse(GetMiotProperty(2, 1)); } }

    /// <summary>
    /// Turn on power on the plug
    /// </summary>
    public void TurnOn() => SetMiotProperty(2, 1, true);

    /// <summary>
    /// Turn off power on the plug
    /// </summary>
    public void TurnOff() => SetMiotProperty(2, 1, false);

    /// <summary>
    /// Toggle power on the plug
    /// </summary>
    public void TogglePower() => CallMiotAction(2, 1, []);

    /// <summary>
    /// Enter the state after electricity outage: restore last state, turn on, turn off    
    /// </summary>
    public PowerOnState DefaultPowerOnState
    {
        get { return (PowerOnState)byte.Parse(GetMiotProperty(2, 2)); }
        set { SetMiotProperty(2, 2, value); }
    }


    /// <summary>
    /// Get fault state of the plug
    /// </summary>
    public FaultStateValue DeviceFaultState
    {
        get { return (FaultStateValue)byte.Parse(GetMiotProperty(2, 3)); }
    }

    /// <summary>
    /// Enable/disable charging protection functionality
    /// </summary>
    public bool ChargingProtectionEnabled
    {
        get { return bool.Parse(GetMiotProperty(4, 1)); }
        set { SetMiotProperty(4, 1, value); }
    }

    /// <summary>
    /// Charging protection power threshold in watt
    /// If power is less than this threshold (for specific amount of time) plug will turn off automatically
    /// </summary>
    public ushort ChargingProtectionPowerThreshold
    {
        get { return ushort.Parse(GetMiotProperty(4, 2)); }
        set
        {
            if(value < 2 || value > 1200) throw new ArgumentOutOfRangeException("Threshold should be in range 2 - 1200");

            SetMiotProperty(4, 2, value);
        }
    }

    /// <summary>
    /// Charging protection time in minutes.
    /// If power is less than specific threshold for this amount of minutes the plug will turn off automatically
    /// </summary>
    public ushort ChargingProtectionTime
    {
        get { return ushort.Parse(GetMiotProperty(4, 3)); }
        set
        {
            if(value < 1 || value > 300) throw new ArgumentOutOfRangeException("Time should be in range 1 - 300 minutes");
            SetMiotProperty(4, 3, value);
        }
    }

    /// <summary>
    /// Enable/disable cycle
    /// </summary>
    public bool CyclingEnabled
    {
        get { return bool.Parse(GetMiotProperty(5, 1)); }
        set { SetMiotProperty(5, 1, value); }
    }

    /// <summary>
    /// Enable/disable cycling
    /// This plug can turn on/off specific amount of times.<br/>
    /// minutesOn - amount of minutes in "turn on" state in range 0 - 1439.<br/>
    /// minutesOff - amount of minutes in "turn off" state in range 0 - 1439.<br/>
    /// cycleState - what state should be in the end of cycle: turn off, turn on, restore last<br/>
    /// cyclesCount - amount of cycles in range 0 - 10 (0 - unlimited)
    /// </summary>    
    public (ushort minutesOn, ushort minutesOff, CycleState cycleState, byte cyclesCount) CycleData
    {
        get
        {
            var chunks = GetMiotProperty(5, 2).Split(';');
            return (ushort.Parse(chunks[0]), ushort.Parse(chunks[1]),(CycleState)int.Parse(chunks[2]), byte.Parse(chunks[3]));
        }

        set
        {
            if (value.minutesOn < 1 || value.minutesOn > 1439) throw new ArgumentOutOfRangeException("Minutes on should be in range 1 - 1439");
            if (value.minutesOff < 1 || value.minutesOff > 1439) throw new ArgumentOutOfRangeException("Minutes off should be in range 1 - 1439");
            if (value.cyclesCount > 10) throw new ArgumentOutOfRangeException("Cycle cound be in range 0 - 10");

            var str = $"{value.minutesOn};{value.minutesOff};{(byte)value.cycleState};{value.cyclesCount}";
            SetMiotProperty(5, 2, string.Empty, str);
        }
    }

    /// <summary>
    /// Enable/disable lock of physical button on the plug
    /// </summary>
    public bool PhysicalControlBlocked
    {
        get { return bool.Parse(GetMiotProperty(7, 1)); }
        set { SetMiotProperty(7, 1, value); }
    }

    /// <summary>
    /// Enable/disable max power limit for the plug
    /// After reaching this limit plug will turn off
    /// </summary>
    public bool MaxPowerLimitEnabled
    {
        get { return bool.Parse(GetMiotProperty(9, 1)); }
        set { SetMiotProperty(9, 1, value); }
    }

     /// <summary>
    /// Max power threshold in watts, range 300 - 3600, step 100
    /// Works only if max power limit is enabled
    /// </summary>
    public ushort MaxPowerThreshold
    {
        get { return ushort.Parse(GetMiotProperty(9, 2)); }
        set
        {
            if(value % 100 != 0) throw new ArgumentOutOfRangeException("Threshold must be a multiple of 100");
            if(value < 300 || value > 3600) throw new ArgumentOutOfRangeException("Threshold should be in range 300 - 3600");

            SetMiotProperty(9, 2, value);
        }
    }

    /// <summary>
    /// Get current power in watts, range 0 - 5000
    /// </summary>
    public ushort CurrentElectricPower { get { return ushort.Parse(GetMiotProperty(11, 2)); } }

    /// <summary>
    /// Enable/disable led indicator of the plug
    /// </summary>
    public bool LedEnabled
    {
        get { return bool.Parse(GetMiotProperty(13, 1)); }
        set { SetMiotProperty(13, 1, value); }
    }

    //WARNING: Delay functions not implemented according to spec !
    // Because on this version of firmware 1.0.6@ESP32C3
    // They don't work :(
    public override string ToString()
    {
        return $"Model: {MARKET_MODEL} {MODEL}," +
                $" Uptime: {_uptimeSeconds} seconds," +
                $" Miio Version: {_miioVersion}," +
                $" Mac: {_mac}," +
                $" Firmware Version: {_firmwareVersion}," +
                $" Hardware: {_hardware}," +
                $" SSID: {_wifiSettings.Ssid}," +
                $" BSSID: {_wifiSettings.Bssid}," +
                $" RSSI: {_wifiSettings.Rssi}," +
                $" Primary: {_wifiSettings.Primary}," +
                $" Ip: {_netifSettings.Ip}," +
                $" Mask: {_netifSettings.Mask}," +
                $" Gateway: {_netifSettings.Gateway}";
    }
    private class WifiSettings
    {
        public string Ssid { get; internal set; }
        public string Bssid { get; internal set; }
        public int Rssi { get; internal set; }
        public int Primary { get; internal set; }
    }
    private class NetifSettings
    {
        public string Ip { get; internal set; }
        public string Mask { get; internal set; }
        public string Gateway { get; internal set; }
    }    
}

