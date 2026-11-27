using MiHomeLib.MiioDevices.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.MiioDevices;

/// <summary>Discovers miio devices through the local network hello exchange.</summary>
public static class MiioDiscovery
{
    /// <summary>Metadata extracted from a device's hello response.</summary>
    /// <param name="Ip">IP address of the responding device.</param>
    /// <param name="DeviceType">Device type extracted from the hello packet.</param>
    /// <param name="Serial">Serial portion extracted from the hello packet.</param>
    /// <param name="Token">Checksum returned by GetChecksum() for the hello packet.</param>
    public sealed record DiscoveredDevice(string Ip, string DeviceType, string Serial, string Token);

    /// <summary>Collects hello responses within the total timeout, which defaults to five seconds. Repeated identical metadata is returned once.</summary>
#pragma warning disable RS0027 // Keep the existing optional overload source-compatible while exposing a required cancellation token overload.
    public static Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(TimeSpan? timeout = null) =>
        DiscoverAsync(timeout, CancellationToken.None);
#pragma warning restore RS0027

    /// <summary>Collects hello responses until the timeout expires or the operation is canceled.</summary>
    /// <param name="timeout">The total discovery duration; defaults to five seconds when null.</param>
    /// <param name="cancellationToken">Token used to cancel discovery.</param>
    public static Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(TimeSpan? timeout, CancellationToken cancellationToken) =>
        DiscoverAsync(new IPEndPoint(IPAddress.Broadcast, 54321), timeout ?? TimeSpan.FromSeconds(5), cancellationToken);

    internal static Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(IPEndPoint destination, TimeSpan timeout) =>
        DiscoverAsync(destination, timeout, CancellationToken.None);

    internal static async Task<IReadOnlyList<DiscoveredDevice>> DiscoverAsync(
        IPEndPoint destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var devices = await MiioTransport.SendDiscoverMessageAsync(destination, timeout, cancellationToken).ConfigureAwait(false);
        return devices.Select(device => new DiscoveredDevice(device.ip, device.type, device.serial, device.token)).ToArray();
    }
}
