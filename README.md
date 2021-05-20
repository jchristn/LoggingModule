# SyslogLogging

[![NuGet Version](https://img.shields.io/nuget/v/SyslogLogging.svg?style=flat)](https://www.nuget.org/packages/SyslogLogging/) [![NuGet](https://img.shields.io/nuget/dt/SyslogLogging.svg)](https://www.nuget.org/packages/SyslogLogging) 

Simple C# class library for logging to syslog, console, and file, targeted to .NET Core, .NET Standard, and .NET Framework.  For a sample app please refer to the included test project.

SyslogLogging is targeted to .NET Core, .NET Standard, and .NET Framework.

## Help or Feedback

First things first - do you need help or have feedback?  File an issue here!  We'd love to hear from you.

## New in v2.0.1

- Breaking changes including new constructors and minor API changes
- Support for multiple syslog servers
- Simplified class definitions

## It's Really Easy...  I Mean, REALLY Easy

By default, ```127.0.0.1:514``` is automatically included in the list of servers.

### Single Syslog Server
```csharp
using SyslogLogging;

LoggingModule log = new LoggingModule(); // 127.0.0.1:514 is automatically included
log.Debug("This is a debug message!");
```

### Multiple Syslog Servers and Console
```csharp
using SyslogLogging;

List<SyslogServer> servers = new List<SyslogServer>
{
  new SyslogServer("logginghost.com", 2000),
  new SyslogServer("myhost.com", 514)
};

LoggingModule log = new LoggingModule(servers, true); // true to enable console
log.Warn("Look out!");
```

### Logging to File
```csharp
using SyslogLogging;

LoggingModule log = new LoggingModule("mylogfile.txt");
log.Info("Here's some new information!");
```

### Logging EVERYWHERE
```csharp
using SyslogLogging;

List<SyslogServer> servers = new List<SyslogServer>
{
  new SyslogServer("127.0.0.1", 514)
};

LoggingModule log = new LoggingModule(servers, true); // true to enable console
log.Settings.FileLogging = FileLoggingMode.SingleLogFile;
log.Settings.LogFilename = "mylogfile.txt";
log.Alert("We're going everywhere!");
```

When using ```FileLoggingMode.FileWithDate```, LoggingModule with append ```.yyyyMMdd``` to the supplied filename in ```LogFilename```.  When using ```FileLoggingMode.SingleLogFile```, the filename is left untouched.

## Supported Environments

Tested and works well in Windows in .NET Framework 4.5.2 or later or .NET Core.  Tested and works well in Linux and OSX environments, too.  Should work well in Mono environments.  You may want to use the Mono Ahead-of-time compiler (AOT).

This package has also been tested with PaperTrail.  Realistically, any cloud syslog server should work.

## Changing Console Message Color

If you wish to change the colors used by the library, modify the ```Settings.Colors``` property.  A variable of type ```ColorScheme``` exists for each severity level.  To disable colors, set ```Settings.EnableColors``` to false.

```csharp
log.Settings.EnableColors = true;
log.Settings.Colors.Debug = new ColorScheme(ConsoleColor.DarkGray, ConsoleColor.Black);
```

## Special Thanks

We'd like to extend a special thank you to those that have helped make this library better, including:

@dev-jan @jisotalo

## Version History

Please refer to CHANGELOG.md.
