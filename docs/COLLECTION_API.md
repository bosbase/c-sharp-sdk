# Collection API - C# SDK Documentation

## Overview

The Collection API provides endpoints for managing collections (Base, Auth, and View types). All operations require superuser authentication and allow you to create, read, update, and delete collections along with their schemas and configurations.

**Key Features:**
- List and search collections
- View collection details
- Create collections (base, auth, view)
- Update collection schemas and rules
- Delete collections
- Truncate collections (delete all records)
- Import collections in bulk
- Get collection scaffolds (templates)

**Backend Endpoints:**
- `GET /api/collections` - List collections
- `GET /api/collections/{collection}` - View collection
- `POST /api/collections` - Create collection
- `PATCH /api/collections/{collection}` - Update collection
- `DELETE /api/collections/{collection}` - Delete collection
- `DELETE /api/collections/{collection}/truncate` - Truncate collection
- `PUT /api/collections/import` - Import collections
- `GET /api/collections/meta/scaffolds` - Get scaffolds

**Note**: All Collection API operations require superuser authentication.

## Authentication

All Collection API operations require superuser authentication:

```csharp
using Bosbase;

var pb = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate as superuser
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
```

## List Collections

Returns a paginated list of collections with support for filtering and sorting.

```csharp
// Basic list
var result = await pb.Collections.GetListAsync(1, 30);

Console.WriteLine(result["page"]);        // 1
Console.WriteLine(result["perPage"]);     // 30
Console.WriteLine(result["totalItems"]);  // Total collections count
Console.WriteLine(result["items"]);       // List of collections
```

### Advanced Filtering and Sorting

```csharp
// Filter by type
var authCollections = await pb.Collections.GetListAsync(
    page: 1,
    perPage: 100,
    filter: "type = \"auth\""
);

// Filter by name pattern
var matchingCollections = await pb.Collections.GetListAsync(
    page: 1,
    perPage: 100,
    filter: "name ~ \"user\""
);

// Sort by creation date
var sortedCollections = await pb.Collections.GetListAsync(
    page: 1,
    perPage: 100,
    sort: "-created"
);

// Complex filter
var filtered = await pb.Collections.GetListAsync(
    page: 1,
    perPage: 100,
    filter: "type = \"base\" && system = false && created >= \"2023-01-01\"",
    sort: "name"
);
```

### Get Full List

```csharp
// Get all collections at once
var allCollections = await pb.Collections.GetFullListAsync(
    sort: "name",
    filter: "system = false"
);
```

### Get First Matching Collection

```csharp
// Get first auth collection
var authCollection = await pb.Collections.GetFirstListItemAsync("type = \"auth\"");
```

## View Collection

Retrieve a single collection by ID or name:

```csharp
// By name
var collection = await pb.Collections.GetOneAsync("posts");

// By ID
var collection = await pb.Collections.GetOneAsync("_pbc_2287844090");

// With field selection
var collection = await pb.Collections.GetOneAsync(
    "posts",
    fields: "id,name,type,fields.name,fields.type"
);
```

## Create Collection

Create a new collection with schema fields and configuration.

**Note**: If the `created` and `updated` fields are not specified during collection initialization, BosBase will automatically create them. These system fields are added to all collections by default and track when records are created and last modified. You don't need to include them in your field definitions.

### Create Base Collection

```csharp
var baseCollection = await pb.Collections.CreateAsync(new Dictionary<string, object?>
{
    ["name"] = "posts",
    ["type"] = "base",
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?>
        {
            ["name"] = "title",
            ["type"] = "text",
            ["required"] = true,
            ["min"] = 10,
            ["max"] = 255
        },
        new Dictionary<string, object?>
        {
            ["name"] = "content",
            ["type"] = "editor",
            ["required"] = false
        },
        new Dictionary<string, object?>
        {
            ["name"] = "published",
            ["type"] = "bool",
            ["required"] = false
        },
        new Dictionary<string, object?>
        {
            ["name"] = "author",
            ["type"] = "relation",
            ["required"] = true,
            ["options"] = new Dictionary<string, object?> { ["collectionId"] = "_pbc_users_auth_" },
            ["maxSelect"] = 1
        }
    },
    ["listRule"] = "@request.auth.id != \"\"",
    ["viewRule"] = "@request.auth.id != \"\" || published = true",
    ["createRule"] = "@request.auth.id != \"\"",
    ["updateRule"] = "author = @request.auth.id",
    ["deleteRule"] = "author = @request.auth.id"
});
```

