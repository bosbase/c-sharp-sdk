using System.Net;

namespace Bosbase.Exceptions;

/// <summary>
/// Normalized error raised for failed API requests.
/// </summary>
public class ClientResponseError : Exception
{
    public string? Url { get; }
    public int Status { get; }
    public IDictionary<string, object?> Response { get; }
    public bool IsAbort { get; }
    public Exception? OriginalError { get; }

    public ClientResponseError(
        string? url = null,
        int status = 0,
        IDictionary<string, object?>? response = null,
        bool isAbort = false,
        Exception? originalError = null,
        string? message = null) : base(message ?? BuildMessage(status, response))
    {
        Url = url;
        Status = status;
        Response = response ?? new Dictionary<string, object?>();
        IsAbort = isAbort;
        OriginalError = originalError;
    }

    public ClientResponseError(HttpRequestException httpException, string? url = null)
        : this(
            url: url,
            status: (int?)(httpException.StatusCode ?? HttpStatusCode.InternalServerError) ?? 0,
            response: new Dictionary<string, object?>(),
            isAbort: false,
            originalError: httpException,
            message: httpException.Message)
    {
    }

    private static string BuildMessage(int status, IDictionary<string, object?>? response)
    {
        if (status == 0)
        {
            return "Network request failed.";
        }

        var details = response != null && response.TryGetValue("message", out var msg) && msg is string str
            ? str
            : "Request failed.";
        return $"{status}: {details}";
    }
}
