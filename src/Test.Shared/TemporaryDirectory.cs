namespace SyslogLogging.Tests.Shared
{
    using System;
    using System.IO;

    internal sealed class TemporaryDirectory : IDisposable
    {
        public string FullPath { get; }

        public TemporaryDirectory(string prefix)
        {
            FullPath = Path.Combine(
                Path.GetTempPath(),
                "SyslogLogging.Tests",
                prefix + "-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(FullPath);
        }

        public string GetPath(string relativePath)
        {
            return Path.Combine(FullPath, relativePath);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(FullPath))
                {
                    Directory.Delete(FullPath, true);
                }
            }
            catch
            {
                // Best-effort cleanup for test artifacts.
            }
        }
    }
}
