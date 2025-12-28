namespace SyslogLogging
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Extension methods for integrating SyslogLogging with Microsoft.Extensions.Logging.
    /// </summary>
    public static class SyslogExtensions
    {
        /// <summary>
        /// Add syslog logging to the logging builder.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="configure">Configuration action for the logging module.</param>
        /// <returns>The logging builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
        public static ILoggingBuilder AddSyslog(this ILoggingBuilder builder, Action<LoggingModule> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            LoggingModule loggingModule = new LoggingModule();
            configure?.Invoke(loggingModule);

            SyslogLoggerProvider provider = new SyslogLoggerProvider(loggingModule);
            builder.Services.AddSingleton<ILoggerProvider>(provider);

            return builder;
        }

        /// <summary>
        /// Add syslog logging to the logging builder with specific servers.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="servers">List of syslog servers.</param>
        /// <param name="enableConsole">Enable console logging.</param>
        /// <param name="configure">Configuration action for the logging module.</param>
        /// <returns>The logging builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or servers is null.</exception>
        public static ILoggingBuilder AddSyslog(this ILoggingBuilder builder, List<SyslogServer> servers, bool enableConsole = true, Action<LoggingModule> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (servers == null) throw new ArgumentNullException(nameof(servers));

            LoggingModule loggingModule = new LoggingModule(servers, enableConsole);
            configure?.Invoke(loggingModule);

            SyslogLoggerProvider provider = new SyslogLoggerProvider(loggingModule);
            builder.Services.AddSingleton<ILoggerProvider>(provider);

            return builder;
        }

        /// <summary>
        /// Add syslog logging to the logging builder with a single server.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="hostname">Syslog server hostname.</param>
        /// <param name="port">Syslog server port.</param>
        /// <param name="enableConsole">Enable console logging.</param>
        /// <param name="configure">Configuration action for the logging module.</param>
        /// <returns>The logging builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or hostname is null.</exception>
        /// <exception cref="ArgumentException">Thrown when port is invalid.</exception>
        public static ILoggingBuilder AddSyslog(this ILoggingBuilder builder, string hostname, int port, bool enableConsole = true, Action<LoggingModule> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (string.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (port < 0 || port > 65535) throw new ArgumentException("Port must be between 0 and 65535.", nameof(port));

            LoggingModule loggingModule = new LoggingModule(hostname, port, enableConsole);
            configure?.Invoke(loggingModule);

            SyslogLoggerProvider provider = new SyslogLoggerProvider(loggingModule);
            builder.Services.AddSingleton<ILoggerProvider>(provider);

            return builder;
        }

        /// <summary>
        /// Add file logging to the logging builder.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="filename">Log filename.</param>
        /// <param name="fileLoggingMode">File logging mode.</param>
        /// <param name="enableConsole">Enable console logging.</param>
        /// <param name="configure">Configuration action for the logging module.</param>
        /// <returns>The logging builder for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
        public static ILoggingBuilder AddFileLogging(this ILoggingBuilder builder, string filename, FileLoggingMode fileLoggingMode = FileLoggingMode.SingleLogFile, bool enableConsole = true, Action<LoggingModule> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            LoggingModule loggingModule = new LoggingModule(filename, fileLoggingMode, enableConsole);
            configure?.Invoke(loggingModule);

            SyslogLoggerProvider provider = new SyslogLoggerProvider(loggingModule);
            builder.Services.AddSingleton<ILoggerProvider>(provider);

            return builder;
        }

        /// <summary>
        /// Create a structured log entry builder for fluent logging.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="severity">Log severity level.</param>
        /// <param name="message">Log message.</param>
        /// <returns>Structured log entry builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public static StructuredLogBuilder BeginStructuredLog(this LoggingModule logger, Severity severity, string message)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            return new StructuredLogBuilder(logger, severity, message);
        }
    }
}