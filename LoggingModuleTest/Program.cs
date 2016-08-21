using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyslogLogging;

namespace SyslogLogging
{
    class Program
    {
        public static LoggingModule Logging;

        static void Main(string[] args)
        {
            Logging = new LoggingModule("localhost", 514, true, LoggingModule.Severity.Debug, false, true, true, true, true, true);
            Logging.Log(LoggingModule.Severity.Debug, "Hello from Main!");
            Method1();
            Logging.Log(LoggingModule.Severity.Debug, "Back from Method1!  Press ENTER to exit");
            Console.ReadLine();
        }

        static void Method1()
        {
            Logging.Log(LoggingModule.Severity.Warn, "Warning from Method1!");
            Method2();
            Logging.Log(LoggingModule.Severity.Debug, "Back from Method2!");
        }

        static void Method2()
        {
            Logging.Log(LoggingModule.Severity.Alert, "Alert from Method2!");
            Method3();
            Logging.Log(LoggingModule.Severity.Debug, "Back from Method3!");
        }

        static void Method3()
        {
            try
            {
                Logging.Log(LoggingModule.Severity.Critical, "We're about to get an exception!");
                Method4(null);
                Logging.Log(LoggingModule.Severity.Debug, "You shouldn't see me");
            }
            catch (Exception e)
            {
                Logging.LogException("Program", "Method3", e);
            }
        }

        static void Method4(object someArg)
        {
            throw new ArgumentNullException(nameof(someArg));
        }
    }
}
