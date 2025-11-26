# API Rules Documentation - C# SDK

API Rules are collection access controls and data filters that determine who can perform actions on your collections and what data they can access.

## Overview

Each collection has 5 standard API rules, corresponding to specific API actions:

- **`listRule`** - Controls read/list access
- **`viewRule`** - Controls read/view access  
- **`createRule`** - Controls create access
- **`updateRule`** - Controls update access
- **`deleteRule`** - Controls delete access

Auth collections have two additional rules:

- **`manageRule`** - Admin-like permissions for managing auth records
- **`authRule`** - Additional constraints applied during authentication

## Rule Values

Each rule can be set to one of three values:

### 1. `null` (Locked)
Only authorized superusers can perform the action.

```csharp
await client.Collections.SetListRuleAsync("products", null);
```

### 2. `""` (Empty String - Public)
Anyone (superusers, authorized users, and guests) can perform the action.

```csharp
await client.Collections.SetListRuleAsync("products", "");
```

### 3. Non-empty String (Filter Expression)
Only users satisfying the filter expression can perform the action.

```csharp
await client.Collections.SetListRuleAsync("products", "@request.auth.id != \"\"");
```

## Default Permissions

When you create a base collection without specifying rules, BosBase applies opinionated defaults:

- `listRule` and `viewRule` default to an empty string (`""`), so guests and authenticated users can query records.
- `createRule` defaults to `@request.auth.id != ""`, restricting writes to authenticated users or superusers.
- `updateRule` and `deleteRule` default to `@request.auth.id != "" && createdBy = @request.auth.id`, which limits mutations to the record creator (superusers still bypass rules).

Every base collection now includes hidden system fields named `createdBy` and `updatedBy`. BosBase adds those fields automatically when a collection is created and manages their values server-side: `createdBy` always captures the authenticated actor that inserted the record (or stays empty for anonymous writes) and cannot be overridden later, while `updatedBy` is overwritten on each write (or cleared for anonymous writes). View collections inherit the public read defaults, and system collections such as `users`, `_superusers`, `_authOrigins`, `_externalAuths`, `_mfas`, and `_otps` keep their custom API rules.

## Setting Rules

### Individual Rules

Set individual rules using dedicated methods:

```csharp
// Set list rule
await client.Collections.SetListRuleAsync(
    "products",
    "@request.auth.id != \"\""
);

// Set view rule
await client.Collections.SetViewRuleAsync(
    "products",
    "@request.auth.id != \"\""
);

// Set create rule
await client.Collections.SetCreateRuleAsync(
    "products",
    "@request.auth.id != \"\""
);

// Set update rule
await client.Collections.SetUpdateRuleAsync(
    "products",
    "@request.auth.id != \"\" && author.id ?= @request.auth.id"
);

// Set delete rule
await client.Collections.SetDeleteRuleAsync(
    "products",
    null  // Only superusers
);
```

### Bulk Rule Updates

Set multiple rules at once:

```csharp
await client.Collections.SetRulesAsync("products", new Dictionary<string, object?>
{
    ["listRule"] = "@request.auth.id != \"\"",
    ["viewRule"] = "@request.auth.id != \"\"",
    ["createRule"] = "@request.auth.id != \"\"",
    ["updateRule"] = "@request.auth.id != \"\" && author = @request.auth.id",
    ["deleteRule"] = null
});
```

## Common Rule Patterns

### Public Read, Authenticated Write

```csharp
await client.Collections.SetRulesAsync("posts", new Dictionary<string, object?>
{
    ["listRule"] = "",
    ["viewRule"] = "",
    ["createRule"] = "@request.auth.id != \"\"",
    ["updateRule"] = "@request.auth.id != \"\" && author = @request.auth.id",
    ["deleteRule"] = "@request.auth.id != \"\" && author = @request.auth.id"
});
```

### Owner-Only Access

```csharp
await client.Collections.SetRulesAsync("notes", new Dictionary<string, object?>
{
    ["listRule"] = "author = @request.auth.id",
    ["viewRule"] = "author = @request.auth.id",
    ["createRule"] = "@request.auth.id != \"\"",
    ["updateRule"] = "author = @request.auth.id",
    ["deleteRule"] = "author = @request.auth.id"
});
```

### Role-Based Access

```csharp
// Assuming you have a 'role' field in your users collection
await client.Collections.SetRulesAsync("admin_panel", new Dictionary<string, object?>
{
    ["listRule"] = "@request.auth.role = \"admin\"",
    ["viewRule"] = "@request.auth.role = \"admin\"",
    ["createRule"] = "@request.auth.role = \"admin\"",
    ["updateRule"] = "@request.auth.role = \"admin\"",
    ["deleteRule"] = "@request.auth.role = \"admin\""
});
```

## Related Documentation

- [API Rules and Filters](./API_RULES_AND_FILTERS.md) - Detailed filter syntax
- [Collection API](./COLLECTION_API.md) - Collection management
- [Users Collection Guide](./USERS_COLLECTION_GUIDE.md) - Using users in API rules

