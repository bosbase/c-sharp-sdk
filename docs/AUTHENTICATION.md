# Authentication - C# SDK Documentation

## Overview

Authentication in BosBase is stateless and token-based. A client is considered authenticated as long as it sends a valid `Authorization: YOUR_AUTH_TOKEN` header with requests.

**Key Points:**
- **No sessions**: BosBase APIs are fully stateless (tokens are not stored in the database)
- **No logout endpoint**: To "logout", simply clear the token from your local state (`pb.AuthStore.Clear()`)
- **Token generation**: Auth tokens are generated through auth collection Web APIs or programmatically
- **Admin users**: `_superusers` collection works like regular auth collections but with full access (API rules are ignored)
- **OAuth2 limitation**: OAuth2 is not supported for `_superusers` collection

## Authentication Methods

BosBase supports multiple authentication methods that can be configured individually for each auth collection:

1. **Password Authentication** - Email/username + password
2. **OTP Authentication** - One-time password via email
3. **OAuth2 Authentication** - Google, GitHub, Microsoft, etc.
4. **Multi-factor Authentication (MFA)** - Requires 2 different auth methods

## Authentication Store

The SDK maintains an `AuthStore` that automatically manages the authentication state:

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");

// Check authentication status
Console.WriteLine(pb.AuthStore.IsValid());      // true/false
Console.WriteLine(pb.AuthStore.Token);          // current auth token
Console.WriteLine(pb.AuthStore.Record);         // authenticated user record

// Clear authentication (logout)
pb.AuthStore.Clear();
```

## Password Authentication

Authenticate using email/username and password. The identity field can be configured in the collection options (default is email).

**Backend Endpoint:** `POST /api/collections/{collection}/auth-with-password`

### Basic Usage

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");

// Authenticate with email and password
var authData = await pb.Collection("users").AuthWithPasswordAsync(
    "test@example.com",
    "password123"
);

// Auth data is automatically stored in pb.AuthStore
Console.WriteLine(pb.AuthStore.IsValid());  // true
Console.WriteLine(pb.AuthStore.Token);      // JWT token
var record = pb.AuthStore.Record;
Console.WriteLine(record?["id"]);          // user record ID
```

### Response Format

```csharp
{
    "token": "eyJhbGciOiJIUzI1NiJ9...",
    "record": {
        "id": "record_id",
        "email": "test@example.com",
        // ... other user fields
    }
}
```

### Error Handling with MFA

```csharp
try
{
    await pb.Collection("users").AuthWithPasswordAsync("test@example.com", "pass123");
}
catch (ClientResponseError err)
{
    // Check for MFA requirement
    if (err.Response?.TryGetValue("mfaId", out var mfaIdObj))
    {
        var mfaId = mfaIdObj?.ToString();
        // Handle MFA flow (see Multi-factor Authentication section)
    }
    else
    {
        Console.Error.WriteLine($"Authentication failed: {err}");
    }
}
```

## OTP Authentication

One-time password authentication via email.

**Backend Endpoints:**
- `POST /api/collections/{collection}/request-otp` - Request OTP
- `POST /api/collections/{collection}/auth-with-otp` - Authenticate with OTP

### Request OTP

```csharp
// Send OTP to user's email
var result = await pb.Collection("users").RequestOtpAsync("test@example.com");
Console.WriteLine(result["otpId"]);  // OTP ID to use in AuthWithOtpAsync
```

### Authenticate with OTP

```csharp
// Step 1: Request OTP
var result = await pb.Collection("users").RequestOtpAsync("test@example.com");

// Step 2: User enters OTP from email
var authData = await pb.Collection("users").AuthWithOtpAsync(
    result["otpId"]?.ToString() ?? "",
    "123456"  // OTP code from email
);
```

## OAuth2 Authentication

**Backend Endpoint:** `POST /api/collections/{collection}/auth-with-oauth2`

### All-in-One Method (Recommended)

