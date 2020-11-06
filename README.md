# SyslogLogging

[![NuGet Version](https://img.shields.io/nuget/v/SyslogLogging.svg?style=flat)](https://www.nuget.org/packages/SyslogLogging/) [![NuGet](https://img.shields.io/nuget/dt/SyslogLogging.svg)](https://www.nuget.org/packages/SyslogLogging) 

Simple C# class library for logging to syslog and console.  For a sample app please refer to the included test project.

SyslogLogging is targeted to .NET Core, .NET Standard, and .NET Framework.

## Help or Feedback

First things first - do you need help or have feedback?  File an issue here!  We'd love to hear from you.

## New in v1.3.2

- EnableColors property for console logging
- Expose ```Log``` API (thank you @dev-jan!)

## It's Really Easy...  I Mean, REALLY Easy

```csharp
using SyslogLogging;

LoggingModule logging = new LoggingModule("127.0.0.1", 514);
logging.ConsoleEnable = true;
logging.FileLogging = FileLoggingMode.FileWithDate;  // or Disabled, or SingleLogFile
logging.LogFilename = "syslog.txt";

logging.Debug("This is a debug message!");
logging.Exception("Program", "Main", e);
```

When using ```FileLoggingMode.FileWithDate```, LoggingModule with append ```.yyyyMMdd``` to the supplied filename in ```LogFilename```.  When using ```FileLoggingMode.SingleLogFile```, the filename is left untouched.

## Supported Environments

Tested and works well in Windows in .NET Framework 4.5.2 or later or .NET Core.  Tested and works well in Linux and OSX environments, too.  Should work well in Mono environments.  You may want to use the Mono Ahead-of-time compiler (AOT).

## Changing Console Message Color

If you wish to change the colors used by the library, modify the ```LoggingModule.Colors``` property.  A variable of type ```ColorScheme``` exists for each severity level.  To disable color manipulation, set this property to ```null```.

```
log.Colors.Debug = new ColorScheme(ConsoleColor.DarkGray, ConsoleColor.Black);
```

## Version History

Please refer to CHANGELOG.md.
