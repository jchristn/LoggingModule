namespace SyslogLogging.Tests.Aot
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(AotOrder))]
    internal partial class AotJsonContext : JsonSerializerContext
    {
    }
}