```csharp
using Bosbase;

var pb = new BosbaseClient("https://bosbase.io");

// Opens popup window with OAuth2 provider page (in web context)
var authData = await pb.Collection("users").AuthWithOAuth2Async(
    "google",
    url => {
        // In a web app, open the URL in a popup or redirect
        // Process.Start(url);  // For desktop apps
        // window.open(url);    // For web apps
    }
);

Console.WriteLine(pb.AuthStore.Token);
Console.WriteLine(pb.AuthStore.Record);
```

### Manual Code Exchange

```csharp
// Get auth methods
var authMethods = await pb.Collection("users").ListAuthMethodsAsync();
var oauth2 = authMethods["oauth2"] as Dictionary<string, object?>;
var providers = oauth2?["providers"] as List<object?>;
var provider = providers?
    .Cast<Dictionary<string, object?>>()
    .FirstOrDefault(p => p["name"]?.ToString() == "google");

// Exchange code for token (after OAuth2 redirect)
var authData = await pb.Collection("users").AuthWithOAuth2CodeAsync(
    provider?["name"]?.ToString() ?? "",
    code,
    provider?["codeVerifier"]?.ToString() ?? "",
    redirectUrl
);
```

## Multi-Factor Authentication (MFA)

Requires 2 different auth methods.

```csharp
string? mfaId = null;

try
{
    // First auth method (password)
    await pb.Collection("users").AuthWithPasswordAsync("test@example.com", "pass123");
}
catch (ClientResponseError err)
{
    if (err.Response?.TryGetValue("mfaId", out var mfaIdObj))
    {
        mfaId = mfaIdObj?.ToString();
        
        // Second auth method (OTP)
        var otpResult = await pb.Collection("users").RequestOtpAsync("test@example.com");
        await pb.Collection("users").AuthWithOtpAsync(
            otpResult["otpId"]?.ToString() ?? "",
            "123456",
            body: new Dictionary<string, object?> { ["mfaId"] = mfaId }
        );
    }
}
```

## User Impersonation

Superusers can impersonate other users.

**Backend Endpoint:** `POST /api/collections/{collection}/impersonate/{id}`

```csharp
// Authenticate as superuser
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "adminpass");

// Impersonate a user
var impersonateClient = await pb.Collection("users").ImpersonateAsync(
    "USER_RECORD_ID",
    3600  // Optional: token duration in seconds
);

// Use impersonate client
var data = await impersonateClient.Collection("posts").GetFullListAsync();
```

## Auth Token Verification

Verify token by calling `AuthRefreshAsync()`.

**Backend Endpoint:** `POST /api/collections/{collection}/auth-refresh`

```csharp
try
{
    var authData = await pb.Collection("users").AuthRefreshAsync();
    Console.WriteLine("Token is valid");
}
catch (Exception err)
{
    Console.Error.WriteLine($"Token verification failed: {err}");
    pb.AuthStore.Clear();
}
```

## List Available Auth Methods

**Backend Endpoint:** `GET /api/collections/{collection}/auth-methods`

```csharp
var authMethods = await pb.Collection("users").ListAuthMethodsAsync();
var password = authMethods["password"] as Dictionary<string, object?>;
var oauth2 = authMethods["oauth2"] as Dictionary<string, object?>;
var mfa = authMethods["mfa"] as Dictionary<string, object?>;

Console.WriteLine(password?["enabled"]);
Console.WriteLine(oauth2?["providers"]);
Console.WriteLine(mfa?["enabled"]);
```

## Complete Examples

See the full documentation for detailed examples of:
- Full authentication flow
- OAuth2 integration
- Token management
- Admin impersonation
- Error handling

## Related Documentation

- [Collections](./COLLECTIONS.md)
- [API Rules](./API_RULES_AND_FILTERS.md)

## Detailed Examples

### Example 1: Complete Authentication Flow with Error Handling

