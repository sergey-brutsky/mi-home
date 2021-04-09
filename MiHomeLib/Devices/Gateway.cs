using System;
using MiHomeLib.Commands;
using MiHomeLib.Contracts;
using Newtonsoft.Json.Linq;
#pragma warning disable CS0675

namespace MiHomeLib.Devices
{
    public class Gateway : MiHomeDevice
    {
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

        public const string TypeKey = "gateway";

        private readonly IMessageTransport _transport;

        public Gateway(string sid, IMessageTransport transport) : base(sid, TypeKey)
        {
            _transport = transport;
        }

        public string Ip { get; private set; }
        public int Rgb { get; private set; }
        public int Illumination { get; private set; }
        public string ProtoVersion { get; private set; }

        public override void ParseData(string command)
        {
            var jObject = JObject.Parse(command);

            if (jObject.ParseInt("rgb", out int rgb))
            {
                Rgb = rgb;
            }

            if (jObject.ParseInt("illumination", out int illumination))
            {
                Illumination = illumination;
            }

            if (jObject.ParseString("proto_version", out string protoVersion))
            {
                ProtoVersion = protoVersion;
            }

            if(jObject.ParseString("ip", out string ip))
            {
                Ip = ip;
            }
        }

        public override string ToString()
        {
            return $"Rgb: {Rgb}, Illumination: {Illumination}, ProtoVersion: {ProtoVersion}";
        }

        public void EnableLight(byte r = 255, byte g = 255, byte b = 255, int brightness = 100)
        {
            var rgb = (uint)brightness << 24 | r << 16 | g << 8 | b;

            if (brightness < 1 || brightness > 100) throw new ArgumentException("Brightness must be in range 1 - 100");

            _transport.SendWriteCommand(Sid, Type, new GatewayLightCommand(rgb));
        }

        public void DisableLight()
        {
            _transport.SendWriteCommand(Sid, Type, new GatewayLightCommand(0));
        }

        public void SoundsOff()
        {
            _transport.SendWriteCommand(Sid, Type, new PlaySoundCommand(1000, 0));
        }

        public void PlaySound(Sound soundNo, int volume)
        {
            if (volume < 0 || volume > 100) throw new ArgumentException("Volume must be in range 0-100");

            _transport.SendWriteCommand(Sid, Type, new PlaySoundCommand((int)soundNo, volume));
        }

        public void PlayCustomSound(int soundNo, int volume)
        {
            if (soundNo <= 10_000) throw new ArgumentException("Custom sounds must start from 10001");

            if (volume < 0 || volume > 100) throw new ArgumentException("Volume must be in range 0-100");

            _transport.SendWriteCommand(Sid, Type, new PlaySoundCommand(soundNo, volume));
        }

        [Obsolete("Use PlaySound method instead. Will be removed in the future releases.")]
        public void StartPlayMusic(int midNo = 0)
        {
            if (midNo <= 0 || midNo == 9 || midNo > 13 && midNo < 20 || midNo > 29) throw new ArgumentException("Mid No must be in range 0-8 or 10-13 or 20-29");

            _transport.SendWriteCommand(Sid, Type, new GatewayMusicCommand(midNo));
        }

        [Obsolete("Use SoundsOff method instead. Will be removed in the future releases.")]
        public void StopPlayMusic()
        {
            _transport.SendWriteCommand(Sid, Type, new PlaySoundCommand(1000, 0));
        }
    }
}