# Working with Relations - C# SDK Documentation

## Overview

Relations allow you to link records between collections. BosBase supports both single and multiple relations, and provides powerful features for expanding related records and working with back-relations.

**Key Features:**
- Single and multiple relations
- Expand related records without additional requests
- Nested relation expansion (up to 6 levels)
- Back-relations for reverse lookups
- Field modifiers for append/prepend/remove operations

**Relation Field Types:**
- **Single Relation**: Links to one record (MaxSelect <= 1)
- **Multiple Relation**: Links to multiple records (MaxSelect > 1)

**Backend Behavior:**
- Relations are stored as record IDs or arrays of IDs
- Expand only includes relations the client can view (satisfies View API Rule)
- Back-relations use format: `collectionName_via_fieldName`
- Back-relation expand limited to 1000 records per field

## Setting Up Relations

### Creating a Relation Field

```csharp
var collection = await pb.Collections.GetOneAsync("posts");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

fields.Add(new Dictionary<string, object?>
{
    ["name"] = "user",
    ["type"] = "relation",
    ["options"] = new Dictionary<string, object?> { ["collectionId"] = "users" },
    ["maxSelect"] = 1,           // Single relation
    ["required"] = true
});

// Multiple relation field
fields.Add(new Dictionary<string, object?>
{
    ["name"] = "tags",
    ["type"] = "relation",
    ["options"] = new Dictionary<string, object?> { ["collectionId"] = "tags" },
    ["maxSelect"] = 10,          // Multiple relation (max 10)
    ["minSelect"] = 1,           // Minimum 1 required
    ["cascadeDelete"] = false     // Don't delete post when tags deleted
});

await pb.Collections.UpdateAsync("posts", new Dictionary<string, object?>
{
    ["fields"] = fields
});
```

## Creating Records with Relations

### Single Relation

```csharp
// Create a post with a single user relation
var post = await pb.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Post",
    ["user"] = "USER_ID"  // Single relation ID
});
```

### Multiple Relations

```csharp
// Create a post with multiple tags
var post = await pb.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "My Post",
    ["tags"] = new[] { "TAG_ID1", "TAG_ID2", "TAG_ID3" }  // Array of IDs
});
```

### Mixed Relations

```csharp
// Create a comment with both single and multiple relations
var comment = await pb.Collection("comments").CreateAsync(new Dictionary<string, object?>
{
    ["message"] = "Great post!",
    ["post"] = "POST_ID",        // Single relation
    ["user"] = "USER_ID",        // Single relation
    ["tags"] = new[] { "TAG1", "TAG2" }  // Multiple relation
});
```

## Updating Relations

### Replace All Relations

```csharp
// Replace all tags
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["tags"] = new[] { "NEW_TAG1", "NEW_TAG2" }
});
```

### Append Relations (Using + Modifier)

```csharp
// Append tags to existing ones
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["tags+"] = "NEW_TAG_ID"  // Append single tag
});

// Append multiple tags
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["tags+"] = new[] { "TAG_ID1", "TAG_ID2" }  // Append multiple tags
});
```

### Prepend Relations (Using + Prefix)

```csharp
// Prepend tags (tags will appear first)
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["+tags"] = "PRIORITY_TAG"  // Prepend single tag
});

// Prepend multiple tags
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["+tags"] = new[] { "TAG1", "TAG2" }  // Prepend multiple tags
});
```

### Remove Relations (Using - Modifier)

```csharp
// Remove single tag
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["tags-"] = "TAG_ID_TO_REMOVE"
});

// Remove multiple tags
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["tags-"] = new[] { "TAG1", "TAG2" }
});
```

### Complete Example

```csharp
// Get existing post
var post = await pb.Collection("posts").GetOneAsync("POST_ID");
var tags = post["tags"] as List<object?>;
Console.WriteLine($"Tags: {string.Join(", ", tags?.Select(t => t?.ToString()) ?? Array.Empty<string>())}");  // ['tag1', 'tag2']

// Remove one tag, add two new ones
await pb.Collection("posts").UpdateAsync("POST_ID", new Dictionary<string, object?>
{
    ["tags-"] = "tag1",           // Remove
    ["tags+"] = new[] { "tag3", "tag4" }  // Append
});

var updated = await pb.Collection("posts").GetOneAsync("POST_ID");
var updatedTags = updated["tags"] as List<object?>;
Console.WriteLine($"Updated Tags: {string.Join(", ", updatedTags?.Select(t => t?.ToString()) ?? Array.Empty<string>())}");  // ['tag2', 'tag3', 'tag4']
```

## Expanding Relations

The `expand` parameter allows you to fetch related records in a single request, eliminating the need for multiple API calls.

### Basic Expand

