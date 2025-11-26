# OAuth2 Configuration Guide - C# SDK Documentation

This guide explains how to configure OAuth2 authentication providers for auth collections using the BosBase C# SDK.

## Overview

OAuth2 allows users to authenticate with your application using third-party providers like Google, GitHub, Facebook, etc. Before you can use OAuth2 authentication, you need to:

1. **Create an OAuth2 app** in the provider's dashboard
2. **Obtain Client ID and Client Secret** from the provider
3. **Register a redirect URL** (typically: `https://yourdomain.com/api/oauth2-redirect`)
4. **Configure the provider** in your BosBase auth collection using the SDK

## Prerequisites

- An auth collection in your BosBase instance
- OAuth2 app credentials (Client ID and Client Secret) from your chosen provider
- Admin/superuser authentication to configure collections

## Supported Providers

The following OAuth2 providers are supported:

- **google** - Google OAuth2
- **github** - GitHub OAuth2
- **gitlab** - GitLab OAuth2
- **discord** - Discord OAuth2
- **facebook** - Facebook OAuth2
- **microsoft** - Microsoft OAuth2
- **apple** - Apple Sign In
- **twitter** - Twitter OAuth2
- **spotify** - Spotify OAuth2
- **kakao** - Kakao OAuth2
- **twitch** - Twitch OAuth2
- **strava** - Strava OAuth2
- **vk** - VK OAuth2
- **yandex** - Yandex OAuth2
- **patreon** - Patreon OAuth2
- **linkedin** - LinkedIn OAuth2
- **instagram** - Instagram OAuth2
- **vimeo** - Vimeo OAuth2
- **digitalocean** - DigitalOcean OAuth2
- **bitbucket** - Bitbucket OAuth2
- **dropbox** - Dropbox OAuth2
- **planningcenter** - Planning Center OAuth2
- **notion** - Notion OAuth2
- **linear** - Linear OAuth2
- **oidc**, **oidc2**, **oidc3** - OpenID Connect (OIDC) providers

## Basic Usage

### 1. Enable OAuth2 for a Collection

First, enable OAuth2 authentication for your auth collection:

```csharp
using Bosbase;

var client = new BosbaseClient("https://your-instance.com");

// Authenticate as admin
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Enable OAuth2 for the "users" collection
// Note: This is typically done through collection update
var collection = await client.Collections.GetOneAsync("users");
// Update collection to enable OAuth2 (implementation depends on SDK)
```

### 2. Add an OAuth2 Provider

Add a provider configuration to your collection. You'll need the URLs and credentials from your OAuth2 app:

```csharp
// Add Google OAuth2 provider
// Note: Actual method names may vary - check SDK implementation
await client.Collections.UpdateAsync("users", new Dictionary<string, object?>
{
    ["oauth2"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["providers"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["name"] = "google",
                ["clientId"] = "your-google-client-id",
                ["clientSecret"] = "your-google-client-secret",
                ["authURL"] = "https://accounts.google.com/o/oauth2/v2/auth",
                ["tokenURL"] = "https://oauth2.googleapis.com/token",
                ["userInfoURL"] = "https://www.googleapis.com/oauth2/v2/userinfo",
                ["displayName"] = "Google",
                ["pkce"] = true // Optional: enable PKCE if supported
            }
        }
    }
});
```

### 3. Configure Field Mapping

Map OAuth2 provider fields to your collection fields:

```csharp
await client.Collections.UpdateAsync("users", new Dictionary<string, object?>
{
    ["oauth2"] = new Dictionary<string, object?>
    {
        ["mappedFields"] = new Dictionary<string, object?>
        {
            ["name"] = "name",        // OAuth2 "name" → collection "name"
            ["email"] = "email",      // OAuth2 "email" → collection "email"
            ["avatarUrl"] = "avatar"  // OAuth2 "avatarUrl" → collection "avatar"
        }
    }
});
```

## Complete Example

Here's a complete example of setting up Google OAuth2:

