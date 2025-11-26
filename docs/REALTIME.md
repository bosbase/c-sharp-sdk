# Realtime API - C# SDK Documentation

## Overview

The Realtime API enables real-time updates for collection records using **Server-Sent Events (SSE)**. It allows you to subscribe to changes in collections or specific records and receive instant notifications when records are created, updated, or deleted.

**Key Features:**
- Real-time notifications for record changes
- Collection-level and record-level subscriptions
- Automatic connection management and reconnection
- Authorization support
- Subscription options (expand, custom headers, query params)
- Event-driven architecture

**Backend Endpoints:**
- `GET /api/realtime` - Establish SSE connection
- `POST /api/realtime` - Set subscriptions

## How It Works

1. **Connection**: The SDK establishes an SSE connection to `/api/realtime`
2. **Client ID**: Server sends `PB_CONNECT` event with a unique `clientId`
3. **Subscriptions**: Client submits subscription topics via POST request
4. **Events**: Server sends events when matching records change
5. **Reconnection**: SDK automatically reconnects on connection loss

## Basic Usage

### Subscribe to Collection Changes

Subscribe to all changes in a collection:

```csharp
using Bosbase;

var pb = new BosbaseClient("http://127.0.0.1:8090");

// Subscribe to all changes in the 'posts' collection
var unsubscribe = pb.Collection("posts").Subscribe("*", (e) =>
{
    Console.WriteLine($"Action: {e["action"]}");  // 'create', 'update', or 'delete'
    Console.WriteLine($"Record: {e["record"]}");  // The record data
});

// Later, unsubscribe
unsubscribe();
```

### Subscribe to Specific Record

Subscribe to changes for a single record:

```csharp
// Subscribe to changes for a specific post
pb.Collection("posts").Subscribe("RECORD_ID", (e) =>
{
    Console.WriteLine($"Record changed: {e["record"]}");
    Console.WriteLine($"Action: {e["action"]}");
});
```

### Multiple Subscriptions

You can subscribe multiple times to the same or different topics:

```csharp
// Subscribe to multiple records
var unsubscribe1 = pb.Collection("posts").Subscribe("RECORD_ID_1", HandleChange);
var unsubscribe2 = pb.Collection("posts").Subscribe("RECORD_ID_2", HandleChange);
var unsubscribe3 = pb.Collection("posts").Subscribe("*", HandleAllChanges);

void HandleChange(Dictionary<string, object?> e)
{
    Console.WriteLine($"Change event: {e}");
}

void HandleAllChanges(Dictionary<string, object?> e)
{
    Console.WriteLine($"Collection-wide change: {e}");
}

// Unsubscribe individually
unsubscribe1();
unsubscribe2();
unsubscribe3();
```

## Event Structure

Each event received contains:

```csharp
{
    "action": "create" | "update" | "delete",  // Action type
    "record": {                                 // Record data
        "id": "RECORD_ID",
        "collectionId": "COLLECTION_ID",
        "collectionName": "collection_name",
        "created": "2023-01-01 00:00:00.000Z",
        "updated": "2023-01-01 00:00:00.000Z",
        // ... other fields
    }
}
```

### PB_CONNECT Event

When the connection is established, you receive a `PB_CONNECT` event:

```csharp
pb.Realtime.Subscribe("PB_CONNECT", (e) =>
{
    Console.WriteLine($"Connected! Client ID: {e["clientId"]}");
    // e["clientId"] - unique client identifier
});
```

## Subscription Topics

### Collection-Level Subscription

Subscribe to all changes in a collection:

```csharp
// Wildcard subscription - all records in collection
pb.Collection("posts").Subscribe("*", handler);
```

**Access Control**: Uses the collection's `ListRule` to determine if the subscriber has access to receive events.

### Record-Level Subscription

Subscribe to changes for a specific record:

```csharp
// Specific record subscription
pb.Collection("posts").Subscribe("RECORD_ID", handler);
```

**Access Control**: Uses the collection's `ViewRule` to determine if the subscriber has access to receive events.

## Subscription Options

You can pass additional options when subscribing:

```csharp
pb.Collection("posts").Subscribe("*", handler, new Dictionary<string, object?>
{
    // Query parameters (for API rule filtering)
    ["filter"] = "status = \"published\"",
    ["expand"] = "author"
}, new Dictionary<string, string>
{
    // Custom headers
    ["X-Custom-Header"] = "value"
});
```

### Expand Relations

Expand relations in the event data:

```csharp
pb.Collection("posts").Subscribe("RECORD_ID", (e) =>
{
    var record = e["record"] as Dictionary<string, object?>;
    var expand = record?["expand"] as Dictionary<string, object?>;
    var author = expand?["author"] as Dictionary<string, object?>;
    Console.WriteLine($"Author: {author?["name"]}");  // Author relation expanded
}, new Dictionary<string, object?>
{
    ["expand"] = "author,categories"
});
```

