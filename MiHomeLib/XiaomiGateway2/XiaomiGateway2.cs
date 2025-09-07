using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.Contracts;
using MiHomeLib.MiioDevices;
using MiHomeLib.Transport;
using MiHomeLib.XiaomiGateway2.Commands;
using MiHomeLib.XiaomiGateway2.Devices;

namespace MiHomeLib.XiaomiGateway2;

/// <summary>
/// Xiaomi Gateway (CN) DGNWG02LM
/// </summary>    
public class XiaomiGateway2 : MiioDevice, IDisposable
{
    public const string MARKET_MODEL = "DGNWG02LM";
    public const string MODEL = "lumi.gateway.v3";
    private readonly string _gatewaySid;
    private static ILoggerFactory _loggerFactory = new NullLoggerFactory();
    private static ILogger<XiaomiGateway2> _logger = _loggerFactory.CreateLogger<XiaomiGateway2>();
    private readonly IMessageTransport _messageTransport;
    private readonly Dictionary<string, XiaomiGateway2SubDevice> _devicesList = [];
    private const int ReadDeviceInterval = 100;
    public enum Sound
    {
        PoliceСar1 = 0,
        PoliceСar2 = 1,
        Accident = 2,
        Countdown = 3,
        Ghost = 4,
        SniperRifle = 5,
        Battle = 6,
        AirRaid = 7,
        Bark = 8,
        Doorbell = 10,
        KnockAtDoor = 11,
        Amuse = 12,
        AlarmClock = 13,
        Mimix = 20,
        Enthusiastic = 21,
        GuitarClassic = 22,
        IceWorldPiano = 23,
        LeisureTime = 24,
        ChildHood = 25,
        MorningStreamList = 26,
        MusicBox = 27,
        Orange = 28,
        Thinker = 29,
        Custom1 = 10001,
        Custom2 = 10002,
        Custom3 = 10003,
    }
    private readonly List<ResponseCommandType> _supportedCommands =
    [
        ResponseCommandType.GetIdListAck,
        ResponseCommandType.ReadAck,
        ResponseCommandType.Report,
        ResponseCommandType.Hearbeat
    ];

    public event Func<XiaomiGateway2SubDevice, Task> OnDeviceDiscoveredAsync = (_) => Task.CompletedTask;
    private readonly Dictionary<string, Func<string, int, XiaomiGateway2SubDevice>> _supportedModels = [];
    public XiaomiGateway2(string ip, string token, string gatewaySid = null)
        : this(new MiioTransport(ip, token), new UdpTransport(), gatewaySid) { }

