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
            if (severity < _Settings.MinimumSeverity) return;

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
            if (severity < _Settings.MinimumSeverity) return;

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
            if (entry.Severity < _Settings.MinimumSeverity) return;

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
            if (entry.Severity < _Settings.MinimumSeverity) return;

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

            Log(Severity.Error, message);
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

            await LogAsync(Severity.Error, message, token).ConfigureAwait(false);
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
        public async ValueTask DisposeAsync()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Process a log entry by sending it to all configured destinations immediately.
        /// </summary>
        /// <param name="entry">Log entry to process.</param>
        private void ProcessLogEntry(LogEntry entry)
        {
            try
            {
                IEnumerable<string> messageParts = SplitMessage(entry.Message, _Settings.MaxMessageLength);
                int sequenceNumber = 1;
                bool isMultiPart = messageParts.Count() > 1;

                foreach (string messagePart in messageParts)
                {
                    LogEntry splitEntry = CreateSplitEntry(entry, messagePart, sequenceNumber, isMultiPart);

                    lock (_IoLock)
                    {
                        if (_Settings.EnableConsole) WriteToConsole(splitEntry);

                        if (_Settings.FileLogging != FileLoggingMode.Disabled 
                            && !string.IsNullOrEmpty(_Settings.LogFilename))
                        {
                            WriteToFile(splitEntry);
                        }

                        foreach (SyslogServer server in _Servers)
                        {
                            SendToSyslog(server, splitEntry);
                        }
                    }

                    sequenceNumber++;
                }
            }
            catch (Exception ex)
            {
                OnLoggingError?.Invoke(new Exception("Error processing log entry", ex));
            }
        }

        /// <summary>
        /// Process a log entry asynchronously by sending it to all configured destinations immediately.
        /// </summary>
        /// <param name="entry">Log entry to process.</param>
        /// <param name="token">Cancellation token.</param>
        private async Task ProcessLogEntryAsync(LogEntry entry, CancellationToken token)
        {
            try
            {
                IEnumerable<string> messageParts = SplitMessage(entry.Message, _Settings.MaxMessageLength);
                int sequenceNumber = 1;
                bool isMultiPart = messageParts.Count() > 1;

                foreach (string messagePart in messageParts)
                {
                    LogEntry splitEntry = CreateSplitEntry(entry, messagePart, sequenceNumber, isMultiPart);

                    if (_Settings.EnableConsole)
                    {
                        lock (_IoLock)
                        {
                            WriteToConsole(splitEntry);
                        }
                    }

                    if (_Settings.FileLogging != FileLoggingMode.Disabled && !string.IsNullOrEmpty(_Settings.LogFilename))
                    {
                        await WriteToFileAsync(splitEntry, token).ConfigureAwait(false);
                    }

                    List<SyslogServer> servers;
                    lock (_IoLock)
                    {
                        servers = new List<SyslogServer>(_Servers);
                    }

                    foreach (SyslogServer server in servers)
                    {
                        await SendToSyslogAsync(server, splitEntry, token).ConfigureAwait(false);
                    }

                    sequenceNumber++;
                }
            }
            catch (Exception ex)
            {
                OnLoggingError?.Invoke(new Exception("Error processing log entry async", ex));
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
                Exception = originalEntry.Exception
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
        private void WriteToConsole(LogEntry entry)
        {
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
                OnLoggingError?.Invoke(new Exception("Error writing to console", ex));
            }
        }

        /// <summary>
        /// Write log entry to file.
        /// </summary>
        /// <param name="entry">Log entry to write.</param>
        private void WriteToFile(LogEntry entry)
        {
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
                OnLoggingError?.Invoke(new Exception("Error writing to file", ex));
            }
        }

        /// <summary>
        /// Write log entry to file asynchronously.
        /// </summary>
        /// <param name="entry">Log entry to write.</param>
        /// <param name="token">Cancellation token.</param>
        private async Task WriteToFileAsync(LogEntry entry, CancellationToken token)
        {
            try
            {
                string formattedMessage = FormatLogEntry(entry) + Environment.NewLine;
                string filename = GetLogFilename();
                EnsureDirectoryExists(filename);

#if NET6_0_OR_GREATER
                // True async file I/O for modern .NET
                await File.AppendAllTextAsync(filename, formattedMessage, token).ConfigureAwait(false);
#else
                // For older frameworks, use FileStream with async
                using (FileStream fs = new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true))
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    await writer.WriteAsync(formattedMessage).ConfigureAwait(false);
                }
#endif
            }
            catch (Exception ex)
            {
                OnLoggingError?.Invoke(new Exception("Error writing to file async", ex));
            }
        }

        /// <summary>
        /// Send log entry to syslog server.
        /// </summary>
        /// <param name="server">Syslog server.</param>
        /// <param name="entry">Log entry to send.</param>
        private void SendToSyslog(SyslogServer server, LogEntry entry)
        {
            try
            {
                using (UdpClient client = CreateUdpClient(server.Hostname, server.Port))
                {
                    string syslogMessage = BuildSyslogMessage(entry);
                    byte[] data = Encoding.UTF8.GetBytes(syslogMessage);
                    client.Send(data, data.Length);
                }
            }
            catch (Exception ex)
            {
                OnLoggingError?.Invoke(new Exception($"Error sending to syslog {server.IpPort}", ex));
            }
        }

        /// <summary>
        /// Send log entry to syslog server asynchronously.
        /// </summary>
        /// <param name="server">Syslog server.</param>
        /// <param name="entry">Log entry to send.</param>
        /// <param name="token">Cancellation token.</param>
        private async Task SendToSyslogAsync(SyslogServer server, LogEntry entry, CancellationToken token)
        {
            try
            {
                using (UdpClient client = CreateUdpClient(server.Hostname, server.Port))
                {
                    string syslogMessage = BuildSyslogMessage(entry);
                    byte[] data = Encoding.UTF8.GetBytes(syslogMessage);
                    await client.SendAsync(data, data.Length).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                OnLoggingError?.Invoke(new Exception($"Error sending to syslog async {server.IpPort}", ex));
            }
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
                {"{app}", _ => GetProcessName()},
                {"{correlation}", entry => entry.CorrelationId ?? ""},
                {"{source}", entry => entry.Source ?? ""}
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
                _RetentionTimer = new Timer(
                    RetentionTimerCallback,
                    null,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromMinutes(1));

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
                OnLoggingError?.Invoke(new Exception("Error during log retention cleanup", ex));
            }
        }

        /// <summary>
        /// Clean up log files older than the configured retention period.
        /// </summary>
        private void CleanupOldLogFiles()
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
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        OnLoggingError?.Invoke(new Exception($"Error deleting old log file: {filePath}", ex));
                    }
                }
            }
            catch (Exception ex)
            {
                OnLoggingError?.Invoke(new Exception($"Error enumerating log files in directory: {directory}", ex));
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
            }
        }

        #endregion
    }
}