### Filter with Query Parameters

Use query parameters for API rule filtering:

```csharp
pb.Collection("posts").Subscribe("*", handler, new Dictionary<string, object?>
{
    ["filter"] = "status = \"published\""
});
```

## Unsubscribing

### Unsubscribe from Specific Topic

```csharp
// Remove all subscriptions for a specific record
pb.Collection("posts").Unsubscribe("RECORD_ID");

// Remove all wildcard subscriptions for the collection
pb.Collection("posts").Unsubscribe("*");
```

### Unsubscribe from All

```csharp
// Unsubscribe from all subscriptions in the collection
pb.Collection("posts").Unsubscribe();

// Or unsubscribe from everything
pb.Realtime.Unsubscribe();
```

### Unsubscribe Using Returned Function

```csharp
var unsubscribe = pb.Collection("posts").Subscribe("*", handler);

// Later...
unsubscribe();  // Removes this specific subscription
```

## Connection Management

### Connection Status

Check if the realtime connection is established:

```csharp
// Note: The C# SDK manages connection automatically
// Connection is established when you subscribe
```

### Disconnect Handler

Handle disconnection events:

```csharp
pb.Realtime.OnDisconnect = (activeSubscriptions) =>
{
    if (activeSubscriptions.Count > 0)
    {
        Console.WriteLine($"Connection lost, but subscriptions remain: {activeSubscriptions.Count}");
        // Connection will automatically reconnect
    }
    else
    {
        Console.WriteLine("Intentionally disconnected (no active subscriptions)");
    }
};
```

### Automatic Reconnection

The SDK automatically:
- Reconnects when the connection is lost
- Resubmits all active subscriptions
- Handles network interruptions gracefully
- Closes connection after 5 minutes of inactivity (server-side timeout)

## Authorization

### Authenticated Subscriptions

Subscriptions respect authentication. If you're authenticated, events are filtered based on your permissions:

```csharp
// Authenticate first
await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password");

// Now subscribe - events will respect your permissions
pb.Collection("posts").Subscribe("*", handler);
```

### Authorization Rules

- **Collection-level (`*`)**: Uses `ListRule` to determine access
- **Record-level**: Uses `ViewRule` to determine access
- **Superusers**: Can receive all events (if rules allow)
- **Guests**: Only receive events they have permission to see

### Auth State Changes

When authentication state changes, you may need to resubscribe:

```csharp
// After login/logout, resubscribe to update permissions
await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password");

// Re-subscribe to update auth state in realtime connection
pb.Collection("posts").Subscribe("*", handler);
```

## Advanced Examples

### Example 1: Real-time Chat

```csharp
// Subscribe to messages in a chat room
Action SetupChatRoom(string roomId)
{
    return pb.Collection("messages").Subscribe("*", (e) =>
    {
        var record = e["record"] as Dictionary<string, object?>;
        var recordRoomId = record?["roomId"]?.ToString();
        
        // Filter for this room only
        if (recordRoomId == roomId)
        {
            if (e["action"]?.ToString() == "create")
            {
                DisplayMessage(record);
            }
            else if (e["action"]?.ToString() == "delete")
            {
                RemoveMessage(record?["id"]?.ToString() ?? "");
            }
        }
    }, new Dictionary<string, object?>
    {
        ["filter"] = $"roomId = \"{roomId}\""
    });
}

// Usage
var unsubscribeChat = SetupChatRoom("ROOM_ID");

// Cleanup
unsubscribeChat();
```

### Example 2: Real-time Dashboard

```csharp
// Subscribe to multiple collections
void SetupDashboard()
{
    // Posts updates
    pb.Collection("posts").Subscribe("*", (e) =>
    {
        if (e["action"]?.ToString() == "create")
        {
            var record = e["record"] as Dictionary<string, object?>;
            AddPostToFeed(record);
        }
        else if (e["action"]?.ToString() == "update")
        {
            var record = e["record"] as Dictionary<string, object?>;
            UpdatePostInFeed(record);
        }
    }, new Dictionary<string, object?>
    {
        ["filter"] = "status = \"published\"",
        ["expand"] = "author"
    });

    // Comments updates
    pb.Collection("comments").Subscribe("*", (e) =>
    {
        var record = e["record"] as Dictionary<string, object?>;
        var postId = record?["postId"]?.ToString();
        if (postId != null)
        {
            UpdateCommentsCount(postId);
        }
    }, new Dictionary<string, object?>
    {
        ["expand"] = "user"
    });
}

SetupDashboard();
```

### Example 3: User Activity Tracking

