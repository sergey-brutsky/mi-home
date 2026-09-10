// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/xiaomi.vacuum.c102gl
// Your contributions are appreciated
using System;
using MiHomeLib.Contracts;

namespace MiHomeLib.MiioDevices;

public class XiaomiRobotVacuumX20Plus : MiotGenericDevice
{
    public const string MARKET_MODEL = "X20 Plus";
    public const string MODEL = "xiaomi.vacuum.c102gl";
    private const int VACUUM_SIID = 2;
    private const int START_SWEEP_AIID = 1;
    private const int PAUSE_AIID = 2;
    private const int BATTERY_SIID = 3;
    private const int START_CHARGE_AIID = 1;
    private const int VACUUM_EXTEND_SIID = 4;
    private const int STOP_CLEAN_AIID = 2;

    private const int AUDIO_SIID = 7;
    private const int POSITION_AIID = 1;

    public XiaomiRobotVacuumX20Plus(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiRobotVacuumX20Plus(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal) {}

    /// <summary>
    /// Start cleaning
    /// </summary>
    public void StartCleaning() => CallMiotAction(VACUUM_SIID, START_SWEEP_AIID);

    /// <summary>
    /// Stop cleaning (stops the cleaning task entirely, not just pause)
    /// </summary>
    public void StopCleaning() => CallMiotAction(VACUUM_EXTEND_SIID, STOP_CLEAN_AIID);

    /// <summary>
    /// Pause cleaning
    /// </summary>
    public void Pause() => CallMiotAction(VACUUM_SIID, PAUSE_AIID);

    /// <summary>
    /// Go to the dock station for charging
    /// </summary>
    public void GoHome() => CallMiotAction(BATTERY_SIID, START_CHARGE_AIID);

    /// <summary>
    /// Find the vacuum via voice/sound notification (locate my robot)
    /// </summary>
    public void FindMe() => CallMiotAction(AUDIO_SIID, POSITION_AIID);

    public override string ToString()
    {
        return $"Model: {MARKET_MODEL} {MODEL}," +
                $" Uptime: {UptimeSeconds} seconds," +
                $" Miio Version: {MiioVersion}," +
                $" Mac: {Mac}," +
                $" Firmware Version: {FirmwareVersion}," +
                $" Hardware: {Hardware}," +
                $" SSID: {Wifi.Ssid}," +
                $" BSSID: {Wifi.Bssid}," +
                $" RSSI: {Wifi.Rssi}," +
                $" Freq: {Wifi.Freq}," +
                $" Ip: {Network.Ip}," +
                $" Mask: {Network.Mask}," +
                $" Gateway: {Network.Gateway}";
    }
}