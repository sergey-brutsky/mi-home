using MiHomeLib.Helpers;
using MiHomeLib.Devices;
using MiHomeLib.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiHomeLib.MiioDevices;


namespace MiHomeLib.XiaomiGateway2;

public partial class XiaomiGateway2
{
    /// <summary>
    /// Returns when every sub-device has answered the read request, or has not after 3 attempts.
    /// Sub-devices are reported through OnDeviceDiscoveredAsync as their answers arrive;
    /// one that never answered appears later, with its next report or heartbeat.
    /// Throws TimeoutException if the gateway does not answer within 10 seconds
    /// </summary>
    public void DiscoverDevices() => RunSync(DiscoverDevicesAsync);

    /// <summary>
    /// Completes when every sub-device has answered the read request, or has not after 3 attempts.
    /// Sub-devices are reported through OnDeviceDiscoveredAsync as their answers arrive;
    /// one that never answered appears later, with its next report or heartbeat.
    /// Throws TimeoutException if the gateway does not answer within 10 seconds
    /// </summary>
    public async Task DiscoverDevicesAsync()
    {
        await _discoveryLock.WaitAsync(_lifetimeToken).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _messageBufferFailure, null);
            var discovery = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _asyncDiscovery, discovery);

            SubscribeOnce();
            _messageTransport.SendDiscover();

            // The timeout covers only the gateway's answer, reading the devices is bounded by ReadAttempts
            if (await Task.WhenAny(discovery.Task, Task.Delay(DiscoveryTimeout, _lifetimeToken)).ConfigureAwait(false) != discovery.Task)
            {
                _lifetimeToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Gateway did not answer the discovery request within {DiscoveryTimeout.TotalSeconds} seconds");
            }

            var readingDevices = await discovery.Task.ConfigureAwait(false);
            await readingDevices.ConfigureAwait(false);
            ThrowIfMessageBufferFailed();
            _lifetimeToken.ThrowIfCancellationRequested();
        }
        finally
        {
            Interlocked.Exchange(ref _asyncDiscovery, null);
            _awaitedReads.Clear();
            _discoveryLock.Release();
        }
    }

    /// <summary>
    /// Sends read requests with short pauses and repeats them for the devices that have not answered
    /// </summary>
    private async Task ReadDevicesAsync(IReadOnlyList<string> sids)
    {
        foreach (var sid in sids) _awaitedReads[sid] = 0;

        for (var attempt = 1; attempt <= ReadAttempts; attempt++)
        {
            ThrowIfMessageBufferFailed();
            _lifetimeToken.ThrowIfCancellationRequested();
            var unanswered = sids.Where(_awaitedReads.ContainsKey).ToList();
            if (unanswered.Count == 0) return;

            if (attempt > 1)
                _logger.LogDebug("{Count} device(s) did not answer the read request, repeating it (attempt {Attempt} of {Attempts})",
                    unanswered.Count, attempt, ReadAttempts);

            var allAnswered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _allReadsAnswered, allAnswered);

            for (var i = 0; i < unanswered.Count; i++)
            {
                ThrowIfMessageBufferFailed();
                if (i > 0) await Task.Delay(ReadDeviceInterval, _lifetimeToken).ConfigureAwait(false);

                _lifetimeToken.ThrowIfCancellationRequested();
                _messageTransport.SendRead(unanswered[i]);
            }

            if (!sids.Any(_awaitedReads.ContainsKey)) return;

            await Task.WhenAny(allAnswered.Task, Task.Delay(ReadAnswerTimeout, _lifetimeToken)).ConfigureAwait(false);
            ThrowIfMessageBufferFailed();
        }

        _lifetimeToken.ThrowIfCancellationRequested();
        foreach (var sid in sids)
        {
            if (_awaitedReads.TryRemove(sid, out _))
                _logger.LogWarning("Device '{Sid}' did not answer the read request after {Attempts} attempts, it will appear with its next report or heartbeat",
                    sid, ReadAttempts);
        }
    }

    private void OnReadAnswered(string sid)
    {
        if (_awaitedReads.TryRemove(sid, out _) && _awaitedReads.IsEmpty)
            Volatile.Read(ref _allReadsAnswered)?.TrySetResult(true);
    }

    // Subscribed once: a repeated discovery must not process every message twice
    private void SubscribeOnce()
    {
        lock (_messagesLock)
        {
            _lifetimeToken.ThrowIfCancellationRequested();
            if (_subscribed) return;
            _messageLoop = Task.Run(() => ProcessMessagesAsync(_lifetimeToken));
            _messageTransport.OnMessageReceived += OnMessageReceived;
            _subscribed = true;
        }
    }

    private void OnMessageReceived(string str)
    {
        lock (_messagesLock)
        {
            if (_lifetimeToken.IsCancellationRequested) return;
            var bytes = System.Text.Encoding.UTF8.GetByteCount(str);
            if (_messages.Count >= MaxPendingMessages || bytes > MaxPendingMessageBytes - _messageBytes)
            {
                var failure = new System.IO.IOException("Gateway2 message buffer is full. Reports were lost; retry discovery.");
                Volatile.Write(ref _messageBufferFailure, failure);
                Volatile.Read(ref _asyncDiscovery)?.TrySetException(failure);
                Volatile.Read(ref _allReadsAnswered)?.TrySetResult(true);
                _logger.LogWarning("Gateway2 message buffer is full; incoming message was dropped.");
                return;
            }
            _messages.Enqueue((str, DateTime.UtcNow, bytes));
            _messageBytes += bytes;
            _messagesAvailable.Release();
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _messagesAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                (string Text, DateTime ReceivedAt, int Bytes) message;
                lock (_messagesLock)
                {
                    if (!_messages.TryDequeue(out message)) continue;
                    _messageBytes -= message.Bytes;
                }
                Task processing = null;
                MessageReception.Run(message.ReceivedAt, () => processing = HandleMessageAsync(message.Text));
                await processing.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void ThrowIfMessageBufferFailed()
    {
        if (Volatile.Read(ref _messageBufferFailure) is Exception failure) throw failure;
    }

    private async Task HandleMessageAsync(string str)
    {
        try
        {
            _logger.LogDebug("{Message}", str);

            var cmd = Gateway2Message.Parse(str);

            if (cmd.Type == Gateway2MessageType.Unknown)
            {
                _logger.LogWarning("Command '{Command}' is not a response command, skipping it", cmd.Command);
                return;
            }

            if (cmd.Type == Gateway2MessageType.WriteAck)
            {
                using var acknowledgement = JsonDocument.Parse(cmd.Data);
                var data = acknowledgement.RootElement;
                if (data.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"Write acknowledgement for device '{cmd.Sid}' must contain an object.");
                if (data.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                    _logger.LogError("Gateway2 write failed for device '{Sid}' with error {Error}.", cmd.Sid, error.ToString());
                else
                    _logger.LogDebug("Received write acknowledgement for device '{Sid}'.", cmd.Sid);
                // A write_ack can contain the preceding state. Only reports and reads update device state.
                return;
            }

            if (cmd.Type == Gateway2MessageType.GetIdListAck)
            {
                if ((Sid ?? _gatewaySid) is string expectedSid && expectedSid != cmd.Sid)
                {
                    _logger.LogWarning("Discovered gateway with sid '{Sid}', but expected gateway must be with sid '{ExpectedSid}'. Skipping it...", cmd.Sid, _gatewaySid);
                    return;
                }

                var asyncDiscovery = Interlocked.Exchange(ref _asyncDiscovery, null);
                if (asyncDiscovery is null) return; // duplicate or unsolicited acknowledgement
                Task readingDevices;

                try
                {
                    _messageTransport.Token = cmd.Token;
                    Sid = cmd.Sid;

                    // the gateway itself is read first, then its sub-devices
                    List<string> sids = [cmd.Sid, .. JsonNode.Parse(cmd.Data).AsArray().Select(x => x.ToString())];
                    readingDevices = ReadDevicesAsync(sids);
                }
                catch (Exception e)
                {
                    asyncDiscovery.TrySetException(e);
                    throw;
                }

                asyncDiscovery.TrySetResult(readingDevices);
                return;
            }

            var processing = ProcessAndAcknowledgeAsync(cmd);
            // State is applied synchronously. Gateway events and discovery subscribers may await
            // other reports, so their completion must not hold up the message queue.
            if (processing.IsCompleted)
                await processing.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error");
        }
    }

    private async Task ProcessAndAcknowledgeAsync(Gateway2Message cmd)
    {
        try
        {
            await ProcessDeviceMessage(cmd).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { }
        catch (Exception exception) { _logger.LogError(exception, "Unexpected error"); }
        finally
        {
            if (cmd.Type == Gateway2MessageType.ReadAck) OnReadAnswered(cmd.Sid);
        }
    }

    private Task ProcessDeviceMessage(Gateway2Message cmd)
    {
        if (cmd.Model == GATEWAY_SUB_DEVICE_MODEL)
        {
            ProcessGatewayMessage(cmd);
            return Task.CompletedTask;
        }

        if (!ProtocolDeviceRegistry.IsSupported(DeviceProtocol.XiaomiGateway2, cmd.Model))
        {
            _logger.LogWarning("Model '{Model}' is not supported, please contribute to support", cmd.Model);
            return Task.CompletedTask;
        }

        _lifetimeToken.ThrowIfCancellationRequested();
        _devices.TryGet(cmd.Sid, out var device);
        var discovered = device is null;
        if (discovered)
        {
            if (!ProtocolDeviceRegistry.TryCreate(DeviceProtocol.XiaomiGateway2, cmd.Model, cmd.Sid,
                cmd.ShortId, _gatewayLoggerFactory, out device))
                throw new InvalidOperationException($"No XiaomiGateway2 adapter is registered for model '{cmd.Model}'.");
            try
            {
                device.LastTimeMessageReceived = MessageReception.UtcNow;
                device.ParseData(cmd.Data);
                if (device is IProtocolMappedGateway2ManageableDevice manageable)
                    manageable.SetGateway2Writer(this);
                _devices.Add(cmd.Sid, device, _lifetimeToken);
            }
            catch (Exception failure)
            {
                ResourceCleanup.PreserveFailure(failure, device.Dispose);
                throw;
            }
        }
        else
        {
            device.LastTimeMessageReceived = MessageReception.UtcNow;
            device.ParseData(cmd.Data);
        }
        return discovered
            ? AsyncEvents.InvokeAsync(OnDeviceDiscoveredAsync, handler => handler(device),
                exception => _logger.LogError(exception, "Device discovery subscriber failed."), _lifetimeToken)
            : Task.CompletedTask;
    }

    // The gateway reports its own state the same way as its sub-devices, under the model "gateway"
    private void ProcessGatewayMessage(Gateway2Message cmd)
    {
        // Other gateways in the same multicast group report themselves as well
        var expectedSid = Sid ?? _gatewaySid;
        if (expectedSid is not null && cmd.Sid != expectedSid) return;

        // The heartbeat carries the token for write commands
        if (cmd.Token is not null) _messageTransport.Token = cmd.Token;

        using var data = JsonDocument.Parse(cmd.Data);
        foreach (var property in data.RootElement.EnumerateObject())
        {
            var value = property.Value;
            switch (property.Name)
            {
                case "illumination" when value.TryGetInt32(out var illumination):
                    var oldIllumination = Illumination;
                    Illumination = illumination;
                    _ = RaiseGatewayEvent(OnIlluminationChangeAsync, oldIllumination);
                    break;
                case "rgb" when value.TryGetInt32(out var rgb):
                    var oldRgb = Rgb;
                    Rgb = rgb;
                    _ = RaiseGatewayEvent(OnRgbChangeAsync, oldRgb);
                    break;
                case "proto_version":
                    ProtoVersion = value.GetString();
                    break;
                case "ip":
                    Ip = value.GetString();
                    break;
                default:
                    _logger.LogWarning("Gateway property '{Property}' is not supported yet. Please contribute to support.", property.Name);
                    break;
            }
        }
    }

    // A failing user handler must not stop processing the gateway messages
    private Task RaiseGatewayEvent<T>(Func<T, Task> handlers, T value) =>
        AsyncEvents.InvokeAsync(handlers, handler => handler(value),
            exception => _logger.LogError(exception, "Processing an event of the gateway failed"));
}
