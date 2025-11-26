# Collections - C# SDK Documentation

## Overview

**Collections** represent your application data. Under the hood they are backed by plain SQLite tables that are generated automatically with the collection **name** and **fields** (columns).

A single entry of a collection is called a **record** (a single row in the SQL table).

## Collection Types

### Base Collection

Default collection type for storing any application data.

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

var collection = await pb.Collections.CreateBaseAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "title", ["type"] = "text", ["required"] = true },
        new Dictionary<string, object?> { ["name"] = "description", ["type"] = "text" }
    }
});
```

### View Collection

Read-only collection populated from a SQL SELECT statement.

```csharp
var view = await pb.Collections.CreateViewAsync("post_stats", 
    "SELECT posts.id, posts.name, count(comments.id) as totalComments " +
    "FROM posts LEFT JOIN comments on comments.postId = posts.id " +
    "GROUP BY posts.id"
);
```

### Auth Collection

Base collection with authentication fields (email, password, etc.).

```csharp
var users = await pb.Collections.CreateAuthAsync("users", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "name", ["type"] = "text", ["required"] = true }
    }
});
```

## Collections API

### List Collections

```csharp
var result = await pb.Collections.GetListAsync(1, 50);
var all = await pb.Collections.GetFullListAsync();
```

### Get Collection

```csharp
var collection = await pb.Collections.GetOneAsync("articles");
```

### Create Collection

```csharp
// Using scaffolds
var base = await pb.Collections.CreateBaseAsync("articles");
var auth = await pb.Collections.CreateAuthAsync("users");
var view = await pb.Collections.CreateViewAsync("stats", "SELECT * FROM posts");

// Manual
var collection = await pb.Collections.CreateAsync(new Dictionary<string, object?>
{
    ["type"] = "base",
    ["name"] = "articles",
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "title", ["type"] = "text", ["required"] = true },
        // Note: created and updated fields must be explicitly added if you want to use them
        // For autodate fields, onCreate and onUpdate must be direct properties, not nested in options
        new Dictionary<string, object?>
        {
            ["name"] = "created",
            ["type"] = "autodate",
            ["required"] = false,
            ["onCreate"] = true,
            ["onUpdate"] = false
        },
        new Dictionary<string, object?>
        {
            ["name"] = "updated",
            ["type"] = "autodate",
            ["required"] = false,
            ["onCreate"] = true,
            ["onUpdate"] = true
        }
    }
});
```

### Update Collection

```csharp
// Update collection rules
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["listRule"] = "published = true"
});

// Update collection name
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["name"] = "posts"
});
```

### Add Fields to Collection

To add a new field to an existing collection, fetch the collection, add the field to the fields array, and update:

```csharp
// Get existing collection
var collection = await pb.Collections.GetOneAsync("articles");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

// Add new field to existing fields
fields.Add(new Dictionary<string, object?>
{
    ["name"] = "views",
    ["type"] = "number",
    ["min"] = 0,
    ["onlyInt"] = true
});

// Update collection with new field
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = fields
});

// Or add multiple fields at once
fields.AddRange(new[]
{
    new Dictionary<string, object?>
    {
        ["name"] = "excerpt",
        ["type"] = "text",
        ["max"] = 500
    },
    new Dictionary<string, object?>
    {
        ["name"] = "cover",
        ["type"] = "file",
        ["maxSelect"] = 1,
        ["mimeTypes"] = new[] { "image/jpeg", "image/png" }
    }
});

await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = fields
});

// Adding created and updated autodate fields to existing collection
// Note: onCreate and onUpdate must be direct properties, not nested in options
fields.AddRange(new[]
{
    new Dictionary<string, object?>
    {
        ["name"] = "created",
        ["type"] = "autodate",
        ["required"] = false,
        ["onCreate"] = true,
        ["onUpdate"] = false
    },
    new Dictionary<string, object?>
    {
        ["name"] = "updated",
        ["type"] = "autodate",
        ["required"] = false,
        ["onCreate"] = true,
        ["onUpdate"] = true
    }
});

await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = fields
});
```

### Delete Fields from Collection

To delete a field, fetch the collection, remove the field from the fields array, and update:

```csharp
// Get existing collection
var collection = await pb.Collections.GetOneAsync("articles");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

