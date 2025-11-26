# API Records - C# SDK Documentation

## Overview

The Records API provides comprehensive CRUD (Create, Read, Update, Delete) operations for collection records, along with powerful search, filtering, and authentication capabilities.

**Key Features:**
- Paginated list and search with filtering and sorting
- Single record retrieval with expand support
- Create, update, and delete operations
- Batch operations for multiple records
- Authentication methods (password, OAuth2, OTP)
- Email verification and password reset
- Relation expansion up to 6 levels deep
- Field selection and excerpt modifiers

**Backend Endpoints:**
- `GET /api/collections/{collection}/records` - List records
- `GET /api/collections/{collection}/records/{id}` - View record
- `POST /api/collections/{collection}/records` - Create record
- `PATCH /api/collections/{collection}/records/{id}` - Update record
- `DELETE /api/collections/{collection}/records/{id}` - Delete record
- `POST /api/batch` - Batch operations

## CRUD Operations

### List/Search Records

Returns a paginated records list with support for sorting, filtering, and expansion.

```csharp
using Bosbase;

var pb = new BosbaseClient("http://127.0.0.1:8090");

// Basic list with pagination
var result = await pb.Collection("posts").GetListAsync(1, 50);

Console.WriteLine(result["page"]);        // 1
Console.WriteLine(result["perPage"]);   // 50
Console.WriteLine(result["totalItems"]); // 150
Console.WriteLine(result["totalPages"]); // 3
Console.WriteLine(result["items"]);      // List of records
```

#### Advanced List with Filtering and Sorting

```csharp
// Filter and sort
var result = await pb.Collection("posts").GetListAsync(
    page: 1,
    perPage: 50,
    filter: "created >= \"2022-01-01 00:00:00\" && status = \"published\"",
    sort: "-created,title",  // DESC by created, ASC by title
    expand: "author,categories"
);

// Filter with operators
var result2 = await pb.Collection("posts").GetListAsync(
    page: 1,
    perPage: 50,
    filter: "title ~ \"javascript\" && views > 100",
    sort: "-views"
);
```

#### Get Full List

Fetch all records at once (useful for small collections):

```csharp
// Get all records
var allPosts = await pb.Collection("posts").GetFullListAsync(
    sort: "-created",
    filter: "status = \"published\""
);

// With batch size for large collections
var allPosts = await pb.Collection("posts").GetFullListAsync(
    batch: 200,
    sort: "-created"
);
```

#### Get First Matching Record

Get only the first record that matches a filter:

```csharp
var post = await pb.Collection("posts").GetFirstListItemAsync(
    "slug = \"my-post-slug\"",
    expand: "author,categories.tags"
);
```

### View Record

Retrieve a single record by ID:

```csharp
// Basic retrieval
var record = await pb.Collection("posts").GetOneAsync("RECORD_ID");

// With expanded relations
var record = await pb.Collection("posts").GetOneAsync(
    "RECORD_ID",
    expand: "author,categories,tags"
);

// Nested expand
var record = await pb.Collection("comments").GetOneAsync(
    "COMMENT_ID",
    expand: "post.author,user"
);

// Field selection
var record = await pb.Collection("posts").GetOneAsync(
    "RECORD_ID",
    fields: "id,title,content,author.name"
);
```

### Create Record

Create a new record:

```csharp
// Simple create
var record = await pb.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My First Post",
    ["content"] = "Lorem ipsum...",
    ["status"] = "draft"
});

// Create with relations
var record = await pb.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Post",
    ["author"] = "AUTHOR_ID",           // Single relation
    ["categories"] = new[] { "cat1", "cat2" }  // Multiple relation
});

// Create with file upload
var fileAttachment = new FileAttachment
{
    FieldName = "image",
    FileName = "photo.jpg",
    Content = fileBytes,  // byte[]
    ContentType = "image/jpeg"
};

var record = await pb.Collection("posts").CreateAsync(
    new Dictionary<string, object?> { ["title"] = "My Post" },
    files: new[] { fileAttachment }
);

// Create with expand to get related data immediately
var record = await pb.Collection("posts").CreateAsync(
    new Dictionary<string, object?>
    {
        ["title"] = "My Post",
        ["author"] = "AUTHOR_ID"
    },
    expand: "author"
);
```

### Update Record

Update an existing record:

