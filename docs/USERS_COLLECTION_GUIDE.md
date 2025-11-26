# Built-in Users Collection Guide - C# SDK Documentation

This guide explains how to use the built-in `users` collection for authentication, registration, and API rules. **The `users` collection is automatically created when BosBase is initialized and does not need to be created manually.**

## Table of Contents

1. [Overview](#overview)
2. [Users Collection Structure](#users-collection-structure)
3. [User Registration](#user-registration)
4. [User Login/Authentication](#user-loginauthentication)
5. [API Rules and Filters with Users](#api-rules-and-filters-with-users)
6. [Using Users with Other Collections](#using-users-with-other-collections)
7. [Complete Examples](#complete-examples)

---

## Overview

The `users` collection is a **built-in auth collection** that is automatically created when BosBase starts. It has:

- **Collection ID**: `_pb_users_auth_`
- **Collection Name**: `users`
- **Type**: `auth` (authentication collection)
- **Purpose**: User accounts, authentication, and authorization

**Important**: 
- ✅ **DO NOT** create a new `users` collection manually
- ✅ **DO** use the existing built-in `users` collection
- ✅ The collection already has proper API rules configured
- ✅ It supports password, OAuth2, and OTP authentication

### Getting Users Collection Information

```csharp
// Get the users collection details
var usersCollection = await client.Collections.GetOneAsync("users");
// or by ID
var usersCollection = await client.Collections.GetOneAsync("_pb_users_auth_");

Console.WriteLine($"Collection ID: {usersCollection["id"]}");
Console.WriteLine($"Collection Name: {usersCollection["name"]}");
Console.WriteLine($"Collection Type: {usersCollection["type"]}");
```

---

## Users Collection Structure

### System Fields (Automatically Created)

These fields are automatically added to all auth collections (including `users`):

| Field Name | Type | Description | Required | Hidden |
|------------|------|-------------|----------|--------|
| `id` | text | Unique record identifier | Yes | No |
| `email` | email | User email address | Yes* | No |
| `username` | text | Username (optional, if enabled) | No* | No |
| `password` | password | Hashed password | Yes* | Yes |
| `tokenKey` | text | Token key for auth tokens | Yes | Yes |
| `emailVisibility` | bool | Whether email is visible to others | No | No |
| `verified` | bool | Whether email is verified | No | No |
| `created` | date | Record creation timestamp | Yes | No |
| `updated` | date | Last update timestamp | Yes | No |

*Required based on authentication method configuration (password auth, username auth, etc.)

### Custom Fields (Pre-configured)

The built-in `users` collection includes these custom fields:

| Field Name | Type | Description | Required |
|------------|------|-------------|----------|
| `name` | text | User's display name | No (max: 255 characters) |
| `avatar` | file | User avatar image | No (max: 1 file, images only) |

### Default API Rules

The `users` collection comes with these default API rules:

```csharp
{
    "listRule": "id = @request.auth.id",    // Users can only list themselves
    "viewRule": "id = @request.auth.id",   // Users can only view themselves
    "createRule": "",                       // Anyone can register (public)
    "updateRule": "id = @request.auth.id", // Users can only update themselves
    "deleteRule": "id = @request.auth.id"  // Users can only delete themselves
}
```

**Understanding the Rules:**

1. **`listRule: "id = @request.auth.id"`**
   - Users can only see their own record when listing
   - If not authenticated, returns empty list (not an error)
   - Superusers can see all users

2. **`viewRule: "id = @request.auth.id"`**
   - Users can only view their own record
   - If trying to view another user, returns 404
   - Superusers can view any user

3. **`createRule: ""`** (empty string)
   - **Public registration** - Anyone can create a user account
   - No authentication required
   - This enables self-registration

4. **`updateRule: "id = @request.auth.id"`**
   - Users can only update their own record
   - Prevents users from modifying other users' data
   - Superusers can update any user

5. **`deleteRule: "id = @request.auth.id"`**
   - Users can only delete their own account
   - Prevents users from deleting other users
   - Superusers can delete any user

**Note**: These rules ensure user privacy and security. Users can only access and modify their own data unless they are superusers.

---

## User Registration

### Basic Registration

Users can register by creating a record in the `users` collection. The `createRule` is set to `""` (empty string), meaning **anyone can register**.

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");

// Register a new user
var newUser = await client.Collection("users").CreateAsync(new Dictionary<string, object?>
{
    ["email"] = "user@example.com",
    ["password"] = "securepassword123",
    ["passwordConfirm"] = "securepassword123",
    ["name"] = "John Doe"
});

Console.WriteLine($"User registered: {newUser["id"]}");
Console.WriteLine($"Email: {newUser["email"]}");
```

### Registration with Email Verification

```csharp
// Register user (verification email sent automatically if configured)
var newUser = await client.Collection("users").CreateAsync(new Dictionary<string, object?>
{
    ["email"] = "user@example.com",
    ["password"] = "securepassword123",
    ["passwordConfirm"] = "securepassword123",
    ["name"] = "John Doe"
});

// User will receive verification email
// After clicking link, verified field becomes true
```

### Registration with Username

If username authentication is enabled in the collection settings:

```csharp
var newUser = await client.Collection("users").CreateAsync(new Dictionary<string, object?>
{
    ["email"] = "user@example.com",
    ["username"] = "johndoe",
    ["password"] = "securepassword123",
    ["passwordConfirm"] = "securepassword123",
    ["name"] = "John Doe"
});
```

### Registration with Avatar Upload

```csharp
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("avatar", "/path/to/avatar.jpg", "image/jpeg");
var newUser = await client.Collection("users").CreateAsync(
    new Dictionary<string, object?>
    {
        ["email"] = "user@example.com",
        ["password"] = "securepassword123",
        ["passwordConfirm"] = "securepassword123",
        ["name"] = "John Doe"
    },
    files: new[] { fileAttachment }
);
```

### Check if Email Exists

```csharp
try
{
    var existing = await client.Collection("users").GetFirstListItemAsync(
        "email = \"user@example.com\""
    );
    Console.WriteLine("Email already exists");
}
catch (ClientResponseError ex)
{
    if (ex.Status == 404)
    {
        Console.WriteLine("Email is available");
    }
}
```

---

## User Login/Authentication

### Password Authentication

```csharp
// Login with email and password
var authData = await client.Collection("users").AuthWithPasswordAsync(
    "user@example.com",
    "password123"
);

// Auth data is automatically stored
Console.WriteLine(client.AuthStore.IsValid());  // true
Console.WriteLine(client.AuthStore.Token);      // JWT token
Console.WriteLine(client.AuthStore.Record);      // User record
```

### Login with Username

If username authentication is enabled:

```csharp
var authData = await client.Collection("users").AuthWithPasswordAsync(
    "johndoe",  // username instead of email
    "password123"
);
```

### OAuth2 Authentication

```csharp
// Login with OAuth2 (e.g., Google)
var authData = await client.Collection("users").AuthWithOAuth2Async(new Dictionary<string, object?>
{
    ["provider"] = "google"
});

// If user doesn't exist, account is created automatically
Console.WriteLine(authData.Record["email"]);
```

### OTP Authentication

```csharp
// Step 1: Request OTP
var otpResult = await client.Collection("users").RequestOtpAsync("user@example.com");

// Step 2: Authenticate with OTP code from email
var authData = await client.Collection("users").AuthWithOtpAsync(
    otpResult["otpId"]?.ToString() ?? "",
    "123456" // OTP code from email
);
```

### Check Current Authentication

```csharp
if (client.AuthStore.IsValid())
{
    var user = client.AuthStore.Record;
    Console.WriteLine($"Logged in as: {user["email"]}");
    Console.WriteLine($"User ID: {user["id"]}");
    Console.WriteLine($"Name: {user["name"]}");
}
else
{
    Console.WriteLine("Not authenticated");
}
```

### Refresh Auth Token

```csharp
// Refresh the authentication token
await client.Collection("users").AuthRefreshAsync();
```

### Logout

```csharp
client.AuthStore.Clear();
```

### Get Current User

```csharp
var currentUser = client.AuthStore.Record;
if (currentUser != null)
{
    Console.WriteLine($"Current user: {currentUser["email"]}");
    Console.WriteLine($"User ID: {currentUser["id"]}");
    Console.WriteLine($"Name: {currentUser["name"]}");
    Console.WriteLine($"Verified: {currentUser["verified"]}");
}
```

---

## API Rules and Filters with Users

### Understanding @request.auth

The `@request.auth` identifier provides access to the currently authenticated user's data in API rules and filters.

**Available Properties:**
- `@request.auth.id` - User's record ID
- `@request.auth.email` - User's email
- `@request.auth.username` - User's username (if enabled)
- `@request.auth.*` - Any field from the user record

### Common API Rule Patterns

#### 1. Require Authentication

```csharp
// Only authenticated users can access
listRule: "@request.auth.id != \"\""
viewRule: "@request.auth.id != \"\""
createRule: "@request.auth.id != \"\""
```

#### 2. Owner-Based Access

```csharp
// Users can only access their own records
viewRule: "author = @request.auth.id"
updateRule: "author = @request.auth.id"
deleteRule: "author = @request.auth.id"
```

#### 3. Public with User-Specific Data

```csharp
// Public can see published, users can see their own
listRule: "@request.auth.id != \"\" && author = @request.auth.id || status = \"published\""
viewRule: "@request.auth.id != \"\" && author = @request.auth.id || status = \"published\""
```

#### 4. Role-Based Access (if you add a role field)

```csharp
// Assuming you add a 'role' select field to users collection
listRule: "@request.auth.id != \"\" && @request.auth.role = \"admin\""
updateRule: "@request.auth.role = \"admin\" || author = @request.auth.id"
```

#### 5. Verified Users Only

```csharp
// Only verified users can create
createRule: "@request.auth.id != \"\" && @request.auth.verified = true"
```

### Setting API Rules for Other Collections

When creating collections that relate to users:

```csharp
// Create posts collection with user-based rules
var postsCollection = await client.Collections.CreateBaseAsync("posts", new Dictionary<string, object?>
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
            ["collectionId"] = "_pb_users_auth_", // Reference to users collection
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
    // Public can see published posts, users can see their own
    ["listRule"] = "@request.auth.id != \"\" && author = @request.auth.id || status = \"published\"",
    ["viewRule"] = "@request.auth.id != \"\" && author = @request.auth.id || status = \"published\"",
    // Only authenticated users can create
    ["createRule"] = "@request.auth.id != \"\"",
    // Only authors can update their posts
    ["updateRule"] = "author = @request.auth.id",
    // Only authors can delete their posts
    ["deleteRule"] = "author = @request.auth.id"
});
```

### Using Filters with Users

```csharp
// Get posts by current user
var myPosts = await client.Collection("posts").GetListAsync(1, 20, 
    filter: "author = @request.auth.id"
);

// Get posts by verified users only
var verifiedPosts = await client.Collection("posts").GetListAsync(1, 20, 
    filter: "author.verified = true",
    expand: "author"
);

// Get posts where author name contains "John"
var posts = await client.Collection("posts").GetListAsync(1, 20, 
    filter: "author.name ~ \"John\"",
    expand: "author"
);
```

---

## Using Users with Other Collections

### Creating Relations to Users

When creating collections that need to reference users:

```csharp
// Create a posts collection with author relation
var postsCollection = await client.Collections.CreateBaseAsync("posts", new Dictionary<string, object?>
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
        },
        new Dictionary<string, object?>
        {
            ["name"] = "author",
            ["type"] = "relation",
            ["collectionId"] = "_pb_users_auth_", // Users collection ID
            ["maxSelect"] = 1,
            ["required"] = true
        }
    }
});
```

### Creating Records with User Relations

```csharp
// Authenticate first
await client.Collection("users").AuthWithPasswordAsync("user@example.com", "password");

// Create a post with current user as author
var post = await client.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My First Post",
    ["author"] = client.AuthStore.Record["id"]?.ToString() // Current user's ID
});
```

### Querying with User Relations

```csharp
// Get posts with author information
var posts = await client.Collection("posts").GetListAsync(1, 20, expand: "author");

if (posts["items"] is List<object?> items)
{
    foreach (var item in items)
    {
        if (item is Dictionary<string, object?> post)
        {
            Console.WriteLine($"Post: {post["title"]}");
            var expand = post["expand"] as Dictionary<string, object?>;
            var author = expand?["author"] as Dictionary<string, object?>;
            Console.WriteLine($"Author: {author?["name"]}");
            Console.WriteLine($"Author Email: {author?["email"]}");
        }
    }
}

// Filter posts by author
var userPosts = await client.Collection("posts").GetListAsync(1, 20, 
    filter: "author = \"USER_ID\"",
    expand: "author"
);
```

### Updating User Profile

```csharp
// Users can update their own profile
await client.Collection("users").UpdateAsync(client.AuthStore.Record["id"]?.ToString() ?? "", new Dictionary<string, object?>
{
    ["name"] = "Updated Name"
});

// Update with avatar
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("avatar", "/path/to/new-avatar.jpg", "image/jpeg");
await client.Collection("users").UpdateAsync(
    client.AuthStore.Record["id"]?.ToString() ?? "",
    new Dictionary<string, object?> { ["name"] = "New Name" },
    files: new[] { fileAttachment }
);
```

---

## Complete Examples

### Example 1: User Registration and Login Flow

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");

async Task RegisterAndLogin()
{
    try
    {
        // 1. Register new user
        var newUser = await client.Collection("users").CreateAsync(new Dictionary<string, object?>
        {
            ["email"] = "newuser@example.com",
            ["password"] = "securepassword123",
            ["passwordConfirm"] = "securepassword123",
            ["name"] = "New User"
        });
        
        Console.WriteLine($"Registration successful: {newUser["id"]}");
        
        // 2. Login with credentials
        var authData = await client.Collection("users").AuthWithPasswordAsync(
            "newuser@example.com",
            "securepassword123"
        );
        
        Console.WriteLine("Login successful");
        Console.WriteLine($"Token: {authData.Token}");
        Console.WriteLine($"User: {authData.Record["email"]}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (ex is ClientResponseError error && error.Data != null)
        {
            Console.Error.WriteLine($"Validation errors: {error.Data}");
        }
    }
}

await RegisterAndLogin();
```

### Example 2: Creating User-Related Collections

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");

// Authenticate as superuser to create collections
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "adminpassword");

