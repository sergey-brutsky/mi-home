namespace MiHomeSetup;

internal sealed record GatewayProfile(string Login, string Prompt, string Architecture, string AgentMd5, string BlePath)
{
    internal static void ValidateModel(string model)
    {
        if (model != Gateway2Checker.Model) Resolve(model);
    }

    internal string ModelCommand => Architecture == "mips"
        ? "sed -n 's/^model=//p' /data/miio/device.conf"
        : "agetprop | grep -E '\\[persist[^]]*\\.model\\]'";

    internal static GatewayProfile FromTelnetBanner(string banner)
    {
        var model = banner switch
        {
            var text when text.Contains("rlxlinux", StringComparison.Ordinal) => "lumi.gateway.mgl03",
            var text when text.Contains("Aqara-Hub-E1", StringComparison.Ordinal) || text.Contains("Aqara_Hub_E1", StringComparison.Ordinal) => "lumi.gateway.aqcn02",
            var text when text.Contains("Mijia_Hub_V2", StringComparison.Ordinal) => "lumi.gateway.mcn001",
            _ => throw new InvalidDataException("Unknown Telnet banner. Specify --model explicitly.")
        };
        // All supported shell prompts end in '# '. Read the model to distinguish CN/Global Gateway 2.
        return Resolve(model) with { Prompt = "# " };
    }

    public static GatewayProfile Resolve(string model) => model switch
    {
        "lumi.gateway.mgl03" => new("admin", "# ", "mips", "6c3f4dca62647b9d19a81e1ccaa5ccc0", "/data/miio/mible_local.db"),
        "lumi.gateway.mcn001" => Arm("~ # "),
        "lumi.gateway.mgl001" => Arm("/ # "),
        "lumi.gateway.aqcn02" => Arm("/ # ", ""),
        _ => throw new ArgumentException("This operation requires an MQTT gateway model: lumi.gateway.mgl03, lumi.gateway.mcn001, lumi.gateway.mgl001 or lumi.gateway.aqcn02. Gateway 2 (lumi.gateway.v3) uses native check/setup diagnostics.")
    };

    private static GatewayProfile Arm(string prompt, string blePath = "/data/local/miio_bt/mible_local.db") =>
        new("root", prompt, "arm", "bb0b33b8d71acbfb9668ae9a0600c2d8", blePath);
}
