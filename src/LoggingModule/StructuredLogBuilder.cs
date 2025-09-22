namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Fluent builder for creating structured log entries.
    /// </summary>
    public class StructuredLogBuilder
    {
#pragma warning disable CS8632
        private readonly LoggingModule _Logger;
        private readonly LogEntry _Entry;

        /// <summary>
        /// Create a new structured log builder.
        /// </summary>
        /// <param name="logger">The logging module.</param>
        /// <param name="severity">Log severity level.</param>
        /// <param name="message">Log message.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        internal StructuredLogBuilder(LoggingModule logger, Severity severity, string message)
        {
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _Entry = new LogEntry(severity, message ?? string.Empty);
        }

        /// <summary>
        /// Add a property to the log entry.
        /// </summary>
        /// <param name="key">Property key.</param>
        /// <param name="value">Property value.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
        public StructuredLogBuilder WithProperty(string key, object? value)
        {
            _Entry.WithProperty(key, value);
            return this;
        }

        /// <summary>
        /// Add multiple properties to the log entry.
        /// </summary>
        /// <param name="properties">Dictionary of properties.</param>
        /// <returns>This builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when properties is null.</exception>
        public StructuredLogBuilder WithProperties(Dictionary<string, object?> properties)
        {
            _Entry.WithProperties(properties);
            return this;
        }

        /// <summary>
        /// Set the correlation ID for the log entry.
        /// </summary>
        /// <param name="correlationId">Correlation ID.</param>
        /// <returns>This builder for method chaining.</returns>
        public StructuredLogBuilder WithCorrelationId(string? correlationId)
        {
            _Entry.WithCorrelationId(correlationId);
            return this;
        }

        /// <summary>
        /// Set the source context for the log entry.
        /// </summary>
        /// <param name="source">Source context.</param>
        /// <returns>This builder for method chaining.</returns>
        public StructuredLogBuilder WithSource(string? source)
        {
            _Entry.WithSource(source);
            return this;
        }

        /// <summary>
        /// Set the exception for the log entry.
        /// </summary>
        /// <param name="exception">Exception.</param>
        /// <returns>This builder for method chaining.</returns>
        public StructuredLogBuilder WithException(Exception? exception)
        {
            _Entry.Exception = exception;
            return this;
        }

        /// <summary>
        /// Write the log entry.
        /// </summary>
        public void Write()
        {
            _Logger.LogEntry(_Entry);
        }

        /// <summary>
        /// Write the log entry asynchronously.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        public async System.Threading.Tasks.Task WriteAsync(System.Threading.CancellationToken token = default)
        {
            await _Logger.LogEntryAsync(_Entry, token).ConfigureAwait(false);
        }
#pragma warning restore CS8632
    }
}