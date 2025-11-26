# Vector Database API - C# SDK Documentation

Vector database operations for semantic search, RAG (Retrieval-Augmented Generation), and AI applications.

> **Note**: Vector operations are currently implemented using sqlite-vec but are designed with abstraction in mind to support future vector database providers.

## Overview

The Vector API provides a unified interface for working with vector embeddings, enabling you to:
- Store and search vector embeddings
- Perform similarity search
- Build RAG applications
- Create recommendation systems
- Enable semantic search capabilities

## Getting Started

```csharp
using Bosbase;
using Bosbase.Models;

var client = new BosbaseClient("http://localhost:8090");

// Authenticate as superuser (vectors require superuser auth)
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
```

## Types

### VectorDocument

A vector document with embedding, metadata, and optional content.

```csharp
var document = new VectorDocument(
    Vector: new List<double> { 0.1, 0.2, 0.3, 0.4 },
    Id: "doc_001",
    Metadata: new Dictionary<string, object?> { ["category"] = "tech" },
    Content: "Document about machine learning"
);
```

### VectorSearchOptions

Options for vector similarity search.

```csharp
var searchOptions = new VectorSearchOptions(
    QueryVector: new List<double> { 0.1, 0.2, 0.3, 0.4 },
    Limit: 10,
    Filter: new Dictionary<string, object?> { ["category"] = "tech" },
    MinScore: 0.7,
    MaxDistance: 0.3,
    IncludeDistance: true,
    IncludeContent: true
);
```

## Collection Management

### Create Collection

Create a new vector collection with specified dimension and distance metric.

```csharp
// With custom configuration
var config = new VectorCollectionConfig(
    Dimension: 384,
    Distance: "cosine"
);
await client.Vectors.CreateCollectionAsync("documents", config);

// Minimal example (uses defaults)
await client.Vectors.CreateCollectionAsync("documents", new VectorCollectionConfig());
```

**Parameters:**
- `name` (string): Collection name
- `config` (VectorCollectionConfig, optional):
  - `Dimension` (int, optional): Vector dimension. Default: 384
  - `Distance` (string, optional): Distance metric. Default: 'cosine'
  - Options: 'cosine', 'l2', 'dot'

### List Collections

Get all available vector collections.

```csharp
var collections = await client.Vectors.ListCollectionsAsync();

foreach (var collection in collections)
{
    Console.WriteLine($"{collection.Name}: {collection.Count} vectors");
}
```

**Response:**
```csharp
List<VectorCollectionInfo> // Each with Name, Count?, Dimension?
```

### Update Collection

Update a vector collection configuration (distance metric and options).
Note: Collection name and dimension cannot be changed after creation.

```csharp
// Change distance metric
var config = new VectorCollectionConfig(Distance: "l2");
await client.Vectors.UpdateCollectionAsync("documents", config);

// Update with options
var configWithOptions = new VectorCollectionConfig(
    Distance: "inner_product",
    Options: new Dictionary<string, object?> { ["customOption"] = "value" }
);
await client.Vectors.UpdateCollectionAsync("documents", configWithOptions);
```

### Delete Collection

Delete a vector collection and all its data.

```csharp
await client.Vectors.DeleteCollectionAsync("documents");
```

**⚠️ Warning**: This permanently deletes the collection and all vectors in it!

## Document Operations

### Insert Document

Insert a single vector document.

```csharp
// With custom ID
var document = new VectorDocument(
    Vector: new List<double> { 0.1, 0.2, 0.3, 0.4 },
    Id: "doc_001",
    Metadata: new Dictionary<string, object?> 
    { 
        ["category"] = "tech", 
        ["tags"] = new[] { "AI", "ML" } 
    },
    Content: "Document about machine learning"
);

var result = await client.Vectors.InsertAsync(document, collection: "documents");
Console.WriteLine($"Inserted: {result.Id}");

// Without ID (auto-generated)
var document2 = new VectorDocument(
    Vector: new List<double> { 0.5, 0.6, 0.7, 0.8 },
    Content: "Another document"
);

var result2 = await client.Vectors.InsertAsync(document2, collection: "documents");
```

### Batch Insert

Insert multiple vector documents efficiently.

```csharp
var documents = new List<VectorDocument>
{
    new VectorDocument(
        Vector: new List<double> { 0.1, 0.2, 0.3 },
        Metadata: new Dictionary<string, object?> { ["cat"] = "A" },
        Content: "Doc A"
    ),
    new VectorDocument(
        Vector: new List<double> { 0.4, 0.5, 0.6 },
        Metadata: new Dictionary<string, object?> { ["cat"] = "B" },
        Content: "Doc B"
    ),
    new VectorDocument(
        Vector: new List<double> { 0.7, 0.8, 0.9 },
        Metadata: new Dictionary<string, object?> { ["cat"] = "A" },
        Content: "Doc C"
    )
};

var batchOptions = new VectorBatchInsertOptions(
    Documents: documents,
    SkipDuplicates: true
);

var result = await client.Vectors.BatchInsertAsync(batchOptions, collection: "documents");
Console.WriteLine($"Inserted: {result.InsertedCount}");
Console.WriteLine($"Failed: {result.FailedCount}");
Console.WriteLine($"IDs: {string.Join(", ", result.Ids)}");
```

