# Logs API - C# SDK Documentation

## Overview

The Logs API provides endpoints for viewing and analyzing application logs. All operations require superuser authentication and allow you to query request logs, filter by various criteria, and get aggregated statistics.

**Key Features:**
- List and paginate logs
- View individual log entries
- Filter logs by status, URL, method, IP, etc.
- Sort logs by various fields
- Get hourly aggregated statistics
- Filter statistics by criteria

**Backend Endpoints:**
- `GET /api/logs` - List logs
- `GET /api/logs/{id}` - View log
- `GET /api/logs/stats` - Get statistics

**Note**: All Logs API operations require superuser authentication.

## Authentication

All Logs API operations require superuser authentication:

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate as superuser
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
```

## List Logs

Returns a paginated list of logs with support for filtering and sorting.

### Basic Usage

```csharp
// Basic list
var result = await client.Logs.GetListAsync(1, 30);

var page = result["page"];        // 1
var perPage = result["perPage"];     // 30
var totalItems = result["totalItems"];  // Total logs count
var items = result["items"] as List<object?>;       // Array of log entries
```

### Log Entry Structure

Each log entry contains:

```csharp
{
  "id": "ai5z3aoed6809au",
  "created": "2024-10-27 09:28:19.524Z",
  "level": 0,
  "message": "GET /api/collections/posts/records",
  "data": {
    "auth": "_superusers",
    "execTime": 2.392327,
    "method": "GET",
    "referer": "http://localhost:8090/_/",
    "remoteIP": "127.0.0.1",
    "status": 200,
    "type": "request",
    "url": "/api/collections/posts/records?page=1",
    "userAgent": "Mozilla/5.0...",
    "userIP": "127.0.0.1"
  }
}
```

### Filtering Logs

```csharp
// Filter by HTTP status code
var errorLogs = await client.Logs.GetListAsync(1, 50, filter: "data.status >= 400");

// Filter by method
var getLogs = await client.Logs.GetListAsync(1, 50, filter: "data.method = \"GET\"");

// Filter by URL pattern
var apiLogs = await client.Logs.GetListAsync(1, 50, filter: "data.url ~ \"/api/\"");

// Filter by IP address
var ipLogs = await client.Logs.GetListAsync(1, 50, filter: "data.remoteIP = \"127.0.0.1\"");

// Filter by execution time (slow requests)
var slowLogs = await client.Logs.GetListAsync(1, 50, filter: "data.execTime > 1.0");

// Filter by log level
var errorLevelLogs = await client.Logs.GetListAsync(1, 50, filter: "level > 0");

// Filter by date range
var recentLogs = await client.Logs.GetListAsync(1, 50, filter: "created >= \"2024-10-27 00:00:00\"");
```

### Complex Filters

```csharp
// Multiple conditions
var complexFilter = await client.Logs.GetListAsync(1, 50, 
  filter: "data.status >= 400 && data.method = \"POST\" && data.execTime > 0.5");

// Exclude superuser requests
var userLogs = await client.Logs.GetListAsync(1, 50, 
  filter: "data.auth != \"_superusers\"");

// Specific endpoint errors
var endpointErrors = await client.Logs.GetListAsync(1, 50, 
  filter: "data.url ~ \"/api/collections/posts/records\" && data.status >= 400");

// Errors or slow requests
var problems = await client.Logs.GetListAsync(1, 50, 
  filter: "data.status >= 400 || data.execTime > 2.0");
```

### Sorting Logs

```csharp
// Sort by creation date (newest first)
var recent = await client.Logs.GetListAsync(1, 50, sort: "-created");

// Sort by execution time (slowest first)
var slowest = await client.Logs.GetListAsync(1, 50, sort: "-data.execTime");

// Sort by status code
var byStatus = await client.Logs.GetListAsync(1, 50, sort: "data.status");

// Sort by rowid (most efficient)
var byRowId = await client.Logs.GetListAsync(1, 50, sort: "-rowid");

