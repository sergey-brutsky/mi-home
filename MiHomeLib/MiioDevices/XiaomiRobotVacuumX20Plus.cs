using MiHomeLib.Helpers;
using MiHomeLib.MiioDevices.Internal;
// MIoT specification https://home.miot-spec.com/spec/xiaomi.vacuum.c102gl

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace MiHomeLib.MiioDevices;

public class XiaomiRobotVacuumX20Plus : MiotGenericDevice
{
    #region Types and snapshots

    public enum VacuumState
    {
        Sweeping = 1, Idle = 2, Paused = 3, Error = 4, ReturningToDock = 5,
        Charging = 6, Mopping = 7, Drying = 8, WashingMop = 9, ReturningToWash = 10,
        BuildingMap = 11, SweepingAndMopping = 12, ChargingCompleted = 13,
        Upgrading = 14, WaterInspecting = 19, WashingPaused = 21,
        CollectingDust = 22, RemoteCleaning = 23
    }

    public enum ChargingState { Charging = 1, NotCharging = 2, ReturningToDock = 5 }
    public enum SuctionMode { Silent = 0, Standard = 1, Strong = 2, Max = 3 }
    public enum WaterLevel { Low = 1, Medium = 2, High = 3 }
    public enum ConsumableType { MainBrush = 9, SideBrush = 10, Filter = 11, Mop = 18 }

    /// <summary>A fresh status snapshot. Area retains the device's reported units; work, task and station codes retain unknown values.</summary>
    public sealed record DeviceStatus(VacuumState State, byte FaultCode, byte BatteryPercent,
        ChargingState Charging, uint WorkMode, uint TaskStatus, uint CleaningMinutes,
        uint CleaningArea, byte WashStation, ushort DryingMinutesLeft);

    /// <summary>Remaining consumable life in hours and percent.</summary>
    public sealed record Consumable(ushort RemainingHours, byte RemainingPercent);
    public sealed record ConsumablesInfo(Consumable MainBrush, Consumable SideBrush, Consumable Filter, Consumable Mop);

    /// <summary>Lifetime counters. The first-clean value and areas retain the device's numeric representation.</summary>
    public sealed record CleaningStatistics(uint FirstCleaningTimestamp, uint TotalCleaningMinutes,
        uint TotalCleaningCount, uint TotalCleaningArea);

    /// <summary>Times use the robot's configured time zone.</summary>
    public sealed record DndSettings(bool Enabled, TimeSpan StartTime, TimeSpan EndTime);
    public sealed record DeviceSettings(SuctionMode Suction, WaterLevel Water, bool ResumeAfterCharging,
        bool CarpetBoost, bool ChildLock, byte Volume, DndSettings DoNotDisturb, string TimeZone);

    /// <summary>A property value or its protocol error. Values retain the firmware's string representation.</summary>
    public sealed record DiagnosticProperty(int ServiceId, int PropertyId, string Name, string Value, string Error)
    {
        public bool IsAvailable => Error is null;
    }

    /// <summary>One short map segment ID and its cleaning parameters. Order follows the input list.</summary>
    public sealed record RoomSelection(int RoomId, int RepeatCount = 1,
        SuctionMode Suction = SuctionMode.Standard, WaterLevel Water = WaterLevel.Medium);

    /// <summary>Firmware-specific timer-clean data preserved verbatim, together with the robot's time zone.</summary>
    public sealed record CleaningSchedule(string Data, string TimeZone);
    public sealed record VoiceSettings(string PacketId, string State, int Progress);

    #endregion

    #region Constants and construction

    public const string MARKET_MODEL = "X20 Plus";
    public const string MODEL = "xiaomi.vacuum.c102gl";
    private const int VACUUM_SIID = 2;
    private const int START_SWEEP_AIID = 1;
    private const int PAUSE_AIID = 2;
    private const int BATTERY_SIID = 3;
    private const int START_CHARGE_AIID = 1;
    private const int VACUUM_EXTEND_SIID = 4;
    private const int STOP_CLEAN_AIID = 2;

    private const int AUDIO_SIID = 7;
    private const int POSITION_AIID = 1;

