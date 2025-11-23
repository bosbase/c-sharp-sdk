using Bosbase.Models;
using Bosbase.Utils;

namespace Bosbase.Services;

public class BatchService : BaseService
{
    private readonly List<Dictionary<string, object?>> _requests = new();
    private readonly Dictionary<string, SubBatchService> _collections = new();

    public BatchService(BosbaseClient client) : base(client) { }

    public SubBatchService Collection(string collectionIdOrName)
    {
        if (!_collections.TryGetValue(collectionIdOrName, out var service))
        {
            service = new SubBatchService(this, collectionIdOrName);
            _collections[collectionIdOrName] = service;
        }
        return service;
    }

    public void QueueRequest(
        string method,
        string url,
        IDictionary<string, string>? headers = null,
        IDictionary<string, object?>? body = null,
        IEnumerable<(string Field, FileAttachment Attachment)>? files = null)
    {
        _requests.Add(new Dictionary<string, object?>
        {
            ["method"] = method,
            ["url"] = url,
            ["headers"] = new Dictionary<string, string>(headers ?? new Dictionary<string, string>()),
            ["body"] = Serialization.ToSerializable(body) ?? new Dictionary<string, object?>(),
            ["files"] = files?.ToList() ?? new List<(string Field, FileAttachment Attachment)>()
        });
    }

    public async Task<List<Dictionary<string, object?>>?> SendAsync(
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var requestsPayload = new List<Dictionary<string, object?>>();
        var attachments = new List<FileAttachment>();

        for (var index = 0; index < _requests.Count; index++)
        {
            var req = _requests[index];
            requestsPayload.Add(new Dictionary<string, object?>
            {
                ["method"] = req["method"],
                ["url"] = req["url"],
                ["headers"] = req["headers"],
                ["body"] = req["body"]
            });

            if (req.TryGetValue("files", out var fileObj) && fileObj is IEnumerable<(string Field, FileAttachment Attachment)> filesList)
            {
                foreach (var (field, attachment) in filesList)
                {
                    attachments.Add(new FileAttachment($"requests.{index}.{field}", attachment.Content, attachment.FileName, attachment.ContentType));
                }
            }
        }

        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["requests"] = requestsPayload
        };

        var response = await Client.SendAsync<List<Dictionary<string, object?>>?>(
            "/api/batch",
            new SendOptions
            {
                Method = HttpMethod.Post,
                Body = payload,
                Query = query,
                Headers = headers,
                Files = attachments.Any() ? attachments : null
            },
            cancellationToken);

        _requests.Clear();
        return response;
    }
}

public class SubBatchService
{
    private readonly BatchService _batch;
    private readonly string _collection;

    public SubBatchService(BatchService batch, string collectionIdOrName)
    {
        _batch = batch;
        _collection = collectionIdOrName;
    }

    private string CollectionUrl() => $"/api/collections/{HttpHelpers.EncodePathSegment(_collection)}/records";

    public void Create(
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IEnumerable<FileAttachment>? files = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);
        var url = HttpHelpers.BuildRelativeUrl(CollectionUrl(), parameters);

        _batch.QueueRequest("POST", url, headers, body, NormalizeFiles(files));
    }

    public void Upsert(
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IEnumerable<FileAttachment>? files = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);
        var url = HttpHelpers.BuildRelativeUrl(CollectionUrl(), parameters);

        _batch.QueueRequest("PUT", url, headers, body, NormalizeFiles(files));
    }

    public void Update(
        string recordId,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IEnumerable<FileAttachment>? files = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);
        var url = HttpHelpers.BuildRelativeUrl($"{CollectionUrl()}/{HttpHelpers.EncodePathSegment(recordId)}", parameters);
        _batch.QueueRequest("PATCH", url, headers, body, NormalizeFiles(files));
    }

    public void Delete(
        string recordId,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null)
    {
        var url = HttpHelpers.BuildRelativeUrl($"{CollectionUrl()}/{HttpHelpers.EncodePathSegment(recordId)}", query);
        _batch.QueueRequest("DELETE", url, headers, body, null);
    }

    private static IEnumerable<(string Field, FileAttachment Attachment)>? NormalizeFiles(IEnumerable<FileAttachment>? files)
    {
        if (files == null) return null;
        return files.Select(f => (f.FieldName, f));
    }
}
