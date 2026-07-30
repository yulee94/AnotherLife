using System;
using System.Collections.Generic;
using System.Linq;
using AL.RealmWar.Territories.Runtime;

namespace AL.Terrestrials.Slagfall
{
    public enum SlagfallEvidenceLane
    {
        MobileLow = 0,
        MobileStandard = 1,
        DesktopLow = 2,
        DesktopStandard = 3
    }

    public static class SlagfallEvidenceContract
    {
        public const string SchemaVersion =
            "slagfall-device-evidence-v001";
        public const float MinimumRunSeconds = 30f * 60f;
        public const float EvidenceWindowSeconds = 5f * 60f;
        public const int RequiredRepresentedUsers =
            TerritoryLoadDegradationPlanner.SafeRepresentedUserCapacity;

        public static string StableId(SlagfallEvidenceLane lane)
        {
            switch (lane)
            {
                case SlagfallEvidenceLane.MobileLow:
                    return "mobile_low";
                case SlagfallEvidenceLane.MobileStandard:
                    return "mobile_standard";
                case SlagfallEvidenceLane.DesktopLow:
                    return "desktop_low";
                case SlagfallEvidenceLane.DesktopStandard:
                    return "desktop_standard";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(lane),
                        lane,
                        "Unknown Slagfall evidence lane.");
            }
        }

