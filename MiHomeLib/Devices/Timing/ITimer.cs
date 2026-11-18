using System;

namespace MiHomeLib.Devices.Timing;

internal interface ITimer : IDisposable
{
    // A queued callback may arrive after Stop. Each start retains its own callback.
    void Start(Action elapsed);
    void Stop();
}
