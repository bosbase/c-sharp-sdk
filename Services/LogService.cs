using Bosbase.Exceptions;

namespace Bosbase.Services;

public class LogService : BaseService
{
    public LogService(BosbaseClient client) : base(client) { }

    public Task<Dictionary<string, object?>> GetListAsync(
        int page = 1,
        int perPage = 30,
        string? filter = null,
        string? sort = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>())
        {
            ["page"] = page,
            ["perPage"] = perPage
        };
        if (filter != null) parameters.TryAdd("filter", filter);
        if (sort != null) parameters.TryAdd("sort", sort);

        return Client.SendAsync<Dictionary<string, object?>>(
            "/api/logs",
            new SendOptions { Query = parameters, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> GetOneAsync(
        string logId,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logId))
        {
            throw new ClientResponseError(
                url: Client.BuildUrl("/api/logs/"),
                status: 404,
                response: new Dictionary<string, object?>
                {
                    ["code"] = 404,
                    ["message"] = "Missing required log id.",
                    ["data"] = new Dictionary<string, object?>()
                });
        }

        return Client.SendAsync<Dictionary<string, object?>>(
            $"/api/logs/{logId}",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<List<Dictionary<string, object?>>?> GetStatsAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<List<Dictionary<string, object?>>?>(
            "/api/logs/stats",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
    }
}
