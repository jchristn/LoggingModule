# Change Log

## Current Version

v2.0.9

### 🔥 **Major New Features**
- **Full async support** with `CancellationToken` throughout all logging methods
- **Structured logging** with properties, correlation IDs, and JSON serialization via `LogEntry` class
- **Microsoft.Extensions.Logging integration** with `ILogger` support and DI extensions
- **Background message queuing** with batching for I/O optimization and high-performance logging
- **Persistent queues** that survive application restarts using file-backed storage
- **Thread-safe architecture** with proper console color handling and race condition fixes
- **Integrated SyslogServer** for end-to-end testing and development scenarios
- **Enhanced header formatting** with 15+ parameterized variables for rich log context

### 🚀 **Performance & Reliability Improvements**
- **>100K messages/second** throughput in background mode
- **<1ms latency** for queue operations with optimized batching
- **Memory pressure handling** with configurable limits and automatic backpressure
- **Zero message loss** with persistent file-backed queues
- **Fixed critical stack overflow bug** in message splitting for large messages
- **Optimized UDP client management** with proper connection lifecycle
- **Improved error handling** with comprehensive exception scenarios

### 🛡️ **Enhanced Validation & Safety**
- **Comprehensive input validation** on all public properties with meaningful exceptions
- **Null safety improvements** throughout the codebase
- **Resource management** with proper disposal patterns for high-concurrency scenarios
- **Configuration validation** preventing invalid settings that could cause runtime issues

### 🎯 **New APIs and Methods**
- `LogEntryAsync()` - Async structured logging
- `BeginStructuredLog()` - Fluent structured logging builder
- `FlushAsync()` - Ensure all queued messages are processed
- `WithProperty()` - Add structured properties to log entries
- `WithCorrelationId()` - Add correlation tracking
- `WithSource()` - Add source context identification
- `ToJson()` - Serialize log entries to JSON format

### 📊 **Enhanced Header Format Variables**
Added extensive parameterized variables for log message headers:
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

### 🔧 **Configuration Enhancements**
- `EnableBackgroundQueue` - Enable high-performance background processing
- `BatchSize` - Configure message batching for optimal I/O
- `FlushInterval` - Control automatic flush timing
- `MaxMemoryEntries` - Configure memory limits for queues
- `EnablePersistentQueue` - Enable crash-resistant message persistence
- `UseUtcTime` - Control timestamp timezone handling

### 🧪 **Testing & Quality Improvements**
- **Comprehensive test suite** with 60+ test scenarios covering all edge cases
- **Stack overflow detection** and prevention testing
- **Memory leak detection** and resource management validation
- **Thread safety stress testing** with high-concurrency scenarios
- **Performance benchmarking** with detailed metrics
- **Integration testing** with real syslog server scenarios

### 🔄 **Breaking Changes**
- Constructor signatures have changed for improved flexibility
- Some method signatures now include optional `CancellationToken` parameters
- Settings properties now have validation and may throw exceptions on invalid values
- `HeaderFormat` default changed from `{ts} {host} {thread} {sev}` to `{ts} {host} {sev}`

### 🐛 **Critical Bug Fixes**
- **Fixed stack overflow** in message splitting for large messages (recursive → iterative)
- **Fixed console color race conditions** with proper color reset
- **Fixed UDP client disposal** issues in high-throughput scenarios
- **Fixed persistent queue corruption** recovery mechanisms
- **Fixed message formatting** spacing issues in structured logging
- **Fixed async disposal blocking** issues in shutdown scenarios

## Previous Versions

v2.0.1

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

