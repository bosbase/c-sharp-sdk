using Bosbase.Exceptions;
using Bosbase.Utils;

namespace Bosbase.Services;

public abstract class BaseService
{
    protected BaseService(BosbaseClient client)
    {
        Client = client;
    }

    protected BosbaseClient Client { get; }
}

public abstract class BaseCrudService : BaseService
{
    protected BaseCrudService(BosbaseClient client) : base(client)
    {
    }

    protected abstract string BaseCrudPath { get; }

    public virtual async Task<IReadOnlyList<object?>> GetFullListAsync(
        int batch = 500,
        string? expand = null,
        string? filter = null,
        string? sort = null,
        string? fields = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (batch <= 0) throw new ArgumentException("batch must be > 0", nameof(batch));

        var result = new List<object?>();
        var page = 1;

        while (true)
        {
            var list = await GetListAsync(
                page: page,
                perPage: batch,
                skipTotal: true,
                expand: expand,
                filter: filter,
                sort: sort,
                fields: fields,
                query: query,
                headers: headers,
                cancellationToken: cancellationToken);

            if (list.TryGetValue("items", out var itemsObj) && itemsObj is IEnumerable<object?> items)
            {
                var batchItems = items.ToList();
                result.AddRange(batchItems);
                if (batchItems.Count < Convert.ToInt32(DictionaryExtensions.SafeGet(list, "perPage", batch)))
                {
                    break;
                }
            }
            else
            {
                break;
            }

            page++;
        }

        return result;
    }

    public virtual Task<Dictionary<string, object?>> GetListAsync(
        int page = 1,
        int perPage = 30,
        bool skipTotal = false,
        string? expand = null,
        string? filter = null,
        string? sort = null,
        string? fields = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>())
        {
            ["page"] = page,
            ["perPage"] = perPage,
            ["skipTotal"] = skipTotal
        };
        if (filter != null) parameters.TryAdd("filter", filter);
        if (sort != null) parameters.TryAdd("sort", sort);
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        return Client.SendAsync<Dictionary<string, object?>>(
            BaseCrudPath,
            new SendOptions
            {
                Query = parameters,
                Headers = headers
            },
            cancellationToken);
    }

    public virtual Task<Dictionary<string, object?>> GetOneAsync(
        string recordId,
        string? expand = null,
        string? fields = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            throw new ClientResponseError(
                url: Client.BuildUrl($"{BaseCrudPath}/"),
                status: 404,
                response: new Dictionary<string, object?>
                {
                    ["code"] = 404,
                    ["message"] = "Missing required record id.",
                    ["data"] = new Dictionary<string, object?>()
                });
        }

        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var encodedId = HttpHelpers.EncodePathSegment(recordId);

        return Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCrudPath}/{encodedId}",
            new SendOptions
            {
                Query = parameters,
                Headers = headers
            },
            cancellationToken);
    }

    public virtual async Task<Dictionary<string, object?>> GetFirstListItemAsync(
        string filter,
        string? expand = null,
        string? fields = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await GetListAsync(
            page: 1,
            perPage: 1,
            skipTotal: true,
            filter: filter,
            expand: expand,
            fields: fields,
            query: query,
            headers: headers,
            cancellationToken: cancellationToken);

        if (data.TryGetValue("items", out var itemsObj) && itemsObj is IEnumerable<object?> items)
        {
            var first = items.FirstOrDefault() as Dictionary<string, object?>;
            if (first != null)
            {
                return first;
            }
        }

        throw new ClientResponseError(
            status: 404,
            response: new Dictionary<string, object?>
            {
                ["code"] = 404,
                ["message"] = "The requested resource wasn't found.",
                ["data"] = new Dictionary<string, object?>()
            });
    }

    public virtual Task<Dictionary<string, object?>> CreateAsync(
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IEnumerable<Models.FileAttachment>? files = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        return Client.SendAsync<Dictionary<string, object?>>(
            BaseCrudPath,
            new SendOptions
            {
                Method = HttpMethod.Post,
                Body = body,
                Query = parameters,
                Headers = headers,
                Files = files
            },
            cancellationToken);
    }

    public virtual Task<Dictionary<string, object?>> UpdateAsync(
        string recordId,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IEnumerable<Models.FileAttachment>? files = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var encodedId = HttpHelpers.EncodePathSegment(recordId);

        return Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCrudPath}/{encodedId}",
            new SendOptions
            {
                Method = HttpMethod.Patch,
                Body = body,
                Query = parameters,
                Headers = headers,
                Files = files
            },
            cancellationToken);
    }

    public virtual Task DeleteAsync(
        string recordId,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var encodedId = HttpHelpers.EncodePathSegment(recordId);

        return Client.SendAsync(
            $"{BaseCrudPath}/{encodedId}",
            new SendOptions
            {
                Method = HttpMethod.Delete,
                Body = body,
                Query = query,
                Headers = headers
            },
            cancellationToken);
    }
}
