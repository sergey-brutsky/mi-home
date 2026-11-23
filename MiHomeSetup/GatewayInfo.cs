namespace MiHomeSetup;

internal sealed record GatewayInfo(string Model, string? Firmware, string? Mac = null);
