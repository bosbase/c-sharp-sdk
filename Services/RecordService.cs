using System.Text;
using System.Text.Json;
using Bosbase.Auth;
using Bosbase.Exceptions;
using Bosbase.Utils;

namespace Bosbase.Services;

public class RecordService : BaseCrudService
{
    public string CollectionIdOrName { get; }

    private string BaseCollectionPath => $"/api/collections/{HttpHelpers.EncodePathSegment(CollectionIdOrName)}";
    protected override string BaseCrudPath => $"{BaseCollectionPath}/records";

    public RecordService(BosbaseClient client, string collectionIdOrName) : base(client)
    {
        CollectionIdOrName = collectionIdOrName;
    }

    // ------------------------------------------------------------------
    // Realtime
    // ------------------------------------------------------------------
    public Action Subscribe(
        string topic,
        Action<Dictionary<string, object?>> callback,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null)
    {
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("topic must be set", nameof(topic));
        var fullTopic = $"{CollectionIdOrName}/{topic}";
        return Client.Realtime.Subscribe(fullTopic, callback, query, headers);
    }

    public void Unsubscribe(string? topic = null)
    {
        if (topic != null)
        {
            Client.Realtime.Unsubscribe($"{CollectionIdOrName}/{topic}");
        }
        else
        {
            Client.Realtime.UnsubscribeByPrefix(CollectionIdOrName);
        }
    }

    // ------------------------------------------------------------------
    // CRUD sync with auth store
    // ------------------------------------------------------------------
    public override async Task<Dictionary<string, object?>> UpdateAsync(
        string recordId,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IEnumerable<Models.FileAttachment>? files = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        var item = await base.UpdateAsync(recordId, body, query, files, headers, expand, fields, cancellationToken);
        MaybeUpdateAuthRecord(item);
        return item;
    }

    public override async Task DeleteAsync(
        string recordId,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await base.DeleteAsync(recordId, body, query, headers, cancellationToken);
        if (IsAuthRecord(recordId))
        {
            Client.AuthStore.Clear();
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    public async Task<int> GetCountAsync(
        string? filter = null,
        string? expand = null,
        string? fields = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (filter != null) parameters.TryAdd("filter", filter);
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCrudPath}/count",
            new SendOptions { Query = parameters, Headers = headers },
            cancellationToken);
        return Convert.ToInt32(DictionaryExtensions.SafeGet(data, "count") ?? 0);
    }

