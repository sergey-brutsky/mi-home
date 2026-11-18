using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.Helpers;

internal static class AsyncEvents
{
    public static async Task InvokeAsync<T>(T handlers, Func<T, Task> invoke, Action<Exception> onError,
        CancellationToken cancellationToken) where T : Delegate
    {
        cancellationToken.ThrowIfCancellationRequested();
        var notification = InvokeAsync(handlers, invoke, onError);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
        {
            await Task.WhenAny(notification, cancelled.Task).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await notification.ConfigureAwait(false);
        }
    }

    public static Task InvokeAsync<T>(T handlers, Func<T, Task> invoke, Action<Exception> onError) where T : Delegate
    {
        if (handlers is null) return Task.CompletedTask;
        return Task.WhenAll(handlers.GetInvocationList().Cast<T>().Select(InvokeOne));

        async Task InvokeOne(T handler)
        {
            try
            {
                var task = invoke(handler);
                if (task is not null) await task.ConfigureAwait(false);
            }
            catch (Exception exception) { onError(exception); }
        }
    }
}
