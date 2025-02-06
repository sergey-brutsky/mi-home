using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MiHomeLib.Events;
using MiHomeLib.Transport;

[assembly: InternalsVisibleTo("MiHomeUnitTests")]

namespace MiHomeLib.MiioDevices;

public class MiRobotV1 : MiioDevice
{
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

    public const string version = "rockrobo.vacuum.v1";

    public static event EventHandler<DiscoverEventArgs> OnDiscovered;

    public MiRobotV1(string ip, string token, int clientId = 0) :
        base(new MiioTransport(ip, token), clientId)
    { }

    internal MiRobotV1(IMiioTransport transport) : base(transport, 0) { }

    public override string ToString()
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("get_status", ""));

        var values = JsonNode.Parse(response)["result"][0].Deserialize<Dictionary<string, object>>().ToDictionary(x => x.Key, x => x.Value.ToString());

        return $"Model: {version}\nState: {(Status)int.Parse(values["state"])}\n" +
            $"Battery: {values["battery"]} %\nFanspeed: {values["fan_power"]} %\n" +
            $"Cleaning since: {values["clean_time"]} seconds\n" +
            $"Cleaned area: {(float)int.Parse(values["clean_area"]) / 1_000_000} m²\n" +
            $"IP Address: {_miioTransport.Ip}\nToken: {_miioTransport.Token}";
    }

    /// <summary>
    /// Find all available mi robot vacuums in the LAN
    /// </summary>
    public static void DiscoverDevices()
    {
        var discoveredRobots = MiioTransport
            .SendDiscoverMessage()
            .Where(x => x.type == "05c5"); // magic number identifying rockrobo.vacuum.v1

        foreach (var (ip, type, serial, token) in discoveredRobots)
        {
            OnDiscovered?.Invoke(null, new DiscoverEventArgs(ip, type, serial, token));
        }
    }

    /// <summary>
    /// This will make a robot to give a voice
    /// </summary>
    public void FindMe()
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("find_me", ""));
        CheckMessage(response, "Unable to find mi robot");
    }

    /// <summary>
    /// This will make a robot to give a voice
    /// </summary>
    public async Task FindMeAsync()
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("find_me", ""));
        CheckMessage(response, "Unable to find mi robot");
    }

    /// <summary>
    /// Tell the robot to go to the base station
    /// </summary>
    public void Home()
    {
        var resp1 = _miioTransport.SendMessage(BuildParamsArray("app_pause", ""));
        CheckMessage(resp1, "Unable to tell the robot to go to the base station");
        var resp2 = _miioTransport.SendMessage(BuildParamsArray("app_charge", ""));
        CheckMessage(resp2, "Unable to tell the robot to go to the base station");
    }

    /// <summary>
    /// Tell the robot to go to the base station
    /// </summary>
    public async Task HomeAsync()
    {
        var resp1 = await _miioTransport.SendMessageAsync(BuildParamsArray("app_pause", ""));
        CheckMessage(resp1, "Unable to tell the robot to go to the base station");
        var resp2 = await _miioTransport.SendMessageAsync(BuildParamsArray("app_charge", ""));
        CheckMessage(resp2, "Unable to tell the robot to go to the base station");
    }

    /// <summary>
    /// Tell the robot to start cleaning
    /// </summary>
    public void Start()
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("app_start", ""));
        CheckMessage(response, "Unable to tell the robot to start cleaning");
    }

    /// <summary>
    /// Tell the robot to start cleaning
    /// </summary>
    public async Task StartAsync()
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("app_start", ""));
        CheckMessage(response, "Unable to tell the robot to start cleaning");
    }

    /// <summary>
    /// Tell the robot to stop cleaning
    /// </summary>
    public void Stop()
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("app_stop", ""));
        CheckMessage(response, "Unable to tell the robot to stop cleaning");
    }

    /// <summary>
    /// Tell the robot to stop cleaning
    /// </summary>
    public async Task StopAsync()
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("app_stop", ""));
        CheckMessage(response, "Unable to tell the robot to stop cleaning");
    }

    /// <summary>
    /// Tell the robot to pause cleaning
    /// </summary>
    public void Pause()
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("app_pause", ""));
        CheckMessage(response, "Unable to tell the robot to pause cleaning");
    }

    /// <summary>
    /// Tell the robot to pause cleaning
    /// </summary>
    public async Task PauseAsync()
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("app_pause", ""));
        CheckMessage(response, "Unable to tell the robot to pause cleaning");
    }

    /// <summary>
    /// Tell the robot to start spot cleaning
    /// </summary>
    public void Spot()
    {
        var response = _miioTransport.SendMessage(BuildParamsArray("app_spot", ""));
        CheckMessage(response, "Unable to tell the robot to start spot cleaning");
    }

    /// <summary>
    /// Tell the robot to start spot cleaning
    /// </summary>
    public async Task SpotAsync()
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("app_spot", ""));
        CheckMessage(response, "Unable to tell the robot to start spot cleaning");
    }
}
