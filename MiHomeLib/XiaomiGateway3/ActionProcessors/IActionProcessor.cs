using System.Text.Json.Nodes;

namespace MiHomeLib.XiaomiGateway3.ActionProcessors;

public interface IActionProcessor
{
    void ProcessMessage(JsonNode json);
}
