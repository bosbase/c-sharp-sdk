using Bosbase.Models;

namespace Bosbase.Services;

public class LangChaingoService : BaseService
{
    private const string BasePath = "/api/langchaingo";

    public LangChaingoService(BosbaseClient client) : base(client) { }

    public async Task<LangChaingoCompletionResponse> CompletionsAsync(
        LangChaingoCompletionRequest payload,
        IDictionary<string, string>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BasePath}/completions",
            new SendOptions
            {
                Method = HttpMethod.Post,
                Body = payload.ToDictionary(),
                Query = query?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
                Headers = headers
            },
            cancellationToken);
        return LangChaingoCompletionResponse.FromDictionary(data);
    }

    public async Task<LangChaingoRagResponse> RagAsync(
        LangChaingoRagRequest payload,
        IDictionary<string, string>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BasePath}/rag",
            new SendOptions
            {
                Method = HttpMethod.Post,
                Body = payload.ToDictionary(),
                Query = query?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
                Headers = headers
            },
            cancellationToken);
        return LangChaingoRagResponse.FromDictionary(data);
    }

    public async Task<LangChaingoRagResponse> QueryDocumentsAsync(
        LangChaingoDocumentQueryRequest payload,
        IDictionary<string, string>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BasePath}/documents/query",
            new SendOptions
            {
                Method = HttpMethod.Post,
                Body = payload.ToDictionary(),
                Query = query?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
                Headers = headers
            },
            cancellationToken);
        return LangChaingoRagResponse.FromDictionary(data);
    }

    public async Task<LangChaingoSqlResponse> SqlAsync(
        LangChaingoSqlRequest payload,
        IDictionary<string, string>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BasePath}/sql",
            new SendOptions
            {
                Method = HttpMethod.Post,
                Body = payload.ToDictionary(),
                Query = query?.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
                Headers = headers
            },
            cancellationToken);
        return LangChaingoSqlResponse.FromDictionary(data);
    }
}
