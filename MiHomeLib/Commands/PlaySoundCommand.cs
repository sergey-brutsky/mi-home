namespace MiHomeLib.Commands;

public class PlaySoundCommand(int soundNo, int volume) : Command
{
    private readonly int _soundNo = soundNo;
    private readonly int _volume = volume;

    public override string ToString() => $"{{\"mid\":{_soundNo},\"vol\":{_volume}}}";
}