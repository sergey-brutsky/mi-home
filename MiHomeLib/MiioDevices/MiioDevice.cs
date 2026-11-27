using MiHomeLib.MiioDevices.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.MiioDevices;

public abstract class MiioDevice(IMiioTransport miioTransport, int initialIdExternal = 0) : IDisposable
{
    private int _initialId = initialIdExternal;
    private static readonly JsonSerializerOptions _serializerSettings = MiioJson.Options;
    protected readonly IMiioTransport _miioTransport = miioTransport;

    /// <summary>
    /// Run an async Task-returning function synchronously, avoiding boilerplate
    /// </summary>
    protected static void RunSync(Func<Task> asyncFunc) => asyncFunc().ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Run an async Task&lt;T&gt;-returning function synchronously, avoiding boilerplate
    /// </summary>
    protected static T RunSync<T>(Func<Task<T>> asyncFunc) => asyncFunc().ConfigureAwait(false).GetAwaiter().GetResult();

    protected void CheckMessage(string response, string errorMessage)
    {
        using var document = MiioResponseReader.Parse(response, errorMessage);
        var first = MiioResponseReader.First(document.RootElement.GetProperty("result"), errorMessage);
        if (first.ValueKind != JsonValueKind.String || first.GetString() != "ok")
            throw new InvalidOperationException($"{errorMessage}: expected 'ok'.");
    }
    protected int GetInteger(string response, string msg)
    {
        using var document = MiioResponseReader.Parse(response, msg);
        var first = MiioResponseReader.First(document.RootElement.GetProperty("result"), msg);
        if (!int.TryParse(first.ToString(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var number))
            throw new InvalidOperationException($"{msg}: invalid integer value.");
        return number;
    }
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection-based JSON serialization.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses reflection-based JSON serialization.")]
#endif
    protected string BuildParamsArray(string method, params object[] methodParams)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>(){
            { "id", Interlocked.Increment(ref _initialId) },
            { "method", method},
            { "params", methodParams}
        }, _serializerSettings);
    }
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection-based JSON serialization.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses reflection-based JSON serialization.")]
#endif
    protected string BuildParamsObject(string method, object methodParams)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>(){
            { "id", Interlocked.Increment(ref _initialId) },
            { "method", method},
            { "params", methodParams }
        }, _serializerSettings);
    }
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Uses reflection-based JSON serialization.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Uses reflection-based JSON serialization.")]
#endif
    protected string BuildSidProp(string method, string sid, string prop, int value)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object>(){
            { "id", Interlocked.Increment(ref _initialId) },
            { "method", method},
            { "params", new Dictionary<string, object>
                {
                    { "sid", sid },
                    { prop, value },
                }
            }
        }, _serializerSettings);
    }
    /// <summary>
    /// Sends {"id":..,"method":..,"params":[..]} and returns the raw answer. <see cref="MiioTransport"/> numbers the request itself,
    /// other transports get it built here and sent through <see cref="IMiioTransport.SendMessageAsync"/>
    /// </summary>
    private protected Task<string> RequestArrayAsync(string method, params object[] parameters) =>
        RequestObjectAsync(method, parameters);

    private protected Task<string> RequestArrayAsync(string method, CancellationToken cancellationToken, params object[] parameters) =>
        RequestObjectAsync(method, parameters, MiioRetry.ForMethod(method), cancellationToken);

    /// <summary>
    /// Same as <see cref="RequestArrayAsync(string, object[])"/> for requests whose "params" is an object
    /// </summary>
    private protected Task<string> RequestObjectAsync(string method, object parameters) =>
        RequestObjectAsync(method, parameters, MiioRetry.ForMethod(method), CancellationToken.None);

    private protected Task<string> RequestObjectAsync(string method, object parameters, MiioRetryPolicy retryPolicy) =>
        RequestObjectAsync(method, parameters, retryPolicy, CancellationToken.None);

    private protected Task<string> RequestObjectAsync(string method, object parameters, CancellationToken cancellationToken) =>
        RequestObjectAsync(method, parameters, MiioRetry.ForMethod(method), cancellationToken);

    private protected Task<string> RequestObjectAsync(string method, object parameters, MiioRetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_miioTransport is not IMiioRequests requests)
            return _miioTransport.SendMessageAsync(BuildRequest(method, parameters));

        return cancellationToken.CanBeCanceled
            ? requests.RequestAsync(method, parameters, retryPolicy, cancellationToken)
            : requests.RequestAsync(method, parameters, retryPolicy);
    }

    private string BuildRequest(string method, object parameters) => new JsonObject
    {
        ["id"] = Interlocked.Increment(ref _initialId),
        ["method"] = method,
        ["params"] = MiioParameters.ToNode(parameters)?.DeepClone()
    }.ToJsonString();

    /// <summary>
    /// {"params":{"sid":..,"prop":value}}
    /// </summary>
    private protected Task<string> RequestSidPropAsync(string method, string sid, string prop, int value) =>
        RequestObjectAsync(method, new Dictionary<string, object> { { "sid", sid }, { prop, value } });

    protected string[] GetProps(params string[] props) => RunSync(() => GetPropsAsync(props));

    protected async Task<string[]> GetPropsAsync(params string[] props)
    {
        return ResultProps(await RequestArrayAsync("get_prop", props).ConfigureAwait(false), props);
    }
    private protected async Task<T> ReadPropertyValueAsync<T>(string property, Func<string, T> parse)
    {
        var value = (await GetPropsAsync(property).ConfigureAwait(false))[0];
        return MiioResponseReader.ConvertValue(() => parse(value), $"Unable to read property '{property}'");
    }

    private protected static void ValidateEnum<T>(T value, string parameterName) where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
            throw new ArgumentOutOfRangeException(parameterName, value, "Unknown enum value.");
    }

    private static string[] ResultProps(string response, string[] properties)
    {
        var operation = $"Unable to read properties [{string.Join(", ", properties)}]";
        using var document = MiioResponseReader.Parse(response, operation);
        var result = document.RootElement.GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{operation}: expected result array.");
        // Calls without named properties retain their variable-length result.
        if (properties.Length > 0 && result.GetArrayLength() != properties.Length)
            throw new InvalidOperationException($"{operation}: expected {properties.Length} values but received {result.GetArrayLength()}.");
        var values = new string[result.GetArrayLength()];
        for (var i = 0; i < values.Length; i++)
        {
            if (result[i].ValueKind == JsonValueKind.Null)
                throw new InvalidOperationException($"{operation}: value at index {i} is null.");
            values[i] = result[i].ToString();
        }
        return values;
    }
    public static string GetDeviceIdByIp(string ip)
        => RunSync(() => GetDeviceIdByIpAsync(ip));

    /// <summary>Finds a device in the local network and returns its unsigned device identifier.</summary>
    public static Task<string> GetDeviceIdByIpAsync(string ip) =>
        GetDeviceIdByIpAsync(ip, CancellationToken.None);

    /// <summary>Finds a device in the local network and returns its unsigned device identifier.</summary>
    /// <param name="ip">The device IP address.</param>
    /// <param name="cancellationToken">Token used to cancel local-network discovery.</param>
    public static async Task<string> GetDeviceIdByIpAsync(string ip, CancellationToken cancellationToken) =>
        GetDeviceIdByIp(ip, await MiioTransport.SendDiscoverMessageAsync(
            new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, 54321), TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false));

    internal static string GetDeviceIdByIp(string ip, IEnumerable<(string ip, string type, string serial, string token)> devices)
    {
        var identities = devices.Where(device => device.ip == ip)
            .Select(device => device.type + device.serial).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (identities.Length == 0)
            throw new InvalidOperationException($"Cannot get device id for {ip}. Make sure that your app is in the same LAN.");
        if (identities.Length > 1)
            throw new InvalidOperationException($"Cannot get device id for {ip}: conflicting device identities were reported.");
        return Convert.ToUInt32(identities[0], 16).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public virtual void Dispose() => _miioTransport?.Dispose();
}
