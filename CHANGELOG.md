# Change Log

## Current Version

v2.0.x

- New features
  - **Full async support** with `CancellationToken` throughout all logging methods
  - **Structured logging** with properties, correlation IDs, and JSON serialization via `LogEntry` class
  - **Microsoft.Extensions.Logging integration** with `ILogger` support and DI extensions
  - **Thread-safe architecture** with proper console color handling and race condition fixes
  - **Integrated SyslogServer** for end-to-end testing and development scenarios
  - **Enhanced header formatting** with 15+ parameterized variables for rich log context
- Performance reliability improvements
- Bugfixes and stability
- Additional testing
- New APIs and methods
  - `LogEntryAsync()` - Async structured logging
  - `BeginStructuredLog()` - Fluent structured logging builder
  - `WithProperty()` - Add structured properties to log entries
  - `WithCorrelationId()` - Add correlation tracking
  - `WithSource()` - Add source context identification
  - `ToJson()` - Serialize log entries to JSON format
- Enhanced header format variables including
  - `{ts}` - Timestamp (customizable format)
  - `{host}` - Machine/hostname
  - `{thread}` - Thread ID
  - `{sev}` - Severity level name
  - `{level}` - Severity level number (0-7)
  - `{pid}` - Process ID
  - `{user}` - Current username
  - `{app}` - Application/process name
  - `{domain}` - Application domain name
  - `{cpu}` - CPU core count
  - `{mem}` - Memory usage in MB
  - `{uptime}` - Process uptime (HH:mm:ss)
  - `{correlation}` - Correlation ID from log entry
  - `{source}` - Log source from log entry

## Previous Versions

v1.3.2

- EnableColors property for console logging
- Expose ```Log``` API (thank you @dev-jan!)

v1.3.1

- Bugfix for file logging

v1.3.0

- Breaking changes
- Enumerations now part of the namespace and not nested under LoggingModule class
- Logging to file
- Configurable maximum message length
- Configuration exception logging severity (default: alert)

v1.2.1

- XML documentation

v1.1.x

- Simplified constructors
- Simplified methods
- Added IDisposable support
- Cleanup and fixes, minor refactoring
 
v1.0.x

- Initial release
- Bugfixes and stability