```csharp
// Track changes to a user's own records
void TrackUserActivity(string userId)
{
    pb.Collection("posts").Subscribe("*", (e) =>
    {
        var record = e["record"] as Dictionary<string, object?>;
        var author = record?["author"]?.ToString();
        
        // Only track changes to user's own posts
        if (author == userId)
        {
            Console.WriteLine($"Your post {e["action"]}: {record?["title"]}");
            
            if (e["action"]?.ToString() == "update")
            {
                ShowNotification("Post updated");
            }
        }
    }, new Dictionary<string, object?>
    {
        ["filter"] = $"author = \"{userId}\""
    });
}

var userRecord = pb.AuthStore.Record;
if (userRecord != null)
{
    TrackUserActivity(userRecord["id"]?.ToString() ?? "");
}
```

### Example 4: Real-time Collaboration

```csharp
// Track when a document is being edited
void TrackDocumentEdits(string documentId)
{
    pb.Collection("documents").Subscribe(documentId, (e) =>
    {
        if (e["action"]?.ToString() == "update")
        {
            var record = e["record"] as Dictionary<string, object?>;
            var lastEditor = record?["lastEditor"]?.ToString();
            var updatedAt = record?["updated"]?.ToString();
            
            // Show who last edited the document
            ShowEditorIndicator(lastEditor, updatedAt);
        }
    }, new Dictionary<string, object?>
    {
        ["expand"] = "lastEditor"
    });
}
```

### Example 5: Connection Monitoring

```csharp
// Monitor connection state
pb.Realtime.OnDisconnect = (activeSubscriptions) =>
{
    if (activeSubscriptions.Count > 0)
    {
        Console.WriteLine("Connection lost, attempting to reconnect...");
        ShowConnectionStatus("Reconnecting...");
    }
};

// Monitor connection establishment
pb.Realtime.Subscribe("PB_CONNECT", (e) =>
{
    Console.WriteLine($"Connected to realtime: {e["clientId"]}");
    ShowConnectionStatus("Connected");
});
```

### Example 6: Conditional Subscriptions

```csharp
// Subscribe conditionally based on user state
void SetupConditionalSubscriptions()
{
    if (pb.AuthStore.IsValid())
    {
        // Authenticated user - subscribe to private posts
        pb.Collection("posts").Subscribe("*", handler, new Dictionary<string, object?>
        {
            ["filter"] = "@request.auth.id != \"\""
        });
    }
    else
    {
        // Guest user - subscribe only to public posts
        pb.Collection("posts").Subscribe("*", handler, new Dictionary<string, object?>
        {
            ["filter"] = "public = true"
        });
    }
}
```

## Error Handling

```csharp
try
{
    pb.Collection("posts").Subscribe("*", handler);
}
catch (Exception error)
{
    if (error is ClientResponseError err)
    {
        if (err.Status == 403)
        {
            Console.Error.WriteLine("Permission denied");
        }
        else if (err.Status == 404)
        {
            Console.Error.WriteLine("Collection not found");
        }
        else
        {
            Console.Error.WriteLine($"Subscription error: {error}");
        }
    }
}
```

## Best Practices

1. **Unsubscribe When Done**: Always unsubscribe when components unmount or subscriptions are no longer needed
2. **Handle Disconnections**: Implement `OnDisconnect` handler for better UX
3. **Filter Server-Side**: Use query parameters to filter events server-side when possible
4. **Limit Subscriptions**: Don't subscribe to more collections than necessary
5. **Use Record-Level When Possible**: Prefer record-level subscriptions over collection-level when you only need specific records
6. **Monitor Connection**: Track connection state for debugging and user feedback
7. **Handle Errors**: Wrap subscriptions in try-catch blocks
8. **Respect Permissions**: Understand that events respect API rules and permissions

## Limitations

- **Maximum Subscriptions**: Up to 1000 subscriptions per client
- **Topic Length**: Maximum 2500 characters per topic
- **Idle Timeout**: Connection closes after 5 minutes of inactivity
- **Network Dependency**: Requires stable network connection
- **Platform Support**: SSE requires .NET 6+ or compatible runtime

## Troubleshooting

### Connection Not Establishing

```csharp
// Manually trigger connection by subscribing
pb.Collection("posts").Subscribe("*", handler);
```

### Events Not Received

1. Check API rules - you may not have permission
2. Verify subscription is active
3. Check network connectivity
4. Review server logs for errors

### Memory Leaks

Always unsubscribe:

```csharp
// Good
var unsubscribe = pb.Collection("posts").Subscribe("*", handler);
// ... later
unsubscribe();

// Bad - no cleanup
pb.Collection("posts").Subscribe("*", handler);
// Never unsubscribed - memory leak!
```

## Related Documentation

- [API Records](./API_RECORDS.md) - CRUD operations
- [Collections](./COLLECTIONS.md) - Collection configuration
- [API Rules and Filters](./API_RULES_AND_FILTERS.md) - Understanding API rules

