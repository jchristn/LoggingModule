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

    public static class LoggingModuleSuites
    {
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    SettingsSuite(),
                    SeveritySuite(),
                    FileLoggingSuite(),
                    StructuredSuite(),
                    FluentSuite(),
                    ExceptionSuite(),
                    RetentionSuite(),
                    OrderingSuite(),
                    ThreadSafetySuite(),
                    SyslogSuite(),
                    IntegrationSuite(),
                    MessageLoggedSuite(),
                };
            }
        }

        public static TestSuiteDescriptor SettingsSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Settings",
                displayName: "Settings and Construction",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ApplicationNameOverride",
                        displayName: "Configured ApplicationName overrides the default app name",
                        executeAsync: async ct =>
                        {
                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

                            TestHelpers.ConfigureSettings(log, settings =>
                            {
                                settings.HeaderFormat = "{app}";
                                settings.ApplicationName = "CustomApp";
                            });

                            await log.InfoAsync("override-case", ct).ConfigureAwait(false);
                            string message = await listener.ReceiveAsync().ConfigureAwait(false);

                            TestHelpers.AssertContains(
                                message,
                                "CustomApp override-case",
                                "Configured application name should appear in the syslog payload.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ApplicationNameDefault",
                        displayName: "Null ApplicationName falls back to the entry assembly name",
                        executeAsync: async ct =>
                        {
                            string expected = TestHelpers.GetExpectedDefaultApplicationName();

                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

                            TestHelpers.ConfigureSettings(log, settings =>
                            {
                                settings.HeaderFormat = "{app}";
                                settings.ApplicationName = null;
                            });

                            await log.InfoAsync("default-case", ct).ConfigureAwait(false);
                            string message = await listener.ReceiveAsync().ConfigureAwait(false);

                            TestHelpers.AssertContains(
                                message,
                                expected + " default-case",
                                "Default application name should resolve from the entry assembly or process fallback.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ApplicationNameWhitespace",
                        displayName: "Whitespace ApplicationName clears the override",
                        executeAsync: async ct =>
                        {
                            string expected = TestHelpers.GetExpectedDefaultApplicationName();

                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

                            TestHelpers.ConfigureSettings(log, settings =>
                            {
                                settings.HeaderFormat = "{app}";
                                settings.ApplicationName = "InitialOverride";
                            });

                            log.Settings.ApplicationName = "   ";

                            await log.InfoAsync("whitespace-case", ct).ConfigureAwait(false);
                            string message = await listener.ReceiveAsync().ConfigureAwait(false);

                            TestHelpers.AssertContains(
                                message,
                                expected + " whitespace-case",
                                "Whitespace should reset ApplicationName to the default resolver.");
                            TestHelpers.AssertDoesNotContain(
                                message,
                                "InitialOverride whitespace-case",
                                "Whitespace reset should remove the prior override.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ApplicationNameRuntimeChange",
                        displayName: "ApplicationName changes take effect without reassigning Settings",
                        executeAsync: async ct =>
                        {
                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

                            TestHelpers.ConfigureSettings(log, settings =>
                            {
                                settings.HeaderFormat = "{app}";
                                settings.ApplicationName = "FirstName";
                            });

                            await log.InfoAsync("first-message", ct).ConfigureAwait(false);
                            string firstMessage = await listener.ReceiveAsync().ConfigureAwait(false);

                            log.Settings.ApplicationName = "SecondName";

                            await log.InfoAsync("second-message", ct).ConfigureAwait(false);
                            string secondMessage = await listener.ReceiveAsync().ConfigureAwait(false);

                            TestHelpers.AssertContains(firstMessage, "FirstName first-message", "Initial application name should be used.");
                            TestHelpers.AssertContains(secondMessage, "SecondName second-message", "Updated application name should be used.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "DefaultConstructor",
                        displayName: "Default constructor initializes the default loopback syslog server",
                        executeAsync: _ =>
                        {
                            using LoggingModule log = new LoggingModule();

                            TestHelpers.AssertEqual(1, log.Servers.Count, "Default constructor should create one syslog server.");
                            TestHelpers.AssertEqual("127.0.0.1", log.Servers[0].Hostname, "Default server hostname should be loopback.");
                            TestHelpers.AssertEqual(514, log.Servers[0].Port, "Default syslog port should be 514.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "SingleServerConstructor",
                        displayName: "Single-server constructor stores the provided endpoint",
                        executeAsync: _ =>
                        {
                            int port = PortAllocator.GetEphemeralPort();

                            using LoggingModule log = new LoggingModule("127.0.0.1", port, false);

                            TestHelpers.AssertEqual(1, log.Servers.Count, "Expected a single configured server.");
                            TestHelpers.AssertEqual("127.0.0.1", log.Servers[0].Hostname, "Configured hostname should match.");
                            TestHelpers.AssertEqual(port, log.Servers[0].Port, "Configured port should match.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "MultipleServerConstructor",
                        displayName: "Multi-server constructor preserves the supplied list",
                        executeAsync: _ =>
                        {
                            int firstPort = PortAllocator.GetEphemeralPort();
                            int secondPort = GetDistinctPort(firstPort);
                            List<SyslogServer> servers = new List<SyslogServer>
                            {
                                new SyslogServer("127.0.0.1", firstPort),
                                new SyslogServer("127.0.0.1", secondPort),
                            };

                            using LoggingModule log = new LoggingModule(servers, false);

                            TestHelpers.AssertEqual(2, log.Servers.Count, "Expected two configured servers.");
                            TestHelpers.AssertEqual(firstPort, log.Servers[0].Port, "First server port should match.");
                            TestHelpers.AssertEqual(secondPort, log.Servers[1].Port, "Second server port should match.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "FileConstructor",
                        displayName: "File-only constructor sets file logging settings",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-constructor");
                            string logFile = temp.GetPath("constructor.log");

                            using LoggingModule log = new LoggingModule(logFile, FileLoggingMode.SingleLogFile, false);

                            TestHelpers.AssertEqual(logFile, log.Settings.LogFilename, "Configured log filename should match.");
                            TestHelpers.AssertEqual(FileLoggingMode.SingleLogFile, log.Settings.FileLogging, "Configured file logging mode should match.");
                            TestHelpers.AssertEqual(0, log.Servers.Count, "File-only constructor should not configure syslog servers.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ConstructorValidation",
                        displayName: "Constructors reject invalid arguments",
                        executeAsync: _ =>
                        {
                            int ephemeralPort = PortAllocator.GetEphemeralPort();

                            TestHelpers.ExpectThrows<ArgumentNullException>(
                                () => new LoggingModule((string)null!, ephemeralPort),
                                "hostname");

                            TestHelpers.ExpectThrows<ArgumentException>(
                                () => new LoggingModule("127.0.0.1", -1),
                                "port");

                            TestHelpers.ExpectThrows<ArgumentNullException>(
                                () => new LoggingModule((List<SyslogServer>)null!),
                                "servers");

                            TestHelpers.ExpectThrows<ArgumentException>(
                                () => new LoggingModule(new List<SyslogServer>()),
                                "servers");

                            TestHelpers.ExpectThrows<ArgumentNullException>(
                                () => new LoggingModule((string)null!, FileLoggingMode.SingleLogFile),
                                "filename");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ServersAreDeduplicated",
                        displayName: "Servers setter removes duplicate ip:port values",
                        executeAsync: _ =>
                        {
                            int firstPort = PortAllocator.GetEphemeralPort();
                            int secondPort = GetDistinctPort(firstPort);

                            using LoggingModule log = new LoggingModule(
                                new List<SyslogServer> { new SyslogServer("127.0.0.1", firstPort) },
                                false);

                            log.Servers = new List<SyslogServer>
                            {
                                new SyslogServer("127.0.0.1", firstPort),
                                new SyslogServer("127.0.0.1", firstPort),
                                new SyslogServer("127.0.0.1", secondPort),
                            };

                            TestHelpers.AssertEqual(2, log.Servers.Count, "Duplicate syslog servers should be collapsed.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ServersGetterReturnsCopy",
                        displayName: "Servers getter returns a defensive copy",
                        executeAsync: _ =>
                        {
                            int port = PortAllocator.GetEphemeralPort();

                            using LoggingModule log = new LoggingModule("127.0.0.1", port, false);
                            List<SyslogServer> servers = log.Servers;
                            servers.Clear();

                            TestHelpers.AssertEqual(1, log.Servers.Count, "Modifying the returned server list should not mutate logger state.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "TimestampFormatValidation",
                        displayName: "TimestampFormat rejects null and empty values",
                        executeAsync: _ =>
                        {
                            LoggingSettings settings = new LoggingSettings();

                            TestHelpers.ExpectThrows<ArgumentException>(() => settings.TimestampFormat = null!, "TimestampFormat");
                            TestHelpers.ExpectThrows<ArgumentException>(() => settings.TimestampFormat = string.Empty, "TimestampFormat");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ColorValidation",
                        displayName: "Colors rejects null",
                        executeAsync: _ =>
                        {
                            LoggingSettings settings = new LoggingSettings();

                            TestHelpers.ExpectThrows<ArgumentNullException>(() => settings.Colors = null!, "Colors");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "MaxMessageLengthValidation",
                        displayName: "MaxMessageLength rejects values below 32",
                        executeAsync: _ =>
                        {
                            LoggingSettings settings = new LoggingSettings();

                            TestHelpers.ExpectThrows<ArgumentOutOfRangeException>(() => settings.MaxMessageLength = 31, "MaxMessageLength");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "LogFilenameCreatesDirectory",
                        displayName: "LogFilename creates a missing directory",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("settings");
                            string nestedFile = temp.GetPath(Path.Combine("nested", "logs", "app.log"));

                            LoggingSettings settings = new LoggingSettings
                            {
                                LogFilename = nestedFile,
                            };

                            string? directory = Path.GetDirectoryName(settings.LogFilename);
                            TestHelpers.AssertTrue(!string.IsNullOrEmpty(directory) && Directory.Exists(directory), "Setting LogFilename should create the target directory.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "LogRetentionDaysDefault",
                        displayName: "LogRetentionDays defaults to zero",
                        executeAsync: _ =>
                        {
                            LoggingSettings settings = new LoggingSettings();
                            TestHelpers.AssertEqual(0, settings.LogRetentionDays, "Default retention should be zero.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "LogRetentionDaysPositive",
                        displayName: "LogRetentionDays stores positive values",
                        executeAsync: _ =>
                        {
                            LoggingSettings settings = new LoggingSettings
                            {
                                LogRetentionDays = 30,
                            };

                            TestHelpers.AssertEqual(30, settings.LogRetentionDays, "Positive retention should be preserved.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "LogRetentionDaysNegative",
                        displayName: "LogRetentionDays coerces negative values to zero",
                        executeAsync: _ =>
                        {
                            LoggingSettings settings = new LoggingSettings
                            {
                                LogRetentionDays = -5,
                            };

                            TestHelpers.AssertEqual(0, settings.LogRetentionDays, "Negative retention should be coerced to zero.");

                            return Task.CompletedTask;
                        }),
                });
        }

        public static TestSuiteDescriptor SeveritySuite()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.AddRange(CreateSyncSeverityCases());
            cases.AddRange(CreateAsyncSeverityCases());

            cases.Add(
                new TestCaseDescriptor(
                    suiteId: "Severity",
                    caseId: "GenericLogSync",
                    displayName: "Log writes the requested severity synchronously",
                    executeAsync: _ =>
                    {
                        using TemporaryDirectory temp = new TemporaryDirectory("severity-generic-sync");
                        string logFile = temp.GetPath("generic-sync.log");

                        using LoggingModule log = CreateFileLogger(logFile, "{sev}|{level}");
                        log.Log(Severity.Critical, "generic-sync");

                        string contents = TestHelpers.ReadAllText(logFile);
                        TestHelpers.AssertContains(contents, "Critical|5 generic-sync", "Generic sync log should preserve requested severity.");

                        return Task.CompletedTask;
                    }));

            cases.Add(
                new TestCaseDescriptor(
                    suiteId: "Severity",
                    caseId: "GenericLogAsync",
                    displayName: "LogAsync writes the requested severity asynchronously",
                    executeAsync: async ct =>
                    {
                        using TemporaryDirectory temp = new TemporaryDirectory("severity-generic-async");
                        string logFile = temp.GetPath("generic-async.log");

                        using LoggingModule log = CreateFileLogger(logFile, "{sev}|{level}");
                        await log.LogAsync(Severity.Emergency, "generic-async", ct).ConfigureAwait(false);

                        string contents = TestHelpers.ReadAllText(logFile);
                        TestHelpers.AssertContains(contents, "Emergency|6 generic-async", "Generic async log should preserve requested severity.");
                    }));

            cases.Add(
                new TestCaseDescriptor(
                    suiteId: "Severity",
                    caseId: "NullMessageSync",
                    displayName: "Null sync messages are ignored",
                    executeAsync: _ =>
                    {
                        using TemporaryDirectory temp = new TemporaryDirectory("null-sync");
                        string logFile = temp.GetPath("null-sync.log");

                        using LoggingModule log = CreateFileLogger(logFile);
                        log.Info(null!);

                        TestHelpers.AssertTrue(!File.Exists(logFile), "Null sync messages should not create a log file.");

                        return Task.CompletedTask;
                    }));

            cases.Add(
                new TestCaseDescriptor(
                    suiteId: "Severity",
                    caseId: "NullMessageAsync",
                    displayName: "Null async messages are ignored",
                    executeAsync: async ct =>
                    {
                        using TemporaryDirectory temp = new TemporaryDirectory("null-async");
                        string logFile = temp.GetPath("null-async.log");

                        using LoggingModule log = CreateFileLogger(logFile);
                        await log.InfoAsync(null!, ct).ConfigureAwait(false);

                        TestHelpers.AssertTrue(!File.Exists(logFile), "Null async messages should not create a log file.");
                    }));

            cases.Add(
                new TestCaseDescriptor(
                    suiteId: "Severity",
                    caseId: "EmptyMessageSync",
                    displayName: "Empty sync messages are ignored",
                    executeAsync: _ =>
                    {
                        using TemporaryDirectory temp = new TemporaryDirectory("empty-sync");
                        string logFile = temp.GetPath("empty-sync.log");

                        using LoggingModule log = CreateFileLogger(logFile);
                        log.Info(string.Empty);

                        TestHelpers.AssertTrue(!File.Exists(logFile), "Empty sync messages should not create a log file.");

                        return Task.CompletedTask;
                    }));

            cases.Add(
                new TestCaseDescriptor(
                    suiteId: "Severity",
                    caseId: "EmptyMessageAsync",
                    displayName: "Empty async messages are ignored",
                    executeAsync: async ct =>
                    {
                        using TemporaryDirectory temp = new TemporaryDirectory("empty-async");
                        string logFile = temp.GetPath("empty-async.log");

                        using LoggingModule log = CreateFileLogger(logFile);
                        await log.InfoAsync(string.Empty, ct).ConfigureAwait(false);

                        TestHelpers.AssertTrue(!File.Exists(logFile), "Empty async messages should not create a log file.");
                    }));

            return new TestSuiteDescriptor(
                suiteId: "Severity",
                displayName: "Severity Convenience Methods",
                cases: cases);
        }

        public static TestSuiteDescriptor FileLoggingSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "File",
                displayName: "File Logging",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "InfoWritesToFile",
                        displayName: "Info writes to a single log file",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-basic");
                            string logFile = temp.GetPath("basic.log");

                            using LoggingModule log = CreateFileLogger(logFile, "FILE");
                            await log.InfoAsync("basic-file-case", ct).ConfigureAwait(false);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "FILE basic-file-case", "The file log should contain the configured header and message.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "MinimumSeverityFilters",
                        displayName: "MinimumSeverity filters messages below the threshold",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-severity");
                            string logFile = temp.GetPath("severity.log");

                            using LoggingModule log = CreateFileLogger(logFile, "FILE", settings => settings.MinimumSeverity = Severity.Error);
                            await log.InfoAsync("filtered-info", ct).ConfigureAwait(false);
                            await log.ErrorAsync("accepted-error", ct).ConfigureAwait(false);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertDoesNotContain(contents, "filtered-info", "Messages below the minimum severity should not be written.");
                            TestHelpers.AssertContains(contents, "accepted-error", "Messages at or above the minimum severity should be written.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "FileWithDateWritesToDatedFile",
                        displayName: "FileWithDate writes to a dated filename",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-dated");
                            string baseLogFile = temp.GetPath("dated.log");

                            using LoggingModule log = new LoggingModule(baseLogFile, FileLoggingMode.FileWithDate, false);
                            TestHelpers.ConfigureSettings(log, settings => settings.HeaderFormat = "DATED");
                            log.Info("dated-message");

                            string expectedFile = GetDatedLogFilePath(baseLogFile, DateTime.Now);
                            string contents = TestHelpers.ReadAllText(expectedFile);
                            TestHelpers.AssertContains(contents, "DATED dated-message", "Dated file logging should write to the expected file.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "HeaderFormatEmptyStillWritesMessage",
                        displayName: "Empty HeaderFormat still writes the message body",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-empty-header");
                            string logFile = temp.GetPath("empty-header.log");

                            using LoggingModule log = CreateFileLogger(logFile, "INITIAL");
                            TestHelpers.ConfigureSettings(log, settings => settings.HeaderFormat = string.Empty);
                            log.Info("headerless-message");

                            string contents = TestHelpers.ReadAllText(logFile).Trim();
                            TestHelpers.AssertTrue(contents.EndsWith("headerless-message", StringComparison.Ordinal), "Message body should still be written when HeaderFormat is empty.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "HeaderVariablesExpand",
                        displayName: "Header variables expand into the formatted output",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-variables");
                            string logFile = temp.GetPath("variables.log");

                            using LoggingModule log = CreateFileLogger(
                                logFile,
                                "{host}|{thread}|{sev}|{level}|{pid}|{user}|{source}|{correlation}|{app}",
                                settings => settings.ApplicationName = "HeaderApp");

                            LogEntry entry = new LogEntry(Severity.Warn, "variables-message")
                            {
                                ThreadId = 321,
                            };
                            entry.WithSource("HeaderSource");
                            entry.WithCorrelationId("HeaderCorrelation");

                            log.LogEntry(entry);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, Environment.MachineName, "Host variable should expand.");
                            TestHelpers.AssertContains(contents, "|321|Warn|2|", "Thread, severity, and numeric level should expand.");
                            TestHelpers.AssertContains(contents, "|" + Process.GetCurrentProcess().Id + "|", "Process id should expand.");
                            TestHelpers.AssertContains(contents, Environment.UserName, "User variable should expand.");
                            TestHelpers.AssertContains(contents, "HeaderSource|HeaderCorrelation|HeaderApp variables-message", "Source, correlation, app, and message should expand.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "UtcTimestampFormatting",
                        displayName: "UseUtcTime=true formats timestamps in UTC",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-utc-ts");
                            string logFile = temp.GetPath("utc.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{ts}", settings =>
                            {
                                settings.TimestampFormat = "yyyyMMddHHmmss";
                                settings.UseUtcTime = true;
                            });

                            LogEntry entry = new LogEntry(Severity.Info, "utc-message")
                            {
                                Timestamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                            };

                            log.LogEntry(entry);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "20250102030405 utc-message", "UTC timestamp formatting should use the UTC clock.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "LocalTimestampFormatting",
                        displayName: "UseUtcTime=false formats timestamps in local time",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-local-ts");
                            string logFile = temp.GetPath("local.log");

                            DateTime timestamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                            string expected = timestamp.ToLocalTime().ToString("yyyyMMddHHmmss");

                            using LoggingModule log = CreateFileLogger(logFile, "{ts}", settings =>
                            {
                                settings.TimestampFormat = "yyyyMMddHHmmss";
                                settings.UseUtcTime = false;
                            });

                            LogEntry entry = new LogEntry(Severity.Info, "local-message")
                            {
                                Timestamp = timestamp,
                            };

                            log.LogEntry(entry);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, expected + " local-message", "Local timestamp formatting should use local time.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "StructuredLogWritesContext",
                        displayName: "Structured LogEntry writes properties, source, correlation, and app name",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-structured");
                            string logFile = temp.GetPath("structured.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{source}|{correlation}|{app}", settings => settings.ApplicationName = "StructApp");

                            LogEntry entry = new LogEntry(Severity.Warn, "structured-message")
                                .WithProperty("OrderId", 42)
                                .WithCorrelationId("corr-123")
                                .WithSource("PaymentService");

                            await log.LogEntryAsync(entry, ct).ConfigureAwait(false);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "PaymentService|corr-123|StructApp [OrderId=42] structured-message", "Structured log output should contain header context and property rendering.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "LargeMessagesSplit",
                        displayName: "Large messages split into multiple file entries with sequence metadata",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-split");
                            string logFile = temp.GetPath("split.log");

                            using LoggingModule log = CreateFileLogger(logFile, "SPLIT", settings => settings.MaxMessageLength = 32);
                            log.Info(new string('X', 96));

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertTrue(lines.Length >= 3, "Large messages should be split across multiple lines.");
                            TestHelpers.AssertContains(lines[0], "Sequence=1", "First split line should include sequence metadata.");
                            TestHelpers.AssertContains(lines[1], "Sequence=2", "Second split line should include sequence metadata.");
                            TestHelpers.AssertContains(lines[0], "IsSplit=True", "Split lines should indicate split state.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "DeepSplitDoesNotCrash",
                        displayName: "Deep message splitting completes without recursion failures",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-deep-split");
                            string logFile = temp.GetPath("deep-split.log");

                            using LoggingModule log = CreateFileLogger(logFile, "DEEP", settings => settings.MaxMessageLength = 32);
                            log.Info(new string('R', 3200));

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertTrue(lines.Length >= 100, "Configured message splitting should generate many lines without crashing.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "SplitPreservesSourceAndCorrelation",
                        displayName: "Split structured messages preserve source and correlation metadata",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-split-structured");
                            string logFile = temp.GetPath("split-structured.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{source}|{correlation}", settings => settings.MaxMessageLength = 32);

                            LogEntry entry = new LogEntry(Severity.Info, new string('S', 128))
                                .WithSource("SplitSource")
                                .WithCorrelationId("SplitCorrelation");

                            log.LogEntry(entry);

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertTrue(lines.Length >= 4, "Split structured message should produce multiple lines.");
                            TestHelpers.AssertTrue(lines.All(line => line.Contains("SplitSource|SplitCorrelation", StringComparison.Ordinal)), "Every split line should preserve source and correlation metadata.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "FlushAsyncCompletes",
                        displayName: "FlushAsync completes without additional side effects",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-flush");
                            string logFile = temp.GetPath("flush.log");

                            using LoggingModule log = CreateFileLogger(logFile, "FLUSH");
                            await log.InfoAsync("before-flush", ct).ConfigureAwait(false);
                            await log.FlushAsync(ct).ConfigureAwait(false);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "FLUSH before-flush", "FlushAsync should not lose previously written messages.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "LogEntryAsyncWritesToFile",
                        displayName: "LogEntryAsync writes asynchronously to file output",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("file-async-entry");
                            string logFile = temp.GetPath("entry.log");

                            using LoggingModule log = CreateFileLogger(logFile, "ASYNC");

                            LogEntry entry = new LogEntry(Severity.Error, "async-entry-message");
                            await log.LogEntryAsync(entry, ct).ConfigureAwait(false);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "ASYNC async-entry-message", "Async LogEntry writes should reach the file target.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "File",
                        caseId: "WriteFailuresRaiseLoggingError",
                        displayName: "Write failures trigger OnLoggingError without throwing to the caller",
                        executeAsync: _ =>
                        {
                            Exception? captured = null;

                            using TemporaryDirectory temp = new TemporaryDirectory("logging-error");
                            using LoggingModule log = new LoggingModule(temp.FullPath, FileLoggingMode.SingleLogFile, false);
                            log.OnLoggingError += ex => captured = ex;

                            log.Info("file-write-failure");

                            TestHelpers.AssertTrue(captured != null, "Write failures should surface through OnLoggingError.");
                            TestHelpers.AssertContains(captured!.Message, "Error writing to file", "The logging error should describe the file write failure.");

                            return Task.CompletedTask;
                        }),
                });
        }

        public static TestSuiteDescriptor StructuredSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Structured",
                displayName: "Structured LogEntry API",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Structured",
                        caseId: "ConstructorNormalizesNullMessage",
                        displayName: "LogEntry constructor normalizes null messages to empty strings",
                        executeAsync: _ =>
                        {
                            LogEntry entry = new LogEntry(Severity.Info, null!);
                            TestHelpers.AssertEqual(string.Empty, entry.Message, "Null constructor messages should become empty strings.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Structured",
                        caseId: "WithPropertyValidation",
                        displayName: "WithProperty rejects null and empty keys",
                        executeAsync: _ =>
                        {
                            LogEntry entry = new LogEntry(Severity.Info, "property-validation");
                            TestHelpers.ExpectThrows<ArgumentException>(() => entry.WithProperty(null!, 1), "key");
                            TestHelpers.ExpectThrows<ArgumentException>(() => entry.WithProperty(string.Empty, 1), "key");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Structured",
                        caseId: "WithPropertiesValidation",
                        displayName: "WithProperties rejects null dictionaries",
                        executeAsync: _ =>
                        {
                            LogEntry entry = new LogEntry(Severity.Info, "properties-validation");
                            TestHelpers.ExpectThrows<ArgumentNullException>(() => entry.WithProperties(null!), "properties");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Structured",
                        caseId: "WithPropertiesMerge",
                        displayName: "WithProperties merges and overwrites values",
                        executeAsync: _ =>
                        {
                            LogEntry entry = new LogEntry(Severity.Info, "properties-merge")
                                .WithProperty("A", 1)
                                .WithProperties(new Dictionary<string, object>
                                {
                                    { "B", 2 },
                                    { "A", 3 },
                                });

                            TestHelpers.AssertEqual(2, entry.Properties.Count, "Two structured properties should remain.");
                            TestHelpers.AssertEqual(3, (int)entry.Properties["A"], "Later property values should overwrite earlier ones.");
                            TestHelpers.AssertEqual(2, (int)entry.Properties["B"], "New property values should be added.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Structured",
                        caseId: "LogEntryNullValidationSync",
                        displayName: "LogEntry rejects null entries synchronously",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("structured-null-sync");
                            string logFile = temp.GetPath("structured-null-sync.log");

                            using LoggingModule log = CreateFileLogger(logFile);
                            TestHelpers.ExpectThrows<ArgumentNullException>(() => log.LogEntry(null!), "entry");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Structured",
                        caseId: "LogEntryNullValidationAsync",
                        displayName: "LogEntryAsync rejects null entries asynchronously",
                        executeAsync: async _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("structured-null-async");
                            string logFile = temp.GetPath("structured-null-async.log");

                            using LoggingModule log = CreateFileLogger(logFile);
                            await TestHelpers.ExpectThrowsAsync<ArgumentNullException>(() => log.LogEntryAsync(null!), "entry").ConfigureAwait(false);
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Structured",
                        caseId: "LogEntryJsonSerialization",
                        displayName: "LogEntry JSON serialization includes structured metadata",
                        executeAsync: _ =>
                        {
                            LogEntry entry = new LogEntry(Severity.Error, "json-message")
                                .WithProperty("OrderId", 42)
                                .WithCorrelationId("corr-json")
                                .WithSource("JsonSuite");

                            entry.Exception = new InvalidOperationException("json-failure");

                            string json = entry.ToJson();

                            TestHelpers.AssertContains(json, "\"message\":\"json-message\"", "JSON output should contain the message.");
                            TestHelpers.AssertContains(json, "\"source\":\"JsonSuite\"", "JSON output should contain the source.");
                            TestHelpers.AssertContains(json, "\"correlationId\":\"corr-json\"", "JSON output should contain the correlation id.");
                            TestHelpers.AssertContains(json, "\"OrderId\":42", "JSON output should contain structured properties.");
                            TestHelpers.AssertContains(json, "\"type\":\"System.InvalidOperationException\"", "JSON output should contain exception type information.");

                            return Task.CompletedTask;
                        }),
                });
        }

        public static TestSuiteDescriptor FluentSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Fluent",
                displayName: "Fluent Builder API",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Fluent",
                        caseId: "BuilderWritesSync",
                        displayName: "BeginStructuredLog writes synchronously",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("fluent-sync");
                            string logFile = temp.GetPath("fluent-sync.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{source}|{correlation}");
                            log.BeginStructuredLog(Severity.Info, "fluent-sync-message")
                                .WithSource("FluentSource")
                                .WithCorrelationId("FluentCorrelation")
                                .WithProperty("RequestId", 1)
                                .Write();

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "FluentSource|FluentCorrelation [RequestId=1] fluent-sync-message", "Fluent sync writes should preserve builder state.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Fluent",
                        caseId: "BuilderWritesAsync",
                        displayName: "BeginStructuredLog writes asynchronously",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("fluent-async");
                            string logFile = temp.GetPath("fluent-async.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{source}|{correlation}");
                            await log.BeginStructuredLog(Severity.Warn, "fluent-async-message")
                                .WithSource("AsyncFluentSource")
                                .WithCorrelationId("AsyncFluentCorrelation")
                                .WriteAsync(ct).ConfigureAwait(false);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "AsyncFluentSource|AsyncFluentCorrelation fluent-async-message", "Fluent async writes should preserve builder state.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Fluent",
                        caseId: "BuilderWithMultipleProperties",
                        displayName: "BeginStructuredLog writes multiple structured properties",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("fluent-multi-props");
                            string logFile = temp.GetPath("fluent-multi-props.log");

                            using LoggingModule log = CreateFileLogger(logFile, "FLUENT");
                            log.BeginStructuredLog(Severity.Error, "fluent-props-message")
                                .WithProperties(new Dictionary<string, object?>
                                {
                                    { "A", 1 },
                                    { "B", "two" },
                                })
                                .Write();

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "[A=1 B=two] fluent-props-message", "Fluent writes should render multiple structured properties.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Fluent",
                        caseId: "BuilderWithExceptionChaining",
                        displayName: "BeginStructuredLog supports exception chaining without breaking writes",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("fluent-exception");
                            string logFile = temp.GetPath("fluent-exception.log");

                            using LoggingModule log = CreateFileLogger(logFile, "FLUENT");
                            log.BeginStructuredLog(Severity.Critical, "fluent-exception-message")
                                .WithException(new InvalidOperationException("builder-exception"))
                                .Write();

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "FLUENT fluent-exception-message", "Fluent builder should still write the message when an exception is attached.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Fluent",
                        caseId: "BuilderPropertyValidation",
                        displayName: "BeginStructuredLog propagates property validation failures",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("fluent-validation");
                            string logFile = temp.GetPath("fluent-validation.log");

                            using LoggingModule log = CreateFileLogger(logFile);
                            StructuredLogBuilder builder = log.BeginStructuredLog(Severity.Info, "fluent-validation-message");
                            TestHelpers.ExpectThrows<ArgumentException>(() => builder.WithProperty(string.Empty, 1), "key");

                            return Task.CompletedTask;
                        }),
                });
        }

        public static TestSuiteDescriptor ExceptionSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Exception",
                displayName: "Exception Logging",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Exception",
                        caseId: "DefaultSeverity",
                        displayName: "Exception uses the configured default ExceptionSeverity",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("exception-default");
                            string logFile = temp.GetPath("exception-default.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{sev}");
                            log.Exception(new InvalidOperationException("default-exception"), "Module", "Method");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "Alert Exception in Module.Method: default-exception", "Default exception severity should be Alert.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Exception",
                        caseId: "ConfiguredSeverity",
                        displayName: "Exception honors custom ExceptionSeverity settings",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("exception-configured");
                            string logFile = temp.GetPath("exception-configured.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{sev}", settings => settings.ExceptionSeverity = Severity.Warn);
                            log.Exception(new InvalidOperationException("configured-exception"), "Module", "Method");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "Warn Exception in Module.Method: configured-exception", "Configured exception severity should be honored.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Exception",
                        caseId: "ExceptionIncludesModuleAndMethod",
                        displayName: "Exception includes module and method information",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("exception-module-method");
                            string logFile = temp.GetPath("exception-module-method.log");

                            using LoggingModule log = CreateFileLogger(logFile, "EXCEPTION");
                            log.Exception(new InvalidOperationException("module-method-exception"), "BillingService", "Charge");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "Exception in BillingService.Charge: module-method-exception", "Exception message should include module and method.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Exception",
                        caseId: "ExceptionAsyncIncludesModuleAndMethod",
                        displayName: "ExceptionAsync includes module and method information",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("exception-async");
                            string logFile = temp.GetPath("exception-async.log");

                            using LoggingModule log = CreateFileLogger(logFile, "EXCEPTION");
                            await log.ExceptionAsync(new InvalidOperationException("async-exception"), "AsyncBilling", "ChargeAsync", ct).ConfigureAwait(false);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "Exception in AsyncBilling.ChargeAsync: async-exception", "Async exception message should include module and method.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Exception",
                        caseId: "ExceptionValidation",
                        displayName: "Exception logging rejects null exceptions",
                        executeAsync: async _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("exception-validation");
                            string logFile = temp.GetPath("exceptions.log");

                            using LoggingModule log = CreateFileLogger(logFile);

                            TestHelpers.ExpectThrows<ArgumentNullException>(() => log.Exception(null!), "exception");
                            await TestHelpers.ExpectThrowsAsync<ArgumentNullException>(() => log.ExceptionAsync(null!), "exception").ConfigureAwait(false);
                        }),
                });
        }

        public static TestSuiteDescriptor RetentionSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Retention",
                displayName: "Retention Cleanup",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Retention",
                        caseId: "DeletesOldFiles",
                        displayName: "Retention cleanup deletes files older than the configured cutoff",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("retention-delete-old");
                            string baseLogFile = temp.GetPath("retention.log");

                            using LoggingModule log = new LoggingModule(baseLogFile, FileLoggingMode.FileWithDate, false);
                            TestHelpers.ConfigureSettings(log, settings => settings.LogRetentionDays = 7);

                            string oldFile = baseLogFile + ".20000101";
                            string recentFile = GetDatedLogFilePath(baseLogFile, DateTime.Now);
                            File.WriteAllText(oldFile, "old");
                            File.WriteAllText(recentFile, "recent");

                            InvokeRetentionCleanup(log);

                            TestHelpers.AssertTrue(!File.Exists(oldFile), "Old dated log file should be deleted.");
                            TestHelpers.AssertTrue(File.Exists(recentFile), "Recent dated log file should be preserved.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Retention",
                        caseId: "KeepsRecentFiles",
                        displayName: "Retention cleanup keeps files on the retention boundary",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("retention-keep-recent");
                            string baseLogFile = temp.GetPath("retention.log");

                            using LoggingModule log = new LoggingModule(baseLogFile, FileLoggingMode.FileWithDate, false);
                            TestHelpers.ConfigureSettings(log, settings => settings.LogRetentionDays = 1);

                            string boundaryFile = GetDatedLogFilePath(baseLogFile, DateTime.Now.AddDays(-1));
                            File.WriteAllText(boundaryFile, "boundary");

                            InvokeRetentionCleanup(log);

                            TestHelpers.AssertTrue(File.Exists(boundaryFile), "Files on the retention boundary should be preserved.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Retention",
                        caseId: "IgnoresInvalidFilenames",
                        displayName: "Retention cleanup ignores invalid dated filename suffixes",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("retention-invalid");
                            string baseLogFile = temp.GetPath("retention.log");

                            using LoggingModule log = new LoggingModule(baseLogFile, FileLoggingMode.FileWithDate, false);
                            TestHelpers.ConfigureSettings(log, settings => settings.LogRetentionDays = 7);

                            string invalidFile = baseLogFile + ".notadate";
                            File.WriteAllText(invalidFile, "invalid");

                            InvokeRetentionCleanup(log);

                            TestHelpers.AssertTrue(File.Exists(invalidFile), "Files with invalid suffixes should be ignored by retention cleanup.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Retention",
                        caseId: "DisabledDoesNothing",
                        displayName: "Retention cleanup does nothing when retention is disabled",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("retention-disabled");
                            string baseLogFile = temp.GetPath("retention.log");

                            using LoggingModule log = new LoggingModule(baseLogFile, FileLoggingMode.FileWithDate, false);
                            TestHelpers.ConfigureSettings(log, settings => settings.LogRetentionDays = 0);

                            string oldFile = baseLogFile + ".20000101";
                            File.WriteAllText(oldFile, "old");

                            InvokeRetentionCleanup(log);

                            TestHelpers.AssertTrue(File.Exists(oldFile), "Retention cleanup should not remove files when disabled.");

                            return Task.CompletedTask;
                        }),
                });
        }

        public static TestSuiteDescriptor OrderingSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Ordering",
                displayName: "Ordering Guarantees",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Ordering",
                        caseId: "SequentialMessagesPreserveOrder",
                        displayName: "Sequential file writes preserve message order",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("ordering-sequential");
                            string logFile = temp.GetPath("ordering.log");

                            using LoggingModule log = CreateFileLogger(logFile, "ORDER");
                            for (int i = 0; i < 10; i++)
                            {
                                log.Info("message-" + i);
                            }

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertEqual(10, lines.Length, "Expected one file line per sequential message.");

                            for (int i = 0; i < 10; i++)
                            {
                                TestHelpers.AssertTrue(lines[i].EndsWith("message-" + i, StringComparison.Ordinal), "Sequential message ordering should be preserved.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Ordering",
                        caseId: "SplitMessagesPreserveSequenceOrder",
                        displayName: "Split messages emit ordered sequence metadata",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("ordering-split");
                            string logFile = temp.GetPath("ordering-split.log");

                            using LoggingModule log = CreateFileLogger(logFile, "ORDER", settings => settings.MaxMessageLength = 32);
                            log.Info(new string('Q', 96));

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertTrue(lines.Length >= 3, "Expected at least three split lines.");
                            TestHelpers.AssertContains(lines[0], "Sequence=1", "First split line should be sequence 1.");
                            TestHelpers.AssertContains(lines[1], "Sequence=2", "Second split line should be sequence 2.");
                            TestHelpers.AssertContains(lines[2], "Sequence=3", "Third split line should be sequence 3.");

                            return Task.CompletedTask;
                        }),
                });
        }

        public static TestSuiteDescriptor ThreadSafetySuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Threading",
                displayName: "Thread Safety",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Threading",
                        caseId: "ConcurrentSyncLogging",
                        displayName: "Concurrent sync logging writes every message",
                        executeAsync: async _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("threading-sync");
                            string logFile = temp.GetPath("threading-sync.log");

                            using LoggingModule log = CreateFileLogger(logFile, "THREAD");

                            List<Task> tasks = Enumerable.Range(0, 50)
                                .Select(i => Task.Run(() => log.Info("sync-" + i)))
                                .ToList();

                            await Task.WhenAll(tasks).ConfigureAwait(false);

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertEqual(50, lines.Length, "Concurrent sync logging should write one line per message.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Threading",
                        caseId: "ConcurrentAsyncLogging",
                        displayName: "Concurrent async logging writes every message",
                        executeAsync: async _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("threading-async");
                            string logFile = temp.GetPath("threading-async.log");

                            using LoggingModule log = CreateFileLogger(logFile, "THREAD");

                            List<Task> tasks = Enumerable.Range(0, 50)
                                .Select(i => log.InfoAsync("async-" + i))
                                .ToList();

                            await Task.WhenAll(tasks).ConfigureAwait(false);

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertEqual(50, lines.Length, "Concurrent async logging should write one line per message.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Threading",
                        caseId: "ConcurrentStructuredLogging",
                        displayName: "Concurrent structured logging writes every message",
                        executeAsync: async _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("threading-structured");
                            string logFile = temp.GetPath("threading-structured.log");

                            using LoggingModule log = CreateFileLogger(logFile, "{source}");

                            List<Task> tasks = Enumerable.Range(0, 50)
                                .Select(i => Task.Run(() =>
                                {
                                    log.BeginStructuredLog(Severity.Info, "structured-" + i)
                                        .WithSource("StructuredThread")
                                        .WithProperty("Index", i)
                                        .Write();
                                }))
                                .ToList();

                            await Task.WhenAll(tasks).ConfigureAwait(false);

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertEqual(50, lines.Length, "Concurrent structured logging should write one line per message.");
                            TestHelpers.AssertTrue(lines.All(line => line.Contains("StructuredThread", StringComparison.Ordinal)), "Each structured line should preserve source metadata.");
                        }),
                });
        }

        public static TestSuiteDescriptor SyslogSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Syslog",
                displayName: "Syslog Delivery",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Syslog",
                        caseId: "WireFormat",
                        displayName: "Syslog payload contains priority, hostname, and formatted body",
                        executeAsync: async ct =>
                        {
                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

                            TestHelpers.ConfigureSettings(log, settings =>
                            {
                                settings.HeaderFormat = "{app}|{sev}";
                                settings.ApplicationName = "WireApp";
                            });

                            await log.InfoAsync("wire-case", ct).ConfigureAwait(false);
                            string message = await listener.ReceiveAsync().ConfigureAwait(false);

                            int expectedPriority = 16 * 8 + (int)Severity.Info;

                            TestHelpers.AssertTrue(message.StartsWith("<" + expectedPriority + ">", StringComparison.Ordinal), "Syslog payload should start with the expected priority.");
                            TestHelpers.AssertContains(message, Environment.MachineName, "Syslog payload should contain the local hostname.");
                            TestHelpers.AssertContains(message, "WireApp|Info wire-case", "Syslog payload should contain the formatted log body.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Syslog",
                        caseId: "MultipleServersReceiveMessage",
                        displayName: "Configured syslog servers each receive the message",
                        executeAsync: async ct =>
                        {
                            using UdpCaptureListener listener1 = new UdpCaptureListener();
                            using UdpCaptureListener listener2 = new UdpCaptureListener();

                            using LoggingModule log = new LoggingModule(
                                new List<SyslogServer>
                                {
                                    new SyslogServer("127.0.0.1", listener1.Port),
                                    new SyslogServer("127.0.0.1", listener2.Port),
                                },
                                false);

                            TestHelpers.ConfigureSettings(log, settings =>
                            {
                                settings.HeaderFormat = "{app}";
                                settings.ApplicationName = "FanoutApp";
                            });

                            await log.WarnAsync("fanout-case", ct).ConfigureAwait(false);

                            string message1 = await listener1.ReceiveAsync().ConfigureAwait(false);
                            string message2 = await listener2.ReceiveAsync().ConfigureAwait(false);

                            TestHelpers.AssertContains(message1, "FanoutApp fanout-case", "The first syslog target should receive the message.");
                            TestHelpers.AssertContains(message2, "FanoutApp fanout-case", "The second syslog target should receive the message.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Syslog",
                        caseId: "AsyncStructuredSyslog",
                        displayName: "Structured LogEntryAsync reaches syslog output",
                        executeAsync: async ct =>
                        {
                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

                            TestHelpers.ConfigureSettings(log, settings =>
                            {
                                settings.HeaderFormat = "{source}|{correlation}|{app}";
                                settings.ApplicationName = "AsyncSyslogApp";
                            });

                            LogEntry entry = new LogEntry(Severity.Error, "async-syslog-message")
                                .WithCorrelationId("corr-syslog")
                                .WithSource("AsyncSuite");

                            await log.LogEntryAsync(entry, ct).ConfigureAwait(false);
                            string message = await listener.ReceiveAsync().ConfigureAwait(false);

                            TestHelpers.AssertContains(message, "AsyncSuite|corr-syslog|AsyncSyslogApp async-syslog-message", "Async structured writes should preserve source, correlation, and app name.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Syslog",
                        caseId: "PriorityChangesWithSeverity",
                        displayName: "Syslog priority changes with severity",
                        executeAsync: async ct =>
                        {
                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using LoggingModule log = new LoggingModule("127.0.0.1", listener.Port, false);

                            await log.EmergencyAsync("priority-case", ct).ConfigureAwait(false);
                            string message = await listener.ReceiveAsync().ConfigureAwait(false);

                            int expectedPriority = 16 * 8 + (int)Severity.Emergency;
                            TestHelpers.AssertTrue(message.StartsWith("<" + expectedPriority + ">", StringComparison.Ordinal), "Syslog priority should reflect emergency severity.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Syslog",
                        caseId: "MicrosoftExtensionsLoggingSyslog",
                        displayName: "Microsoft.Extensions.Logging syslog integration includes source and structured properties",
                        executeAsync: async _ =>
                        {
                            using UdpCaptureListener listener = new UdpCaptureListener();
                            using ILoggerFactory factory = LoggerFactory.Create(builder =>
                            {
                                builder.ClearProviders();
                                builder.AddSyslog("127.0.0.1", listener.Port, false, module =>
                                {
                                    TestHelpers.ConfigureSettings(module, settings =>
                                    {
                                        settings.HeaderFormat = "{source}|{app}";
                                        settings.ApplicationName = "MsExtApp";
                                    });
                                });
                            });

                            ILogger logger = factory.CreateLogger("Sample.Category");
                            logger.LogInformation("User {UserId} logged in", 42);

                            string message = await listener.ReceiveAsync().ConfigureAwait(false);
                            TestHelpers.AssertContains(message, "Sample.Category|MsExtApp [UserId=42] User 42 logged in", "ILogger integration should preserve category source and structured values.");
                        }),
                });
        }

        public static TestSuiteDescriptor IntegrationSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Integration",
                displayName: "Extension and LoggerFactory Integration",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Integration",
                        caseId: "AddFileLoggingWritesFile",
                        displayName: "AddFileLogging writes through ILoggerFactory",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("integration-file");
                            string logFile = temp.GetPath("integration-file.log");

                            using ILoggerFactory factory = LoggerFactory.Create(builder =>
                            {
                                builder.ClearProviders();
                                builder.AddFileLogging(logFile, FileLoggingMode.SingleLogFile, false, module =>
                                {
                                    TestHelpers.ConfigureSettings(module, settings =>
                                    {
                                        settings.HeaderFormat = "{source}|{app}";
                                        settings.ApplicationName = "FactoryFileApp";
                                    });
                                });
                            });

                            ILogger logger = factory.CreateLogger("Factory.Category");
                            logger.LogWarning("factory-file-message");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "Factory.Category|FactoryFileApp factory-file-message", "AddFileLogging should write through ILoggerFactory.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Integration",
                        caseId: "LoggerFactoryRespectsMinimumSeverity",
                        displayName: "ILoggerFactory integration respects MinimumSeverity",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("integration-min-severity");
                            string logFile = temp.GetPath("integration-min-severity.log");

                            using ILoggerFactory factory = LoggerFactory.Create(builder =>
                            {
                                builder.ClearProviders();
                                builder.AddFileLogging(logFile, FileLoggingMode.SingleLogFile, false, module =>
                                {
                                    TestHelpers.ConfigureSettings(module, settings =>
                                    {
                                        settings.HeaderFormat = "{sev}";
                                        settings.MinimumSeverity = Severity.Error;
                                    });
                                });
                            });

                            ILogger logger = factory.CreateLogger("Severity.Category");
                            logger.LogInformation("filtered-by-factory");
                            logger.LogError("allowed-by-factory");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertDoesNotContain(contents, "filtered-by-factory", "Messages below the configured minimum severity should be filtered.");
                            TestHelpers.AssertContains(contents, "Error allowed-by-factory", "Messages at or above the minimum severity should be written.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Integration",
                        caseId: "EventIdAndEventNameCaptured",
                        displayName: "ILoggerFactory integration captures EventId and EventName",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("integration-eventid");
                            string logFile = temp.GetPath("integration-eventid.log");

                            using ILoggerFactory factory = LoggerFactory.Create(builder =>
                            {
                                builder.ClearProviders();
                                builder.AddFileLogging(logFile, FileLoggingMode.SingleLogFile, false, module =>
                                {
                                    TestHelpers.ConfigureSettings(module, settings => settings.HeaderFormat = "{source}");
                                });
                            });

                            ILogger logger = factory.CreateLogger("Event.Category");
                            logger.LogInformation(new EventId(7, "LoginEvent"), "User {UserId} logged in", 42);

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "Event.Category [EventId=7 EventName=LoginEvent UserId=42] User 42 logged in", "EventId and EventName should be emitted as structured properties.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Integration",
                        caseId: "LoggerSkipsEmptyMessage",
                        displayName: "ILoggerFactory integration skips empty messages when no exception is present",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("integration-empty-message");
                            string logFile = temp.GetPath("integration-empty-message.log");

                            using ILoggerFactory factory = LoggerFactory.Create(builder =>
                            {
                                builder.ClearProviders();
                                builder.AddFileLogging(logFile, FileLoggingMode.SingleLogFile, false, module =>
                                {
                                    TestHelpers.ConfigureSettings(module, settings => settings.HeaderFormat = "LOGGER");
                                });
                            });

                            ILogger logger = factory.CreateLogger("Empty.Category");
                            logger.Log<string>(LogLevel.Information, default, string.Empty, null, (state, ex) => state);

                            TestHelpers.AssertTrue(!File.Exists(logFile), "Empty logger messages without exceptions should be ignored.");

                            return Task.CompletedTask;
                        }),
                });
        }

        public static TestSuiteDescriptor MessageLoggedSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MessageLogged",
                displayName: "MessageLogged Event",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "FiresOnceSync",
                        displayName: "MessageLogged fires once for a synchronous log",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-sync");
                            string logFile = temp.GetPath("event-sync.log");

                            int count = 0;
                            LogEntry? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += entry =>
                            {
                                count++;
                                captured = entry;
                            };

                            log.Info("event-sync-message");

                            TestHelpers.AssertEqual(1, count, "MessageLogged should fire exactly once for a single sync log.");
                            TestHelpers.AssertTrue(captured != null, "MessageLogged should provide the emitted log entry.");
                            TestHelpers.AssertEqual("event-sync-message", captured!.Message, "The emitted entry should carry the original message.");
                            TestHelpers.AssertEqual(Severity.Info, captured.Severity, "The emitted entry should carry the original severity.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "FiresOnceAsync",
                        displayName: "MessageLogged fires once for an asynchronous log",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-async");
                            string logFile = temp.GetPath("event-async.log");

                            int count = 0;
                            LogEntry? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += entry =>
                            {
                                count++;
                                captured = entry;
                            };

                            await log.InfoAsync("event-async-message", ct).ConfigureAwait(false);

                            TestHelpers.AssertEqual(1, count, "MessageLogged should fire exactly once for a single async log.");
                            TestHelpers.AssertTrue(captured != null, "MessageLogged should provide the emitted log entry.");
                            TestHelpers.AssertEqual("event-async-message", captured!.Message, "The emitted async entry should carry the original message.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "FiresOnceForSplitSync",
                        displayName: "MessageLogged fires once with the unsplit entry for a split sync message",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-split-sync");
                            string logFile = temp.GetPath("event-split-sync.log");

                            int count = 0;
                            LogEntry? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT", settings => settings.MaxMessageLength = 32);
                            log.MessageLogged += entry =>
                            {
                                count++;
                                captured = entry;
                            };

                            string original = new string('X', 96);
                            log.Info(original);

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertTrue(lines.Length >= 3, "The message should have been split across multiple file lines.");
                            TestHelpers.AssertEqual(1, count, "MessageLogged should fire exactly once even when the message is split.");
                            TestHelpers.AssertTrue(captured != null, "MessageLogged should provide the emitted log entry.");
                            TestHelpers.AssertEqual(original, captured!.Message, "The emitted entry should carry the original, unsplit message.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "FiresOnceForSplitAsync",
                        displayName: "MessageLogged fires once with the unsplit entry for a split async message",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-split-async");
                            string logFile = temp.GetPath("event-split-async.log");

                            int count = 0;
                            LogEntry? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT", settings => settings.MaxMessageLength = 32);
                            log.MessageLogged += entry =>
                            {
                                count++;
                                captured = entry;
                            };

                            string original = new string('Y', 128);
                            await log.InfoAsync(original, ct).ConfigureAwait(false);

                            string[] lines = TestHelpers.ReadAllLines(logFile);
                            TestHelpers.AssertTrue(lines.Length >= 4, "The async message should have been split across multiple file lines.");
                            TestHelpers.AssertEqual(1, count, "MessageLogged should fire exactly once even when the async message is split.");
                            TestHelpers.AssertTrue(captured != null, "MessageLogged should provide the emitted log entry.");
                            TestHelpers.AssertEqual(original, captured!.Message, "The emitted async entry should carry the original, unsplit message.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "PreservesStructuredMetadata",
                        displayName: "MessageLogged provides the original entry with structured metadata intact",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-structured");
                            string logFile = temp.GetPath("event-structured.log");

                            LogEntry? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += entry => captured = entry;

                            LogEntry entry = new LogEntry(Severity.Warn, "event-structured-message")
                                .WithProperty("OrderId", 99)
                                .WithCorrelationId("corr-event")
                                .WithSource("EventSuite");

                            log.LogEntry(entry);

                            TestHelpers.AssertTrue(captured != null, "MessageLogged should provide the emitted log entry.");
                            TestHelpers.AssertTrue(ReferenceEquals(entry, captured), "MessageLogged should provide the original entry instance.");
                            TestHelpers.AssertEqual("corr-event", captured!.CorrelationId, "The emitted entry should preserve the correlation id.");
                            TestHelpers.AssertEqual("EventSuite", captured.Source, "The emitted entry should preserve the source.");
                            TestHelpers.AssertTrue(captured.Properties.ContainsKey("OrderId"), "The emitted entry should preserve structured properties.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "InvokesMultipleSubscribers",
                        displayName: "MessageLogged invokes every subscribed handler",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-multi-subscriber");
                            string logFile = temp.GetPath("event-multi-subscriber.log");

                            int first = 0;
                            int second = 0;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += _ => first++;
                            log.MessageLogged += _ => second++;

                            log.Info("event-multi-subscriber-message");

                            TestHelpers.AssertEqual(1, first, "The first subscriber should be invoked once.");
                            TestHelpers.AssertEqual(1, second, "The second subscriber should be invoked once.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "UnsubscribedHandlerNotInvoked",
                        displayName: "MessageLogged does not invoke an unsubscribed handler",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-unsubscribe");
                            string logFile = temp.GetPath("event-unsubscribe.log");

                            int count = 0;
                            Action<LogEntry> handler = _ => count++;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += handler;
                            log.MessageLogged -= handler;

                            log.Info("event-unsubscribe-message");

                            TestHelpers.AssertEqual(0, count, "A handler removed before logging should not be invoked.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "FiresOncePerMessage",
                        displayName: "MessageLogged fires once for each of several sequential messages",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-per-message");
                            string logFile = temp.GetPath("event-per-message.log");

                            List<string> observed = new List<string>();

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += entry => observed.Add(entry.Message);

                            for (int i = 0; i < 5; i++)
                            {
                                log.Info("event-message-" + i);
                            }

                            TestHelpers.AssertEqual(5, observed.Count, "MessageLogged should fire once per emitted message.");
                            for (int i = 0; i < 5; i++)
                            {
                                TestHelpers.AssertEqual("event-message-" + i, observed[i], "MessageLogged should preserve message order.");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "FiresOncePerConcurrentMessage",
                        displayName: "MessageLogged fires once per message under concurrent logging",
                        executeAsync: async _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-concurrent");
                            string logFile = temp.GetPath("event-concurrent.log");

                            int count = 0;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += _ => Interlocked.Increment(ref count);

                            List<Task> tasks = Enumerable.Range(0, 50)
                                .Select(i => Task.Run(() => log.Info("event-concurrent-" + i)))
                                .ToList();

                            await Task.WhenAll(tasks).ConfigureAwait(false);

                            TestHelpers.AssertEqual(50, count, "MessageLogged should fire exactly once per concurrent message.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "DoesNotFireBelowMinimumSeveritySync",
                        displayName: "MessageLogged does not fire for sync messages below MinimumSeverity",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-min-severity-sync");
                            string logFile = temp.GetPath("event-min-severity-sync.log");

                            int count = 0;
                            LogEntry? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT", settings => settings.MinimumSeverity = Severity.Error);
                            log.MessageLogged += entry =>
                            {
                                count++;
                                captured = entry;
                            };

                            log.Info("filtered-info");
                            TestHelpers.AssertEqual(0, count, "MessageLogged should not fire for messages below the minimum severity.");

                            log.Error("accepted-error");
                            TestHelpers.AssertEqual(1, count, "MessageLogged should fire for messages at or above the minimum severity.");
                            TestHelpers.AssertEqual("accepted-error", captured!.Message, "Only the accepted message should be emitted.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "DoesNotFireBelowMinimumSeverityAsync",
                        displayName: "MessageLogged does not fire for async messages below MinimumSeverity",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-min-severity-async");
                            string logFile = temp.GetPath("event-min-severity-async.log");

                            int count = 0;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT", settings => settings.MinimumSeverity = Severity.Error);
                            log.MessageLogged += _ => count++;

                            await log.InfoAsync("filtered-info-async", ct).ConfigureAwait(false);
                            TestHelpers.AssertEqual(0, count, "MessageLogged should not fire for async messages below the minimum severity.");

                            await log.ErrorAsync("accepted-error-async", ct).ConfigureAwait(false);
                            TestHelpers.AssertEqual(1, count, "MessageLogged should fire for async messages at or above the minimum severity.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "DoesNotFireForNullMessage",
                        displayName: "MessageLogged does not fire for null messages",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-null-message");
                            string logFile = temp.GetPath("event-null-message.log");

                            int count = 0;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += _ => count++;

                            log.Info(null!);

                            TestHelpers.AssertEqual(0, count, "MessageLogged should not fire for ignored null messages.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "DoesNotFireForEmptyMessage",
                        displayName: "MessageLogged does not fire for empty messages",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-empty-message");
                            string logFile = temp.GetPath("event-empty-message.log");

                            int count = 0;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.MessageLogged += _ => count++;

                            log.Info(string.Empty);
                            await log.InfoAsync(string.Empty, ct).ConfigureAwait(false);

                            TestHelpers.AssertEqual(0, count, "MessageLogged should not fire for ignored empty messages.");
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "NoSubscribersDoesNotThrowSync",
                        displayName: "Logging without a MessageLogged subscriber succeeds synchronously",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-no-subscriber-sync");
                            string logFile = temp.GetPath("event-no-subscriber-sync.log");

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.Info("event-no-subscriber-message");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "EVENT event-no-subscriber-message", "Logging without subscribers should still write output.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "HandlerExceptionRoutedToOnLoggingErrorSync",
                        displayName: "A throwing sync handler routes to OnLoggingError without breaking delivery",
                        executeAsync: _ =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-handler-throws-sync");
                            string logFile = temp.GetPath("event-handler-throws-sync.log");

                            Exception? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.OnLoggingError += ex => captured = ex;
                            log.MessageLogged += _ => throw new InvalidOperationException("handler-boom");

                            log.Info("event-handler-throws-message");

                            TestHelpers.AssertTrue(captured != null, "A throwing handler should surface through OnLoggingError.");
                            TestHelpers.AssertContains(captured!.Message, "Error in MessageLogged handler", "The routed error should identify the MessageLogged handler.");
                            TestHelpers.AssertTrue(captured.InnerException is InvalidOperationException, "The original handler exception should be preserved as the inner exception.");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "EVENT event-handler-throws-message", "A throwing handler must not prevent the message from being written.");

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "MessageLogged",
                        caseId: "HandlerExceptionRoutedToOnLoggingErrorAsync",
                        displayName: "A throwing async handler routes to OnLoggingError without breaking delivery",
                        executeAsync: async ct =>
                        {
                            using TemporaryDirectory temp = new TemporaryDirectory("event-handler-throws-async");
                            string logFile = temp.GetPath("event-handler-throws-async.log");

                            Exception? captured = null;

                            using LoggingModule log = CreateFileLogger(logFile, "EVENT");
                            log.OnLoggingError += ex => captured = ex;
                            log.MessageLogged += _ => throw new InvalidOperationException("handler-boom-async");

                            await log.InfoAsync("event-handler-throws-async-message", ct).ConfigureAwait(false);

                            TestHelpers.AssertTrue(captured != null, "A throwing async handler should surface through OnLoggingError.");
                            TestHelpers.AssertContains(captured!.Message, "Error in MessageLogged handler", "The routed error should identify the MessageLogged handler.");

                            string contents = TestHelpers.ReadAllText(logFile);
                            TestHelpers.AssertContains(contents, "EVENT event-handler-throws-async-message", "A throwing async handler must not prevent the message from being written.");
                        }),
                });
        }

        private static IEnumerable<TestCaseDescriptor> CreateSyncSeverityCases()
        {
            yield return CreateSyncSeverityCase("DebugSync", "Debug writes Debug severity synchronously", Severity.Debug, (log, message) => log.Debug(message));
            yield return CreateSyncSeverityCase("InfoSync", "Info writes Info severity synchronously", Severity.Info, (log, message) => log.Info(message));
            yield return CreateSyncSeverityCase("WarnSync", "Warn writes Warn severity synchronously", Severity.Warn, (log, message) => log.Warn(message));
            yield return CreateSyncSeverityCase("ErrorSync", "Error writes Error severity synchronously", Severity.Error, (log, message) => log.Error(message));
            yield return CreateSyncSeverityCase("AlertSync", "Alert writes Alert severity synchronously", Severity.Alert, (log, message) => log.Alert(message));
            yield return CreateSyncSeverityCase("CriticalSync", "Critical writes Critical severity synchronously", Severity.Critical, (log, message) => log.Critical(message));
            yield return CreateSyncSeverityCase("EmergencySync", "Emergency writes Emergency severity synchronously", Severity.Emergency, (log, message) => log.Emergency(message));
        }

        private static IEnumerable<TestCaseDescriptor> CreateAsyncSeverityCases()
        {
            yield return CreateAsyncSeverityCase("DebugAsync", "DebugAsync writes Debug severity asynchronously", Severity.Debug, (log, message, ct) => log.DebugAsync(message, ct));
            yield return CreateAsyncSeverityCase("InfoAsync", "InfoAsync writes Info severity asynchronously", Severity.Info, (log, message, ct) => log.InfoAsync(message, ct));
            yield return CreateAsyncSeverityCase("WarnAsync", "WarnAsync writes Warn severity asynchronously", Severity.Warn, (log, message, ct) => log.WarnAsync(message, ct));
            yield return CreateAsyncSeverityCase("ErrorAsync", "ErrorAsync writes Error severity asynchronously", Severity.Error, (log, message, ct) => log.ErrorAsync(message, ct));
            yield return CreateAsyncSeverityCase("AlertAsync", "AlertAsync writes Alert severity asynchronously", Severity.Alert, (log, message, ct) => log.AlertAsync(message, ct));
            yield return CreateAsyncSeverityCase("CriticalAsync", "CriticalAsync writes Critical severity asynchronously", Severity.Critical, (log, message, ct) => log.CriticalAsync(message, ct));
            yield return CreateAsyncSeverityCase("EmergencyAsync", "EmergencyAsync writes Emergency severity asynchronously", Severity.Emergency, (log, message, ct) => log.EmergencyAsync(message, ct));
        }

        private static TestCaseDescriptor CreateSyncSeverityCase(
            string caseId,
            string displayName,
            Severity severity,
            Action<LoggingModule, string> write)
        {
            return new TestCaseDescriptor(
                suiteId: "Severity",
                caseId: caseId,
                displayName: displayName,
                executeAsync: _ =>
                {
                    using TemporaryDirectory temp = new TemporaryDirectory("severity-" + caseId);
                    string logFile = temp.GetPath(caseId + ".log");
                    string message = "message-" + caseId;

                    using LoggingModule log = CreateFileLogger(logFile, "{sev}|{level}");
                    write(log, message);

                    string contents = TestHelpers.ReadAllText(logFile);
                    TestHelpers.AssertContains(contents, severity + "|" + (int)severity + " " + message, "Severity convenience method should preserve severity and message.");

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor CreateAsyncSeverityCase(
            string caseId,
            string displayName,
            Severity severity,
            Func<LoggingModule, string, CancellationToken, Task> writeAsync)
        {
            return new TestCaseDescriptor(
                suiteId: "Severity",
                caseId: caseId,
                displayName: displayName,
                executeAsync: async ct =>
                {
                    using TemporaryDirectory temp = new TemporaryDirectory("severity-" + caseId);
                    string logFile = temp.GetPath(caseId + ".log");
                    string message = "message-" + caseId;

                    using LoggingModule log = CreateFileLogger(logFile, "{sev}|{level}");
                    await writeAsync(log, message, ct).ConfigureAwait(false);

                    string contents = TestHelpers.ReadAllText(logFile);
                    TestHelpers.AssertContains(contents, severity + "|" + (int)severity + " " + message, "Async severity convenience method should preserve severity and message.");
                });
        }

        private static LoggingModule CreateFileLogger(
            string logFile,
            string headerFormat = "FILE",
            Action<LoggingSettings>? configure = null)
        {
            LoggingModule log = new LoggingModule(logFile, FileLoggingMode.SingleLogFile, false);
            TestHelpers.ConfigureSettings(log, settings =>
            {
                settings.HeaderFormat = headerFormat;
                configure?.Invoke(settings);
            });
            return log;
        }

        private static int GetDistinctPort(int disallowedPort)
        {
            int port = PortAllocator.GetEphemeralPort();
            while (port == disallowedPort)
            {
                port = PortAllocator.GetEphemeralPort();
            }

            return port;
        }

        private static string GetDatedLogFilePath(string baseLogFile, DateTime date)
        {
            string directory = Path.GetDirectoryName(baseLogFile) ?? string.Empty;
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(baseLogFile);
            string extension = Path.GetExtension(baseLogFile);
            string dateString = date.ToString("yyyyMMdd");
            return Path.Combine(directory, filenameWithoutExtension + extension + "." + dateString);
        }

        private static void InvokeRetentionCleanup(LoggingModule log)
        {
            MethodInfo? method = typeof(LoggingModule).GetMethod("CleanupOldLogFiles", BindingFlags.Instance | BindingFlags.NonPublic);
            TestHelpers.AssertTrue(method != null, "Expected private CleanupOldLogFiles method to exist.");
            method!.Invoke(log, null);
        }
    }
}