```csharp
// Simple update
var record = await pb.Collection("posts").UpdateAsync("RECORD_ID", new Dictionary<string, object?>
{
    ["title"] = "Updated Title",
    ["status"] = "published"
});

// Update with relations
await pb.Collection("posts").UpdateAsync("RECORD_ID", new Dictionary<string, object?>
{
    ["categories+"] = "NEW_CATEGORY_ID",  // Append
    ["tags-"] = "OLD_TAG_ID"              // Remove
});

// Update with file upload
var fileAttachment = new FileAttachment
{
    FieldName = "image",
    FileName = "newphoto.jpg",
    Content = newFileBytes,
    ContentType = "image/jpeg"
};

var record = await pb.Collection("posts").UpdateAsync(
    "RECORD_ID",
    new Dictionary<string, object?> { ["title"] = "Updated Title" },
    files: new[] { fileAttachment }
);

// Update with expand
var record = await pb.Collection("posts").UpdateAsync(
    "RECORD_ID",
    new Dictionary<string, object?> { ["title"] = "Updated" },
    expand: "author,categories"
);
```

### Delete Record

Delete a record:

```csharp
// Simple delete
await pb.Collection("posts").DeleteAsync("RECORD_ID");

// Note: Returns 204 No Content on success
// Throws error if record doesn't exist or permission denied
```

## Filter Syntax

The filter parameter supports a powerful query syntax:

### Comparison Operators

```csharp
// Equal
filter: "status = \"published\""

// Not equal
filter: "status != \"draft\""

// Greater than / Less than
filter: "views > 100"
filter: "created < \"2023-01-01\""

// Greater/Less than or equal
filter: "age >= 18"
filter: "price <= 99.99"
```

### String Operators

```csharp
// Contains (like)
filter: "title ~ \"javascript\""
// Equivalent to: title LIKE "%javascript%"

// Not contains
filter: "title !~ \"deprecated\""

// Exact match (case-sensitive)
filter: "email = \"user@example.com\""
```

### Array Operators (for multiple relations/files)

```csharp
// Any of / At least one
filter: "tags.id ?= \"TAG_ID\""         // Any tag matches
filter: "tags.name ?~ \"important\""    // Any tag name contains "important"

// All must match
filter: "tags.id = \"TAG_ID\" && tags.id = \"TAG_ID2\""
```

### Logical Operators

```csharp
// AND
filter: "status = \"published\" && views > 100"

// OR
filter: "status = \"published\" || status = \"featured\""

// Parentheses for grouping
filter: "(status = \"published\" || featured = true) && views > 50"
```

### Special Identifiers

```csharp
// Request context (only in API rules, not client filters)
// @request.auth.id, @request.query.*, etc.

// Collection joins
filter: "@collection.users.email = \"test@example.com\""

// Record fields
filter: "author.id = @request.auth.id"
```

### Comments

```csharp
// Single-line comments are supported
filter: "status = \"published\" // Only published posts"
```

## Sorting

Sort records using the `sort` parameter:

```csharp
// Single field (ASC)
sort: "created"

// Single field (DESC)
sort: "-created"

// Multiple fields
sort: "-created,title"  // DESC by created, then ASC by title

// Supported fields
sort: "@random"         // Random order
sort: "@rowid"          // Internal row ID
sort: "id"              // Record ID
sort: "fieldName"       // Any collection field

// Relation field sorting
sort: "author.name"     // Sort by related author's name
```

## Field Selection

Control which fields are returned:

```csharp
// Specific fields
fields: "id,title,content"

// All fields at level
fields: "*"

// Nested field selection
fields: "*,author.name,author.email"

// Excerpt modifier for text fields
fields: "*,content:excerpt(200,true)"
// Returns first 200 characters with ellipsis if truncated

// Combined
fields: "*,content:excerpt(200),author.name,author.email"
```

## Expanding Relations

Expand related records without additional API calls:

```csharp
// Single relation
expand: "author"

// Multiple relations
expand: "author,categories,tags"

// Nested relations (up to 6 levels)
expand: "author.profile,categories.tags"

// Back-relations
expand: "comments_via_post.user"
```

See [Relations Documentation](./RELATIONS.md) for detailed information.

## Pagination Options

```csharp
// Skip total count (faster queries)
var result = await pb.Collection("posts").GetListAsync(
    page: 1,
    perPage: 50,
    skipTotal: true,  // totalItems and totalPages will be -1
    filter: "status = \"published\""
);

// Get Full List with batch processing
var allPosts = await pb.Collection("posts").GetFullListAsync(
    batch: 200,
    sort: "-created"
);
// Processes in batches of 200 to avoid memory issues
```

## Batch Operations

Execute multiple operations in a single transaction:

