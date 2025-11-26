# Management API Documentation - C# SDK

This document covers the management API capabilities available in the C# SDK, which correspond to the features available in the backend management UI.

> **Note**: All management API operations require superuser authentication (🔐).

## Table of Contents

- [Settings Service](#settings-service)
  - [Application Configuration](#application-configuration)
  - [Mail Configuration](#mail-configuration)
  - [Storage Configuration](#storage-configuration)
  - [Backup Configuration](#backup-configuration)
  - [Log Configuration](#log-configuration)
- [Backup Service](#backup-service)
- [Log Service](#log-service)
- [Cron Service](#cron-service)
- [Health Service](#health-service)
- [Collection Service](#collection-service)

---

## Settings Service

The Settings Service provides comprehensive management of application settings, matching the capabilities available in the backend management UI.

### Application Configuration

Manage application settings including meta information, trusted proxy, rate limits, and batch configuration.

#### Get Application Settings

```csharp
var settings = await client.Settings.GetAllAsync();
// Returns: Dictionary with meta, trustedProxy, rateLimits, batch
```

**Example:**
```csharp
var appSettings = await client.Settings.GetAllAsync();
var meta = appSettings["meta"] as Dictionary<string, object?>;
Console.WriteLine(meta?["appName"]);
```

#### Update Application Settings

```csharp
await client.Settings.UpdateAsync(new Dictionary<string, object?>
{
    ["meta"] = new Dictionary<string, object?>
    {
        ["appName"] = "My App",
        ["appURL"] = "https://example.com",
        ["hideControls"] = false
    },
    ["trustedProxy"] = new Dictionary<string, object?>
    {
        ["headers"] = new[] { "X-Forwarded-For" },
        ["useLeftmostIP"] = true
    },
    ["rateLimits"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["rules"] = new[]
        {
            new Dictionary<string, object?>
            {
                ["label"] = "api/users",
                ["duration"] = 3600,
                ["maxRequests"] = 100
            }
        }
    },
    ["batch"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["maxRequests"] = 100,
        ["interval"] = 200
    }
});
```

---

### Mail Configuration

Manage SMTP email settings and sender information.

#### Get Mail Settings

```csharp
var mailSettings = await client.Settings.GetAllAsync();
// Access mail settings from the response
var meta = mailSettings["meta"] as Dictionary<string, object?>;
var smtp = mailSettings["smtp"] as Dictionary<string, object?>;
```

#### Update Mail Settings

```csharp
await client.Settings.UpdateAsync(new Dictionary<string, object?>
{
    ["senderName"] = "My App",
    ["senderAddress"] = "noreply@example.com",
    ["smtp"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["host"] = "smtp.example.com",
        ["port"] = 587,
        ["username"] = "user@example.com",
        ["password"] = "password",
        ["authMethod"] = "PLAIN",
        ["tls"] = true,
        ["localName"] = "localhost"
    }
});
```

#### Test Email

```csharp
await client.Settings.TestEmailAsync(
    "test@example.com",
    "verification", // template: verification, password-reset, email-change, otp, login-alert
    "_superusers" // collection (optional, defaults to _superusers)
);
```

**Email Templates:**
- `verification` - Email verification template
- `password-reset` - Password reset template
- `email-change` - Email change confirmation template
- `otp` - One-time password template
- `login-alert` - Login alert template

---

### Storage Configuration

Manage S3 storage configuration for file storage.

#### Get Storage S3 Configuration

```csharp
var settings = await client.Settings.GetAllAsync();
var s3Config = settings["storage"] as Dictionary<string, object?>;
// Returns: { enabled, bucket, region, endpoint, accessKey, secret, forcePathStyle }
```

#### Update Storage S3 Configuration

```csharp
await client.Settings.UpdateAsync(new Dictionary<string, object?>
{
    ["storage"] = new Dictionary<string, object?>
    {
        ["enabled"] = true,
        ["bucket"] = "my-bucket",
        ["region"] = "us-east-1",
        ["endpoint"] = "https://s3.amazonaws.com",
        ["accessKey"] = "ACCESS_KEY",
        ["secret"] = "SECRET_KEY",
        ["forcePathStyle"] = false
    }
});
```

#### Test Storage S3 Connection

```csharp
await client.Settings.TestS3Async("storage");
// Returns success if connection succeeds
```

---

### Backup Configuration

Manage auto-backup scheduling and S3 storage for backups.

#### Get Backup Settings

```csharp
var settings = await client.Settings.GetAllAsync();
var backupSettings = settings["backup"] as Dictionary<string, object?>;
var cron = backupSettings?["cron"]?.ToString();
var cronMaxKeep = backupSettings?["cronMaxKeep"];
```

#### Update Backup Settings

```csharp
await client.Settings.UpdateAsync(new Dictionary<string, object?>
{
    ["backup"] = new Dictionary<string, object?>
    {
        ["cron"] = "0 0 * * *", // Daily at midnight (empty string to disable)
        ["cronMaxKeep"] = 10, // Keep maximum 10 backups
        ["s3"] = new Dictionary<string, object?>
        {
            ["enabled"] = true,
            ["bucket"] = "backup-bucket",
            ["region"] = "us-east-1",
            ["endpoint"] = "https://s3.amazonaws.com",
            ["accessKey"] = "ACCESS_KEY",
            ["secret"] = "SECRET_KEY",
            ["forcePathStyle"] = false
        }
    }
});
```

**Common Cron Expressions:**
- `"0 0 * * *"` - Daily at midnight
- `"0 0 * * 0"` - Weekly on Sunday at midnight
- `"0 0 1 * *"` - Monthly on the 1st at midnight
- `"0 0 * * 1,3"` - Twice weekly (Monday and Wednesday)

---

### Log Configuration

Manage log retention and logging settings.

#### Get Log Settings

```csharp
var settings = await client.Settings.GetAllAsync();
var logSettings = settings["logs"] as Dictionary<string, object?>;
// Returns: { maxDays, minLevel, logIP, logAuthId }
```

#### Update Log Settings

```csharp
await client.Settings.UpdateAsync(new Dictionary<string, object?>
{
    ["logs"] = new Dictionary<string, object?>
    {
        ["maxDays"] = 30, // Retain logs for 30 days
        ["minLevel"] = 0, // Minimum log level (negative=debug/info, 0=warning, positive=error)
        ["logIP"] = true, // Log IP addresses
        ["logAuthId"] = true // Log authentication IDs
    }
});
```

**Log Levels:**
- Negative values: Debug/Info levels
- `0`: Default/Warning level
- Positive values: Error levels

---

## Backup Service

Manage application backups - create, list, upload, delete, and restore backups.

### List All Backups

```csharp
var backups = await client.Backups.GetFullListAsync();
// Returns: List<Dictionary<string, object?>> with key, size, modified
```

**Example:**
```csharp
var backups = await client.Backups.GetFullListAsync();
foreach (var backup in backups)
{
    Console.WriteLine($"{backup["key"]}: {backup["size"]} bytes, modified: {backup["modified"]}");
}
```

### Create Backup

```csharp
await client.Backups.CreateAsync("backup-2024-01-01");
// Creates a new backup with the specified basename
```

### Upload Backup

Upload an existing backup file:

```csharp
using Bosbase.Models;

var fileAttachment = FileAttachment.FromPath("file", "/path/to/backup.zip", "application/zip");
await client.Backups.UploadAsync(new[] { fileAttachment });
```

### Delete Backup

```csharp
await client.Backups.DeleteAsync("backup-2024-01-01");
// Deletes the specified backup file
```

### Restore Backup

```csharp
await client.Backups.RestoreAsync("backup-2024-01-01");
// Restores the application from the specified backup
```

**⚠️ Warning**: Restoring a backup will replace all current application data!

### Get Backup Download URL

```csharp
// First, get a file token
var token = await client.Files.GetTokenAsync();

// Then build the download URL
var url = client.Backups.GetDownloadUrl(token, "backup-2024-01-01");
Console.WriteLine(url); // Full URL to download the backup
```

---

## Log Service

Query and analyze application logs.

### List Logs

```csharp
var result = await client.Logs.GetListAsync(1, 30, 
    filter: "level >= 0",
    sort: "-created"
);
// Returns: Dictionary with page, perPage, totalItems, totalPages, items
```

**Example with filtering:**
```csharp
// Get error logs from the last 24 hours
var yesterday = DateTime.UtcNow.AddDays(-1);

var errorLogs = await client.Logs.GetListAsync(1, 50, 
    filter: $"level > 0 && created >= \"{yesterday:yyyy-MM-ddTHH:mm:ss}\"",
    sort: "-created"
);

if (errorLogs["items"] is List<object?> items)
{
    foreach (var log in items)
    {
        if (log is Dictionary<string, object?> logDict)
        {
            Console.WriteLine($"[{logDict["level"]}] {logDict["message"]}");
        }
    }
}
```

### Get Single Log

```csharp
var log = await client.Logs.GetOneAsync("log-id");
// Returns: Dictionary with full log details
```

### Get Log Statistics

```csharp
var stats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
{
    ["filter"] = "level >= 0" // Optional filter
});
// Returns: List<Dictionary<string, object?>> with total, date - hourly statistics
```

**Example:**
```csharp
var stats = await client.Logs.GetStatsAsync();
if (stats != null)
{
    foreach (var stat in stats)
    {
        Console.WriteLine($"{stat["date"]}: {stat["total"]} requests");
    }
}
```

---

## Cron Service

Manage and execute cron jobs.

### List All Cron Jobs

```csharp
var cronJobs = await client.Crons.GetFullListAsync();
// Returns: List<Dictionary<string, object?>> with id, expression
```

**Example:**
```csharp
var cronJobs = await client.Crons.GetFullListAsync();
foreach (var job in cronJobs)
{
    Console.WriteLine($"Job {job["id"]}: {job["expression"]}");
}
```

### Run Cron Job

Manually trigger a cron job:

```csharp
await client.Crons.RunAsync("job-id");
// Executes the specified cron job immediately
```

**Example:**
```csharp
var cronJobs = await client.Crons.GetFullListAsync();
var backupJob = cronJobs.FirstOrDefault(j => j["id"]?.ToString()?.Contains("backup") == true);
if (backupJob != null)
{
    await client.Crons.RunAsync(backupJob["id"]?.ToString() ?? "");
    Console.WriteLine("Backup job executed manually");
}
```

---

## Health Service

Check the health status of the API.

### Check Health

```csharp
var health = await client.Health.CheckAsync();
// Returns: Health status information
```

**Example:**
```csharp
try
{
    var health = await client.Health.CheckAsync();
    Console.WriteLine("API is healthy");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Health check failed: {ex.Message}");
}
```

---

## Collection Service

Manage collections (schemas) programmatically.

### List Collections

```csharp
var collections = await client.Collections.GetListAsync(1, 30);
// Returns: Paginated list of collections
```

### Get Collection

```csharp
var collection = await client.Collections.GetOneAsync("collection-id-or-name");
// Returns: Full collection schema
```

### Create Collection

```csharp
var collection = await client.Collections.CreateAsync(new Dictionary<string, object?>
{
    ["name"] = "posts",
    ["type"] = "base",
    ["schema"] = new[]
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
            ["type"] = "editor",
            ["required"] = false
        }
    }
});
```

### Update Collection

```csharp
await client.Collections.UpdateAsync("collection-id", new Dictionary<string, object?>
{
    ["schema"] = new[]
    {
        // Updated schema
    }
});
```

### Delete Collection

```csharp
await client.Collections.DeleteCollectionAsync("collection-id");
```

### Truncate Collection

Delete all records in a collection (keeps the schema):

```csharp
await client.Collections.TruncateAsync("collection-id");
```

### Import Collections

```csharp
var collections = new[]
{
    new Dictionary<string, object?>
    {
        ["name"] = "collection1",
        // ... collection schema
    },
    new Dictionary<string, object?>
    {
        ["name"] = "collection2",
        // ... collection schema
    }
};

await client.Collections.ImportCollectionsAsync(collections, deleteMissing: false);
```

---

## Complete Example: Automated Backup Management

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate as superuser
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Check current backup settings
var settings = await client.Settings.GetAllAsync();
var backupSettings = settings["backup"] as Dictionary<string, object?>;
Console.WriteLine($"Current backup schedule: {backupSettings?["cron"]}");

// List all existing backups
var backups = await client.Backups.GetFullListAsync();
Console.WriteLine($"Found {backups.Count} backups");

// Create a new backup
var backupName = $"manual-backup-{DateTime.UtcNow:yyyy-MM-dd}";
await client.Backups.CreateAsync(backupName);
Console.WriteLine("Backup created successfully");

// Get updated backup list
var updatedBackups = await client.Backups.GetFullListAsync();
Console.WriteLine($"Now have {updatedBackups.Count} backups");

// Configure auto-backup (daily at 2 AM, keep 7 backups)
await client.Settings.UpdateAsync(new Dictionary<string, object?>
{
    ["backup"] = new Dictionary<string, object?>
    {
        ["cron"] = "0 2 * * *",
        ["cronMaxKeep"] = 7
    }
});
Console.WriteLine("Auto-backup configured");

// Test backup S3 connection if configured
try
{
    await client.Settings.TestS3Async("backup");
    Console.WriteLine("S3 backup storage is working");
}
catch (Exception ex)
{
    Console.Warn($"S3 backup storage test failed: {ex.Message}");
}
```

---

## Error Handling

All management API methods can throw `ClientResponseError`. Always handle errors appropriately:

```csharp
try
{
    await client.Backups.CreateAsync("my-backup");
    Console.WriteLine("Backup created successfully");
}
catch (ClientResponseError ex)
{
    if (ex.Status == 401)
    {
        Console.Error.WriteLine("Authentication required");
    }
    else if (ex.Status == 403)
    {
        Console.Error.WriteLine("Superuser access required");
    }
    else
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
}
```

---

## Notes

1. **Authentication**: All management API operations require superuser authentication. Use `client.Collection("_superusers").AuthWithPasswordAsync()` to authenticate.

2. **Rate Limiting**: Be mindful of rate limits when making multiple management API calls.

3. **Backup Safety**: Always test backup restoration in a safe environment before using in production.

4. **Log Retention**: Setting appropriate log retention helps manage storage usage.

5. **Cron Jobs**: Manual cron execution is useful for testing but should be used carefully in production.

For more information on specific services, see:
- [Backups API](./BACKUPS_API.md) - Detailed backup operations
- [Logs API](./LOGS_API.md) - Detailed log operations
- [Collections API](./COLLECTION_API.md) - Collection management
- [Health API](./HEALTH_API.md) - Health check operations

