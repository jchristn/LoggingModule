# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SyslogLogging is a C# class library for logging to syslog, console, and file systems. The project targets multiple .NET versions including .NET Standard 2.0/2.1, .NET Framework 4.62/4.8, and .NET 6.0/8.0.

## Architecture

### Core Components

- **LoggingModule.cs**: Main class implementing `IDisposable` and `IAsyncDisposable`. Provides sync/async API for logging with background processing, structured logging, and persistent queuing.
- **LogEntry.cs**: Structured log entry with properties, correlation IDs, and metadata support.
- **PersistentLogQueue.cs**: File-backed queue system for reliable log message delivery that survives application restarts.
- **LogProcessingService.cs**: Background service handling batched processing of log entries with per-target queues.
- **SyslogLoggerProvider.cs**: Microsoft.Extensions.Logging integration provider.
- **SyslogExtensions.cs**: Extension methods for DI integration and fluent structured logging.
- **SyslogServer.cs**: Represents a syslog server configuration (hostname, port).
- **LoggingSettings.cs**: Configuration class containing formatting options, minimum severity levels, file logging modes, and color schemes.
- **Severity.cs**: Enumeration defining log levels (Debug, Info, Warn, Error, Alert, etc.).
- **ColorScheme.cs**: Defines console color configuration for different severity levels.
- **FileLoggingMode.cs**: Enumeration for file logging options (None, SingleLogFile, FileWithDate).

### Project Structure

