using System.Text.Json.Nodes;

namespace MiHomeLib.MqttGateway.ActionProcessors;

public interface IActionProcessor
{
    void ProcessMessage(JsonNode json);
}
