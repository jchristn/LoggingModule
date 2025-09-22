# SyslogLogging Library - Developer Guide

## Overview
SyslogLogging is a comprehensive .NET logging library that provides high-performance, thread-safe logging to multiple destinations including syslog servers, console, and files. The library features async support, structured logging, Microsoft.Extensions.Logging integration, background queuing, and message batching.

## Code Style Rules (STRICTLY ENFORCED)

### 1. Namespace and Using Statements
- Namespace declaration MUST be at the top of the file (not within namespace block)
- Using statements MUST be inside the namespace block, grouped logically
- Standard framework usings come first, then third-party, then project-specific

```csharp
namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
}
```

### 2. Variable and Type Declarations
- NEVER use `var` - always specify explicit types
- Use underscore + PascalCase for private fields: `_MyField`
- Use PascalCase for public/protected members: `MyProperty`
- Use camelCase for local variables and parameters: `myVariable`

```csharp
// Correct
List<string> messages = new List<string>();
private readonly LoggingModule _Logger;

// Incorrect
var messages = new List<string>();
private readonly LoggingModule logger;
```

### 3. Async Patterns
- ALL async methods MUST accept `CancellationToken token = default` as the LAST parameter
- ALL async calls MUST use `ConfigureAwait(false)`
- Async method names MUST end with "Async"

```csharp
public async Task LogAsync(string message, CancellationToken token = default)
{
    await WriteToFileAsync(message, token).ConfigureAwait(false);
}
```

### 4. Exception Handling and Disposal
- ALL public APIs MUST call `ThrowIfDisposed()` at the beginning
- Use `ArgumentNullException.ThrowIfNull()` for null parameter checks on .NET 6+
- Implement both `IDisposable` and `IAsyncDisposable` for classes with resources

```csharp
public void Log(string message)
{
    ThrowIfDisposed();
    if (string.IsNullOrEmpty(message)) return;
    // Implementation
}
```

### 5. Documentation
- ALL public APIs MUST have XML documentation
- Include `<exception>` tags for all thrown exceptions
- Include `<param>` tags for all parameters
- Include meaningful `<summary>` descriptions

```csharp
/// <summary>
/// Logs a message asynchronously to all configured destinations.
/// </summary>
/// <param name="message">The message to log.</param>
/// <param name="token">Cancellation token.</param>
/// <exception cref="ObjectDisposedException">Thrown when the logger is disposed.</exception>
/// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
public async Task LogAsync(string message, CancellationToken token = default)
```

### 6. Threading and Thread Safety
- Use `lock` statements for thread synchronization
- Console color operations MUST use `Console.ResetColor()` to prevent race conditions
- Background processing MUST use proper cancellation token handling

```csharp
lock (_ConsoleLock)
{
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ResetColor(); // CRITICAL for thread safety
}
```

## Architecture Overview

### Core Components

#### 1. LoggingModule.cs
Main entry point for all logging operations. Features:
- Async/sync logging methods for all severity levels
- Multiple destination support (console, file, syslog)
- Thread-safe operations with proper disposal
- Background queue processing integration

#### 2. LogEntry.cs
Structured logging support with:
- Property-based metadata
- Correlation ID tracking
- JSON serialization
- Fluent builder pattern

#### 3. LogProcessingService.cs
Background processing service providing:
- Per-destination message queues
- Batch processing for I/O optimization
- Thread-safe console color handling
- Configurable batching and delays

#### 4. PersistentLogQueue.cs
File-backed message queue featuring:
- Persistence across application restarts
- Memory limit enforcement
- Automatic file rotation
- Corruption recovery

#### 5. Microsoft.Extensions.Logging Integration
- `SyslogLoggerProvider.cs` - ILoggerProvider implementation
- `SyslogExtensions.cs` - DI container extensions
- Full compatibility with .NET logging framework

#### 6. SyslogServer Integration
- Complete SyslogServer project integration
- UDP syslog message reception
- File-based logging with timestamp support
- Settings-based configuration

### Key Features

#### Async Support
All logging methods have async variants with proper cancellation token support:

```csharp
await logger.InfoAsync("Message", cancellationToken);
await logger.LogEntryAsync(entry, cancellationToken);
```

#### Structured Logging
Rich metadata support with fluent API:

```csharp
logger.BeginStructuredLog(Severity.Info, "User login")
    .WithProperty("UserId", userId)
    .WithProperty("IpAddress", ipAddress)
    .WithCorrelationId(correlationId)
    .Write();
```

#### Background Processing
Non-blocking logging with configurable batching:
- Separate queues per destination
- Batch size optimization
- Memory pressure handling
- Graceful shutdown support

#### Thread Safety
Comprehensive thread-safe operations:
- Console color race condition prevention
- Lock-free queue operations where possible
- Proper disposal in multi-threaded environments

