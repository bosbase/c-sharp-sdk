namespace Bosbase.Models;

public record LangChaingoModelConfig(
    string? Provider = null,
    string? Model = null,
    string? ApiKey = null,
    string? BaseUrl = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>();
        if (Provider != null) payload["provider"] = Provider;
        if (Model != null) payload["model"] = Model;
        if (ApiKey != null) payload["apiKey"] = ApiKey;
        if (BaseUrl != null) payload["baseUrl"] = BaseUrl;
        return payload;
    }
}

public record LangChaingoCompletionMessage(string Content, string? Role = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?> { ["content"] = Content };
        if (Role != null) payload["role"] = Role;
        return payload;
    }
}

public record LangChaingoCompletionRequest(
    LangChaingoModelConfig? Model = null,
    string? Prompt = null,
    List<LangChaingoCompletionMessage>? Messages = null,
    double? Temperature = null,
    int? MaxTokens = null,
    double? TopP = null,
    int? CandidateCount = null,
    List<string>? Stop = null,
    bool? JsonResponse = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>();
        if (Model != null) payload["model"] = Model.ToDictionary();
        if (Prompt != null) payload["prompt"] = Prompt;
        if (Messages != null) payload["messages"] = Messages.Select(m => m.ToDictionary()).ToList();
        if (Temperature.HasValue) payload["temperature"] = Temperature.Value;
        if (MaxTokens.HasValue) payload["maxTokens"] = MaxTokens.Value;
        if (TopP.HasValue) payload["topP"] = TopP.Value;
        if (CandidateCount.HasValue) payload["candidateCount"] = CandidateCount.Value;
        if (Stop != null) payload["stop"] = Stop;
        if (JsonResponse.HasValue) payload["json"] = JsonResponse.Value;
        return payload;
    }
}

public record LangChaingoFunctionCall(string Name, string Arguments)
{
    public static LangChaingoFunctionCall FromDictionary(IDictionary<string, object?> data)
    {
        return new LangChaingoFunctionCall(
            Name: data.TryGetValue("name", out var name) ? name?.ToString() ?? string.Empty : string.Empty,
            Arguments: data.TryGetValue("arguments", out var args) ? args?.ToString() ?? string.Empty : string.Empty);
    }
}

public record LangChaingoToolCall(string Id, string Type, LangChaingoFunctionCall? FunctionCall = null)
{
    public static LangChaingoToolCall FromDictionary(IDictionary<string, object?> data)
    {
        LangChaingoFunctionCall? func = null;
        if (data.TryGetValue("functionCall", out var funcObj) && funcObj is IDictionary<string, object?> funcDict)
        {
            func = LangChaingoFunctionCall.FromDictionary(funcDict);
        }

        return new LangChaingoToolCall(
            Id: data.TryGetValue("id", out var id) ? id?.ToString() ?? string.Empty : string.Empty,
            Type: data.TryGetValue("type", out var type) ? type?.ToString() ?? string.Empty : string.Empty,
            FunctionCall: func);
    }
}

public record LangChaingoCompletionResponse(
    string Content,
    string? StopReason = null,
    Dictionary<string, object?>? GenerationInfo = null,
    LangChaingoFunctionCall? FunctionCall = null,
    List<LangChaingoToolCall>? ToolCalls = null)
{
    public static LangChaingoCompletionResponse FromDictionary(IDictionary<string, object?> data)
    {
        LangChaingoFunctionCall? func = null;
        if (data.TryGetValue("functionCall", out var funcObj) && funcObj is IDictionary<string, object?> funcDict)
        {
            func = LangChaingoFunctionCall.FromDictionary(funcDict);
        }

        List<LangChaingoToolCall>? toolCalls = null;
        if (data.TryGetValue("toolCalls", out var toolsObj) && toolsObj is IEnumerable<object?> toolsList)
        {
            toolCalls = toolsList
                .Select(t => t as IDictionary<string, object?>)
                .Where(d => d != null)
                .Select(d => LangChaingoToolCall.FromDictionary(d!))
                .ToList();
        }

        return new LangChaingoCompletionResponse(
            Content: data.TryGetValue("content", out var content) ? content?.ToString() ?? string.Empty : string.Empty,
            StopReason: data.TryGetValue("stopReason", out var stop) ? stop?.ToString() : null,
            GenerationInfo: data.TryGetValue("generationInfo", out var info) && info is Dictionary<string, object?> dict ? dict : null,
            FunctionCall: func,
            ToolCalls: toolCalls);
    }
}

public record LangChaingoRagFilters(
    Dictionary<string, string>? Where = null,
    Dictionary<string, string>? WhereDocument = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>();
        if (Where != null) payload["where"] = Where;
        if (WhereDocument != null) payload["whereDocument"] = WhereDocument;
        return payload;
    }
}

