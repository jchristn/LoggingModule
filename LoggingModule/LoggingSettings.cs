using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SyslogLogging
{
    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// Header format.  Provide a string that specifies how the preamble of each message should be structured.  You can use variables including:
        /// {ts}: UTC timestamp
        /// {host}: Hostname
        /// {thread}: Thread ID
        /// {sev}: Severity
        /// Default: {ts} {host} {thread} {sev}
        /// A space will be inserted between the header and the message.
        /// </summary>
        public string HeaderFormat
        {
            get
            {
                return _HeaderFormat;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) _HeaderFormat = "";
                else _HeaderFormat = value;
            }
        }

        /// <summary>
        /// Timestamp format.
        /// </summary>
        public string TimestampFormat
        {
            get
            {
                return _TimestampFormat;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(HeaderFormat));
                _TimestampFormat = value;
            }
        }

        /// <summary>
        /// Enable or disable console logging.  
        /// Settings this to true will first validate if a console exists. 
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
        public bool EnableColors { get; set; } = true;

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
                    string dir = Path.GetDirectoryName(LogFilename);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                }
            }
        }

        /// <summary>
        /// The severity level to use when logging exceptions through the .Exception() method.  
        /// </summary>
        public Severity ExceptionSeverity { get; set; } = Severity.Alert;

        /// <summary>
        /// Maximum message length.  Must be greater than or equal to 32.  Default is 1024.
        /// </summary>
        public int MaxMessageLength
        {
            get
            {
                return _MaxMessageLength;
            }
            set
            {
                if (value < 32) throw new ArgumentException("Maximum message length must be at least 32.");
                _MaxMessageLength = value;
            }
        }

        #endregion

        #region Private-Members

        private string _HeaderFormat = "{ts} {host} {thread} {sev}";
        private string _TimestampFormat = "yyyy-MM-dd HH:mm:ss";
        private bool _EnableConsole = true;
        private int _MaxMessageLength = 1024;
        private ColorSchema _Colors = new ColorSchema();
        private string _LogFilename = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public LoggingSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

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

        #endregion
    }
}