## Development Commands

### Build
```bash
dotnet build
```

### Test
```bash
cd src/Test
dotnet run
```

### Package
```bash
dotnet pack src/LoggingModule/LoggingModule.csproj
```

### Clean
```bash
dotnet clean
```

## Project Structure

```
src/
├── LoggingModule/          # Main library
│   ├── LoggingModule.cs    # Core logging class
│   ├── LogEntry.cs         # Structured logging
│   ├── LogProcessingService.cs # Background processing
│   ├── PersistentLogQueue.cs   # Persistent queue
│   ├── SyslogLoggerProvider.cs # ILogger integration
│   ├── SyslogExtensions.cs     # DI extensions
│   └── [Other core files]
├── Test/                   # Comprehensive test suite
│   └── Program.cs          # All capability testing
└── SyslogServer/           # Integrated syslog server
    ├── SyslogServer.cs     # UDP syslog receiver
    └── Settings.cs         # Server configuration
```

## Testing Strategy

The test program validates ALL library capabilities:

1. **Constructor Testing** - All initialization patterns
2. **Basic Logging** - All severity levels, sync/async
3. **Structured Logging** - Properties, correlation IDs, JSON
4. **Fluent Logging** - Builder pattern validation
5. **Exception Logging** - Error capture and formatting
6. **Settings Validation** - Configuration edge cases
7. **Background Processing** - Queue and batch operations
8. **Thread Safety** - Concurrent access validation
9. **Multiple Destinations** - Syslog, console, file combinations
10. **Persistent Queuing** - Restart survival testing
11. **Error Handling** - Failure mode validation
12. **Disposal** - Resource cleanup verification
13. **Performance** - Throughput and latency metrics
14. **SyslogServer Integration** - End-to-end validation

Each test category includes both success and failure cases with clear pass/fail reporting.

## Performance Characteristics

- **Throughput**: >100K messages/second (background mode)
- **Latency**: <1ms for queue operations
- **Memory**: Configurable limits with pressure handling
- **Batching**: Optimized I/O operations
- **Threading**: Lock-free where possible

## Dependencies

- **.NET Standard 2.0+** / **.NET Framework 4.6.2+** / **.NET 6.0+**
- **Microsoft.Extensions.Logging.Abstractions** (8.0.0)
- **System.Text.Json** (8.0.5)
- **SerializationHelper** (2.0.1) - for SyslogServer

## Version History

- **v2.0.9** - Latest release with all new features
- Added async support with CancellationToken
- Implemented structured logging and semantic patterns
- Created ILogger implementation and Microsoft.Extensions.Logging integration
- Implemented persistent background queue with per-target queues
- Added message batching for I/O optimization
- Refactored LoggingModule to use new architecture
- Optimized UdpClient management
- Fixed console color race conditions
- Integrated SyslogServer project

## Best Practices

1. **Always use async methods** in async contexts
2. **Dispose properly** - use `using` statements or explicit disposal
3. **Configure background processing** for high-throughput scenarios
4. **Use structured logging** for searchable, analyzable logs
5. **Set appropriate batch sizes** based on your I/O characteristics
6. **Monitor queue depths** to detect backpressure
7. **Use correlation IDs** for request tracing
8. **Test failure scenarios** including network outages and disk full

## Common Patterns

### Basic Setup
```csharp
using SyslogLogging;

LoggingModule logger = new LoggingModule("localhost", 514, true);
await logger.InfoAsync("Application started");
```

### Structured Logging
```csharp
logger.BeginStructuredLog(Severity.Warning, "Rate limit exceeded")
    .WithProperty("RequestsPerSecond", rps)
    .WithProperty("ClientId", clientId)
    .WithCorrelationId(Request.Headers["X-Correlation-ID"])
    .WriteAsync();
```

### Microsoft.Extensions.Logging Integration
```csharp
services.AddLogging(builder =>
{
    builder.AddSyslog("localhost", 514, configure: module =>
    {
        module.Settings.EnableBackgroundQueue = true;
        module.Settings.BatchSize = 100;
    });
});
```

### Error Handling
```csharp
try
{
    await riskyOperation();
}
catch (Exception ex)
{
    await logger.ExceptionAsync("Operation failed", ex);
    throw;
}
```

## Troubleshooting

### Common Issues

1. **Console colors bleeding** - Fixed in v2.0.9 with proper Console.ResetColor()
2. **High memory usage** - Configure queue limits and batch sizes
3. **Slow performance** - Enable background processing
4. **Network timeouts** - Adjust UDP timeout settings
5. **File access denied** - Check permissions on log directory

### Debug Mode
Enable detailed diagnostics:
```csharp
logger.Settings.EnableDebug = true;
logger.Settings.EnableConsole = true;
```

This will show internal operations and queue states for troubleshooting.