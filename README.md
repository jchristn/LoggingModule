<img src="assets/logo.png" alt="SyslogLogging Logo" width="128" height="128" />

# SyslogLogging

[![NuGet Version](https://img.shields.io/nuget/v/SyslogLogging.svg?style=flat)](https://www.nuget.org/packages/SyslogLogging/) [![NuGet](https://img.shields.io/nuget/dt/SyslogLogging.svg)](https://www.nuget.org/packages/SyslogLogging)

🚀 **Modern, high-performance C# logging library** for syslog, console, and file destinations with **async support**, **structured logging**, and **Microsoft.Extensions.Logging integration**.

Targeted to .NET Standard 2.0+, .NET Framework 4.6.2+, .NET 6.0+, and .NET 8.0.

## ✨ What's New in v2.0.9+

### 🔥 **Major New Features**
- 🌪️ **Full async support** with `CancellationToken` throughout
- 📊 **Structured logging** with properties, correlation IDs, and JSON serialization
- 🔌 **Microsoft.Extensions.Logging integration** (ILogger, DI support)
- 🏗️ **Enterprise-grade thread safety** with comprehensive race condition prevention
- 🎯 **Integrated SyslogServer** for end-to-end testing
- 🛡️ **Comprehensive input validation** on all public properties
- 🧪 **Extensive thread safety testing** (20+ specialized concurrent scenarios)

### 🔧 **Performance & Reliability**
- **Thread-safe operations** with proper locking mechanisms
- **Immediate log delivery** with direct processing
- **Memory efficient** with minimal overhead
- **Standards compliant** RFC 3164 syslog format support

## 🚀 Quick Start

### Simple Logging
```csharp
using SyslogLogging;

LoggingModule log = new LoggingModule();
await log.InfoAsync("Hello, world!");
```

### Async with Structured Data
```csharp
using SyslogLogging;

LoggingModule log = new LoggingModule("mysyslogserver", 514);

// Simple async logging
await log.ErrorAsync("Something went wrong", cancellationToken);

// Structured logging with properties
LogEntry entry = new LogEntry(Severity.Warning, "Rate limit exceeded")
    .WithProperty("RequestsPerSecond", 150)
    .WithProperty("ClientId", "user123")
    .WithCorrelationId(Request.Headers["X-Correlation-ID"]);

await log.LogEntryAsync(entry);
```

### Fluent Structured Logging
```csharp
log.BeginStructuredLog(Severity.Info, "User login")
    .WithProperty("UserId", userId)
    .WithProperty("IpAddress", ipAddress)
    .WithProperty("Timestamp", DateTime.UtcNow)
    .WithCorrelationId(correlationId)
    .WriteAsync();
```

## 🔌 Microsoft.Extensions.Logging Integration

### ASP.NET Core / Generic Host
```csharp
// Program.cs or Startup.cs
services.AddLogging(builder =>
{
    builder.AddSyslog("syslogserver", 514);
});

// In your controllers/services
public class MyController : ControllerBase
{
    private readonly ILogger<MyController> _logger;

    public MyController(ILogger<MyController> logger)
    {
        _logger = logger;
    }

    public IActionResult Get()
    {
        _logger.LogInformation("API called with correlation {CorrelationId}",
            HttpContext.TraceIdentifier);
        return Ok();
    }
}
```

### Multiple Destinations
```csharp
services.AddLogging(builder =>
{
    builder.AddSyslog(new List<SyslogServer>
    {
        new SyslogServer("primary-log", 514),
        new SyslogServer("backup-log", 514)
    }, enableConsole: true);
});
```

## 📊 Advanced Structured Logging

### Rich Metadata
```csharp
LogEntry entry = new LogEntry(Severity.Error, "Payment processing failed")
    .WithProperty("OrderId", orderId)
    .WithProperty("Amount", amount)
    .WithProperty("Currency", "USD")
    .WithProperty("PaymentProvider", "Stripe")
    .WithProperty("ErrorCode", errorCode)
    .WithCorrelationId(correlationId)
    .WithSource("PaymentService")
    .WithException(exception);

await log.LogEntryAsync(entry);
```

### JSON Serialization
```csharp
LogEntry entry = new LogEntry(Severity.Info, "User session")
    .WithProperty("SessionDuration", TimeSpan.FromMinutes(45))
    .WithProperty("PagesVisited", new[] { "/home", "/products", "/checkout" });

string json = entry.ToJson();
// Output: {"timestamp":"2023-12-01T10:30:00.000Z","severity":"Info","message":"User session","threadId":1,"properties":{"SessionDuration":"00:45:00","PagesVisited":["/home","/products","/checkout"]}}
```

## 🎯 Multiple Destinations

### Syslog + Console + File
```csharp
List<SyslogServer> servers = new List<SyslogServer>
{
    new SyslogServer("primary-syslog", 514),
    new SyslogServer("backup-syslog", 514)
};

LoggingModule log = new LoggingModule(servers, enableConsole: true);
log.Settings.FileLogging = FileLoggingMode.FileWithDate;  // Creates dated files
log.Settings.LogFilename = "./logs/app.log";

log.Alert("This goes to 2 syslog servers, console, AND file!");
```

### File-Only Logging
```csharp
LoggingModule log = new LoggingModule("./logs/app.log", FileLoggingMode.SingleLogFile);
await log.InfoAsync("File-only message");
```

### Log Retention (Automatic Cleanup)
```csharp
// Automatically delete log files older than 30 days
LoggingModule log = new LoggingModule("./logs/app.log", FileLoggingMode.FileWithDate, true);
LoggingSettings settings = log.Settings;
settings.LogRetentionDays = 30;  // Keep 30 days of logs (0 = disabled, default)
log.Settings = settings;         // Re-assign to start cleanup timer

// Or configure settings first
LoggingSettings settings = new LoggingSettings();
settings.LogFilename = "./logs/app.log";
settings.FileLogging = FileLoggingMode.FileWithDate;
settings.LogRetentionDays = 7;   // Keep 7 days of logs
LoggingModule log = new LoggingModule();
log.Settings = settings;
```

**Note:** Log retention only applies when using `FileLoggingMode.FileWithDate`. The cleanup timer runs every 60 seconds and removes files matching the pattern `filename.ext.yyyyMMdd` that are older than the specified retention period.

## 🎨 Console Colors & Formatting

### Enable Colors
```csharp
log.Settings.EnableColors = true;
log.Settings.Colors.Error = new ColorScheme(ConsoleColor.Red, ConsoleColor.Black);
log.Settings.Colors.Warning = new ColorScheme(ConsoleColor.Yellow, ConsoleColor.Black);
```

### Custom Message Format with Rich Variables
```csharp
// Basic format
log.Settings.HeaderFormat = "{ts} [{sev}] {host}:{thread}";

// Detailed production format
log.Settings.HeaderFormat = "{ts} {host}[{pid}] {sev} [T:{thread}] [{app}]";

// Performance monitoring format
log.Settings.HeaderFormat = "{ts} {host} CPU:{cpu} MEM:{mem}MB UP:{uptime} {sev}";

// Microservices format
log.Settings.HeaderFormat = "{ts} [{app}:{pid}] {sev} [{correlation}] [{source}]";

log.Settings.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
log.Settings.UseUtcTime = true;
```

#### Available Header Format Variables

| Variable | Description | Example Output |
|----------|-------------|----------------|
| `{ts}` | Timestamp | `2024-01-15 14:30:25.123` |
| `{host}` | Machine name | `web-server-01` |
| `{thread}` | Thread ID | `12` |
| `{sev}` | Severity name | `Info` |
| `{level}` | Severity number (0-7) | `6` |
| `{pid}` | Process ID | `1234` |
| `{user}` | Current username | `john.doe` |
| `{app}` | Application name | `MyWebApp` |
| `{domain}` | App domain | `MyWebApp.exe` |
| `{cpu}` | CPU core count | `8` |
| `{mem}` | Memory usage (MB) | `256` |
| `{uptime}` | Process uptime | `02:45:30` |
| `{correlation}` | Correlation ID | `abc-123-def` |
| `{source}` | Log source | `UserService` |

## 🔧 Configuration Examples

### Production Configuration
```csharp
LoggingModule log = new LoggingModule("prod-syslog", 514, enableConsole: false);

// Set appropriate filters
log.Settings.MinimumSeverity = Severity.Warning;
log.Settings.MaxMessageLength = 8192;

// Structured logging for analysis
await log.BeginStructuredLog(Severity.Info, "Application started")
    .WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version)
    .WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
    .WithProperty("MachineName", Environment.MachineName)
    .WriteAsync();
```

### Development Configuration
```csharp
LoggingModule log = new LoggingModule("localhost", 514, enableConsole: true);

// Immediate feedback for development
log.Settings.EnableColors = true;
log.Settings.MinimumSeverity = Severity.Debug;

// File logging for detailed debugging
log.Settings.FileLogging = FileLoggingMode.FileWithDate;
log.Settings.LogFilename = "./logs/debug.log";
```

### High-Concurrency Configuration
```csharp
LoggingModule log = new LoggingModule("logserver", 514, enableConsole: true);

// Thread-safe operations
log.Settings.EnableColors = true;

Task.Run(async () =>
{
    while (true)
    {
        // Even rapid server changes are thread-safe
        log.Servers = GetAvailableServers();
        await Task.Delay(1000);
    }
});

// Multiple threads can safely log concurrently
Parallel.For(0, 1000, i =>
{
    log.Info($"Concurrent message from thread {Thread.CurrentThread.ManagedThreadId}: {i}");
});
```

## 🧪 Testing

Run the comprehensive test suite:

```bash
cd src/Test
dotnet run
```

The test program validates each library capability including:
- ✅ All constructor patterns and validation
- ✅ Sync and async logging methods
- ✅ Structured logging with properties and correlation IDs
- ✅ Comprehensive thread safety under concurrent load
- ✅ Multiple destination delivery (syslog + console + file)
- ✅ Error handling and edge cases
- ✅ Performance benchmarks
- ✅ SyslogServer integration and end-to-end testing

## 🤝 Help or Feedback

Found a bug or have a feature request? [File an issue](https://github.com/jchristn/LoggingModule/issues) - we'd love to hear from you!

## 🙏 Special Thanks

We'd like to extend a special thank you to those that have helped make this library better:
@dev-jan @jisotalo

## 📜 Version History

Please refer to [CHANGELOG.md](./CHANGELOG.md) for detailed version history.

---

⭐ **Star this repo** if SyslogLogging has helped your project!