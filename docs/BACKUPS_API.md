# Backups API - C# SDK Documentation

## Overview

The Backups API provides endpoints for managing application data backups. You can create backups, upload existing backup files, download backups, delete backups, and restore the application from a backup.

**Key Features:**
- List all available backup files
- Create new backups with custom names or auto-generated names
- Upload existing backup ZIP files
- Download backup files (requires file token)
- Delete backup files
- Restore the application from a backup (restarts the app)

**Backend Endpoints:**
- `GET /api/backups` - List backups
- `POST /api/backups` - Create backup
- `POST /api/backups/upload` - Upload backup
- `GET /api/backups/{key}` - Download backup
- `DELETE /api/backups/{key}` - Delete backup
- `POST /api/backups/{key}/restore` - Restore backup

**Note**: All Backups API operations require superuser authentication (except download which requires a superuser file token).

## Authentication

All Backups API operations require superuser authentication:

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate as superuser
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
```

**Downloading backups** requires a superuser file token (obtained via `client.Files.GetTokenAsync()`), but does not require the Authorization header.

## Backup File Structure

Each backup file contains:
- `key`: The filename/key of the backup file (string)
- `size`: File size in bytes (long)
- `modified`: ISO 8601 timestamp of when the backup was last modified (string)

```csharp
// Backup file structure
var backup = new Dictionary<string, object?>
{
    ["key"] = "pb_backup_20230519162514.zip",
    ["size"] = 251316185L,
    ["modified"] = "2023-05-19T16:25:57.542Z"
};
```

## List Backups

Returns a list of all available backup files with their metadata.

### Basic Usage

```csharp
// Get all backups
var backups = await client.Backups.GetFullListAsync();

foreach (var backup in backups)
{
    Console.WriteLine($"Key: {backup["key"]}");
    Console.WriteLine($"Size: {backup["size"]} bytes");
    Console.WriteLine($"Modified: {backup["modified"]}");
}
```

### Working with Backup Lists

```csharp
// Sort backups by modification date (newest first)
var backups = await client.Backups.GetFullListAsync();
var sortedBackups = backups
    .OrderByDescending(b => DateTime.Parse(b["modified"]?.ToString() ?? ""))
    .ToList();

// Find the most recent backup
var mostRecent = sortedBackups.FirstOrDefault();

// Filter backups by size (larger than 100MB)
var largeBackups = backups
    .Where(b => Convert.ToInt64(b["size"]) > 100 * 1024 * 1024)
    .ToList();

// Get total storage used by backups
var totalSize = backups.Sum(b => Convert.ToInt64(b["size"]));
Console.WriteLine($"Total backup storage: {totalSize / 1024.0 / 1024.0:F2} MB");
```

## Create Backup

Creates a new backup of the application data. The backup process is asynchronous and may take some time depending on the size of your data.

### Basic Usage

```csharp
// Create backup with custom name
await client.Backups.CreateAsync("my_backup_2024.zip");

// Create backup with auto-generated name (pass empty string or let backend generate)
await client.Backups.CreateAsync("");
```

### Backup Name Format

Backup names must follow the format: `[a-z0-9_-].zip`
- Only lowercase letters, numbers, underscores, and hyphens
- Must end with `.zip`
- Maximum length: 150 characters
- Must be unique (no existing backup with the same name)

### Examples

```csharp
// Create a named backup
async Task CreateNamedBackup(string name)
{
  try
  {
    await client.Backups.CreateAsync(name);
    Console.WriteLine($"Backup \"{name}\" creation initiated");
  }
  catch (Exception ex)
  {
    if (ex is ClientResponseError error && error.Status == 400)
    {
      Console.Error.WriteLine("Invalid backup name or backup already exists");
    }
    else
    {
      Console.Error.WriteLine($"Failed to create backup: {ex.Message}");
    }
  }
}

