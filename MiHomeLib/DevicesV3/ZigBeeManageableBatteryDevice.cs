using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MiHomeLib.Transport;

namespace MiHomeLib.DevicesV3;

public abstract class ZigBeeManageableBatteryDevice(string did, IMqttTransport mqttTransport, ILoggerFactory loggerFactory) : ZigBeeBatteryDevice(did, loggerFactory)
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    protected readonly IMqttTransport _mqttTransport = mqttTransport;
    protected void SendWriteCommand(string resName, int value)
    {
        var cmd = JsonSerializer.Serialize(new ZigbeeWriteCommand
        {
            Did = Did,
            Params = [
                new()  { ResName = resName, Value = value }
            ]
        }, _options);

        _mqttTransport.SendMessage(cmd);
    }

    protected void SendWriteCommand((int siid, int piid) res, int value)
    {
        var cmd = JsonSerializer.Serialize(new ZigbeeWriteCommand
        {
            Did = Did,
            MiSpec = [
                new()  { Siid = res.siid, Piid = res.piid, Value = value }
            ]
        }, _options);

        _mqttTransport.SendMessage(cmd);
    }

    public class ZigbeeWriteCommand
    {
        public string Cmd { get; } = "write";
        public string Did { get; internal set; }
        public List<ZigbeeWriteItem> Params { get; internal set; }
        public List<ZigbeeMiSpecWriteItem> MiSpec { get; internal set; }

        public class ZigbeeWriteItem
        {
            public string ResName { get; set; }
            public int Value { get; set; }
        }

        public class ZigbeeMiSpecWriteItem
        {
            public int Siid { get; set; }
            public int Piid { get; set; }
            public int Value { get; set; }
        }
    }
}