```csharp
// Create a batch
var batch = pb.CreateBatch();

// Add operations
batch.Collection("posts").Create(new Dictionary<string, object?>
{
    ["title"] = "Post 1",
    ["author"] = "AUTHOR_ID"
});

batch.Collection("posts").Create(new Dictionary<string, object?>
{
    ["title"] = "Post 2",
    ["author"] = "AUTHOR_ID"
});

batch.Collection("tags").Update("TAG_ID", new Dictionary<string, object?>
{
    ["name"] = "Updated Tag"
});

batch.Collection("categories").Delete("CAT_ID");

// Upsert (create or update based on id)
batch.Collection("posts").Upsert(new Dictionary<string, object?>
{
    ["id"] = "EXISTING_ID",
    ["title"] = "Updated Post"
});

// Send batch request
var results = await batch.SendAsync();

// Results is a list matching the order of operations
for (int index = 0; index < results.Count; index++)
{
    var result = results[index];
    if (result.TryGetValue("status", out var statusObj) && 
        Convert.ToInt32(statusObj) >= 400)
    {
        Console.Error.WriteLine($"Operation {index} failed: {result["body"]}");
    }
    else
    {
        Console.WriteLine($"Operation {index} succeeded: {result["body"]}");
    }
}
```

**Note**: Batch operations must be enabled in Dashboard > Settings > Application.

## Authentication Actions

### List Auth Methods

Get available authentication methods for a collection:

```csharp
var methods = await pb.Collection("users").ListAuthMethodsAsync();

var password = methods["password"] as Dictionary<string, object?>;
var oauth2 = methods["oauth2"] as Dictionary<string, object?>;
var otp = methods["otp"] as Dictionary<string, object?>;
var mfa = methods["mfa"] as Dictionary<string, object?>;

Console.WriteLine(password?["enabled"]);      // true/false
Console.WriteLine(oauth2?["enabled"]);       // true/false
Console.WriteLine(oauth2?["providers"]);      // List of OAuth2 providers
Console.WriteLine(otp?["enabled"]);          // true/false
Console.WriteLine(mfa?["enabled"]);          // true/false
```

### Auth with Password

```csharp
var authData = await pb.Collection("users").AuthWithPasswordAsync(
    "user@example.com",  // username or email
    "password123"
);

// Auth data is automatically stored in pb.AuthStore
Console.WriteLine(pb.AuthStore.IsValid());    // true
Console.WriteLine(pb.AuthStore.Token);        // JWT token
var record = pb.AuthStore.Record;
Console.WriteLine(record?["id"]);            // User ID

// Access the returned data
Console.WriteLine(authData["token"]);
Console.WriteLine(authData["record"]);

// With expand
var authData = await pb.Collection("users").AuthWithPasswordAsync(
    "user@example.com",
    "password123",
    expand: "profile"
);
```

### Auth with OAuth2

```csharp
// Step 1: Get OAuth2 URL (usually done in UI)
var methods = await pb.Collection("users").ListAuthMethodsAsync();
var oauth2 = methods["oauth2"] as Dictionary<string, object?>;
var providers = oauth2?["providers"] as List<object?>;
var provider = providers?
    .Cast<Dictionary<string, object?>>()
    .FirstOrDefault(p => p["name"]?.ToString() == "google");

// Redirect user to provider["authURL"]
// In a web app: Process.Start(provider["authURL"].ToString());

// Step 2: After redirect, exchange code for token
var authData = await pb.Collection("users").AuthWithOAuth2CodeAsync(
    "google",                    // Provider name
    "AUTHORIZATION_CODE",        // From redirect URL
    provider?["codeVerifier"]?.ToString() ?? "",       // From step 1
    "https://yourapp.com/callback", // Redirect URL
    createData: new Dictionary<string, object?>      // Optional data for new accounts
    {
        ["name"] = "John Doe"
    }
);
```

### Auth with OTP (One-Time Password)

```csharp
// Step 1: Request OTP
var otpRequest = await pb.Collection("users").RequestOtpAsync("user@example.com");
// Returns: { "otpId": "..." }

// Step 2: User enters OTP from email
// Step 3: Authenticate with OTP
var authData = await pb.Collection("users").AuthWithOtpAsync(
    otpRequest["otpId"]?.ToString() ?? "",
    "123456"  // OTP from email
);
```

### Auth Refresh

Refresh the current auth token and get updated user data:

```csharp
// Refresh auth (useful on page reload)
var authData = await pb.Collection("users").AuthRefreshAsync();

// Check if still valid
if (pb.AuthStore.IsValid())
{
    Console.WriteLine("User is authenticated");
}
else
{
    Console.WriteLine("Token expired or invalid");
}
```

