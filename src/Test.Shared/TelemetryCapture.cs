namespace SyslogLogging.Tests.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;

    using SyslogLogging;

    /// <summary>
    /// In-memory metric and span capture for the SyslogLogging meter and activity source.
    /// SyslogLogging instruments are process-wide, so each capture only keeps measurements and spans
    /// produced inside its own async flow (tracked with AsyncLocal). That keeps captures isolated when
    /// test cases run in parallel.
    /// </summary>
    internal sealed class TelemetryCapture : IDisposable
    {
        public const string TestSourceName = "SyslogLogging.Tests";

        public static readonly ActivitySource TestSource = new ActivitySource(TestSourceName);

        private static readonly AsyncLocal<TelemetryCapture?> _Current = new AsyncLocal<TelemetryCapture?>();

        private readonly object _Lock = new object();
        private readonly List<CapturedMeasurement> _Measurements = new List<CapturedMeasurement>();
        private readonly List<Activity> _Activities = new List<Activity>();
        private readonly HashSet<string> _PublishedInstruments = new HashSet<string>();
        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;
        private readonly TelemetryCapture? _Previous;

        public TelemetryCapture()
        {
            _Previous = _Current.Value;
            _Current.Value = this;

            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != SyslogLoggingTelemetry.MeterName) return;
                lock (_Lock) _PublishedInstruments.Add(instrument.Name);
                listener.EnableMeasurementEvents(instrument);
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == SyslogLoggingTelemetry.ActivitySourceName || source.Name == TestSourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (!ReferenceEquals(_Current.Value, this)) return;
                    if (activity.Source.Name != SyslogLoggingTelemetry.ActivitySourceName) return;
                    lock (_Lock) _Activities.Add(activity);
                }
            };
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        public IReadOnlyCollection<string> PublishedInstruments
        {
            get
            {
                lock (_Lock) return _PublishedInstruments.ToList();
            }
        }

        public List<CapturedMeasurement> Measurements(string name)
        {
            lock (_Lock) return _Measurements.Where(m => m.Name == name).ToList();
        }

        public List<Activity> Spans(string name)
        {
            lock (_Lock) return _Activities.Where(a => a.DisplayName == name).ToList();
        }

        public List<Activity> AllSpans()
        {
            lock (_Lock) return _Activities.ToList();
        }

        public double Sum(string name, params KeyValuePair<string, object?>[] tags)
        {
            return Measurements(name).Where(m => tags.All(t => m.HasTag(t.Key, t.Value))).Sum(m => m.Value);
        }

        public int Count(string name, params KeyValuePair<string, object?>[] tags)
        {
            return Measurements(name).Count(m => tags.All(t => m.HasTag(t.Key, t.Value)));
        }

        public void CollectObservables()
        {
            _MeterListener.RecordObservableInstruments();
        }

        public string Dump()
        {
            lock (_Lock)
            {
                return string.Join(Environment.NewLine, _Measurements.Select(m => m.ToString()))
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, _Activities.Select(a => "span " + a.DisplayName + " " + a.Status));
            }
        }

        public static KeyValuePair<string, object?> Tag(string key, object? value)
        {
            return new KeyValuePair<string, object?>(key, value);
        }

        public void Dispose()
        {
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
            _Current.Value = _Previous;
        }

        private void OnMeasurement<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (!ReferenceEquals(_Current.Value, this)) return;

            Dictionary<string, object?> copy = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, object?> tag in tags) copy[tag.Key] = tag.Value;

            CapturedMeasurement measurement = new CapturedMeasurement(instrument.Name, Convert.ToDouble(value), copy);
            lock (_Lock) _Measurements.Add(measurement);
        }
    }
}
