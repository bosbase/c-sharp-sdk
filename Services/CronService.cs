using Bosbase.Utils;

namespace Bosbase.Services;

public class CronService : BaseService
{
    public CronService(BosbaseClient client) : base(client) { }

    public async Task<List<Dictionary<string, object?>>> GetFullListAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<List<Dictionary<string, object?>>?>(
            "/api/crons",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
        return data?.ToList() ?? new List<Dictionary<string, object?>>();
    }

    public Task RunAsync(
        string jobId,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"/api/crons/{HttpHelpers.EncodePathSegment(jobId)}",
            new SendOptions { Method = HttpMethod.Post, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }
}
