# Schema Query API - C# SDK Documentation

## Overview

The Schema Query API provides lightweight interfaces to retrieve collection field information without fetching full collection definitions. This is particularly useful for AI systems that need to understand the structure of collections and the overall system architecture.

**Key Features:**
- Get schema for a single collection by name or ID
- Get schemas for all collections in the system
- Lightweight response with only essential field information
- Support for all collection types (base, auth, view)
- Fast and efficient queries

**Backend Endpoints:**
- `GET /api/collections/{collection}/schema` - Get single collection schema
- `GET /api/collections/schemas` - Get all collection schemas

**Note**: All Schema Query API operations require superuser authentication.

## Authentication

All Schema Query API operations require superuser authentication:

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate as superuser
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
```

## Get Single Collection Schema

Retrieves the schema (fields and types) for a single collection by name or ID.

### Basic Usage

```csharp
// Get schema for a collection by name
var schema = await client.Collections.GetSchemaAsync("demo1");

var name = schema["name"]?.ToString();    // "demo1"
var type = schema["type"]?.ToString();    // "base"
var fields = schema["fields"] as List<object?>;  // Array of field information

// Iterate through fields
if (fields != null)
{
    foreach (var field in fields)
    {
        if (field is Dictionary<string, object?> fieldDict)
        {
            var fieldName = fieldDict["name"]?.ToString();
            var fieldType = fieldDict["type"]?.ToString();
            var required = fieldDict.GetValueOrDefault("required")?.ToString() == "True";
            Console.WriteLine($"{fieldName}: {fieldType}{(required ? " (required)" : "")}");
        }
    }
}
```

### Using Collection ID

```csharp
// Get schema for a collection by ID
var schema = await client.Collections.GetSchemaAsync("_pbc_base_123");

var name = schema["name"]?.ToString();  // "demo1"
```

### Handling Different Collection Types

```csharp
// Base collection
var baseSchema = await client.Collections.GetSchemaAsync("demo1");
Console.WriteLine(baseSchema["type"]);  // "base"

// Auth collection
var authSchema = await client.Collections.GetSchemaAsync("users");
Console.WriteLine(authSchema["type"]);  // "auth"

// View collection
var viewSchema = await client.Collections.GetSchemaAsync("view1");
Console.WriteLine(viewSchema["type"]);  // "view"
```

### Error Handling

```csharp
try
{
    var schema = await client.Collections.GetSchemaAsync("nonexistent");
}
catch (ClientResponseError ex)
{
    if (ex.Status == 404)
    {
        Console.WriteLine("Collection not found");
    }
    else
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}
```

## Get All Collection Schemas

Retrieves the schema (fields and types) for all collections in the system.

### Basic Usage

```csharp
// Get schemas for all collections
var result = await client.Collections.GetAllSchemasAsync();

var collections = result["collections"] as List<object?>;  // Array of all collection schemas

// Iterate through all collections
if (collections != null)
{
    foreach (var collection in collections)
    {
        if (collection is Dictionary<string, object?> collDict)
        {
            Console.WriteLine($"Collection: {collDict["name"]} ({collDict["type"]})");
            
            if (collDict["fields"] is List<object?> fields)
            {
                Console.WriteLine($"Fields: {fields.Count}");
                
                // List all fields
                foreach (var field in fields)
                {
                    if (field is Dictionary<string, object?> fieldDict)
                    {
                        Console.WriteLine($"  - {fieldDict["name"]}: {fieldDict["type"]}");
                    }
                }
            }
        }
    }
}
```

### Filtering Collections by Type

```csharp
var result = await client.Collections.GetAllSchemasAsync();
var collections = result["collections"] as List<object?>;

// Filter to only base collections
var baseCollections = collections?
    .Where(c => c is Dictionary<string, object?> dict && dict["type"]?.ToString() == "base")
    .ToList();

// Filter to only auth collections
var authCollections = collections?
    .Where(c => c is Dictionary<string, object?> dict && dict["type"]?.ToString() == "auth")
    .ToList();

// Filter to only view collections
var viewCollections = collections?
    .Where(c => c is Dictionary<string, object?> dict && dict["type"]?.ToString() == "view")
    .ToList();
```

### Building a Field Index

```csharp
// Build a map of all field names and types across all collections
var result = await client.Collections.GetAllSchemasAsync();
var collections = result["collections"] as List<object?>;

var fieldIndex = new Dictionary<string, Dictionary<string, object?>>();

