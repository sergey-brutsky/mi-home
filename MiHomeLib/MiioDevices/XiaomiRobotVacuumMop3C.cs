using MiHomeLib.MiioDevices.Internal;
using System.Threading.Tasks;
// Partial support for this device has been implemented on top of https://home.miot-spec.com/spec/ijai.vacuum.v18
// Your contributions are appreciated
using System;
using System.Linq;

namespace MiHomeLib.MiioDevices;

public class XiaomiRobotVacuumMop3C : MiotGenericDevice
{
    public const string MARKET_MODEL = "B106CN";
    public const string MODEL = "ijai.vacuum.v18";

    public enum Status
    {
        Sleep = 0,
        Idle = 1,
        Paused = 2,
        GoCharging = 3,
        Charging = 4,
        Sweeping = 5,
        SweepingAndMopping = 6,
        Mopping = 7,
        Upgrading = 8,
    }
    public enum CleaningMode
    {
        Sweep = 0,
        SweepAndMop = 1,
        Mop = 2,
    }
    public enum SweepType
    {
        Global = 0,
        Mop = 1,
        Edge = 2,
        Area = 3,
        Point = 4,
        Remote = 5,
        Explore = 6,
        Room = 7,
        Floor = 8,
    }
    public enum CleaningType
    {
        GlobalSweep = 1,
        OnlySweep = 3,
        SweepAndMop = 5,
        OnlyMop = 6
    }
    
    // Initialize requests from Random value in order to avoid overlapping requests with the same id    
    public XiaomiRobotVacuumMop3C(string ip, string token) : this(new MiioTransport(ip, token), new Random().Next(0, 1000)) { }

    internal XiaomiRobotVacuumMop3C(IMiioTransport transport, int initialIdExternal = 0) : base("", transport, initialIdExternal) {}

    /// <summary>A snapshot of robot state, battery, cleaning mode, sweep type and voice level.</summary>
    public sealed record DeviceStatus(Status State, ushort BatteryPercent, CleaningMode CleaningMode,
        SweepType SweepType, ushort VoiceLevel);

    /// <summary>Reads the current state and settings in one MIoT request.</summary>
    public DeviceStatus GetStatus() => RunSync(GetStatusAsync);

