using Bosbase.Models;
using Bosbase.Utils;

namespace Bosbase.Services;

public class LlmDocumentService : BaseService
{
    private const string BasePath = "/api/llm-documents";

    public LlmDocumentService(BosbaseClient client) : base(client) { }

    private string CollectionPath(string collection) => $"{BasePath}/{HttpHelpers.EncodePathSegment(collection)}";

    public async Task<List<Dictionary<string, object?>>> ListCollectionsAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<List<Dictionary<string, object?>>?>(
            $"{BasePath}/collections",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
        return data?.ToList() ?? new List<Dictionary<string, object?>>();
    }

    public Task CreateCollectionAsync(
        string name,
        IDictionary<string, string>? metadata = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"{BasePath}/collections/{HttpHelpers.EncodePathSegment(name)}",
            new SendOptions { Method = HttpMethod.Post, Body = new Dictionary<string, object?> { ["metadata"] = metadata }, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task DeleteCollectionAsync(
        string name,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"{BasePath}/collections/{HttpHelpers.EncodePathSegment(name)}",
            new SendOptions { Method = HttpMethod.Delete, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> InsertAsync(
        string collection,
        LlmDocument document,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            CollectionPath(collection),
            new SendOptions { Method = HttpMethod.Post, Body = document.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task<LlmDocument> GetAsync(
        string collection,
        string documentId,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{CollectionPath(collection)}/{HttpHelpers.EncodePathSegment(documentId)}",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
        return LlmDocument.FromDictionary(data);
    }

    public Task<Dictionary<string, object?>> UpdateAsync(
        string collection,
        string documentId,
        LlmDocumentUpdate document,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            $"{CollectionPath(collection)}/{HttpHelpers.EncodePathSegment(documentId)}",
            new SendOptions { Method = HttpMethod.Patch, Body = document.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);
    }

    public Task DeleteAsync(
        string collection,
        string documentId,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"{CollectionPath(collection)}/{HttpHelpers.EncodePathSegment(documentId)}",
            new SendOptions { Method = HttpMethod.Delete, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> ListAsync(
        string collection,
        int? page = null,
        int? perPage = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (page.HasValue) parameters["page"] = page.Value;
        if (perPage.HasValue) parameters["perPage"] = perPage.Value;

        return Client.SendAsync<Dictionary<string, object?>>(
            CollectionPath(collection),
            new SendOptions { Query = parameters, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> QueryAsync(
        string collection,
        LlmQueryOptions options,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            $"{CollectionPath(collection)}/documents/query",
            new SendOptions { Method = HttpMethod.Post, Body = options.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);
    }
}
