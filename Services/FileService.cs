using Bosbase.Utils;

namespace Bosbase.Services;

public class FileService : BaseService
{
    public FileService(BosbaseClient client) : base(client) { }

    public string GetUrl(
        IDictionary<string, object?> record,
        string filename,
        string? thumb = null,
        string? token = null,
        bool? download = null,
        IDictionary<string, object?>? query = null)
    {
        var recordId = DictionaryExtensions.SafeGet(record, "id")?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(recordId) || string.IsNullOrWhiteSpace(filename))
        {
            return string.Empty;
        }

        var collection = DictionaryExtensions.SafeGet(record, "collectionId")?.ToString()
            ?? DictionaryExtensions.SafeGet(record, "collectionName")?.ToString()
            ?? string.Empty;

        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (thumb != null) parameters.TryAdd("thumb", thumb);
        if (token != null) parameters.TryAdd("token", token);
        if (download == true) parameters["download"] = string.Empty;

        return Client.BuildUrl(
            $"/api/files/{HttpHelpers.EncodePathSegment(collection)}/{HttpHelpers.EncodePathSegment(recordId)}/{HttpHelpers.EncodePathSegment(filename)}",
            parameters);
    }

    public async Task<string> GetTokenAsync(
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var data = await Client.SendAsync<Dictionary<string, object?>>(
            "/api/files/token",
            new SendOptions { Method = HttpMethod.Post, Body = body, Query = query, Headers = headers },
            cancellationToken);

        return DictionaryExtensions.SafeGet(data, "token")?.ToString() ?? string.Empty;
    }
}