        public static float TargetFrameTimeMilliseconds(
            SlagfallEvidenceLane lane)
        {
            switch (lane)
            {
                case SlagfallEvidenceLane.MobileLow:
                    return 1000f / 30f;
                case SlagfallEvidenceLane.MobileStandard:
                    return 1000f / 45f;
                case SlagfallEvidenceLane.DesktopLow:
                case SlagfallEvidenceLane.DesktopStandard:
                    return 1000f / 60f;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(lane),
                        lane,
                        "Unknown Slagfall evidence lane.");
            }
        }

        public static int TargetFrameRate(SlagfallEvidenceLane lane)
        {
            switch (lane)
            {
                case SlagfallEvidenceLane.MobileLow:
                    return 30;
                case SlagfallEvidenceLane.MobileStandard:
                    return 45;
                case SlagfallEvidenceLane.DesktopLow:
                case SlagfallEvidenceLane.DesktopStandard:
                    return 60;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(lane),
                        lane,
                        "Unknown Slagfall evidence lane.");
            }
        }
    }

    [Serializable]
    public sealed class SlagfallMetricSummary
    {
        public bool available;
        public int sampleCount;
        public double p50;
        public double p95;
        public double worst;
        public string unavailableReason;

        public static SlagfallMetricSummary Unavailable(string reason)
        {
            return new SlagfallMetricSummary
            {
                available = false,
                sampleCount = 0,
                unavailableReason = reason ?? "not_available"
            };
        }

        public static SlagfallMetricSummary From(
            IEnumerable<double> samples)
        {
            if (samples == null)
            {
                return Unavailable("samples_missing");
            }

            double[] ordered = samples
                .Where(
                    value =>
                        !double.IsNaN(value) &&
                        !double.IsInfinity(value) &&
                        value >= 0d)
                .OrderBy(value => value)
                .ToArray();
            if (ordered.Length == 0)
            {
                return Unavailable("samples_empty");
            }

            return new SlagfallMetricSummary
            {
                available = true,
                sampleCount = ordered.Length,
                p50 = Percentile(ordered, 0.50d),
                p95 = Percentile(ordered, 0.95d),
                worst = ordered[ordered.Length - 1],
                unavailableReason = string.Empty
            };
        }

        private static double Percentile(
            IReadOnlyList<double> ordered,
            double percentile)
        {
            if (ordered.Count == 1)
            {
                return ordered[0];
            }

            double position = (ordered.Count - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            if (lower == upper)
            {
                return ordered[lower];
            }

            double fraction = position - lower;
            return ordered[lower] +
                (ordered[upper] - ordered[lower]) * fraction;
        }
    }

    [Serializable]
    public sealed class SlagfallDeviceEvidenceReport
    {
        public string schemaVersion;
        public string runId;
        public string sourceVersion;
        public string evidenceLane;
        public string startedUtc;
        public string lastCheckpointUtc;
        public string completedUtc;
        public bool completed;
        public bool productionScoringEligible;
        public string[] productionScoringBlockers = Array.Empty<string>();

        public string productVersion;
        public string unityVersion;
        public string platform;
        public string operatingSystem;
        public string deviceModel;
        public string processorType;
        public int processorCount;
        public string graphicsDeviceName;
        public string graphicsDeviceType;
        public string graphicsDeviceVersion;
        public int graphicsMemoryMegabytes;
        public int systemMemoryMegabytes;
        public string resolution;
        public int qualityLevel;
        public string qualityName;
        public int targetFrameRate;
        public double targetFrameTimeMilliseconds;

        public double intendedDurationSeconds;
        public double observedDurationSeconds;
        public int registeredUserCount;
        public int minimumRepresentedUserCount;
        public string initialLoadLevel;
        public string finalLoadLevel;
        public bool effectsOff;
        public bool reducedMotion;

        public SlagfallMetricSummary cpuFrameMilliseconds;
        public SlagfallMetricSummary gpuFrameMilliseconds;
        public SlagfallMetricSummary firstFiveMinuteCpuMilliseconds;
        public SlagfallMetricSummary firstFiveMinuteGpuMilliseconds;
        public SlagfallMetricSummary finalFiveMinuteCpuMilliseconds;
        public SlagfallMetricSummary finalFiveMinuteGpuMilliseconds;

        public SlagfallMetricSummary totalAllocatedMemoryBytes;
        public SlagfallMetricSummary totalReservedMemoryBytes;
        public SlagfallMetricSummary graphicsDriverMemoryBytes;
        public SlagfallMetricSummary loadedTextureMemoryBytes;
        public SlagfallMetricSummary loadedMeshMemoryBytes;
        public SlagfallMetricSummary loadedAnimationMemoryBytes;
        public SlagfallMetricSummary activeRendererCount;
        public SlagfallMetricSummary activeMaterialCount;
        public SlagfallMetricSummary activeTriangleCount;
        public SlagfallMetricSummary drawCallCount;
        public SlagfallMetricSummary batchCount;
        public SlagfallMetricSummary setPassCallCount;
        public SlagfallMetricSummary shadowCasterCount;
        public SlagfallMetricSummary particleSystemCount;

        public double coldReadySeconds;
        public bool optionalTierCancellationPassed;
        public double optionalTierCancellationSeconds;
        public bool exitReleasePlateauPassed;
        public double exitReleaseSeconds;
        public string streamingEvidenceBoundary;
        public string overdrawEvidenceBoundary;
        public string instanceBufferEvidenceBoundary;

        public float batteryLevelAtStart;
        public float batteryLevelAtEnd;
        public string batteryStatusAtStart;
        public string batteryStatusAtEnd;
        public string thermalStateAtStart;
        public string thermalStateAtEnd;
        public string thermalEvidenceSource;
        public string externalGpuCaptureId;
        public string externalThermalCaptureId;
        public string externalCrashAnrCaptureId;
        public string externalBuildSizeEvidenceId;
        public string externalOverdrawCaptureId;
        public string externalResidencyCaptureId;
        public int lowMemoryEventCount;
        public int focusLossCount;
        public int applicationPauseCount;
        public int severeLogCount;
        public string completionMarker;
    }

    public sealed class SlagfallEvidenceAccumulator
    {
        private readonly List<double> _cpu = new List<double>();
        private readonly List<double> _gpu = new List<double>();
        private readonly List<double> _firstCpu = new List<double>();
        private readonly List<double> _firstGpu = new List<double>();
        private readonly List<double> _finalCpu = new List<double>();
        private readonly List<double> _finalGpu = new List<double>();
        private readonly Dictionary<string, List<double>> _counters =
            new Dictionary<string, List<double>>(StringComparer.Ordinal);
        private readonly double _intendedDurationSeconds;

        public SlagfallEvidenceAccumulator(double intendedDurationSeconds)
        {
            if (double.IsNaN(intendedDurationSeconds) ||
                double.IsInfinity(intendedDurationSeconds) ||
                intendedDurationSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intendedDurationSeconds));
            }

            _intendedDurationSeconds = intendedDurationSeconds;
        }

        public void AddFrame(
            double elapsedSeconds,
            double cpuMilliseconds,
            double gpuMilliseconds)
        {
            AddFinite(_cpu, cpuMilliseconds);
            if (gpuMilliseconds > 0d)
            {
                AddFinite(_gpu, gpuMilliseconds);
            }

            if (elapsedSeconds <=
                SlagfallEvidenceContract.EvidenceWindowSeconds)
            {
                AddFinite(_firstCpu, cpuMilliseconds);
                if (gpuMilliseconds > 0d)
                {
                    AddFinite(_firstGpu, gpuMilliseconds);
                }
            }

            double finalWindowStart = Math.Max(
                0d,
                _intendedDurationSeconds -
                    SlagfallEvidenceContract.EvidenceWindowSeconds);
            if (elapsedSeconds >= finalWindowStart)
            {
                AddFinite(_finalCpu, cpuMilliseconds);
                if (gpuMilliseconds > 0d)
                {
                    AddFinite(_finalGpu, gpuMilliseconds);
                }
            }
        }

        public void AddCounter(string id, double value)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Counter ID is required.",
                    nameof(id));
            }

            if (!_counters.TryGetValue(
                id,
                out List<double> samples))
            {
                samples = new List<double>();
                _counters.Add(id, samples);
            }

            AddFinite(samples, value);
        }

        public SlagfallMetricSummary Cpu() =>
            SlagfallMetricSummary.From(_cpu);

        public SlagfallMetricSummary Gpu() =>
            _gpu.Count > 0
                ? SlagfallMetricSummary.From(_gpu)
                : SlagfallMetricSummary.Unavailable(
                    "gpu_frame_timing_not_reported_by_device");

        public SlagfallMetricSummary FirstCpu() =>
            SlagfallMetricSummary.From(_firstCpu);

        public SlagfallMetricSummary FirstGpu() =>
            _firstGpu.Count > 0
                ? SlagfallMetricSummary.From(_firstGpu)
                : SlagfallMetricSummary.Unavailable(
                    "gpu_frame_timing_not_reported_by_device");

        public SlagfallMetricSummary FinalCpu() =>
            SlagfallMetricSummary.From(_finalCpu);

        public SlagfallMetricSummary FinalGpu() =>
            _finalGpu.Count > 0
                ? SlagfallMetricSummary.From(_finalGpu)
                : SlagfallMetricSummary.Unavailable(
                    "gpu_frame_timing_not_reported_by_device");

        public SlagfallMetricSummary Counter(
            string id,
            string unavailableReason)
        {
            return _counters.TryGetValue(
                    id,
                    out List<double> samples) &&
                samples.Count > 0
                ? SlagfallMetricSummary.From(samples)
                : SlagfallMetricSummary.Unavailable(unavailableReason);
        }

        private static void AddFinite(
            ICollection<double> samples,
            double value)
        {
            if (!double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0d)
            {
                samples.Add(value);
            }
        }
    }

    public static class SlagfallDeviceEvidenceValidator
    {
        public static bool ValidateForProductionScoring(
            SlagfallDeviceEvidenceReport report,
            out string[] blockers)
        {
            var failures = new List<string>();
            if (report == null)
            {
                blockers = new[] { "report_missing" };
                return false;
            }

            Require(
                failures,
                report.schemaVersion ==
                    SlagfallEvidenceContract.SchemaVersion,
                "schema_version_mismatch");
            Require(
                failures,
                report.sourceVersion ==
                    SlagfallSourceAuthority.SourceVersion,
                "approved_source_version_mismatch");
            Require(failures, report.completed, "run_not_completed");
            Require(
                failures,
                report.observedDurationSeconds >=
                    SlagfallEvidenceContract.MinimumRunSeconds,
                "run_shorter_than_30_minutes");
            Require(
                failures,
                report.registeredUserCount ==
                    SlagfallEvidenceContract.RequiredRepresentedUsers,
                "registered_user_count_not_100");
            Require(
                failures,
                report.minimumRepresentedUserCount ==
                    SlagfallEvidenceContract.RequiredRepresentedUsers,
                "registered_user_became_unrepresented");
            Require(
                failures,
                report.cpuFrameMilliseconds?.available == true,
                "cpu_frame_evidence_missing");
            Require(
                failures,
                report.gpuFrameMilliseconds?.available == true,
                "gpu_frame_evidence_missing");
            Require(
                failures,
                report.firstFiveMinuteCpuMilliseconds?.available == true,
                "first_five_minute_cpu_evidence_missing");
            Require(
                failures,
                report.firstFiveMinuteGpuMilliseconds?.available == true,
                "first_five_minute_gpu_evidence_missing");
            Require(
                failures,
                report.finalFiveMinuteCpuMilliseconds?.available == true,
                "final_five_minute_cpu_evidence_missing");
            Require(
                failures,
                report.finalFiveMinuteGpuMilliseconds?.available == true,
                "final_five_minute_gpu_evidence_missing");
            Require(
                failures,
                report.totalAllocatedMemoryBytes?.available == true,
                "resident_memory_evidence_missing");
            Require(
                failures,
                report.totalReservedMemoryBytes?.available == true,
                "reserved_memory_evidence_missing");
            Require(
                failures,
                report.loadedTextureMemoryBytes?.available == true,
                "texture_residency_evidence_missing");
            Require(
                failures,
                report.loadedMeshMemoryBytes?.available == true,
                "mesh_residency_evidence_missing");
            Require(
                failures,
                report.loadedAnimationMemoryBytes?.available == true,
                "animation_residency_evidence_missing");
            Require(
                failures,
                report.activeRendererCount?.available == true,
                "renderer_evidence_missing");
            Require(
                failures,
                report.activeTriangleCount?.available == true,
                "triangle_evidence_missing");
            Require(
                failures,
                report.drawCallCount?.available == true &&
                    report.drawCallCount.worst > 0d,
                "draw_call_evidence_missing");
            Require(
                failures,
                report.batchCount?.available == true &&
                    report.batchCount.worst > 0d,
                "batch_evidence_missing");
            Require(
                failures,
                report.setPassCallCount?.available == true &&
                    report.setPassCallCount.worst > 0d,
                "set_pass_evidence_missing");
            Require(
                failures,
                report.coldReadySeconds > 0d,
                "cold_ready_timing_missing");
            Require(
                failures,
                report.optionalTierCancellationPassed,
                "optional_tier_cancellation_failed");
            Require(
                failures,
                report.exitReleasePlateauPassed,
                "exit_release_plateau_failed");
            Require(
                failures,
                report.lowMemoryEventCount == 0,
                "low_memory_event_observed");
            Require(
                failures,
                report.severeLogCount == 0,
                "severe_log_observed");
            RequireReference(
                failures,
                report.externalGpuCaptureId,
                "external_gpu_capture_missing");
            RequireReference(
                failures,
                report.externalThermalCaptureId,
                "external_thermal_capture_missing");
            RequireReference(
                failures,
                report.externalCrashAnrCaptureId,
                "external_crash_anr_capture_missing");
            RequireReference(
                failures,
                report.externalBuildSizeEvidenceId,
                "external_build_size_evidence_missing");
            RequireReference(
                failures,
                report.externalOverdrawCaptureId,
                "external_overdraw_capture_missing");
            RequireReference(
                failures,
                report.externalResidencyCaptureId,
                "external_residency_capture_missing");
            Require(
                failures,
                string.Equals(
                    report.completionMarker,
                    "SLAGFALL_EVIDENCE_COMPLETE",
                    StringComparison.Ordinal),
                "completion_marker_missing");

            blockers = failures.ToArray();
            return blockers.Length == 0;
        }

        private static void Require(
            ICollection<string> failures,
            bool condition,
            string diagnostic)
        {
            if (!condition)
            {
                failures.Add(diagnostic);
            }
        }

        private static void RequireReference(
            ICollection<string> failures,
            string value,
            string diagnostic)
        {
            Require(
                failures,
                !string.IsNullOrWhiteSpace(value),
                diagnostic);
        }
    }
}