// Create backup with timestamp
string CreateTimestampedBackup()
{
  var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
  var name = $"backup_{timestamp}.zip";
  client.Backups.CreateAsync(name).Wait();
  return name;
}
```

### Important Notes

- **Asynchronous Process**: Backup creation happens in the background. The API returns immediately (204 No Content).
- **Concurrent Operations**: Only one backup or restore operation can run at a time. If another operation is in progress, you'll receive a 400 error.
- **Storage**: Backups are stored in the configured backup filesystem (local or S3).
- **S3 Consistency**: For S3 storage, the backup file may not be immediately available after creation due to eventual consistency.

## Upload Backup

Uploads an existing backup ZIP file to the server. This is useful for restoring backups created elsewhere or for importing backups.

### Basic Usage

```csharp
using Bosbase.Models;

// Upload from a file path
var filePath = "/path/to/backup.zip";
var fileAttachment = FileAttachment.FromPath("file", filePath, "application/zip");
await client.Backups.UploadAsync(new[] { fileAttachment });

// Upload from bytes
var backupBytes = File.ReadAllBytes("/path/to/backup.zip");
var fileAttachment = FileAttachment.FromBytes("file", backupBytes, "backup.zip", "application/zip");
await client.Backups.UploadAsync(new[] { fileAttachment });

// Upload from a stream
using var fileStream = File.OpenRead("/path/to/backup.zip");
var fileAttachment = new FileAttachment("file", fileStream, "backup.zip", "application/zip");
await client.Backups.UploadAsync(new[] { fileAttachment });
```

### File Requirements

- **MIME Type**: Must be `application/zip`
- **Format**: Must be a valid ZIP archive
- **Name**: Must be unique (no existing backup with the same name)
- **Validation**: The file will be validated before upload

### Examples

```csharp
// Upload backup from file path
async Task UploadBackupFromPath(string filePath)
{
  if (!File.Exists(filePath))
  {
    Console.Error.WriteLine("File not found");
    return;
  }
  
  try
  {
    var fileAttachment = FileAttachment.FromPath("file", filePath, "application/zip");
    await client.Backups.UploadAsync(new[] { fileAttachment });
    Console.WriteLine("Backup uploaded successfully");
  }
  catch (Exception ex)
  {
    if (ex is ClientResponseError error && error.Status == 400)
    {
      Console.Error.WriteLine("Invalid file or file already exists");
    }
    else
    {
      Console.Error.WriteLine($"Upload failed: {ex.Message}");
    }
  }
}

// Upload backup from URL (e.g., downloading from another server)
async Task UploadBackupFromURL(string url)
{
  using var httpClient = new HttpClient();
  var response = await httpClient.GetAsync(url);
  var bytes = await response.Content.ReadAsByteArrayAsync();
  
  // Create a file attachment with the original filename
  var filename = Path.GetFileName(url) ?? "backup.zip";
  var fileAttachment = FileAttachment.FromBytes("file", bytes, filename, "application/zip");
  
  await client.Backups.UploadAsync(new[] { fileAttachment });
}
```

## Download Backup

Downloads a backup file. Requires a superuser file token for authentication.

### Basic Usage

```csharp
// Get file token
var token = await client.Files.GetTokenAsync();

// Build download URL
var url = client.Backups.GetDownloadUrl(token, "pb_backup_20230519162514.zip");

// Download the file using HttpClient
using var httpClient = new HttpClient();
var response = await httpClient.GetAsync(url);
var bytes = await response.Content.ReadAsByteArrayAsync();
await File.WriteAllBytesAsync("pb_backup_20230519162514.zip", bytes);
```

### Download URL Structure

The download URL format is:
```
/api/backups/{key}?token={fileToken}
```

### Examples

```csharp
// Download backup function
async Task DownloadBackup(string backupKey)
{
  try
  {
    // Get file token (valid for short period)
    var token = await client.Files.GetTokenAsync();
    
    // Build download URL
    var url = client.Backups.GetDownloadUrl(token, backupKey);
    
    // Download the file
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(url);
    var bytes = await response.Content.ReadAsByteArrayAsync();
    
    // Save to file
    await File.WriteAllBytesAsync(backupKey, bytes);
    Console.WriteLine($"Backup downloaded: {backupKey}");
  }
  catch (Exception ex)
  {
    Console.Error.WriteLine($"Failed to download backup: {ex.Message}");
  }
}

