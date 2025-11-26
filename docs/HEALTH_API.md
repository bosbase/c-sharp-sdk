# Health API - C# SDK Documentation

## Overview

The Health API provides a simple endpoint to check the health status of the server. It returns basic health information and, when authenticated as a superuser, provides additional diagnostic information about the server state.

**Key Features:**
- No authentication required for basic health check
- Superuser authentication provides additional diagnostic data
- Lightweight endpoint for monitoring and health checks
- Supports both GET and HEAD methods

**Backend Endpoints:**
- `GET /api/health` - Check health status
- `HEAD /api/health` - Check health status (HEAD method)

**Note**: The health endpoint is publicly accessible, but superuser authentication provides additional information.

## Authentication

Basic health checks do not require authentication:

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Basic health check (no auth required)
var health = await client.Health.CheckAsync();
```

For additional diagnostic information, authenticate as a superuser:

```csharp
// Authenticate as superuser for extended health data
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
var health = await client.Health.CheckAsync();
```

## Health Check Response Structure

### Basic Response (Guest/Regular User)

```csharp
{
  "code": 200,
  "message": "API is healthy.",
  "data": {}
}
```

### Superuser Response

```csharp
{
  "code": 200,
  "message": "API is healthy.",
  "data": {
    "canBackup": bool,           // Whether backup operations are allowed
    "realIP": string,               // Real IP address of the client
    "requireS3": bool,           // Whether S3 storage is required
    "possibleProxyHeader": string   // Detected proxy header (if behind reverse proxy)
  }
}
```

## Check Health Status

Returns the health status of the API server.

### Basic Usage

```csharp
// Simple health check
var health = await client.Health.CheckAsync();

var message = health["message"]?.ToString(); // "API is healthy."
var code = health["code"]; // 200
```

### With Superuser Authentication

```csharp
// Authenticate as superuser first
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Get extended health information
var health = await client.Health.CheckAsync();

var data = health["data"] as Dictionary<string, object?>;
var canBackup = data?["canBackup"];           // true/false
var realIP = data?["realIP"]?.ToString();      // "192.168.1.100"
var requireS3 = data?["requireS3"];           // false
var possibleProxyHeader = data?["possibleProxyHeader"]?.ToString(); // "" or header name
```

## Response Fields

### Common Fields (All Users)

| Field | Type | Description |
|-------|------|-------------|
| `code` | int | HTTP status code (always 200 for healthy server) |
| `message` | string | Health status message ("API is healthy.") |
| `data` | Dictionary | Health data (empty for non-superusers, populated for superusers) |

### Superuser-Only Fields (in `data`)

| Field | Type | Description |
|-------|------|-------------|
| `canBackup` | bool | `true` if backup/restore operations can be performed, `false` if a backup/restore is currently in progress |
| `realIP` | string | The real IP address of the client (useful when behind proxies) |
| `requireS3` | bool | `true` if S3 storage is required (local fallback disabled), `false` otherwise |
| `possibleProxyHeader` | string | Detected proxy header name (e.g., "X-Forwarded-For", "CF-Connecting-IP") if the server appears to be behind a reverse proxy, empty string otherwise |

## Use Cases

### 1. Basic Health Monitoring

```csharp
async Task<bool> CheckServerHealth()
{
  try
  {
    var health = await client.Health.CheckAsync();
    
    var code = health["code"];
    var message = health["message"]?.ToString();
    
    if (code != null && code.ToString() == "200" && message == "API is healthy.")
    {
      Console.WriteLine("✓ Server is healthy");
      return true;
    }
    else
    {
      Console.WriteLine("✗ Server health check failed");
      return false;
    }
  }
  catch (Exception ex)
  {
    Console.Error.WriteLine($"✗ Health check error: {ex.Message}");
    return false;
  }
}

// Use in monitoring
var timer = new System.Timers.Timer(60000); // Check every minute
timer.Elapsed += async (sender, e) =>
{
  var isHealthy = await CheckServerHealth();
  if (!isHealthy)
  {
    Console.Warn("Server health check failed!");
  }
};
timer.Start();
```

### 2. Backup Readiness Check

```csharp
async Task<bool> CanPerformBackup()
{
  try
  {
    // Authenticate as superuser
    await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
    
    var health = await client.Health.CheckAsync();
    var data = health["data"] as Dictionary<string, object?>;
    var canBackup = data?["canBackup"];
    
    if (canBackup != null && canBackup.ToString() == "False")
    {
      Console.WriteLine("⚠️ Backup operation is currently in progress");
      return false;
    }
    
    Console.WriteLine("✓ Backup operations are allowed");
    return true;
  }
  catch (Exception ex)
  {
    Console.Error.WriteLine($"Failed to check backup readiness: {ex.Message}");
    return false;
  }
}

// Use before creating backups
if (await CanPerformBackup())
{
  await client.Backups.CreateAsync("backup.zip");
}
```

### 3. Monitoring Dashboard

```csharp
class HealthMonitor
{
  private readonly BosbaseClient _client;
  private bool _isSuperuser = false;

  public HealthMonitor(BosbaseClient client)
  {
    _client = client;
  }

