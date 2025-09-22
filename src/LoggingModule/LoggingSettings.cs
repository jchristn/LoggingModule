namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Simplified logging settings for direct processing mode.
    /// </summary>
    public class LoggingSettings
    {
#pragma warning disable CS8600
#pragma warning disable CS8625
        /// <summary>
        /// Header format template. Supports the following placeholders:
        /// {ts}: Timestamp (formatted per TimestampFormat setting)
        /// {host}: Hostname/machine name
        /// {thread}: Thread ID
        /// {sev}: Severity level name (Debug, Info, Warn, etc.)
        /// {level}: Severity level number (0-7)
        /// {pid}: Process ID
        /// {user}: Current username
        /// {app}: Application/process name
        /// {domain}: Application domain name
        /// {cpu}: Number of processor cores
        /// {mem}: Current working set memory (MB)
        /// {uptime}: Process uptime (HH:mm:ss)
        /// {correlation}: Correlation ID (if present in log entry)
        /// {source}: Log source (if present in log entry)
        /// Default: {ts} {host} {sev}
        /// A space will be inserted between the header and the message.
        /// Setting to null or empty will result in an empty header.
        /// </summary>
        public string HeaderFormat
        {
            get
            {
                return _HeaderFormat;
            }
            set
            {
                _HeaderFormat = value ?? "";
            }
        }

        /// <summary>
        /// Timestamp format. Cannot be null or empty. Must be a valid DateTime format string.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is null or empty.</exception>
        public string TimestampFormat
        {
            get
            {
                return _TimestampFormat;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentException("Timestamp format cannot be null or empty.", nameof(TimestampFormat));
                _TimestampFormat = value;
            }
        }

        /// <summary>
        /// Boolean to enable or disable use of UTC timestamps.
        /// </summary>
        public bool UseUtcTime { get; set; } = true;

        /// <summary>
        /// Enable or disable console logging.
        /// Setting this to true will first validate if a console exists.
        /// If a console is not available, it will be set to false.
        /// </summary>
        public bool EnableConsole
        {
            get
            {
                return _EnableConsole;
            }
            set
            {
                if (value) _EnableConsole = ConsoleExists();
                else _EnableConsole = false;
            }
        }

        /// <summary>
        /// Minimum severity required to send a message.
        /// </summary>
        public Severity MinimumSeverity { get; set; } = Severity.Debug;

        /// <summary>
        /// Enable or disable use of color for console messages.
        /// </summary>
        public bool EnableColors { get; set; } = false;

        /// <summary>
        /// Colors to use for console messages based on message severity.
        /// </summary>
        public ColorSchema Colors
        {
            get
            {
                return _Colors;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Colors));
                _Colors = value;
            }
        }

        /// <summary>
        /// Enable or disable logging to a file.
        /// Disabled: file logging will not be used.
        /// SingleLogFile: all messages will be appended to a single file.
        /// FileWithDate: all messages will be appended to a file, where the name of the file is the supplied filename followed by '.yyyyMMdd'.
        /// </summary>
        public FileLoggingMode FileLogging = FileLoggingMode.Disabled;

        /// <summary>
        /// The file to which log messages should be appended.
        /// </summary>
        public string LogFilename
        {
            get
            {
                return _LogFilename;
            }
            set
            {
                _LogFilename = value;

                if (!String.IsNullOrEmpty(_LogFilename))
                {
                    string dir = Path.GetDirectoryName(_LogFilename);
                    if (!String.IsNullOrEmpty(dir))
                    {
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The severity level to use when logging exceptions through the .Exception() method.
        /// </summary>
        public Severity ExceptionSeverity { get; set; } = Severity.Alert;

        /// <summary>
        /// Maximum message length. Valid range: 32 or greater. Default is 1024.
        /// Messages longer than this will be split into multiple messages with sequence numbers.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 32.</exception>
        public int MaxMessageLength
        {
            get
            {
                return _MaxMessageLength;
            }
            set
            {
                if (value < 32) throw new ArgumentOutOfRangeException(nameof(MaxMessageLength), "Maximum message length must be at least 32.");
                _MaxMessageLength = value;
            }
        }

        private string _HeaderFormat = "{ts} {host} {sev}";
        private string _TimestampFormat = "yyyy-MM-dd HH:mm:ss";
        private bool _EnableConsole = true;
        private int _MaxMessageLength = 1024;
        private ColorSchema _Colors = new ColorSchema();
        private string _LogFilename = null;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public LoggingSettings()
        {

        }

        private bool ConsoleExists()
        {
            try
            {
                bool test1 = Environment.UserInteractive;
                bool test2 = Console.WindowHeight > 0;
                return test1 && test2;
            }
            catch (Exception)
            {
                return false;
            }
        }
#pragma warning restore CS8625
#pragma warning restore CS8600
    }
}