// Download and save backup with custom name
async Task DownloadBackupAs(string backupKey, string saveAs)
{
  var token = await client.Files.GetTokenAsync();
  var url = client.Backups.GetDownloadUrl(token, backupKey);
  
  using var httpClient = new HttpClient();
  var response = await httpClient.GetAsync(url);
  var bytes = await response.Content.ReadAsByteArrayAsync();
  
  await File.WriteAllBytesAsync(saveAs, bytes);
}
```

## Delete Backup

Deletes a backup file from the server.

### Basic Usage

```csharp
await client.Backups.DeleteAsync("pb_backup_20230519162514.zip");
```

### Important Notes

- **Active Backups**: Cannot delete a backup that is currently being created or restored
- **No Undo**: Deletion is permanent
- **File System**: The file will be removed from the backup filesystem

### Examples

```csharp
// Delete backup with confirmation
async Task DeleteBackupWithConfirmation(string backupKey)
{
  Console.WriteLine($"Are you sure you want to delete {backupKey}? (y/n)");
  var confirmation = Console.ReadLine();
  
  if (confirmation?.ToLower() == "y")
  {
    try
    {
      await client.Backups.DeleteAsync(backupKey);
      Console.WriteLine("Backup deleted successfully");
    }
    catch (Exception ex)
    {
      if (ex is ClientResponseError error)
      {
        if (error.Status == 400)
        {
          Console.Error.WriteLine("Backup is currently in use and cannot be deleted");
        }
        else if (error.Status == 404)
        {
          Console.Error.WriteLine("Backup not found");
        }
        else
        {
          Console.Error.WriteLine($"Failed to delete backup: {ex.Message}");
        }
      }
    }
  }
}

// Delete old backups (older than 30 days)
async Task DeleteOldBackups()
{
  var backups = await client.Backups.GetFullListAsync();
  var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
  
  var oldBackups = backups.Where(backup =>
  {
    var modified = DateTime.Parse(backup["modified"]?.ToString() ?? "");
    return modified < thirtyDaysAgo;
  }).ToList();
  
  foreach (var backup in oldBackups)
  {
    try
    {
      await client.Backups.DeleteAsync(backup["key"]?.ToString() ?? "");
      Console.WriteLine($"Deleted old backup: {backup["key"]}");
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Failed to delete {backup["key"]}: {ex.Message}");
    }
  }
}
```

## Restore Backup

Restores the application from a backup file. **This operation will restart the application**.

### Basic Usage

```csharp
await client.Backups.RestoreAsync("pb_backup_20230519162514.zip");
```

### Important Warnings

⚠️ **CRITICAL**: Restoring a backup will:
1. Replace all current application data with data from the backup
2. **Restart the application process**
3. Any unsaved changes will be lost
4. The application will be unavailable during the restore process

### Prerequisites

- **Disk Space**: Recommended to have at least **2x the backup size** in free disk space
- **UNIX Systems**: Restore is primarily supported on UNIX-based systems (Linux, macOS)
- **No Concurrent Operations**: Cannot restore if another backup or restore is in progress
- **Backup Existence**: The backup file must exist on the server

### Restore Process

The restore process performs the following steps:
1. Downloads the backup file to a temporary location
2. Extracts the backup to a temporary directory
3. Moves current `pb_data` content to a temporary location (to be deleted on next app start)
4. Moves extracted backup content to `pb_data`
5. Restarts the application

### Examples

```csharp
// Restore backup with confirmation
async Task RestoreBackupWithConfirmation(string backupKey)
{
  Console.WriteLine(
    $"⚠️ WARNING: This will replace all current data with data from {backupKey} and restart the application.\n\n" +
    "Are you absolutely sure you want to continue? (y/n)"
  );
  
  var confirmation = Console.ReadLine();
  if (confirmation?.ToLower() != "y") return;
  
  try
  {
    await client.Backups.RestoreAsync(backupKey);
    Console.WriteLine("Restore initiated. Application will restart...");
    
    // Optionally wait and reload
    await Task.Delay(2000);
  }
  catch (Exception ex)
  {
    if (ex is ClientResponseError error && error.Status == 400)
    {
      if (error.Message?.Contains("another backup/restore") == true)
      {
        Console.Error.WriteLine("Another backup or restore operation is in progress");
      }
      else
      {
        Console.Error.WriteLine("Invalid or missing backup file");
      }
    }
    else
    {
      Console.Error.WriteLine($"Failed to restore backup: {ex.Message}");
    }
  }
}
```

## Complete Examples

### Example 1: Backup Manager Class

```csharp
class BackupManager
{
  private readonly BosbaseClient _client;

