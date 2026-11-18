using MiHomeLib.Protocols;
using MiHomeLib.MiioDevices;
using MiHomeLib.Helpers;
using FluentAssertions;
using Moq;
using Xunit;
using AutoFixture;
using MiHomeLib.XiaomiGateway2;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using MiHomeLib;
using UnifiedTemperatureHumiditySensor = MiHomeLib.Devices.XiaomiTemperatureHumiditySensor;
using UnifiedAqaraTemperatureHumiditySensor = MiHomeLib.Devices.AqaraTemperatureHumiditySensor;
using UnifiedXiaomiSmartWirelessSwitch = MiHomeLib.Devices.XiaomiSmartWirelessSwitch;
using UnifiedXiaomiBluetoothHygrothermograph2 = MiHomeLib.Devices.XiaomiBluetoothHygrothermograph2;
using UnifiedAqaraWaterLeakSensor = MiHomeLib.Devices.AqaraWaterLeakSensor;

namespace MiHomeUnitTests.Gateways.Gateway2;

public class Gateway2LifecycleTests : Gateway2TestBase
{
    [Fact]
    public async Task Sync_Command_Does_Not_Capture_Ui_Context()
    {
        _miioTransport.Setup(x => x.SendMessageAsync(It.Is<string>(s => s.Contains("set_rgb"))))
            .Returns(() => Task.Run(async () => { await Task.Delay(20); return "{\"result\":[\"ok\"]}"; }));
        var complete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new NonPumpingContext();
        var thread = new System.Threading.Thread(() =>
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            try { _gateway.EnableLight(1, 2, 3, 50); complete.SetResult(true); }
            catch (Exception exception) { complete.SetException(exception); }
        }) { IsBackground = true };
        thread.Start();
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(2));
        context.Posts.Should().Be(0);
    }
}
