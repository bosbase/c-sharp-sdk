using System.Collections;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bosbase.Models;

namespace Bosbase.Utils;

public static class HttpHelpers
{
    public static string EncodePathSegment(object value)
    {
        return Uri.EscapeDataString(Convert.ToString(value) ?? string.Empty);
    }

    public static IDictionary<string, List<string>> NormalizeQuery(IDictionary<string, object?>? query)
    {
        var normalized = new Dictionary<string, List<string>>();
        if (query == null)
        {
            return normalized;
        }

        foreach (var kvp in query)
        {
            if (kvp.Value == null) continue;
            var key = kvp.Key ?? string.Empty;

            IEnumerable<object?> values;
            if (kvp.Value is string)
            {
                values = new[] { kvp.Value };
            }
            else if (kvp.Value is IEnumerable enumerable)
            {
                var items = new List<object?>();
                foreach (var item in enumerable)
                {
                    items.Add(item);
                }
                values = items;
            }
            else
            {
                values = new[] { kvp.Value };
            }

            foreach (var val in values)
            {
                if (val == null) continue;
                if (!normalized.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    normalized[key] = list;
                }
                list.Add(Convert.ToString(val) ?? string.Empty);
            }
        }

        return normalized;
    }

    public static string BuildQuery(IDictionary<string, object?>? query)
    {
        var normalized = NormalizeQuery(query);
        var builder = new StringBuilder();
        foreach (var kvp in normalized)
        {
            foreach (var val in kvp.Value)
            {
                if (builder.Length > 0) builder.Append('&');
                builder.Append(Uri.EscapeDataString(kvp.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(val));
            }
        }
        return builder.ToString();
    }

    public static string BuildRelativeUrl(string path, IDictionary<string, object?>? query)
    {
        var rel = path.StartsWith("/") ? path : "/" + path;
        var queryString = BuildQuery(query);
        if (string.IsNullOrEmpty(queryString))
        {
            return rel;
        }
        return $"{rel}?{queryString}";
    }

    public static HttpContent? BuildContent(
        object? body,
        IEnumerable<FileAttachment>? files,
        JsonSerializerOptions? jsonOptions = null)
    {
        var attachments = files?.ToList() ?? new List<FileAttachment>();
        if (attachments.Count == 0)
        {
            if (body is HttpContent httpContent)
            {
                return httpContent;
            }

            if (body == null)
            {
                return null;
            }

            var json = JsonSerializer.Serialize(Serialization.ToSerializable(body), jsonOptions ?? Serialization.DefaultJsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        var multipart = new MultipartFormDataContent();
        var payloadJson = JsonSerializer.Serialize(Serialization.ToSerializable(body) ?? new object(), jsonOptions ?? Serialization.DefaultJsonOptions);
        multipart.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "@jsonPayload");

        foreach (var attachment in attachments)
        {
            var streamContent = new StreamContent(attachment.Content);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
            multipart.Add(streamContent, attachment.FieldName, attachment.FileName);
        }

        return multipart;
    }
}
