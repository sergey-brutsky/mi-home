// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/zhimi.airp.rma3
// Your contributions are appreciated
using System;
using System.Globalization;
using MiHomeLib.Contracts;

namespace MiHomeLib.MiioDevices;

public enum FaultState { NoFaults = 0, Motor = 2, Pm25Sensor = 3 }
public enum OperationMode { Auto = 0, Sleep = 1, Favorite = 2 }
public enum ScreenBrightness { Off = 0, Bright = 1, Brightest = 2 }
public enum AirQuality { Excellent = 0, Good = 1, Moderate = 2, Poor = 3, HeavyPollution = 4, Hazardous = 5 }

public class XiaomiSmartAirPurifier4Lite : MiotGenericDevice
{
    public const string MARKET_MODEL = "AC-M17-SC";
    public const string MODEL = "zhimi.airp.rma3";

    public XiaomiSmartAirPurifier4Lite(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiSmartAirPurifier4Lite(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal) {}

    /// <summary>
    /// Get power state on/off
    /// </summary>
    public bool IsTurnedOn { get { return bool.Parse(GetMiotProperty(2, 1)); } }

    /// <summary>
    /// Turn on the air purifier
    /// </summary>
    public void TurnOn() => SetMiotProperty(2, 1, true);

    /// <summary>
    /// Turn off the air purifier
    /// </summary>
    public void TurnOff() => SetMiotProperty(2, 1, false);

    /// <summary>
    /// Toggle power on the air purifier
    /// </summary>
    public void TogglePower() => CallMiotAction(2, 1, []);

    /// <summary>
    /// Get device fault state
    /// </summary>
    public FaultState DeviceFaultState
    {
        get { return (FaultState)byte.Parse(GetMiotProperty(2, 2)); }
    }

    /// <summary>
    /// Get/Set operation mode: Auto, Sleep, Favorite
    /// </summary>
    public OperationMode Mode
    {
        get { return (OperationMode)byte.Parse(GetMiotProperty(2, 4)); }
        set { SetMiotProperty(2, 4, value); }
    }

    /// <summary>
    /// Get relative humidity percentage
    /// </summary>
    public byte Humidity { get { return byte.Parse(GetMiotProperty(3, 1)); } }

    /// <summary>
    /// Get PM2.5 density in µg/m³
    /// </summary>
    public ushort Pm25Density { get { return ushort.Parse(GetMiotProperty(3, 4)); } }

    /// <summary>
    /// Get temperature in degrees Celsius
    /// </summary>
    public float Temperature { get { return float.Parse(GetMiotProperty(3, 7), CultureInfo.InvariantCulture); } }

    /// <summary>
    /// Get air quality level
    /// </summary>
    public AirQuality AirQuality
    {
        get { return (AirQuality)byte.Parse(GetMiotProperty(3, 8)); }
    }

    /// <summary>
    /// Get filter life level percentage
    /// </summary>
    public byte FilterLifeLevel { get { return byte.Parse(GetMiotProperty(4, 1)); } }

    /// <summary>
    /// Get filter used time in hours
    /// </summary>
    public ushort FilterUsedTime { get { return ushort.Parse(GetMiotProperty(4, 3)); } }

    /// <summary>
    /// Get filter left time in days
    /// </summary>
    public ushort FilterLeftTime { get { return ushort.Parse(GetMiotProperty(4, 4)); } }

    /// <summary>
    /// Reset filter life
    /// </summary>
    public void ResetFilterLife() => CallMiotAction(4, 1, []);

    /// <summary>
    /// Enable/disable buzzer
    /// </summary>
    public bool BuzzerEnabled
    {
        get { return bool.Parse(GetMiotProperty(6, 1)); }
        set { SetMiotProperty(6, 1, value); }
    }

    /// <summary>
    /// Get/Set screen brightness: Off, Bright, Brightest
    /// </summary>
    public ScreenBrightness ScreenBrightness
    {
        get { return (ScreenBrightness)byte.Parse(GetMiotProperty(7, 2)); }
        set { SetMiotProperty(7, 2, value); }
    }

    /// <summary>
    /// Enable/disable child lock
    /// </summary>
    public bool ChildLockEnabled
    {
        get { return bool.Parse(GetMiotProperty(8, 1)); }
        set { SetMiotProperty(8, 1, value); }
    }

    /// <summary>
    /// Get motor speed in RPM
    /// </summary>
    public ushort MotorSpeedRpm { get { return ushort.Parse(GetMiotProperty(9, 1)); } }

    /// <summary>
    /// Get/Set fan level, range [0, 14]
    /// </summary>
    public byte FanLevel
    {
        get { return byte.Parse(GetMiotProperty(11, 1)); }
        set
        {
            if (value > 14) throw new ArgumentOutOfRangeException("Fan level should be in range 0 - 14");
            SetMiotProperty(11, 1, value);
        }
    }

    /// <summary>
    /// Toggle operation mode
    /// </summary>
    public void ToggleMode() => CallMiotAction(9, 1, []);

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
                $" Primary: {Wifi.Freq}," +
                $" Ip: {Network.Ip}," +
                $" Mask: {Network.Mask}," +
                $" Gateway: {Network.Gateway}";
    } 
}