```csharp
using Bosbase;
using Bosbase.Exceptions;

var pb = new BosbaseClient("http://localhost:8090");

async Task<Dictionary<string, object?>> AuthenticateUserAsync(string email, string password)
{
    try
    {
        // Try password authentication
        var authData = await pb.Collection("users").AuthWithPasswordAsync(email, password);
        
        Console.WriteLine($"Successfully authenticated: {authData["record"]}");
        return authData;
        
    }
    catch (ClientResponseError err)
    {
        // Check if MFA is required
        if (err.Status == 401 && err.Response?.TryGetValue("mfaId", out var mfaIdObj))
        {
            Console.WriteLine("MFA required, proceeding with second factor...");
            return await HandleMfaAsync(email, mfaIdObj?.ToString() ?? "");
        }
        
        // Handle other errors
        if (err.Status == 400)
        {
            throw new Exception("Invalid credentials");
        }
        else if (err.Status == 403)
        {
            throw new Exception("Password authentication is not enabled for this collection");
        }
        else
        {
            throw;
        }
    }
}

async Task<Dictionary<string, object?>> HandleMfaAsync(string email, string mfaId)
{
    // Request OTP for second factor
    var otpResult = await pb.Collection("users").RequestOtpAsync(email);
    
    // In a real app, show a modal/form for the user to enter OTP
    // For this example, we'll simulate getting the OTP
    var userEnteredOTP = await GetUserOtpInputAsync(); // Your UI function
    
    try
    {
        // Authenticate with OTP and MFA ID
        var authData = await pb.Collection("users").AuthWithOtpAsync(
            otpResult["otpId"]?.ToString() ?? "",
            userEnteredOTP,
            body: new Dictionary<string, object?> { ["mfaId"] = mfaId }
        );
        
        Console.WriteLine("MFA authentication successful");
        return authData;
    }
    catch (ClientResponseError err)
    {
        if (err.Status == 429)
        {
            throw new Exception("Too many OTP attempts, please request a new OTP");
        }
        throw new Exception("Invalid OTP code");
    }
}

// Usage
try
{
    await AuthenticateUserAsync("user@example.com", "password123");
    Console.WriteLine($"User is authenticated: {pb.AuthStore.Record}");
}
catch (Exception err)
{
    Console.Error.WriteLine($"Authentication failed: {err.Message}");
}
```

### Example 2: OAuth2 Integration

```csharp
using Bosbase;
using Bosbase.Exceptions;

var pb = new BosbaseClient("https://your-domain.com");

// Setup OAuth2 login
async Task HandleOAuth2LoginAsync()
{
    try
    {
        // Check available providers first
        var authMethods = await pb.Collection("users").ListAuthMethodsAsync();
        var oauth2 = authMethods["oauth2"] as Dictionary<string, object?>;
        
        if (oauth2?["enabled"]?.ToString() != "true")
        {
            Console.WriteLine("OAuth2 is not enabled for this collection");
            return;
        }
        
        var providers = oauth2?["providers"] as List<object?>;
        var googleProvider = providers?
            .Cast<Dictionary<string, object?>>()
            .FirstOrDefault(p => p["name"]?.ToString() == "google");
        
        if (googleProvider == null)
        {
            Console.WriteLine("Google OAuth2 is not configured");
            return;
        }
        
        // Authenticate with Google (opens popup/redirect)
        var authData = await pb.Collection("users").AuthWithOAuth2Async(
            "google",
            url => {
                // In a web app: window.open(url);
                // In a desktop app: Process.Start(url);
                Console.WriteLine($"Open URL: {url}");
            }
        );
        
        // Check if this is a new user
        var meta = authData.TryGetValue("meta", out var metaObj) ? metaObj as Dictionary<string, object?> : null;
        if (meta?["isNew"]?.ToString() == "true")
        {
            Console.WriteLine("Welcome new user!", authData["record"]);
            // Redirect to onboarding
        }
        else
        {
            Console.WriteLine("Welcome back!", authData["record"]);
            // Redirect to dashboard
        }
        
    }
    catch (ClientResponseError err)
    {
        if (err.Status == 403)
        {
            Console.WriteLine("OAuth2 authentication is not enabled");
        }
        else
        {
            Console.Error.WriteLine($"OAuth2 authentication failed: {err}");
            Console.WriteLine("Login failed. Please try again.");
        }
    }
}
```

