namespace SyslogLogging
{
    using System;
    using System.Collections;
#if NET5_0_OR_GREATER
    using System.Diagnostics.CodeAnalysis;
#endif
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    /// <summary>
    /// Writes <see cref="LogEntry"/> property values to a <see cref="Utf8JsonWriter"/> without requiring
    /// reflection, so that JSON output works in trimmed and Native AOT applications.
    /// </summary>
    internal static class LogEntryJsonWriter
    {
        private const int _MaxStructuralDepth = 64;

        private static readonly JsonSerializerOptions _ScalarOptions = new JsonSerializerOptions();

        private static readonly JsonSerializerOptions _ReflectionOptions = new JsonSerializerOptions();

        /// <summary>
        /// Write a single property value.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="value">Value to write. May be null.</param>
        /// <param name="options">Caller-supplied options used to resolve a contract for the value, or null.</param>
        /// <param name="allowReflection">True to fall back to reflection-based serialization for complex values.</param>
        /// <param name="depth">Current structural nesting depth.</param>
        internal static void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options, bool allowReflection, int depth)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (TryWriteNonFinite(writer, value)) return;

            if (options != null)
            {
                if (options.TypeInfoResolver == null)
                {
                    if (allowReflection)
                    {
                        WriteWithReflection(writer, value, options);
                        return;
                    }
                }
                else if (TryResolveTypeInfo(options, value.GetType(), out JsonTypeInfo typeInfo))
                {
                    JsonSerializer.Serialize(writer, value, typeInfo);
                    return;
                }
            }

            if (TryWriteScalar(writer, value)) return;

            if (allowReflection)
            {
                WriteWithReflection(writer, value, _ReflectionOptions);
                return;
            }

            WriteStructural(writer, value, options, depth);
        }

        private static bool TryResolveTypeInfo(JsonSerializerOptions options, Type type, out JsonTypeInfo typeInfo)
        {
            try
            {
                return options.TryGetTypeInfo(type, out typeInfo) && typeInfo != null;
            }
            catch (InvalidOperationException)
            {
                // No resolver configured and reflection-based serialization is disabled.
                typeInfo = null;
                return false;
            }
            catch (NotSupportedException)
            {
                typeInfo = null;
                return false;
            }
        }

        private static bool TryWriteNonFinite(Utf8JsonWriter writer, object value)
        {
            double number;

            if (value is double d) number = d;
            else if (value is float f) number = f;
#if NET5_0_OR_GREATER
            else if (value is Half h) number = (double)h;
#endif
            else return false;

            if (double.IsNaN(number)) writer.WriteStringValue("NaN");
            else if (double.IsPositiveInfinity(number)) writer.WriteStringValue("Infinity");
            else if (double.IsNegativeInfinity(number)) writer.WriteStringValue("-Infinity");
            else return false;

            return true;
        }

        private static bool TryWriteScalar(Utf8JsonWriter writer, object value)
        {
            switch (value)
            {
                case string v: JsonMetadataServices.StringConverter.Write(writer, v, _ScalarOptions); return true;
                case bool v: JsonMetadataServices.BooleanConverter.Write(writer, v, _ScalarOptions); return true;
                case int v: JsonMetadataServices.Int32Converter.Write(writer, v, _ScalarOptions); return true;
                case long v: JsonMetadataServices.Int64Converter.Write(writer, v, _ScalarOptions); return true;
                case double v: JsonMetadataServices.DoubleConverter.Write(writer, v, _ScalarOptions); return true;
                case decimal v: JsonMetadataServices.DecimalConverter.Write(writer, v, _ScalarOptions); return true;
                case float v: JsonMetadataServices.SingleConverter.Write(writer, v, _ScalarOptions); return true;
                case short v: JsonMetadataServices.Int16Converter.Write(writer, v, _ScalarOptions); return true;
                case byte v: JsonMetadataServices.ByteConverter.Write(writer, v, _ScalarOptions); return true;
                case sbyte v: JsonMetadataServices.SByteConverter.Write(writer, v, _ScalarOptions); return true;
                case ushort v: JsonMetadataServices.UInt16Converter.Write(writer, v, _ScalarOptions); return true;
                case uint v: JsonMetadataServices.UInt32Converter.Write(writer, v, _ScalarOptions); return true;
                case ulong v: JsonMetadataServices.UInt64Converter.Write(writer, v, _ScalarOptions); return true;
                case char v: JsonMetadataServices.CharConverter.Write(writer, v, _ScalarOptions); return true;
                case DateTime v: JsonMetadataServices.DateTimeConverter.Write(writer, v, _ScalarOptions); return true;
                case DateTimeOffset v: JsonMetadataServices.DateTimeOffsetConverter.Write(writer, v, _ScalarOptions); return true;
                case Guid v: JsonMetadataServices.GuidConverter.Write(writer, v, _ScalarOptions); return true;
                case TimeSpan v: JsonMetadataServices.TimeSpanConverter.Write(writer, v, _ScalarOptions); return true;
                case Uri v: JsonMetadataServices.UriConverter.Write(writer, v, _ScalarOptions); return true;
                case Version v: JsonMetadataServices.VersionConverter.Write(writer, v, _ScalarOptions); return true;
                case byte[] v: JsonMetadataServices.ByteArrayConverter.Write(writer, v, _ScalarOptions); return true;
#if NET5_0_OR_GREATER
                case Half v: JsonMetadataServices.HalfConverter.Write(writer, v, _ScalarOptions); return true;
#endif
#if NET6_0_OR_GREATER
                case DateOnly v: JsonMetadataServices.DateOnlyConverter.Write(writer, v, _ScalarOptions); return true;
                case TimeOnly v: JsonMetadataServices.TimeOnlyConverter.Write(writer, v, _ScalarOptions); return true;
#endif
#if NET7_0_OR_GREATER
                case Int128 v: JsonMetadataServices.Int128Converter.Write(writer, v, _ScalarOptions); return true;
                case UInt128 v: JsonMetadataServices.UInt128Converter.Write(writer, v, _ScalarOptions); return true;
#endif
                case Enum v: WriteEnum(writer, v); return true;
                default: return false;
            }
        }

        private static void WriteEnum(Utf8JsonWriter writer, Enum value)
        {
            // Matches the System.Text.Json default: enums are written as their underlying numeric value.
            Type underlying = Enum.GetUnderlyingType(value.GetType());
            if (underlying == typeof(ulong))
                writer.WriteNumberValue(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
            else
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        private static void WriteStructural(Utf8JsonWriter writer, object value, JsonSerializerOptions options, int depth)
        {
            if (depth < _MaxStructuralDepth)
            {
                if (value is IDictionary dictionary)
                {
                    writer.WriteStartObject();
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                        WriteValue(writer, entry.Value, options, false, depth + 1);
                    }
                    writer.WriteEndObject();
                    return;
                }

                if (value is IEnumerable enumerable)
                {
                    writer.WriteStartArray();
                    foreach (object item in enumerable)
                    {
                        WriteValue(writer, item, options, false, depth + 1);
                    }
                    writer.WriteEndArray();
                    return;
                }
            }

            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (text == null) writer.WriteNullValue();
            else writer.WriteStringValue(text);
        }

#if NET5_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "Only reached when JsonSerializer.IsReflectionEnabledByDefault is true. Trimmed and Native AOT applications disable it by default and take the reflection-free path.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "Only reached when JsonSerializer.IsReflectionEnabledByDefault is true. Trimmed and Native AOT applications disable it by default and take the reflection-free path.")]
#endif
        private static void WriteWithReflection(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
