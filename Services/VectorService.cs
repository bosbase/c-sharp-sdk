using Bosbase.Models;
using Bosbase.Utils;

namespace Bosbase.Services;

public class VectorService : BaseService
{
    private const string BasePath = "/api/vectors";

    public VectorService(BosbaseClient client) : base(client) { }

    private string CollectionPath(string? collection)
    {
        return collection != null ? $"{BasePath}/{HttpHelpers.EncodePathSegment(collection)}" : BasePath;
    }

    public async Task<VectorInsertResponse> InsertAsync(
        VectorDocument document,
        string? collection = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            CollectionPath(collection),
            new SendOptions { Method = HttpMethod.Post, Body = document.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);
        return new VectorInsertResponse(DictionaryExtensions.SafeGet(data, "id")?.ToString() ?? string.Empty, Convert.ToBoolean(DictionaryExtensions.SafeGet(data, "success")));
    }

    public async Task<VectorBatchInsertResponse> BatchInsertAsync(
        VectorBatchInsertOptions options,
        string? collection = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{CollectionPath(collection)}/documents/batch",
            new SendOptions { Method = HttpMethod.Post, Body = options.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);

        var ids = (DictionaryExtensions.SafeGet(data, "ids") as IEnumerable<object?>)?.Select(x => x?.ToString() ?? string.Empty).ToList() ?? new List<string>();
        var errors = (DictionaryExtensions.SafeGet(data, "errors") as IEnumerable<object?>)?.Select(x => x?.ToString() ?? string.Empty).ToList();

        return new VectorBatchInsertResponse(
            InsertedCount: Convert.ToInt32(DictionaryExtensions.SafeGet(data, "insertedCount") ?? 0),
            FailedCount: Convert.ToInt32(DictionaryExtensions.SafeGet(data, "failedCount") ?? 0),
            Ids: ids,
            Errors: errors);
    }

    public async Task<VectorInsertResponse> UpdateAsync(
        string documentId,
        VectorDocument document,
        string? collection = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{CollectionPath(collection)}/{HttpHelpers.EncodePathSegment(documentId)}",
            new SendOptions { Method = HttpMethod.Patch, Body = document.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);

        return new VectorInsertResponse(DictionaryExtensions.SafeGet(data, "id")?.ToString() ?? string.Empty, Convert.ToBoolean(DictionaryExtensions.SafeGet(data, "success")));
    }

    public Task DeleteAsync(
        string documentId,
        string? collection = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"{CollectionPath(collection)}/{HttpHelpers.EncodePathSegment(documentId)}",
            new SendOptions { Method = HttpMethod.Delete, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task<VectorSearchResponse> SearchAsync(
        VectorSearchOptions options,
        string? collection = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{CollectionPath(collection)}/documents/search",
            new SendOptions { Method = HttpMethod.Post, Body = options.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);

        var results = new List<VectorSearchResult>();
        if (data.TryGetValue("results", out var resObj) && resObj is IEnumerable<object?> list)
        {
            foreach (var item in list)
            {
                if (item is IDictionary<string, object?> dict)
                {
                    var docObj = DictionaryExtensions.SafeGet(dict, "document") as IDictionary<string, object?> ?? new Dictionary<string, object?>();
                    var doc = VectorDocument.FromDictionary(docObj);
                    double.TryParse(DictionaryExtensions.SafeGet(dict, "score")?.ToString(), out var score);
                    double? distance = null;
                    if (dict.TryGetValue("distance", out var distObj) && double.TryParse(distObj?.ToString(), out var dist))
                    {
                        distance = dist;
                    }
                    results.Add(new VectorSearchResult(doc, score, distance));
                }
            }
        }

        int? totalMatches = null;
        if (data.TryGetValue("totalMatches", out var tm) && int.TryParse(tm?.ToString(), out var tmVal))
        {
            totalMatches = tmVal;
        }

        int? queryTime = null;
        if (data.TryGetValue("queryTime", out var qt) && int.TryParse(qt?.ToString(), out var qtVal))
        {
            queryTime = qtVal;
        }

        return new VectorSearchResponse(results, totalMatches, queryTime);
    }

    public async Task<VectorDocument> GetAsync(
        string documentId,
        string? collection = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{CollectionPath(collection)}/{HttpHelpers.EncodePathSegment(documentId)}",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
        return VectorDocument.FromDictionary(data);
    }

    public Task<Dictionary<string, object?>> ListAsync(
        string? collection = null,
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

    public Task CreateCollectionAsync(
        string name,
        VectorCollectionConfig config,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"{BasePath}/collections/{HttpHelpers.EncodePathSegment(name)}",
            new SendOptions { Method = HttpMethod.Post, Body = config.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);
    }

    public Task UpdateCollectionAsync(
        string name,
        VectorCollectionConfig config,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"{BasePath}/collections/{HttpHelpers.EncodePathSegment(name)}",
            new SendOptions { Method = HttpMethod.Patch, Body = config.ToDictionary(), Query = query, Headers = headers },
            cancellationToken);
    }

    public Task DeleteCollectionAsync(
        string name,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync(
            $"{BasePath}/collections/{HttpHelpers.EncodePathSegment(name)}",
            new SendOptions { Method = HttpMethod.Delete, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task<List<VectorCollectionInfo>> ListCollectionsAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<List<Dictionary<string, object?>>?>(
            $"{BasePath}/collections",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);

        var result = new List<VectorCollectionInfo>();
        if (data != null)
        {
            foreach (var item in data)
            {
                var name = DictionaryExtensions.SafeGet(item, "name")?.ToString() ?? string.Empty;
                int? count = null;
                if (item.TryGetValue("count", out var countObj) && int.TryParse(countObj?.ToString(), out var cnt))
                {
                    count = cnt;
                }
                int? dimension = null;
                if (item.TryGetValue("dimension", out var dimObj) && int.TryParse(dimObj?.ToString(), out var dim))
                {
                    dimension = dim;
                }
                result.Add(new VectorCollectionInfo(name, count, dimension));
            }
        }

        return result;
    }
}