### Example 3: Token Management and Refresh

> **BosBase note:** Calls to `pb.Collection("users").AuthWithPasswordAsync()` now return static, non-expiring tokens. Environment variables can no longer shorten their lifetime, so the refresh logic below is only required for custom auth collections, impersonation flows, or any token you mint manually.

```csharp
using Bosbase;
using System.Text.Json;

var pb = new BosbaseClient("http://localhost:8090");

// Check if user is already authenticated
async Task<bool> CheckAuthAsync()
{
    if (pb.AuthStore.IsValid())
    {
        var record = pb.AuthStore.Record;
        Console.WriteLine($"User is authenticated: {record?["email"]}");
        
        // Verify token is still valid and refresh if needed
        try
        {
            await pb.Collection("users").AuthRefreshAsync();
            Console.WriteLine("Token refreshed successfully");
            return true;
        }
        catch (Exception err)
        {
            Console.WriteLine("Token expired or invalid, clearing auth");
            pb.AuthStore.Clear();
            return false;
        }
    }
    return false;
}

// Auto-refresh token before expiration
async Task SetupAutoRefreshAsync()
{
    if (!pb.AuthStore.IsValid()) return;
    
    // Calculate time until token expiration (JWT tokens have exp claim)
    var token = pb.AuthStore.Token;
    var parts = token.Split('.');
    if (parts.Length == 3)
    {
        var payload = parts[1];
        // Pad base64
        payload += new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var doc = JsonDocument.Parse(json);
        
        if (doc.RootElement.TryGetProperty("exp", out var expElement))
        {
            var expSeconds = expElement.GetInt64();
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            var now = DateTimeOffset.UtcNow;
            var timeUntilExpiry = expiresAt - now;
            
            // Refresh 5 minutes before expiration
            var refreshTime = TimeSpan.FromMilliseconds(
                Math.Max(0, (timeUntilExpiry - TimeSpan.FromMinutes(5)).TotalMilliseconds)
            );
            
            await Task.Delay(refreshTime);
            try
            {
                await pb.Collection("users").AuthRefreshAsync();
                Console.WriteLine("Token auto-refreshed");
                await SetupAutoRefreshAsync(); // Schedule next refresh
            }
            catch (Exception err)
            {
                Console.Error.WriteLine($"Auto-refresh failed: {err}");
                pb.AuthStore.Clear();
            }
        }
    }
}

// Usage
var isAuthenticated = await CheckAuthAsync();
if (!isAuthenticated)
{
    // Redirect to login
    Console.WriteLine("Redirect to login");
}
else
{
    await SetupAutoRefreshAsync();
}
```

### Example 4: Admin Impersonation for Support

```csharp
using Bosbase;

var pb = new BosbaseClient("http://localhost:8090");

async Task<Dictionary<string, object?>> ImpersonateUserForSupportAsync(string userId)
{
    // Authenticate as admin
    await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "adminpassword");
    
    // Impersonate the user (1 hour token)
    var userClient = await pb.Collection("users").ImpersonateAsync(userId, 3600);
    
    var userRecord = userClient.AuthStore.Record;
    Console.WriteLine($"Impersonating user: {userRecord?["email"]}");
    
    // Use the impersonated client to test user experience
    var userRecords = await userClient.Collection("posts").GetFullListAsync();
    Console.WriteLine($"User can see {userRecords.Count} posts");
    
    // Check what the user sees
    var userView = await userClient.Collection("posts").GetListAsync(
        page: 1,
        perPage: 10,
        filter: "published = true"
    );
    
    var items = userView.TryGetValue("items", out var itemsObj) ? itemsObj as List<object?> : new List<object?>();
    
    return new Dictionary<string, object?>
    {
        ["canAccess"] = items.Count,
        ["totalPosts"] = userRecords.Count
    };
}

// Usage in support dashboard
try
{
    var result = await ImpersonateUserForSupportAsync("user_record_id");
    Console.WriteLine($"User access check: {result}");
}
catch (Exception err)
{
    Console.Error.WriteLine($"Impersonation failed: {err}");
}
```

