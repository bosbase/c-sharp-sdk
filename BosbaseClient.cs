using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bosbase.Auth;
using Bosbase.Exceptions;
using Bosbase.Models;
using Bosbase.Services;
using Bosbase.Utils;

namespace Bosbase;

public class SendOptions
{
    public HttpMethod Method { get; set; } = HttpMethod.Get;
    public IDictionary<string, string>? Headers { get; set; }
    public IDictionary<string, object?>? Query { get; set; }
    public object? Body { get; set; }
    public IEnumerable<FileAttachment>? Files { get; set; }
    public TimeSpan? Timeout { get; set; }
    public HttpClient? CustomHttpClient { get; set; }

    public SendOptions Clone()
    {
        return new SendOptions
        {
            Method = Method,
            Headers = Headers != null ? new Dictionary<string, string>(Headers) : null,
            Query = Query != null ? new Dictionary<string, object?>(Query) : null,
            Body = Body,
            Files = Files?.ToList(),
            Timeout = Timeout,
            CustomHttpClient = CustomHttpClient
        };
    }
}

public class BeforeSendResult
{
    public string? Url { get; set; }
    public SendOptions? Options { get; set; }
}

/// <summary>
/// Main entry point to interact with the BosBase API.
/// </summary>
public class BosbaseClient : IDisposable
{
    private const string UserAgent = "bosbase-csharp-sdk/0.1.0";
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, RecordService> _recordServices = new();
    private bool _disposed;