### Create Auth Collection

```csharp
var authCollection = await pb.Collections.CreateAsync(new Dictionary<string, object?>
{
    ["name"] = "users",
    ["type"] = "auth",
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?>
        {
            ["name"] = "name",
            ["type"] = "text",
            ["required"] = false
        },
        new Dictionary<string, object?>
        {
            ["name"] = "avatar",
            ["type"] = "file",
            ["required"] = false,
            ["maxSelect"] = 1,
            ["maxSize"] = 2097152, // 2MB
            ["mimeTypes"] = new[] { "image/jpeg", "image/png" }
        }
    },
    ["listRule"] = null,
    ["viewRule"] = "@request.auth.id = id",
    ["createRule"] = null,
    ["updateRule"] = "@request.auth.id = id",
    ["deleteRule"] = "@request.auth.id = id",
    ["manageRule"] = null,
    ["authRule"] = "verified = true", // Only verified users can authenticate
    ["passwordAuth"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["identityFields"] = new[] { "email", "username" }
    },
    ["authToken"] = new Dictionary<string, object?>
    {
        ["duration"] = 604800 // 7 days
    },
    ["oauth2"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["providers"] = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?>
            {
                ["name"] = "google",
                ["clientId"] = "YOUR_CLIENT_ID",
                ["clientSecret"] = "YOUR_CLIENT_SECRET",
                ["authURL"] = "https://accounts.google.com/o/oauth2/auth",
                ["tokenURL"] = "https://oauth2.googleapis.com/token",
                ["userInfoURL"] = "https://www.googleapis.com/oauth2/v2/userinfo",
                ["displayName"] = "Google"
            }
        }
    }
});
```

### Create View Collection

```csharp
var viewCollection = await pb.Collections.CreateAsync(new Dictionary<string, object?>
{
    ["name"] = "published_posts",
    ["type"] = "view",
    ["listRule"] = "@request.auth.id != \"\"",
    ["viewRule"] = "@request.auth.id != \"\"",
    ["viewQuery"] = @"
        SELECT 
          p.id,
          p.title,
          p.content,
          p.created,
          u.name as author_name,
          u.email as author_email
        FROM posts p
        LEFT JOIN users u ON p.author = u.id
        WHERE p.published = true
    "
});
```

### Create from Scaffold

Use predefined scaffolds as a starting point:

```csharp
// Get available scaffolds
var scaffolds = await pb.Collections.GetScaffoldsAsync();

// Create base collection from scaffold
var baseCollection = await pb.Collections.CreateBaseAsync("my_posts", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?>
        {
            ["name"] = "title",
            ["type"] = "text",
            ["required"] = true
        }
    }
});

// Create auth collection from scaffold
var authCollection = await pb.Collections.CreateAuthAsync("my_users", new Dictionary<string, object?>
{
    ["passwordAuth"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["identityFields"] = new[] { "email" }
    }
});

// Create view collection from scaffold
var viewCollection = await pb.Collections.CreateViewAsync(
    "my_view",
    "SELECT id, title FROM posts",
    new Dictionary<string, object?>
    {
        ["listRule"] = "@request.auth.id != \"\""
    }
);
```

### Accessing Collection ID After Creation

When a collection is successfully created, the returned dictionary includes the `id` property, which contains the unique identifier assigned by the backend. You can access it immediately after creation:

