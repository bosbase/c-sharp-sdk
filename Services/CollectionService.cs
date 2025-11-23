using Bosbase.Utils;

namespace Bosbase.Services;

public class CollectionService : BaseCrudService
{
    public CollectionService(BosbaseClient client) : base(client) { }

    protected override string BaseCrudPath => "/api/collections";

    public Task DeleteCollectionAsync(
        string collectionIdOrName,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return DeleteAsync(collectionIdOrName, body, query, headers, cancellationToken);
    }

    public Task TruncateAsync(
        string collectionIdOrName,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var encoded = HttpHelpers.EncodePathSegment(collectionIdOrName);
        return Client.SendAsync(
            $"{BaseCrudPath}/{encoded}/truncate",
            new SendOptions { Method = HttpMethod.Delete, Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task ImportCollectionsAsync(
        object collections,
        bool deleteMissing = false,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["collections"] = collections,
            ["deleteMissing"] = deleteMissing
        };

        return Client.SendAsync(
            $"{BaseCrudPath}/import",
            new SendOptions { Method = HttpMethod.Put, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> GetScaffoldsAsync(
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCrudPath}/meta/scaffolds",
            new SendOptions { Body = body, Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task<Dictionary<string, object?>> CreateFromScaffoldAsync(
        string scaffoldType,
        string name,
        IDictionary<string, object?>? overrides = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var scaffolds = await GetScaffoldsAsync(query: query, headers: headers, cancellationToken: cancellationToken);
        if (!scaffolds.TryGetValue(scaffoldType, out var scaffoldObj) || scaffoldObj is not Dictionary<string, object?> scaffold)
        {
            throw new ArgumentException($"Scaffold for type '{scaffoldType}' not found.");
        }

        var data = new Dictionary<string, object?>(scaffold) { ["name"] = name };
        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                data[kvp.Key] = kvp.Value;
            }
        }
        if (body != null)
        {
            foreach (var kvp in body)
            {
                data[kvp.Key] = kvp.Value;
            }
        }

        return await CreateAsync(data, query, headers: headers, cancellationToken: cancellationToken);
    }

    public Task<Dictionary<string, object?>> CreateBaseAsync(
        string name,
        IDictionary<string, object?>? overrides = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return CreateFromScaffoldAsync("base", name, overrides, body, query, headers, cancellationToken);
    }

    public Task<Dictionary<string, object?>> CreateAuthAsync(
        string name,
        IDictionary<string, object?>? overrides = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return CreateFromScaffoldAsync("auth", name, overrides, body, query, headers, cancellationToken);
    }

    public Task<Dictionary<string, object?>> CreateViewAsync(
        string name,
        string? viewQuery = null,
        IDictionary<string, object?>? overrides = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var scaffoldOverrides = new Dictionary<string, object?>(overrides ?? new Dictionary<string, object?>());
        if (viewQuery != null) scaffoldOverrides["viewQuery"] = viewQuery;
        return CreateFromScaffoldAsync("view", name, scaffoldOverrides, body, query, headers, cancellationToken);
    }

    public async Task<Dictionary<string, object?>> AddIndexAsync(
        string collectionIdOrName,
        IEnumerable<string> columns,
        bool unique = false,
        string? indexName = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var cols = columns.ToList();
        if (!cols.Any()) throw new ArgumentException("At least one column must be specified.", nameof(columns));

        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        var fields = DictionaryExtensions.SafeGet(collection, "fields") as IEnumerable<object?> ?? Enumerable.Empty<object?>();
        var fieldNames = fields
            .Select(f => f as IDictionary<string, object?>)
            .Where(f => f != null && f.TryGetValue("name", out _))
            .Select(f => f!["name"]?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet();

        foreach (var column in cols)
        {
            if (column != "id" && !fieldNames.Contains(column))
            {
                throw new ArgumentException($"Field \"{column}\" does not exist in the collection.");
            }
        }

        var collectionName = DictionaryExtensions.SafeGet(collection, "name")?.ToString() ?? collectionIdOrName;
        var idxName = indexName ?? $"idx_{collectionName}_{string.Join("_", cols)}";
        var columnsSql = string.Join(", ", cols.Select(c => $"`{c}`"));
        var indexSql = unique
            ? $"CREATE UNIQUE INDEX `{idxName}` ON `{collectionName}` ({columnsSql})"
            : $"CREATE INDEX `{idxName}` ON `{collectionName}` ({columnsSql})";

        var indexes = (DictionaryExtensions.SafeGet(collection, "indexes") as IEnumerable<object?>)?.Select(i => i?.ToString() ?? string.Empty).Where(i => !string.IsNullOrEmpty(i)).ToList() ?? new List<string>();
        if (indexes.Contains(indexSql))
        {
            throw new ArgumentException("Index already exists.");
        }

        indexes.Add(indexSql);
        collection["indexes"] = indexes;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> RemoveIndexAsync(
        string collectionIdOrName,
        IEnumerable<string> columns,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var cols = columns.ToList();
        if (!cols.Any()) throw new ArgumentException("At least one column must be specified.", nameof(columns));

        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        var indexes = (DictionaryExtensions.SafeGet(collection, "indexes") as IEnumerable<object?>)?.Select(i => i?.ToString() ?? string.Empty).Where(i => !string.IsNullOrEmpty(i)).ToList() ?? new List<string>();
        var initial = indexes.Count;

        bool Matches(string idx)
        {
            foreach (var column in cols)
            {
                var backticked = $"`{column}`";
                if (idx.Contains($"({column})") || idx.Contains($"({column},") || idx.Contains($", {column})") || idx.Contains(backticked))
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        indexes = indexes.Where(idx => !Matches(idx)).ToList();
        if (indexes.Count == initial)
        {
            throw new ArgumentException("Index not found.");
        }

        collection["indexes"] = indexes;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<List<string>> GetIndexesAsync(
        string collectionIdOrName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        var existing = DictionaryExtensions.SafeGet(collection, "indexes") as IEnumerable<object?> ?? Enumerable.Empty<object?>();
        return existing.Select(x => x?.ToString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    public Task<Dictionary<string, object?>> GetSchemaAsync(
        string collectionIdOrName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var encoded = HttpHelpers.EncodePathSegment(collectionIdOrName);
        return Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCrudPath}/{encoded}/schema",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
    }

    public Task<Dictionary<string, object?>> GetAllSchemasAsync(
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        return Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCrudPath}/schemas",
            new SendOptions { Query = query, Headers = headers },
            cancellationToken);
    }

    // -------------------------------------------------------------------
    // Export/Import Helpers
    // -------------------------------------------------------------------

    public async Task<List<Dictionary<string, object?>>> ExportCollectionsAsync(
        Func<Dictionary<string, object?>, bool>? filterCollections = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var fullList = await GetFullListAsync(query: query, headers: headers, cancellationToken: cancellationToken);
        var collections = fullList
            .OfType<Dictionary<string, object?>>()
            .ToList();

        var filtered = filterCollections != null
            ? collections.Where(filterCollections).ToList()
            : collections;

        // Clean collections for export (matching UI behavior)
        var cleaned = filtered.Select(collection =>
        {
            var cleanedCollection = new Dictionary<string, object?>(collection);
            
            // Remove timestamps
            cleanedCollection.Remove("created");
            cleanedCollection.Remove("updated");
            
            // Remove OAuth2 providers
            if (cleanedCollection.TryGetValue("oauth2", out var oauth2Obj) && oauth2Obj is Dictionary<string, object?> oauth2)
            {
                oauth2.Remove("providers");
            }
            
            return cleanedCollection;
        }).ToList();

        return cleaned;
    }

    public List<Dictionary<string, object?>> NormalizeForImport(
        IEnumerable<Dictionary<string, object?>> collections)
    {
        var collectionsList = collections.ToList();
        
        // Remove duplicates by id
        var seenIds = new HashSet<string>();
        var uniqueCollections = collectionsList.Where(collection =>
        {
            var id = DictionaryExtensions.SafeGet(collection, "id")?.ToString();
            if (string.IsNullOrWhiteSpace(id)) return true;
            if (seenIds.Contains(id)) return false;
            seenIds.Add(id);
            return true;
        }).ToList();

        // Normalize each collection
        return uniqueCollections.Select(collection =>
        {
            var normalized = new Dictionary<string, object?>(collection);
            
            // Remove timestamps
            normalized.Remove("created");
            normalized.Remove("updated");
            
            // Remove duplicate fields by id
            if (normalized.TryGetValue("fields", out var fieldsObj) && fieldsObj is IEnumerable<object?> fields)
            {
                var seenFieldIds = new HashSet<string>();
                var uniqueFields = fields
                    .OfType<Dictionary<string, object?>>()
                    .Where(field =>
                    {
                        var fieldId = DictionaryExtensions.SafeGet(field, "id")?.ToString();
                        if (string.IsNullOrWhiteSpace(fieldId)) return true;
                        if (seenFieldIds.Contains(fieldId)) return false;
                        seenFieldIds.Add(fieldId);
                        return true;
                    })
                    .Cast<object?>()
                    .ToList();
                normalized["fields"] = uniqueFields;
            }
            
            return normalized;
        }).ToList();
    }

    // -------------------------------------------------------------------
    // Field Management Helpers
    // -------------------------------------------------------------------

    public async Task<Dictionary<string, object?>> AddFieldAsync(
        string collectionIdOrName,
        Dictionary<string, object?> field,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        if (!field.TryGetValue("name", out var nameObj) || nameObj == null)
        {
            throw new ArgumentException("Field name is required.", nameof(field));
        }
        if (!field.TryGetValue("type", out var typeObj) || typeObj == null)
        {
            throw new ArgumentException("Field type is required.", nameof(field));
        }

        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        var fields = (DictionaryExtensions.SafeGet(collection, "fields") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>()
            .ToList();

        var fieldName = nameObj?.ToString();
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be null or empty.", nameof(field));
        }
        if (fields.Any(f => DictionaryExtensions.SafeGet(f, "name")?.ToString() == fieldName))
        {
            throw new ArgumentException($"Field with name \"{fieldName}\" already exists.");
        }

        var newField = new Dictionary<string, object?>(field)
        {
            ["id"] = "",
            ["name"] = fieldName,
            ["type"] = typeObj,
            ["system"] = false,
            ["hidden"] = field.GetValueOrDefault("hidden", false),
            ["presentable"] = field.GetValueOrDefault("presentable", false),
            ["required"] = field.GetValueOrDefault("required", false)
        };

        fields.Add(newField);
        collection["fields"] = fields;

        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> UpdateFieldAsync(
        string collectionIdOrName,
        string fieldName,
        Dictionary<string, object?> updates,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        var fields = (DictionaryExtensions.SafeGet(collection, "fields") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>()
            .ToList();

        var fieldIndex = fields.FindIndex(f => DictionaryExtensions.SafeGet(f, "name")?.ToString() == fieldName);
        if (fieldIndex == -1)
        {
            throw new ArgumentException($"Field with name \"{fieldName}\" not found.");
        }

        var field = fields[fieldIndex];
        
        // Don't allow changing system fields
        if (DictionaryExtensions.SafeGet(field, "system") is true && (updates.ContainsKey("type") || updates.ContainsKey("name")))
        {
            throw new InvalidOperationException("Cannot modify system fields.");
        }

        // If renaming, check for name conflicts
        if (updates.TryGetValue("name", out var newNameObj) && newNameObj?.ToString() != fieldName)
        {
            var newName = newNameObj?.ToString();
            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("New field name cannot be null or empty.", nameof(updates));
            }
            if (fields.Any(f => DictionaryExtensions.SafeGet(f, "name")?.ToString() == newName && f != field))
            {
                throw new ArgumentException($"Field with name \"{newName}\" already exists.");
            }
        }

        // Apply updates
        foreach (var kvp in updates)
        {
            field[kvp.Key] = kvp.Value;
        }
        fields[fieldIndex] = field;
        collection["fields"] = fields;

        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> RemoveFieldAsync(
        string collectionIdOrName,
        string fieldName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        var fields = (DictionaryExtensions.SafeGet(collection, "fields") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>()
            .ToList();

        var fieldIndex = fields.FindIndex(f => DictionaryExtensions.SafeGet(f, "name")?.ToString() == fieldName);
        if (fieldIndex == -1)
        {
            throw new ArgumentException($"Field with name \"{fieldName}\" not found.");
        }

        var field = fields[fieldIndex];
        
        // Don't allow removing system fields
        if (DictionaryExtensions.SafeGet(field, "system") is true)
        {
            throw new InvalidOperationException("Cannot remove system fields.");
        }

        // Remove the field
        fields.RemoveAt(fieldIndex);
        collection["fields"] = fields;

        // Remove indexes that reference this field
        var indexes = (DictionaryExtensions.SafeGet(collection, "indexes") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .Select(i => i?.ToString() ?? string.Empty)
            .Where(i => !string.IsNullOrEmpty(i))
            .Where(idx => !idx.Contains($"({fieldName})") && !idx.Contains($"({fieldName},") && !idx.Contains($", {fieldName})"))
            .Cast<object?>()
            .ToList();
        collection["indexes"] = indexes;

        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>?> GetFieldAsync(
        string collectionIdOrName,
        string fieldName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        var fields = (DictionaryExtensions.SafeGet(collection, "fields") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>();
        return fields.FirstOrDefault(f => DictionaryExtensions.SafeGet(f, "name")?.ToString() == fieldName);
    }

    // -------------------------------------------------------------------
    // API Rules Management Helpers
    // -------------------------------------------------------------------

    public async Task<Dictionary<string, object?>> SetListRuleAsync(
        string collectionIdOrName,
        string? rule,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        collection["listRule"] = rule;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> SetViewRuleAsync(
        string collectionIdOrName,
        string? rule,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        collection["viewRule"] = rule;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> SetCreateRuleAsync(
        string collectionIdOrName,
        string? rule,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        collection["createRule"] = rule;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> SetUpdateRuleAsync(
        string collectionIdOrName,
        string? rule,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        collection["updateRule"] = rule;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> SetDeleteRuleAsync(
        string collectionIdOrName,
        string? rule,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        collection["deleteRule"] = rule;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> SetRulesAsync(
        string collectionIdOrName,
        Dictionary<string, string?> rules,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (rules.ContainsKey("listRule"))
        {
            collection["listRule"] = rules["listRule"];
        }
        if (rules.ContainsKey("viewRule"))
        {
            collection["viewRule"] = rules["viewRule"];
        }
        if (rules.ContainsKey("createRule"))
        {
            collection["createRule"] = rules["createRule"];
        }
        if (rules.ContainsKey("updateRule"))
        {
            collection["updateRule"] = rules["updateRule"];
        }
        if (rules.ContainsKey("deleteRule"))
        {
            collection["deleteRule"] = rules["deleteRule"];
        }
        
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, string?>> GetRulesAsync(
        string collectionIdOrName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        return new Dictionary<string, string?>
        {
            ["listRule"] = DictionaryExtensions.SafeGet(collection, "listRule")?.ToString(),
            ["viewRule"] = DictionaryExtensions.SafeGet(collection, "viewRule")?.ToString(),
            ["createRule"] = DictionaryExtensions.SafeGet(collection, "createRule")?.ToString(),
            ["updateRule"] = DictionaryExtensions.SafeGet(collection, "updateRule")?.ToString(),
            ["deleteRule"] = DictionaryExtensions.SafeGet(collection, "deleteRule")?.ToString()
        };
    }

    public async Task<Dictionary<string, object?>> SetManageRuleAsync(
        string collectionIdOrName,
        string? rule,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("ManageRule is only available for auth collections.");
        }
        
        collection["manageRule"] = rule;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> SetAuthRuleAsync(
        string collectionIdOrName,
        string? rule,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("AuthRule is only available for auth collections.");
        }
        
        collection["authRule"] = rule;
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    // -------------------------------------------------------------------
    // OAuth2 Configuration Methods
    // -------------------------------------------------------------------

    public async Task<Dictionary<string, object?>> EnableOAuth2Async(
        string collectionIdOrName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("OAuth2 is only available for auth collections.");
        }
        
        if (!collection.TryGetValue("oauth2", out var oauth2Obj) || oauth2Obj == null)
        {
            collection["oauth2"] = new Dictionary<string, object?>
            {
                ["enabled"] = true,
                ["mappedFields"] = new Dictionary<string, object?>(),
                ["providers"] = new List<object?>()
            };
        }
        else if (oauth2Obj is Dictionary<string, object?> oauth2)
        {
            oauth2["enabled"] = true;
        }
        
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> DisableOAuth2Async(
        string collectionIdOrName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("OAuth2 is only available for auth collections.");
        }
        
        if (collection.TryGetValue("oauth2", out var oauth2Obj) && oauth2Obj is Dictionary<string, object?> oauth2)
        {
            oauth2["enabled"] = false;
        }
        
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> GetOAuth2ConfigAsync(
        string collectionIdOrName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("OAuth2 is only available for auth collections.");
        }
        
        var oauth2 = collection.TryGetValue("oauth2", out var oauth2Obj) && oauth2Obj is Dictionary<string, object?> oauth2Dict
            ? oauth2Dict
            : new Dictionary<string, object?>();
        
        return new Dictionary<string, object?>
        {
            ["enabled"] = DictionaryExtensions.SafeGet(oauth2, "enabled") ?? false,
            ["mappedFields"] = DictionaryExtensions.SafeGet(oauth2, "mappedFields") ?? new Dictionary<string, object?>(),
            ["providers"] = DictionaryExtensions.SafeGet(oauth2, "providers") ?? new List<object?>()
        };
    }

    public async Task<Dictionary<string, object?>> SetOAuth2MappedFieldsAsync(
        string collectionIdOrName,
        Dictionary<string, string> mappedFields,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("OAuth2 is only available for auth collections.");
        }
        
        if (!collection.TryGetValue("oauth2", out var oauth2Obj) || oauth2Obj == null)
        {
            collection["oauth2"] = new Dictionary<string, object?>
            {
                ["enabled"] = false,
                ["mappedFields"] = mappedFields,
                ["providers"] = new List<object?>()
            };
        }
        else if (oauth2Obj is Dictionary<string, object?> oauth2)
        {
            oauth2["mappedFields"] = mappedFields;
        }
        
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> AddOAuth2ProviderAsync(
        string collectionIdOrName,
        Dictionary<string, object?> provider,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("OAuth2 is only available for auth collections.");
        }
        
        if (!collection.TryGetValue("oauth2", out var oauth2Obj) || oauth2Obj == null)
        {
            collection["oauth2"] = new Dictionary<string, object?>
            {
                ["enabled"] = false,
                ["mappedFields"] = new Dictionary<string, object?>(),
                ["providers"] = new List<object?>()
            };
        }
        
        var oauth2 = collection["oauth2"] as Dictionary<string, object?> ?? new Dictionary<string, object?>();
        var providers = (DictionaryExtensions.SafeGet(oauth2, "providers") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>()
            .ToList();
        
        var providerName = DictionaryExtensions.SafeGet(provider, "name")?.ToString();
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name is required.", nameof(provider));
        }
        
        if (providers.Any(p => DictionaryExtensions.SafeGet(p, "name")?.ToString() == providerName))
        {
            throw new ArgumentException($"OAuth2 provider with name \"{providerName}\" already exists.");
        }
        
        var newProvider = new Dictionary<string, object?>(provider)
        {
            ["displayName"] = DictionaryExtensions.SafeGet(provider, "displayName") ?? providerName
        };
        
        providers.Add(newProvider);
        oauth2["providers"] = providers;
        collection["oauth2"] = oauth2;
        
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> UpdateOAuth2ProviderAsync(
        string collectionIdOrName,
        string providerName,
        Dictionary<string, object?> updates,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("OAuth2 is only available for auth collections.");
        }
        
        var oauth2 = collection.TryGetValue("oauth2", out var oauth2Obj) && oauth2Obj is Dictionary<string, object?> oauth2Dict
            ? oauth2Dict
            : null;
        
        if (oauth2 == null)
        {
            throw new InvalidOperationException("OAuth2 is not configured for this collection.");
        }
        
        var providers = (DictionaryExtensions.SafeGet(oauth2, "providers") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>()
            .ToList();
        
        var providerIndex = providers.FindIndex(p => DictionaryExtensions.SafeGet(p, "name")?.ToString() == providerName);
        if (providerIndex == -1)
        {
            throw new ArgumentException($"OAuth2 provider with name \"{providerName}\" not found.");
        }
        
        // Update the provider
        var provider = providers[providerIndex];
        foreach (var kvp in updates)
        {
            provider[kvp.Key] = kvp.Value;
        }
        providers[providerIndex] = provider;
        oauth2["providers"] = providers;
        collection["oauth2"] = oauth2;
        
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<string, object?>> RemoveOAuth2ProviderAsync(
        string collectionIdOrName,
        string providerName,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var collection = await GetOneAsync(collectionIdOrName, query: query, headers: headers, cancellationToken: cancellationToken);
        
        if (DictionaryExtensions.SafeGet(collection, "type")?.ToString() != "auth")
        {
            throw new InvalidOperationException("OAuth2 is only available for auth collections.");
        }
        
        var oauth2 = collection.TryGetValue("oauth2", out var oauth2Obj) && oauth2Obj is Dictionary<string, object?> oauth2Dict
            ? oauth2Dict
            : null;
        
        if (oauth2 == null)
        {
            throw new InvalidOperationException("OAuth2 is not configured for this collection.");
        }
        
        var providers = (DictionaryExtensions.SafeGet(oauth2, "providers") as IEnumerable<object?> ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>()
            .ToList();
        
        var providerIndex = providers.FindIndex(p => DictionaryExtensions.SafeGet(p, "name")?.ToString() == providerName);
        if (providerIndex == -1)
        {
            throw new ArgumentException($"OAuth2 provider with name \"{providerName}\" not found.");
        }
        
        // Remove the provider
        providers.RemoveAt(providerIndex);
        oauth2["providers"] = providers;
        collection["oauth2"] = oauth2;
        
        return await UpdateAsync(collectionIdOrName, collection, query, headers: headers, cancellationToken: cancellationToken);
    }
}
