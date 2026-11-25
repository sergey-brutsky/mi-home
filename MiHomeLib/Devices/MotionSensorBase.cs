using MiHomeLib.Protocols;
using MiHomeLib.Devices.Timing;
using MiHomeLib.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MiHomeLib.Devices;

/// <summary>
/// Motion sensor shared by Xiaomi Gateway 2 and Lumi MQTT gateways. Every event works the same way on every gateway
/// </summary>
public abstract class MotionSensorBase : LumiBatteryDeviceBase
{
    /// <summary>
    /// No motion periods in seconds, as Xiaomi Gateway 2 reports them. Lumi MQTT gateways do not report
    /// "no motion", so there the same periods are timed by the library
    /// </summary>
    public static IReadOnlyList<int> NoMotionPeriods { get; } = [60, 120, 180, 300, 600, 1800];

    private readonly (int Seconds, ITimer Timer)[] _noMotionTimers;
    private readonly object _timerLock = new();
    private long _motionGeneration;

    public override string DeviceId { get; }
    public int ShortId { get; }

    /// <summary>
    /// Local time of the last detected motion, null until the first one
    /// </summary>
    public DateTime? MotionDate { get; private set; }





    /// <summary>
    /// Raised when the sensor detects motion
    /// </summary>
    public event Func<Task> OnMotionAsync = () => Task.CompletedTask;

    /// <summary>
    /// Raised when there has been no motion for one of the <see cref="NoMotionPeriods"/>,
    /// the period in seconds is passed as an argument. After a motion it is raised once for every period,
    /// as long as there is no new motion. Works the same way on every gateway
    /// </summary>
    public event Func<int, Task> OnNoMotionAsync = _ => Task.CompletedTask;





    private protected MotionSensorBase(string deviceId, int shortId, ILoggerFactory loggerFactory, Func<TimeSpan, ITimer> createTimer)
        : base(loggerFactory)
    {
        DeviceId = deviceId;
        ShortId = shortId;
        _noMotionTimers = NoMotionPeriods
            .Select(seconds => (seconds, createTimer(TimeSpan.FromSeconds(seconds))))
            .ToArray();
    }

    public override void Dispose()
    {
        lock (_timerLock)
        {
            if (!BeginDispose()) return;
            ResourceCleanup.DisposeAll(_noMotionTimers.SelectMany(entry =>
                new Action[] { entry.Timer.Stop, entry.Timer.Dispose }).ToArray());
        }
    }

    private protected abstract string MarketModel { get; }
    private protected abstract string XiaomiGateway2Model { get; }
    private protected abstract string LumiMqttModel { get; }

    /// <summary>
    /// Mappings of model specific properties, added to the common motion sensor ones
    /// </summary>
    private protected virtual void ConfigureModelProtocolMap(ProtocolDeviceMap map) { }

    /// <summary>
    /// Model specific part of <see cref="ToString"/>, starts with ", " when not empty
    /// </summary>
    private protected virtual string ModelInfo => string.Empty;

    private protected sealed override void ConfigureProtocolMap(ProtocolDeviceMap map)
    {
        map.XiaomiGateway2("status", UpdateGateway2Status);
        map.XiaomiGateway2("no_motion", UpdateGateway2NoMotion);

        map.LumiMqtt("3.1.85", value => DetectLumiMotion());
        MapBattery(map);

        ConfigureModelProtocolMap(map);
    }


    private void UpdateGateway2Status(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String && value.ReadString() == "motion")
            RaiseMotion();
    }

    private void UpdateGateway2NoMotion(JsonElement value)
    {
        if (TryGetGateway2Int(value, out var seconds))
            RaiseEvent(nameof(OnNoMotionAsync), OnNoMotionAsync, seconds);
    }

    private void RaiseMotion()
    {
        MotionDate = DateTime.Now;
        RaiseEvent(nameof(OnMotionAsync), OnMotionAsync);
    }

    // Every motion restarts the countdown before invoking client handlers.
    private void DetectLumiMotion()
    {
        long generation;
        lock (_timerLock)
        {
            if (IsDisposed) return;
            generation = ++_motionGeneration;
            foreach (var (_, timer) in _noMotionTimers) timer.Stop();
            MotionDate = DateTime.Now;
            foreach (var (seconds, timer) in _noMotionTimers)
            {
                if (IsDisposed || generation != _motionGeneration) return;
                StartNoMotionTimer(seconds, timer, generation);
            }
        }
        RaiseEvent(nameof(OnMotionAsync), OnMotionAsync);
    }

    private void StartNoMotionTimer(int seconds, ITimer timer, long generation)
    {
        var reported = false;
        timer.Start(() =>
        {
            lock (_timerLock)
            {
                if (IsDisposed || generation != _motionGeneration || reported) return;
                reported = true;
                timer.Stop();
            }
            RaiseEvent(nameof(OnNoMotionAsync), OnNoMotionAsync, seconds);
        });
    }





    public override string ToString()
    {
        if (IsInitializedFor(DeviceProtocol.XiaomiGateway2))
            return $"Device: {MarketModel} {Model ?? XiaomiGateway2Model} {DeviceId}, Voltage: {Voltage}V{ModelInfo}";

        if (IsInitializedFor(DeviceProtocol.LumiMqtt))
            return $"Device: {MarketModel} {Model ?? LumiMqttModel} {DeviceId}, BatteryPercent: {BatteryPercent}%{ModelInfo}";

        return $"Device: {MarketModel} {DeviceId}";
    }
}
