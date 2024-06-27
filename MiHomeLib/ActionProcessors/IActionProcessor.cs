using System.Text.Json.Nodes;

namespace MiHomeLib.ActionProcessors;

public interface IActionProcessor
{
    void ProcessMessage(JsonNode json);
}
