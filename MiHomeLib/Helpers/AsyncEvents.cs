using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.Helpers;

internal interface IEventTaskTracker
{
    void TrackEvent(Task task);
}

internal static class AsyncEvents
{
    private static readonly AsyncLocal<bool> _insideEventHandler = new();
    private static readonly AsyncLocal<object> _eventOwner = new();

    public static bool InsideEventHandler => _insideEventHandler.Value;

    public static bool IsInsideEventHandlerFor(object owner) =>
        InsideEventHandler && ReferenceEquals(_eventOwner.Value, owner);

    public static async Task InvokeAsync<T>(T handlers, Func<T, Task> invoke, Action<Exception> onError,
        CancellationToken cancellationToken, object eventOwner = null) where T : Delegate
    {
        cancellationToken.ThrowIfCancellationRequested();
        var notification = InvokeAsync(handlers, invoke, onError, eventOwner);
        if (eventOwner is IEventTaskTracker tracker) tracker.TrackEvent(notification);
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
        {
            await Task.WhenAny(notification, cancelled.Task).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await notification.ConfigureAwait(false);
        }
    }

    public static Task InvokeAsync<T>(T handlers, Func<T, Task> invoke, Action<Exception> onError,
        object eventOwner = null) where T : Delegate
    {
        if (handlers is null) return Task.CompletedTask;
        return Task.WhenAll(handlers.GetInvocationList().Cast<T>().Select(handler => InvokeOne(handler)));

        async Task InvokeOne(T handler)
        {
            var previousInsideHandler = _insideEventHandler.Value;
            var previousOwner = _eventOwner.Value;
            _insideEventHandler.Value = true;
            _eventOwner.Value = eventOwner ?? previousOwner;
            try
            {
                var task = invoke(handler);
                if (task is not null) await task.ConfigureAwait(false);
            }
            catch (Exception exception) { onError(exception); }
            finally
            {
                _eventOwner.Value = previousOwner;
                _insideEventHandler.Value = previousInsideHandler;
            }
        }
    }
}
