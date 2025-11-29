# SQL API - C# SDK

Run superuser-only SQL against the BosBase backend from C#. This mirrors the JavaScript SDK `SQLService`.

> **Auth:** requires a `_superusers` token. The backend rejects unauthenticated or non-superuser requests.

## Execute SQL

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

var result = await client.Sql.ExecuteAsync("SELECT id, title FROM articles LIMIT 3");

var columns = result.Columns;        // ["id", "title"]
var rows = result.Rows;              // List<List<string>>
var rowsAffected = result.RowsAffected; // null for SELECT, count for writes

foreach (var row in rows ?? new List<List<string>>())
{
    Console.WriteLine(string.Join(" | ", row));
}
```

### Write statements

```csharp
var update = await client.Sql.ExecuteAsync("UPDATE articles SET title = 'Archived' WHERE id = 'rec123'");
Console.WriteLine(update.RowsAffected); // number of rows changed
```

## Error handling

```csharp
try
{
    await client.Sql.ExecuteAsync("SELECT * FROM missing_table");
}
catch (ClientResponseError ex)
{
    Console.Error.WriteLine($"SQL error ({ex.Status}): {ex.Message}");
}
```

## Parameters and headers

All standard request options are available:

```csharp
var res = await client.Sql.ExecuteAsync(
    "SELECT id FROM users WHERE email = 'admin@example.com'",
    queryParams: new Dictionary<string, object?> { ["trace"] = "sql-1" },
    headers: new Dictionary<string, string> { ["x-debug"] = "true" },
    cancellationToken: cancellationToken);
```

## Notes

- The API endpoint is `POST /api/sql/execute`.
- Only superusers can call it. Regular users receive 401/403.
- SQL is executed directly against the BosBase database; validate statements before running them in production.
