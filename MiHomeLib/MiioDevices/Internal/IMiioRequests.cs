using System.Text.Json;
using System.Threading.Tasks;

namespace MiHomeLib.MiioDevices.Internal;

/// <summary>
/// miio requests where the transport owns the request id: it numbers the request and repeats it only when the caller marks repetition safe
/// and accepts only the answer to it. <see cref="MiioTransport"/> implements it; for other <see cref="IMiioTransport"/>
/// implementations the library builds the request itself and uses <see cref="IMiioTransport.SendMessageAsync"/>
/// </summary>
internal interface IMiioRequests
{
    /// <summary>
    /// Sends {"id":..,"method":..,"params":..} and returns the raw answer
    /// </summary>
    Task<string> RequestAsync(string method, object parameters, MiioRetryPolicy retryPolicy);
}

internal static class MiioJson
{
    /// <summary>
    /// The serializer settings of every miio request, the same for both ways of sending it
    /// </summary>
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
