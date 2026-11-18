using MiHomeLib.MiioDevices.Internal;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiHomeLib.MiioDevices;

public class XiaomiRobotVacuumCleanerV1 : MiioDevice
{
    public const string MARKET_MODEL = "SDJQR02RR";
    public const string MODEL = "rockrobo.vacuum.v1";
    
    public enum Status
    {
        Unknown1 = 1,
        ChargerDisconnected = 2,
        Idle = 3,
        Unknown2 = 4,
        Cleaning = 5,
        ReturningHome = 6,
        ManualMode = 7,
        Charging = 8,
        Unknown3 = 9,
        Paused = 10,
        SpotCleaning = 11,
        Error = 12,
        Unknown4 = 13,
        Updating = 14,
    };
    
    [Obsolete("Use MiioDiscovery.DiscoverAsync and filter by DeviceType \"05c5\".")]
    public static event Action<string, string, string, string> OnDiscovered;

    public XiaomiRobotVacuumCleanerV1(string ip, string token): base(new MiioTransport(ip, token)) {}

    internal XiaomiRobotVacuumCleanerV1(IMiioTransport transport) : base(transport) { }

    /// <summary>A snapshot from one get_status request. Cleaning time is in seconds and area is in square metres.</summary>
    public sealed record DeviceStatus(Status State, int BatteryPercent, int FanPower, int CleaningSeconds, float CleanedAreaSquareMeters);

    /// <summary>Reads the current status without retaining a cached state.</summary>
    public DeviceStatus GetStatus() => RunSync(GetStatusAsync);

    /// <inheritdoc cref="GetStatus"/>
    public async Task<DeviceStatus> GetStatusAsync()
    {
        var response = await RequestArrayAsync("get_status", "").ConfigureAwait(false);
        const string operation = "Unable to read vacuum status";
        using var document = MiioResponseReader.Parse(response, operation);
        var status = MiioResponseReader.First(document.RootElement.GetProperty("result"), operation);
        if (status.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{operation}: expected a status object.");

        int ReadInteger(string property)
        {
            if (!status.TryGetProperty(property, out var value) ||
                !int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                throw new InvalidOperationException($"{operation}: missing or invalid '{property}'.");
            return number;
        }

        return new DeviceStatus((Status)ReadInteger("state"), ReadInteger("battery"), ReadInteger("fan_power"),
            ReadInteger("clean_time"), (float)ReadInteger("clean_area") / 1_000_000);
    }

    public override string ToString()
    {
        var status = GetStatus();
        return $"Model: {MODEL}\nState: {status.State}\n" +
            $"Battery: {status.BatteryPercent} %\nFanspeed: {status.FanPower} %\n" +
            $"Cleaning since: {status.CleaningSeconds} seconds\n" +
            $"Cleaned area: {status.CleanedAreaSquareMeters} m²\n" +
            $"IP Address: {_miioTransport.Ip}";
    }

    /// <summary>
    /// Find all available mi robot vacuums in the LAN
    /// </summary>
    [Obsolete("Use MiioDiscovery.DiscoverAsync and filter by DeviceType \"05c5\".")]
    public static void DiscoverDevices()
    {
        var devices = RunSync(() => MiioDiscovery.DiscoverAsync());
        foreach (var device in devices.Where(device => device.DeviceType == "05c5"))
            OnDiscovered?.Invoke(device.Ip, device.DeviceType, device.Serial, device.Token);
    }

    /// <summary>
    /// This will make a robot to give a voice
    /// </summary>
    public void FindMe() => RunSync(() => FindMeAsync());

    /// <summary>
    /// This will make a robot to give a voice
    /// </summary>
    public async Task FindMeAsync()
    {
        var response = await RequestArrayAsync("find_me", "").ConfigureAwait(false);
        CheckMessage(response, "Unable to find mi robot");
    }

    /// <summary>
    /// Tell the robot to go to the base station
    /// </summary>
    public void GoHome() => RunSync(() => GoHomeAsync());

    /// <summary>
    /// Tell the robot to go to the base station
    /// </summary>
    public async Task GoHomeAsync()
    {
        var resp1 = await RequestArrayAsync("app_pause", "").ConfigureAwait(false);
        CheckMessage(resp1, "Unable to tell the robot to go to the base station");
        var resp2 = await RequestArrayAsync("app_charge", "").ConfigureAwait(false);
        CheckMessage(resp2, "Unable to tell the robot to go to the base station");
    }

    /// <summary>
    /// Tell the robot to start cleaning
    /// </summary>
    public void StartCleaning() => RunSync(() => StartCleaningAsync());

    /// <summary>
    /// Tell the robot to start cleaning
    /// </summary>
    public async Task StartCleaningAsync()
    {
        var response = await RequestArrayAsync("app_start", "").ConfigureAwait(false);
        CheckMessage(response, "Unable to tell the robot to start cleaning");
    }

    /// <summary>
    /// Tell the robot to stop cleaning
    /// </summary>
    public void StopCleaning() => RunSync(() => StopCleaningAsync());

    /// <summary>
    /// Tell the robot to stop cleaning
    /// </summary>
    public async Task StopCleaningAsync()
    {
        var response = await RequestArrayAsync("app_stop", "").ConfigureAwait(false);
        CheckMessage(response, "Unable to tell the robot to stop cleaning");
    }

    /// <summary>
    /// Tell the robot to pause cleaning
    /// </summary>
    public void Pause() => RunSync(() => PauseAsync());

    /// <summary>
    /// Tell the robot to pause cleaning
    /// </summary>
    public async Task PauseAsync()
    {
        var response = await RequestArrayAsync("app_pause", "").ConfigureAwait(false);
        CheckMessage(response, "Unable to tell the robot to pause cleaning");
    }

    /// <summary>
    /// Tell the robot to start spot cleaning
    /// </summary>
    public void StartSpotCleaning() => RunSync(() => StartSpotCleaningAsync());

    /// <summary>
    /// Tell the robot to start spot cleaning
    /// </summary>
    public async Task StartSpotCleaningAsync()
    {
        var response = await RequestArrayAsync("app_spot", "").ConfigureAwait(false);
        CheckMessage(response, "Unable to tell the robot to start spot cleaning");
    }
}