```csharp
// Create a collection and access its ID
var collection = await pb.Collections.CreateAsync(new Dictionary<string, object?>
{
    ["name"] = "posts",
    ["type"] = "base",
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?>
        {
            ["name"] = "title",
            ["type"] = "text",
            ["required"] = true
        }
    }
});

// Access the collection ID
Console.WriteLine(collection["id"]); // e.g., "_pbc_2287844090"

// Use the ID for subsequent operations
await pb.Collections.UpdateAsync(collection["id"]?.ToString() ?? "", new Dictionary<string, object?>
{
    ["listRule"] = "@request.auth.id != \"\""
});

// Store the ID for later use
var collectionId = collection["id"]?.ToString();
```

**Example: Creating multiple collections and storing their IDs**

```csharp
async Task<Dictionary<string, string>> SetupCollectionsAsync()
{
    // Create posts collection
    var posts = await pb.Collections.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "posts",
        ["type"] = "base",
        ["fields"] = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["name"] = "title", ["type"] = "text", ["required"] = true },
            new Dictionary<string, object?> { ["name"] = "content", ["type"] = "editor" }
        }
    });

    // Create categories collection
    var categories = await pb.Collections.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "categories",
        ["type"] = "base",
        ["fields"] = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["name"] = "name", ["type"] = "text", ["required"] = true }
        }
    });

    // Access IDs immediately after creation
    Console.WriteLine($"Posts collection ID: {posts["id"]}");
    Console.WriteLine($"Categories collection ID: {categories["id"]}");

    // Use IDs to create relations
    var postsUpdated = await pb.Collections.GetOneAsync(posts["id"]?.ToString() ?? "");
    var fields = (postsUpdated["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
        ?? new List<Dictionary<string, object?>>();
    
    fields.Add(new Dictionary<string, object?>
    {
        ["name"] = "category",
        ["type"] = "relation",
        ["options"] = new Dictionary<string, object?> { ["collectionId"] = categories["id"]?.ToString() },
        ["maxSelect"] = 1
    });
    
    await pb.Collections.UpdateAsync(posts["id"]?.ToString() ?? "", new Dictionary<string, object?>
    {
        ["fields"] = fields
    });

    return new Dictionary<string, string>
    {
        ["postsId"] = posts["id"]?.ToString() ?? "",
        ["categoriesId"] = categories["id"]?.ToString() ?? ""
    };
}
```

**Note**: The `id` property is automatically generated by the backend and is available immediately after successful creation. You don't need to make a separate API call to retrieve it.

## Update Collection

Update an existing collection's schema, fields, or rules:

```csharp
// Update collection name and rules
var updated = await pb.Collections.UpdateAsync("posts", new Dictionary<string, object?>
{
    ["name"] = "articles",
    ["listRule"] = "@request.auth.id != \"\" || status = \"public\"",
    ["viewRule"] = "@request.auth.id != \"\" || status = \"public\""
});

// Add new field
var collection = await pb.Collections.GetOneAsync("posts");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

fields.Add(new Dictionary<string, object?>
{
    ["name"] = "tags",
    ["type"] = "select",
    ["options"] = new Dictionary<string, object?>
    {
        ["values"] = new[] { "tech", "science", "art" }
    }
});

await pb.Collections.UpdateAsync("posts", new Dictionary<string, object?>
{
    ["fields"] = fields
});

// Update field configuration
var collection2 = await pb.Collections.GetOneAsync("posts");
var fields2 = (collection2["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

var titleField = fields2.FirstOrDefault(f => f["name"]?.ToString() == "title");
if (titleField != null)
{
    titleField["max"] = 200;
    await pb.Collections.UpdateAsync("posts", new Dictionary<string, object?>
    {
        ["fields"] = fields2
    });
}
```

### Updating Auth Collection Options