  public async Task<bool> AuthenticateAsSuperuser(string email, string password)
  {
    try
    {
      await _client.Collection("_superusers").AuthWithPasswordAsync(email, password);
      _isSuperuser = true;
      return true;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Superuser authentication failed: {ex.Message}");
      return false;
    }
  }

  public async Task<Dictionary<string, object?>> GetHealthStatus()
  {
    try
    {
      var health = await _client.Health.CheckAsync();
      
      var status = new Dictionary<string, object?>
      {
        ["healthy"] = health["code"]?.ToString() == "200",
        ["message"] = health["message"],
        ["timestamp"] = DateTime.UtcNow.ToString("O"),
      };
      
      if (_isSuperuser && health["data"] is Dictionary<string, object?> data)
      {
        status["diagnostics"] = new Dictionary<string, object?>
        {
          ["canBackup"] = data.GetValueOrDefault("canBackup"),
          ["realIP"] = data.GetValueOrDefault("realIP"),
          ["requireS3"] = data.GetValueOrDefault("requireS3"),
          ["behindProxy"] = !string.IsNullOrEmpty(data.GetValueOrDefault("possibleProxyHeader")?.ToString()),
          ["proxyHeader"] = data.GetValueOrDefault("possibleProxyHeader"),
        };
      }
      
      return status;
    }
    catch (Exception ex)
    {
      return new Dictionary<string, object?>
      {
        ["healthy"] = false,
        ["error"] = ex.Message,
        ["timestamp"] = DateTime.UtcNow.ToString("O"),
      };
    }
  }

  public void StartMonitoring(int intervalMs = 60000)
  {
    var timer = new System.Timers.Timer(intervalMs);
    timer.Elapsed += async (sender, e) =>
    {
      var status = await GetHealthStatus();
      Console.WriteLine($"Health Status: {System.Text.Json.JsonSerializer.Serialize(status)}");
      
      if (status["healthy"]?.ToString() != "True")
      {
        OnHealthIssue(status);
      }
    };
    timer.Start();
  }

  private void OnHealthIssue(Dictionary<string, object?> status)
  {
    Console.Error.WriteLine($"Health issue detected: {System.Text.Json.JsonSerializer.Serialize(status)}");
    // Implement alerting logic here
  }
}

// Usage
var monitor = new HealthMonitor(client);
await monitor.AuthenticateAsSuperuser("admin@example.com", "password");
monitor.StartMonitoring(30000); // Check every 30 seconds
```

### 4. Load Balancer Health Check

```csharp
// Simple health check for load balancers
async Task<bool> SimpleHealthCheck()
{
  try
  {
    var health = await client.Health.CheckAsync();
    return health["code"]?.ToString() == "200";
  }
  catch
  {
    return false;
  }
}

// Use in ASP.NET Core route for load balancer
app.MapGet("/health", async () =>
{
  var isHealthy = await SimpleHealthCheck();
  return isHealthy 
    ? Results.Ok(new { status = "healthy" })
    : Results.StatusCode(503);
});
```

### 5. Proxy Detection

```csharp
async Task<Dictionary<string, object?>> CheckProxySetup()
{
  await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
  
  var health = await client.Health.CheckAsync();
  var data = health["data"] as Dictionary<string, object?>;
  var proxyHeader = data?["possibleProxyHeader"]?.ToString();
  
  if (!string.IsNullOrEmpty(proxyHeader))
  {
    Console.WriteLine("⚠️ Server appears to be behind a reverse proxy");
    Console.WriteLine($"   Detected proxy header: {proxyHeader}");
    Console.WriteLine($"   Real IP: {data?["realIP"]}");
    
    // Provide guidance on trusted proxy configuration
    Console.WriteLine("   Ensure TrustedProxy settings are configured correctly in admin panel");
  }
  else
  {
    Console.WriteLine("✓ No reverse proxy detected (or properly configured)");
  }
  
  return new Dictionary<string, object?>
  {
    ["behindProxy"] = !string.IsNullOrEmpty(proxyHeader),
    ["proxyHeader"] = proxyHeader,
    ["realIP"] = data?["realIP"],
  };
}
```

### 6. Pre-Flight Checks

```csharp
async Task<Dictionary<string, object?>> PreFlightCheck()
{
  var checks = new Dictionary<string, object?>
  {
    ["serverHealthy"] = false,
    ["canBackup"] = false,
    ["storageConfigured"] = false,
    ["issues"] = new List<string>()
  };
  
  try
  {
    // Basic health check
    var health = await client.Health.CheckAsync();
    checks["serverHealthy"] = health["code"]?.ToString() == "200";
    
    if (checks["serverHealthy"]?.ToString() != "True")
    {
      ((List<string>)checks["issues"]!).Add("Server health check failed");
      return checks;
    }
    
    // Authenticate as superuser for extended checks
    try
    {
      await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
      
      var detailedHealth = await client.Health.CheckAsync();
      var data = detailedHealth["data"] as Dictionary<string, object?>;
      
      checks["canBackup"] = data?["canBackup"]?.ToString() == "True";
      var requireS3 = data?["requireS3"]?.ToString() == "True";
      checks["storageConfigured"] = !requireS3;
      
      if (checks["canBackup"]?.ToString() != "True")
      {
        ((List<string>)checks["issues"]!).Add("Backup operations are currently unavailable");
      }
      
      if (requireS3)
      {
        ((List<string>)checks["issues"]!).Add("S3 storage is required but may not be configured");
      }
    }
    catch (Exception authEx)
    {
      ((List<string>)checks["issues"]!).Add("Superuser authentication failed - limited diagnostics available");
    }
  }
  catch (Exception ex)
  {
    ((List<string>)checks["issues"]!).Add($"Health check error: {ex.Message}");
  }
  
  return checks;
}