    internal XiaomiGateway2(IMiioTransport miioTransport, IMessageTransport messageTransport, string gatewaySid, int externalId = 0) : base(miioTransport, externalId)
    {
        _messageTransport = messageTransport;
        _gatewaySid = gatewaySid;

        var gwPassword = GetDeveloperKey();

        // Building map of the supported devices via reflection props 
        foreach (Type type in Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(XiaomiGateway2SubDevice))))
        {
            var bindFlags = BindingFlags.Public | BindingFlags.Static;
            var model = type.GetField("MODEL", bindFlags).GetValue(type).ToString();

            XiaomiGateway2SubDevice addDevice(string sid, int shortId) =>
                type.IsSubclassOf(typeof(ManageableXiaomiGateway2SubDevice)) ?
                    Activator.CreateInstance(type, sid, shortId, _messageTransport, gwPassword, _loggerFactory) as ManageableXiaomiGateway2SubDevice :
                    Activator.CreateInstance(type, sid, shortId, _loggerFactory) as XiaomiGateway2SubDevice;

            _supportedModels.Add(model, (sid, shortId) => addDevice(sid, shortId));
        }
    }

    public static void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        if (loggerFactory is null) return;

        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<XiaomiGateway2>();
    }

    public void DiscoverDevices()
    {
        _messageTransport.OnMessageReceived += async str =>
        {
            try
            {
                _logger?.LogDebug(str);

                var cmd = ResponseCommand.FromString(str);

                if (!_supportedCommands.Contains(cmd.Command))
                {
                    _logger?.LogWarning($"Command '{cmd.RawCommand}' is not a response command, skipping it");
                    return;
                }

                if (cmd.Command == ResponseCommandType.GetIdListAck)
                {
                    if (_gatewaySid != null && _gatewaySid != cmd.Sid)
                    {
                        _logger?.LogWarning($"Discovered gateway with sid '{cmd.Sid}', but expected gateway must be with sid '{_gatewaySid}'. Skipping it...");
                        return;
                    }

                    _messageTransport.Token = cmd.Token;
                    _messageTransport.SendCommand(new ReadDeviceCommand(cmd.Sid)); // read props of gateway itself

                    foreach (var sid in JsonNode.Parse(cmd.Data).AsArray())
                    {
                        Task.Delay(ReadDeviceInterval).Wait(); // need some time in order not to loose messages
                        _messageTransport.SendCommand(new ReadDeviceCommand(sid.ToString()));
                    }

                    return;
                }

                if (!_supportedModels.ContainsKey(cmd.Model))
                {
                    _logger?.LogWarning($"Model '{cmd.Model}' is not supported, please contribute to support");
                    return;
                }

                if (!_devicesList.ContainsKey(cmd.Sid))
                {
                    var supportedDevice = _supportedModels[cmd.Model](cmd.Sid, cmd.ShortId);
                    _devicesList.Add(cmd.Sid, supportedDevice);
                    supportedDevice.ParseData(cmd.Data);

                    await OnDeviceDiscoveredAsync(supportedDevice);
                    return;
                }

                _devicesList.TryGetValue(cmd.Sid, out var gw2SubDevice);

                if (gw2SubDevice is XiaomiMultifunctionalGateway2 && cmd.Token is not null)
                {
                    _messageTransport.Token = cmd.Token;
                }

                gw2SubDevice.ParseData(cmd.Data);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Unexpected error");
            }
        };

        _messageTransport.SendCommand(new DiscoverGatewayCommand());
    }

    public void SetDeveloperKey(string key)
    {
        SetDeveloperKeyAsync(key).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task SetDeveloperKeyAsync(string key)
    {
        if (key.Length != 16)
        {
            throw new ArgumentException("Developer key must be exactly 16 characters long");
        }

        var msg = BuildParamsArray("set_lumi_dpf_aes_key", key);

        CheckMessage(await _miioTransport.SendMessageAsync(msg), "Unable to set developer key");
    }

    public string GetDeveloperKey()
    {
        return GetDeveloperKeyAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task<string> GetDeveloperKeyAsync()
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsArray("get_lumi_dpf_aes_key"));
        var json = JsonNode.Parse(response).AsObject();

        if (
                json.ContainsKey("error") ||
                !json.ContainsKey("result") ||
                json["result"].AsArray().Count == 0 ||
                json["result"][0].ToString() == string.Empty)
        {
            throw new Exception("Unable to get developer key, please set development mode for your gateway according to <link to wiki page here>");
        }

        return json["result"][0].ToString();
    }

    public void EnableNightLight(byte r, byte g, byte b, int brightness)
    {
        EnableNightLightAsync(r, g, b, brightness).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task EnableNightLightAsync(byte r, byte g, byte b, int brightness)
    {
        await SetLightAsync("set_night_light_rgb", r, g, b, brightness);
    }

    public void DisableNightLight()
    {
        DisableNightLightAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task DisableNightLightAsync()
    {
        await SetLightAsync("set_night_light_rgb", 0, 0, 0, 0);
    }

    public void EnableLight(byte r, byte g, byte b, int brightness)
    {
        EnableLightAsync(r, g, b, brightness).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task EnableLightAsync(byte r, byte g, byte b, int brightness)
    {
        await SetLightAsync("set_rgb", r, g, b, brightness);
    }

    public void DisableLight()
    {
        DisableLightAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task DisableLightAsync()
    {
        await SetLightAsync("set_rgb", 0, 0, 0, 0);
    }

    private async Task SetLightAsync(string dayOrNight, byte r, byte g, byte b, int brightness)
    {
        if (brightness < 0 || brightness > 100) throw new ArgumentOutOfRangeException("Brightness must be in range 1 - 100");

        var rgb = (uint)brightness << 24 | (uint)r << 16 | (uint)g << 8 | b;

        var msg = BuildParamsArray(dayOrNight, rgb, brightness);

        CheckMessage(await _miioTransport.SendMessageAsync(msg), $"Unable to send command '{dayOrNight}' with {rgb} and {brightness}");
    }

    public void PlaySound(Sound sound, int volume)
    {
        PlaySoundAsync(sound, volume).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task PlaySoundAsync(Sound sound, int volume)
    {
        if (volume < 0 || volume > 100) throw new ArgumentException("Volume must be in range 0-100");
        var msg = BuildParamsArray("play_music_new", ((int)sound).ToString(), volume);
        CheckMessage(await _miioTransport.SendMessageAsync(msg), $"Unable to play sound {sound}");
    }

    public void SoundsOff()
    {
        SoundsOffAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task SoundsOffAsync()
    {
        await PlaySoundAsync(0, 0);
    }

    public void PlayCustomSound(int soundNo, int volume)
    {
        PlayCustomSoundAsync(soundNo, volume).ConfigureAwait(false).GetAwaiter().GetResult();
    }
    public async Task PlayCustomSoundAsync(int soundNo, int volume)
    {
        if (soundNo <= 10_000) throw new ArgumentException("Custom sounds must start from 10001");
        if (volume < 0 || volume > 100) throw new ArgumentException("Volume must be in range 0-100");

        var msg = BuildParamsArray("play_music_new", soundNo.ToString(), volume);
        CheckMessage(await _miioTransport.SendMessageAsync(msg), $"Unable to play custom sound {soundNo}");
    }

    public bool IsArmingOn()
    {
        var msg = _miioTransport.SendMessage(BuildParamsArray("get_arming", []));

        return CheckArmingState(msg);
    }

    public async Task<bool> IsArmingOnAsync()
    {
        var msg = await _miioTransport.SendMessageAsync(BuildParamsArray("get_arming", [])).ConfigureAwait(false);

        return CheckArmingState(msg);
    }

    private bool CheckArmingState(string msg)
    {
        string[] _armingStates = ["on", "off"];

        var result = JsonNode.Parse(msg)["result"][0].ToString();

        if (!_armingStates.Contains(result))
        {
            throw new Exception($"Arming state is unknown, looks like miio protocol error --> '{msg}'");
        }
        ;

        return result == "on";
    }

    public void SetArmingOff()
    {
        var msg = BuildParamsArray("set_arming", "off");
        var result = _miioTransport.SendMessage(msg);
        CheckMessage(result, "Unable to set arming off");
    }

    public async Task SetArmingOffAsync()
    {
        var msg = BuildParamsArray("set_arming", "off");
        var result = await _miioTransport.SendMessageAsync(msg).ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming off");
    }

    public void SetArmingOn()
    {
        var msg = BuildParamsArray("set_arming", "on");
        var result = _miioTransport.SendMessage(msg);
        CheckMessage(result, "Unable to set arming on");
    }

    public async Task SetArmingOnAsync()
    {
        var msg = BuildParamsArray("set_arming", "on");
        var result = await _miioTransport.SendMessageAsync(msg).ConfigureAwait(false);
        CheckMessage(result, "Unable to set arming on");
    }

    public int GetArmingWaitTime()
    {
        var msg = BuildParamsArray("get_arm_wait_time", new string[0]);
        var result = _miioTransport.SendMessage(msg);
        return CheckArmingWaitResult(result);
    }

    public async Task<int> GetArmingWaitTimeAsync()
    {
        var msg = BuildParamsArray("get_arm_wait_time", new string[0]);
        var result = await _miioTransport.SendMessageAsync(msg);
        return CheckArmingWaitResult(result);
    }

    private int CheckArmingWaitResult(string result)
    {
        return GetInteger(result, "Unable to get arming wait time");
    }

    public void SetArmingWaitTime(int seconds)
    {
        var msg = BuildParamsArray("set_arm_wait_time", seconds);
        var result = _miioTransport.SendMessage(msg);
        CheckMessage(result, "Unable to set arming wait time");
    }

    public async Task SetArmingWaitTimeAsync(int seconds)
    {
        var msg = BuildParamsArray("set_arm_wait_time", seconds);
        var result = await _miioTransport.SendMessageAsync(msg);
        CheckMessage(result, "Unable to set arming wait time");
    }

    public int GetArmingOffTime()
    {
        var msg = BuildParamsArray("get_device_prop", "lumi.0", "alarm_time_len");
        var result = _miioTransport.SendMessage(msg);
        return CheckArmingOffTimeResult(result);
    }

    public async Task<int> GetArmingOffTimeAsync()
    {
        var msg = BuildParamsArray("get_device_prop", "lumi.0", "alarm_time_len");
        var result = await _miioTransport.SendMessageAsync(msg);
        return CheckArmingOffTimeResult(result);
    }

    private int CheckArmingOffTimeResult(string result)
    {
        return GetInteger(result, "Unable to get arming off time");
    }

    public void SetArmingOffTime(int seconds)
    {
        var msg = BuildSidProp("set_device_prop", "lumi.0", "alarm_time_len", seconds);
        var result = _miioTransport.SendMessage(msg);
        CheckMessage(result, "Unable to set arming off time");
    }

    public async Task SetArmingOffTimeAsync(int seconds)
    {
        var msg = BuildSidProp("set_device_prop", "lumi.0", "alarm_time_len", seconds);
        var result = await _miioTransport.SendMessageAsync(msg);
        CheckMessage(result, "Unable to set arming off time");
    }

    public int GetArmingBlinkingTime()
    {
        var msg = BuildParamsArray("get_device_prop", "lumi.0", "en_alarm_light");
        var result = _miioTransport.SendMessage(msg);
        return CheckArmingBlinkingResult(result);
    }

    public async Task<int> GetArmingBlinkingTimeAsync()
    {
        var msg = BuildParamsArray("get_device_prop", "lumi.0", "en_alarm_light");
        var result = await _miioTransport.SendMessageAsync(msg);
        return CheckArmingBlinkingResult(result);
    }

    private int CheckArmingBlinkingResult(string result)
    {
        return GetInteger(result, "Unable to get arming blinking time");
    }

    public void SetArmingBlinkingTime(int seconds)
    {
        var msg = BuildSidProp("set_device_prop", "lumi.0", "en_alarm_light", seconds);
        var result = _miioTransport.SendMessage(msg);
        CheckMessage(result, "Unable to set arming blinking time");
    }

    public async Task SetArmingBlinkingTimeAsync(int seconds)
    {
        var msg = BuildSidProp("set_device_prop", "lumi.0", "en_alarm_light", seconds);
        var result = await _miioTransport.SendMessageAsync(msg);
        CheckMessage(result, "Unable to set arming blinking time");
    }

    public int GetArmingVolume()
    {
        var msg = BuildParamsArray("get_alarming_volume", []);
        var result = _miioTransport.SendMessage(msg);
        return CheckArmingVolumeResult(result);
    }

    public async Task<int> GetArmingVolumeAsync()
    {
        var msg = BuildParamsArray("get_alarming_volume", []);
        var result = await _miioTransport.SendMessageAsync(msg);
        return CheckArmingVolumeResult(result);
    }

    private int CheckArmingVolumeResult(string result)
    {
        return GetInteger(result, "Unable to get arming volume level");
    }

    public void SetArmingVolume(int volume)
    {
        var msg = BuildParamsArray("set_alarming_volume", volume);
        var result = _miioTransport.SendMessage(msg);
        CheckMessage(result, "Unable to set arming volume level");
    }

    public async Task SetArmingVolumeAsync(int volume)
    {
        var msg = BuildParamsArray("set_alarming_volume", volume);
        var result = await _miioTransport.SendMessageAsync(msg);
        CheckMessage(result, "Unable to set arming volume level");
    }

    /// <summary>
    /// Get last time when alarm was triggered unix timestamp 
    /// </summary>
    /// <returns>unix timestamp seconds</returns>
    public int GetArmingLastTimeTriggeredTimestamp()
    {
        var msg = BuildParamsArray("get_arming_time", []);
        var result = _miioTransport.SendMessage(msg);
        return CheckArmingLastTimeTriggeredResult(result);
    }

    /// <summary>
    /// Get last time when alarm was triggered unix timestamp 
    /// </summary>
    /// <returns>unix timestamp seconds</returns>
    public async Task<int> GetArmingLastTimeTriggeredTimestampAsync()
    {
        var msg = BuildParamsArray("get_arming_time", []);
        var result = await _miioTransport.SendMessageAsync(msg);
        return CheckArmingLastTimeTriggeredResult(result);
    }

    private int CheckArmingLastTimeTriggeredResult(string result)
    {
        return GetInteger(result, "Unable to get timestamp when arming was triggered");
    }

    /// <summary>
    /// Get list or radio channels from gateway in format {id: <int>, url: <url>}
    /// </summary>
    /// <returns>List of RadioChannel</returns>
    public List<RadioChannel> GetRadioChannels()
    {
        var response = _miioTransport.SendMessage(BuildParamsObject("get_channels", new { start = 0 }));
        var channelsJson = JsonNode.Parse(response)["result"]["chs"].ToString();
        var radioChannels = JsonSerializer.Deserialize<List<RadioChannel>>(channelsJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        return [.. radioChannels];
    }

    /// <summary>
    /// Get list or radio channels from gateway in format {id: <int>, url: <url>}
    /// </summary>
    /// <returns>List of RadioChannel</returns>
    public async Task<List<RadioChannel>> GetRadioChannelsAsync()
    {
        var response = await _miioTransport.SendMessageAsync(BuildParamsObject("get_channels", new { start = 0 }));
        var channelsJson = JsonNode.Parse(response)["result"]["chs"].ToString();
        return JsonSerializer.Deserialize<List<RadioChannel>>(channelsJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
    }

    /// <summary>
    /// Add custom radio channel to the gateway
    /// </summary>
    public void AddRadioChannel(int channelId, string channelUrl)
    {
        if (channelId < 1024) throw new ArgumentException($"Radio channel id must be > 1024");

        if (GetRadioChannels().Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} already exists, choose another id");

        var msg = BuildParamsObject("add_channels", new { chs = new List<RadioChannel> { new() { Id = channelId, Url = channelUrl, Type = 0 } } });
        var result = _miioTransport.SendMessage(msg);

        CheckMessage(result, "Unable to add radio channel");
    }

    /// <summary>
    /// Add custom radio channel to the gateway
    /// </summary>
    public async Task AddRadioChannelAsync(int channelId, string channelUrl)
    {
        if (channelId < 1024) throw new ArgumentException($"Radio channel id must be > 1024");

        if ((await GetRadioChannelsAsync()).Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} already exists, choose another id");

        var msg = BuildParamsObject("add_channels", new { chs = new List<RadioChannel> { new() { Id = channelId, Url = channelUrl, Type = 0 } } });
        var result = await _miioTransport.SendMessageAsync(msg);

        CheckMessage(result, "Unable to add radio channel");
    }

    /// <summary>
    /// Remove custom radio channel from gateway stations list
    /// </summary>
    public void RemoveRadioChannel(int channelId)
    {
        var radioChannels = GetRadioChannels();

        if (!radioChannels.Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} doesn't exist");

        radioChannels.RemoveAll(x => x.Id != channelId);

        var msg = BuildParamsObject("remove_channels", new { chs = radioChannels });
        var result = _miioTransport.SendMessage(msg);

        CheckMessage(result, $"Unable to remove radio channel with id {channelId}");
    }

    /// <summary>
    /// Remove custom radio channel from gateway stations list
    /// </summary>
    public async Task RemoveRadioChannelAsync(int channelId)
    {
        var radioChannels = await GetRadioChannelsAsync();

        if (!radioChannels.Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} doesn't exist");

        radioChannels.RemoveAll(x => x.Id != channelId);

        var msg = BuildParamsObject("remove_channels", new { chs = radioChannels });
        var result = await _miioTransport.SendMessageAsync(msg);

        CheckMessage(result, $"Unable to remove radio channel with id {channelId}");
    }

    /// <summary>
    /// Clear all custom radio channels from the gateway
    /// </summary>
    public void RemoveAllRadioChannels()
    {
        var radioChannels = GetRadioChannels();
        var msg = BuildParamsObject("remove_channels", new { chs = radioChannels });
        var result = _miioTransport.SendMessage(msg);
        CheckMessage(result, "Unable to remove all radio channels");
    }

    /// <summary>
    /// Clear all custom radio channels from the gateway
    /// </summary>
    public async Task RemoveAllRadioChannelsAsync()
    {
        var radioChannels = await GetRadioChannelsAsync();
        var msg = BuildParamsObject("remove_channels", new { chs = radioChannels });
        var result = await _miioTransport.SendMessageAsync(msg);
        CheckMessage(result, "Unable to remove all radio channels");
    }

    /// <summary>
    /// Start playing custom channel
    /// </summary>
    public void PlayRadio(int channelId, int volume)
    {
        if (volume < 0 || volume > 100)
            throw new ArgumentException($"Volume must be within range 0-100");

        if (!GetRadioChannels().Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} doesn't exist");

        var result = _miioTransport.SendMessage(BuildParamsArray("play_specify_fm", channelId, volume));
        CheckMessage(result, $"Unable to play channelId: {channelId} with volume {volume}");
    }

    /// <summary>
    /// Start playing custom channel
    /// </summary>
    public async Task PlayRadioAsync(int channelId, int volume)
    {
        if (volume < 0 || volume > 100)
            throw new ArgumentException($"Volume must be within range 0-100");

        if (!(await GetRadioChannelsAsync()).Any(x => x.Id == channelId))
            throw new ArgumentException($"Radio channel with id {channelId} doesn't exist");

        var result = await _miioTransport.SendMessageAsync(BuildParamsArray("play_specify_fm", channelId, volume));
        CheckMessage(result, $"Unable to play channelId: {channelId} with volume {volume}");
    }

    /// <summary>
    /// Stop playing radio
    /// </summary>
    public void StopRadio()
    {
        var result = _miioTransport.SendMessage(BuildParamsArray("play_fm", "off"));
        CheckMessage(result, $"Unable to stop playing radio");
    }

    /// <summary>
    /// Stop playing radio
    /// </summary>
    public async Task StopRadioAsync()
    {
        var result = await _miioTransport.SendMessageAsync(BuildParamsArray("play_fm", "off"));
        CheckMessage(result, $"Unable to stop playing radio");
    }

    public class RadioChannel
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public int Type { get; set; }

        public override string ToString()
        {
            return $"Radio channel -> Id: {Id}, Url: {Url}";
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            RadioChannel other = (RadioChannel)obj;

            return Id == other.Id && Url == other.Url;
        }

        public override int GetHashCode()
        {
            return new { Id, Url }.GetHashCode();
        }
    }

    public new void Dispose()
    {
        _messageTransport?.Dispose();
        base.Dispose();
    }

    public override string ToString() => $"Model: {MARKET_MODEL} {MODEL}";
}