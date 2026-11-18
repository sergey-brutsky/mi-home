using System;

namespace MiHomeLib.Helpers;

internal static class ResourceFactory
{
    // Ownership passes to the receiving object only after both resources exist.
    public static (TFirst First, TSecond Second) CreatePair<TFirst, TSecond>(Func<TFirst> first, Func<TSecond> second)
        where TFirst : IDisposable where TSecond : IDisposable
    {
        var resource = first();
        try { return (resource, second()); }
        catch (Exception failure)
        {
            ResourceCleanup.PreserveFailure(failure, resource.Dispose);
            throw;
        }
    }
}