// Multiple sort fields
var multiSort = await client.Logs.GetListAsync(1, 50, sort: "-created,level");
```

### Get Full List

```csharp
// Get all logs (be careful with large datasets)
var allLogs = await client.Logs.GetListAsync(1, 1000, 
  filter: "created >= \"2024-10-27 00:00:00\"",
  sort: "-created");
```

## View Log

Retrieve a single log entry by ID:

```csharp
// Get specific log
var log = await client.Logs.GetOneAsync("ai5z3aoed6809au");

Console.WriteLine(log["message"]);
var data = log["data"] as Dictionary<string, object?>;
Console.WriteLine(data?["status"]);
Console.WriteLine(data?["execTime"]);
```

### Log Details

```csharp
async Task AnalyzeLog(string logId)
{
  var log = await client.Logs.GetOneAsync(logId);
  
  Console.WriteLine($"Log ID: {log["id"]}");
  Console.WriteLine($"Created: {log["created"]}");
  Console.WriteLine($"Level: {log["level"]}");
  Console.WriteLine($"Message: {log["message"]}");
  
  if (log["data"] is Dictionary<string, object?> data && data["type"]?.ToString() == "request")
  {
    Console.WriteLine($"Method: {data["method"]}");
    Console.WriteLine($"URL: {data["url"]}");
    Console.WriteLine($"Status: {data["status"]}");
    Console.WriteLine($"Execution Time: {data["execTime"]} ms");
    Console.WriteLine($"Remote IP: {data["remoteIP"]}");
    Console.WriteLine($"User Agent: {data["userAgent"]}");
    Console.WriteLine($"Auth Collection: {data["auth"]}");
  }
}
```

## Logs Statistics

Get hourly aggregated statistics for logs:

### Basic Usage

```csharp
// Get all statistics
var stats = await client.Logs.GetStatsAsync();

// Each stat entry contains:
// { "total": 4, "date": "2022-06-01 19:00:00.000" }
```

### Filtered Statistics

```csharp
// Statistics for errors only
var errorStats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
{
  ["filter"] = "data.status >= 400"
});

// Statistics for specific endpoint
var endpointStats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
{
  ["filter"] = "data.url ~ \"/api/collections/posts/records\""
});

// Statistics for slow requests
var slowStats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
{
  ["filter"] = "data.execTime > 1.0"
});

// Statistics excluding superuser requests
var userStats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
{
  ["filter"] = "data.auth != \"_superusers\""
});
```

### Visualizing Statistics

```csharp
async Task DisplayLogChart()
{
  var stats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
  {
    ["filter"] = "created >= \"2024-10-27 00:00:00\""
  });
  
  // Use with charting library (e.g., Chart.js, OxyPlot)
  var chartData = stats?.Select(stat => new
  {
    x = DateTime.Parse(stat["date"]?.ToString() ?? ""),
    y = Convert.ToInt32(stat["total"])
  }).ToList();
  
  // Render chart...
}
```

## Filter Syntax

Logs support filtering with a flexible syntax similar to records filtering.

### Supported Fields

**Direct Fields:**
- `id` - Log ID
- `created` - Creation timestamp
- `updated` - Update timestamp
- `level` - Log level (0 = info, higher = warnings/errors)
- `message` - Log message

**Data Fields (nested):**
- `data.status` - HTTP status code
- `data.method` - HTTP method (GET, POST, etc.)
- `data.url` - Request URL
- `data.execTime` - Execution time in seconds
- `data.remoteIP` - Remote IP address
- `data.userIP` - User IP address
- `data.userAgent` - User agent string
- `data.referer` - Referer header
- `data.auth` - Auth collection ID
- `data.type` - Log type (usually "request")

### Filter Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `=` | Equal | `data.status = 200` |
| `!=` | Not equal | `data.status != 200` |
| `>` | Greater than | `data.status > 400` |
| `>=` | Greater than or equal | `data.status >= 400` |
| `<` | Less than | `data.execTime < 0.5` |
| `<=` | Less than or equal | `data.execTime <= 1.0` |
| `~` | Contains/Like | `data.url ~ "/api/"` |
| `!~` | Not contains | `data.url !~ "/admin/"` |
| `?=` | Any equal | `data.method ?= "GET,POST"` |
| `?!=` | Any not equal | `data.method ?!= "DELETE"` |
| `?>` | Any greater | `data.status ?> "400,500"` |
| `?>=` | Any greater or equal | `data.status ?>= "400,500"` |
| `?<` | Any less | `data.execTime ?< "0.5,1.0"` |
| `?<=` | Any less or equal | `data.execTime ?<= "1.0,2.0"` |
| `?~` | Any contains | `data.url ?~ "/api/,/admin/"` |
| `?!~` | Any not contains | `data.url ?!~ "/test/,/debug/"` |

### Logical Operators

- `&&` - AND
- `||` - OR
- `()` - Grouping

### Filter Examples

```csharp
// Simple equality
filter: "data.method = \"GET\""

