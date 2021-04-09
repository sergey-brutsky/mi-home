using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MiHomeLib.Events;

[assembly: InternalsVisibleTo("MiHomeUnitTests")]

namespace MiHomeLib.Devices
{
    public class AirHumidifier: MiioDevice
    {
        public enum Mode
        {
            Silent,
            Medium,
            High
        };

        public enum Brightness
        {
            Bright = 0,
            Dim = 1,
            Off = 2,
        };

        private readonly Dictionary<string, Mode> _mapMode = new Dictionary<string, Mode>
        {
            { "high", Mode.High},
            { "medium", Mode.Medium},
            { "silent", Mode.Silent},
        };

        public const string version = "zhimi.humidifier.v1";

        public static event EventHandler<DiscoverEventArgs> OnDiscovered;

        public AirHumidifier(string ip, string token) : base(new MiioTransport(ip, token)) { }

        internal AirHumidifier(IMiioTransport transport) : base(transport) { }

        public override string ToString()
        {
            var values = GetProps("power", "mode", "temp_dec", "humidity", "led_b", "buzzer", "child_lock", "limit_hum");
            var temp = $"{values[2].Substring(0, 2)}.{values[2].Substring(2, 1)} °C";
            var brightness = ((Brightness)Enum.Parse(typeof(Brightness), values[4])).ToString().ToLower();

            return $"Power: {values[0]}\nMode: {values[1]}\nTemperature: {temp}\n" +
                $"Humidity: {values[3]}%\nLED brightness: {brightness}\n" +
                $"Buzzer: {values[5]}\nChild lock: {values[6]}\nTarget humidity: {values[7]}%\n" +
                $"Model: {version}\nIP Address:{_miioTransport.Ip}\nToken: {_miioTransport.Token}";
        }

        public static void DiscoverDevices()
        {
            var discoveredHumidifiers = MiioTransport
                .SendDiscoverMessage()
                .Where(x => x.type == "0404"); // magic number identifying air humidifier

            foreach(var (ip, type, serial, token) in discoveredHumidifiers)
            {
                OnDiscovered?.Invoke(null, new DiscoverEventArgs(ip, type, serial, token));
            }
        }

        public bool IsTurnedOn()
        {
            return GetProps("power")[0] == "on";
        }

        public async Task<bool> IsTurnedOnAsync()
        {
            return (await GetPropsAsync("power").ConfigureAwait(false))[0] == "on";
        }

        public void PowerOn()
        {
            var response = _miioTransport.SendMessage(BuildParams("set_power", "on"));
            CheckMessage(response, "Unable to power on air humidifier");
        }

        public async Task PowerOnAsync()
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_power", "on")).ConfigureAwait(false);
            CheckMessage(response, "Unable to power on air humidifier");
        }

        public void PowerOff()
        {
            var response = _miioTransport.SendMessage(BuildParams("set_power", "off"));
            CheckMessage(response, "Unable to power off air humidifier");
        }

        public async Task PowerOffAsync()
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_power", "off")).ConfigureAwait(false);
            CheckMessage(response, "Unable to power off air humidifier");
        }

        public Mode GetDeviceMode()
        {
            return _mapMode[GetProps("mode")[0]];
        }

        public async Task<Mode> GetDeviceModeAsync()
        {
            return _mapMode[(await GetPropsAsync("mode").ConfigureAwait(false))[0]];
        }

        public void SetMode(Mode mode)
        {
            var response = _miioTransport.SendMessage(BuildParams("set_mode", mode.ToString().ToLower()));
            CheckMessage(response, "Unable to set fan mode of air humidifier");
        }

        public async Task SetModeAsync(Mode mode)
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_mode", mode.ToString().ToLower())).ConfigureAwait(false);
            CheckMessage(response, "Unable to set fan mode of air humidifier");
        }

        public float GetTemperature()
        {
            return int.Parse(GetProps("temp_dec")[0]) / 10f;
        }

        public async Task<float> GetTemperatureAsync()
        {
            return int.Parse((await GetPropsAsync("temp_dec").ConfigureAwait(false))[0]) / 10f;
        }

        public int GetHumidity()
        {
            return int.Parse(GetProps("humidity")[0]);
        }

        public async Task<int> GetHumidityAsync()
        {
            return int.Parse((await GetPropsAsync("humidity").ConfigureAwait(false))[0]);
        }

        public Brightness GetBrightness()
        {
            return (Brightness)Enum.Parse(typeof(Brightness), GetProps("led_b")[0]);
        }

        public async Task<Brightness> GetBrightnessAsync()
        {
            return (Brightness)Enum.Parse(typeof(Brightness), (await GetPropsAsync("led_b").ConfigureAwait(false))[0]);
        }

        public void SetBrightness(Brightness brightness)
        {
            var response = _miioTransport.SendMessage(BuildParams("set_led_b", brightness));
            CheckMessage(response, "Unable to set brightness of air humidifier");
        }

        public async Task SetBrightnessAsync(Brightness brightness)
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_led_b", brightness)).ConfigureAwait(false);
            CheckMessage(response, "Unable to set brightness of air humidifier");
        }

        public int GetTargetHumidity()
        {
            return int.Parse(GetProps("limit_hum")[0]);
        }

        public async Task<int> GetTargetHumidityAsync()
        {
            return int.Parse((await GetPropsAsync("limit_hum").ConfigureAwait(false))[0]);
        }

        public bool IsBuzzerOn()
        {
            return GetProps("buzzer")[0] == "on";
        }

        public async Task<bool> IsBuzzerOnAsync()
        {
            return (await GetPropsAsync("buzzer").ConfigureAwait(false))[0] == "on";
        }

        public void BuzzerOn()
        {
            var response = _miioTransport.SendMessage(BuildParams("set_buzzer", "on"));
            CheckMessage(response, "Unable to enable buzzer on air humidifier");
        }

        public async Task BuzzerOnAsync()
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_buzzer", "on")).ConfigureAwait(false);
            CheckMessage(response, "Unable to enable buzzer on air humidifier");
        }

        public void BuzzerOff()
        {
            var response = _miioTransport.SendMessage(BuildParams("set_buzzer", "off"));
            CheckMessage(response, "Unable to disable buzzer on air humidifier");
        }

        public async Task BuzzerOffAsync()
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_buzzer", "off")).ConfigureAwait(false);
            CheckMessage(response, "Unable to disable buzzer on air humidifier");
        }

        public bool IsChildLockOn()
        {
            return GetProps("child_lock")[0] == "on";
        }

        public async Task<bool> IsChildLockOnAsync()
        {
            return (await GetPropsAsync("child_lock").ConfigureAwait(false))[0] == "on";
        }

        public void ChildLockOn()
        {
            var response = _miioTransport.SendMessage(BuildParams("set_child_lock", "on"));
            CheckMessage(response, "Unable to enable child lock on air humidifier");
        }

        public async Task ChildLockOnAsync()
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_child_lock", "on")).ConfigureAwait(false);
            CheckMessage(response, "Unable to enable child lock on air humidifier");
        }

        public void ChildLockOff()
        {
            var response = _miioTransport.SendMessage(BuildParams("set_child_lock", "off"));
            CheckMessage(response, "Unable to disable child lock on air humidifier");
        }

        public async Task ChildLockOffAsync()
        {
            var response = await _miioTransport.SendMessageAsync(BuildParams("set_child_lock", "off")).ConfigureAwait(false);
            CheckMessage(response, "Unable to disable child lock on air humidifier");
        }
    }
}
