namespace Syslog
{
    using System;
    using System.Text;

    /// <summary>
    /// Settings.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// UDP port on which to listen2. Valid range: 0-65535.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not between 0 and 65535.</exception>
        public int UdpPort
        {
            get
            {
                return _UdpPort;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(UdpPort));
                _UdpPort = value;
            }
        }

        /// <summary>
        /// Flag to enable or disable displaying timestamps.
        /// </summary>
        public bool DisplayTimestamps { get; set; } = false;

        /// <summary>
        /// Directory in which to write log files. Cannot be null or empty.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is null or empty.</exception>
        public string LogFileDirectory
        {
            get { return _LogFileDirectory; }
            set { _LogFileDirectory = !string.IsNullOrEmpty(value) ? value : throw new ArgumentException("Log file directory cannot be null or empty.", nameof(LogFileDirectory)); }
        }

        /// <summary>
        /// Log filename. Cannot be null or empty.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is null or empty.</exception>
        public string LogFilename
        {
            get { return _LogFilename; }
            set { _LogFilename = !string.IsNullOrEmpty(value) ? value : throw new ArgumentException("Log filename cannot be null or empty.", nameof(LogFilename)); }
        }

        /// <summary>
        /// Number of seconds between each log file update. Valid range: 1 or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is less than 1.</exception>
        public int LogWriterIntervalSec
        {
            get
            {
                return _LogWriterIntervalSec;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(LogWriterIntervalSec));
                _LogWriterIntervalSec = value;
            }
        }

        private int _UdpPort = 514;
        private int _LogWriterIntervalSec = 5;
        private string _LogFileDirectory = "./logs/";
        private string _LogFilename = "log.txt";

        /// <summary>
        /// Settings.
        /// </summary>
        public Settings()
        {

        }

        /// <summary>
        /// Human readable representation of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Syslog server settings  : " + Environment.NewLine);
            sb.Append("  UDP port              : " + UdpPort + Environment.NewLine);
            sb.Append("  Display timestamps    : " + DisplayTimestamps + Environment.NewLine);
            sb.Append("  Log file directory    : " + LogFileDirectory + Environment.NewLine);
            sb.Append("  Log filename          : " + LogFilename + Environment.NewLine);
            sb.Append("  Writer interval (sec) : " + LogWriterIntervalSec + Environment.NewLine);
            return sb.ToString();
        }
    }
}