async Task SetupUserRelatedCollections()
{
    // Create posts collection linked to users
    var postsCollection = await client.Collections.CreateBaseAsync("posts", new Dictionary<string, object?>
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
                ["collectionId"] = "_pb_users_auth_", // Link to users
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
        // API rules using users collection
        ["listRule"] = "@request.auth.id != \"\" && author = @request.auth.id || status = \"published\"",
        ["viewRule"] = "@request.auth.id != \"\" && author = @request.auth.id || status = \"published\"",
        ["createRule"] = "@request.auth.id != \"\"",
        ["updateRule"] = "author = @request.auth.id",
        ["deleteRule"] = "author = @request.auth.id"
    });
    
    Console.WriteLine("Collections created successfully");
}

await SetupUserRelatedCollections();
```

### Example 3: User Creates and Manages Their Posts

```csharp
using Bosbase;

var client = new BosbaseClient("http://localhost:8090");

async Task UserPostManagement()
{
    // 1. User logs in
    await client.Collection("users").AuthWithPasswordAsync("user@example.com", "password");
    var userId = client.AuthStore.Record["id"]?.ToString() ?? "";
    
    // 2. User creates a post
    var post = await client.Collection("posts").CreateAsync(new Dictionary<string, object?>
    {
        ["title"] = "My First Post",
        ["content"] = "This is my content",
        ["author"] = userId,
        ["status"] = "draft"
    });
    
    Console.WriteLine($"Post created: {post["id"]}");
    
    // 3. User lists their own posts
    var myPosts = await client.Collection("posts").GetListAsync(1, 20, 
        filter: $"author = \"{userId}\"",
        sort: "-created"
    );
    
    Console.WriteLine($"My posts: {(myPosts["items"] as List<object?>)?.Count ?? 0}");
    
    // 4. User updates their post
    await client.Collection("posts").UpdateAsync(post["id"]?.ToString() ?? "", new Dictionary<string, object?>
    {
        ["title"] = "Updated Title",
        ["status"] = "published"
    });
    
    // 5. User views their post with author info
    var updatedPost = await client.Collection("posts").GetOneAsync(post["id"]?.ToString() ?? "", expand: "author");
    
    var expand = updatedPost["expand"] as Dictionary<string, object?>;
    var author = expand?["author"] as Dictionary<string, object?>;
    Console.WriteLine($"Post author: {author?["name"]}");
    
    // 6. User deletes their post
    await client.Collection("posts").DeleteAsync(post["id"]?.ToString() ?? "");
    
    Console.WriteLine("Post deleted");
}

