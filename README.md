# SyslogLogging

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/SyslogLogging/
[nuget-img]: https://badge.fury.io/nu/Object.svg

Simple C# class library for logging to syslog and console.  For a sample app please refer to the included test project.

## help or feedback
first things first - do you need help or have feedback?  Contact me at joel at maraudersoftware.com dot com or file an issue here!

## it's easy
```
using SyslogLogging;

LoggingModule logging = new LoggingModule(
   "localhost",                              // hostname of the syslog server
   514,                                      // syslog server port
   true,                                     // also log to console
   LoggingModule.Severity.Debug,             // minimum severity to send
   false,                                    // use async logging (start a task)
   true,                                     // include timestamp
   true,                                     // include severity
   true,                                     // include hostname
   true,                                     // include thread ID
   true);                                    // indent by stack depth

logging.Log(Severity.Debug, "This is a debug message!");
logging.LogException("Program", "Main", e);
```

## running under Mono
Should work well in Mono environments.  
