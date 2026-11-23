using System.Collections.Concurrent;
using MiHomeLib.Devices;
using Xunit;

namespace MiHomeHardwareTests;

[Collection("Hardware")]
public sealed class WriteTests(HardwareFixture fixture)
{
    [SkippableFact]
    public async Task Plug_Power_Is_Reported_And_Restored()
    {
        var device = Writable<XiaomiMiSmartPowerPlugCN>();
        Skip.If(!device.HasKnownState, "The plug's initial on/off state could not be verified");
        await Flip(() => device.State, value => value == XiaomiMiSmartPowerPlugCN.PlugState.On
            ? XiaomiMiSmartPowerPlugCN.PlugState.Off : XiaomiMiSmartPowerPlugCN.PlugState.On,
            value => value == XiaomiMiSmartPowerPlugCN.PlugState.On ? device.PowerOnAsync() : device.PowerOffAsync());
    }

    [SkippableFact]
    public async Task T1_Power_Is_Reported_And_Restored()
    {
        var device = Writable<AqaraT1WithNeutral>();
        Skip.If(device.State == AqaraT1WithNeutral.RelayState.Unknown, "Initial relay state is unknown");
        await Flip(() => device.State, value => value == AqaraT1WithNeutral.RelayState.On
            ? AqaraT1WithNeutral.RelayState.Off : AqaraT1WithNeutral.RelayState.On,
            value => value == AqaraT1WithNeutral.RelayState.On ? device.PowerOnAsync() : device.PowerOffAsync());
    }

    [SkippableFact]
    public async Task Dual_Relay_Channels_Are_Reported_And_Restored()
    {
        var device = Writable<AqaraDualWirelessRelayCN>();
        Skip.If(device.Channel1 == AqaraDualWirelessRelayCN.ChannelState.Unknown || device.Channel2 == AqaraDualWirelessRelayCN.ChannelState.Unknown,
            "Initial relay channel states are unknown");
        static AqaraDualWirelessRelayCN.ChannelState Opposite(AqaraDualWirelessRelayCN.ChannelState value) =>
            value == AqaraDualWirelessRelayCN.ChannelState.On ? AqaraDualWirelessRelayCN.ChannelState.Off : AqaraDualWirelessRelayCN.ChannelState.On;
        var original1 = device.Channel1;
        var original2 = device.Channel2;
        try
        {
            await Flip(() => device.Channel1, Opposite, value => value == AqaraDualWirelessRelayCN.ChannelState.On
                ? device.Channel1PowerOnAsync() : device.Channel1PowerOffAsync());
            await Flip(() => device.Channel2, Opposite, value => value == AqaraDualWirelessRelayCN.ChannelState.On
                ? device.Channel2PowerOnAsync() : device.Channel2PowerOffAsync());
        }
        finally
        {
            // An interlock may change the other channel as well; restore the complete snapshot.
            try
            {
                await (original1 == AqaraDualWirelessRelayCN.ChannelState.On ? device.Channel1PowerOnAsync() : device.Channel1PowerOffAsync())
                    .WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await (original2 == AqaraDualWirelessRelayCN.ChannelState.On ? device.Channel2PowerOnAsync() : device.Channel2PowerOffAsync())
                    .WaitAsync(TimeSpan.FromSeconds(10));
            }
            await WaitUntil(() => device.Channel1 == original1 && device.Channel2 == original2);
        }
    }

    [SkippableFact]
    public async Task Wall_Switch_Channels_Are_Reported_And_Restored()
    {
        var device = Writable<AqaraDualWallSwitch>();
        Skip.If(device.StatusLeft is not ("on" or "off") || device.StatusRight is not ("on" or "off"), "Initial channel states are unknown");
        static string Opposite(string value) => value == "on" ? "off" : "on";
        await Flip(() => device.StatusLeft, Opposite, value => value == "on" ? device.ChannelLeftPowerOnAsync() : device.ChannelLeftPowerOffAsync());
        await Flip(() => device.StatusRight, Opposite, value => value == "on" ? device.ChannelRightPowerOnAsync() : device.ChannelRightPowerOffAsync());
    }

    [SkippableFact]
    public async Task Mesh_Power_Is_Reported_And_Restored()
    {
        var device = Writable<XiaomiMeshWallSwitchSingleOpen>();
        Skip.If(!fixture.HasStateReport(device), "Initial mesh switch state has not been reported");
        await Flip(() => device.IsTurnedOn, value => !value, value => value ? device.TurnOnAsync() : device.TurnOffAsync());
    }

    [SkippableFact]
    public async Task Gateway2_Light_Reports_And_Is_Always_Turned_Off()
    {
        Skip.If(!fixture.Config.AllowWrites, "MIHOME_ALLOW_WRITES is not 1");
        Skip.If(fixture.Gateway2 is null, "Gateway 2 is not configured");
        var gateway = fixture.Gateway2!;
        Skip.If(string.IsNullOrEmpty(gateway.Sid) || !fixture.Config.Writable.Contains(gateway.Sid),
            "Gateway 2 SID is not authorized by MIHOME_WRITABLE for the light test");

        const int expectedRgb = (30 << 24) | 0xFFFFFF;
        var reported = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new ConcurrentQueue<int>();
        Task OnRgbChange(int _)
        {
            var rgb = gateway.Rgb;
            observed.Enqueue(rgb);
            if (rgb == expectedRgb) reported.TrySetResult(true);
            return Task.CompletedTask;
        }
        gateway.OnRgbChangeAsync += OnRgbChange;
        try
        {
            await gateway.EnableLightAsync(255, 255, 255, 30).WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                await reported.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException exception)
            {
                var values = observed.ToArray();
                var details = values.Length == 0 ? "no RGB reports arrived" : $"last RGB report was 0x{values[^1]:X8}";
                throw new TimeoutException($"Gateway 2 accepted the light command, but {details}. Expected 0x{expectedRgb:X8} within 10 seconds.", exception);
            }
        }
        finally
        {
            gateway.OnRgbChangeAsync -= OnRgbChange;
            await gateway.DisableLightAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private T Writable<T>() where T : GatewaySubDeviceBase
    {
        Skip.If(!fixture.Config.AllowWrites, "MIHOME_ALLOW_WRITES is not 1");
        var device = fixture.FindWritable<T>();
        Skip.If(device is null, "No matching device authorized by MIHOME_WRITABLE");
        return device!;
    }

    private static async Task Flip<T>(Func<T> read, Func<T, T> opposite, Func<T, Task> set)
    {
        var original = read();
        var target = opposite(original);
        try
        {
            await set(target).WaitAsync(TimeSpan.FromSeconds(10));
            await WaitUntil(() => EqualityComparer<T>.Default.Equals(read(), target));
        }
        finally
        {
            await set(original).WaitAsync(TimeSpan.FromSeconds(10));
            await WaitUntil(() => EqualityComparer<T>.Default.Equals(read(), original));
        }
    }
    private static async Task WaitUntil(Func<bool> check)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!check()) await Task.Delay(25, timeout.Token);
    }
}