// Remove field by filtering it out
fields = fields.Where(field => field["name"]?.ToString() != "oldFieldName").ToList();

// Update collection without the deleted field
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = fields
});

// Or remove multiple fields
var fieldsToKeep = new[] { "title", "content", "author", "status" };
fields = fields.Where(field => 
    fieldsToKeep.Contains(field["name"]?.ToString()) || 
    field.ContainsKey("system") && field["system"]?.ToString() == "true"
).ToList();

await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = fields
});
```

### Modify Fields in Collection

To modify an existing field (e.g., change its type, add options, etc.), fetch the collection, update the field object, and save:

```csharp
// Get existing collection
var collection = await pb.Collections.GetOneAsync("articles");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

// Find and modify a field
var titleField = fields.FirstOrDefault(f => f["name"]?.ToString() == "title");
if (titleField != null)
{
    titleField["max"] = 200;  // Change max length
    titleField["required"] = true;  // Make required
}

// Update the field type
var statusField = fields.FirstOrDefault(f => f["name"]?.ToString() == "status");
if (statusField != null)
{
    // Note: Changing field types may require data migration
    statusField["type"] = "select";
    statusField["options"] = new Dictionary<string, object?>
    {
        ["values"] = new[] { "draft", "published", "archived" }
    };
    statusField["maxSelect"] = 1;
}

// Save changes
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = fields
});
```

### Complete Example: Managing Collection Fields

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Get existing collection
var collection = await pb.Collections.GetOneAsync("articles");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

// Add new fields
fields.AddRange(new[]
{
    new Dictionary<string, object?>
    {
        ["name"] = "tags",
        ["type"] = "select",
        ["options"] = new Dictionary<string, object?>
        {
            ["values"] = new[] { "tech", "design", "business" }
        },
        ["maxSelect"] = 5
    },
    new Dictionary<string, object?>
    {
        ["name"] = "published_at",
        ["type"] = "date"
    }
});

// Remove an old field
fields = fields.Where(f => f["name"]?.ToString() != "oldField").ToList();

// Modify existing field
var viewsField = fields.FirstOrDefault(f => f["name"]?.ToString() == "views");
if (viewsField != null)
{
    viewsField["max"] = 1000000;  // Increase max value
}

// Save all changes at once
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = fields
});
```

### Delete Collection

```csharp
await pb.Collections.DeleteCollectionAsync("articles");
```

## Records API

### List Records

```csharp
var result = await pb.Collection("articles").GetListAsync(1, 20, new Dictionary<string, object?>
{
    ["filter"] = "published = true",
    ["sort"] = "-created",
    ["expand"] = "author"
});
```

### Get Record

```csharp
var record = await pb.Collection("articles").GetOneAsync("RECORD_ID", new Dictionary<string, object?>
{
    ["expand"] = "author,category"
});
```

### Create Record

```csharp
var record = await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Article",
    ["views+"] = 1  // Field modifier
});
```

### Update Record

```csharp
await pb.Collection("articles").UpdateAsync("RECORD_ID", new Dictionary<string, object?>
{
    ["title"] = "Updated",
    ["views+"] = 1,
    ["tags+"] = "new-tag"
});
```

### Delete Record

```csharp
await pb.Collection("articles").DeleteAsync("RECORD_ID");
```

## Field Types

### BoolField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "published", ["type"] = "bool", ["required"] = true }

// Usage
await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["published"] = true
});
```

### NumberField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "views", ["type"] = "number", ["min"] = 0 }

// Usage
await pb.Collection("articles").UpdateAsync("ID", new Dictionary<string, object?>
{
    ["views+"] = 1
});
```

### TextField

```csharp
// Field definition
new Dictionary<string, object?> 
{ 
    ["name"] = "title", 
    ["type"] = "text", 
    ["required"] = true, 
    ["min"] = 6, 
    ["max"] = 100 
}

// Usage
await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["slug:autogenerate"] = "article-"
});
```

### EmailField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "email", ["type"] = "email", ["required"] = true }
```

### URLField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "website", ["type"] = "url" }
```

### EditorField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "content", ["type"] = "editor", ["required"] = true }

// Usage
await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["content"] = "<p>HTML content</p>"
});
```

### DateField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "published_at", ["type"] = "date" }

// Usage
await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["published_at"] = "2024-11-10 18:45:27.123Z"
});
```

