namespace SyslogLogging.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;

    using SyslogLogging;

    internal static class TestHelpers
    {
        public static void ConfigureSettings(LoggingModule logger, Action<LoggingSettings> configure)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            LoggingSettings settings = logger.Settings;
            configure(settings);
            logger.Settings = settings;
        }

        public static string GetExpectedDefaultApplicationName()
        {
            try
            {
                string? entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
                if (!string.IsNullOrWhiteSpace(entryAssemblyName))
                {
                    return entryAssemblyName;
                }
            }
            catch
            {
                // Fall through to process name fallback.
            }

            try
            {
                string processName = Process.GetCurrentProcess().ProcessName;
                if (!string.IsNullOrWhiteSpace(processName))
                {
                    return processName;
                }
            }
            catch
            {
                // Fall through to unknown.
            }

            return "unknown";
        }

        public static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + $" Expected '{expected}' but found '{actual}'.");
            }
        }

        public static void AssertContains(string value, string expectedSubstring, string message)
        {
            if (value == null || !value.Contains(expectedSubstring, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(message + $" Expected to find '{expectedSubstring}' in '{value}'.");
            }
        }

        public static void AssertDoesNotContain(string value, string unexpectedSubstring, string message)
        {
            if (value != null && value.Contains(unexpectedSubstring, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(message + $" Did not expect to find '{unexpectedSubstring}' in '{value}'.");
            }
        }

        public static void ExpectThrows<TException>(Action action, string? expectedParamName = null)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                if (expectedParamName != null && exception is ArgumentException argumentException)
                {
                    AssertEqual(expectedParamName, argumentException.ParamName, "Unexpected parameter name.");
                }

                return;
            }

            throw new InvalidOperationException($"Expected exception of type {typeof(TException).Name}.");
        }

        public static async Task ExpectThrowsAsync<TException>(Func<Task> action, string? expectedParamName = null)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException exception)
            {
                if (expectedParamName != null && exception is ArgumentException argumentException)
                {
                    AssertEqual(expectedParamName, argumentException.ParamName, "Unexpected parameter name.");
                }

                return;
            }

            throw new InvalidOperationException($"Expected exception of type {typeof(TException).Name}.");
        }

        public static string ReadAllText(string filePath)
        {
            AssertTrue(File.Exists(filePath), $"Expected file '{filePath}' to exist.");
            return File.ReadAllText(filePath);
        }

        public static string[] ReadAllLines(string filePath)
        {
            AssertTrue(File.Exists(filePath), $"Expected file '{filePath}' to exist.");
            return File.ReadAllLines(filePath);
        }
    }
}
