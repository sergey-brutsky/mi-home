using System.Text.Json.Nodes;

namespace MiHomeLib.MultimodeGateway.ActionProcessors;

public interface IActionProcessor
{
    void ProcessMessage(JsonNode json);
}
