namespace SyslogLogging.Tests.Shared
{
    using System.Collections.Generic;

    public sealed class JsonTestOrder
    {
        public string Name { get; set; } = "widget";

        public int Quantity { get; set; } = 3;

        public List<int> Items { get; set; } = new List<int> { 1, 2 };

        public override string ToString()
        {
            return "Order:" + Name;
        }
    }
}
