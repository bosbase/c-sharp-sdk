# LLM Document API - C# SDK Documentation

The `LlmDocumentService` wraps the `/api/llm-documents` endpoints that are backed by the embedded chromem-go vector store (persisted in rqlite). Each document contains text content, optional metadata and an embedding vector that can be queried with semantic search.

## Getting Started

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");

// create a logical namespace for your documents
await client.LlmDocuments.CreateCollectionAsync("knowledge-base", new Dictionary<string, object?>
{
    ["domain"] = "internal"
});
```

## Insert Documents

```csharp
var doc = await client.LlmDocuments.InsertAsync(
    new Dictionary<string, object?>
    {
        ["content"] = "Leaves are green because chlorophyll absorbs red and blue light.",
        ["metadata"] = new Dictionary<string, object?> { ["topic"] = "biology" }
    },
    collection: "knowledge-base"
);

await client.LlmDocuments.InsertAsync(
    new Dictionary<string, object?>
    {
        ["id"] = "sky",
        ["content"] = "The sky is blue because of Rayleigh scattering.",
        ["metadata"] = new Dictionary<string, object?> { ["topic"] = "physics" }
    },
    collection: "knowledge-base"
);
```

## Query Documents

```csharp
var result = await client.LlmDocuments.QueryAsync(
    new Dictionary<string, object?>
    {
        ["queryText"] = "Why is the sky blue?",
        ["limit"] = 3,
        ["where"] = new Dictionary<string, object?> { ["topic"] = "physics" }
    },
    collection: "knowledge-base"
);

if (result["results"] is List<object?> results)
{
    foreach (var match in results)
    {
        if (match is Dictionary<string, object?> matchDict)
        {
            Console.WriteLine($"{matchDict["id"]} {matchDict["similarity"]}");
        }
    }
}
```

## Manage Documents

```csharp
// update a document
await client.LlmDocuments.UpdateAsync(
    "sky",
    new Dictionary<string, object?>
    {
        ["metadata"] = new Dictionary<string, object?>
        {
            ["topic"] = "physics",
            ["reviewed"] = "true"
        }
    },
    collection: "knowledge-base"
);

// list documents with pagination
var page = await client.LlmDocuments.ListAsync(new Dictionary<string, object?>
{
    ["collection"] = "knowledge-base",
    ["page"] = 1,
    ["perPage"] = 25
});

// delete unwanted entries
await client.LlmDocuments.DeleteAsync("sky", collection: "knowledge-base");
```

## Complete Example

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Create collection
await client.LlmDocuments.CreateCollectionAsync("knowledge-base", new Dictionary<string, object?>
{
    ["domain"] = "internal"
});

// Insert multiple documents
var documents = new[]
{
    new Dictionary<string, object?>
    {
        ["id"] = "doc1",
        ["content"] = "The sky is blue because of Rayleigh scattering.",
        ["metadata"] = new Dictionary<string, object?> { ["topic"] = "physics" }
    },
    new Dictionary<string, object?>
    {
        ["id"] = "doc2",
        ["content"] = "Leaves are green because chlorophyll absorbs red and blue light.",
        ["metadata"] = new Dictionary<string, object?> { ["topic"] = "biology" }
    }
};

foreach (var doc in documents)
{
    await client.LlmDocuments.InsertAsync(doc, collection: "knowledge-base");
}

// Query documents
var queryResult = await client.LlmDocuments.QueryAsync(
    new Dictionary<string, object?>
    {
        ["queryText"] = "Why is the sky blue?",
        ["limit"] = 5,
        ["where"] = new Dictionary<string, object?> { ["topic"] = "physics" }
    },
    collection: "knowledge-base"
);

if (queryResult["results"] is List<object?> results)
{
    foreach (var match in results)
    {
        if (match is Dictionary<string, object?> matchDict)
        {
            Console.WriteLine($"ID: {matchDict["id"]}");
            Console.WriteLine($"Similarity: {matchDict["similarity"]}");
            Console.WriteLine($"Content: {matchDict["content"]}");
        }
    }
}
```

## HTTP Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/api/llm-documents/collections/{name}` | Create a collection |
| `GET` | `/api/llm-documents/collections` | List collections |
| `DELETE` | `/api/llm-documents/collections/{name}` | Delete a collection |
| `POST` | `/api/llm-documents/collections/{name}/documents` | Insert a document |
| `GET` | `/api/llm-documents/collections/{name}/documents` | List documents |
| `PATCH` | `/api/llm-documents/collections/{name}/documents/{id}` | Update a document |
| `DELETE` | `/api/llm-documents/collections/{name}/documents/{id}` | Delete a document |
| `POST` | `/api/llm-documents/collections/{name}/query` | Query documents |

## Related Documentation

- [LangChaingo API](./LANGCHAINGO_API.md) - LangChainGo integration
- [Vector API](./VECTOR_API.md) - Vector search operations

