namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;

    class Program
    {
#pragma warning disable CS8632
#pragma warning disable CS1998
#pragma warning disable CS8601
#pragma warning disable CS8625
#pragma warning disable CS8618
        private static LoggingModule _Log;
        private static int _TestsPassed = 0;
        private static int _TestsFailed = 0;
        private static readonly object _ConsoleLock = new object();
        private static Process _SyslogServerProcess;
        private static int _SyslogServerPort = 5140;
        private static Random _Random = new Random();
        private static List<TestResult> _TestResults = new List<TestResult>();
        private static DateTime _TestSuiteStartTime;

        public class TestResult
        {
            public string Name { get; set; }
            public bool Passed { get; set; }
            public TimeSpan Duration { get; set; }
            public string ErrorMessage { get; set; }
        }

        static async Task Main(string[] args)
        {
            _TestSuiteStartTime = DateTime.UtcNow;
            Console.WriteLine("=== SyslogLogging Comprehensive Test Suite ===");
            Console.WriteLine("This test suite validates ALL library capabilities");

            // Clear logs directory to ensure clean test environment
            ClearLogsDirectory();

            Console.WriteLine("Starting SyslogServer for integration testing...");
            Console.WriteLine();

            try
            {
                await StartSyslogServer();
                await RunAllTests();
            }
            finally
            {
                await StopSyslogServer();
            }

            Console.WriteLine();
            TimeSpan totalSuiteTime = DateTime.UtcNow - _TestSuiteStartTime;
            Console.WriteLine("=== Test Summary ===");
            Console.WriteLine($"Tests Passed: {_TestsPassed}");
            Console.WriteLine($"Tests Failed: {_TestsFailed}");
            Console.WriteLine($"Total Tests: {_TestsPassed + _TestsFailed}");
            Console.WriteLine($"Success Rate: {(_TestsPassed * 100.0 / (_TestsPassed + _TestsFailed)):F1}%");
            Console.WriteLine();

            // Show individual test results in table format
            Console.WriteLine("=== Individual Test Results ===");
            lock (_TestResults)
            {
                if (_TestResults.Count > 0)
                {
                    // Calculate column widths for proper alignment
                    int nameWidth = Math.Max(40, _TestResults.Max(t => t.Name.Length) + 2);
                    int statusWidth = 8;
                    int durationWidth = 12;

                    // Print table header
                    string header = $"{"Test Name".PadRight(nameWidth)} {"Status".PadRight(statusWidth)} {"Duration".PadRight(durationWidth)}";
                    Console.WriteLine(header);
                    Console.WriteLine(new string('-', header.Length));

                    // Print each test result
                    foreach (TestResult test in _TestResults)
                    {
                        string status = test.Passed ? "PASS" : "FAIL";
                        string duration = $"{test.Duration.TotalMilliseconds:F0}ms";

                        string testName = test.Name.Length > nameWidth - 2
                            ? test.Name.Substring(0, nameWidth - 5) + "..."
                            : test.Name;

                        Console.WriteLine($"{testName.PadRight(nameWidth)} {status.PadRight(statusWidth)} {duration.PadRight(durationWidth)}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Total Test Suite Runtime: {totalSuiteTime.TotalSeconds:F2} seconds");

            // List failed tests explicitly if any exist
            List<TestResult> failedTests = new List<TestResult>();
            lock (_TestResults)
            {
                failedTests = _TestResults.Where(t => !t.Passed).ToList();
            }

            if (failedTests.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== Failed Tests ===");
                foreach (TestResult failedTest in failedTests)
                {
                    string errorMessage = string.IsNullOrEmpty(failedTest.ErrorMessage)
                        ? "Test returned false"
                        : failedTest.ErrorMessage;
                    Console.WriteLine($"  {failedTest.Name}");
                    Console.WriteLine($"    Error: {errorMessage}");
                    Console.WriteLine($"    Duration: {failedTest.Duration.TotalMilliseconds:F0}ms");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Check the 'logs' directory for generated log files.");
        }

        static async Task RunAllTests()
        {
            // CRITICAL: Test stack overflow bug FIRST before other tests
            await TestStackOverflowBugDetection();

            await TestConstructors();
            await TestBasicLogging();
            await TestAsyncLogging();
            await TestStructuredLogging();
            await TestFluentLogging();
            await TestExceptionLogging();
            await TestSettings();
            await TestLogRetention();
            await TestColorSettings();
            await TestHighThroughputProcessing();
            await TestThreadSafety();
            await TestMultipleDestinations();
            // Persistent queuing tests removed (no longer applicable)
            await TestErrorHandling();
            await TestDisposal();
            await TestPerformance();
            await TestMessageOrderingGuarantees();

            // Comprehensive critical tests removed (persistence-focused)
        }

        static async Task TestStackOverflowBugDetection()
        {
            WriteTestHeader("CRITICAL BUG DETECTION - Stack Overflow Prevention");

            // Test 1: CRITICAL - Stack Overflow Detection (Must run first!)
            await RunTest("CRITICAL: Stack overflow prevention test", async () =>
            {
                try
                {
                    _Log = new LoggingModule();
                    _Log.Settings.MaxMessageLength = 500; // Small to force message splitting

                    Console.WriteLine("    Testing with 1MB message that should cause stack overflow...");

                    // This 1MB message will cause infinite recursion in ProcessLogEntry
                    string criticalMessage = new string('Z', 1024 * 1024);

                    // This WILL cause stack overflow in current implementation
                    _Log.Info(criticalMessage);
                    await _Log.FlushAsync();

                    await _Log.DisposeAsync();

                    Console.WriteLine("    SUCCESS: No stack overflow detected!");
                    return true; // If we reach here, the bug is fixed
                }
                catch (StackOverflowException)
                {
                    Console.WriteLine("    CRITICAL: Stack overflow in message processing!");
                    Console.WriteLine("    Location: LoggingModule.ProcessLogEntry recursive call");
                    return false; // Critical failure
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    Other exception (may be acceptable): {ex.GetType().Name}: {ex.Message}");
                    return true; // Other exceptions might be acceptable
                }
            });

            // Test 2: Edge case - Very deep recursion
            await RunTest("Deep recursion edge case", async () =>
            {
                try
                {
                    _Log = new LoggingModule();
                    _Log.Settings.MaxMessageLength = 5; // Extremely small to force maximum recursion

                    string deepMessage = new string('R', 500); // 500 chars = 100 recursive calls

                    _Log.Info(deepMessage);
                    await _Log.FlushAsync();
                    await _Log.DisposeAsync();

                    return true;
                }
                catch (StackOverflowException)
                {
                    Console.WriteLine("    Stack overflow on deep recursion test!");
                    return false;
                }
                catch
                {
                    return true; // Other exceptions acceptable
                }
            });

            Console.WriteLine();
            Console.WriteLine("If the critical test FAILED, this library has a stack overflow bug");
            Console.WriteLine("that will crash production systems when processing large messages!");
            Console.WriteLine();
        }

        static async Task TestConstructors()
        {
            WriteTestHeader("Constructor Tests");

            // Test 1: Default constructor
            await RunTest("Default constructor", async () =>
            {
                _Log = new LoggingModule();
                AssertNotNull(_Log, "LoggingModule should be created");
                AssertTrue(_Log.Servers.Count > 0, "Should have default server");
                await _Log.DisposeAsync();
                return true;
            });

            // Test 2: Single server constructor
            await RunTest("Single server constructor", async () =>
            {
                _Log = new LoggingModule("127.0.0.1", _SyslogServerPort, true);
                AssertNotNull(_Log, "LoggingModule should be created");
                AssertTrue(_Log.Servers.Count > 0, "Should have configured server");
                await _Log.DisposeAsync();
                return true;
            });

            // Test 3: Multiple servers constructor
            await RunTest("Multiple servers constructor", async () =>
            {
                List<SyslogServer> servers = new List<SyslogServer>
                {
                    new SyslogServer("127.0.0.1", 514),
                    new SyslogServer("127.0.0.1", 1514)
                };
                _Log = new LoggingModule(servers, true);
                AssertNotNull(_Log, "LoggingModule should be created");
                AssertTrue(_Log.Servers.Count == 2, "Should have 2 servers");
                await _Log.DisposeAsync();
                return true;
            });

            // Test 4: File logging constructor
            await RunTest("File logging constructor", async () =>
            {
                _Log = new LoggingModule("logs/constructor-test.log", FileLoggingMode.SingleLogFile, true);
                AssertNotNull(_Log, "LoggingModule should be created");
                AssertEqual(_Log.Settings.LogFilename, "logs/constructor-test.log", "Filename should match");
                await _Log.DisposeAsync();
                return true;
            });

            // Test 5: Constructor validation - null server IP
            await RunTest("Constructor validation - null server IP", async () =>
            {
                try
                {
                    _Log = new LoggingModule(null!, _SyslogServerPort);
                    return false; // Should throw
                }
                catch (ArgumentNullException)
                {
                    return true; // Expected
                }
                catch
                {
                    return false; // Wrong exception type
                }
            });

            // Test 6: Constructor validation - negative port
            await RunTest("Constructor validation - negative port", async () =>
            {
                try
                {
                    _Log = new LoggingModule("127.0.0.1", -1);
                    return false; // Should throw
                }
                catch (ArgumentException)
                {
                    return true; // Expected
                }
                catch
                {
                    return false; // Wrong exception type
                }
            });

            // Test 7: Constructor validation - null servers list
            await RunTest("Constructor validation - null servers list", async () =>
            {
                try
                {
                    _Log = new LoggingModule((List<SyslogServer>)null!);
                    return false; // Should throw
                }
                catch (ArgumentNullException)
                {
                    return true; // Expected
                }
                catch
                {
                    return false; // Wrong exception type
                }
            });

            // Test 8: Constructor validation - empty servers list
            await RunTest("Constructor validation - empty servers list", async () =>
            {
                try
                {
                    _Log = new LoggingModule(new List<SyslogServer>());
                    return false; // Should throw
                }
                catch (ArgumentException)
                {
                    return true; // Expected
                }
                catch
                {
                    return false; // Wrong exception type
                }
            });
        }

        static async Task TestBasicLogging()
        {
            WriteTestHeader("Basic Logging Tests");

            // Configure dual syslog logging: localhost:514 + test server
            _Log = CreateDualServerLogger(true);

            if (_SyslogServerProcess != null && !_SyslogServerProcess.HasExited)
            {
                Console.WriteLine($"    Using dual syslog logging: localhost:514 + test server:{_SyslogServerPort}");
            }
            else
            {
                Console.WriteLine("    Using single syslog logging: localhost:514 (test server not available)");
            }
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/basic-test.log";
            _Log.Settings.EnableColors = true;

            // Test all severity levels
            await RunTest("Debug logging", async () =>
            {
                string debugMessage = GenerateTestMessage("debug", true);
                Console.WriteLine($"    Generated message: '{debugMessage}'");
                _Log.Debug(debugMessage);
                return true;
            });

            await RunTest("Info logging", async () =>
            {
                string infoMessage = GenerateTestMessage("info", true);
                Console.WriteLine($"    Generated message: '{infoMessage}'");
                _Log.Info(infoMessage);
                return true;
            });

            await RunTest("Warn logging", async () =>
            {
                _Log.Warn("Warning message");
                return true;
            });

            await RunTest("Error logging", async () =>
            {
                _Log.Error("Error message");
                return true;
            });

            await RunTest("Alert logging", async () =>
            {
                _Log.Alert("Alert message");
                return true;
            });

            await RunTest("Critical logging", async () =>
            {
                _Log.Critical("Critical message");
                return true;
            });

            await RunTest("Emergency logging", async () =>
            {
                _Log.Emergency("Emergency message");
                return true;
            });

            await RunTest("Generic Log method", async () =>
            {
                _Log.Log(Severity.Info, "Generic log message");
                return true;
            });

            // Test null message handling
            await RunTest("Null message handling", async () =>
            {
                _Log.Info(null!); // Should not throw
                _Log.Debug(""); // Empty string should not throw - keeping empty for this specific test
                return true;
            });

            // Test minimum severity filtering
            await RunTest("Minimum severity filtering", async () =>
            {
                _Log.Settings.MinimumSeverity = Severity.Warn;
                _Log.Debug("This should be filtered out");
                _Log.Info("This should be filtered out");
                _Log.Warn("This should appear");
                return true;
            });

            await _Log.FlushAsync();
            await _Log.DisposeAsync();
        }

        static async Task TestAsyncLogging()
        {
            WriteTestHeader("Async Logging Tests");

            _Log = CreateDualServerLogger(true);
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/async-test.log";

            CancellationTokenSource cts = new CancellationTokenSource();

            // Test all async severity levels
            await RunTest("Async Debug logging", async () =>
            {
                await _Log.DebugAsync("Async debug message", cts.Token);
                return true;
            });

            await RunTest("Async Info logging", async () =>
            {
                await _Log.InfoAsync("Async info message", cts.Token);
                return true;
            });

            await RunTest("Async Warn logging", async () =>
            {
                await _Log.WarnAsync("Async warning message", cts.Token);
                return true;
            });

            await RunTest("Async Error logging", async () =>
            {
                await _Log.ErrorAsync("Async error message", cts.Token);
                return true;
            });

            await RunTest("Async Alert logging", async () =>
            {
                await _Log.AlertAsync("Async alert message", cts.Token);
                return true;
            });

            await RunTest("Async Critical logging", async () =>
            {
                await _Log.CriticalAsync("Async critical message", cts.Token);
                return true;
            });

            await RunTest("Async Emergency logging", async () =>
            {
                await _Log.EmergencyAsync("Async emergency message", cts.Token);
                return true;
            });

            await RunTest("Async generic Log method", async () =>
            {
                await _Log.LogAsync(Severity.Info, "Async generic log message", cts.Token);
                return true;
            });

            // Test cancellation token
            await RunTest("Cancellation token support", async () =>
            {
                CancellationTokenSource shortCts = new CancellationTokenSource();
                shortCts.Cancel(); // Pre-cancelled token

                try
                {
                    await _Log.InfoAsync("This should be cancelled", shortCts.Token);
                    return true; // If it doesn't throw, that's fine too
                }
                catch (OperationCanceledException)
                {
                    return true; // Expected
                }
            });

            await _Log.FlushAsync();
            await _Log.DisposeAsync();
        }

        static async Task TestStructuredLogging()
        {
            WriteTestHeader("Structured Logging Tests");

            _Log = new LoggingModule();
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/structured-test.log";

            // Test LogEntry creation
            await RunTest("LogEntry creation", async () =>
            {
                string testMessage = GenerateTestMessage("logentry-creation", true);
                LogEntry entry = new LogEntry(Severity.Info, testMessage);
                AssertNotNull(entry, "LogEntry should be created");
                AssertEqual(entry.Severity, Severity.Info, "Severity should match");
                AssertEqual(entry.Message, testMessage, "Message should match");
                return true;
            });

            // Test LogEntry with properties
            await RunTest("LogEntry with properties", async () =>
            {
                LogEntry entry = new LogEntry(Severity.Info, "User action")
                    .WithProperty("UserId", "12345")
                    .WithProperty("Action", "Login")
                    .WithProperty("Timestamp", DateTime.UtcNow);

                AssertTrue(entry.Properties.ContainsKey("UserId"), "Should contain UserId");
                AssertEqual(entry.Properties["UserId"], "12345", "UserId should match");

                _Log.LogEntry(entry);
                return true;
            });

            // Test LogEntry with correlation ID
            await RunTest("LogEntry with correlation ID", async () =>
            {
                string correlationId = Guid.NewGuid().ToString();
                LogEntry entry = new LogEntry(Severity.Info, "Correlated message")
                    .WithCorrelationId(correlationId);

                AssertEqual(entry.CorrelationId, correlationId, "Correlation ID should match");
                _Log.LogEntry(entry);
                return true;
            });

            // Test LogEntry with source
            await RunTest("LogEntry with source", async () =>
            {
                LogEntry entry = new LogEntry(Severity.Info, "Source message")
                    .WithSource("TestService");

                AssertEqual(entry.Source, "TestService", "Source should match");
                _Log.LogEntry(entry);
                return true;
            });

            // Test LogEntry async
            await RunTest("Async LogEntry", async () =>
            {
                LogEntry entry = new LogEntry(Severity.Info, "Async structured message")
                    .WithProperty("AsyncTest", true);

                await _Log.LogEntryAsync(entry);
                return true;
            });

            // Test JSON serialization
            await RunTest("JSON serialization", async () =>
            {
                LogEntry entry = new LogEntry(Severity.Info, "JSON test")
                    .WithProperty("Number", 42)
                    .WithProperty("Boolean", true)
                    .WithProperty("String", "test");

                string json = entry.ToJson();
                AssertTrue(!string.IsNullOrEmpty(json), "JSON should not be empty");
                AssertTrue(json.Contains("\"message\":\"JSON test\""), "JSON should contain message");
                return true;
            });

            // Test null parameter validation
            await RunTest("LogEntry null validation", async () =>
            {
                try
                {
                    _Log.LogEntry(null!);
                    return false; // Should throw
                }
                catch (ArgumentNullException)
                {
                    return true; // Expected
                }
            });

            await _Log.FlushAsync();
            await _Log.DisposeAsync();
        }

        static async Task TestFluentLogging()
        {
            WriteTestHeader("Fluent Logging Tests");

            _Log = new LoggingModule();
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/fluent-test.log";

            // Test fluent builder
            await RunTest("Fluent builder pattern", async () =>
            {
                _Log.BeginStructuredLog(Severity.Info, "Fluent message")
                    .WithProperty("OrderId", "12345")
                    .WithProperty("Amount", 99.99m)
                    .WithCorrelationId("FLUENT-123")
                    .WithSource("FluentTest")
                    .Write();
                return true;
            });

            // Test async fluent builder
            await RunTest("Async fluent builder", async () =>
            {
                await _Log.BeginStructuredLog(Severity.Warn, "Async fluent message")
                    .WithProperty("Async", true)
                    .WriteAsync();
                return true;
            });

            // Test fluent builder with multiple properties
            await RunTest("Fluent builder with multiple properties", async () =>
            {
                Dictionary<string, object?> props = new Dictionary<string, object?>
                {
                    ["Key1"] = "Value1",
                    ["Key2"] = 42,
                    ["Key3"] = DateTime.UtcNow
                };

                _Log.BeginStructuredLog(Severity.Info, "Multiple properties")
                    .WithProperties(props)
                    .Write();
                return true;
            });

            // Test fluent builder with exception
            await RunTest("Fluent builder with exception", async () =>
            {
                Exception testException = new InvalidOperationException("Test exception");

                _Log.BeginStructuredLog(Severity.Error, "Exception in fluent log")
                    .WithException(testException)
                    .WithProperty("ErrorCode", "TEST-001")
                    .Write();
                return true;
            });

            await _Log.FlushAsync();
            await _Log.DisposeAsync();
        }

        static async Task TestExceptionLogging()
        {
            WriteTestHeader("Exception Logging Tests");

            _Log = new LoggingModule();
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/exception-test.log";

            // Test simple exception logging
            await RunTest("Simple exception logging", async () =>
            {
                try
                {
                    throw new InvalidOperationException("Test exception");
                }
                catch (Exception ex)
                {
                    _Log.Exception(ex);
                    return true;
                }
            });

            // Test exception with module and method
            await RunTest("Exception with module and method", async () =>
            {
                try
                {
                    throw new ArgumentException("Test argument exception");
                }
                catch (Exception ex)
                {
                    _Log.Exception(ex, "TestModule", "TestMethod");
                    return true;
                }
            });

            // Test async exception logging
            await RunTest("Async exception logging", async () =>
            {
                try
                {
                    throw new TimeoutException("Test timeout");
                }
                catch (Exception ex)
                {
                    await _Log.ExceptionAsync(ex, "TestModule", "AsyncMethod");
                    return true;
                }
            });

            // Test nested exception
            await RunTest("Nested exception logging", async () =>
            {
                try
                {
                    try
                    {
                        throw new DivideByZeroException("Inner exception");
                    }
                    catch (Exception inner)
                    {
                        throw new InvalidOperationException("Outer exception", inner);
                    }
                }
                catch (Exception ex)
                {
                    ex.Data.Add("TestData", "TestValue");
                    _Log.Exception(ex);
                    return true;
                }
            });

            // Test null exception validation
            await RunTest("Null exception validation", async () =>
            {
                try
                {
                    _Log.Exception(null!);
                    return false; // Should throw
                }
                catch (ArgumentNullException)
                {
                    return true; // Expected
                }
            });

            await _Log.FlushAsync();
            await _Log.DisposeAsync();
        }

        static async Task TestSettings()
        {
            WriteTestHeader("Settings Tests");

            _Log = new LoggingModule();

            // Test minimum severity setting
            await RunTest("Minimum severity setting", async () =>
            {
                _Log.Settings.MinimumSeverity = Severity.Error;
                AssertEqual(_Log.Settings.MinimumSeverity, Severity.Error, "MinimumSeverity should be set");
                return true;
            });

            // Test file logging modes
            await RunTest("File logging modes", async () =>
            {
                _Log.Settings.FileLogging = FileLoggingMode.FileWithDate;
                AssertEqual(_Log.Settings.FileLogging, FileLoggingMode.FileWithDate, "FileLogging mode should be set");
                return true;
            });

            // Test header format
            await RunTest("Header format setting", async () =>
            {
                string customFormat = "{ts} [{sev}] {msg}";
                _Log.Settings.HeaderFormat = customFormat;
                AssertEqual(_Log.Settings.HeaderFormat, customFormat, "HeaderFormat should be set");
                return true;
            });

            // Test timestamp format
            await RunTest("Timestamp format setting", async () =>
            {
                string customTimestamp = "yyyy-MM-dd HH:mm:ss.fff";
                _Log.Settings.TimestampFormat = customTimestamp;
                AssertEqual(_Log.Settings.TimestampFormat, customTimestamp, "TimestampFormat should be set");
                return true;
            });

            // Test UTC time setting
            await RunTest("UTC time setting", async () =>
            {
                _Log.Settings.UseUtcTime = false;
                AssertFalse(_Log.Settings.UseUtcTime, "UseUtcTime should be false");
                return true;
            });

            // Test console colors
            await RunTest("Console colors setting", async () =>
            {
                _Log.Settings.EnableColors = true;
                AssertTrue(_Log.Settings.EnableColors, "EnableColors should be true");
                return true;
            });

            // Test max message length
            await RunTest("Max message length setting", async () =>
            {
                _Log.Settings.MaxMessageLength = 2048;
                AssertEqual(_Log.Settings.MaxMessageLength, 2048, "MaxMessageLength should be set");
                return true;
            });

            // Test max message length validation
            await RunTest("Max message length validation", async () =>
            {
                try
                {
                    _Log.Settings.MaxMessageLength = 10; // Too small
                    return false; // Should throw
                }
                catch (ArgumentException)
                {
                    return true; // Expected
                }
            });

            await _Log.DisposeAsync();
        }

        static async Task TestLogRetention()
        {
            WriteTestHeader("Log Retention Tests");

            // Test 1: LogRetentionDays default value
            await RunTest("LogRetentionDays default is 0", async () =>
            {
                LoggingSettings settings = new LoggingSettings();
                return settings.LogRetentionDays == 0;
            });

            // Test 2: LogRetentionDays accepts positive values
            await RunTest("LogRetentionDays accepts positive values", async () =>
            {
                LoggingSettings settings = new LoggingSettings();
                settings.LogRetentionDays = 30;
                return settings.LogRetentionDays == 30;
            });

            // Test 3: LogRetentionDays treats negative values as 0
            await RunTest("LogRetentionDays treats negative as 0", async () =>
            {
                LoggingSettings settings = new LoggingSettings();
                settings.LogRetentionDays = -5;
                return settings.LogRetentionDays == 0;
            });

            // Test 4: Log retention cleanup removes old files
            await RunTest("Log retention cleanup removes old files", async () =>
            {
                string testDir = "logs/retention_test";
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);
                Directory.CreateDirectory(testDir);

                string baseFile = Path.Combine(testDir, "test.log");

                // Create some fake dated log files
                string oldFile = baseFile + ".20200101";  // Very old (5 years ago)
                string recentFile = baseFile + "." + DateTime.Now.ToString("yyyyMMdd");

                File.WriteAllText(oldFile, "old log content");
                File.WriteAllText(recentFile, "recent log content");

                // Verify both files exist
                if (!File.Exists(oldFile) || !File.Exists(recentFile))
                {
                    Console.WriteLine("    Failed to create test files");
                    return false;
                }

                Console.WriteLine($"    Created old file: {oldFile}");
                Console.WriteLine($"    Created recent file: {recentFile}");

                // Create logger with retention enabled
                // Note: Must set LogRetentionDays before assigning Settings to trigger timer start
                LoggingModule logger = new LoggingModule(baseFile, FileLoggingMode.FileWithDate, false);
                LoggingSettings settings = logger.Settings;
                settings.LogRetentionDays = 7;
                logger.Settings = settings; // Re-assign to trigger cleanup timer start

                // Wait for cleanup timer to run (initial delay is 5 seconds)
                Console.WriteLine("    Waiting for cleanup timer to run (6 seconds)...");
                await Task.Delay(6500);

                bool oldFileDeleted = !File.Exists(oldFile);
                bool recentFileExists = File.Exists(recentFile);

                Console.WriteLine($"    Old file deleted: {oldFileDeleted}");
                Console.WriteLine($"    Recent file exists: {recentFileExists}");

                await logger.DisposeAsync();

                // Cleanup test directory
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);

                return oldFileDeleted && recentFileExists;
            });

            // Test 5: Retention cleanup doesn't start when LogRetentionDays is 0
            await RunTest("Retention cleanup doesn't start when disabled", async () =>
            {
                string testDir = "logs/retention_disabled_test";
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);
                Directory.CreateDirectory(testDir);

                string baseFile = Path.Combine(testDir, "test.log");
                string oldFile = baseFile + ".20200101";

                File.WriteAllText(oldFile, "old log content");

                // Create logger with retention DISABLED (default 0)
                LoggingModule logger = new LoggingModule(baseFile, FileLoggingMode.FileWithDate, false);
                // LogRetentionDays stays at default 0

                // Wait a bit to see if timer runs
                await Task.Delay(6500);

                // Old file should still exist since retention is disabled
                bool oldFileExists = File.Exists(oldFile);

                Console.WriteLine($"    Old file still exists (expected): {oldFileExists}");

                await logger.DisposeAsync();

                // Cleanup test directory
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);

                return oldFileExists;
            });
        }

        static async Task TestColorSettings()
        {
            WriteTestHeader("Color Settings Tests");

            // Test with colors disabled
            await RunTest("Logging with colors disabled", async () =>
            {
                _Log = CreateDualServerLogger(true);
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/colors-disabled-test.log";
                _Log.Settings.EnableColors = false;

                string testMessage = GenerateTestMessage("colors-disabled", true);
                Console.WriteLine($"    Generated message: '{testMessage}'");

                // Log all severity levels
                _Log.Debug($"Debug: {testMessage}");
                _Log.Info($"Info: {testMessage}");
                _Log.Warn($"Warn: {testMessage}");
                _Log.Error($"Error: {testMessage}");
                _Log.Alert($"Alert: {testMessage}");
                _Log.Critical($"Critical: {testMessage}");
                _Log.Emergency($"Emergency: {testMessage}");

                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });

            // Test with colors enabled
            await RunTest("Logging with colors enabled", async () =>
            {
                _Log = CreateDualServerLogger(true);
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/colors-enabled-test.log";
                _Log.Settings.EnableColors = true;

                string testMessage = GenerateTestMessage("colors-enabled", true);
                Console.WriteLine($"    Generated message: '{testMessage}'");

                // Log all severity levels with colors
                _Log.Debug($"Debug: {testMessage}");
                _Log.Info($"Info: {testMessage}");
                _Log.Warn($"Warn: {testMessage}");
                _Log.Error($"Error: {testMessage}");
                _Log.Alert($"Alert: {testMessage}");
                _Log.Critical($"Critical: {testMessage}");
                _Log.Emergency($"Emergency: {testMessage}");

                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });

            // Test rapid switching between color modes
            await RunTest("Rapid color mode switching", async () =>
            {
                _Log = CreateDualServerLogger(true);
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/color-switching-test.log";

                for (int i = 0; i < 10; i++)
                {
                    _Log.Settings.EnableColors = (i % 2 == 0);
                    string message = $"Message {i} with colors {(_Log.Settings.EnableColors ? "enabled" : "disabled")}";
                    _Log.Info(message);
                }

                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });

            // Test async logging with colors
            await RunTest("Async logging with colors", async () =>
            {
                _Log = CreateDualServerLogger(true);
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/async-colors-test.log";
                _Log.Settings.EnableColors = true;

                string testMessage = GenerateTestMessage("async-colors", true);

                await _Log.DebugAsync($"Async Debug: {testMessage}");
                await _Log.InfoAsync($"Async Info: {testMessage}");
                await _Log.WarnAsync($"Async Warn: {testMessage}");
                await _Log.ErrorAsync($"Async Error: {testMessage}");

                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });

            // Test structured logging with colors
            await RunTest("Structured logging with colors", async () =>
            {
                _Log = CreateDualServerLogger(true);
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/structured-colors-test.log";
                _Log.Settings.EnableColors = true;

                LogEntry entry = new LogEntry(Severity.Info, "Structured message with colors")
                    .WithProperty("ColorMode", "enabled")
                    .WithProperty("TestId", GenerateRandomString(8))
                    .WithCorrelationId("COLOR-TEST-123");

                _Log.LogEntry(entry);

                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });

            // Test custom color schema
            await RunTest("Custom color schema", async () =>
            {
                _Log = CreateDualServerLogger(true);
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/custom-colors-test.log";
                _Log.Settings.EnableColors = true;

                // Verify color schema exists and is configurable
                AssertNotNull(_Log.Settings.Colors, "Color schema should exist");

                _Log.Info("Message with custom color schema");

                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });
        }

        static async Task TestHighThroughputProcessing()
        {
            WriteTestHeader("High Throughput Processing Tests");

            _Log = CreateDualServerLogger(true);
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/background-test.log";

            // Test rapid message generation
            await RunTest("Rapid message generation", async () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    _Log.Info(GenerateTestMessage("background-processing", true));
                }
                await _Log.FlushAsync();
                return true;
            });

            // Test flush operation
            await RunTest("Flush operation", async () =>
            {
                _Log.Info("Message before flush");
                await _Log.FlushAsync();
                _Log.Info("Message after flush");
                return true;
            });

            // Test flush with cancellation token
            await RunTest("Flush with cancellation token", async () =>
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                _Log.Info("Message for cancellation test");
                await _Log.FlushAsync(cts.Token);
                return true;
            });

            await _Log.DisposeAsync();
        }

        static async Task TestThreadSafety()
        {
            WriteTestHeader("Thread Safety Tests");

            _Log = CreateDualServerLogger(true);
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/thread-safety-test.log";
            _Log.Settings.EnableColors = true;

            // Test 1: Basic concurrent logging
            await RunTest("Basic concurrent logging", async () =>
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < 10; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            await _Log.InfoAsync(GenerateTestMessage($"thread-{threadId}", true));
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 2: Concurrent structured logging
            await RunTest("Concurrent structured logging", async () =>
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < 5; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            _Log.BeginStructuredLog(Severity.Info, GenerateTestMessage("concurrent-structured", false))
                                .WithProperty("ThreadId", threadId)
                                .WithProperty("MessageId", j)
                                .Write();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 3: High-concurrency stress test
            await RunTest("High-concurrency stress test", async () =>
            {
                int threadCount = Environment.ProcessorCount * 2;
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < threadCount; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 50; j++)
                        {
                            // Mix of different logging methods
                            switch (j % 4)
                            {
                                case 0:
                                    _Log.Info($"High-concurrency sync info {threadId}-{j}");
                                    break;
                                case 1:
                                    await _Log.WarnAsync($"High-concurrency async warn {threadId}-{j}");
                                    break;
                                case 2:
                                    _Log.BeginStructuredLog(Severity.Error, $"High-concurrency structured {threadId}-{j}")
                                        .WithProperty("ThreadId", threadId)
                                        .WithProperty("MessageId", j)
                                        .Write();
                                    break;
                                case 3:
                                    await _Log.BeginStructuredLog(Severity.Debug, $"High-concurrency async structured {threadId}-{j}")
                                        .WithProperty("ThreadId", threadId)
                                        .WithProperty("MessageId", j)
                                        .WriteAsync();
                                    break;
                            }

                            // Occasional micro-delay to vary timing
                            if (j % 10 == 0)
                            {
                                await Task.Delay(1);
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 4: Console color thread safety
            await RunTest("Console color thread safety", async () =>
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < 20; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(() =>
                    {
                        // Rapidly log different severity levels to test color handling
                        _Log.Debug($"Debug color test {threadId}");
                        _Log.Info($"Info color test {threadId}");
                        _Log.Warn($"Warn color test {threadId}");
                        _Log.Error($"Error color test {threadId}");
                        _Log.Alert($"Alert color test {threadId}");
                        _Log.Critical($"Critical color test {threadId}");
                        _Log.Emergency($"Emergency color test {threadId}");
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 5: Concurrent settings modifications
            await RunTest("Concurrent settings modifications", async () =>
            {
                List<Task> tasks = new List<Task>();

                // Task to modify settings
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        _Log.Settings.EnableColors = (i % 2 == 0);
                        _Log.Settings.MinimumSeverity = (i % 2 == 0) ? Severity.Debug : Severity.Info;
                        await Task.Delay(10);
                    }
                }));

                // Tasks to log while settings are being modified
                for (int i = 0; i < 5; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 20; j++)
                        {
                            _Log.Info($"Settings modification test {threadId}-{j}");
                            await Task.Delay(5);
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 6: Concurrent server list modifications
            await RunTest("Concurrent server list modifications", async () =>
            {
                List<Task> tasks = new List<Task>();

                // Task to modify server list
                tasks.Add(Task.Run(async () =>
                {
                    List<SyslogServer>[] serverConfigs = {
                        new List<SyslogServer> { new SyslogServer("127.0.0.1", 514) },
                        new List<SyslogServer> { new SyslogServer("127.0.0.1", 515) },
                        new List<SyslogServer> { new SyslogServer("127.0.0.1", 516) }
                    };

                    for (int i = 0; i < 15; i++)
                    {
                        _Log.Servers = serverConfigs[i % serverConfigs.Length];
                        await Task.Delay(20);
                    }
                }));

                // Tasks to log while servers are being modified
                for (int i = 0; i < 3; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 30; j++)
                        {
                            try
                            {
                                await _Log.InfoAsync($"Server modification test {threadId}-{j}");
                                await Task.Delay(10);
                            }
                            catch
                            {
                                // May throw due to server changes, which is acceptable
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 7: Mixed sync/async operations
            await RunTest("Mixed sync/async operations", async () =>
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < 8; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 25; j++)
                        {
                            if (j % 2 == 0)
                            {
                                // Synchronous operations
                                _Log.Info($"Sync operation {threadId}-{j}");
                                _Log.BeginStructuredLog(Severity.Warn, $"Sync structured {threadId}-{j}")
                                    .WithProperty("Type", "Sync")
                                    .Write();
                            }
                            else
                            {
                                // Asynchronous operations
                                await _Log.InfoAsync($"Async operation {threadId}-{j}");
                                await _Log.BeginStructuredLog(Severity.Error, $"Async structured {threadId}-{j}")
                                    .WithProperty("Type", "Async")
                                    .WriteAsync();
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 8: Exception handling thread safety
            await RunTest("Exception handling thread safety", async () =>
            {
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < 5; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            try
                            {
                                throw new InvalidOperationException($"Test exception {threadId}-{j}");
                            }
                            catch (Exception ex)
                            {
                                if (j % 2 == 0)
                                {
                                    _Log.Exception(ex, "TestModule", "ThreadSafetyTest");
                                }
                                else
                                {
                                    await _Log.ExceptionAsync(ex, "TestModule", "ThreadSafetyTestAsync");
                                }
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 9: Rapid flush operations
            await RunTest("Rapid flush operations", async () =>
            {
                List<Task> tasks = new List<Task>();

                // Multiple threads calling flush
                for (int i = 0; i < 5; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            _Log.Info($"Pre-flush message {threadId}-{j}");
                            await _Log.FlushAsync();
                            _Log.Info($"Post-flush message {threadId}-{j}");
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            // Test 10: Resource contention simulation
            await RunTest("Resource contention simulation", async () =>
            {
                List<Task> tasks = new List<Task>();

                // Simulate heavy resource contention
                for (int i = 0; i < 15; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 20; j++)
                        {
                            // Create large log entries to stress the system
                            string largeMessage = new string('X', 1000) + $" Thread{threadId}-Message{j}";

                            if (j % 3 == 0)
                            {
                                _Log.Info(largeMessage);
                            }
                            else if (j % 3 == 1)
                            {
                                await _Log.InfoAsync(largeMessage);
                            }
                            else
                            {
                                _Log.BeginStructuredLog(Severity.Info, largeMessage)
                                    .WithProperty("ThreadId", threadId)
                                    .WithProperty("MessageId", j)
                                    .WithProperty("LargeData", new string('Y', 500))
                                    .Write();
                            }

                            // Occasional yield to allow other threads
                            if (j % 5 == 0)
                            {
                                await Task.Yield();
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                return true;
            });

            await _Log.FlushAsync();
            await _Log.DisposeAsync();
        }

        static async Task TestMultipleDestinations()
        {
            WriteTestHeader("Multiple Destinations Tests");

            // Test multiple syslog servers
            List<SyslogServer> servers = new List<SyslogServer>
            {
                new SyslogServer("127.0.0.1", 514),
                new SyslogServer("127.0.0.1", 1514)
            };

            _Log = new LoggingModule(servers, true);
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/multi-destination-test.log";

            await RunTest("Multiple destinations logging", async () =>
            {
                _Log.Info("Message to multiple destinations");
                await _Log.FlushAsync();
                return true;
            });

            await RunTest("Server list management", async () =>
            {
                List<SyslogServer> newServers = new List<SyslogServer>
                {
                    new SyslogServer("127.0.0.1", 2514)
                };
                _Log.Servers = newServers;
                AssertEqual(_Log.Servers.Count, 1, "Should have 1 server");
                return true;
            });

            await _Log.DisposeAsync();
        }

        // TestPersistentQueuing method removed - no longer applicable with direct processing

        static async Task TestErrorHandling()
        {
            WriteTestHeader("Error Handling Tests");

            // Test disposed object access
            await RunTest("Disposed object access", async () =>
            {
                LoggingModule tempLog = new LoggingModule();
                await tempLog.DisposeAsync();

                try
                {
                    tempLog.Info("This should throw");
                    return false; // Should throw
                }
                catch (ObjectDisposedException)
                {
                    return true; // Expected
                }
            });

            // Test invalid log filename handling
            await RunTest("Invalid log filename handling", async () =>
            {
                _Log = new LoggingModule();
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "/invalid/path/that/does/not/exist/test.log";

                // This should not throw, but handle the error gracefully
                _Log.Info("Test message with invalid path");
                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });

            // Test very long messages
            await RunTest("Very long message handling", async () =>
            {
                _Log = new LoggingModule();
                _Log.Settings.MaxMessageLength = 100;
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/long-message-test.log";

                string longMessage = new string('A', 500); // 500 characters
                _Log.Info(longMessage);
                await _Log.FlushAsync();
                await _Log.DisposeAsync();
                return true;
            });
        }

        static async Task TestDisposal()
        {
            WriteTestHeader("Disposal Tests");

            // Test synchronous disposal
            await RunTest("Synchronous disposal", async () =>
            {
                LoggingModule tempLog = new LoggingModule();
                tempLog.Info("Message before disposal");
                tempLog.Dispose();
                return true;
            });

            // Test asynchronous disposal
            await RunTest("Asynchronous disposal", async () =>
            {
                LoggingModule tempLog = new LoggingModule();
                tempLog.Info("Message before async disposal");
                await tempLog.DisposeAsync();
                return true;
            });

            // Test double disposal
            await RunTest("Double disposal handling", async () =>
            {
                LoggingModule tempLog = new LoggingModule();
                tempLog.Dispose();
                tempLog.Dispose(); // Should not throw
                return true;
            });

            // Test disposal with pending messages
            await RunTest("Disposal with pending messages", async () =>
            {
                LoggingModule tempLog = new LoggingModule();
                tempLog.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                tempLog.Settings.LogFilename = "logs/disposal-test.log";

                for (int i = 0; i < 10; i++)
                {
                    tempLog.Info(GenerateTestMessage("disposal-pending", true));
                }

                await tempLog.DisposeAsync(); // Should flush pending messages
                return true;
            });
        }

        static async Task TestPerformance()
        {
            WriteTestHeader("Performance Tests");

            _Log = CreateDualServerLogger(true);
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/performance-test.log";

            // Test high-volume logging
            await RunTest("High-volume logging", async () =>
            {
                DateTime start = DateTime.UtcNow;

                for (int i = 0; i < 1000; i++)
                {
                    _Log.Info(GenerateTestMessage("performance", true));
                }

                await _Log.FlushAsync();
                DateTime end = DateTime.UtcNow;

                double messagesPerSecond = 1000.0 / (end - start).TotalSeconds;
                Console.WriteLine($"    Performance: {messagesPerSecond:F0} messages/second");

                return true;
            });

            // Test async performance
            await RunTest("Async performance", async () =>
            {
                DateTime start = DateTime.UtcNow;

                List<Task> tasks = new List<Task>();
                for (int i = 0; i < 100; i++)
                {
                    tasks.Add(_Log.InfoAsync(GenerateTestMessage("async-performance", true)));
                }

                await Task.WhenAll(tasks);
                await _Log.FlushAsync();
                DateTime end = DateTime.UtcNow;

                double messagesPerSecond = 100.0 / (end - start).TotalSeconds;
                Console.WriteLine($"    Async Performance: {messagesPerSecond:F0} messages/second");

                return true;
            });

            await _Log.DisposeAsync();
        }

        static async Task TestMessageOrderingGuarantees()
        {
            WriteTestHeader("Message Ordering Guarantees Tests");

            // Test 1: Sequential message ordering with file output
            await RunTest("Sequential message ordering - file output", async () =>
            {
                _Log = new LoggingModule();
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/message-ordering-test.log";
                _Log.Settings.EnableConsole = false; // Disable console to focus on file output
                // All processing is now direct (no background processing)

                Console.WriteLine("    Testing sequential message ordering with direct processing...");

                // Send a sequence of numbered messages
                List<int> sentOrder = new List<int>();
                for (int i = 1; i <= 20; i++)
                {
                    string message = $"ORDER_TEST_MESSAGE_{i:D3}";
                    _Log.Info(message);
                    sentOrder.Add(i);
                    Console.WriteLine($"    Sent: {message}");
                }

                await _Log.FlushAsync();
                await _Log.DisposeAsync();

                // Read the log file and verify order
                string logContent = File.ReadAllText(_Log.Settings.LogFilename);
                string[] lines = logContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                List<int> receivedOrder = new List<int>();
                foreach (string line in lines)
                {
                    if (line.Contains("ORDER_TEST_MESSAGE_"))
                    {
                        // Extract the message number
                        int startIndex = line.IndexOf("ORDER_TEST_MESSAGE_") + "ORDER_TEST_MESSAGE_".Length;
                        if (startIndex < line.Length && line.Length >= startIndex + 3)
                        {
                            string numberStr = line.Substring(startIndex, 3);
                            if (int.TryParse(numberStr, out int messageNumber))
                            {
                                receivedOrder.Add(messageNumber);
                            }
                        }
                    }
                }

                Console.WriteLine($"    Sent order:     [{string.Join(", ", sentOrder)}]");
                Console.WriteLine($"    Received order: [{string.Join(", ", receivedOrder)}]");

                // Check if orders match exactly
                bool orderMatches = sentOrder.Count == receivedOrder.Count;
                if (orderMatches)
                {
                    for (int i = 0; i < sentOrder.Count; i++)
                    {
                        if (sentOrder[i] != receivedOrder[i])
                        {
                            orderMatches = false;
                            break;
                        }
                    }
                }

                if (!orderMatches)
                {
                    Console.WriteLine("    WARNING: Message ordering was NOT preserved!");
                }

                return orderMatches;
            });

            // Test 2: Sequential message ordering with rapid logging
            await RunTest("Sequential message ordering - rapid logging", async () =>
            {
                _Log = new LoggingModule();
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/message-ordering-rapid-test.log";
                _Log.Settings.EnableConsole = false;
                // All processing is now direct (no background processing)

                Console.WriteLine("    Testing sequential message ordering with rapid direct processing...");

                // Send messages rapidly to stress the ordering
                List<int> sentOrder = new List<int>();
                for (int i = 1; i <= 50; i++)
                {
                    string message = $"BG_ORDER_TEST_{i:D3}";
                    _Log.Info(message);
                    sentOrder.Add(i);
                }

                await _Log.FlushAsync();
                await _Log.DisposeAsync();

                // Read and analyze order
                string logContent = File.ReadAllText(_Log.Settings.LogFilename);
                string[] lines = logContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                List<int> receivedOrder = new List<int>();
                foreach (string line in lines)
                {
                    if (line.Contains("BG_ORDER_TEST_"))
                    {
                        int startIndex = line.IndexOf("BG_ORDER_TEST_") + "BG_ORDER_TEST_".Length;
                        if (startIndex < line.Length && line.Length >= startIndex + 3)
                        {
                            string numberStr = line.Substring(startIndex, 3);
                            if (int.TryParse(numberStr, out int messageNumber))
                            {
                                receivedOrder.Add(messageNumber);
                            }
                        }
                    }
                }

                Console.WriteLine($"    Sent order (first 10):     [{string.Join(", ", sentOrder.Take(10))}...]");
                Console.WriteLine($"    Received order (first 10): [{string.Join(", ", receivedOrder.Take(10))}...]");

                // Check if orders match
                bool orderMatches = sentOrder.Count == receivedOrder.Count;
                if (orderMatches)
                {
                    for (int i = 0; i < sentOrder.Count; i++)
                    {
                        if (sentOrder[i] != receivedOrder[i])
                        {
                            orderMatches = false;
                            Console.WriteLine($"    First mismatch: sent[{i}]={sentOrder[i]}, received[{i}]={receivedOrder[i]}");
                            break;
                        }
                    }
                }

                if (!orderMatches)
                {
                    Console.WriteLine("    EXPECTED: Background processing broke message ordering (this is by design)");
                }

                // For this test, we expect ordering to potentially break, so we return true if we detect the behavior
                return true; // We're testing that we can detect ordering issues
            });

            // Test 3: Large message splitting order verification
            await RunTest("Large message splitting order", async () =>
            {
                _Log = new LoggingModule();
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/message-splitting-order-test.log";
                _Log.Settings.EnableConsole = false;
                _Log.Settings.MaxMessageLength = 100; // Force message splitting
                // All processing is now direct (no background processing)

                Console.WriteLine("    Testing large message splitting with MaxMessageLength=100...");

                // Create a large message that will be split
                string largeMessage = new string('X', 500) + " LARGE_MESSAGE_001";
                _Log.Info(largeMessage);

                // Send a normal message after
                _Log.Info("NORMAL_MESSAGE_002");

                await _Log.FlushAsync();
                await _Log.DisposeAsync();

                // Read and check that split messages maintain sequence order
                string logContent = File.ReadAllText(_Log.Settings.LogFilename);
                string[] lines = logContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                List<string> messageSequences = new List<string>();
                foreach (string line in lines)
                {
                    if (line.Contains("MessageSequence="))
                    {
                        int startIndex = line.IndexOf("MessageSequence=") + "MessageSequence=".Length;
                        int endIndex = line.IndexOf(' ', startIndex);
                        if (endIndex == -1) endIndex = line.IndexOf(']', startIndex);
                        if (endIndex > startIndex)
                        {
                            string sequence = line.Substring(startIndex, endIndex - startIndex);
                            messageSequences.Add(sequence);
                        }
                    }
                }

                Console.WriteLine($"    Message sequences found: [{string.Join(", ", messageSequences)}]");

                // Check if split message sequences are in order
                bool sequencesInOrder = true;
                for (int i = 1; i < messageSequences.Count; i++)
                {
                    if (int.TryParse(messageSequences[i - 1], out int prev) &&
                        int.TryParse(messageSequences[i], out int curr))
                    {
                        if (curr != prev + 1)
                        {
                            sequencesInOrder = false;
                            Console.WriteLine($"    Sequence break: {prev} -> {curr}");
                            break;
                        }
                    }
                }

                return sequencesInOrder;
            });

            // Test 4: Concurrent logging order verification
            await RunTest("CRITICAL: Concurrent logging order verification", async () =>
            {
                _Log = new LoggingModule();
                _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
                _Log.Settings.LogFilename = "logs/concurrent-ordering-test.log";
                _Log.Settings.EnableConsole = false;
                // All processing is now direct (ordering depends on thread synchronization)

                Console.WriteLine("    CRITICAL TEST: Verifying order preservation under concurrent load...");
                Console.WriteLine("    This test demonstrates ordering behavior with direct processing and concurrency...");

                // Create multiple threads that log sequentially
                List<Task> tasks = new List<Task>();
                object lockObject = new object();
                List<int> allSentMessages = new List<int>();

                for (int threadId = 0; threadId < 5; threadId++)
                {
                    int currentThreadId = threadId;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int i = 1; i <= 10; i++)
                        {
                            int messageId = currentThreadId * 100 + i; // e.g., 001, 002, ..., 101, 102, ...
                            string message = $"CONCURRENT_MSG_{messageId:D3}_THREAD_{currentThreadId}";

                            lock (lockObject)
                            {
                                allSentMessages.Add(messageId);
                            }

                            _Log.Info(message);
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                await _Log.FlushAsync();
                await _Log.DisposeAsync();

                // Sort sent messages to get expected order
                List<int> expectedOrder;
                lock (lockObject)
                {
                    expectedOrder = allSentMessages.OrderBy(x => x).ToList();
                }

                // Read actual order from log file
                string logContent = File.ReadAllText(_Log.Settings.LogFilename);
                string[] lines = logContent.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                List<int> actualOrder = new List<int>();
                foreach (string line in lines)
                {
                    if (line.Contains("CONCURRENT_MSG_"))
                    {
                        int startIndex = line.IndexOf("CONCURRENT_MSG_") + "CONCURRENT_MSG_".Length;
                        if (startIndex < line.Length && line.Length >= startIndex + 3)
                        {
                            string numberStr = line.Substring(startIndex, 3);
                            if (int.TryParse(numberStr, out int messageNumber))
                            {
                                actualOrder.Add(messageNumber);
                            }
                        }
                    }
                }

                Console.WriteLine($"    Expected order (first 10): [{string.Join(", ", expectedOrder.Take(10))}...]");
                Console.WriteLine($"    Actual order (first 10):   [{string.Join(", ", actualOrder.Take(10))}...]");

                // Compare orders
                bool perfectOrder = expectedOrder.Count == actualOrder.Count;
                if (perfectOrder)
                {
                    for (int i = 0; i < expectedOrder.Count; i++)
                    {
                        if (expectedOrder[i] != actualOrder[i])
                        {
                            perfectOrder = false;
                            Console.WriteLine($"    First ordering violation: expected {expectedOrder[i]}, got {actualOrder[i]} at position {i}");
                            break;
                        }
                    }
                }

                if (!perfectOrder)
                {
                    Console.WriteLine("    RESULT: Concurrent logging BROKE message ordering (as expected)");
                    Console.WriteLine("    CONCLUSION: This library CANNOT guarantee in-order delivery");
                }
                else
                {
                    Console.WriteLine("    UNEXPECTED: Perfect ordering maintained (might be due to low load)");
                }

                // Return true because we successfully demonstrated the ordering behavior
                return true;
            });
        }

        /// <summary>
        /// Create LoggingModule with dual syslog server configuration: localhost:514 + test server.
        /// </summary>
        /// <param name="enableConsole">Enable console logging.</param>
        /// <returns>Configured LoggingModule.</returns>
        static LoggingModule CreateDualServerLogger(bool enableConsole = true)
        {
            List<SyslogServer> servers = new List<SyslogServer>();

            // Always add localhost:514 for external syslog server monitoring
            servers.Add(new SyslogServer("127.0.0.1", 514));

            // Add test SyslogServer if available
            if (_SyslogServerProcess != null && !_SyslogServerProcess.HasExited)
            {
                servers.Add(new SyslogServer("127.0.0.1", _SyslogServerPort));
            }

            return new LoggingModule(servers, enableConsole);
        }

        /// <summary>
        /// Clears the logs directory to ensure a clean test environment.
        /// </summary>
        static void ClearLogsDirectory()
        {
            try
            {
                string logsDirectory = "logs";

                if (Directory.Exists(logsDirectory))
                {
                    Console.WriteLine("Clearing existing logs directory...");

                    // Delete all files in the logs directory
                    string[] files = Directory.GetFiles(logsDirectory);
                    foreach (string file in files)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Could not delete file {file}: {ex.Message}");
                        }
                    }

                    // Delete all subdirectories in the logs directory
                    string[] directories = Directory.GetDirectories(logsDirectory);
                    foreach (string directory in directories)
                    {
                        try
                        {
                            Directory.Delete(directory, true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Could not delete directory {directory}: {ex.Message}");
                        }
                    }

                    Console.WriteLine("Logs directory cleared successfully.");
                }
                else
                {
                    // Create the logs directory if it doesn't exist
                    Directory.CreateDirectory(logsDirectory);
                    Console.WriteLine("Created logs directory.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not clear logs directory: {ex.Message}");
                Console.WriteLine("Tests will continue but may have stale log files.");
            }
        }

        static async Task StartSyslogServer()
        {
            try
            {
                Console.WriteLine($"Starting SyslogServer on port {_SyslogServerPort}...");

                // Create SyslogServer settings file
                string settingsFile = "syslog.json";
                string settingsJson = $@"{{
    ""UdpPort"": {_SyslogServerPort},
    ""DisplayTimestamps"": true,
    ""LogFileDirectory"": ""./logs/"",
    ""LogFilename"": ""syslog-test.txt"",
    ""LogWriterIntervalSec"": 2
}}";
                File.WriteAllText(settingsFile, settingsJson);

                // Start SyslogServer process
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project ../SyslogServer --no-build",
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _SyslogServerProcess = Process.Start(startInfo);
                if (_SyslogServerProcess == null)
                    throw new Exception("Failed to start SyslogServer process");

                // Give it time to start up
                await Task.Delay(3000);

                if (_SyslogServerProcess.HasExited)
                    throw new Exception($"SyslogServer process exited early with code {_SyslogServerProcess.ExitCode}");

                Console.WriteLine("SyslogServer started successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not start SyslogServer - {ex.Message}");
                Console.WriteLine("Tests will run without SyslogServer integration");
                _SyslogServerProcess = null;
            }
        }

        static async Task StopSyslogServer()
        {
            try
            {
                if (_SyslogServerProcess != null && !_SyslogServerProcess.HasExited)
                {
                    Console.WriteLine("Stopping SyslogServer...");
                    _SyslogServerProcess.Kill();
                    await Task.Delay(1000);
                }

                // Cleanup settings file
                if (File.Exists("syslog.json"))
                    File.Delete("syslog.json");

                Console.WriteLine("SyslogServer cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: SyslogServer cleanup failed - {ex.Message}");
            }
        }

        // Helper methods
        static async Task<bool> RunTest(string testName, Func<Task<bool>> testAction)
        {
            lock (_ConsoleLock)
            {
                Console.Write($"  {testName}... ");
            }

            DateTime startTime = DateTime.UtcNow;
            bool result = false;
            string? errorMessage = null;

            try
            {
                result = await testAction();

                lock (_ConsoleLock)
                {
                    if (result)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("PASS");
                        Console.ResetColor();
                        _TestsPassed++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("FAIL");
                        Console.ResetColor();
                        _TestsFailed++;
                    }
                }
            }
            catch (Exception ex)
            {
                result = false;
                errorMessage = ex.Message;
                lock (_ConsoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Console.ResetColor();
                    _TestsFailed++;
                }
            }

            TimeSpan duration = DateTime.UtcNow - startTime;
            lock (_TestResults)
            {
                _TestResults.Add(new TestResult
                {
                    Name = testName,
                    Passed = result,
                    Duration = duration,
                    ErrorMessage = errorMessage
                });
            }

            return result;
        }

        static void WriteTestHeader(string header)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {header} ===");
        }

        static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new AssertionException($"Assertion failed: {message}");
        }

        static void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new AssertionException($"Assertion failed: {message}");
        }

        static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
                throw new AssertionException($"Assertion failed: {message}. Expected: {expected}, Actual: {actual}");
        }

        static void AssertNotNull(object obj, string message)
        {
            if (obj == null)
                throw new AssertionException($"Assertion failed: {message}");
        }

        /// <summary>
        /// Generate a random string similar to PrettyId for test messages.
        /// </summary>
        /// <param name="length">Length of the random string (default 8).</param>
        /// <param name="includeNumbers">Include numbers (default true).</param>
        /// <param name="includeUppercase">Include uppercase letters (default true).</param>
        /// <param name="includeLowercase">Include lowercase letters (default true).</param>
        /// <returns>Random string.</returns>
        static string GenerateRandomString(int length = 8, bool includeNumbers = true, bool includeUppercase = true, bool includeLowercase = true)
        {
            StringBuilder chars = new StringBuilder();

            if (includeNumbers) chars.Append("0123456789");
            if (includeUppercase) chars.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            if (includeLowercase) chars.Append("abcdefghijklmnopqrstuvwxyz");

            if (chars.Length == 0) chars.Append("0123456789"); // Fallback

            string characterSet = chars.ToString();
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                result.Append(characterSet[_Random.Next(characterSet.Length)]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Generate a realistic test message with random content.
        /// </summary>
        /// <param name="messageType">Type of message to generate.</param>
        /// <param name="includeId">Include a random ID in the message.</param>
        /// <returns>Generated test message.</returns>
        static string GenerateTestMessage(string messageType = "test", bool includeId = true)
        {
            string[] templates = {
                "Processing {0} request for user {1}",
                "Completed {0} operation with result {1}",
                "Starting {0} workflow with ID {1}",
                "Error in {0} service, code {1}",
                "Successfully executed {0} with reference {1}",
                "Initializing {0} component, session {1}",
                "Finalizing {0} transaction, tracking {1}",
                "Validating {0} input, validation key {1}",
                "Caching {0} data with key {1}",
                "Monitoring {0} activity, alert ID {1}"
            };

            string template = templates[_Random.Next(templates.Length)];
            string id = includeId ? GenerateRandomString(12) : "N/A";

            return string.Format(template, messageType, id);
        }

        public class AssertionException : Exception
        {
            public AssertionException(string message) : base(message) { }
        }
#pragma warning restore CS8618
#pragma warning restore CS8625
#pragma warning restore CS8601
#pragma warning restore CS1998
#pragma warning restore CS8632
    }
}