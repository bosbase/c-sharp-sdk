# Crons API - C# SDK Documentation

## Overview

The Crons API provides endpoints for viewing and manually triggering scheduled cron jobs. All operations require superuser authentication and allow you to list registered cron jobs and execute them on-demand.

**Key Features:**
- List all registered cron jobs
- View cron job schedules (cron expressions)
- Manually trigger cron jobs
- Built-in system jobs for maintenance tasks

**Backend Endpoints:**
- `GET /api/crons` - List cron jobs
- `POST /api/crons/{jobId}` - Run cron job

**Note**: All Crons API operations require superuser authentication.

## Authentication

All Crons API operations require superuser authentication:

```csharp
using Bosbase;

var client = new BosbaseClient("http://127.0.0.1:8090");

// Authenticate as superuser
await client.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");
```

## List Cron Jobs

Returns a list of all registered cron jobs with their IDs and schedule expressions.

### Basic Usage

```csharp
// Get all cron jobs
var jobs = await client.Crons.GetFullListAsync();

foreach (var job in jobs)
{
    Console.WriteLine($"ID: {job["id"]}, Expression: {job["expression"]}");
}
```

### Cron Job Structure

Each cron job contains:

```csharp
{
  "id": string,        // Unique identifier for the job
  "expression": string // Cron expression defining the schedule
}
```

### Built-in System Jobs

The following cron jobs are typically registered by default:

| Job ID | Expression | Description | Schedule |
|--------|-----------|-------------|----------|
| `__pbLogsCleanup__` | `0 */6 * * *` | Cleans up old log entries | Every 6 hours |
| `__pbDBOptimize__` | `0 0 * * *` | Optimizes database | Daily at midnight |
| `__pbMFACleanup__` | `0 * * * *` | Cleans up expired MFA records | Every hour |
| `__pbOTPCleanup__` | `0 * * * *` | Cleans up expired OTP codes | Every hour |

### Working with Cron Jobs

```csharp
// List all cron jobs
var jobs = await client.Crons.GetFullListAsync();

// Find a specific job
var logsCleanup = jobs.FirstOrDefault(j => j["id"]?.ToString() == "__pbLogsCleanup__");

if (logsCleanup != null)
{
    Console.WriteLine($"Logs cleanup runs: {logsCleanup["expression"]}");
}

// Filter system jobs
var systemJobs = jobs.Where(j => j["id"]?.ToString()?.StartsWith("__pb") == true).ToList();

// Filter custom jobs
var customJobs = jobs.Where(j => !(j["id"]?.ToString()?.StartsWith("__pb") == true)).ToList();
```

## Run Cron Job

Manually trigger a cron job to execute immediately.

### Basic Usage

```csharp
// Run a specific cron job
await client.Crons.RunAsync("__pbLogsCleanup__");
```

### Use Cases

```csharp
// Trigger logs cleanup manually
async Task CleanupLogsNow()
{
  await client.Crons.RunAsync("__pbLogsCleanup__");
  Console.WriteLine("Logs cleanup triggered");
}

// Trigger database optimization
async Task OptimizeDatabase()
{
  await client.Crons.RunAsync("__pbDBOptimize__");
  Console.WriteLine("Database optimization triggered");
}

// Trigger MFA cleanup
async Task CleanupMFA()
{
  await client.Crons.RunAsync("__pbMFACleanup__");
  Console.WriteLine("MFA cleanup triggered");
}

// Trigger OTP cleanup
async Task CleanupOTP()
{
  await client.Crons.RunAsync("__pbOTPCleanup__");
  Console.WriteLine("OTP cleanup triggered");
}
```

## Cron Expression Format

Cron expressions use the standard 5-field format:

```
* * * * *
│ │ │ │ │
│ │ │ │ └─── Day of week (0-7, 0 or 7 is Sunday)
│ │ │ └───── Month (1-12)
│ │ └─────── Day of month (1-31)
│ └───────── Hour (0-23)
└─────────── Minute (0-59)
```

### Common Patterns

| Expression | Description |
|------------|-------------|
| `0 * * * *` | Every hour at minute 0 |
| `0 */6 * * *` | Every 6 hours |
| `0 0 * * *` | Daily at midnight |
| `0 0 * * 0` | Weekly on Sunday at midnight |
| `0 0 1 * *` | Monthly on the 1st at midnight |
| `*/30 * * * *` | Every 30 minutes |
| `0 9 * * 1-5` | Weekdays at 9 AM |

### Supported Macros

| Macro | Equivalent Expression | Description |
|-------|----------------------|-------------|
| `@yearly` or `@annually` | `0 0 1 1 *` | Once a year |
| `@monthly` | `0 0 1 * *` | Once a month |
| `@weekly` | `0 0 * * 0` | Once a week |
| `@daily` or `@midnight` | `0 0 * * *` | Once a day |
| `@hourly` | `0 * * * *` | Once an hour |

