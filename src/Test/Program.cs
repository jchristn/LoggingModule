using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyslogLogging;

namespace Test
{
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

            List<SyslogServer> servers = new List<SyslogServer>
            {
                new SyslogServer("myhost.com", 514),
                new SyslogServer("127.0.0.1", 514),
                new SyslogServer("127.0.0.1", 514),
                new SyslogServer("127.0.0.1", 514)
            };
         
            _Log = new LoggingModule(servers, true);
            _Log.Settings.MinimumSeverity = Severity.Debug;
            _Log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            _Log.Settings.LogFilename = "logs/test.log";
             
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

            _Log.Alert("Press ENTER to exit");
            Console.ReadLine();
        }
    }
}
