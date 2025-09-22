# SyslogLogging

[![NuGet Version](https://img.shields.io/nuget/v/SyslogLogging.svg?style=flat)](https://www.nuget.org/packages/SyslogLogging/) [![NuGet](https://img.shields.io/nuget/dt/SyslogLogging.svg)](https://www.nuget.org/packages/SyslogLogging)

🚀 **Modern, high-performance C# logging library** for syslog, console, and file destinations with **async support**, **structured logging**, **background queuing**, and **Microsoft.Extensions.Logging integration**.

Targeted to .NET Standard 2.0+, .NET Framework 4.6.2+, .NET 6.0+, and .NET 8.0.

## ✨ What's New in v2.0.9+

### 🔥 **Major New Features**
- 🌪️ **Full async support** with `CancellationToken` throughout
- 📊 **Structured logging** with properties, correlation IDs, and JSON serialization
- 🔌 **Microsoft.Extensions.Logging integration** (ILogger, DI support)
- ⚡ **Background message queuing** with batching for I/O optimization
- 💾 **Persistent queues** that survive application restarts
- 🏗️ **Enterprise-grade thread safety** with comprehensive race condition prevention
- 🎯 **Integrated SyslogServer** for end-to-end testing
- 🛡️ **Comprehensive input validation** on all public properties
- 🧪 **Extensive thread safety testing** (20+ specialized concurrent scenarios)

### 🔧 **Performance & Reliability**
- **>100K messages/second** throughput in background mode
- **<1ms latency** for queue operations
- **Memory pressure handling** with configurable limits
- **Automatic batch processing** for optimal I/O
- **Zero message loss** with persistent file-backed queues

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

### High-Performance Background Processing
```csharp
LoggingModule log = new LoggingModule("logserver", 514);
log.Settings.EnableBackgroundQueue = true;
log.Settings.BatchSize = 100;        // Process 100 messages at once
log.Settings.FlushInterval = 5000;   // Flush every 5 seconds

// Fire-and-forget logging - messages are queued and processed in background
log.Info("High-throughput message 1");
log.Info("High-throughput message 2");
// ... thousands more messages
```

## 🔌 Microsoft.Extensions.Logging Integration

### ASP.NET Core / Generic Host
```csharp
// Program.cs or Startup.cs
services.AddLogging(builder =>
{
    builder.AddSyslog("syslogserver", 514, configure: module =>
    {
        module.Settings.EnableBackgroundQueue = true;
        module.Settings.BatchSize = 50;
    });
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

## ⚡ Background Processing & Batching

### Enable Background Queue
```csharp
LoggingModule log = new LoggingModule("logserver", 514);
log.Settings.EnableBackgroundQueue = true;
log.Settings.BatchSize = 100;           // Batch up to 100 messages
log.Settings.FlushInterval = 5000;      // Force flush every 5 seconds
log.Settings.MaxMemoryEntries = 10000;  // Queue up to 10K messages in memory

// Messages are now processed asynchronously in background
for (int i = 0; i < 100000; i++)
{
    log.Info($"High-volume message {i}");  // Returns immediately
}

// Ensure all messages are processed before shutdown
await log.FlushAsync();
```

### Persistent Queues
```csharp
// Messages survive application restarts
log.Settings.EnablePersistentQueue = true;
log.Settings.QueueFilePath = "./logs/message-queue.dat";

log.Critical("This message will be delivered even if app crashes!");
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

## 🛡️ Input Validation & Safety

All public properties now have comprehensive validation:

```csharp
// Validates port range (0-65535)
server.Port = 65536; // ❌ Throws ArgumentOutOfRangeException

// Validates hostname not null/empty
server.Hostname = null; // ❌ Throws ArgumentException

// Validates minimum values
log.Settings.MaxMessageLength = 16; // ❌ Throws ArgumentOutOfRangeException (min: 32)

// Validates color schemes
log.Settings.Colors.Debug = null; // ❌ Throws ArgumentNullException
```

## 🧪 Testing with Integrated SyslogServer

The library now includes a complete SyslogServer for testing:

```csharp
// Start a test syslog server
var serverSettings = new Syslog.Settings
{
    UdpPort = 5140,
    LogFileDirectory = "./test-logs/"
};

// Use with your logging client
LoggingModule log = new LoggingModule("127.0.0.1", 5140);
await log.InfoAsync("Test message");

// Server automatically logs to file for verification
```

## 📈 Performance Metrics

### Sync vs Async Performance
```csharp
// Measure sync performance
DateTime start = DateTime.UtcNow;
for (int i = 0; i < 1000; i++)
{
    log.Info($"Sync message {i}");
}
await log.FlushAsync();
DateTime end = DateTime.UtcNow;
double syncMps = 1000.0 / (end - start).TotalSeconds;

// Measure async performance
start = DateTime.UtcNow;
List<Task> tasks = new List<Task>();
for (int i = 0; i < 1000; i++)
{
    tasks.Add(log.InfoAsync($"Async message {i}"));
}
await Task.WhenAll(tasks);
await log.FlushAsync();
end = DateTime.UtcNow;
double asyncMps = 1000.0 / (end - start).TotalSeconds;

Console.WriteLine($"Sync: {syncMps:F0} msg/sec, Async: {asyncMps:F0} msg/sec");
```

## 🔧 Configuration Examples

### Production Configuration
```csharp
LoggingModule log = new LoggingModule("prod-syslog", 514, enableConsole: false);

// Enable background processing for high throughput
log.Settings.EnableBackgroundQueue = true;
log.Settings.BatchSize = 200;
log.Settings.FlushInterval = 10000;

// Enable persistence for reliability
log.Settings.EnablePersistentQueue = true;
log.Settings.QueueFilePath = "/var/log/app/message-queue.dat";

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
log.Settings.EnableBackgroundQueue = false;
log.Settings.EnableColors = true;
log.Settings.MinimumSeverity = Severity.Debug;

// File logging for detailed debugging
log.Settings.FileLogging = FileLoggingMode.FileWithDate;
log.Settings.LogFilename = "./logs/debug.log";
```

### High-Concurrency Configuration
```csharp
LoggingModule log = new LoggingModule("logserver", 514, enableConsole: true);

// Optimized for multi-threaded applications
log.Settings.EnableBackgroundQueue = true;     // Essential for high concurrency
log.Settings.BatchSize = 500;                  // Larger batches for efficiency
log.Settings.FlushInterval = 2000;             // Frequent flushing under load
log.Settings.MaxMemoryEntries = 50000;         // Higher memory limit
log.Settings.EnableColors = true;              // Safe - race conditions prevented

// Thread-safe server switching (safe during active logging)
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

## 🔄 Migration from v1.x

### Breaking Changes
- Constructors have changed - update your initialization code
- Some method signatures now include `CancellationToken` parameters
- Settings properties now have validation (may throw exceptions on invalid values)

### New Recommended Patterns
```csharp
// Old v1.x style
LoggingModule log = new LoggingModule("server", 514);
log.Log(Severity.Info, "Message");

// New v2.x style (both work, async preferred)
LoggingModule log = new LoggingModule("server", 514);
await log.InfoAsync("Message");                    // ✅ Preferred
log.Info("Message");                                // ✅ Still works

// New structured logging
await log.BeginStructuredLog(Severity.Info, "User action")
    .WithProperty("Action", "Login")
    .WithProperty("UserId", userId)
    .WriteAsync();
```

## 🏗️ Architecture

- **Thread-Safe**: All operations are thread-safe with comprehensive locking and race condition prevention
  - Console color operations properly isolated to prevent bleeding between threads
  - UDP client management handles concurrent server configuration changes
  - Background queue operations optimized for high-concurrency scenarios
  - Settings modifications safe during concurrent logging operations
- **Memory Efficient**: Configurable memory limits with pressure handling and leak prevention
- **I/O Optimized**: Background batching reduces I/O operations with intelligent queue management
- **Fault Tolerant**: Persistent queues survive crashes and restarts with corruption recovery
- **Standards Compliant**: RFC 3164 syslog format support with proper message formatting
- **Cross-Platform**: Works on Windows, Linux, and macOS with platform-specific optimizations

## 📚 Documentation

- [Developer Guide (CLAUDE.md)](./CLAUDE.md) - Comprehensive development documentation
- [API Reference](./src/LoggingModule/LoggingModule.xml) - Generated XML documentation
- [Performance Benchmarks](./docs/performance.md) - Detailed performance analysis
- [Integration Examples](./src/Test/Program.cs) - Comprehensive test suite with examples

## 🧪 Testing

Run the comprehensive test suite:

```bash
cd src/Test
dotnet run
```

The test program validates **every** library capability including:
- ✅ All constructor patterns and validation
- ✅ Sync and async logging methods
- ✅ Structured logging with properties and correlation IDs
- ✅ Background processing and persistent queuing
- ✅ **Comprehensive thread safety** under extreme concurrent load
- ✅ Multiple destination delivery (syslog + console + file)
- ✅ Persistent queue recovery across restarts
- ✅ Error handling and edge cases
- ✅ Performance benchmarks (>100K msg/sec)
- ✅ SyslogServer integration and end-to-end testing

### Advanced Thread Safety Testing

The test suite includes **20+ specialized thread safety tests** covering:
- **Console color race condition prevention** (50 concurrent threads)
- **UDP client management** during server configuration changes
- **Background queue operations** under extreme load (20 threads × 100 messages)
- **Settings modification race conditions** (concurrent property changes)
- **Disposal safety** with active operations across multiple threads
- **Exception handling** thread safety (15 threads × 20 exceptions)
- **File handle management** with concurrent file access
- **Memory pressure testing** under concurrent load
- **Mixed sync/async operations** validation
- **Resource contention simulation** with large message payloads

## 🤝 Help or Feedback

Found a bug or have a feature request? [File an issue](https://github.com/jchristn/LoggingModule/issues) - we'd love to hear from you!

## 🙏 Special Thanks

We'd like to extend a special thank you to those that have helped make this library better:
@dev-jan @jisotalo

## 📜 Version History

Please refer to [CHANGELOG.md](./CHANGELOG.md) for detailed version history.

---

⭐ **Star this repo** if SyslogLogging has helped your project!