### Email Verification

```csharp
// Request verification email
await pb.Collection("users").RequestVerificationAsync("user@example.com");

// Confirm verification (on verification page)
await pb.Collection("users").ConfirmVerificationAsync("VERIFICATION_TOKEN");
```

### Password Reset

```csharp
// Request password reset email
await pb.Collection("users").RequestPasswordResetAsync("user@example.com");

// Confirm password reset (on reset page)
// Note: This invalidates all previous auth tokens
await pb.Collection("users").ConfirmPasswordResetAsync(
    "RESET_TOKEN",
    "newpassword123",
    "newpassword123"  // Confirm
);
```

### Email Change

```csharp
// Must be authenticated first
await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password");

// Request email change
await pb.Collection("users").RequestEmailChangeAsync("newemail@example.com");

// Confirm email change (on confirmation page)
// Note: This invalidates all previous auth tokens
await pb.Collection("users").ConfirmEmailChangeAsync(
    "EMAIL_CHANGE_TOKEN",
    "currentpassword"
);
```

### Impersonate (Superuser Only)

Generate a token to authenticate as another user:

```csharp
// Must be authenticated as superuser
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Impersonate a user
var impersonateClient = await pb.Collection("users").ImpersonateAsync("USER_ID", 3600);
// Returns a new client instance with impersonated user's token

// Use the impersonated client
var posts = await impersonateClient.Collection("posts").GetFullListAsync();

// Access the token
Console.WriteLine(impersonateClient.AuthStore.Token);
Console.WriteLine(impersonateClient.AuthStore.Record);
```

## Complete Examples

### Example 1: Blog Post Search with Filters

```csharp
async Task<List<object?>> SearchPostsAsync(string query, string? categoryId, int? minViews)
{
    var filter = $"title ~ \"{query}\" || content ~ \"{query}\"";
    
    if (!string.IsNullOrEmpty(categoryId))
    {
        filter += $" && categories.id ?= \"{categoryId}\"";
    }
    
    if (minViews.HasValue)
    {
        filter += $" && views >= {minViews.Value}";
    }
    
    var result = await pb.Collection("posts").GetListAsync(
        page: 1,
        perPage: 20,
        filter: filter,
        sort: "-created",
        expand: "author,categories"
    );
    
    if (result.TryGetValue("items", out var itemsObj) && itemsObj is List<object?> items)
    {
        return items;
    }
    
    return new List<object?>();
}
```

### Example 2: User Dashboard with Related Content

```csharp
async Task<Dictionary<string, object?>> GetUserDashboardAsync(string userId)
{
    // Get user's posts
    var postsResult = await pb.Collection("posts").GetListAsync(
        page: 1,
        perPage: 10,
        filter: $"author = \"{userId}\"",
        sort: "-created",
        expand: "categories"
    );
    
    // Get user's comments
    var commentsResult = await pb.Collection("comments").GetListAsync(
        page: 1,
        perPage: 10,
        filter: $"user = \"{userId}\"",
        sort: "-created",
        expand: "post"
    );
    
    return new Dictionary<string, object?>
    {
        ["posts"] = postsResult.TryGetValue("items", out var postsItems) ? postsItems : new List<object?>(),
        ["comments"] = commentsResult.TryGetValue("items", out var commentsItems) ? commentsItems : new List<object?>()
    };
}
```

### Example 3: Advanced Filtering

```csharp
// Complex filter example
var result = await pb.Collection("posts").GetListAsync(
    page: 1,
    perPage: 50,
    filter: @"
        (status = ""published"" || featured = true) &&
        created >= ""2023-01-01"" &&
        (tags.id ?= ""important"" || categories.id = ""news"") &&
        views > 100 &&
        author.email != """"
    ",
    sort: "-views,created",
    expand: "author.profile,tags,categories",
    fields: "*,content:excerpt(300),author.name,author.email"
);
```

### Example 4: Batch Create Posts

```csharp
async Task<List<object?>> CreateMultiplePostsAsync(List<Dictionary<string, object?>> postsData)
{
    var batch = pb.CreateBatch();
    
    foreach (var postData in postsData)
    {
        batch.Collection("posts").Create(postData);
    }
    
    var results = await batch.SendAsync();
    
    // Check for failures
    var failures = results?
        .Select((result, index) => new { index, result })
        .Where(r => r.result.TryGetValue("status", out var status) && 
                    Convert.ToInt32(status) >= 400)
        .ToList();
    
    if (failures != null && failures.Any())
    {
        Console.Error.WriteLine($"Some posts failed to create: {failures.Count}");
    }
    
    return results?
        .Select(r => r.TryGetValue("body", out var body) ? body : null)
        .ToList() ?? new List<object?>();
}
```

