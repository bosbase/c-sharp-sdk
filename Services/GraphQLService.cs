namespace Bosbase.Services;

public class GraphQLService : BaseService
{
    public GraphQLService(BosbaseClient client) : base(client) { }

    public Task<Dictionary<string, object?>> QueryAsync(
        string query,
        IDictionary<string, object?>? variables = null,
        string? operationName = null,
        IDictionary<string, object?>? queryParams = null,
        IDictionary<string, string>? headers = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["variables"] = new Dictionary<string, object?>(variables ?? new Dictionary<string, object?>())
        };
        if (!string.IsNullOrWhiteSpace(operationName))
        {
            payload["operationName"] = operationName;
        }

        return Client.SendAsync<Dictionary<string, object?>>(
            "/api/graphql",
            new SendOptions
            {
                Method = HttpMethod.Post,
                Query = queryParams,
                Headers = headers,
                Body = payload,
                Timeout = timeout
            },
            cancellationToken);
    }
}