### Get Document

Retrieve a vector document by ID.

```csharp
var doc = await client.Vectors.GetAsync("doc_001", collection: "documents");
Console.WriteLine($"Vector: [{string.Join(", ", doc.Vector)}]");
Console.WriteLine($"Content: {doc.Content}");
Console.WriteLine($"Metadata: {System.Text.Json.JsonSerializer.Serialize(doc.Metadata)}");
```

### Update Document

Update an existing vector document.

```csharp
// Update all fields
var updatedDoc = new VectorDocument(
    Vector: new List<double> { 0.9, 0.8, 0.7, 0.6 },
    Metadata: new Dictionary<string, object?> { ["updated"] = true },
    Content: "Updated content"
);

await client.Vectors.UpdateAsync("doc_001", updatedDoc, collection: "documents");

// Partial update (only metadata and content)
var partialDoc = new VectorDocument(
    Vector: new List<double>(), // Empty vector means don't update it
    Metadata: new Dictionary<string, object?> { ["category"] = "updated" },
    Content: "New content"
);

await client.Vectors.UpdateAsync("doc_001", partialDoc, collection: "documents");
```

### Delete Document

Delete a vector document.

```csharp
await client.Vectors.DeleteAsync("doc_001", collection: "documents");
```

### List Documents

List all documents in a collection with pagination.

```csharp
// Get first page
var result = await client.Vectors.ListAsync(
    collection: "documents",
    page: 1,
    perPage: 100
);

var page = Convert.ToInt32(result["page"]);
var totalPages = Convert.ToInt32(result["totalPages"]);
Console.WriteLine($"Page {page} of {totalPages}");

if (result["items"] is List<object?> items)
{
    foreach (var item in items)
    {
        if (item is Dictionary<string, object?> docDict)
        {
            var doc = VectorDocument.FromDictionary(docDict);
            Console.WriteLine($"{doc.Id}: {doc.Content}");
        }
    }
}
```

## Vector Search

### Basic Search

Perform similarity search on vectors.

```csharp
var searchOptions = new VectorSearchOptions(
    QueryVector: new List<double> { 0.1, 0.2, 0.3, 0.4 },
    Limit: 10
);

var results = await client.Vectors.SearchAsync(searchOptions, collection: "documents");

foreach (var result in results.Results)
{
    Console.WriteLine($"Score: {result.Score} - {result.Document.Content}");
}
```

### Advanced Search

```csharp
var searchOptions = new VectorSearchOptions(
    QueryVector: new List<double> { 0.1, 0.2, 0.3, 0.4 },
    Limit: 20,
    MinScore: 0.7,              // Minimum similarity threshold
    MaxDistance: 0.3,           // Maximum distance threshold
    IncludeDistance: true,      // Include distance metric
    IncludeContent: true,       // Include full content
    Filter: new Dictionary<string, object?> { ["category"] = "tech" } // Filter by metadata
);

var results = await client.Vectors.SearchAsync(searchOptions, collection: "documents");

Console.WriteLine($"Found {results.TotalMatches} matches in {results.QueryTime}ms");
foreach (var r in results.Results)
{
    Console.WriteLine($"Score: {r.Score}, Distance: {r.Distance}");
    Console.WriteLine($"Content: {r.Document.Content}");
}
```

## Common Use Cases

### Semantic Search

```csharp
// 1. Generate embeddings for your documents
var documents = new[]
{
    new { Text = "Introduction to machine learning", Id = "doc1" },
    new { Text = "Deep learning fundamentals", Id = "doc2" },
    new { Text = "Natural language processing", Id = "doc3" }
};

foreach (var doc in documents)
{
    // Generate embedding using your model
    var embedding = await GenerateEmbedding(doc.Text);
    
    var vectorDoc = new VectorDocument(
        Vector: embedding,
        Id: doc.Id,
        Content: doc.Text,
        Metadata: new Dictionary<string, object?> { ["type"] = "tutorial" }
    );
    
    await client.Vectors.InsertAsync(vectorDoc, collection: "articles");
}

// 2. Search
var queryEmbedding = await GenerateEmbedding("What is AI?");
var searchOptions = new VectorSearchOptions(
    QueryVector: queryEmbedding,
    Limit: 5,
    MinScore: 0.75
);

var results = await client.Vectors.SearchAsync(searchOptions, collection: "articles");

foreach (var r in results.Results)
{
    Console.WriteLine($"{r.Score:F2}: {r.Document.Content}");
}
```

