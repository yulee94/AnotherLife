using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace AL.Benchmarks.GoldenScenes
{
    public static class GoldenSceneTelemetryGaugeRegistry
    {
        private sealed class Gauge
        {
            public double Value;
            public string Unit;
            public string Source;
            public bool Active;
        }

        private static readonly Dictionary<string, Gauge> Gauges =
            new Dictionary<string, Gauge>(StringComparer.Ordinal);

        public static void Publish(string metricId, double value, string unit, string source)
        {
            if (string.IsNullOrWhiteSpace(metricId))
                throw new ArgumentException("Metric ID is required.", nameof(metricId));
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("Unit is required.", nameof(unit));
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source is required.", nameof(source));

            if (Gauges.TryGetValue(metricId, out Gauge existing))
            {
                if (!string.Equals(existing.Unit, unit, StringComparison.Ordinal) ||
                    !string.Equals(existing.Source, source, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Gauge unit and source cannot change during a collection window.");
                }
                existing.Value = value;
                existing.Active = true;
                return;
            }

            Gauges.Add(metricId, new Gauge
            {
                Value = value,
                Unit = unit,
                Source = source,
                Active = true
            });
        }

        public static void Remove(string metricId)
        {
            if (!string.IsNullOrWhiteSpace(metricId) &&
                Gauges.TryGetValue(metricId, out Gauge gauge))
                gauge.Active = false;
        }

        internal static void BeginCollectionWindow()
        {
            Gauges.Clear();
        }

        internal static bool TryRead(
            string metricId,
            out double value,
            out string unit,
            out string source)
        {
            if (Gauges.TryGetValue(metricId, out Gauge gauge) && gauge.Active)
            {
                value = gauge.Value;
                unit = gauge.Unit;
                source = gauge.Source;
                return true;
            }

            value = 0d;
            unit = string.Empty;
            source = string.Empty;
            return false;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GoldenSceneRuntimeTelemetryCollector : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float warmupSeconds = 10f;
        [SerializeField, Min(0.01f)] private float measurementSeconds = 60f;
        [SerializeField] private bool startOnEnable;

        private GoldenSceneUnityTelemetrySource source;
        private GoldenSceneTelemetrySession session;
        private double startedAtRealtime;
        private double nextCounterSampleAtSeconds;
        private double nextDeviceSampleAtSeconds;
        private int sequence;
        private bool measurementWindowClosed;

        public event Action<GoldenSceneTelemetryReport> ReportReady;

        public bool IsCollecting => session != null;
        public GoldenSceneTelemetryReport LatestReport { get; private set; }

        private void OnEnable()
        {
            if (startOnEnable) StartCollection();
        }

        private void OnDisable()
        {
            if (session != null) CancelCollection();
        }

        private void LateUpdate()
        {
            if (session == null) return;

            double elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAtRealtime;
            source.CaptureFrameTiming();
            if (!measurementWindowClosed)
            {
                bool includeCounters = GoldenSceneTelemetrySampling.ShouldSample(
                    elapsedSeconds,
                    ref nextCounterSampleAtSeconds,
                    1d);
                source.EnqueueFrame(
                    sequence++,
                    elapsedSeconds,
                    Time.unscaledDeltaTime * 1000d,
                    includeCounters);
                if (elapsedSeconds >= nextDeviceSampleAtSeconds)
                {
                    session.RecordDeviceSample(elapsedSeconds, source.CaptureDeviceSnapshot());
                    do
                    {
                        nextDeviceSampleAtSeconds += 60d;
                    }
                    while (nextDeviceSampleAtSeconds <= elapsedSeconds);
                }
                measurementWindowClosed = elapsedSeconds >= session.Configuration.TotalSeconds;
            }

            if (source.TryResolveOldestFrameTiming(
                    measurementWindowClosed,
                    out GoldenSceneFrameObservation observation))
                session.RecordFrame(observation);

            if (measurementWindowClosed && source.PendingFrameCount == 0)
                FinishCollection();
        }

        public void StartCollection()
        {
            StartCollection(new GoldenSceneTelemetryConfiguration(
                Application.targetFrameRate > 0 ? Application.targetFrameRate : 30,
                warmupSeconds,
                measurementSeconds));
        }

        public void StartCollection(GoldenSceneTelemetryConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (session != null) throw new InvalidOperationException("Telemetry collection is already active.");

            source = new GoldenSceneUnityTelemetrySource();
            source.Start();
            session = new GoldenSceneTelemetrySession(
                configuration,
                UtcNow(),
                !Application.isEditor,
                source.CaptureDeviceSnapshot());
            source.ApplyCapabilities(session, false);
            startedAtRealtime = Time.realtimeSinceStartupAsDouble;
            nextCounterSampleAtSeconds = 0d;
            nextDeviceSampleAtSeconds = 60d;
            sequence = 0;
            measurementWindowClosed = false;
            LatestReport = null;
        }

        public GoldenSceneTelemetryReport FinishCollection()
        {
            if (session == null) throw new InvalidOperationException("Telemetry collection is not active.");
            double elapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAtRealtime;
            if (elapsedSeconds < session.Configuration.TotalSeconds)
            {
                throw new InvalidOperationException(
                    "Telemetry collection cannot finish before the configured duration.");
            }

            GoldenSceneTelemetrySession completingSession = session;
            GoldenSceneUnityTelemetrySource completingSource = source;
            GoldenSceneDeviceSnapshot endDevice = completingSource.CaptureDeviceSnapshot();
            completingSource.ApplyCapabilities(completingSession, true);
            GoldenSceneTelemetryReport report = completingSession.Complete(
                UtcNow(),
                endDevice);

            session = null;
            source = null;
            completingSource.Dispose();
            LatestReport = report;

            ReportReady?.Invoke(LatestReport);
            return LatestReport;
        }

        public void CancelCollection()
        {
            if (session == null) return;
            session = null;
            source?.Dispose();
            source = null;
        }

        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }
    }

    internal static class GoldenSceneTelemetrySampling
    {
        public static bool ShouldSample(
            double elapsedSeconds,
            ref double nextSampleAtSeconds,
            double intervalSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (double.IsNaN(nextSampleAtSeconds) ||
                double.IsInfinity(nextSampleAtSeconds) ||
                nextSampleAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(nextSampleAtSeconds));
            if (double.IsNaN(intervalSeconds) ||
                double.IsInfinity(intervalSeconds) ||
                intervalSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            if (elapsedSeconds < nextSampleAtSeconds) return false;

            do
            {
                nextSampleAtSeconds += intervalSeconds;
            }
            while (nextSampleAtSeconds <= elapsedSeconds);
            return true;
        }
    }

    internal sealed class GoldenSceneUnityTelemetrySource : IDisposable
    {
        private sealed class Counter : IDisposable
        {
            public Counter(
                string metricId,
                string unit,
                ProfilerCategory category,
                string profilerName)
            {
                MetricId = metricId;
                Unit = unit;
                ProfilerName = profilerName;
                try
                {
                    Recorder = ProfilerRecorder.StartNew(category, profilerName, 1);
                    IsSupported = Recorder.Valid;
                    FailureReason = IsSupported
                        ? string.Empty
                        : "Unity profiler counter is not exposed by this player/platform.";
                }
                catch (Exception exception)
                {
                    IsSupported = false;
                    HasError = true;
                    FailureReason = exception.GetType().Name + ": " + exception.Message;
                }
            }

            public string MetricId { get; }
            public string Unit { get; }
            public string ProfilerName { get; }
            public ProfilerRecorder Recorder { get; private set; }
            public bool IsSupported { get; }
            public bool HasError { get; private set; }
            public string FailureReason { get; }
            public bool ObservedSample { get; private set; }

            public double? Read()
            {
                if (!IsSupported) return null;
                double? value = NormalizeProfilerCounterSample(Recorder.Count, Recorder.LastValue);
                if (value.HasValue) ObservedSample = true;
                return value;
            }

            public void Dispose()
            {
                if (Recorder.Valid) Recorder.Dispose();
            }
        }

        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private static readonly string[] GaugeMetricIds =
        {
            GoldenSceneTelemetryMetricIds.TextureStreamingRequests,
            GoldenSceneTelemetryMetricIds.TextureStreamingBytes,
            GoldenSceneTelemetryMetricIds.AssetStreamingStalls,
            GoldenSceneTelemetryMetricIds.ShaderCompilationEvents,
            GoldenSceneTelemetryMetricIds.LodGroups,
            GoldenSceneTelemetryMetricIds.LodTransitions,
            GoldenSceneTelemetryMetricIds.VfxSources,
            GoldenSceneTelemetryMetricIds.ParticleCount,
            GoldenSceneTelemetryMetricIds.FullActors,
            GoldenSceneTelemetryMetricIds.FallbackActors,
            GoldenSceneTelemetryMetricIds.NameplateActors
        };
        private readonly List<Counter> counters = new List<Counter>();
        private readonly SortedDictionary<string, TelemetryCapability> capabilities =
            new SortedDictionary<string, TelemetryCapability>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> frameMetricIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly GoldenSceneFrameTimingAlignment frameTimingAlignment =
            new GoldenSceneFrameTimingAlignment(8);
        private string[] frameMetricIds = Array.Empty<string>();
        private ulong cpuTimerFrequency;
        private ulong lastFrameTimingTimestamp;
        private bool hasFrameTimingTimestamp;
        private bool cpuTimingObserved;
        private bool gpuTimingObserved;
        private bool started;

        public void Start()
        {
            if (started) throw new InvalidOperationException("Telemetry source is already started.");
            started = true;
            GoldenSceneTelemetryGaugeRegistry.BeginCollectionWindow();

            DeclareUnsupportedMetrics();
            capabilities[GoldenSceneTelemetryMetricIds.UnityUsedMemory] =
                Supported(GoldenSceneTelemetryMetricIds.UnityUsedMemory, "bytes", "UnityEngine.Profiling.Profiler");
            capabilities[GoldenSceneTelemetryMetricIds.UnityReservedMemory] =
                Supported(GoldenSceneTelemetryMetricIds.UnityReservedMemory, "bytes", "UnityEngine.Profiling.Profiler");
            capabilities[GoldenSceneTelemetryMetricIds.ManagedHeapUsed] =
                Supported(GoldenSceneTelemetryMetricIds.ManagedHeapUsed, "bytes", "UnityEngine.Profiling.Profiler");
            capabilities[GoldenSceneTelemetryMetricIds.ManagedHeapReserved] =
                Supported(GoldenSceneTelemetryMetricIds.ManagedHeapReserved, "bytes", "UnityEngine.Profiling.Profiler");
            capabilities[GoldenSceneTelemetryMetricIds.NativeUsedMemory] =
                Supported(GoldenSceneTelemetryMetricIds.NativeUsedMemory, "bytes", "unity-total-minus-managed-derived");
            capabilities[GoldenSceneTelemetryMetricIds.RenderScale] =
                Supported(GoldenSceneTelemetryMetricIds.RenderScale, "ratio", "UnityEngine.ScalableBufferManager");
            capabilities[GoldenSceneTelemetryMetricIds.LodBias] =
                Supported(GoldenSceneTelemetryMetricIds.LodBias, "ratio", "UnityEngine.QualitySettings");
            capabilities[GoldenSceneTelemetryMetricIds.VfxDensity] =
                Supported(GoldenSceneTelemetryMetricIds.VfxDensity, "ratio", "GoldenSceneRuntimeSetup");

            AddCounter(GoldenSceneTelemetryMetricIds.SystemUsedMemory, "bytes", ProfilerCategory.Memory, "System Used Memory");
            AddCounter(GoldenSceneTelemetryMetricIds.GraphicsUsedMemory, "bytes", ProfilerCategory.Memory, "Gfx Used Memory");
            AddCounter(GoldenSceneTelemetryMetricIds.ManagedAllocatedInFrame, "bytes", ProfilerCategory.Memory, "GC Allocated In Frame");
            AddCounter(GoldenSceneTelemetryMetricIds.NativeAllocationCount, "count", ProfilerCategory.Memory, "Native Allocations Count");
            AddCounter(GoldenSceneTelemetryMetricIds.GarbageCollectionCount, "count", ProfilerCategory.Memory, "GC Collection Count");
            AddCounter(GoldenSceneTelemetryMetricIds.DrawCalls, "count", ProfilerCategory.Render, "Draw Calls Count");
            AddCounter(GoldenSceneTelemetryMetricIds.Batches, "count", ProfilerCategory.Render, "Batches Count");
            AddCounter(GoldenSceneTelemetryMetricIds.Triangles, "count", ProfilerCategory.Render, "Triangles Count");
            AddCounter(GoldenSceneTelemetryMetricIds.Vertices, "count", ProfilerCategory.Render, "Vertices Count");
            AddCounter(GoldenSceneTelemetryMetricIds.ActiveRenderers, "count", ProfilerCategory.Render, "Visible Renderers");
            AddCounter(GoldenSceneTelemetryMetricIds.TextureStreamingRequests, "count", ProfilerCategory.Render, "Texture Streaming Requests");
            AddCounter(GoldenSceneTelemetryMetricIds.TextureStreamingBytes, "bytes", ProfilerCategory.Render, "Texture Streaming Memory");
            AddCounter(GoldenSceneTelemetryMetricIds.AssetStreamingStalls, "nanoseconds", ProfilerCategory.Loading, "Async Upload Time");
            AddCounter(GoldenSceneTelemetryMetricIds.ShaderCompilationEvents, "count", ProfilerCategory.Render, "Shader Compilations");
            AddCounter(GoldenSceneTelemetryMetricIds.LodGroups, "count", ProfilerCategory.Render, "LOD Group Count");
            AddCounter(GoldenSceneTelemetryMetricIds.LodTransitions, "count", ProfilerCategory.Render, "LOD Transitions");
            AddCounter(GoldenSceneTelemetryMetricIds.VfxSources, "count", ProfilerCategory.Particles, "Particle System Count");
            AddCounter(GoldenSceneTelemetryMetricIds.ParticleCount, "count", ProfilerCategory.Particles, "Particle Count");
            BuildFrameMetricLayout();
            cpuTimerFrequency = FrameTimingManager.GetCpuTimerFrequency();
            if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0)
            {
                lastFrameTimingTimestamp = frameTimings[0].frameStartTimestamp;
                hasFrameTimingTimestamp = true;
            }
        }

        public void CaptureFrameTiming()
        {
            FrameTimingManager.CaptureFrameTimings();
        }

        internal static double? NormalizeProfilerCounterSample(int sampleCount, long value)
        {
            return sampleCount > 0 && value >= 0 ? value : (double?)null;
        }

        public int PendingFrameCount => frameTimingAlignment.PendingCount;

        public void EnqueueFrame(
            int sequence,
            double elapsedSeconds,
            double deliveredFrameTimeMs,
            bool includeCounters)
        {
            string[] sampleMetricIds = includeCounters ? frameMetricIds : Array.Empty<string>();
            double?[] values = includeCounters
                ? new double?[frameMetricIds.Length]
                : Array.Empty<double?>();
            if (includeCounters)
            {
                foreach (Counter counter in counters)
                    SetValue(values, counter.MetricId, counter.Read());

                double totalUsed = Profiler.GetTotalAllocatedMemoryLong();
                double managedUsed = Profiler.GetMonoUsedSizeLong();
                SetValue(values, GoldenSceneTelemetryMetricIds.UnityUsedMemory, totalUsed);
                SetValue(values, GoldenSceneTelemetryMetricIds.UnityReservedMemory, Profiler.GetTotalReservedMemoryLong());
                SetValue(values, GoldenSceneTelemetryMetricIds.ManagedHeapUsed, managedUsed);
                SetValue(values, GoldenSceneTelemetryMetricIds.ManagedHeapReserved, Profiler.GetMonoHeapSizeLong());
                SetValue(values, GoldenSceneTelemetryMetricIds.NativeUsedMemory, Math.Max(0d, totalUsed - managedUsed));
                SetValue(values, GoldenSceneTelemetryMetricIds.RenderScale, ScalableBufferManager.widthScaleFactor);
                SetValue(values, GoldenSceneTelemetryMetricIds.LodBias, QualitySettings.lodBias);
                SetValue(values, GoldenSceneTelemetryMetricIds.VfxDensity, GoldenSceneRuntimeSetup.AppliedVfxDensity);
                foreach (string metricId in GaugeMetricIds)
                {
                    if (!GoldenSceneTelemetryGaugeRegistry.TryRead(
                            metricId,
                            out double value,
                            out string unit,
                            out string source)) continue;
                    SetValue(values, metricId, value);
                    capabilities[metricId] = Supported(metricId, unit, source);
                }
            }

            frameTimingAlignment.Enqueue(new GoldenSceneFrameObservation(
                sequence,
                elapsedSeconds,
                deliveredFrameTimeMs,
                null,
                null,
                null,
                null,
                sampleMetricIds,
                values));
        }

        public bool TryResolveOldestFrameTiming(
            bool forceReleaseWithoutTiming,
            out GoldenSceneFrameObservation observation)
        {
            uint timingCount = FrameTimingManager.GetLatestTimings(1, frameTimings);
            if (timingCount > 0 && cpuTimerFrequency > 0UL)
            {
                FrameTiming timing = frameTimings[0];
                if (!hasFrameTimingTimestamp || timing.frameStartTimestamp != lastFrameTimingTimestamp)
                {
                    lastFrameTimingTimestamp = timing.frameStartTimestamp;
                    hasFrameTimingTimestamp = true;
                    double? cpuFrameTimeMs = FiniteNonNegative(timing.cpuFrameTime) &&
                                             timing.cpuFrameTime > 0d
                        ? timing.cpuFrameTime
                        : (double?)null;
                    double? gpuFrameTimeMs = FiniteNonNegative(timing.gpuFrameTime) &&
                                             timing.gpuFrameTime > 0d
                        ? timing.gpuFrameTime
                        : (double?)null;
                    cpuTimingObserved |= cpuFrameTimeMs.HasValue;
                    gpuTimingObserved |= gpuFrameTimeMs.HasValue;
                    return frameTimingAlignment.TryCompleteOldest(
                        timing.frameStartTimestamp,
                        cpuTimerFrequency,
                        cpuFrameTimeMs,
                        gpuFrameTimeMs,
                        out observation);
                }
            }

            if (forceReleaseWithoutTiming || frameTimingAlignment.IsAtCapacity)
                return frameTimingAlignment.TryReleaseOldest(out observation);
            observation = default;
            return false;
        }

        public GoldenSceneDeviceSnapshot CaptureDeviceSnapshot()
        {
            float battery = SystemInfo.batteryLevel;
            double? batteryLevel = battery >= 0f && battery <= 1f ? battery : (double?)null;
            string batteryStatus = batteryLevel.HasValue
                ? SystemInfo.batteryStatus.ToString().ToLowerInvariant()
                : string.Empty;
            if (batteryLevel.HasValue)
            {
                capabilities[GoldenSceneTelemetryMetricIds.BatteryLevel] = Supported(
                    GoldenSceneTelemetryMetricIds.BatteryLevel,
                    "ratio",
                    "UnityEngine.SystemInfo",
                    "run-device-snapshots");
            }

            double? temperature = null;
            string thermalState = string.Empty;
#if UNITY_ANDROID && !UNITY_EDITOR
            TryCollectAndroidThermals(out temperature, out thermalState);
            if (temperature.HasValue)
            {
                capabilities[GoldenSceneTelemetryMetricIds.DeviceTemperature] = Supported(
                    GoldenSceneTelemetryMetricIds.DeviceTemperature,
                    "celsius",
                    "Android ACTION_BATTERY_CHANGED",
                    "run-device-snapshots");
            }
            if (!string.IsNullOrWhiteSpace(thermalState))
            {
                capabilities[GoldenSceneTelemetryMetricIds.DeviceThermalState] = Supported(
                    GoldenSceneTelemetryMetricIds.DeviceThermalState,
                    "state",
                    "Android PowerManager",
                    "run-device-snapshots");
            }
#endif
            return new GoldenSceneDeviceSnapshot(
                batteryLevel,
                batteryStatus,
                temperature,
                thermalState);
        }

        public void ApplyCapabilities(GoldenSceneTelemetrySession session, bool final)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (cpuTimingObserved)
            {
                capabilities[GoldenSceneTelemetryMetricIds.CpuFrameTime] = Supported(
                    GoldenSceneTelemetryMetricIds.CpuFrameTime,
                    "milliseconds",
                    "UnityEngine.FrameTimingManager",
                    "measured-frames");
            }
            else if (final)
            {
                capabilities[GoldenSceneTelemetryMetricIds.CpuFrameTime] = TelemetryCapability.Unsupported(
                    GoldenSceneTelemetryMetricIds.CpuFrameTime,
                    "milliseconds",
                    "UnityEngine.FrameTimingManager",
                    "No CPU frame-timing samples were returned by this player/platform.",
                    "measured-frames");
            }

            if (gpuTimingObserved)
            {
                capabilities[GoldenSceneTelemetryMetricIds.GpuFrameTime] = Supported(
                    GoldenSceneTelemetryMetricIds.GpuFrameTime,
                    "milliseconds",
                    "UnityEngine.FrameTimingManager",
                    "measured-frames");
            }
            else if (final)
            {
                capabilities[GoldenSceneTelemetryMetricIds.GpuFrameTime] = TelemetryCapability.Unsupported(
                    GoldenSceneTelemetryMetricIds.GpuFrameTime,
                    "milliseconds",
                    "UnityEngine.FrameTimingManager",
                    "No GPU frame-timing samples were returned by this player/platform.",
                    "measured-frames");
            }

            foreach (TelemetryCapability capability in capabilities.Values)
                session.SetCapability(capability);
        }

        public void Dispose()
        {
            foreach (Counter counter in counters) counter.Dispose();
            counters.Clear();
        }

        private void AddCounter(
            string metricId,
            string unit,
            ProfilerCategory category,
            string profilerName)
        {
            var counter = new Counter(metricId, unit, category, profilerName);
            counters.Add(counter);
            if (counter.IsSupported)
            {
                capabilities[metricId] = Supported(
                    metricId, unit, "Unity.Profiling.ProfilerRecorder:" + profilerName);
            }
            else if (counter.HasError)
            {
                capabilities[metricId] = TelemetryCapability.Error(
                    metricId,
                    unit,
                    "Unity.Profiling.ProfilerRecorder:" + profilerName,
                    counter.FailureReason,
                    "measured-1s-snapshots");
            }
            else
            {
                capabilities[metricId] = TelemetryCapability.Unsupported(
                    metricId,
                    unit,
                    "Unity.Profiling.ProfilerRecorder:" + profilerName,
                    counter.FailureReason,
                    "measured-1s-snapshots");
            }
        }

        private void BuildFrameMetricLayout()
        {
            var metricIds = new List<string>();
            foreach (string metricId in capabilities.Keys)
            {
                if (metricId == GoldenSceneTelemetryMetricIds.CpuFrameTime ||
                    metricId == GoldenSceneTelemetryMetricIds.GpuFrameTime ||
                    metricId == GoldenSceneTelemetryMetricIds.BatteryLevel ||
                    metricId == GoldenSceneTelemetryMetricIds.DeviceTemperature ||
                    metricId == GoldenSceneTelemetryMetricIds.DeviceThermalState)
                    continue;
                metricIds.Add(metricId);
            }

            metricIds.Sort(StringComparer.Ordinal);
            frameMetricIds = metricIds.ToArray();
            frameMetricIndices.Clear();
            for (int index = 0; index < frameMetricIds.Length; index++)
                frameMetricIndices.Add(frameMetricIds[index], index);
        }

        private void SetValue(double?[] values, string metricId, double? value)
        {
            if (frameMetricIndices.TryGetValue(metricId, out int index))
                values[index] = value;
        }

        private void DeclareUnsupportedMetrics()
        {
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.CpuFrameTime, "milliseconds", "Awaiting player timing samples.", "measured-frames");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.GpuFrameTime, "milliseconds", "Awaiting player timing samples.", "measured-frames");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.SystemUsedMemory, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.UnityUsedMemory, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.UnityReservedMemory, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.GraphicsUsedMemory, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.ManagedHeapUsed, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.ManagedHeapReserved, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.ManagedAllocatedInFrame, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.NativeAllocationCount, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.NativeUsedMemory, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.GarbageCollectionCount, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.DrawCalls, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.Batches, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.Triangles, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.Vertices, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.ActiveRenderers, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.TextureStreamingRequests, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.TextureStreamingBytes, "bytes", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.AssetStreamingStalls, "nanoseconds", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.ShaderCompilationEvents, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.LodGroups, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.LodTransitions, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.VfxSources, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.ParticleCount, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.FullActors, "count", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.FallbackActors, "count", "No generic fallback-actor counter exists; scene instrumentation may replace this capability.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.NameplateActors, "count", "No generic nameplate-actor counter exists; scene instrumentation may replace this capability.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.DeviceTemperature, "celsius", "Platform temperature API is unavailable.", "run-device-snapshots");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.DeviceThermalState, "state", "Platform thermal-state API is unavailable.", "run-device-snapshots");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.BatteryLevel, "ratio", "Battery API is unavailable.", "run-device-snapshots");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.RenderScale, "ratio", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.LodBias, "ratio", "Counter unavailable.");
            DeclareUnsupported(GoldenSceneTelemetryMetricIds.VfxDensity, "ratio", "Counter unavailable.");
        }

        private void DeclareUnsupported(
            string metricId,
            string unit,
            string reason,
            string sampleScope = "measured-1s-snapshots")
        {
            capabilities[metricId] = TelemetryCapability.Unsupported(
                metricId,
                unit,
                "golden-scene-runtime-telemetry",
                reason,
                sampleScope);
        }

        private static TelemetryCapability Supported(
            string metricId,
            string unit,
            string source,
            string sampleScope = "measured-1s-snapshots")
        {
            return TelemetryCapability.Supported(
                metricId, unit, source, sampleScope: sampleScope);
        }

        private static bool FiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void TryCollectAndroidThermals(
            out double? temperatureCelsius,
            out string thermalState)
        {
            temperatureCelsius = null;
            thermalState = string.Empty;
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var filter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("registerReceiver", null, filter))
                {
                    int tenthsCelsius = intent == null ? -1 : intent.Call<int>("getIntExtra", "temperature", -1);
                    if (tenthsCelsius >= 0) temperatureCelsius = tenthsCelsius / 10d;
                }

                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") < 29) return;
                }
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject power = activity.Call<AndroidJavaObject>("getSystemService", "power"))
                {
                    thermalState = ThermalStateName(power.Call<int>("getCurrentThermalStatus"));
                }
            }
            catch (Exception)
            {
                temperatureCelsius = null;
                thermalState = string.Empty;
            }
        }

        private static string ThermalStateName(int value)
        {
            switch (value)
            {
                case 0: return "none";
                case 1: return "light";
                case 2: return "moderate";
                case 3: return "severe";
                case 4: return "critical";
                case 5: return "emergency";
                case 6: return "shutdown";
                default: return "unknown-" + value.ToString(CultureInfo.InvariantCulture);
            }
        }
#endif
    }
}
