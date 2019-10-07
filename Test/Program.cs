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
        public static LoggingModule log;

        static void Main(string[] args)
        {
            log = new LoggingModule("127.0.0.1", 514);
            log.ConsoleEnable = true;
            log.IncludeUtcTimestamp = true;
            log.FileLogging = FileLoggingMode.FileWithDate;
            log.LogFilename = "syslog";

            log.Debug("This is a Debug message.");
            log.Info("This is an Info message.");
            log.Warn("This is a Warn message.");
            log.Error("This is an Error message.");
            log.Alert("This is an Alert message.");
            log.Critical("This is a Critical message.");
            log.Emergency("This is an Emergency message.");
            log.Info("Let's test logging an exception.");

            int numerator = 15;
            int denominator = 0;

            try
            {
                log.Critical("Shall we divide by zero?");
                int testVal = numerator / denominator;
                log.Warn("If you see this, there's a problem.");
            }
            catch (Exception e)
            {
                log.Exception("Program", "Main", e);
            }

            log.Alert("Press ENTER to exit");
            Console.ReadLine();
        }
    }
}
