// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/chuangmi.plug.212a01
// Your contributions are appreciated
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public class MijiaSmartSocket2China : MiotGenericDevice
{
    public const string MARKET_MODEL = "ZNCZ07CM";
    public const string MODEL = "chuangmi.plug.212a01";
    
    private readonly int _uptimeSeconds;
    private readonly string _miioVersion;
    private readonly string _mac;
    private readonly string _firmwareVersion;
    private readonly string _hardware;
    private readonly WifiSettings _wifiSettings = new();
    private readonly NetifSettings _netifSettings = new();

    public MijiaSmartSocket2China(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal MijiaSmartSocket2China(IMiioTransport transport, int initialIdExternal = 0) : base(transport, initialIdExternal)
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
    /// Toggle power of the plug
    /// </summary>
    public void TogglePower() => SetMiotProperty(4, 6, true);
    
    /// <summary>
    /// Enable/disable led indicator of the plug
    /// </summary>
    public bool LedEnabled
    {
        get { return bool.Parse(GetMiotProperty(3, 1)); }
        set { SetMiotProperty(3, 1, value); }
    }

    // Native timer functions are not implemented, because it's super unclear how they work !
    // If you know how they work, please contribute


    /// <summary>
    /// Get plug temperature in celsius, range 0 - 255
    /// </summary>
    public byte Temperature { get { return byte.Parse(GetMiotProperty(2, 6)); } }

    /// <summary>
    /// Get electric current in ampere
    /// </summary>
    public ushort ElectricCurrent { get { return ushort.Parse(GetMiotProperty(5, 2)); } }

    /// <summary>
    /// Get plug voltage in volts
    /// </summary>
    public ushort Voltage { get { return ushort.Parse(GetMiotProperty(5, 3)); } }

    /// <summary>
    /// Get plug power in watts
    /// </summary>
    public float ElectricPower { get { return uint.Parse(GetMiotProperty(5, 6))/100.0f; } }

    /// <summary>
    /// Enable/disable over power protection
    /// </summary>
    public bool OverPowerEnabled
    {
        get { return bool.Parse(GetMiotProperty(7, 1)); }
        set { SetMiotProperty(7, 1, value); }
    }

    /// <summary>
    /// Get/Set threshold in watts for over power protections
    /// </summary>
    public ushort OverPowerThreshold
    {
        get { return ushort.Parse(GetMiotProperty(7, 2)); }
        set
        {
            if (value > 65525) throw new ArgumentOutOfRangeException("Over power threshold should be in range 0 - 65525");
            SetMiotProperty(7, 2, value);
        }
    }

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