```csharp
// Get comment with expanded user
var comment = await pb.Collection("comments").GetOneAsync("COMMENT_ID", expand: "user");

var expand = comment["expand"] as Dictionary<string, object?>;
var user = expand?["user"] as Dictionary<string, object?>;
Console.WriteLine(user?["name"]);  // "John Doe"
Console.WriteLine(comment["user"]);  // Still the ID: "USER_ID"
```

### Expand Multiple Relations

```csharp
// Expand multiple relations (comma-separated)
var comment = await pb.Collection("comments").GetOneAsync("COMMENT_ID", expand: "user,post");

var expand = comment["expand"] as Dictionary<string, object?>;
var user = expand?["user"] as Dictionary<string, object?>;
var post = expand?["post"] as Dictionary<string, object?>;
Console.WriteLine(user?["name"]);   // "John Doe"
Console.WriteLine(post?["title"]);  // "My Post"
```

### Nested Expand (Dot Notation)

You can expand nested relations up to 6 levels deep using dot notation:

```csharp
// Expand post and its tags, and user
var comment = await pb.Collection("comments").GetOneAsync("COMMENT_ID", expand: "user,post.tags");

// Access nested expands
var expand = comment["expand"] as Dictionary<string, object?>;
var post = expand?["post"] as Dictionary<string, object?>;
var postExpand = post?["expand"] as Dictionary<string, object?>;
var tags = postExpand?["tags"] as List<object?>;
// Array of tag records

// Expand even deeper
var postRecord = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "user,comments.user");

// Access: postRecord["expand"]["comments"][0]["expand"]["user"]
```

### Expand with List Requests

```csharp
// List comments with expanded users
var comments = await pb.Collection("comments").GetListAsync(1, 20, expand: "user");

if (comments.TryGetValue("items", out var itemsObj) && itemsObj is List<object?> items)
{
    foreach (var item in items.Cast<Dictionary<string, object?>>())
    {
        Console.WriteLine(item["message"]);
        var expand = item["expand"] as Dictionary<string, object?>;
        var user = expand?["user"] as Dictionary<string, object?>;
        Console.WriteLine(user?["name"]);
    }
}
```

### Expand Single vs Multiple Relations

```csharp
// Single relation - expand.user is an object
var post = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "user");
var expand = post["expand"] as Dictionary<string, object?>;
var user = expand?["user"] as Dictionary<string, object?>;
Console.WriteLine(user != null ? "object" : "null");  // "object"

// Multiple relation - expand.tags is an array
var postWithTags = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "tags");
var expandTags = postWithTags["expand"] as Dictionary<string, object?>;
var tags = expandTags?["tags"] as List<object?>;
Console.WriteLine(tags != null ? "array" : "null");  // "array"
```

### Expand Permissions

**Important**: Only relations that satisfy the related collection's `viewRule` will be expanded. If you don't have permission to view a related record, it won't appear in the expand.

```csharp
// If you don't have view permission for user, expand.user will be undefined
var comment = await pb.Collection("comments").GetOneAsync("COMMENT_ID", expand: "user");

var expand = comment["expand"] as Dictionary<string, object?>;
var user = expand?["user"] as Dictionary<string, object?>;
if (user != null)
{
    Console.WriteLine(user["name"]);
}
else
{
    Console.WriteLine("User not accessible or not found");
}
```

## Back-Relations

Back-relations allow you to query and expand records that reference the current record through a relation field.

### Back-Relation Syntax

The format is: `collectionName_via_fieldName`

- `collectionName`: The collection that contains the relation field
- `fieldName`: The name of the relation field that points to your record

### Example: Posts with Comments

```csharp
// Get a post and expand all comments that reference it
var post = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "comments_via_post");

// comments_via_post is always an array (even if original field is single)
var expand = post["expand"] as Dictionary<string, object?>;
var comments = expand?["comments_via_post"] as List<object?>;
Console.WriteLine($"Comments: {comments?.Count ?? 0}");
// Array of comment records
```

### Back-Relation with Nested Expand

```csharp
// Get post with comments, and expand each comment's user
var post = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "comments_via_post.user");

// Access nested expands
var expand = post["expand"] as Dictionary<string, object?>;
var comments = expand?["comments_via_post"] as List<object?>;
foreach (var commentObj in comments?.Cast<Dictionary<string, object?>>() ?? Enumerable.Empty<Dictionary<string, object?>>())
{
    Console.WriteLine(commentObj["message"]);
    var commentExpand = commentObj["expand"] as Dictionary<string, object?>;
    var user = commentExpand?["user"] as Dictionary<string, object?>;
    Console.WriteLine(user?["name"]);
}
```

### Filtering with Back-Relations

