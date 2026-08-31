using System;
using System.IO;
using System.Linq;
using AL.Benchmarks.GoldenScenes;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Benchmarks
{
    public sealed class GoldenSceneCaptureTests
    {
        private const string SourceManifestId =
            "al.postmvp.graphics_benchmark_sources.2026-08-25";

        [Test]
        public void ArtifactNamesEmbedStableSceneSeedAnchorAndRunIdentity()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "android_floor_30");

            Assert.That(
                GoldenSceneArtifactNaming.BuildDirectoryName(setup, "run-0001"),
                Is.EqualTo("scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001"));
            Assert.That(
                GoldenSceneArtifactNaming.BuildFileName(
                    setup,
                    "run-0001",
                    GoldenSceneArtifactKind.Still,
                    "png"),
                Is.EqualTo(
                    "scene-GS-03_seed-903031_anchor-boss_entry_run-run-0001_still.png"));
            Assert.That(
                () => GoldenSceneArtifactNaming.BuildFileName(
                    setup,
                    "../escape",
                    GoldenSceneArtifactKind.Manifest,
                    "json"),
                Throws.ArgumentException);
        }

        [Test]
        public void ManifestLinksEveryArtifactToTheExactRunConfiguration()
        {
            GoldenSceneSetup setup = Resolve("GS-05", "city_overview", "pc_high_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-0007");
            var media = new GoldenSceneCaptureMediaSettings(
                1920,
                1080,
                60,
                12d,
                GoldenSceneUiCaptureMode.RequiredByBenchmark,
                "PostMVP graphics benchmark section 8 GS-05");
            var artifact = GoldenSceneArtifactRecord.Captured(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Still,
                GoldenSceneArtifactNaming.BuildFileName(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Still,
                    "png"),
                "image/png",
                new string('a', 64),
                4096,
                "2026-08-31T03:00:01.0000000Z",
                "2026-08-31T03:00:02.0000000Z");

            Assert.That(GoldenSceneCaptureManifest.TryCreate(
                identity,
                setup,
                media,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:12.0000000Z",
                SourceManifestId,
                false,
                new GoldenSceneAnchorConsistency(1, 12, 0),
                new[] { artifact },
                out GoldenSceneCaptureManifest manifest,
                out string diagnostic), Is.True, diagnostic);

            Assert.That(manifest.Artifacts.Single().RunId, Is.EqualTo("run-0007"));
            Assert.That(manifest.Artifacts.Single().ConfigurationFingerprint,
                Is.EqualTo(setup.ConfigurationFingerprint));
            string json = manifest.ToJson();
            Assert.That(json, Does.Contain("\"runId\":\"run-0007\""));
            Assert.That(json, Does.Contain("\"sceneId\":\"GS-05\""));
            Assert.That(json, Does.Contain("\"seed\":905051"));
            Assert.That(json, Does.Contain("\"anchorId\":\"city_overview\""));
            Assert.That(json, Does.Contain(
                "\"sourceManifestId\":\"" + SourceManifestId + "\""));
            Assert.That(json, Does.Contain("\"thirdPartyMediaIncluded\":false"));
            Assert.That(json, Does.Contain("\"width\":1920"));
            Assert.That(json, Does.Contain("\"videoFrameRate\":60"));
        }

        [Test]
        public void ManifestCannotBeCompleteWhenRequiredArtifactKindsAreMissing()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-incomplete");
            var media = new GoldenSceneCaptureMediaSettings(
                1280,
                720,
                30,
                1d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            var still = GoldenSceneArtifactRecord.Captured(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Still,
                GoldenSceneArtifactNaming.BuildFileName(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Still,
                    "png"),
                "image/png",
                new string('a', 64),
                4096,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:01.0000000Z");

            Assert.That(GoldenSceneCaptureManifest.TryCreate(
                identity,
                setup,
                media,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:01.0000000Z",
                SourceManifestId,
                false,
                new GoldenSceneAnchorConsistency(1, 30, 0),
                new[] { still },
                out GoldenSceneCaptureManifest manifest,
                out string diagnostic), Is.True, diagnostic);

            Assert.That(manifest.HasAllRequiredArtifacts, Is.False);
            Assert.That(manifest.IsComplete, Is.False);
            Assert.That(diagnostic, Is.EqualTo("AL-GS-CAPTURE-MANIFEST-FAILURES-RECORDED"));
            Assert.That(manifest.ToJson(), Does.Contain("\"hasAllRequiredArtifacts\":false"));
        }

        [Test]
        public void VideoCompletenessRejectsFractionalDurationAndFrameShortfalls()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-exact-video");
            var media = new GoldenSceneCaptureMediaSettings(
                1280,
                720,
                24,
                1.00000001d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            GoldenSceneArtifactRecord[] artifacts =
            {
                CreateCapturedArtifact(setup, identity, GoldenSceneArtifactKind.Still),
                CreateCapturedArtifact(setup, identity, GoldenSceneArtifactKind.Video),
                CreateCapturedArtifact(setup, identity, GoldenSceneArtifactKind.Profiler),
                CreateCapturedArtifact(setup, identity, GoldenSceneArtifactKind.Telemetry)
            };

            Assert.That(GoldenSceneCaptureManifest.TryCreate(
                identity,
                setup,
                media,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:02.0000000Z",
                SourceManifestId,
                false,
                new GoldenSceneAnchorConsistency(1, 24, 0),
                artifacts,
                out GoldenSceneCaptureManifest manifest,
                out string diagnostic), Is.True, diagnostic);

            Assert.That(manifest.RequiredVideoFrameCount, Is.EqualTo(25));
            Assert.That(manifest.VideoFrameRequirementMet, Is.False);
            Assert.That(manifest.DurationRequirementMet, Is.False);
            Assert.That(manifest.IsComplete, Is.False);
        }

        [Test]
        public void AnchorVerifierDetectsDriftAndReapplicationRestoresExactSetup()
        {
            GoldenSceneSetup setup = Resolve("GS-02", "threshold_reveal", "balanced_60");
            var host = new GameObject("golden-scene-anchor-verifier");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                GoldenSceneCameraState.Apply(camera, setup);
                Assert.That(GoldenSceneCameraAnchorVerifier.Matches(camera, setup), Is.True);

                camera.transform.position += Vector3.right;
                Assert.That(GoldenSceneCameraAnchorVerifier.Matches(camera, setup), Is.False);

                GoldenSceneCameraState.Apply(camera, setup);
                Assert.That(GoldenSceneCameraAnchorVerifier.Matches(camera, setup), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnsupportedVideoAndProfilerFacilitiesRemainExplicitFailures()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "combat_readability", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-unsupported");
            var video = GoldenSceneArtifactRecord.Unavailable(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Video,
                GoldenSceneArtifactStatus.Unsupported,
                "video/mp4",
                "AL-GS-VIDEO-UNSUPPORTED",
                "No licensed runtime video encoder is installed.",
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:00.0000000Z");
            var profiler = GoldenSceneArtifactRecord.Unavailable(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Profiler,
                GoldenSceneArtifactStatus.Error,
                "application/vnd.unity.profiler",
                "AL-GS-PROFILER-START-FAILED",
                "Unity native profiler rejected binary logging.",
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:01.0000000Z");

            Assert.That(video.Status, Is.EqualTo(GoldenSceneArtifactStatus.Unsupported));
            Assert.That(video.RelativePath, Is.Empty);
            Assert.That(video.Sha256, Is.Empty);
            Assert.That(profiler.Status, Is.EqualTo(GoldenSceneArtifactStatus.Error));
            Assert.That(
                new GoldenSceneUnsupportedProfilerCaptureFacility("Unavailable.").Extension,
                Is.EqualTo("raw"));
            Assert.That(new GoldenSceneNativeProfilerCaptureFacility().Extension, Is.EqualTo("raw"));
            Assert.That(video.ToJson(), Does.Contain("\"status\":\"unsupported\""));
            Assert.That(video.ToJson(), Does.Contain("\"reason\":\"No licensed runtime video encoder is installed.\""));
            Assert.That(video.ToJson(), Does.Not.Contain("\"status\":\"captured\""));
            Assert.That(
                () => GoldenSceneArtifactRecord.Unavailable(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Video,
                    GoldenSceneArtifactStatus.Captured,
                    "video/mp4",
                    "AL-GS-INVALID",
                    "Must not claim capture.",
                    "2026-08-31T03:00:00.0000000Z",
                    "2026-08-31T03:00:00.0000000Z"),
                Throws.ArgumentException);
        }

        [Test]
        public void UiAndProvenancePolicyFailClosed()
        {
            GoldenSceneSetup capital = Resolve("GS-02", "distant_approach", "balanced_60");
            var uiSettings = new GoldenSceneCaptureMediaSettings(
                1280,
                720,
                30,
                5d,
                GoldenSceneUiCaptureMode.RequiredByBenchmark,
                "Unscoped UI request");

            Assert.That(GoldenSceneCapturePolicy.TryValidate(
                capital,
                uiSettings,
                SourceManifestId,
                false,
                out string uiDiagnostic), Is.False);
            Assert.That(uiDiagnostic, Is.EqualTo("AL-GS-CAPTURE-UI-NOT-REQUIRED:GS-02"));

            GoldenSceneSetup hud = Resolve("GS-04", "hud_combat", "balanced_60");
            Assert.That(GoldenSceneCapturePolicy.TryValidate(
                hud,
                uiSettings,
                SourceManifestId,
                false,
                out string validDiagnostic), Is.True, validDiagnostic);
            Assert.That(GoldenSceneCapturePolicy.TryValidate(
                hud,
                uiSettings,
                SourceManifestId,
                true,
                out string provenanceDiagnostic), Is.False);
            Assert.That(provenanceDiagnostic,
                Is.EqualTo("AL-GS-CAPTURE-THIRD-PARTY-MEDIA-FORBIDDEN"));
            Assert.That(
                () => new GoldenSceneCaptureMediaSettings(
                    1280,
                    720,
                    30,
                    5d,
                    (GoldenSceneUiCaptureMode)999,
                    string.Empty),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void RuntimeSessionCapturesSupportedFacilitiesFromTheSameAnchor()
        {
            string outputRoot = Path.Combine(
                Path.GetTempPath(),
                "al-gs-capture-" + Guid.NewGuid().ToString("N"));
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-runtime");
            var media = new GoldenSceneCaptureMediaSettings(
                640,
                360,
                30,
                2d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            var clock = new FakeCaptureClock("2026-08-31T03:00:00.0000000Z");
            var still = new FakeStillCaptureFacility();
            var video = new FakeVideoCaptureFacility(setup);
            var profiler = new FakeProfilerCaptureFacility();
            var host = new GameObject("golden-scene-runtime-capture-test");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                var session = new GoldenSceneRuntimeCaptureSession(
                    setup,
                    identity,
                    media,
                    outputRoot,
                    still,
                    video,
                    profiler,
                    clock);

                session.Begin(camera);
                camera.transform.position += Vector3.one * 5f;
                clock.AdvanceSeconds(1d);
                session.CaptureVideoFrame(camera);
                clock.AdvanceSeconds(1d);
                GoldenSceneCaptureManifest manifest = session.Complete(camera, null);

                Assert.That(still.AnchorMatched, Is.True);
                Assert.That(video.AnchorMatched, Is.True);
                Assert.That(manifest.AnchorConsistency.IsConsistent, Is.True);
                Assert.That(manifest.VideoFrameRequirementMet, Is.False);
                Assert.That(manifest.Artifacts.Count(artifact =>
                    artifact.Status == GoldenSceneArtifactStatus.Captured), Is.EqualTo(3));
                Assert.That(manifest.Artifacts.Single(artifact =>
                    artifact.Kind == GoldenSceneArtifactKind.Telemetry).Status,
                    Is.EqualTo(GoldenSceneArtifactStatus.Unsupported));
                Assert.That(File.Exists(session.ManifestPath), Is.True);
                Assert.That(File.ReadAllText(session.ManifestPath), Does.Contain(
                    "\"diagnosticCode\":\"AL-GS-TELEMETRY-NOT-PROVIDED\""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        [Test]
        public void RuntimeSessionRecordsUnsupportedFacilitiesInsteadOfClaimingFiles()
        {
            string outputRoot = Path.Combine(
                Path.GetTempPath(),
                "al-gs-capture-" + Guid.NewGuid().ToString("N"));
            GoldenSceneSetup setup = Resolve("GS-02", "distant_approach", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-no-facilities");
            var media = new GoldenSceneCaptureMediaSettings(
                640,
                360,
                30,
                1d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            var host = new GameObject("golden-scene-runtime-unsupported-test");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                var session = new GoldenSceneRuntimeCaptureSession(
                    setup,
                    identity,
                    media,
                    outputRoot,
                    new FakeStillCaptureFacility(),
                    new GoldenSceneUnsupportedVideoCaptureFacility(
                        "No licensed runtime video encoder is installed."),
                    new GoldenSceneUnsupportedProfilerCaptureFacility(
                        "Unity native profiler is unavailable."),
                    new FakeCaptureClock("2026-08-31T03:00:00.0000000Z"));

                session.Begin(camera);
                GoldenSceneCaptureManifest manifest = session.Complete(camera, null);

                GoldenSceneArtifactRecord video = manifest.Artifacts.Single(artifact =>
                    artifact.Kind == GoldenSceneArtifactKind.Video);
                GoldenSceneArtifactRecord profiler = manifest.Artifacts.Single(artifact =>
                    artifact.Kind == GoldenSceneArtifactKind.Profiler);
                Assert.That(video.Status, Is.EqualTo(GoldenSceneArtifactStatus.Unsupported));
                Assert.That(profiler.Status, Is.EqualTo(GoldenSceneArtifactStatus.Unsupported));
                Assert.That(video.RelativePath, Is.Empty);
                Assert.That(profiler.RelativePath, Is.Empty);
                Assert.That(manifest.IsComplete, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        [Test]
        public void RuntimeSessionUsesActualFinalizationTimesForTelemetryArtifacts()
        {
            string outputRoot = Path.Combine(
                Path.GetTempPath(),
                "al-gs-capture-" + Guid.NewGuid().ToString("N"));
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-finalization-time");
            var media = new GoldenSceneCaptureMediaSettings(
                640,
                360,
                30,
                1d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            var host = new GameObject("golden-scene-runtime-finalization-time-test");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                var session = new GoldenSceneRuntimeCaptureSession(
                    setup,
                    identity,
                    media,
                    outputRoot,
                    new FakeStillCaptureFacility(),
                    new FakeVideoCaptureFacility(setup),
                    new FakeProfilerCaptureFacility(),
                    new IncrementingCaptureClock(
                        "2026-08-31T03:00:00.0000000Z",
                        0.5d));

                session.Begin(camera);
                for (int frame = 0; frame < 30; frame++) session.CaptureVideoFrame(camera);
                GoldenSceneCaptureManifest manifest = session.Complete(
                    camera,
                    CreateTelemetryReport());

                Assert.That(manifest.Artifacts.Single(artifact =>
                    artifact.Kind == GoldenSceneArtifactKind.Telemetry).Status,
                    Is.EqualTo(GoldenSceneArtifactStatus.Captured));
                Assert.That(manifest.DurationRequirementMet, Is.True);
                Assert.That(manifest.VideoFrameRequirementMet, Is.True);
                Assert.That(manifest.IsComplete, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        [Test]
        public void RuntimeSessionExcludesAndRestoresUiForVideoCapture()
        {
            string outputRoot = Path.Combine(
                Path.GetTempPath(),
                "al-gs-capture-" + Guid.NewGuid().ToString("N"));
            GoldenSceneSetup setup = Resolve("GS-03", "combat_readability", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-video-ui");
            var media = new GoldenSceneCaptureMediaSettings(
                640,
                360,
                30,
                1d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            var clock = new FakeCaptureClock("2026-08-31T03:00:00.0000000Z");
            var host = new GameObject("golden-scene-runtime-video-ui-test");
            var uiHost = new GameObject("golden-scene-runtime-video-ui-canvas");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                Canvas canvas = uiHost.AddComponent<Canvas>();
                canvas.enabled = true;
                var video = new CanvasObservingVideoCaptureFacility(canvas);
                var session = new GoldenSceneRuntimeCaptureSession(
                    setup,
                    identity,
                    media,
                    outputRoot,
                    new FakeStillCaptureFacility(),
                    video,
                    new FakeProfilerCaptureFacility(),
                    clock);

                session.Begin(camera);
                session.CaptureVideoFrame(camera);
                clock.AdvanceSeconds(1d);
                session.Complete(camera, null);

                Assert.That(video.UiExcludedAtBegin, Is.True);
                Assert.That(video.UiExcludedDuringFrame, Is.True);
                Assert.That(canvas.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(uiHost);
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        [Test]
        public void RuntimeSessionRecordsThrownVideoFrameFailureInsteadOfAbortingManifest()
        {
            string outputRoot = Path.Combine(
                Path.GetTempPath(),
                "al-gs-capture-" + Guid.NewGuid().ToString("N"));
            GoldenSceneSetup setup = Resolve("GS-03", "combat_readability", "balanced_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-video-throw");
            var media = new GoldenSceneCaptureMediaSettings(
                640,
                360,
                30,
                1d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            var clock = new FakeCaptureClock("2026-08-31T03:00:00.0000000Z");
            var host = new GameObject("golden-scene-runtime-video-throw-test");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                var session = new GoldenSceneRuntimeCaptureSession(
                    setup,
                    identity,
                    media,
                    outputRoot,
                    new FakeStillCaptureFacility(),
                    new ThrowingFrameVideoCaptureFacility(),
                    new FakeProfilerCaptureFacility(),
                    clock);

                session.Begin(camera);
                session.CaptureVideoFrame(camera);
                clock.AdvanceSeconds(1d);
                GoldenSceneCaptureManifest manifest = session.Complete(
                    camera,
                    CreateTelemetryReport());

                GoldenSceneArtifactRecord video = manifest.Artifacts.Single(artifact =>
                    artifact.Kind == GoldenSceneArtifactKind.Video);
                Assert.That(video.Status, Is.EqualTo(GoldenSceneArtifactStatus.Error));
                Assert.That(video.DiagnosticCode, Is.EqualTo("AL-GS-VIDEO-FRAME-FAILED"));
                Assert.That(video.Reason, Does.StartWith("InvalidOperationException:"));
                Assert.That(File.Exists(session.ManifestPath), Is.True);
                Assert.That(manifest.IsComplete, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        [Test]
        public void PngStillFacilityRendersARealSameAnchorArtifact()
        {
            string outputRoot = Path.Combine(
                Path.GetTempPath(),
                "al-gs-still-" + Guid.NewGuid().ToString("N"));
            string outputPath = Path.Combine(outputRoot, "same-anchor.png");
            GoldenSceneSetup setup = Resolve("GS-01", "class_reveal", "balanced_60");
            var media = new GoldenSceneCaptureMediaSettings(
                64,
                64,
                30,
                1d,
                GoldenSceneUiCaptureMode.Excluded,
                string.Empty);
            var host = new GameObject("golden-scene-real-still-test");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.1f, 0.2f, 0.4f, 1f);
                camera.cullingMask = 0;
                var facility = new GoldenScenePngStillCaptureFacility();

                Assert.That(facility.TryCapture(
                    outputPath,
                    camera,
                    setup,
                    media,
                    out string failureReason), Is.True, failureReason);

                byte[] bytes = File.ReadAllBytes(outputPath);
                Assert.That(bytes.Length, Is.GreaterThan(8));
                Assert.That(bytes.Take(8), Is.EqualTo(new byte[]
                {
                    137, 80, 78, 71, 13, 10, 26, 10
                }));
                Assert.That(GoldenSceneCameraAnchorVerifier.Matches(camera, setup), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        private sealed class FakeCaptureClock : IGoldenSceneCaptureClock
        {
            private DateTimeOffset now;

            public FakeCaptureClock(string utc)
            {
                now = DateTimeOffset.Parse(utc);
            }

            public DateTimeOffset UtcNow => now;

            public void AdvanceSeconds(double seconds)
            {
                now = now.AddSeconds(seconds);
            }
        }

        private sealed class IncrementingCaptureClock : IGoldenSceneCaptureClock
        {
            private DateTimeOffset now;
            private readonly double incrementSeconds;

            public IncrementingCaptureClock(string utc, double incrementSeconds)
            {
                now = DateTimeOffset.Parse(utc);
                this.incrementSeconds = incrementSeconds;
            }

            public DateTimeOffset UtcNow
            {
                get
                {
                    DateTimeOffset current = now;
                    now = now.AddSeconds(incrementSeconds);
                    return current;
                }
            }
        }

        private sealed class FakeStillCaptureFacility : IGoldenSceneStillCaptureFacility
        {
            public bool AnchorMatched { get; private set; }

            public bool TryCapture(
                string outputPath,
                Camera camera,
                GoldenSceneSetup setup,
                GoldenSceneCaptureMediaSettings mediaSettings,
                out string failureReason)
            {
                AnchorMatched = GoldenSceneCameraAnchorVerifier.Matches(camera, setup);
                File.WriteAllBytes(outputPath, new byte[] { 1, 2, 3, 4 });
                failureReason = string.Empty;
                return true;
            }
        }

        private sealed class FakeVideoCaptureFacility : IGoldenSceneVideoCaptureFacility
        {
            private readonly GoldenSceneSetup setup;
            private string outputPath;

            public FakeVideoCaptureFacility(GoldenSceneSetup setup)
            {
                this.setup = setup;
            }

            public bool IsSupported => true;
            public string Format => "video/mp4";
            public string Extension => "mp4";
            public string UnsupportedReason => string.Empty;
            public bool AnchorMatched { get; private set; }

            public bool TryBegin(
                string path,
                Camera camera,
                GoldenSceneSetup captureSetup,
                GoldenSceneCaptureMediaSettings mediaSettings,
                out string failureReason)
            {
                outputPath = path;
                AnchorMatched = GoldenSceneCameraAnchorVerifier.Matches(camera, captureSetup);
                failureReason = string.Empty;
                return true;
            }

            public bool TryCaptureFrame(Camera camera, out string failureReason)
            {
                AnchorMatched &= GoldenSceneCameraAnchorVerifier.Matches(camera, setup);
                failureReason = string.Empty;
                return true;
            }

            public bool TryEnd(out string failureReason)
            {
                File.WriteAllBytes(outputPath, new byte[] { 5, 6, 7, 8 });
                failureReason = string.Empty;
                return true;
            }
        }

        private sealed class ThrowingFrameVideoCaptureFacility : IGoldenSceneVideoCaptureFacility
        {
            private string outputPath;

            public bool IsSupported => true;
            public string Format => "video/mp4";
            public string Extension => "mp4";
            public string UnsupportedReason => string.Empty;

            public bool TryBegin(
                string path,
                Camera camera,
                GoldenSceneSetup setup,
                GoldenSceneCaptureMediaSettings mediaSettings,
                out string failureReason)
            {
                outputPath = path;
                failureReason = string.Empty;
                return true;
            }

            public bool TryCaptureFrame(Camera camera, out string failureReason)
            {
                throw new InvalidOperationException("Synthetic encoder frame failure.");
            }

            public bool TryEnd(out string failureReason)
            {
                File.WriteAllBytes(outputPath, new byte[] { 5, 6, 7, 8 });
                failureReason = string.Empty;
                return true;
            }
        }

        private sealed class CanvasObservingVideoCaptureFacility : IGoldenSceneVideoCaptureFacility
        {
            private readonly Canvas canvas;
            private string outputPath;

            public CanvasObservingVideoCaptureFacility(Canvas canvas)
            {
                this.canvas = canvas;
            }

            public bool IsSupported => true;
            public string Format => "video/mp4";
            public string Extension => "mp4";
            public string UnsupportedReason => string.Empty;
            public bool UiExcludedAtBegin { get; private set; }
            public bool UiExcludedDuringFrame { get; private set; }

            public bool TryBegin(
                string path,
                Camera camera,
                GoldenSceneSetup setup,
                GoldenSceneCaptureMediaSettings mediaSettings,
                out string failureReason)
            {
                outputPath = path;
                UiExcludedAtBegin = !canvas.enabled;
                failureReason = string.Empty;
                return true;
            }

            public bool TryCaptureFrame(Camera camera, out string failureReason)
            {
                UiExcludedDuringFrame = !canvas.enabled;
                failureReason = string.Empty;
                return true;
            }

            public bool TryEnd(out string failureReason)
            {
                File.WriteAllBytes(outputPath, new byte[] { 5, 6, 7, 8 });
                failureReason = string.Empty;
                return true;
            }
        }

        private sealed class FakeProfilerCaptureFacility : IGoldenSceneProfilerCaptureFacility
        {
            private string outputPath;

            public bool IsSupported => true;
            public string Format => "application/vnd.unity.profiler";
            public string Extension => "data";
            public string UnsupportedReason => string.Empty;

            public bool TryBegin(string path, out string failureReason)
            {
                outputPath = path;
                failureReason = string.Empty;
                return true;
            }

            public bool TryEnd(out string failureReason)
            {
                File.WriteAllBytes(outputPath, new byte[] { 9, 10, 11, 12 });
                failureReason = string.Empty;
                return true;
            }
        }

        private static GoldenSceneIdentityRecord CreateIdentity(
            GoldenSceneSetup setup,
            string runId)
        {
            GoldenSceneCatalogLoadResult catalog = LoadCanonical();
            var request = new GoldenSceneIdentityRequest(
                "build-20260831.1",
                "1aedfba024b7c82701494188492876a4b8a7828f",
                runId,
                "capture-" + runId,
                "2026-08-31T03:00:00.0000000Z",
                "automation",
                "al-golden-scene-capture",
                "1.0.0",
                true,
                12d);
            var environment = new GoldenSceneRuntimeEnvironment(
                "6000.3.22f1",
                "WindowsPlayer",
                "AL-Test-Model",
                "Windows 11",
                "Test CPU",
                "Test GPU",
                8192,
                4096,
                "Direct3D11",
                new string('b', 64));
            Assert.That(GoldenSceneRuntimeIdentityCollector.TryCollect(
                setup,
                catalog.CatalogFingerprint,
                request,
                environment,
                out GoldenSceneIdentityRecord identity,
                out string diagnostic), Is.True, diagnostic);
            return identity;
        }

        private static GoldenSceneArtifactRecord CreateCapturedArtifact(
            GoldenSceneSetup setup,
            GoldenSceneIdentityRecord identity,
            GoldenSceneArtifactKind kind)
        {
            string extension;
            string format;
            switch (kind)
            {
                case GoldenSceneArtifactKind.Still:
                    extension = "png";
                    format = "image/png";
                    break;
                case GoldenSceneArtifactKind.Video:
                    extension = "mp4";
                    format = "video/mp4";
                    break;
                case GoldenSceneArtifactKind.Profiler:
                    extension = "raw";
                    format = "application/vnd.unity.profiler";
                    break;
                case GoldenSceneArtifactKind.Telemetry:
                    extension = "json";
                    format = "application/json";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
            return GoldenSceneArtifactRecord.Captured(
                setup,
                identity.RunId,
                kind,
                GoldenSceneArtifactNaming.BuildFileName(
                    setup,
                    identity.RunId,
                    kind,
                    extension),
                format,
                new string('c', 64),
                4096,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:01.0000000Z");
        }

        private static GoldenSceneTelemetryReport CreateTelemetryReport()
        {
            var device = new GoldenSceneDeviceSnapshot(
                null,
                string.Empty,
                null,
                string.Empty);
            var session = new GoldenSceneTelemetrySession(
                new GoldenSceneTelemetryConfiguration(30, 0d, 1d),
                "2026-08-31T03:00:00.0000000Z",
                true,
                device);
            session.RecordFrame(new GoldenSceneFrameObservation(
                1,
                1d,
                16.666d,
                8d,
                7d,
                null));
            return session.Complete(
                "2026-08-31T03:00:01.0000000Z",
                device);
        }

        private static GoldenSceneSetup Resolve(
            string sceneId,
            string anchorId,
            string presetId)
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                sceneId,
                anchorId,
                presetId,
                null,
                out GoldenSceneSetup setup,
                out string diagnostic), Is.True, diagnostic);
            return setup;
        }

        private static GoldenSceneCatalogLoadResult LoadCanonical()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                GoldenSceneCatalogContract.FileName);
            GoldenSceneCatalogLoadResult result = GoldenSceneCatalogLoader.Validate(
                File.ReadAllBytes(path));
            Assert.That(result.IsAccepted, Is.True);
            return result;
        }
    }
}
