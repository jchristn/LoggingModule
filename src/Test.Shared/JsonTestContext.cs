namespace SyslogLogging.Tests.Shared
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(JsonTestOrder))]
    internal partial class JsonTestContext : JsonSerializerContext
    {
    }
}