### Example 5: Pagination Helper

```csharp
async Task<List<object?>> GetAllRecordsPaginatedAsync(
    string collectionName, 
    Dictionary<string, object?>? options = null)
{
    var allRecords = new List<object?>();
    var page = 1;
    var hasMore = true;
    
    while (hasMore)
    {
        var queryParams = new Dictionary<string, object?>(options ?? new Dictionary<string, object?>());
        var result = await pb.Collection(collectionName).GetListAsync(
            page: page,
            perPage: 500,
            skipTotal: true,  // Skip count for performance
            query: queryParams
        );
        
        if (result.TryGetValue("items", out var itemsObj) && itemsObj is List<object?> items)
        {
            allRecords.AddRange(items);
            hasMore = items.Count == 500;
        }
        else
        {
            hasMore = false;
        }
        
        page++;
    }
    
    return allRecords;
}
```

### Example 6: OAuth2 Authentication Flow

```csharp
async Task HandleOAuth2LoginAsync(string providerName)
{
    // Get OAuth2 methods
    var methods = await pb.Collection("users").ListAuthMethodsAsync();
    var oauth2 = methods["oauth2"] as Dictionary<string, object?>;
    var providers = oauth2?["providers"] as List<object?>;
    var provider = providers?
        .Cast<Dictionary<string, object?>>()
        .FirstOrDefault(p => p["name"]?.ToString() == providerName);
    
    if (provider == null)
    {
        throw new Exception($"Provider {providerName} not available");
    }
    
    // Store code verifier for later (in a real app, use secure storage)
    // sessionStorage["oauth2_code_verifier"] = provider["codeVerifier"];
    // sessionStorage["oauth2_provider"] = providerName;
    
    // Redirect to provider (in a web app)
    // Process.Start(provider["authURL"].ToString());
}

// After redirect callback
async Task HandleOAuth2CallbackAsync(string code)
{
    // Retrieve stored values (in a real app, from secure storage)
    // var codeVerifier = sessionStorage["oauth2_code_verifier"];
    // var provider = sessionStorage["oauth2_provider"];
    var redirectUrl = "https://yourapp.com/auth/callback";
    
    try
    {
        var authData = await pb.Collection("users").AuthWithOAuth2CodeAsync(
            "google",  // provider
            code,
            "",  // codeVerifier (retrieved from storage)
            redirectUrl,
            createData: new Dictionary<string, object?>
            {
                // Optional: data for new account creation
                ["name"] = "User"
            }
        );
        
        // Success! User is now authenticated
        // Redirect to dashboard
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"OAuth2 authentication failed: {error}");
    }
}
```

## Error Handling

```csharp
try
{
    var record = await pb.Collection("posts").CreateAsync(new Dictionary<string, object?>
    {
        ["title"] = "My Post"
    });
}
catch (ClientResponseError error)
{
    if (error.Status == 400)
    {
        // Validation error
        Console.Error.WriteLine($"Validation errors: {error.Response}");
    }
    else if (error.Status == 403)
    {
        // Permission denied
        Console.Error.WriteLine("Access denied");
    }
    else if (error.Status == 404)
    {
        // Not found
        Console.Error.WriteLine("Collection or record not found");
    }
    else
    {
        Console.Error.WriteLine($"Unexpected error: {error}");
    }
}
```

## Best Practices

1. **Use Pagination**: Always use pagination for large datasets
2. **Skip Total When Possible**: Use `skipTotal: true` for better performance when you don't need counts
3. **Batch Operations**: Use batch for multiple operations to reduce round trips
4. **Field Selection**: Only request fields you need to reduce payload size
5. **Expand Wisely**: Only expand relations you actually use
6. **Filter Before Sort**: Apply filters before sorting for better performance
7. **Cache Auth Tokens**: Auth tokens are automatically stored in `AuthStore`, no need to manually cache
8. **Handle Errors**: Always handle authentication and permission errors gracefully

## Related Documentation

- [Collections](./COLLECTIONS.md) - Collection configuration
- [Relations](./RELATIONS.md) - Working with relations
- [API Rules and Filters](./API_RULES_AND_FILTERS.md) - Filter syntax details
- [Authentication](./AUTHENTICATION.md) - Detailed authentication guide
- [Files](./FILES.md) - File uploads and handling

