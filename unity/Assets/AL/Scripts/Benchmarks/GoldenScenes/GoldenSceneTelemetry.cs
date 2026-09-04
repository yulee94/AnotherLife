using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AL.Benchmarks.GoldenScenes
{
    public enum TelemetryCapabilityStatus
    {
        Supported,
        Unsupported,
        Error
    }

    public sealed class TelemetryCapability
    {
        private TelemetryCapability(
            string metricId,
            string unit,
            string source,
            TelemetryCapabilityStatus status,
            string reason,
            int sampleCount,
            string sampleScope)
        {
            if (string.IsNullOrWhiteSpace(metricId))
                throw new ArgumentException("Metric ID is required.", nameof(metricId));
            if (string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("Metric unit is required.", nameof(unit));
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Metric source is required.", nameof(source));
            if (sampleCount < 0)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            if (string.IsNullOrWhiteSpace(sampleScope))
                throw new ArgumentException("Metric sample scope is required.", nameof(sampleScope));

            MetricId = metricId;
            Unit = unit;
            Source = source;
            Status = status;
            Reason = reason ?? string.Empty;
            SampleCount = sampleCount;
            SampleScope = sampleScope;
        }

        public string MetricId { get; }
        public string Unit { get; }
        public string Source { get; }
        public TelemetryCapabilityStatus Status { get; }
        public string Reason { get; }
        public int SampleCount { get; }
        public string SampleScope { get; }

        public static TelemetryCapability Supported(
            string metricId,
            string unit,
            string source,
            int sampleCount = 0,
            string sampleScope = "measured-frames")
        {
            return new TelemetryCapability(
                metricId,
                unit,
                source,
                TelemetryCapabilityStatus.Supported,
                string.Empty,
                sampleCount,
                sampleScope);
        }

        public static TelemetryCapability Unsupported(
            string metricId,
            string unit,
            string source,
            string reason,
            string sampleScope = "measured-frames")
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Unsupported metrics require a reason.", nameof(reason));
            return new TelemetryCapability(
                metricId,
                unit,
                source,
                TelemetryCapabilityStatus.Unsupported,
                reason,
                0,
                sampleScope);
        }

        public static TelemetryCapability Error(
            string metricId,
            string unit,
            string source,
            string reason,
            string sampleScope = "measured-frames")
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Errored metrics require a reason.", nameof(reason));
            return new TelemetryCapability(
                metricId,
                unit,
                source,
                TelemetryCapabilityStatus.Error,
                reason,
                0,
                sampleScope);
        }

        internal TelemetryCapability WithSampleCount(int sampleCount)
        {
            if (Status == TelemetryCapabilityStatus.Supported && sampleCount == 0)
            {
                return Unsupported(
                    MetricId,
                    Unit,
                    Source,
                    "No measured samples were collected.",
                    SampleScope);
            }
            return new TelemetryCapability(
                MetricId, Unit, Source, Status, Reason, sampleCount, SampleScope);
        }

        public string ToJson()
        {
            var json = new StringBuilder(192);
            json.Append('{');
            TelemetryJson.AppendString(json, "metricId", MetricId, true);
            TelemetryJson.AppendString(json, "unit", Unit);
            TelemetryJson.AppendString(json, "source", Source);
            TelemetryJson.AppendString(json, "sampleScope", SampleScope);
            TelemetryJson.AppendString(json, "status", Status.ToString().ToLowerInvariant());
            TelemetryJson.AppendInteger(json, "sampleCount", SampleCount);
            TelemetryJson.AppendString(json, "reason", Reason);
            json.Append('}');
            return json.ToString();
        }
    }

    public enum TelemetryHitchSeverity
    {
        None,
        PacingMiss,
        Hitch,
        SevereHitch
    }

    public sealed class TelemetryDistribution
    {
        internal TelemetryDistribution(
            int sampleCount,
            double minimum,
            double p50,
            double p90,
            double p95,
            double p99,
            double maximum)
        {
            SampleCount = sampleCount;
            Minimum = minimum;
            P50 = p50;
            P90 = p90;
            P95 = p95;
            P99 = p99;
            Maximum = maximum;
        }

        public int SampleCount { get; }
        public double Minimum { get; }
        public double P50 { get; }
        public double P90 { get; }
        public double P95 { get; }
        public double P99 { get; }
        public double Maximum { get; }
        public string Method => "nearest-rank";
    }

    public sealed class FramePacingSummary
    {
        internal FramePacingSummary(
            int sampleCount,
            int targetFrameRate,
            double targetFrameTimeMs,
            double averageFrameTimeMs,
            double standardDeviationMs,
            int withinBudgetCount,
            int overBudgetCount,
            int longestOverBudgetRun,
            int pacingMissCount,
            int hitchCount,
            int severeHitchCount)
        {
            SampleCount = sampleCount;
            TargetFrameRate = targetFrameRate;
            TargetFrameTimeMs = targetFrameTimeMs;
            AverageFrameTimeMs = averageFrameTimeMs;
            StandardDeviationMs = standardDeviationMs;
            WithinBudgetCount = withinBudgetCount;
            OverBudgetCount = overBudgetCount;
            LongestOverBudgetRun = longestOverBudgetRun;
            PacingMissCount = pacingMissCount;
            HitchCount = hitchCount;
            SevereHitchCount = severeHitchCount;
        }

        public int SampleCount { get; }
        public int TargetFrameRate { get; }
        public double TargetFrameTimeMs { get; }
        public double AverageFrameTimeMs { get; }
        public double StandardDeviationMs { get; }
        public int WithinBudgetCount { get; }
        public int OverBudgetCount { get; }
        public int LongestOverBudgetRun { get; }
        public int PacingMissCount { get; }
        public int HitchCount { get; }
        public int SevereHitchCount { get; }
        public double WithinBudgetRatio => SampleCount == 0 ? 0d : (double)WithinBudgetCount / SampleCount;
    }

    public static class GoldenSceneTelemetryMetricIds
    {
        public const string DeliveredFrameTime = "frame.delivered_time";
        public const string CpuFrameTime = "frame.cpu_time";
        public const string GpuFrameTime = "frame.gpu_time";
        public const string SystemUsedMemory = "memory.system_used";
        public const string UnityUsedMemory = "memory.unity_used";
        public const string UnityReservedMemory = "memory.unity_reserved";
        public const string GraphicsUsedMemory = "memory.graphics_used";
        public const string ManagedHeapUsed = "memory.managed_heap_used";
        public const string ManagedHeapReserved = "memory.managed_heap_reserved";
        public const string ManagedAllocatedInFrame = "allocation.managed_in_frame";
        public const string NativeAllocationCount = "allocation.native_count";
        public const string NativeUsedMemory = "allocation.native_used";
        public const string GarbageCollectionCount = "allocation.gc_collection_count";
        public const string DrawCalls = "render.draw_calls";
        public const string Batches = "render.batches";
        public const string Triangles = "render.triangles";
        public const string Vertices = "render.vertices";
        public const string ActiveRenderers = "render.active_renderers";
        public const string TextureStreamingRequests = "streaming.texture_requests";
        public const string TextureStreamingBytes = "streaming.texture_bytes";
        public const string AssetStreamingStalls = "streaming.asset_stall_time";
        public const string ShaderCompilationEvents = "streaming.shader_compilation_events";
        public const string LodGroups = "lod.active_groups";
        public const string LodTransitions = "lod.transitions";
        public const string VfxSources = "density.vfx_sources";
        public const string ParticleCount = "density.particles";
        public const string FullActors = "density.actors_full";
        public const string FallbackActors = "density.actors_fallback";
        public const string NameplateActors = "density.actors_nameplate";
        public const string DeviceTemperature = "device.temperature";
        public const string DeviceThermalState = "device.thermal_state";
        public const string BatteryLevel = "device.battery_level";
        public const string RenderScale = "quality.render_scale";
        public const string LodBias = "quality.lod_bias";
        public const string VfxDensity = "quality.vfx_density";
    }

    public enum GoldenSceneTelemetryInterval
    {
        Warmup,
        Measured
    }

    public sealed class GoldenSceneTelemetryConfiguration
    {
        public GoldenSceneTelemetryConfiguration(
            int targetFrameRate,
            double warmupSeconds,
            double measurementSeconds)
        {
            if (targetFrameRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate));
            if (!FiniteNonNegative(warmupSeconds))
                throw new ArgumentOutOfRangeException(nameof(warmupSeconds));
            if (!FiniteNonNegative(measurementSeconds) || measurementSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(measurementSeconds));

            TargetFrameRate = targetFrameRate;
            WarmupSeconds = warmupSeconds;
            MeasurementSeconds = measurementSeconds;
        }

        public int TargetFrameRate { get; }
        public double WarmupSeconds { get; }
        public double MeasurementSeconds { get; }
        public double TotalSeconds => WarmupSeconds + MeasurementSeconds;

        private static bool FiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }
    }

    public sealed class GoldenSceneDeviceSnapshot
    {
        public GoldenSceneDeviceSnapshot(
            double? batteryLevel,
            string batteryStatus,
            double? temperatureCelsius,
            string thermalState)
        {
            if (batteryLevel.HasValue &&
                (double.IsNaN(batteryLevel.Value) ||
                 double.IsInfinity(batteryLevel.Value) ||
                 batteryLevel.Value < 0d ||
                 batteryLevel.Value > 1d))
                throw new ArgumentOutOfRangeException(nameof(batteryLevel));
            if (temperatureCelsius.HasValue &&
                (double.IsNaN(temperatureCelsius.Value) ||
                 double.IsInfinity(temperatureCelsius.Value)))
                throw new ArgumentOutOfRangeException(nameof(temperatureCelsius));

            BatteryLevel = batteryLevel;
            BatteryStatus = batteryStatus ?? string.Empty;
            TemperatureCelsius = temperatureCelsius;
            ThermalState = thermalState ?? string.Empty;
        }

        public double? BatteryLevel { get; }
        public string BatteryStatus { get; }
        public double? TemperatureCelsius { get; }
        public string ThermalState { get; }
    }

    public sealed class GoldenSceneDeviceSample
    {
        internal GoldenSceneDeviceSample(
            double elapsedSeconds,
            GoldenSceneTelemetryInterval interval,
            GoldenSceneDeviceSnapshot snapshot)
        {
            ElapsedSeconds = elapsedSeconds;
            Interval = interval;
            Snapshot = snapshot;
        }

        public double ElapsedSeconds { get; }
        public GoldenSceneTelemetryInterval Interval { get; }
        public GoldenSceneDeviceSnapshot Snapshot { get; }
    }

    public readonly struct GoldenSceneFrameObservation
    {
        private readonly string[] counterIds;
        private readonly double?[] counterValues;

        public GoldenSceneFrameObservation(
            int sequence,
            double elapsedSeconds,
            double deliveredFrameTimeMs,
            double? cpuFrameTimeMs,
            double? gpuFrameTimeMs,
            IReadOnlyDictionary<string, double?> counters,
            ulong? frameTimingFrameStartTimestampTicks = null,
            ulong? cpuTimerFrequency = null)
        {
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            ValidateValue(elapsedSeconds, nameof(elapsedSeconds));
            ValidateValue(deliveredFrameTimeMs, nameof(deliveredFrameTimeMs));
            ValidateOptional(cpuFrameTimeMs, nameof(cpuFrameTimeMs));
            ValidateOptional(gpuFrameTimeMs, nameof(gpuFrameTimeMs));
            ValidateFrameTimingIdentity(frameTimingFrameStartTimestampTicks, cpuTimerFrequency);

            Sequence = sequence;
            ElapsedSeconds = elapsedSeconds;
            DeliveredFrameTimeMs = deliveredFrameTimeMs;
            CpuFrameTimeMs = cpuFrameTimeMs;
            GpuFrameTimeMs = gpuFrameTimeMs;
            FrameTimingFrameStartTimestampTicks = frameTimingFrameStartTimestampTicks;
            CpuTimerFrequency = cpuTimerFrequency;
            CounterSnapshot snapshot = CounterSnapshot.FromDictionary(counters);
            counterIds = snapshot.Ids;
            counterValues = snapshot.RawValues;
        }

        internal GoldenSceneFrameObservation(
            int sequence,
            double elapsedSeconds,
            double deliveredFrameTimeMs,
            double? cpuFrameTimeMs,
            double? gpuFrameTimeMs,
            ulong? frameTimingFrameStartTimestampTicks,
            ulong? cpuTimerFrequency,
            string[] counterIds,
            double?[] counterValues)
        {
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            ValidateValue(elapsedSeconds, nameof(elapsedSeconds));
            ValidateValue(deliveredFrameTimeMs, nameof(deliveredFrameTimeMs));
            ValidateOptional(cpuFrameTimeMs, nameof(cpuFrameTimeMs));
            ValidateOptional(gpuFrameTimeMs, nameof(gpuFrameTimeMs));
            ValidateFrameTimingIdentity(frameTimingFrameStartTimestampTicks, cpuTimerFrequency);
            if (counterIds == null) throw new ArgumentNullException(nameof(counterIds));
            if (counterValues == null) throw new ArgumentNullException(nameof(counterValues));
            if (counterIds.Length != counterValues.Length)
                throw new ArgumentException("Counter IDs and values must have equal lengths.");
            for (int index = 0; index < counterValues.Length; index++)
                ValidateOptional(counterValues[index], counterIds[index]);

            Sequence = sequence;
            ElapsedSeconds = elapsedSeconds;
            DeliveredFrameTimeMs = deliveredFrameTimeMs;
            CpuFrameTimeMs = cpuFrameTimeMs;
            GpuFrameTimeMs = gpuFrameTimeMs;
            FrameTimingFrameStartTimestampTicks = frameTimingFrameStartTimestampTicks;
            CpuTimerFrequency = cpuTimerFrequency;
            this.counterIds = counterIds;
            this.counterValues = counterValues;
        }

        public int Sequence { get; }
        public double ElapsedSeconds { get; }
        public double DeliveredFrameTimeMs { get; }
        public double? CpuFrameTimeMs { get; }
        public double? GpuFrameTimeMs { get; }
        public ulong? FrameTimingFrameStartTimestampTicks { get; }
        public ulong? CpuTimerFrequency { get; }
        public IReadOnlyDictionary<string, double?> Counters =>
            new CounterSnapshot(
                counterIds ?? Array.Empty<string>(),
                counterValues ?? Array.Empty<double?>());

        private static void ValidateOptional(double? value, string name)
        {
            if (value.HasValue) ValidateValue(value.Value, name);
        }

        private static void ValidateFrameTimingIdentity(
            ulong? frameTimingFrameStartTimestampTicks,
            ulong? cpuTimerFrequency)
        {
            if (frameTimingFrameStartTimestampTicks.HasValue != cpuTimerFrequency.HasValue)
                throw new ArgumentException("Frame timing ticks and CPU timer frequency must be paired.");
            if (cpuTimerFrequency.HasValue && cpuTimerFrequency.Value == 0UL)
                throw new ArgumentOutOfRangeException(nameof(cpuTimerFrequency));
        }

        internal GoldenSceneFrameObservation WithFrameTiming(
            ulong frameStartTimestampTicks,
            ulong cpuTimerFrequency,
            double? cpuFrameTimeMs,
            double? gpuFrameTimeMs)
        {
            return new GoldenSceneFrameObservation(
                Sequence,
                ElapsedSeconds,
                DeliveredFrameTimeMs,
                cpuFrameTimeMs,
                gpuFrameTimeMs,
                frameStartTimestampTicks,
                cpuTimerFrequency,
                counterIds ?? Array.Empty<string>(),
                counterValues ?? Array.Empty<double?>());
        }

        private static void ValidateValue(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }

        private sealed class CounterSnapshot : IReadOnlyDictionary<string, double?>
        {
            private readonly string[] ids;
            private readonly double?[] values;

            public CounterSnapshot(string[] ids, double?[] values)
            {
                this.ids = ids;
                this.values = values;
            }

            public int Count => ids.Length;
            public string[] Ids => ids;
            public double?[] RawValues => values;
            public IEnumerable<string> Keys => ids;
            public IEnumerable<double?> Values => values;

            public double? this[string key]
            {
                get
                {
                    if (!TryGetValue(key, out double? value))
                        throw new KeyNotFoundException(key);
                    return value;
                }
            }

            public static CounterSnapshot FromDictionary(
                IReadOnlyDictionary<string, double?> counters)
            {
                if (counters == null || counters.Count == 0)
                    return new CounterSnapshot(Array.Empty<string>(), Array.Empty<double?>());

                string[] sortedIds = counters.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
                var sortedValues = new double?[sortedIds.Length];
                for (int index = 0; index < sortedIds.Length; index++)
                {
                    string id = sortedIds[index];
                    if (string.IsNullOrWhiteSpace(id))
                        throw new ArgumentException("Counter IDs cannot be empty.", nameof(counters));
                    double? value = counters[id];
                    ValidateOptional(value, id);
                    sortedValues[index] = value;
                }
                return new CounterSnapshot(sortedIds, sortedValues);
            }

            public bool ContainsKey(string key)
            {
                return Array.BinarySearch(ids, key, StringComparer.Ordinal) >= 0;
            }

            public bool TryGetValue(string key, out double? value)
            {
                int index = Array.BinarySearch(ids, key, StringComparer.Ordinal);
                if (index >= 0)
                {
                    value = values[index];
                    return true;
                }
                value = null;
                return false;
            }

            public IEnumerator<KeyValuePair<string, double?>> GetEnumerator()
            {
                for (int index = 0; index < ids.Length; index++)
                    yield return new KeyValuePair<string, double?>(ids[index], values[index]);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }

    internal sealed class GoldenSceneFrameTimingAlignment
    {
        private readonly Queue<GoldenSceneFrameObservation> pending;
        private readonly int maxPendingFrames;

        public GoldenSceneFrameTimingAlignment(int maxPendingFrames)
        {
            if (maxPendingFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPendingFrames));
            this.maxPendingFrames = maxPendingFrames;
            pending = new Queue<GoldenSceneFrameObservation>(maxPendingFrames);
        }

        public int PendingCount => pending.Count;
        public bool IsAtCapacity => pending.Count >= maxPendingFrames;

        public void Enqueue(GoldenSceneFrameObservation observation)
        {
            pending.Enqueue(observation);
        }

        public bool TryCompleteOldest(
            ulong frameStartTimestampTicks,
            ulong cpuTimerFrequency,
            double? cpuFrameTimeMs,
            double? gpuFrameTimeMs,
            out GoldenSceneFrameObservation observation)
        {
            if (pending.Count == 0)
            {
                observation = default;
                return false;
            }
            observation = pending.Dequeue().WithFrameTiming(
                frameStartTimestampTicks,
                cpuTimerFrequency,
                cpuFrameTimeMs,
                gpuFrameTimeMs);
            return true;
        }

        public bool TryReleaseOldest(out GoldenSceneFrameObservation observation)
        {
            if (pending.Count == 0)
            {
                observation = default;
                return false;
            }
            observation = pending.Dequeue();
            return true;
        }
    }

    public readonly struct GoldenSceneRawFrameSample
    {
        internal GoldenSceneRawFrameSample(
            GoldenSceneFrameObservation observation,
            GoldenSceneTelemetryInterval interval)
        {
            Observation = observation;
            Interval = interval;
        }

        public GoldenSceneFrameObservation Observation { get; }
        public GoldenSceneTelemetryInterval Interval { get; }
    }

    public sealed class TelemetryHitchEvent
    {
        internal TelemetryHitchEvent(
            int sequence,
            double elapsedSeconds,
            double durationMs,
            TelemetryHitchSeverity severity)
        {
            Sequence = sequence;
            ElapsedSeconds = elapsedSeconds;
            DurationMs = durationMs;
            Severity = severity;
        }

        public int Sequence { get; }
        public double ElapsedSeconds { get; }
        public double DurationMs { get; }
        public TelemetryHitchSeverity Severity { get; }
    }

    public sealed class TelemetryMetricSummary
    {
        internal TelemetryMetricSummary(
            string metricId,
            string unit,
            TelemetryDistribution distribution)
        {
            MetricId = metricId;
            Unit = unit;
            Distribution = distribution;
        }

        public string MetricId { get; }
        public string Unit { get; }
        public TelemetryDistribution Distribution { get; }
    }

    public sealed class GoldenSceneTelemetrySession
    {
        private readonly List<GoldenSceneRawFrameSample> samples;
        private readonly List<GoldenSceneDeviceSample> deviceSamples;
        private readonly SortedDictionary<string, TelemetryCapability> capabilities =
            new SortedDictionary<string, TelemetryCapability>(StringComparer.Ordinal);
        private bool complete;

        public GoldenSceneTelemetrySession(
            GoldenSceneTelemetryConfiguration configuration,
            string collectionStartedAtUtc,
            bool isPlayerBuild,
            GoldenSceneDeviceSnapshot startDevice)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            int expectedFrames = (int)Math.Min(
                262144d,
                Math.Ceiling(Configuration.TotalSeconds * Configuration.TargetFrameRate) +
                Configuration.TargetFrameRate);
            samples = new List<GoldenSceneRawFrameSample>(Math.Max(1, expectedFrames));
            int expectedDeviceSamples = (int)Math.Min(
                4096d,
                Math.Ceiling(Configuration.TotalSeconds / 60d) + 2d);
            deviceSamples = new List<GoldenSceneDeviceSample>(Math.Max(2, expectedDeviceSamples));
            ValidateUtc(collectionStartedAtUtc, nameof(collectionStartedAtUtc));
            CollectionStartedAtUtc = collectionStartedAtUtc;
            IsPlayerBuild = isPlayerBuild;
            StartDevice = startDevice ?? throw new ArgumentNullException(nameof(startDevice));
            deviceSamples.Add(new GoldenSceneDeviceSample(
                0d,
                Configuration.WarmupSeconds > 0d
                    ? GoldenSceneTelemetryInterval.Warmup
                    : GoldenSceneTelemetryInterval.Measured,
                StartDevice));
            SetCapability(TelemetryCapability.Supported(
                GoldenSceneTelemetryMetricIds.DeliveredFrameTime,
                "milliseconds",
                "unity-unscaled-delta-time"));
        }

        public GoldenSceneTelemetryConfiguration Configuration { get; }
        public string CollectionStartedAtUtc { get; }
        public bool IsPlayerBuild { get; }
        public GoldenSceneDeviceSnapshot StartDevice { get; }

        public void SetCapability(TelemetryCapability capability)
        {
            if (complete) throw new InvalidOperationException("The telemetry session is complete.");
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            capabilities[capability.MetricId] = capability;
        }

        public void RecordFrame(GoldenSceneFrameObservation observation)
        {
            if (complete) throw new InvalidOperationException("The telemetry session is complete.");
            if (samples.Count > 0)
            {
                GoldenSceneFrameObservation previous = samples[samples.Count - 1].Observation;
                if (observation.Sequence <= previous.Sequence)
                    throw new InvalidOperationException("Frame sequence must increase.");
                if (observation.ElapsedSeconds < previous.ElapsedSeconds)
                    throw new InvalidOperationException("Elapsed time must not decrease.");
            }

            GoldenSceneTelemetryInterval interval =
                observation.ElapsedSeconds < Configuration.WarmupSeconds
                    ? GoldenSceneTelemetryInterval.Warmup
                    : GoldenSceneTelemetryInterval.Measured;
            samples.Add(new GoldenSceneRawFrameSample(observation, interval));
        }

        public void RecordDeviceSample(
            double elapsedSeconds,
            GoldenSceneDeviceSnapshot snapshot)
        {
            if (complete) throw new InvalidOperationException("The telemetry session is complete.");
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (deviceSamples.Count > 0 &&
                elapsedSeconds < deviceSamples[deviceSamples.Count - 1].ElapsedSeconds)
                throw new InvalidOperationException("Device sample time must not decrease.");

            var sample = new GoldenSceneDeviceSample(
                elapsedSeconds,
                elapsedSeconds < Configuration.WarmupSeconds
                    ? GoldenSceneTelemetryInterval.Warmup
                    : GoldenSceneTelemetryInterval.Measured,
                snapshot);
            if (deviceSamples.Count > 0 &&
                elapsedSeconds == deviceSamples[deviceSamples.Count - 1].ElapsedSeconds)
            {
                deviceSamples[deviceSamples.Count - 1] = sample;
                return;
            }
            deviceSamples.Add(sample);
        }

        public GoldenSceneTelemetryReport Complete(
            string collectionEndedAtUtc,
            GoldenSceneDeviceSnapshot endDevice)
        {
            if (complete) throw new InvalidOperationException("The telemetry session is complete.");
            ValidateUtc(collectionEndedAtUtc, nameof(collectionEndedAtUtc));
            if (endDevice == null) throw new ArgumentNullException(nameof(endDevice));
            if (samples.Count == 0 ||
                samples[samples.Count - 1].Observation.ElapsedSeconds < Configuration.TotalSeconds)
            {
                throw new InvalidOperationException(
                    "Telemetry collection cannot complete before the configured duration.");
            }
            DateTimeOffset startedAt = ParseUtc(CollectionStartedAtUtc);
            DateTimeOffset endedAt = ParseUtc(collectionEndedAtUtc);
            if ((endedAt - startedAt).TotalSeconds < Configuration.TotalSeconds)
            {
                throw new InvalidOperationException(
                    "Telemetry timestamps do not cover the configured duration.");
            }

            double endElapsedSeconds = samples.Count == 0
                ? Configuration.TotalSeconds
                : Math.Max(Configuration.TotalSeconds, samples[samples.Count - 1].Observation.ElapsedSeconds);
            RecordDeviceSample(endElapsedSeconds, endDevice);
            complete = true;
            return GoldenSceneTelemetryReport.Create(
                Configuration,
                CollectionStartedAtUtc,
                collectionEndedAtUtc,
                IsPlayerBuild,
                StartDevice,
                endDevice,
                samples,
                deviceSamples,
                capabilities);
        }

        private static void ValidateUtc(string value, string parameterName)
        {
            if (!DateTimeOffset.TryParseExact(
                    value,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed) ||
                parsed.Offset != TimeSpan.Zero ||
                !value.EndsWith("Z", StringComparison.Ordinal))
                throw new ArgumentException("Timestamp must be canonical UTC round-trip format.", parameterName);
        }

        private static DateTimeOffset ParseUtc(string value)
        {
            return DateTimeOffset.ParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }

    public sealed class GoldenSceneTelemetryReport
    {
        private GoldenSceneTelemetryReport(
            GoldenSceneTelemetryConfiguration configuration,
            string collectionStartedAtUtc,
            string collectionEndedAtUtc,
            bool isPlayerBuild,
            GoldenSceneDeviceSnapshot startDevice,
            GoldenSceneDeviceSnapshot endDevice,
            IReadOnlyList<GoldenSceneRawFrameSample> rawSamples,
            IReadOnlyList<GoldenSceneDeviceSample> deviceSamples,
            IReadOnlyList<TelemetryCapability> capabilities,
            IReadOnlyDictionary<string, TelemetryMetricSummary> metricSummaries,
            FramePacingSummary framePacing,
            IReadOnlyList<TelemetryHitchEvent> hitches,
            int warmupSampleCount,
            int measuredSampleCount)
        {
            Configuration = configuration;
            CollectionStartedAtUtc = collectionStartedAtUtc;
            CollectionEndedAtUtc = collectionEndedAtUtc;
            IsPlayerBuild = isPlayerBuild;
            StartDevice = startDevice;
            EndDevice = endDevice;
            RawSamples = rawSamples;
            DeviceSamples = deviceSamples;
            Capabilities = capabilities;
            MetricSummaries = metricSummaries;
            FramePacing = framePacing;
            Hitches = hitches;
            WarmupSampleCount = warmupSampleCount;
            MeasuredSampleCount = measuredSampleCount;
        }

        public GoldenSceneTelemetryConfiguration Configuration { get; }
        public string CollectionStartedAtUtc { get; }
        public string CollectionEndedAtUtc { get; }
        public double ActualDurationSeconds =>
            (DateTimeOffset.ParseExact(
                 CollectionEndedAtUtc,
                 "O",
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal) -
             DateTimeOffset.ParseExact(
                 CollectionStartedAtUtc,
                 "O",
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)).TotalSeconds;
        public bool IsPlayerBuild { get; }
        public bool IsTargetPlatformCertificationEligible => false;
        public string CertificationStatus => IsPlayerBuild
            ? "player-build-telemetry-awaiting-validated-benchmark-identity"
            : "editor-development-only-not-certifying";
        public GoldenSceneDeviceSnapshot StartDevice { get; }
        public GoldenSceneDeviceSnapshot EndDevice { get; }
        public double? BatteryDelta => StartDevice.BatteryLevel.HasValue && EndDevice.BatteryLevel.HasValue
            ? Math.Round(EndDevice.BatteryLevel.Value - StartDevice.BatteryLevel.Value, 6)
            : (double?)null;
        public IReadOnlyList<GoldenSceneRawFrameSample> RawSamples { get; }
        public IReadOnlyList<GoldenSceneDeviceSample> DeviceSamples { get; }
        public IReadOnlyList<TelemetryCapability> Capabilities { get; }
        public IReadOnlyDictionary<string, TelemetryMetricSummary> MetricSummaries { get; }
        public FramePacingSummary FramePacing { get; }
        public IReadOnlyList<TelemetryHitchEvent> Hitches { get; }
        public int WarmupSampleCount { get; }
        public int MeasuredSampleCount { get; }

        internal static GoldenSceneTelemetryReport Create(
            GoldenSceneTelemetryConfiguration configuration,
            string collectionStartedAtUtc,
            string collectionEndedAtUtc,
            bool isPlayerBuild,
            GoldenSceneDeviceSnapshot startDevice,
            GoldenSceneDeviceSnapshot endDevice,
            IReadOnlyList<GoldenSceneRawFrameSample> rawSamples,
            IReadOnlyList<GoldenSceneDeviceSample> deviceSamples,
            IReadOnlyDictionary<string, TelemetryCapability> declaredCapabilities)
        {
            var measured = new List<GoldenSceneRawFrameSample>();
            var hitches = new List<TelemetryHitchEvent>();
            int warmupCount = 0;
            foreach (GoldenSceneRawFrameSample sample in rawSamples)
            {
                if (sample.Interval == GoldenSceneTelemetryInterval.Warmup) warmupCount++;
                else
                {
                    measured.Add(sample);
                    TelemetryHitchSeverity severity = GoldenSceneTelemetryMath.ClassifyHitch(
                        sample.Observation.DeliveredFrameTimeMs,
                        configuration.TargetFrameRate);
                    if (severity == TelemetryHitchSeverity.Hitch ||
                        severity == TelemetryHitchSeverity.SevereHitch)
                    {
                        hitches.Add(new TelemetryHitchEvent(
                            sample.Observation.Sequence,
                            sample.Observation.ElapsedSeconds,
                            sample.Observation.DeliveredFrameTimeMs,
                            severity));
                    }
                }
            }
            if (measured.Count == 0)
                throw new InvalidOperationException("At least one measured frame is required.");

            var values = new SortedDictionary<string, List<double>>(StringComparer.Ordinal)
            {
                [GoldenSceneTelemetryMetricIds.DeliveredFrameTime] = new List<double>(),
                [GoldenSceneTelemetryMetricIds.CpuFrameTime] = new List<double>(),
                [GoldenSceneTelemetryMetricIds.GpuFrameTime] = new List<double>()
            };
            foreach (GoldenSceneRawFrameSample sample in measured)
            {
                GoldenSceneFrameObservation observation = sample.Observation;
                values[GoldenSceneTelemetryMetricIds.DeliveredFrameTime]
                    .Add(observation.DeliveredFrameTimeMs);
                if (observation.CpuFrameTimeMs.HasValue)
                    values[GoldenSceneTelemetryMetricIds.CpuFrameTime]
                        .Add(observation.CpuFrameTimeMs.Value);
                if (observation.GpuFrameTimeMs.HasValue)
                    values[GoldenSceneTelemetryMetricIds.GpuFrameTime]
                        .Add(observation.GpuFrameTimeMs.Value);
                foreach (KeyValuePair<string, double?> counter in observation.Counters)
                {
                    if (!counter.Value.HasValue) continue;
                    if (!values.TryGetValue(counter.Key, out List<double> metricValues))
                    {
                        metricValues = new List<double>();
                        values.Add(counter.Key, metricValues);
                    }
                    metricValues.Add(counter.Value.Value);
                }
            }

            var summaries = new SortedDictionary<string, TelemetryMetricSummary>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<double>> metric in values)
            {
                if (metric.Value.Count == 0) continue;
                string unit = declaredCapabilities.TryGetValue(metric.Key, out TelemetryCapability capability)
                    ? capability.Unit
                    : "unknown";
                summaries.Add(metric.Key, new TelemetryMetricSummary(
                    metric.Key,
                    unit,
                    GoldenSceneTelemetryMath.CalculateDistribution(metric.Value)));
            }

            var resolvedCapabilities = new List<TelemetryCapability>();
            foreach (KeyValuePair<string, TelemetryCapability> item in declaredCapabilities)
            {
                int sampleCount = summaries.TryGetValue(item.Key, out TelemetryMetricSummary summary)
                    ? summary.Distribution.SampleCount
                    : DeviceSampleCount(item.Key, deviceSamples);
                resolvedCapabilities.Add(item.Value.WithSampleCount(sampleCount));
            }

            var sampleCopy = new List<GoldenSceneRawFrameSample>(rawSamples);
            return new GoldenSceneTelemetryReport(
                configuration,
                collectionStartedAtUtc,
                collectionEndedAtUtc,
                isPlayerBuild,
                startDevice,
                endDevice,
                new ReadOnlyCollection<GoldenSceneRawFrameSample>(sampleCopy),
                new ReadOnlyCollection<GoldenSceneDeviceSample>(
                    new List<GoldenSceneDeviceSample>(deviceSamples)),
                new ReadOnlyCollection<TelemetryCapability>(resolvedCapabilities),
                new ReadOnlyDictionary<string, TelemetryMetricSummary>(summaries),
                GoldenSceneTelemetryMath.CalculateFramePacing(
                    values[GoldenSceneTelemetryMetricIds.DeliveredFrameTime],
                    configuration.TargetFrameRate),
                new ReadOnlyCollection<TelemetryHitchEvent>(hitches),
                warmupCount,
                measured.Count);
        }

        public string ToJson()
        {
            var json = new StringBuilder(Math.Max(4096, RawSamples.Count * 320));
            json.Append('{');
            TelemetryJson.AppendString(json, "schemaVersion", "1.0.0", true);
            TelemetryJson.AppendString(json, "collectionStartedAtUtc", CollectionStartedAtUtc);
            TelemetryJson.AppendString(json, "collectionEndedAtUtc", CollectionEndedAtUtc);
            TelemetryJson.AppendNumber(json, "actualDurationSeconds", ActualDurationSeconds);
            TelemetryJson.AppendNumber(json, "warmupSeconds", Configuration.WarmupSeconds);
            TelemetryJson.AppendNumber(json, "measurementSeconds", Configuration.MeasurementSeconds);
            TelemetryJson.AppendInteger(json, "targetFrameRate", Configuration.TargetFrameRate);
            TelemetryJson.AppendBoolean(json, "isPlayerBuild", IsPlayerBuild);
            TelemetryJson.AppendBoolean(
                json, "isTargetPlatformCertificationEligible", IsTargetPlatformCertificationEligible);
            TelemetryJson.AppendString(json, "certificationStatus", CertificationStatus);
            TelemetryJson.AppendInteger(json, "warmupSampleCount", WarmupSampleCount);
            TelemetryJson.AppendInteger(json, "measuredSampleCount", MeasuredSampleCount);
            TelemetryJson.AppendNullableNumber(json, "batteryDelta", BatteryDelta);
            TelemetryJson.Prefix(json, "deviceStart");
            AppendDevice(json, StartDevice);
            TelemetryJson.Prefix(json, "deviceEnd");
            AppendDevice(json, EndDevice);
            TelemetryJson.Prefix(json, "deviceSamples");
            json.Append('[');
            for (int index = 0; index < DeviceSamples.Count; index++)
            {
                if (index > 0) json.Append(',');
                AppendDeviceSample(json, DeviceSamples[index]);
            }
            json.Append(']');
            TelemetryJson.Prefix(json, "framePacing");
            AppendFramePacing(json, FramePacing);
            TelemetryJson.Prefix(json, "hitches");
            json.Append('[');
            for (int index = 0; index < Hitches.Count; index++)
            {
                if (index > 0) json.Append(',');
                AppendHitch(json, Hitches[index]);
            }
            json.Append(']');

            TelemetryJson.Prefix(json, "capabilities");
            json.Append('[');
            for (int index = 0; index < Capabilities.Count; index++)
            {
                if (index > 0) json.Append(',');
                json.Append(Capabilities[index].ToJson());
            }
            json.Append(']');

            TelemetryJson.Prefix(json, "aggregates");
            json.Append('{');
            bool firstAggregate = true;
            foreach (KeyValuePair<string, TelemetryMetricSummary> item in MetricSummaries)
            {
                TelemetryJson.Prefix(json, item.Key, firstAggregate);
                firstAggregate = false;
                AppendMetricSummary(json, item.Value);
            }
            json.Append('}');

            TelemetryJson.Prefix(json, "rawSamples");
            json.Append('[');
            for (int index = 0; index < RawSamples.Count; index++)
            {
                if (index > 0) json.Append(',');
                AppendRawSample(json, RawSamples[index]);
            }
            json.Append(']');
            json.Append('}');
            return json.ToString();
        }

        private static int DeviceSampleCount(
            string metricId,
            IReadOnlyList<GoldenSceneDeviceSample> deviceSamples)
        {
            int count = 0;
            if (metricId == GoldenSceneTelemetryMetricIds.BatteryLevel)
            {
                foreach (GoldenSceneDeviceSample sample in deviceSamples)
                    if (sample.Snapshot.BatteryLevel.HasValue) count++;
                return count;
            }
            if (metricId == GoldenSceneTelemetryMetricIds.DeviceTemperature)
            {
                foreach (GoldenSceneDeviceSample sample in deviceSamples)
                    if (sample.Snapshot.TemperatureCelsius.HasValue) count++;
                return count;
            }
            if (metricId != GoldenSceneTelemetryMetricIds.DeviceThermalState) return 0;
            foreach (GoldenSceneDeviceSample sample in deviceSamples)
                if (!string.IsNullOrWhiteSpace(sample.Snapshot.ThermalState)) count++;
            return count;
        }

        private static void AppendDevice(StringBuilder json, GoldenSceneDeviceSnapshot snapshot)
        {
            json.Append('{');
            TelemetryJson.AppendNullableNumber(json, "batteryLevel", snapshot.BatteryLevel, true);
            TelemetryJson.AppendString(json, "batteryStatus", snapshot.BatteryStatus);
            TelemetryJson.AppendNullableNumber(json, "temperatureCelsius", snapshot.TemperatureCelsius);
            TelemetryJson.AppendString(json, "thermalState", snapshot.ThermalState);
            json.Append('}');
        }

        private static void AppendDeviceSample(StringBuilder json, GoldenSceneDeviceSample sample)
        {
            json.Append('{');
            TelemetryJson.AppendNumber(json, "elapsedSeconds", sample.ElapsedSeconds, true);
            TelemetryJson.AppendString(
                json, "interval", sample.Interval.ToString().ToLowerInvariant());
            TelemetryJson.Prefix(json, "snapshot");
            AppendDevice(json, sample.Snapshot);
            json.Append('}');
        }

        private static void AppendFramePacing(StringBuilder json, FramePacingSummary summary)
        {
            json.Append('{');
            TelemetryJson.AppendInteger(json, "sampleCount", summary.SampleCount, true);
            TelemetryJson.AppendInteger(json, "targetFrameRate", summary.TargetFrameRate);
            TelemetryJson.AppendNumber(json, "targetFrameTimeMs", summary.TargetFrameTimeMs);
            TelemetryJson.AppendNumber(json, "averageFrameTimeMs", summary.AverageFrameTimeMs);
            TelemetryJson.AppendNumber(json, "standardDeviationMs", summary.StandardDeviationMs);
            TelemetryJson.AppendInteger(json, "withinBudgetCount", summary.WithinBudgetCount);
            TelemetryJson.AppendInteger(json, "overBudgetCount", summary.OverBudgetCount);
            TelemetryJson.AppendNumber(json, "withinBudgetRatio", summary.WithinBudgetRatio);
            TelemetryJson.AppendInteger(json, "longestOverBudgetRun", summary.LongestOverBudgetRun);
            TelemetryJson.AppendInteger(json, "pacingMissCount", summary.PacingMissCount);
            TelemetryJson.AppendInteger(json, "hitchCount", summary.HitchCount);
            TelemetryJson.AppendInteger(json, "severeHitchCount", summary.SevereHitchCount);
            json.Append('}');
        }

        private static void AppendHitch(StringBuilder json, TelemetryHitchEvent hitch)
        {
            json.Append('{');
            TelemetryJson.AppendInteger(json, "sequence", hitch.Sequence, true);
            TelemetryJson.AppendNumber(json, "elapsedSeconds", hitch.ElapsedSeconds);
            TelemetryJson.AppendNumber(json, "durationMs", hitch.DurationMs);
            TelemetryJson.AppendString(
                json, "severity", hitch.Severity.ToString().ToLowerInvariant());
            json.Append('}');
        }

        private static void AppendMetricSummary(StringBuilder json, TelemetryMetricSummary summary)
        {
            TelemetryDistribution distribution = summary.Distribution;
            json.Append('{');
            TelemetryJson.AppendString(json, "unit", summary.Unit, true);
            TelemetryJson.AppendString(json, "percentileMethod", distribution.Method);
            TelemetryJson.AppendInteger(json, "sampleCount", distribution.SampleCount);
            TelemetryJson.AppendNumber(json, "minimum", distribution.Minimum);
            TelemetryJson.AppendNumber(json, "p50", distribution.P50);
            TelemetryJson.AppendNumber(json, "p90", distribution.P90);
            TelemetryJson.AppendNumber(json, "p95", distribution.P95);
            TelemetryJson.AppendNumber(json, "p99", distribution.P99);
            TelemetryJson.AppendNumber(json, "maximum", distribution.Maximum);
            json.Append('}');
        }

        private static void AppendRawSample(StringBuilder json, GoldenSceneRawFrameSample sample)
        {
            GoldenSceneFrameObservation observation = sample.Observation;
            json.Append('{');
            TelemetryJson.AppendInteger(json, "sequence", observation.Sequence, true);
            TelemetryJson.AppendNumber(json, "elapsedSeconds", observation.ElapsedSeconds);
            TelemetryJson.AppendString(
                json, "interval", sample.Interval.ToString().ToLowerInvariant());
            TelemetryJson.AppendNumber(
                json, "deliveredFrameTimeMs", observation.DeliveredFrameTimeMs);
            TelemetryJson.AppendNullableNumber(
                json, "cpuFrameTimeMs", observation.CpuFrameTimeMs);
            TelemetryJson.AppendNullableNumber(
                json, "gpuFrameTimeMs", observation.GpuFrameTimeMs);
            TelemetryJson.AppendNullableInteger(
                json,
                "frameTimingFrameStartTimestampTicks",
                observation.FrameTimingFrameStartTimestampTicks);
            TelemetryJson.AppendNullableInteger(
                json,
                "cpuTimerFrequency",
                observation.CpuTimerFrequency);
            TelemetryJson.Prefix(json, "counters");
            json.Append('{');
            bool firstCounter = true;
            foreach (KeyValuePair<string, double?> counter in observation.Counters)
            {
                TelemetryJson.AppendNullableNumber(json, counter.Key, counter.Value, firstCounter);
                firstCounter = false;
            }
            json.Append('}');
            json.Append('}');
        }
    }

    public static class GoldenSceneTelemetryMath
    {
        public static FramePacingSummary CalculateFramePacing(
            IReadOnlyList<double> deliveredFrameTimesMs,
            int targetFrameRate)
        {
            if (deliveredFrameTimesMs == null)
                throw new ArgumentNullException(nameof(deliveredFrameTimesMs));
            if (deliveredFrameTimesMs.Count == 0)
                throw new ArgumentException("At least one sample is required.", nameof(deliveredFrameTimesMs));
            if (targetFrameRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate));

            double targetFrameTimeMs = 1000d / targetFrameRate;
            double sum = 0d;
            int withinBudget = 0;
            int overBudget = 0;
            int currentOverBudgetRun = 0;
            int longestOverBudgetRun = 0;
            int pacingMisses = 0;
            int hitches = 0;
            int severeHitches = 0;

            for (int index = 0; index < deliveredFrameTimesMs.Count; index++)
            {
                double value = deliveredFrameTimesMs[index];
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                    throw new ArgumentException(
                        "Frame times must be finite and non-negative.",
                        nameof(deliveredFrameTimesMs));

                sum += value;
                if (value <= targetFrameTimeMs)
                {
                    withinBudget++;
                    currentOverBudgetRun = 0;
                }
                else
                {
                    overBudget++;
                    currentOverBudgetRun++;
                    longestOverBudgetRun = Math.Max(longestOverBudgetRun, currentOverBudgetRun);
                }

                switch (ClassifyHitch(value, targetFrameRate))
                {
                    case TelemetryHitchSeverity.PacingMiss: pacingMisses++; break;
                    case TelemetryHitchSeverity.Hitch: hitches++; break;
                    case TelemetryHitchSeverity.SevereHitch: severeHitches++; break;
                }
            }

            double average = sum / deliveredFrameTimesMs.Count;
            double squaredDeviationSum = 0d;
            for (int index = 0; index < deliveredFrameTimesMs.Count; index++)
            {
                double deviation = deliveredFrameTimesMs[index] - average;
                squaredDeviationSum += deviation * deviation;
            }

            return new FramePacingSummary(
                deliveredFrameTimesMs.Count,
                targetFrameRate,
                targetFrameTimeMs,
                average,
                Math.Sqrt(squaredDeviationSum / deliveredFrameTimesMs.Count),
                withinBudget,
                overBudget,
                longestOverBudgetRun,
                pacingMisses,
                hitches,
                severeHitches);
        }

        public static TelemetryHitchSeverity ClassifyHitch(
            double deliveredFrameTimeMs,
            int targetFrameRate)
        {
            if (double.IsNaN(deliveredFrameTimeMs) ||
                double.IsInfinity(deliveredFrameTimeMs) ||
                deliveredFrameTimeMs < 0d)
                throw new ArgumentOutOfRangeException(nameof(deliveredFrameTimeMs));
            if (targetFrameRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetFrameRate));

            if (deliveredFrameTimeMs >= 250d)
                return TelemetryHitchSeverity.SevereHitch;
            if (deliveredFrameTimeMs >= 100d)
                return TelemetryHitchSeverity.Hitch;

            double pacingMissThresholdMs = (1000d / targetFrameRate) * 1.5d;
            return deliveredFrameTimeMs >= pacingMissThresholdMs
                ? TelemetryHitchSeverity.PacingMiss
                : TelemetryHitchSeverity.None;
        }

        public static TelemetryDistribution CalculateDistribution(
            IReadOnlyList<double> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (samples.Count == 0)
                throw new ArgumentException("At least one sample is required.", nameof(samples));

            var sorted = new double[samples.Count];
            for (int index = 0; index < samples.Count; index++)
            {
                double value = samples[index];
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                    throw new ArgumentException("Samples must be finite and non-negative.", nameof(samples));
                sorted[index] = value;
            }

            Array.Sort(sorted);
            return new TelemetryDistribution(
                sorted.Length,
                sorted[0],
                NearestRank(sorted, 0.50d),
                NearestRank(sorted, 0.90d),
                NearestRank(sorted, 0.95d),
                NearestRank(sorted, 0.99d),
                sorted[sorted.Length - 1]);
        }

        private static double NearestRank(double[] sorted, double percentile)
        {
            int rank = (int)Math.Ceiling(percentile * sorted.Length);
            int index = Math.Max(0, Math.Min(sorted.Length - 1, rank - 1));
            return sorted[index];
        }
    }

    internal static class TelemetryJson
    {
        public static void AppendString(
            StringBuilder json,
            string name,
            string value,
            bool first = false)
        {
            Prefix(json, name, first);
            json.Append('"');
            Escape(json, value ?? string.Empty);
            json.Append('"');
        }

        public static void AppendInteger(
            StringBuilder json,
            string name,
            long value,
            bool first = false)
        {
            Prefix(json, name, first);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public static void AppendBoolean(
            StringBuilder json,
            string name,
            bool value,
            bool first = false)
        {
            Prefix(json, name, first);
            json.Append(value ? "true" : "false");
        }

        public static void AppendNumber(
            StringBuilder json,
            string name,
            double value,
            bool first = false)
        {
            Prefix(json, name, first);
            json.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        public static void AppendNullableNumber(
            StringBuilder json,
            string name,
            double? value,
            bool first = false)
        {
            Prefix(json, name, first);
            if (value.HasValue)
                json.Append(value.Value.ToString("R", CultureInfo.InvariantCulture));
            else
                json.Append("null");
        }

        public static void AppendNullableInteger(
            StringBuilder json,
            string name,
            ulong? value,
            bool first = false)
        {
            Prefix(json, name, first);
            if (value.HasValue)
                json.Append(value.Value.ToString(CultureInfo.InvariantCulture));
            else
                json.Append("null");
        }

        public static void Prefix(
            StringBuilder json,
            string name,
            bool first = false)
        {
            if (!first) json.Append(',');
            json.Append('"');
            Escape(json, name);
            json.Append("\":");
        }

        public static void Escape(StringBuilder json, string value)
        {
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\b': json.Append("\\b"); break;
                    case '\f': json.Append("\\f"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            json.Append("\\u")
                                .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            json.Append(character);
                        }
                        break;
                }
            }
        }
    }
}
