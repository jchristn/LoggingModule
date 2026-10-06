namespace SyslogLogging.Tests.Aot
{
    using System;
    using System.Threading.Tasks;

    public sealed class AotCheck
    {
        public string Name { get; }

        public Func<Task> Execute { get; }

        public AotCheck(string name, Func<Task> execute)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }
    }
}
