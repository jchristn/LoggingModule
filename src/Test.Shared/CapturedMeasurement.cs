namespace SyslogLogging.Tests.Shared
{
    using System;
    using System.Collections.Generic;

    internal sealed class CapturedMeasurement
    {
        public string Name { get; }

        public double Value { get; }

        public Dictionary<string, object?> Tags { get; }

        public CapturedMeasurement(string name, double value, Dictionary<string, object?> tags)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
            Tags = tags ?? new Dictionary<string, object?>();
        }

        public bool HasTag(string key, object? expected)
        {
            if (!Tags.TryGetValue(key, out object? actual)) return false;
            return Equals(Convert.ToString(actual), Convert.ToString(expected));
        }

        public override string ToString()
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, object?> tag in Tags) parts.Add(tag.Key + "=" + tag.Value);
            return Name + "(" + Value + ") {" + string.Join(", ", parts) + "}";
        }
    }
}
