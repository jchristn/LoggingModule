# Change Log

## v2.4.0

Native AOT and trimming support. This is a minor release: the only API addition is a new overload, and `ToJson()` output in regular (JIT) applications is byte-identical to 2.3.x.

- The `net8.0` and `net10.0` builds are marked `IsAotCompatible` (which also marks them trimmable). The trim, AOT, and single-file analyzers report no warnings, and the trim/AOT warning codes are now build errors so a regression fails the build. The `netstandard2.0`, `netstandard2.1`, `net462`, and `net48` builds are unchanged.
- `LogEntry.ToJson()` no longer uses reflection-based `System.Text.Json` for the entry itself. It writes with `Utf8JsonWriter`, so fixed fields and property values of common scalar types (string, bool, every numeric type, char, enums, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `Uri`, `Version`, `byte[]`) serialize without reflection in exactly the format `System.Text.Json` uses.
  - In regular applications, other property values (objects, collections, dictionaries) still go through reflection-based `System.Text.Json`, so output is unchanged. A new shared test suite compares the new writer byte-for-byte against a verbatim copy of the 2.3.x implementation.
  - In trimmed and Native AOT applications, where reflection-based serialization is disabled by default (`JsonSerializer.IsReflectionEnabledByDefault` is false), dictionaries are written as JSON objects, other enumerables as arrays, and any remaining object as its invariant-culture string. Previously `ToJson()` threw `InvalidOperationException` in those applications.
- Added `LogEntry.ToJson(JsonSerializerOptions options)`. Property values are serialized with the contract the options resolve, so a source-generated `JsonSerializerContext` serializes complex property values in full under Native AOT. The options also control indentation, the encoder, maximum depth, and converters. Values the resolver doesn't know are written as `ToJson()` would write them.
- Fixed: a `NaN`, `Infinity`, or `-Infinity` property value made `ToJson()` throw `ArgumentException`. It is now written as the string `"NaN"`, `"Infinity"`, or `"-Infinity"`.
- Fixed: `ToJson()` threw `NullReferenceException` when `LogEntry.Properties` had been set to null. A null dictionary is now treated as empty.
- Fixed `LoggingModule.csproj` copying `LoggingModule.xml` to the output twice, which made `dotnet publish` fail with `NETSDK1152` for projects that reference the library project directly. The NuGet package was not affected and still ships the XML documentation for every target.
- `SyslogServer` (2.4.0): now reads and writes `syslog.json` with `System.Text.Json` source generation and can be published as a native executable (`-p:NativeAot=true`). Removed the `SerializationHelper` and `Microsoft.CSharp` dependencies. Existing settings files load unchanged (property names are matched case-insensitively, and comments and trailing commas are allowed), and a newly created `syslog.json` is byte-identical to the one 2.3.2 wrote.
- Added `Test.Aot`, a Native AOT smoke test that runs as a native binary and covers JSON serialization, file, dated-file, syslog, and console logging, structured logging, events, exception severity, `Microsoft.Extensions.Logging`, metrics and traces, concurrency, and disposal. It treats every trim/AOT warning as an error.
- Added a shared Touchstone `Json` suite (25 cases) covering the documented format, 2.3.x parity in both modes, enums, non-finite numbers, key escaping, depth limiting, cycles, every `ToJson(options)` behavior, concurrency, `ILogger` template arguments, and the trimmable assembly marker. All 157 shared cases pass on net8.0 and net10.0 under the CLI, xUnit, and NUnit runners.

## v2.3.2

- Dependency maintenance release. Updated `System.Text.Json`, `Microsoft.Extensions.Logging.Abstractions`, and `System.Diagnostics.DiagnosticSource` (10.0.11 → 10.0.12) in the library, and `SerializationHelper` (2.0.3 → 2.1.0) plus `System.Text.Json` in the bundled `SyslogServer` (now versioned 2.3.2 to align with the library). No public API changes; this is a drop-in upgrade from 2.3.1.
- Refreshed the test toolchain: `Touchstone.*` (0.1.12 → 0.2.0), `NUnit` (4.6.1 → 5.0.0), `NUnit.Analyzers` (4.14.0 → 4.15.0), `NUnit3TestAdapter` (6.2.0 → 6.3.0), `Microsoft.NET.Test.Sdk` (18.9.0 → 18.10.1), `coverlet.collector` (10.0.1 → 10.1.0), and `Microsoft.Extensions.Logging` (10.0.11 → 10.0.12). All 132 shared cases pass on net8.0 and net10.0 under the CLI, xUnit, and NUnit runners.

## v2.3.1

- Fixed `DisposeAsync()`, which called `Dispose(false)` and so skipped stopping the log retention timer. It now releases the same resources as `Dispose()`. Modules disposed with `await using` or `DisposeAsync()` no longer leave a retention timer running.
- Added `Disposal` suite cases verifying that both `Dispose()` and `DisposeAsync()` stop the retention timer.

## v2.3.0

