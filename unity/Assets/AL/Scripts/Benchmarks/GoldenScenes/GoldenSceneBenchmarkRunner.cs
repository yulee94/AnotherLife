using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AL.Benchmarks.GoldenScenes
{
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class GoldenSceneBenchmarkRunner : MonoBehaviour
    {
        private GoldenSceneBenchmarkContext context;
        private GoldenSceneRuntimeTelemetryCollector telemetry;
        private GoldenSceneRuntimeCapture capture;
        private string stagingRoot;
        private string applicationBuildGuid;
        private bool isBuiltInRenderPipeline;
        private bool finishing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!GoldenSceneBenchmarkRequestParser.IsRequested(arguments)) return;
            var host = new GameObject("AL Golden Scene Benchmark Runner");
            DontDestroyOnLoad(host);
            host.AddComponent<GoldenSceneBenchmarkRunner>();
        }

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string gameDataDirectory = ResolveGameDataDirectory();
            string catalogPath = GoldenSceneBenchmarkInputLoader.CombinePath(
                gameDataDirectory,
                GoldenSceneCatalogContract.FileName);
            string buildIdentityPath = GoldenSceneBenchmarkInputLoader.CombinePath(
                gameDataDirectory,
                GoldenSceneBuildIdentityContract.FileName);
            GoldenSceneBenchmarkInputLoadResult catalogInput = null;
            yield return GoldenSceneBenchmarkInputLoader.ReadAllBytes(
                catalogPath,
                value => catalogInput = value);
            if (catalogInput == null || !catalogInput.IsSuccess)
            {
                Fail(
                    "AL-GS-BENCHMARK-CATALOG-READ-FAILED",
                    catalogInput?.Diagnostic ?? "AL-GS-INPUT-NO-RESULT");
                yield break;
            }

            string buildIdentityJson = string.Empty;
            if (!Application.isEditor || File.Exists(buildIdentityPath))
            {
                GoldenSceneBenchmarkInputLoadResult buildInput = null;
                yield return GoldenSceneBenchmarkInputLoader.ReadAllBytes(
                    buildIdentityPath,
                    value => buildInput = value);
                if (buildInput == null || !buildInput.IsSuccess)
                {
                    Fail(
                        "AL-GS-BENCHMARK-BUILD-IDENTITY-READ-FAILED",
                        buildInput?.Diagnostic ?? "AL-GS-INPUT-NO-RESULT");
                    yield break;
                }
                buildIdentityJson = Encoding.UTF8.GetString(buildInput.Bytes);
            }

            isBuiltInRenderPipeline = GraphicsSettings.currentRenderPipeline == null;
            try
            {
                applicationBuildGuid =
                    GoldenSceneBenchmarkRuntimeValues.ResolveApplicationBuildGuid(
                        Application.isEditor,
                        Application.buildGUID);
            }
            catch (Exception exception)
            {
                Fail("AL-GS-BUILD-GUID-INVALID", exception.Message);
                yield break;
            }
            if (!GoldenSceneBenchmarkPreparation.TryCreate(
                    arguments,
                    Application.isEditor,
                    catalogInput.Bytes,
                    buildIdentityJson,
                    GoldenSceneRuntimeEnvironment.CollectFromUnity(),
                    isBuiltInRenderPipeline,
                    applicationBuildGuid,
                    out context,
                    out string diagnostic))
            {
                Fail(diagnostic, "Benchmark preparation failed.");
                yield break;
            }

            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    context.Setup.UnitySceneName,
                    StringComparison.Ordinal))
            {
                if (!GoldenSceneBenchmarkRuntimeActions.TryInvoke(
                        () => SceneManager.LoadSceneAsync(
                            context.Setup.UnitySceneName,
                            LoadSceneMode.Single),
                        out AsyncOperation load,
                        out string loadFailure) || load == null)
                {
                    Fail(
                        "AL-GS-BENCHMARK-SCENE-LOAD-FAILED",
                        string.IsNullOrWhiteSpace(loadFailure)
                            ? context.Setup.UnitySceneName
                            : loadFailure);
                    yield break;
                }
                while (!load.isDone) yield return null;
            }
            yield return null;

            Camera benchmarkCamera = Camera.main;
            if (benchmarkCamera == null) benchmarkCamera = FindAnyObjectByType<Camera>();
            if (benchmarkCamera == null)
            {
                Fail("AL-GS-BENCHMARK-CAMERA-MISSING", context.Setup.UnitySceneName);
                yield break;
            }

            if (!GoldenSceneBenchmarkRuntimeActions.TryInvoke(
                    () =>
                    {
                        GoldenSceneRuntimeSetup.Apply(benchmarkCamera, context.Setup);
                        telemetry = gameObject.AddComponent<GoldenSceneRuntimeTelemetryCollector>();
                        capture = gameObject.AddComponent<GoldenSceneRuntimeCapture>();
                        capture.AutoComplete = false;
                        telemetry.StartCollection(new GoldenSceneTelemetryConfiguration(
                            context.Setup.QualityPreset.TargetFrameRate,
                            context.Request.WarmupSeconds,
                            context.Request.MeasurementSeconds));
                        return true;
                    },
                    out _,
                    out string setupFailure))
            {
                Fail("AL-GS-BENCHMARK-RUNTIME-SETUP-FAILED", setupFailure);
                yield break;
            }

            double warmupEndsAt = Time.realtimeSinceStartupAsDouble + context.Request.WarmupSeconds;
            while (Time.realtimeSinceStartupAsDouble < warmupEndsAt) yield return null;

            GoldenSceneUiCaptureMode uiMode = context.Request.UiCaptureMode;
            if (!GoldenSceneBenchmarkRuntimeActions.TryInvoke(
                    () => new GoldenSceneCaptureMediaSettings(
                        context.Request.Width,
                        context.Request.Height,
                        context.Request.VideoFrameRate,
                        context.Request.MeasurementSeconds,
                        uiMode,
                        uiMode == GoldenSceneUiCaptureMode.RequiredByBenchmark
                            ? "PostMVP_Graphics_Benchmark_Spec_2026-08-25.md"
                            : string.Empty),
                    out GoldenSceneCaptureMediaSettings mediaSettings,
                    out string mediaFailure))
            {
                Fail("AL-GS-BENCHMARK-MEDIA-SETTINGS-INVALID", mediaFailure);
                yield break;
            }
            string stagingToken = context.Setup.Scene.Id + "-" + context.Request.RunId + "-staging";
            string requestedStagingRoot = Path.Combine(
                context.Request.OutputRoot,
                ".staging",
                stagingToken);
            if (Directory.Exists(requestedStagingRoot) || File.Exists(requestedStagingRoot))
            {
                Fail(
                    "AL-GS-BENCHMARK-STAGING-COLLISION",
                    "A staging path already exists for this scene/run ID.");
                yield break;
            }
            stagingRoot = requestedStagingRoot;
            if (!GoldenSceneBenchmarkRuntimeActions.TryInvoke(
                    () => Directory.CreateDirectory(stagingRoot),
                    out _,
                    out string stagingFailure))
            {
                Fail("AL-GS-BENCHMARK-STAGING-CREATE-FAILED", stagingFailure);
                yield break;
            }

            try
            {
                IGoldenSceneVideoCaptureFacility videoFacility =
                    string.IsNullOrEmpty(context.Request.FfmpegPath)
                        ? null
                        : new GoldenSceneFfmpegVideoCaptureFacility(
                            context.Request.FfmpegPath,
                            Application.productName,
                            Application.platform == RuntimePlatform.WindowsPlayer);
                capture.BeginCapture(
                    benchmarkCamera,
                    context.Setup,
                    context.Identity,
                    mediaSettings,
                    stagingRoot,
                    videoFacility,
                    telemetry: telemetry);
            }
            catch (Exception exception)
            {
                Fail("AL-GS-BENCHMARK-CAPTURE-START-FAILED", exception.Message);
                yield break;
            }

            double timeoutAt = Time.realtimeSinceStartupAsDouble +
                               context.Request.MeasurementSeconds + 30d;
            while (telemetry.LatestReport == null &&
                   Time.realtimeSinceStartupAsDouble < timeoutAt)
                yield return null;
            if (telemetry.LatestReport == null)
            {
                Fail("AL-GS-BENCHMARK-TELEMETRY-TIMEOUT", "Telemetry did not complete.");
                yield break;
            }

            GoldenSceneCaptureManifest manifest;
            try
            {
                manifest = capture.IsCapturing
                    ? capture.CompleteCapture()
                    : capture.LatestManifest;
            }
            catch (Exception exception)
            {
                Fail("AL-GS-BENCHMARK-CAPTURE-COMPLETE-FAILED", exception.Message);
                yield break;
            }
            if (manifest == null || string.IsNullOrWhiteSpace(capture.ManifestPath))
            {
                Fail("AL-GS-BENCHMARK-MANIFEST-MISSING", "Capture returned no manifest.");
                yield break;
            }

            try
            {
                GoldenSceneScorecardReport scorecard = GoldenSceneScorecardReport.Create(
                    context.Identity,
                    applicationBuildGuid,
                    manifest,
                    telemetry.LatestReport,
                    isBuiltInRenderPipeline,
                    context.Request.RequestsTargetPlatformCertification);
                string packageDirectory = Path.GetDirectoryName(capture.ManifestPath);
                string finalToken = context.Setup.Scene.Id + "-" + context.Request.RunId;
                string publishedDirectory = GoldenSceneBenchmarkPackageWriter.WriteAndPublish(
                    packageDirectory,
                    context.Request.OutputRoot,
                    finalToken,
                    context.Identity,
                    applicationBuildGuid,
                    manifest,
                    telemetry.LatestReport,
                    scorecard);
                RemoveEmptyStagingRoot();
                Debug.Log("[AL-GS-BENCHMARK-COMPLETE] " + publishedDirectory);
                Exit(0);
            }
            catch (Exception exception)
            {
                Fail("AL-GS-BENCHMARK-PUBLISH-FAILED", exception.Message);
            }
        }

        private void OnDisable()
        {
            if (finishing) return;
            telemetry?.CancelCollection();
        }

        private static string ResolveGameDataDirectory()
        {
            if (Application.isEditor)
            {
                return Path.Combine(
                    Application.dataPath,
                    "AL", "StreamingAssets", "GameData");
            }
            return GoldenSceneBenchmarkInputLoader.CombinePath(
                Application.streamingAssetsPath,
                "GameData");
        }

        private void Fail(string diagnosticCode, string detail)
        {
            if (finishing) return;
            finishing = true;
            if (capture != null && capture.IsCapturing)
            {
                try { capture.CompleteCapture(); }
                catch (Exception finalizeError)
                {
                    Debug.LogError(
                        "[AL-GS-BENCHMARK-CAPTURE-ABORT-FAILED] " + finalizeError.Message);
                }
            }
            telemetry?.CancelCollection();
            if (!string.IsNullOrWhiteSpace(stagingRoot) && Directory.Exists(stagingRoot))
            {
                try { Directory.Delete(stagingRoot, true); }
                catch (Exception cleanupError)
                {
                    Debug.LogError("[AL-GS-BENCHMARK-CLEANUP-FAILED] " + cleanupError.Message);
                }
            }
            Debug.LogError("[" + diagnosticCode + "] " + detail);
            Exit(2);
        }

        private void RemoveEmptyStagingRoot()
        {
            if (string.IsNullOrWhiteSpace(stagingRoot) || !Directory.Exists(stagingRoot)) return;
            if (!Directory.EnumerateFileSystemEntries(stagingRoot).Any())
                Directory.Delete(stagingRoot);
        }

        private static void Exit(int exitCode)
        {
#if UNITY_EDITOR
            EditorApplication.Exit(exitCode);
#else
            Application.Quit(exitCode);
#endif
        }
    }
}
