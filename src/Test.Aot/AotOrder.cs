namespace SyslogLogging.Tests.Aot
{
    using System.Collections.Generic;

    public sealed class AotOrder
    {
        public string Name { get; set; } = "widget";

        public int Quantity { get; set; } = 3;

        public List<string> Tags { get; set; } = new List<string> { "a", "b" };

        public override string ToString()
        {
            return "AotOrder:" + Name;
        }
    }
}