- Added built-in observability through the base class library: a `Meter` and an `ActivitySource`, both named `SyslogLogging` (constants on the new public `SyslogLoggingTelemetry` class). No exporter or SDK dependency, and near-zero cost when nothing subscribes. See `TELEMETRY.md`.
- Metrics: `sysloglogging.entries` (severity/mode/outcome), `sysloglogging.entry.duration`, `sysloglogging.entries.split`, `sysloglogging.io_lock.wait.duration`, `sysloglogging.destination.writes` and `sysloglogging.destination.duration` (per destination and per syslog server, `error.type` on failure), `sysloglogging.syslog.sent` (bytes), `sysloglogging.errors` (component/`error.type`), `sysloglogging.event_handler.duration`, `sysloglogging.retention.runs` / `.duration` / `.files_deleted` / `.last_success`, `sysloglogging.modules.active`, and `sysloglogging.build.info`.
- Spans: `sysloglogging write` with `console write`, `file write`, `syslog send` (Client), and `sysloglogging MessageLogged` children, and a root `sysloglogging retention` span per cleanup run. Explicit status, `exception` events, and no message payloads.
- `LogEntry` captures the current W3C trace and span IDs (`TraceId`, `SpanId`). New `{trace}` and `{span}` header tokens, and `traceId`/`spanId` in `ToJson()`.
- Added `LoggingSettings.EnableMetrics` and `LoggingSettings.EnableTracing` (default `true`).
- The retention timer no longer captures the constructing thread's execution context, so retention runs start their own trace.
- Added an explicit `System.Diagnostics.DiagnosticSource` 10.0.11 reference for non-`net10.0` targets. It was already a transitive dependency through `Microsoft.Extensions.Logging.Abstractions`.
- Added a shared Touchstone `Telemetry` suite (21 cases) that verifies emission for every operation and failure path with an in-memory listener.
- README: fixed the `{level}` example and removed the `{domain}`, `{cpu}`, `{mem}`, and `{uptime}` header tokens, which were documented but never implemented.

## v2.2.2

- Dependency maintenance release. Updated `System.Text.Json` (8.0.5 → 10.0.11) and `Microsoft.Extensions.Logging.Abstractions` (8.0.0 → 10.0.11) in the library, and `SerializationHelper` (2.0.1 → 2.0.3) plus `System.Text.Json` in the bundled `SyslogServer`. No public API changes; this is a drop-in upgrade from 2.2.1.
- Refreshed the test toolchain (`Microsoft.NET.Test.Sdk`, `coverlet.collector`, `xunit.runner.visualstudio`, `NUnit`, `NUnit3TestAdapter`, `NUnit.Analyzers`, and `Microsoft.Extensions.Logging`) to their current releases.
- Added a shared Touchstone `Disposal` suite covering object-lifetime behavior: use-after-`Dispose`/`DisposeAsync` throws `ObjectDisposedException` across the sync, async, and `LogEntry` paths, and `Dispose`/`DisposeAsync` are idempotent.

## v2.2.1

- Republished with the correct binaries. The 2.2.0 package was inadvertently packed from a pre-feature build and shipped without the `MessageLogged` event; use 2.2.1 or later. No source changes versus the intended 2.2.0 — the additions below ship in 2.2.1.
- Added the `LoggingModule.MessageLogged` event, raised once for each emitted log entry after it has been written to all configured destinations (console, file, and syslog). Handlers receive the original, unsplit `LogEntry` even when the message was split for delivery.
- Isolated `MessageLogged` handler exceptions so a throwing subscriber is routed to `OnLoggingError` and never interrupts logging; handlers are invoked outside of any internal lock.
- Expanded shared Touchstone coverage with positive and negative `MessageLogged` scenarios across sync and async paths, including split-message, minimum-severity, null/empty, multi-subscriber, concurrency, and handler-failure cases.

## v2.1.0

- Added `LoggingSettings.ApplicationName` so callers can override the `{app}` header token without materially changing the existing API surface.
- Changed `{app}` fallback resolution to use `Assembly.GetEntryAssembly()?.GetName().Name` before the current process name.
- Fixed `.Exception()` and `.ExceptionAsync()` so they respect `LoggingSettings.ExceptionSeverity`.
- Fixed concurrent async file logging so writes are serialized correctly under load.
- Migrated tests to shared Touchstone suites exposed through CLI, xUnit, and NUnit runners.
- Expanded automated coverage across validation, severity helpers, structured logging, file retention, concurrency, syslog delivery, and `Microsoft.Extensions.Logging` integration.

## v2.0.x

- Added async support with `CancellationToken` throughout logging methods.
- Added structured logging with properties, correlation IDs, and JSON serialization via `LogEntry`.
- Added `Microsoft.Extensions.Logging` integration with DI extensions.
- Improved thread safety and validation across logging paths.
- Added `SyslogServer` support for end-to-end development and testing scenarios.

## v1.3.2

- Added `EnableColors` for console logging.
- Exposed the `Log` API.

## v1.3.1

- Fixed file logging behavior.

## v1.3.0

- Moved enumerations into the namespace rather than nesting them under `LoggingModule`.
- Added file logging support.
- Added configurable maximum message length.
- Added configurable exception logging severity.

## v1.2.1

- Added XML documentation.

## v1.1.x

- Simplified constructors and methods.
- Added `IDisposable` support.
- Performed cleanup and minor refactoring.

## v1.0.x

- Initial release.
