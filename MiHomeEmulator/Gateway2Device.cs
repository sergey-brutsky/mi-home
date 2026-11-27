using System.Text.Json.Nodes;

namespace MiHomeEmulator;

public sealed record Gateway2Device(string Sid, string Model, int ShortId, JsonObject Data);
