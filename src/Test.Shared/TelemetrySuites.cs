namespace SyslogLogging.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Touchstone.Core;

    using SyslogLogging;

    using T = SyslogLogging.SyslogLoggingTelemetry;

    public static class TelemetrySuites
    {
        private const string SuiteId = "Telemetry";

        public static TestSuiteDescriptor TelemetrySuite()
        {
            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "Telemetry (metrics and traces)",
                cases: new List<TestCaseDescriptor>
                {
                    Case("StableNames", "Meter, activity source, and every catalog instrument are published under stable names", StableNamesAsync),
                    Case("SyncFileSuccess", "Sync file write emits entry, destination, lock-wait metrics and a write span with a file child span", SyncFileSuccessAsync),
                    Case("AsyncSyslogSuccess", "Async syslog send emits per-server metrics, bytes sent, and a Client span", AsyncSyslogSuccessAsync),
                    Case("SyncSyslogSuccess", "Sync syslog send emits per-server metrics and a Client span", SyncSyslogSuccessAsync),
                    Case("ConsoleDestination", "Console writes are measured and traced", ConsoleDestinationAsync),
                    Case("Filtered", "Entries below MinimumSeverity count as filtered with no duration and no span", FilteredAsync),
                    Case("Split", "Oversized entries count as split and produce one destination span per part", SplitAsync),
                    Case("FileFailureDegraded", "A failing destination yields outcome=degraded, error.type, and Error span status", FileFailureDegradedAsync),
                    Case("SyslogFailure", "A syslog send failure is counted with error.type and traced as Error", SyslogFailureAsync),
                    Case("PipelineFailure", "An exception escaping the pipeline yields outcome=failure", PipelineFailureAsync),
                    Case("EventHandlerFailure", "A throwing MessageLogged handler is measured, counted as an error, and traced as Error", EventHandlerFailureAsync),
                    Case("EventHandlerSuccess", "A MessageLogged handler is measured and traced", EventHandlerSuccessAsync),
                    Case("RetentionRun", "A retention run emits run, duration, files-deleted, last-success metrics and a root span", RetentionRunAsync),
                    Case("Lifecycle", "Module construction and disposal move the active-modules counter exactly once each", LifecycleAsync),
                    Case("BuildInfo", "Build info gauge reports 1 with the library version", BuildInfoAsync),
                    Case("TraceCorrelation", "Write span nests under the caller's Activity and {trace}/{span} tokens stamp the caller's IDs", TraceCorrelationAsync),
                    Case("JsonTraceIds", "LogEntry captures trace and span IDs and serializes them to JSON", JsonTraceIdsAsync),
                    Case("ILoggerPath", "Microsoft.Extensions.Logging path is measured and the span carries the category", ILoggerPathAsync),
                    Case("Toggles", "EnableMetrics=false and EnableTracing=false suppress this module's telemetry", TogglesAsync),
                    Case("NoListener", "Logging with no listener attached works and LogEntry has no trace IDs", NoListenerAsync),
                    Case("NoPayloadOnTelemetry", "Message text never appears in metric tags or span tags", NoPayloadAsync),
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> execute)
        {
            return new TestCaseDescriptor(suiteId: SuiteId, caseId: caseId, displayName: displayName, executeAsync: execute);
        }

        private static async Task StableNamesAsync(CancellationToken ct)
        {
            TestHelpers.AssertEqual("SyslogLogging", T.MeterName, "Meter name is public contract.");
            TestHelpers.AssertEqual("SyslogLogging", T.ActivitySourceName, "Activity source name is public contract.");

            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-names");
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(temp.GetPath("names.log"));
            await log.InfoAsync("names", ct).ConfigureAwait(false);

            string[] expected = new string[]
            {
                T.EntriesMetric, T.EntryDurationMetric, T.EntriesSplitMetric, T.IoLockWaitMetric,
                T.DestinationWritesMetric, T.DestinationDurationMetric, T.SyslogSentBytesMetric, T.ErrorsMetric,
                T.EventHandlerDurationMetric, T.RetentionRunsMetric, T.RetentionDurationMetric,
                T.RetentionFilesDeletedMetric, T.RetentionLastSuccessMetric, T.ModulesActiveMetric, T.BuildInfoMetric
            };

            IReadOnlyCollection<string> published = capture.PublishedInstruments;
            foreach (string name in expected)
            {
                TestHelpers.AssertTrue(published.Contains(name), "Instrument '" + name + "' should be published on the SyslogLogging meter.");
            }

            TestHelpers.AssertEqual(expected.Length, published.Count, "Every published instrument should be in the documented catalog.");
        }

        private static Task SyncFileSuccessAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-sync-file");
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(temp.GetPath("sync.log"));

            log.Warn("sync-file");

            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric,
                TelemetryCapture.Tag(T.SeverityAttribute, "Warn"),
                TelemetryCapture.Tag(T.ModeAttribute, "sync"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "One successful sync entry. " + capture.Dump());
            TestHelpers.AssertEqual(1, capture.Count(T.EntryDurationMetric,
                TelemetryCapture.Tag(T.ModeAttribute, "sync"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "Entry duration recorded.");
            TestHelpers.AssertEqual(1d, capture.Sum(T.DestinationWritesMetric,
                TelemetryCapture.Tag(T.DestinationAttribute, "file"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "One successful file write.");
            TestHelpers.AssertEqual(1, capture.Count(T.DestinationDurationMetric, TelemetryCapture.Tag(T.DestinationAttribute, "file")), "File duration recorded.");
            TestHelpers.AssertTrue(capture.Count(T.IoLockWaitMetric, TelemetryCapture.Tag(T.ModeAttribute, "sync")) >= 1, "Lock wait recorded.");
            TestHelpers.AssertTrue(capture.Measurements(T.EntryDurationMetric).All(m => m.Value >= 0), "Durations are non-negative seconds.");

            Activity write = Single(capture.Spans(T.WriteSpan), "write span");
            Activity file = Single(capture.Spans(T.FileWriteSpan), "file write span");
            TestHelpers.AssertEqual(write.SpanId, file.ParentSpanId, "File span is a child of the write span.");
            TestHelpers.AssertEqual(ActivityStatusCode.Ok, write.Status, "Write span status is Ok.");
            TestHelpers.AssertEqual(ActivityStatusCode.Ok, file.Status, "File span status is Ok.");
            TestHelpers.AssertEqual("Warn", write.GetTagItem(T.SeverityAttribute) as string, "Severity tag on write span.");
            TestHelpers.AssertEqual("success", write.GetTagItem(T.OutcomeAttribute) as string, "Outcome tag on write span.");
            return Task.CompletedTask;
        }

        private static async Task AsyncSyslogSuccessAsync(CancellationToken ct)
        {
            using UdpCaptureListener listener = new UdpCaptureListener();
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

            await log.ErrorAsync("async-syslog", ct).ConfigureAwait(false);
            string received = await listener.ReceiveAsync().ConfigureAwait(false);

            KeyValuePair<string, object?> port = TelemetryCapture.Tag(T.ServerPortAttribute, listener.Port);
            KeyValuePair<string, object?> address = TelemetryCapture.Tag(T.ServerAddressAttribute, "127.0.0.1");

            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric,
                TelemetryCapture.Tag(T.ModeAttribute, "async"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "One successful async entry. " + capture.Dump());
            TestHelpers.AssertEqual(1d, capture.Sum(T.DestinationWritesMetric,
                TelemetryCapture.Tag(T.DestinationAttribute, "syslog"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success"), address, port), "One successful syslog write tagged with the server.");
            TestHelpers.AssertEqual(1, capture.Count(T.DestinationDurationMetric, TelemetryCapture.Tag(T.DestinationAttribute, "syslog"), port), "Syslog latency recorded.");
            TestHelpers.AssertEqual((double)System.Text.Encoding.UTF8.GetByteCount(received), capture.Sum(T.SyslogSentBytesMetric, port), "Bytes sent equals the datagram size.");

            Activity send = Single(capture.Spans(T.SyslogSendSpan), "syslog send span");
            TestHelpers.AssertEqual(ActivityKind.Client, send.Kind, "Syslog send is a Client span.");
            TestHelpers.AssertEqual("127.0.0.1", send.GetTagItem(T.ServerAddressAttribute) as string, "server.address on span.");
            TestHelpers.AssertEqual(listener.Port, (int)send.GetTagItem(T.ServerPortAttribute)!, "server.port on span.");
            TestHelpers.AssertEqual("udp", send.GetTagItem(T.NetworkTransportAttribute) as string, "network.transport on span.");
            Activity write = Single(capture.Spans(T.WriteSpan), "write span");
            TestHelpers.AssertEqual(write.SpanId, send.ParentSpanId, "Async syslog span nests under the write span across awaits.");
        }

        private static async Task SyncSyslogSuccessAsync(CancellationToken ct)
        {
            using UdpCaptureListener listener = new UdpCaptureListener();
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

            log.Info("sync-syslog");
            await listener.ReceiveAsync().ConfigureAwait(false);

            TestHelpers.AssertEqual(1d, capture.Sum(T.DestinationWritesMetric,
                TelemetryCapture.Tag(T.DestinationAttribute, "syslog"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success"),
                TelemetryCapture.Tag(T.ServerPortAttribute, listener.Port)), "One successful sync syslog write. " + capture.Dump());
            TestHelpers.AssertTrue(capture.Sum(T.SyslogSentBytesMetric, TelemetryCapture.Tag(T.ServerPortAttribute, listener.Port)) > 0, "Bytes sent recorded.");
            Single(capture.Spans(T.SyslogSendSpan), "syslog send span");
        }

        private static Task ConsoleDestinationAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = new LoggingModule("telemetry-console-unused.log", FileLoggingMode.Disabled, true);

            TextWriter original = Console.Out;
            try
            {
                Console.SetOut(TextWriter.Null);
                log.Info("console");
            }
            finally
            {
                Console.SetOut(original);
            }

            TestHelpers.AssertEqual(1d, capture.Sum(T.DestinationWritesMetric,
                TelemetryCapture.Tag(T.DestinationAttribute, "console"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "One successful console write. " + capture.Dump());
            Single(capture.Spans(T.ConsoleWriteSpan), "console write span");
            return Task.CompletedTask;
        }

        private static async Task FilteredAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-filtered");
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(temp.GetPath("filtered.log"), settings => settings.MinimumSeverity = Severity.Error);

            log.Info("filtered-sync");
            await log.DebugAsync("filtered-async", ct).ConfigureAwait(false);
            log.LogEntry(new LogEntry(Severity.Warn, "filtered-entry"));
            await log.LogEntryAsync(new LogEntry(Severity.Warn, "filtered-entry-async"), ct).ConfigureAwait(false);

            TestHelpers.AssertEqual(2d, capture.Sum(T.EntriesMetric,
                TelemetryCapture.Tag(T.ModeAttribute, "sync"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "filtered")), "Two filtered sync entries. " + capture.Dump());
            TestHelpers.AssertEqual(2d, capture.Sum(T.EntriesMetric,
                TelemetryCapture.Tag(T.ModeAttribute, "async"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "filtered")), "Two filtered async entries.");
            TestHelpers.AssertEqual(0, capture.Measurements(T.EntryDurationMetric).Count, "Filtered entries record no duration.");
            TestHelpers.AssertEqual(0, capture.AllSpans().Count, "Filtered entries produce no spans.");
        }

        private static async Task SplitAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-split");
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(temp.GetPath("split.log"), settings => settings.MaxMessageLength = 32);

            await log.InfoAsync(new string('x', 100), ct).ConfigureAwait(false);

            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesSplitMetric, TelemetryCapture.Tag(T.ModeAttribute, "async")), "One split entry. " + capture.Dump());
            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric, TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "Split entry still counts once.");
            Activity write = Single(capture.Spans(T.WriteSpan), "write span");
            TestHelpers.AssertEqual(4, (int)write.GetTagItem(T.PartsAttribute)!, "Write span carries part count.");
            List<Activity> files = capture.Spans(T.FileWriteSpan);
            TestHelpers.AssertEqual(4, files.Count, "One file span per part.");
            TestHelpers.AssertTrue(files.Select(f => (int)f.GetTagItem(T.PartAttribute)!).OrderBy(p => p).SequenceEqual(new[] { 1, 2, 3, 4 }), "Part numbers 1..4.");
        }

        private static async Task FileFailureDegradedAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-file-failure");
            string directoryAsFile = temp.GetPath("i-am-a-directory");
            Directory.CreateDirectory(directoryAsFile);

            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(directoryAsFile);
            int errors = 0;
            log.OnLoggingError += _ => Interlocked.Increment(ref errors);

            log.Info("file-fail-sync");
            await log.InfoAsync("file-fail-async", ct).ConfigureAwait(false);

            TestHelpers.AssertEqual(2, errors, "OnLoggingError still raised for each failure.");
            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric, TelemetryCapture.Tag(T.ModeAttribute, "sync"), TelemetryCapture.Tag(T.OutcomeAttribute, "degraded")), "Sync entry degraded. " + capture.Dump());
            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric, TelemetryCapture.Tag(T.ModeAttribute, "async"), TelemetryCapture.Tag(T.OutcomeAttribute, "degraded")), "Async entry degraded.");

            List<CapturedMeasurement> failures = capture.Measurements(T.DestinationWritesMetric)
                .Where(m => m.HasTag(T.DestinationAttribute, "file") && m.HasTag(T.OutcomeAttribute, "failure"))
                .ToList();
            TestHelpers.AssertEqual(2, failures.Count, "Two failed file writes.");
            TestHelpers.AssertTrue(failures.All(m => m.Tags.ContainsKey(T.ErrorTypeAttribute) && !string.IsNullOrEmpty(m.Tags[T.ErrorTypeAttribute] as string)), "Failed writes carry error.type.");
            TestHelpers.AssertEqual(2d, capture.Sum(T.ErrorsMetric, TelemetryCapture.Tag(T.ComponentAttribute, "file")), "Errors counted by component.");

            List<Activity> fileSpans = capture.Spans(T.FileWriteSpan);
            TestHelpers.AssertEqual(2, fileSpans.Count, "Two file spans.");
            TestHelpers.AssertTrue(fileSpans.All(s => s.Status == ActivityStatusCode.Error), "Failed file spans are Error.");
            TestHelpers.AssertTrue(fileSpans.All(s => s.Events.Any(e => e.Name == "exception")), "Failed file spans record the exception.");
            TestHelpers.AssertTrue(capture.Spans(T.WriteSpan).All(s => s.Status == ActivityStatusCode.Error && (s.GetTagItem(T.OutcomeAttribute) as string) == "degraded"), "Degraded write spans are Error.");
        }

        private static async Task SyslogFailureAsync(CancellationToken ct)
        {
            // A datagram larger than the 65507-byte UDP maximum fails at send time (EMSGSIZE).
            using UdpCaptureListener listener = new UdpCaptureListener();
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);
            TestHelpers.ConfigureSettings(log, settings => settings.MaxMessageLength = 70000);
            log.OnLoggingError += _ => { };

            await log.InfoAsync(new string('x', 69000), ct).ConfigureAwait(false);

            TestHelpers.AssertEqual(1d, capture.Sum(T.DestinationWritesMetric,
                TelemetryCapture.Tag(T.DestinationAttribute, "syslog"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "failure"),
                TelemetryCapture.Tag(T.ServerPortAttribute, listener.Port)), "Syslog failure counted. " + capture.Dump());
            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric, TelemetryCapture.Tag(T.OutcomeAttribute, "degraded")), "Entry degraded by syslog failure.");
            TestHelpers.AssertTrue(capture.Measurements(T.ErrorsMetric).Any(m => m.HasTag(T.ComponentAttribute, "syslog") && m.Tags.ContainsKey(T.ErrorTypeAttribute)), "Syslog error counted with error.type.");
            TestHelpers.AssertEqual(0d, capture.Sum(T.SyslogSentBytesMetric), "No bytes sent on failure.");
            Activity send = Single(capture.Spans(T.SyslogSendSpan), "syslog send span");
            TestHelpers.AssertEqual(ActivityStatusCode.Error, send.Status, "Failed syslog span is Error.");
            TestHelpers.AssertTrue(!string.IsNullOrEmpty(send.GetTagItem(T.ErrorTypeAttribute) as string), "Failed syslog span carries error.type.");
        }

        private static Task PipelineFailureAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-pipeline-failure");
            string directoryAsFile = temp.GetPath("dir");
            Directory.CreateDirectory(directoryAsFile);

            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(directoryAsFile);
            int calls = 0;
            log.OnLoggingError += _ =>
            {
                if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("handler failure");
            };

            log.Info("pipeline-fail");

            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric, TelemetryCapture.Tag(T.OutcomeAttribute, "failure")), "Entry counted as failure. " + capture.Dump());
            TestHelpers.AssertEqual(1d, capture.Sum(T.ErrorsMetric,
                TelemetryCapture.Tag(T.ComponentAttribute, "pipeline"),
                TelemetryCapture.Tag(T.ErrorTypeAttribute, typeof(InvalidOperationException).FullName)), "Pipeline error counted with inner error.type.");
            Activity write = Single(capture.Spans(T.WriteSpan), "write span");
            TestHelpers.AssertEqual(ActivityStatusCode.Error, write.Status, "Failed write span is Error.");
            TestHelpers.AssertEqual(typeof(InvalidOperationException).FullName, write.GetTagItem(T.ErrorTypeAttribute) as string, "error.type on write span.");
            return Task.CompletedTask;
        }

        private static async Task EventHandlerFailureAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-handler-failure");
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(temp.GetPath("handler.log"));
            log.OnLoggingError += _ => { };
            log.MessageLogged += _ => throw new InvalidOperationException("boom");

            await log.InfoAsync("handler-fail", ct).ConfigureAwait(false);

            TestHelpers.AssertEqual(1, capture.Count(T.EventHandlerDurationMetric,
                TelemetryCapture.Tag(T.EventAttribute, "MessageLogged"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "failure")), "Handler failure duration recorded. " + capture.Dump());
            TestHelpers.AssertEqual(1d, capture.Sum(T.ErrorsMetric,
                TelemetryCapture.Tag(T.ComponentAttribute, "event_handler"),
                TelemetryCapture.Tag(T.ErrorTypeAttribute, typeof(InvalidOperationException).FullName)), "Handler error counted.");
            Activity span = Single(capture.Spans(T.EventHandlerSpan), "handler span");
            TestHelpers.AssertEqual(ActivityStatusCode.Error, span.Status, "Handler span is Error.");
            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric, TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "Handler failure does not fail the entry.");
        }

        private static Task EventHandlerSuccessAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-handler-success");
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(temp.GetPath("handler.log"));
            log.MessageLogged += _ => { };

            log.Info("handler-ok");

            TestHelpers.AssertEqual(1, capture.Count(T.EventHandlerDurationMetric, TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "Handler duration recorded. " + capture.Dump());
            Activity span = Single(capture.Spans(T.EventHandlerSpan), "handler span");
            Activity write = Single(capture.Spans(T.WriteSpan), "write span");
            TestHelpers.AssertEqual(write.SpanId, span.ParentSpanId, "Handler span nests under the write span.");
            return Task.CompletedTask;
        }

        private static Task RetentionRunAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-retention");
            string logFile = temp.GetPath("app.log");
            for (int i = 10; i < 13; i++)
            {
                File.WriteAllText(logFile + "." + DateTime.Now.Date.AddDays(-i).ToString("yyyyMMdd"), "old");
            }
            File.WriteAllText(logFile + "." + DateTime.Now.Date.ToString("yyyyMMdd"), "new");

            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = new LoggingModule(logFile, FileLoggingMode.FileWithDate, false);
            TestHelpers.ConfigureSettings(log, settings => settings.LogRetentionDays = 5);

            MethodInfo? method = typeof(LoggingModule).GetMethod("CleanupOldLogFiles", BindingFlags.Instance | BindingFlags.NonPublic);
            TestHelpers.AssertTrue(method != null, "Expected private CleanupOldLogFiles method to exist.");
            method!.Invoke(log, null);
            capture.CollectObservables();

            TestHelpers.AssertEqual(1d, capture.Sum(T.RetentionRunsMetric, TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "One successful retention run. " + capture.Dump());
            TestHelpers.AssertEqual(1, capture.Count(T.RetentionDurationMetric, TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "Retention duration recorded.");
            TestHelpers.AssertEqual(3d, capture.Sum(T.RetentionFilesDeletedMetric), "Three expired files deleted.");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            TestHelpers.AssertTrue(capture.Measurements(T.RetentionLastSuccessMetric).Any(m => Math.Abs(m.Value - now) < 60), "Last-success gauge is recent.");

            Activity span = Single(capture.Spans(T.RetentionSpan), "retention span");
            TestHelpers.AssertEqual(3, (int)span.GetTagItem(T.FilesDeletedAttribute)!, "Files deleted on span.");
            TestHelpers.AssertEqual(default(ActivitySpanId), span.ParentSpanId, "Retention run is a root span.");
            TestHelpers.AssertEqual(ActivityStatusCode.Ok, span.Status, "Retention span is Ok.");
            return Task.CompletedTask;
        }

        private static async Task LifecycleAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            LoggingModule log = new LoggingModule("telemetry-lifecycle.log", FileLoggingMode.Disabled, false);
            TestHelpers.AssertEqual(1d, capture.Sum(T.ModulesActiveMetric), "Construction adds one active module. " + capture.Dump());

            log.Dispose();
            log.Dispose();
            await log.DisposeAsync().ConfigureAwait(false);
            TestHelpers.AssertEqual(0d, capture.Sum(T.ModulesActiveMetric), "Disposal removes exactly one active module.");

            LoggingModule asyncLog = new LoggingModule("telemetry-lifecycle.log", FileLoggingMode.Disabled, false);
            await asyncLog.DisposeAsync().ConfigureAwait(false);
            TestHelpers.AssertEqual(0d, capture.Sum(T.ModulesActiveMetric), "DisposeAsync also removes the module.");
        }

        private static Task BuildInfoAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = new LoggingModule("telemetry-build.log", FileLoggingMode.Disabled, false);
            capture.CollectObservables();

            CapturedMeasurement info = capture.Measurements(T.BuildInfoMetric).FirstOrDefault()
                ?? throw new InvalidOperationException("Build info gauge not observed. " + capture.Dump());
            TestHelpers.AssertEqual(1d, info.Value, "Build info value is 1.");
            string? version = info.Tags.TryGetValue(T.VersionAttribute, out object? v) ? v as string : null;
            TestHelpers.AssertTrue(!string.IsNullOrEmpty(version) && version != "unknown", "Build info carries the library version.");
            return Task.CompletedTask;
        }

        private static async Task TraceCorrelationAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-correlation");
            string logFile = temp.GetPath("trace.log");

            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(logFile, settings => settings.HeaderFormat = "{trace}|{span}");

            string traceId;
            string spanId;
            using (Activity? parent = TelemetryCapture.TestSource.StartActivity("request"))
            {
                TestHelpers.AssertTrue(parent != null, "Test parent activity should be sampled.");
                traceId = parent!.TraceId.ToHexString();
                spanId = parent.SpanId.ToHexString();
                await log.InfoAsync("correlated", ct).ConfigureAwait(false);
            }

            string contents = TestHelpers.ReadAllText(logFile);
            TestHelpers.AssertContains(contents, traceId + "|" + spanId + " correlated", "Log line carries the caller's trace and span IDs.");

            Activity write = Single(capture.Spans(T.WriteSpan), "write span");
            TestHelpers.AssertEqual(traceId, write.TraceId.ToHexString(), "Write span joins the caller's trace.");
            TestHelpers.AssertEqual(spanId, write.ParentSpanId.ToHexString(), "Write span is a child of the caller's span.");
        }

        private static Task JsonTraceIdsAsync(CancellationToken ct)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using (Activity? parent = TelemetryCapture.TestSource.StartActivity("json"))
            {
                LogEntry entry = new LogEntry(Severity.Info, "json");
                TestHelpers.AssertEqual(parent!.TraceId.ToHexString(), entry.TraceId, "TraceId captured.");
                TestHelpers.AssertEqual(parent.SpanId.ToHexString(), entry.SpanId, "SpanId captured.");
                string json = entry.ToJson();
                TestHelpers.AssertContains(json, "\"traceId\":\"" + entry.TraceId + "\"", "JSON includes traceId.");
                TestHelpers.AssertContains(json, "\"spanId\":\"" + entry.SpanId + "\"", "JSON includes spanId.");
            }

            return Task.CompletedTask;
        }

        private static Task ILoggerPathAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-ilogger");
            using TelemetryCapture capture = new TelemetryCapture();
            LoggingModule module = CreateFileLogger(temp.GetPath("ilogger.log"));
            using SyslogLoggerProvider provider = new SyslogLoggerProvider(module);
            ILogger logger = provider.CreateLogger("Orders.Service");

            logger.LogWarning("ilogger {Value}", 42);

            TestHelpers.AssertEqual(1d, capture.Sum(T.EntriesMetric,
                TelemetryCapture.Tag(T.SeverityAttribute, "Warn"),
                TelemetryCapture.Tag(T.OutcomeAttribute, "success")), "ILogger entry counted. " + capture.Dump());
            Activity write = Single(capture.Spans(T.WriteSpan), "write span");
            TestHelpers.AssertEqual("Orders.Service", write.GetTagItem(T.SourceAttribute) as string, "Category on write span.");
            return Task.CompletedTask;
        }

        private static async Task TogglesAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-toggles");
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = CreateFileLogger(temp.GetPath("toggles.log"), settings =>
            {
                settings.EnableMetrics = false;
                settings.EnableTracing = false;
            });

            log.Info("toggled");
            await log.InfoAsync("toggled-async", ct).ConfigureAwait(false);

            List<CapturedMeasurement> recorded = capture.Measurements(T.EntriesMetric)
                .Concat(capture.Measurements(T.EntryDurationMetric))
                .Concat(capture.Measurements(T.DestinationWritesMetric))
                .Concat(capture.Measurements(T.IoLockWaitMetric))
                .ToList();
            TestHelpers.AssertEqual(0, recorded.Count, "No per-entry metrics when EnableMetrics=false. " + capture.Dump());
            TestHelpers.AssertEqual(0, capture.AllSpans().Count, "No spans when EnableTracing=false.");
            TestHelpers.AssertContains(TestHelpers.ReadAllText(temp.GetPath("toggles.log")), "toggled-async", "Logging still works with telemetry off.");
        }

        private static async Task NoListenerAsync(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("telemetry-nolistener");
            string logFile = temp.GetPath("nolistener.log");
            Activity? previous = Activity.Current;
            Activity.Current = null;
            try
            {
                using LoggingModule log = CreateFileLogger(logFile, settings => settings.HeaderFormat = "[{trace}][{span}]");
                LogEntry entry = new LogEntry(Severity.Info, "no-listener-entry");
                TestHelpers.AssertTrue(entry.TraceId == null && entry.SpanId == null, "No trace IDs without a current Activity.");

                log.Info("no-listener");
                await log.InfoAsync("no-listener-async", ct).ConfigureAwait(false);
                log.LogEntry(entry);

                string contents = TestHelpers.ReadAllText(logFile);
                TestHelpers.AssertContains(contents, "[][] no-listener-async", "Empty trace tokens without a current Activity.");
            }
            finally
            {
                Activity.Current = previous;
            }
        }

        private static async Task NoPayloadAsync(CancellationToken ct)
        {
            const string secret = "SECRET-PAYLOAD-7f3a";
            using UdpCaptureListener listener = new UdpCaptureListener();
            using TelemetryCapture capture = new TelemetryCapture();
            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

            await log.BeginStructuredLog(Severity.Info, secret).WithProperty("Password", secret).WithCorrelationId(secret).WriteAsync(ct).ConfigureAwait(false);
            await listener.ReceiveAsync().ConfigureAwait(false);

            foreach (string name in new[] { T.EntriesMetric, T.EntryDurationMetric, T.DestinationWritesMetric, T.DestinationDurationMetric, T.SyslogSentBytesMetric })
            {
                foreach (CapturedMeasurement m in capture.Measurements(name))
                {
                    TestHelpers.AssertTrue(m.Tags.Values.All(v => !(Convert.ToString(v) ?? string.Empty).Contains(secret)), "Metric tags must not contain payload: " + m);
                }
            }

            foreach (Activity span in capture.AllSpans())
            {
                TestHelpers.AssertTrue(span.TagObjects.All(t => !(Convert.ToString(t.Value) ?? string.Empty).Contains(secret)), "Span tags must not contain payload: " + span.DisplayName);
            }
        }

        private static LoggingModule CreateFileLogger(string logFile, Action<LoggingSettings>? configure = null)
        {
            LoggingModule log = new LoggingModule(logFile, FileLoggingMode.SingleLogFile, false);
            TestHelpers.ConfigureSettings(log, settings =>
            {
                settings.HeaderFormat = "FILE";
                configure?.Invoke(settings);
            });
            return log;
        }

        private static Activity Single(List<Activity> spans, string what)
        {
            TestHelpers.AssertEqual(1, spans.Count, "Expected exactly one " + what + ".");
            return spans[0];
        }
    }
}