if (collections != null)
{
    foreach (var collection in collections)
    {
        if (collection is Dictionary<string, object?> collDict)
        {
            var collName = collDict["name"]?.ToString() ?? "";
            var collType = collDict["type"]?.ToString() ?? "";
            
            if (collDict["fields"] is List<object?> fields)
            {
                foreach (var field in fields)
                {
                    if (field is Dictionary<string, object?> fieldDict)
                    {
                        var fieldName = fieldDict["name"]?.ToString() ?? "";
                        var key = $"{collName}.{fieldName}";
                        
                        fieldIndex[key] = new Dictionary<string, object?>
                        {
                            ["collection"] = collName,
                            ["collectionType"] = collType,
                            ["fieldName"] = fieldName,
                            ["fieldType"] = fieldDict["type"],
                            ["required"] = fieldDict.GetValueOrDefault("required")?.ToString() == "True",
                            ["system"] = fieldDict.GetValueOrDefault("system")?.ToString() == "True",
                            ["hidden"] = fieldDict.GetValueOrDefault("hidden")?.ToString() == "True",
                        };
                    }
                }
            }
        }
    }
}

// Use the index
if (fieldIndex.TryGetValue("demo1.title", out var fieldInfo))
{
    Console.WriteLine($"Field info: {System.Text.Json.JsonSerializer.Serialize(fieldInfo)}");
}
```

## Complete Examples

### Example 1: AI System Understanding Collection Structure

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Get all collection schemas for system understanding
var result = await client.Collections.GetAllSchemasAsync();
var collections = result["collections"] as List<object?>;

// Create a comprehensive system overview
var systemOverview = new List<Dictionary<string, object?>>();

if (collections != null)
{
    foreach (var collection in collections)
    {
        if (collection is Dictionary<string, object?> collDict)
        {
            var fields = new List<Dictionary<string, object?>>();
            
            if (collDict["fields"] is List<object?> fieldList)
            {
                foreach (var field in fieldList)
                {
                    if (field is Dictionary<string, object?> fieldDict)
                    {
                        fields.Add(new Dictionary<string, object?>
                        {
                            ["name"] = fieldDict["name"],
                            ["type"] = fieldDict["type"],
                            ["required"] = fieldDict.GetValueOrDefault("required")?.ToString() == "True",
                        });
                    }
                }
            }
            
            systemOverview.Add(new Dictionary<string, object?>
            {
                ["name"] = collDict["name"],
                ["type"] = collDict["type"],
                ["fields"] = fields
            });
        }
    }
}

Console.WriteLine("System Collections Overview:");
foreach (var collection in systemOverview)
{
    Console.WriteLine($"\n{collection["name"]} ({collection["type"]}):");
    
    if (collection["fields"] is List<object?> fields)
    {
        foreach (var field in fields)
        {
            if (field is Dictionary<string, object?> fieldDict)
            {
                var required = fieldDict["required"]?.ToString() == "True" ? " [required]" : "";
                Console.WriteLine($"  {fieldDict["name"]}: {fieldDict["type"]}{required}");
            }
        }
    }
}
```

### Example 2: Validating Field Existence Before Query

```csharp
// Check if a field exists before querying
async Task<bool> CheckFieldExists(string collectionName, string fieldName)
{
    try
    {
        var schema = await client.Collections.GetSchemaAsync(collectionName);
        
        if (schema["fields"] is List<object?> fields)
        {
            return fields.Any(f => 
                f is Dictionary<string, object?> fieldDict && 
                fieldDict["name"]?.ToString() == fieldName
            );
        }
    }
    catch
    {
        return false;
    }
    
    return false;
}

// Usage
var hasTitleField = await CheckFieldExists("demo1", "title");
if (hasTitleField)
{
    // Safe to query the field
    var records = await client.Collection("demo1").GetListAsync(1, 20);
}
```

### Example 3: Dynamic Form Generation

```csharp
// Generate form fields based on collection schema
async Task<List<Dictionary<string, object?>>> GenerateFormFields(string collectionName)
{
    var schema = await client.Collections.GetSchemaAsync(collectionName);
    var fields = new List<Dictionary<string, object?>>();
    
    if (schema["fields"] is List<object?> fieldList)
    {
        foreach (var field in fieldList)
        {
            if (field is Dictionary<string, object?> fieldDict)
            {
                var isSystem = fieldDict.GetValueOrDefault("system")?.ToString() == "True";
                var isHidden = fieldDict.GetValueOrDefault("hidden")?.ToString() == "True";
                
                // Exclude system/hidden fields
                if (!isSystem && !isHidden)
                {
                    var fieldName = fieldDict["name"]?.ToString() ?? "";
                    fields.Add(new Dictionary<string, object?>
                    {
                        ["name"] = fieldName,
                        ["type"] = fieldDict["type"],
                        ["required"] = fieldDict.GetValueOrDefault("required")?.ToString() == "True",
                        ["label"] = fieldName.Length > 0 
                            ? char.ToUpper(fieldName[0]) + fieldName.Substring(1) 
                            : fieldName,
                    });
                }
            }
        }
    }
    
    return fields;
}

// Usage
var formFields = await GenerateFormFields("demo1");
foreach (var field in formFields)
{
    Console.WriteLine($"{field["name"]}: {field["type"]}, Required: {field["required"]}");
}
```