- **LoggingModule/**: Main library project containing the logging implementation
- **Test/**: Console application demonstrating usage patterns and serving as integration tests
- **assets/**: Contains logo files and branding assets

### Multi-targeting Support

The LoggingModule project targets:
- .NET Standard 2.0 and 2.1 (for broad compatibility)
- .NET Framework 4.62 and 4.8 (for legacy applications)
- .NET 6.0 and 8.0 (for modern applications)

## Key Features (v2.0.9+)

### Async Support
- All logging methods have async variants with `CancellationToken` support
- Background processing with persistent queuing for high-performance logging
- Non-blocking message ingestion with reliable delivery

### Structured Logging
- `LogEntry` class for structured log data with properties and metadata
- JSON serialization support for structured data
- Correlation ID and source context tracking
- Fluent builder pattern for structured logging

### Microsoft.Extensions.Logging Integration
- `SyslogLoggerProvider` for seamless integration
- Extension methods for DI container registration
- Support for structured logging patterns from Microsoft's logging framework

### Persistent Queuing
- File-backed queues that survive application restarts
- Per-target queues (console, file, each syslog server)
- Configurable memory limits and file rotation
- Batch processing for I/O optimization

### Thread Safety & Performance
- Thread-safe console color handling with proper color reset
- Optimized UDP client management with connection reuse
- Background processing to minimize caller blocking
- Configurable batch sizes for optimal throughput

## Development Commands

### Building
```bash
# Build entire solution
dotnet build SyslogLogging.sln

# Build specific configuration
dotnet build SyslogLogging.sln -c Release

# Build for specific framework
dotnet build LoggingModule/LoggingModule.csproj -f netstandard2.0
```

### Testing
```bash
# Run the test console application
dotnet run --project Test/Test.csproj

# Build and run test project for specific framework
dotnet run --project Test/Test.csproj -f net8.0
```

### Packaging
```bash
# Create NuGet package (already configured for automatic generation)
dotnet pack LoggingModule/LoggingModule.csproj

# Pack with specific configuration
dotnet pack LoggingModule/LoggingModule.csproj -c Release
```

## Key Usage Patterns

The library supports multiple initialization patterns:

1. **Default constructor**: Logs to localhost:514
2. **Single server**: Specify hostname and port
3. **Multiple servers**: Pass List<SyslogServer>
4. **File-only logging**: Provide filename in constructor
5. **Combined logging**: Configure syslog servers + console + file

### Configuration

LoggingModule.Settings provides extensive configuration:
- `MinimumSeverity`: Filter logs by severity level
- `FileLogging`: Set file logging mode (None/SingleLogFile/FileWithDate)
- `LogFilename`: Specify log file path
- `EnableColors`: Enable/disable console colors
- `HeaderFormat`: Customize log message format using variables like {ts}, {host}, {thread}, {sev}

## NuGet Package Configuration

The project is configured for automatic NuGet package generation with:
- Package ID: SyslogLogging
- Version: 2.0.8
- Multi-framework targeting
- Includes documentation XML, license, and logo assets
- Generates symbol packages (.snupkg) for debugging

## STRICT Code Style and Implementation Rules

**ALL CODE MUST FOLLOW THESE RULES WITHOUT EXCEPTION:**

### Code Organization
- **Namespace declaration at top, using statements INSIDE namespace block**
- **Microsoft/system library usings first (alphabetical), then other usings (alphabetical)**
- **One class or enum per file - no nesting**
- **Regions only for files over 500 lines: Public-Members, Private-Members, Constructors-and-Factories, Public-Methods, Private-Methods**

### Documentation
- **All public members, constructors, and methods MUST have XML documentation**
- **NO documentation on private members or methods**
- **Document default values, min/max values, and their effects**
- **Document exceptions with /// <exception> tags**
- **Document thread safety guarantees**
- **Document nullability in XML comments**

### Naming and Variables
- **Private member variables: underscore + PascalCase (_FooBar, not _fooBar)**
- **NO var keyword - use explicit types**
- **NO tuples unless absolutely necessary**

### Properties and Validation
- **Public members with explicit getters/setters using backing variables for validation**
- **Input validation with guard clauses at method start**
- **Use ArgumentNullException.ThrowIfNull() for .NET 6+ or manual null checks**
- **Validate ranges and null values in setters**

### Async and Threading
- **Every async method accepts CancellationToken (unless class has CancellationToken/CancellationTokenSource member)**
- **Use .ConfigureAwait(false) where appropriate**
- **Check cancellation at appropriate places**
- **Document thread safety in XML comments**
- **Use Interlocked for simple atomic operations**
- **Prefer ReaderWriterLockSlim over lock for read-heavy scenarios**

### Exception Handling
- **Use specific exception types, not generic Exception**
- **Include meaningful error messages with context**
- **Consider custom exception types for domain-specific errors**
- **Use exception filters when appropriate: catch (SqlException ex) when (ex.Number == 2601)**

### Resource Management
- **Implement IDisposable/IAsyncDisposable for unmanaged resources**
- **Use 'using' statements or declarations for IDisposable objects**
- **Follow full Dispose pattern with protected virtual void Dispose(bool disposing)**
- **Always call base.Dispose() in derived classes**

### Null Safety and Validation
- **Use nullable reference types (enable <Nullable>enable</Nullable>)**
- **Proactively eliminate null exception scenarios**
- **Consider Result pattern or Option/Maybe types for methods that can fail**

### LINQ and Collections
- **Prefer LINQ when readability not compromised**
- **Use .Any() instead of .Count() > 0**
- **Be aware of multiple enumeration - use .ToList() when needed**
- **Use .FirstOrDefault() with null checks rather than .First()**
- **Create async variants for methods returning IEnumerable (with CancellationToken)**

### Configuration and Constants
- **Avoid hardcoded constants - use configurable public members with backing privates**
- **Set reasonable defaults for configurable values**

### Library Code Restrictions
- **NO Console.WriteLine statements in library code**
- **Assume SQL string preparation is intentional (don't suggest ORMs)**

### Code Analysis
- **If README exists, analyze and ensure accuracy**
- **Compile code to ensure no errors or warnings**
- **Don't assume what class members/methods exist - ask for implementation details**

### Current Codebase Status
The existing code generally follows these patterns but may need updates for:
- Nullable reference types enablement
- Some async method patterns
- Exception handling specificity in some areas