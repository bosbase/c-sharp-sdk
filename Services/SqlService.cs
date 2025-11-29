using Bosbase.Models;

namespace Bosbase.Services;

public class SqlService : BaseService
{
    private const string BasePath = "/api/sql";

    public SqlService(BosbaseClient client) : base(client) { }

    public async Task<SqlExecuteResponse> ExecuteAsync(
        string query,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? queryParams = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("query is required", nameof(query));
        }

        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["query"] = trimmed
        };

        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BasePath}/execute",
            new SendOptions
            {
                Method = HttpMethod.Post,
                Body = payload,
                Query = queryParams,
                Headers = headers
            },
            cancellationToken);

        return SqlExecuteResponse.FromDictionary(data);
    }
}
