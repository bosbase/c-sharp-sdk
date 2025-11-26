# API Rules and Filters - C# SDK Documentation

## Overview

API Rules are your collection access controls and data filters. They control who can perform actions on your collections and what data they can access.

Each collection has 5 rules, corresponding to specific API actions:
- `listRule` - Controls who can list records
- `viewRule` - Controls who can view individual records
- `createRule` - Controls who can create records
- `updateRule` - Controls who can update records
- `deleteRule` - Controls who can delete records

Auth collections have an additional `manageRule` that allows one user to fully manage another user's data.

## Rule Values

Each rule can be set to:

- **`null` (locked)** - Only authorized superusers can perform the action (default)
- **Empty string `""`** - Anyone can perform the action (superusers, authenticated users, and guests)
- **Non-empty string** - Only users that satisfy the filter expression can perform the action

## Important Notes

1. **Rules act as filters**: API Rules also act as record filters. For example, setting `listRule` to `status = "active"` will only return active records.
2. **HTTP Status Codes**: 
   - `200` with empty items for unsatisfied `listRule`
   - `400` for unsatisfied `createRule`
   - `404` for unsatisfied `viewRule`, `updateRule`, `deleteRule`
   - `403` for locked rules when not a superuser
3. **Superuser bypass**: API Rules are ignored when the action is performed by an authorized superuser.

## Setting Rules via SDK

### C# SDK

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Create collection with rules
var collection = await pb.Collections.CreateBaseAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "title", ["type"] = "text", ["required"] = true },
        new Dictionary<string, object?>
        {
            ["name"] = "status",
            ["type"] = "select",
            ["options"] = new Dictionary<string, object?> { ["values"] = new[] { "draft", "published" } },
            ["maxSelect"] = 1
        },
        new Dictionary<string, object?>
        {
            ["name"] = "author",
            ["type"] = "relation",
            ["options"] = new Dictionary<string, object?> { ["collectionId"] = "users" },
            ["maxSelect"] = 1
        }
    },
    ["listRule"] = "@request.auth.id != \"\" || status = \"published\"",
    ["viewRule"] = "@request.auth.id != \"\" || status = \"published\"",
    ["createRule"] = "@request.auth.id != \"\"",
    ["updateRule"] = "author = @request.auth.id || @request.auth.role = \"admin\"",
    ["deleteRule"] = "author = @request.auth.id || @request.auth.role = \"admin\""
});

// Update rules
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["listRule"] = "@request.auth.id != \"\" && (status = \"published\" || status = \"draft\")"
});

// Remove rule (set to empty string for public access)
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["listRule"] = ""  // Anyone can list
});

// Lock rule (set to null for superuser only)
await pb.Collections.UpdateAsync("articles", new Dictionary<string, object?>
{
    ["deleteRule"] = null  // Only superusers can delete
});
```

## Filter Syntax

The syntax follows: `OPERAND OPERATOR OPERAND`

### Operators

**Comparison Operators:**
- `=` - Equal
- `!=` - NOT equal
- `>` - Greater than
- `>=` - Greater than or equal
- `<` - Less than
- `<=` - Less than or equal

**String Operators:**
- `~` - Like/Contains (auto-wraps right operand in `%` for wildcard match)
- `!~` - NOT Like/Contains

**Array Operators (Any/At least one of):**
- `?=` - Any Equal
- `?!=` - Any NOT equal
- `?>` - Any Greater than
- `?>=` - Any Greater than or equal
- `?<` - Any Less than
- `?<=` - Any Less than or equal
- `?~` - Any Like/Contains
- `?!~` - Any NOT Like/Contains

**Logical Operators:**
- `&&` - AND
- `||` - OR
- `()` - Grouping
- `//` - Single line comments

## Special Identifiers

### @request.*

Access current request data:

**@request.context** - The context where the rule is used
```csharp
listRule: "@request.context != \"oauth2\""
```

**@request.method** - HTTP request method
```csharp
updateRule: "@request.method = \"PATCH\""
```

**@request.headers.*** - Request headers (normalized to lowercase, `-` replaced with `_`)
```csharp
listRule: "@request.headers.x_token = \"test\""
```

**@request.query.*** - Query parameters
```csharp
listRule: "@request.query.page = \"1\""
```

**@request.auth.*** - Current authenticated user
```csharp
listRule: "@request.auth.id != \"\""
viewRule: "@request.auth.email = \"admin@example.com\""
updateRule: "@request.auth.role = \"admin\""
```

