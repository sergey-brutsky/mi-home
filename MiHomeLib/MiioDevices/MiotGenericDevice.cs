using MiHomeLib.MiioDevices.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MiHomeLib.Helpers;

namespace MiHomeLib.MiioDevices;

public class MiotGenericDevice: MiioDevice
{   
    protected readonly string _did;
    public uint UptimeSeconds { get; private set; }
    public string MiioVersion { get; private set; }
    public string Mac { get; private set; }
    public string FirmwareVersion { get; private set; }
    public string Hardware { get; private set; }
    public WifiSettings Wifi { get; private set; }
    public NetifSettings Network { get; private set; }

    public MiotGenericDevice(string did, IMiioTransport miioTransport, int initialIdExternal)
        : this(did, miioTransport, initialIdExternal, null) { }

    private protected MiotGenericDevice(string did, IMiioTransport miioTransport, int initialIdExternal, Action initializationFailed)
        : base(miioTransport, initialIdExternal)
    {
        try
        {
            _did = did;

            var response = RunSync(() => RequestArrayAsync("miIO.info", string.Empty));

            using var document = MiioResponseReader.Parse(response, "Unable to read miIO.info");
            var info = MiioResponseReader.Object(document.RootElement.GetProperty("result"), "Unable to read miIO.info");
            const string operation = "Unable to read miIO.info";
            string Text(JsonElement parent, string name, string path) => MiioResponseReader.StringProperty(parent, name, $"{operation} {path}");
            int Number(JsonElement parent, string name, string path) => MiioResponseReader.ConvertValue(
                () => int.Parse(MiioResponseReader.Property(parent, name, $"{operation} {path}").ToString(), System.Globalization.CultureInfo.InvariantCulture), $"{operation} {path}");
            UptimeSeconds = MiioResponseReader.ConvertValue(() => uint.Parse(
                MiioResponseReader.Property(info, "life", $"{operation} life").ToString(), System.Globalization.CultureInfo.InvariantCulture), $"{operation} life");
            MiioVersion = Text(info, "miio_ver", "miio_ver");
            Mac = Text(info, "mac", "mac");
            FirmwareVersion = Text(info, "fw_ver", "fw_ver");
            Hardware = Text(info, "hw_ver", "hw_ver");
            var ap = MiioResponseReader.Object(MiioResponseReader.Property(info, "ap", $"{operation} ap"), $"{operation} ap");
            Wifi = new WifiSettings
            {
                Ssid = Text(ap, "ssid", "ap.ssid"),
                Bssid = Text(ap, "bssid", "ap.bssid"),
                Rssi = Number(ap, "rssi", "ap.rssi"),
                Freq = ap.TryGetProperty("freq", out _) ? Number(ap, "freq", "ap.freq") : 0
            };
            var network = MiioResponseReader.Object(MiioResponseReader.Property(info, "netif", $"{operation} netif"), $"{operation} netif");
            Network = new NetifSettings
            {
                Ip = Text(network, "localIp", "netif.localIp"),
                Mask = Text(network, "mask", "netif.mask"),
                Gateway = Text(network, "gw", "netif.gw")
            };
        }
        catch (Exception failure)
        {
            ResourceCleanup.PreserveFailure(failure, initializationFailed ?? miioTransport.Dispose);
            throw;
        }
    }

    protected string GetDeviceInfo(string marketModel, string model)
    {
        return $"Model: {marketModel} {model}," +
                $" Uptime: {UptimeSeconds} seconds," +
                $" Miio Version: {MiioVersion}," +
                $" Mac: {Mac}," +
                $" Firmware Version: {FirmwareVersion}," +
                $" Hardware: {Hardware}," +
                $" SSID: {Wifi.Ssid}," +
                $" BSSID: {Wifi.Bssid}," +
                $" RSSI: {Wifi.Rssi}," +
                $" Freq: {Wifi.Freq}," +
                $" Ip: {Network.Ip}," +
                $" Mask: {Network.Mask}," +
                $" Gateway: {Network.Gateway}";
    }

    public class WifiSettings
    {
        public string Ssid { get; internal set; }
        public string Bssid { get; internal set; }
        public int Rssi { get; internal set; }
        public int Freq { get; internal set; }
    }
    public class NetifSettings
    {
        public string Ip { get; internal set; }
        public string Mask { get; internal set; }
        public string Gateway { get; internal set; }
    }
    protected string GetMiotProperty(int siid, int piid, string did = null) => GetMiotProperties([(siid, piid, did ?? $"{siid}-{piid}")])[0];
    protected string[] GetMiotProperties((int siid, int piid)[] arr)
    {
        return GetMiotProperties(arr.Select(x => (x.siid, x.piid, $"{x.siid}-{x.piid}")).ToArray());
    }
    protected string[] GetMiotProperties((int siid, int piid, string did)[] arr) => RunSync(() => GetMiotPropertiesAsync(arr));

    private protected async Task<string> GetMiotPropertyAsync(int siid, int piid, string did = null) =>
        (await GetMiotPropertiesAsync([(siid, piid, did ?? $"{siid}-{piid}")]).ConfigureAwait(false))[0];