```csharp
// List posts that have at least one comment containing "hello"
var posts = await pb.Collection("posts").GetListAsync(
    page: 1,
    perPage: 20,
    filter: "comments_via_post.message ?~ 'hello'",
    expand: "comments_via_post.user"
);

if (posts.TryGetValue("items", out var itemsObj) && itemsObj is List<object?> items)
{
    foreach (var postObj in items.Cast<Dictionary<string, object?>>())
    {
        Console.WriteLine(postObj["title"]);
        var expand = postObj["expand"] as Dictionary<string, object?>;
        var comments = expand?["comments_via_post"] as List<object?>;
        foreach (var commentObj in comments?.Cast<Dictionary<string, object?>>() ?? Enumerable.Empty<Dictionary<string, object?>>())
        {
            var commentExpand = commentObj["expand"] as Dictionary<string, object?>;
            var user = commentExpand?["user"] as Dictionary<string, object?>;
            Console.WriteLine($"  - {commentObj["message"]} by {user?["name"]}");
        }
    }
}
```

### Back-Relation Caveats

1. **Always Multiple**: Back-relations are always treated as arrays, even if the original relation field is single. This is because one record can be referenced by multiple records.

   ```csharp
   // Even if comments.post is single, comments_via_post is always an array
   var post = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "comments_via_post");
   
   var expand = post["expand"] as Dictionary<string, object?>;
   var comments = expand?["comments_via_post"] as List<object?>;
   // Always an array
   Console.WriteLine(comments != null ? "array" : "null");  // "array"
   ```

2. **UNIQUE Index Exception**: If the relation field has a UNIQUE index constraint, the back-relation will be treated as a single object (not an array).

3. **1000 Record Limit**: Back-relation expand is limited to 1000 records per field. For larger datasets, use separate paginated requests:

   ```csharp
   // Instead of expanding all comments (if > 1000)
   var post = await pb.Collection("posts").GetOneAsync("POST_ID");
   
   // Fetch comments separately with pagination
   var comments = await pb.Collection("comments").GetListAsync(
       page: 1,
       perPage: 100,
       filter: $"post = \"{post["id"]}\"",
       expand: "user",
       sort: "-created"
   );
   ```

## Complete Examples

### Example 1: Blog Post with Author and Tags

```csharp
// Create a blog post with relations
var post = await pb.Collection("posts").CreateAsync(new Dictionary<string, object?>
{
    ["title"] = "Getting Started with BosBase",
    ["content"] = "Lorem ipsum...",
    ["author"] = "AUTHOR_ID",           // Single relation
    ["tags"] = new[] { "tag1", "tag2", "tag3" } // Multiple relation
});

// Retrieve with all relations expanded
var fullPost = await pb.Collection("posts").GetOneAsync(post["id"]?.ToString() ?? "", expand: "author,tags");

Console.WriteLine(fullPost["title"]);
var expand = fullPost["expand"] as Dictionary<string, object?>;
var author = expand?["author"] as Dictionary<string, object?>;
Console.WriteLine($"Author: {author?["name"]}");
Console.WriteLine("Tags:");
var tags = expand?["tags"] as List<object?>;
foreach (var tagObj in tags?.Cast<Dictionary<string, object?>>() ?? Enumerable.Empty<Dictionary<string, object?>>())
{
    Console.WriteLine($"  - {tagObj["name"]}");
}
```

### Example 2: Comment System with Nested Relations

```csharp
// Create a comment on a post
var comment = await pb.Collection("comments").CreateAsync(new Dictionary<string, object?>
{
    ["message"] = "Great article!",
    ["post"] = "POST_ID",
    ["user"] = "USER_ID"
});

// Get post with all comments and their authors
var post = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "author,comments_via_post.user");

Console.WriteLine($"Post: {post["title"]}");
var expand = post["expand"] as Dictionary<string, object?>;
var author = expand?["author"] as Dictionary<string, object?>;
Console.WriteLine($"Author: {author?["name"]}");
var comments = expand?["comments_via_post"] as List<object?>;
Console.WriteLine($"Comments ({comments?.Count ?? 0}):");
foreach (var commentObj in comments?.Cast<Dictionary<string, object?>>() ?? Enumerable.Empty<Dictionary<string, object?>>())
{
    var commentExpand = commentObj["expand"] as Dictionary<string, object?>;
    var user = commentExpand?["user"] as Dictionary<string, object?>;
    Console.WriteLine($"  {user?["name"]}: {commentObj["message"]}");
}
```

### Example 3: Dynamic Tag Management

```csharp
class PostManager
{
    private readonly BosbaseClient _pb;

    public PostManager(BosbaseClient pb)
    {
        _pb = pb;
    }

    public async Task AddTagAsync(string postId, string tagId)
    {
        await _pb.Collection("posts").UpdateAsync(postId, new Dictionary<string, object?>
        {
            ["tags+"] = tagId
        });
    }

    public async Task RemoveTagAsync(string postId, string tagId)
    {
        await _pb.Collection("posts").UpdateAsync(postId, new Dictionary<string, object?>
        {
            ["tags-"] = tagId
        });
    }

    public async Task<Dictionary<string, object?>> GetPostWithTagsAsync(string postId)
    {
        return await _pb.Collection("posts").GetOneAsync(postId, expand: "tags");
    }
}

// Usage
var manager = new PostManager(pb);
await manager.AddTagAsync("POST_ID", "NEW_TAG_ID");
var post = await manager.GetPostWithTagsAsync("POST_ID");
```

