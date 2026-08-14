<img src="assets/logo.png" alt="SyslogLogging Logo" width="128" height="128" />

# SyslogLogging

[![NuGet Version](https://img.shields.io/nuget/v/SyslogLogging.svg?style=flat)](https://www.nuget.org/packages/SyslogLogging/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SyslogLogging.svg)](https://www.nuget.org/packages/SyslogLogging/)

SyslogLogging is a C# logging library for syslog, console, and file destinations. It supports synchronous and asynchronous logging, structured log entries, `Microsoft.Extensions.Logging` integration, and file retention management.

Current release: `2.2.1`

Target builds:
- `.NET Standard 2.0`
- `.NET Standard 2.1`
- `.NET Framework 4.6.2`
- `.NET Framework 4.8`
- `.NET 8.0`
- `.NET 10.0`

## Highlights

- RFC 3164 syslog output
- Console and file logging in the same logger
- Structured logging with `LogEntry`
- Fluent structured logging builder
- `Microsoft.Extensions.Logging` provider and DI registration
- Configurable header format tokens including `{app}`, `{pid}`, `{source}`, and `{correlation}`
- Configurable exception severity
- Automatic retention cleanup for dated log files
- `MessageLogged` event for post-delivery notification of each emitted log entry
- Shared Touchstone test coverage exposed through CLI, xUnit, and NUnit runners

## What's New in 2.2.1

- Added the `MessageLogged` event, raised once for each emitted log entry after it has been written to every configured destination. Handlers receive the original, unsplit `LogEntry` even when the message was split for delivery, are invoked outside of any internal lock, and any handler exception is isolated and routed to `OnLoggingError` without interrupting logging.
- Expanded shared Touchstone coverage with positive and negative `MessageLogged` scenarios across the sync and async paths.

### Previously in 2.1.0

- Added `LoggingSettings.ApplicationName` so callers can explicitly control the `{app}` header token without changing the existing logging API.
- Changed `{app}` fallback resolution to use `Assembly.GetEntryAssembly()?.GetName().Name` before falling back to the current process name.
- Fixed `.Exception()` and `.ExceptionAsync()` so they honor `LoggingSettings.ExceptionSeverity`.
- Fixed concurrent async file logging so writes are serialized correctly under load.
- Migrated tests to Touchstone shared suites with CLI, xUnit, and NUnit runners on `net8.0` and `net10.0`.

## Installation

```bash
dotnet add package SyslogLogging
```

## Quick Start

### Simple Logging

```csharp
using SyslogLogging;

LoggingModule log = new LoggingModule();
await log.InfoAsync("Hello, world!");
```

### Syslog Logging

```csharp
using SyslogLogging;

LoggingModule log = new LoggingModule("mysyslogserver", 514);
await log.WarnAsync("Rate limit exceeded");
```

### File Logging

```csharp
using SyslogLogging;

LoggingModule log = new LoggingModule("./logs/app.log", FileLoggingMode.SingleLogFile);
await log.InfoAsync("File-only message");
```

## Structured Logging

### LogEntry

```csharp
LogEntry entry = new LogEntry(Severity.Error, "Payment processing failed")
    .WithProperty("OrderId", orderId)
    .WithProperty("Amount", amount)
    .WithProperty("Currency", "USD")
    .WithCorrelationId(correlationId)
    .WithSource("PaymentService")
    .WithException(exception);

await log.LogEntryAsync(entry);
```

### Fluent Builder

```csharp
await log.BeginStructuredLog(Severity.Info, "User login")
    .WithProperty("UserId", userId)
    .WithProperty("IpAddress", ipAddress)
    .WithCorrelationId(correlationId)
    .WriteAsync();
```

## Microsoft.Extensions.Logging Integration

```csharp
services.AddLogging(builder =>
{
    builder.AddSyslog("syslogserver", 514);
});
```

Multiple syslog targets are also supported:

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

## Message Notifications

Subscribe to `MessageLogged` to observe each log entry after it has been delivered to all configured destinations. The handler receives the original `LogEntry` even when a long message is split into multiple parts for delivery, so the event fires exactly once per logged entry:

```csharp
log.MessageLogged += entry =>
{
    metrics.Increment("logs." + entry.Severity);
    if (entry.Severity >= Severity.Error) alerting.Notify(entry);
};

await log.ErrorAsync("Payment gateway timeout");
```

Notes:

- The event is raised only for entries that pass `Settings.MinimumSeverity`; filtered messages do not fire it.
- Handlers are invoked outside of any internal lock, so a slow handler does not block other log writers, but it does run on the logging thread &mdash; keep handlers fast or hand off to your own queue.
- Exceptions thrown by a handler are isolated and routed to `OnLoggingError`; they never interrupt logging or reach the caller.

## Error Notifications

Subscribe to `OnLoggingError` to observe failures in the logging pipeline itself, such as file write or syslog delivery errors:

```csharp
log.OnLoggingError += ex => Console.Error.WriteLine(ex);
```

## Header Formatting

```csharp
log.Settings.HeaderFormat = "{ts} {host}[{pid}] {sev} [T:{thread}] [{app}]";
log.Settings.ApplicationName = "MyService";
log.Settings.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
log.Settings.UseUtcTime = true;
```

Available header variables:

| Variable | Description | Example |
|----------|-------------|---------|
| `{ts}` | Timestamp | `2024-01-15 14:30:25.123` |
| `{host}` | Machine name | `web-server-01` |
| `{thread}` | Thread ID | `12` |
| `{sev}` | Severity name | `Info` |
| `{level}` | Severity number | `6` |
| `{pid}` | Process ID | `1234` |
| `{user}` | Current username | `john.doe` |
| `{app}` | Application name | `MyWebApp` |
| `{domain}` | App domain | `MyWebApp.exe` |
| `{cpu}` | CPU core count | `8` |
| `{mem}` | Memory usage in MB | `256` |
| `{uptime}` | Process uptime | `02:45:30` |
| `{correlation}` | Correlation ID | `abc-123-def` |
| `{source}` | Log source | `UserService` |

`{app}` resolves in this order:

1. `log.Settings.ApplicationName`
2. `Assembly.GetEntryAssembly()?.GetName().Name`
3. Current process name

## File Retention

```csharp
LoggingModule log = new LoggingModule("./logs/app.log", FileLoggingMode.FileWithDate, true);
LoggingSettings settings = log.Settings;
settings.LogRetentionDays = 30;
log.Settings = settings;
```

Retention cleanup only applies when using `FileLoggingMode.FileWithDate`. The cleanup timer removes files matching the dated filename pattern when they are older than the configured retention period.

## Testing

Run the shared Touchstone suite through the CLI runner:

```bash
dotnet run --project src/Test.Automated/Test.Automated.csproj -f net10.0
dotnet run --project src/Test.Automated/Test.Automated.csproj -f net8.0
```

Run the same shared descriptors through xUnit and NUnit:

```bash
dotnet test src/Test.Xunit/Test.Xunit.csproj
dotnet test src/Test.Nunit/Test.Nunit.csproj
```

The shared suite covers:

- Constructor and settings validation
- Severity helpers
- Structured and fluent logging APIs
- Exception severity behavior
- File output and retention cleanup
- Message ordering and concurrency
- Syslog delivery and error handling
- `Microsoft.Extensions.Logging` integration

## Related Project

The repository also includes `SyslogServer`, a simple utility application for receiving syslog traffic during development and testing.

## Version History

See [CHANGELOG.md](./CHANGELOG.md) for release details.

## Help

File issues or feature requests at:

https://github.com/jchristn/LoggingModule/issues
