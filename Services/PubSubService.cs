using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Bosbase.Exceptions;
using Bosbase.Utils;

namespace Bosbase.Services;

public record PubSubMessage(string Id, string Topic, string Created, object? Data);
public record PublishAck(string Id, string Topic, string Created);

internal class PendingAck
{
    public TaskCompletionSource<Dictionary<string, object?>> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public class PubSubService : BaseService
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, List<Action<PubSubMessage>>> _subscriptions = new();
    private readonly ConcurrentDictionary<string, PendingAck> _pendingAcks = new();
    private readonly TimeSpan[] _reconnectIntervals = new[]
    {
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1.2),
        TimeSpan.FromSeconds(1.5),
        TimeSpan.FromSeconds(2)
    };

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private int _reconnectAttempts;
    private bool _manualClose;
    private bool _isReady;
    private string _clientId = string.Empty;
    private readonly TimeSpan _ackTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(15);

    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                return _isReady;
            }
        }
    }

    public PubSubService(BosbaseClient client) : base(client) { }

    public async Task<PublishAck> PublishAsync(string topic, object data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("topic must be set.", nameof(topic));

        await EnsureSocketAsync(cancellationToken);

        var requestId = NextRequestId();
        var ackTask = WaitForAck(requestId, cancellationToken);

        await SendEnvelopeAsync(new Dictionary<string, object?>
        {
            ["type"] = "publish",
            ["topic"] = topic,
            ["data"] = data,
            ["requestId"] = requestId
        }, cancellationToken);

        var payload = await ackTask;
        return new PublishAck(
            Id: DictionaryExtensions.SafeGet(payload, "id")?.ToString() ?? string.Empty,
            Topic: DictionaryExtensions.SafeGet(payload, "topic")?.ToString() ?? topic,
            Created: DictionaryExtensions.SafeGet(payload, "created")?.ToString() ?? string.Empty);
    }

    public async Task<Func<Task>> SubscribeAsync(string topic, Action<PubSubMessage> callback, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("topic must be set.", nameof(topic));

        var list = _subscriptions.GetOrAdd(topic, _ => new List<Action<PubSubMessage>>());
        lock (list)
        {
            list.Add(callback);
        }

        await EnsureSocketAsync(cancellationToken);

        if (list.Count == 1)
        {
            var requestId = NextRequestId();
            _ = WaitForAck(requestId, cancellationToken); // best effort
            await SendEnvelopeAsync(new Dictionary<string, object?>
            {
                ["type"] = "subscribe",
                ["topic"] = topic,
                ["requestId"] = requestId
            }, cancellationToken);
        }

        return async () =>
        {
            await UnsubscribeAsync(topic, callback, cancellationToken);
        };
    }

    public async Task UnsubscribeAsync(string? topic = null, Action<PubSubMessage>? callback = null, CancellationToken cancellationToken = default)
    {
        if (topic == null)
        {
            _subscriptions.Clear();
            await SendEnvelopeAsync(new Dictionary<string, object?> { ["type"] = "unsubscribe" }, cancellationToken);
            Disconnect();
            return;
        }

        if (_subscriptions.TryGetValue(topic, out var list))
        {
            lock (list)
            {
                if (callback != null)
                {
                    list.Remove(callback);
                }
                else
                {
                    list.Clear();
                }
            }

            if (!list.Any())
            {
                _subscriptions.TryRemove(topic, out _);
                await SendUnsubscribeAsync(topic, cancellationToken);
                if (!_subscriptions.Any())
                {
                    Disconnect();
                }
            }
        }
    }

    public void Disconnect()
    {
        _manualClose = true;
        foreach (var pending in _pendingAcks.Values)
        {
            pending.Tcs.TrySetException(new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = "pubsub connection closed" }));
        }
        _pendingAcks.Clear();
        try
        {
            _cts?.Cancel();
        }
        catch { }
        _cts = null;
        _socket?.Dispose();
        _socket = null;
        _listenTask = null;
        lock (_lock)
        {
            _isReady = false;
        }
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------
    private async Task EnsureSocketAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isReady && _socket != null && _socket.State == WebSocketState.Open)
            {
                return;
            }
        }

        await ConnectAsync(cancellationToken);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        Disconnect();
        _manualClose = false;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Accept-Language", Client.Lang);
        if (Client.AuthStore.IsValid())
        {
            ws.Options.SetRequestHeader("Authorization", Client.AuthStore.Token);
        }

        var uri = BuildWebSocketUri();
        var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        connectCts.CancelAfter(_connectTimeout);

        try
        {
            await ws.ConnectAsync(uri, connectCts.Token);
            _socket = ws;
            lock (_lock)
            {
                _isReady = true;
            }
            _reconnectAttempts = 0;
            _listenTask = Task.Run(() => ListenAsync(ws, _cts.Token));
        }
        catch (Exception ex)
        {
            HandleConnectError(ex);
        }
    }

    private async Task ListenAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult? result = null;
            var ms = new MemoryStream();
            try
            {
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleCloseAsync();
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var text = Encoding.UTF8.GetString(ms.ToArray());
                HandleMessage(text);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await HandleCloseAsync();
                break;
            }
            finally
            {
                ms.Dispose();
            }
        }
    }

    private void HandleMessage(string payload)
    {
        Dictionary<string, object?> data;
        try
        {
            data = JsonSerializer.Deserialize<Dictionary<string, object?>>(payload) ?? new Dictionary<string, object?>();
        }
        catch
        {
            return;
        }

        var msgType = DictionaryExtensions.SafeGet(data, "type")?.ToString();
        switch (msgType)
        {
            case "ready":
                _clientId = DictionaryExtensions.SafeGet(data, "clientId")?.ToString() ?? string.Empty;
                HandleConnected();
                break;
            case "message":
                var topic = DictionaryExtensions.SafeGet(data, "topic")?.ToString() ?? string.Empty;
                if (_subscriptions.TryGetValue(topic, out var listeners))
                {
                    var message = new PubSubMessage(
                        Id: DictionaryExtensions.SafeGet(data, "id")?.ToString() ?? string.Empty,
                        Topic: topic,
                        Created: DictionaryExtensions.SafeGet(data, "created")?.ToString() ?? string.Empty,
                        Data: DictionaryExtensions.SafeGet(data, "data"));
                    List<Action<PubSubMessage>> copy;
                    lock (listeners)
                    {
                        copy = listeners.ToList();
                    }
                    foreach (var listener in copy)
                    {
                        try { listener(message); } catch { }
                    }
                }
                break;
            case "published":
            case "subscribed":
            case "unsubscribed":
            case "pong":
                var reqId = DictionaryExtensions.SafeGet(data, "requestId")?.ToString();
                if (reqId != null)
                {
                    ResolvePending(reqId, data);
                }
                break;
            case "error":
                var errReq = DictionaryExtensions.SafeGet(data, "requestId")?.ToString();
                if (errReq != null)
                {
                    RejectPending(errReq, new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = DictionaryExtensions.SafeGet(data, "message")?.ToString() ?? "pubsub error" }));
                }
                break;
        }
    }

    private void HandleConnected()
    {
        _reconnectAttempts = 0;
        lock (_lock)
        {
            _isReady = true;
        }

        foreach (var topic in _subscriptions.Keys.ToList())
        {
            var requestId = NextRequestId();
            _ = WaitForAck(requestId, CancellationToken.None);
            _ = SendEnvelopeAsync(new Dictionary<string, object?>
            {
                ["type"] = "subscribe",
                ["topic"] = topic,
                ["requestId"] = requestId
            }, CancellationToken.None);
        }
    }

    private async Task HandleCloseAsync()
    {
        lock (_lock)
        {
            _isReady = false;
        }

        if (_manualClose)
        {
            return;
        }

        foreach (var pending in _pendingAcks.Values)
        {
            pending.Tcs.TrySetException(new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = "pubsub connection closed" }));
        }
        _pendingAcks.Clear();

        if (!_subscriptions.Any())
        {
            return;
        }

        var delay = _reconnectIntervals[Math.Min(_reconnectAttempts, _reconnectIntervals.Length - 1)];
        _reconnectAttempts++;
        try
        {
            await Task.Delay(delay, CancellationToken.None);
        }
        catch { }
        await ConnectAsync(CancellationToken.None);
    }

    private async Task SendEnvelopeAsync(Dictionary<string, object?> envelope, CancellationToken cancellationToken)
    {
        if (!IsConnected || _socket == null)
        {
            await EnsureSocketAsync(cancellationToken);
        }

        if (_socket == null)
        {
            throw new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = "Unable to send websocket message - socket not initialized." });
        }

        var json = JsonSerializer.Serialize(envelope);
        var data = Encoding.UTF8.GetBytes(json);
        try
        {
            await _socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = ex.Message }, originalError: ex);
        }
    }

    private async Task SendUnsubscribeAsync(string topic, CancellationToken cancellationToken)
    {
        var requestId = NextRequestId();
        _ = WaitForAck(requestId, cancellationToken);
        await SendEnvelopeAsync(new Dictionary<string, object?>
        {
            ["type"] = "unsubscribe",
            ["topic"] = topic,
            ["requestId"] = requestId
        }, cancellationToken);
    }

    private Task<Dictionary<string, object?>> WaitForAck(string requestId, CancellationToken cancellationToken)
    {
        var pending = new PendingAck();
        _pendingAcks[requestId] = pending;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_ackTimeout, cancellationToken);
                pending.Tcs.TrySetException(new TimeoutException("Timed out waiting for pubsub response."));
            }
            catch (OperationCanceledException)
            {
                // ignore cancellations
            }
        }, CancellationToken.None);

        return pending.Tcs.Task;
    }

    private void ResolvePending(string requestId, Dictionary<string, object?> payload)
    {
        if (_pendingAcks.TryRemove(requestId, out var pending))
        {
            pending.Tcs.TrySetResult(payload);
        }
    }

    private void RejectPending(string requestId, Exception err)
    {
        if (_pendingAcks.TryRemove(requestId, out var pending))
        {
            pending.Tcs.TrySetException(err);
        }
    }

    private Uri BuildWebSocketUri()
    {
        var query = new Dictionary<string, object?>();
        if (Client.AuthStore.IsValid())
        {
            query["token"] = Client.AuthStore.Token;
        }
        var baseUrl = Client.BuildUrl("/api/pubsub", query);
        if (baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "wss" + baseUrl.Substring(5);
        }
        else if (baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "ws" + baseUrl.Substring(4);
        }
        else
        {
            baseUrl = "ws://" + baseUrl.TrimStart('/');
        }
        return new Uri(baseUrl);
    }

    private void HandleConnectError(Exception err)
    {
        if (_reconnectAttempts > 1_000_000 || _manualClose)
        {
            foreach (var pending in _pendingAcks.Values)
            {
                pending.Tcs.TrySetException(new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = err.Message }));
            }
            _pendingAcks.Clear();
            Disconnect();
            return;
        }

        var delay = _reconnectIntervals[Math.Min(_reconnectAttempts, _reconnectIntervals.Length - 1)];
        _reconnectAttempts++;
        Task.Run(async () =>
        {
            try { await Task.Delay(delay); } catch { }
            await ConnectAsync(CancellationToken.None);
        });
    }

    private string NextRequestId()
    {
        return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}{Guid.NewGuid():N}".Substring(0, 24);
    }
}