### AutodateField

**Important Note:** Bosbase does not initialize `created` and `updated` fields by default. To use these fields, you must explicitly add them when initializing the collection. For autodate fields, `onCreate` and `onUpdate` must be direct properties of the field object, not nested in an `options` object:

```csharp
// Create field with proper structure
new Dictionary<string, object?>
{
    ["name"] = "created",
    ["type"] = "autodate",
    ["required"] = false,
    ["onCreate"] = true,  // Set on record creation (direct property)
    ["onUpdate"] = false  // Don't update on record update (direct property)
}

// For updated field
new Dictionary<string, object?>
{
    ["name"] = "updated",
    ["type"] = "autodate",
    ["required"] = false,
    ["onCreate"] = true,  // Set on record creation (direct property)
    ["onUpdate"] = true   // Update on record update (direct property)
}

// The value is automatically set by the backend based on onCreate and onUpdate properties
```

### SelectField

```csharp
// Single select
new Dictionary<string, object?>
{
    ["name"] = "status",
    ["type"] = "select",
    ["options"] = new Dictionary<string, object?> { ["values"] = new[] { "draft", "published" } },
    ["maxSelect"] = 1
}

await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["status"] = "published"
});

// Multiple select
new Dictionary<string, object?>
{
    ["name"] = "tags",
    ["type"] = "select",
    ["options"] = new Dictionary<string, object?> { ["values"] = new[] { "tech", "design" } },
    ["maxSelect"] = 5
}

await pb.Collection("articles").UpdateAsync("ID", new Dictionary<string, object?>
{
    ["tags+"] = "marketing"
});
```

### FileField

```csharp
// Single file
new Dictionary<string, object?>
{
    ["name"] = "cover",
    ["type"] = "file",
    ["maxSelect"] = 1,
    ["mimeTypes"] = new[] { "image/jpeg" }
}

// Upload file using FileAttachment
var fileAttachment = new FileAttachment
{
    FieldName = "cover",
    FileName = "image.jpg",
    Content = fileBytes,  // byte[]
    ContentType = "image/jpeg"
};

var record = await pb.Collection("articles").CreateAsync(
    new Dictionary<string, object?> { ["title"] = "My Post" },
    files: new[] { fileAttachment }
);
```

### RelationField

```csharp
// Field definition
new Dictionary<string, object?>
{
    ["name"] = "author",
    ["type"] = "relation",
    ["options"] = new Dictionary<string, object?> { ["collectionId"] = "users" },
    ["maxSelect"] = 1
}

// Usage
await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["author"] = "USER_ID"
});

var record = await pb.Collection("articles").GetOneAsync("ID", new Dictionary<string, object?>
{
    ["expand"] = "author"
});
```

### JSONField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "metadata", ["type"] = "json" }

// Usage
await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["metadata"] = new Dictionary<string, object?>
    {
        ["seo"] = new Dictionary<string, object?> { ["title"] = "SEO Title" }
    }
});
```

### GeoPointField

```csharp
// Field definition
new Dictionary<string, object?> { ["name"] = "location", ["type"] = "geoPoint" }

// Usage
await pb.Collection("places").CreateAsync(new Dictionary<string, object?>
{
    ["location"] = new Dictionary<string, object?>
    {
        ["lon"] = 139.6917,
        ["lat"] = 35.6586
    }
});
```

## Complete Example

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Create collections
var users = await pb.Collections.CreateAuthAsync("users");
var articles = await pb.Collections.CreateBaseAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "title", ["type"] = "text", ["required"] = true },
        new Dictionary<string, object?>
        {
            ["name"] = "author",
            ["type"] = "relation",
            ["options"] = new Dictionary<string, object?> { ["collectionId"] = users["id"]?.ToString() },
            ["maxSelect"] = 1
        }
    }
});

// Create and authenticate user
var user = await pb.Collection("users").CreateAsync(new Dictionary<string, object?>
{
    ["email"] = "user@example.com",
    ["password"] = "password123",
    ["passwordConfirm"] = "password123"
});
await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password123");

// Create article
var article = await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Article",
    ["author"] = user["id"]?.ToString()
});

// Subscribe to changes
pb.Collection("articles").Subscribe("*", (e) =>
{
    Console.WriteLine($"Action: {e["action"]}, Record: {e["record"]}");
});
```

