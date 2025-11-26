# Collections - C# SDK Documentation

This document provides comprehensive documentation for working with Collections and Fields in the BosBase C# SDK. This documentation is designed to be AI-readable and includes practical examples for all operations.

## Table of Contents

- [Overview](#overview)
- [Collection Types](#collection-types)
- [Collections API](#collections-api)
- [Records API](#records-api)
- [Field Types](#field-types)
- [Examples](#examples)

## Overview

**Collections** represent your application data. Under the hood they are backed by plain SQLite tables that are generated automatically with the collection **name** and **fields** (columns).

A single entry of a collection is called a **record** (a single row in the SQL table).

You can manage your **collections** from the Dashboard, or with the C# SDK using the `Collections` service.

Similarly, you can manage your **records** from the Dashboard, or with the C# SDK using the `Collection(name)` method which returns a `RecordService` instance.

## Collection Types

Currently there are 3 collection types: **Base**, **View** and **Auth**.

### Base Collection

**Base collection** is the default collection type and it could be used to store any application data (articles, products, posts, etc.).

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Create a base collection
var collection = await client.Collections.CreateBaseAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = new[]
    {
        new Dictionary<string, object?>
        {
            ["name"] = "title",
            ["type"] = "text",
            ["required"] = true,
            ["min"] = 6,
            ["max"] = 100
        },
        new Dictionary<string, object?>
        {
            ["name"] = "description",
            ["type"] = "text"
        }
    },
    ["listRule"] = "@request.auth.id != \"\" || status = \"public\"",
    ["viewRule"] = "@request.auth.id != \"\" || status = \"public\""
});
```

### View Collection

**View collection** is a read-only collection type where the data is populated from a plain SQL `SELECT` statement, allowing users to perform aggregations or any other custom queries.

For example, the following query will create a read-only collection with 3 _posts_ fields - _id_, _name_ and _totalComments_:

```csharp
// Create a view collection
var viewCollection = await client.Collections.CreateViewAsync("post_stats", 
    @"SELECT posts.id, posts.name, count(comments.id) as totalComments 
      FROM posts 
      LEFT JOIN comments on comments.postId = posts.id 
      GROUP BY posts.id"
);
```

**Note**: View collections don't receive realtime events because they don't have create/update/delete operations.

### Auth Collection

**Auth collection** has everything from the **Base collection** but with some additional special fields to help you manage your app users and also provide various authentication options.

Each Auth collection has the following special system fields: `email`, `emailVisibility`, `verified`, `password` and `tokenKey`. They cannot be renamed or deleted but can be configured using their specific field options.

```csharp
// Create an auth collection
var usersCollection = await client.Collections.CreateAuthAsync("users", new Dictionary<string, object?>
{
    ["fields"] = new[]
    {
        new Dictionary<string, object?>
        {
            ["name"] = "name",
            ["type"] = "text",
            ["required"] = true
        },
        new Dictionary<string, object?>
        {
            ["name"] = "role",
            ["type"] = "select",
            ["options"] = new Dictionary<string, object?>
            {
                ["values"] = new[] { "employee", "staff", "admin" }
            }
        }
    }
});
```

## Collections API

### Create Collection

```csharp
var collection = await client.Collections.CreateAsync(new Dictionary<string, object?>
{
    ["name"] = "posts",
    ["type"] = "base",
    ["fields"] = new[]
    {
        new Dictionary<string, object?>
        {
            ["name"] = "title",
            ["type"] = "text",
            ["required"] = true
        }
    }
});

Console.WriteLine($"Collection ID: {collection["id"]}");
```

### Update Collection

```csharp
var collection = await client.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["listRule"] = "@request.auth.id != \"\" || published = true && status = \"public\""
});
```

### Delete Collection

```csharp
// Warning: This will delete the collection and all its records
await client.Collections.DeleteCollectionAsync("articles");
```

### Truncate Collection

Deletes all records but keeps the collection structure:

```csharp
await client.Collections.TruncateAsync("articles");
```

### Import Collections

```csharp
var collectionsToImport = new[]
{
    new Dictionary<string, object?>
    {
        ["type"] = "base",
        ["name"] = "articles",
        ["fields"] = new[] { /* fields */ }
    },
    new Dictionary<string, object?>
    {
        ["type"] = "auth",
        ["name"] = "users",
        ["fields"] = new[] { /* fields */ }
    }
};

// Import collections (deleteMissing will delete collections not in the import list)
await client.Collections.ImportCollectionsAsync(collectionsToImport, deleteMissing: false);
```

### Get Scaffolds

```csharp
var scaffolds = await client.Collections.GetScaffoldsAsync();
// Returns: Dictionary with base, auth, view scaffold templates
```

## Records API

### Get Record Service

```csharp
// Get a RecordService instance for a collection
var articles = client.Collection("articles");
```

### List Records

```csharp
// Paginated list 
var result = await client.Collection("articles").GetListAsync(1, 20, 
    filter: "published = true",
    sort: "-created",
    expand: "author",
    fields: "id,title,description"
);

var items = result["items"] as List<object?>;      // Array of records
var page = result["page"];       // Current page number
var perPage = result["perPage"];    // Items per page
var totalItems = result["totalItems"]; // Total items count
var totalPages = result["totalPages"]; // Total pages count

// Get all records (automatically paginates)
var allRecords = await client.Collection("articles").GetFullListAsync(
    filter: "published = true",
    sort: "-created"
);
```

### Get Single Record

```csharp
var record = await client.Collection("articles").GetOneAsync("RECORD_ID", 
    expand: "author,category",
    fields: "id,title,description,author"
);
```

### Get First Matching Record

```csharp
var record = await client.Collection("articles").GetFirstListItemAsync(
    "title ~ \"example\" && published = true",
    expand: "author"
);
```

### Create Record

```csharp
// Simple create
var record = await client.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My First Article",
    ["description"] = "This is a test article",
    ["published"] = true,
    ["views"] = 0
});

