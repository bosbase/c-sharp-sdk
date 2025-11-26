# Register Existing SQL Tables with the C# SDK

Use the SQL table helpers to expose existing tables (or run SQL to create them) and automatically generate REST collections. Both calls are **superuser-only**.

- `RegisterSqlTablesAsync(tables: string[])` – map existing tables to collections without running SQL.
- `ImportSqlTablesAsync(tables: SqlTableDefinition[])` – optionally run SQL to create tables first, then register them. Returns `{ created, skipped }`.

## Requirements

- Authenticate with a `_superusers` token.
- Each table must contain a `TEXT` primary key column named `id`.
- Missing audit columns (`created`, `updated`, `createdBy`, `updatedBy`) are automatically added so the default API rules can be applied.
- Non-system columns are mapped by best effort (text, number, bool, date/time, JSON).

## Basic Usage

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

var collections = await client.Collections.RegisterSqlTablesAsync(new[]
{
    "projects",
    "accounts"
});

foreach (var collection in collections)
{
    Console.WriteLine(collection["name"]);
}
// => ["projects", "accounts"]
```

## With Request Options

You can pass standard request options (headers, query params, cancellation tokens, etc.).

```csharp
var collections = await client.Collections.RegisterSqlTablesAsync(
    new[] { "legacy_orders" },
    query: new Dictionary<string, object?> { ["q"] = 1 },
    headers: new Dictionary<string, string> { ["x-trace-id"] = "reg-123" }
);
```

## Create-or-register flow

`ImportSqlTablesAsync()` accepts `SqlTableDefinition { name: string; sql?: string }` items, runs the SQL (if provided), and registers collections. Existing collection names are reported under `skipped`.

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

var tables = new[]
{
    new Dictionary<string, object?>
    {
        ["name"] = "legacy_orders",
        ["sql"] = @"
            CREATE TABLE IF NOT EXISTS legacy_orders (
                id TEXT PRIMARY KEY,
                customer_email TEXT NOT NULL
            );
        "
    },
    new Dictionary<string, object?>
    {
        ["name"] = "reporting_view"
        // assumes table already exists
    }
};

var result = await client.Collections.ImportSqlTablesAsync(tables);

if (result["created"] is List<object?> created)
{
    foreach (var collection in created)
    {
        if (collection is Dictionary<string, object?> collDict)
        {
            Console.WriteLine(collDict["name"]);
        }
    }
}

if (result["skipped"] is List<object?> skipped)
{
    Console.WriteLine($"Skipped: {string.Join(", ", skipped)}");
}
```

## What It Does

- Creates BosBase collection metadata for the provided tables.
- Generates REST endpoints for CRUD against those tables.
- Applies the standard default API rules (authenticated create; update/delete scoped to the creator).
- Ensures audit columns exist (`created`, `updated`, `createdBy`, `updatedBy`) and leaves all other existing SQL schema and data untouched; no further field mutations or table syncs are performed.
- Marks created collections with `externalTable: true` so you can distinguish them from regular BosBase-managed tables.

## Troubleshooting

- 400 error: ensure `id` exists as `TEXT PRIMARY KEY` and the table name is not system-reserved (no leading `_`).
- 401/403: confirm you are authenticated as a superuser.
- Default audit fields (`created`, `updated`, `createdBy`, `updatedBy`) are auto-added if they're missing so the default owner rules validate successfully.

## Related Documentation

- [Collection API](./COLLECTION_API.md) - Collection management
- [API Rules](./API_RULES_AND_FILTERS.md) - API rules and filters

