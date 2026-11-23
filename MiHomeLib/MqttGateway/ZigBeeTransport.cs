using MiHomeLib.Helpers;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MiHomeLib.MqttGateway;

internal sealed class ZigBeeTransport(IMqttTransport mqttTransport)
{
    private readonly IMqttTransport _mqttTransport = mqttTransport;
    public Task SendConfirmedWriteCommandAsync(string did, string resName, int value)
    {
        if (_mqttTransport is not IMqttCommandTransport commands)
            throw new System.NotSupportedException("This MQTT transport does not support device write acknowledgements.");
        var message = JsonSerializer.Serialize(new ZigbeeWriteCommand
        {
            Did = did,
            Params = [new() { ResName = resName, Value = value }]
        }, MiHomeJsonContext.Default.ZigbeeWriteCommand);
        return commands.SendCommandAsync(message);
    }
    public void SendWriteCommand(string did, string resName, int value)
    {
        var cmd = JsonSerializer.Serialize(new ZigbeeWriteCommand
        {
            Did = did,
            Params = [
                new()  { ResName = resName, Value = value }
            ]
        }, MiHomeJsonContext.Default.ZigbeeWriteCommand);

        _mqttTransport.SendMessage(cmd);
    }
    public void SendWriteCommand(string did, (int siid, int piid) res, int value)
    {
        var cmd = JsonSerializer.Serialize(new ZigbeeWriteCommand
        {
            Did = did,
            MiSpec = [
                new()  { Siid = res.siid, Piid = res.piid, Value = value }
            ]
        }, MiHomeJsonContext.Default.ZigbeeWriteCommand);

        _mqttTransport.SendMessage(cmd);
    }
    internal class ZigbeeWriteCommand
    {
        public string Cmd { get; } = "write";
        public string Did { get; internal set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ZigbeeWriteItem> Params { get; internal set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("mi_spec")]
        public List<ZigbeeMiSpecWriteItem> MiSpec { get; internal set; }

        public class ZigbeeWriteItem
        {
            [JsonPropertyName("res_name")]
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