    public string BaseUrl { get; }
    public string Lang { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public AuthStore AuthStore { get; }
    public Func<string, SendOptions, Task<BeforeSendResult?>>? BeforeSend { get; set; }
    public Func<HttpResponseMessage, object?, SendOptions, Task<object?>>? AfterSend { get; set; }

    public CollectionService Collections { get; }
    public FileService Files { get; }
    public LogService Logs { get; }
    public SettingsService Settings { get; }
    public RealtimeService Realtime { get; }
    public PubSubService PubSub { get; }
    public HealthService Health { get; }
    public BackupService Backups { get; }
    public CronService Crons { get; }
    public VectorService Vectors { get; }
    public LlmDocumentService LlmDocuments { get; }
    public LangChaingoService LangChaingo { get; }
    public CacheService Caches { get; }
    public GraphQLService Graphql { get; }
    public SqlService Sql { get; }

    public BosbaseClient(string baseUrl, string lang = "en-US", AuthStore? authStore = null, HttpClient? httpClient = null)
    {
        var normalized = (baseUrl ?? "/").TrimEnd('/');
        BaseUrl = string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
        Lang = lang;
        AuthStore = authStore ?? new AuthStore();
        _httpClient = httpClient ?? new HttpClient();

        Collections = new CollectionService(this);
        Files = new FileService(this);
        Logs = new LogService(this);
        Settings = new SettingsService(this);
        Realtime = new RealtimeService(this);
        PubSub = new PubSubService(this);
        Health = new HealthService(this);
        Backups = new BackupService(this);
        Crons = new CronService(this);
        Vectors = new VectorService(this);
        LlmDocuments = new LlmDocumentService(this);
        LangChaingo = new LangChaingoService(this);
        Caches = new CacheService(this);
        Graphql = new GraphQLService(this);
        Sql = new SqlService(this);
    }

    public RecordService Collection(string collectionIdOrName)
    {
        if (!_recordServices.TryGetValue(collectionIdOrName, out var service))
        {
            service = new RecordService(this, collectionIdOrName);
            _recordServices[collectionIdOrName] = service;
        }
        return service;
    }

    public BatchService CreateBatch()
    {
        return new BatchService(this);
    }

    public string Filter(string raw, IDictionary<string, object?>? parameters = null)
    {
        if (parameters == null || !parameters.Any())
        {
            return raw;
        }

        foreach (var kvp in parameters)
        {
            var placeholder = "{:" + kvp.Key + "}";
            var val = kvp.Value;
            string replacement;
            switch (val)
            {
                case null:
                    replacement = "null";
                    break;
                case bool b:
                    replacement = b ? "true" : "false";
                    break;
                case DateTime dt:
                    replacement = $"'{dt.ToUniversalTime():yyyy-MM-dd HH:mm:ss}'";
                    break;
                case DateTimeOffset dto:
                    replacement = $"'{dto.UtcDateTime:yyyy-MM-dd HH:mm:ss}'";
                    break;
                case string s:
                    replacement = $"'{s.Replace("'", "\\'")}'";
                    break;
                default:
                    replacement = $"'{Serialization.ToJson(Serialization.ToSerializable(val))?.Replace("'", "\\'")}'";
                    break;
            }
            raw = raw.Replace(placeholder, replacement);
        }

        return raw;
    }

    public string BuildUrl(string path, IDictionary<string, object?>? query = null)
    {
        var baseUrl = BaseUrl.EndsWith("/") ? BaseUrl : $"{BaseUrl}/";
        var rel = path.TrimStart('/');
        var url = $"{baseUrl}{rel}";

        if (query != null && query.Any())
        {
            var queryString = HttpHelpers.BuildQuery(query);
            if (!string.IsNullOrEmpty(queryString))
            {
                url += url.Contains("?") ? "&" : "?";
                url += queryString;
            }
        }

        return url;
    }

    public string GetFileUrl(
        IDictionary<string, object?> record,
        string filename,
        string? thumb = null,
        string? token = null,
        bool? download = null,
        IDictionary<string, object?>? query = null)
    {
        return Files.GetUrl(record, filename, thumb: thumb, token: token, download: download, query: query);
    }

    public async Task<object?> SendAsync(string path, SendOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SendOptions();
        var currentQuery = new Dictionary<string, object?>(options.Query ?? new Dictionary<string, object?>());
        var url = BuildUrl(path, currentQuery);

        if (BeforeSend != null)
        {
            var hookOptions = options.Clone();
            var hookResult = await BeforeSend(url, hookOptions);

            options = hookOptions;
            currentQuery = new Dictionary<string, object?>(options.Query ?? new Dictionary<string, object?>());
            url = BuildUrl(path, currentQuery);

            if (hookResult != null)
            {
                if (!string.IsNullOrWhiteSpace(hookResult.Url))
                {
                    url = hookResult.Url!;
                }

                if (hookResult.Options != null)
                {
                    options = hookResult.Options;
                    currentQuery = new Dictionary<string, object?>(options.Query ?? new Dictionary<string, object?>());
                    url = BuildUrl(path, currentQuery);
                }
            }
        }

        var request = new HttpRequestMessage(options.Method, url);
        var headers = new Dictionary<string, string>(options.Headers ?? new Dictionary<string, string>());

        if (!headers.ContainsKey("Accept-Language"))
        {
            headers["Accept-Language"] = Lang;
        }

        if (!headers.ContainsKey("User-Agent"))
        {
            headers["User-Agent"] = UserAgent;
        }

        if (!headers.ContainsKey("Authorization") && AuthStore.IsValid())
        {
            headers["Authorization"] = AuthStore.Token;
        }

        foreach (var kvp in headers)
        {
            if (!request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value))
            {
                request.Content ??= new StringContent(string.Empty);
                request.Content.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        }

        var content = HttpHelpers.BuildContent(options.Body, options.Files, Serialization.DefaultJsonOptions);
        if (content != null && request.Content == null)
        {
            request.Content = content;
        }

        var client = options.CustomHttpClient ?? _httpClient;
        client.Timeout = options.Timeout ?? Timeout;

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException tex)
        {
            throw new ClientResponseError(url: url, status: 0, response: new Dictionary<string, object?>(), isAbort: true, originalError: tex);
        }
        catch (HttpRequestException httpEx)
        {
            throw new ClientResponseError(httpEx, url);
        }
        catch (Exception ex)
        {
            throw new ClientResponseError(url: url, status: 0, response: new Dictionary<string, object?>(), originalError: ex);
        }

        object? data = null;
        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            var contentType = response.Content?.Headers.ContentType?.MediaType ?? string.Empty;
            if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                var text = await response.Content!.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    try
                    {
                        var element = JsonSerializer.Deserialize<JsonElement>(text);
                        data = Serialization.FromJsonElement(element);
                    }
                    catch
                    {
                        data = new Dictionary<string, object?>();
                    }
                }
            }
            else
            {
                data = response.Content != null ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : null;
            }
        }

        if ((int)response.StatusCode >= 400)
        {
            var errData = data as IDictionary<string, object?> ?? new Dictionary<string, object?>();
            throw new ClientResponseError(url: url, status: (int)response.StatusCode, response: errData);
        }

        if (AfterSend != null)
        {
            data = await AfterSend(response, data, options);
        }

        return data;
    }

    public async Task<T> SendAsync<T>(string path, SendOptions? options = null, CancellationToken cancellationToken = default)
    {
        var data = await SendAsync(path, options, cancellationToken);
        if (data is T typed)
        {
            return typed;
        }

        if (data is JsonElement element)
        {
            var raw = element.GetRawText();
            return JsonSerializer.Deserialize<T>(raw, Serialization.DefaultJsonOptions)!;
        }

        var json = Serialization.ToJson(data);
        return JsonSerializer.Deserialize<T>(json, Serialization.DefaultJsonOptions)!;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            Realtime.Disconnect();
            PubSub.Disconnect();
        }
        catch
        {
            // best effort
        }
    }
}
