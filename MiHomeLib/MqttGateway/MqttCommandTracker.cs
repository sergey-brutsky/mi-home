using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MiHomeLib.MqttGateway;

internal sealed class MqttCommandTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<int, Pending> _pending = new();
    private static int _id = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & 0x3FFFFFFF;

    private sealed record Pending(string Did, string Resource, int Value, TaskCompletionSource<bool> Completion);

    public async Task SendAsync(string message, Action<string> send, TimeSpan timeout, CancellationToken lifetime)
    {
        var request = JsonNode.Parse(message).AsObject();
        var item = request["params"].AsArray()[0].AsObject();
        var pending = new Pending(request["did"].GetValue<string>(), item["res_name"].GetValue<string>(),
            item["value"].GetValue<int>(), new(TaskCreationOptions.RunContinuationsAsynchronously));
        var id = Interlocked.Increment(ref _id) & int.MaxValue;
        request["id"] = id;
        lock (_sync)
        {
            lifetime.ThrowIfCancellationRequested();
            _pending.Add(id, pending);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        try
        {
            var delay = Task.Delay(timeout, deadline.Token);
            send(request.ToJsonString());
            if (await Task.WhenAny(pending.Completion.Task, delay).ConfigureAwait(false) != pending.Completion.Task)
            {
                lifetime.ThrowIfCancellationRequested();
                throw new TimeoutException($"No device write_ack for '{pending.Did}' resource '{pending.Resource}'. The command was sent once and its outcome is unknown.");
            }
            deadline.Cancel();
            await pending.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            deadline.Cancel();
            lock (_sync) _pending.Remove(id);
            if (pending.Completion.Task.IsFaulted) _ = pending.Completion.Task.Exception;
        }
    }

    public void Receive(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (!root.TryGetProperty("cmd", out var command) || command.ValueKind != JsonValueKind.String) return;
            var action = command.GetString();
            if (action is not ("write_ack" or "write_rsp") ||
                !root.TryGetProperty("id", out var id) || !id.TryGetInt32(out var value)) return;
            lock (_sync)
            {
                if (!_pending.TryGetValue(value, out var pending) ||
                    !root.TryGetProperty("did", out var did) || did.GetString() != pending.Did) return;
                var field = action == "write_ack" ? "params" : "results";
                if (!root.TryGetProperty(field, out var items) || items.ValueKind != JsonValueKind.Array) return;
                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("res_name", out var resource) || resource.GetString() != pending.Resource) continue;
                    if (item.TryGetProperty("error_code", out var error) && error.GetInt32() != 0)
                        pending.Completion.TrySetException(new InvalidOperationException($"Device '{pending.Did}' rejected resource '{pending.Resource}', error {error.GetInt32()}."));
                    else if (action == "write_ack" && item.TryGetProperty("value", out var state) &&
                        state.TryGetInt32(out var actual) && actual == pending.Value)
                        pending.Completion.TrySetResult(true);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException) { }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            foreach (var pending in _pending.Values)
                pending.Completion.TrySetException(new IOException("MQTT connection was lost after sending the command. Its outcome is unknown."));
            _pending.Clear();
        }
    }
}
