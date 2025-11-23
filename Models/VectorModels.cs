namespace Bosbase.Models;

public record VectorDocument(
    List<double> Vector,
    string? Id = null,
    Dictionary<string, object?>? Metadata = null,
    string? Content = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?> { ["vector"] = Vector };
        if (!string.IsNullOrWhiteSpace(Id)) payload["id"] = Id;
        if (Metadata != null) payload["metadata"] = Metadata;
        if (Content != null) payload["content"] = Content;
        return payload;
    }

    public static VectorDocument FromDictionary(IDictionary<string, object?> data)
    {
        var vector = new List<double>();
        if (data.TryGetValue("vector", out var vecObj) && vecObj is IEnumerable<object?> vecList)
        {
            foreach (var item in vecList)
            {
                if (item == null) continue;
                if (double.TryParse(item.ToString(), out var d))
                {
                    vector.Add(d);
                }
            }
        }

        return new VectorDocument(
            vector,
            Id: data.TryGetValue("id", out var id) ? id?.ToString() : null,
            Metadata: data.TryGetValue("metadata", out var meta) && meta is Dictionary<string, object?> md ? md : null,
            Content: data.TryGetValue("content", out var content) ? content?.ToString() : null);
    }
}

public record VectorSearchOptions(
    List<double> QueryVector,
    int? Limit = null,
    Dictionary<string, object?>? Filter = null,
    double? MinScore = null,
    double? MaxDistance = null,
    bool? IncludeDistance = null,
    bool? IncludeContent = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>
        {
            ["queryVector"] = QueryVector
        };
        if (Limit.HasValue) payload["limit"] = Limit.Value;
        if (Filter != null) payload["filter"] = Filter;
        if (MinScore.HasValue) payload["minScore"] = MinScore.Value;
        if (MaxDistance.HasValue) payload["maxDistance"] = MaxDistance.Value;
        if (IncludeDistance.HasValue) payload["includeDistance"] = IncludeDistance.Value;
        if (IncludeContent.HasValue) payload["includeContent"] = IncludeContent.Value;
        return payload;
    }
}

public record VectorSearchResult(VectorDocument Document, double Score, double? Distance = null);

public record VectorSearchResponse(
    List<VectorSearchResult> Results,
    int? TotalMatches = null,
    int? QueryTime = null);

public record VectorBatchInsertOptions(
    List<VectorDocument> Documents,
    bool? SkipDuplicates = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>
        {
            ["documents"] = Documents.Select(d => d.ToDictionary()).ToList()
        };
        if (SkipDuplicates.HasValue) payload["skipDuplicates"] = SkipDuplicates.Value;
        return payload;
    }
}

public record VectorInsertResponse(string Id, bool Success);

public record VectorBatchInsertResponse(
    int InsertedCount,
    int FailedCount,
    List<string> Ids,
    List<string>? Errors = null);

public record VectorCollectionConfig(int? Dimension = null, string? Distance = null, Dictionary<string, object?>? Options = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>();
        if (Dimension.HasValue) payload["dimension"] = Dimension.Value;
        if (Distance != null) payload["distance"] = Distance;
        if (Options != null) payload["options"] = Options;
        return payload;
    }
}

public record VectorCollectionInfo(string Name, int? Count = null, int? Dimension = null);