### Expression Examples

```csharp
// Every hour
"0 * * * *"

// Every 6 hours
"0 */6 * * *"

// Daily at midnight
"0 0 * * *"

// Every 30 minutes
"*/30 * * * *"

// Weekdays at 9 AM
"0 9 * * 1-5"

// First day of every month
"0 0 1 * *"

// Using macros
"@daily"   // Same as "0 0 * * *"
"@hourly"  // Same as "0 * * * *"
```

## Complete Examples

### Example 1: Cron Job Monitor

```csharp
class CronMonitor
{
  private readonly BosbaseClient _client;

  public CronMonitor(BosbaseClient client)
  {
    _client = client;
  }

  public async Task<List<Dictionary<string, object?>>> ListAllJobs()
  {
    var jobs = await _client.Crons.GetFullListAsync();
    
    Console.WriteLine($"Found {jobs.Count} cron jobs:");
    foreach (var job in jobs)
    {
      Console.WriteLine($"  - {job["id"]}: {job["expression"]}");
    }
    
    return jobs;
  }

  public async Task<bool> RunJob(string jobId)
  {
    try
    {
      await _client.Crons.RunAsync(jobId);
      Console.WriteLine($"Successfully triggered: {jobId}");
      return true;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Failed to run {jobId}: {ex.Message}");
      return false;
    }
  }

  public async Task RunMaintenanceJobs()
  {
    var maintenanceJobs = new[]
    {
      "__pbLogsCleanup__",
      "__pbDBOptimize__",
      "__pbMFACleanup__",
      "__pbOTPCleanup__",
    };

    foreach (var jobId in maintenanceJobs)
    {
      Console.WriteLine($"Running {jobId}...");
      await RunJob(jobId);
      // Wait a bit between jobs
      await Task.Delay(1000);
    }
  }
}

// Usage
var monitor = new CronMonitor(client);
await monitor.ListAllJobs();
await monitor.RunMaintenanceJobs();
```

### Example 2: Cron Job Health Check

```csharp
async Task<bool> CheckCronJobs()
{
  try
  {
    var jobs = await client.Crons.GetFullListAsync();
    
    var expectedJobs = new[]
    {
      "__pbLogsCleanup__",
      "__pbDBOptimize__",
      "__pbMFACleanup__",
      "__pbOTPCleanup__",
    };
    
    var missingJobs = expectedJobs
      .Where(expectedId => !jobs.Any(job => job["id"]?.ToString() == expectedId))
      .ToList();
    
    if (missingJobs.Count > 0)
    {
      Console.Warn($"Missing expected cron jobs: {string.Join(", ", missingJobs)}");
      return false;
    }
    
    Console.WriteLine("All expected cron jobs are registered");
    return true;
  }
  catch (Exception ex)
  {
    Console.Error.WriteLine($"Failed to check cron jobs: {ex.Message}");
    return false;
  }
}
```

### Example 3: Manual Maintenance Script

```csharp
async Task PerformMaintenance()
{
  Console.WriteLine("Starting maintenance tasks...");
  
  // Cleanup old logs
  Console.WriteLine("1. Cleaning up old logs...");
  await client.Crons.RunAsync("__pbLogsCleanup__");
  
  // Cleanup expired MFA records
  Console.WriteLine("2. Cleaning up expired MFA records...");
  await client.Crons.RunAsync("__pbMFACleanup__");
  
  // Cleanup expired OTP codes
  Console.WriteLine("3. Cleaning up expired OTP codes...");
  await client.Crons.RunAsync("__pbOTPCleanup__");
  
  // Optimize database (run last as it may take longer)
  Console.WriteLine("4. Optimizing database...");
  await client.Crons.RunAsync("__pbDBOptimize__");
  
  Console.WriteLine("Maintenance tasks completed");
}
```

### Example 4: Cron Job Status Dashboard

```csharp
async Task<Dictionary<string, object?>> GetCronStatus()
{
  var jobs = await client.Crons.GetFullListAsync();
  
  var status = new Dictionary<string, object?>
  {
    ["total"] = jobs.Count,
    ["system"] = jobs.Count(j => j["id"]?.ToString()?.StartsWith("__pb") == true),
    ["custom"] = jobs.Count(j => !(j["id"]?.ToString()?.StartsWith("__pb") == true)),
    ["jobs"] = jobs.Select(job => new Dictionary<string, object?>
    {
      ["id"] = job["id"],
      ["expression"] = job["expression"],
      ["type"] = job["id"]?.ToString()?.StartsWith("__pb") == true ? "system" : "custom",
    }).ToList(),
  };
  
  return status;
}

// Usage
var status = await GetCronStatus();
Console.WriteLine($"Total: {status["total"]}, System: {status["system"]}, Custom: {status["custom"]}");
```