```csharp
// Update OAuth2 configuration
var collection = await pb.Collections.GetOneAsync("users");
var oauth2 = collection["oauth2"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
oauth2["enabled"] = true;
oauth2["providers"] = new List<Dictionary<string, object?>>
{
    new Dictionary<string, object?>
    {
        ["name"] = "github",
        ["clientId"] = "NEW_CLIENT_ID",
        ["clientSecret"] = "NEW_CLIENT_SECRET",
        ["displayName"] = "GitHub"
    }
};

await pb.Collections.UpdateAsync("users", new Dictionary<string, object?>
{
    ["oauth2"] = oauth2
});

// Update token duration
var authToken = collection["authToken"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
authToken["duration"] = 2592000; // 30 days

await pb.Collections.UpdateAsync("users", new Dictionary<string, object?>
{
    ["authToken"] = authToken
});
```

## Manage Indexes

BosBase stores collection indexes as SQL expressions on the `indexes` property of a collection. The C# SDK provides dedicated helpers so you don't have to manually craft the SQL or resend the full collection payload every time you want to adjust an index.

### Add or Update Indexes

```csharp
// Create a unique slug index (index names are optional)
await pb.Collections.AddIndexAsync("posts", new[] { "slug" }, unique: true, indexName: "idx_posts_slug_unique");

// Composite (non-unique) index; defaults to idx_{collection}_{columns}
await pb.Collections.AddIndexAsync("posts", new[] { "status", "published" });
```

- `collectionIdOrName` can be either the collection name or internal id.
- `columns` must reference existing columns (system fields such as `id`, `created`, and `updated` are allowed).
- `unique` (default `false`) controls whether `CREATE UNIQUE INDEX` or `CREATE INDEX` is generated.
- `indexName` is optional; omit it to let the SDK generate `idx_{collection}_{column1}_{column2}` automatically.

Calling `AddIndexAsync` twice with the same name replaces the definition on the backend, making it easy to iterate on your schema.

### Remove Indexes

```csharp
// Remove the index that targets the slug column
await pb.Collections.RemoveIndexAsync("posts", new[] { "slug" });
```

`RemoveIndexAsync` looks for indexes that contain all of the provided columns (in any order) and drops them from the collection. This deletes the actual database index when the collection is saved.

### List Indexes

```csharp
var indexes = await pb.Collections.GetIndexesAsync("posts");

foreach (var idx in indexes)
{
    Console.WriteLine(idx);
}
// => CREATE UNIQUE INDEX `idx_posts_slug_unique` ON `posts` (`slug`)
```

`GetIndexesAsync` returns the raw SQL strings stored on the collection so you can audit existing indexes or decide whether you need to create new ones.

## Delete Collection

Delete a collection (including all records and files):

```csharp
// Delete by name
await pb.Collections.DeleteCollectionAsync("old_collection");

// Delete by ID
await pb.Collections.DeleteCollectionAsync("_pbc_2287844090");
```

**Warning**: This operation is destructive and will:
- Delete the collection schema
- Delete all records in the collection
- Delete all associated files
- Remove all indexes

**Note**: Collections referenced by other collections cannot be deleted.

## Truncate Collection

Delete all records in a collection while keeping the collection schema:

```csharp
// Truncate collection (delete all records)
await pb.Collections.TruncateAsync("posts");

// This will:
// - Delete all records in the collection
// - Delete all associated files
// - Delete cascade-enabled relations
// - Keep the collection schema intact
```

**Warning**: This operation is destructive and cannot be undone. It's useful for:
- Clearing test data
- Resetting collections
- Bulk data removal

**Note**: View collections cannot be truncated.

## Import Collections

Bulk import multiple collections at once:

```csharp
var collectionsToImport = new List<Dictionary<string, object?>>
{
    new Dictionary<string, object?>
    {
        ["name"] = "posts",
        ["type"] = "base",
        ["fields"] = new List<Dictionary<string, object?>>
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
            }
        },
        ["listRule"] = "@request.auth.id != \"\""
    },
    new Dictionary<string, object?>
    {
        ["name"] = "categories",
        ["type"] = "base",
        ["fields"] = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?>
            {
                ["name"] = "name",
                ["type"] = "text",
                ["required"] = true
            }
        }
    }
};

// Import without deleting existing collections
await pb.Collections.ImportCollectionsAsync(collectionsToImport, deleteMissing: false);

// Import and delete collections not in the import list
await pb.Collections.ImportCollectionsAsync(collectionsToImport, deleteMissing: true);
```

