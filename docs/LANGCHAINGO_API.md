# LangChaingo API - C# SDK Documentation

BosBase exposes the `/api/langchaingo` endpoints so you can run LangChainGo powered workflows without leaving the platform. The C# SDK wraps these endpoints with the `client.LangChaingo` service.

The service exposes four high-level methods:

| Method | HTTP Endpoint | Description |
| --- | --- | --- |
| `client.LangChaingo.CompletionsAsync()` | `POST /api/langchaingo/completions` | Runs a chat/completion call using the configured LLM provider. |
| `client.LangChaingo.RagAsync()` | `POST /api/langchaingo/rag` | Runs a retrieval-augmented generation pass over an `llmDocuments` collection. |
| `client.LangChaingo.QueryDocumentsAsync()` | `POST /api/langchaingo/documents/query` | Asks an OpenAI-backed chain to answer questions over `llmDocuments` and optionally return matched sources. |
| `client.LangChaingo.SqlAsync()` | `POST /api/langchaingo/sql` | Lets OpenAI draft and execute SQL against your BosBase database, then returns the results. |

Each method accepts an optional `model` block:

```csharp
var modelConfig = new Dictionary<string, object?>
{
    ["provider"] = "openai", // or "ollama" or other
    ["model"] = "gpt-4o-mini",
    ["apiKey"] = "your-api-key", // optional, overrides server defaults
    ["baseUrl"] = "https://api.openai.com/v1" // optional
};
```

If you omit the `model` section, BosBase defaults to `provider: "openai"` and `model: "gpt-4o-mini"` with credentials read from the server environment. Passing an `apiKey` lets you override server defaults on a per-request basis.

## Text + Chat Completions

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");

var completion = await client.LangChaingo.CompletionsAsync(new Dictionary<string, object?>
{
    ["model"] = new Dictionary<string, object?>
    {
        ["provider"] = "openai",
        ["model"] = "gpt-4o-mini"
    },
    ["messages"] = new[]
    {
        new Dictionary<string, object?> { ["role"] = "system", ["content"] = "Answer in one sentence." },
        new Dictionary<string, object?> { ["role"] = "user", ["content"] = "Explain Rayleigh scattering." }
    },
    ["temperature"] = 0.2
});

var content = completion["content"]?.ToString();
Console.WriteLine(content);
```

The completion response mirrors the LangChainGo `ContentResponse` shape, so you can inspect the `functionCall`, `toolCalls`, or `generationInfo` fields when you need more than plain text.

## Retrieval-Augmented Generation (RAG)

Pair the LangChaingo endpoints with the `/api/llm-documents` store to build RAG workflows. The backend automatically uses the chromem-go collection configured for the target LLM collection.

```csharp
var answer = await client.LangChaingo.RagAsync(new Dictionary<string, object?>
{
    ["collection"] = "knowledge-base",
    ["question"] = "Why is the sky blue?",
    ["topK"] = 4,
    ["returnSources"] = true,
    ["filters"] = new Dictionary<string, object?>
    {
        ["where"] = new Dictionary<string, object?> { ["topic"] = "physics" }
    }
});

Console.WriteLine(answer["answer"]);

if (answer["sources"] is List<object?> sources)
{
    foreach (var source in sources)
    {
        if (source is Dictionary<string, object?> sourceDict)
        {
            var score = sourceDict["score"];
            var metadata = sourceDict["metadata"] as Dictionary<string, object?>;
            Console.WriteLine($"{score} {metadata?["title"]}");
        }
    }
}
```

Set `promptTemplate` when you want to control how the retrieved context is stuffed into the answer prompt:

```csharp
await client.LangChaingo.RagAsync(new Dictionary<string, object?>
{
    ["collection"] = "knowledge-base",
    ["question"] = "Summarize the explanation below in 2 sentences.",
    ["promptTemplate"] = "Context:\n{{.context}}\n\nQuestion: {{.question}}\nSummary:"
});
```

## LLM Document Queries

> **Note**: This interface is only available to superusers.

When you want to pose a question to a specific `llmDocuments` collection and have LangChaingo+OpenAI synthesize an answer, use `QueryDocumentsAsync`. It mirrors the RAG arguments but takes a `query` field:

```csharp
var response = await client.LangChaingo.QueryDocumentsAsync(new Dictionary<string, object?>
{
    ["collection"] = "knowledge-base",
    ["query"] = "List three bullet points about Rayleigh scattering.",
    ["topK"] = 3,
    ["returnSources"] = true
});

Console.WriteLine(response["answer"]);
Console.WriteLine(response["sources"]);
```

## SQL Generation + Execution

> **Important Notes**:
> - This interface is only available to superusers. Requests authenticated with regular `users` tokens return a `401 Unauthorized`.
> - SQL execution is read-only. The backend will reject `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, or other mutation statements.
> - Always validate and review generated SQL before execution in production.

```csharp
// Authenticate as superuser first
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

var result = await client.LangChaingo.SqlAsync(new Dictionary<string, object?>
{
    ["query"] = "How many users registered in the last 30 days?",
    ["model"] = new Dictionary<string, object?>
    {
        ["provider"] = "openai",
        ["model"] = "gpt-4o-mini"
    }
});

Console.WriteLine(result["sql"]); // Generated SQL
Console.WriteLine(result["result"]); // Query results
```

## Error Handling

```csharp
try
{
    var completion = await client.LangChaingo.CompletionsAsync(request);
}
catch (ClientResponseError ex)
{
    if (ex.Status == 401)
    {
        Console.Error.WriteLine("Authentication required (superuser for SQL/queryDocuments)");
    }
    else if (ex.Status == 400)
    {
        Console.Error.WriteLine($"Invalid request: {ex.Message}");
    }
    else
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}
```

## Related Documentation

- [LLM Documents API](./LLM_DOCUMENTS.md) - LLM document management
- [Vector API](./VECTOR_API.md) - Vector search operations
- [Authentication](./AUTHENTICATION.md) - User authentication

