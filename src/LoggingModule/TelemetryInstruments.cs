namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using System.Threading;

    /// <summary>
    /// Process-wide meter, activity source, and instruments for SyslogLogging.
    /// Every recording method is best-effort: it never throws into the logging path.
    /// </summary>
    internal static class TelemetryInstruments
    {
        internal const string ModeSync = "sync";
        internal const string ModeAsync = "async";

        internal const string OutcomeSuccess = "success";
        internal const string OutcomeDegraded = "degraded";
        internal const string OutcomeFailure = "failure";
        internal const string OutcomeFiltered = "filtered";

        internal const string DestinationConsole = "console";
        internal const string DestinationFile = "file";
        internal const string DestinationSyslog = "syslog";

        internal const string ComponentPipeline = "pipeline";
        internal const string ComponentEventHandler = "event_handler";
        internal const string ComponentRetention = "retention";

        internal const string EventMessageLogged = "MessageLogged";

        internal static readonly string Version = ResolveVersion();

        internal static readonly ActivitySource Source = new ActivitySource(SyslogLoggingTelemetry.ActivitySourceName, Version);

        internal static readonly Meter Meter = new Meter(SyslogLoggingTelemetry.MeterName, Version);

        private static readonly double[] _DurationBuckets = new double[]
        {
            0.00001, 0.00005, 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01,
            0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10
        };

        private static readonly Counter<long> _Entries = Meter.CreateCounter<long>(
            SyslogLoggingTelemetry.EntriesMetric, "{entry}", "Log entries submitted, by severity, mode, and outcome.");

        private static readonly Histogram<double> _EntryDuration = CreateDurationHistogram(
            SyslogLoggingTelemetry.EntryDurationMetric, "Time to deliver one log entry to every configured destination.");

        private static readonly Counter<long> _EntriesSplit = Meter.CreateCounter<long>(
            SyslogLoggingTelemetry.EntriesSplitMetric, "{entry}", "Log entries split into multiple parts because they exceeded MaxMessageLength.");

        private static readonly Histogram<double> _IoLockWait = CreateDurationHistogram(
            SyslogLoggingTelemetry.IoLockWaitMetric, "Time spent waiting to acquire the module I/O lock.");

        private static readonly Counter<long> _DestinationWrites = Meter.CreateCounter<long>(
            SyslogLoggingTelemetry.DestinationWritesMetric, "{write}", "Writes to a single destination, by destination and outcome.");

        private static readonly Histogram<double> _DestinationDuration = CreateDurationHistogram(
            SyslogLoggingTelemetry.DestinationDurationMetric, "Time spent writing to a single destination.");

        private static readonly Counter<long> _SyslogSentBytes = Meter.CreateCounter<long>(
            SyslogLoggingTelemetry.SyslogSentBytesMetric, "By", "UDP payload bytes sent to syslog servers.");

        private static readonly Counter<long> _Errors = Meter.CreateCounter<long>(
            SyslogLoggingTelemetry.ErrorsMetric, "{error}", "Errors routed to OnLoggingError, by component and error type.");

        private static readonly Histogram<double> _EventHandlerDuration = CreateDurationHistogram(
            SyslogLoggingTelemetry.EventHandlerDurationMetric, "Time spent inside MessageLogged handlers.");

        private static readonly Counter<long> _RetentionRuns = Meter.CreateCounter<long>(
            SyslogLoggingTelemetry.RetentionRunsMetric, "{run}", "Log retention cleanup runs, by outcome.");

        private static readonly Histogram<double> _RetentionDuration = CreateDurationHistogram(
            SyslogLoggingTelemetry.RetentionDurationMetric, "Log retention cleanup run time.");

        private static readonly Counter<long> _RetentionFilesDeleted = Meter.CreateCounter<long>(
            SyslogLoggingTelemetry.RetentionFilesDeletedMetric, "{file}", "Expired log files deleted by retention cleanup.");

        private static readonly UpDownCounter<long> _ModulesActive = Meter.CreateUpDownCounter<long>(
            SyslogLoggingTelemetry.ModulesActiveMetric, "{module}", "LoggingModule instances constructed and not yet disposed.");

        private static long _RetentionLastSuccessUnixSeconds = 0;

        static TelemetryInstruments()
        {
            Meter.CreateObservableGauge<long>(
                SyslogLoggingTelemetry.RetentionLastSuccessMetric,
                ObserveRetentionLastSuccess,
                "s",
                "Unix time of the most recent successful retention cleanup run in this process.");

            Meter.CreateObservableGauge<int>(
                SyslogLoggingTelemetry.BuildInfoMetric,
                ObserveBuildInfo,
                null,
                "Always 1, labeled with the SyslogLogging library version.");
        }

        internal static long Timestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        internal static double ElapsedSeconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
        }

        internal static string SeverityName(Severity severity)
        {
            switch (severity)
            {
                case Severity.Debug: return "Debug";
                case Severity.Info: return "Info";
                case Severity.Warn: return "Warn";
                case Severity.Error: return "Error";
                case Severity.Alert: return "Alert";
                case Severity.Critical: return "Critical";
                case Severity.Emergency: return "Emergency";
                default: return "Unknown";
            }
        }

        internal static string ErrorType(Exception exception)
        {
            if (exception == null) return "unknown";
            return exception.GetType().FullName ?? "unknown";
        }

        internal static Activity StartEntryActivity(LogEntry entry, string mode, int parts)
        {
            try
            {
                if (!Source.HasListeners()) return null;

                Activity activity = Source.StartActivity(SyslogLoggingTelemetry.WriteSpan, ActivityKind.Internal);
                if (activity == null) return null;

                if (activity.IsAllDataRequested)
                {
                    activity.SetTag(SyslogLoggingTelemetry.SeverityAttribute, SeverityName(entry.Severity));
                    activity.SetTag(SyslogLoggingTelemetry.ModeAttribute, mode);
                    activity.SetTag(SyslogLoggingTelemetry.PartsAttribute, parts);
                    if (!string.IsNullOrEmpty(entry.Source)) activity.SetTag(SyslogLoggingTelemetry.SourceAttribute, entry.Source);
                }

                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static Activity StartDestinationActivity(string destination, int part, string serverAddress, int serverPort)
        {
            try
            {
                if (!Source.HasListeners()) return null;

                string name;
                ActivityKind kind;
                switch (destination)
                {
                    case DestinationSyslog:
                        name = SyslogLoggingTelemetry.SyslogSendSpan;
                        kind = ActivityKind.Client;
                        break;
                    case DestinationFile:
                        name = SyslogLoggingTelemetry.FileWriteSpan;
                        kind = ActivityKind.Internal;
                        break;
                    default:
                        name = SyslogLoggingTelemetry.ConsoleWriteSpan;
                        kind = ActivityKind.Internal;
                        break;
                }

                Activity activity = Source.StartActivity(name, kind);
                if (activity == null) return null;

                if (activity.IsAllDataRequested)
                {
                    activity.SetTag(SyslogLoggingTelemetry.DestinationAttribute, destination);
                    activity.SetTag(SyslogLoggingTelemetry.PartAttribute, part);
                    if (serverAddress != null)
                    {
                        activity.SetTag(SyslogLoggingTelemetry.ServerAddressAttribute, serverAddress);
                        activity.SetTag(SyslogLoggingTelemetry.ServerPortAttribute, serverPort);
                        activity.SetTag(SyslogLoggingTelemetry.NetworkTransportAttribute, "udp");
                    }
                }

                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static Activity StartEventHandlerActivity()
        {
            try
            {
                if (!Source.HasListeners()) return null;

                Activity activity = Source.StartActivity(SyslogLoggingTelemetry.EventHandlerSpan, ActivityKind.Internal);
                activity?.SetTag(SyslogLoggingTelemetry.EventAttribute, EventMessageLogged);
                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static Activity StartRetentionActivity()
        {
            try
            {
                if (!Source.HasListeners()) return null;
                return Source.StartActivity(SyslogLoggingTelemetry.RetentionSpan, ActivityKind.Internal);
            }
            catch
            {
                return null;
            }
        }

        internal static void CompleteActivity(Activity activity, string outcome, Exception exception)
        {
            if (activity == null) return;

            try
            {
                activity.SetTag(SyslogLoggingTelemetry.OutcomeAttribute, outcome);

                if (exception != null)
                {
                    string errorType = ErrorType(exception);
                    activity.SetTag(SyslogLoggingTelemetry.ErrorTypeAttribute, errorType);

                    ActivityTagsCollection tags = new ActivityTagsCollection
                    {
                        { "exception.type", errorType },
                        { "exception.message", exception.Message }
                    };
                    activity.AddEvent(new ActivityEvent("exception", default(DateTimeOffset), tags));
                    activity.SetStatus(ActivityStatusCode.Error, errorType);
                }
                else if (outcome == OutcomeSuccess)
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
                else
                {
                    activity.SetStatus(ActivityStatusCode.Error, outcome);
                }
            }
            catch
            {
                // Best-effort; never affect logging.
            }
            finally
            {
                try
                {
                    activity.Dispose();
                }
                catch
                {
                    // Best-effort; never affect logging.
                }
            }
        }

        internal static void RecordEntry(Severity severity, string mode, string outcome, double? seconds)
        {
            try
            {
                if (_Entries.Enabled)
                {
                    TagList tags = new TagList();
                    tags.Add(SyslogLoggingTelemetry.SeverityAttribute, SeverityName(severity));
                    tags.Add(SyslogLoggingTelemetry.ModeAttribute, mode);
                    tags.Add(SyslogLoggingTelemetry.OutcomeAttribute, outcome);
                    _Entries.Add(1, tags);
                }

                if (seconds.HasValue && _EntryDuration.Enabled)
                {
                    TagList tags = new TagList();
                    tags.Add(SyslogLoggingTelemetry.ModeAttribute, mode);
                    tags.Add(SyslogLoggingTelemetry.OutcomeAttribute, outcome);
                    _EntryDuration.Record(seconds.Value, tags);
                }
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void RecordSplit(string mode)
        {
            try
            {
                if (!_EntriesSplit.Enabled) return;
                _EntriesSplit.Add(1, new KeyValuePair<string, object>(SyslogLoggingTelemetry.ModeAttribute, mode));
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static bool IoLockWaitEnabled
        {
            get
            {
                return _IoLockWait.Enabled;
            }
        }

        internal static void RecordIoLockWait(string mode, double seconds)
        {
            try
            {
                if (!_IoLockWait.Enabled) return;
                _IoLockWait.Record(seconds, new KeyValuePair<string, object>(SyslogLoggingTelemetry.ModeAttribute, mode));
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void RecordDestination(string destination, string outcome, double seconds, Exception exception, string serverAddress, int serverPort)
        {
            try
            {
                if (!_DestinationWrites.Enabled && !_DestinationDuration.Enabled) return;

                TagList tags = new TagList();
                tags.Add(SyslogLoggingTelemetry.DestinationAttribute, destination);
                tags.Add(SyslogLoggingTelemetry.OutcomeAttribute, outcome);
                if (serverAddress != null)
                {
                    tags.Add(SyslogLoggingTelemetry.ServerAddressAttribute, serverAddress);
                    tags.Add(SyslogLoggingTelemetry.ServerPortAttribute, serverPort);
                }

                _DestinationDuration.Record(seconds, tags);

                if (exception != null) tags.Add(SyslogLoggingTelemetry.ErrorTypeAttribute, ErrorType(exception));
                _DestinationWrites.Add(1, tags);
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void RecordSyslogBytes(string serverAddress, int serverPort, int bytes)
        {
            try
            {
                if (!_SyslogSentBytes.Enabled) return;
                _SyslogSentBytes.Add(
                    bytes,
                    new KeyValuePair<string, object>(SyslogLoggingTelemetry.ServerAddressAttribute, serverAddress),
                    new KeyValuePair<string, object>(SyslogLoggingTelemetry.ServerPortAttribute, serverPort));
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void RecordError(string component, Exception exception)
        {
            try
            {
                if (!_Errors.Enabled) return;
                _Errors.Add(
                    1,
                    new KeyValuePair<string, object>(SyslogLoggingTelemetry.ComponentAttribute, component),
                    new KeyValuePair<string, object>(SyslogLoggingTelemetry.ErrorTypeAttribute, ErrorType(exception)));
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void RecordEventHandler(string outcome, double seconds)
        {
            try
            {
                if (!_EventHandlerDuration.Enabled) return;
                _EventHandlerDuration.Record(
                    seconds,
                    new KeyValuePair<string, object>(SyslogLoggingTelemetry.EventAttribute, EventMessageLogged),
                    new KeyValuePair<string, object>(SyslogLoggingTelemetry.OutcomeAttribute, outcome));
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void RecordRetention(string outcome, double seconds, int filesDeleted)
        {
            try
            {
                KeyValuePair<string, object> outcomeTag = new KeyValuePair<string, object>(SyslogLoggingTelemetry.OutcomeAttribute, outcome);
                _RetentionRuns.Add(1, outcomeTag);
                _RetentionDuration.Record(seconds, outcomeTag);
                if (filesDeleted > 0) _RetentionFilesDeleted.Add(filesDeleted);

                if (outcome == OutcomeSuccess)
                {
                    Interlocked.Exchange(ref _RetentionLastSuccessUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void ModuleCreated()
        {
            try
            {
                _ModulesActive.Add(1);
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        internal static void ModuleDisposed()
        {
            try
            {
                _ModulesActive.Add(-1);
            }
            catch
            {
                // Best-effort; never affect logging.
            }
        }

        private static Histogram<double> CreateDurationHistogram(string name, string description)
        {
            return Meter.CreateHistogram<double>(
                name,
                "s",
                description,
                null,
                new InstrumentAdvice<double> { HistogramBucketBoundaries = _DurationBuckets });
        }

        private static IEnumerable<Measurement<long>> ObserveRetentionLastSuccess()
        {
            long value = Interlocked.Read(ref _RetentionLastSuccessUnixSeconds);
            if (value <= 0) return Array.Empty<Measurement<long>>();
            return new Measurement<long>[] { new Measurement<long>(value) };
        }

        private static Measurement<int> ObserveBuildInfo()
        {
            return new Measurement<int>(1, new KeyValuePair<string, object>(SyslogLoggingTelemetry.VersionAttribute, Version));
        }

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(TelemetryInstruments).Assembly;
                AssemblyInformationalVersionAttribute info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string version = info?.InformationalVersion;
                if (!string.IsNullOrEmpty(version))
                {
                    int plus = version.IndexOf('+');
                    return plus > 0 ? version.Substring(0, plus) : version;
                }

                return assembly.GetName().Version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
