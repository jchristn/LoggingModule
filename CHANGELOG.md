# Change Log

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
