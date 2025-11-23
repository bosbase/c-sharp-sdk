namespace Bosbase.Services;

public class HealthService : BaseService
{
    public HealthService(BosbaseClient client) : base(client) { }

    public Task<Dictionary<string, object?>> CheckAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            "/api/health",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
    }
}
