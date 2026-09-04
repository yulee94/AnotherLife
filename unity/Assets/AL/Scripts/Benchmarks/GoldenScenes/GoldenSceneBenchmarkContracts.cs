using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AL.Benchmarks.GoldenScenes
{
    public sealed class GoldenSceneBenchmarkRequest
    {
        internal GoldenSceneBenchmarkRequest(
            string sceneId,
            string anchorId,
            string qualityPresetId,
            int? seedOverride,
            double warmupSeconds,
            double measurementSeconds,
            string outputRoot,
            string runId,
            int width,
            int height,
            int videoFrameRate,
            GoldenSceneUiCaptureMode uiCaptureMode,
            string ffmpegPath,
            string operatorId,
            bool requestsTargetPlatformCertification)
        {
            SceneId = sceneId;
            AnchorId = anchorId;
            QualityPresetId = qualityPresetId;
            SeedOverride = seedOverride;
            WarmupSeconds = warmupSeconds;
            MeasurementSeconds = measurementSeconds;
            OutputRoot = outputRoot;
            RunId = runId;
            Width = width;
            Height = height;
            VideoFrameRate = videoFrameRate;
            UiCaptureMode = uiCaptureMode;
            FfmpegPath = ffmpegPath ?? string.Empty;
            OperatorId = operatorId;
            RequestsTargetPlatformCertification = requestsTargetPlatformCertification;
        }

        public string SceneId { get; }
        public string AnchorId { get; }
        public string QualityPresetId { get; }
        public int? SeedOverride { get; }
        public double WarmupSeconds { get; }
        public double MeasurementSeconds { get; }
        public double TotalSeconds => WarmupSeconds + MeasurementSeconds;
        public string OutputRoot { get; }
        public string RunId { get; }
        public int Width { get; }
        public int Height { get; }
        public int VideoFrameRate { get; }
        public GoldenSceneUiCaptureMode UiCaptureMode { get; }
        public string FfmpegPath { get; }
        public string OperatorId { get; }
        public bool RequestsTargetPlatformCertification { get; }
    }

    public static class GoldenSceneBenchmarkRuntimeValues
    {
        public static string ResolveApplicationBuildGuid(bool isEditor, string applicationBuildGuid)
        {
            if (isEditor) return "editor-not-applicable";
            string value = applicationBuildGuid?.Trim() ?? string.Empty;
            if (value.Length != 32 || value.Any(character =>
                    !((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f'))))
                throw new InvalidOperationException("AL-GS-BUILD-GUID-INVALID");
            return value;
        }
    }

    public static class GoldenSceneBenchmarkRuntimeActions
    {
        public static bool TryInvoke<T>(
            Func<T> action,
            out T value,
            out string diagnostic)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            try
            {
                value = action();
                diagnostic = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                value = default;
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }
    }

    public sealed class GoldenSceneBenchmarkInputLoadResult
    {
        private GoldenSceneBenchmarkInputLoadResult(byte[] bytes, string diagnostic)
        {
            Bytes = bytes;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public byte[] Bytes { get; }
        public string Diagnostic { get; }
        public bool IsSuccess => Bytes != null;

        internal static GoldenSceneBenchmarkInputLoadResult Success(byte[] bytes)
        {
            return new GoldenSceneBenchmarkInputLoadResult(
                bytes ?? throw new ArgumentNullException(nameof(bytes)),
                string.Empty);
        }

        internal static GoldenSceneBenchmarkInputLoadResult Failure(string diagnostic)
        {
            return new GoldenSceneBenchmarkInputLoadResult(
                null,
                string.IsNullOrWhiteSpace(diagnostic)
                    ? "AL-GS-INPUT-LOAD-FAILED"
                    : diagnostic.Trim());
        }
    }

    public static class GoldenSceneBenchmarkInputLoader
    {
        public static bool RequiresUnityWebRequest(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return path.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                   path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
        }

        public static string CombinePath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("Input root is required.", nameof(root));
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("Relative path is required.", nameof(relativePath));
            if (RequiresUnityWebRequest(root))
                return root.TrimEnd('/', '\\') + "/" + relativePath.TrimStart('/', '\\');
            return Path.Combine(root, relativePath);
        }

        public static IEnumerator ReadAllBytes(
            string path,
            Action<GoldenSceneBenchmarkInputLoadResult> completed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (string.IsNullOrWhiteSpace(path))
            {
                completed(GoldenSceneBenchmarkInputLoadResult.Failure("AL-GS-INPUT-PATH-MISSING"));
                yield break;
            }

            if (!RequiresUnityWebRequest(path))
            {
                try
                {
                    completed(GoldenSceneBenchmarkInputLoadResult.Success(File.ReadAllBytes(path)));
                }
                catch (Exception exception)
                {
                    completed(GoldenSceneBenchmarkInputLoadResult.Failure(
                        "AL-GS-INPUT-READ-FAILED:" + exception.GetType().Name));
                }
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(path))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    completed(GoldenSceneBenchmarkInputLoadResult.Failure(
                        "AL-GS-INPUT-TRANSPORT-FAILED:" + request.error));
                    yield break;
                }
                completed(GoldenSceneBenchmarkInputLoadResult.Success(request.downloadHandler.data));
            }
        }
    }

    public static class GoldenSceneBenchmarkRequestParser
    {
        public const string EnableArgument = "--al-gs-run";
        private const double MaximumWarmupSeconds = 3600d;
        private const double MaximumMeasurementSeconds = 3600d;
        private static readonly HashSet<string> ValueArguments = new HashSet<string>(
            new[]
            {
                "--al-gs-scene", "--al-gs-anchor", "--al-gs-quality", "--al-gs-seed",
                "--al-gs-warmup-seconds", "--al-gs-measurement-seconds",
                "--al-gs-output", "--al-gs-run-id", "--al-gs-width", "--al-gs-height",
                "--al-gs-video-fps", "--al-gs-ui", "--al-gs-operator",
                "--al-gs-certification", "--al-gs-ffmpeg"
            },
            StringComparer.Ordinal);

        public static bool IsRequested(IEnumerable<string> arguments)
        {
            return arguments != null && arguments.Any(argument =>
                string.Equals(argument, EnableArgument, StringComparison.Ordinal));
        }

        public static bool TryParse(
            string[] arguments,
            bool isEditor,
            out GoldenSceneBenchmarkRequest request,
            out string diagnosticCode)
        {
            request = null;
            if (arguments == null || !IsRequested(arguments))
                return Fail("AL-GS-RUNNER-NOT-REQUESTED", out diagnosticCode);

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < arguments.Length; index++)
            {
                string argument = arguments[index] ?? string.Empty;
                if (string.Equals(argument, EnableArgument, StringComparison.Ordinal)) continue;
                if (!argument.StartsWith("--al-gs-", StringComparison.Ordinal)) continue;
                if (!ValueArguments.Contains(argument))
                    return Fail("AL-GS-RUNNER-ARGUMENT-UNKNOWN:" + argument, out diagnosticCode);
                if (index + 1 >= arguments.Length ||
                    (arguments[index + 1] ?? string.Empty).StartsWith("--", StringComparison.Ordinal))
                    return Fail("AL-GS-RUNNER-ARGUMENT-VALUE-MISSING:" + argument, out diagnosticCode);
                if (values.ContainsKey(argument))
                    return Fail("AL-GS-RUNNER-ARGUMENT-DUPLICATE:" + argument, out diagnosticCode);
                values.Add(argument, arguments[++index] ?? string.Empty);
            }

            string certification = Value(values, "--al-gs-certification", "development");
            if (!string.Equals(certification, "development", StringComparison.Ordinal) &&
                !string.Equals(certification, "target-platform", StringComparison.Ordinal))
                return Fail("AL-GS-RUNNER-CERTIFICATION-MODE-INVALID", out diagnosticCode);
            bool requestsCertification =
                string.Equals(certification, "target-platform", StringComparison.Ordinal);
            if (isEditor && requestsCertification)
                return Fail("AL-GS-RUNNER-EDITOR-CERTIFICATION-FORBIDDEN", out diagnosticCode);

            if (!TryDouble(values, "--al-gs-warmup-seconds", 10d, out double warmupSeconds) ||
                warmupSeconds < 0d || warmupSeconds > MaximumWarmupSeconds)
                return Fail("AL-GS-RUNNER-WARMUP-DURATION-INVALID", out diagnosticCode);
            if (!TryDouble(values, "--al-gs-measurement-seconds", 60d, out double measurementSeconds) ||
                measurementSeconds <= 0d || measurementSeconds > MaximumMeasurementSeconds)
                return Fail("AL-GS-RUNNER-MEASUREMENT-DURATION-INVALID", out diagnosticCode);

            string sceneId = Value(values, "--al-gs-scene");
            string anchorId = Value(values, "--al-gs-anchor");
            string qualityPresetId = Value(values, "--al-gs-quality");
            string output = Value(values, "--al-gs-output");
            if (string.IsNullOrWhiteSpace(sceneId))
                return Fail("AL-GS-RUNNER-SCENE-MISSING", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(anchorId))
                return Fail("AL-GS-RUNNER-ANCHOR-MISSING", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(qualityPresetId))
                return Fail("AL-GS-RUNNER-QUALITY-MISSING", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(output))
                return Fail("AL-GS-RUNNER-OUTPUT-MISSING", out diagnosticCode);

            int? seedOverride = null;
            string seedText = Value(values, "--al-gs-seed");
            if (!string.IsNullOrEmpty(seedText))
            {
                if (!int.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out int seed) ||
                    seed < 0)
                    return Fail("AL-GS-RUNNER-SEED-INVALID", out diagnosticCode);
                seedOverride = seed;
            }

            if (!TryInteger(values, "--al-gs-width", 1280, 1, 8192, out int width) ||
                !TryInteger(values, "--al-gs-height", 720, 1, 8192, out int height))
                return Fail("AL-GS-RUNNER-RESOLUTION-INVALID", out diagnosticCode);
            if (!TryInteger(values, "--al-gs-video-fps", 30, 1, 240, out int frameRate))
                return Fail("AL-GS-RUNNER-VIDEO-FPS-INVALID", out diagnosticCode);

            string runId = Value(values, "--al-gs-run-id", "run-0001");
            string operatorId = Value(values, "--al-gs-operator", "automation");
            if (!GoldenSceneArtifactNaming.IsSafeToken(runId))
                return Fail("AL-GS-RUNNER-RUN-ID-INVALID", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(operatorId) || operatorId.Length > 128)
                return Fail("AL-GS-RUNNER-OPERATOR-INVALID", out diagnosticCode);

            string ui = Value(values, "--al-gs-ui", "excluded");
            GoldenSceneUiCaptureMode uiMode;
            if (string.Equals(ui, "excluded", StringComparison.Ordinal))
                uiMode = GoldenSceneUiCaptureMode.Excluded;
            else if (string.Equals(ui, "required", StringComparison.Ordinal))
                uiMode = GoldenSceneUiCaptureMode.RequiredByBenchmark;
            else
                return Fail("AL-GS-RUNNER-UI-MODE-INVALID", out diagnosticCode);

            string ffmpegPath = Value(values, "--al-gs-ffmpeg");
            if (!string.IsNullOrWhiteSpace(ffmpegPath))
            {
                try
                {
                    ffmpegPath = Path.GetFullPath(ffmpegPath);
                }
                catch (Exception)
                {
                    return Fail("AL-GS-RUNNER-FFMPEG-PATH-INVALID", out diagnosticCode);
                }
                if (!string.Equals(
                        Path.GetFileName(ffmpegPath),
                        "ffmpeg.exe",
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(ffmpegPath))
                    return Fail("AL-GS-RUNNER-FFMPEG-PATH-INVALID", out diagnosticCode);
            }

            string fullOutput;
            try
            {
                fullOutput = Path.GetFullPath(output);
            }
            catch (Exception)
            {
                return Fail("AL-GS-RUNNER-OUTPUT-INVALID", out diagnosticCode);
            }

            request = new GoldenSceneBenchmarkRequest(
                sceneId,
                anchorId,
                qualityPresetId,
                seedOverride,
                warmupSeconds,
                measurementSeconds,
                fullOutput,
                runId,
                width,
                height,
                frameRate,
                uiMode,
                ffmpegPath,
                operatorId,
                requestsCertification);
            diagnosticCode = "AL-GS-RUNNER-REQUEST-READY";
            return true;
        }

        private static string Value(
            IReadOnlyDictionary<string, string> values,
            string name,
            string fallback = "")
        {
            return values.TryGetValue(name, out string value) ? value : fallback;
        }

        private static bool TryDouble(
            IReadOnlyDictionary<string, string> values,
            string name,
            double fallback,
            out double value)
        {
            string text = Value(values, name);
            if (string.IsNullOrEmpty(text))
            {
                value = fallback;
                return true;
            }
            return double.TryParse(
                       text,
                       NumberStyles.AllowDecimalPoint,
                       CultureInfo.InvariantCulture,
                       out value) &&
                   !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool TryInteger(
            IReadOnlyDictionary<string, string> values,
            string name,
            int fallback,
            int minimum,
            int maximum,
            out int value)
        {
            string text = Value(values, name);
            if (string.IsNullOrEmpty(text)) value = fallback;
            else if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
                return false;
            return value >= minimum && value <= maximum;
        }

        private static bool Fail(string code, out string diagnosticCode)
        {
            diagnosticCode = code;
            return false;
        }
    }

    public static class GoldenSceneBuildIdentityContract
    {
        public const string FileName = "al_golden_scene_build_identity.json";
        public const string RelativePath = "GameData/" + FileName;
        public const string RenderPipeline = "Built-in Render Pipeline";
    }

    public sealed class GoldenSceneBuildIdentityMetadata
    {
        [Serializable]
        private sealed class JsonData
        {
            public string schemaVersion;
            public string buildId;
            public string sourceCommit;
            public string catalogFingerprint;
            public string unityVersion;
            public string buildTarget;
            public string renderPipeline;
            public string generatedAtUtc;
        }

        public GoldenSceneBuildIdentityMetadata(
            string buildId,
            string sourceCommit,
            string catalogFingerprint,
            string unityVersion,
            string buildTarget,
            string renderPipeline,
            string generatedAtUtc)
        {
            BuildId = buildId ?? string.Empty;
            SourceCommit = sourceCommit ?? string.Empty;
            CatalogFingerprint = catalogFingerprint ?? string.Empty;
            UnityVersion = unityVersion ?? string.Empty;
            BuildTarget = buildTarget ?? string.Empty;
            RenderPipeline = renderPipeline ?? string.Empty;
            GeneratedAtUtc = generatedAtUtc ?? string.Empty;
        }

        public string SchemaVersion => "1.0.0";
        public string BuildId { get; }
        public string SourceCommit { get; }
        public string CatalogFingerprint { get; }
        public string UnityVersion { get; }
        public string BuildTarget { get; }
        public string RenderPipeline { get; }
        public string GeneratedAtUtc { get; }

        public string ToJson()
        {
            var json = new StringBuilder(512);
            json.Append('{');
            TelemetryJson.AppendString(json, "schemaVersion", SchemaVersion, true);
            TelemetryJson.AppendString(json, "buildId", BuildId);
            TelemetryJson.AppendString(json, "sourceCommit", SourceCommit);
            TelemetryJson.AppendString(json, "catalogFingerprint", CatalogFingerprint);
            TelemetryJson.AppendString(json, "unityVersion", UnityVersion);
            TelemetryJson.AppendString(json, "buildTarget", BuildTarget);
            TelemetryJson.AppendString(json, "renderPipeline", RenderPipeline);
            TelemetryJson.AppendString(json, "generatedAtUtc", GeneratedAtUtc);
            json.Append('}');
            return json.ToString();
        }

        public static bool TryParse(
            string json,
            out GoldenSceneBuildIdentityMetadata metadata,
            out string diagnosticCode)
        {
            metadata = null;
            JsonData data;
            try
            {
                data = JsonUtility.FromJson<JsonData>(json);
            }
            catch (Exception)
            {
                diagnosticCode = "AL-GS-BUILD-IDENTITY-JSON-INVALID";
                return false;
            }
            if (data == null || !string.Equals(data.schemaVersion, "1.0.0", StringComparison.Ordinal))
            {
                diagnosticCode = "AL-GS-BUILD-IDENTITY-VERSION-INVALID";
                return false;
            }
            metadata = new GoldenSceneBuildIdentityMetadata(
                data.buildId,
                data.sourceCommit,
                data.catalogFingerprint,
                data.unityVersion,
                data.buildTarget,
                data.renderPipeline,
                data.generatedAtUtc);
            diagnosticCode = "AL-GS-BUILD-IDENTITY-PARSED";
            return true;
        }
    }

    public static class GoldenSceneBuildIdentityValidator
    {
        public static bool TryValidate(
            GoldenSceneBuildIdentityMetadata metadata,
            string runtimeCatalogFingerprint,
            string runtimeUnityVersion,
            string runtimePlatform,
            string applicationBuildGuid,
            bool isEditor,
            bool isBuiltInRenderPipeline,
            out string diagnosticCode)
        {
            if (metadata == null)
                return Fail("AL-GS-BUILD-IDENTITY-MISSING", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(metadata.BuildId) ||
                !GoldenSceneArtifactNaming.IsSafeToken(metadata.BuildId))
                return Fail("AL-GS-BUILD-ID-INVALID", out diagnosticCode);
            if (!IsLowerHex(metadata.SourceCommit, 40) && !IsLowerHex(metadata.SourceCommit, 64))
                return Fail("AL-GS-BUILD-SOURCE-COMMIT-INVALID", out diagnosticCode);
            if (!IsLowerHex(metadata.CatalogFingerprint, 64))
                return Fail("AL-GS-BUILD-CATALOG-FINGERPRINT-INVALID", out diagnosticCode);
            if (!string.Equals(
                    metadata.CatalogFingerprint,
                    runtimeCatalogFingerprint,
                    StringComparison.Ordinal))
                return Fail("AL-GS-BUILD-CATALOG-FINGERPRINT-MISMATCH", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(metadata.UnityVersion) ||
                !string.Equals(metadata.UnityVersion, runtimeUnityVersion, StringComparison.Ordinal))
                return Fail("AL-GS-BUILD-UNITY-VERSION-MISMATCH", out diagnosticCode);
            if (!string.Equals(
                    metadata.RenderPipeline,
                    GoldenSceneBuildIdentityContract.RenderPipeline,
                    StringComparison.Ordinal) ||
                !isBuiltInRenderPipeline)
                return Fail("AL-GS-BUILD-RENDER-PIPELINE-MISMATCH", out diagnosticCode);
            if (!isEditor && !IsLowerHex(applicationBuildGuid, 32))
                return Fail("AL-GS-BUILD-GUID-INVALID", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(metadata.BuildTarget))
                return Fail("AL-GS-BUILD-TARGET-MISSING", out diagnosticCode);
            if (!MatchesBuildTarget(metadata.BuildTarget, runtimePlatform, isEditor))
                return Fail("AL-GS-BUILD-TARGET-MISMATCH", out diagnosticCode);
            if (!GoldenSceneCaptureValidation.TryUtcRange(
                    metadata.GeneratedAtUtc,
                    metadata.GeneratedAtUtc))
                return Fail("AL-GS-BUILD-TIME-INVALID", out diagnosticCode);

            diagnosticCode = isEditor
                ? "AL-GS-BUILD-IDENTITY-EDITOR-DEVELOPMENT-ONLY"
                : "AL-GS-BUILD-IDENTITY-READY";
            return true;
        }

        private static bool IsLowerHex(string value, int length)
        {
            return value != null && value.Length == length && value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));
        }

        private static bool MatchesBuildTarget(
            string buildTarget,
            string runtimePlatform,
            bool isEditor)
        {
            if (isEditor)
                return string.Equals(buildTarget, "Editor", StringComparison.Ordinal) &&
                       !string.IsNullOrWhiteSpace(runtimePlatform) &&
                       runtimePlatform.EndsWith("Editor", StringComparison.Ordinal);
            string expectedPlatform;
            switch (buildTarget)
            {
                case "StandaloneWindows":
                case "StandaloneWindows64":
                    expectedPlatform = "WindowsPlayer";
                    break;
                case "StandaloneOSX":
                    expectedPlatform = "OSXPlayer";
                    break;
                case "StandaloneLinux64":
                    expectedPlatform = "LinuxPlayer";
                    break;
                case "Android":
                    expectedPlatform = "Android";
                    break;
                case "iOS":
                    expectedPlatform = "IPhonePlayer";
                    break;
                case "WebGL":
                    expectedPlatform = "WebGLPlayer";
                    break;
                default:
                    return false;
            }
            return string.Equals(expectedPlatform, runtimePlatform, StringComparison.Ordinal);
        }

        private static bool Fail(string code, out string diagnosticCode)
        {
            diagnosticCode = code;
            return false;
        }
    }

    public sealed class GoldenSceneBenchmarkContext
    {
        internal GoldenSceneBenchmarkContext(
            GoldenSceneBenchmarkRequest request,
            GoldenSceneCatalogLoadResult catalog,
            GoldenSceneSetup setup,
            GoldenSceneBuildIdentityMetadata buildMetadata,
            GoldenSceneIdentityRecord identity)
        {
            Request = request;
            Catalog = catalog;
            Setup = setup;
            BuildMetadata = buildMetadata;
            Identity = identity;
        }

        public GoldenSceneBenchmarkRequest Request { get; }
        public GoldenSceneCatalogLoadResult Catalog { get; }
        public GoldenSceneSetup Setup { get; }
        public GoldenSceneBuildIdentityMetadata BuildMetadata { get; }
        public GoldenSceneIdentityRecord Identity { get; }
    }

    public static class GoldenSceneBenchmarkPreparation
    {
        public static bool TryCreate(
            string[] arguments,
            bool isEditor,
            byte[] catalogBytes,
            string buildMetadataJson,
            GoldenSceneRuntimeEnvironment environment,
            bool isBuiltInRenderPipeline,
            string applicationBuildGuid,
            out GoldenSceneBenchmarkContext context,
            out string diagnosticCode)
        {
            context = null;
            if (!GoldenSceneBenchmarkRequestParser.TryParse(
                    arguments,
                    isEditor,
                    out GoldenSceneBenchmarkRequest request,
                    out diagnosticCode)) return false;

            GoldenSceneCatalogLoadResult catalog = GoldenSceneCatalogLoader.Validate(catalogBytes);
            if (!catalog.IsAccepted)
                return Fail("AL-GS-CATALOG-REJECTED", out diagnosticCode);
            if (!GoldenSceneConfigurationResolver.TryResolve(
                    catalog.Catalog,
                    request.SceneId,
                    request.AnchorId,
                    request.QualityPresetId,
                    request.SeedOverride,
                    out GoldenSceneSetup setup,
                    out diagnosticCode)) return false;

            GoldenSceneBuildIdentityMetadata metadata;
            if (string.IsNullOrWhiteSpace(buildMetadataJson) && isEditor)
            {
                metadata = new GoldenSceneBuildIdentityMetadata(
                    "editor-development",
                    new string('0', 40),
                    catalog.CatalogFingerprint,
                    environment?.UnityVersion ?? string.Empty,
                    "Editor",
                    GoldenSceneBuildIdentityContract.RenderPipeline,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            }
            else if (!GoldenSceneBuildIdentityMetadata.TryParse(
                         buildMetadataJson,
                         out metadata,
                         out diagnosticCode))
            {
                return false;
            }

            if (!GoldenSceneBuildIdentityValidator.TryValidate(
                    metadata,
                    catalog.CatalogFingerprint,
                    environment?.UnityVersion,
                    environment?.Platform,
                    applicationBuildGuid,
                    isEditor,
                    isBuiltInRenderPipeline,
                    out diagnosticCode)) return false;

            string capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var identityRequest = new GoldenSceneIdentityRequest(
                metadata.BuildId,
                metadata.SourceCommit,
                request.RunId,
                "capture-" + request.RunId,
                capturedAtUtc,
                request.OperatorId,
                string.IsNullOrEmpty(request.FfmpegPath)
                    ? "al-golden-scene-benchmark-runner"
                    : "al-golden-scene-benchmark-runner+ffmpeg-gdigrab",
                "1.1.0",
                !isEditor,
                request.MeasurementSeconds);
            if (!GoldenSceneRuntimeIdentityCollector.TryCollect(
                    setup,
                    catalog.CatalogFingerprint,
                    identityRequest,
                    environment,
                    out GoldenSceneIdentityRecord identity,
                    out diagnosticCode)) return false;

            context = new GoldenSceneBenchmarkContext(
                request,
                catalog,
                setup,
                metadata,
                identity);
            diagnosticCode = "AL-GS-BENCHMARK-READY";
            return true;
        }

        private static bool Fail(string code, out string diagnosticCode)
        {
            diagnosticCode = code;
            return false;
        }
    }

    public sealed class GoldenSceneBenchmarkResultDocument
    {
        private readonly GoldenSceneIdentityRecord identity;
        private readonly string applicationBuildGuid;
        private readonly GoldenSceneCaptureManifest captureManifest;
        private readonly GoldenSceneTelemetryReport telemetry;
        private readonly GoldenSceneScorecardReport scorecard;
        private readonly string captureManifestPath;
        private readonly string scorecardJsonPath;
        private readonly string scorecardMarkdownPath;

        public GoldenSceneBenchmarkResultDocument(
            GoldenSceneIdentityRecord identity,
            string applicationBuildGuid,
            GoldenSceneCaptureManifest captureManifest,
            GoldenSceneTelemetryReport telemetry,
            GoldenSceneScorecardReport scorecard,
            string captureManifestPath,
            string scorecardJsonPath,
            string scorecardMarkdownPath)
        {
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            this.applicationBuildGuid = Require(applicationBuildGuid, nameof(applicationBuildGuid));
            this.captureManifest = captureManifest ?? throw new ArgumentNullException(nameof(captureManifest));
            this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            this.scorecard = scorecard ?? throw new ArgumentNullException(nameof(scorecard));
            this.captureManifestPath = Require(captureManifestPath, nameof(captureManifestPath));
            this.scorecardJsonPath = Require(scorecardJsonPath, nameof(scorecardJsonPath));
            this.scorecardMarkdownPath = Require(scorecardMarkdownPath, nameof(scorecardMarkdownPath));
        }

        public string ToJson()
        {
            string telemetryJson = telemetry.ToJson();
            var json = new StringBuilder(telemetryJson.Length + 8192);
            json.Append('{');
            TelemetryJson.AppendString(json, "schemaVersion", "1.0.0", true);
            TelemetryJson.AppendString(json, "generatedAtUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

            TelemetryJson.Prefix(json, "identity");
            json.Append('{');
            TelemetryJson.AppendString(json, "sceneId", identity.SceneId, true);
            TelemetryJson.AppendString(json, "anchorId", identity.AnchorId);
            TelemetryJson.AppendString(json, "qualityId", identity.QualityPresetId);
            TelemetryJson.AppendString(json, "runId", identity.RunId);
            TelemetryJson.AppendString(json, "buildId", identity.BuildId);
            TelemetryJson.AppendString(json, "sourceCommit", identity.SourceCommit);
            TelemetryJson.AppendString(json, "catalogFingerprint", identity.CatalogFingerprint);
            TelemetryJson.AppendString(json, "unityVersion", identity.UnityVersion);
            TelemetryJson.AppendString(json, "renderPipeline", GoldenSceneBuildIdentityContract.RenderPipeline);
            TelemetryJson.AppendString(json, "applicationBuildGuid", applicationBuildGuid);
            TelemetryJson.AppendString(json, "captureStartedAtUtc", identity.CapturedAtUtc);
            json.Append('}');

            TelemetryJson.Prefix(json, "telemetry");
            json.Append('{');
            TelemetryJson.AppendNumber(json, "rawSampleCount", telemetry.RawSamples.Count, true);
            TelemetryJson.Prefix(json, "report");
            json.Append(telemetryJson);
            json.Append('}');

            TelemetryJson.Prefix(json, "capture");
            json.Append('{');
            TelemetryJson.AppendString(json, "captureManifest", captureManifestPath, true);
            TelemetryJson.AppendString(json, "manifestIdentityKey", identity.ConfigurationFingerprint);
            json.Append('}');

            TelemetryJson.Prefix(json, "artifactReferences");
            json.Append('[');
            for (int index = 0; index < captureManifest.Artifacts.Count; index++)
            {
                if (index > 0) json.Append(',');
                GoldenSceneArtifactRecord artifact = captureManifest.Artifacts[index];
                json.Append('{');
                TelemetryJson.AppendString(
                    json,
                    "artifactId",
                    GoldenSceneArtifactNaming.KindName(artifact.Kind),
                    true);
                TelemetryJson.AppendString(json, "path", artifact.RelativePath);
                TelemetryJson.AppendString(json, "status", artifact.Status.ToString().ToLowerInvariant());
                TelemetryJson.AppendInteger(json, "byteLength", artifact.ByteSize);
                TelemetryJson.AppendString(json, "sha256", artifact.Sha256);
                TelemetryJson.AppendString(json, "diagnosticCode", artifact.DiagnosticCode);
                TelemetryJson.AppendString(json, "reason", artifact.Reason);
                json.Append('}');
            }
            AppendReference(json, "capture-manifest", captureManifestPath, captureManifest.Artifacts.Count > 0);
            AppendReference(json, "scorecard-json", scorecardJsonPath, true);
            AppendReference(json, "scorecard-markdown", scorecardMarkdownPath, true);
            json.Append(']');

            TelemetryJson.Prefix(json, "provenance");
            json.Append('{');
            TelemetryJson.AppendString(json, "sourceManifestId", GoldenSceneCapturePolicy.RequiredSourceManifestId, true);
            TelemetryJson.AppendBoolean(json, "thirdPartyMediaIncluded", false);
            json.Append('}');

            TelemetryJson.Prefix(json, "scorecard");
            json.Append('{');
            TelemetryJson.AppendString(json, "certificationStatus", scorecard.CertificationStatus, true);
            TelemetryJson.AppendString(json, "scorecardJson", scorecardJsonPath);
            TelemetryJson.AppendString(json, "scorecardMarkdown", scorecardMarkdownPath);
            TelemetryJson.AppendString(
                json,
                "warning",
                "Editor output is development-only and cannot certify a target platform.");
            json.Append('}');
            json.Append('}');
            return json.ToString();
        }

        private static void AppendReference(
            StringBuilder json,
            string artifactId,
            string path,
            bool includeLeadingComma)
        {
            if (includeLeadingComma) json.Append(',');
            json.Append('{');
            TelemetryJson.AppendString(json, "artifactId", artifactId, true);
            TelemetryJson.AppendString(json, "path", path);
            TelemetryJson.AppendString(json, "status", "captured");
            json.Append('}');
        }

        private static string Require(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value is required.", paramName);
            return value.Trim();
        }
    }

    public enum GoldenSceneScorecardStatus
    {
        Pass,
        Fail,
        Unavailable
    }

    public sealed class GoldenSceneScorecardField
    {
        internal GoldenSceneScorecardField(
            string section,
            string label,
            GoldenSceneScorecardStatus status,
            string value,
            string evidence,
            string note)
        {
            Section = section;
            Label = label;
            Status = status;
            Value = value ?? string.Empty;
            Evidence = evidence ?? string.Empty;
            Note = note ?? string.Empty;
        }

        public string Section { get; }
        public string Label { get; }
        public GoldenSceneScorecardStatus Status { get; }
        public string Value { get; }
        public string Evidence { get; }
        public string Note { get; }
    }

    public sealed class GoldenSceneScorecardReport
    {
        private static readonly string[] MandatoryUnavailableLabels =
        {
            "Memory and allocation budget",
            "Streaming/residency behavior",
            "LOD/impostor/quality transitions",
            "Primary read and gameplay silhouette",
            "Realm/role/threat identity beyond color",
            "Material distinction without emission",
            "Lighting and navigation clarity",
            "Animation weight/contact/transitions",
            "VFX protected-information contract",
            "UI/HUD hierarchy and central scan path",
            "Phone/tablet/PC composition as required",
            "Minimap/world-map agreement as required",
            "Text/UI scaling and safe areas",
            "Contrast and color-independent state",
            "Reduced motion/shake/flash/VFX",
            "Audio-off/caption semantic parity",
            "Input navigation/remapping/focus",
            "Originality/non-copy review",
            "No placeholder/debug/fallback presentation"
        };

        private GoldenSceneScorecardReport(
            string certificationStatus,
            IList<GoldenSceneScorecardField> fields)
        {
            CertificationStatus = certificationStatus;
            Fields = new ReadOnlyCollection<GoldenSceneScorecardField>(fields.ToArray());
        }

        public string CertificationStatus { get; }
        public IReadOnlyList<GoldenSceneScorecardField> Fields { get; }

        public static GoldenSceneScorecardReport Create(
            GoldenSceneIdentityRecord identity,
            string applicationBuildGuid,
            GoldenSceneCaptureManifest manifest,
            GoldenSceneTelemetryReport telemetry,
            bool isBuiltInRenderPipeline,
            bool requestsTargetPlatformCertification)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));

            var fields = new List<GoldenSceneScorecardField>(64);
            Add(fields, "Identity", "Golden scene", GoldenSceneScorecardStatus.Pass, identity.SceneId);
            Add(fields, "Identity", "Scene revision", GoldenSceneScorecardStatus.Pass, identity.SceneRevision);
            Add(fields, "Identity", "Build ID / commit", GoldenSceneScorecardStatus.Pass,
                identity.BuildId + " / " + identity.SourceCommit,
                "Application.buildGUID=" + (applicationBuildGuid ?? string.Empty));
            Add(fields, "Identity", "Catalog fingerprint", GoldenSceneScorecardStatus.Pass,
                identity.CatalogFingerprint);
            Add(fields, "Identity", "Unity version", GoldenSceneScorecardStatus.Pass,
                identity.UnityVersion);
            Add(fields, "Identity", "Platform / device / OS", GoldenSceneScorecardStatus.Pass,
                identity.Platform + " / " + identity.DeviceModel + " / " + identity.OperatingSystem);
            Add(fields, "Identity", "CPU / GPU / RAM", GoldenSceneScorecardStatus.Pass,
                identity.ProcessorType + " / " + identity.GraphicsDeviceName + " / " +
                identity.SystemMemoryMb.ToString(CultureInfo.InvariantCulture) + " MB");
            Add(fields, "Identity", "Graphics API", GoldenSceneScorecardStatus.Pass,
                identity.GraphicsApi);
            Add(fields, "Identity", "Resolution / render scale / upscaler",
                GoldenSceneScorecardStatus.Unavailable,
                manifest.MediaSettings.Width.ToString(CultureInfo.InvariantCulture) + "x" +
                manifest.MediaSettings.Height.ToString(CultureInfo.InvariantCulture) + " / " +
                identity.RenderScale.ToString("R", CultureInfo.InvariantCulture) + " / unavailable",
                note: "No upscaler identity is exposed by the current Built-in pipeline contract.");
            Add(fields, "Identity", "Quality preset", GoldenSceneScorecardStatus.Pass,
                identity.QualityPresetId + " rev " + identity.QualityPresetRevision);
            Add(fields, "Identity", "Capture date and operator", GoldenSceneScorecardStatus.Pass,
                identity.CapturedAtUtc + " / " + identity.OperatorId);
            Add(fields, "Identity", "Deterministic seed / anchor", GoldenSceneScorecardStatus.Pass,
                identity.Seed.ToString(CultureInfo.InvariantCulture) + " / " + identity.AnchorId,
                identity.ConfigurationFingerprint);
            bool hasStartingState = telemetry.StartDevice.BatteryLevel.HasValue ||
                                    telemetry.StartDevice.TemperatureCelsius.HasValue ||
                                    !string.IsNullOrWhiteSpace(telemetry.StartDevice.ThermalState);
            Add(fields, "Identity", "Thermal/power starting state",
                hasStartingState ? GoldenSceneScorecardStatus.Pass : GoldenSceneScorecardStatus.Unavailable,
                DeviceValue(telemetry.StartDevice));

            TelemetryDistribution delivered = Distribution(
                telemetry,
                GoldenSceneTelemetryMetricIds.DeliveredFrameTime);
            double frameBudget = 1000d / identity.TargetFrameRate;
            Add(fields, "Mandatory objective gates", "Intended frame-rate/frame-time contract",
                delivered == null
                    ? GoldenSceneScorecardStatus.Unavailable
                    : delivered.P95 <= frameBudget
                        ? GoldenSceneScorecardStatus.Pass
                        : GoldenSceneScorecardStatus.Fail,
                delivered == null
                    ? string.Empty
                    : "p95=" + delivered.P95.ToString("R", CultureInfo.InvariantCulture) +
                      "ms; budget=" + frameBudget.ToString("R", CultureInfo.InvariantCulture) + "ms",
                "telemetry.json");
            bool hitchFailure = telemetry.FramePacing.HitchCount > 0 ||
                                telemetry.FramePacing.SevereHitchCount > 0;
            Add(fields, "Mandatory objective gates", "Frame pacing and hitch contract",
                hitchFailure ? GoldenSceneScorecardStatus.Fail : GoldenSceneScorecardStatus.Pass,
                "hitches=" + telemetry.FramePacing.HitchCount.ToString(CultureInfo.InvariantCulture) +
                "; severe=" + telemetry.FramePacing.SevereHitchCount.ToString(CultureInfo.InvariantCulture),
                "telemetry.json");
            bool sustainedThermal = telemetry.Configuration.MeasurementSeconds >= 1200d &&
                                    telemetry.EndDevice.TemperatureCelsius.HasValue &&
                                    !string.IsNullOrWhiteSpace(telemetry.EndDevice.ThermalState);
            Add(fields, "Mandatory objective gates", "Sustained thermal behavior",
                sustainedThermal ? GoldenSceneScorecardStatus.Pass : GoldenSceneScorecardStatus.Unavailable,
                DeviceValue(telemetry.EndDevice),
                "telemetry.json",
                sustainedThermal ? string.Empty : "The binding gate requires a 20-minute measured soak and thermal data.");
            foreach (string label in MandatoryUnavailableLabels)
            {
                Add(fields, "Mandatory objective gates", label,
                    GoldenSceneScorecardStatus.Unavailable,
                    note: "Requires benchmark review or an accepted numeric budget beyond automated capture.");
            }
            Add(fields, "Mandatory objective gates", "Provenance and rights traceability",
                manifest.ThirdPartyMediaIncluded ||
                !string.Equals(manifest.SourceManifestId,
                    GoldenSceneCapturePolicy.RequiredSourceManifestId,
                    StringComparison.Ordinal)
                    ? GoldenSceneScorecardStatus.Fail
                    : GoldenSceneScorecardStatus.Pass,
                manifest.SourceManifestId,
                "capture manifest");

            AddMetric(fields, telemetry, "CPU frame time (ms)",
                GoldenSceneTelemetryMetricIds.CpuFrameTime);
            AddMetric(fields, telemetry, "GPU frame time (ms)",
                GoldenSceneTelemetryMetricIds.GpuFrameTime);
            AddMetric(fields, telemetry, "Delivered frame time (ms)",
                GoldenSceneTelemetryMetricIds.DeliveredFrameTime);
            Add(fields, "Performance record", "Input-to-visible response (ms)",
                GoldenSceneScorecardStatus.Unavailable,
                note: "No input marker facility is registered for this run.");
            Add(fields, "Performance record", "Gameplay hitches",
                hitchFailure ? GoldenSceneScorecardStatus.Fail : GoldenSceneScorecardStatus.Pass,
                telemetry.Hitches.Count.ToString(CultureInfo.InvariantCulture),
                "telemetry.json");
            AddMetric(fields, telemetry, "System memory", GoldenSceneTelemetryMetricIds.SystemUsedMemory);
            AddMetric(fields, telemetry, "Unity memory", GoldenSceneTelemetryMetricIds.UnityUsedMemory);
            AddMetric(fields, telemetry, "Graphics memory estimate", GoldenSceneTelemetryMetricIds.GraphicsUsedMemory);
            AddCombinedMetric(fields, telemetry, "Allocations / GC",
                GoldenSceneTelemetryMetricIds.ManagedAllocatedInFrame,
                GoldenSceneTelemetryMetricIds.NativeAllocationCount,
                GoldenSceneTelemetryMetricIds.GarbageCollectionCount);

            AddCombinedMetric(fields, telemetry, "Draw calls / batches",
                GoldenSceneTelemetryMetricIds.DrawCalls, GoldenSceneTelemetryMetricIds.Batches);
            AddCombinedMetric(fields, telemetry, "Triangles / vertices",
                GoldenSceneTelemetryMetricIds.Triangles, GoldenSceneTelemetryMetricIds.Vertices);
            AddCombinedMetric(fields, telemetry, "Active full/fallback/nameplate actors",
                GoldenSceneTelemetryMetricIds.FullActors,
                GoldenSceneTelemetryMetricIds.FallbackActors,
                GoldenSceneTelemetryMetricIds.NameplateActors);
            AddCombinedMetric(fields, telemetry, "Particle/VFX counts by source",
                GoldenSceneTelemetryMetricIds.ParticleCount, GoldenSceneTelemetryMetricIds.VfxSources);
            AddCombinedMetric(fields, telemetry, "Texture residency / streaming stalls",
                GoldenSceneTelemetryMetricIds.TextureStreamingRequests,
                GoldenSceneTelemetryMetricIds.TextureStreamingBytes,
                GoldenSceneTelemetryMetricIds.AssetStreamingStalls);
            AddMetric(fields, telemetry, "Shader compilation events",
                GoldenSceneTelemetryMetricIds.ShaderCompilationEvents);
            Add(fields, "Additional fields", "Thermal status/headroom",
                telemetry.EndDevice.TemperatureCelsius.HasValue ||
                !string.IsNullOrWhiteSpace(telemetry.EndDevice.ThermalState)
                    ? GoldenSceneScorecardStatus.Pass
                    : GoldenSceneScorecardStatus.Unavailable,
                DeviceValue(telemetry.EndDevice),
                "telemetry.json");
            Add(fields, "Additional fields", "Battery delta and duration",
                telemetry.BatteryDelta.HasValue
                    ? GoldenSceneScorecardStatus.Pass
                    : GoldenSceneScorecardStatus.Unavailable,
                (telemetry.BatteryDelta?.ToString("R", CultureInfo.InvariantCulture) ?? "unavailable") +
                " / " + telemetry.ActualDurationSeconds.ToString("R", CultureInfo.InvariantCulture) + "s",
                "telemetry.json");
            AddCombinedMetric(fields, telemetry, "Quality-scaling events",
                GoldenSceneTelemetryMetricIds.RenderScale,
                GoldenSceneTelemetryMetricIds.LodBias,
                GoldenSceneTelemetryMetricIds.VfxDensity);
            GoldenSceneScorecardStatus artifactStatus = manifest.Artifacts.Any(artifact =>
                artifact.Status == GoldenSceneArtifactStatus.Error)
                ? GoldenSceneScorecardStatus.Fail
                : manifest.Artifacts.All(artifact => artifact.Status == GoldenSceneArtifactStatus.Captured)
                    ? GoldenSceneScorecardStatus.Pass
                    : GoldenSceneScorecardStatus.Unavailable;
            Add(fields, "Additional fields", "Raw capture paths", artifactStatus,
                string.Join(", ", manifest.Artifacts
                    .Where(artifact => !string.IsNullOrEmpty(artifact.RelativePath))
                    .Select(artifact => artifact.RelativePath)),
                "capture manifest");

            string certificationStatus;
            if (identity.IsEditor)
                certificationStatus = "editor-development-only-not-certifying";
            else if (!requestsTargetPlatformCertification)
                certificationStatus = "player-build-development-evidence-not-certifying";
            else if (manifest.IsComplete && telemetry.IsPlayerBuild && isBuiltInRenderPipeline &&
                     SupportsDeviceCertificationTelemetry(identity, telemetry))
                certificationStatus = "target-platform-evidence-ready-for-review";
            else
                certificationStatus = "target-platform-evidence-incomplete";
            return new GoldenSceneScorecardReport(certificationStatus, fields);
        }

        private static bool SupportsDeviceCertificationTelemetry(
            GoldenSceneIdentityRecord identity,
            GoldenSceneTelemetryReport telemetry)
        {
            string[] requiredMetricIds =
            {
                GoldenSceneTelemetryMetricIds.BatteryLevel,
                GoldenSceneTelemetryMetricIds.DeviceTemperature,
                GoldenSceneTelemetryMetricIds.DeviceThermalState
            };
            if (string.Equals(identity.Platform, "WindowsPlayer", StringComparison.Ordinal))
            {
                return requiredMetricIds.All(metricId => telemetry.Capabilities.Any(capability =>
                    string.Equals(capability.MetricId, metricId, StringComparison.Ordinal) &&
                    ((capability.Status == TelemetryCapabilityStatus.Supported &&
                      capability.SampleCount > 0) ||
                     (capability.Status == TelemetryCapabilityStatus.Unsupported &&
                      capability.SampleCount == 0 &&
                      !string.IsNullOrWhiteSpace(capability.Reason)))));
            }
            return requiredMetricIds.All(metricId => telemetry.Capabilities.Any(capability =>
                string.Equals(capability.MetricId, metricId, StringComparison.Ordinal) &&
                capability.Status == TelemetryCapabilityStatus.Supported &&
                capability.SampleCount > 0));
        }

        public string ToJson()
        {
            var json = new StringBuilder(16384);
            json.Append('{');
            TelemetryJson.AppendString(json, "schemaVersion", "1.0.0", true);
            TelemetryJson.AppendString(json, "certificationStatus", CertificationStatus);
            TelemetryJson.AppendString(json, "statusSemantics",
                "pass=validated evidence or accepted automated gate; fail=contradiction or failed gate; unavailable=not collected or requires review");
            TelemetryJson.Prefix(json, "fields");
            json.Append('[');
            for (int index = 0; index < Fields.Count; index++)
            {
                if (index > 0) json.Append(',');
                GoldenSceneScorecardField field = Fields[index];
                json.Append('{');
                TelemetryJson.AppendString(json, "section", field.Section, true);
                TelemetryJson.AppendString(json, "label", field.Label);
                TelemetryJson.AppendString(json, "status", field.Status.ToString().ToLowerInvariant());
                TelemetryJson.AppendString(json, "value", field.Value);
                TelemetryJson.AppendString(json, "evidence", field.Evidence);
                TelemetryJson.AppendString(json, "note", field.Note);
                json.Append('}');
            }
            json.Append(']');
            json.Append('}');
            return json.ToString();
        }

        public string ToMarkdown()
        {
            var text = new StringBuilder(16384);
            text.AppendLine("# Post-MVP Golden Scene Scorecard — generated evidence map");
            text.AppendLine();
            text.AppendLine("Certification status: `" + CertificationStatus + "`");
            text.AppendLine();
            text.AppendLine("Editor output is development-only and cannot certify a target platform.");
            text.AppendLine();
            string section = string.Empty;
            foreach (GoldenSceneScorecardField field in Fields)
            {
                if (!string.Equals(section, field.Section, StringComparison.Ordinal))
                {
                    section = field.Section;
                    text.AppendLine("## " + section);
                    text.AppendLine();
                    text.AppendLine("| Field | Status | Value | Evidence | Notes |");
                    text.AppendLine("| --- | --- | --- | --- | --- |");
                }
                text.Append("| ").Append(EscapeMarkdown(field.Label)).Append(" | ")
                    .Append(field.Status.ToString().ToUpperInvariant()).Append(" | ")
                    .Append(EscapeMarkdown(field.Value)).Append(" | ")
                    .Append(EscapeMarkdown(field.Evidence)).Append(" | ")
                    .Append(EscapeMarkdown(field.Note)).AppendLine(" |");
            }
            return text.ToString();
        }

        private static string DeviceValue(GoldenSceneDeviceSnapshot device)
        {
            return "battery=" + (device.BatteryLevel?.ToString("R", CultureInfo.InvariantCulture) ?? "unavailable") +
                   "; temperatureC=" +
                   (device.TemperatureCelsius?.ToString("R", CultureInfo.InvariantCulture) ?? "unavailable") +
                   "; thermal=" + (string.IsNullOrWhiteSpace(device.ThermalState)
                       ? "unavailable"
                       : device.ThermalState);
        }

        private static void AddMetric(
            ICollection<GoldenSceneScorecardField> fields,
            GoldenSceneTelemetryReport telemetry,
            string label,
            string metricId)
        {
            TelemetryDistribution distribution = Distribution(telemetry, metricId);
            TelemetryCapability capability = telemetry.Capabilities.FirstOrDefault(item =>
                string.Equals(item.MetricId, metricId, StringComparison.Ordinal));
            GoldenSceneScorecardStatus status = distribution != null
                ? GoldenSceneScorecardStatus.Pass
                : capability != null && capability.Status == TelemetryCapabilityStatus.Error
                    ? GoldenSceneScorecardStatus.Fail
                    : GoldenSceneScorecardStatus.Unavailable;
            string value = distribution == null
                ? capability?.Reason ?? string.Empty
                : "p50=" + distribution.P50.ToString("R", CultureInfo.InvariantCulture) +
                  "; p90=" + distribution.P90.ToString("R", CultureInfo.InvariantCulture) +
                  "; p95=" + distribution.P95.ToString("R", CultureInfo.InvariantCulture) +
                  "; p99=" + distribution.P99.ToString("R", CultureInfo.InvariantCulture) +
                  "; peak=" + distribution.Maximum.ToString("R", CultureInfo.InvariantCulture);
            Add(fields, "Performance record", label, status, value, "telemetry.json");
        }

        private static void AddCombinedMetric(
            ICollection<GoldenSceneScorecardField> fields,
            GoldenSceneTelemetryReport telemetry,
            string label,
            params string[] metricIds)
        {
            var values = new List<string>();
            bool error = false;
            foreach (string metricId in metricIds)
            {
                TelemetryDistribution distribution = Distribution(telemetry, metricId);
                if (distribution != null)
                {
                    values.Add(metricId + "=" + distribution.Maximum.ToString("R", CultureInfo.InvariantCulture));
                    continue;
                }
                TelemetryCapability capability = telemetry.Capabilities.FirstOrDefault(item =>
                    string.Equals(item.MetricId, metricId, StringComparison.Ordinal));
                if (capability != null && capability.Status == TelemetryCapabilityStatus.Error) error = true;
            }
            Add(fields, "Additional fields", label,
                error
                    ? GoldenSceneScorecardStatus.Fail
                    : values.Count > 0
                        ? GoldenSceneScorecardStatus.Pass
                        : GoldenSceneScorecardStatus.Unavailable,
                string.Join("; ", values),
                "telemetry.json");
        }

        private static TelemetryDistribution Distribution(
            GoldenSceneTelemetryReport telemetry,
            string metricId)
        {
            return telemetry.MetricSummaries.TryGetValue(
                metricId,
                out TelemetryMetricSummary summary)
                ? summary.Distribution
                : null;
        }

        private static void Add(
            ICollection<GoldenSceneScorecardField> fields,
            string section,
            string label,
            GoldenSceneScorecardStatus status,
            string value = "",
            string evidence = "",
            string note = "")
        {
            fields.Add(new GoldenSceneScorecardField(section, label, status, value, evidence, note));
        }

        private static string EscapeMarkdown(string value)
        {
            return (value ?? string.Empty)
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }

    public static class GoldenSceneAtomicResultPublisher
    {
        public static string Publish(
            string stagingPackageDirectory,
            string outputRoot,
            string finalDirectoryName,
            IEnumerable<string> requiredFiles)
        {
            if (!GoldenSceneArtifactNaming.IsSafeToken(finalDirectoryName))
                throw new ArgumentException("A path-safe final directory name is required.",
                    nameof(finalDirectoryName));
            string staging = Path.GetFullPath(stagingPackageDirectory ?? string.Empty);
            string root = Path.GetFullPath(outputRoot ?? string.Empty);
            string final = Path.Combine(root, finalDirectoryName);
            string rootPrefix = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!staging.StartsWith(rootPrefix, pathComparison))
                throw new InvalidOperationException(
                    "The staging package must be inside the output root.");
            if (Directory.Exists(final) || File.Exists(final))
                throw new InvalidOperationException("The final result package already exists: " + final);
            if (!Directory.Exists(staging))
                throw new DirectoryNotFoundException(staging);
            foreach (string requiredFile in requiredFiles ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(requiredFile) ||
                    !string.Equals(Path.GetFileName(requiredFile), requiredFile, StringComparison.Ordinal))
                    throw new ArgumentException("Required package files must be relative leaf names.",
                        nameof(requiredFiles));
                string path = Path.Combine(staging, requiredFile);
                if (!File.Exists(path) || new FileInfo(path).Length <= 0)
                    throw new InvalidOperationException("Required package file is missing or empty: " + requiredFile);
            }
            Directory.CreateDirectory(root);
            Directory.Move(staging, final);
            return final;
        }
    }

    public static class GoldenSceneBenchmarkPackageWriter
    {
        public const string IdentityFileName = "runtime-identity.json";
        public const string TelemetryFileName = "telemetry.json";
        public const string CaptureManifestFileName = "capture-manifest.json";
        public const string ScorecardJsonFileName = "scorecard.json";
        public const string ScorecardMarkdownFileName = "scorecard.md";
        public const string ResultFileName = "benchmark-result.json";

        private static readonly string[] RequiredFiles =
        {
            IdentityFileName,
            TelemetryFileName,
            CaptureManifestFileName,
            ScorecardJsonFileName,
            ScorecardMarkdownFileName,
            ResultFileName
        };

        public static string WriteAndPublish(
            string stagingDirectory,
            string outputRoot,
            string finalDirectoryToken,
            GoldenSceneIdentityRecord identity,
            string applicationBuildGuid,
            GoldenSceneCaptureManifest captureManifest,
            GoldenSceneTelemetryReport telemetry,
            GoldenSceneScorecardReport scorecard)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (captureManifest == null) throw new ArgumentNullException(nameof(captureManifest));
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));
            if (scorecard == null) throw new ArgumentNullException(nameof(scorecard));
            string source = Path.GetFullPath(stagingDirectory ?? string.Empty);
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException("Benchmark staging directory does not exist.");

            foreach (GoldenSceneArtifactRecord artifact in captureManifest.Artifacts.Where(
                         artifact => artifact.Status == GoldenSceneArtifactStatus.Captured))
            {
                string artifactPath = Path.Combine(source, artifact.RelativePath);
                var artifactFile = new FileInfo(artifactPath);
                if (!artifactFile.Exists || artifactFile.Length != artifact.ByteSize)
                    throw new InvalidDataException(
                        "Captured artifact is missing or has the wrong size: " + artifact.RelativePath);
                if (!string.Equals(
                        ComputeSha256(artifactPath),
                        artifact.Sha256,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Captured artifact hash does not match the manifest: " + artifact.RelativePath);
            }

            var result = new GoldenSceneBenchmarkResultDocument(
                identity,
                applicationBuildGuid,
                captureManifest,
                telemetry,
                scorecard,
                CaptureManifestFileName,
                ScorecardJsonFileName,
                ScorecardMarkdownFileName);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(source, IdentityFileName), identity.ToJson(), utf8);
            File.WriteAllText(Path.Combine(source, TelemetryFileName), telemetry.ToJson(), utf8);
            File.WriteAllText(
                Path.Combine(source, CaptureManifestFileName),
                captureManifest.ToJson(),
                utf8);
            File.WriteAllText(Path.Combine(source, ScorecardJsonFileName), scorecard.ToJson(), utf8);
            File.WriteAllText(
                Path.Combine(source, ScorecardMarkdownFileName),
                scorecard.ToMarkdown(),
                utf8);
            File.WriteAllText(Path.Combine(source, ResultFileName), result.ToJson(), utf8);

            return GoldenSceneAtomicResultPublisher.Publish(
                source,
                outputRoot,
                finalDirectoryToken,
                RequiredFiles);
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(stream);
                var text = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest)
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }
    }
}