  public BackupManager(BosbaseClient client)
  {
    _client = client;
  }

  public async Task<List<Dictionary<string, object?>>> ListAsync()
  {
    var backups = await _client.Backups.GetFullListAsync();
    return backups
      .OrderByDescending(b => DateTime.Parse(b["modified"]?.ToString() ?? ""))
      .ToList();
  }

  public async Task<string> CreateAsync(string? name = null)
  {
    if (string.IsNullOrEmpty(name))
    {
      var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
      name = $"backup_{timestamp}.zip";
    }
    await _client.Backups.CreateAsync(name);
    return name;
  }

  public async Task<string> GetDownloadUrlAsync(string key)
  {
    var token = await _client.Files.GetTokenAsync();
    return _client.Backups.GetDownloadUrl(token, key);
  }

  public async Task DeleteAsync(string key)
  {
    await _client.Backups.DeleteAsync(key);
  }

  public async Task<bool> RestoreAsync(string key, string? confirmMessage = null)
  {
    if (!string.IsNullOrEmpty(confirmMessage))
    {
      Console.WriteLine(confirmMessage);
      var confirmation = Console.ReadLine();
      if (confirmation?.ToLower() != "y")
      {
        return false;
      }
    }
    await _client.Backups.RestoreAsync(key);
    return true;
  }

  public async Task<int> CleanupAsync(int daysOld = 30)
  {
    var backups = await ListAsync();
    var cutoff = DateTime.UtcNow.AddDays(-daysOld);
    
    var toDelete = backups
      .Where(b => DateTime.Parse(b["modified"]?.ToString() ?? "") < cutoff)
      .ToList();
    
    foreach (var backup in toDelete)
    {
      try
      {
        await DeleteAsync(backup["key"]?.ToString() ?? "");
        Console.WriteLine($"Deleted: {backup["key"]}");
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Failed to delete {backup["key"]}: {ex.Message}");
      }
    }
    
    return toDelete.Count;
  }
}

// Usage
var manager = new BackupManager(client);
var backups = await manager.ListAsync();
await manager.CreateAsync("weekly_backup.zip");
```

### Example 2: Automated Backup Strategy

```csharp
class AutomatedBackup
{
  private readonly BosbaseClient _client;
  private readonly string _strategy; // 'daily', 'weekly', 'monthly'
  private readonly int _maxBackups;

  public AutomatedBackup(BosbaseClient client, string strategy = "daily", int maxBackups = 7)
  {
    _client = client;
    _strategy = strategy;
    _maxBackups = maxBackups;
  }

  public async Task CreateScheduledBackup()
  {
    try
    {
      var name = GenerateBackupName();
      await _client.Backups.CreateAsync(name);
      Console.WriteLine($"Created backup: {name}");
      
      await CleanupOldBackups();
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Backup creation failed: {ex.Message}");
    }
  }

