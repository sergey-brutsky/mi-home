using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.XiaomiGateway3.Devices;

public abstract class ZigBeeDevice : XiaomiGateway3SubDevice
{
    protected const string RES_NAME = "res_name";
    protected const string VALUE = "value";
    private const string LQI_RES_NAME = "8.0.2007";
    private const string CHIP_TEMPERATURE_RES_NAME = "8.0.2006";
    public byte LinqQuality { get; set; }
    public byte ChipTemperature { get; set; }
    /// <summary>
    /// Catching event when linq quality is changed (0-255)
    /// </summary>
    public event Func<byte, Task> OnLinkQualityChangeAsync = (_) => Task.CompletedTask;
    /// <summary>
    /// Old value chip temperature percent 0-100°C (1 step) passed as an argument
    /// </summary>
    public event Func<byte, Task> OnChipTemperatureChangeAsync = (_) => Task.CompletedTask;
    protected Dictionary<string, Action<JsonElement>> Actions = [];
    public ZigBeeDevice(string did, ILoggerFactory loggerFactory) : base(did, loggerFactory)
    {
        Actions = new() {
            {LQI_RES_NAME, async x =>
                {
                   var oldVal = LinqQuality;
                    LinqQuality = (byte)x.GetInt32();
                    await OnLinkQualityChangeAsync(oldVal);
                }
            },
            {CHIP_TEMPERATURE_RES_NAME, async x =>
                {
                   var oldVal = ChipTemperature;
                    ChipTemperature = (byte)x.GetInt32();
                    await OnChipTemperatureChangeAsync(oldVal);
                }
            },
        };
    }
    protected internal virtual string[] GetProps()
    {
        // order of properties does matter ! don't change it
        return ["lqi", "chip_temperature"];
    }
    protected internal virtual void SetProps(JsonNode[] props)
    {
        LinqQuality = (byte)props[0].GetValue<int>();
        ChipTemperature = (byte)props[1].GetValue<int>();
    }
    protected internal override void ParseData(string data)
    {
        var listProps = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(data);

        foreach (var prop in listProps)
        {
            if (prop.ContainsKey(RES_NAME) && Actions.ContainsKey(prop[RES_NAME].GetString()))
            {
                Actions[prop[RES_NAME].ToString()](prop[VALUE]);
            }
            else
            {
                _logger.LogWarning($"Property '{prop[RES_NAME].GetString()}' is not supported for this device yet. Please contribute to support.");
            }
        }
    }
    public override string ToString()
    {
        return
            $"ChipTemperature {ChipTemperature}°C, " +
            $"LinqQuality: {LinqQuality}dBm, " +
            $"Last seen: {LastTimeMessageReceived}";
    }
}
