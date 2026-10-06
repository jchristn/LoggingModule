namespace SyslogLogging.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using Touchstone.Core;

    using SyslogLogging;

    public static class JsonSuites
    {
        private const string SuiteId = "Json";

        private const string FixedPrefix = "{\"timestamp\":\"2026-01-02T03:04:05.678Z\",\"severity\":\"Warn\",\"message\":\"json \\u003C\\u0026\\u003E \\u0022q\\u0022 \\u00E9\",\"threadId\":7";

        public static TestSuiteDescriptor JsonSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: SuiteId,
                displayName: "JSON Serialization and Native AOT Compatibility",
                cases: new List<TestCaseDescriptor>
                {
                    Case("FixedFieldsGolden", "Fixed fields serialize in the documented order, names, and escaping", FixedFieldsGolden),
                    Case("OptionalFieldsOmitted", "Empty source, correlation, trace, span, exception, and properties are omitted", OptionalFieldsOmitted),
                    Case("NullMessage", "A null message serializes as JSON null", NullMessage),
                    Case("ExceptionWithoutStackTrace", "An exception that was never thrown serializes with a null stackTrace", ExceptionWithoutStackTrace),
                    Case("NullPropertiesDictionary", "A null Properties dictionary is treated as empty instead of throwing", NullPropertiesDictionary),
                    Case("LegacyParityScalars", "Scalar property values serialize byte-identically to the 2.3.x reflection output", LegacyParityScalars),
                    Case("LegacyParityScalarsReflectionDisabled", "Scalar property values serialize byte-identically with reflection disabled (Native AOT path)", LegacyParityScalarsReflectionDisabled),
                    Case("LegacyParityComplex", "Objects, anonymous types, and nested dictionaries serialize byte-identically when reflection is enabled", LegacyParityComplex),
                    Case("ReflectionDisabledCollections", "With reflection disabled, arrays, lists, and dictionaries serialize as JSON arrays and objects", ReflectionDisabledCollections),
                    Case("ReflectionDisabledObjectFallback", "With reflection disabled, an unknown object serializes as its string representation", ReflectionDisabledObjectFallback),
                    Case("EnumsNumeric", "Enums serialize as their underlying number, including undefined and ulong values, in both modes", EnumsNumeric),
                    Case("NonFiniteNumbers", "NaN and infinity serialize as strings in both modes instead of throwing", NonFiniteNumbers),
                    Case("PropertyKeyEscaping", "Property keys are escaped exactly as System.Text.Json escapes them", PropertyKeyEscaping),
                    Case("DepthLimitSelfReference", "A self-referencing collection with reflection disabled is truncated instead of overflowing the stack", DepthLimitSelfReference),
                    Case("ReflectionCycleThrows", "A cyclic object graph with reflection enabled throws JsonException", ReflectionCycleThrows),
                    Case("OptionsNullThrows", "ToJson(JsonSerializerOptions) rejects null options", OptionsNullThrows),
                    Case("OptionsSourceGenerated", "ToJson(options) serializes complex values in full through a source-generated resolver with reflection disabled", OptionsSourceGenerated),
                    Case("OptionsUnresolvedFallsBack", "ToJson(options) writes values the resolver does not know exactly as ToJson() would", OptionsUnresolvedFallsBack),
                    Case("OptionsIndented", "ToJson(options) honors WriteIndented", OptionsIndented),
                    Case("OptionsEncoder", "ToJson(options) honors the configured Encoder", OptionsEncoder),
                    Case("OptionsConverters", "ToJson(options) applies the options' converters to property values", OptionsConverters),
                    Case("DefaultMatchesOptionsDefault", "ToJson() and ToJson(new JsonSerializerOptions()) produce identical output", DefaultMatchesOptionsDefault),
                    Case("ConcurrentSerialization", "Concurrent ToJson calls on separate entries are independent and correct", ConcurrentSerialization),
                    Case("ILoggerProperties", "Microsoft.Extensions.Logging template arguments serialize as typed JSON properties", ILoggerProperties),
                    Case("AssemblyTrimmable", "The net8.0+ library assembly is marked trimmable (IsAotCompatible)", AssemblyTrimmable),
                });
        }

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> execute)
        {
            return new TestCaseDescriptor(suiteId: SuiteId, caseId: caseId, displayName: displayName, executeAsync: execute);
        }

        private static LogEntry CreateEntry()
        {
            LogEntry entry = new LogEntry(Severity.Warn, "json <&> \"q\" é");
            entry.Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);
            entry.ThreadId = 7;
            entry.TraceId = null;
            entry.SpanId = null;
            return entry;
        }

        private static LogEntry CreateEntry(string key, object? value)
        {
            LogEntry entry = CreateEntry();
            entry.Properties[key] = value!;
            return entry;
        }

        private static Dictionary<string, object?> ScalarValues()
        {
            return new Dictionary<string, object?>
            {
                ["string"] = "a<b>&'\"",
                ["emptyString"] = string.Empty,
                ["null"] = null,
                ["true"] = true,
                ["false"] = false,
                ["int"] = -5,
                ["long"] = long.MaxValue,
                ["ulong"] = ulong.MaxValue,
                ["short"] = (short)-3,
                ["ushort"] = (ushort)3,
                ["byte"] = (byte)255,
                ["sbyte"] = (sbyte)-128,
                ["uint"] = uint.MaxValue,
                ["double"] = 3.14159,
                ["doubleWhole"] = 2.0,
                ["doubleSmall"] = 1e-10,
                ["doubleMax"] = double.MaxValue,
                ["float"] = 1.5f,
                ["floatFraction"] = 0.1f,
                ["decimal"] = 123.4500m,
                ["char"] = 'x',
                ["dateTimeUtc"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                ["dateTimeUnspecified"] = new DateTime(2026, 1, 2, 3, 4, 5, 123, DateTimeKind.Unspecified),
                ["dateTimeOffset"] = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5)),
                ["guid"] = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
                ["timeSpan"] = TimeSpan.FromSeconds(3661.5),
                ["uri"] = new Uri("https://example.com/a?b=c"),
                ["version"] = new Version(1, 2, 3, 4),
                ["bytes"] = new byte[] { 1, 2, 3 },
                ["half"] = (Half)1.5,
                ["int128"] = (Int128)12345,
                ["uint128"] = (UInt128)67890,
                ["dateOnly"] = new DateOnly(2026, 3, 4),
                ["timeOnly"] = new TimeOnly(13, 14, 15),
                ["enum"] = JsonTestColor.Blue,
            };
        }

        private static Dictionary<string, object?> CollectionValues()
        {
            return new Dictionary<string, object?>
            {
                ["intArray"] = new int[] { 1, 2, 3 },
                ["emptyArray"] = new string[0],
                ["stringList"] = new List<string> { "x", "y" },
                ["objectArray"] = new object?[] { 1, "two", null, true, 2.5 },
                ["nestedDictionary"] = new Dictionary<string, object> { ["k"] = 1, ["n"] = new Dictionary<string, int> { ["z"] = 9 } },
                ["intKeyDictionary"] = new Dictionary<int, string> { [1] = "one" },
                ["listOfLists"] = new List<List<int>> { new List<int> { 1 }, new List<int> { 2, 3 } },
                ["guidList"] = new List<Guid> { Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e") },
            };
        }

        // Exact copy of the 2.3.x LogEntry.ToJson() implementation, used as the parity reference.
        private static string LegacyToJson(LogEntry entry)
        {
            Dictionary<string, object> serializable = new Dictionary<string, object>
            {
                ["timestamp"] = entry.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["severity"] = entry.Severity.ToString(),
                ["message"] = entry.Message,
                ["threadId"] = entry.ThreadId
            };

            if (!string.IsNullOrEmpty(entry.Source)) serializable["source"] = entry.Source;
            if (!string.IsNullOrEmpty(entry.CorrelationId)) serializable["correlationId"] = entry.CorrelationId;
            if (!string.IsNullOrEmpty(entry.TraceId)) serializable["traceId"] = entry.TraceId;
            if (!string.IsNullOrEmpty(entry.SpanId)) serializable["spanId"] = entry.SpanId;

            if (entry.Exception != null)
            {
                serializable["exception"] = new Dictionary<string, object>
                {
                    ["type"] = entry.Exception.GetType().FullName!,
                    ["message"] = entry.Exception.Message,
                    ["stackTrace"] = entry.Exception.StackTrace!
                };
            }

            if (entry.Properties.Count > 0) serializable["properties"] = entry.Properties;

            return JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = false });
        }

        private static void AssertParity(Dictionary<string, object?> values, bool allowReflection, string mode)
        {
            foreach (KeyValuePair<string, object?> kvp in values)
            {
                LogEntry entry = CreateEntry(kvp.Key, kvp.Value);
                string expected = LegacyToJson(entry);
                string actual = allowReflection ? entry.ToJson() : entry.ToJsonCore(null, false);
                TestHelpers.AssertEqual(expected, actual, $"[{mode}] Property '{kvp.Key}' must serialize identically to the 2.3.x output.");
            }

            LogEntry combined = CreateEntry();
            foreach (KeyValuePair<string, object?> kvp in values) combined.Properties[kvp.Key] = kvp.Value!;
            string combinedActual = allowReflection ? combined.ToJson() : combined.ToJsonCore(null, false);
            TestHelpers.AssertEqual(LegacyToJson(combined), combinedActual, $"[{mode}] All properties together must serialize identically to the 2.3.x output.");
        }

        private static Task FixedFieldsGolden(CancellationToken ct)
        {
            LogEntry entry = CreateEntry()
                .WithSource("src")
                .WithCorrelationId("cid");
            entry.TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
            entry.SpanId = "00f067aa0ba902b7";
            entry.Exception = new InvalidOperationException("boom <x>");
            entry.WithProperty("OrderId", 42);

            string expected = FixedPrefix
                + ",\"source\":\"src\",\"correlationId\":\"cid\",\"traceId\":\"4bf92f3577b34da6a3ce929d0e0e4736\",\"spanId\":\"00f067aa0ba902b7\""
                + ",\"exception\":{\"type\":\"System.InvalidOperationException\",\"message\":\"boom \\u003Cx\\u003E\",\"stackTrace\":null}"
                + ",\"properties\":{\"OrderId\":42}}";

            TestHelpers.AssertEqual(expected, entry.ToJson(), "Default mode fixed fields must match the documented format.");
            TestHelpers.AssertEqual(expected, entry.ToJsonCore(null, false), "Reflection-disabled fixed fields must match the documented format.");
            TestHelpers.AssertEqual(LegacyToJson(entry), entry.ToJson(), "Fixed fields must match the 2.3.x output.");
            return Task.CompletedTask;
        }

        private static Task OptionalFieldsOmitted(CancellationToken ct)
        {
            LogEntry entry = CreateEntry();
            entry.Source = string.Empty;
            entry.CorrelationId = null!;

            TestHelpers.AssertEqual(FixedPrefix + "}", entry.ToJson(), "Optional fields must be omitted when empty.");
            TestHelpers.AssertEqual(FixedPrefix + "}", entry.ToJsonCore(null, false), "Optional fields must be omitted when empty with reflection disabled.");
            return Task.CompletedTask;
        }

        private static Task NullMessage(CancellationToken ct)
        {
            LogEntry entry = CreateEntry();
            entry.Message = null!;
            string expected = "{\"timestamp\":\"2026-01-02T03:04:05.678Z\",\"severity\":\"Warn\",\"message\":null,\"threadId\":7}";
            TestHelpers.AssertEqual(expected, entry.ToJson(), "Null message must serialize as null.");
            TestHelpers.AssertEqual(LegacyToJson(entry), entry.ToJson(), "Null message must match the 2.3.x output.");
            return Task.CompletedTask;
        }

        private static Task ExceptionWithoutStackTrace(CancellationToken ct)
        {
            LogEntry entry = CreateEntry();
            entry.Exception = new ArgumentException("bad");
            TestHelpers.AssertEqual(
                FixedPrefix + ",\"exception\":{\"type\":\"System.ArgumentException\",\"message\":\"bad\",\"stackTrace\":null}}",
                entry.ToJson(),
                "Unthrown exception must serialize with a null stack trace.");

            try
            {
                throw new InvalidOperationException("thrown");
            }
            catch (InvalidOperationException ex)
            {
                entry.Exception = ex;
            }

            string json = entry.ToJson();
            TestHelpers.AssertContains(json, "\"stackTrace\":\"", "Thrown exception must include its stack trace.");
            TestHelpers.AssertEqual(LegacyToJson(entry), json, "Thrown exception must match the 2.3.x output.");
            return Task.CompletedTask;
        }

        private static Task NullPropertiesDictionary(CancellationToken ct)
        {
            LogEntry entry = CreateEntry();
            entry.Properties = null!;
            TestHelpers.AssertEqual(FixedPrefix + "}", entry.ToJson(), "Null Properties must be treated as empty.");
            return Task.CompletedTask;
        }

        private static Task LegacyParityScalars(CancellationToken ct)
        {
            AssertParity(ScalarValues(), true, "default");
            return Task.CompletedTask;
        }

        private static Task LegacyParityScalarsReflectionDisabled(CancellationToken ct)
        {
            AssertParity(ScalarValues(), false, "reflection-disabled");
            return Task.CompletedTask;
        }

        private static Task LegacyParityComplex(CancellationToken ct)
        {
            Dictionary<string, object?> values = CollectionValues();
            values["order"] = new JsonTestOrder();
            values["anonymous"] = new { A = 1, B = "x", C = new[] { 1.5, 2.5 } };
            values["orders"] = new List<JsonTestOrder> { new JsonTestOrder(), new JsonTestOrder { Name = "gadget" } };
            AssertParity(values, true, "default");

            LogEntry entry = CreateEntry("order", new JsonTestOrder());
            TestHelpers.AssertEqual(
                FixedPrefix + ",\"properties\":{\"order\":{\"Name\":\"widget\",\"Quantity\":3,\"Items\":[1,2]}}}",
                entry.ToJson(),
                "Objects must serialize in full when reflection is enabled.");
            return Task.CompletedTask;
        }

        private static Task ReflectionDisabledCollections(CancellationToken ct)
        {
            AssertParity(CollectionValues(), false, "reflection-disabled");
            return Task.CompletedTask;
        }

        private static Task ReflectionDisabledObjectFallback(CancellationToken ct)
        {
            LogEntry entry = CreateEntry("order", new JsonTestOrder());
            TestHelpers.AssertEqual(
                FixedPrefix + ",\"properties\":{\"order\":\"Order:widget\"}}",
                entry.ToJsonCore(null, false),
                "Unknown objects must fall back to their string representation.");

            LogEntry nested = CreateEntry("orders", new List<object> { new JsonTestOrder(), 5 });
            TestHelpers.AssertEqual(
                FixedPrefix + ",\"properties\":{\"orders\":[\"Order:widget\",5]}}",
                nested.ToJsonCore(null, false),
                "Unknown objects inside collections must fall back to their string representation.");
            return Task.CompletedTask;
        }

        private static Task EnumsNumeric(CancellationToken ct)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>
            {
                ["defined"] = JsonTestColor.Blue,
                ["undefined"] = (JsonTestColor)99,
                ["wide"] = JsonTestWideEnum.Max,
                ["severity"] = Severity.Critical,
            };

            AssertParity(values, true, "default");
            AssertParity(values, false, "reflection-disabled");

            TestHelpers.AssertEqual(
                FixedPrefix + ",\"properties\":{\"wide\":18446744073709551615}}",
                CreateEntry("wide", JsonTestWideEnum.Max).ToJsonCore(null, false),
                "ulong enums must serialize their full unsigned value.");
            return Task.CompletedTask;
        }

        private static Task NonFiniteNumbers(CancellationToken ct)
        {
            LogEntry entry = CreateEntry();
            entry.Properties["nan"] = double.NaN;
            entry.Properties["posInf"] = double.PositiveInfinity;
            entry.Properties["negInf"] = float.NegativeInfinity;
            entry.Properties["halfNan"] = Half.NaN;

            string expected = FixedPrefix + ",\"properties\":{\"nan\":\"NaN\",\"posInf\":\"Infinity\",\"negInf\":\"-Infinity\",\"halfNan\":\"NaN\"}}";
            TestHelpers.AssertEqual(expected, entry.ToJson(), "Non-finite values must serialize as strings.");
            TestHelpers.AssertEqual(expected, entry.ToJsonCore(null, false), "Non-finite values must serialize as strings with reflection disabled.");

            LogEntry nested = CreateEntry("values", new List<double> { 1.0, double.NaN });
            TestHelpers.AssertEqual(
                FixedPrefix + ",\"properties\":{\"values\":[1,\"NaN\"]}}",
                nested.ToJsonCore(null, false),
                "Non-finite values inside collections must serialize as strings with reflection disabled.");
            return Task.CompletedTask;
        }

        private static Task PropertyKeyEscaping(CancellationToken ct)
        {
            Dictionary<string, object?> values = new Dictionary<string, object?>
            {
                ["a<b>"] = 1,
                ["quote\"key"] = 2,
                ["unicodé"] = 3,
                [string.Empty] = 4,
            };

            AssertParity(values, true, "default");
            AssertParity(values, false, "reflection-disabled");
            return Task.CompletedTask;
        }

        private static Task DepthLimitSelfReference(CancellationToken ct)
        {
            List<object> loop = new List<object>();
            loop.Add(loop);

            string json = CreateEntry("loop", loop).ToJsonCore(null, false);
            TestHelpers.AssertContains(json, "System.Collections.Generic.List", "The depth limit must write the innermost value as a string.");
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 256 });
            TestHelpers.AssertEqual(JsonValueKind.Object, document.RootElement.ValueKind, "Truncated output must still be valid JSON.");
            return Task.CompletedTask;
        }

        private static Task ReflectionCycleThrows(CancellationToken ct)
        {
            JsonTestNode node = new JsonTestNode();
            node.Next = node;
            LogEntry entry = CreateEntry("node", node);
            TestHelpers.ExpectThrows<JsonException>(() => entry.ToJson());
            return Task.CompletedTask;
        }

        private static Task OptionsNullThrows(CancellationToken ct)
        {
            TestHelpers.ExpectThrows<ArgumentNullException>(() => CreateEntry().ToJson(null!), "options");
            return Task.CompletedTask;
        }

        private static Task OptionsSourceGenerated(CancellationToken ct)
        {
            JsonSerializerOptions options = new JsonSerializerOptions { TypeInfoResolver = JsonTestContext.Default };
            LogEntry entry = CreateEntry("order", new JsonTestOrder { Name = "gizmo" });

            string expected = FixedPrefix + ",\"properties\":{\"order\":{\"Name\":\"gizmo\",\"Quantity\":3,\"Items\":[1,2]}}}";
            TestHelpers.AssertEqual(expected, entry.ToJsonCore(options, false), "Source-generated contracts must serialize complex values in full with reflection disabled.");
            TestHelpers.AssertEqual(expected, entry.ToJson(options), "Source-generated contracts must serialize complex values through the public overload.");
            return Task.CompletedTask;
        }

        private static Task OptionsUnresolvedFallsBack(CancellationToken ct)
        {
            JsonSerializerOptions options = new JsonSerializerOptions { TypeInfoResolver = JsonTestContext.Default };
            LogEntry entry = CreateEntry();
            entry.Properties["guid"] = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
            entry.Properties["node"] = new JsonTestNode { Name = "n1" };
            entry.Properties["nan"] = double.NaN;

            string expected = FixedPrefix + ",\"properties\":{\"guid\":\"0f8fad5b-d9cb-469f-a165-70867728950e\",\"node\":\"SyslogLogging.Tests.Shared.JsonTestNode\",\"nan\":\"NaN\"}}";
            TestHelpers.AssertEqual(expected, entry.ToJsonCore(options, false), "Unresolved values must fall back to the reflection-free writer.");
            return Task.CompletedTask;
        }

        private static Task OptionsIndented(CancellationToken ct)
        {
            JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
            string json = CreateEntry("OrderId", 42).ToJson(options);
            TestHelpers.AssertContains(json, "{" + Environment.NewLine + "  \"timestamp\"", "Indented output must place fields on separate lines.");
            TestHelpers.AssertContains(json, "    \"OrderId\": 42", "Indented output must indent nested properties.");
            return Task.CompletedTask;
        }

        private static Task OptionsEncoder(CancellationToken ct)
        {
            JsonSerializerOptions options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            string json = CreateEntry("text", "<b>").ToJson(options);
            TestHelpers.AssertContains(json, "\"message\":\"json <&> \\\"q\\\" é\"", "The configured encoder must apply to fixed fields.");
            TestHelpers.AssertContains(json, "\"text\":\"<b>\"", "The configured encoder must apply to property values.");
            return Task.CompletedTask;
        }

        private static Task OptionsConverters(CancellationToken ct)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            string json = CreateEntry("color", JsonTestColor.Blue).ToJson(options);
            TestHelpers.AssertContains(json, "\"color\":\"Blue\"", "Options converters must apply to property values.");
            return Task.CompletedTask;
        }

        private static Task DefaultMatchesOptionsDefault(CancellationToken ct)
        {
            LogEntry entry = CreateEntry();
            foreach (KeyValuePair<string, object?> kvp in ScalarValues()) entry.Properties[kvp.Key] = kvp.Value!;
            foreach (KeyValuePair<string, object?> kvp in CollectionValues()) entry.Properties[kvp.Key] = kvp.Value!;
            entry.Properties["order"] = new JsonTestOrder();

            TestHelpers.AssertEqual(entry.ToJson(), entry.ToJson(new JsonSerializerOptions()), "Default options must not change output.");
            return Task.CompletedTask;
        }

        private static async Task ConcurrentSerialization(CancellationToken ct)
        {
            Task[] tasks = Enumerable.Range(0, 32).Select(i => Task.Run(() =>
            {
                for (int j = 0; j < 50; j++)
                {
                    LogEntry entry = CreateEntry("index", i * 1000 + j);
                    entry.Properties["order"] = new JsonTestOrder { Quantity = i };
                    string expected = LegacyToJson(entry);
                    TestHelpers.AssertEqual(expected, entry.ToJson(), "Concurrent default serialization must be correct.");
                    TestHelpers.AssertContains(entry.ToJsonCore(null, false), "\"index\":" + (i * 1000 + j), "Concurrent reflection-disabled serialization must be correct.");
                }
            }, ct)).ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static Task ILoggerProperties(CancellationToken ct)
        {
            using TemporaryDirectory temp = new TemporaryDirectory("json-ilogger");
            using LoggingModule module = new LoggingModule(temp.GetPath("json.log"), FileLoggingMode.SingleLogFile, false);

            LogEntry? captured = null;
            module.MessageLogged += entry => captured = entry;

            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(new SyslogLoggerProvider(module)));
            ILogger logger = factory.CreateLogger("JsonCategory");
            logger.LogWarning("User {UserId} paid {Amount} on {When}", 42, 19.95m, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

            TestHelpers.AssertTrue(captured != null, "MessageLogged must capture the ILogger entry.");
            string json = captured!.ToJsonCore(null, false);
            TestHelpers.AssertContains(json, "\"source\":\"JsonCategory\"", "Category must serialize as source.");
            TestHelpers.AssertContains(json, "\"UserId\":42", "Integer template arguments must serialize as numbers.");
            TestHelpers.AssertContains(json, "\"Amount\":19.95", "Decimal template arguments must serialize as numbers.");
            TestHelpers.AssertContains(json, "\"When\":\"2026-01-02T00:00:00Z\"", "DateTime template arguments must serialize as ISO 8601 strings.");
            TestHelpers.AssertEqual(json, captured.ToJson(), "ILogger entries must serialize identically in both modes.");
            return Task.CompletedTask;
        }

        private static Task AssemblyTrimmable(CancellationToken ct)
        {
            Assembly assembly = typeof(LogEntry).Assembly;
            AssemblyMetadataAttribute? trimmable = assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "IsTrimmable");

            TestHelpers.AssertTrue(trimmable != null, "The library must carry IsTrimmable assembly metadata.");
            TestHelpers.AssertEqual("True", trimmable!.Value, "The library must be marked trimmable.");
            return Task.CompletedTask;
        }
    }
}