    /// <inheritdoc cref="GetStatus"/>
    public async Task<DeviceStatus> GetStatusAsync()
    {
        var values = await GetMiotPropertiesAsync([(2, 1), (3, 1), (2, 4), (2, 8), (4, 2)]).ConfigureAwait(false);
        return MiioResponseReader.ConvertValue(() => new DeviceStatus(
            (Status)ushort.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture),
            ushort.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture),
            (CleaningMode)short.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture),
            (SweepType)ushort.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture),
            ushort.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture)), "Unable to read Mop 3C status");
    }

    /// <summary>
    /// Returns vacuum battery percentage
    /// </summary>
    /// <returns>value from range 0 - 100</returns>
    public ushort GetBatteryPercent() => RunSync(() => GetBatteryPercentAsync());

    /// <inheritdoc cref="GetBatteryPercent"/>
    public async Task<ushort> GetBatteryPercentAsync() => await ReadMiotValueAsync(3, 1, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Returns vacuum status
    /// </summary>
    /// <returns>vacuum status: idle, sleep, charging, etc</returns>
    public Status GetState() => RunSync(() => GetStateAsync());

    /// <inheritdoc cref="GetState"/>
    public async Task<Status> GetStateAsync() => (Status)await ReadMiotValueAsync(2, 1, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Set up cleaning mode of the vacuum
    /// </summary>
    public void SetCleaningMode(CleaningMode cleaningMode) => RunSync(() => SetCleaningModeAsync(cleaningMode));

    /// <inheritdoc cref="SetCleaningMode"/>
    public async Task SetCleaningModeAsync(CleaningMode cleaningMode)
    {
        ValidateEnum(cleaningMode, nameof(cleaningMode));
        await SetMiotPropertyAsync(2, 4, cleaningMode).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns cleaning mode of the vacuum
    /// </summary>
    /// <returns>Sweep, Mop, SweepAndMop</returns>
    public CleaningMode GetCleaningMode() => RunSync(() => GetCleaningModeAsync());

    /// <inheritdoc cref="GetCleaningMode"/>
    public async Task<CleaningMode> GetCleaningModeAsync() => (CleaningMode)await ReadMiotValueAsync(2, 4, value => short.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Set sweep type for vacuum
    /// </summary>
    /// <param name="sweepType">Global, Mop, Edge, etc</param>
    public void SetSweepType(SweepType sweepType) => RunSync(() => SetSweepTypeAsync(sweepType));

    /// <inheritdoc cref="SetSweepType"/>
    public async Task SetSweepTypeAsync(SweepType sweepType)
    {
        ValidateEnum(sweepType, nameof(sweepType));
        await SetMiotPropertyAsync(2, 8, sweepType).ConfigureAwait(false);
    }

    /// <summary>
    /// Get type of cleaning sweep
    /// </summary>
    /// <returns>Global, Mop, Edge, etc</returns>
    public SweepType GetSweepType() => RunSync(() => GetSweepTypeAsync());

    /// <inheritdoc cref="GetSweepType"/>
    public async Task<SweepType> GetSweepTypeAsync() => (SweepType)await ReadMiotValueAsync(2, 8, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Find the vacuum via voice search
    /// </summary>
    public void FindMe() => RunSync(() => FindMeAsync());

    /// <inheritdoc cref="FindMe"/>
    public async Task FindMeAsync() => await SetMiotPropertyOnceAsync(4, 1).ConfigureAwait(false);

    /// <summary>
    /// Start cleaning with selected mode
    /// </summary>
    /// <param name="ct">Sweep Only, Mop Only, Sweep And Mop, etc</param>
    public void StartCleaning(CleaningType ct) => RunSync(() => StartCleaningAsync(ct));

    /// <inheritdoc cref="StartCleaning"/>
    public async Task StartCleaningAsync(CleaningType ct)
    {
        ValidateEnum(ct, nameof(ct));
        await CallMiotActionAsync(2, (int)ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Stop cleaning (only stop !, no return to the dock station)
    /// </summary>
    public void StopCleaning() => RunSync(() => StopCleaningAsync());

    /// <inheritdoc cref="StopCleaning"/>
    public async Task StopCleaningAsync() => await CallMiotActionAsync(2, 2).ConfigureAwait(false);

    /// <summary>
    /// Go to the dock station for charging
    /// </summary>
    public void GoHome() => RunSync(() => GoHomeAsync());

    /// <inheritdoc cref="GoHome"/>
    public async Task GoHomeAsync() => await CallMiotActionAsync(3, 1).ConfigureAwait(false);

    /// <summary>
    /// Set vacuum voice level
    /// </summary>
    /// <param name="level">Voice level should be in range 0 - 10 </param>
    public void SetVoiceLevel(ushort level) => RunSync(() => SetVoiceLevelAsync(level));

    /// <inheritdoc cref="SetVoiceLevel"/>
    public async Task SetVoiceLevelAsync(ushort level)
    {
        if (level < 0 || level > 10) throw new ArgumentOutOfRangeException(nameof(level));

        await SetMiotPropertyAsync(4, 2, level).ConfigureAwait(false);
    }

    /// <summary>
    /// Get vacuum voice level
    /// Voice level is in range 0 - 10, with step 1
    /// </summary>
    public ushort GetVoiceLevel() => RunSync(() => GetVoiceLevelAsync());

    /// <inheritdoc cref="GetVoiceLevel"/>
    public async Task<ushort> GetVoiceLevelAsync() => await ReadMiotValueAsync(4, 2, value => ushort.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);

    /// <summary>
    /// Get vacuum no disturbing settings
    /// </summary>
    public NoDisturbingSettings GetDndSettings() => RunSync(() => GetDndSettingsAsync());

    /// <inheritdoc cref="GetDndSettings"/>
    public async Task<NoDisturbingSettings> GetDndSettingsAsync()
    {
        var str = (await GetMiotPropertyAsync(12, 7).ConfigureAwait(false));
        const string operation = "Unable to read DND settings (12/7)";
        str = str.Trim();
        if (str.Length < 2 || str[0] != '[' || str[str.Length - 1] != ']')
            throw new InvalidOperationException($"{operation}: expected a bracketed list.");
        var parts = str.Substring(1, str.Length - 2).Split(',');
        if (parts.Length != 5) throw new InvalidOperationException($"{operation}: expected five values.");
        var arr = MiioResponseReader.ConvertValue(() => parts.Select(x => short.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray(), operation);
        return new NoDisturbingSettings
        {
            IsNoDisturbingEnabled = arr[0] == 1,
            DndStartingHour = arr[1],
            DndStartingMinute = arr[2],
            DndEndingHour = arr[3],
            DndEndingMinute = arr[4]
        };
    }

    /// <summary>
    /// Get sweep consumables info
    /// </summary>
    /// <returns>Data about supported consumables like main brush/side brush/mop/hepa filter etc</returns>
    public SweepConsumables GetConsumables() => RunSync(() => GetConsumablesAsync());

    /// <inheritdoc cref="GetConsumables"/>
    public async Task<SweepConsumables> GetConsumablesAsync()
    {
        var values = (await GetMiotPropertiesAsync([(7, 8), (7, 9), (7, 10), (7, 11), (7, 12), (7, 13), (7, 14), (7, 15)]).ConfigureAwait(false));

        return MiioResponseReader.ConvertValue(() => new SweepConsumables()
        {
            SideBrushLifePercent = ushort.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture),
            SideBrushLifeHours = uint.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture),
            MainBrushLifePercent = ushort.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture),
            MainBrushLifeHours = uint.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture),
            HepaFilterLifePercent = ushort.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture),
            HepaFilterLifeHours = uint.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture),
            MopLifePercent = ushort.Parse(values[6], System.Globalization.CultureInfo.InvariantCulture),
            MopLifeHours = uint.Parse(values[7], System.Globalization.CultureInfo.InvariantCulture)
        }, "Unable to read sweep consumables (7/8-15)");
    }
    
    public override string ToString() => GetDeviceInfo(MARKET_MODEL, MODEL);
    
    public class NoDisturbingSettings
    {
        public bool IsNoDisturbingEnabled { get; internal set; }
        public int DndStartingHour { get; internal set; }
        public int DndStartingMinute { get; internal set; }
        public int DndEndingHour { get; internal set; }
        public int DndEndingMinute { get; internal set; }
    }
    public class SweepConsumables
    {    
        public uint SideBrushLifeHours { get; internal set; }
        public ushort SideBrushLifePercent { get; internal set; }
        public ushort MainBrushLifePercent { get; internal set; }
        public uint MainBrushLifeHours { get; internal set; }
        public ushort HepaFilterLifePercent { get; internal set; }
        public uint HepaFilterLifeHours { get; internal set; }
        public ushort MopLifePercent { get; internal set; }
        public uint MopLifeHours { get; internal set; }
    }
}