### Example 5: API Key Generation for Server-to-Server

```csharp
using Bosbase;

var pb = new BosbaseClient("https://api.example.com");

async Task<Dictionary<string, object?>> GenerateApiKeyAsync(string adminEmail, string adminPassword)
{
    // Authenticate as admin
    await pb.Collection("_superusers").AuthWithPasswordAsync(adminEmail, adminPassword);
    
    // Get superuser ID
    var adminRecord = pb.AuthStore.Record;
    var adminId = adminRecord?["id"]?.ToString();
    
    if (string.IsNullOrEmpty(adminId))
    {
        throw new Exception("Failed to get admin ID");
    }
    
    // Generate impersonation token (1 year duration for long-lived API key)
    var apiClient = await pb.Collection("_superusers").ImpersonateAsync(adminId, 31536000);
    
    var apiKey = new Dictionary<string, object?>
    {
        ["token"] = apiClient.AuthStore.Token,
        ["expiresAt"] = DateTimeOffset.UtcNow.AddSeconds(31536000).ToString("O"),
        ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O")
    };
    
    // Store API key securely (e.g., in environment variables, secret manager)
    var tokenPreview = apiClient.AuthStore.Token.Length > 20 
        ? apiClient.AuthStore.Token.Substring(0, 20) + "..." 
        : apiClient.AuthStore.Token;
    Console.WriteLine($"API Key generated (store securely): {tokenPreview}");
    
    return apiKey;
}

// Usage in server environment
try
{
    var apiKey = await GenerateApiKeyAsync("admin@example.com", "securepassword");
    // Store in your server configuration
    Environment.SetEnvironmentVariable("BOSBASE_API_KEY", apiKey["token"]?.ToString());
}
catch (Exception err)
{
    Console.Error.WriteLine($"Failed to generate API key: {err}");
}

// Using the API key in another service
var serviceClient = new BosbaseClient("https://api.example.com");
var token = Environment.GetEnvironmentVariable("BOSBASE_API_KEY");
if (!string.IsNullOrEmpty(token))
{
    serviceClient.AuthStore.Save(token, new Dictionary<string, object?>
    {
        ["id"] = "superuser_id",
        ["email"] = "admin@example.com"
    });
    
    // Make authenticated requests
    var data = await serviceClient.Collection("records").GetFullListAsync();
}
```

### Example 6: OAuth2 Manual Flow (Advanced)

