# GraphQL queries with the C# SDK

Use `client.Graphql.QueryAsync()` to call `/api/graphql` with your current auth token. It returns a dictionary containing `data`, `errors`, and `extensions`.

> Authentication: the GraphQL endpoint is **superuser-only**. Authenticate as a superuser before calling GraphQL, e.g. `await client.Collection("_superusers").AuthWithPasswordAsync(email, password);`.

## Single-table query

```csharp
var query = @"
  query ActiveUsers($limit: Int!) {
    records(collection: ""users"", perPage: $limit, filter: ""status = true"") {
      items { id data }
    }
  }
";

var variables = new Dictionary<string, object?> { ["limit"] = 5 };
var result = await client.Graphql.QueryAsync(query, variables);

var data = result["data"] as Dictionary<string, object?>;
```

## Multi-table join via expands

```csharp
var query = @"
  query PostsWithAuthors {
    records(
      collection: ""posts"",
      expand: [""author"", ""author.profile""],
      sort: ""-created""
    ) {
      items {
        id
        data  // expanded relations live under data.expand
      }
    }
  }
";

var result = await client.Graphql.QueryAsync(query);
var data = result["data"] as Dictionary<string, object?>;
```

## Conditional query with variables

```csharp
var query = @"
  query FilteredOrders($minTotal: Float!, $state: String!) {
    records(
      collection: ""orders"",
      filter: ""total >= $minTotal && status = $state"",
      sort: ""created""
    ) {
      items { id data }
    }
  }
";

var variables = new Dictionary<string, object?>
{
    ["minTotal"] = 100.0,
    ["state"] = "paid"
};

var result = await client.Graphql.QueryAsync(query, variables);
```

Use the `filter`, `sort`, `page`, `perPage`, and `expand` arguments to mirror REST list behavior while keeping query logic in GraphQL.

## Create a record

```csharp
var mutation = @"
  mutation CreatePost($data: JSON!) {
    createRecord(collection: ""posts"", data: $data, expand: [""author""]) {
      id
      data
    }
  }
";

var data = new Dictionary<string, object?>
{
    ["title"] = "Hello",
    ["author"] = "USER_ID"
};

var variables = new Dictionary<string, object?> { ["data"] = data };
var result = await client.Graphql.QueryAsync(mutation, variables);
var resultData = result["data"] as Dictionary<string, object?>;
```

## Update a record

```csharp
var mutation = @"
  mutation UpdatePost($id: ID!, $data: JSON!) {
    updateRecord(collection: ""posts"", id: $id, data: $data) {
      id
      data
    }
  }
";

var variables = new Dictionary<string, object?>
{
    ["id"] = "POST_ID",
    ["data"] = new Dictionary<string, object?> { ["title"] = "Updated title" }
};

await client.Graphql.QueryAsync(mutation, variables);
```

## Delete a record

```csharp
var mutation = @"
  mutation DeletePost($id: ID!) {
    deleteRecord(collection: ""posts"", id: $id)
  }
";

var variables = new Dictionary<string, object?> { ["id"] = "POST_ID" };
await client.Graphql.QueryAsync(mutation, variables);
```

## Error Handling

```csharp
try
{
    var result = await client.Graphql.QueryAsync(query, variables);
    
    if (result.ContainsKey("errors"))
    {
        var errors = result["errors"] as List<object?>;
        foreach (var error in errors ?? new List<object?>())
        {
            Console.Error.WriteLine($"GraphQL error: {error}");
        }
    }
    
    var data = result["data"] as Dictionary<string, object?>;
    // Process data...
}
catch (ClientResponseError ex)
{
    Console.Error.WriteLine($"GraphQL request failed: {ex.Message}");
}
```

## Complete Example

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate as superuser
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Query with variables
var query = @"
  query GetPosts($page: Int!, $perPage: Int!) {
    records(collection: ""posts"", page: $page, perPage: $perPage, sort: ""-created"") {
      pageInfo {
        page
        perPage
        totalItems
        totalPages
      }
      items {
        id
        data
      }
    }
  }
";

var variables = new Dictionary<string, object?>
{
    ["page"] = 1,
    ["perPage"] = 10
};

var result = await client.Graphql.QueryAsync(query, variables);
var data = result["data"] as Dictionary<string, object?>;
var records = data?["records"] as Dictionary<string, object?>;
var items = records?["items"] as List<object?>;

foreach (var item in items ?? new List<object?>())
{
    if (item is Dictionary<string, object?> record)
    {
        Console.WriteLine($"ID: {record["id"]}");
        var recordData = record["data"] as Dictionary<string, object?>;
        Console.WriteLine($"Title: {recordData?["title"]}");
    }
}
```

## Related Documentation

- [API Records](./API_RECORDS.md) - REST API for records
- [Authentication](./AUTHENTICATION.md) - User authentication

