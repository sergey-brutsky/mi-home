using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using MiHomeLib.Helpers;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// Window and door contact sensor shared by Xiaomi Gateway 2 and Lumi MQTT gateways.
/// Every event is raised on both kinds of gateways
/// </summary>
public abstract class WindowDoorSensorBase : LumiBatteryDeviceBase, IContactSensor
{
    public enum DoorWindowContactState
    {
        Closed = 0,
        Opened = 1
    }

    private readonly ITimer _oneMinuteTimer;
    private readonly ITimer _fiveMinutesTimer;
    private readonly object _contactLock = new();
    private long _contactGeneration;

    public override string DeviceId { get; }
    public int ShortId { get; }
    public DoorWindowContactState Contact { get; private set; }
    bool? IContactSensor.IsOpen => Contact == DoorWindowContactState.Opened;

    /// <summary>
    /// "open" or "close", null until the first contact report
    /// </summary>
    public string Status { get; private set; }





    public event Func<Task> OnOpenAsync = () => Task.CompletedTask;
    public event Func<Task> OnCloseAsync = () => Task.CompletedTask;

    /// <summary>
    /// Raised on every contact report, together with <see cref="OnOpenAsync"/> or <see cref="OnCloseAsync"/>.
    /// The new state is in <see cref="Contact"/>
    /// </summary>
    public event Func<Task> OnContactChangedAsync = () => Task.CompletedTask;

    /// <summary>
    /// Xiaomi Gateway 2 reports it itself, on Lumi MQTT gateways it is detected by a timer
    /// </summary>
    public event Func<Task> NotClosedFor1MinuteAsync = () => Task.CompletedTask;

    /// <summary>
    /// Xiaomi Gateway 2 reports it itself, on Lumi MQTT gateways it is detected by a timer
    /// </summary>
    public event Func<Task> NotClosedFor5MinutesAsync = () => Task.CompletedTask;





    private protected WindowDoorSensorBase(string deviceId, int shortId, ILoggerFactory loggerFactory, ITimer oneMinuteTimer, ITimer fiveMinutesTimer)
        : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
        _oneMinuteTimer = oneMinuteTimer;
        _fiveMinutesTimer = fiveMinutesTimer;
    }

    public override void Dispose()
    {
        lock (_contactLock)
        {
            if (!BeginDispose()) return;
            ResourceCleanup.DisposeAll(_oneMinuteTimer.Stop, _fiveMinutesTimer.Stop,
                _oneMinuteTimer.Dispose, _fiveMinutesTimer.Dispose);
        }
    }

    private protected abstract string MarketModel { get; }
    private protected abstract string XiaomiGateway2Model { get; }
    private protected abstract string LumiMqttModel { get; }

    private protected sealed override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("status", UpdateGateway2Status);
        map.XiaomiGateway2("no_close", UpdateGateway2NoClose);

        map.LumiMqtt("3.1.85", UpdateLumiContact);
        MapBattery(map);
    }

    private void UpdateGateway2Status(JsonElement value)
    {
        switch (value.ReadString())
        {
            case "open":
                UpdateContact(DoorWindowContactState.Opened);
                break;
            case "close":
                UpdateContact(DoorWindowContactState.Closed);
                break;
        }
    }

    private void UpdateGateway2NoClose(JsonElement value)
    {
        // Gateway 2 sends the period as a string, a number is accepted as well
        var seconds = 0;
        var parsed = value.ValueKind == JsonValueKind.Number
            ? value.TryGetInt32(out seconds)
            : int.TryParse(value.ReadString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds);

        if (!parsed) return;

        if (seconds == 60)
            RaiseEvent(nameof(NotClosedFor1MinuteAsync), NotClosedFor1MinuteAsync);
        else if (seconds == 300)
            RaiseEvent(nameof(NotClosedFor5MinutesAsync), NotClosedFor5MinutesAsync);
    }

    private void UpdateLumiContact(JsonElement value)
    {
        var contact = value.ReadInt32();
        if (contact is not (0 or 1))
            return;

        UpdateContact((DoorWindowContactState)contact, manageTimers: true);
    }

    private void UpdateContact(DoorWindowContactState state, bool manageTimers = false)
    {
        // Lumi MQTT gateways do not report "not closed for N minutes", so it is timed here.
        // A repeated "opened" report must not restart the countdown
        lock (_contactLock)
        {
            if (IsDisposed) return;
            var wasOpened = Contact == DoorWindowContactState.Opened;
            Contact = state;
            Status = state == DoorWindowContactState.Opened ? "open" : "close";
            if (manageTimers && state == DoorWindowContactState.Closed)
            {
                ++_contactGeneration;
                _oneMinuteTimer.Stop();
                _fiveMinutesTimer.Stop();
            }
            else if (manageTimers && !wasOpened)
            {
                var generation = ++_contactGeneration;
                StartContactTimer(_oneMinuteTimer, generation, () =>
                    RaiseEvent(nameof(NotClosedFor1MinuteAsync), NotClosedFor1MinuteAsync));
                if (!IsDisposed && generation == _contactGeneration)
                    StartContactTimer(_fiveMinutesTimer, generation, () =>
                        RaiseEvent(nameof(NotClosedFor5MinutesAsync), NotClosedFor5MinutesAsync));
            }
        }

        if (state == DoorWindowContactState.Opened)
            RaiseEvent(nameof(OnOpenAsync), OnOpenAsync);
        else
            RaiseEvent(nameof(OnCloseAsync), OnCloseAsync);
        RaiseEvent(nameof(OnContactChangedAsync), OnContactChangedAsync);
    }

    private void StartContactTimer(ITimer timer, long generation, Action raiseEvent)
    {
        var reported = false;
        timer.Start(() =>
        {
            lock (_contactLock)
            {
                if (IsDisposed || generation != _contactGeneration || Contact != DoorWindowContactState.Opened || reported) return;
                reported = true;
                timer.Stop();
            }
            raiseEvent();
        });
    }





    public override string ToString()
    {
        if (IsInitializedFor(DeviceProtocol.XiaomiGateway2))
            return $"Device: {MarketModel} {Model ?? XiaomiGateway2Model} {DeviceId}, Voltage: {Voltage}V, Status: {Status}";

        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return $"Device: {MarketModel} {Model ?? LumiMqttModel} {DeviceId}, Contact: {Contact}, " +
                   $"Voltage: {Voltage}V, BatteryPercent: {BatteryPercent}%, " +
                   $"ChipTemperature: {ChipTemperature}°C, LinkQuality: {LinkQuality}";

        return $"Device: {MarketModel} {DeviceId}";
    }
}
