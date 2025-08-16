using System.Timers;

namespace MiHomeLib.Contracts;

public interface ITimer
{
    void Start();
    void Stop();
    event ElapsedEventHandler Elapsed;
}