### Example 4: Filtering Posts by Tag

```csharp
// Get all posts with a specific tag
var posts = await pb.Collection("posts").GetListAsync(
    page: 1,
    perPage: 50,
    filter: "tags.id ?= \"TAG_ID\"",
    expand: "author,tags",
    sort: "-created"
);

if (posts.TryGetValue("items", out var itemsObj) && itemsObj is List<object?> items)
{
    foreach (var postObj in items.Cast<Dictionary<string, object?>>())
    {
        var expand = postObj["expand"] as Dictionary<string, object?>;
        var author = expand?["author"] as Dictionary<string, object?>;
        Console.WriteLine($"{postObj["title"]} by {author?["name"]}");
    }
}
```

### Example 5: User Dashboard with Related Content

```csharp
async Task GetUserDashboardAsync(string userId)
{
    // Get user with all related content
    var user = await pb.Collection("users").GetOneAsync(userId, expand: "posts_via_author,comments_via_user.post");

    Console.WriteLine($"Dashboard for {user["name"]}");
    var expand = user["expand"] as Dictionary<string, object?>;
    var posts = expand?["posts_via_author"] as List<object?>;
    Console.WriteLine($"\nPosts ({posts?.Count ?? 0}):");
    foreach (var postObj in posts?.Cast<Dictionary<string, object?>>() ?? Enumerable.Empty<Dictionary<string, object?>>())
    {
        Console.WriteLine($"  - {postObj["title"]}");
    }

    Console.WriteLine("\nRecent Comments:");
    var comments = expand?["comments_via_user"] as List<object?>;
    foreach (var commentObj in comments?.Take(5).Cast<Dictionary<string, object?>>() ?? Enumerable.Empty<Dictionary<string, object?>>())
    {
        var commentExpand = commentObj["expand"] as Dictionary<string, object?>;
        var post = commentExpand?["post"] as Dictionary<string, object?>;
        Console.WriteLine($"  On \"{post?["title"]}\": {commentObj["message"]}");
    }
}
```

### Example 6: Complex Nested Expand

```csharp
// Get a post with author, tags, comments, comment authors, and comment reactions
var post = await pb.Collection("posts").GetOneAsync("POST_ID", expand: "author,tags,comments_via_post.user,comments_via_post.reactions_via_comment");

// Access deeply nested data
var expand = post["expand"] as Dictionary<string, object?>;
var comments = expand?["comments_via_post"] as List<object?>;
foreach (var commentObj in comments?.Cast<Dictionary<string, object?>>() ?? Enumerable.Empty<Dictionary<string, object?>>())
{
    var commentExpand = commentObj["expand"] as Dictionary<string, object?>;
    var user = commentExpand?["user"] as Dictionary<string, object?>;
    Console.WriteLine($"{user?["name"]}: {commentObj["message"]}");
    var reactions = commentExpand?["reactions_via_comment"] as List<object?>;
    if (reactions != null)
    {
        Console.WriteLine($"  Reactions: {reactions.Count}");
    }
}
```

## Best Practices

1. **Use Expand Wisely**: Only expand relations you actually need to reduce response size and improve performance.

2. **Handle Missing Expands**: Always check if expand data exists before accessing:

   ```csharp
   var expand = record["expand"] as Dictionary<string, object?>;
   var user = expand?["user"] as Dictionary<string, object?>;
   if (user != null)
   {
       Console.WriteLine(user["name"]);
   }
   ```

3. **Pagination for Large Back-Relations**: If you expect more than 1000 related records, fetch them separately with pagination.

4. **Cache Expansion**: Consider caching expanded data on the client side to reduce API calls.

5. **Error Handling**: Handle cases where related records might not be accessible due to API rules.

6. **Nested Limit**: Remember that nested expands are limited to 6 levels deep.

## Performance Considerations

- **Expand Cost**: Expanding relations doesn't require additional round trips, but increases response payload size
- **Back-Relation Limit**: The 1000 record limit for back-relations prevents extremely large responses
- **Permission Checks**: Each expanded relation is checked against the collection's `viewRule`
- **Nested Depth**: Limit nested expands to avoid performance issues (max 6 levels supported)

## Related Documentation

- [Collections](./COLLECTIONS.md) - Collection and field configuration
- [API Rules and Filters](./API_RULES_AND_FILTERS.md) - Filtering and querying related records