### Import Options

- **`deleteMissing: false`** (default): Only create/update collections in the import list
- **`deleteMissing: true`**: Delete all collections not present in the import list

**Warning**: Using `deleteMissing: true` will permanently delete collections and all their data.

## Get Scaffolds

Get collection templates for creating new collections:

```csharp
var scaffolds = await pb.Collections.GetScaffoldsAsync();

// Available scaffold types
var baseTemplate = scaffolds["base"] as Dictionary<string, object?>;   // Base collection template
var authTemplate = scaffolds["auth"] as Dictionary<string, object?>;   // Auth collection template
var viewTemplate = scaffolds["view"] as Dictionary<string, object?>;    // View collection template

// Use scaffold as starting point
if (baseTemplate != null)
{
    var newCollection = new Dictionary<string, object?>(baseTemplate)
    {
        ["name"] = "my_collection"
    };
    
    var fields = (baseTemplate["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
        ?? new List<Dictionary<string, object?>>();
    
    fields.Add(new Dictionary<string, object?>
    {
        ["name"] = "custom_field",
        ["type"] = "text"
    });
    
    newCollection["fields"] = fields;
    
    await pb.Collections.CreateAsync(newCollection);
}
```

## Filter Syntax

Collections support filtering with the same syntax as records:

### Supported Fields

- `id` - Collection ID
- `created` - Creation date
- `updated` - Last update date
- `name` - Collection name
- `type` - Collection type (`base`, `auth`, `view`)
- `system` - System collection flag (boolean)

### Filter Examples

```csharp
// Filter by type
filter: "type = \"auth\""

// Filter by name pattern
filter: "name ~ \"user\""

// Filter non-system collections
filter: "system = false"

// Multiple conditions
filter: "type = \"base\" && system = false && created >= \"2023-01-01\""

// Complex filter
filter: "(type = \"auth\" || type = \"base\") && name !~ \"test\""
```

## Sort Options

Supported sort fields:

- `@random` - Random order
- `id` - Collection ID
- `created` - Creation date
- `updated` - Last update date
- `name` - Collection name
- `type` - Collection type
- `system` - System flag

```csharp
// Sort examples
sort: "name"           // ASC by name
sort: "-created"       // DESC by creation date
sort: "type,-created"  // ASC by type, then DESC by created
```

## Complete Examples

### Example 1: Setup Blog Collections