```csharp
using Bosbase;

var client = new BosbaseClient("https://your-instance.com");

// Authenticate as admin
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

try
{
    // Get current collection
    var collection = await client.Collections.GetOneAsync("users");
    
    // Update collection with OAuth2 configuration
    var updateData = new Dictionary<string, object?>(collection)
    {
        ["oauth2"] = new Dictionary<string, object?>
        {
            ["enabled"] = true,
            ["providers"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["name"] = "google",
                    ["clientId"] = "your-google-client-id.apps.googleusercontent.com",
                    ["clientSecret"] = "your-google-client-secret",
                    ["authURL"] = "https://accounts.google.com/o/oauth2/v2/auth",
                    ["tokenURL"] = "https://oauth2.googleapis.com/token",
                    ["userInfoURL"] = "https://www.googleapis.com/oauth2/v2/userinfo",
                    ["displayName"] = "Google",
                    ["pkce"] = true
                }
            },
            ["mappedFields"] = new Dictionary<string, object?>
            {
                ["name"] = "name",
                ["email"] = "email",
                ["avatarUrl"] = "avatar"
            }
        }
    };
    
    await client.Collections.UpdateAsync("users", updateData);
    
    Console.WriteLine("OAuth2 configuration completed successfully!");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error configuring OAuth2: {ex.Message}");
}
```

## Provider-Specific Examples

### GitHub

```csharp
var provider = new Dictionary<string, object?>
{
    ["name"] = "github",
    ["clientId"] = "your-github-client-id",
    ["clientSecret"] = "your-github-client-secret",
    ["authURL"] = "https://github.com/login/oauth/authorize",
    ["tokenURL"] = "https://github.com/login/oauth/access_token",
    ["userInfoURL"] = "https://api.github.com/user",
    ["displayName"] = "GitHub",
    ["pkce"] = false
};
```

### Discord

```csharp
var provider = new Dictionary<string, object?>
{
    ["name"] = "discord",
    ["clientId"] = "your-discord-client-id",
    ["clientSecret"] = "your-discord-client-secret",
    ["authURL"] = "https://discord.com/api/oauth2/authorize",
    ["tokenURL"] = "https://discord.com/api/oauth2/token",
    ["userInfoURL"] = "https://discord.com/api/users/@me",
    ["displayName"] = "Discord",
    ["pkce"] = true
};
```

### Microsoft

```csharp
var provider = new Dictionary<string, object?>
{
    ["name"] = "microsoft",
    ["clientId"] = "your-microsoft-client-id",
    ["clientSecret"] = "your-microsoft-client-secret",
    ["authURL"] = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
    ["tokenURL"] = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
    ["userInfoURL"] = "https://graph.microsoft.com/v1.0/me",
    ["displayName"] = "Microsoft",
    ["pkce"] = true
};
```

## Important Notes

1. **Redirect URL**: When creating your OAuth2 app in the provider's dashboard, you must register the redirect URL as: `https://yourdomain.com/api/oauth2-redirect`

2. **Provider Names**: The `name` field must match one of the supported provider names exactly (case-sensitive).

3. **PKCE Support**: Some providers support PKCE (Proof Key for Code Exchange) for enhanced security. Check your provider's documentation to determine if PKCE should be enabled.

4. **Client Secret Security**: Never expose your client secret in client-side code. These configuration methods should only be called from server-side code or with proper authentication.

5. **Field Mapping**: The mapped fields determine how OAuth2 user data is mapped to your collection fields. Common OAuth2 fields include:
   - `name` - User's full name
   - `email` - User's email address
   - `avatarUrl` - User's avatar/profile picture URL
   - `username` - User's username

6. **Multiple Providers**: You can add multiple OAuth2 providers to the same collection. Users can choose which provider to use during authentication.

## Error Handling

All methods throw `ClientResponseError` if something goes wrong:

```csharp
try
{
    await client.Collections.UpdateAsync("users", oauth2Config);
}
catch (ClientResponseError ex)
{
    if (ex.Status == 400)
    {
        Console.Error.WriteLine($"Invalid provider configuration: {ex.Message}");
    }
    else if (ex.Status == 403)
    {
        Console.Error.WriteLine("Permission denied. Make sure you are authenticated as admin.");
    }
    else
    {
        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
    }
}
```

## Using OAuth2 Authentication

After configuring OAuth2 providers, users can authenticate using the `AuthWithOAuth2Async()` method:

```csharp
// Authenticate with OAuth2
var authData = await client.Collection("users").AuthWithOAuth2Async(new Dictionary<string, object?>
{
    ["provider"] = "google"
});

Console.WriteLine($"Authenticated: {authData.Record["email"]}");
```

## Related Documentation

- [Authentication](./AUTHENTICATION.md) - Using OAuth2 authentication
- [Collection API](./COLLECTION_API.md) - Collection management
- [Users Collection Guide](./USERS_COLLECTION_GUIDE.md) - Working with users collection

