using System.Timers;

namespace MiHomeLib.Utils;

public interface ITimer
{
    void Start();
    void Stop();
    event ElapsedEventHandler Elapsed;
}