```csharp
async Task<Dictionary<string, string>> SetupBlogAsync()
{
    // Create posts collection
    var posts = await pb.Collections.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "posts",
        ["type"] = "base",
        ["fields"] = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?>
            {
                ["name"] = "title",
                ["type"] = "text",
                ["required"] = true,
                ["min"] = 10,
                ["max"] = 255
            },
            new Dictionary<string, object?>
            {
                ["name"] = "slug",
                ["type"] = "text",
                ["required"] = true,
                ["options"] = new Dictionary<string, object?>
                {
                    ["pattern"] = "^[a-z0-9-]+$"
                }
            },
            new Dictionary<string, object?>
            {
                ["name"] = "content",
                ["type"] = "editor",
                ["required"] = true
            },
            new Dictionary<string, object?>
            {
                ["name"] = "featured_image",
                ["type"] = "file",
                ["maxSelect"] = 1,
                ["maxSize"] = 5242880, // 5MB
                ["mimeTypes"] = new[] { "image/jpeg", "image/png" }
            },
            new Dictionary<string, object?>
            {
                ["name"] = "published",
                ["type"] = "bool",
                ["required"] = false
            },
            new Dictionary<string, object?>
            {
                ["name"] = "author",
                ["type"] = "relation",
                ["options"] = new Dictionary<string, object?> { ["collectionId"] = "_pbc_users_auth_" },
                ["maxSelect"] = 1
            },
            new Dictionary<string, object?>
            {
                ["name"] = "categories",
                ["type"] = "relation",
                ["options"] = new Dictionary<string, object?> { ["collectionId"] = "categories" },
                ["maxSelect"] = 5
            }
        },
        ["listRule"] = "@request.auth.id != \"\" || published = true",
        ["viewRule"] = "@request.auth.id != \"\" || published = true",
        ["createRule"] = "@request.auth.id != \"\"",
        ["updateRule"] = "author = @request.auth.id",
        ["deleteRule"] = "author = @request.auth.id"
    });

    // Create categories collection
    var categories = await pb.Collections.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "categories",
        ["type"] = "base",
        ["fields"] = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?>
            {
                ["name"] = "name",
                ["type"] = "text",
                ["required"] = true,
                ["unique"] = true
            },
            new Dictionary<string, object?>
            {
                ["name"] = "slug",
                ["type"] = "text",
                ["required"] = true
            },
            new Dictionary<string, object?>
            {
                ["name"] = "description",
                ["type"] = "text",
                ["required"] = false
            }
        },
        ["listRule"] = "@request.auth.id != \"\"",
        ["viewRule"] = "@request.auth.id != \"\""
    });

    // Access collection IDs immediately after creation
    Console.WriteLine($"Posts collection ID: {posts["id"]}");
    Console.WriteLine($"Categories collection ID: {categories["id"]}");

    // Update posts collection to use the categories collection ID
    var postsUpdated = await pb.Collections.GetOneAsync(posts["id"]?.ToString() ?? "");
    var fields = (postsUpdated["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
        ?? new List<Dictionary<string, object?>>();
    
    var categoryField = fields.FirstOrDefault(f => f["name"]?.ToString() == "categories");
    if (categoryField != null)
    {
        categoryField["options"] = new Dictionary<string, object?> { ["collectionId"] = categories["id"]?.ToString() };
        await pb.Collections.UpdateAsync(posts["id"]?.ToString() ?? "", new Dictionary<string, object?>
        {
            ["fields"] = fields
        });
    }

    Console.WriteLine("Blog setup complete!");
    return new Dictionary<string, string>
    {
        ["postsId"] = posts["id"]?.ToString() ?? "",
        ["categoriesId"] = categories["id"]?.ToString() ?? ""
    };
}
```

### Example 2: Migrate Collections

```csharp
async Task MigrateCollectionsAsync()
{
    // Export existing collections
    var existingCollections = await pb.Collections.GetFullListAsync();
    
    // Modify collections
    var modifiedCollections = existingCollections
        .OfType<Dictionary<string, object?>>()
        .Select(collection =>
        {
            if (collection["name"]?.ToString() == "posts")
            {
                var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
                    ?? new List<Dictionary<string, object?>>();
                
                // Add new field
                fields.Add(new Dictionary<string, object?>
                {
                    ["name"] = "views",
                    ["type"] = "number",
                    ["required"] = false,
                    ["options"] = new Dictionary<string, object?> { ["min"] = 0 }
                });
                
                collection["fields"] = fields;
                
                // Update rules
                collection["updateRule"] = "@request.auth.id != \"\" || published = true";
            }
            return collection;
        })
        .ToList();
    
    // Import modified collections
    await pb.Collections.ImportCollectionsAsync(modifiedCollections, deleteMissing: false);
}
```

### Example 3: Clone Collection

```csharp
async Task<Dictionary<string, object?>> CloneCollectionAsync(string sourceName, string targetName)
{
    // Get source collection
    var source = await pb.Collections.GetOneAsync(sourceName);
    
    // Create new collection based on source
    var clone = new Dictionary<string, object?>(source)
    {
        ["name"] = targetName,
        ["system"] = false
    };
    
    // Remove system fields
    var fields = (clone["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
        ?? new List<Dictionary<string, object?>>();
    
    fields = fields.Where(f => f["system"]?.ToString() != "true").ToList();
    clone["fields"] = fields;
    
    // Remove ID and timestamps
    clone.Remove("id");
    clone.Remove("created");
    clone.Remove("updated");
    
    // Create cloned collection
    return await pb.Collections.CreateAsync(clone);
}
```

