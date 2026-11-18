using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

[ProtocolDeviceModel(DeviceProtocol.LumiBle, MODEL, PDID)]
/// <summary>LYWSD03MMC BLE temperature and humidity sensor used by Lumi MQTT gateways.</summary>
public sealed class XiaomiBluetoothHygrothermograph2(string deviceId, ILoggerFactory loggerFactory) : ProtocolMappedDeviceBase(loggerFactory), IProtocolMappedBleDevice
{
    public const string MARKET_MODEL = "LYWSD03MMC";
    public const string MODEL = "miaomiaoce.sensor_ht";
    public const int PDID = 1371;

    private bool _hasTemperatureReport;
    private bool _hasHumidityReport;
    private bool _hasBatteryReport;

    public override string DeviceId { get; } = deviceId;
    public string Mac { get; private set; }
    public float Temperature { get; private set; }
    public float Humidity { get; private set; }
    public byte BatteryPercent { get; private set; }

    /// <summary>
    /// Raised on every report of the temperature, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Temperature"/>
    /// </summary>
    public event Func<float, Task> OnTemperatureChangeAsync = _ => Task.CompletedTask;
    /// <summary>
    /// Raised on every report of the relative humidity, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="Humidity"/>
    /// </summary>
    public event Func<float, Task> OnHumidityChangeAsync = _ => Task.CompletedTask;
    /// <summary>
    /// Raised on every report of the battery percent, even if the value has not changed. The previous value is passed as an argument,
    /// the new one is in <see cref="BatteryPercent"/>
    /// </summary>
    public event Func<byte, Task> OnBatteryPercentChangeAsync = _ => Task.CompletedTask;

    void IProtocolMappedBleDevice.SetMacAddress(string mac) => Mac = mac;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiBle(4100, value => SetAndRaise(Temperature, () =>
        {
            Temperature = BleValueDecoder.DecodeSignedTenths(value.ReadString());
            _hasTemperatureReport = true;
        }, OnTemperatureChangeAsync));
        map.LumiBle(4102, value => SetAndRaise(Humidity, () =>
        {
            Humidity = BleValueDecoder.DecodeSignedTenths(value.ReadString());
            _hasHumidityReport = true;
        }, OnHumidityChangeAsync));
        map.LumiBle(4106, value => SetAndRaise(BatteryPercent, () =>
        {
            BatteryPercent = BleValueDecoder.DecodeByte(value.ReadString(), "a battery value");
            _hasBatteryReport = true;
        }, OnBatteryPercentChangeAsync));
    }




    public override string ToString()
    {
        var temperature = _hasTemperatureReport ? $"{Temperature}°C" : "Unknown";
        var humidity = _hasHumidityReport ? $"{Humidity}%" : "Unknown";
        var battery = _hasBatteryReport ? $"{BatteryPercent}%" : "Unknown";
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Mac: {Mac}, " +
               $"Temperature: {temperature}, Humidity: {humidity}, Battery Percent: {battery}";
    }
}
