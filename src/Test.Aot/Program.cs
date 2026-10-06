namespace SyslogLogging.Tests.Aot
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using SyslogLogging;

    public static class Program
    {
        private const string FixedPrefix = "{\"timestamp\":\"2026-01-02T03:04:05.678Z\",\"severity\":\"Info\",\"message\":\"aot\",\"threadId\":1";

        // A Native AOT publish has no managed entry assembly on disk. Under "dotnet run" the SDK applies the same
        // feature switches as Native AOT (reflection-based JSON disabled), but the code still runs on the JIT.
        private static readonly string _EntryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Test.Aot";

        private static readonly bool _IsNative = !File.Exists(Path.Combine(AppContext.BaseDirectory, _EntryAssemblyName + ".dll"));

        private static readonly bool _ReflectionDisabled = !JsonSerializer.IsReflectionEnabledByDefault;

        private static string _WorkDirectory = string.Empty;

        public static async Task<int> Main(string[] args)
        {
            bool requireNative = args.Contains("--require-native");

            Console.WriteLine("SyslogLogging Native AOT smoke test");
            Console.WriteLine("  Runtime mode                 : " + (_IsNative ? "Native AOT" : "JIT"));
            Console.WriteLine("  Expected JSON behavior       : " + (_ReflectionDisabled ? "reflection-free" : "reflection-based"));
            Console.WriteLine("  Dynamic code supported       : " + RuntimeFeature.IsDynamicCodeSupported);
            Console.WriteLine("  Reflection JSON enabled      : " + JsonSerializer.IsReflectionEnabledByDefault);
            Console.WriteLine();

            if (requireNative && !_IsNative)
            {
                Console.WriteLine("FAIL: --require-native was specified but the process is running under the JIT.");
                return 2;
            }

            _WorkDirectory = Path.Combine(Path.GetTempPath(), "sysloglogging-aot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_WorkDirectory);

            List<AotCheck> checks = new List<AotCheck>
            {
                new AotCheck("Runtime is consistent (native implies no dynamic code and no reflection JSON)", RuntimeConsistent),
                new AotCheck("ToJson fixed fields", JsonFixedFields),
                new AotCheck("ToJson scalar property values", JsonScalars),
                new AotCheck("ToJson collections and dictionaries", JsonCollections),
                new AotCheck("ToJson complex object (string fallback without reflection)", JsonComplexObject),
                new AotCheck("ToJson non-finite numbers", JsonNonFinite),
                new AotCheck("ToJson(options) with source-generated resolver", JsonSourceGenerated),
                new AotCheck("ToJson(options) without resolver", JsonOptionsWithoutResolver),
                new AotCheck("ToJson exception", JsonException),
                new AotCheck("File logging (sync and async, all severities)", FileLogging),
                new AotCheck("Dated file logging with retention", DatedFileLogging),
                new AotCheck("Syslog UDP delivery and header tokens", SyslogDelivery),
                new AotCheck("Console logging with colors", ConsoleLogging),
                new AotCheck("Structured logging builder and LogEntry", StructuredLogging),
                new AotCheck("Exception logging honors ExceptionSeverity", ExceptionLogging),
                new AotCheck("MessageLogged and OnLoggingError events", Events),
                new AotCheck("Microsoft.Extensions.Logging integration", ExtensionsLogging),
                new AotCheck("Metrics and traces", Telemetry),
                new AotCheck("Concurrent logging", Concurrency),
                new AotCheck("Disposal", Disposal),
            };

            int failed = 0;
            foreach (AotCheck check in checks)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    await check.Execute().ConfigureAwait(false);
                    Console.WriteLine("PASS  " + check.Name + " (" + stopwatch.ElapsedMilliseconds + "ms)");
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("FAIL  " + check.Name + " (" + stopwatch.ElapsedMilliseconds + "ms)");
                    Console.WriteLine("      " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            try
            {
                Directory.Delete(_WorkDirectory, true);
            }
            catch (IOException)
            {
                // Best effort cleanup.
            }

            Console.WriteLine();
            Console.WriteLine("Total: " + checks.Count + "  Passed: " + (checks.Count - failed) + "  Failed: " + failed);
            return failed == 0 ? 0 : 1;
        }

        private static Task RuntimeConsistent()
        {
            if (_IsNative)
            {
                Assert(!RuntimeFeature.IsDynamicCodeSupported, "Native AOT builds must not support dynamic code.");
                Assert(_ReflectionDisabled, "Native AOT builds must disable reflection-based JSON by default.");
            }

            return Task.CompletedTask;
        }

        private static LogEntry CreateEntry()
        {
            LogEntry entry = new LogEntry(Severity.Info, "aot");
            entry.Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);
            entry.ThreadId = 1;
            entry.TraceId = null;
            entry.SpanId = null;
            return entry;
        }

        private static Task JsonFixedFields()
        {
            LogEntry entry = CreateEntry().WithSource("src").WithCorrelationId("cid");
            entry.TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
            entry.SpanId = "00f067aa0ba902b7";
            AssertEqual(
                FixedPrefix + ",\"source\":\"src\",\"correlationId\":\"cid\",\"traceId\":\"4bf92f3577b34da6a3ce929d0e0e4736\",\"spanId\":\"00f067aa0ba902b7\"}",
                entry.ToJson(),
                "Fixed fields");
            return Task.CompletedTask;
        }

        private static Task JsonScalars()
        {
            LogEntry entry = CreateEntry();
            entry.Properties["s"] = "a<b";
            entry.Properties["b"] = true;
            entry.Properties["i"] = -5;
            entry.Properties["l"] = long.MaxValue;
            entry.Properties["ul"] = ulong.MaxValue;
            entry.Properties["d"] = 3.14159;
            entry.Properties["f"] = 0.1f;
            entry.Properties["m"] = 123.4500m;
            entry.Properties["c"] = 'x';
            entry.Properties["dt"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            entry.Properties["dto"] = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5));
            entry.Properties["g"] = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
            entry.Properties["ts"] = TimeSpan.FromSeconds(3661.5);
            entry.Properties["u"] = new Uri("https://example.com/a?b=c");
            entry.Properties["v"] = new Version(1, 2, 3);
            entry.Properties["bytes"] = new byte[] { 1, 2, 3 };
            entry.Properties["e"] = Severity.Critical;
            entry.Properties["do"] = new DateOnly(2026, 3, 4);
            entry.Properties["to"] = new TimeOnly(13, 14, 15);
            entry.Properties["h"] = (Half)1.5;
            entry.Properties["i128"] = (Int128)12345;
            entry.Properties["n"] = null!;

            string expected = FixedPrefix + ",\"properties\":{"
                + "\"s\":\"a\\u003Cb\",\"b\":true,\"i\":-5,\"l\":9223372036854775807,\"ul\":18446744073709551615,"
                + "\"d\":3.14159,\"f\":0.1,\"m\":123.4500,\"c\":\"x\",\"dt\":\"2026-01-02T03:04:05Z\","
                + "\"dto\":\"2026-01-02T03:04:05-05:00\",\"g\":\"0f8fad5b-d9cb-469f-a165-70867728950e\","
                + "\"ts\":\"01:01:01.5000000\",\"u\":\"https://example.com/a?b=c\",\"v\":\"1.2.3\",\"bytes\":\"AQID\","
                + "\"e\":" + (int)Severity.Critical + ",\"do\":\"2026-03-04\",\"to\":\"13:14:15\",\"h\":1.5,\"i128\":12345,\"n\":null}}";

            AssertEqual(expected, entry.ToJson(), "Scalar values");
            return Task.CompletedTask;
        }

        private static Task JsonCollections()
        {
            LogEntry entry = CreateEntry();
            entry.Properties["arr"] = new int[] { 1, 2, 3 };
            entry.Properties["list"] = new List<string> { "x", "y" };
            entry.Properties["objs"] = new object?[] { 1, "two", null, true };
            entry.Properties["dict"] = new Dictionary<string, object> { ["k"] = 1, ["n"] = new Dictionary<string, int> { ["z"] = 9 } };
            entry.Properties["intKeys"] = new Dictionary<int, string> { [1] = "one" };

            AssertEqual(
                FixedPrefix + ",\"properties\":{\"arr\":[1,2,3],\"list\":[\"x\",\"y\"],\"objs\":[1,\"two\",null,true],\"dict\":{\"k\":1,\"n\":{\"z\":9}},\"intKeys\":{\"1\":\"one\"}}}",
                entry.ToJson(),
                "Collections");
            return Task.CompletedTask;
        }

        private static Task JsonComplexObject()
        {
            LogEntry entry = CreateEntry();
            entry.Properties["order"] = new AotOrder();

            string expected = _ReflectionDisabled
                ? FixedPrefix + ",\"properties\":{\"order\":\"AotOrder:widget\"}}"
                : FixedPrefix + ",\"properties\":{\"order\":{\"Name\":\"widget\",\"Quantity\":3,\"Tags\":[\"a\",\"b\"]}}}";

            AssertEqual(expected, entry.ToJson(), "Complex object");
            return Task.CompletedTask;
        }

        private static Task JsonNonFinite()
        {
            LogEntry entry = CreateEntry();
            entry.Properties["nan"] = double.NaN;
            entry.Properties["inf"] = float.PositiveInfinity;
            AssertEqual(FixedPrefix + ",\"properties\":{\"nan\":\"NaN\",\"inf\":\"Infinity\"}}", entry.ToJson(), "Top-level non-finite numbers");

            if (_ReflectionDisabled)
            {
                // Inside collections the reflection-free writer also applies; under the JIT, System.Text.Json number handling applies.
                LogEntry nested = CreateEntry();
                nested.Properties["list"] = new List<double> { 1.0, double.NegativeInfinity };
                AssertEqual(FixedPrefix + ",\"properties\":{\"list\":[1,\"-Infinity\"]}}", nested.ToJson(), "Nested non-finite numbers");
            }

            return Task.CompletedTask;
        }

        private static Task JsonSourceGenerated()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { TypeInfoResolver = AotJsonContext.Default };
            LogEntry entry = CreateEntry();
            entry.Properties["order"] = new AotOrder { Name = "gizmo" };
            entry.Properties["id"] = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

            AssertEqual(
                FixedPrefix + ",\"properties\":{\"order\":{\"Name\":\"gizmo\",\"Quantity\":3,\"Tags\":[\"a\",\"b\"]},\"id\":\"0f8fad5b-d9cb-469f-a165-70867728950e\"}}",
                entry.ToJson(options),
                "Source-generated options");

            JsonSerializerOptions indented = new JsonSerializerOptions { TypeInfoResolver = AotJsonContext.Default, WriteIndented = true };
            AssertContains(entry.ToJson(indented), "\"Name\": \"gizmo\"", "Indented source-generated options");
            return Task.CompletedTask;
        }

        private static Task JsonOptionsWithoutResolver()
        {
            LogEntry entry = CreateEntry();
            entry.Properties["i"] = 7;
            entry.Properties["order"] = new AotOrder();

            string json = entry.ToJson(new JsonSerializerOptions());
            AssertContains(json, "\"i\":7", "Options without resolver must still write scalars.");
            AssertContains(json, _ReflectionDisabled ? "\"order\":\"AotOrder:widget\"" : "\"order\":{\"Name\":\"widget\"", "Options without resolver complex value");
            return Task.CompletedTask;
        }

        private static Task JsonException()
        {
            LogEntry entry = CreateEntry();
            try
            {
                throw new InvalidOperationException("native boom");
            }
            catch (InvalidOperationException ex)
            {
                entry.Exception = ex;
            }

            string json = entry.ToJson();
            AssertContains(json, "\"exception\":{\"type\":\"System.InvalidOperationException\",\"message\":\"native boom\",\"stackTrace\":", "Exception JSON");
            using JsonDocument document = JsonDocument.Parse(json);
            AssertEqual("native boom", document.RootElement.GetProperty("exception").GetProperty("message").GetString(), "Exception JSON parses");
            return Task.CompletedTask;
        }

        private static async Task FileLogging()
        {
            string path = Path.Combine(_WorkDirectory, "file.log");
            using (LoggingModule log = new LoggingModule(path, FileLoggingMode.SingleLogFile, false))
            {
                log.Debug("sync-debug");
                log.Info("sync-info");
                log.Warn("sync-warn");
                log.Error("sync-error");
                log.Alert("sync-alert");
                log.Critical("sync-critical");
                log.Emergency("sync-emergency");
                await log.DebugAsync("async-debug").ConfigureAwait(false);
                await log.InfoAsync("async-info").ConfigureAwait(false);
                await log.WarnAsync("async-warn").ConfigureAwait(false);
                await log.ErrorAsync("async-error").ConfigureAwait(false);
                await log.AlertAsync("async-alert").ConfigureAwait(false);
                await log.CriticalAsync("async-critical").ConfigureAwait(false);
                await log.EmergencyAsync("async-emergency").ConfigureAwait(false);
                await log.LogAsync(Severity.Info, "async-log").ConfigureAwait(false);
                await log.FlushAsync().ConfigureAwait(false);
            }

            string content = File.ReadAllText(path);
            foreach (string expected in new[] { "sync-debug", "sync-emergency", "async-debug", "async-emergency", "async-log" })
            {
                AssertContains(content, expected, "File content");
            }
        }

        private static async Task DatedFileLogging()
        {
            string path = Path.Combine(_WorkDirectory, "dated.log");
            using (LoggingModule log = new LoggingModule(path, FileLoggingMode.FileWithDate, false))
            {
                LoggingSettings settings = log.Settings;
                settings.LogRetentionDays = 7;
                log.Settings = settings;
                await log.InfoAsync("dated-entry").ConfigureAwait(false);
            }

            string[] files = Directory.GetFiles(_WorkDirectory, "dated.log*");
            Assert(files.Length == 1, "Expected exactly one dated log file, found " + files.Length + ".");
            AssertContains(File.ReadAllText(files[0]), "dated-entry", "Dated file content");
        }

        private static async Task SyslogDelivery()
        {
            using UdpClient listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

            using (LoggingModule log = new LoggingModule("127.0.0.1", port, false))
            {
                LoggingSettings settings = log.Settings;
                settings.HeaderFormat = "{app}|{pid}|{host}|{sev}|{source}|{correlation}";
                log.Settings = settings;

                await log.LogEntryAsync(new LogEntry(Severity.Warn, "udp-async").WithSource("Udp").WithCorrelationId("c-1")).ConfigureAwait(false);
                string first = await ReceiveAsync(listener).ConfigureAwait(false);
                AssertContains(first, _EntryAssemblyName + "|" + Environment.ProcessId + "|", "Header {app}/{pid}");
                AssertContains(first, "|Warn|Udp|c-1 udp-async", "Header {sev}/{source}/{correlation}");

                log.Error("udp-sync");
                string second = await ReceiveAsync(listener).ConfigureAwait(false);
                AssertContains(second, "udp-sync", "Sync syslog payload");
            }
        }

        private static async Task<string> ReceiveAsync(UdpClient listener)
        {
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            UdpReceiveResult result = await listener.ReceiveAsync(cts.Token).ConfigureAwait(false);
            return Encoding.UTF8.GetString(result.Buffer);
        }

        private static async Task ConsoleLogging()
        {
            TextWriter original = Console.Out;
            StringWriter captured = new StringWriter();
            Console.SetOut(captured);
            try
            {
                using LoggingModule log = new LoggingModule(Path.Combine(_WorkDirectory, "console.log"), FileLoggingMode.Disabled, true);
                LoggingSettings settings = log.Settings;
                settings.EnableColors = true;
                log.Settings = settings;

                log.Info("console-sync");
                await log.ErrorAsync("console-async").ConfigureAwait(false);
            }
            finally
            {
                Console.SetOut(original);
            }

            string output = captured.ToString();
            AssertContains(output, "console-sync", "Console sync output");
            AssertContains(output, "console-async", "Console async output");
        }

        private static async Task StructuredLogging()
        {
            string path = Path.Combine(_WorkDirectory, "structured.log");
            ConcurrentQueue<LogEntry> entries = new ConcurrentQueue<LogEntry>();

            using (LoggingModule log = new LoggingModule(path, FileLoggingMode.SingleLogFile, false))
            {
                log.MessageLogged += entries.Enqueue;

                await log.BeginStructuredLog(Severity.Info, "structured-async")
                    .WithProperty("UserId", 42)
                    .WithProperty("Order", new AotOrder())
                    .WithCorrelationId("corr-1")
                    .WithSource("Builder")
                    .WriteAsync()
                    .ConfigureAwait(false);

                log.BeginStructuredLog(Severity.Warn, "structured-sync")
                    .WithProperties(new Dictionary<string, object?> { ["A"] = 1, ["B"] = "two" })
                    .Write();

                log.LogEntry(new LogEntry(Severity.Error, "entry-sync").WithProperty("Amount", 9.5m));
            }

            Assert(entries.Count == 3, "Expected three MessageLogged entries, found " + entries.Count + ".");
            LogEntry first = entries.First();
            AssertEqual("corr-1", first.CorrelationId, "Builder correlation id");
            AssertContains(first.ToJson(), "\"UserId\":42", "Builder property JSON");
            AssertContains(File.ReadAllText(path), "structured-async", "Structured file output");
        }

        private static async Task ExceptionLogging()
        {
            List<LogEntry> entries = new List<LogEntry>();
            using LoggingModule log = new LoggingModule(Path.Combine(_WorkDirectory, "exception.log"), FileLoggingMode.SingleLogFile, false);
            LoggingSettings settings = log.Settings;
            settings.ExceptionSeverity = Severity.Critical;
            log.Settings = settings;
            log.MessageLogged += entries.Add;

            InvalidOperationException exception = new InvalidOperationException("exception-case");
            log.Exception(exception, "Module", "Method");
            await log.ExceptionAsync(exception, "Module", "MethodAsync").ConfigureAwait(false);

            Assert(entries.Count == 2, "Expected two exception entries, found " + entries.Count + ".");
            Assert(entries.All(e => e.Severity == Severity.Critical), "Exception entries must honor ExceptionSeverity.");
        }

        private static async Task Events()
        {
            int logged = 0;
            int errors = 0;
            using LoggingModule log = new LoggingModule(Path.Combine(_WorkDirectory, "events.log"), FileLoggingMode.SingleLogFile, false);
            log.MessageLogged += _ => Interlocked.Increment(ref logged);
            log.MessageLogged += _ => throw new InvalidOperationException("handler failure");
            log.OnLoggingError += _ => Interlocked.Increment(ref errors);

            await log.InfoAsync("event-1").ConfigureAwait(false);
            log.Info("event-2");

            AssertEqual(2, logged, "MessageLogged count");
            Assert(errors >= 2, "OnLoggingError must observe handler failures.");
        }

        private static Task ExtensionsLogging()
        {
            string path = Path.Combine(_WorkDirectory, "mel.log");
            using UdpClient listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

            using (ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddFileLogging(path, FileLoggingMode.SingleLogFile, false);
                builder.AddSyslog("127.0.0.1", port, false);
            }))
            {
                ILogger logger = factory.CreateLogger("Aot.Category");
                logger.LogInformation("User {UserId} logged in from {Address}", 42, "10.0.0.1");
                logger.LogError(new EventId(7, "Failure"), new InvalidOperationException("mel-boom"), "Operation {Name} failed", "sync");
                using (logger.BeginScope("scope"))
                {
                    logger.LogWarning("scoped-warning");
                }
            }

            string content = File.ReadAllText(path);
            AssertContains(content, "User 42 logged in from 10.0.0.1", "ILogger file output");
            AssertContains(content, "Operation sync failed", "ILogger error output");
            AssertContains(content, "scoped-warning", "ILogger scoped output");

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            UdpReceiveResult result = listener.ReceiveAsync(cts.Token).AsTask().GetAwaiter().GetResult();
            AssertContains(Encoding.UTF8.GetString(result.Buffer), "User 42 logged in", "ILogger syslog output");
            return Task.CompletedTask;
        }

        private static async Task Telemetry()
        {
            ConcurrentDictionary<string, long> counters = new ConcurrentDictionary<string, long>();
            ConcurrentQueue<string> spans = new ConcurrentQueue<string>();

            using MeterListener meterListener = new MeterListener();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SyslogLoggingTelemetry.MeterName) listener.EnableMeasurementEvents(instrument);
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => counters.AddOrUpdate(instrument.Name, value, (_, existing) => existing + value));
            meterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => counters.AddOrUpdate(instrument.Name, value, (_, existing) => existing + value));
            meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => counters.AddOrUpdate(instrument.Name, 1, (_, existing) => existing + 1));
            meterListener.Start();

            using ActivityListener activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == SyslogLoggingTelemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => spans.Enqueue(activity.DisplayName),
            };
            ActivitySource.AddActivityListener(activityListener);

            using (LoggingModule log = new LoggingModule(Path.Combine(_WorkDirectory, "telemetry.log"), FileLoggingMode.SingleLogFile, false))
            {
                await log.InfoAsync("telemetry").ConfigureAwait(false);
                log.Warn("telemetry-sync");
            }

            meterListener.RecordObservableInstruments();

            Assert(counters.TryGetValue(SyslogLoggingTelemetry.EntriesMetric, out long entries) && entries >= 2, "Entries counter must record writes.");
            Assert(counters.ContainsKey(SyslogLoggingTelemetry.EntryDurationMetric), "Entry duration histogram must record writes.");
            Assert(counters.ContainsKey(SyslogLoggingTelemetry.DestinationWritesMetric), "Destination writes counter must record writes.");
            Assert(spans.Contains(SyslogLoggingTelemetry.WriteSpan), "Write span must be emitted.");
            Assert(spans.Contains(SyslogLoggingTelemetry.FileWriteSpan), "File write span must be emitted.");
        }

        private static async Task Concurrency()
        {
            string path = Path.Combine(_WorkDirectory, "concurrent.log");
            using (LoggingModule log = new LoggingModule(path, FileLoggingMode.SingleLogFile, false))
            {
                Task[] tasks = Enumerable.Range(0, 16).Select(i => Task.Run(async () =>
                {
                    for (int j = 0; j < 50; j++)
                    {
                        if (j % 2 == 0) log.Info("concurrent-" + i + "-" + j);
                        else await log.InfoAsync("concurrent-" + i + "-" + j).ConfigureAwait(false);
                    }
                })).ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            int count = File.ReadAllLines(path).Count(line => line.Contains("concurrent-", StringComparison.Ordinal));
            AssertEqual(800, count, "Concurrent line count");
        }

        private static async Task Disposal()
        {
            LoggingModule log = new LoggingModule(Path.Combine(_WorkDirectory, "dispose.log"), FileLoggingMode.FileWithDate, false);
            await log.DisposeAsync().ConfigureAwait(false);
            log.Dispose();

            bool threw = false;
            try
            {
                log.Info("after-dispose");
            }
            catch (ObjectDisposedException)
            {
                threw = true;
            }

            Assert(threw, "Logging after dispose must throw ObjectDisposedException.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + ": expected '" + expected + "' but found '" + actual + "'.");
        }

        private static void AssertContains(string value, string expected, string message)
        {
            if (value == null || !value.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException(message + ": expected to find '" + expected + "' in '" + value + "'.");
        }
    }
}