    public XiaomiRobotVacuumX20Plus(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiRobotVacuumX20Plus(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal) {}

    #endregion

    #region Basic commands

    /// <summary>
    /// Start cleaning
    /// </summary>
    public void StartCleaning() => RunSync(() => StartCleaningAsync());

    /// <inheritdoc cref="StartCleaning"/>
    public async Task StartCleaningAsync() => await CallMiotActionAsync(VACUUM_SIID, START_SWEEP_AIID).ConfigureAwait(false);

    /// <summary>
    /// Stop cleaning (stops the cleaning task entirely, not just pause)
    /// </summary>
    public void StopCleaning() => RunSync(() => StopCleaningAsync());

    /// <inheritdoc cref="StopCleaning"/>
    public async Task StopCleaningAsync() => await CallMiotActionAsync(VACUUM_EXTEND_SIID, STOP_CLEAN_AIID).ConfigureAwait(false);

    /// <summary>
    /// Pause cleaning
    /// </summary>
    public void Pause() => RunSync(() => PauseAsync());

    /// <inheritdoc cref="Pause"/>
    public async Task PauseAsync() => await CallMiotActionAsync(VACUUM_SIID, PAUSE_AIID).ConfigureAwait(false);

    /// <summary>
    /// Go to the dock station for charging
    /// </summary>
    public void GoHome() => RunSync(() => GoHomeAsync());

    /// <inheritdoc cref="GoHome"/>
    public async Task GoHomeAsync() => await CallMiotActionAsync(BATTERY_SIID, START_CHARGE_AIID).ConfigureAwait(false);

    /// <summary>
    /// Find the vacuum via voice/sound notification (locate my robot)
    /// </summary>
    public void FindMe() => RunSync(() => FindMeAsync());

    /// <inheritdoc cref="FindMe"/>
    public async Task FindMeAsync() => await CallMiotActionAsync(AUDIO_SIID, POSITION_AIID).ConfigureAwait(false);

    public override string ToString() => GetDeviceInfo(MARKET_MODEL, MODEL);

    #endregion

    #region Status and diagnostics

    /// <summary>Reads current state, battery, fault and cleaning counters in one request.</summary>
    public DeviceStatus GetStatus() => RunSync(GetStatusAsync);

    /// <inheritdoc cref="GetStatus"/>
    public async Task<DeviceStatus> GetStatusAsync()
    {
        var values = await GetMiotPropertiesAsync([(2, 1), (2, 2), (3, 1), (3, 2), (4, 1),
            (4, 7), (4, 2), (4, 3), (4, 25), (2, 5)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new DeviceStatus(
            (VacuumState)Byte(values[0]), Byte(values[1]), Byte(values[2]),
            (ChargingState)Byte(values[3]), UInt(values[4]), UInt(values[5]),
            UInt(values[6]), UInt(values[7]), Byte(values[8]), UShort(values[9])), "Unable to read X20+ status");
    }

    /// <summary>Reads main brush, side brush, filter and mop lifetime counters.</summary>
    public ConsumablesInfo GetConsumables() => RunSync(GetConsumablesAsync);

    /// <inheritdoc cref="GetConsumables"/>
    public async Task<ConsumablesInfo> GetConsumablesAsync()
    {
        var values = await GetMiotPropertiesAsync([(9, 1), (9, 2), (10, 1), (10, 2),
            (11, 2), (11, 1), (18, 2), (18, 1)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new ConsumablesInfo(
            new Consumable(UShort(values[0]), Byte(values[1])),
            new Consumable(UShort(values[2]), Byte(values[3])),
            new Consumable(UShort(values[4]), Byte(values[5])),
            new Consumable(UShort(values[6]), Byte(values[7]))), "Unable to read X20+ consumables");
    }

    /// <summary>Reads lifetime cleaning statistics without caching them.</summary>
    public CleaningStatistics GetCleaningStatistics() => RunSync(GetCleaningStatisticsAsync);

    /// <inheritdoc cref="GetCleaningStatistics"/>
    public async Task<CleaningStatistics> GetCleaningStatisticsAsync()
    {
        var values = await GetMiotPropertiesAsync([(12, 1), (12, 2), (12, 3), (12, 4)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new CleaningStatistics(UInt(values[0]), UInt(values[1]),
            UInt(values[2]), UInt(values[3])), "Unable to read X20+ cleaning statistics");
    }

    /// <summary>Reads extended firmware properties in batches of ten. Unsupported properties carry an Error instead of aborting the snapshot.</summary>
    public IReadOnlyList<DiagnosticProperty> GetDiagnostics() => RunSync(GetDiagnosticsAsync);

    /// <inheritdoc cref="GetDiagnostics"/>
    public async Task<IReadOnlyList<DiagnosticProperty>> GetDiagnosticsAsync()
    {
        var properties = new (int Service, int Property, string Name)[]
        {
            (4, 4, "cleaning-mode"), (4, 6, "waterbox-status"), (4, 14, "serial-number"),
            (4, 16, "clean-rags-tip"), (4, 17, "keep-sweeper-time"), (4, 18, "faults"),
            (4, 19, "nation-matched"), (4, 20, "relocation-status"), (4, 21, "laser-switch"),
            (4, 23, "clean-setting"), (4, 24, "stop-map"), (4, 26, "custom-enable"),
            (4, 28, "sensitivity"), (4, 29, "mop-way"), (4, 30, "clean-cancel"),
            (4, 31, "y-clean"), (4, 32, "electric-water"), (4, 33, "carpet-recognition"),
            (4, 34, "smart-wash-switch"), (4, 35, "warn-status"), (4, 36, "carpet-excape"),
            (4, 37, "clean-fluid-switch"), (4, 38, "capability"), (4, 39, "save-water-tips"),
            (4, 40, "drying-time"), (4, 41, "no-water-info"), (4, 45, "auto-install-mop"),
            (4, 46, "mop-wash-mode"), (4, 47, "clean-disable-mode"), (4, 48, "clean-quick"),
            (4, 49, "reloc-enable"), (4, 50, "clean-switch-set"), (4, 51, "auto-water"),
            (4, 52, "mop-in-station"), (4, 53, "mop-pad-installed"), (4, 55, "dry-stop-remind"),
            (4, 56, "int-message-prompt"), (8, 4, "timer-cancel-reason")
        };
        var result = new List<DiagnosticProperty>();
        for (var offset = 0; offset < properties.Length; offset += 10)
        {
            var batch = properties.Skip(offset).Take(10).ToArray();
            try
            {
                var values = await GetMiotPropertiesAsync(batch.Select(property => (property.Service, property.Property)).ToArray()).ConfigureAwait(false);
                for (var i = 0; i < batch.Length; i++)
                    result.Add(new DiagnosticProperty(batch[i].Service, batch[i].Property, batch[i].Name, values[i], null));
            }
            catch (InvalidOperationException)
            {
                // A firmware may reject one optional property. Retry individually to retain the others.
                foreach (var property in batch)
                {
                    try
                    {
                        var value = await GetMiotPropertyAsync(property.Service, property.Property).ConfigureAwait(false);
                        result.Add(new DiagnosticProperty(property.Service, property.Property, property.Name, value, null));
                    }
                    catch (InvalidOperationException error)
                    {
                        result.Add(new DiagnosticProperty(property.Service, property.Property, property.Name, null, error.Message));
                    }
                }
            }
        }
        return result.AsReadOnly();
    }

    #endregion

    #region Cleaning and dock commands

    /// <summary>Starts dust collection at the dock.</summary>
    public void StartDustCollection() => RunSync(StartDustCollectionAsync);
    /// <inheritdoc cref="StartDustCollection"/>
    public Task StartDustCollectionAsync() => CallMiotActionAsync(2, 4);

    /// <summary>Starts washing the mop pads.</summary>
    public void StartMopWashing() => RunSync(StartMopWashingAsync);
    /// <inheritdoc cref="StartMopWashing"/>
    public Task StartMopWashingAsync() => CallMiotActionAsync(2, 6);

    /// <summary>Starts drying the mop pads.</summary>
    public void StartDrying() => RunSync(StartDryingAsync);
    /// <inheritdoc cref="StartDrying"/>
    public Task StartDryingAsync() => CallMiotActionAsync(2, 8);

    /// <summary>Stops drying the mop pads.</summary>
    public void StopDrying() => RunSync(StopDryingAsync);
    /// <inheritdoc cref="StopDrying"/>
    public Task StopDryingAsync() => CallMiotActionAsync(2, 9);

    /// <summary>Invokes the dock eject action.</summary>
    public void EjectFromDock() => RunSync(EjectFromDockAsync);
    /// <inheritdoc cref="EjectFromDock"/>
    public Task EjectFromDockAsync() => CallMiotActionAsync(2, 10);

    /// <summary>Plays the robot's sample sound.</summary>
    public void PlaySound() => RunSync(PlaySoundAsync);
    /// <inheritdoc cref="PlaySound"/>
    public Task PlaySoundAsync() => CallMiotActionAsync(7, 2);

    /// <summary>Resets one consumable's lifetime counter after replacement.</summary>
    public void ResetConsumable(ConsumableType consumable) => RunSync(() => ResetConsumableAsync(consumable));
    /// <inheritdoc cref="ResetConsumable"/>
    public async Task ResetConsumableAsync(ConsumableType consumable)
    {
        ValidateEnum(consumable, nameof(consumable));
        await CallMiotActionAsync((int)consumable, 1).ConfigureAwait(false);
    }

    /// <summary>Sends one experimental remote-control step using firmware-native velocity and rotation values. Zeroes request a stop. The command is sent once.</summary>
    public void RemoteControlMoveStep(int velocity, int rotation, bool playSound = false) =>
        RunSync(() => RemoteControlMoveStepAsync(velocity, rotation, playSound));
    /// <inheritdoc cref="RemoteControlMoveStep"/>
    public async Task RemoteControlMoveStepAsync(int velocity, int rotation, bool playSound = false)
    {
        var nonceBytes = new byte[2];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(nonceBytes);
        var nonce = BitConverter.ToUInt16(nonceBytes, 0) % 65535;
        var data = JsonSerializer.Serialize(new RemoteControlPayload(velocity, rotation, playSound ? "true" : "false", nonce), MiHomeJsonContext.Default.RemoteControlPayload);
        await SetMiotPropertyOnceAsync(4, 15, data).ConfigureAwait(false);
    }

    /// <summary>Starts rooms in input order using short map segment IDs supplied by the caller.</summary>
    public void StartRoomCleaning(IReadOnlyList<RoomSelection> rooms) => RunSync(() => StartRoomCleaningAsync(rooms));
    /// <inheritdoc cref="StartRoomCleaning"/>
    public async Task StartRoomCleaningAsync(IReadOnlyList<RoomSelection> rooms)
    {
        if (rooms is null) throw new ArgumentNullException(nameof(rooms));
        var selections = rooms.ToArray();
        if (selections.Length == 0) throw new ArgumentException("At least one room is required.", nameof(rooms));
        var ids = new HashSet<int>();
        var data = new List<int[]>();
        for (var index = 0; index < selections.Length; index++)
        {
            var room = selections[index];
            if (room is null || room.RoomId <= 0 || room.RepeatCount <= 0 || !ids.Add(room.RoomId))
                throw new ArgumentException("Rooms must have unique positive IDs and a positive repeat count.", nameof(rooms));
            ValidateEnum(room.Suction, nameof(rooms));
            ValidateEnum(room.Water, nameof(rooms));
            data.Add([room.RoomId, room.RepeatCount, (int)room.Suction, (int)room.Water, index + 1]);
        }
        // c102gl firmware 4.5.6_1087 requires work-mode as well as the selects string.
        var payload = JsonSerializer.Serialize(new RoomCleaningPayload(data), MiHomeJsonContext.Default.RoomCleaningPayload);
        await CallMiotActionAsync(4, 1, new MiotActionInput(1, MiioParameters.ToNode(18)), new MiotActionInput(10, MiioParameters.ToNode(payload))).ConfigureAwait(false);
    }

    /// <summary>Starts a firmware-specific cleaning task. The data is an object encoded as JSON.</summary>
    public void StartCustomCleaning(int workMode, string data) => RunSync(() => StartCustomCleaningAsync(workMode, data));
    /// <inheritdoc cref="StartCustomCleaning"/>
    public async Task StartCustomCleaningAsync(int workMode, string data)
    {
        if (workMode < 0 || workMode > 50) throw new ArgumentOutOfRangeException(nameof(workMode), workMode, "Expected a firmware work mode from 0 to 50.");
        if (data is null) throw new ArgumentNullException(nameof(data));
        using var document = JsonDocument.Parse(data);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Expected a JSON object.", nameof(data));
        await CallMiotActionAsync(4, 1, new MiotActionInput(1, MiioParameters.ToNode(workMode)), new MiotActionInput(10, MiioParameters.ToNode(data))).ConfigureAwait(false);
    }

    #endregion

    #region Settings and voice packages

    /// <summary>Reads suction, water, switches, volume, DND times and the robot's time zone.</summary>
    public DeviceSettings GetSettings() => RunSync(GetSettingsAsync);

    /// <inheritdoc cref="GetSettings"/>
    public async Task<DeviceSettings> GetSettingsAsync()
    {
        var values = await GetMiotPropertiesAsync([(2, 3), (4, 5), (4, 11), (4, 12), (4, 27),
            (7, 1), (5, 1), (5, 2), (5, 3), (8, 1)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new DeviceSettings((SuctionMode)Byte(values[0]),
            (WaterLevel)Byte(values[1]), Switch(values[2]), Switch(values[3]), Switch(values[4]), Byte(values[5]),
            new DndSettings(bool.Parse(values[6]), ParseTime(values[7]), ParseTime(values[8])), values[9]),
            "Unable to read X20+ settings");
    }

    /// <summary>Reads do-not-disturb times in the robot's configured time zone.</summary>
    public DndSettings GetDndSettings() => RunSync(GetDndSettingsAsync);

    /// <inheritdoc cref="GetDndSettings"/>
    public async Task<DndSettings> GetDndSettingsAsync()
    {
        var values = await GetMiotPropertiesAsync([(5, 1), (5, 2), (5, 3)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new DndSettings(bool.Parse(values[0]), ParseTime(values[1]),
            ParseTime(values[2])), "Unable to read X20+ DND settings");
    }

    /// <summary>Sets the suction mode.</summary>
    public void SetSuctionMode(SuctionMode mode) => RunSync(() => SetSuctionModeAsync(mode));
    /// <inheritdoc cref="SetSuctionMode"/>
    public async Task SetSuctionModeAsync(SuctionMode mode)
    {
        ValidateEnum(mode, nameof(mode));
        await SetMiotPropertyAsync(2, 3, (int)mode).ConfigureAwait(false);
    }

    /// <summary>Sets the mop water level.</summary>
    public void SetWaterLevel(WaterLevel level) => RunSync(() => SetWaterLevelAsync(level));
    /// <inheritdoc cref="SetWaterLevel"/>
    public async Task SetWaterLevelAsync(WaterLevel level)
    {
        ValidateEnum(level, nameof(level));
        await SetMiotPropertyAsync(4, 5, (int)level).ConfigureAwait(false);
    }

    /// <summary>Enables or disables resuming cleaning after charging.</summary>
    public void SetResumeAfterCharging(bool enabled) => RunSync(() => SetResumeAfterChargingAsync(enabled));
    /// <inheritdoc cref="SetResumeAfterCharging"/>
    public Task SetResumeAfterChargingAsync(bool enabled) => SetMiotPropertyAsync(4, 11, enabled ? 1 : 0);

    /// <summary>Enables or disables carpet suction boost.</summary>
    public void SetCarpetBoost(bool enabled) => RunSync(() => SetCarpetBoostAsync(enabled));
    /// <inheritdoc cref="SetCarpetBoost"/>
    public Task SetCarpetBoostAsync(bool enabled) => SetMiotPropertyAsync(4, 12, enabled ? 1 : 0);

    /// <summary>Enables or disables the child lock.</summary>
    public void SetChildLock(bool enabled) => RunSync(() => SetChildLockAsync(enabled));
    /// <inheritdoc cref="SetChildLock"/>
    public Task SetChildLockAsync(bool enabled) => SetMiotPropertyAsync(4, 27, enabled ? 1 : 0);

    /// <summary>Sets the mop washing reminder from 0 to 120 minutes.</summary>
    public void SetMopWashReminder(int minutes) => RunSync(() => SetMopWashReminderAsync(minutes));
    /// <inheritdoc cref="SetMopWashReminder"/>
    public async Task SetMopWashReminderAsync(int minutes)
    {
        if (minutes < 0 || minutes > 120) throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Expected 0 to 120 minutes.");
        await SetMiotPropertyAsync(4, 16, minutes).ConfigureAwait(false);
    }

    /// <summary>Sets volume from 0 to 100 percent.</summary>
    public void SetVolume(int volume) => RunSync(() => SetVolumeAsync(volume));
    /// <inheritdoc cref="SetVolume"/>
    public async Task SetVolumeAsync(int volume)
    {
        if (volume < 0 || volume > 100) throw new ArgumentOutOfRangeException(nameof(volume), volume, "Expected 0 to 100 percent.");
        await SetMiotPropertyAsync(7, 1, volume).ConfigureAwait(false);
    }

    /// <summary>Enables or disables do-not-disturb.</summary>
    public void SetDndEnabled(bool enabled) => RunSync(() => SetDndEnabledAsync(enabled));
    /// <inheritdoc cref="SetDndEnabled"/>
    public Task SetDndEnabledAsync(bool enabled) => SetMiotPropertyAsync(5, 1, enabled);

    /// <summary>Sets the DND start time, with minute precision, in the robot's configured time zone.</summary>
    public void SetDndStartTime(TimeSpan time) => RunSync(() => SetDndStartTimeAsync(time));
    /// <inheritdoc cref="SetDndStartTime"/>
    public async Task SetDndStartTimeAsync(TimeSpan time) =>
        await SetMiotPropertyAsync(5, 2, value: FormatTime(time)).ConfigureAwait(false);

    /// <summary>Sets the DND end time, with minute precision, in the robot's configured time zone.</summary>
    public void SetDndEndTime(TimeSpan time) => RunSync(() => SetDndEndTimeAsync(time));
    /// <inheritdoc cref="SetDndEndTime"/>
    public async Task SetDndEndTimeAsync(TimeSpan time) =>
        await SetMiotPropertyAsync(5, 3, value: FormatTime(time)).ConfigureAwait(false);

    /// <summary>Reads firmware k/v switches, retaining unknown keys and JSON value types.</summary>
    public IReadOnlyDictionary<string, JsonElement> GetExtendedSwitches() => RunSync(GetExtendedSwitchesAsync);
    /// <inheritdoc cref="GetExtendedSwitches"/>
    public async Task<IReadOnlyDictionary<string, JsonElement>> GetExtendedSwitchesAsync()
    {
        var value = await GetMiotPropertyAsync(4, 50).ConfigureAwait(false);
        const string operation = "Unable to read X20+ extended switches (4/50)";
        try
        {
            using var document = ParseFirmwareJson(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Expected a k/v array.");
            var switches = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var key = item.GetProperty("k").GetString();
                if (string.IsNullOrWhiteSpace(key) || switches.ContainsKey(key)) throw new FormatException("Empty or duplicate switch key.");
                switches.Add(key, item.GetProperty("v").Clone());
            }
            return new ReadOnlyDictionary<string, JsonElement>(switches);
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidOperationException(operation, error);
        }
    }

    /// <summary>Writes one experimental firmware k/v switch. Writable support and key meanings require hardware verification.</summary>
    public void SetExtendedSwitch(string key, int value) => RunSync(() => SetExtendedSwitchAsync(key, value));
    /// <inheritdoc cref="SetExtendedSwitch"/>
    public async Task SetExtendedSwitchAsync(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A switch key is required.", nameof(key));
        // Dreame writes one k/v object, while reads return an array of all switches.
        var data = JsonSerializer.Serialize(new SettingPayload(key, MiioParameters.ToNode(value)), MiHomeJsonContext.Default.SettingPayload);
        await SetMiotPropertyAsync(4, 50, value: data).ConfigureAwait(false);
    }

    /// <summary>Reads the active voice packet and its installation progress.</summary>
    public VoiceSettings GetVoiceSettings() => RunSync(GetVoiceSettingsAsync);
    /// <inheritdoc cref="GetVoiceSettings"/>
    public async Task<VoiceSettings> GetVoiceSettingsAsync()
    {
        var values = await GetMiotPropertiesAsync([(7, 2), (7, 3)]).ConfigureAwait(false);
        try
        {
            using var document = ParseFirmwareJson(values[1]);
            var root = document.RootElement;
            return new VoiceSettings(values[0], root.GetProperty("state").GetString(), root.GetProperty("progress").GetInt32());
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidOperationException("Unable to read X20+ voice state (7/3)", error);
        }
    }

    /// <summary>Selects a voice packet ID supported by the robot's firmware.</summary>
    public void SetVoicePacket(string packetId) => RunSync(() => SetVoicePacketAsync(packetId));
    /// <inheritdoc cref="SetVoicePacket"/>
    public async Task SetVoicePacketAsync(string packetId)
    {
        if (string.IsNullOrWhiteSpace(packetId)) throw new ArgumentException("A voice packet ID is required.", nameof(packetId));
        await SetMiotPropertyAsync(7, 2, value: packetId).ConfigureAwait(false);
    }

    /// <summary>Requests installation of a voice package from a caller-supplied URL. Requires verification on the target firmware.</summary>
    public void InstallVoicePackage(string packetId, string url, string md5, int sizeBytes) =>
        RunSync(() => InstallVoicePackageAsync(packetId, url, md5, sizeBytes));
    /// <inheritdoc cref="InstallVoicePackage"/>
    public async Task InstallVoicePackageAsync(string packetId, string url, string md5, int sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(packetId)) throw new ArgumentException("A voice packet ID is required.", nameof(packetId));
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || (address.Scheme != "http" && address.Scheme != "https"))
            throw new ArgumentException("An absolute HTTP(S) package URL is required.", nameof(url));
        if (md5 is null || md5.Length != 32 || !md5.All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 32-character MD5 checksum.", nameof(md5));
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "Expected a positive package size in bytes.");
        var data = JsonSerializer.Serialize(new UploadPayload(packetId, url, md5, sizeBytes), MiHomeJsonContext.Default.UploadPayload);
        await SetMiotPropertyOnceAsync(7, 4, data).ConfigureAwait(false);
    }

    #endregion

    #region Schedules

    /// <summary>Reads the complete firmware schedule string and its time zone. Unknown fields are preserved.</summary>
    public CleaningSchedule GetCleaningSchedule() => RunSync(GetCleaningScheduleAsync);
    /// <inheritdoc cref="GetCleaningSchedule"/>
    public async Task<CleaningSchedule> GetCleaningScheduleAsync()
    {
        var values = await GetMiotPropertiesAsync([(8, 2), (8, 1)]).ConfigureAwait(false);
        return new CleaningSchedule(values[0], values[1]);
    }

    /// <summary>Writes timer-clean data in the firmware's format, as captured from the application or a previous read.</summary>
    public void SetCleaningSchedule(string data) => RunSync(() => SetCleaningScheduleAsync(data));
    /// <inheritdoc cref="SetCleaningSchedule"/>
    public async Task SetCleaningScheduleAsync(string data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        await SetMiotPropertyAsync(8, 2, value: data).ConfigureAwait(false);
    }

    /// <summary>Deletes a schedule by its firmware timer ID.</summary>
    public void DeleteCleaningSchedule(string timerId) => RunSync(() => DeleteCleaningScheduleAsync(timerId));
    /// <inheritdoc cref="DeleteCleaningSchedule"/>
    public async Task DeleteCleaningScheduleAsync(string timerId)
    {
        if (string.IsNullOrWhiteSpace(timerId)) throw new ArgumentException("A timer ID is required.", nameof(timerId));
        await CallMiotActionAsync(8, 1, new MiotActionInput(3, MiioParameters.ToNode(timerId))).ConfigureAwait(false);
    }

    #endregion

    #region Formatting and parsing

    private static byte Byte(string value) => byte.Parse(value, CultureInfo.InvariantCulture);
    private static ushort UShort(string value) => ushort.Parse(value, CultureInfo.InvariantCulture);
    private static uint UInt(string value) => uint.Parse(value, CultureInfo.InvariantCulture);

    private static bool Switch(string value) => value switch
    {
        "0" => false,
        "1" => true,
        _ => throw new FormatException("Expected a 0/1 switch value.")
    };

    private static TimeSpan ParseTime(string value) =>
        TimeSpan.ParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture);

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1) || time.Ticks % TimeSpan.TicksPerMinute != 0)
            throw new ArgumentOutOfRangeException(nameof(time), time, "Expected a time of day with minute precision.");
        return time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    private static JsonDocument ParseFirmwareJson(string value)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(value); }
        catch (JsonException)
        {
            // Firmware 4.5.6_1087 also returns JSON with escaped quotes but no outer quotes.
            return JsonDocument.Parse(JsonSerializer.Deserialize("\"" + value + "\"", MiHomeJsonContext.Default.String));
        }
        if (document.RootElement.ValueKind != JsonValueKind.String) return document;
        using (document) return JsonDocument.Parse(document.RootElement.GetString());
    }

    #endregion
}