### RAG (Retrieval-Augmented Generation)

```csharp
async Task<List<string>> RetrieveContext(string query, int limit = 5)
{
    var queryEmbedding = await GenerateEmbedding(query);
    
    var searchOptions = new VectorSearchOptions(
        QueryVector: queryEmbedding,
        Limit: limit,
        MinScore: 0.75,
        IncludeContent: true
    );
    
    var results = await client.Vectors.SearchAsync(searchOptions, collection: "knowledge_base");
    
    return results.Results.Select(r => r.Document.Content ?? "").ToList();
}

// Use with your LLM
var context = await RetrieveContext("What are best practices for security?");
var answer = await LlmGenerate(context, userQuery);
```

### Recommendation System

```csharp
// Store user profile embeddings
var userProfileDoc = new VectorDocument(
    Vector: userProfileEmbedding,
    Id: userId,
    Metadata: new Dictionary<string, object?>
    {
        ["preferences"] = new[] { "tech", "science" },
        ["demographics"] = new Dictionary<string, object?> 
        { 
            ["age"] = 30, 
            ["location"] = "US" 
        }
    }
);

await client.Vectors.InsertAsync(userProfileDoc, collection: "users");

// Find similar users
var searchOptions = new VectorSearchOptions(
    QueryVector: currentUserEmbedding,
    Limit: 20,
    IncludeDistance: true
);

var similarUsers = await client.Vectors.SearchAsync(searchOptions, collection: "users");

// Generate recommendations based on similar users
var recommendations = await GenerateRecommendations(similarUsers.Results);
```

## Best Practices

### Vector Dimensions

Choose the right dimension for your use case:

- **OpenAI embeddings**: 1536 (`text-embedding-3-large`)
- **Sentence Transformers**: 384-768
  - `all-MiniLM-L6-v2`: 384
  - `all-mpnet-base-v2`: 768
- **Custom models**: Match your model's output

### Distance Metrics

| Metric | Best For | Notes |
|--------|----------|-------|
| `cosine` | Text embeddings | Works well with normalized vectors |
| `l2` | General similarity | Euclidean distance |
| `dot` | Performance | Requires normalized vectors |

### Performance Tips

1. **Use batch insert** for multiple vectors
2. **Set appropriate limits** to avoid excessive results
3. **Use metadata filtering** to narrow search space
4. **Enable indexes** (automatic with sqlite-vec)

### Security

- All vector endpoints require superuser authentication
- Never expose credentials in client-side code
- Use environment variables for sensitive data

## Error Handling

```csharp
try
{
    var searchOptions = new VectorSearchOptions(
        QueryVector: new List<double> { 0.1, 0.2, 0.3 }
    );
    
    var results = await client.Vectors.SearchAsync(searchOptions, collection: "documents");
}
catch (ClientResponseError ex)
{
    if (ex.Status == 404)
    {
        Console.Error.WriteLine("Collection not found");
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

## Complete RAG Application Example

```csharp
using Bosbase;
using Bosbase.Models;

var client = new BosbaseClient("http://localhost:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// 1. Create knowledge base collection
await client.Vectors.CreateCollectionAsync("knowledge_base", new VectorCollectionConfig(
    Dimension: 1536,  // OpenAI dimensions
    Distance: "cosine"
));

// 2. Index documents
async Task IndexDocuments(List<Document> documents)
{
    foreach (var doc in documents)
    {
        // Generate OpenAI embedding
        var embedding = await GenerateOpenAIEmbedding(doc.Content);
        
        var vectorDoc = new VectorDocument(
            Id: doc.Id,
            Vector: embedding,
            Content: doc.Content,
            Metadata: new Dictionary<string, object?>
            {
                ["source"] = doc.Source,
                ["topic"] = doc.Topic
            }
        );
        
        await client.Vectors.InsertAsync(vectorDoc, collection: "knowledge_base");
    }
}

// 3. RAG Query
async Task<string> Ask(string question)
{
    // Generate query embedding
    var embedding = await GenerateOpenAIEmbedding(question);
    
    // Search for relevant context
    var searchOptions = new VectorSearchOptions(
        QueryVector: embedding,
        Limit: 5,
        MinScore: 0.8,
        IncludeContent: true,
        Filter: new Dictionary<string, object?> { ["topic"] = "relevant_topic" }
    );
    
    var results = await client.Vectors.SearchAsync(searchOptions, collection: "knowledge_base");
    
    // Build context
    var context = string.Join("\n\n", results.Results.Select(r => r.Document.Content));
    
    // Generate answer with LLM
    var answer = await GenerateLLMAnswer(context, question);
    
    return answer;
}

// Use it
var answer = await Ask("What is machine learning?");
Console.WriteLine(answer);
```

## Related Documentation

- [Authentication](./AUTHENTICATION.md) - Superuser authentication
- [Collection API](./COLLECTION_API.md) - Collection management