// Range filter
filter: "data.status >= 400 && data.status < 500"

// Pattern matching
filter: "data.url ~ \"/api/collections/\""

// Complex logic
filter: "(data.status >= 400 || data.execTime > 2.0) && data.method = \"POST\""

// Exclude patterns
filter: "data.url !~ \"/admin/\" && data.auth != \"_superusers\""

// Date range
filter: "created >= \"2024-10-27 00:00:00\" && created <= \"2024-10-28 00:00:00\""
```

## Sort Options

Supported sort fields:

- `@random` - Random order
- `rowid` - Row ID (most efficient, use negative for DESC)
- `id` - Log ID
- `created` - Creation date
- `updated` - Update date
- `level` - Log level
- `message` - Message text
- `data.*` - Any data field (e.g., `data.status`, `data.execTime`)

```csharp
// Sort examples
sort: "-created"              // Newest first
sort: "data.execTime"         // Fastest first
sort: "-data.execTime"        // Slowest first
sort: "-rowid"                // Most efficient (newest)
sort: "level,-created"        // By level, then newest
```

## Complete Examples

### Example 1: Error Monitoring Dashboard

```csharp
async Task<Dictionary<string, object?>> GetErrorMetrics()
{
  // Get error logs from last 24 hours
  var yesterday = DateTime.UtcNow.AddDays(-1);
  var dateFilter = $"created >= \"{yesterday:yyyy-MM-dd} 00:00:00\"";
  
  // 4xx errors
  var clientErrors = await client.Logs.GetListAsync(1, 100, 
    filter: $"{dateFilter} && data.status >= 400 && data.status < 500",
    sort: "-created");
  
  // 5xx errors
  var serverErrors = await client.Logs.GetListAsync(1, 100, 
    filter: $"{dateFilter} && data.status >= 500",
    sort: "-created");
  
  // Get hourly statistics
  var errorStats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
  {
    ["filter"] = $"{dateFilter} && data.status >= 400"
  });
  
  return new Dictionary<string, object?>
  {
    ["clientErrors"] = clientErrors["items"],
    ["serverErrors"] = serverErrors["items"],
    ["stats"] = errorStats,
  };
}
```

### Example 2: Performance Analysis

```csharp
async Task<Dictionary<string, Dictionary<string, object?>>> AnalyzePerformance()
{
  // Get slow requests
  var slowRequests = await client.Logs.GetListAsync(1, 50, 
    filter: "data.execTime > 1.0",
    sort: "-data.execTime");
  
  // Analyze by endpoint
  var endpointStats = new Dictionary<string, Dictionary<string, object?>>();
  
  if (slowRequests["items"] is List<object?> items)
  {
    foreach (var item in items)
    {
      if (item is Dictionary<string, object?> log)
      {
        var data = log["data"] as Dictionary<string, object?>;
        var url = data?["url"]?.ToString()?.Split('?')[0] ?? ""; // Remove query params
        
        if (!endpointStats.ContainsKey(url))
        {
          endpointStats[url] = new Dictionary<string, object?>
          {
            ["count"] = 0,
            ["totalTime"] = 0.0,
            ["maxTime"] = 0.0,
          };
        }
        
        var execTime = Convert.ToDouble(data?["execTime"] ?? 0);
        endpointStats[url]["count"] = Convert.ToInt32(endpointStats[url]["count"]) + 1;
        endpointStats[url]["totalTime"] = Convert.ToDouble(endpointStats[url]["totalTime"]) + execTime;
        endpointStats[url]["maxTime"] = Math.Max(Convert.ToDouble(endpointStats[url]["maxTime"]), execTime);
      }
    }
  }
  
  // Calculate averages
  foreach (var url in endpointStats.Keys.ToList())
  {
    var count = Convert.ToInt32(endpointStats[url]["count"]);
    var totalTime = Convert.ToDouble(endpointStats[url]["totalTime"]);
    endpointStats[url]["avgTime"] = totalTime / count;
  }
  
  return endpointStats;
}
```

### Example 3: Security Monitoring

```csharp
async Task<Dictionary<string, object?>> MonitorSecurity()
{
  // Failed authentication attempts
  var authFailures = await client.Logs.GetListAsync(1, 100, 
    filter: "data.url ~ \"/api/collections/\" && data.url ~ \"/auth-with-password\" && data.status >= 400",
    sort: "-created");
  
  // Suspicious IPs (multiple failed attempts)
  var ipCounts = new Dictionary<string, int>();
  
  if (authFailures["items"] is List<object?> items)
  {
    foreach (var item in items)
    {
      if (item is Dictionary<string, object?> log)
      {
        var data = log["data"] as Dictionary<string, object?>;
        var ip = data?["remoteIP"]?.ToString() ?? "";
        ipCounts[ip] = ipCounts.GetValueOrDefault(ip, 0) + 1;
      }
    }
  }
  
  var suspiciousIPs = ipCounts
    .Where(kvp => kvp.Value >= 5)
    .Select(kvp => new Dictionary<string, object?> { ["ip"] = kvp.Key, ["attempts"] = kvp.Value })
    .ToList();
  
  return new Dictionary<string, object?>
  {
    ["totalFailures"] = authFailures["totalItems"],
    ["suspiciousIPs"] = suspiciousIPs,
  };
}
```

### Example 4: API Usage Analytics

```csharp
async Task<Dictionary<string, object?>> GetAPIUsage()
{
  var stats = await client.Logs.GetStatsAsync(new Dictionary<string, object?>
  {
    ["filter"] = "data.url ~ \"/api/\" && data.auth != \"_superusers\""
  });
  
  // Group by method
  var methods = new Dictionary<string, int>();
  var recentLogs = await client.Logs.GetListAsync(1, 1000, 
    filter: "data.url ~ \"/api/\" && data.auth != \"_superusers\"");
  
  if (recentLogs["items"] is List<object?> items)
  {
    foreach (var item in items)
    {
      if (item is Dictionary<string, object?> log)
      {
        var data = log["data"] as Dictionary<string, object?>;
        var method = data?["method"]?.ToString() ?? "";
        methods[method] = methods.GetValueOrDefault(method, 0) + 1;
      }
    }
  }
  
  return new Dictionary<string, object?>
  {
    ["hourlyStats"] = stats,
    ["methodBreakdown"] = methods,
    ["totalRequests"] = recentLogs["totalItems"],
  };
}
```

### Example 5: Real-time Error Tracking

```csharp
async Task<List<Dictionary<string, object?>>> TrackErrorsInRealTime()
{
  var lastCheck = DateTime.UtcNow.AddMinutes(-1); // Last minute
  
  var newErrors = await client.Logs.GetListAsync(1, 100, 
    filter: $"created >= \"{lastCheck:yyyy-MM-ddTHH:mm:ss}\" && data.status >= 400",
    sort: "-created");
  
  if (newErrors["items"] is List<object?> items && items.Count > 0)
  {
    Console.Warn($"Found {items.Count} errors in the last minute:");
    foreach (var item in items)
    {
      if (item is Dictionary<string, object?> log)
      {
        var data = log["data"] as Dictionary<string, object?>;
        Console.Error.WriteLine($"[{data?["status"]}] {data?["method"]} {data?["url"]}");
      }
    }
    
    // Send alerts, notifications, etc.
  }
  
  return items?.Cast<Dictionary<string, object?>>().ToList() ?? new List<Dictionary<string, object?>>();
}
```

### Example 6: Log Viewer Component

```csharp
class LogViewer
{
  private readonly BosbaseClient _client;
  private int _currentPage = 1;
  private int _perPage = 50;
  private string _filter = "";
  private string _sort = "-created";

