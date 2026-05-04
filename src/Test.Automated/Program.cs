using SyslogLogging.Tests.Shared;
using Touchstone.Cli;

string? resultsPath = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--results" && i + 1 < args.Length)
    {
        resultsPath = args[i + 1];
        break;
    }
}

return await ConsoleRunner.RunAsync(LoggingModuleSuites.All, resultsPath: resultsPath);