    public Task<Dictionary<string, object?>> ListAuthMethodsAsync(
        string? fields = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>())
        {
            ["fields"] = fields ?? "mfa,otp,password,oauth2"
        };
        return Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCollectionPath}/auth-methods",
            new SendOptions { Query = parameters, Headers = headers },
            cancellationToken);
    }

    public async Task<Dictionary<string, object?>> AuthWithPasswordAsync(
        string identity,
        string password,
        string? expand = null,
        string? fields = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["identity"] = identity,
            ["password"] = password
        };
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCollectionPath}/auth-with-password",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = parameters, Headers = headers },
            cancellationToken);
        return AuthResponse(data);
    }

    public async Task<Dictionary<string, object?>> AuthWithOAuth2CodeAsync(
        string provider,
        string code,
        string codeVerifier,
        string redirectUrl,
        IDictionary<string, object?>? createData = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["provider"] = provider,
            ["code"] = code,
            ["codeVerifier"] = codeVerifier,
            ["redirectURL"] = redirectUrl
        };
        if (createData != null) payload["createData"] = createData;

        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCollectionPath}/auth-with-oauth2",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = parameters, Headers = headers },
            cancellationToken);
        return AuthResponse(data);
    }

    public async Task<Dictionary<string, object?>> AuthWithOAuth2Async(
        string providerName,
        Action<string> urlCallback,
        IEnumerable<string>? scopes = null,
        IDictionary<string, object?>? createData = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        string? expand = null,
        string? fields = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var authMethods = await ListAuthMethodsAsync(fields: null, query: query, headers: headers, cancellationToken: cancellationToken);
        var providers = DictionaryExtensions.SafeGet(authMethods, "oauth2") as IDictionary<string, object?>;
        var providersList = providers != null ? DictionaryExtensions.SafeGet(providers, "providers") as IEnumerable<object?> : null;
        var provider = providersList?
            .Select(p => p as IDictionary<string, object?>)
            .FirstOrDefault(p => p != null && DictionaryExtensions.SafeGet(p, "name")?.ToString() == providerName);

        if (provider == null)
        {
            throw new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = $"missing provider {providerName}" });
        }

        var redirectUrl = Client.BuildUrl("/api/oauth2-redirect");
        var tcs = new TaskCompletionSource<Dictionary<string, object?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errors = new List<Exception>();

        Action<Dictionary<string, object?>> handler = null!;
        handler = async payload =>
        {
            try
            {
                var state = DictionaryExtensions.SafeGet(payload, "state")?.ToString();
                var code = DictionaryExtensions.SafeGet(payload, "code")?.ToString();
                var err = DictionaryExtensions.SafeGet(payload, "error")?.ToString();
                if (state != Client.Realtime.ClientId)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(err))
                {
                    throw new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = err });
                }

                if (string.IsNullOrEmpty(code))
                {
                    throw new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = "OAuth2 redirect missing code" });
                }

                var auth = await AuthWithOAuth2CodeAsync(
                    providerName,
                    code,
                    DictionaryExtensions.SafeGet(provider, "codeVerifier")?.ToString() ?? string.Empty,
                    redirectUrl,
                    createData,
                    body,
                    query,
                    headers,
                    expand,
                    fields,
                    cancellationToken);

                tcs.TrySetResult(auth);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                tcs.TrySetResult(new Dictionary<string, object?>());
            }
        };

        var unsubscribe = Client.Realtime.Subscribe("@oauth2", handler);
        try
        {
            Client.Realtime.EnsureConnected(TimeSpan.FromSeconds(10));
            var state = Client.Realtime.ClientId;
            var authUrl = (DictionaryExtensions.SafeGet(provider, "authURL")?.ToString() ?? string.Empty) + redirectUrl;
            var separator = authUrl.Contains("?") ? "&" : "?";
            var url = $"{authUrl}{separator}state={Uri.EscapeDataString(state ?? string.Empty)}";
            if (scopes != null && scopes.Any())
            {
                url += $"&scope={Uri.EscapeDataString(string.Join(" ", scopes))}";
            }
            urlCallback(url);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(180), cancellationToken));
            if (completed != tcs.Task)
            {
                throw new ClientResponseError(response: new Dictionary<string, object?> { ["message"] = "OAuth2 flow timed out" });
            }

            if (errors.Any())
            {
                throw errors.First();
            }

            return tcs.Task.Result;
        }
        finally
        {
            unsubscribe();
        }
    }

    public async Task<Dictionary<string, object?>> AuthRefreshAsync(
        string? expand = null,
        string? fields = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCollectionPath}/auth-refresh",
            new SendOptions { Method = HttpMethod.Post, Body = body, Query = parameters, Headers = headers },
            cancellationToken);
        return AuthResponse(data);
    }

    public Task RequestPasswordResetAsync(
        string email,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["email"] = email
        };
        return Client.SendAsync(
            $"{BaseCollectionPath}/request-password-reset",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task ConfirmPasswordResetAsync(
        string token,
        string password,
        string passwordConfirm,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["token"] = token,
            ["password"] = password,
            ["passwordConfirm"] = passwordConfirm
        };
        return Client.SendAsync(
            $"{BaseCollectionPath}/confirm-password-reset",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public Task RequestVerificationAsync(
        string email,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["email"] = email
        };
        return Client.SendAsync(
            $"{BaseCollectionPath}/request-verification",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task ConfirmVerificationAsync(
        string token,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["token"] = token
        };
        await Client.SendAsync(
            $"{BaseCollectionPath}/confirm-verification",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
        MarkVerified(token);
    }

    public Task RequestEmailChangeAsync(
        string newEmail,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["newEmail"] = newEmail
        };
        return Client.SendAsync(
            $"{BaseCollectionPath}/request-email-change",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task ConfirmEmailChangeAsync(
        string token,
        string password,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["token"] = token,
            ["password"] = password
        };
        await Client.SendAsync(
            $"{BaseCollectionPath}/confirm-email-change",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
        ClearIfSameToken(token);
    }

    public Task<Dictionary<string, object?>> RequestOtpAsync(
        string email,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["email"] = email
        };
        return Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCollectionPath}/request-otp",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = query, Headers = headers },
            cancellationToken);
    }

    public async Task<Dictionary<string, object?>> AuthWithOtpAsync(
        string otpId,
        string password,
        string? expand = null,
        string? fields = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["otpId"] = otpId,
            ["password"] = password
        };
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var data = await Client.SendAsync<Dictionary<string, object?>>(
            $"{BaseCollectionPath}/auth-with-otp",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = parameters, Headers = headers },
            cancellationToken);
        return AuthResponse(data);
    }

    public async Task<BosbaseClient> ImpersonateAsync(
        string recordId,
        int duration,
        string? expand = null,
        string? fields = null,
        IDictionary<string, object?>? body = null,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(body ?? new Dictionary<string, object?>())
        {
            ["duration"] = duration
        };
        var parameters = new Dictionary<string, object?>(query ?? new Dictionary<string, object?>());
        if (expand != null) parameters.TryAdd("expand", expand);
        if (fields != null) parameters.TryAdd("fields", fields);

        var enrichedHeaders = new Dictionary<string, string>(headers ?? new Dictionary<string, string>())
        {
            ["Authorization"] = Client.AuthStore.Token
        };

        var newClient = new BosbaseClient(Client.BaseUrl, Client.Lang);
        var data = await newClient.SendAsync<Dictionary<string, object?>>(
            $"{BaseCollectionPath}/impersonate/{HttpHelpers.EncodePathSegment(recordId)}",
            new SendOptions { Method = HttpMethod.Post, Body = payload, Query = parameters, Headers = enrichedHeaders },
            cancellationToken);
        newClient.AuthStore.Save(DictionaryExtensions.SafeGet(data, "token")?.ToString() ?? string.Empty, DictionaryExtensions.SafeGet(data, "record") as IDictionary<string, object?>);
        return newClient;
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------
    private Dictionary<string, object?> AuthResponse(Dictionary<string, object?> data)
    {
        var token = DictionaryExtensions.SafeGet(data, "token")?.ToString() ?? string.Empty;
        var record = DictionaryExtensions.SafeGet(data, "record") as IDictionary<string, object?>;
        if (!string.IsNullOrWhiteSpace(token) && record != null)
        {
            Client.AuthStore.Save(token, record);
        }
        return data;
    }

    private void MaybeUpdateAuthRecord(Dictionary<string, object?> item)
    {
        var current = Client.AuthStore.Record;
        if (current == null) return;
        if (!string.Equals(DictionaryExtensions.SafeGet(current, "id")?.ToString(), DictionaryExtensions.SafeGet(item, "id")?.ToString(), StringComparison.Ordinal))
        {
            return;
        }
        var collectionMatch = DictionaryExtensions.SafeGet(current, "collectionId")?.ToString();
        if (collectionMatch != CollectionIdOrName && collectionMatch != DictionaryExtensions.SafeGet(current, "collectionName")?.ToString())
        {
            return;
        }

        var merged = new Dictionary<string, object?>(current);
        foreach (var kvp in item)
        {
            merged[kvp.Key] = kvp.Value;
        }
        if (current.TryGetValue("expand", out var expand) && expand is IDictionary<string, object?> currentExpand &&
            item.TryGetValue("expand", out var newExpand) && newExpand is IDictionary<string, object?> newExpandDict)
        {
            var mergedExpand = new Dictionary<string, object?>(currentExpand);
            foreach (var kvp in newExpandDict)
            {
                mergedExpand[kvp.Key] = kvp.Value;
            }
            merged["expand"] = mergedExpand;
        }

        Client.AuthStore.Save(Client.AuthStore.Token, merged);
    }

    private bool IsAuthRecord(string recordId)
    {
        var current = Client.AuthStore.Record;
        return current != null &&
               string.Equals(DictionaryExtensions.SafeGet(current, "id")?.ToString(), recordId, StringComparison.Ordinal) &&
               (string.Equals(DictionaryExtensions.SafeGet(current, "collectionId")?.ToString(), CollectionIdOrName, StringComparison.Ordinal) ||
                string.Equals(DictionaryExtensions.SafeGet(current, "collectionName")?.ToString(), CollectionIdOrName, StringComparison.Ordinal));
    }

    private void MarkVerified(string token)
    {
        var current = Client.AuthStore.Record;
        if (current == null) return;
        var payload = DecodeTokenPayload(token);
        if (payload == null) return;
        if (DictionaryExtensions.SafeGet(current, "id")?.ToString() == DictionaryExtensions.SafeGet(payload, "id")?.ToString() &&
            DictionaryExtensions.SafeGet(current, "collectionId")?.ToString() == DictionaryExtensions.SafeGet(payload, "collectionId")?.ToString() &&
            DictionaryExtensions.SafeGet(current, "verified") == null)
        {
            current["verified"] = true;
            Client.AuthStore.Save(Client.AuthStore.Token, current);
        }
    }

    private void ClearIfSameToken(string token)
    {
        var current = Client.AuthStore.Record;
        var payload = DecodeTokenPayload(token);
        if (current != null && payload != null &&
            DictionaryExtensions.SafeGet(current, "id")?.ToString() == DictionaryExtensions.SafeGet(payload, "id")?.ToString() &&
            DictionaryExtensions.SafeGet(current, "collectionId")?.ToString() == DictionaryExtensions.SafeGet(payload, "collectionId")?.ToString())
        {
            Client.AuthStore.Clear();
        }
    }

    private Dictionary<string, object?>? DecodeTokenPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        var payloadPart = parts[1];
        payloadPart += new string('=', (4 - payloadPart.Length % 4) % 4);
        try
        {
            var decoded = Convert.FromBase64String(payloadPart.Replace('-', '+').Replace('_', '/'));
            var json = Encoding.UTF8.GetString(decoded);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------------
    // External Auth Methods (deprecated - use collection("_externalAuths") instead)
    // ------------------------------------------------------------------

    /// <summary>
    /// @deprecated use collection("_externalAuths").*
    /// Lists all linked external auth providers for the specified auth record.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> ListExternalAuthsAsync(
        string recordId,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var filter = Client.Filter("recordRef = {:id}", new Dictionary<string, object?> { ["id"] = recordId });
        var fullList = await Client.Collection("_externalAuths").GetFullListAsync(
            filter: filter,
            query: query,
            headers: headers,
            cancellationToken: cancellationToken);
        return fullList.OfType<Dictionary<string, object?>>().ToList();
    }

    /// <summary>
    /// @deprecated use collection("_externalAuths").*
    /// Unlink a single external auth provider from the specified auth record.
    /// </summary>
    public async Task UnlinkExternalAuthAsync(
        string recordId,
        string provider,
        IDictionary<string, object?>? query = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var filter = Client.Filter("recordRef = {:recordId} && provider = {:provider}", new Dictionary<string, object?>
        {
            ["recordId"] = recordId,
            ["provider"] = provider
        });
        var ea = await Client.Collection("_externalAuths").GetFirstListItemAsync(
            filter: filter,
            query: query,
            headers: headers,
            cancellationToken: cancellationToken);
        await Client.Collection("_externalAuths").DeleteAsync(
            DictionaryExtensions.SafeGet(ea, "id")?.ToString() ?? string.Empty,
            query: query,
            headers: headers,
            cancellationToken: cancellationToken);
    }
}
