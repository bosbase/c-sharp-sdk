namespace Bosbase.Models;

public record LlmDocument(
    string Content,
    string? Id = null,
    Dictionary<string, string>? Metadata = null,
    List<double>? Embedding = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>
        {
            ["content"] = Content
        };
        if (!string.IsNullOrWhiteSpace(Id)) payload["id"] = Id;
        if (Metadata != null) payload["metadata"] = Metadata;
        if (Embedding != null) payload["embedding"] = Embedding;
        return payload;
    }

    public static LlmDocument FromDictionary(IDictionary<string, object?> data)
    {
        var embedding = new List<double>();
        if (data.TryGetValue("embedding", out var emb) && emb is IEnumerable<object?> list)
        {
            foreach (var val in list)
            {
                if (double.TryParse(val?.ToString(), out var d))
                {
                    embedding.Add(d);
                }
            }
        }

        Dictionary<string, string>? metadata = null;
        if (data.TryGetValue("metadata", out var metaObj) && metaObj is IDictionary<string, object?> metaDict)
        {
            metadata = metaDict.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty);
        }

        return new LlmDocument(
            Id: data.TryGetValue("id", out var id) ? id?.ToString() ?? string.Empty : string.Empty,
            Content: data.TryGetValue("content", out var content) ? content?.ToString() ?? string.Empty : string.Empty,
            Metadata: metadata,
            Embedding: embedding.Any() ? embedding : null);
    }
}

public record LlmDocumentUpdate(
    string? Content = null,
    Dictionary<string, string>? Metadata = null,
    List<double>? Embedding = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>();
        if (Content != null) payload["content"] = Content;
        if (Metadata != null) payload["metadata"] = Metadata;
        if (Embedding != null) payload["embedding"] = Embedding;
        return payload;
    }
}

public record LlmQueryOptions(
    string? QueryText = null,
    List<double>? QueryEmbedding = null,
    int? Limit = null,
    Dictionary<string, string>? Where = null,
    Dictionary<string, object?>? Negative = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>();
        if (QueryText != null) payload["queryText"] = QueryText;
        if (QueryEmbedding != null) payload["queryEmbedding"] = QueryEmbedding;
        if (Limit.HasValue) payload["limit"] = Limit.Value;
        if (Where != null) payload["where"] = Where;
        if (Negative != null) payload["negative"] = Negative;
        return payload;
    }
}

public record LlmQueryResult(string Id, string Content, Dictionary<string, string> Metadata, double Similarity)
{
    public static LlmQueryResult FromDictionary(IDictionary<string, object?> data)
    {
        var metadata = new Dictionary<string, string>();
        if (data.TryGetValue("metadata", out var metaObj) && metaObj is IDictionary<string, object?> metaDict)
        {
            foreach (var kvp in metaDict)
            {
                metadata[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        var similarity = 0d;
        if (data.TryGetValue("similarity", out var simObj))
        {
            double.TryParse(simObj?.ToString(), out similarity);
        }

        return new LlmQueryResult(
            Id: data.TryGetValue("id", out var id) ? id?.ToString() ?? string.Empty : string.Empty,
            Content: data.TryGetValue("content", out var content) ? content?.ToString() ?? string.Empty : string.Empty,
            Metadata: metadata,
            Similarity: similarity);
    }
}
