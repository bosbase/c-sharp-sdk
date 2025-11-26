# Cache API - C# SDK Documentation

BosBase caches combine in-memory [FreeCache](https://github.com/coocood/freecache) storage with persistent database copies. Each cache instance is safe to use in single-node or multi-node (cluster) mode: nodes read from FreeCache first, fall back to the database if an item is missing or expired, and then reload FreeCache automatically.

The C# SDK exposes the cache endpoints through `client.Caches`. Typical use cases include:

- Caching AI prompts/responses that must survive restarts.
- Quickly sharing feature flags and configuration between workers.
- Preloading expensive vector search results for short periods.

> **Timeouts & TTLs:** Each cache defines a default TTL (in seconds). Individual entries may provide their own `ttlSeconds`. A value of `0` keeps the entry until it is manually deleted.

## List available caches

The `ListAsync()` function allows you to query and retrieve all currently available caches, including their names and capacities. This is particularly useful for AI systems to discover existing caches before creating new ones, avoiding duplicate cache creation.

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("root@example.com", "hunter2");

// Query all available caches
var caches = await client.Caches.ListAsync();

// Each cache object contains:
// - name: string - The cache identifier
// - sizeBytes: long - The cache capacity in bytes
// - defaultTTLSeconds: int - Default expiration time
// - readTimeoutMs: int - Read timeout in milliseconds
// - created: string - Creation timestamp (RFC3339)
// - updated: string - Last update timestamp (RFC3339)

// Example: Find a cache by name and check its capacity
var targetCache = caches.FirstOrDefault(c => c["name"]?.ToString() == "ai-session");
if (targetCache != null)
{
    var sizeBytes = Convert.ToInt64(targetCache["sizeBytes"]);
    Console.WriteLine($"Cache \"{targetCache["name"]}\" has capacity of {sizeBytes} bytes");
    // Use the existing cache directly
}
else
{
    Console.WriteLine("Cache not found, create a new one if needed");
}
```

## Manage cache configurations

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("root@example.com", "hunter2");

// List all available caches (including name and capacity).
// This is useful for AI to discover existing caches before creating new ones.
var caches = await client.Caches.ListAsync();
Console.WriteLine("Available caches:");
foreach (var cache in caches)
{
    Console.WriteLine($"  Name: {cache["name"]}, Size: {cache["sizeBytes"]} bytes, TTL: {cache["defaultTTLSeconds"]}s");
}

// Find an existing cache by name
var existingCache = caches.FirstOrDefault(c => c["name"]?.ToString() == "ai-session");
if (existingCache != null)
{
    var sizeBytes = Convert.ToInt64(existingCache["sizeBytes"]);
    Console.WriteLine($"Found cache \"{existingCache["name"]}\" with capacity {sizeBytes} bytes");
    // Use the existing cache directly without creating a new one
}
else
{
    // Create a new cache only if it doesn't exist
    await client.Caches.CreateAsync(
        name: "ai-session",
        sizeBytes: 64 * 1024 * 1024,
        defaultTtlSeconds: 300,
        readTimeoutMs: 25 // optional concurrency guard
    );
}

// Update limits later (eg. shrink TTL to 2 minutes).
await client.Caches.UpdateAsync("ai-session", new Dictionary<string, object?>
{
    ["defaultTTLSeconds"] = 120
});

// Delete the cache (DB rows + FreeCache).
await client.Caches.DeleteAsync("ai-session");
```

Field reference:

| Field | Description |
|-------|-------------|
| `sizeBytes` | Approximate FreeCache size. Values too small (<512KB) or too large (>512MB) are clamped. |
| `defaultTTLSeconds` | Default expiration for entries. `0` means no expiration. |
| `readTimeoutMs` | Optional lock timeout while reading FreeCache. When exceeded, the value is fetched from the database instead. |

## Work with cache entries

```csharp
// Store an object in cache. The same payload is serialized into the DB.
var cacheValue = new Dictionary<string, object?>
{
    ["prompt"] = "describe Saturn",
    ["embedding"] = new[] { 0.1, 0.2, 0.3 } // vector
};

await client.Caches.SetEntryAsync(
    cache: "ai-session",
    key: "dialog:42",
    value: cacheValue,
    ttlSeconds: 90 // per-entry TTL in seconds
);

// Read from cache. `source` indicates where the hit came from.
var entry = await client.Caches.GetEntryAsync("ai-session", "dialog:42");

var source = entry["source"]?.ToString();   // "cache" or "database"
var expiresAt = entry["expiresAt"]?.ToString(); // RFC3339 timestamp or null

Console.WriteLine($"Source: {source}");
Console.WriteLine($"Expires at: {expiresAt}");

// Access the cached value
var value = entry["value"] as Dictionary<string, object?>;
if (value != null)
{
    Console.WriteLine($"Prompt: {value["prompt"]}");
    if (value["embedding"] is object[] embedding)
    {
        Console.WriteLine($"Embedding length: {embedding.Length}");
    }
}

// Renew an entry's TTL without changing its value.
// This extends the expiration time by the specified TTL (or uses the cache's default TTL if omitted).
var renewed = await client.Caches.RenewEntryAsync("ai-session", "dialog:42", ttlSeconds: 120); // extend by 120 seconds
Console.WriteLine($"New expiration: {renewed["expiresAt"]}");

// Delete an entry.
await client.Caches.DeleteEntryAsync("ai-session", "dialog:42");
```

### Cluster-aware behaviour

1. **Write-through persistence** – every `SetEntryAsync` writes to FreeCache and the `_cache_entries` table so other nodes (or a restarted node) can immediately reload values.
2. **Read path** – FreeCache is consulted first. If a lock cannot be acquired within `readTimeoutMs` or if the entry is missing/expired, BosBase queries the database copy and repopulates FreeCache in the background.
3. **Automatic cleanup** – expired entries are ignored and removed from the database when fetched, preventing stale data across nodes.

Use caches whenever you need fast, transient data that must still be recoverable or shareable across BosBase nodes.

## Complete Examples

### Example 1: Cache Manager

```csharp
class CacheManager
{
    private readonly BosbaseClient _client;

    public CacheManager(BosbaseClient client)
    {
        _client = client;
    }

    public async Task<bool> CacheExists(string cacheName)
    {
        var caches = await _client.Caches.ListAsync();
        return caches.Any(c => c["name"]?.ToString() == cacheName);
    }

    public async Task EnsureCacheExists(string cacheName, int sizeBytes = 64 * 1024 * 1024, int defaultTtlSeconds = 300)
    {
        if (!await CacheExists(cacheName))
        {
            await _client.Caches.CreateAsync(cacheName, sizeBytes, defaultTtlSeconds);
            Console.WriteLine($"Created cache: {cacheName}");
        }
    }

    public async Task<T?> GetAsync<T>(string cacheName, string key) where T : class
    {
        try
        {
            var entry = await _client.Caches.GetEntryAsync(cacheName, key);
            if (entry["value"] is T value)
            {
                return value;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync<T>(string cacheName, string key, T value, int? ttlSeconds = null)
    {
        await _client.Caches.SetEntryAsync(cacheName, key, value, ttlSeconds);
    }

    public async Task DeleteAsync(string cacheName, string key)
    {
        await _client.Caches.DeleteEntryAsync(cacheName, key);
    }
}

// Usage
var cacheManager = new CacheManager(client);
await cacheManager.EnsureCacheExists("my-cache");

await cacheManager.SetAsync("my-cache", "key1", new { data = "value1" });
var value = await cacheManager.GetAsync<Dictionary<string, object?>>("my-cache", "key1");
```

### Example 2: Feature Flags Cache

```csharp
class FeatureFlagsCache
{
    private readonly BosbaseClient _client;
    private const string CacheName = "feature-flags";

    public FeatureFlagsCache(BosbaseClient client)
    {
        _client = client;
    }

    public async Task InitializeAsync()
    {
        var caches = await _client.Caches.ListAsync();
        if (!caches.Any(c => c["name"]?.ToString() == CacheName))
        {
            await _client.Caches.CreateAsync(CacheName, 10 * 1024 * 1024, defaultTtlSeconds: 3600);
        }
    }

    public async Task<bool> IsFeatureEnabled(string featureName)
    {
        try
        {
            var entry = await _client.Caches.GetEntryAsync(CacheName, featureName);
            if (entry["value"] is Dictionary<string, object?> value)
            {
                return Convert.ToBoolean(value.GetValueOrDefault("enabled", false));
            }
        }
        catch
        {
            // Cache miss or error
        }
        return false;
    }

    public async Task SetFeatureFlag(string featureName, bool enabled)
    {
        await _client.Caches.SetEntryAsync(CacheName, featureName, new Dictionary<string, object?>
        {
            ["enabled"] = enabled,
            ["updated"] = DateTime.UtcNow.ToString("O")
        }, ttlSeconds: 3600);
    }
}

// Usage
var featureFlags = new FeatureFlagsCache(client);
await featureFlags.InitializeAsync();
await featureFlags.SetFeatureFlag("new-ui", true);
var isEnabled = await featureFlags.IsFeatureEnabled("new-ui");
```

### Example 3: AI Session Cache

```csharp
class AISessionCache
{
    private readonly BosbaseClient _client;
    private const string CacheName = "ai-sessions";

    public AISessionCache(BosbaseClient client)
    {
        _client = client;
    }

    public async Task InitializeAsync()
    {
        var caches = await _client.Caches.ListAsync();
        if (!caches.Any(c => c["name"]?.ToString() == CacheName))
        {
            await _client.Caches.CreateAsync(CacheName, 100 * 1024 * 1024, defaultTtlSeconds: 1800);
        }
    }

    public async Task StoreSessionAsync(string sessionId, Dictionary<string, object?> sessionData)
    {
        await _client.Caches.SetEntryAsync(
            CacheName,
            $"session:{sessionId}",
            sessionData,
            ttlSeconds: 1800 // 30 minutes
        );
    }

    public async Task<Dictionary<string, object?>?> GetSessionAsync(string sessionId)
    {
        try
        {
            var entry = await _client.Caches.GetEntryAsync(CacheName, $"session:{sessionId}");
            return entry["value"] as Dictionary<string, object?>;
        }
        catch
        {
            return null;
        }
    }

    public async Task ExtendSessionAsync(string sessionId)
    {
        await _client.Caches.RenewEntryAsync(CacheName, $"session:{sessionId}", ttlSeconds: 1800);
    }

    public async Task ClearSessionAsync(string sessionId)
    {
        await _client.Caches.DeleteEntryAsync(CacheName, $"session:{sessionId}");
    }
}

// Usage
var aiCache = new AISessionCache(client);
await aiCache.InitializeAsync();

var sessionData = new Dictionary<string, object?>
{
    ["prompt"] = "What is the weather?",
    ["response"] = "I don't have access to real-time weather data.",
    ["timestamp"] = DateTime.UtcNow.ToString("O")
};

await aiCache.StoreSessionAsync("user123", sessionData);
var retrieved = await aiCache.GetSessionAsync("user123");
```

## Error Handling

```csharp
try
{
    var entry = await client.Caches.GetEntryAsync("my-cache", "key");
}
catch (ClientResponseError ex)
{
    if (ex.Status == 404)
    {
        Console.WriteLine("Cache or entry not found");
    }
    else if (ex.Status == 401)
    {
        Console.WriteLine("Not authenticated");
    }
    else if (ex.Status == 403)
    {
        Console.WriteLine("Not a superuser");
    }
    else
    {
        Console.WriteLine($"Unexpected error: {ex.Message}");
    }
}
```

## Best Practices

1. **Check Before Create**: Always check if a cache exists before creating it to avoid duplicates
2. **Appropriate TTLs**: Set appropriate TTLs based on your data freshness requirements
3. **Size Limits**: Be mindful of cache size limits to avoid memory issues
4. **Error Handling**: Always handle cache misses and errors gracefully
5. **Key Naming**: Use consistent, descriptive key naming conventions
6. **Cluster Awareness**: Remember that caches are cluster-aware and will sync across nodes
7. **Cleanup**: Periodically clean up expired or unused cache entries
8. **Monitoring**: Monitor cache hit rates and performance

## Limitations

- **Superuser Only**: Cache management operations require superuser authentication
- **Size Constraints**: Cache sizes are clamped between 512KB and 512MB
- **TTL Precision**: TTLs are in seconds, not milliseconds
- **Serialization**: Values are serialized as JSON, so complex objects must be serializable
- **No Atomic Operations**: Cache operations are not atomic across multiple entries

## Related Documentation

- [Authentication](./AUTHENTICATION.md) - Superuser authentication
- [Collection API](./COLLECTION_API.md) - Collection management

