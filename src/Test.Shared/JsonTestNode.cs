namespace SyslogLogging.Tests.Shared
{
    public sealed class JsonTestNode
    {
        public string Name { get; set; } = "node";

        public JsonTestNode? Next { get; set; }
    }
}