### Example 4: Schema Comparison

```csharp
// Compare schemas between two collections
async Task<Dictionary<string, object?>> CompareSchemas(string collection1, string collection2)
{
    var schemas = await Task.WhenAll(
        client.Collections.GetSchemaAsync(collection1),
        client.Collections.GetSchemaAsync(collection2)
    );
    
    var schema1 = schemas[0];
    var schema2 = schemas[1];
    
    var fields1 = new HashSet<string>();
    var fields2 = new HashSet<string>();
    
    if (schema1["fields"] is List<object?> fields1List)
    {
        foreach (var field in fields1List)
        {
            if (field is Dictionary<string, object?> fieldDict)
            {
                fields1.Add(fieldDict["name"]?.ToString() ?? "");
            }
        }
    }
    
    if (schema2["fields"] is List<object?> fields2List)
    {
        foreach (var field in fields2List)
        {
            if (field is Dictionary<string, object?> fieldDict)
            {
                fields2.Add(fieldDict["name"]?.ToString() ?? "");
            }
        }
    }
    
    return new Dictionary<string, object?>
    {
        ["common"] = fields1.Intersect(fields2).ToList(),
        ["onlyIn1"] = fields1.Except(fields2).ToList(),
        ["onlyIn2"] = fields2.Except(fields1).ToList(),
    };
}

// Usage
var comparison = await CompareSchemas("demo1", "demo2");
Console.WriteLine($"Common fields: {string.Join(", ", comparison["common"] as List<object?> ?? new List<object?>())}");
Console.WriteLine($"Only in demo1: {string.Join(", ", comparison["onlyIn1"] as List<object?> ?? new List<object?>())}");
Console.WriteLine($"Only in demo2: {string.Join(", ", comparison["onlyIn2"] as List<object?> ?? new List<object?>())}");
```

## Response Structure

### Single Collection Schema Response

```json
{
  "name": "demo1",
  "type": "base",
  "fields": [
    {
      "name": "id",
      "type": "text",
      "required": true,
      "system": true,
      "hidden": false
    },
    {
      "name": "title",
      "type": "text",
      "required": true,
      "system": false,
      "hidden": false
    },
    {
      "name": "description",
      "type": "text",
      "required": false,
      "system": false,
      "hidden": false
    }
  ]
}
```

### All Collections Schemas Response

```json
{
  "collections": [
    {
      "name": "demo1",
      "type": "base",
      "fields": [...]
    },
    {
      "name": "users",
      "type": "auth",
      "fields": [...]
    },
    {
      "name": "view1",
      "type": "view",
      "fields": [...]
    }
  ]
}
```

## Use Cases

1. **AI System Design**: AI systems can query all collection schemas to understand the overall database structure and design queries or operations accordingly.
2. **Code Generation**: Generate client-side code, TypeScript types, or form components based on collection schemas.
3. **Documentation Generation**: Automatically generate API documentation or data dictionaries from collection schemas.
4. **Schema Validation**: Validate queries or operations before execution by checking field existence and types.
5. **Migration Planning**: Compare schemas between environments or versions to plan migrations.
6. **Dynamic UI Generation**: Create dynamic forms, tables, or interfaces based on collection field definitions.

## Performance Considerations

- **Lightweight**: Schema queries return only essential field information, not full collection definitions
- **Efficient**: Much faster than fetching full collection objects
- **Cached**: Results can be cached for better performance
- **Batch**: Use `GetAllSchemasAsync()` to get all schemas in a single request

## Error Handling

```csharp
try
{
    var schema = await client.Collections.GetSchemaAsync("demo1");
}
catch (ClientResponseError ex)
{
    switch (ex.Status)
    {
        case 401:
            Console.Error.WriteLine("Authentication required");
            break;
        case 403:
            Console.Error.WriteLine("Superuser access required");
            break;
        case 404:
            Console.Error.WriteLine("Collection not found");
            break;
        default:
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            break;
    }
}
```

## Best Practices

1. **Cache Results**: Schema information rarely changes, so cache results when appropriate
2. **Error Handling**: Always handle 404 errors for non-existent collections
3. **Filter System Fields**: When building UI, filter out system and hidden fields
4. **Batch Queries**: Use `GetAllSchemasAsync()` when you need multiple collection schemas
5. **Type Safety**: Use proper type checking when accessing dictionary values

## Related Documentation

- [Collection API](./COLLECTION_API.md) - Full collection management API
- [Records API](./API_RECORDS.md) - Record CRUD operations

