namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Represents a structured log entry with properties and context.
    /// </summary>
    public class LogEntry
    {
#pragma warning disable CS8632

        /// <summary>
        /// The severity level of the log entry.
        /// </summary>
        public Severity Severity { get; set; } = Severity.Info;

        /// <summary>
        /// The main log message. Cannot be null (empty string is allowed).
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the log entry was created. Default is UTC now.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional structured properties for the log entry.
        /// </summary>
        public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Exception associated with this log entry, if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Correlation ID for tracking related log entries.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Source context (typically class name or module).
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Thread ID where the log entry was created.
        /// </summary>
        public int ThreadId { get; set; } = System.Threading.Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// Create a new log entry.
        /// </summary>
        public LogEntry()
        {
        }

        /// <summary>
        /// Create a new log entry with the specified message and severity.
        /// </summary>
        /// <param name="severity">Log severity level.</param>
        /// <param name="message">Log message.</param>
        public LogEntry(Severity severity, string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Create a new log entry with the specified message, severity, and exception.
        /// </summary>
        /// <param name="severity">Log severity level.</param>
        /// <param name="message">Log message.</param>
        /// <param name="exception">Associated exception.</param>
        public LogEntry(Severity severity, string message, Exception? exception)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        /// <summary>
        /// Add a structured property to the log entry.
        /// </summary>
        /// <param name="key">Property key.</param>
        /// <param name="value">Property value.</param>
        /// <returns>This log entry for method chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when key is null or empty.</exception>
        public LogEntry WithProperty(string key, object? value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Property key cannot be null or empty.", nameof(key));

            Properties[key] = value;
            return this;
        }

        /// <summary>
        /// Add multiple structured properties to the log entry.
        /// </summary>
        /// <param name="properties">Dictionary of properties to add.</param>
        /// <returns>This log entry for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when properties is null.</exception>
        public LogEntry WithProperties(Dictionary<string, object?> properties)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            foreach (KeyValuePair<string, object?> kvp in properties)
            {
                Properties[kvp.Key] = kvp.Value;
            }
            return this;
        }

        /// <summary>
        /// Set the correlation ID for this log entry.
        /// </summary>
        /// <param name="correlationId">Correlation ID.</param>
        /// <returns>This log entry for method chaining.</returns>
        public LogEntry WithCorrelationId(string? correlationId)
        {
            CorrelationId = correlationId;
            return this;
        }

        /// <summary>
        /// Set the source context for this log entry.
        /// </summary>
        /// <param name="source">Source context.</param>
        /// <returns>This log entry for method chaining.</returns>
        public LogEntry WithSource(string? source)
        {
            Source = source;
            return this;
        }

        /// <summary>
        /// Serialize the log entry to JSON format.
        /// </summary>
        /// <returns>JSON representation of the log entry.</returns>
        public string ToJson()
        {
            Dictionary<string, object?> serializable = new Dictionary<string, object?>
            {
                ["timestamp"] = Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["severity"] = Severity.ToString(),
                ["message"] = Message,
                ["threadId"] = ThreadId
            };

            if (!string.IsNullOrEmpty(Source))
                serializable["source"] = Source;

            if (!string.IsNullOrEmpty(CorrelationId))
                serializable["correlationId"] = CorrelationId;

            if (Exception != null)
            {
                serializable["exception"] = new Dictionary<string, object?>
                {
                    ["type"] = Exception.GetType().FullName,
                    ["message"] = Exception.Message,
                    ["stackTrace"] = Exception.StackTrace
                };
            }

            if (Properties.Count > 0)
                serializable["properties"] = Properties;

            return JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = false });
        }

#pragma warning restore CS8632
    }
}