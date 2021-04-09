using MiHomeLib.Devices;

namespace MiHomeLib.Commands
{
    public class PlaySoundCommand : Command
    {
        private readonly int _soundNo;
        private readonly int _volume;

        public PlaySoundCommand(int soundNo, int volume)
        {
            _soundNo = soundNo;
            _volume = volume;
        }

        public override string ToString()
        {
            return $"{{\"mid\":{_soundNo},\"vol\":{_volume}}}";
        }
    }
}