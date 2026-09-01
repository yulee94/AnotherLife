using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Benchmarks.GoldenScenes;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Benchmarks
{
    public sealed class GoldenSceneBenchmarkRunnerTests
    {
        [Test]
        public void CommandLineSelectsExactBoundedRunInputs()
        {
            string output = Path.Combine(Path.GetTempPath(), "al-gs-results");
            string[] arguments =
            {
                "AnotherLifeUnity.exe",
                "--al-gs-run",
                "--al-gs-scene", "GS-05",
                "--al-gs-anchor", "city_overview",
                "--al-gs-quality", "android_floor_30",
                "--al-gs-seed", "905099",
                "--al-gs-warmup-seconds", "12.5",
                "--al-gs-measurement-seconds", "45",
                "--al-gs-output", output,
                "--al-gs-run-id", "run-0009",
                "--al-gs-width", "1920",
                "--al-gs-height", "1080",
                "--al-gs-video-fps", "30",
                "--al-gs-ui", "required",
                "--al-gs-operator", "automation",
                "--al-gs-certification", "target-platform"
            };

            Assert.That(GoldenSceneBenchmarkRequestParser.TryParse(
                arguments,
                isEditor: false,
                out GoldenSceneBenchmarkRequest request,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(request.SceneId, Is.EqualTo("GS-05"));
            Assert.That(request.AnchorId, Is.EqualTo("city_overview"));
            Assert.That(request.QualityPresetId, Is.EqualTo("android_floor_30"));
            Assert.That(request.SeedOverride, Is.EqualTo(905099));
            Assert.That(request.WarmupSeconds, Is.EqualTo(12.5d));
            Assert.That(request.MeasurementSeconds, Is.EqualTo(45d));
            Assert.That(request.OutputRoot, Is.EqualTo(Path.GetFullPath(output)));
            Assert.That(request.RunId, Is.EqualTo("run-0009"));
            Assert.That(request.Width, Is.EqualTo(1920));
            Assert.That(request.Height, Is.EqualTo(1080));
            Assert.That(request.VideoFrameRate, Is.EqualTo(30));
            Assert.That(request.UiCaptureMode,
                Is.EqualTo(GoldenSceneUiCaptureMode.RequiredByBenchmark));
            Assert.That(request.OperatorId, Is.EqualTo("automation"));
            Assert.That(request.RequestsTargetPlatformCertification, Is.True);
            Assert.That(request.TotalSeconds, Is.EqualTo(57.5d));
        }

        [Test]
        public void CommandLineRejectsEditorCertificationAndUnboundedDurations()
        {
            string[] editorArguments =
            {
                "Unity.exe", "--al-gs-run",
                "--al-gs-scene", "GS-01",
                "--al-gs-anchor", "class_reveal",
                "--al-gs-quality", "balanced_60",
                "--al-gs-output", Path.GetTempPath(),
                "--al-gs-certification", "target-platform"
            };
            Assert.That(GoldenSceneBenchmarkRequestParser.TryParse(
                editorArguments,
                isEditor: true,
                out _,
                out string editorDiagnostic), Is.False);
            Assert.That(editorDiagnostic,
                Is.EqualTo("AL-GS-RUNNER-EDITOR-CERTIFICATION-FORBIDDEN"));

            string[] durationArguments =
            {
                "AnotherLifeUnity.exe", "--al-gs-run",
                "--al-gs-scene", "GS-01",
                "--al-gs-anchor", "class_reveal",
                "--al-gs-quality", "balanced_60",
                "--al-gs-output", Path.GetTempPath(),
                "--al-gs-measurement-seconds", "3601"
            };
            Assert.That(GoldenSceneBenchmarkRequestParser.TryParse(
                durationArguments,
                isEditor: false,
                out _,
                out string durationDiagnostic), Is.False);
            Assert.That(durationDiagnostic,
                Is.EqualTo("AL-GS-RUNNER-MEASUREMENT-DURATION-INVALID"));

            string[] unknownArguments =
            {
                "AnotherLifeUnity.exe", "--al-gs-run",
                "--al-gs-scene", "GS-01",
                "--al-gs-anchor", "class_reveal",
                "--al-gs-quality", "balanced_60",
                "--al-gs-output", Path.GetTempPath(),
                "--al-gs-measurement-second", "60"
            };
            Assert.That(GoldenSceneBenchmarkRequestParser.TryParse(
                unknownArguments,
                isEditor: false,
                out _,
                out string unknownDiagnostic), Is.False);
            Assert.That(unknownDiagnostic,
                Is.EqualTo("AL-GS-RUNNER-ARGUMENT-UNKNOWN:--al-gs-measurement-second"));
        }

        [Test]
        public void CommandLineAcceptsOnlyAnExistingFfmpegExecutable()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "al-gs-ffmpeg-parser-" + Guid.NewGuid().ToString("N"));
            string ffmpegPath = Path.Combine(root, "ffmpeg.exe");
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllBytes(ffmpegPath, new byte[] { 1 });
                string[] arguments =
                {
                    "AnotherLifeUnity.exe", "--al-gs-run",
                    "--al-gs-scene", "GS-03",
                    "--al-gs-anchor", "boss_entry",
                    "--al-gs-quality", "pc_high_60",
                    "--al-gs-output", Path.Combine(root, "evidence"),
                    "--al-gs-ffmpeg", ffmpegPath
                };

                Assert.That(GoldenSceneBenchmarkRequestParser.TryParse(
                    arguments,
                    isEditor: false,
                    out GoldenSceneBenchmarkRequest request,
                    out string diagnostic), Is.True, diagnostic);
                Assert.That(request.FfmpegPath, Is.EqualTo(Path.GetFullPath(ffmpegPath)));

                arguments[arguments.Length - 1] = Path.Combine(root, "missing", "ffmpeg.exe");
                Assert.That(GoldenSceneBenchmarkRequestParser.TryParse(
                    arguments,
                    isEditor: false,
                    out _,
                    out diagnostic), Is.False);
                Assert.That(diagnostic, Is.EqualTo("AL-GS-RUNNER-FFMPEG-PATH-INVALID"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void RuntimeValuesMarkEditorBuildGuidAsNotApplicable()
        {
            Assert.That(
                GoldenSceneBenchmarkRuntimeValues.ResolveApplicationBuildGuid(true, string.Empty),
                Is.EqualTo("editor-not-applicable"));
            Assert.That(
                GoldenSceneBenchmarkRuntimeValues.ResolveApplicationBuildGuid(
                    false,
                    "1234567890abcdef1234567890abcdef"),
                Is.EqualTo("1234567890abcdef1234567890abcdef"));
            Assert.Throws<InvalidOperationException>(() =>
                GoldenSceneBenchmarkRuntimeValues.ResolveApplicationBuildGuid(false, string.Empty));
        }

        [Test]
        public void RuntimeActionsConvertExpectedExceptionsToDiagnostics()
        {
            Assert.That(GoldenSceneBenchmarkRuntimeActions.TryInvoke(
                () => 7,
                out int value,
                out string successDiagnostic), Is.True, successDiagnostic);
            Assert.That(value, Is.EqualTo(7));

            Assert.That(GoldenSceneBenchmarkRuntimeActions.TryInvoke<int>(
                () => throw new InvalidOperationException("expected"),
                out _,
                out string failureDiagnostic), Is.False);
            Assert.That(failureDiagnostic, Does.StartWith("InvalidOperationException: expected"));
        }

        [Test]
        public void InputLoaderUsesTransportForPackagedUrisAndReadsLocalFiles()
        {
            Assert.That(
                GoldenSceneBenchmarkInputLoader.RequiresUnityWebRequest(
                    "jar:file:///data/app/base.apk!/assets/GameData/catalog.json"),
                Is.True);
            Assert.That(
                GoldenSceneBenchmarkInputLoader.RequiresUnityWebRequest(
                    "https://example.invalid/StreamingAssets/GameData/catalog.json"),
                Is.True);
            Assert.That(
                GoldenSceneBenchmarkInputLoader.CombinePath(
                    "jar:file:///data/app/base.apk!/assets/StreamingAssets",
                    "GameData/catalog.json"),
                Is.EqualTo(
                    "jar:file:///data/app/base.apk!/assets/StreamingAssets/GameData/catalog.json"));

            string path = Path.Combine(Path.GetTempPath(), "al-gs-input-" + Guid.NewGuid().ToString("N"));
            try
            {
                byte[] expected = { 1, 2, 3, 4 };
                File.WriteAllBytes(path, expected);
                GoldenSceneBenchmarkInputLoadResult result = null;
                IEnumerator operation = GoldenSceneBenchmarkInputLoader.ReadAllBytes(
                    path,
                    value => result = value);
                while (operation.MoveNext()) { }

                Assert.That(result, Is.Not.Null);
                Assert.That(result.IsSuccess, Is.True, result.Diagnostic);
                CollectionAssert.AreEqual(expected, result.Bytes);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void EmbeddedBuildIdentityMustMatchCatalogUnityAndBuiltInRuntime()
        {
            GoldenSceneCatalogLoadResult catalog = LoadCanonical();
            var metadata = new GoldenSceneBuildIdentityMetadata(
                "al-gs-20260831-001",
                "1aedfba024b7c82701494188492876a4b8a7828f",
                catalog.CatalogFingerprint,
                "6000.3.22f1",
                "StandaloneWindows64",
                "Built-in Render Pipeline",
                "2026-08-31T03:00:00.0000000Z");

            Assert.That(GoldenSceneBuildIdentityValidator.TryValidate(
                metadata,
                catalog.CatalogFingerprint,
                "6000.3.22f1",
                runtimePlatform: "WindowsPlayer",
                applicationBuildGuid: "1234567890abcdef1234567890abcdef",
                isEditor: false,
                isBuiltInRenderPipeline: true,
                out string readyDiagnostic), Is.True, readyDiagnostic);
            Assert.That(readyDiagnostic, Is.EqualTo("AL-GS-BUILD-IDENTITY-READY"));

            Assert.That(GoldenSceneBuildIdentityValidator.TryValidate(
                metadata,
                new string('a', 64),
                "6000.3.22f1",
                runtimePlatform: "WindowsPlayer",
                applicationBuildGuid: "1234567890abcdef1234567890abcdef",
                isEditor: false,
                isBuiltInRenderPipeline: true,
                out string catalogDiagnostic), Is.False);
            Assert.That(catalogDiagnostic,
                Is.EqualTo("AL-GS-BUILD-CATALOG-FINGERPRINT-MISMATCH"));

            Assert.That(GoldenSceneBuildIdentityValidator.TryValidate(
                metadata,
                catalog.CatalogFingerprint,
                "6000.3.22f1",
                runtimePlatform: "WindowsPlayer",
                applicationBuildGuid: "1234567890abcdef1234567890abcdef",
                isEditor: false,
                isBuiltInRenderPipeline: false,
                out string pipelineDiagnostic), Is.False);
            Assert.That(pipelineDiagnostic,
                Is.EqualTo("AL-GS-BUILD-RENDER-PIPELINE-MISMATCH"));

            Assert.That(GoldenSceneBuildIdentityValidator.TryValidate(
                metadata,
                catalog.CatalogFingerprint,
                "6000.3.22f1",
                runtimePlatform: "Android",
                applicationBuildGuid: "1234567890abcdef1234567890abcdef",
                isEditor: false,
                isBuiltInRenderPipeline: true,
                out string targetDiagnostic), Is.False);
            Assert.That(targetDiagnostic,
                Is.EqualTo("AL-GS-BUILD-TARGET-MISMATCH"));
        }

        [Test]
        public void ScorecardMapsEveryTemplateFieldToPassFailOrUnavailable()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "android_floor_30");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-scorecard");
            GoldenSceneTelemetryReport telemetry = CreateTelemetryReport();
            GoldenSceneCaptureManifest manifest = CreateManifest(setup, identity);

            GoldenSceneScorecardReport scorecard = GoldenSceneScorecardReport.Create(
                identity,
                "1234567890abcdef1234567890abcdef",
                manifest,
                telemetry,
                isBuiltInRenderPipeline: true,
                requestsTargetPlatformCertification: true);

            string[] requiredLabels =
            {
                "Golden scene", "Scene revision", "Build ID / commit",
                "Catalog fingerprint", "Unity version", "Platform / device / OS",
                "CPU / GPU / RAM", "Graphics API",
                "Resolution / render scale / upscaler", "Quality preset",
                "Capture date and operator", "Deterministic seed / anchor",
                "Thermal/power starting state",
                "Intended frame-rate/frame-time contract",
                "Frame pacing and hitch contract", "Sustained thermal behavior",
                "Memory and allocation budget", "Streaming/residency behavior",
                "LOD/impostor/quality transitions", "Primary read and gameplay silhouette",
                "Realm/role/threat identity beyond color", "Material distinction without emission",
                "Lighting and navigation clarity", "Animation weight/contact/transitions",
                "VFX protected-information contract", "UI/HUD hierarchy and central scan path",
                "Phone/tablet/PC composition as required",
                "Minimap/world-map agreement as required", "Text/UI scaling and safe areas",
                "Contrast and color-independent state", "Reduced motion/shake/flash/VFX",
                "Audio-off/caption semantic parity", "Input navigation/remapping/focus",
                "Provenance and rights traceability", "Originality/non-copy review",
                "No placeholder/debug/fallback presentation",
                "CPU frame time (ms)", "GPU frame time (ms)",
                "Delivered frame time (ms)", "Input-to-visible response (ms)",
                "Gameplay hitches", "System memory", "Unity memory",
                "Graphics memory estimate", "Allocations / GC",
                "Draw calls / batches", "Triangles / vertices",
                "Active full/fallback/nameplate actors", "Particle/VFX counts by source",
                "Texture residency / streaming stalls", "Shader compilation events",
                "Thermal status/headroom", "Battery delta and duration",
                "Quality-scaling events", "Raw capture paths"
            };

            CollectionAssert.IsSubsetOf(
                requiredLabels,
                scorecard.Fields.Select(field => field.Label).ToArray());
            Assert.That(scorecard.Fields.Select(field => field.Label), Is.Unique);
            Assert.That(scorecard.Fields.All(field =>
                field.Status == GoldenSceneScorecardStatus.Pass ||
                field.Status == GoldenSceneScorecardStatus.Fail ||
                field.Status == GoldenSceneScorecardStatus.Unavailable), Is.True);
            Assert.That(scorecard.CertificationStatus,
                Is.EqualTo("target-platform-evidence-incomplete"));
            Assert.That(scorecard.ToJson(), Does.Contain("\"status\":\"unavailable\""));
            Assert.That(scorecard.ToMarkdown(), Does.Contain("| Golden scene | PASS |"));
            Assert.That(scorecard.ToMarkdown(),
                Does.Contain("Editor output is development-only and cannot certify a target platform."));
        }

        [Test]
        public void TargetPlatformCertificationRequiresSupportedDeviceTelemetry()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "android_floor_30");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-device-gate");
            GoldenSceneScorecardReport scorecard = GoldenSceneScorecardReport.Create(
                identity,
                "1234567890abcdef1234567890abcdef",
                CreateCompleteManifest(setup, identity),
                CreateTelemetryReport(),
                isBuiltInRenderPipeline: true,
                requestsTargetPlatformCertification: true);

            Assert.That(scorecard.CertificationStatus,
                Is.EqualTo("target-platform-evidence-incomplete"));
        }

        [Test]
        public void WindowsCertificationAcceptsExplicitUnavailableDeviceApis()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "pc_high_60");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-windows-device-policy");
            GoldenSceneScorecardReport scorecard = GoldenSceneScorecardReport.Create(
                identity,
                "1234567890abcdef1234567890abcdef",
                CreateCompleteManifest(setup, identity),
                CreateTelemetryReport(explicitUnavailableDeviceCapabilities: true),
                isBuiltInRenderPipeline: true,
                requestsTargetPlatformCertification: true);

            Assert.That(scorecard.CertificationStatus,
                Is.EqualTo("target-platform-evidence-ready-for-review"));
        }

        [Test]
        public void RuntimeCaptureAllowsRunnerOwnedCompletion()
        {
            var host = new GameObject("GoldenSceneRuntimeCapture-Test");
            try
            {
                GoldenSceneRuntimeCapture runtimeCapture =
                    host.AddComponent<GoldenSceneRuntimeCapture>();
                Assert.That(runtimeCapture.AutoComplete, Is.True);
                runtimeCapture.AutoComplete = false;
                Assert.That(runtimeCapture.AutoComplete, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AtomicPublisherExposesOnlyCompleteValidatedPackage()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "al-gs-package-" + Guid.NewGuid().ToString("N"));
            string stagingRoot = Path.Combine(root, ".staging-run-1");
            string packageDirectory = Path.Combine(stagingRoot, "package-run-1");
            string finalDirectory = Path.Combine(root, "package-run-1");
            try
            {
                Directory.CreateDirectory(packageDirectory);
                File.WriteAllText(Path.Combine(packageDirectory, "result.json"), "{}");
                File.WriteAllText(Path.Combine(packageDirectory, "scorecard.json"), "{}");
                File.WriteAllText(Path.Combine(packageDirectory, "scorecard.md"), "ok");

                string published = GoldenSceneAtomicResultPublisher.Publish(
                    packageDirectory,
                    root,
                    "package-run-1",
                    new[] { "result.json", "scorecard.json", "scorecard.md" });

                Assert.That(published, Is.EqualTo(finalDirectory));
                Assert.That(Directory.Exists(finalDirectory), Is.True);
                Assert.That(Directory.Exists(packageDirectory), Is.False);
                Assert.That(File.Exists(Path.Combine(finalDirectory, "result.json")), Is.True);
                Assert.That(
                    () => GoldenSceneAtomicResultPublisher.Publish(
                        finalDirectory,
                        root,
                        "package-run-1",
                        new[] { "result.json" }),
                    Throws.InvalidOperationException);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void AtomicPublisherRejectsStagingOutsideOutputRoot()
        {
            string token = Guid.NewGuid().ToString("N");
            string root = Path.Combine(Path.GetTempPath(), "al-gs-root-" + token);
            string outside = Path.Combine(Path.GetTempPath(), "al-gs-outside-" + token);
            try
            {
                Directory.CreateDirectory(outside);
                File.WriteAllText(Path.Combine(outside, "result.json"), "{}");
                Assert.That(
                    () => GoldenSceneAtomicResultPublisher.Publish(
                        outside,
                        root,
                        "package-run-1",
                        new[] { "result.json" }),
                    Throws.InvalidOperationException);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                if (Directory.Exists(outside)) Directory.Delete(outside, true);
            }
        }

        [Test]
        public void ResultDocumentLinksIdentityTelemetryArtifactsScorecardAndProvenance()
        {
            GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "android_floor_30");
            GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-result");
            GoldenSceneTelemetryReport telemetry = CreateTelemetryReport();
            GoldenSceneCaptureManifest manifest = CreateManifest(setup, identity);
            GoldenSceneScorecardReport scorecard = GoldenSceneScorecardReport.Create(
                identity,
                "1234567890abcdef1234567890abcdef",
                manifest,
                telemetry,
                isBuiltInRenderPipeline: true,
                requestsTargetPlatformCertification: true);

            var result = new GoldenSceneBenchmarkResultDocument(
                identity,
                "1234567890abcdef1234567890abcdef",
                manifest,
                telemetry,
                scorecard,
                "capture-manifest.json",
                "scorecard.json",
                "scorecard.md");

            string json = result.ToJson();
            Assert.That(json, Does.Contain("\"sceneId\":\"GS-03\""));
            Assert.That(json, Does.Contain("\"applicationBuildGuid\":\"1234567890abcdef1234567890abcdef\""));
            Assert.That(json, Does.Contain("\"catalogFingerprint\":\"" + identity.CatalogFingerprint + "\""));
            Assert.That(json, Does.Contain("\"rawSampleCount\":1"));
            Assert.That(json, Does.Contain("\"capabilities\":["));
            Assert.That(json, Does.Contain("\"artifactReferences\":["));
            Assert.That(json, Does.Contain("\"sourceManifestId\":\"" +
                GoldenSceneCapturePolicy.RequiredSourceManifestId + "\""));
            Assert.That(json, Does.Contain("\"thirdPartyMediaIncluded\":false"));
            Assert.That(json, Does.Contain("\"certificationStatus\":\"target-platform-evidence-incomplete\""));
            Assert.That(json, Does.Contain("\"scorecardJson\":\"scorecard.json\""));
        }

        [Test]
        public void PackageWriterPublishesOneCompleteAtomicResultDirectory()
        {
            string outputRoot = Path.Combine(
                Application.temporaryCachePath,
                "al-gs-package-writer-" + Guid.NewGuid().ToString("N"));
            try
            {
                string stagingDirectory = Path.Combine(outputRoot, ".staging", "run-package");
                Directory.CreateDirectory(stagingDirectory);
                GoldenSceneSetup setup = Resolve("GS-03", "boss_entry", "android_floor_30");
                GoldenSceneIdentityRecord identity = CreateIdentity(setup, "run-package");
                GoldenSceneTelemetryReport telemetry = CreateTelemetryReport();
                GoldenSceneCaptureManifest manifest = CreateManifest(setup, identity);
                string stillFileName = manifest.Artifacts.Single(artifact =>
                    artifact.Kind == GoldenSceneArtifactKind.Still).RelativePath;
                foreach (GoldenSceneArtifactRecord artifact in manifest.Artifacts.Where(
                             artifact => artifact.Status == GoldenSceneArtifactStatus.Captured))
                {
                    File.WriteAllBytes(
                        Path.Combine(stagingDirectory, artifact.RelativePath),
                        new byte[checked((int)artifact.ByteSize)]);
                }
                GoldenSceneScorecardReport scorecard = GoldenSceneScorecardReport.Create(
                    identity,
                    "1234567890abcdef1234567890abcdef",
                    manifest,
                    telemetry,
                    isBuiltInRenderPipeline: true,
                    requestsTargetPlatformCertification: true);

                File.WriteAllBytes(
                    Path.Combine(stagingDirectory, stillFileName),
                    Enumerable.Repeat((byte)1, 100).ToArray());
                Assert.That(
                    () => GoldenSceneBenchmarkPackageWriter.WriteAndPublish(
                        stagingDirectory,
                        outputRoot,
                        "GS-03-corrupt-package",
                        identity,
                        "1234567890abcdef1234567890abcdef",
                        manifest,
                        telemetry,
                        scorecard),
                    Throws.TypeOf<InvalidDataException>());
                File.WriteAllBytes(
                    Path.Combine(stagingDirectory, stillFileName),
                    new byte[100]);

                string finalDirectory = GoldenSceneBenchmarkPackageWriter.WriteAndPublish(
                    stagingDirectory,
                    outputRoot,
                    "GS-03-run-package",
                    identity,
                    "1234567890abcdef1234567890abcdef",
                    manifest,
                    telemetry,
                    scorecard);

                Assert.That(Directory.Exists(stagingDirectory), Is.False);
                Assert.That(finalDirectory,
                    Is.EqualTo(Path.GetFullPath(Path.Combine(outputRoot, "GS-03-run-package"))));
                CollectionAssert.IsSubsetOf(
                    new[]
                    {
                        "runtime-identity.json",
                        "telemetry.json",
                        "capture-manifest.json",
                        "scorecard.json",
                        "scorecard.md",
                        "benchmark-result.json",
                        stillFileName
                    },
                    Directory.GetFiles(finalDirectory).Select(Path.GetFileName).ToArray());
                string resultJson = File.ReadAllText(
                    Path.Combine(finalDirectory, "benchmark-result.json"));
                Assert.That(resultJson, Does.Contain("\"certificationStatus\""));
                Assert.That(resultJson, Does.Contain("\"artifactReferences\""));
            }
            finally
            {
                if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            }
        }

        [Test]
        public void RuntimePreparationFailsClosedUnlessArgumentsCatalogAndBuildIdentityAgree()
        {
            byte[] catalogBytes = LoadCatalogBytes();
            string catalogFingerprint = GoldenSceneCatalogLoader.Validate(catalogBytes)
                .CatalogFingerprint;
            var metadata = new GoldenSceneBuildIdentityMetadata(
                "build-player-001",
                "1aedfba024b7c82701494188492876a4b8a7828f",
                catalogFingerprint,
                "6000.3.22f1",
                "StandaloneWindows64",
                GoldenSceneBuildIdentityContract.RenderPipeline,
                "2026-08-31T03:00:00.0000000Z");
            var environment = new GoldenSceneRuntimeEnvironment(
                "6000.3.22f1", "WindowsPlayer", "test-device", "test-os",
                "test-cpu", "test-gpu", 8192, 2048, "Direct3D11",
                new string('d', 64));
            string[] args =
            {
                "AnotherLife.exe", "--al-gs-run",
                "--al-gs-scene", "GS-03",
                "--al-gs-anchor", "boss_entry",
                "--al-gs-quality", "android_floor_30",
                "--al-gs-seed", "903031",
                "--al-gs-warmup-seconds", "10",
                "--al-gs-measurement-seconds", "60",
                "--al-gs-width", "1920",
                "--al-gs-height", "1080",
                "--al-gs-video-fps", "30",
                "--al-gs-ui", "excluded",
                "--al-gs-output", "C:/captures",
                "--al-gs-run-id", "run-preparation",
                "--al-gs-operator", "automation",
                "--al-gs-certification", "target-platform"
            };

            Assert.That(GoldenSceneBenchmarkPreparation.TryCreate(
                args,
                false,
                catalogBytes,
                metadata.ToJson(),
                environment,
                true,
                "1234567890abcdef1234567890abcdef",
                out GoldenSceneBenchmarkContext context,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(context.Setup.Scene.Id, Is.EqualTo("GS-03"));
            Assert.That(context.Identity.BuildId, Is.EqualTo(metadata.BuildId));
            Assert.That(context.Identity.SourceCommit, Is.EqualTo(metadata.SourceCommit));
            Assert.That(context.Identity.RunId, Is.EqualTo("run-preparation"));
            Assert.That(context.Request.MeasurementSeconds, Is.EqualTo(60d));

            var mismatched = new GoldenSceneBuildIdentityMetadata(
                metadata.BuildId,
                metadata.SourceCommit,
                new string('e', 64),
                metadata.UnityVersion,
                metadata.BuildTarget,
                metadata.RenderPipeline,
                metadata.GeneratedAtUtc);
            Assert.That(GoldenSceneBenchmarkPreparation.TryCreate(
                args,
                false,
                catalogBytes,
                mismatched.ToJson(),
                environment,
                true,
                "1234567890abcdef1234567890abcdef",
                out _,
                out diagnostic), Is.False);
            Assert.That(diagnostic,
                Is.EqualTo("AL-GS-BUILD-CATALOG-FINGERPRINT-MISMATCH"));
        }

        private static GoldenSceneCaptureManifest CreateManifest(
            GoldenSceneSetup setup,
            GoldenSceneIdentityRecord identity)
        {
            var media = new GoldenSceneCaptureMediaSettings(
                1280, 720, 30, 1d, GoldenSceneUiCaptureMode.Excluded, string.Empty);
            GoldenSceneArtifactRecord[] artifacts =
            {
                Captured(setup, identity, GoldenSceneArtifactKind.Still, "png", "image/png"),
                GoldenSceneArtifactRecord.Unavailable(
                    setup, identity.RunId, GoldenSceneArtifactKind.Video,
                    GoldenSceneArtifactStatus.Unsupported, "video/mp4",
                    "AL-GS-VIDEO-UNSUPPORTED", "No licensed encoder.",
                    "2026-08-31T03:00:00.0000000Z", "2026-08-31T03:00:01.0000000Z"),
                Captured(setup, identity, GoldenSceneArtifactKind.Profiler, "raw",
                    "application/vnd.unity.profiler"),
                Captured(setup, identity, GoldenSceneArtifactKind.Telemetry, "json",
                    "application/json")
            };
            Assert.That(GoldenSceneCaptureManifest.TryCreate(
                identity, setup, media,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:01.0000000Z",
                GoldenSceneCapturePolicy.RequiredSourceManifestId,
                false,
                new GoldenSceneAnchorConsistency(1, 30, 0),
                artifacts,
                out GoldenSceneCaptureManifest manifest,
                out string diagnostic), Is.True, diagnostic);
            return manifest;
        }

        private static GoldenSceneCaptureManifest CreateCompleteManifest(
            GoldenSceneSetup setup,
            GoldenSceneIdentityRecord identity)
        {
            var media = new GoldenSceneCaptureMediaSettings(
                1280, 720, 30, 1d, GoldenSceneUiCaptureMode.Excluded, string.Empty);
            GoldenSceneArtifactRecord[] artifacts =
            {
                Captured(setup, identity, GoldenSceneArtifactKind.Still, "png", "image/png"),
                Captured(setup, identity, GoldenSceneArtifactKind.Video, "mp4", "video/mp4"),
                Captured(setup, identity, GoldenSceneArtifactKind.Profiler, "raw",
                    "application/vnd.unity.profiler"),
                Captured(setup, identity, GoldenSceneArtifactKind.Telemetry, "json",
                    "application/json")
            };
            Assert.That(GoldenSceneCaptureManifest.TryCreate(
                identity, setup, media,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:01.0000000Z",
                GoldenSceneCapturePolicy.RequiredSourceManifestId,
                false,
                new GoldenSceneAnchorConsistency(1, 30, 0),
                artifacts,
                out GoldenSceneCaptureManifest manifest,
                out string diagnostic), Is.True, diagnostic);
            return manifest;
        }

        private static GoldenSceneArtifactRecord Captured(
            GoldenSceneSetup setup,
            GoldenSceneIdentityRecord identity,
            GoldenSceneArtifactKind kind,
            string extension,
            string format)
        {
            return GoldenSceneArtifactRecord.Captured(
                setup, identity.RunId, kind,
                GoldenSceneArtifactNaming.BuildFileName(setup, identity.RunId, kind, extension),
                format, GoldenSceneHash.ComputeSha256(new byte[100]), 100,
                "2026-08-31T03:00:00.0000000Z",
                "2026-08-31T03:00:01.0000000Z");
        }

        private static GoldenSceneTelemetryReport CreateTelemetryReport(
            bool explicitUnavailableDeviceCapabilities = false)
        {
            var device = explicitUnavailableDeviceCapabilities
                ? new GoldenSceneDeviceSnapshot(null, string.Empty, null, string.Empty)
                : new GoldenSceneDeviceSnapshot(0.80d, "discharging", 35d, "none");
            var session = new GoldenSceneTelemetrySession(
                new GoldenSceneTelemetryConfiguration(30, 0d, 1d),
                "2026-08-31T03:00:00.0000000Z", true, device);
            session.SetCapability(TelemetryCapability.Supported(
                GoldenSceneTelemetryMetricIds.CpuFrameTime, "milliseconds", "test"));
            session.SetCapability(TelemetryCapability.Supported(
                GoldenSceneTelemetryMetricIds.GpuFrameTime, "milliseconds", "test"));
            if (explicitUnavailableDeviceCapabilities)
            {
                const string reason = "Windows does not expose this metric through Unity SystemInfo.";
                session.SetCapability(TelemetryCapability.Unsupported(
                    GoldenSceneTelemetryMetricIds.BatteryLevel,
                    "ratio",
                    "unity-systeminfo",
                    reason));
                session.SetCapability(TelemetryCapability.Unsupported(
                    GoldenSceneTelemetryMetricIds.DeviceTemperature,
                    "celsius",
                    "unity-systeminfo",
                    reason));
                session.SetCapability(TelemetryCapability.Unsupported(
                    GoldenSceneTelemetryMetricIds.DeviceThermalState,
                    "state",
                    "unity-systeminfo",
                    reason));
            }
            session.RecordFrame(new GoldenSceneFrameObservation(
                1, 1d, 30d, 10d, 11d,
                new Dictionary<string, double?>
                {
                    [GoldenSceneTelemetryMetricIds.UnityUsedMemory] = 100d
                }));
            return session.Complete(
                "2026-08-31T03:00:01.0000000Z",
                explicitUnavailableDeviceCapabilities
                    ? new GoldenSceneDeviceSnapshot(null, string.Empty, null, string.Empty)
                    : new GoldenSceneDeviceSnapshot(0.79d, "discharging", 36d, "none"));
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
                "al-golden-scene-benchmark-runner",
                "1.0.0",
                true,
                1d);
            var environment = new GoldenSceneRuntimeEnvironment(
                "6000.3.22f1", "WindowsPlayer", "AL-Test-Model", "Windows 11",
                "Test CPU", "Test GPU", 8192, 4096, "Direct3D11", new string('b', 64));
            Assert.That(GoldenSceneRuntimeIdentityCollector.TryCollect(
                setup, catalog.CatalogFingerprint, request, environment,
                out GoldenSceneIdentityRecord identity,
                out string diagnostic), Is.True, diagnostic);
            return identity;
        }

        private static GoldenSceneSetup Resolve(
            string sceneId,
            string anchorId,
            string presetId)
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog, sceneId, anchorId, presetId, null,
                out GoldenSceneSetup setup,
                out string diagnostic), Is.True, diagnostic);
            return setup;
        }

        private static GoldenSceneCatalogLoadResult LoadCanonical()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL", "StreamingAssets", "GameData",
                GoldenSceneCatalogContract.FileName);
            GoldenSceneCatalogLoadResult result = GoldenSceneCatalogLoader.Validate(
                File.ReadAllBytes(path));
            Assert.That(result.IsAccepted, Is.True);
            return result;
        }

        private static byte[] LoadCatalogBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL", "StreamingAssets", "GameData",
                GoldenSceneCatalogContract.FileName));
        }
    }
}