  public LogViewer(BosbaseClient client)
  {
    _client = client;
  }

  public async Task<Dictionary<string, object?>> LoadLogs()
  {
    return await _client.Logs.GetListAsync(_currentPage, _perPage, _filter, _sort);
  }

  public async Task<Dictionary<string, object?>> SearchLogs(string searchTerm)
  {
    _filter = $"message ~ \"{searchTerm}\" || data.url ~ \"{searchTerm}\"";
    _currentPage = 1;
    return await LoadLogs();
  }

  public async Task<Dictionary<string, object?>> FilterByStatus(int status)
  {
    _filter = $"data.status = {status}";
    _currentPage = 1;
    return await LoadLogs();
  }

  public async Task<Dictionary<string, object?>> GetErrorRate()
  {
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
    var stats = await _client.Logs.GetStatsAsync(new Dictionary<string, object?>
    {
      ["filter"] = $"created >= \"{today} 00:00:00\""
    });
    
    var errorStats = await _client.Logs.GetStatsAsync(new Dictionary<string, object?>
    {
      ["filter"] = $"created >= \"{today} 00:00:00\" && data.status >= 400"
    });
    
    var total = stats?.Sum(s => Convert.ToInt32(s["total"] ?? 0)) ?? 0;
    var errors = errorStats?.Sum(s => Convert.ToInt32(s["total"] ?? 0)) ?? 0;
    
    return new Dictionary<string, object?>
    {
      ["total"] = total,
      ["errors"] = errors,
      ["rate"] = total > 0 ? (errors / (double)total) * 100 : 0,
    };
  }
}
```

## Error Handling

```csharp
try
{
  var logs = await client.Logs.GetListAsync(1, 50, filter: "data.status >= 400");
}
catch (ClientResponseError ex)
{
  if (ex.Status == 401)
  {
    Console.Error.WriteLine("Not authenticated");
  }
  else if (ex.Status == 403)
  {
    Console.Error.WriteLine("Not a superuser");
  }
  else if (ex.Status == 400)
  {
    Console.Error.WriteLine($"Invalid filter: {ex.Message}");
  }
  else
  {
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
  }
}
```

## Best Practices

1. **Use Filters**: Always use filters to narrow down results, especially for large log datasets
2. **Paginate**: Use pagination instead of fetching all logs at once
3. **Efficient Sorting**: Use `-rowid` for default sorting (most efficient)
4. **Filter Statistics**: Always filter statistics for meaningful insights
5. **Monitor Errors**: Regularly check for 4xx/5xx errors
6. **Performance Tracking**: Monitor execution times for slow endpoints
7. **Security Auditing**: Track authentication failures and suspicious activity
8. **Archive Old Logs**: Consider deleting or archiving old logs to maintain performance

## Limitations

- **Superuser Only**: All operations require superuser authentication
- **Data Fields**: Only fields in the `data` object are filterable
- **Statistics**: Statistics are aggregated hourly
- **Performance**: Large log datasets may be slow to query
- **Storage**: Logs accumulate over time and may need periodic cleanup

## Log Levels

- **0**: Info (normal requests)
- **> 0**: Warnings/Errors (non-200 status codes, exceptions, etc.)

Higher values typically indicate more severe issues.

## Related Documentation

- [Authentication](./AUTHENTICATION.md) - User authentication
- [API Records](./API_RECORDS.md) - Record operations
- [Collection API](./COLLECTION_API.md) - Collection management