// With file upload
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("cover", "/path/to/cover.jpg", "image/jpeg");
var record = await client.Collection("articles").CreateAsync(
    new Dictionary<string, object?> { ["title"] = "My Article" },
    files: new[] { fileAttachment }
);
```

### Update Record

```csharp
// Simple update
var record = await client.Collection("articles").UpdateAsync("RECORD_ID", new Dictionary<string, object?>
{
    ["title"] = "Updated Title",
    ["published"] = true
});

// With file upload
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("cover", "/path/to/new-cover.jpg", "image/jpeg");
var record = await client.Collection("articles").UpdateAsync(
    "RECORD_ID",
    new Dictionary<string, object?> { ["title"] = "Updated Title" },
    files: new[] { fileAttachment }
);
```

### Delete Record

```csharp
await client.Collection("articles").DeleteAsync("RECORD_ID");
```

### Batch Operations

```csharp
var batch = client.CreateBatch();
batch.Collection("articles").Create(new Dictionary<string, object?> { ["title"] = "Article 1" });
batch.Collection("articles").Create(new Dictionary<string, object?> { ["title"] = "Article 2" });
batch.Collection("articles").Update("RECORD_ID", new Dictionary<string, object?> { ["published"] = true });
var results = await batch.SendAsync();
```

## Field Types

All collection fields (with exception of the `JSONField`) are **non-nullable and use a zero-default** for their respective type as fallback value when missing (empty string for `text`, 0 for `number`, etc.).

### BoolField

Stores a single `false` (default) or `true` value.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "published",
    ["type"] = "bool",
    ["required"] = true
}

// Usage
await client.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["published"] = true
});
```

### NumberField

Stores numeric/float64 value: `0` (default), `2`, `-1`, `1.5`.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "views",
    ["type"] = "number",
    ["min"] = 0,
    ["max"] = 1000000,
    ["onlyInt"] = false  // Allow decimals
}

// Usage
await client.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["views"] = 0
});
```

### TextField

Stores string values: `""` (default), `"example"`.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "title",
    ["type"] = "text",
    ["required"] = true,
    ["min"] = 6,
    ["max"] = 100,
    ["pattern"] = "^[A-Z]"  // Must start with uppercase
}

// Usage
await client.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Article"
});
```

### EmailField

Stores a single email string address: `""` (default), `"john@example.com"`.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "email",
    ["type"] = "email",
    ["required"] = true
}
```

### DateField

Stores date/time values.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "published_at",
    ["type"] = "date",
    ["required"] = false
}

// Usage
await client.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["published_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
});
```

### FileField

Stores file references.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "avatar",
    ["type"] = "file",
    ["required"] = false,
    ["maxSelect"] = 1,
    ["maxSize"] = 2097152, // 2MB
    ["mimeTypes"] = new[] { "image/jpeg", "image/png" }
}
```

### RelationField

Stores references to other collection records.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "author",
    ["type"] = "relation",
    ["required"] = true,
    ["collectionId"] = "_pbc_users_auth_",
    ["maxSelect"] = 1
}
```

### SelectField

Stores one or more values from a predefined list.

```csharp
// Create field
new Dictionary<string, object?>
{
    ["name"] = "status",
    ["type"] = "select",
    ["required"] = true,
    ["options"] = new Dictionary<string, object?>
    {
        ["values"] = new[] { "draft", "published", "archived" }
    }
}
```

## Complete Examples

### Example 1: Blog System

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Create posts collection
var postsCollection = await client.Collections.CreateBaseAsync("posts", new Dictionary<string, object?>
{
    ["fields"] = new[]
    {
        new Dictionary<string, object?>
        {
            ["name"] = "title",
            ["type"] = "text",
            ["required"] = true
        },
        new Dictionary<string, object?>
        {
            ["name"] = "content",
            ["type"] = "editor"
        },
        new Dictionary<string, object?>
        {
            ["name"] = "author",
            ["type"] = "relation",
            ["collectionId"] = "_pb_users_auth_",
            ["maxSelect"] = 1,
            ["required"] = true
        },
        new Dictionary<string, object?>
        {
            ["name"] = "status",
            ["type"] = "select",
            ["options"] = new Dictionary<string, object?>
            {
                ["values"] = new[] { "draft", "published" }
            }
        }
    },
    ["listRule"] = "status = \"published\" || author = @request.auth.id",
    ["viewRule"] = "status = \"published\" || author = @request.auth.id",
    ["createRule"] = "@request.auth.id != \"\"",
    ["updateRule"] = "author = @request.auth.id",
    ["deleteRule"] = "author = @request.auth.id"
});

// Create a post
var post = await client.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My First Post",
    ["content"] = "Post content",
    ["author"] = "user_id",
    ["status"] = "published"
});
```

## Related Documentation

- [Collections](./COLLECTIONS.md) - Basic collection operations
- [API Records](./API_RECORDS.md) - Record CRUD operations
- [Field Types](./COLLECTIONS.md#field-types) - Detailed field type information

