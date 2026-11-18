using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiHomeUnitTests.Infrastructure;

/// <summary>Records the event argument and the property value observed inside the handler.</summary>
internal sealed class ValueChangeRecorder<T>(Func<T> readProperty)
{
    public List<(T Previous, T Current)> Changes { get; } = [];

    public Task Handle(T previous)
    {
        Changes.Add((previous, readProperty()));
        return Task.CompletedTask;
    }
}
