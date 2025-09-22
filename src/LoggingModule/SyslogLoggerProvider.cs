namespace SyslogLogging
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Logger provider for Microsoft.Extensions.Logging integration.
    /// </summary>
    public class SyslogLoggerProvider : ILoggerProvider
    {
#pragma warning disable CS8632
        private readonly LoggingModule _LoggingModule;
        private readonly ConcurrentDictionary<string, SyslogLogger> _Loggers = new ConcurrentDictionary<string, SyslogLogger>();
        private bool _Disposed = false;

        /// <summary>
        /// Create a new syslog logger provider.
        /// </summary>
        /// <param name="loggingModule">The underlying logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when loggingModule is null.</exception>
        public SyslogLoggerProvider(LoggingModule loggingModule)
        {
            _LoggingModule = loggingModule ?? throw new ArgumentNullException(nameof(loggingModule));
        }

        /// <summary>
        /// Create a logger for the specified category.
        /// </summary>
        /// <param name="categoryName">The category name for the logger.</param>
        /// <returns>A logger instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when categoryName is null.</exception>
        public ILogger CreateLogger(string categoryName)
        {
            if (categoryName == null) throw new ArgumentNullException(nameof(categoryName));

            return _Loggers.GetOrAdd(categoryName, name => new SyslogLogger(name, _LoggingModule));
        }

        /// <summary>
        /// Dispose of the logger provider.
        /// </summary>
        public void Dispose()
        {
            if (!_Disposed)
            {
                _LoggingModule?.Dispose();
                _Disposed = true;
            }
        }
#pragma warning restore CS8632
    }
}