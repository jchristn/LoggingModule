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
        public static LoggingModule Logging;

        static void Main(string[] args)
        {
            Logging = new LoggingModule("127.0.0.1", 514);
            Logging.ConsoleEnable = true;
            Logging.IncludeUtcTimestamp = false;

            Logging.Debug("Hello!");
            Logging.Info("Let's test logging an exception.");

            int numerator = 15;
            int denominator = 0;

            try
            {
                Logging.Critical("Shall we divide by zero?");
                int testVal = numerator / denominator;
                Logging.Warn("If you see this, there's a problem.");
            }
            catch (Exception e)
            {
                Logging.Exception("Program", "Main", e);
            }

            Logging.Alert("Press ENTER to exit");
            Console.ReadLine();
        }
    }
}
