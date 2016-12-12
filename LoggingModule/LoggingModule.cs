using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyslogLogging
{
    public class LoggingModule
    {
        #region Public-Enums

        public enum Severity
        {
            Debug = 0,
            Info = 1,
            Warn = 2,
            Error = 3,
            Alert = 4,
            Critical = 5,
            Emergency = 6
        }

        #endregion

        #region Public-Members

        public string SyslogServerIp = "";
        public int SyslogServerPort = 0;
        public bool ConsoleEnable = true;
        public Severity MinimumSeverity;
        public bool AsyncLogging = false;
        public bool IncludeUtcTimestamp = true;
        public bool IncludeSeverity = true;
        public bool IncludeHostname = true;
        public bool IncludeThreadId = true;
        public bool IndentByStackSize = true;

        #endregion

        #region Private-Members

        private UdpClient UDP = null;
        private string Hostname = null;
        private object SendLock = new object();
        private int BaseDepth = 0;

        #endregion

        #region Constructor

        public LoggingModule(
            string syslogServerIp,
            int syslogServerPort,
            bool consoleEnable,
            Severity minimumSeverity,
            bool asyncLogging,
            bool includeUtcTimestamp,
            bool includeSeverity,
            bool includeHostname,
            bool includeThreadId,
            bool indentByStackSize)
        {
            SyslogServerIp = syslogServerIp;
            SyslogServerPort = syslogServerPort;
            ConsoleEnable = consoleEnable;
            MinimumSeverity = minimumSeverity;
            AsyncLogging = asyncLogging;
            IncludeUtcTimestamp = includeUtcTimestamp;
            IncludeSeverity = includeSeverity;
            IncludeHostname = includeHostname;
            IncludeThreadId = includeThreadId;
            IndentByStackSize = indentByStackSize;

            if (!String.IsNullOrEmpty(SyslogServerIp) && SyslogServerPort > 0)
            {
                try
                {
                    UDP = new UdpClient(SyslogServerIp, SyslogServerPort);
                }
                catch (Exception e)
                {
                    Console.WriteLine("---");
                    Console.WriteLine("");
                    Console.WriteLine("NOTICE:");
                    Console.WriteLine("");
                    Console.WriteLine("Exception while initializing UDP syslog client: " + e.Message);
                    Console.WriteLine("Syslog logging to server " + SyslogServerIp + ":" + SyslogServerPort + " is DISABLED as a result");
                    Console.WriteLine("");
                    Console.WriteLine("---");
                    UDP = null;
                }
            }
            else
            {
                UDP = null;
            }

            Hostname = Dns.GetHostName();
            StackTrace st = new StackTrace();
            BaseDepth = st.FrameCount - 1;
        }

        #endregion

        #region Public-Internal-Classes

        #endregion

        #region Private-Internal-Classes

        #endregion

        #region Public-Methods

        public void Close()
        {
            if (UDP != null) UDP.Close();
        }

        public void Log(string msg)
        {
            Log(Severity.Debug, msg);
        }

        public void Log(Severity sev, string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;

            string message = "";
            string currMsg = "";
            string remainder = "";

            if (msg.Length > 1024)
            {
                currMsg = msg.Substring(0, 1024);
                remainder = msg.Substring(1024, (msg.Length - 1024));
            }
            else
            {
                currMsg = msg;
            }

            if (IncludeUtcTimestamp) message += DateTime.Now.ToUniversalTime().ToString("MM/dd/yyyy HH:mm:ss") + " ";
            if (IncludeSeverity) message += sev.ToString() + " ";
            if (IncludeHostname) message += Hostname + " ";
            if (IncludeThreadId) message += "thr-" + Thread.CurrentThread.ManagedThreadId + " ";
            if (IndentByStackSize)
            {
                StackTrace st = new StackTrace();
                int CurrentDepth = st.FrameCount;
                if (CurrentDepth > BaseDepth)
                {
                    for (int i = 0; i < (CurrentDepth - BaseDepth); i++)
                    {
                        message += " ";
                    }
                }
            }

            message += currMsg;

            if (ConsoleEnable)
            {
                if (sev >= MinimumSeverity)
                {
                    if (!AsyncLogging) SendToConsole(message);
                    else Task.Run(() => SendToConsole(message));
                }
            }

            if (UDP != null)
            {
                if (sev >= MinimumSeverity)
                {
                    if (!AsyncLogging) SendToSyslog(message);
                    else Task.Run(() => SendToSyslog(message));
                }
            }

            if (!String.IsNullOrEmpty(remainder))
            {
                Log(sev, remainder);
            }
        }

        public void LogException(string module, string method, Exception e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var st = new StackTrace(e, true);
            var frame = st.GetFrame(0);
            int fileLine = frame.GetFileLineNumber();
            string filename = frame.GetFileName();

            string message =
                Environment.NewLine +
                "---" + Environment.NewLine +
                "An exception was encountered which triggered this message" + Environment.NewLine +
                "  Module     : " + module + Environment.NewLine +
                "  Method     : " + method + Environment.NewLine +
                "  Type       : " + e.GetType().ToString() + Environment.NewLine +
                "  Data       : " + e.Data + Environment.NewLine +
                "  Inner      : " + e.InnerException + Environment.NewLine +
                "  Message    : " + e.Message + Environment.NewLine +
                "  Source     : " + e.Source + Environment.NewLine +
                "  StackTrace : " + e.StackTrace + Environment.NewLine +
                "  Stack      : " + StackToString() + Environment.NewLine +
                "  Line       : " + fileLine + Environment.NewLine +
                "  File       : " + filename + Environment.NewLine +
                "  ToString   : " + e.ToString() + Environment.NewLine +
                "  Servername : " + Dns.GetHostName() + Environment.NewLine +
                "---";

            Log(Severity.Alert, message);
        }

        public static void ConsoleException(string module, string method, Exception e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var st = new StackTrace(e, true);
            var frame = st.GetFrame(0);
            int fileLine = frame.GetFileLineNumber();
            string filename = frame.GetFileName();

            string message =
                Environment.NewLine +
                "---" + Environment.NewLine +
                "An exception was encountered which triggered this message" + Environment.NewLine +
                "  Module     : " + module + Environment.NewLine +
                "  Method     : " + method + Environment.NewLine +
                "  Type       : " + e.GetType().ToString() + Environment.NewLine +
                "  Data       : " + e.Data + Environment.NewLine +
                "  Inner      : " + e.InnerException + Environment.NewLine +
                "  Message    : " + e.Message + Environment.NewLine +
                "  Source     : " + e.Source + Environment.NewLine +
                "  StackTrace : " + e.StackTrace + Environment.NewLine +
                "  Stack      : " + StaticStackToString() + Environment.NewLine +
                "  Line       : " + fileLine + Environment.NewLine +
                "  File       : " + filename + Environment.NewLine +
                "  ToString   : " + e.ToString() + Environment.NewLine +
                "  Servername : " + Dns.GetHostName() + Environment.NewLine +
                "---";

            Console.WriteLine(message);
        }

        #endregion

        #region Private-Methods

        private void SendToConsole(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            if (Console.CursorLeft != 0) Console.WriteLine("");
            Console.WriteLine(msg);
        }

        private void SendToSyslog(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;

            if (!String.IsNullOrEmpty(SyslogServerIp)
                && SyslogServerPort > 0)
            {
                lock (SendLock)
                {
                    if (UDP != null)
                    {
                        byte[] data = Encoding.UTF8.GetBytes(msg);

                        try
                        {
                            UDP.Send(data, data.Length);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Exception while sending to syslog server " + SyslogServerIp + ":" + SyslogServerPort + ", disabling: " + e.Message);
                            UDP = null;
                        }
                    }
                }
            }
        }

        private string StackToString()
        {
            string ret = "";

            StackTrace t = new StackTrace();
            for (int i = 0; i < t.FrameCount; i++)
            {
                if (i == 0)
                {
                    ret += t.GetFrame(i).GetMethod().Name;
                }
                else
                {
                    ret += " <= " + t.GetFrame(i).GetMethod().Name;
                }
            }

            return ret;
        }

        private static string StaticStackToString()
        {
            string ret = "";

            StackTrace t = new StackTrace();
            for (int i = 0; i < t.FrameCount; i++)
            {
                if (i == 0)
                {
                    ret += t.GetFrame(i).GetMethod().Name;
                }
                else
                {
                    ret += " <= " + t.GetFrame(i).GetMethod().Name;
                }
            }

            return ret;
        }

        #endregion
    }
}