  private string GenerateBackupName()
  {
    var now = DateTime.UtcNow;
    var dateStr = now.ToString("yyyy-MM-dd");
    
    return _strategy switch
    {
      "daily" => $"daily_{dateStr}.zip",
      "weekly" => $"weekly_{now.Year}_W{GetWeekOfYear(now)}.zip",
      "monthly" => $"monthly_{now.Year}_{now.Month:D2}.zip",
      _ => $"backup_{dateStr}.zip"
    };
  }

  private int GetWeekOfYear(DateTime date)
  {
    var culture = System.Globalization.CultureInfo.CurrentCulture;
    var calendar = culture.Calendar;
    return calendar.GetWeekOfYear(date, culture.DateTimeFormat.CalendarWeekRule, culture.DateTimeFormat.FirstDayOfWeek);
  }

  private async Task CleanupOldBackups()
  {
    var backups = await _client.Backups.GetFullListAsync();
    var sorted = backups
      .OrderByDescending(b => DateTime.Parse(b["modified"]?.ToString() ?? ""))
      .ToList();
    
    if (sorted.Count > _maxBackups)
    {
      var toDelete = sorted.Skip(_maxBackups).ToList();
      foreach (var backup in toDelete)
      {
        try
        {
          await _client.Backups.DeleteAsync(backup["key"]?.ToString() ?? "");
          Console.WriteLine($"Cleaned up old backup: {backup["key"]}");
        }
        catch (Exception ex)
        {
          Console.Error.WriteLine($"Failed to delete {backup["key"]}: {ex.Message}");
        }
      }
    }
  }
}

// Setup daily automated backups
var autoBackup = new AutomatedBackup(client, "daily");

// Run backup (could be called from a cron job or scheduler)
var timer = new System.Timers.Timer(24 * 60 * 60 * 1000); // Every 24 hours
timer.Elapsed += async (sender, e) => await autoBackup.CreateScheduledBackup();
timer.Start();
```

### Example 3: Backup Migration Tool

```csharp
class BackupMigrator
{
  private readonly BosbaseClient _sourceClient;
  private readonly BosbaseClient _targetClient;

  public BackupMigrator(BosbaseClient sourceClient, BosbaseClient targetClient)
  {
    _sourceClient = sourceClient;
    _targetClient = targetClient;
  }

  public async Task MigrateBackup(string backupKey)
  {
    Console.WriteLine($"Migrating backup: {backupKey}");
    
    // Step 1: Download from source
    Console.WriteLine("Downloading from source...");
    var sourceToken = await _sourceClient.Files.GetTokenAsync();
    var downloadUrl = _sourceClient.Backups.GetDownloadUrl(sourceToken, backupKey);
    
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(downloadUrl);
    var bytes = await response.Content.ReadAsByteArrayAsync();
    
    // Step 2: Create file attachment
    using var fileAttachment = FileAttachment.FromBytes("file", bytes, backupKey, "application/zip");
    
    // Step 3: Upload to target
    Console.WriteLine("Uploading to target...");
    await _targetClient.Backups.UploadAsync(new[] { fileAttachment });
    
    Console.WriteLine("Migration completed");
  }

  public async Task MigrateAllBackups()
  {
    var backups = await _sourceClient.Backups.GetFullListAsync();
    
    foreach (var backup in backups)
    {
      try
      {
        var key = backup["key"]?.ToString() ?? "";
        await MigrateBackup(key);
        Console.WriteLine($"✓ Migrated: {key}");
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"✗ Failed to migrate {backup["key"]}: {ex.Message}");
      }
    }
  }
}

