# Change Log

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
