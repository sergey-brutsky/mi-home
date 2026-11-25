using MiHomeLib.Protocols;
// Support for this device has been partially implemented on top of https://home.miot-spec.com/spec/090615.remote.btsw1
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// PTX_YK1_QMIMB PTX Wireless Switch (Bluetooth version) BLE 5.0
/// </summary>
[ProtocolDeviceModel(DeviceProtocol.LumiBle, MODEL, PDID)]
public sealed class PtxWirelessSwitchBle : ProtocolMappedDeviceBase, IProtocolMappedBleDevice
{
    public const string MARKET_MODEL = "PTX-YK1-QMIMB";
    public const string MODEL = "090615.remote.btsw1";
    public const int PDID = 14523;

    public enum ClickArg
    {
        SingleClick = 1012,
        DoubleClick = 1013,
        LongPressClick = 1014, // press & hold more than 5 seconds
    }

    private const int CLICK_SIID = 2;
    
    // low battery event, only event
    // unfortunately percentage is not available for this device :(
    private const int LOW_BATTERY_SIID = 3; 

    public override string DeviceId { get; }
    public string Mac { get; private set; }

    public PtxWirelessSwitchBle(string deviceId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
    }

    void IProtocolMappedBleDevice.SetMacAddress(string mac) => Mac = mac;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiBleMiSpecEvent(CLICK_SIID, (int)ClickArg.SingleClick, _ => RaiseEvent(nameof(OnClickAsync), OnClickAsync, ClickArg.SingleClick));
        map.LumiBleMiSpecEvent(CLICK_SIID, (int)ClickArg.DoubleClick, _ => RaiseEvent(nameof(OnClickAsync), OnClickAsync, ClickArg.DoubleClick));
        map.LumiBleMiSpecEvent(CLICK_SIID, (int)ClickArg.LongPressClick, _ => RaiseEvent(nameof(OnClickAsync), OnClickAsync, ClickArg.LongPressClick));
        // Preserve the existing service-level interpretation until real logs identify its event IDs.
        map.LumiBleMiSpecServiceEvent(LOW_BATTERY_SIID, _ => RaiseEvent(nameof(OnLowBatteryAsync), OnLowBatteryAsync));
    }
    /// <summary>
    /// Value of click type passed as a parameter
    /// </summary>
    public event Func<ClickArg, Task> OnClickAsync = (_) => Task.CompletedTask;
    
    /// <summary>
    /// Low battery event
    /// </summary>
    public event Func<Task> OnLowBatteryAsync = () => Task.CompletedTask;

    public override string ToString() => GetBaseInfo(MARKET_MODEL, MODEL) + $"Mac: {Mac}";
}