### Example 5: Scheduled Maintenance Trigger

```csharp
// Function to trigger maintenance jobs on a schedule
class ScheduledMaintenance
{
  private readonly BosbaseClient _client;
  private readonly int _intervalMinutes;
  private System.Timers.Timer? _timer;

  public ScheduledMaintenance(BosbaseClient client, int intervalMinutes = 60)
  {
    _client = client;
    _intervalMinutes = intervalMinutes;
  }

  public void Start()
  {
    // Run immediately
    RunMaintenance();
    
    // Then run on schedule
    _timer = new System.Timers.Timer(_intervalMinutes * 60 * 1000);
    _timer.Elapsed += async (sender, e) => await RunMaintenance();
    _timer.Start();
  }

  public void Stop()
  {
    _timer?.Stop();
    _timer?.Dispose();
    _timer = null;
  }

  private async Task RunMaintenance()
  {
    try
    {
      Console.WriteLine("Running scheduled maintenance...");
      
      // Run cleanup jobs
      await _client.Crons.RunAsync("__pbLogsCleanup__");
      await _client.Crons.RunAsync("__pbMFACleanup__");
      await _client.Crons.RunAsync("__pbOTPCleanup__");
      
      Console.WriteLine("Scheduled maintenance completed");
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Maintenance failed: {ex.Message}");
    }
  }
}

// Usage
var maintenance = new ScheduledMaintenance(client, 60); // Every hour
maintenance.Start();
```

### Example 6: Cron Job Testing

```csharp
async Task<bool> TestCronJob(string jobId)
{
  Console.WriteLine($"Testing cron job: {jobId}");
  
  try
  {
    // Check if job exists
    var jobs = await client.Crons.GetFullListAsync();
    var job = jobs.FirstOrDefault(j => j["id"]?.ToString() == jobId);
    
    if (job == null)
    {
      Console.Error.WriteLine($"Cron job {jobId} not found");
      return false;
    }
    
    Console.WriteLine($"Job found with expression: {job["expression"]}");
    
    // Run the job
    Console.WriteLine("Triggering job...");
    await client.Crons.RunAsync(jobId);
    
    Console.WriteLine("Job triggered successfully");
    return true;
  }
  catch (Exception ex)
  {
    Console.Error.WriteLine($"Failed to test cron job: {ex.Message}");
    return false;
  }
}

// Test a specific job
await TestCronJob("__pbLogsCleanup__");
```

## Error Handling

```csharp
try
{
  var jobs = await client.Crons.GetFullListAsync();
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
  else
  {
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
  }
}

try
{
  await client.Crons.RunAsync("__pbLogsCleanup__");
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
  else if (ex.Status == 404)
  {
    Console.Error.WriteLine("Cron job not found");
  }
  else
  {
    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
  }
}
```

## Best Practices

1. **Check Job Existence**: Verify a cron job exists before trying to run it
2. **Error Handling**: Always handle errors when running cron jobs
3. **Rate Limiting**: Don't trigger cron jobs too frequently manually
4. **Monitoring**: Regularly check that expected cron jobs are registered
5. **Logging**: Log when cron jobs are manually triggered for auditing
6. **Testing**: Test cron jobs in development before running in production
7. **Documentation**: Document custom cron jobs and their purposes
8. **Scheduling**: Let the cron scheduler handle regular execution; use manual triggers sparingly

## Limitations

- **Superuser Only**: All operations require superuser authentication
- **Read-Only API**: The SDK API only allows listing and running jobs; adding/removing jobs must be done via backend hooks
- **Asynchronous Execution**: Running a cron job triggers it asynchronously; the API returns immediately
- **No Status**: The API doesn't provide execution status or history
- **System Jobs**: Built-in system jobs (prefixed with `__pb`) cannot be removed via the API

## Custom Cron Jobs

Custom cron jobs are typically registered through backend hooks (JavaScript VM plugins). The Crons API only allows you to:

- **View** all registered jobs (both system and custom)
- **Trigger** any registered job manually

To add or remove cron jobs, you need to use the backend hook system:

```javascript
// In a backend hook file (pb_hooks/main.js)
routerOnInit((e) => {
  // Add custom cron job
  cronAdd("myCustomJob", "0 */2 * * *", () => {
    console.log("Custom job runs every 2 hours");
    // Your custom logic here
  });
});
```

## Related Documentation

- [Collection API](./COLLECTION_API.md) - Collection management
- [Logs API](./LOGS_API.md) - Log viewing and analysis
- [Backups API](./BACKUPS_API.md) - Backup management (if available)

