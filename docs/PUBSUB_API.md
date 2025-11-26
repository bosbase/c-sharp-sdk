# Pub/Sub API - C# SDK Documentation

BosBase now exposes a lightweight WebSocket-based publish/subscribe channel so SDK users can push and receive custom messages. The Go backend uses the `ws` transport and persists each published payload in the `_pubsub_messages` table so every node in a cluster can replay and fan-out messages to its local subscribers.

- Endpoint: `/api/pubsub` (WebSocket)
- Auth: the SDK automatically forwards `authStore.token` as a `token` query parameter; cookie-based auth also works. Anonymous clients may subscribe, but publishing requires an authenticated token.
- Reliability: automatic reconnect with topic re-subscription; messages are stored in the database and broadcasted to all connected nodes.

## Quick Start

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Subscribe to a topic
var unsubscribe = await client.PubSub.SubscribeAsync("chat/general", (msg) =>
{
    Console.WriteLine($"message {msg.Topic}: {msg.Data}");
});

// Publish to a topic (resolves when the server stores and accepts it)
var ack = await client.PubSub.PublishAsync("chat/general", new { text = "Hello team!" });
Console.WriteLine($"published at {ack.Created}");

// Later, stop listening
await unsubscribe();
```

## API Surface

- `client.PubSub.PublishAsync(topic, data)` → `Task<PublishAck>`
- `client.PubSub.SubscribeAsync(topic, handler)` → `Task<Func<Task>>`
- `client.PubSub.UnsubscribeAsync(topic?)` → `Task` (omit `topic` to drop all topics)
- `client.PubSub.Disconnect()` to explicitly close the socket and clear pending requests.
- `client.PubSub.IsConnected` exposes the current WebSocket state.

## Notes for Clusters

- Messages are written to `_pubsub_messages` with a timestamp; every running node polls the table and pushes new rows to its connected WebSocket clients.
- Old pub/sub rows are cleaned up automatically after a day to keep the table small.
- If a node restarts, it resumes from the latest message and replays new rows as they are inserted, so connected clients on other nodes stay in sync.

## Complete Examples

### Example 1: Chat Application

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate (required for publishing)
await client.Collection("users").AuthWithPasswordAsync("user@example.com", "password");

// Subscribe to chat room
var unsubscribe = await client.PubSub.SubscribeAsync("chat/room1", (msg) =>
{
    var data = msg.Data as Dictionary<string, object?>;
    Console.WriteLine($"[{msg.Created}] {data?["user"]}: {data?["message"]}");
});

// Publish a message
await client.PubSub.PublishAsync("chat/room1", new Dictionary<string, object?>
{
    ["user"] = "user@example.com",
    ["message"] = "Hello everyone!"
});

// Keep the connection alive
await Task.Delay(TimeSpan.FromMinutes(5));

// Unsubscribe when done
await unsubscribe();
```

### Example 2: Real-time Notifications

```csharp
class NotificationService
{
    private readonly BosbaseClient _client;
    private Func<Task>? _unsubscribe;

    public NotificationService(BosbaseClient client)
    {
        _client = client;
    }

    public async Task StartListening(string userId)
    {
        _unsubscribe = await _client.PubSub.SubscribeAsync($"notifications/{userId}", (msg) =>
        {
            var notification = msg.Data as Dictionary<string, object?>;
            HandleNotification(notification);
        });
    }

    private void HandleNotification(Dictionary<string, object?>? notification)
    {
        var type = notification?["type"]?.ToString();
        var message = notification?["message"]?.ToString();
        
        Console.WriteLine($"Notification [{type}]: {message}");
        
        // Show system notification, update UI, etc.
    }

    public async Task SendNotification(string userId, string type, string message)
    {
        await _client.PubSub.PublishAsync($"notifications/{userId}", new Dictionary<string, object?>
        {
            ["type"] = type,
            ["message"] = message,
            ["timestamp"] = DateTime.UtcNow.ToString("O")
        });
    }

    public async Task StopListening()
    {
        if (_unsubscribe != null)
        {
            await _unsubscribe();
            _unsubscribe = null;
        }
    }
}

// Usage
var notificationService = new NotificationService(client);
await notificationService.StartListening("user123");
await notificationService.SendNotification("user123", "info", "Your order has been shipped");
```

### Example 3: Multi-topic Subscription

```csharp
var client = new BosbaseClient("http://127.0.0.1:8090");

// Subscribe to multiple topics
var unsubscribe1 = await client.PubSub.SubscribeAsync("events/system", (msg) =>
{
    Console.WriteLine($"System event: {msg.Data}");
});

var unsubscribe2 = await client.PubSub.SubscribeAsync("events/user", (msg) =>
{
    Console.WriteLine($"User event: {msg.Data}");
});

// Publish to different topics
await client.PubSub.PublishAsync("events/system", new { type = "maintenance", message = "Scheduled maintenance in 1 hour" });
await client.PubSub.PublishAsync("events/user", new { userId = "123", action = "login" });

// Unsubscribe from specific topics
await unsubscribe1();
await unsubscribe2();
```

### Example 4: Connection Status Monitoring

```csharp
class PubSubManager
{
    private readonly BosbaseClient _client;
    private readonly System.Timers.Timer _statusTimer;

    public PubSubManager(BosbaseClient client)
    {
        _client = client;
        _statusTimer = new System.Timers.Timer(5000); // Check every 5 seconds
        _statusTimer.Elapsed += (sender, e) => CheckConnectionStatus();
    }

    public void StartMonitoring()
    {
        _statusTimer.Start();
    }

    public void StopMonitoring()
    {
        _statusTimer.Stop();
    }

    private void CheckConnectionStatus()
    {
        var isConnected = _client.PubSub.IsConnected;
        Console.WriteLine($"PubSub connection status: {(isConnected ? "Connected" : "Disconnected")}");
        
        if (!isConnected)
        {
            Console.Warn("PubSub connection lost. Reconnection will be attempted automatically.");
        }
    }
}

// Usage
var manager = new PubSubManager(client);
manager.StartMonitoring();

var unsubscribe = await client.PubSub.SubscribeAsync("test/topic", (msg) =>
{
    Console.WriteLine($"Received: {msg.Data}");
});

// Connection status is monitored automatically
```

### Example 5: Error Handling

```csharp
try
{
    var unsubscribe = await client.PubSub.SubscribeAsync("test/topic", (msg) =>
    {
        try
        {
            // Process message
            ProcessMessage(msg);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing message: {ex.Message}");
        }
    });

    await client.PubSub.PublishAsync("test/topic", new { data = "test" });
}
catch (Exception ex)
{
    Console.Error.WriteLine($"PubSub error: {ex.Message}");
}
```

## Best Practices

1. **Error Handling**: Always wrap message handlers in try-catch blocks
2. **Connection Management**: Use `IsConnected` to check connection status
3. **Cleanup**: Always unsubscribe when done to free resources
4. **Reconnection**: The SDK handles automatic reconnection, but monitor connection status
5. **Message Format**: Use consistent message formats for easier processing
6. **Authentication**: Ensure authentication before publishing messages
7. **Topic Naming**: Use hierarchical topic names (e.g., `chat/room1`, `notifications/user123`)

## Limitations

- **WebSocket Only**: Requires WebSocket support
- **Authentication Required**: Publishing requires authentication
- **Message Size**: Large messages may impact performance
- **Connection Limits**: Be mindful of connection limits on the server

## Related Documentation

- [Realtime API](./REALTIME.md) - Real-time record subscriptions
- [Authentication](./AUTHENTICATION.md) - User authentication

