// Support for this device has been partially implemented on top of https://home.miot-spec.com/spec/miaomiaoce.sensor_ht.t9
using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.MqttGateway.Devices;

/// <summary>
/// Xiaomi TH Sensor 3 (MJWSD05MMC)
/// </summary>
public class XiaomiBluetoothHygrothermograph3 : BleDevice
{
    public const string MARKET_MODEL = "MJWSD05MMC";
    public const string MODEL = "miaomiaoce.sensor_ht.t9";
    public const int PDID = 10290;
    private const int TEMPERATURE_PIID = 1001;
    private const int HUMIDITY_PIID = 1002;
    private const int BATTERY_PIID = 1003;

    public XiaomiBluetoothHygrothermograph3(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        EidToActions.Add(BATTERY_PIID, async value =>
        {
            var oldBatteryPercent = BatteryPercent;

            if(byte.TryParse(value, out var result))
            {
                BatteryPercent = result;
                await OnBatteryPercentAsync(oldBatteryPercent);
            }
        });

        EidToActions.Add(TEMPERATURE_PIID, async value =>
        {
            var oldTemperature = Temperature;
            
            if(float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                Temperature = result;
                await OnTemperatureChangeAsync(oldTemperature);
            }
        });

        EidToActions.Add(HUMIDITY_PIID, async value =>
        {
            var oldHumidity = Humidity;
            
            if(byte.TryParse(value, out var result))
            {
                Humidity = result;
                await OnHumidityChangeAsync(oldHumidity);
            }
        });
    }

    protected internal override void ParseData(string data)
    {
        JsonObject @params = JsonNode.Parse(data).AsObject();
        var piid = @params["piid"].GetValue<int>();        

        if(EidToActions.ContainsKey(piid))
        {
            EidToActions[piid](@params["value"].ToString());
        }
    }

    public float Temperature { get; set; }    
    public byte Humidity { get; set; }
    public byte BatteryPercent { get; set; }

    /// <summary>
    /// Old value temperature -30 - 100°C (0.1 step) passed as an argument
    /// </summary>
    public event Func<float, Task> OnTemperatureChangeAsync = (_) => Task.CompletedTask;

    /// <summary>
    /// Old value relative humidity 0-100% (1% step) passed as an argument
    /// </summary>
    public event Func<byte, Task> OnHumidityChangeAsync = (_) => Task.CompletedTask;
    
    /// <summary>
    /// Old value battery 0-100% (1% step) passed as an argument
    /// </summary>
    public event Func<byte, Task> OnBatteryPercentAsync = (_) => Task.CompletedTask;

    public override string ToString()
    {
        return GetBaseInfo(MARKET_MODEL, MODEL) +
                $"Temperature: {Temperature}°C, " +
                $"Humidity: {Humidity}%, " +
                $"Battery Percent: {BatteryPercent}% ";
    }
}
