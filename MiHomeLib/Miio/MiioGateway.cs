using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MiHomeLib.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("MiHomeUnitTests")]

namespace MiHomeLib.Devices
{
    public class MiioGateway : MiioDevice
    {
        public const string version = "lumi.gateway.v3";

        private readonly string[] _armingStates = new[] { "on", "off" };

        public static event EventHandler<DiscoverEventArgs> OnDiscovered;

        public MiioGateway(string ip, string token) : base(new MiioTransport(ip, token)) { }

        internal MiioGateway(IMiioTransport transport) : base(transport) { }

        public bool IsArmingOn()
        {
            var msg = _miioTransport.SendMessage(BuildParamsArray("get_arming", new string[0]));

            return CheckArmingState(msg);
        }

        public async Task<bool> IsArmingOnAsync()
        {   
            var msg = await _miioTransport.SendMessageAsync(BuildParamsArray("get_arming", new string[0])).ConfigureAwait(false);

            return CheckArmingState(msg);
        }

        private bool CheckArmingState(string msg)
        {
            var result = GetString(msg);

            if (!_armingStates.Contains(result))
            {
                throw new Exception($"Arming state is unknown, looks like miio protocol error --> '{msg}'");
            };

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
            var msg = BuildParamsArray("get_alarming_volume", new string[0]);
            var result = _miioTransport.SendMessage(msg);
            return CheckArmingVolumeResult(result);
        }

        public async Task<int> GetArmingVolumeAsync()
        {
            var msg = BuildParamsArray("get_alarming_volume", new string[0]);
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
            var msg = BuildParamsArray("get_arming_time", new string[0]);
            var result = _miioTransport.SendMessage(msg);
            return CheckArmingLastTimeTriggeredResult(result);
        }

        /// <summary>
        /// Get last time when alarm was triggered unix timestamp 
        /// </summary>
        /// <returns>unix timestamp seconds</returns>
        public async Task<int> GetArmingLastTimeTriggeredTimestampAsync()
        {
            var msg = BuildParamsArray("get_arming_time", new string[0]);
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
            var channelsJson = JObject.Parse(response)["result"]["chs"].ToString();            
            var radioChannels = JsonConvert.DeserializeObject<List<RadioChannel>>(channelsJson);

            return radioChannels.Skip(1).ToList();
        }

        /// <summary>
        /// Get list or radio channels from gateway in format {id: <int>, url: <url>}
        /// </summary>
        /// <returns>List of RadioChannel</returns>
        public async Task<List<RadioChannel>> GetRadioChannelsAsync()
        {
            var response = await _miioTransport.SendMessageAsync(BuildParamsObject("get_channels", new { start = 0 }));
            var channelsJson = JObject.Parse(response)["result"]["chs"].ToString();

            return JsonConvert.DeserializeObject<List<RadioChannel>>(channelsJson);
        }

        /// <summary>
        /// Add custom radio channel to the gateway
        /// </summary>
        public void AddRadioChannel(int channelId, string channelUrl)
        {
            if (channelId < 1024) throw new ArgumentException($"Radio channel id must be > 1024");

            if(GetRadioChannels().Any(x => x.Id == channelId))
                throw new ArgumentException($"Radio channel with id {channelId} already exists, choose another id");

            var msg = BuildParamsObject("add_channels", new { chs = new List<RadioChannel>{ new RadioChannel() { Id = channelId, Url = channelUrl, Type = 0} } });
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

            var msg = BuildParamsObject("add_channels", new { chs = new List<RadioChannel> { new RadioChannel() { Id = channelId, Url = channelUrl, Type = 0 } } });
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
    }
}
