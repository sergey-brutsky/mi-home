using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MiHomeLib.Events;

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
            var msg = _miioTransport.SendMessage(BuildParams("get_arming", new string[0]));

            return CheckArmingState(msg);
        }

        public async Task<bool> IsArmingOnAsync()
        {   
            var msg = await _miioTransport.SendMessageAsync(BuildParams("get_arming", new string[0])).ConfigureAwait(false);

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
            var msg = BuildParams("set_arming", "off");
            var result = _miioTransport.SendMessage(msg);
            CheckMessage(result, "Unable to set arming off");
        }

        public async Task SetArmingOffAsync()
        {
            var msg = BuildParams("set_arming", "off");
            var result = await _miioTransport.SendMessageAsync(msg).ConfigureAwait(false);
            CheckMessage(result, "Unable to set arming off");
        }

        public void SetArmingOn()
        {
            var msg = BuildParams("set_arming", "on");
            var result = _miioTransport.SendMessage(msg);
            CheckMessage(result, "Unable to set arming on");
        }

        public async Task SetArmingOnAsync()
        {
            var msg = BuildParams("set_arming", "on");
            var result = await _miioTransport.SendMessageAsync(msg).ConfigureAwait(false);
            CheckMessage(result, "Unable to set arming on");
        }

        public int GetArmingWaitTime()
        {
            var msg = BuildParams("get_arm_wait_time", new string[0]);
            var result = _miioTransport.SendMessage(msg);
            return CheckArmingWaitResult(result);
        }

        public async Task<int> GetArmingWaitTimeAsync()
        {
            var msg = BuildParams("get_arm_wait_time", new string[0]);
            var result = await _miioTransport.SendMessageAsync(msg);
            return CheckArmingWaitResult(result);
        }

        private int CheckArmingWaitResult(string result)
        {
            return GetInteger(result, "Unable to get arming wait time");
        }

        public void SetArmingWaitTime(int seconds)
        {
            var msg = BuildParams("set_arm_wait_time", seconds);
            var result = _miioTransport.SendMessage(msg);
            CheckMessage(result, "Unable to set arming wait time");
        }

        public async Task SetArmingWaitTimeAsync(int seconds)
        {
            var msg = BuildParams("set_arm_wait_time", seconds);
            var result = await _miioTransport.SendMessageAsync(msg);
            CheckMessage(result, "Unable to set arming wait time");
        }

        public int GetArmingOffTime()
        {
            var msg = BuildParams("get_device_prop", "lumi.0", "alarm_time_len");
            var result = _miioTransport.SendMessage(msg);
            return CheckArmingOffTimeResult(result);
        }

        public async Task<int> GetArmingOffTimeAsync()
        {
            var msg = BuildParams("get_device_prop", "lumi.0", "alarm_time_len");
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
            var msg = BuildParams("get_device_prop", "lumi.0", "en_alarm_light");
            var result = _miioTransport.SendMessage(msg);
            return CheckArmingBlinkingResult(result);
        }

        public async Task<int> GetArmingBlinkingTimeAsync()
        {
            var msg = BuildParams("get_device_prop", "lumi.0", "en_alarm_light");
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
            var msg = BuildParams("get_alarming_volume", new string[0]);
            var result = _miioTransport.SendMessage(msg);
            return CheckArmingVolumeResult(result);
        }

        public async Task<int> GetArmingVolumeAsync()
        {
            var msg = BuildParams("get_alarming_volume", new string[0]);
            var result = await _miioTransport.SendMessageAsync(msg);
            return CheckArmingVolumeResult(result);
        }

        private int CheckArmingVolumeResult(string result)
        {
            return GetInteger(result, "Unable to get arming volume level");
        }

        public void SetArmingVolume(int volume)
        {
            var msg = BuildParams("set_alarming_volume", volume);
            var result = _miioTransport.SendMessage(msg);
            CheckMessage(result, "Unable to set arming volume level");
        }

        public async Task SetArmingVolumeAsync(int volume)
        {
            var msg = BuildParams("set_alarming_volume", volume);
            var result = await _miioTransport.SendMessageAsync(msg);
            CheckMessage(result, "Unable to set arming volume level");
        }

        /// <summary>
        /// Get last time when alarm was triggered unix timestamp 
        /// </summary>
        /// <returns>unix timestamp seconds</returns>
        public int GetArmingLastTimeTriggeredTimestamp()
        {
            var msg = BuildParams("get_arming_time", new string[0]);
            var result = _miioTransport.SendMessage(msg);
            return CheckArmingLastTimeTriggeredResult(result);
        }

        /// <summary>
        /// Get last time when alarm was triggered unix timestamp 
        /// </summary>
        /// <returns>unix timestamp seconds</returns>
        public async Task<int> GetArmingLastTimeTriggeredTimestampAsync()
        {
            var msg = BuildParams("get_arming_time", new string[0]);
            var result = await _miioTransport.SendMessageAsync(msg);
            return CheckArmingLastTimeTriggeredResult(result);
        }

        private int CheckArmingLastTimeTriggeredResult(string result)
        {
            return GetInteger(result, "Unable to get timestamp when arming was triggered");
        }

        public void GetRadioChannels()
        {
            //var a = _miioTransport.SendMessage("{\"id\":1, \"method\":\"get_channels\",\"params\":{\"start\":0}}");
            //var b = 1;
        }

        public void AddRadioChannel()
        {
            //var a = _miioTransport.SendMessage("{\"id\":1, \"method\":\"get_channels\",\"params\":{\"start\":0}}");
            //var b = 1;
        }

        public void RemoveRadioChannel()
        {
            //var a = _miioTransport.SendMessage("{\"id\":1, \"method\":\"get_channels\",\"params\":{\"start\":0}}");
            //var b = 1;
        }

        public void PlayRadioChannel()
        {
            //var a = _miioTransport.SendMessage("{\"id\":1, \"method\":\"get_channels\",\"params\":{\"start\":0}}");
            //var b = 1;
        }
    }
}
