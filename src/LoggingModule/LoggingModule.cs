namespace SyslogLogging
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Simplified syslog, console, and file logging module with direct processing.
    /// Thread-safe with immediate log delivery - no persistence or background processing.
    /// </summary>
    public class LoggingModule : IDisposable, IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// Event fired when a logging error occurs. Provides visibility into logging failures.
        /// </summary>
        public event Action<Exception> OnLoggingError;

        /// <summary>
        /// Event fired once for each log entry after it has been written to all configured
        /// destinations (console, file, and syslog). The original, unsplit <see cref="LogEntry"/>
        /// is provided even when the message was split into multiple parts for delivery.
        /// Handlers are invoked outside of any internal lock. Exceptions thrown by a handler are
        /// isolated and routed to <see cref="OnLoggingError"/>; they never interrupt logging.
        /// </summary>
        public event Action<LogEntry> MessageLogged;

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Settings
        {
            get
            {
                return _Settings;
            }
            set
            {
                _Settings = value ?? new LoggingSettings();
                InitializeHeaderFormat(); // Re-initialize when settings change
                StopLogfileCleanup();
                StartLogfileCleanup();
            }
        }

        /// <summary>
        /// List of syslog servers.
        /// </summary>
        public List<SyslogServer> Servers
        {
            get
            {
                lock (_IoLock)
                    return new List<SyslogServer>(_Servers);
            }
            set
            {
                if (value == null) value = new List<SyslogServer>();

                lock (_IoLock)
                {
                    _Servers = new List<SyslogServer>();

                    foreach (SyslogServer server in value)
                    {
                        if (!_Servers.Any(s => s.IpPort.Equals(server.IpPort)))
                            _Servers.Add(server);
                    }
                }
            }
        }

        #endregion

        #region Private-Members

        private bool _Disposed = false;
        private LoggingSettings _Settings = new LoggingSettings();

        private List<SyslogServer> _Servers = new List<SyslogServer>();
        private readonly object _IoLock = new object(); // Single lock for all I/O operations

        // Log file cleanup members
        private Timer _RetentionTimer;
        private CancellationTokenSource _RetentionCts;
        private readonly object _RetentionLock = new object();
        private bool _RetentionStarted = false;

        private string _Hostname = Dns.GetHostName();

        // Pre-compiled header format for optimization
        private string _StaticHeaderPart = string.Empty;
        private List<HeaderVariable> _DynamicVariables = new List<HeaderVariable>();

        /// <summary>
        /// Represents a dynamic variable in the header format.
        /// </summary>
        private class HeaderVariable
        {
            public string Token { get; set; }
            public Func<LogEntry, string> ValueProvider { get; set; }

            public HeaderVariable(string token, Func<LogEntry, string> valueProvider)
            {
                Token = token;
                ValueProvider = valueProvider;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public LoggingModule()
        {
            _Servers = new List<SyslogServer> { new SyslogServer("127.0.0.1", 514) };
            InitializeHeaderFormat();
            StartLogfileCleanup();
            TelemetryInstruments.ModuleCreated();
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="hostname">Hostname of the syslog server.</param>
        /// <param name="port">Port number of the syslog server.</param>
        /// <param name="enableConsole">Enable console logging.</param>
        public LoggingModule(string hostname, int port, bool enableConsole = true)
        {
            if (string.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (port < 0 || port > 65535) throw new ArgumentException("Port must be between 0 and 65535.", nameof(port));

            _Servers = new List<SyslogServer> { new SyslogServer(hostname, port) };
            _Settings.EnableConsole = enableConsole;
            InitializeHeaderFormat();
            StartLogfileCleanup();
            TelemetryInstruments.ModuleCreated();
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="servers">List of syslog servers.</param>
        /// <param name="enableConsole">Enable console logging.</param>
        public LoggingModule(List<SyslogServer> servers, bool enableConsole = true)
        {
            if (servers == null) throw new ArgumentNullException(nameof(servers));
            if (servers.Count == 0) throw new ArgumentException("At least one server must be specified.", nameof(servers));

            _Servers = new List<SyslogServer>(servers);
            _Settings.EnableConsole = enableConsole;
            InitializeHeaderFormat();
            StartLogfileCleanup();
            TelemetryInstruments.ModuleCreated();
        }

        /// <summary>
        /// Instantiate the object for file logging only.
        /// </summary>
        /// <param name="filename">Log filename.</param>
        /// <param name="fileLogging">File logging mode.</param>
        /// <param name="enableConsole">Enable console logging.</param>
        public LoggingModule(string filename, FileLoggingMode fileLogging, bool enableConsole = true)
        {
            if (string.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            _Servers = new List<SyslogServer>();
            _Settings.LogFilename = filename;
            _Settings.FileLogging = fileLogging;
            _Settings.EnableConsole = enableConsole;
            InitializeHeaderFormat();
            StartLogfileCleanup();
            TelemetryInstruments.ModuleCreated();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Write a debug log entry.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public void Debug(string message)
        {
            Log(Severity.Debug, message);
        }

        /// <summary>
        /// Write a debug log entry asynchronously.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DebugAsync(string message, CancellationToken token = default)
        {
            await LogAsync(Severity.Debug, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write an informational log entry.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public void Info(string message)
        {
            Log(Severity.Info, message);
        }

        /// <summary>
        /// Write an informational log entry asynchronously.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task InfoAsync(string message, CancellationToken token = default)
        {
            await LogAsync(Severity.Info, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a warning log entry.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public void Warn(string message)
        {
            Log(Severity.Warn, message);
        }

        /// <summary>
        /// Write a warning log entry asynchronously.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task WarnAsync(string message, CancellationToken token = default)
        {
            await LogAsync(Severity.Warn, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write an error log entry.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public void Error(string message)
        {
            Log(Severity.Error, message);
        }

        /// <summary>
        /// Write an error log entry asynchronously.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task ErrorAsync(string message, CancellationToken token = default)
        {
            await LogAsync(Severity.Error, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write an alert log entry.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public void Alert(string message)
        {
            Log(Severity.Alert, message);
        }

        /// <summary>
        /// Write an alert log entry asynchronously.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task AlertAsync(string message, CancellationToken token = default)
        {
            await LogAsync(Severity.Alert, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a critical log entry.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public void Critical(string message)
        {
            Log(Severity.Critical, message);
        }

        /// <summary>
        /// Write a critical log entry asynchronously.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task CriticalAsync(string message, CancellationToken token = default)
        {
            await LogAsync(Severity.Critical, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write an emergency log entry.
        /// </summary>
        /// <param name="message">Message to log.</param>
        public void Emergency(string message)
        {
            Log(Severity.Emergency, message);
        }

        /// <summary>
        /// Write an emergency log entry asynchronously.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task EmergencyAsync(string message, CancellationToken token = default)
        {
            await LogAsync(Severity.Emergency, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a log entry with specified severity.
        /// </summary>
        /// <param name="severity">Severity level.</param>
        /// <param name="message">Message to log.</param>
        public void Log(Severity severity, string message)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(message)) return;
            if (severity < _Settings.MinimumSeverity)
            {
                RecordFiltered(severity, TelemetryInstruments.ModeSync);
                return;
            }

            LogEntry entry = new LogEntry(severity, message);
            ProcessLogEntry(entry);
        }

        /// <summary>
        /// Write a log entry with specified severity asynchronously.
        /// </summary>
        /// <param name="severity">Severity level.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task LogAsync(Severity severity, string message, CancellationToken token = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(message)) return;
            if (severity < _Settings.MinimumSeverity)
            {
                RecordFiltered(severity, TelemetryInstruments.ModeAsync);
                return;
            }

            LogEntry entry = new LogEntry(severity, message);
            await ProcessLogEntryAsync(entry, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Write a structured log entry.
        /// </summary>
        /// <param name="entry">Log entry to write.</param>
        public void LogEntry(LogEntry entry)
        {
            ThrowIfDisposed();
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.Severity < _Settings.MinimumSeverity)
            {
                RecordFiltered(entry.Severity, TelemetryInstruments.ModeSync);
                return;
            }

            ProcessLogEntry(entry);
        }

        /// <summary>
        /// Write a structured log entry asynchronously.
        /// </summary>
        /// <param name="entry">Log entry to write.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task LogEntryAsync(LogEntry entry, CancellationToken token = default)
        {
            ThrowIfDisposed();
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.Severity < _Settings.MinimumSeverity)
            {
                RecordFiltered(entry.Severity, TelemetryInstruments.ModeAsync);
                return;
            }

            await ProcessLogEntryAsync(entry, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Log an exception.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <param name="module">Module name.</param>
        /// <param name="method">Method name.</param>
        public void Exception(Exception exception, string module = null, string method = null)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            string message = $"Exception in {module ?? "Unknown"}.{method ?? "Unknown"}: {exception.Message}";
            if (exception.StackTrace != null)
                message += Environment.NewLine + exception.StackTrace;

            Log(_Settings.ExceptionSeverity, message);
        }

        /// <summary>
        /// Log an exception asynchronously.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <param name="module">Module name.</param>
        /// <param name="method">Method name.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task ExceptionAsync(Exception exception, string module = null, string method = null, CancellationToken token = default)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            string message = $"Exception in {module ?? "Unknown"}.{method ?? "Unknown"}: {exception.Message}";
            if (exception.StackTrace != null)
                message += Environment.NewLine + exception.StackTrace;

            await LogAsync(_Settings.ExceptionSeverity, message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Begin building a structured log entry using fluent syntax.
        /// </summary>
        /// <param name="severity">Severity level.</param>
        /// <param name="message">Message to log.</param>
        /// <returns>Structured log builder.</returns>
        public StructuredLogBuilder BeginStructuredLog(Severity severity, string message)
        {
            return new StructuredLogBuilder(this, severity, message);
        }

        /// <summary>
        /// Flush any pending log entries. In direct processing mode, this is a no-op.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task FlushAsync(CancellationToken token = default)
        {
            // No-op in direct processing mode - all logs are immediately processed
            await Task.CompletedTask;
        }

        /// <summary>
        /// Dispose of the object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of the object asynchronously.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            return default;
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Process a log entry by sending it to all configured destinations immediately.
        /// </summary>
        /// <param name="entry">Log entry to process.</param>
        private void ProcessLogEntry(LogEntry entry)
        {
            LoggingSettings settings = _Settings;
            bool metrics = settings.EnableMetrics;
            long start = TelemetryInstruments.Timestamp();
            Activity activity = null;
            Exception failure = null;
            int destinationFailures = 0;

            try
            {
                List<string> messageParts = SplitMessage(entry.Message, settings.MaxMessageLength).ToList();
                int sequenceNumber = 1;
                bool isMultiPart = messageParts.Count > 1;

                if (settings.EnableTracing) activity = TelemetryInstruments.StartEntryActivity(entry, TelemetryInstruments.ModeSync, messageParts.Count);
                if (isMultiPart && metrics) TelemetryInstruments.RecordSplit(TelemetryInstruments.ModeSync);

                foreach (string messagePart in messageParts)
                {
                    LogEntry splitEntry = CreateSplitEntry(entry, messagePart, sequenceNumber, isMultiPart);

                    long waitStart = TelemetryInstruments.Timestamp();
                    lock (_IoLock)
                    {
                        if (metrics) TelemetryInstruments.RecordIoLockWait(TelemetryInstruments.ModeSync, TelemetryInstruments.ElapsedSeconds(waitStart));

                        if (_Settings.EnableConsole)
                        {
                            if (!WriteToConsole(splitEntry, sequenceNumber)) destinationFailures++;
                        }

                        if (_Settings.FileLogging != FileLoggingMode.Disabled 
                            && !string.IsNullOrEmpty(_Settings.LogFilename))
                        {
                            if (!WriteToFile(splitEntry, sequenceNumber)) destinationFailures++;
                        }

                        foreach (SyslogServer server in _Servers)
                        {
                            if (!SendToSyslog(server, splitEntry, sequenceNumber)) destinationFailures++;
                        }
                    }

                    sequenceNumber++;
                }

                RaiseMessageLogged(entry);
            }
            catch (Exception ex)
            {
                failure = ex;
                RaiseLoggingError(TelemetryInstruments.ComponentPipeline, new Exception("Error processing log entry", ex));
            }
            finally
            {
                CompleteEntry(entry, TelemetryInstruments.ModeSync, metrics, start, activity, failure, destinationFailures);
            }
        }

        /// <summary>
        /// Process a log entry asynchronously by sending it to all configured destinations immediately.
        /// </summary>
        /// <param name="entry">Log entry to process.</param>
        /// <param name="token">Cancellation token.</param>
        private async Task ProcessLogEntryAsync(LogEntry entry, CancellationToken token)
        {
            LoggingSettings settings = _Settings;
            bool metrics = settings.EnableMetrics;
            long start = TelemetryInstruments.Timestamp();
            Activity activity = null;
            Exception failure = null;
            int destinationFailures = 0;

            try
            {
                List<string> messageParts = SplitMessage(entry.Message, settings.MaxMessageLength).ToList();
                int sequenceNumber = 1;
                bool isMultiPart = messageParts.Count > 1;

                if (settings.EnableTracing) activity = TelemetryInstruments.StartEntryActivity(entry, TelemetryInstruments.ModeAsync, messageParts.Count);
                if (isMultiPart && metrics) TelemetryInstruments.RecordSplit(TelemetryInstruments.ModeAsync);

                foreach (string messagePart in messageParts)
                {
                    LogEntry splitEntry = CreateSplitEntry(entry, messagePart, sequenceNumber, isMultiPart);

                    if (_Settings.EnableConsole)
                    {
                        long waitStart = TelemetryInstruments.Timestamp();
                        lock (_IoLock)
                        {
                            if (metrics) TelemetryInstruments.RecordIoLockWait(TelemetryInstruments.ModeAsync, TelemetryInstruments.ElapsedSeconds(waitStart));
                            if (!WriteToConsole(splitEntry, sequenceNumber)) destinationFailures++;
                        }
                    }

                    if (_Settings.FileLogging != FileLoggingMode.Disabled && !string.IsNullOrEmpty(_Settings.LogFilename))
                    {
                        if (!await WriteToFileAsync(splitEntry, sequenceNumber, token).ConfigureAwait(false)) destinationFailures++;
                    }

                    List<SyslogServer> servers;
                    lock (_IoLock)
                    {
                        servers = new List<SyslogServer>(_Servers);
                    }

                    foreach (SyslogServer server in servers)
                    {
                        if (!await SendToSyslogAsync(server, splitEntry, sequenceNumber, token).ConfigureAwait(false)) destinationFailures++;
                    }

                    sequenceNumber++;
                }

                RaiseMessageLogged(entry);
            }
            catch (Exception ex)
            {
                failure = ex;
                RaiseLoggingError(TelemetryInstruments.ComponentPipeline, new Exception("Error processing log entry async", ex));
            }
            finally
            {
                CompleteEntry(entry, TelemetryInstruments.ModeAsync, metrics, start, activity, failure, destinationFailures);
            }
        }

        private void CompleteEntry(
            LogEntry entry,
            string mode,
            bool metrics,
            long start,
            Activity activity,
            Exception failure,
            int destinationFailures)
        {
            string outcome;
            if (failure != null) outcome = TelemetryInstruments.OutcomeFailure;
            else if (destinationFailures > 0) outcome = TelemetryInstruments.OutcomeDegraded;
            else outcome = TelemetryInstruments.OutcomeSuccess;

            if (metrics) TelemetryInstruments.RecordEntry(entry.Severity, mode, outcome, TelemetryInstruments.ElapsedSeconds(start));
            TelemetryInstruments.CompleteActivity(activity, outcome, failure);
        }

        private void RecordFiltered(Severity severity, string mode)
        {
            if (_Settings.EnableMetrics) TelemetryInstruments.RecordEntry(severity, mode, TelemetryInstruments.OutcomeFiltered, null);
        }

        private void RaiseLoggingError(string component, Exception exception)
        {
            if (_Settings.EnableMetrics) TelemetryInstruments.RecordError(component, exception.InnerException ?? exception);
            OnLoggingError?.Invoke(exception);
        }

        private Activity StartDestinationActivity(string destination, int part, SyslogServer server)
        {
            if (!_Settings.EnableTracing) return null;
            return TelemetryInstruments.StartDestinationActivity(destination, part, server?.Hostname, server != null ? server.Port : 0);
        }

        private void CompleteDestination(string destination, long start, Activity activity, Exception exception, SyslogServer server)
        {
            string outcome = exception == null ? TelemetryInstruments.OutcomeSuccess : TelemetryInstruments.OutcomeFailure;
            if (_Settings.EnableMetrics)
            {
                TelemetryInstruments.RecordDestination(
                    destination,
                    outcome,
                    TelemetryInstruments.ElapsedSeconds(start),
                    exception,
                    server?.Hostname,
                    server != null ? server.Port : 0);
            }

            TelemetryInstruments.CompleteActivity(activity, outcome, exception);
        }

        private void RaiseMessageLogged(LogEntry entry)
        {
            Action<LogEntry> handler = MessageLogged;
            if (handler == null) return;

            long start = TelemetryInstruments.Timestamp();
            Activity activity = _Settings.EnableTracing ? TelemetryInstruments.StartEventHandlerActivity() : null;
            Exception failure = null;

            try
            {
                handler(entry);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                string outcome = failure == null ? TelemetryInstruments.OutcomeSuccess : TelemetryInstruments.OutcomeFailure;
                if (_Settings.EnableMetrics) TelemetryInstruments.RecordEventHandler(outcome, TelemetryInstruments.ElapsedSeconds(start));
                TelemetryInstruments.CompleteActivity(activity, outcome, failure);
            }

            if (failure != null)
            {
                RaiseLoggingError(TelemetryInstruments.ComponentEventHandler, new Exception("Error in MessageLogged handler", failure));
            }
        }

        /// <summary>
        /// Split a message if it exceeds the maximum length.
        /// </summary>
        /// <param name="message">Message to split.</param>
        /// <param name="maxLength">Maximum length per chunk.</param>
        /// <returns>Enumerable of message chunks.</returns>
        private static IEnumerable<string> SplitMessage(string message, int maxLength)
        {
            if (message.Length <= maxLength)
            {
                yield return message;
                yield break;
            }

            for (int i = 0; i < message.Length; i += maxLength)
            {
                yield return message.Substring(i, Math.Min(maxLength, message.Length - i));
            }
        }

        /// <summary>
        /// Create a split log entry from the original entry and message part.
        /// </summary>
        /// <param name="originalEntry">Original log entry.</param>
        /// <param name="messagePart">Message part for this split entry.</param>
        /// <param name="sequenceNumber">Sequence number for split messages.</param>
        /// <param name="isMultiPart">Whether this is part of a multi-part message.</param>
        /// <returns>Split log entry.</returns>
        private static LogEntry CreateSplitEntry(
            LogEntry originalEntry, 
            string messagePart, 
            int sequenceNumber, 
            bool isMultiPart)
        {
            LogEntry splitEntry = new LogEntry(originalEntry.Severity, messagePart)
            {
                Timestamp = originalEntry.Timestamp,
                ThreadId = originalEntry.ThreadId,
                Source = originalEntry.Source,
                CorrelationId = originalEntry.CorrelationId,
                Exception = originalEntry.Exception,
                TraceId = originalEntry.TraceId,
                SpanId = originalEntry.SpanId
            };

            // Copy properties
            foreach (KeyValuePair<string, object> prop in originalEntry.Properties)
            {
                splitEntry.Properties[prop.Key] = prop.Value;
            }

            // Add sequence information for split messages
            if (isMultiPart)
            {
                splitEntry.WithProperty("Sequence", sequenceNumber);
                splitEntry.WithProperty("IsSplit", true);
            }

            return splitEntry;
        }

        /// <summary>
        /// Write log entry to console with color coding.
        /// </summary>
        /// <param name="entry">Log entry to write.</param>
        /// <param name="part">1-based part sequence number.</param>
        /// <returns>True if the write succeeded.</returns>
        private bool WriteToConsole(LogEntry entry, int part)
        {
            long start = TelemetryInstruments.Timestamp();
            Activity activity = StartDestinationActivity(TelemetryInstruments.DestinationConsole, part, null);
            Exception failure = null;

            try
            {
                string formattedMessage = FormatLogEntry(entry);

                // Note: _IoLock is already held by caller
                if (_Settings.EnableColors)
                {
                    ColorScheme colors = GetConsoleColors(entry.Severity);
                    Console.ForegroundColor = colors.Foreground;
                    Console.BackgroundColor = colors.Background;
                    Console.WriteLine(formattedMessage);
                    Console.ResetColor(); // Always reset to prevent color bleeding
                }
                else
                {
                    Console.WriteLine(formattedMessage);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                CompleteDestination(TelemetryInstruments.DestinationConsole, start, activity, failure, null);
            }

            if (failure != null) RaiseLoggingError(TelemetryInstruments.DestinationConsole, new Exception("Error writing to console", failure));
            return failure == null;
        }

        /// <summary>
        /// Write log entry to file.
        /// </summary>
        /// <param name="entry">Log entry to write.</param>
        /// <param name="part">1-based part sequence number.</param>
        /// <returns>True if the write succeeded.</returns>
        private bool WriteToFile(LogEntry entry, int part)
        {
            long start = TelemetryInstruments.Timestamp();
            Activity activity = StartDestinationActivity(TelemetryInstruments.DestinationFile, part, null);
            Exception failure = null;

            try
            {
                string formattedMessage = FormatLogEntry(entry);

                // Note: _IoLock is already held by caller
                string filename = GetLogFilename();
                EnsureDirectoryExists(filename);
                File.AppendAllText(filename, formattedMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                CompleteDestination(TelemetryInstruments.DestinationFile, start, activity, failure, null);
            }

            if (failure != null) RaiseLoggingError(TelemetryInstruments.DestinationFile, new Exception("Error writing to file", failure));
            return failure == null;
        }

        /// <summary>
        /// Write log entry to file asynchronously.
        /// </summary>
        /// <param name="entry">Log entry to write.</param>
        /// <param name="part">1-based part sequence number.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the write succeeded.</returns>
        private async Task<bool> WriteToFileAsync(LogEntry entry, int part, CancellationToken token)
        {
            long start = TelemetryInstruments.Timestamp();
            Activity activity = StartDestinationActivity(TelemetryInstruments.DestinationFile, part, null);
            bool metrics = _Settings.EnableMetrics;
            Exception failure = null;

            try
            {
                string formattedMessage = FormatLogEntry(entry) + Environment.NewLine;
                string filename = GetLogFilename();
                EnsureDirectoryExists(filename);

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    long waitStart = TelemetryInstruments.Timestamp();
                    lock (_IoLock)
                    {
                        if (metrics) TelemetryInstruments.RecordIoLockWait(TelemetryInstruments.ModeAsync, TelemetryInstruments.ElapsedSeconds(waitStart));
                        File.AppendAllText(filename, formattedMessage);
                    }
                }, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                CompleteDestination(TelemetryInstruments.DestinationFile, start, activity, failure, null);
            }

            if (failure != null) RaiseLoggingError(TelemetryInstruments.DestinationFile, new Exception("Error writing to file async", failure));
            return failure == null;
        }

        /// <summary>
        /// Send log entry to syslog server.
        /// </summary>
        /// <param name="server">Syslog server.</param>
        /// <param name="entry">Log entry to send.</param>
        /// <param name="part">1-based part sequence number.</param>
        /// <returns>True if the datagram was sent.</returns>
        private bool SendToSyslog(SyslogServer server, LogEntry entry, int part)
        {
            long start = TelemetryInstruments.Timestamp();
            Activity activity = StartDestinationActivity(TelemetryInstruments.DestinationSyslog, part, server);
            Exception failure = null;

            try
            {
                using (UdpClient client = CreateUdpClient(server.Hostname, server.Port))
                {
                    string syslogMessage = BuildSyslogMessage(entry);
                    byte[] data = Encoding.UTF8.GetBytes(syslogMessage);
                    int sent = client.Send(data, data.Length);
                    if (_Settings.EnableMetrics) TelemetryInstruments.RecordSyslogBytes(server.Hostname, server.Port, sent);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                CompleteDestination(TelemetryInstruments.DestinationSyslog, start, activity, failure, server);
            }

            if (failure != null) RaiseLoggingError(TelemetryInstruments.DestinationSyslog, new Exception($"Error sending to syslog {server.IpPort}", failure));
            return failure == null;
        }

        /// <summary>
        /// Send log entry to syslog server asynchronously.
        /// </summary>
        /// <param name="server">Syslog server.</param>
        /// <param name="entry">Log entry to send.</param>
        /// <param name="part">1-based part sequence number.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the datagram was sent.</returns>
        private async Task<bool> SendToSyslogAsync(SyslogServer server, LogEntry entry, int part, CancellationToken token)
        {
            long start = TelemetryInstruments.Timestamp();
            Activity activity = StartDestinationActivity(TelemetryInstruments.DestinationSyslog, part, server);
            Exception failure = null;

            try
            {
                using (UdpClient client = CreateUdpClient(server.Hostname, server.Port))
                {
                    string syslogMessage = BuildSyslogMessage(entry);
                    byte[] data = Encoding.UTF8.GetBytes(syslogMessage);
                    int sent = await client.SendAsync(data, data.Length).ConfigureAwait(false);
                    if (_Settings.EnableMetrics) TelemetryInstruments.RecordSyslogBytes(server.Hostname, server.Port, sent);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                CompleteDestination(TelemetryInstruments.DestinationSyslog, start, activity, failure, server);
            }

            if (failure != null) RaiseLoggingError(TelemetryInstruments.DestinationSyslog, new Exception($"Error sending to syslog async {server.IpPort}", failure));
            return failure == null;
        }

        /// <summary>
        /// Create a UDP client for the specified hostname and port.
        /// </summary>
        /// <param name="hostname">Hostname of the syslog server.</param>
        /// <param name="port">Port number of the syslog server.</param>
        /// <returns>UDP client connected to the server.</returns>
        private UdpClient CreateUdpClient(string hostname, int port)
        {
            try
            {
                return new UdpClient(hostname, port);
            }
            catch (Exception ex)
            {
                OnLoggingError?.Invoke(new Exception($"Failed to create UDP client for {hostname}:{port}", ex));
                throw;
            }
        }

        /// <summary>
        /// Initialize the header format by pre-compiling static and dynamic parts.
        /// </summary>
        private void InitializeHeaderFormat()
        {
            string headerFormat = _Settings.HeaderFormat;
            if (string.IsNullOrEmpty(headerFormat))
            {
                _StaticHeaderPart = string.Empty;
                _DynamicVariables.Clear();
                return;
            }

            _DynamicVariables.Clear();

            // Define all possible dynamic variables
            Dictionary<string, Func<LogEntry, string>> variableProviders = new Dictionary<string, Func<LogEntry, string>>
            {
                {"{ts}", entry => _Settings.UseUtcTime ? entry.Timestamp.ToString(_Settings.TimestampFormat) : entry.Timestamp.ToLocalTime().ToString(_Settings.TimestampFormat)},
                {"{host}", _ => Environment.MachineName},
                {"{thread}", entry => entry.ThreadId.ToString()},
                {"{sev}", entry => entry.Severity.ToString()},
                {"{level}", entry => ((int)entry.Severity).ToString()},
                {"{pid}", _ => GetProcessId()},
                {"{user}", _ => Environment.UserName ?? "unknown"},
                {"{app}", _ => GetApplicationName()},
                {"{correlation}", entry => entry.CorrelationId ?? ""},
                {"{source}", entry => entry.Source ?? ""},
                {"{trace}", entry => entry.TraceId ?? ""},
                {"{span}", entry => entry.SpanId ?? ""}
            };

            // Find all dynamic variables in the header format
            foreach (KeyValuePair<string, Func<LogEntry, string>> kvp in variableProviders)
            {
                if (headerFormat.Contains(kvp.Key))
                {
                    _DynamicVariables.Add(new HeaderVariable(kvp.Key, kvp.Value));
                }
            }

            // Pre-compute static part (everything that doesn't contain variables)
            _StaticHeaderPart = headerFormat;
            foreach (HeaderVariable variable in _DynamicVariables)
            {
                _StaticHeaderPart = _StaticHeaderPart.Replace(variable.Token, "§" + variable.Token.Substring(1, variable.Token.Length - 2) + "§");
            }
        }

        /// <summary>
        /// Get process ID safely.
        /// </summary>
        /// <returns>Process ID or "unknown" if unavailable.</returns>
        private static string GetProcessId()
        {
            try
            {
                return Process.GetCurrentProcess().Id.ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Resolve the effective application name.
        /// </summary>
        /// <returns>Configured application name, entry assembly name, process name, or "unknown".</returns>
        private string GetApplicationName()
        {
            if (!string.IsNullOrWhiteSpace(_Settings.ApplicationName))
            {
                return _Settings.ApplicationName;
            }

            try
            {
                string entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
                if (!string.IsNullOrWhiteSpace(entryAssemblyName))
                {
                    return entryAssemblyName;
                }
            }
            catch
            {
                // Fall through to process name fallback.
            }

            return GetProcessName();
        }

        /// <summary>
        /// Get process name safely.
        /// </summary>
        /// <returns>Process name or "unknown" if unavailable.</returns>
        private static string GetProcessName()
        {
            try
            {
                return Process.GetCurrentProcess().ProcessName;
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Format a log entry according to the configured header format.
        /// </summary>
        /// <param name="entry">Log entry to format.</param>
        /// <returns>Formatted log message.</returns>
        private string FormatLogEntry(LogEntry entry)
        {
            StringBuilder sb = new StringBuilder();

            // Start with static header part and apply dynamic variables
            string header = _StaticHeaderPart;
            foreach (HeaderVariable variable in _DynamicVariables)
            {
                string placeholder = "§" + variable.Token.Substring(1, variable.Token.Length - 2) + "§";
                string value = variable.ValueProvider(entry);
                header = header.Replace(placeholder, value);
            }

            sb.Append(header);

            // Add structured data if present
            if (entry.Properties.Count > 0)
            {
                sb.Append(" [");
                bool first = true;
                foreach (KeyValuePair<string, object> prop in entry.Properties)
                {
                    if (!first) sb.Append(" ");
                    sb.Append($"{prop.Key}={prop.Value}");
                    first = false;
                }
                sb.Append("]");
            }

            // Add the main message
            sb.Append(" ");
            sb.Append(entry.Message);

            return sb.ToString();
        }

        /// <summary>
        /// Build RFC3164 syslog message format.
        /// </summary>
        /// <param name="entry">Log entry to format.</param>
        /// <returns>Syslog formatted message.</returns>
        private string BuildSyslogMessage(LogEntry entry)
        {
            // Priority calculation: facility * 8 + severity
            int facility = 16; // Local use 0
            int priority = facility * 8 + (int)entry.Severity;

            string timestamp = entry.Timestamp.ToString("MMM dd HH:mm:ss");
            string hostname = _Hostname;
            string message = FormatLogEntry(entry);

            return $"<{priority}>{timestamp} {hostname} {message}";
        }

        /// <summary>
        /// Get console colors (foreground and background) for severity level.
        /// </summary>
        /// <param name="severity">Severity level.</param>
        /// <returns>Color scheme for the severity level.</returns>
        private ColorScheme GetConsoleColors(Severity severity)
        {
            return severity switch
            {
                Severity.Debug => _Settings.Colors.Debug,
                Severity.Info => _Settings.Colors.Info,
                Severity.Warn => _Settings.Colors.Warn,
                Severity.Error => _Settings.Colors.Error,
                Severity.Alert => _Settings.Colors.Alert,
                Severity.Critical => _Settings.Colors.Critical,
                Severity.Emergency => _Settings.Colors.Emergency,
                _ => new ColorScheme(ConsoleColor.White, ConsoleColor.Black)
            };
        }

        /// <summary>
        /// Get the log filename based on the file logging mode.
        /// </summary>
        /// <returns>Log filename.</returns>
        private string GetLogFilename()
        {
            if (_Settings.FileLogging == FileLoggingMode.FileWithDate)
            {
                string directory = Path.GetDirectoryName(_Settings.LogFilename) ?? "";
                string filenameWithoutExtension = Path.GetFileNameWithoutExtension(_Settings.LogFilename);
                string extension = Path.GetExtension(_Settings.LogFilename);
                string dateString = DateTime.Now.ToString("yyyyMMdd");
                return Path.Combine(directory, $"{filenameWithoutExtension}{extension}.{dateString}");
            }
            return _Settings.LogFilename;
        }

        /// <summary>
        /// Ensure the directory for the log file exists.
        /// </summary>
        /// <param name="filename">Log filename.</param>
        private void EnsureDirectoryExists(string filename)
        {
            string directory = Path.GetDirectoryName(filename);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Start the log file cleanup service if configured.
        /// </summary>
        private void StartLogfileCleanup()
        {
            // Only start if conditions are met
            if (_Settings.LogRetentionDays <= 0) return;
            if (string.IsNullOrEmpty(_Settings.LogFilename)) return;
            if (_Settings.FileLogging != FileLoggingMode.FileWithDate) return;

            lock (_RetentionLock)
            {
                if (_RetentionStarted) return;

                _RetentionCts = new CancellationTokenSource();

                // Timer fires every 60 seconds (60000 ms)
                // Initial delay of 5 seconds to allow application startup
                // Suppress execution-context flow so retention runs start their own root trace
                // instead of inheriting whatever Activity was current when the module was configured.
                bool restoreFlow = !ExecutionContext.IsFlowSuppressed();
                if (restoreFlow) ExecutionContext.SuppressFlow();
                try
                {
                    _RetentionTimer = new Timer(
                        RetentionTimerCallback,
                        null,
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromMinutes(1));
                }
                finally
                {
                    if (restoreFlow) ExecutionContext.RestoreFlow();
                }

                _RetentionStarted = true;
            }
        }

        /// <summary>
        /// Stop the log file cleanup service.
        /// </summary>
        private void StopLogfileCleanup()
        {
            lock (_RetentionLock)
            {
                if (!_RetentionStarted) return;

                _RetentionCts?.Cancel();
                _RetentionTimer?.Dispose();
                _RetentionCts?.Dispose();

                _RetentionTimer = null;
                _RetentionCts = null;
                _RetentionStarted = false;
            }
        }

        /// <summary>
        /// Timer callback for log retention cleanup.
        /// </summary>
        /// <param name="state">Timer state (unused).</param>
        private void RetentionTimerCallback(object state)
        {
            if (_Disposed) return;
            if (_RetentionCts?.IsCancellationRequested == true) return;

            try
            {
                CleanupOldLogFiles();
            }
            catch (Exception ex)
            {
                RaiseLoggingError(TelemetryInstruments.ComponentRetention, new Exception("Error during log retention cleanup", ex));
            }
        }

        /// <summary>
        /// Clean up log files older than the configured retention period.
        /// </summary>
        private void CleanupOldLogFiles()
        {
            bool metrics = _Settings.EnableMetrics;
            long start = TelemetryInstruments.Timestamp();
            Activity activity = _Settings.EnableTracing ? TelemetryInstruments.StartRetentionActivity() : null;
            int filesDeleted = 0;
            Exception failure = null;

            try
            {
                CleanupOldLogFilesCore(ref filesDeleted, ref failure);
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                string outcome = failure == null ? TelemetryInstruments.OutcomeSuccess : TelemetryInstruments.OutcomeFailure;
                if (metrics) TelemetryInstruments.RecordRetention(outcome, TelemetryInstruments.ElapsedSeconds(start), filesDeleted);
                activity?.SetTag(SyslogLoggingTelemetry.FilesDeletedAttribute, filesDeleted);
                TelemetryInstruments.CompleteActivity(activity, outcome, failure);
            }
        }

        private void CleanupOldLogFilesCore(ref int filesDeleted, ref Exception failure)
        {
            string logFilename;
            int retentionDays;
            FileLoggingMode fileLoggingMode;

            // Capture settings under lock to ensure consistency
            lock (_IoLock)
            {
                logFilename = _Settings.LogFilename;
                retentionDays = _Settings.LogRetentionDays;
                fileLoggingMode = _Settings.FileLogging;
            }

            // Validate conditions
            if (string.IsNullOrEmpty(logFilename)) return;
            if (retentionDays <= 0) return;
            if (fileLoggingMode != FileLoggingMode.FileWithDate) return;

            // Extract directory and base filename pattern
            string directory = Path.GetDirectoryName(logFilename);
            if (string.IsNullOrEmpty(directory))
            {
                directory = ".";
            }

            if (!Directory.Exists(directory)) return;

            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(logFilename);
            string extension = Path.GetExtension(logFilename);

            // Pattern: {filenameWithoutExtension}{extension}.yyyyMMdd
            // Example: mylogfile.txt.20251225
            string basePattern = filenameWithoutExtension + extension + ".";

            DateTime cutoffDate = DateTime.Now.Date.AddDays(-retentionDays);

            try
            {
                string[] files = Directory.GetFiles(directory, basePattern + "*");

                foreach (string filePath in files)
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);

                        // Extract date suffix (last 8 characters should be yyyyMMdd)
                        if (fileName.Length < basePattern.Length + 8) continue;

                        string dateSuffix = fileName.Substring(basePattern.Length);

                        // Validate it's exactly 8 digits
                        if (dateSuffix.Length != 8) continue;
                        if (!IsAllDigits(dateSuffix)) continue;

                        // Parse the date
                        if (DateTime.TryParseExact(
                            dateSuffix,
                            "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out DateTime fileDate))
                        {
                            if (fileDate < cutoffDate)
                            {
                                File.Delete(filePath);
                                filesDeleted++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        if (failure == null) failure = ex;
                        RaiseLoggingError(TelemetryInstruments.ComponentRetention, new Exception($"Error deleting old log file: {filePath}", ex));
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
                RaiseLoggingError(TelemetryInstruments.ComponentRetention, new Exception($"Error enumerating log files in directory: {directory}", ex));
            }
        }

        /// <summary>
        /// Check if a string contains only digit characters.
        /// </summary>
        /// <param name="value">String to check.</param>
        /// <returns>True if all characters are digits, false otherwise.</returns>
        private static bool IsAllDigits(string value)
        {
            foreach (char c in value)
            {
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        /// <summary>
        /// Throw ObjectDisposedException if the object is disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_Disposed)
                throw new ObjectDisposedException(nameof(LoggingModule));
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        private void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    StopLogfileCleanup();
                }

                _Disposed = true;
                TelemetryInstruments.ModuleDisposed();
            }
        }

        #endregion
    }
}
