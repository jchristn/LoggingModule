namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Represents a structured log entry with properties and context.
    /// </summary>
    public class LogEntry
    {
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
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Exception associated with this log entry, if any.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Correlation ID for tracking related log entries.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Source context (typically class name or module).
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Thread ID where the log entry was created.
        /// </summary>
        public int ThreadId { get; set; } = System.Threading.Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// W3C trace ID (32 hex characters) of the <see cref="Activity"/> that was current when
        /// the entry was created, or null when there was none. Used by the {trace} header token and JSON output
        /// to correlate log lines with distributed traces.
        /// </summary>
        public string TraceId { get; set; } = CaptureTraceId();

        /// <summary>
        /// W3C span ID (16 hex characters) of the <see cref="Activity"/> that was current when
        /// the entry was created, or null when there was none. Used by the {span} header token and JSON output.
        /// </summary>
        public string SpanId { get; set; } = CaptureSpanId();

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
        public LogEntry(Severity severity, string message, Exception exception)
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
        public LogEntry WithProperty(string key, object value)
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
        public LogEntry WithProperties(Dictionary<string, object> properties)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            foreach (KeyValuePair<string, object> kvp in properties)
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
        public LogEntry WithCorrelationId(string correlationId)
        {
            CorrelationId = correlationId;
            return this;
        }

        /// <summary>
        /// Set the source context for this log entry.
        /// </summary>
        /// <param name="source">Source context.</param>
        /// <returns>This log entry for method chaining.</returns>
        public LogEntry WithSource(string source)
        {
            Source = source;
            return this;
        }

        /// <summary>
        /// Serialize the log entry to compact JSON.
        /// <para>
        /// Safe for trimmed and Native AOT applications. The fixed fields (timestamp, severity, message, threadId,
        /// source, correlationId, traceId, spanId, exception) are always written without reflection. Property values
        /// of common scalar types (string, bool, every numeric type, char, enums, DateTime, DateTimeOffset, DateOnly,
        /// TimeOnly, TimeSpan, Guid, Uri, Version, and byte arrays) are also written without reflection, in the same
        /// format System.Text.Json uses.
        /// </para>
        /// <para>
        /// Other property values (objects, collections, dictionaries) are serialized with reflection-based
        /// System.Text.Json when the runtime allows it (<see cref="JsonSerializer.IsReflectionEnabledByDefault"/>),
        /// which is the default for regular JIT applications. When reflection-based serialization is disabled, which is
        /// the default for trimmed and Native AOT applications, dictionaries are written as JSON objects, other
        /// enumerables as JSON arrays, and any remaining value as its invariant-culture string representation. Use
        /// <see cref="ToJson(JsonSerializerOptions)"/> with a source-generated resolver to serialize complex values in full
        /// under Native AOT.
        /// </para>
        /// <para>
        /// A non-finite floating-point property value (NaN, positive or negative infinity) is written as the JSON string
        /// "NaN", "Infinity", or "-Infinity" instead of throwing. Inside a complex value serialized by reflection,
        /// System.Text.Json number handling applies instead.
        /// </para>
        /// <para>
        /// This method is not thread-safe with respect to concurrent modification of <see cref="Properties"/>.
        /// </para>
        /// </summary>
        /// <returns>JSON representation of the log entry. Never null.</returns>
        /// <exception cref="JsonException">Thrown when a complex property value cannot be serialized by reflection-based System.Text.Json, for example an object graph containing a cycle.</exception>
        /// <exception cref="NotSupportedException">Thrown when a complex property value is of a type reflection-based System.Text.Json does not support.</exception>
        public string ToJson()
        {
            return ToJsonCore(null, JsonSerializer.IsReflectionEnabledByDefault);
        }

        /// <summary>
        /// Serialize the log entry to JSON using the supplied <see cref="JsonSerializerOptions"/> for property values.
        /// <para>
        /// The fixed field names are unchanged by the options. The options control indentation
        /// (<see cref="JsonSerializerOptions.WriteIndented"/>), character escaping (<see cref="JsonSerializerOptions.Encoder"/>),
        /// maximum depth (<see cref="JsonSerializerOptions.MaxDepth"/>), and how each property value is serialized.
        /// </para>
        /// <para>
        /// Each non-null property value is serialized with the contract the options resolve for its runtime type.
        /// When <see cref="JsonSerializerOptions.TypeInfoResolver"/> is null, reflection-based System.Text.Json is used
        /// with these options if the runtime allows it (<see cref="JsonSerializer.IsReflectionEnabledByDefault"/>).
        /// To serialize complex values in trimmed or Native AOT applications, set
        /// <see cref="JsonSerializerOptions.TypeInfoResolver"/> to a source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
        /// that includes those types. A value whose type the options cannot resolve is written exactly as <see cref="ToJson()"/>
        /// would write it. Non-finite floating-point property values are always written as strings, as with <see cref="ToJson()"/>.
        /// </para>
        /// <para>
        /// As with any System.Text.Json call, the options instance becomes read-only once a property value has been
        /// serialized with it.
        /// </para>
        /// </summary>
        /// <param name="options">Serializer options. Cannot be null.</param>
        /// <returns>JSON representation of the log entry. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
        /// <exception cref="JsonException">Thrown when a property value cannot be serialized, for example an object graph containing a cycle.</exception>
        /// <exception cref="NotSupportedException">Thrown when a property value is of a type the resolved contract does not support.</exception>
        public string ToJson(JsonSerializerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            return ToJsonCore(options, JsonSerializer.IsReflectionEnabledByDefault);
        }

        internal string ToJsonCore(JsonSerializerOptions options, bool allowReflection)
        {
            JsonWriterOptions writerOptions = new JsonWriterOptions
            {
                Indented = options != null && options.WriteIndented,
                Encoder = options?.Encoder,
                MaxDepth = options != null ? options.MaxDepth : 0
            };

            using (MemoryStream stream = new MemoryStream())
            {
                using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, writerOptions))
                {
                    writer.WriteStartObject();
                    writer.WriteString("timestamp", Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                    writer.WriteString("severity", Severity.ToString());
                    writer.WriteString("message", Message);
                    writer.WriteNumber("threadId", ThreadId);

                    if (!string.IsNullOrEmpty(Source)) writer.WriteString("source", Source);
                    if (!string.IsNullOrEmpty(CorrelationId)) writer.WriteString("correlationId", CorrelationId);
                    if (!string.IsNullOrEmpty(TraceId)) writer.WriteString("traceId", TraceId);
                    if (!string.IsNullOrEmpty(SpanId)) writer.WriteString("spanId", SpanId);

                    if (Exception != null)
                    {
                        writer.WriteStartObject("exception");
                        writer.WriteString("type", Exception.GetType().FullName);
                        writer.WriteString("message", Exception.Message);
                        writer.WriteString("stackTrace", Exception.StackTrace);
                        writer.WriteEndObject();
                    }

                    if (Properties != null && Properties.Count > 0)
                    {
                        writer.WriteStartObject("properties");
                        foreach (KeyValuePair<string, object> kvp in Properties)
                        {
                            writer.WritePropertyName(kvp.Key);
                            LogEntryJsonWriter.WriteValue(writer, kvp.Value, options, allowReflection, 0);
                        }
                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }

                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }
        }

        private static string CaptureTraceId()
        {
            Activity current = Activity.Current;
            if (current == null || current.IdFormat != ActivityIdFormat.W3C) return null;
            return current.TraceId.ToHexString();
        }

        private static string CaptureSpanId()
        {
            Activity current = Activity.Current;
            if (current == null || current.IdFormat != ActivityIdFormat.W3C) return null;
            return current.SpanId.ToHexString();
        }
    }
}