**@request.body.*** - Submitted body parameters
```csharp
createRule: "@request.body.title != \"\""
updateRule: "@request.body.status:isset = false"  // Prevent changing status
```

### @collection.*

Target other collections that aren't directly related:

```csharp
// Check if user has access to related collection
listRule: "@request.auth.id != \"\" && @collection.news.categoryId ?= categoryId && @collection.news.author ?= @request.auth.id"

// Using aliases for multiple joins
listRule: @"
  @request.auth.id != "" &&
  @collection.courseRegistrations.user ?= id &&
  @collection.courseRegistrations:auth.user ?= @request.auth.id &&
  @collection.courseRegistrations.courseGroup ?= @collection.courseRegistrations:auth.courseGroup
"
```

### @ Macros (Datetime)

All macros are UTC-based:

- `@now` - Current datetime as string
- `@second` - Current second (0-59)
- `@minute` - Current minute (0-59)
- `@hour` - Current hour (0-23)
- `@weekday` - Current weekday (0-6)
- `@day` - Current day
- `@month` - Current month
- `@year` - Current year
- `@yesterday` - Yesterday datetime
- `@tomorrow` - Tomorrow datetime
- `@todayStart` - Beginning of current day
- `@todayEnd` - End of current day
- `@monthStart` - Beginning of current month
- `@monthEnd` - End of current month
- `@yearStart` - Beginning of current year
- `@yearEnd` - End of current year

**Example:**
```csharp
listRule: "@request.body.publicDate >= @now"
listRule: "created >= @todayStart && created <= @todayEnd"
```

## Field Modifiers

### :isset

Check if a field was submitted in the request (only for `@request.*` fields):

```csharp
// Prevent changing role field
updateRule: "@request.body.role:isset = false"

// Require email field
createRule: "@request.body.email:isset = true"
```

### :length

Check the number of items in an array field (multiple file, select, relation):

```csharp
// Check submitted array length
createRule: "@request.body.tags:length > 1 && @request.body.tags:length <= 5"

// Check existing record array length
listRule: "categories:length = 2"
listRule: "documents:length >= 1"
```

### :each

Apply condition on each item in an array field:

```csharp
// Check if all submitted select options contain "create"
createRule: "@request.body.permissions:each ~ \"create\""

// Check if all existing field values have "pb_" prefix
listRule: "tags:each ~ \"pb_%\""
```

### :lower

Perform case-insensitive string comparisons:

```csharp
// Case-insensitive comparison
listRule: "@request.body.title:lower = \"test\""
updateRule: "status:lower ~ \"active\""
```

## geoDistance Function

Calculate Haversine distance between two geographic points in kilometers:

```csharp
// Offices within 25km of location
listRule: "geoDistance(address.lon, address.lat, 23.32, 42.69) < 25"

// Using request data
listRule: "geoDistance(location.lon, location.lat, @request.query.lon, @request.query.lat) < @request.query.radius"
```

## Common Rule Examples

### Allow Only Authenticated Users

```csharp
listRule: "@request.auth.id != \"\""
viewRule: "@request.auth.id != \"\""
createRule: "@request.auth.id != \"\""
updateRule: "@request.auth.id != \"\""
deleteRule: "@request.auth.id != \"\""
```

### Owner-Based Access

```csharp
viewRule: "@request.auth.id != \"\" && author = @request.auth.id"
updateRule: "@request.auth.id != \"\" && author = @request.auth.id"
deleteRule: "@request.auth.id != \"\" && author = @request.auth.id"
```

### Role-Based Access

```csharp
// Assuming users have a "role" select field
listRule: "@request.auth.id != \"\" && @request.auth.role = \"admin\""
updateRule: "@request.auth.role = \"admin\" || author = @request.auth.id"
```

### Public with Authentication

```csharp
// Public can view published, authenticated can view all
listRule: "@request.auth.id != \"\" || status = \"published\""
viewRule: "@request.auth.id != \"\" || status = \"published\""
```

### Filtered Results

```csharp
// Only show active records
listRule: "status = \"active\""

// Only show records from last 30 days
listRule: "created >= @yesterday"

// Only show records matching user's organization
listRule: "@request.auth.id != \"\" && organization = @request.auth.organization"
```

### Complex Rules

```csharp
// Multiple conditions
listRule: "@request.auth.id != \"\" && (status = \"published\" || status = \"draft\") && author = @request.auth.id"

// Nested relations
listRule: "@request.auth.id != \"\" && author.role = \"staff\""

// Back relations
listRule: "@request.auth.id != \"\" && comments_via_author.id != \"\""
```