    private protected async Task<T> ReadMiotValueAsync<T>(int siid, int piid, Func<string, T> parse)
    {
        var value = await GetMiotPropertyAsync(siid, piid).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => parse(value), $"Unable to read MI-spec property {siid}/{piid}");
    }

    private protected Task<string[]> GetMiotPropertiesAsync((int siid, int piid)[] arr) =>
        GetMiotPropertiesAsync(arr.Select(x => (x.siid, x.piid, $"{x.siid}-{x.piid}")).ToArray());

    private protected async Task<string[]> GetMiotPropertiesAsync((int siid, int piid, string did)[] arr)
    {
        object[] properties = [.. arr.Select(x => new MiotReadProperty(x.did, x.siid, x.piid))];

        var requested = string.Join(", ", arr.Select(x => $"{x.siid}/{x.piid} ({x.did})"));
        var response = await RequestArrayAsync("get_properties", properties).ConfigureAwait(false);
        using var document = MiioResponseReader.Parse(response, $"Cannot read MI-spec properties {requested}");
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Cannot read MI-spec properties {requested}: gateway returned no result array.");

        var byDid = result.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("did", out var did) && did.ValueKind == JsonValueKind.String)
            .ToLookup(item => item.GetProperty("did").GetString(), StringComparer.Ordinal);
        return arr.Select(property =>
        {
            var matches = byDid[property.did].Where(item =>
                (!item.TryGetProperty("siid", out var siid) || siid.ValueKind == JsonValueKind.Number && siid.TryGetInt32(out var service) && service == property.siid) &&
                (!item.TryGetProperty("piid", out var piid) || piid.ValueKind == JsonValueKind.Number && piid.TryGetInt32(out var identifier) && identifier == property.piid)).ToArray();
            var description = $"{property.siid}/{property.piid} ({property.did})";
            if (matches.Length != 1)
                throw new InvalidOperationException($"Cannot read MI-spec property {description}: expected one matching result.");
            var item = matches[0];
            if (!item.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var status))
                throw new InvalidOperationException($"Cannot read MI-spec property {description}: missing result code.");
            if (status != 0)
                throw new InvalidOperationException($"Cannot read MI-spec property {description}: gateway returned code {status}.");
            if (!item.TryGetProperty("value", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                throw new InvalidOperationException($"Cannot read MI-spec property {description}: missing value.");
            return value.ToString();
        }).ToArray();
    }

#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
#endif
    protected void SetMiotProperty(int siid, int piid, object value = null)
    {
        SetMiotProperty(siid, piid, $"set-{siid}-{piid}", value);
    }
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
#endif
    protected void SetMiotProperty(int siid, int piid, string did, object value = null) =>
        RunSync(() => SetMiotPropertyAsync(siid, piid, did, SerializeExternalParameter(value, validateEnum: true)));

    private protected Task SetMiotPropertyAsync(int siid, int piid, object value = null) =>
        SetMiotPropertyAsync(siid, piid, $"set-{siid}-{piid}", value);

    private protected Task SetMiotPropertyAsync(int siid, int piid, string did, object value = null) =>
        SetMiotPropertyCoreAsync(siid, piid, did, value, MiioRetryPolicy.SafeToRepeat);

    private protected Task SetMiotPropertyOnceAsync(int siid, int piid, object value = null) =>
        SetMiotPropertyCoreAsync(siid, piid, $"set-{siid}-{piid}", value, MiioRetryPolicy.DoNotRepeat);

    private async Task SetMiotPropertyCoreAsync(int siid, int piid, string did, object value, MiioRetryPolicy retryPolicy)
    {
        if (value is Enum enumeration && !Enum.IsDefined(enumeration.GetType(), enumeration))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown enum value.");
        var response = await RequestObjectAsync("set_properties", new object[] { new MiotWriteProperty(did, siid, piid, MiioParameters.ToNode(value)) }, retryPolicy).ConfigureAwait(false);

        var operation = $"Unable to set property {siid}/{piid} for {did}";
        using var document = MiioResponseReader.Parse(response, operation);
        MiioResponseReader.CheckStatus(MiioResponseReader.First(document.RootElement.GetProperty("result"), operation), operation);
    }
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
#endif
    protected void CallMiotAction(int siid, int aiid, params object[] args) => RunSync(() => CallMiotActionAsync(siid, aiid, args is null ? null : args.Select(value => (object)SerializeExternalParameter(value)).ToArray()));

#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses reflection-based JSON serialization for arbitrary external parameters.")]
#endif
    private static JsonNode SerializeExternalParameter(object value, bool validateEnum = false)
    {
        if (validateEnum && value is Enum enumeration && !Enum.IsDefined(enumeration.GetType(), enumeration))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown enum value.");
        return JsonSerializer.SerializeToNode(value, MiioJson.Options);
    }

    private protected async Task CallMiotActionAsync(int siid, int aiid, params object[] args)
    {
        var response = await RequestObjectAsync("action", new MiotAction($"call-{siid}-{aiid}", siid, aiid, MiioParameters.ToNode(args))).ConfigureAwait(false);

        var operation = $"Unable to call action siid: {siid}, aiid: {aiid} for {_did}";
        using var document = MiioResponseReader.Parse(response, operation);
        MiioResponseReader.CheckStatus(document.RootElement.GetProperty("result"), operation);
    }
}