```csharp
using Bosbase;

var pb = new BosbaseClient("https://your-domain.com");

// Step 1: Get available OAuth2 providers
async Task<List<Dictionary<string, object?>>> GetOAuth2ProvidersAsync()
{
    var authMethods = await pb.Collection("users").ListAuthMethodsAsync();
    var oauth2 = authMethods["oauth2"] as Dictionary<string, object?>;
    var providers = oauth2?["providers"] as List<object?>;
    return providers?
        .Cast<Dictionary<string, object?>>()
        .ToList() ?? new List<Dictionary<string, object?>>();
}

// Step 2: Initiate OAuth2 flow
async Task InitiateOAuth2LoginAsync(string providerName)
{
    var providers = await GetOAuth2ProvidersAsync();
    var provider = providers.FirstOrDefault(p => p["name"]?.ToString() == providerName);
    
    if (provider == null)
    {
        throw new Exception($"Provider {providerName} not available");
    }
    
    // Store provider info for verification (in a real app, use secure storage)
    // sessionStorage["oauth2_provider"] = JsonSerializer.Serialize(provider);
    
    // Redirect to provider's auth URL
    var redirectUrl = "https://yourapp.com/oauth2-callback";
    var authUrl = provider["authURL"]?.ToString() ?? "";
    var separator = authUrl.Contains("?") ? "&" : "?";
    var fullUrl = $"{authUrl}{separator}redirect_url={Uri.EscapeDataString(redirectUrl)}";
    
    // In a web app: window.location.href = fullUrl;
    // In a desktop app: Process.Start(fullUrl);
    Console.WriteLine($"Redirect to: {fullUrl}");
}

// Step 3: Handle OAuth2 callback
async Task HandleOAuth2CallbackAsync(string code, string state, string? error)
{
    if (!string.IsNullOrEmpty(error))
    {
        Console.Error.WriteLine($"OAuth2 error: {error}");
        return;
    }
    
    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
    {
        Console.Error.WriteLine("Missing OAuth2 parameters");
        return;
    }
    
    // Retrieve stored provider info (in a real app, from secure storage)
    // var providerStr = sessionStorage["oauth2_provider"];
    // var provider = JsonSerializer.Deserialize<Dictionary<string, object?>>(providerStr);
    
    // For this example, we'll assume provider info is available
    var provider = new Dictionary<string, object?>(); // Retrieved from storage
    
    // Verify state parameter
    if (provider.TryGetValue("state", out var stateObj) && stateObj?.ToString() != state)
    {
        Console.Error.WriteLine("State parameter mismatch - possible CSRF attack");
        return;
    }
    
    // Exchange code for token
    var redirectUrl = "https://yourapp.com/oauth2-callback";
    
    try
    {
        var authData = await pb.Collection("users").AuthWithOAuth2CodeAsync(
            provider["name"]?.ToString() ?? "",
            code,
            provider["codeVerifier"]?.ToString() ?? "",
            redirectUrl,
            createData: new Dictionary<string, object?>
            {
                // Optional: additional data for new users
                ["emailVisibility"] = false
            }
        );
        
        Console.WriteLine($"OAuth2 authentication successful: {authData["record"]}");
        
        // Clear stored provider info
        // sessionStorage.Remove("oauth2_provider");
        
        // Redirect to app
        Console.WriteLine("Redirect to dashboard");
        
    }
    catch (Exception err)
    {
        Console.Error.WriteLine($"OAuth2 code exchange failed: {err}");
        Console.WriteLine("Authentication failed. Please try again.");
    }
}
```

## Best Practices

1. **Secure Token Storage**: Never expose tokens in client-side code or logs
2. **Token Refresh**: Implement automatic token refresh before expiration
3. **Error Handling**: Always handle MFA requirements and token expiration
4. **OAuth2 Security**: Always validate the `state` parameter in OAuth2 callbacks
5. **API Keys**: Use impersonation tokens for server-to-server communication only
6. **Superuser Tokens**: Never expose superuser impersonation tokens in client code
7. **OTP Security**: Use OTP with MFA for security-critical applications
8. **Rate Limiting**: Be aware of rate limits on authentication endpoints

## Troubleshooting

### Token Expired
If you get 401 errors, check if the token has expired:
```csharp
try
{
    await pb.Collection("users").AuthRefreshAsync();
}
catch (Exception err)
{
    // Token expired, require re-authentication
    pb.AuthStore.Clear();
    // Redirect to login
}
```

### MFA Required
If authentication returns 401 with mfaId:
```csharp
if (err.Status == 401 && err.Response?.TryGetValue("mfaId", out var mfaIdObj))
{
    // Proceed with second authentication factor
}
```

### OAuth2 Popup Blocked
Ensure OAuth2 is triggered from a user interaction (click event), not from async code:
```csharp
// Good - direct click handler
button.Click += (sender, e) =>
{
    pb.Collection("users").AuthWithOAuth2Async("google", url => Process.Start(url));
};

// Bad - async in click handler (may be blocked in Safari)
button.Click += async (sender, e) =>
{
    await SomeAsyncFunction();
    pb.Collection("users").AuthWithOAuth2Async("google", url => Process.Start(url));
};
```

