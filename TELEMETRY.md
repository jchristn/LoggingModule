# SyslogLogging Telemetry

SyslogLogging measures itself and hands the numbers to whatever your application already runs. It emits metrics through a `System.Diagnostics.Metrics.Meter` and traces through a `System.Diagnostics.ActivitySource`, both named **`SyslogLogging`**, and stops there. It takes no dependency on OpenTelemetry, Radiant, or any exporter, and it never opens a connection to a telemetry backend. **The library emits; your host collects.** When nothing subscribes, each instrumentation point costs a few nanoseconds and allocates nothing.

This document is the contract for that telemetry: source names, every metric and span, how to subscribe, recommended alerts, and what a Grafana dashboard built on it should show.

- [Why a logging library emits telemetry](#why-a-logging-library-emits-telemetry)
- [Sources](#sources)
- [Subscribing](#subscribing)
- [Configuration](#configuration)
- [Metrics catalog](#metrics-catalog)
- [Spans catalog](#spans-catalog)
- [Log-to-trace correlation](#log-to-trace-correlation)
- [Recommended alerts (PromQL)](#recommended-alerts-promql)
- [Dashboard map](#dashboard-map)
- [Conventions and guarantees](#conventions-and-guarantees)

## Why a logging library emits telemetry

`LoggingModule` writes to the console, to a file, and to every configured syslog server **on the caller's thread, under a single I/O lock**. Every failure is swallowed and routed to the `OnLoggingError` event so that logging never throws into your application. That is the right behavior for a logger, but it means that without telemetry:

- a syslog server that stopped accepting datagrams, or a disk that filled up, fails **silently** unless someone subscribed to `OnLoggingError`;
- time spent formatting, waiting for the I/O lock, appending to a file, or sending UDP shows up only as unexplained latency in the caller's request;
- the background retention job that deletes old log files can stall or fail with no visible signal.

The instrumentation below answers *where the time went* (per destination, lock wait, event handlers) and *what failed* (destination, server, `error.type`) from dashboards and traces alone.

## Sources

| Kind | Name | Constant |
| --- | --- | --- |
| Meter | `SyslogLogging` | `SyslogLoggingTelemetry.MeterName` |
| ActivitySource | `SyslogLogging` | `SyslogLoggingTelemetry.ActivitySourceName` |

Both carry the library version (for example `2.3.0`). Every metric, span, and attribute name is a public constant on `SyslogLogging.SyslogLoggingTelemetry`. These names are a public contract: dashboards and alerts depend on them, so they won't change within a major version.

## Subscribing

### Radiant

```csharp
using Radiant;
using SyslogLogging;

RadiantSettings settings = new RadiantSettings("orders-api");
settings.Otlp.Endpoint = "http://127.0.0.1:4317";
settings.Prometheus.Enable = true;
settings.Sources.AddMeter(SyslogLoggingTelemetry.MeterName);
settings.Sources.AddActivitySource(SyslogLoggingTelemetry.ActivitySourceName);

using (RadiantHost host = RadiantHost.Start(settings))
{
    // ... run the application ...
}
```

### OpenTelemetry SDK

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SyslogLogging;

using MeterProvider meters = Sdk.CreateMeterProviderBuilder()
    .AddMeter(SyslogLoggingTelemetry.MeterName)
    .AddOtlpExporter()
    .Build();

using TracerProvider tracer = Sdk.CreateTracerProviderBuilder()
    .AddSource(SyslogLoggingTelemetry.ActivitySourceName)
    .AddOtlpExporter()
    .Build();
```

In ASP.NET Core, use `services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("SyslogLogging")).WithTracing(t => t.AddSource("SyslogLogging"))`.

### Ad hoc

```bash
dotnet-counters monitor --process-id <pid> --counters SyslogLogging
```

### Bucket boundaries

Duration histograms publish an `InstrumentAdvice` with boundaries suited to a logging call (10 µs to 10 s): `0.00001, 0.00005, 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10` seconds. OpenTelemetry .NET 1.10+ honors the advice automatically. With an older SDK, configure a view with the same boundaries. Otherwise the default buckets, which are built for milliseconds, will put nearly every logging call in the first bucket.

### Runtime metrics

The library doesn't emit process or runtime metrics. That's the host's job (Radiant's built-in runtime metrics, or `OpenTelemetry.Instrumentation.Runtime`).

## Configuration

| Setting | Default | Effect |
| --- | --- | --- |
| `LoggingSettings.EnableMetrics` | `true` | When `false`, this module records no per-entry, destination, lock, handler, error, or retention metrics, even if the host subscribes. Lifecycle (`modules.active`) and `build.info` are process-level and always available. |
| `LoggingSettings.EnableTracing` | `true` | When `false`, this module creates no spans. Use it to keep metrics while suppressing per-entry spans on very high-volume loggers. |
| `LoggingSettings.HeaderFormat` tokens `{trace}` / `{span}` | not in the default format | Stamp the caller's W3C trace and span IDs into each log line. See [Log-to-trace correlation](#log-to-trace-correlation). |

No telemetry is produced unless a host subscribes to the names above. There are no environment variables or endpoints to configure in the library itself. Export endpoints, sampling, and resource attributes belong to the host's Radiant or OpenTelemetry configuration.

## Metrics catalog

Instrument names are dotted. A Prometheus exporter rewrites them to snake case and appends unit and type suffixes (`_seconds`, `_bytes`, `_total`, `_bucket`). Attribute keys likewise become `sysloglogging_outcome`, `server_address`, `error_type`, and so on.

| Instrument | Type | Unit | Attributes | Prometheus series | Description |
| --- | --- | --- | --- | --- | --- |
| `sysloglogging.entries` | Counter | `{entry}` | `sysloglogging.severity`, `sysloglogging.mode`, `sysloglogging.outcome` | `sysloglogging_entries_total` | Every log entry submitted to a `LoggingModule`. Outcome is `success` (all destinations written), `degraded` (at least one destination failed), `failure` (the pipeline itself threw), or `filtered` (below `MinimumSeverity`). Null or empty messages are ignored and not counted. |
| `sysloglogging.entry.duration` | Histogram | `s` | `sysloglogging.mode`, `sysloglogging.outcome` | `sysloglogging_entry_duration_seconds_*` | End-to-end time to deliver one entry to every destination, including lock waits, all parts of a split message, and `MessageLogged` handlers. Not recorded for `filtered`. |
| `sysloglogging.entries.split` | Counter | `{entry}` | `sysloglogging.mode` | `sysloglogging_entries_split_total` | Entries longer than `MaxMessageLength` that were split into multiple parts. |
| `sysloglogging.io_lock.wait.duration` | Histogram | `s` | `sysloglogging.mode` | `sysloglogging_io_lock_wait_duration_seconds_*` | Time spent waiting to acquire the module's I/O lock, the single concurrency slot that serializes console/file/syslog delivery (the "queued" stage). |
| `sysloglogging.destination.writes` | Counter | `{write}` | `sysloglogging.destination`, `sysloglogging.outcome`, `server.address`\*, `server.port`\*, `error.type`\*\* | `sysloglogging_destination_writes_total` | One write of one message part to one destination. Outcome is `success` or `failure`. |
| `sysloglogging.destination.duration` | Histogram | `s` | `sysloglogging.destination`, `sysloglogging.outcome`, `server.address`\*, `server.port`\* | `sysloglogging_destination_duration_seconds_*` | Time for one destination write. For syslog this includes UDP client creation, name resolution, formatting, and send. |
| `sysloglogging.syslog.sent` | Counter | `By` | `server.address`, `server.port` | `sysloglogging_syslog_sent_bytes_total` | UDP payload bytes sent to each syslog server. |
| `sysloglogging.errors` | Counter | `{error}` | `sysloglogging.component`, `error.type` | `sysloglogging_errors_total` | Errors routed to `OnLoggingError`. Component is `console`, `file`, `syslog`, `pipeline`, `event_handler`, or `retention`. `error.type` is the full .NET type name of the underlying exception. |
| `sysloglogging.event_handler.duration` | Histogram | `s` | `sysloglogging.event`, `sysloglogging.outcome` | `sysloglogging_event_handler_duration_seconds_*` | Time spent inside `MessageLogged` subscribers (they run on the logging thread). |
| `sysloglogging.retention.runs` | Counter | `{run}` | `sysloglogging.outcome` | `sysloglogging_retention_runs_total` | Retention cleanup runs (every minute when `LogRetentionDays > 0` with `FileWithDate`). `failure` if enumerating or deleting any file failed. |
| `sysloglogging.retention.duration` | Histogram | `s` | `sysloglogging.outcome` | `sysloglogging_retention_duration_seconds_*` | Retention run time. |
| `sysloglogging.retention.files_deleted` | Counter | `{file}` | none | `sysloglogging_retention_files_deleted_total` | Expired dated log files deleted. |
| `sysloglogging.retention.last_success` | Observable gauge | `s` | none | `sysloglogging_retention_last_success_seconds` | Unix time of the most recent successful retention run in this process. Absent until the first success. |
| `sysloglogging.modules.active` | UpDownCounter | `{module}` | none | `sysloglogging_modules_active` | `LoggingModule` instances constructed and not yet disposed. Steady growth indicates a leak. |
| `sysloglogging.build.info` | Observable gauge | none | `sysloglogging.version` | `sysloglogging_build_info` | Always `1`, labeled with the library version. |

\* syslog destination only. \*\* failures only.

### Attribute values (all bounded)

| Attribute | Values |
| --- | --- |
| `sysloglogging.severity` | `Debug`, `Info`, `Warn`, `Error`, `Alert`, `Critical`, `Emergency` |
| `sysloglogging.mode` | `sync`, `async` |
| `sysloglogging.outcome` | `success`, `degraded`, `failure`, `filtered` (entries); `success`, `failure` (everything else) |
| `sysloglogging.destination` | `console`, `file`, `syslog` |
| `sysloglogging.component` | `console`, `file`, `syslog`, `pipeline`, `event_handler`, `retention` |
| `sysloglogging.event` | `MessageLogged` |
| `server.address`, `server.port` | The configured syslog servers. Bounded by your configuration, not by traffic. |
| `error.type` | .NET exception type names (for example `System.Net.Sockets.SocketException`, `System.IO.IOException`) |

Message text, properties, correlation IDs, file paths, and exception messages **never** appear on metrics.

## Spans catalog

Spans are created only when a listener subscribes to the `SyslogLogging` source and `EnableTracing` is `true`. Each one nests under `Activity.Current`. In ASP.NET Core or Watson, that's the inbound request span, so a slow request's waterfall shows exactly how long logging took and which destination took it.

| Span name | Kind | Parent | Attributes | Status |
| --- | --- | --- | --- | --- |
| `sysloglogging write` | Internal | caller's current Activity | `sysloglogging.severity`, `sysloglogging.mode`, `sysloglogging.parts`, `sysloglogging.source` (logger category, when set), `sysloglogging.outcome`, `error.type` (on failure) | `Ok` on success. `Error` on `degraded` (description `degraded`) or `failure` (description is the exception type, plus an `exception` event). |
| `console write` | Internal | `sysloglogging write` | `sysloglogging.destination=console`, `sysloglogging.part`, `sysloglogging.outcome` | `Ok` / `Error` plus `exception` event |
| `file write` | Internal | `sysloglogging write` | `sysloglogging.destination=file`, `sysloglogging.part`, `sysloglogging.outcome` | `Ok` / `Error` plus `exception` event |
| `syslog send` | Client | `sysloglogging write` | `sysloglogging.destination=syslog`, `sysloglogging.part`, `server.address`, `server.port`, `network.transport=udp`, `sysloglogging.outcome`, `error.type` | `Ok` / `Error` plus `exception` event |
| `sysloglogging MessageLogged` | Internal | `sysloglogging write` | `sysloglogging.event=MessageLogged`, `sysloglogging.outcome` | `Ok` / `Error` plus `exception` event |
| `sysloglogging retention` | Internal | **root** (execution context is not flowed into the timer) | `sysloglogging.retention.files_deleted`, `sysloglogging.outcome` | `Ok` / `Error` plus `exception` event |

A message split into *n* parts produces one destination span per part per destination, numbered by `sysloglogging.part`. The `exception` event carries `exception.type` and `exception.message` (no stack trace). Span attributes never include message text or structured properties.

On high-volume loggers, sample at the host (for example a parent-based ratio sampler), or set `EnableTracing = false` on that module and rely on the metrics.

## Log-to-trace correlation

Every `LogEntry` captures the W3C trace and span IDs of `Activity.Current` at construction (`LogEntry.TraceId`, `LogEntry.SpanId`; `null` when no Activity is current). They're used in three places:

- **Header tokens.** Add `{trace}` and `{span}` to `HeaderFormat`, for example `"{ts} {host} {sev} trace={trace} span={span}"`. Every console line, file line, and syslog datagram then carries the IDs. This is how trace context crosses the syslog boundary: RFC 3164 has no header for `traceparent`, so the IDs travel in the message text. Loki's derived fields, or any syslog-ingesting log store, can then link each line to Tempo.
- **JSON.** `LogEntry.ToJson()` includes `traceId` and `spanId` when present.
- **Split messages.** Every part carries the original entry's IDs.

## Recommended alerts (PromQL)

```yaml
groups:
  - name: sysloglogging
    rules:
      - alert: SyslogDeliveryFailing
        expr: sum by (server_address, server_port) (rate(sysloglogging_destination_writes_total{sysloglogging_destination="syslog", sysloglogging_outcome="failure"}[5m])) > 0
        for: 5m
        annotations:
          summary: "Syslog sends to {{ $labels.server_address }}:{{ $labels.server_port }} are failing"

      - alert: LogFileWritesFailing
        expr: sum by (error_type) (rate(sysloglogging_destination_writes_total{sysloglogging_destination="file", sysloglogging_outcome="failure"}[5m])) > 0
        for: 5m
        annotations:
          summary: "Log file appends failing ({{ $labels.error_type }}); check disk space and permissions"

      - alert: LoggingDegradedRatioHigh
        expr: |
          sum(rate(sysloglogging_entries_total{sysloglogging_outcome=~"degraded|failure"}[5m]))
            / clamp_min(sum(rate(sysloglogging_entries_total{sysloglogging_outcome!="filtered"}[5m])), 1e-9) > 0.05
        for: 10m
        annotations:
          summary: "More than 5% of log entries are not reaching every destination"

      - alert: LoggingLatencyHigh
        expr: histogram_quantile(0.99, sum by (le) (rate(sysloglogging_entry_duration_seconds_bucket[5m]))) > 0.05
        for: 10m
        annotations:
          summary: "p99 logging call latency above 50 ms; logging is adding latency to callers"

      - alert: LoggingLockContention
        expr: histogram_quantile(0.99, sum by (le) (rate(sysloglogging_io_lock_wait_duration_seconds_bucket[5m]))) > 0.01
        for: 10m
        annotations:
          summary: "p99 wait for the SyslogLogging I/O lock above 10 ms"

      - alert: MessageLoggedHandlerSlow
        expr: histogram_quantile(0.99, sum by (le) (rate(sysloglogging_event_handler_duration_seconds_bucket[5m]))) > 0.01
        for: 10m
        annotations:
          summary: "MessageLogged subscribers are slow and run on the logging thread"

      - alert: LogRetentionFailing
        expr: increase(sysloglogging_retention_runs_total{sysloglogging_outcome="failure"}[15m]) > 0
        annotations:
          summary: "Log retention cleanup is failing; old log files are not being deleted"

      - alert: LogRetentionStale
        expr: time() - sysloglogging_retention_last_success_seconds > 900
        annotations:
          summary: "No successful log retention run in 15 minutes (runs every minute)"

      - alert: CriticalLogRate
        expr: sum(rate(sysloglogging_entries_total{sysloglogging_severity=~"Critical|Emergency", sysloglogging_outcome!="filtered"}[5m])) > 0
        for: 2m
        annotations:
          summary: "Application is emitting Critical/Emergency log entries"

      - alert: LoggingModuleLeak
        expr: deriv(sysloglogging_modules_active[30m]) > 0 and sysloglogging_modules_active > 50
        for: 30m
        annotations:
          summary: "LoggingModule instances are being created and never disposed"
```

Thresholds are starting points. Tune them to your traffic.

## Dashboard map

SyslogLogging is a library, so it ships no Grafana stack or dashboards of its own. The host application owns `compose.yaml`, Prometheus, Tempo, and the dashboard folder. A host that uses SyslogLogging should add a **Logging** dashboard to its product folder. Every query below uses the real emitted series names.

| Row | Panel | Query |
| --- | --- | --- |
| Overview | Entries/s by outcome | `sum by (sysloglogging_outcome) (rate(sysloglogging_entries_total[$__rate_interval]))` |
| Overview | Entries/s by severity | `sum by (sysloglogging_severity) (rate(sysloglogging_entries_total{sysloglogging_outcome!="filtered"}[$__rate_interval]))` |
| Overview | Degraded ratio | the `LoggingDegradedRatioHigh` expression without the threshold |
| Latency | Entry p50/p95/p99 by mode | `histogram_quantile(0.95, sum by (le, sysloglogging_mode) (rate(sysloglogging_entry_duration_seconds_bucket[$__rate_interval])))` |
| Latency | Destination p95 | `histogram_quantile(0.95, sum by (le, sysloglogging_destination) (rate(sysloglogging_destination_duration_seconds_bucket[$__rate_interval])))` |
| Latency | I/O lock wait p95/p99 | `histogram_quantile(0.99, sum by (le) (rate(sysloglogging_io_lock_wait_duration_seconds_bucket[$__rate_interval])))` |
| Latency | MessageLogged handler p95 | `histogram_quantile(0.95, sum by (le) (rate(sysloglogging_event_handler_duration_seconds_bucket[$__rate_interval])))` |
| Syslog (integration) | Sends/s and failures/s by server | `sum by (server_address, server_port, sysloglogging_outcome) (rate(sysloglogging_destination_writes_total{sysloglogging_destination="syslog"}[$__rate_interval]))` |
| Syslog (integration) | p95 by server | `histogram_quantile(0.95, sum by (le, server_address, server_port) (rate(sysloglogging_destination_duration_seconds_bucket{sysloglogging_destination="syslog"}[$__rate_interval])))` |
| Syslog (integration) | Bytes/s by server | `sum by (server_address, server_port) (rate(sysloglogging_syslog_sent_bytes_total[$__rate_interval]))` |
| Errors | Errors/s by component and type | `sum by (sysloglogging_component, error_type) (rate(sysloglogging_errors_total[$__rate_interval]))` |
| Retention | Runs by outcome, files deleted, time since last success | `sysloglogging_retention_runs_total`, `sysloglogging_retention_files_deleted_total`, `time() - sysloglogging_retention_last_success_seconds` |
| Lifecycle | Active modules, version | `sysloglogging_modules_active`, `sysloglogging_build_info` |

For traces, search Tempo for `{ resource.service.name = "<your service>" && name = "syslog send" && status = error }`, or open any slow request and expand its `sysloglogging write` children.

## Conventions and guarantees

- **Best-effort.** Every recording call is wrapped so that a telemetry failure never affects logging, and logging behavior (including `OnLoggingError` and `MessageLogged`) is the same with or without a listener.
- **Near-zero cost when unobserved.** Instruments check `Enabled` before building tags, and spans aren't created unless the source has listeners.
- **Bounded cardinality.** Only the enumerations above appear as metric attributes. `server.address`/`server.port` are bounded by configuration.
- **No secrets, PII, or payloads.** Message text, structured properties, correlation IDs, file paths, and usernames are never recorded on metrics or spans.
- **No in-process quantiles.** Durations are histograms in seconds (UCUM `s`). Derive percentiles in PromQL.
- **Process scope.** Instruments are static and shared by every `LoggingModule` in the process. Per-module breakdowns come from the bounded attributes (destination, server), not from module identity.
- **Dependency.** The library uses `System.Diagnostics.DiagnosticSource` (in-box on .NET 10, a package on other targets, which `Microsoft.Extensions.Logging.Abstractions` already brought in transitively).
