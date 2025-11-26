# Custom Token Binding and Login - C# SDK Documentation

The C# SDK and BosBase service now support binding a custom token to an auth record (both `users` and `_superusers`) and signing in with that token. The server stores bindings in the `_token_bindings` table (created automatically on first bind; legacy `_tokenBindings`/`tokenBindings` are auto-renamed). Tokens are stored as hashes so raw values aren't persisted.

## API endpoints
- `POST /api/collections/{collection}/bind-token`
- `POST /api/collections/{collection}/unbind-token`
- `POST /api/collections/{collection}/auth-with-token`

## Binding a token

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// bind for a regular user
await client.Collection("users").BindCustomTokenAsync(
    "user@example.com",
    "user-password",
    "my-app-token"
);

// bind for a superuser
await client.Collection("_superusers").BindCustomTokenAsync(
    "admin@example.com",
    "admin-password",
    "admin-app-token"
);
```

## Unbinding a token

```csharp
// stop accepting the token for the user
await client.Collection("users").UnbindCustomTokenAsync(
    "user@example.com",
    "user-password",
    "my-app-token"
);

// stop accepting the token for a superuser
await client.Collection("_superusers").UnbindCustomTokenAsync(
    "admin@example.com",
    "admin-password",
    "admin-app-token"
);
```

## Logging in with a token

```csharp
// login with the previously bound token
var auth = await client.Collection("users").AuthWithTokenAsync("my-app-token");

Console.WriteLine(auth.Token);  // BosBase auth token
Console.WriteLine(auth.Record); // authenticated record

// superuser token login
var superAuth = await client.Collection("_superusers").AuthWithTokenAsync("admin-app-token");
Console.WriteLine(superAuth.Token);
Console.WriteLine(superAuth.Record);
```

## Complete Example

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

try
{
    // Step 1: Bind a custom token for a user
    await client.Collection("users").BindCustomTokenAsync(
        "user@example.com",
        "password123",
        "my-custom-app-token"
    );
    Console.WriteLine("Token bound successfully");

    // Step 2: Authenticate using the custom token
    var authData = await client.Collection("users").AuthWithTokenAsync("my-custom-app-token");
    Console.WriteLine($"Authenticated as: {authData.Record["email"]}");
    Console.WriteLine($"Auth token: {authData.Token}");

    // Step 3: Use the authenticated client
    var records = await client.Collection("posts").GetListAsync(1, 10);

    // Step 4: Unbind the token when done
    await client.Collection("users").UnbindCustomTokenAsync(
        "user@example.com",
        "password123",
        "my-custom-app-token"
    );
    Console.WriteLine("Token unbound successfully");
}
catch (ClientResponseError ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    if (ex.Status == 400)
    {
        Console.Error.WriteLine("Invalid credentials or token");
    }
    else if (ex.Status == 401)
    {
        Console.Error.WriteLine("Authentication failed");
    }
}
```

## Error Handling

```csharp
try
{
    await client.Collection("users").BindCustomTokenAsync(
        email,
        password,
        token
    );
}
catch (ClientResponseError ex)
{
    if (ex.Status == 400)
    {
        Console.Error.WriteLine("Invalid email, password, or token");
    }
    else if (ex.Status == 401)
    {
        Console.Error.WriteLine("Authentication failed");
    }
    else
    {
        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    }
}
```

## Notes

- Binding and unbinding require a valid email and password for the target account.
- The same token value can be used for either `users` or `_superusers` collections; the collection is enforced during login.
- MFA and existing auth rules still apply when authenticating with a token.

## Related Documentation

- [Authentication](./AUTHENTICATION.md) - General authentication guide
- [API Records](./API_RECORDS.md) - Record operations

