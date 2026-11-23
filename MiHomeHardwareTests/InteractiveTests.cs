using MiHomeLib.Devices;
using Xunit;
using Xunit.Abstractions;

namespace MiHomeHardwareTests;

[Collection("Hardware")]
[Trait("Category", "Interactive")]
public sealed class InteractiveTests(HardwareFixture fixture, ITestOutputHelper output)
{
    [SkippableFact]
    public async Task XiaomiSmartWirelessSwitch_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<XiaomiSmartWirelessSwitch>();
        Skip.If(device is null, "No XiaomiSmartWirelessSwitch found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<XiaomiSmartWirelessSwitch.ClickArg, Task> handler = _ => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnClickAsync += handler;
        try
        {
            output.WriteLine("Press the button once on XiaomiSmartWirelessSwitch {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnClickAsync -= handler; }
    }

    [SkippableFact]
    public async Task AqaraWirelessMiniSwitchCN_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<AqaraWirelessMiniSwitchCN>();
        Skip.If(device is null, "No AqaraWirelessMiniSwitchCN found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<AqaraWirelessMiniSwitchCN.ClickArg, Task> handler = _ => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnClickAsync += handler;
        try
        {
            output.WriteLine("Press the button once on AqaraWirelessMiniSwitchCN {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnClickAsync -= handler; }
    }

    [SkippableFact]
    public async Task AqaraWirelessMiniSwitchCN2_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<AqaraWirelessMiniSwitchCN2>();
        Skip.If(device is null, "No AqaraWirelessMiniSwitchCN2 found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<AqaraWirelessMiniSwitchCN2.ClickArg, Task> handler = _ => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnClickAsync += handler;
        try
        {
            output.WriteLine("Press the button once on AqaraWirelessMiniSwitchCN2 {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnClickAsync -= handler; }
    }

    [SkippableFact]
    public async Task AqaraDualWallWirelessSwitch_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<AqaraDualWallWirelessSwitch>();
        Skip.If(device is null, "No AqaraDualWallWirelessSwitch found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<AqaraDualWallWirelessSwitch.Button, AqaraDualWallWirelessSwitch.ClickArg, Task> handler = (_, _) => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnButtonClickAsync += handler;
        try
        {
            output.WriteLine("Press either button once on AqaraDualWallWirelessSwitch {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnButtonClickAsync -= handler; }
    }

    [SkippableFact]
    public async Task AqaraOppleTwoButtonsWirelessSwitch_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<AqaraOppleTwoButtonsWirelessSwitch>();
        Skip.If(device is null, "No AqaraOppleTwoButtonsWirelessSwitch found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<AqaraOppleWirelesSwitch.ClickArg, Task> handler = _ => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnButton1ClickAsync += handler;
        try
        {
            output.WriteLine("Press button 1 once on AqaraOppleTwoButtonsWirelessSwitch {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnButton1ClickAsync -= handler; }
    }

    [SkippableFact]
    public async Task AqaraOppleFourButtonsWirelessSwitch_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<AqaraOppleFourButtonsWirelessSwitch>();
        Skip.If(device is null, "No AqaraOppleFourButtonsWirelessSwitch found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<AqaraOppleWirelesSwitch.ClickArg, Task> handler = _ => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnButton1ClickAsync += handler;
        try
        {
            output.WriteLine("Press button 1 once on AqaraOppleFourButtonsWirelessSwitch {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnButton1ClickAsync -= handler; }
    }

    [SkippableFact]
    public async Task XiaomiSmartHumanBodySensor_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<XiaomiSmartHumanBodySensor>();
        Skip.If(device is null, "No XiaomiSmartHumanBodySensor found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> handler = () => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnMotionAsync += handler;
        try
        {
            output.WriteLine("Move in front of the sensor on XiaomiSmartHumanBodySensor {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnMotionAsync -= handler; }
    }

    [SkippableFact]
    public async Task AqaraSmartHumanBodySensor_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<AqaraSmartHumanBodySensor>();
        Skip.If(device is null, "No AqaraSmartHumanBodySensor found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> handler = () => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnMotionAsync += handler;
        try
        {
            output.WriteLine("Move in front of the sensor on AqaraSmartHumanBodySensor {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnMotionAsync -= handler; }
    }

    [SkippableFact]
    public async Task XiaomiSmartHumanBodySensor2_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<XiaomiSmartHumanBodySensor2>();
        Skip.If(device is null, "No XiaomiSmartHumanBodySensor2 found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<XiaomiSmartHumanBodySensor2.MotionState, Task> handler = _ => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnMotionDetectedAsync += handler;
        try
        {
            output.WriteLine("Move in front of the sensor on XiaomiSmartHumanBodySensor2 {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnMotionDetectedAsync -= handler; }
    }

    [SkippableFact]
    public async Task XiaomiWindowDoorSensor_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<XiaomiWindowDoorSensor>();
        Skip.If(device is null, "No XiaomiWindowDoorSensor found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> handler = () => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnContactChangedAsync += handler;
        try
        {
            output.WriteLine("Open or close the contact on XiaomiWindowDoorSensor {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnContactChangedAsync -= handler; }
    }

    [SkippableFact]
    public async Task AqaraWindowDoorSensor_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<AqaraWindowDoorSensor>();
        Skip.If(device is null, "No AqaraWindowDoorSensor found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> handler = () => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnContactChangedAsync += handler;
        try
        {
            output.WriteLine("Open or close the contact on AqaraWindowDoorSensor {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnContactChangedAsync -= handler; }
    }

    [SkippableFact]
    public async Task XiaomiDoorWindowSensor2_Delivers_A_Physical_Event()
    {
        Skip.If(!fixture.Config.Interactive, "MIHOME_INTERACTIVE is not 1");
        var device = fixture.Find<XiaomiDoorWindowSensor2>();
        Skip.If(device is null, "No XiaomiDoorWindowSensor2 found");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<XiaomiDoorWindowSensor2.ContactState, Task> handler = _ => { received.TrySetResult(true); return Task.CompletedTask; };
        device!.OnContactChangeAsync += handler;
        try
        {
            output.WriteLine("Open or close the contact on XiaomiDoorWindowSensor2 {0} within 30 seconds", device.DeviceId);
            await received.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally { device.OnContactChangeAsync -= handler; }
    }

}
