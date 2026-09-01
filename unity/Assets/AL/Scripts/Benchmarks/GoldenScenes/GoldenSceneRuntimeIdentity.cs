using System;
using System.Globalization;
using System.Text;
using AL.Benchmarks.GoldenScenes;
using UnityEngine;

namespace AL.Benchmarks.GoldenScenes
{
    public static class GoldenSceneCameraState
    {
        public static void Apply(Camera camera, GoldenSceneSetup setup)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (setup == null) throw new ArgumentNullException(nameof(setup));

            GoldenSceneCameraAnchor anchor = setup.Anchor;
            camera.transform.SetPositionAndRotation(
                ToUnity(anchor.Position),
                Quaternion.Euler(ToUnity(anchor.EulerAngles)));
            camera.orthographic = anchor.IsOrthographic;
            camera.fieldOfView = anchor.FieldOfViewDegrees;
            camera.orthographicSize = anchor.OrthographicSize;
            camera.nearClipPlane = anchor.NearClipMeters;
            camera.farClipPlane = anchor.FarClipMeters;
        }

        private static Vector3 ToUnity(GoldenSceneVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }
    }

    public static class GoldenSceneRuntimeSetup
    {
        public static float AppliedVfxDensity { get; private set; }
        public static float AppliedRenderScale { get; private set; }
        public static bool DynamicResolutionRequested { get; private set; }
        public static string AppliedConfigurationFingerprint { get; private set; } = string.Empty;

        public static void Apply(Camera camera, GoldenSceneSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));

            GoldenSceneCameraState.Apply(camera, setup);
            camera.allowDynamicResolution = true;
            UnityEngine.Random.InitState(setup.Seed);
            GoldenSceneQualityPreset preset = setup.QualityPreset;
            Application.targetFrameRate = preset.TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = preset.ShadowDistanceMeters;
            QualitySettings.lodBias = preset.LodBias;
            QualitySettings.globalTextureMipmapLimit = preset.TextureMipmapLimit;
            QualitySettings.pixelLightCount = preset.PixelLightCount;
            QualitySettings.resolutionScalingFixedDPIFactor = preset.RenderScale;
            ScalableBufferManager.ResizeBuffers(preset.RenderScale, preset.RenderScale);
            AppliedVfxDensity = preset.VfxDensity;
            AppliedRenderScale = preset.RenderScale;
            DynamicResolutionRequested = true;
            AppliedConfigurationFingerprint = setup.ConfigurationFingerprint;
        }
    }

    public sealed class GoldenSceneIdentityRequest
    {
        public GoldenSceneIdentityRequest(
            string buildId,
            string sourceCommit,
            string runId,
            string captureId,
            string capturedAtUtc,
            string operatorId,
            string captureTool,
            string captureToolVersion,
            bool isPlayerBuild,
            double durationSeconds)
        {
            BuildId = buildId ?? string.Empty;
            SourceCommit = sourceCommit ?? string.Empty;
            RunId = runId ?? string.Empty;
            CaptureId = captureId ?? string.Empty;
            CapturedAtUtc = capturedAtUtc ?? string.Empty;
            OperatorId = operatorId ?? string.Empty;
            CaptureTool = captureTool ?? string.Empty;
            CaptureToolVersion = captureToolVersion ?? string.Empty;
            IsPlayerBuild = isPlayerBuild;
            DurationSeconds = durationSeconds;
        }

        public string BuildId { get; }
        public string SourceCommit { get; }
        public string RunId { get; }
        public string CaptureId { get; }
        public string CapturedAtUtc { get; }
        public string OperatorId { get; }
        public string CaptureTool { get; }
        public string CaptureToolVersion { get; }
        public bool IsPlayerBuild { get; }
        public double DurationSeconds { get; }
    }

    public sealed class GoldenSceneRuntimeEnvironment
    {
        public GoldenSceneRuntimeEnvironment(
            string unityVersion,
            string platform,
            string deviceModel,
            string operatingSystem,
            string processorType,
            string graphicsDeviceName,
            int systemMemoryMb,
            int graphicsMemoryMb,
            string graphicsApi,
            string deviceIdentityHash)
        {
            UnityVersion = unityVersion ?? string.Empty;
            Platform = platform ?? string.Empty;
            DeviceModel = deviceModel ?? string.Empty;
            OperatingSystem = operatingSystem ?? string.Empty;
            ProcessorType = processorType ?? string.Empty;
            GraphicsDeviceName = graphicsDeviceName ?? string.Empty;
            SystemMemoryMb = systemMemoryMb;
            GraphicsMemoryMb = graphicsMemoryMb;
            GraphicsApi = graphicsApi ?? string.Empty;
            DeviceIdentityHash = deviceIdentityHash ?? string.Empty;
        }

        public string UnityVersion { get; }
        public string Platform { get; }
        public string DeviceModel { get; }
        public string OperatingSystem { get; }
        public string ProcessorType { get; }
        public string GraphicsDeviceName { get; }
        public int SystemMemoryMb { get; }
        public int GraphicsMemoryMb { get; }
        public string GraphicsApi { get; }
        public string DeviceIdentityHash { get; }
        public bool IsEditor => Platform.EndsWith("Editor", StringComparison.Ordinal);

        public static GoldenSceneRuntimeEnvironment CollectFromUnity()
        {
            string deviceModel = SystemInfo.deviceModel ?? string.Empty;
            string operatingSystem = SystemInfo.operatingSystem ?? string.Empty;
            string processorType = SystemInfo.processorType ?? string.Empty;
            string graphicsDeviceName = SystemInfo.graphicsDeviceName ?? string.Empty;
            string graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            string scopedDeviceIdentity = GoldenSceneHash.ComputeSha256(
                "al-golden-scene-device-v1",
                Application.buildGUID ?? string.Empty,
                deviceModel,
                operatingSystem,
                processorType,
                graphicsDeviceName,
                SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture),
                SystemInfo.graphicsMemorySize.ToString(CultureInfo.InvariantCulture),
                graphicsApi);
            return new GoldenSceneRuntimeEnvironment(
                Application.unityVersion,
                Application.platform.ToString(),
                deviceModel,
                operatingSystem,
                processorType,
                graphicsDeviceName,
                SystemInfo.systemMemorySize,
                SystemInfo.graphicsMemorySize,
                graphicsApi,
                scopedDeviceIdentity);
        }
    }

    public sealed class GoldenSceneIdentityRecord
    {
        internal GoldenSceneIdentityRecord(
            GoldenSceneSetup setup,
            string catalogFingerprint,
            GoldenSceneIdentityRequest request,
            GoldenSceneRuntimeEnvironment environment)
        {
            SchemaVersion = "1.0.0";
            ConfigurationFingerprint = setup.ConfigurationFingerprint;
            CatalogFingerprint = catalogFingerprint;
            BuildId = request.BuildId;
            SourceCommit = request.SourceCommit;
            UnityVersion = environment.UnityVersion;
            Platform = environment.Platform;
            IsPlayerBuild = request.IsPlayerBuild;
            IsEditor = environment.IsEditor;
            DeviceModel = environment.DeviceModel;
            OperatingSystem = environment.OperatingSystem;
            ProcessorType = environment.ProcessorType;
            GraphicsDeviceName = environment.GraphicsDeviceName;
            SystemMemoryMb = environment.SystemMemoryMb;
            GraphicsMemoryMb = environment.GraphicsMemoryMb;
            GraphicsApi = environment.GraphicsApi;
            DeviceIdentityHash = environment.DeviceIdentityHash;
            SceneId = setup.Scene.Id;
            SceneRevision = setup.Scene.Revision;
            ScenarioId = setup.Scene.ScenarioId;
            UnitySceneId = setup.Scene.UnitySceneId;
            UnitySceneName = setup.Scene.UnitySceneName;
            Seed = setup.Seed;
            AnchorId = setup.Anchor.Id;
            AnchorPosition = setup.Anchor.Position;
            AnchorEulerAngles = setup.Anchor.EulerAngles;
            Projection = setup.Anchor.Projection;
            FieldOfViewDegrees = setup.Anchor.FieldOfViewDegrees;
            OrthographicSize = setup.Anchor.OrthographicSize;
            NearClipMeters = setup.Anchor.NearClipMeters;
            FarClipMeters = setup.Anchor.FarClipMeters;
            QualityPresetId = setup.QualityPreset.Id;
            QualityPresetRevision = setup.QualityPreset.Revision;
            TargetFrameRate = setup.QualityPreset.TargetFrameRate;
            RenderScale = setup.QualityPreset.RenderScale;
            ShadowDistanceMeters = setup.QualityPreset.ShadowDistanceMeters;
            LodBias = setup.QualityPreset.LodBias;
            TextureMipmapLimit = setup.QualityPreset.TextureMipmapLimit;
            PixelLightCount = setup.QualityPreset.PixelLightCount;
            VfxDensity = setup.QualityPreset.VfxDensity;
            RunId = request.RunId;
            CaptureId = request.CaptureId;
            CapturedAtUtc = request.CapturedAtUtc;
            OperatorId = request.OperatorId;
            CaptureTool = request.CaptureTool;
            CaptureToolVersion = request.CaptureToolVersion;
            DurationSeconds = request.DurationSeconds;
        }

        public string SchemaVersion { get; }
        public string ConfigurationFingerprint { get; }
        public string CatalogFingerprint { get; }
        public string BuildId { get; }
        public string SourceCommit { get; }
        public string UnityVersion { get; }
        public string Platform { get; }
        public bool IsPlayerBuild { get; }
        public bool IsEditor { get; }
        public string DeviceModel { get; }
        public string OperatingSystem { get; }
        public string ProcessorType { get; }
        public string GraphicsDeviceName { get; }
        public int SystemMemoryMb { get; }
        public int GraphicsMemoryMb { get; }
        public string GraphicsApi { get; }
        public string DeviceIdentityHash { get; }
        public string SceneId { get; }
        public string SceneRevision { get; }
        public string ScenarioId { get; }
        public string UnitySceneId { get; }
        public string UnitySceneName { get; }
        public int Seed { get; }
        public string AnchorId { get; }
        public GoldenSceneVector3 AnchorPosition { get; }
        public GoldenSceneVector3 AnchorEulerAngles { get; }
        public string Projection { get; }
        public float FieldOfViewDegrees { get; }
        public float OrthographicSize { get; }
        public float NearClipMeters { get; }
        public float FarClipMeters { get; }
        public string QualityPresetId { get; }
        public string QualityPresetRevision { get; }
        public int TargetFrameRate { get; }
        public float RenderScale { get; }
        public float ShadowDistanceMeters { get; }
        public float LodBias { get; }
        public int TextureMipmapLimit { get; }
        public int PixelLightCount { get; }
        public float VfxDensity { get; }
        public string RunId { get; }
        public string CaptureId { get; }
        public string CapturedAtUtc { get; }
        public string OperatorId { get; }
        public string CaptureTool { get; }
        public string CaptureToolVersion { get; }
        public double DurationSeconds { get; }

        public string ToJson()
        {
            var json = new StringBuilder(2048);
            json.Append('{');
            Append(json, "schemaVersion", SchemaVersion, true);
            Append(json, "configurationFingerprint", ConfigurationFingerprint);
            Append(json, "catalogFingerprint", CatalogFingerprint);
            Append(json, "buildId", BuildId);
            Append(json, "sourceCommit", SourceCommit);
            Append(json, "unityVersion", UnityVersion);
            Append(json, "platform", Platform);
            Append(json, "isPlayerBuild", IsPlayerBuild);
            Append(json, "isEditor", IsEditor);
            Append(json, "deviceModel", DeviceModel);
            Append(json, "operatingSystem", OperatingSystem);
            Append(json, "processorType", ProcessorType);
            Append(json, "graphicsDeviceName", GraphicsDeviceName);
            Append(json, "systemMemoryMb", SystemMemoryMb);
            Append(json, "graphicsMemoryMb", GraphicsMemoryMb);
            Append(json, "graphicsApi", GraphicsApi);
            Append(json, "deviceIdentityHash", DeviceIdentityHash);
            Append(json, "sceneId", SceneId);
            Append(json, "sceneRevision", SceneRevision);
            Append(json, "scenarioId", ScenarioId);
            Append(json, "unitySceneId", UnitySceneId);
            Append(json, "unitySceneName", UnitySceneName);
            Append(json, "seed", Seed);
            Append(json, "anchorId", AnchorId);
            Append(json, "anchorPosition", AnchorPosition);
            Append(json, "anchorEulerAngles", AnchorEulerAngles);
            Append(json, "projection", Projection);
            Append(json, "fieldOfViewDegrees", FieldOfViewDegrees);
            Append(json, "orthographicSize", OrthographicSize);
            Append(json, "nearClipMeters", NearClipMeters);
            Append(json, "farClipMeters", FarClipMeters);
            Append(json, "qualityPresetId", QualityPresetId);
            Append(json, "qualityPresetRevision", QualityPresetRevision);
            Append(json, "targetFrameRate", TargetFrameRate);
            Append(json, "renderScale", RenderScale);
            Append(json, "shadowDistanceMeters", ShadowDistanceMeters);
            Append(json, "lodBias", LodBias);
            Append(json, "textureMipmapLimit", TextureMipmapLimit);
            Append(json, "pixelLightCount", PixelLightCount);
            Append(json, "vfxDensity", VfxDensity);
            Append(json, "runId", RunId);
            Append(json, "captureId", CaptureId);
            Append(json, "capturedAtUtc", CapturedAtUtc);
            Append(json, "operator", OperatorId);
            Append(json, "captureTool", CaptureTool);
            Append(json, "captureToolVersion", CaptureToolVersion);
            Append(json, "durationSeconds", DurationSeconds);
            json.Append('}');
            return json.ToString();
        }

        private static void Append(
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

        private static void Append(StringBuilder json, string name, bool value)
        {
            Prefix(json, name, false);
            json.Append(value ? "true" : "false");
        }

        private static void Append(StringBuilder json, string name, int value)
        {
            Prefix(json, name, false);
            json.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Append(StringBuilder json, string name, float value)
        {
            Prefix(json, name, false);
            json.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Append(StringBuilder json, string name, double value)
        {
            Prefix(json, name, false);
            json.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void Append(
            StringBuilder json,
            string name,
            GoldenSceneVector3 value)
        {
            Prefix(json, name, false);
            json.Append('[')
                .Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.Z.ToString("R", CultureInfo.InvariantCulture)).Append(']');
        }

        private static void Prefix(StringBuilder json, string name, bool first)
        {
            if (!first) json.Append(',');
            json.Append('"');
            Escape(json, name);
            json.Append("\":");
        }

        private static void Escape(StringBuilder json, string value)
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

    public static class GoldenSceneRuntimeIdentityCollector
    {
        public static bool TryCollect(
            GoldenSceneSetup setup,
            string catalogFingerprint,
            GoldenSceneIdentityRequest request,
            GoldenSceneRuntimeEnvironment environment,
            out GoldenSceneIdentityRecord record,
            out string diagnosticCode)
        {
            record = null;
            if (setup == null)
                return Fail("AL-GS-IDENTITY-SETUP-MISSING", out diagnosticCode);
            if (!IsCanonicalSha256(catalogFingerprint))
                return Fail("AL-GS-IDENTITY-CATALOG-FINGERPRINT-INVALID", out diagnosticCode);
            if (request == null)
                return Fail("AL-GS-IDENTITY-REQUEST-MISSING", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(request.BuildId))
                return Fail("AL-GS-IDENTITY-BUILD-ID-MISSING", out diagnosticCode);
            if (!IsSourceCommit(request.SourceCommit))
                return Fail(string.IsNullOrWhiteSpace(request.SourceCommit)
                    ? "AL-GS-IDENTITY-SOURCE-COMMIT-MISSING"
                    : "AL-GS-IDENTITY-SOURCE-COMMIT-INVALID", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(request.RunId))
                return Fail("AL-GS-IDENTITY-RUN-ID-MISSING", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(request.CaptureId))
                return Fail("AL-GS-IDENTITY-CAPTURE-ID-MISSING", out diagnosticCode);
            if (!TryParseUtc(request.CapturedAtUtc))
                return Fail("AL-GS-IDENTITY-CAPTURE-TIME-INVALID", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(request.OperatorId))
                return Fail("AL-GS-IDENTITY-OPERATOR-MISSING", out diagnosticCode);
            if (string.IsNullOrWhiteSpace(request.CaptureTool) ||
                string.IsNullOrWhiteSpace(request.CaptureToolVersion))
                return Fail("AL-GS-IDENTITY-CAPTURE-TOOL-MISSING", out diagnosticCode);
            if (double.IsNaN(request.DurationSeconds) ||
                double.IsInfinity(request.DurationSeconds) || request.DurationSeconds <= 0d)
                return Fail("AL-GS-IDENTITY-DURATION-INVALID", out diagnosticCode);
            if (environment == null)
                return Fail("AL-GS-IDENTITY-ENVIRONMENT-MISSING", out diagnosticCode);
            if (request.IsPlayerBuild == environment.IsEditor)
                return Fail("AL-GS-IDENTITY-BUILD-PLATFORM-MISMATCH", out diagnosticCode);
            if (!CompleteEnvironment(environment))
                return Fail("AL-GS-IDENTITY-DEVICE-INCOMPLETE", out diagnosticCode);
            if (!IsCanonicalSha256(environment.DeviceIdentityHash))
                return Fail("AL-GS-IDENTITY-DEVICE-IDENTITY-INVALID", out diagnosticCode);

            record = new GoldenSceneIdentityRecord(
                setup, catalogFingerprint, request, environment);
            diagnosticCode = "AL-GS-IDENTITY-READY";
            return true;
        }

        private static bool CompleteEnvironment(GoldenSceneRuntimeEnvironment environment)
        {
            return !string.IsNullOrWhiteSpace(environment.UnityVersion) &&
                   !string.IsNullOrWhiteSpace(environment.Platform) &&
                   !string.IsNullOrWhiteSpace(environment.DeviceModel) &&
                   !string.IsNullOrWhiteSpace(environment.OperatingSystem) &&
                   !string.IsNullOrWhiteSpace(environment.ProcessorType) &&
                   !string.IsNullOrWhiteSpace(environment.GraphicsDeviceName) &&
                   environment.SystemMemoryMb > 0 &&
                   environment.GraphicsMemoryMb >= 0 &&
                   !string.IsNullOrWhiteSpace(environment.GraphicsApi) &&
                   !string.IsNullOrWhiteSpace(environment.DeviceIdentityHash);
        }

        private static bool TryParseUtc(string value)
        {
            return DateTimeOffset.TryParseExact(
                       value,
                       "O",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                       out DateTimeOffset parsed) &&
                   parsed.Offset == TimeSpan.Zero &&
                   value.EndsWith("Z", StringComparison.Ordinal);
        }

        private static bool IsCanonicalSha256(string value)
        {
            return IsLowerHex(value, 64);
        }

        private static bool IsSourceCommit(string value)
        {
            return IsLowerHex(value, 40) || IsLowerHex(value, 64);
        }

        private static bool IsLowerHex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char character in value)
            {
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f')) return false;
            }
            return true;
        }

        private static bool Fail(string code, out string diagnosticCode)
        {
            diagnosticCode = code;
            return false;
        }
    }
}
