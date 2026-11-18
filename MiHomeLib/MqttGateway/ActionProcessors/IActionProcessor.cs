using System.Text.Json;

namespace MiHomeLib.MqttGateway.ActionProcessors;

internal interface IActionProcessor
{
    void ProcessMessage(JsonElement json);
}
