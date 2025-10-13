// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/chuangmi.plug.212a01
// Your contributions are appreciated
using System;
using MiHomeLib.Transport;

namespace MiHomeLib.MiioDevices;

public class MijiaSmartSocket2China : MiotGenericDevice
{
    public const string MARKET_MODEL = "ZNCZ07CM";
    public const string MODEL = "chuangmi.plug.212a01";

    public MijiaSmartSocket2China(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal MijiaSmartSocket2China(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal)
    {}

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
                $" Uptime: {UptimeSeconds} seconds," +
                $" Miio Version: {MiioVersion}," +
                $" Mac: {Mac}," +
                $" Firmware Version: {FirmwareVersion}," +
                $" Hardware: {Hardware}," +
                $" SSID: {Wifi.Ssid}," +
                $" BSSID: {Wifi.Bssid}," +
                $" RSSI: {Wifi.Rssi}," +
                $" Ip: {Network.Ip}," +
                $" Mask: {Network.Mask}," +
                $" Gateway: {Network.Gateway}";
    } 
}

