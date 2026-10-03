namespace SyslogLogging
{
    /// <summary>
    /// Stable public names for the telemetry SyslogLogging emits through the base class library.
    /// The library emits metrics through a <c>System.Diagnostics.Metrics.Meter</c> and traces through a
    /// <c>System.Diagnostics.ActivitySource</c>, both named <see cref="MeterName"/> / <see cref="ActivitySourceName"/>.
    /// It takes no dependency on any exporter or SDK. A host subscribes to these names (for example with
    /// Radiant's <c>settings.Sources.AddMeter(SyslogLoggingTelemetry.MeterName)</c> or the OpenTelemetry SDK's
    /// <c>AddMeter</c>/<c>AddSource</c>) and exports them. When nothing subscribes, emission is effectively free.
    /// These names are a public contract consumed by dashboards and alerts; they do not change between minor versions.
    /// See TELEMETRY.md in the repository root for the full catalog.
    /// </summary>
    public static class SyslogLoggingTelemetry
    {
        #region Sources

        /// <summary>
        /// Name of the meter that carries all SyslogLogging metrics. Value: <c>SyslogLogging</c>.
        /// </summary>
        public const string MeterName = "SyslogLogging";

        /// <summary>
        /// Name of the activity source that carries all SyslogLogging spans. Value: <c>SyslogLogging</c>.
        /// </summary>
        public const string ActivitySourceName = "SyslogLogging";

        #endregion

        #region Metrics

        /// <summary>
        /// Counter of log entries submitted to a <see cref="LoggingModule"/>, by severity, mode, and outcome. Unit: {entry}.
        /// </summary>
        public const string EntriesMetric = "sysloglogging.entries";

        /// <summary>
        /// Histogram of end-to-end time to deliver one log entry to every configured destination, by mode and outcome. Unit: s.
        /// </summary>
        public const string EntryDurationMetric = "sysloglogging.entry.duration";

        /// <summary>
        /// Counter of log entries that exceeded <see cref="LoggingSettings.MaxMessageLength"/> and were split into multiple parts. Unit: {entry}.
        /// </summary>
        public const string EntriesSplitMetric = "sysloglogging.entries.split";

        /// <summary>
        /// Histogram of time spent waiting to acquire the module's I/O lock (the single concurrency slot that serializes delivery). Unit: s.
        /// </summary>
        public const string IoLockWaitMetric = "sysloglogging.io_lock.wait.duration";

        /// <summary>
        /// Counter of writes to a single destination (console, file, syslog), by destination and outcome. Unit: {write}.
        /// </summary>
        public const string DestinationWritesMetric = "sysloglogging.destination.writes";

        /// <summary>
        /// Histogram of time spent writing to a single destination, by destination and outcome. Unit: s.
        /// </summary>
        public const string DestinationDurationMetric = "sysloglogging.destination.duration";

        /// <summary>
        /// Counter of UDP payload bytes sent to syslog servers, by server. Unit: By.
        /// </summary>
        public const string SyslogSentBytesMetric = "sysloglogging.syslog.sent";

        /// <summary>
        /// Counter of errors routed to <see cref="LoggingModule.OnLoggingError"/>, by component and error type. Unit: {error}.
        /// </summary>
        public const string ErrorsMetric = "sysloglogging.errors";

        /// <summary>
        /// Histogram of time spent inside <see cref="LoggingModule.MessageLogged"/> handlers, by outcome. Unit: s.
        /// </summary>
        public const string EventHandlerDurationMetric = "sysloglogging.event_handler.duration";

        /// <summary>
        /// Counter of log retention cleanup runs, by outcome. Unit: {run}.
        /// </summary>
        public const string RetentionRunsMetric = "sysloglogging.retention.runs";

        /// <summary>
        /// Histogram of log retention cleanup run time, by outcome. Unit: s.
        /// </summary>
        public const string RetentionDurationMetric = "sysloglogging.retention.duration";

        /// <summary>
        /// Counter of expired log files deleted by retention cleanup. Unit: {file}.
        /// </summary>
        public const string RetentionFilesDeletedMetric = "sysloglogging.retention.files_deleted";

        /// <summary>
        /// Gauge of the Unix time (seconds) of the most recent successful retention cleanup run in this process. Unit: s.
        /// </summary>
        public const string RetentionLastSuccessMetric = "sysloglogging.retention.last_success";

        /// <summary>
        /// Up-down counter of <see cref="LoggingModule"/> instances that have been constructed and not yet disposed. Unit: {module}.
        /// </summary>
        public const string ModulesActiveMetric = "sysloglogging.modules.active";

        /// <summary>
        /// Gauge that is always 1, labeled with the library version. No unit (so Prometheus exporters do not append a suffix).
        /// </summary>
        public const string BuildInfoMetric = "sysloglogging.build.info";

        #endregion

        #region Spans

        /// <summary>
        /// Span covering delivery of one log entry to every configured destination. Kind: Internal.
        /// </summary>
        public const string WriteSpan = "sysloglogging write";

        /// <summary>
        /// Span covering one console write. Kind: Internal.
        /// </summary>
        public const string ConsoleWriteSpan = "console write";

        /// <summary>
        /// Span covering one file append. Kind: Internal.
        /// </summary>
        public const string FileWriteSpan = "file write";

        /// <summary>
        /// Span covering one UDP datagram sent to one syslog server, including client creation and name resolution. Kind: Client.
        /// </summary>
        public const string SyslogSendSpan = "syslog send";

        /// <summary>
        /// Span covering invocation of the <see cref="LoggingModule.MessageLogged"/> handlers for one entry. Kind: Internal.
        /// </summary>
        public const string EventHandlerSpan = "sysloglogging MessageLogged";

        /// <summary>
        /// Root span covering one log retention cleanup run. Kind: Internal.
        /// </summary>
        public const string RetentionSpan = "sysloglogging retention";

        #endregion

        #region Attributes

        /// <summary>
        /// Attribute: entry severity name (Debug, Info, Warn, Error, Alert, Critical, Emergency).
        /// </summary>
        public const string SeverityAttribute = "sysloglogging.severity";

        /// <summary>
        /// Attribute: API mode, <c>sync</c> or <c>async</c>.
        /// </summary>
        public const string ModeAttribute = "sysloglogging.mode";

        /// <summary>
        /// Attribute: operation outcome. Entries: success, degraded, failure, filtered. Other operations: success, failure.
        /// </summary>
        public const string OutcomeAttribute = "sysloglogging.outcome";

        /// <summary>
        /// Attribute: destination, <c>console</c>, <c>file</c>, or <c>syslog</c>.
        /// </summary>
        public const string DestinationAttribute = "sysloglogging.destination";

        /// <summary>
        /// Attribute: component that reported an error (console, file, syslog, pipeline, event_handler, retention).
        /// </summary>
        public const string ComponentAttribute = "sysloglogging.component";

        /// <summary>
        /// Attribute: name of the event whose handler ran, currently always <c>MessageLogged</c>.
        /// </summary>
        public const string EventAttribute = "sysloglogging.event";

        /// <summary>
        /// Span attribute: number of parts the entry was split into.
        /// </summary>
        public const string PartsAttribute = "sysloglogging.parts";

        /// <summary>
        /// Span attribute: 1-based part sequence number of a destination write.
        /// </summary>
        public const string PartAttribute = "sysloglogging.part";

        /// <summary>
        /// Span attribute: source context of the entry (for example the Microsoft.Extensions.Logging category).
        /// </summary>
        public const string SourceAttribute = "sysloglogging.source";

        /// <summary>
        /// Span attribute: number of files deleted by a retention run.
        /// </summary>
        public const string FilesDeletedAttribute = "sysloglogging.retention.files_deleted";

        /// <summary>
        /// Attribute: library version (build info only).
        /// </summary>
        public const string VersionAttribute = "sysloglogging.version";

        /// <summary>
        /// OpenTelemetry semantic-convention attribute: syslog server host name or address.
        /// </summary>
        public const string ServerAddressAttribute = "server.address";

        /// <summary>
        /// OpenTelemetry semantic-convention attribute: syslog server port.
        /// </summary>
        public const string ServerPortAttribute = "server.port";

        /// <summary>
        /// OpenTelemetry semantic-convention attribute: network transport (always <c>udp</c> for syslog).
        /// </summary>
        public const string NetworkTransportAttribute = "network.transport";

        /// <summary>
        /// OpenTelemetry semantic-convention attribute: full type name of the exception that caused a failure.
        /// </summary>
        public const string ErrorTypeAttribute = "error.type";

        #endregion
    }
}
