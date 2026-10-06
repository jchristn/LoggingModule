namespace Syslog
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated System.Text.Json metadata for <see cref="T:Syslog.Settings"/>, used to read and write syslog.json
    /// without reflection so the server can be published with Native AOT.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true)]
    [JsonSerializable(typeof(Settings))]
    internal partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