## Using Filters in Queries

Filters can also be used in regular queries (not just rules):

```csharp
// List with filter
var result = await pb.Collection("articles").GetListAsync(
    page: 1,
    perPage: 20,
    filter: "status = \"published\" && created >= @todayStart"
);

// Complex filter
var result = await pb.Collection("articles").GetListAsync(
    page: 1,
    perPage: 20,
    filter: "(title ~ \"test\" || description ~ \"test\") && status = \"published\""
);

// Using relation filters
var result = await pb.Collection("articles").GetListAsync(
    page: 1,
    perPage: 20,
    filter: "author.role = \"admin\" && categories.id ?= \"CAT_ID\""
);

// Geo distance filter
var result = await pb.Collection("offices").GetListAsync(
    page: 1,
    perPage: 20,
    filter: "geoDistance(location.lon, location.lat, 23.32, 42.69) < 25"
);
```

## Complete Example

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Create users collection with role field
var users = await pb.Collections.CreateAuthAsync("users", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "name", ["type"] = "text", ["required"] = true },
        new Dictionary<string, object?>
        {
            ["name"] = "role",
            ["type"] = "select",
            ["options"] = new Dictionary<string, object?> { ["values"] = new[] { "user", "staff", "admin" } },
            ["maxSelect"] = 1
        }
    }
});

// Create articles collection with comprehensive rules
var articles = await pb.Collections.CreateBaseAsync("articles", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "title", ["type"] = "text", ["required"] = true },
        new Dictionary<string, object?> { ["name"] = "content", ["type"] = "editor", ["required"] = true },
        new Dictionary<string, object?>
        {
            ["name"] = "status",
            ["type"] = "select",
            ["options"] = new Dictionary<string, object?> { ["values"] = new[] { "draft", "published", "archived" } },
            ["maxSelect"] = 1
        },
        new Dictionary<string, object?>
        {
            ["name"] = "author",
            ["type"] = "relation",
            ["options"] = new Dictionary<string, object?> { ["collectionId"] = users["id"]?.ToString() },
            ["maxSelect"] = 1,
            ["required"] = true
        },
        new Dictionary<string, object?>
        {
            ["name"] = "categories",
            ["type"] = "relation",
            ["options"] = new Dictionary<string, object?> { ["collectionId"] = "categories" },
            ["maxSelect"] = 5
        },
        new Dictionary<string, object?> { ["name"] = "published_at", ["type"] = "date" }
    },
    // Public can see published, authenticated can see their own or published
    ["listRule"] = "@request.auth.id != \"\" && (author = @request.auth.id || status = \"published\") || status = \"published\"",
    
    // Same logic for viewing
    ["viewRule"] = "@request.auth.id != \"\" && (author = @request.auth.id || status = \"published\") || status = \"published\"",
    
    // Only authenticated users can create
    ["createRule"] = "@request.auth.id != \"\"",
    
    // Owners or admins can update, but prevent changing status after publishing
    ["updateRule"] = "@request.auth.id != \"\" && (author = @request.auth.id || @request.auth.role = \"admin\") && (@request.body.status:isset = false || status != \"published\")",
    
    // Only owners or admins can delete
    ["deleteRule"] = "@request.auth.id != \"\" && (author = @request.auth.id || @request.auth.role = \"admin\")"
});

// Authenticate as regular user
await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password123");

// User can create article
var userRecord = pb.AuthStore.Record;
var article = await pb.Collection("articles").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Article",
    ["content"] = "<p>Content</p>",
    ["status"] = "draft",
    ["author"] = userRecord?["id"]?.ToString()
});

// User can update their own article
await pb.Collection("articles").UpdateAsync(article["id"]?.ToString() ?? "", new Dictionary<string, object?>
{
    ["title"] = "Updated Title"
});

// User can list their own articles or published ones
var myArticles = await pb.Collection("articles").GetListAsync(
    page: 1,
    perPage: 20,
    filter: "author = @request.auth.id"
);

// User can also query with additional filters
var published = await pb.Collection("articles").GetListAsync(
    page: 1,
    perPage: 20,
    filter: "status = \"published\" && created >= @todayStart"
);
```

## Related Documentation

- [Collections](./COLLECTIONS.md) - Collection configuration
- [API Records](./API_RECORDS.md) - Using filters in queries

