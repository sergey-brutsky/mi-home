// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/ijai.vacuum.v18
// Your contributions are appreciated
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public class XiaomiRobotVacuumMop3C : MiotGenericDevice
{
    public const string MARKET_MODEL = "B106CN";
    public const string MODEL = "ijai.vacuum.v18";

    public enum Status
    {
        Sleep = 0,
        Idle = 1,
        Paused = 2,
        GoCharging = 3,
        Charging = 4,
        Sweeping = 5,
        SweepingAndMopping = 6,
        Mopping = 7,
        Upgrading = 8,
    }
    public enum CleaningMode
    {
        Sweep = 0,
        SweepAndMop = 1,
        Mop = 2,
    }
    public enum SweepType
    {
        Global = 0,
        Mop = 1,
        Edge = 2,
        Area = 3,
        Point = 4,
        Remote = 5,
        Explore = 6,
        Room = 7,
        Floor = 8,
    }
    public enum CleaningType
    {
        GlobalSweep = 1,
        OnlySweep = 3,
        SweepAndMop = 5,
        OnlyMop = 6
    }
    private readonly int _uptimeSeconds;
    private readonly string _miioVersion;
    private readonly string _mac;
    private readonly string _firmwareVersion;
    private readonly string _hardware;
    private readonly WifiSettings _wifiSettings = new();
    private readonly NetifSettings _netifSettings = new();

    // Initialize requests from Random value in order to avoid overlapping requests with the same id    
    public XiaomiRobotVacuumMop3C(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiRobotVacuumMop3C(IMiioTransport transport, int initialIdExternal = 0) : base(transport, initialIdExternal)
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("miIO.info", string.Empty));
        var values = JsonNode
            .Parse(response)["result"]
            .Deserialize<Dictionary<string, object>>()
            .ToDictionary(x => x.Key, x => x.Value.ToString());

        _uptimeSeconds = int.Parse(values["uptime"]);
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
            Freq = int.Parse(apValues["freq"]),
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
    /// Returns vacuum battery percentage
    /// </summary>
    /// <returns>value from range 0 - 100</returns>
    public ushort GetBatteryPercent() => ushort.Parse(GetMiotProperty(3, 1));

    /// <summary>
    /// Returns vacuum status
    /// </summary>
    /// <returns>vacuum status: idle, sleep, charging, etc</returns>
    public Status GetStatus() => (Status)ushort.Parse(GetMiotProperty(2, 1));

    /// <summary>
    /// Set up cleaning mode of the vacuum
    /// </summary>
    public void SetCleaningMode(CleaningMode cleaningMode) => SetMiotProperty(2, 4, cleaningMode);

    /// <summary>
    /// Returns cleaning mode of the vacuum
    /// </summary>
    /// <returns>Sweep, Mop, SweepAndMop</returns>
    public CleaningMode GetCleaningMode() => (CleaningMode)short.Parse(GetMiotProperty(2, 4));

    /// <summary>
    /// Set sweep type for vacuum
    /// </summary>
    /// <param name="sweepType">Global, Mop, Edge, etc</param>
    public void SetSweepType(SweepType sweepType) => SetMiotProperty(2, 8, sweepType);

    /// <summary>
    /// Get type of cleaning sweep
    /// </summary>
    /// <returns>Global, Mop, Edge, etc</returns>
    public SweepType GetSweepType() => (SweepType)ushort.Parse(GetMiotProperty(2, 8));

    /// <summary>
    /// Find the vacuum via voice search
    /// </summary>
    public void FindMe() => SetMiotProperty(4, 1);

    /// <summary>
    /// Start cleaning with selected mode
    /// </summary>
    /// <param name="ct">Sweep Only, Mop Only, Sweep And Mop, etc</param>
    public void StartCleaning(CleaningType ct) => CallMiotAction(2, (int)ct);

    /// <summary>
    /// Stop cleaning (only stop !, no return to the dock station)
    /// </summary>
    public void StopCleaning() => CallMiotAction(2, 2);

    /// <summary>
    /// Go to the dock station for charging
    /// </summary>
    public void GoCharging() => CallMiotAction(3, 1);

    /// <summary>
    /// Set vacuum voice level
    /// </summary>
    /// <param name="level">Voice level should be in range 0 - 10 </param>
    public void SetVoiceLevel(ushort level)
    {
        if (level < 0 || level > 10) throw new ArgumentOutOfRangeException(nameof(level));

        SetMiotProperty(4, 2, level);
    }

    /// <summary>
    /// Get vacuum voice level
    /// Voice level is in range 0 - 10, with step 1
    /// </summary>
    public ushort GetVoiceLevel() => ushort.Parse(GetMiotProperty(4, 2));

    /// <summary>
    /// Get vacuum no disturbing settings
    /// </summary>
    public NoDisturbingSettings GetNoDisturbingSettings()
    {
        var str = GetMiotProperty(12, 7);
        var arr = str.Substring(1, str.Length - 2).Split(',').Select(x => short.Parse(x)).ToArray();
        return new NoDisturbingSettings
        {
            IsNoDisturbingEnabled = arr[0] == 1,
            DndStartingHour = arr[1],
            DndStartingMinute = arr[2],
            DndEndingHour = arr[3],
            DndEndingMinute = arr[4]
        };
    }

    /// <summary>
    /// Get sweep consumables info
    /// </summary>
    /// <returns>Data about supported consumables like main brush/side brush/mop/hepa filter etc</returns>
    public SweepConsumables GetSweepConsumablesInfo()
    {
        var values = GetMiotProperties([(7, 8), (7, 9), (7, 10), (7, 11), (7, 12), (7, 13), (7, 14), (7, 15)]);

        return new SweepConsumables()
        {
            SideBrushLifePercent = ushort.Parse(values[0]),
            SideBrushLifeHours = uint.Parse(values[1]),
            MainBrushLifePercent = ushort.Parse(values[2]),
            MainBrushLifeHours = uint.Parse(values[3]),
            HepaFilterLifePercent = ushort.Parse(values[4]),
            HepaFilterLifeHours = uint.Parse(values[5]),
            MopLifePercent = ushort.Parse(values[6]),
            MopLifeHours = uint.Parse(values[7])
        };
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
                $" Freq: {_wifiSettings.Freq}," +
                $" Ip: {_netifSettings.Ip}," +
                $" Mask: {_netifSettings.Mask}," +
                $" Gateway: {_netifSettings.Gateway}";
    }
    private class WifiSettings
    {
        public string Ssid { get; internal set; }
        public string Bssid { get; internal set; }
        public int Rssi { get; internal set; }
        public int Freq { get; internal set; }
    }
    private class NetifSettings
    {
        public string Ip { get; internal set; }
        public string Mask { get; internal set; }
        public string Gateway { get; internal set; }
    }
    public class NoDisturbingSettings
    {
        public bool IsNoDisturbingEnabled { get; internal set; }
        public int DndStartingHour { get; internal set; }
        public int DndStartingMinute { get; internal set; }
        public int DndEndingHour { get; internal set; }
        public int DndEndingMinute { get; internal set; }
    }
    public class SweepConsumables
    {    
        public uint SideBrushLifeHours { get; internal set; }
        public ushort SideBrushLifePercent { get; internal set; }
        public ushort MainBrushLifePercent { get; internal set; }
        public uint MainBrushLifeHours { get; internal set; }
        public ushort HepaFilterLifePercent { get; internal set; }
        public uint HepaFilterLifeHours { get; internal set; }
        public ushort MopLifePercent { get; internal set; }
        public uint MopLifeHours { get; internal set; }
    }
}