// Usage
var migrator = new BackupMigrator(sourceClient, targetClient);
await migrator.MigrateAllBackups();
```

### Example 4: Backup Health Check

```csharp
async Task<bool> CheckBackupHealth()
{
  var backups = await client.Backups.GetFullListAsync();
  
  if (backups.Count == 0)
  {
    Console.Warn("⚠️ No backups found!");
    return false;
  }
  
  // Check for recent backup (within last 7 days)
  var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
  
  var recentBackups = backups
    .Where(b => DateTime.Parse(b["modified"]?.ToString() ?? "") > sevenDaysAgo)
    .ToList();
  
  if (recentBackups.Count == 0)
  {
    Console.Warn("⚠️ No backups found in the last 7 days");
  }
  else
  {
    Console.WriteLine($"✓ Found {recentBackups.Count} recent backup(s)");
  }
  
  // Check total storage
  var totalSize = backups.Sum(b => Convert.ToInt64(b["size"]));
  var totalSizeMB = totalSize / 1024.0 / 1024.0;
  Console.WriteLine($"Total backup storage: {totalSizeMB:F2} MB");
  
  // Check largest backup
  var largest = backups
    .OrderByDescending(b => Convert.ToInt64(b["size"]))
    .FirstOrDefault();
  
  if (largest != null)
  {
    var largestSizeMB = Convert.ToInt64(largest["size"]) / 1024.0 / 1024.0;
    Console.WriteLine($"Largest backup: {largest["key"]} ({largestSizeMB:F2} MB)");
  }
  
  return true;
}
```

## Error Handling

```csharp
// Handle common backup errors
async Task HandleBackupError(string operation, params object[] args)
{
  try
  {
    switch (operation)
    {
      case "create":
        await client.Backups.CreateAsync((string)args[0]);
        break;
      case "delete":
        await client.Backups.DeleteAsync((string)args[0]);
        break;
      case "restore":
        await client.Backups.RestoreAsync((string)args[0]);
        break;
    }
  }
  catch (ClientResponseError error)
  {
    switch (error.Status)
    {
      case 400:
        if (error.Message?.Contains("another backup/restore") == true)
        {
          Console.Error.WriteLine("Another backup or restore operation is in progress");
        }
        else if (error.Message?.Contains("already exists") == true)
        {
          Console.Error.WriteLine("Backup with this name already exists");
        }
        else
        {
          Console.Error.WriteLine($"Invalid request: {error.Message}");
        }
        break;
      
      case 401:
        Console.Error.WriteLine("Not authenticated");
        break;
      
      case 403:
        Console.Error.WriteLine("Not a superuser");
        break;
      
      case 404:
        Console.Error.WriteLine("Backup not found");
        break;
      
      default:
        Console.Error.WriteLine($"Unexpected error: {error.Message}");
        break;
    }
    throw;
  }
}
```

## Best Practices

1. **Regular Backups**: Create backups regularly (daily, weekly, or based on your needs)
2. **Naming Convention**: Use clear, consistent naming (e.g., `backup_YYYY-MM-DD.zip`)
3. **Backup Rotation**: Implement cleanup to remove old backups and prevent storage issues
4. **Test Restores**: Periodically test restoring backups to ensure they work
5. **Off-site Storage**: Download and store backups in a separate location
6. **Pre-Restore Backup**: Always create a backup before restoring (if possible)
7. **Monitor Storage**: Monitor backup storage usage to prevent disk space issues
8. **Documentation**: Document your backup and restore procedures
9. **Automation**: Use cron jobs or schedulers for automated backups
10. **Verification**: Verify backup integrity after creation/download

## Limitations

- **Superuser Only**: All operations require superuser authentication
- **Concurrent Operations**: Only one backup or restore can run at a time
- **Restore Restart**: Restoring a backup restarts the application
- **UNIX Systems**: Restore primarily works on UNIX-based systems
- **Disk Space**: Restore requires significant free disk space (2x backup size recommended)
- **S3 Consistency**: S3 backups may not be immediately available after creation
- **Active Backups**: Cannot delete backups that are currently being created or restored

## Related Documentation

- [File API](./FILE_API.md) - File handling and tokens
- [Crons API](./CRONS_API.md) - Automated backup scheduling
- [Collection API](./COLLECTION_API.md) - Collection management