await UserPostManagement();
```

---

## Best Practices

1. **Always use the built-in `users` collection** - Don't create a new one
2. **Use `_pb_users_auth_` as collectionId** when creating relations
3. **Check authentication** before user-specific operations
4. **Use `@request.auth.id`** in API rules for user-based access control
5. **Expand user relations** when you need user information
6. **Respect emailVisibility** - Don't expose emails unless user allows it
7. **Handle verification** - Check `verified` field for email verification status
8. **Use proper error handling** for registration/login failures

---

## Common Patterns

### Pattern 1: Owner-Only Access

```csharp
// Users can only access their own records
updateRule: "author = @request.auth.id"
deleteRule: "author = @request.auth.id"
```

### Pattern 2: Public Read, Authenticated Write

```csharp
listRule: "status = \"published\" || author = @request.auth.id"
viewRule: "status = \"published\" || author = @request.auth.id"
createRule: "@request.auth.id != \"\""
```

### Pattern 3: Verified Users Only

```csharp
createRule: "@request.auth.id != \"\" && @request.auth.verified = true"
```

### Pattern 4: Filter by Current User

```csharp
var myRecords = await client.Collection("posts").GetListAsync(1, 20, 
    filter: $"author = \"{client.AuthStore.Record["id"]}\""
);
```

---

This guide covers all essential operations with the built-in `users` collection. Remember: **always use the existing `users` collection, never create a new one manually.**

## Related Documentation

- [Authentication](./AUTHENTICATION.md) - Authentication methods
- [API Rules](./api-rules.md) - API rules configuration
- [Relations](./RELATIONS.md) - Working with relations

