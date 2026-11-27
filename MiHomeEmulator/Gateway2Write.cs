using System.Text.Json.Nodes;

namespace MiHomeEmulator;

public sealed record Gateway2Write(string Sid, string Model, JsonObject Data, bool KeyIsValid);
