# SyslogLogging

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/SyslogLogging/
[nuget-img]: https://badge.fury.io/nu/Object.svg

Simple C# class library for logging to syslog and console.  For a sample app please refer to the included test project.

## Help or Feedback

First things first - do you need help or have feedback?  File an issue here!  We'd love to hear from you.

## New in v1.1.x

- Simplified constructors
- Simplified methods
- Added IDisposable support
- Cleanup and fixes, minor refactoring

## It's Really Easy

```
using SyslogLogging;

LoggingModule logging = new LoggingModule("127.0.0.1", 514);
logging.ConsoleEnable = true;

logging.Debug("This is a debug message!");
logging.Exception("Program", "Main", e);
```

## Supported Environments

Tested and works well in Windows in .NET Framework 4.5.2 or later or .NET Core.

Tested and works well in Linux and OSX environments, too.

Should work well in Mono environments.  You may want to use the Mono Ahead-of-time compiler (AOT).

## Version History

v1.0.x

- Initial release
- Bugfixes and stability
