using System.Globalization;
using System.Text.RegularExpressions;

namespace MiHomeSetup;

internal readonly record struct GatewayFirmware(int Major, int Minor, int Patch, int? Build) : IComparable<GatewayFirmware>
{
    internal static GatewayFirmware Parse(string value)
    {
        var match = Regex.Match(value, @"\A([0-9]+)\.([0-9]+)\.([0-9]+)(?:_([0-9]+))?\z", RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException("Unsupported firmware version format.");
        try
        {
            return new(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : null);
        }
        catch (OverflowException) { throw new InvalidDataException("Firmware version component is too large."); }
    }

    public int CompareTo(GatewayFirmware other)
    {
        var version = new Version(Major, Minor, Patch).CompareTo(new Version(other.Major, other.Minor, other.Patch));
        return version != 0 ? version : (Build ?? 0).CompareTo(other.Build ?? 0);
    }

    internal string TelnetMethod(string model) => (model, Major, Minor, Patch) switch
    {
        ("lumi.gateway.mgl03", 1, 4, 6) when Build is null =>
            throw new InvalidOperationException("Firmware 1.4.6 requires a build number to select the Telnet method."),
        ("lumi.gateway.mgl03", 1, 4, 6) when CompareTo(new GatewayFirmware(1, 4, 6, 43)) < 0 => "enable_telnet_service",
        ("lumi.gateway.mgl03", 1, 4, 6 or 7) => "set_ip_info",
        ("lumi.gateway.mgl03", 1, 5, >= 0 and <= 4) => "set_ip_info",
        ("lumi.gateway.mgl03", 1, 5, >= 5 and <= 7) => "system_command",
        ("lumi.gateway.mcn001" or "lumi.gateway.mgl001", 1, 0, >= 3 and <= 6) => "set_ip_info",
        ("lumi.gateway.mcn001" or "lumi.gateway.mgl001", 1, 0, 7 or 8) => "system_command",
        ("lumi.gateway.aqcn02", 4, 0, 1) => "set_ip_info",
        ("lumi.gateway.aqcn02", 4, 0, 4) => "system_command",
        _ => throw new InvalidOperationException("Automatic Telnet activation is unavailable for this model/firmware. Enable Telnet manually using the gateway instructions or add a verified firmware rule.")
    };
}
