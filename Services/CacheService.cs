using Bosbase.Utils;

namespace Bosbase.Services;

public class CacheService : BaseService
{
    public CacheService(BosbaseClient client) : base(client) { }

    public async Task<List<Dictionary<string, object?>>> ListAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            "/api/cache",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);

        var items = DictionaryExtensions.SafeGet(data, "items");
        if (items is IEnumerable<object?> enumerable)
        {
            return enumerable
                .Select(item => item as Dictionary<string, object?>)
                .Where(item => item != null)
                .Select(item => item!)
                .ToList();
        }
        return new List<Dictionary<string, object?>>();
    }

    public Task<Dictionary<string, object?>> CreateAsync(
        string name,
        int? sizeBytes = null,
        int? defaultTtlSeconds = null,
        int? readTimeoutMs = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["name"] = name
        };
        if (sizeBytes.HasValue) payload["sizeBytes"] = sizeBytes.Value;
        if (defaultTtlSeconds.HasValue) payload["defaultTTLSeconds"] = defaultTtlSeconds.Value;
        if (readTimeoutMs.HasValue) payload["readTimeoutMs"] = readTimeoutMs.Value;

        return Client.SendAsync<Dictionary<string, object?>>(
            "/api/cache",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> UpdateAsync(
        string name,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            $"/api/cache/{HttpHelpers.EncodePathSegment(name)}",
            new SendOptions { Method = HttpMethod.Patch, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task DeleteAsync(
        string name,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"/api/cache/{HttpHelpers.EncodePathSegment(name)}",
            new SendOptions { Method = HttpMethod.Delete, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> SetEntryAsync(
        string cache,
        string key,
        object value,
        int? ttlSeconds = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["value"] = value
        };
        if (ttlSeconds.HasValue) payload["ttlSeconds"] = ttlSeconds.Value;

        return Client.SendAsync<Dictionary<string, object?>>(
            $"/api/cache/{HttpHelpers.EncodePathSegment(cache)}/entries/{HttpHelpers.EncodePathSegment(key)}",
            new SendOptions { Method = HttpMethod.Put, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> GetEntryAsync(
        string cache,
        string key,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            $"/api/cache/{HttpHelpers.EncodePathSegment(cache)}/entries/{HttpHelpers.EncodePathSegment(key)}",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> RenewEntryAsync(
        string cache,
        string key,
        int? ttlSeconds = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>());
        if (ttlSeconds.HasValue) payload["ttlSeconds"] = ttlSeconds.Value;

        return Client.SendAsync<Dictionary<string, object?>>(
            $"/api/cache/{HttpHelpers.EncodePathSegment(cache)}/entries/{HttpHelpers.EncodePathSegment(key)}",
            new SendOptions { Method = HttpMethod.Patch, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task DeleteEntryAsync(
        string cache,
        string key,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"/api/cache/{HttpHelpers.EncodePathSegment(cache)}/entries/{HttpHelpers.EncodePathSegment(key)}",
            new SendOptions { Method = HttpMethod.Delete, Query = query, Headers = headers },
            cancellationToken);
    }
}
