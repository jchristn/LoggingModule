using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyslogLogging
{
    /// <summary>
    /// Syslog, console, and file logging module.
    /// </summary>
    public class LoggingModule : IDisposable
    {
        #region Public-Members

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
                if (value == null) _Settings = new LoggingSettings();
                else _Settings = value;
            }
        }

        /// <summary>
        /// List of syslog servers.
        /// </summary>
        public List<SyslogServer> Servers
        {
            get
            {
                return _Servers.DistinctBy(s => s.IpPort).ToList();
            }
            set
            {
                if (value == null) _Servers = new List<SyslogServer>();
                _Servers = value.DistinctBy(s => s.IpPort).ToList();
            }
        }
         
        #endregion

        #region Private-Members

        private bool _Disposed = false;
        private LoggingSettings _Settings = new LoggingSettings();
        private List<SyslogServer> _Servers = new List<SyslogServer> { new SyslogServer("127.0.0.1", 514) };
        private readonly object _FileLock = new object();
        private string _Hostname = Dns.GetHostName();
        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object using localhost syslog (UDP port 514).
        /// </summary>
        public LoggingModule()
        {
            _Token = _TokenSource.Token;
        }

        /// <summary>
        /// Instantiate the object using the specified syslog server IP address and UDP port.
        /// </summary>
        /// <param name="serverIp">Server IP address.</param>
        /// <param name="serverPort">Server port number.</param>
        /// <param name="enableConsole">Enable or disable console logging.</param>
        public LoggingModule(
            string serverIp,
            int serverPort,
            bool enableConsole = true)
        {
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < 0) throw new ArgumentException("Server port must be zero or greater.");

            Servers = new List<SyslogServer>()
            {
                new SyslogServer
                {
                    Hostname = serverIp,
                    Port = serverPort
                }
            };

            _Settings.EnableConsole = enableConsole;
            _Token = _TokenSource.Token;
        }

        /// <summary>
        /// Instantiate the object using a series of servers.
        /// </summary>
        /// <param name="servers">Servers.</param>
        /// <param name="enableConsole">Enable or disable console logging.</param>
        public LoggingModule(
            List<SyslogServer> servers,
            bool enableConsole = true)
        {
            if (servers == null) throw new ArgumentNullException(nameof(servers));
            if (servers.Count < 1) throw new ArgumentException("At least one server must be specified.");

            Servers = servers;

            _Settings.EnableConsole = enableConsole;
            _Token = _TokenSource.Token;
        }

        /// <summary>
        /// Instantiate the object to enable either file logging or console logging.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <param name="fileLoggingMode">File logging mode.  If you specify 'FileWithDate', .yyyyMMdd will be appended to the specified filename.</param>
        /// <param name="enableConsole">Enable or disable console logging.</param>
        public LoggingModule(
            string filename,
            FileLoggingMode fileLoggingMode = FileLoggingMode.SingleLogFile,
            bool enableConsole = true)
        {
            if (String.IsNullOrEmpty(filename) && !enableConsole) throw new ArgumentException("Either a filename must be specified or console logging must be enabled.");

            _Settings.FileLogging = fileLoggingMode;
            _Settings.LogFilename = filename;
            _Settings.EnableConsole = enableConsole;
            _Token = _TokenSource.Token;
        }
       
        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the client and dispose of background workers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
          
        /// <summary>
        /// Send a log message using 'Debug' severity.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        public virtual void Debug(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Log(Severity.Debug, msg);
        }

        /// <summary>
        /// Send a log message using 'Info' severity.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        public virtual void Info(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Log(Severity.Info, msg); 
        }

        /// <summary>
        /// Send a log message using 'Warn' severity.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        public virtual void Warn(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Log(Severity.Warn, msg);
        }

        /// <summary>
        /// Send a log message using 'Error' severity.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        public virtual void Error(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Log(Severity.Error, msg);
        }

        /// <summary>
        /// Send a log message using 'Alert' severity.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        public virtual void Alert(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Log(Severity.Alert, msg);
        }

        /// <summary>
        /// Send a log message using 'Critical' severity.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        public virtual void Critical(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Log(Severity.Critical, msg);
        }

        /// <summary>
        /// Send a log message using 'Emergency' severity.
        /// </summary>
        /// <param name="msg">Message to send.</param>
        public virtual void Emergency(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Log(Severity.Emergency, msg);
        }
         
        /// <summary>
        /// Send log messages containing Exception details using 'Alert' severity.
        /// </summary>
        /// <param name="module">Module name (user-specified).</param>
        /// <param name="method">Method name (user-specified).</param>
        /// <param name="e">Exception.</param>
        public virtual void Exception(Exception e, string module = null, string method = null)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var st = new StackTrace(e, true);
            var frame = st.GetFrame(0);
            int fileLine = frame.GetFileLineNumber();
            string filename = frame.GetFileName();

            string message =
                Environment.NewLine +
                "--- Exception details ---" + Environment.NewLine +
                (!String.IsNullOrEmpty(module) ? "  Module     : " + module + Environment.NewLine : "") +
                (!String.IsNullOrEmpty(method) ? "  Method     : " + method + Environment.NewLine : "") +
                "  Type       : " + e.GetType().ToString() + Environment.NewLine;

            if (e.Data != null && e.Data.Count > 0)
            {
                message += "  Data       : " + Environment.NewLine;
                foreach (DictionaryEntry curr in e.Data)
                {
                    message += "  | " + curr.Key + ": " + curr.Value + Environment.NewLine;
                }
            }
            else
            {
                message += "  Data       : (none)" + Environment.NewLine;
            }

            message +=
                "  Inner      : ";

            if (e.InnerException == null) message += "(null)" + Environment.NewLine;
            else
            {
                message += e.InnerException.GetType().ToString() + Environment.NewLine;
                message +=
                    "    Message    : " + e.InnerException.Message + Environment.NewLine +
                    "    Source     : " + e.InnerException.Source + Environment.NewLine +
                    "    StackTrace : " + e.InnerException.StackTrace + Environment.NewLine +
                    "    ToString   : " + e.InnerException.ToString() + Environment.NewLine;

                if (e.InnerException.Data != null && e.InnerException.Data.Count > 0)
                {
                    message += "    Data       : " + Environment.NewLine;
                    foreach (DictionaryEntry curr in e.Data)
                    {
                        message += "    | " + curr.Key + ": " + curr.Value + Environment.NewLine;
                    }
                }
                else
                {
                    message += "    Data       : (none)" + Environment.NewLine;
                } 
            }

            message += 
                "  Message    : " + e.Message + Environment.NewLine +
                "  Source     : " + e.Source + Environment.NewLine +
                "  StackTrace : " + e.StackTrace + Environment.NewLine +
                "  Line       : " + fileLine + Environment.NewLine +
                "  File       : " + filename + Environment.NewLine +
                "  ToString   : " + e.ToString() + Environment.NewLine +
                "---";

            Log(_Settings.ExceptionSeverity, message);
        }

        /// <summary>
        /// Send a log message using the specified severity.
        /// </summary>
        /// <param name="sev">Severity of the message.</param>
        /// <param name="msg">Message to send.</param>
        public virtual void Log(Severity sev, string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            if (sev < _Settings.MinimumSeverity) return;

            string header = "";
            string currMsg = "";
            string remainder = "";

            if (msg.Length > _Settings.MaxMessageLength)
            {
                currMsg = msg.Substring(0, _Settings.MaxMessageLength);
                remainder = msg.Substring(_Settings.MaxMessageLength, (msg.Length - _Settings.MaxMessageLength));
            }
            else
            {
                currMsg = msg;
            }
            
            header = _Settings.HeaderFormat;
            if (header.Contains("{ts}")) 
                header = header.Replace("{ts}", DateTime.Now.ToUniversalTime().ToString(_Settings.TimestampFormat));
            if (header.Contains("{host}")) 
                header = header.Replace("{host}", _Hostname);
            if (header.Contains("{thread}")) 
                header = header.Replace("{thread}", Thread.CurrentThread.ManagedThreadId.ToString());
            if (header.Contains("{sev}")) 
                header = header.Replace("{sev}", sev.ToString());

            string message = header + " " + currMsg;

            if (_Settings.EnableConsole)
            {
                SendConsole(sev, message);
            }

            if (!String.IsNullOrEmpty(_Settings.LogFilename) && _Settings.FileLogging != FileLoggingMode.Disabled)
            {
                SendFile(sev, message);
            }

            if (Servers != null && Servers.Count > 0)
            {
                List<SyslogServer> servers = new List<SyslogServer>(Servers);
                SendServers(servers, message);
            }
             
            if (!String.IsNullOrEmpty(remainder))
            {
                Log(sev, remainder);
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of the resource.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                _TokenSource.Cancel();
            }

            _Disposed = true;
        }

        private void SendConsole(Severity sev, string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            if (!_Settings.EnableConsole) return;
            if (_Settings.EnableColors)
            {
                ConsoleColor prevForeground = Console.ForegroundColor;
                ConsoleColor prevBackground = Console.BackgroundColor;

                if (_Settings.Colors != null)
                {
                    switch (sev)
                    {
                        case Severity.Debug:
                            Console.ForegroundColor = _Settings.Colors.Debug.Foreground;
                            Console.BackgroundColor = _Settings.Colors.Debug.Background;
                            break;
                        case Severity.Info:
                            Console.ForegroundColor = _Settings.Colors.Info.Foreground;
                            Console.BackgroundColor = _Settings.Colors.Info.Background;
                            break;
                        case Severity.Warn:
                            Console.ForegroundColor = _Settings.Colors.Warn.Foreground;
                            Console.BackgroundColor = _Settings.Colors.Warn.Background;
                            break;
                        case Severity.Error:
                            Console.ForegroundColor = _Settings.Colors.Error.Foreground;
                            Console.BackgroundColor = _Settings.Colors.Error.Background;
                            break;
                        case Severity.Alert:
                            Console.ForegroundColor = _Settings.Colors.Alert.Foreground;
                            Console.BackgroundColor = _Settings.Colors.Alert.Background;
                            break;
                        case Severity.Critical:
                            Console.ForegroundColor = _Settings.Colors.Critical.Foreground;
                            Console.BackgroundColor = _Settings.Colors.Critical.Background;
                            break;
                        case Severity.Emergency:
                            Console.ForegroundColor = _Settings.Colors.Emergency.Foreground;
                            Console.BackgroundColor = _Settings.Colors.Emergency.Background;
                            break;
                    }
                }

                Console.WriteLine(msg);
                Console.ForegroundColor = prevForeground;
                Console.BackgroundColor = prevBackground;
            }
            else
            {
                Console.WriteLine(msg);
            }
        }

        private void SendFile(Severity sev, string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            if (String.IsNullOrEmpty(_Settings.LogFilename)) return;

            switch (_Settings.FileLogging)
            {
                case FileLoggingMode.Disabled:
                    return;

                case FileLoggingMode.SingleLogFile:
                    lock (_FileLock)
                    {
                        File.AppendAllText(_Settings.LogFilename, msg + Environment.NewLine);
                    }
                    return;

                case FileLoggingMode.FileWithDate:
                    string filename = _Settings.LogFilename + "." + DateTime.Now.ToString("yyyyMMdd");
                    lock (_FileLock)
                    {
                        File.AppendAllText(filename, msg + Environment.NewLine);
                    }
                    return;
            }
        }

        private void SendServers(List<SyslogServer> servers, string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            byte[] data = Encoding.UTF8.GetBytes(msg);

            foreach (SyslogServer server in servers)
            {
                lock (server.SendLock)
                {
                    try
                    {
                        server.Udp.Send(data, data.Length);
                    }
                    catch (Exception)
                    {

                    }
                }
            } 
        }

        #endregion
    }
}
