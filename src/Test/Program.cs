namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using SyslogLogging;

    class Program
    {
        public static LoggingModule _Log;

        static void Main(string[] args)
        {
            /*
             * 
             * Testing deserialization
             * 
            string json = "[{'Hostname':'127.0.0.1','Port':514},{'Hostname':'myhost.com','Port':21657}]";
            List<SyslogServer> servers = JsonConvert.DeserializeObject<List<SyslogServer>>(json);
             *
             */

            Console.WriteLine("");
            Console.WriteLine("Using default constructor");
            _Log = new LoggingModule();
            _Log.Settings.MinimumSeverity = Severity.Debug;
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/test.log";
            EmitMessages();

            Console.WriteLine("");
            Console.WriteLine("Using constructor with specific syslog server");
            _Log = new LoggingModule("127.0.0.1", 514);
            _Log.Settings.MinimumSeverity = Severity.Debug;
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/test.log";
            EmitMessages();

            Console.WriteLine("");
            Console.WriteLine("Using constructor with list of syslog servers");
            List<SyslogServer> servers = new List<SyslogServer>()
            {
                new SyslogServer("127.0.0.1", 514),
                new SyslogServer("127.0.0.1", 514),
                new SyslogServer("127.0.0.1", 514),
            };
            _Log = new LoggingModule(servers);
            _Log.Settings.MinimumSeverity = Severity.Debug;
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/test.log";
            EmitMessages();

            Console.WriteLine("");
        }

        static void EmitMessages()
        {
            _Log.Debug("This is a Debug message.");
            _Log.Info("This is an Info message.");
            _Log.Warn("This is a Warn message.");
            _Log.Error("This is an Error message.");
            _Log.Alert("This is an Alert message.");
            _Log.Critical("This is a Critical message.");
            _Log.Emergency("This is an Emergency message.");
            _Log.Info("Let's test logging an exception.");
            _Log.Log(Severity.Info, "Just another way to create log messages!");

            int numerator = 15;
            int denominator = 0;

            try
            {
                _Log.Critical("Shall we divide by zero?");
                int testVal = numerator / denominator;
                _Log.Warn("If you see this, there's a problem.");
            }
            catch (Exception e)
            {
                e.Data.Add("foo", "bar");
                _Log.Exception(e, "Program", "Main");
            }
        }
    }
}
