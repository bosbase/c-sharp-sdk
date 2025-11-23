using Bosbase.Models;
using Bosbase.Utils;

namespace Bosbase.Services;

public class BackupService : BaseService
{
    public BackupService(BosbaseClient client) : base(client) { }

    public async Task<List<Dictionary<string, object?>>> GetFullListAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<List<Dictionary<string, object?>>?>(
            "/api/backups",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
        return data?.ToList() ?? new List<Dictionary<string, object?>>();
    }

    public Task CreateAsync(
        string name,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["name"] = name
        };
        return Client.SendAsync(
            "/api/backups",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task UploadAsync(
        IEnumerable<FileAttachment> files,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            "/api/backups/upload",
            new SendOptions { Method = HttpMethod.Post, Body = body, Query = query, Headers = headers, Files = files },
            cancellationToken);
    }

    public Task DeleteAsync(
        string key,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"/api/backups/{HttpHelpers.EncodePathSegment(key)}",
            new SendOptions { Method = HttpMethod.Delete, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task RestoreAsync(
        string key,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"/api/backups/{HttpHelpers.EncodePathSegment(key)}/restore",
            new SendOptions { Method = HttpMethod.Post, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public string GetDownloadUrl(string token, string key, IDictionary<string, object?>? query = null)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>())
        {
            ["token"] = token
        };

        return Client.BuildUrl($"/api/backups/{HttpHelpers.EncodePathSegment(key)}", parameters);
    }
}
