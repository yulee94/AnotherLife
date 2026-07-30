using System;
using System.Linq;
using AL.RealmWar.Territories.Runtime;
using AL.Terrestrials.Slagfall;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Terrestrials
{
    public sealed class SlagfallDeviceEvidenceTests
    {
        [TestCase(
            SlagfallEvidenceLane.MobileLow,
            "mobile_low",
            30)]
        [TestCase(
            SlagfallEvidenceLane.MobileStandard,
            "mobile_standard",
            45)]
        [TestCase(
            SlagfallEvidenceLane.DesktopLow,
            "desktop_low",
            60)]
        [TestCase(
            SlagfallEvidenceLane.DesktopStandard,
            "desktop_standard",
            60)]
        public void EvidenceLanesHaveStableIdsAndFrameBudgets(
            SlagfallEvidenceLane lane,
            string expectedId,
            int expectedFrameRate)
        {
            Assert.AreEqual(
                expectedId,
                SlagfallEvidenceContract.StableId(lane));
            Assert.AreEqual(
                expectedFrameRate,
                SlagfallEvidenceContract.TargetFrameRate(lane));
            Assert.AreEqual(
                1000f / expectedFrameRate,
                SlagfallEvidenceContract
                    .TargetFrameTimeMilliseconds(lane),
                0.001f);
        }

        [Test]
        public void MetricSummaryCalculatesDeterministicPercentiles()
        {
            SlagfallMetricSummary summary =
                SlagfallMetricSummary.From(
                    new[] { 5d, 1d, 4d, 2d, 3d });

            Assert.IsTrue(summary.available);
            Assert.AreEqual(5, summary.sampleCount);
            Assert.AreEqual(3d, summary.p50, 0.0001d);
            Assert.AreEqual(4.8d, summary.p95, 0.0001d);
            Assert.AreEqual(5d, summary.worst, 0.0001d);
        }

        [Test]
        public void AccumulatorSeparatesFirstAndFinalFiveMinutes()
        {
            var accumulator = new SlagfallEvidenceAccumulator(
                SlagfallEvidenceContract.MinimumRunSeconds);
            accumulator.AddFrame(1d, 10d, 11d);
            accumulator.AddFrame(400d, 20d, 21d);
            accumulator.AddFrame(1500d, 30d, 31d);
            accumulator.AddFrame(1799d, 40d, 41d);

            Assert.AreEqual(4, accumulator.Cpu().sampleCount);
            Assert.AreEqual(1, accumulator.FirstCpu().sampleCount);
            Assert.AreEqual(2, accumulator.FinalCpu().sampleCount);
            Assert.AreEqual(35d, accumulator.FinalCpu().p50, 0.0001d);
            Assert.AreEqual(36d, accumulator.FinalGpu().p50, 0.0001d);
        }

        [Test]
        public void IncompleteRunCannotBecomeProductionScoringEvidence()
        {
            SlagfallDeviceEvidenceReport report =
                CreateCompleteReport();
            report.completed = false;
            report.completionMarker =
                "SLAGFALL_EVIDENCE_INCOMPLETE";
            report.observedDurationSeconds = 1200d;

            Assert.IsFalse(
                SlagfallDeviceEvidenceValidator
                    .ValidateForProductionScoring(
                        report,
                        out string[] blockers));
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "run_not_completed",
                    "run_shorter_than_30_minutes",
                    "completion_marker_missing"
                },
                blockers);
        }

        [Test]
        public void CompleteThirtyMinuteRunCanEnterProductionScoring()
        {
            SlagfallDeviceEvidenceReport report =
                CreateCompleteReport();

            Assert.IsTrue(
                SlagfallDeviceEvidenceValidator
                    .ValidateForProductionScoring(
                        report,
                        out string[] blockers),
                string.Join(", ", blockers));
            Assert.IsEmpty(blockers);
        }

        [Test]
        public void TelemetryWithoutExternalCapturesCannotEnterProductionScoring()
        {
            SlagfallDeviceEvidenceReport report =
                CreateCompleteReport();
            report.externalGpuCaptureId = string.Empty;
            report.externalThermalCaptureId = string.Empty;
            report.externalCrashAnrCaptureId = string.Empty;
            report.externalBuildSizeEvidenceId = string.Empty;
            report.externalOverdrawCaptureId = string.Empty;
            report.externalResidencyCaptureId = string.Empty;

            Assert.IsFalse(
                SlagfallDeviceEvidenceValidator
                    .ValidateForProductionScoring(
                        report,
                        out string[] blockers));
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "external_gpu_capture_missing",
                    "external_thermal_capture_missing",
                    "external_crash_anr_capture_missing",
                    "external_build_size_evidence_missing",
                    "external_overdraw_capture_missing",
                    "external_residency_capture_missing"
                },
                blockers);
        }

        [Test]
        public void MissingOpeningOrClosingWindowCannotEnterProductionScoring()
        {
            SlagfallDeviceEvidenceReport report =
                CreateCompleteReport();
            report.firstFiveMinuteGpuMilliseconds = null;
            report.finalFiveMinuteCpuMilliseconds = null;

            Assert.IsFalse(
                SlagfallDeviceEvidenceValidator
                    .ValidateForProductionScoring(
                        report,
                        out string[] blockers));
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "first_five_minute_gpu_evidence_missing",
                    "final_five_minute_cpu_evidence_missing"
                },
                blockers);
        }

        [Test]
        public void FrameBudgetCanChangeWithoutGlobalQualityMutation()
        {
            int qualityBefore = QualitySettings.GetQualityLevel();
            var root = new GameObject("FrameBudgetController");
            try
            {
                TerritoryLoadDegradationController controller =
                    root.AddComponent<
                        TerritoryLoadDegradationController>();
                controller.SetTargetFrameTimeMilliseconds(
                    1000f / 60f);

                Assert.AreEqual(
                    1000f / 60f,
                    controller.TargetFrameTimeMilliseconds,
                    0.001f);
                Assert.AreEqual(
                    qualityBefore,
                    QualitySettings.GetQualityLevel());
                Assert.Throws<ArgumentOutOfRangeException>(
                    () =>
                        controller
                            .SetTargetFrameTimeMilliseconds(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static SlagfallDeviceEvidenceReport
            CreateCompleteReport()
        {
            SlagfallMetricSummary metric =
                SlagfallMetricSummary.From(
                    Enumerable.Range(1, 10)
                        .Select(value => (double)value));
            return new SlagfallDeviceEvidenceReport
            {
                schemaVersion =
                    SlagfallEvidenceContract.SchemaVersion,
                sourceVersion =
                    SlagfallSourceAuthority.SourceVersion,
                completed = true,
                observedDurationSeconds =
                    SlagfallEvidenceContract.MinimumRunSeconds,
                registeredUserCount =
                    SlagfallEvidenceContract
                        .RequiredRepresentedUsers,
                minimumRepresentedUserCount =
                    SlagfallEvidenceContract
                        .RequiredRepresentedUsers,
                cpuFrameMilliseconds = metric,
                gpuFrameMilliseconds = metric,
                firstFiveMinuteCpuMilliseconds = metric,
                firstFiveMinuteGpuMilliseconds = metric,
                finalFiveMinuteCpuMilliseconds = metric,
                finalFiveMinuteGpuMilliseconds = metric,
                totalAllocatedMemoryBytes = metric,
                totalReservedMemoryBytes = metric,
                loadedTextureMemoryBytes = metric,
                loadedMeshMemoryBytes = metric,
                loadedAnimationMemoryBytes = metric,
                activeRendererCount = metric,
                activeTriangleCount = metric,
                drawCallCount = metric,
                batchCount = metric,
                setPassCallCount = metric,
                coldReadySeconds = 1d,
                optionalTierCancellationPassed = true,
                exitReleasePlateauPassed = true,
                lowMemoryEventCount = 0,
                severeLogCount = 0,
                externalGpuCaptureId = "gpu-capture-001",
                externalThermalCaptureId =
                    "thermal-capture-001",
                externalCrashAnrCaptureId =
                    "crash-anr-capture-001",
                externalBuildSizeEvidenceId =
                    "build-size-evidence-001",
                externalOverdrawCaptureId =
                    "overdraw-capture-001",
                externalResidencyCaptureId =
                    "residency-capture-001",
                completionMarker =
                    "SLAGFALL_EVIDENCE_COMPLETE"
            };
        }
    }
}
