using System;
using System.Collections.Generic;

namespace MiHomeLib.Helpers;

internal static class ResourceCleanup
{
    public static void DisposeAll(params Action[] cleanup)
    {
        var failures = new List<Exception>();
        foreach (var dispose in cleanup)
        {
            try { dispose(); }
            catch (Exception failure) { failures.Add(failure); }
        }
        if (failures.Count > 0)
            throw new AggregateException("Resource cleanup failed.", failures).Flatten();
    }

    public static void PreserveFailure(Exception failure, Action cleanup)
    {
        try { cleanup(); }
        catch (Exception cleanupFailure)
        {
            failure.Data["ResourceCleanupException"] = cleanupFailure;
        }
    }
}
