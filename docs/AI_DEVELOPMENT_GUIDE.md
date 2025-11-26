# AI Development Guide - C# SDK Documentation

This guide provides a comprehensive, fast reference for AI systems to quickly develop applications using the BosBase C# SDK. All examples are production-ready and follow best practices.

## Table of Contents

1. [Authentication](#authentication)
2. [Initialize Collections](#initialize-collections)
3. [Define Collection Fields](#define-collection-fields)
4. [Add Data to Collections](#add-data-to-collections)
5. [Modify Collection Data](#modify-collection-data)
6. [Delete Data from Collections](#delete-data-from-collections)
7. [Query Collection Contents](#query-collection-contents)
8. [Add and Delete Fields from Collections](#add-and-delete-fields-from-collections)
9. [Query Collection Field Information](#query-collection-field-information)
10. [Upload Files](#upload-files)
11. [Query Logs](#query-logs)
12. [Send Emails](#send-emails)

---

## Authentication

### Initialize Client

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");
```

### Password Authentication

```csharp
// Authenticate with email/username and password
var authData = await client.Collection("users").AuthWithPasswordAsync(
    "user@example.com",
    "password123"
);

// Auth data is automatically stored
Console.WriteLine(client.AuthStore.IsValid());  // true
Console.WriteLine(client.AuthStore.Token);      // JWT token
Console.WriteLine(client.AuthStore.Record);      // User record
```

### OAuth2 Authentication

```csharp
// Get OAuth2 providers
var methods = await client.Collection("users").ListAuthMethodsAsync();
// Access available providers from methods

// Authenticate with OAuth2
var authData = await client.Collection("users").AuthWithOAuth2Async(new Dictionary<string, object?>
{
    ["provider"] = "google"
});
```

### OTP Authentication

```csharp
// Request OTP
var otpResponse = await client.Collection("users").RequestVerificationAsync("user@example.com");

// Authenticate with OTP
var authData = await client.Collection("users").AuthWithOtpAsync(
    otpResponse["otpId"]?.ToString() ?? "",
    "123456" // OTP code
);
```

### Check Authentication Status

```csharp
if (client.AuthStore.IsValid())
{
    var record = client.AuthStore.Record;
    Console.WriteLine($"Authenticated as: {record["email"]}");
}
else
{
    Console.WriteLine("Not authenticated");
}
```

### Logout

```csharp
client.AuthStore.Clear();
```

---

## Initialize Collections

### Create Base Collection

```csharp
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

### Create Auth Collection

```csharp
var authCollection = await client.Collections.CreateAuthAsync("users", new Dictionary<string, object?>
{
    ["fields"] = new[]
    {
        new Dictionary<string, object?>
        {
            ["name"] = "name",
            ["type"] = "text",
            ["required"] = false
        }
    }
});
```

### Create View Collection

```csharp
var viewCollection = await client.Collections.CreateViewAsync("published_posts", 
    "SELECT * FROM posts WHERE published = true"
);
```

### Get Collection by ID or Name

```csharp
var collection = await client.Collections.GetOneAsync("posts");
// or by ID
var collection = await client.Collections.GetOneAsync("_pbc_2287844090");
```

---

## Define Collection Fields

### Add Field to Collection

```csharp
var updatedCollection = await client.Collections.UpdateAsync("posts", new Dictionary<string, object?>
{
    ["schema"] = new[]
    {
        // Existing fields...
        new Dictionary<string, object?>
        {
            ["name"] = "content",
            ["type"] = "editor",
            ["required"] = false
        }
    }
});
```

### Common Field Types

```csharp
// Text field
new Dictionary<string, object?>
{
    ["name"] = "title",
    ["type"] = "text",
    ["required"] = true,
    ["min"] = 10,
    ["max"] = 255
}

// Number field
new Dictionary<string, object?>
{
    ["name"] = "views",
    ["type"] = "number",
    ["required"] = false,
    ["min"] = 0
}

// Boolean field
new Dictionary<string, object?>
{
    ["name"] = "published",
    ["type"] = "bool",
    ["required"] = false
}

// Date field
new Dictionary<string, object?>
{
    ["name"] = "published_at",
    ["type"] = "date",
    ["required"] = false
}

// File field
new Dictionary<string, object?>
{
    ["name"] = "avatar",
    ["type"] = "file",
    ["required"] = false,
    ["maxSelect"] = 1,
    ["maxSize"] = 2097152, // 2MB
    ["mimeTypes"] = new[] { "image/jpeg", "image/png" }
}

// Relation field
new Dictionary<string, object?>
{
    ["name"] = "author",
    ["type"] = "relation",
    ["required"] = true,
    ["collectionId"] = "_pbc_users_auth_",
    ["maxSelect"] = 1
}

// Select field
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

---

## Add Data to Collections

### Create Single Record

```csharp
var record = await client.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My First Post",
    ["content"] = "This is the content",
    ["published"] = true
});

Console.WriteLine($"Created record ID: {record["id"]}");
```

### Create Record with File Upload

```csharp
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("image", "/path/to/image.jpg", "image/jpeg");
var record = await client.Collection("posts").CreateAsync(
    new Dictionary<string, object?> { ["title"] = "Post with Image" },
    files: new[] { fileAttachment }
);
```

### Create Record with Relations

```csharp
var record = await client.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Post",
    ["author"] = "user_record_id", // Related record ID
    ["categories"] = new[] { "cat1_id", "cat2_id" } // Multiple relations
});
```

### Batch Create Records

```csharp
var batch = client.CreateBatch();
batch.Collection("posts").Create(new Dictionary<string, object?> { ["title"] = "Post 1" });
batch.Collection("posts").Create(new Dictionary<string, object?> { ["title"] = "Post 2" });
var results = await batch.SendAsync();
```

---

## Modify Collection Data

### Update Single Record

```csharp
var updated = await client.Collection("posts").UpdateAsync("record_id", new Dictionary<string, object?>
{
    ["title"] = "Updated Title",
    ["content"] = "Updated content"
});
```

### Update Record with File

```csharp
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("image", "/path/to/new-image.jpg", "image/jpeg");
var updated = await client.Collection("posts").UpdateAsync(
    "record_id",
    new Dictionary<string, object?> { ["title"] = "Updated Title" },
    files: new[] { fileAttachment }
);
```

### Partial Update

```csharp
// Only update specific fields
var updated = await client.Collection("posts").UpdateAsync("record_id", new Dictionary<string, object?>
{
    ["views"] = 100 // Only update views
});
```

---

## Delete Data from Collections

### Delete Single Record

```csharp
await client.Collection("posts").DeleteAsync("record_id");
```

### Delete Multiple Records

```csharp
// Using batch
var batch = client.CreateBatch();
batch.Collection("posts").Delete("record_id_1");
batch.Collection("posts").Delete("record_id_2");
await batch.SendAsync();
```

### Delete All Records (Truncate)

```csharp
await client.Collections.TruncateAsync("posts");
```

---

## Query Collection Contents

### List Records with Pagination

```csharp
var result = await client.Collection("posts").GetListAsync(1, 50);

var page = result["page"];        // 1
var perPage = result["perPage"];     // 50
var totalItems = result["totalItems"];  // Total count
var items = result["items"] as List<object?>;       // Array of records
```

### Filter Records

```csharp
var result = await client.Collection("posts").GetListAsync(1, 50, 
    filter: "published = true && views > 100",
    sort: "-created"
);
```

### Filter Operators

```csharp
// Equality
filter: "status = \"published\""

// Comparison
filter: "views > 100"
filter: "created >= \"2023-01-01\""

// Text search
filter: "title ~ \"javascript\""

// Multiple conditions
filter: "status = \"published\" && views > 100"
filter: "status = \"draft\" || status = \"pending\""

// Relation filter
filter: "author.id = \"user_id\""
```

### Sort Records

```csharp
// Single field
sort: "-created"  // DESC
sort: "title"     // ASC

// Multiple fields
sort: "-created,title"  // DESC by created, then ASC by title
```

### Expand Relations

```csharp
var result = await client.Collection("posts").GetListAsync(1, 50, expand: "author,categories");

// Access expanded data
if (result["items"] is List<object?> items)
{
    foreach (var item in items)
    {
        if (item is Dictionary<string, object?> post)
        {
            var expand = post["expand"] as Dictionary<string, object?>;
            var author = expand?["author"] as Dictionary<string, object?>;
            Console.WriteLine(author?["name"]);
        }
    }
}
```

### Get Single Record

```csharp
var record = await client.Collection("posts").GetOneAsync("record_id", expand: "author");
```

### Get First Matching Record

```csharp
var record = await client.Collection("posts").GetFirstListItemAsync(
    "slug = \"my-post-slug\"",
    expand: "author"
);
```

### Get All Records

```csharp
var allRecords = await client.Collection("posts").GetFullListAsync(
    filter: "published = true",
    sort: "-created"
);
```

---

## Add and Delete Fields from Collections

### Add Field

```csharp
var collection = await client.Collections.GetOneAsync("posts");
var schema = collection["schema"] as List<object?> ?? new List<object?>();
schema.Add(new Dictionary<string, object?>
{
    ["name"] = "tags",
    ["type"] = "select",
    ["options"] = new Dictionary<string, object?>
    {
        ["values"] = new[] { "tech", "science", "art" }
    }
});

await client.Collections.UpdateAsync("posts", new Dictionary<string, object?>
{
    ["schema"] = schema
});
```

### Update Field

```csharp
// Update field by modifying collection schema
var collection = await client.Collections.GetOneAsync("posts");
// Modify schema and update
```

### Remove Field

```csharp
// Remove field by modifying collection schema
var collection = await client.Collections.GetOneAsync("posts");
// Remove field from schema and update
```

---

## Query Collection Field Information

### Get All Fields for a Collection

```csharp
var collection = await client.Collections.GetOneAsync("posts");
if (collection["schema"] is List<object?> fields)
{
    foreach (var field in fields)
    {
        if (field is Dictionary<string, object?> fieldDict)
        {
            Console.WriteLine($"{fieldDict["name"]} {fieldDict["type"]} {fieldDict["required"]}");
        }
    }
}
```

### Get Collection Schema (Simplified)

```csharp
var schema = await client.Collections.GetSchemaAsync("posts");
if (schema["fields"] is List<object?> fields)
{
    // Array of field info
    foreach (var field in fields)
    {
        Console.WriteLine(field);
    }
}
```

### Get All Collection Schemas

```csharp
var schemas = await client.Collections.GetAllSchemasAsync();
if (schemas["collections"] is List<object?> collections)
{
    foreach (var collection in collections)
    {
        if (collection is Dictionary<string, object?> collDict)
        {
            Console.WriteLine($"{collDict["name"]} {collDict["fields"]}");
        }
    }
}
```

---

## Upload Files

### Upload File with Record Creation

```csharp
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("image", "/path/to/image.jpg", "image/jpeg");
var record = await client.Collection("posts").CreateAsync(
    new Dictionary<string, object?> { ["title"] = "Post Title" },
    files: new[] { fileAttachment }
);
```

### Upload File with Record Update

```csharp
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("image", "/path/to/new-image.jpg", "image/jpeg");
var updated = await client.Collection("posts").UpdateAsync(
    "record_id",
    files: new[] { fileAttachment }
);
```

### Get File URL

```csharp
var record = await client.Collection("posts").GetOneAsync("record_id");
var fileUrl = client.Files.GetUrl(record, record["image"]?.ToString() ?? "");
```

### Get File URL with Options

```csharp
var fileUrl = client.Files.GetUrl(record, record["image"]?.ToString() ?? "", 
    thumb: "100x100",  // Thumbnail
    download: true     // Force download
);
```

### Get Private File Token

```csharp
// For accessing private files
var token = await client.Files.GetTokenAsync();
// Use token in file URL query params
```

---

## Query Logs

### List Logs

```csharp
var logs = await client.Logs.GetListAsync(1, 50);
if (logs["items"] is List<object?> items)
{
    // Array of log entries
    foreach (var log in items)
    {
        Console.WriteLine(log);
    }
}
```

### Filter Logs

```csharp
var errorLogs = await client.Logs.GetListAsync(1, 50, 
    filter: "level > 0 && created >= \"2024-01-01\""
);
```

### Get Log Statistics

```csharp
var stats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
{
    ["filter"] = "level >= 0"
});
```

---

## Send Emails

### Test Email

```csharp
await client.Settings.TestEmailAsync(
    "test@example.com",
    "verification", // template: verification, password-reset, email-change, otp, login-alert
    "_superusers" // collection (optional)
);
```

### Email Templates

- `verification` - Email verification template
- `password-reset` - Password reset template
- `email-change` - Email change confirmation template
- `otp` - One-time password template
- `login-alert` - Login alert template

---

## Complete Example

```csharp
using Bosbase;
using Bosbase.Models;

var client = new BosbaseClient("http://localhost:8090");

// 1. Authenticate
await client.Collection("users").AuthWithPasswordAsync("user@example.com", "password");

// 2. Create collection
var collection = await client.Collections.CreateBaseAsync("posts", new Dictionary<string, object?>
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
        }
    }
});

// 3. Create record
var record = await client.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Post",
    ["content"] = "Post content"
});

// 4. Query records
var posts = await client.Collection("posts").GetListAsync(1, 20, 
    filter: "title ~ \"Post\"",
    sort: "-created"
);

// 5. Update record
await client.Collection("posts").UpdateAsync(record["id"]?.ToString() ?? "", new Dictionary<string, object?>
{
    ["title"] = "Updated Title"
});

// 6. Delete record
await client.Collection("posts").DeleteAsync(record["id"]?.ToString() ?? "");
```

---

## Related Documentation

- [Collections](./COLLECTIONS.md) - Collection management
- [API Records](./API_RECORDS.md) - Record operations
- [Authentication](./AUTHENTICATION.md) - Authentication guide
- [Files](./FILES.md) - File handling