public record LangChaingoRagRequest(
    string Collection,
    string Question,
    LangChaingoModelConfig? Model = null,
    int? TopK = null,
    double? ScoreThreshold = null,
    LangChaingoRagFilters? Filters = null,
    string? PromptTemplate = null,
    bool? ReturnSources = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>
        {
            ["collection"] = Collection,
            ["question"] = Question,
        };
        if (Model != null) payload["model"] = Model.ToDictionary();
        if (TopK.HasValue) payload["topK"] = TopK.Value;
        if (ScoreThreshold.HasValue) payload["scoreThreshold"] = ScoreThreshold.Value;
        if (Filters != null) payload["filters"] = Filters.ToDictionary();
        if (PromptTemplate != null) payload["promptTemplate"] = PromptTemplate;
        if (ReturnSources.HasValue) payload["returnSources"] = ReturnSources.Value;
        return payload;
    }
}

public record LangChaingoSourceDocument(
    string Content,
    Dictionary<string, object?>? Metadata = null,
    double? Score = null)
{
    public static LangChaingoSourceDocument FromDictionary(IDictionary<string, object?> data)
    {
        double? score = null;
        if (data.TryGetValue("score", out var scoreObj) && double.TryParse(scoreObj?.ToString(), out var s))
        {
            score = s;
        }

        Dictionary<string, object?>? meta = null;
        if (data.TryGetValue("metadata", out var metaObj) && metaObj is Dictionary<string, object?> md)
        {
            meta = md;
        }

        return new LangChaingoSourceDocument(
            Content: data.TryGetValue("content", out var content) ? content?.ToString() ?? string.Empty : string.Empty,
            Metadata: meta,
            Score: score);
    }
}

public record LangChaingoRagResponse(string Answer, List<LangChaingoSourceDocument>? Sources = null)
{
    public static LangChaingoRagResponse FromDictionary(IDictionary<string, object?> data)
    {
        List<LangChaingoSourceDocument>? sources = null;
        if (data.TryGetValue("sources", out var sourcesObj) && sourcesObj is IEnumerable<object?> list)
        {
            sources = list
                .Select(item => item as IDictionary<string, object?>)
                .Where(d => d != null)
                .Select(d => LangChaingoSourceDocument.FromDictionary(d!))
                .ToList();
        }

        return new LangChaingoRagResponse(
            Answer: data.TryGetValue("answer", out var answer) ? answer?.ToString() ?? string.Empty : string.Empty,
            Sources: sources);
    }
}

public record LangChaingoDocumentQueryRequest(
    string Collection,
    string Query,
    LangChaingoModelConfig? Model = null,
    int? TopK = null,
    double? ScoreThreshold = null,
    LangChaingoRagFilters? Filters = null,
    string? PromptTemplate = null,
    bool? ReturnSources = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>
        {
            ["collection"] = Collection,
            ["query"] = Query,
        };
        if (Model != null) payload["model"] = Model.ToDictionary();
        if (TopK.HasValue) payload["topK"] = TopK.Value;
        if (ScoreThreshold.HasValue) payload["scoreThreshold"] = ScoreThreshold.Value;
        if (Filters != null) payload["filters"] = Filters.ToDictionary();
        if (PromptTemplate != null) payload["promptTemplate"] = PromptTemplate;
        if (ReturnSources.HasValue) payload["returnSources"] = ReturnSources.Value;
        return payload;
    }
}

public record LangChaingoSqlRequest(
    string Query,
    LangChaingoModelConfig? Model = null,
    List<string>? Tables = null,
    int? TopK = null)
{
    public Dictionary<string, object?> ToDictionary()
    {
        var payload = new Dictionary<string, object?>
        {
            ["query"] = Query
        };
        if (Model != null) payload["model"] = Model.ToDictionary();
        if (Tables != null) payload["tables"] = Tables;
        if (TopK.HasValue) payload["topK"] = TopK.Value;
        return payload;
    }
}

public record LangChaingoSqlResponse(
    string Sql,
    string Answer,
    List<string>? Columns = null,
    List<List<string>>? Rows = null,
    string? RawResult = null)
{
    public static LangChaingoSqlResponse FromDictionary(IDictionary<string, object?> data)
    {
        List<List<string>>? rows = null;
        if (data.TryGetValue("rows", out var rowsObj) && rowsObj is IEnumerable<object?> rawRows)
        {
            rows = rawRows
                .Select(r => r as IEnumerable<object?>)
                .Where(r => r != null)
                .Select(r => r!.Select(cell => cell?.ToString() ?? string.Empty).ToList())
                .ToList();
        }

        List<string>? columns = null;
        if (data.TryGetValue("columns", out var colsObj) && colsObj is IEnumerable<object?> cols)
        {
            columns = cols.Select(c => c?.ToString() ?? string.Empty).ToList();
        }

        return new LangChaingoSqlResponse(
            Sql: data.TryGetValue("sql", out var sql) ? sql?.ToString() ?? string.Empty : string.Empty,
            Answer: data.TryGetValue("answer", out var answer) ? answer?.ToString() ?? string.Empty : string.Empty,
            Columns: columns,
            Rows: rows,
            RawResult: data.TryGetValue("rawResult", out var raw) ? raw?.ToString() : null);
    }
}