### Example 4: Backup and Restore

```csharp
async Task BackupCollectionsAsync()
{
    // Get all collections
    var collections = await pb.Collections.GetFullListAsync();
    
    // Save to file
    var json = System.Text.Json.JsonSerializer.Serialize(collections, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    
    await File.WriteAllTextAsync("collections_backup.json", json);
    
    Console.WriteLine($"Backed up {collections.Count} collections");
}

async Task RestoreCollectionsAsync()
{
    // Load from file
    var json = await File.ReadAllTextAsync("collections_backup.json");
    var collections = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json);
    
    if (collections != null)
    {
        // Restore
        await pb.Collections.ImportCollectionsAsync(collections, deleteMissing: false);
        
        Console.WriteLine($"Restored {collections.Count} collections");
    }
}
```

### Example 5: Validate Collection Configuration

```csharp
async Task<bool> ValidateCollectionAsync(string name)
{
    try
    {
        var collection = await pb.Collections.GetOneAsync(name);
        
        // Check required fields
        var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
            ?? new List<Dictionary<string, object?>>();
        var hasRequiredFields = fields.Any(f => f["required"]?.ToString() == "true");
        if (!hasRequiredFields)
        {
            Console.WriteLine("Collection has no required fields");
        }
        
        // Check API rules
        var type = collection["type"]?.ToString();
        if (type == "base" && collection["listRule"] == null)
        {
            Console.WriteLine("Base collection has no listRule (superuser only)");
        }
        
        // Check indexes
        var indexes = collection["indexes"] as List<object?> ?? new List<object?>();
        if (indexes.Count == 0)
        {
            Console.WriteLine("Collection has no indexes");
        }
        
        return true;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"Validation failed: {error}");
        return false;
    }
}
```

## Error Handling

```csharp
try
{
    await pb.Collections.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "test",
        ["type"] = "base",
        ["fields"] = new List<Dictionary<string, object?>>()
    });
}
catch (ClientResponseError error)
{
    if (error.Status == 401)
    {
        Console.Error.WriteLine("Not authenticated");
    }
    else if (error.Status == 403)
    {
        Console.Error.WriteLine("Not a superuser");
    }
    else if (error.Status == 400)
    {
        Console.Error.WriteLine($"Validation error: {error.Response}");
    }
    else
    {
        Console.Error.WriteLine($"Unexpected error: {error}");
    }
}
```

## Best Practices

1. **Always Authenticate**: Ensure you're authenticated as a superuser before making requests
2. **Backup Before Import**: Always backup existing collections before using `ImportCollectionsAsync` with `deleteMissing: true`
3. **Validate Schema**: Validate collection schemas before creating/updating
4. **Use Scaffolds**: Use scaffolds as starting points for consistency
5. **Test Rules**: Test API rules thoroughly before deploying to production
6. **Index Important Fields**: Add indexes for frequently queried fields
7. **Document Schemas**: Keep documentation of your collection schemas
8. **Version Control**: Store collection schemas in version control for migration tracking

## Limitations

- **Superuser Only**: All operations require superuser authentication
- **System Collections**: System collections cannot be deleted or renamed
- **View Collections**: Cannot be truncated (they don't store records)
- **Relations**: Collections referenced by others cannot be deleted
- **Field Modifications**: Some field type changes may require data migration

## Related Documentation

- [Collections Guide](./COLLECTIONS.md) - Working with collections and records
- [API Records](./API_RECORDS.md) - Record CRUD operations
- [API Rules and Filters](./API_RULES_AND_FILTERS.md) - Understanding API rules

