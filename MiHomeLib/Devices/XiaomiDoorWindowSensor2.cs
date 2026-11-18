using MiHomeLib.Protocols;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>MCCGQ02HL BLE contact and light sensor used by Lumi MQTT gateways.</summary>
[ProtocolDeviceModel(DeviceProtocol.LumiBle, MODEL, PDID)]
public sealed class XiaomiDoorWindowSensor2 : ProtocolMappedDeviceBase, IProtocolMappedBleDevice
{
    public const string MARKET_MODEL = "MCCGQ02HL";
    public const string MODEL = "isa.magnet.dw2hl";
    public enum ContactState
    {
        Unknown = -1,
        Open = 0,
        Closed = 1,
        // To configure timeout, connect to the device via bluetooth and configure timeout in the
        // mobile application
        NotClosedAfterConfiguredTimeout = 2,
    }
    public enum LightState
    {
        Unknown = -1,
        NoLight = 0,
        LightDiscovered = 1,
    }
    public const int PDID = 2443;
    private const int CONTACT_EID = 4121;
    private const int LIGHT_EID = 4120;
    private bool _hasBatteryReport;
    public override string DeviceId { get; }
    public string Mac { get; private set; }
    public byte BatteryPercent { get; private set; }

    /// <summary>Raised on every battery report, including unchanged values. Passes the previous value, the new value is in BatteryPercent.</summary>
    public event Func<byte, Task> OnBatteryPercentChangeAsync = _ => Task.CompletedTask;

    public XiaomiDoorWindowSensor2(string deviceId, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        DeviceId = deviceId;
    }

    void IProtocolMappedBleDevice.SetMacAddress(string mac) => Mac = mac;

    private protected override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.LumiBle(CONTACT_EID, value =>
        {
            var code = BleValueDecoder.DecodeByte(value.ReadString(), "a sensor value");
            var contact = Enum.IsDefined(typeof(ContactState), (int)code) ? (ContactState)code : ContactState.Unknown;
            if (contact == Contact) return;
            var oldContact = Contact;
            Contact = contact;
            RaiseEvent(OnContactChangeAsync, oldContact);
        });
        map.LumiBle(LIGHT_EID, value =>
        {
            var code = BleValueDecoder.DecodeByte(value.ReadString(), "a sensor value");
            var light = Enum.IsDefined(typeof(LightState), (int)code) ? (LightState)code : LightState.Unknown;
            if (light == Light) return;
            var oldLight = Light;
            Light = light;
            RaiseEvent(OnLightChangeAsync, oldLight);
        });
        map.LumiBle(4106, value =>
        {
            var battery = BleValueDecoder.DecodeByte(value.ReadString(), "a sensor value");
            var oldBattery = BatteryPercent;
            BatteryPercent = battery;
            _hasBatteryReport = true;
            RaiseEvent(OnBatteryPercentChangeAsync, oldBattery);
        });
    }

    public ContactState Contact { get; private set; } = ContactState.Unknown;
    /// <summary>Raised only when contact changes. Passes the previous state, the new state is in Contact.</summary>
    public event Func<ContactState, Task> OnContactChangeAsync = (_) => Task.CompletedTask;
    public LightState Light { get; private set; } = LightState.Unknown;
    /// <summary>Raised only when light changes. Passes the previous state, the new state is in Light.</summary>
    public event Func<LightState, Task> OnLightChangeAsync = (_) => Task.CompletedTask;
    public override string ToString()
    {
        var battery = _hasBatteryReport ? $"{BatteryPercent}%" : "Unknown";
        return GetBaseInfo(MARKET_MODEL, MODEL) + $"Mac: {Mac}, " +
                $"Contact: {Contact}, " +
                $"Light: {Light}, " +
                $"Battery Percent: {battery} ";
    }
}
