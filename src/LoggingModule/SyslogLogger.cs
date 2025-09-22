namespace SyslogLogging
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Logger implementation for Microsoft.Extensions.Logging integration.
    /// </summary>
    internal class SyslogLogger : ILogger
    {
#pragma warning disable CS8632
        private readonly string _CategoryName;
        private readonly LoggingModule _LoggingModule;

        /// <summary>
        /// Create a new syslog logger.
        /// </summary>
        /// <param name="categoryName">The category name for this logger.</param>
        /// <param name="loggingModule">The underlying logging module.</param>
        public SyslogLogger(string categoryName, LoggingModule loggingModule)
        {
            _CategoryName = categoryName;
            _LoggingModule = loggingModule;
        }

        /// <summary>
        /// Begin a logical operation scope.
        /// </summary>
        /// <typeparam name="TState">The type of the state.</typeparam>
        /// <param name="state">The identifier for the scope.</param>
        /// <returns>A disposable scope object.</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null; // Scopes not implemented
        }

        /// <summary>
        /// Check if the given log level is enabled.
        /// </summary>
        /// <param name="logLevel">The log level to check.</param>
        /// <returns>True if the log level is enabled.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            Severity severity = ConvertLogLevel(logLevel);
            return severity >= _LoggingModule.Settings.MinimumSeverity;
        }

        /// <summary>
        /// Write a log entry.
        /// </summary>
        /// <typeparam name="TState">The type of the state.</typeparam>
        /// <param name="logLevel">The log level.</param>
        /// <param name="eventId">The event ID.</param>
        /// <param name="state">The state.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="formatter">The formatter function.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            if (formatter == null) throw new ArgumentNullException(nameof(formatter));

            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null) return;

            Severity severity = ConvertLogLevel(logLevel);

            LogEntry entry = new LogEntry(severity, message, exception)
                .WithSource(_CategoryName);

            if (eventId.Id != 0)
                entry.WithProperty("EventId", eventId.Id);

            if (!string.IsNullOrEmpty(eventId.Name))
                entry.WithProperty("EventName", eventId.Name);

            // Extract structured properties from state if it's a collection
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                Dictionary<string, object?> structuredProps = new Dictionary<string, object?>();
                foreach (KeyValuePair<string, object?> kvp in properties)
                {
                    if (!kvp.Key.StartsWith("{") && !kvp.Key.Equals("OriginalFormat", StringComparison.Ordinal))
                    {
                        structuredProps[kvp.Key] = kvp.Value;
                    }
                }
                if (structuredProps.Count > 0)
                    entry.WithProperties(structuredProps);
            }

            _LoggingModule.LogEntry(entry);
        }

        private static Severity ConvertLogLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => Severity.Debug,
                LogLevel.Debug => Severity.Debug,
                LogLevel.Information => Severity.Info,
                LogLevel.Warning => Severity.Warn,
                LogLevel.Error => Severity.Error,
                LogLevel.Critical => Severity.Critical,
                LogLevel.None => Severity.Debug,
                _ => Severity.Info
            };
        }
#pragma warning restore CS8632
    }
}