// Use before critical operations
var checks = await PreFlightCheck();
if (checks["issues"] is List<string> issues && issues.Count > 0)
{
  Console.Warn($"Pre-flight check issues: {string.Join(", ", issues)}");
  // Handle issues before proceeding
}
```

### 7. Automated Backup Scheduler

```csharp
class BackupScheduler
{
  private readonly BosbaseClient _client;

  public BackupScheduler(BosbaseClient client)
  {
    _client = client;
  }

  public async Task<bool> WaitForBackupAvailability(int maxWaitMs = 300000)
  {
    var startTime = DateTime.UtcNow;
    var checkInterval = 5000; // Check every 5 seconds
    
    while ((DateTime.UtcNow - startTime).TotalMilliseconds < maxWaitMs)
    {
      try
      {
        var health = await _client.Health.CheckAsync();
        var data = health["data"] as Dictionary<string, object?>;
        
        if (data?["canBackup"]?.ToString() == "True")
        {
          return true;
        }
        
        Console.WriteLine("Backup in progress, waiting...");
        await Task.Delay(checkInterval);
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Health check failed: {ex.Message}");
        return false;
      }
    }
    
    Console.Error.WriteLine("Timeout waiting for backup availability");
    return false;
  }

  public async Task ScheduleBackup(string backupName)
  {
    // Wait for backup operations to be available
    var isAvailable = await WaitForBackupAvailability();
    
    if (!isAvailable)
    {
      throw new Exception("Backup operations are not available");
    }
    
    // Create the backup
    await _client.Backups.CreateAsync(backupName);
    Console.WriteLine($"Backup \"{backupName}\" created");
  }
}

// Usage
var scheduler = new BackupScheduler(client);
await scheduler.ScheduleBackup("scheduled_backup.zip");
```

## Error Handling

```csharp
async Task<Dictionary<string, object?>> SafeHealthCheck()
{
  try
  {
    var health = await client.Health.CheckAsync();
    return new Dictionary<string, object?>
    {
      ["success"] = true,
      ["data"] = health,
    };
  }
  catch (Exception ex)
  {
    // Network errors, server down, etc.
    return new Dictionary<string, object?>
    {
      ["success"] = false,
      ["error"] = ex.Message,
      ["code"] = 0,
    };
  }
}

// Handle different error scenarios
var result = await SafeHealthCheck();
if (result["success"]?.ToString() != "True")
{
  var code = result["code"]?.ToString();
  if (code == "0")
  {
    Console.Error.WriteLine("Network error or server unreachable");
  }
  else
  {
    Console.Error.WriteLine($"Server returned error: {code}");
  }
}
```

## Best Practices

1. **Monitoring**: Use health checks for regular monitoring (e.g., every 30-60 seconds)
2. **Load Balancers**: Configure load balancers to use the health endpoint for health checks
3. **Pre-flight Checks**: Check `canBackup` before initiating backup operations
4. **Error Handling**: Always handle errors gracefully as the server may be down
5. **Rate Limiting**: Don't poll the health endpoint too frequently (avoid spamming)
6. **Caching**: Consider caching health check results for a few seconds to reduce load
7. **Logging**: Log health check results for troubleshooting and monitoring
8. **Alerting**: Set up alerts for consecutive health check failures
9. **Superuser Auth**: Only authenticate as superuser when you need diagnostic information
10. **Proxy Configuration**: Use `possibleProxyHeader` to detect and configure reverse proxy settings

## Response Codes

| Code | Meaning |
|------|---------|
| 200 | Server is healthy |
| Network Error | Server is unreachable or down |

## Limitations

- **No Detailed Metrics**: The health endpoint does not provide detailed performance metrics
- **Basic Status Only**: Returns basic status, not detailed system information
- **Superuser Required**: Extended diagnostics require superuser authentication
- **No Historical Data**: Only returns current status, no historical health data

## Head Method Support

The health endpoint also supports the HEAD method for lightweight checks:

```csharp
// Using HEAD method (if supported by your HTTP client)
using var httpClient = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Head, "http://127.0.0.1:8090/api/health");
var response = await httpClient.SendAsync(request);

if (response.IsSuccessStatusCode)
{
  Console.WriteLine("Server is healthy");
}
```

## Related Documentation

- [Backups API](./BACKUPS_API.md) - Using `canBackup` to check backup readiness
- [Authentication](./AUTHENTICATION.md) - Superuser authentication
- [Settings API](./SETTINGS_API.md) - Configuring trusted proxy settings

