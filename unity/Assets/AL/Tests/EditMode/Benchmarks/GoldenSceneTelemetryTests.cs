using AL.Benchmarks.GoldenScenes;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.Benchmarks
{
    public sealed class GoldenSceneTelemetryTests
    {
        [Serializable]
        private sealed class ReportProbe
        {
            public string schemaVersion;
            public string certificationStatus;
            public int warmupSampleCount;
            public int measuredSampleCount;
            public double actualDurationSeconds;
        }

        [Test]
        public void PlayerBuildsEnableFrameTimingStatistics()
        {
            Assert.That(PlayerSettings.enableFrameTimingStats, Is.True,
                "Golden-scene player evidence requires CPU/GPU FrameTimingManager samples.");
        }

        [Test]
        public void DelayedFrameTimingsAlignToOldestCapturedFrameAndPreserveRawTicks()
        {
            var alignment = new GoldenSceneFrameTimingAlignment(maxPendingFrames: 8);
            alignment.Enqueue(new GoldenSceneFrameObservation(
                10, 1d, 33d, null, null, null));
            alignment.Enqueue(new GoldenSceneFrameObservation(
                11, 1.033d, 34d, null, null, null));

            Assert.That(alignment.TryCompleteOldest(
                1234567890123456789UL,
                10000000UL,
                8d,
                9d,
                out GoldenSceneFrameObservation completed), Is.True);
            Assert.That(completed.Sequence, Is.EqualTo(10));
            Assert.That(completed.ElapsedSeconds, Is.EqualTo(1d));
            Assert.That(completed.FrameTimingFrameStartTimestampTicks,
                Is.EqualTo(1234567890123456789UL));
            Assert.That(completed.CpuTimerFrequency, Is.EqualTo(10000000UL));
            Assert.That(alignment.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void PublishedGaugeMetadataCannotDriftOrLeakAcrossCollectionWindows()
        {
            GoldenSceneTelemetryGaugeRegistry.BeginCollectionWindow();
            GoldenSceneTelemetryGaugeRegistry.Publish(
                GoldenSceneTelemetryMetricIds.FullActors,
                8d,
                "count",
                "actor-density-probe");
            Assert.That(GoldenSceneTelemetryGaugeRegistry.TryRead(
                GoldenSceneTelemetryMetricIds.FullActors,
                out double value,
                out string unit,
                out string source), Is.True);
            Assert.That(value, Is.EqualTo(8d));
            Assert.That(unit, Is.EqualTo("count"));
            Assert.That(source, Is.EqualTo("actor-density-probe"));
            Assert.That(
                () => GoldenSceneTelemetryGaugeRegistry.Publish(
                    GoldenSceneTelemetryMetricIds.FullActors,
                    9d,
                    "ratio",
                    "different-probe"),
                Throws.InvalidOperationException);
            GoldenSceneTelemetryGaugeRegistry.Remove(GoldenSceneTelemetryMetricIds.FullActors);
            Assert.That(
                () => GoldenSceneTelemetryGaugeRegistry.Publish(
                    GoldenSceneTelemetryMetricIds.FullActors,
                    9d,
                    "ratio",
                    "different-probe"),
                Throws.InvalidOperationException);

            GoldenSceneTelemetryGaugeRegistry.BeginCollectionWindow();
            Assert.That(GoldenSceneTelemetryGaugeRegistry.TryRead(
                GoldenSceneTelemetryMetricIds.FullActors,
                out _,
                out _,
                out _), Is.False);
        }

        [Test]
        public void ExpensiveCountersUseDeterministicOneSecondSamplingWindows()
        {
            double nextSampleAtSeconds = 0d;

            Assert.That(GoldenSceneTelemetrySampling.ShouldSample(
                0.1d, ref nextSampleAtSeconds, 1d), Is.True);
            Assert.That(nextSampleAtSeconds, Is.EqualTo(1d));
            Assert.That(GoldenSceneTelemetrySampling.ShouldSample(
                0.9d, ref nextSampleAtSeconds, 1d), Is.False);
            Assert.That(GoldenSceneTelemetrySampling.ShouldSample(
                2.2d, ref nextSampleAtSeconds, 1d), Is.True);
            Assert.That(nextSampleAtSeconds, Is.EqualTo(3d));
        }

        [Test]
        public void EmptyProfilerCounterStorageIsUnavailableInsteadOfZero()
        {
            Assert.That(GoldenSceneUnityTelemetrySource.NormalizeProfilerCounterSample(0, 0L),
                Is.Null);
            Assert.That(GoldenSceneUnityTelemetrySource.NormalizeProfilerCounterSample(1, 0L),
                Is.EqualTo(0d));
            Assert.That(GoldenSceneUnityTelemetrySource.NormalizeProfilerCounterSample(1, 42L),
                Is.EqualTo(42d));
        }

        [Test]
        public void PercentilesUseDeterministicNearestRankAndPreserveSampleCount()
        {
            TelemetryDistribution distribution = GoldenSceneTelemetryMath.CalculateDistribution(
                new[] { 40d, 10d, 50d, 20d, 30d });

            Assert.That(distribution.SampleCount, Is.EqualTo(5));
            Assert.That(distribution.Minimum, Is.EqualTo(10d));
            Assert.That(distribution.P50, Is.EqualTo(30d));
            Assert.That(distribution.P90, Is.EqualTo(50d));
            Assert.That(distribution.P95, Is.EqualTo(50d));
            Assert.That(distribution.P99, Is.EqualTo(50d));
            Assert.That(distribution.Maximum, Is.EqualTo(50d));
            Assert.That(distribution.Method, Is.EqualTo("nearest-rank"));
        }

        [Test]
        public void HitchClassificationSeparatesPacingMissesFromReportableHitches()
        {
            Assert.That(GoldenSceneTelemetryMath.ClassifyHitch(49.9d, 30),
                Is.EqualTo(TelemetryHitchSeverity.None));
            Assert.That(GoldenSceneTelemetryMath.ClassifyHitch(50d, 30),
                Is.EqualTo(TelemetryHitchSeverity.PacingMiss));
            Assert.That(GoldenSceneTelemetryMath.ClassifyHitch(99.99d, 30),
                Is.EqualTo(TelemetryHitchSeverity.PacingMiss));
            Assert.That(GoldenSceneTelemetryMath.ClassifyHitch(100d, 30),
                Is.EqualTo(TelemetryHitchSeverity.Hitch));
            Assert.That(GoldenSceneTelemetryMath.ClassifyHitch(249.99d, 30),
                Is.EqualTo(TelemetryHitchSeverity.Hitch));
            Assert.That(GoldenSceneTelemetryMath.ClassifyHitch(250d, 30),
                Is.EqualTo(TelemetryHitchSeverity.SevereHitch));
        }

        [Test]
        public void FramePacingSummaryReportsBudgetMissesAndConsecutiveRun()
        {
            FramePacingSummary summary = GoldenSceneTelemetryMath.CalculateFramePacing(
                new[] { 30d, 34d, 60d, 100d, 250d },
                30);

            Assert.That(summary.SampleCount, Is.EqualTo(5));
            Assert.That(summary.TargetFrameRate, Is.EqualTo(30));
            Assert.That(summary.TargetFrameTimeMs, Is.EqualTo(1000d / 30d).Within(0.0001d));
            Assert.That(summary.WithinBudgetCount, Is.EqualTo(1));
            Assert.That(summary.OverBudgetCount, Is.EqualTo(4));
            Assert.That(summary.LongestOverBudgetRun, Is.EqualTo(4));
            Assert.That(summary.PacingMissCount, Is.EqualTo(1));
            Assert.That(summary.HitchCount, Is.EqualTo(1));
            Assert.That(summary.SevereHitchCount, Is.EqualTo(1));
            Assert.That(summary.WithinBudgetRatio, Is.EqualTo(0.2d));
            Assert.That(summary.StandardDeviationMs, Is.GreaterThan(0d));
        }

        [Test]
        public void RuntimeCollectorRejectsEarlyFinishWithoutDiscardingActiveSession()
        {
            var gameObject = new GameObject("golden-scene-telemetry-test");
            var collector = gameObject.AddComponent<GoldenSceneRuntimeTelemetryCollector>();
            try
            {
                collector.StartCollection(new GoldenSceneTelemetryConfiguration(30, 0d, 10d));

                Assert.That(
                    () => collector.FinishCollection(),
                    Throws.InvalidOperationException.With.Message.Contains("configured duration"));
                Assert.That(collector.IsCollecting, Is.True);
            }
            finally
            {
                collector.CancelCollection();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CompletionRejectsRunsThatEndBeforeConfiguredDuration()
        {
            var session = new GoldenSceneTelemetrySession(
                new GoldenSceneTelemetryConfiguration(30, 1d, 2d),
                "2026-08-31T02:00:00.0000000Z",
                true,
                new GoldenSceneDeviceSnapshot(null, string.Empty, null, string.Empty));
            session.RecordFrame(new GoldenSceneFrameObservation(
                0, 1d, 33d, null, null, null));

            Assert.That(
                () => session.Complete(
                    "2026-08-31T02:00:03.0000000Z",
                    new GoldenSceneDeviceSnapshot(null, string.Empty, null, string.Empty)),
                Throws.InvalidOperationException.With.Message.Contains("configured duration"));
        }

        [Test]
        public void UnsupportedCapabilityIsExplicitAndNeverSerializedAsZero()
        {
            TelemetryCapability capability = TelemetryCapability.Unsupported(
                "device.temperature",
                "celsius",
                "platform-api",
                "No temperature API is available on this platform.");

            Assert.That(capability.Status, Is.EqualTo(TelemetryCapabilityStatus.Unsupported));
            Assert.That(capability.SampleCount, Is.Zero);
            string json = capability.ToJson();
            Assert.That(json, Does.Contain("\"status\":\"unsupported\""));
            Assert.That(json, Does.Contain("\"sampleCount\":0"));
            Assert.That(json, Does.Contain("\"reason\":\"No temperature API is available on this platform.\""));
            Assert.That(json, Does.Not.Contain("\"value\":0"));
        }

        [Test]
        public void SessionPreservesWarmupAndMeasuredRawSamplesAndSerializesAggregates()
        {
            var configuration = new GoldenSceneTelemetryConfiguration(30, 1d, 2d);
            var startDevice = new GoldenSceneDeviceSnapshot(
                0.80d, "discharging", null, "nominal");
            var session = new GoldenSceneTelemetrySession(
                configuration,
                "2026-08-31T03:00:00.0000000Z",
                true,
                startDevice);
            session.SetCapability(TelemetryCapability.Supported(
                GoldenSceneTelemetryMetricIds.CpuFrameTime,
                "milliseconds",
                "frame-timing-manager"));
            session.SetCapability(TelemetryCapability.Supported(
                GoldenSceneTelemetryMetricIds.GpuFrameTime,
                "milliseconds",
                "frame-timing-manager"));
            session.SetCapability(TelemetryCapability.Supported(
                GoldenSceneTelemetryMetricIds.UnityUsedMemory,
                "bytes",
                "unity-profiler"));
            session.SetCapability(TelemetryCapability.Supported(
                GoldenSceneTelemetryMetricIds.BatteryLevel,
                "ratio",
                "system-info",
                sampleScope: "run-device-snapshots"));
            session.SetCapability(TelemetryCapability.Unsupported(
                GoldenSceneTelemetryMetricIds.DeviceTemperature,
                "celsius",
                "platform-api",
                "No temperature API is available."));

            session.RecordFrame(new GoldenSceneFrameObservation(
                0, 0.5d, 30d, 8d, 9d,
                new Dictionary<string, double?>
                {
                    [GoldenSceneTelemetryMetricIds.UnityUsedMemory] = 100d
                }));
            session.RecordFrame(new GoldenSceneFrameObservation(
                1, 1.0d, 34d, 10d, 11d,
                new Dictionary<string, double?>
                {
                    [GoldenSceneTelemetryMetricIds.UnityUsedMemory] = 120d
                },
                frameTimingFrameStartTimestampTicks: 1235UL,
                cpuTimerFrequency: 10UL));
            session.RecordFrame(new GoldenSceneFrameObservation(
                2, 3.0d, 100d, 20d, 21d,
                new Dictionary<string, double?>
                {
                    [GoldenSceneTelemetryMetricIds.UnityUsedMemory] = 160d
                }));
            session.RecordDeviceSample(
                3d,
                new GoldenSceneDeviceSnapshot(0.76d, "discharging", null, "nominal"));

            GoldenSceneTelemetryReport report = session.Complete(
                "2026-08-31T03:00:03.0000000Z",
                new GoldenSceneDeviceSnapshot(0.75d, "discharging", null, "nominal"));

            Assert.That(report.WarmupSampleCount, Is.EqualTo(1));
            Assert.That(report.MeasuredSampleCount, Is.EqualTo(2));
            Assert.That(report.RawSamples.Count, Is.EqualTo(3));
            Assert.That(report.DeviceSamples.Count, Is.EqualTo(2));
            Assert.That(report.IsTargetPlatformCertificationEligible, Is.False);
            Assert.That(report.BatteryDelta, Is.EqualTo(-0.05d).Within(0.0001d));
            Assert.That(report.MetricSummaries[GoldenSceneTelemetryMetricIds.CpuFrameTime].Distribution.P50,
                Is.EqualTo(10d));
            Assert.That(report.MetricSummaries[GoldenSceneTelemetryMetricIds.UnityUsedMemory].Distribution.Maximum,
                Is.EqualTo(160d));
            Assert.That(report.FramePacing.HitchCount, Is.EqualTo(1));
            Assert.That(report.Hitches.Count, Is.EqualTo(1));
            Assert.That(report.Hitches[0].Severity, Is.EqualTo(TelemetryHitchSeverity.Hitch));
            Assert.That(report.Hitches[0].Sequence, Is.EqualTo(2));
            Assert.That(report.ActualDurationSeconds, Is.EqualTo(3d));
            TelemetryCapability batteryCapability = null;
            foreach (TelemetryCapability capability in report.Capabilities)
            {
                if (capability.MetricId == GoldenSceneTelemetryMetricIds.BatteryLevel)
                    batteryCapability = capability;
            }
            Assert.That(batteryCapability, Is.Not.Null);
            Assert.That(batteryCapability.SampleCount, Is.EqualTo(2));
            Assert.That(batteryCapability.SampleScope, Is.EqualTo("run-device-snapshots"));

            string json = report.ToJson();
            Assert.That(json, Does.Contain(
                "\"certificationStatus\":\"player-build-telemetry-awaiting-validated-benchmark-identity\""));
            Assert.That(json, Does.Contain("\"warmupSampleCount\":1"));
            Assert.That(json, Does.Contain("\"measuredSampleCount\":2"));
            Assert.That(json, Does.Contain("\"interval\":\"warmup\""));
            Assert.That(json, Does.Contain("\"interval\":\"measured\""));
            Assert.That(json, Does.Contain("\"frameTimingFrameStartTimestampTicks\":1235"));
            Assert.That(json, Does.Contain("\"cpuTimerFrequency\":10"));
            Assert.That(json, Does.Contain("\"deviceSamples\":["));
            Assert.That(json, Does.Contain("\"hitches\":["));
            Assert.That(json, Does.Contain("\"metricId\":\"device.temperature\""));
            Assert.That(json, Does.Contain("\"status\":\"unsupported\""));
            Assert.That(json, Does.Contain("\"batteryDelta\":-0.05"));
            Assert.That(json, Does.Contain("\"sampleScope\":\"run-device-snapshots\""));
            ReportProbe parsed = JsonUtility.FromJson<ReportProbe>(json);
            Assert.That(parsed.schemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(parsed.certificationStatus,
                Is.EqualTo("player-build-telemetry-awaiting-validated-benchmark-identity"));
            Assert.That(parsed.warmupSampleCount, Is.EqualTo(1));
            Assert.That(parsed.measuredSampleCount, Is.EqualTo(2));
            Assert.That(parsed.actualDurationSeconds, Is.EqualTo(3d));
        }

        [Test]
        public void EditorSessionIsExplicitlyDevelopmentOnlyAndCannotCertify()
        {
            var session = new GoldenSceneTelemetrySession(
                new GoldenSceneTelemetryConfiguration(60, 0d, 1d),
                "2026-08-31T04:00:00.0000000Z",
                false,
                new GoldenSceneDeviceSnapshot(null, string.Empty, null, string.Empty));
            session.RecordFrame(new GoldenSceneFrameObservation(
                0, 1d, 16d, null, null, null));

            GoldenSceneTelemetryReport report = session.Complete(
                "2026-08-31T04:00:01.0000000Z",
                new GoldenSceneDeviceSnapshot(null, string.Empty, null, string.Empty));

            Assert.That(report.IsTargetPlatformCertificationEligible, Is.False);
            Assert.That(report.CertificationStatus,
                Is.EqualTo("editor-development-only-not-certifying"));
            Assert.That(report.ToJson(), Does.Contain(
                "\"isTargetPlatformCertificationEligible\":false"));
        }
    }
}
