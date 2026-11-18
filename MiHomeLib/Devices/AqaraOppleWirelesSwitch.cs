using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>Common state and protocol mapping for Aqara Opple wireless switches.</summary>
public abstract class AqaraOppleWirelesSwitch : LumiBatteryDeviceBase
{
    public enum ClickArg
    {
        SingleClick = 1,
        DoubleClick = 2,
        TripleClick = 3,
        LongPressHold = 16,
        LongPressRelease = 17,
    }

    public override string DeviceId { get; }
    public int ShortId { get; }


    protected AqaraOppleWirelesSwitch(string deviceId, int shortId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
    }

    private protected abstract string MarketModel { get; }
    private protected abstract string LumiMqttModel { get; }
    private protected abstract void ConfigureButtons(ProtocolDeviceMap map);

    private protected sealed override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        ConfigureButtons(map);
        MapBattery(map, gateway2Voltage: false);
    }

    private protected void RaiseClick(int value, Func<ClickArg, Task> handler)
    {
        if (Enum.IsDefined(typeof(ClickArg), value))
            RaiseEvent(handler, (ClickArg)value);
        else
            _logger.LogWarning("Click code '{Code}' is not supported yet.", value);
    }





    public override string ToString() =>
        $"Device: {MarketModel} {LumiMqttModel} {DeviceId}, Voltage: {Voltage}V, " +
        $"BatteryPercent: {BatteryPercent}%, ChipTemperature: {ChipTemperature}°C, LinkQuality: {LinkQuality}";
}
