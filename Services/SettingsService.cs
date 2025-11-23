using Bosbase.Utils;

namespace Bosbase.Services;

public class SettingsService : BaseService
{
    public SettingsService(BosbaseClient client) : base(client) { }

    public Task<Dictionary<string, object?>> GetAllAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            "/api/settings",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> UpdateAsync(
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            "/api/settings",
            new SendOptions { Method = HttpMethod.Patch, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task TestS3Async(
        string filesystem = "storage",
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["filesystem"] = filesystem
        };

        return Client.SendAsync(
            "/api/settings/test/s3",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task TestEmailAsync(
        string toEmail,
        string template,
        string? collection = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["email"] = toEmail,
            ["template"] = template
        };
        if (!string.IsNullOrWhiteSpace(collection))
        {
            payload["collection"] = collection;
        }

        return Client.SendAsync(
            "/api/settings/test/email",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> GenerateAppleClientSecretAsync(
        string clientId,
        string teamId,
        string keyId,
        string privateKey,
        int duration,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["clientId"] = clientId,
            ["teamId"] = teamId,
            ["keyId"] = keyId,
            ["privateKey"] = privateKey,
            ["duration"] = duration
        };

        return Client.SendAsync<Dictionary<string, object?>>(
            "/api/settings/apple/generate-client-secret",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task<Dictionary<string, object?>?> GetCategoryAsync(
        string category,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(query, headers, cancellationToken);
        return all.TryGetValue(category, out var value) ? value as Dictionary<string, object?> : null;
    }

    public Task<Dictionary<string, object?>> UpdateMetaAsync(
        string? appName = null,
        string? appUrl = null,
        string? senderName = null,
        string? senderAddress = null,
        bool? hideControls = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var meta = new Dictionary<string, object?>();
        if (appName != null) meta["appName"] = appName;
        if (appUrl != null) meta["appURL"] = appUrl;
        if (senderName != null) meta["senderName"] = senderName;
        if (senderAddress != null) meta["senderAddress"] = senderAddress;
        if (hideControls.HasValue) meta["hideControls"] = hideControls.Value;

        return UpdateAsync(new Dictionary<string, object?> { ["meta"] = meta }, query, headers, cancellationToken);
    }

    public async Task<Dictionary<string, object?>> GetApplicationSettingsAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetAllAsync(query, headers, cancellationToken);
        return new Dictionary<string, object?>
        {
            ["meta"] = DictionaryExtensions.SafeGet(settings, "meta"),
            ["trustedProxy"] = DictionaryExtensions.SafeGet(settings, "trustedProxy"),
            ["rateLimits"] = DictionaryExtensions.SafeGet(settings, "rateLimits"),
            ["batch"] = DictionaryExtensions.SafeGet(settings, "batch"),
        };
    }

    public Task<Dictionary<string, object?>> UpdateApplicationSettingsAsync(
        IDictionary<string, object?>? meta = null,
        IDictionary<string, object?>? trustedProxy = null,
        IDictionary<string, object?>? rateLimits = null,
        IDictionary<string, object?>? batch = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>();
        if (meta != null) payload["meta"] = new Dictionary<string, object?>(meta);
        if (trustedProxy != null) payload["trustedProxy"] = new Dictionary<string, object?>(trustedProxy);
        if (rateLimits != null) payload["rateLimits"] = new Dictionary<string, object?>(rateLimits);
        if (batch != null) payload["batch"] = new Dictionary<string, object?>(batch);

        return UpdateAsync(payload, query, headers, cancellationToken);
    }
}
