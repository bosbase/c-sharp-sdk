using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bosbase.Exceptions;
using Bosbase.Utils;

namespace Bosbase.Services;

public class RealtimeService : BaseService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<Action<Dictionary<string, object?>>>> _subscriptions = new();
    private readonly TimeSpan[] _backoff = new[]
    {
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    };

    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private TaskCompletionSource<bool>? _readyTcs;

    public string ClientId { get; private set; } = string.Empty;
    public Action<List<string>>? OnDisconnect { get; set; }

    public RealtimeService(BosbaseClient client) : base(client) { }

    public Action Subscribe(
        string topic,
        Action<Dictionary<string, object?>> callback,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null)
    {
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("topic must be set", nameof(topic));
        if (callback == null) throw new ArgumentNullException(nameof(callback));

        var key = BuildSubscriptionKey(topic, query, headers);
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(key, out var listeners))
            {
                listeners = new List<Action<Dictionary<string, object?>>>();
                _subscriptions[key] = listeners;
            }
            listeners.Add(callback);
        }

        EnsureListener();
        EnsureConnected();
        _ = SubmitSubscriptionsAsync();

        return () => UnsubscribeByTopicAndListener(topic, callback);
    }

    public void Unsubscribe(string? topic = null)
    {
        bool changed;
        lock (_lock)
        {
            if (topic == null)
            {
                changed = _subscriptions.Any();
                _subscriptions.Clear();
            }
            else
            {
                var keys = KeysForTopic(topic);
                changed = keys.Any();
                foreach (var key in keys)
                {
                    _subscriptions.Remove(key);
                }
            }
        }

        if (changed)
        {
            if (HasSubscriptions())
            {
                _ = SubmitSubscriptionsAsync();
            }
            else
            {
                Disconnect();
            }
        }
    }

    public void UnsubscribeByPrefix(string prefix)
    {
        lock (_lock)
        {
            var keys = _subscriptions.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            foreach (var key in keys)
            {
                _subscriptions.Remove(key);
            }
        }

        if (HasSubscriptions())
        {
            _ = SubmitSubscriptionsAsync();
        }
        else
        {
            Disconnect();
        }
    }

    public void UnsubscribeByTopicAndListener(string topic, Action<Dictionary<string, object?>> listener)
    {
        lock (_lock)
        {
            var keys = KeysForTopic(topic);
            foreach (var key in keys)
            {
                if (_subscriptions.TryGetValue(key, out var listeners))
                {
                    listeners.Remove(listener);
                    if (!listeners.Any())
                    {
                        _subscriptions.Remove(key);
                    }
                }
            }
        }

        if (HasSubscriptions())
        {
            _ = SubmitSubscriptionsAsync();
        }
        else
        {
            Disconnect();
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts = null;
            _listenTask = null;
            ClientId = string.Empty;
            _readyTcs?.TrySetCanceled();
            _readyTcs = null;
        }
    }

    public void EnsureConnected(TimeSpan? timeout = null)
    {
        EnsureListener();
        var tcs = _readyTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!tcs.Task.Wait(timeout ?? TimeSpan.FromSeconds(10)))
        {
            throw new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = "Realtime connection not established" });
        }
    }

    public List<string> GetActiveSubscriptions()
    {
        lock (_lock)
        {
            return _subscriptions.Keys.ToList();
        }
    }

    private void EnsureListener()
    {
        lock (_lock)
        {
            if (_listenTask != null && !_listenTask.IsCompleted) return;
            _cts = new CancellationTokenSource();
            _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _listenTask = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var httpClient = new HttpClient();
        var attempt = 0;
        var url = Client.BuildUrl("/api/realtime");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
                request.Headers.TryAddWithoutValidation("Accept-Language", Client.Lang);
                if (Client.AuthStore.IsValid())
                {
                    request.Headers.TryAddWithoutValidation("Authorization", Client.AuthStore.Token);
                }

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                attempt = 0;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                await ListenAsync(reader, cancellationToken);
                ClientId = string.Empty;
                _readyTcs?.TrySetCanceled();
                OnDisconnect?.Invoke(GetActiveSubscriptions());
            }
            catch (Exception)
            {
                ClientId = string.Empty;
                _readyTcs?.TrySetCanceled();
                OnDisconnect?.Invoke(GetActiveSubscriptions());

                if (cancellationToken.IsCancellationRequested || !HasSubscriptions())
                {
                    break;
                }

                var delay = _backoff[Math.Min(attempt, _backoff.Length - 1)];
                attempt++;
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    private async Task ListenAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new Dictionary<string, string>
        {
            ["event"] = "message",
            ["data"] = string.Empty,
            ["id"] = string.Empty
        };

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var rawLine = await reader.ReadLineAsync();
            if (rawLine == null) continue;
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrEmpty(line))
            {
                DispatchEvent(buffer);
                buffer = new Dictionary<string, string>
                {
                    ["event"] = "message",
                    ["data"] = string.Empty,
                    ["id"] = string.Empty
                };
                continue;
            }

            if (line.StartsWith(":", StringComparison.Ordinal))
            {
                continue;
            }

            string field;
            string value;
            var index = line.IndexOf(':');
            if (index != -1)
            {
                field = line.Substring(0, index);
                value = line.Substring(index + 1).TrimStart(' ');
            }
            else
            {
                field = line;
                value = string.Empty;
            }

            switch (field)
            {
                case "event":
                    buffer["event"] = string.IsNullOrWhiteSpace(value) ? "message" : value;
                    break;
                case "data":
                    buffer.TryGetValue("data", out var existing);
                    buffer["data"] = (existing ?? string.Empty) + value + "\n";
                    break;
                case "id":
                    buffer["id"] = value;
                    break;
            }
        }
    }

    private void DispatchEvent(Dictionary<string, string> evt)
    {
        evt.TryGetValue("event", out var eventName);
        var name = string.IsNullOrWhiteSpace(eventName) ? "message" : eventName;

        evt.TryGetValue("data", out var dataValue);
        var dataStr = (dataValue ?? string.Empty).TrimEnd('\n');
        Dictionary<string, object?> payload;
        if (!string.IsNullOrWhiteSpace(dataStr))
        {
            try
            {
                payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(dataStr) ?? new Dictionary<string, object?>();
            }
            catch
            {
                payload = new Dictionary<string, object?> { ["raw"] = dataStr };
            }
        }
        else
        {
            payload = new Dictionary<string, object?>();
        }

        if (name == "PB_CONNECT")
        {
            evt.TryGetValue("id", out var eventId);
            ClientId = DictionaryExtensions.SafeGet(payload, "clientId")?.ToString() ?? eventId ?? string.Empty;
            _readyTcs?.TrySetResult(true);
            _ = SubmitSubscriptionsAsync();
            OnDisconnect?.Invoke(new List<string>()); // signal reconnect completed
            return;
        }

        List<Action<Dictionary<string, object?>>> listeners;
        lock (_lock)
        {
            _subscriptions.TryGetValue(name, out var found);
            listeners = found != null
                ? new List<Action<Dictionary<string, object?>>>(found)
                : new List<Action<Dictionary<string, object?>>>();
        }

        foreach (var listener in listeners)
        {
            try
            {
                listener(payload);
            }
            catch
            {
                // best effort
            }
        }
    }

    private async Task SubmitSubscriptionsAsync()
    {
        string clientId;
        List<string> subscriptions;

        lock (_lock)
        {
            if (string.IsNullOrEmpty(ClientId) || !HasSubscriptions())
            {
                return;
            }

            clientId = ClientId;
            subscriptions = _subscriptions.Keys.ToList();
        }

        var payload = new Dictionary<string, object?>
        {
            ["clientId"] = clientId,
            ["subscriptions"] = subscriptions
        };

        try
        {
            await Client.SendAsync(
                "/api/realtime",
                new SendOptions { Method = HttpMethod.Post, Body = payload });
        }
        catch (ClientResponseError ex) when (ex.IsAbort)
        {
            // ignore cancellations
        }
    }

    private string BuildSubscriptionKey(string topic, IDictionary<string, object?>? query, IDictionary<string, string>? headers)
    {
        var key = topic;
        var options = new Dictionary<string, object?>();
        if (query != null && query.Any()) options["query"] = query;
        if (headers != null && headers.Any()) options["headers"] = headers;
        if (options.Any())
        {
            var serialized = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = false });
            var suffix = "options=" + Uri.EscapeDataString(serialized);
            key += (key.Contains("?") ? "&" : "?") + suffix;
        }
        return key;
    }

    private List<string> KeysForTopic(string topic)
    {
        lock (_lock)
        {
            var prefix = topic + "?";
            return _subscriptions.Keys.Where(k => k == topic || k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        }
    }

    private bool HasSubscriptions()
    {
        lock (_lock)
        {
            return _subscriptions.Values.Any(list => list.Any());
        }
    }
}
