<img src="assets/logo.png" alt="SyslogLogging Logo" width="128" height="128" />

# SyslogLogging

[![NuGet Version](https://img.shields.io/nuget/v/SyslogLogging.svg?style=flat)](https://www.nuget.org/packages/SyslogLogging/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SyslogLogging.svg)](https://www.nuget.org/packages/SyslogLogging/)

SyslogLogging is a C# logging library for syslog, console, and file destinations. It supports synchronous and asynchronous logging, structured log entries, `Microsoft.Extensions.Logging` integration, file retention management, built-in OpenTelemetry-compatible metrics and traces, and Native AOT and trimmed applications.

Current release: `2.4.0`

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
- Built-in metrics and traces on a `SyslogLogging` `Meter`/`ActivitySource` (no exporter dependency), plus `{trace}`/`{span}` header tokens for log-to-trace correlation
- Native AOT and trimming compatible on `.NET 8.0` and `.NET 10.0`, with zero trim or AOT warnings
- Shared Touchstone test coverage exposed through CLI, xUnit, and NUnit runners

## What's New in 2.4.0

- **Native AOT and trimming support.** The `net8.0` and `net10.0` builds are marked `IsAotCompatible` and produce no trim or AOT warnings, so you can publish with `PublishAot` or `PublishTrimmed` without suppressions. See [Native AOT and Trimming](#native-aot-and-trimming).
- `LogEntry.ToJson()` no longer depends on reflection-based serialization. Its output in regular (JIT) applications is byte-identical to 2.3.x. Under Native AOT it writes the entry and all scalar property values natively, and previously it threw.
- New `LogEntry.ToJson(JsonSerializerOptions)` overload: pass a source-generated `JsonSerializerContext` to serialize complex property values in full under Native AOT, or set indentation, encoder, and converters.
- `NaN` and `Infinity` property values now serialize as strings instead of throwing, and a null `Properties` dictionary no longer throws.
- `SyslogServer` can be published as a native executable and no longer depends on `SerializationHelper` or `Microsoft.CSharp`.
- New `Test.Aot` native smoke test and a 25-case `Json` test suite.

### Previously in 2.3.2

- Dependency maintenance release: updated `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, and `System.Diagnostics.DiagnosticSource` to `10.0.12`, and `SerializationHelper` to `2.1.0` in the bundled `SyslogServer`. No public API changes — a drop-in upgrade from 2.3.1.

### Previously in 2.3.1

- Fixed `DisposeAsync()` so it stops the log retention timer. Previously only `Dispose()` did, so a module released with `await using` or `DisposeAsync()` left its retention timer running.

### Previously in 2.3.0

- Added built-in observability. A `System.Diagnostics.Metrics.Meter` and an `ActivitySource`, both named `SyslogLogging`, cover end-to-end and per-destination latency and outcome (console, file, each syslog server), syslog bytes sent, I/O lock wait, errors by component and `error.type`, `MessageLogged` handler time, retention cleanup runs, active modules, and build info. Nothing is emitted unless your host subscribes, and the library takes no exporter dependency. See [TELEMETRY.md](./TELEMETRY.md).
- `LogEntry` now captures the caller's W3C trace and span IDs (`TraceId`, `SpanId`). They are available as the `{trace}` and `{span}` header tokens and in `ToJson()`, so log lines link to traces even across the syslog boundary.
- Added `LoggingSettings.EnableMetrics` and `LoggingSettings.EnableTracing` (both default `true`).

### Previously in 2.2.2

- Dependency maintenance release: updated `System.Text.Json` and `Microsoft.Extensions.Logging.Abstractions` to `10.0.11` (and the bundled `SyslogServer` dependencies). No public API changes — a drop-in upgrade from 2.2.1.
- Added a shared Touchstone `Disposal` suite verifying that use-after-dispose throws `ObjectDisposedException` and that `Dispose`/`DisposeAsync` are idempotent.

### Previously in 2.2.1

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

### JSON

`LogEntry.ToJson()` returns compact JSON with the fields `timestamp`, `severity`, `message`, `threadId`, and, when set, `source`, `correlationId`, `traceId`, `spanId`, `exception` (`type`, `message`, `stackTrace`), and `properties`:

```json
{"timestamp":"2026-01-02T03:04:05.678Z","severity":"Error","message":"Payment processing failed","threadId":7,"correlationId":"abc-123","properties":{"OrderId":42,"Amount":19.95,"Currency":"USD"}}
```

Property values of common scalar types (string, bool, numbers, char, enums, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `Uri`, `Version`, `byte[]`) are always written natively. Enums are written as their numeric value, and `NaN`/`Infinity` as the strings `"NaN"`, `"Infinity"`, and `"-Infinity"`. How objects and collections are written depends on whether the application allows reflection-based serialization. See [Native AOT and Trimming](#native-aot-and-trimming).

To control serialization, pass `JsonSerializerOptions`:

```csharp
JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
options.Converters.Add(new JsonStringEnumConverter());
string json = entry.ToJson(options);
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
| `{level}` | Severity number | `1` |
| `{pid}` | Process ID | `1234` |
| `{user}` | Current username | `john.doe` |
| `{app}` | Application name | `MyWebApp` |
| `{correlation}` | Correlation ID | `abc-123-def` |
| `{source}` | Log source | `UserService` |
| `{trace}` | W3C trace ID of the caller's current `Activity` (empty if none) | `4bf92f3577b34da6a3ce929d0e0e4736` |
| `{span}` | W3C span ID of the caller's current `Activity` (empty if none) | `00f067aa0ba902b7` |

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

## Native AOT and Trimming

On `.NET 8.0` and `.NET 10.0`, SyslogLogging is AOT-compatible and trimmable. It has no trim or AOT analysis warnings, so it works in applications published with `<PublishAot>true</PublishAot>` or `<PublishTrimmed>true</PublishTrimmed>`. Everything works the same under Native AOT: console, file, and syslog logging, structured logging, `MessageLogged`, `Microsoft.Extensions.Logging`, header tokens, retention, and telemetry.

The one behavior that depends on the runtime is how `LogEntry.ToJson()` writes **complex property values** (objects, collections, dictionaries):

| Application | `JsonSerializer.IsReflectionEnabledByDefault` | Complex property values |
|-------------|-----------------------------------------------|-------------------------|
| Regular (JIT) | `true` | Serialized in full by reflection-based `System.Text.Json`, exactly as in 2.3.x |
| Native AOT or trimmed | `false` (SDK default) | Dictionaries become JSON objects and other enumerables JSON arrays (elements written by the same rules). Any other object is written as its `ToString()` value |

To serialize your own types in full under Native AOT, declare a source-generated context and pass it through `ToJson(JsonSerializerOptions)`:

```csharp
[JsonSerializable(typeof(Order))]
internal partial class AppJsonContext : JsonSerializerContext { }

JsonSerializerOptions options = new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default };

LogEntry entry = new LogEntry(Severity.Info, "Order placed").WithProperty("Order", order);
string json = entry.ToJson(options);   // "Order":{"Id":42,"Total":19.95,...}
```

Values that the context doesn't cover fall back to the `ToJson()` rules, so you only need to list your complex types. To combine several contexts, use `JsonTypeInfoResolver.Combine(...)`.

The repository includes `src/Test.Aot`, a smoke test that publishes as a native binary and checks the full feature set:

```bash
dotnet publish src/Test.Aot/Test.Aot.csproj -c Release -f net10.0 -r osx-arm64 -o ./aot-out
./aot-out/Test.Aot --require-native
```

Replace `osx-arm64` with your runtime identifier (for example `linux-x64` or `win-x64`). Native AOT publishing needs the platform's native toolchain: Xcode command line tools on macOS, clang on Linux, or the Visual Studio C++ workload on Windows.

## Telemetry

SyslogLogging emits metrics and traces through the .NET base class library: a `Meter` and an `ActivitySource`, both named `SyslogLogging`. It never references an exporter. Subscribe from your host and the data flows to Prometheus, Tempo, or any OTLP backend:

```csharp
// Radiant
settings.Sources.AddMeter(SyslogLoggingTelemetry.MeterName);
settings.Sources.AddActivitySource(SyslogLoggingTelemetry.ActivitySourceName);

// OpenTelemetry SDK
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(SyslogLoggingTelemetry.MeterName))
    .WithTracing(t => t.AddSource(SyslogLoggingTelemetry.ActivitySourceName));
```

What you get:

- `sysloglogging.entries` by severity and outcome (`success`, `degraded`, `failure`, `filtered`), and `sysloglogging.entry.duration`
- `sysloglogging.destination.writes` / `.duration` per destination and per syslog server, with `error.type` on failures
- `sysloglogging.syslog.sent` bytes, `sysloglogging.io_lock.wait.duration`, `sysloglogging.errors`, `sysloglogging.event_handler.duration`
- Retention job runs, duration, files deleted, and last-success time; active modules; build info
- Spans `sysloglogging write` with `console write`, `file write`, `syslog send` (Client), and `sysloglogging MessageLogged` children, nested under your request span. Retention runs get their own root span.

Turn telemetry off per module with `Settings.EnableMetrics = false` / `Settings.EnableTracing = false`. Add `{trace}` and `{span}` to `HeaderFormat` to correlate log lines with traces. [TELEMETRY.md](./TELEMETRY.md) has the full metric and span catalog, recommended PromQL alerts, and a Grafana dashboard map.

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

Run the Native AOT smoke test as a native binary (see [Native AOT and Trimming](#native-aot-and-trimming)), or under the JIT with AOT feature switches applied:

```bash
dotnet run --project src/Test.Aot/Test.Aot.csproj -f net10.0
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
- Telemetry emission (metrics and spans for every operation, failure paths, toggles, trace correlation, and the no-listener path) via an in-memory `MeterListener`/`ActivityListener`
- JSON serialization: the documented format, byte-for-byte parity with 2.3.x with reflection on and off, enums, non-finite numbers, escaping, depth limits, cycles, `ToJson(JsonSerializerOptions)`, and `ILogger` template arguments

## Related Project

The repository also includes `SyslogServer`, a simple utility application for receiving syslog traffic during development and testing. It reads its settings from `syslog.json` (created with defaults on first run) and can be published as a native executable:

```bash
dotnet publish src/SyslogServer/SyslogServer.csproj -c Release -f net10.0 -r osx-arm64 -p:NativeAot=true
```

## Version History

See [CHANGELOG.md](./CHANGELOG.md) for release details.

## Help

File issues or feature requests at:

https://github.com/jchristn/LoggingModule/issues
