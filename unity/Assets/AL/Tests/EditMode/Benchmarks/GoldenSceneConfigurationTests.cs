using System;
using System.IO;
using System.Linq;
using System.Text;
using AL.Benchmarks.GoldenScenes;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Benchmarks
{
    public sealed class GoldenSceneConfigurationTests
    {
        [Test]
        public void CanonicalCatalogDefinesEveryGoldenSceneAndQualityPreset()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();

            Assert.That(result.IsAccepted, Is.True, Diagnostics(result));
            Assert.That(result.Catalog.Scenes.Select(scene => scene.Id), Is.EqualTo(new[]
            {
                "GS-01", "GS-02", "GS-03", "GS-04", "GS-05"
            }));
            Assert.That(result.Catalog.QualityPresets.Select(preset => preset.Id), Is.EqualTo(new[]
            {
                "android_floor_30", "balanced_60", "pc_high_60"
            }));
            Assert.That(result.CatalogFingerprint, Does.Match("^[0-9a-f]{64}$"));

            foreach (GoldenSceneDefinition scene in result.Catalog.Scenes)
            {
                Assert.That(scene.Revision, Is.Not.Empty, scene.Id);
                Assert.That(scene.UnitySceneName, Is.Not.Empty, scene.Id);
                Assert.That(scene.Anchors.Count, Is.GreaterThan(0), scene.Id);
                Assert.That(scene.Anchors.Any(anchor => anchor.Id == scene.DefaultAnchorId), Is.True, scene.Id);
                Assert.That(scene.QualityPresetIds, Is.EquivalentTo(new[]
                {
                    "android_floor_30", "balanced_60", "pc_high_60"
                }), scene.Id);
            }
        }

        [Test]
        public void MissingAnchorFailsClosedWithoutSubstitution()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();

            bool resolved = GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-02",
                "missing_anchor",
                "balanced_60",
                null,
                out GoldenSceneSetup setup,
                out string diagnostic);

            Assert.That(resolved, Is.False);
            Assert.That(setup, Is.Null);
            Assert.That(diagnostic, Is.EqualTo("AL-GS-ANCHOR-MISSING:GS-02:missing_anchor"));
        }

        [Test]
        public void InvalidAnchorTransformAndUnknownPropertiesAreRejected()
        {
            string canonical = File.ReadAllText(CanonicalCatalogPath());
            string invalidFieldOfView = canonical.Replace(
                "\"fieldOfViewDegrees\": 55.0",
                "\"fieldOfViewDegrees\": 200.0");
            GoldenSceneCatalogLoadResult invalidTransform = GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(invalidFieldOfView));

            Assert.That(invalidTransform.IsAccepted, Is.False);
            Assert.That(invalidTransform.Diagnostics.Any(item =>
                item.Code == "AL-GS-ANCHOR-FOV-INVALID"), Is.True, Diagnostics(invalidTransform));

            string unknownProperty = canonical.Replace(
                "\"catalogId\": \"al_golden_scene_catalog\"",
                "\"catalogId\": \"al_golden_scene_catalog\", \"fallbackAnchor\": true");
            GoldenSceneCatalogLoadResult unknown = GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(unknownProperty));

            Assert.That(unknown.IsAccepted, Is.False);
            Assert.That(unknown.Diagnostics.Any(item =>
                item.Code == "AL-GS-PROPERTY-UNKNOWN" && item.Path == "$.fallbackAnchor"), Is.True,
                Diagnostics(unknown));

            string unsupportedFrameRate = canonical.Replace(
                "\"targetFrameRate\": 30",
                "\"targetFrameRate\": 45");
            GoldenSceneCatalogLoadResult invalidFrameRate = GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(unsupportedFrameRate));
            Assert.That(invalidFrameRate.IsAccepted, Is.False);
            Assert.That(invalidFrameRate.Diagnostics.Any(item =>
                item.Code == "AL-GS-QUALITY-RANGE-INVALID" &&
                item.Path == "$.qualityPresets[0].targetFrameRate"), Is.True,
                Diagnostics(invalidFrameRate));

            string fractionalFrameRate = canonical.Replace(
                "\"targetFrameRate\": 30",
                "\"targetFrameRate\": 30.00001");
            Assert.That(GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(fractionalFrameRate)).IsAccepted, Is.False);

            string roundedIntoRange = canonical.Replace(
                "\"renderScale\": 0.85",
                "\"renderScale\": 1.00000001");
            Assert.That(GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(roundedIntoRange)).IsAccepted, Is.False);

            string subPrecisionFraction = canonical.Replace(
                "\"targetFrameRate\": 30",
                "\"targetFrameRate\": 30.0000000000000000000000000000000000001");
            Assert.That(GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(subPrecisionFraction)).IsAccepted, Is.False);

            string subPrecisionOverflow = canonical.Replace(
                "\"renderScale\": 0.85",
                "\"renderScale\": 1.0000000000000000000000000000000000001");
            Assert.That(GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(subPrecisionOverflow)).IsAccepted, Is.False);

            string changedLayout = canonical.Replace(
                "\"id\": \"face_detail\"",
                "\"id\": \"portrait_detail\"");
            GoldenSceneCatalogLoadResult invalidLayout = GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(changedLayout));
            Assert.That(invalidLayout.IsAccepted, Is.False);
            Assert.That(invalidLayout.Diagnostics.Any(item =>
                item.Code == "AL-GS-LAYOUT-FINGERPRINT-MISMATCH"), Is.True,
                Diagnostics(invalidLayout));

            string changedLayoutWithMatchingFingerprint = changedLayout.Replace(
                "25804b632d5ffbab372cf33b6435ca0fe5b1ac4705865115270caed42e100ef1",
                "7ace98307efdbefa2ae455c326d1b60488cae7853114f36f51c3f4de8231ac18");
            GoldenSceneCatalogLoadResult forgedLayout = GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(changedLayoutWithMatchingFingerprint));
            Assert.That(forgedLayout.IsAccepted, Is.False);
            Assert.That(forgedLayout.Diagnostics.Any(item =>
                item.Code == "AL-GS-VALUE-INVALID" && item.Path == "$.layoutFingerprint"),
                Is.True, Diagnostics(forgedLayout));

            string largeExponentZero = canonical.Replace(
                "\"shadowDistanceMeters\": 24.0",
                "\"shadowDistanceMeters\": 0e129");
            Assert.That(GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(largeExponentZero)).IsAccepted, Is.True);

            string offPrecisionGrid = canonical.Replace(
                "\"renderScale\": 0.85",
                "\"renderScale\": 0.8500001");
            Assert.That(GoldenSceneCatalogLoader.Validate(
                Encoding.UTF8.GetBytes(offPrecisionGrid)).IsAccepted, Is.False);
        }

        [Test]
        public void SameSelectionResolvesEquivalentSetupAndCameraState()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-03",
                "boss_entry",
                "android_floor_30",
                903031,
                out GoldenSceneSetup first,
                out string firstDiagnostic), Is.True, firstDiagnostic);
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-03",
                "boss_entry",
                "android_floor_30",
                903031,
                out GoldenSceneSetup second,
                out string secondDiagnostic), Is.True, secondDiagnostic);

            Assert.That(second.ConfigurationFingerprint, Is.EqualTo(first.ConfigurationFingerprint));
            Assert.That(second.Seed, Is.EqualTo(first.Seed));
            Assert.That(second.Anchor.Position, Is.EqualTo(first.Anchor.Position));
            Assert.That(second.Anchor.EulerAngles, Is.EqualTo(first.Anchor.EulerAngles));
            Assert.That(second.QualityPreset.Id, Is.EqualTo(first.QualityPreset.Id));

            var firstHost = new GameObject("GoldenSceneCameraFirst");
            var secondHost = new GameObject("GoldenSceneCameraSecond");
            try
            {
                Camera firstCamera = firstHost.AddComponent<Camera>();
                Camera secondCamera = secondHost.AddComponent<Camera>();
                GoldenSceneCameraState.Apply(firstCamera, first);
                GoldenSceneCameraState.Apply(secondCamera, second);

                Assert.That(secondCamera.transform.position, Is.EqualTo(firstCamera.transform.position));
                Assert.That(secondCamera.transform.rotation, Is.EqualTo(firstCamera.transform.rotation));
                Assert.That(secondCamera.fieldOfView, Is.EqualTo(firstCamera.fieldOfView));
                Assert.That(secondCamera.orthographic, Is.EqualTo(firstCamera.orthographic));
                Assert.That(secondCamera.orthographicSize, Is.EqualTo(firstCamera.orthographicSize));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
        }

        [Test]
        public void ApplyingSetupConfiguresCameraAndEffectiveUnityQualityState()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-03",
                "boss_entry",
                "android_floor_30",
                null,
                out GoldenSceneSetup setup,
                out string diagnostic), Is.True, diagnostic);

            int targetFrameRate = Application.targetFrameRate;
            int vSyncCount = QualitySettings.vSyncCount;
            float shadowDistance = QualitySettings.shadowDistance;
            float lodBias = QualitySettings.lodBias;
            int textureMipmapLimit = QualitySettings.globalTextureMipmapLimit;
            int pixelLightCount = QualitySettings.pixelLightCount;
            float fixedDpiScale = QualitySettings.resolutionScalingFixedDPIFactor;
            float widthScale = ScalableBufferManager.widthScaleFactor;
            float heightScale = ScalableBufferManager.heightScaleFactor;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            var host = new GameObject("GoldenSceneRuntimeSetup");
            try
            {
                Camera camera = host.AddComponent<Camera>();
                GoldenSceneRuntimeSetup.Apply(camera, setup);
                float firstRandomValue = UnityEngine.Random.value;
                GoldenSceneRuntimeSetup.Apply(camera, setup);
                float secondRandomValue = UnityEngine.Random.value;

                Assert.That(GoldenSceneRuntimeSetup.DynamicResolutionRequested, Is.True);
                Assert.That(GoldenSceneRuntimeSetup.AppliedRenderScale,
                    Is.EqualTo(0.85f).Within(0.001f));
                Assert.That(secondRandomValue, Is.EqualTo(firstRandomValue));
                Assert.That(Application.targetFrameRate, Is.EqualTo(30));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(QualitySettings.shadowDistance, Is.EqualTo(24f).Within(0.001f));
                Assert.That(QualitySettings.lodBias, Is.EqualTo(0.72f).Within(0.001f));
                Assert.That(QualitySettings.globalTextureMipmapLimit, Is.EqualTo(1));
                Assert.That(QualitySettings.pixelLightCount, Is.EqualTo(1));
                Assert.That(QualitySettings.resolutionScalingFixedDPIFactor,
                    Is.EqualTo(0.85f).Within(0.001f));
                Assert.That(GoldenSceneRuntimeSetup.AppliedVfxDensity,
                    Is.EqualTo(0.65f).Within(0.001f));
                Assert.That(GoldenSceneRuntimeSetup.AppliedConfigurationFingerprint,
                    Is.EqualTo(setup.ConfigurationFingerprint));
            }
            finally
            {
                Application.targetFrameRate = targetFrameRate;
                QualitySettings.vSyncCount = vSyncCount;
                QualitySettings.shadowDistance = shadowDistance;
                QualitySettings.lodBias = lodBias;
                QualitySettings.globalTextureMipmapLimit = textureMipmapLimit;
                QualitySettings.pixelLightCount = pixelLightCount;
                QualitySettings.resolutionScalingFixedDPIFactor = fixedDpiScale;
                ScalableBufferManager.ResizeBuffers(widthScale, heightScale);
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeIdentityIsCompleteMachineReadableAndSetupStable()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-05",
                "city_overview",
                "pc_high_60",
                null,
                out GoldenSceneSetup setup,
                out string setupDiagnostic), Is.True, setupDiagnostic);

            var request = new GoldenSceneIdentityRequest(
                "build-20260831.1",
                "1aedfba024b7c82701494188492876a4b8a7828f",
                "run-0001",
                "capture-0001",
                "2026-08-31T02:30:00.0000000Z",
                "automation",
                "al-benchmark-runner",
                "1.0.0",
                true,
                1200.0);
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
                new string('a', 64));

            Assert.That(GoldenSceneRuntimeIdentityCollector.TryCollect(
                setup,
                result.CatalogFingerprint,
                request,
                environment,
                out GoldenSceneIdentityRecord record,
                out string diagnostic), Is.True, diagnostic);

            Assert.That(record.ConfigurationFingerprint, Is.EqualTo(setup.ConfigurationFingerprint));
            Assert.That(record.CatalogFingerprint, Is.EqualTo(result.CatalogFingerprint));
            Assert.That(record.IsPlayerBuild, Is.True);
            Assert.That(record.IsEditor, Is.False);
            Assert.That(record.Seed, Is.EqualTo(setup.Seed));
            string json = record.ToJson();
            Assert.That(json, Does.StartWith("{"));
            Assert.That(json, Does.Contain("\"buildId\":\"build-20260831.1\""));
            Assert.That(json, Does.Contain("\"sourceCommit\":\"1aedfba024b7c82701494188492876a4b8a7828f\""));
            Assert.That(json, Does.Contain("\"catalogFingerprint\":\"" + result.CatalogFingerprint + "\""));
            Assert.That(json, Does.Contain("\"sceneId\":\"GS-05\""));
            Assert.That(json, Does.Contain("\"anchorId\":\"city_overview\""));
            Assert.That(json, Does.Contain("\"qualityPresetId\":\"pc_high_60\""));
            Assert.That(json, Does.Contain("\"runId\":\"run-0001\""));
            Assert.That(json, Does.Contain("\"isPlayerBuild\":true"));
            Assert.That(json, Does.Contain("\"isEditor\":false"));
            Assert.That(json, Does.Not.Contain("\"deviceName\""));
        }

        [Test]
        public void RuntimeIdentityRejectsMissingSourceCommit()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-01",
                "class_reveal",
                "balanced_60",
                null,
                out GoldenSceneSetup setup,
                out _), Is.True);
            var request = new GoldenSceneIdentityRequest(
                "build-1", string.Empty, "run-1", "capture-1",
                "2026-08-31T02:30:00.0000000Z", "automation", "runner", "1.0.0",
                true, 10.0);
            var environment = new GoldenSceneRuntimeEnvironment(
                "6000.3.22f1", "WindowsPlayer", "model", "os", "cpu", "gpu",
                8192, 4096, "Direct3D11", new string('b', 64));

            Assert.That(GoldenSceneRuntimeIdentityCollector.TryCollect(
                setup,
                result.CatalogFingerprint,
                request,
                environment,
                out GoldenSceneIdentityRecord record,
                out string diagnostic), Is.False);
            Assert.That(record, Is.Null);
            Assert.That(diagnostic, Is.EqualTo("AL-GS-IDENTITY-SOURCE-COMMIT-MISSING"));
        }

        [Test]
        public void RuntimeIdentityRejectsNonShaDeviceIdentity()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-01",
                "class_reveal",
                "balanced_60",
                null,
                out GoldenSceneSetup setup,
                out _), Is.True);
            var request = new GoldenSceneIdentityRequest(
                "build-1", new string('c', 40), "run-1", "capture-1",
                "2026-08-31T02:30:00.0000000Z", "automation", "runner", "1.0.0",
                true, 10.0);
            var environment = new GoldenSceneRuntimeEnvironment(
                "6000.3.22f1", "WindowsPlayer", "model", "os",
                "cpu", "gpu", 8192, 4096, "Direct3D11", "not-a-sha256");

            Assert.That(GoldenSceneRuntimeIdentityCollector.TryCollect(
                setup,
                result.CatalogFingerprint,
                request,
                environment,
                out GoldenSceneIdentityRecord record,
                out string diagnostic), Is.False);
            Assert.That(record, Is.Null);
            Assert.That(diagnostic, Is.EqualTo("AL-GS-IDENTITY-DEVICE-IDENTITY-INVALID"));
        }

        [Test]
        public void RuntimeIdentityRejectsPlayerEnvironmentMarkedAsNonPlayer()
        {
            GoldenSceneCatalogLoadResult result = LoadCanonical();
            Assert.That(GoldenSceneConfigurationResolver.TryResolve(
                result.Catalog,
                "GS-01",
                "class_reveal",
                "balanced_60",
                null,
                out GoldenSceneSetup setup,
                out _), Is.True);
            var request = new GoldenSceneIdentityRequest(
                "build-1", new string('c', 40), "run-1", "capture-1",
                "2026-08-31T02:30:00.0000000Z", "automation", "runner", "1.0.0",
                false, 10.0);
            var environment = new GoldenSceneRuntimeEnvironment(
                "6000.3.22f1", "WindowsPlayer", "model", "os", "cpu", "gpu",
                8192, 4096, "Direct3D11", new string('b', 64));

            Assert.That(GoldenSceneRuntimeIdentityCollector.TryCollect(
                setup,
                result.CatalogFingerprint,
                request,
                environment,
                out GoldenSceneIdentityRecord record,
                out string diagnostic), Is.False);
            Assert.That(record, Is.Null);
            Assert.That(diagnostic, Is.EqualTo("AL-GS-IDENTITY-BUILD-PLATFORM-MISMATCH"));
        }

        private static GoldenSceneCatalogLoadResult LoadCanonical()
        {
            return GoldenSceneCatalogLoader.Validate(File.ReadAllBytes(CanonicalCatalogPath()));
        }

        private static string CanonicalCatalogPath()
        {
            return Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                GoldenSceneCatalogContract.FileName);
        }

        private static string Diagnostics(GoldenSceneCatalogLoadResult result)
        {
            return string.Join("\n", result.Diagnostics.Select(item => item.Fingerprint));
        }
    }
}
