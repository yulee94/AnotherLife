using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AL.Benchmarks.GoldenScenes
{
    public enum GoldenSceneArtifactKind
    {
        Still,
        Video,
        Profiler,
        Telemetry,
        Manifest
    }

    public enum GoldenSceneArtifactStatus
    {
        Captured,
        Unsupported,
        Error
    }

    public enum GoldenSceneUiCaptureMode
    {
        Excluded,
        RequiredByBenchmark
    }

    public static class GoldenSceneArtifactNaming
    {
        public static string BuildDirectoryName(GoldenSceneSetup setup, string runId)
        {
            Validate(setup, runId);
            return string.Join(
                "_",
                "scene-" + setup.Scene.Id,
                "seed-" + setup.Seed.ToString(CultureInfo.InvariantCulture),
                "anchor-" + setup.Anchor.Id,
                "run-" + runId);
        }

        public static string BuildFileName(
            GoldenSceneSetup setup,
            string runId,
            GoldenSceneArtifactKind kind,
            string extension)
        {
            string directory = BuildDirectoryName(setup, runId);
            if (!IsSafeToken(extension) || extension.IndexOf('.') >= 0)
                throw new ArgumentException("A safe extension without a leading dot is required.", nameof(extension));
            return directory + "_" + KindName(kind) + "." + extension;
        }

        private static void Validate(GoldenSceneSetup setup, string runId)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (!IsSafeToken(setup.Scene.Id) ||
                !IsSafeToken(setup.Anchor.Id) ||
                !IsSafeToken(runId))
            {
                throw new ArgumentException(
                    "Scene, anchor, and run identifiers must be path-safe stable tokens.");
            }
        }

        internal static bool IsSafeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
            if (!char.IsLetterOrDigit(value[0])) return false;
            foreach (char character in value)
            {
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' && character != '_' && character != '.') return false;
            }
            return true;
        }

        internal static string KindName(GoldenSceneArtifactKind kind)
        {
            return kind.ToString().ToLowerInvariant();
        }
    }

    public sealed class GoldenSceneCaptureMediaSettings
    {
        public GoldenSceneCaptureMediaSettings(
            int width,
            int height,
            int videoFrameRate,
            double videoDurationSeconds,
            GoldenSceneUiCaptureMode uiCaptureMode,
            string uiRequirementReference)
        {
            if (width <= 0 || width > 8192) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0 || height > 8192) throw new ArgumentOutOfRangeException(nameof(height));
            if (videoFrameRate <= 0 || videoFrameRate > 240)
                throw new ArgumentOutOfRangeException(nameof(videoFrameRate));
            if (double.IsNaN(videoDurationSeconds) ||
                double.IsInfinity(videoDurationSeconds) ||
                videoDurationSeconds <= 0d || videoDurationSeconds > 3600d)
                throw new ArgumentOutOfRangeException(nameof(videoDurationSeconds));
            if (!Enum.IsDefined(typeof(GoldenSceneUiCaptureMode), uiCaptureMode))
                throw new ArgumentOutOfRangeException(nameof(uiCaptureMode));
            if (uiCaptureMode == GoldenSceneUiCaptureMode.RequiredByBenchmark &&
                string.IsNullOrWhiteSpace(uiRequirementReference))
                throw new ArgumentException(
                    "UI capture requires a benchmark requirement reference.",
                    nameof(uiRequirementReference));

            Width = width;
            Height = height;
            VideoFrameRate = videoFrameRate;
            VideoDurationSeconds = videoDurationSeconds;
            UiCaptureMode = uiCaptureMode;
            UiRequirementReference = uiRequirementReference ?? string.Empty;
        }

        public int Width { get; }
        public int Height { get; }
        public int VideoFrameRate { get; }
        public double VideoDurationSeconds { get; }
        public GoldenSceneUiCaptureMode UiCaptureMode { get; }
        public string UiRequirementReference { get; }
        public bool IncludesUi => UiCaptureMode == GoldenSceneUiCaptureMode.RequiredByBenchmark;

        internal void AppendJson(StringBuilder json)
        {
            json.Append('{');
            TelemetryJson.AppendInteger(json, "width", Width, true);
            TelemetryJson.AppendInteger(json, "height", Height);
            TelemetryJson.AppendString(json, "stillFormat", "png");
            TelemetryJson.AppendInteger(json, "videoFrameRate", VideoFrameRate);
            TelemetryJson.AppendNumber(json, "videoDurationSeconds", VideoDurationSeconds);
            TelemetryJson.AppendString(
                json,
                "uiCaptureMode",
                UiCaptureMode == GoldenSceneUiCaptureMode.Excluded
                    ? "excluded"
                    : "required-by-benchmark");
            TelemetryJson.AppendString(json, "uiRequirementReference", UiRequirementReference);
            json.Append('}');
        }
    }

    public static class GoldenSceneCapturePolicy
    {
        public const string RequiredSourceManifestId =
            "al.postmvp.graphics_benchmark_sources.2026-08-25";

        private static readonly HashSet<string> UiRequiredSceneIds =
            new HashSet<string>(new[] { "GS-01", "GS-04", "GS-05" }, StringComparer.Ordinal);

        public static bool TryValidate(
            GoldenSceneSetup setup,
            GoldenSceneCaptureMediaSettings mediaSettings,
            string sourceManifestId,
            bool thirdPartyMediaIncluded,
            out string diagnosticCode)
        {
            if (setup == null)
                return Fail("AL-GS-CAPTURE-SETUP-MISSING", out diagnosticCode);
            if (mediaSettings == null)
                return Fail("AL-GS-CAPTURE-MEDIA-SETTINGS-MISSING", out diagnosticCode);
            if (!string.Equals(
                    sourceManifestId,
                    RequiredSourceManifestId,
                    StringComparison.Ordinal))
            {
                return Fail("AL-GS-CAPTURE-SOURCE-MANIFEST-MISMATCH", out diagnosticCode);
            }
            if (thirdPartyMediaIncluded)
                return Fail("AL-GS-CAPTURE-THIRD-PARTY-MEDIA-FORBIDDEN", out diagnosticCode);
            if (mediaSettings.IncludesUi && !UiRequiredSceneIds.Contains(setup.Scene.Id))
            {
                return Fail(
                    "AL-GS-CAPTURE-UI-NOT-REQUIRED:" + setup.Scene.Id,
                    out diagnosticCode);
            }

            diagnosticCode = "AL-GS-CAPTURE-POLICY-READY";
            return true;
        }

        private static bool Fail(string code, out string diagnosticCode)
        {
            diagnosticCode = code;
            return false;
        }
    }

    public static class GoldenSceneCameraAnchorVerifier
    {
        private const float PositionTolerance = 0.00001f;
        private const float RotationToleranceDegrees = 0.001f;
        private const float LensTolerance = 0.00001f;

        public static bool Matches(Camera camera, GoldenSceneSetup setup)
        {
            if (camera == null || setup == null) return false;
            GoldenSceneCameraAnchor anchor = setup.Anchor;
            Vector3 expectedPosition = new Vector3(
                anchor.Position.X,
                anchor.Position.Y,
                anchor.Position.Z);
            Quaternion expectedRotation = Quaternion.Euler(
                anchor.EulerAngles.X,
                anchor.EulerAngles.Y,
                anchor.EulerAngles.Z);
            return Vector3.SqrMagnitude(camera.transform.position - expectedPosition) <=
                   PositionTolerance * PositionTolerance &&
                   Quaternion.Angle(camera.transform.rotation, expectedRotation) <=
                   RotationToleranceDegrees &&
                   camera.orthographic == anchor.IsOrthographic &&
                   Mathf.Abs(camera.fieldOfView - anchor.FieldOfViewDegrees) <= LensTolerance &&
                   Mathf.Abs(camera.orthographicSize - anchor.OrthographicSize) <= LensTolerance &&
                   Mathf.Abs(camera.nearClipPlane - anchor.NearClipMeters) <= LensTolerance &&
                   Mathf.Abs(camera.farClipPlane - anchor.FarClipMeters) <= LensTolerance;
        }
    }

    public sealed class GoldenSceneAnchorConsistency
    {
        public GoldenSceneAnchorConsistency(
            int stillCaptureCount,
            int videoFrameCount,
            int driftFailureCount)
        {
            if (stillCaptureCount < 0) throw new ArgumentOutOfRangeException(nameof(stillCaptureCount));
            if (videoFrameCount < 0) throw new ArgumentOutOfRangeException(nameof(videoFrameCount));
            if (driftFailureCount < 0) throw new ArgumentOutOfRangeException(nameof(driftFailureCount));
            StillCaptureCount = stillCaptureCount;
            VideoFrameCount = videoFrameCount;
            DriftFailureCount = driftFailureCount;
        }

        public int StillCaptureCount { get; }
        public int VideoFrameCount { get; }
        public int DriftFailureCount { get; }
        public bool IsConsistent => DriftFailureCount == 0;

        internal void AppendJson(StringBuilder json)
        {
            json.Append('{');
            TelemetryJson.AppendInteger(json, "stillCaptureCount", StillCaptureCount, true);
            TelemetryJson.AppendInteger(json, "videoFrameCount", VideoFrameCount);
            TelemetryJson.AppendInteger(json, "driftFailureCount", DriftFailureCount);
            TelemetryJson.AppendBoolean(json, "isConsistent", IsConsistent);
            json.Append('}');
        }
    }

    public sealed class GoldenSceneArtifactRecord
    {
        private GoldenSceneArtifactRecord(
            GoldenSceneSetup setup,
            string runId,
            GoldenSceneArtifactKind kind,
            GoldenSceneArtifactStatus status,
            string relativePath,
            string format,
            string sha256,
            long byteSize,
            string startedAtUtc,
            string endedAtUtc,
            string diagnosticCode,
            string reason)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (!GoldenSceneArtifactNaming.IsSafeToken(runId))
                throw new ArgumentException("A path-safe stable run ID is required.", nameof(runId));
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("Artifact format is required.", nameof(format));
            GoldenSceneCaptureValidation.RequireUtc(startedAtUtc, nameof(startedAtUtc));
            GoldenSceneCaptureValidation.RequireUtc(endedAtUtc, nameof(endedAtUtc));
            if (GoldenSceneCaptureValidation.ParseUtc(endedAtUtc) <
                GoldenSceneCaptureValidation.ParseUtc(startedAtUtc))
                throw new ArgumentException("Artifact end time cannot precede its start time.");

            if (status == GoldenSceneArtifactStatus.Captured)
            {
                if (!IsLeafPath(relativePath))
                    throw new ArgumentException("Captured artifact path must be a relative leaf name.", nameof(relativePath));
                if (!GoldenSceneCaptureValidation.IsCanonicalSha256(sha256))
                    throw new ArgumentException("Captured artifact requires a canonical SHA-256.", nameof(sha256));
                if (byteSize <= 0) throw new ArgumentOutOfRangeException(nameof(byteSize));
                if (!string.IsNullOrEmpty(diagnosticCode) || !string.IsNullOrEmpty(reason))
                    throw new ArgumentException("Captured artifacts cannot carry failure fields.");
            }
            else
            {
                if (!string.IsNullOrEmpty(relativePath) ||
                    !string.IsNullOrEmpty(sha256) || byteSize != 0)
                    throw new ArgumentException("Unavailable artifacts cannot claim a file, hash, or size.");
                if (string.IsNullOrWhiteSpace(diagnosticCode) || string.IsNullOrWhiteSpace(reason))
                    throw new ArgumentException("Unavailable artifacts require a diagnostic and reason.");
            }

            Kind = kind;
            Status = status;
            SceneId = setup.Scene.Id;
            Seed = setup.Seed;
            AnchorId = setup.Anchor.Id;
            RunId = runId;
            ConfigurationFingerprint = setup.ConfigurationFingerprint;
            RelativePath = relativePath ?? string.Empty;
            Format = format;
            Sha256 = sha256 ?? string.Empty;
            ByteSize = byteSize;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public GoldenSceneArtifactKind Kind { get; }
        public GoldenSceneArtifactStatus Status { get; }
        public string SceneId { get; }
        public int Seed { get; }
        public string AnchorId { get; }
        public string RunId { get; }
        public string ConfigurationFingerprint { get; }
        public string RelativePath { get; }
        public string Format { get; }
        public string Sha256 { get; }
        public long ByteSize { get; }
        public string StartedAtUtc { get; }
        public string EndedAtUtc { get; }
        public string DiagnosticCode { get; }
        public string Reason { get; }

        public static GoldenSceneArtifactRecord Captured(
            GoldenSceneSetup setup,
            string runId,
            GoldenSceneArtifactKind kind,
            string relativePath,
            string format,
            string sha256,
            long byteSize,
            string startedAtUtc,
            string endedAtUtc)
        {
            return new GoldenSceneArtifactRecord(
                setup,
                runId,
                kind,
                GoldenSceneArtifactStatus.Captured,
                relativePath,
                format,
                sha256,
                byteSize,
                startedAtUtc,
                endedAtUtc,
                string.Empty,
                string.Empty);
        }

        public static GoldenSceneArtifactRecord Unavailable(
            GoldenSceneSetup setup,
            string runId,
            GoldenSceneArtifactKind kind,
            GoldenSceneArtifactStatus status,
            string format,
            string diagnosticCode,
            string reason,
            string startedAtUtc,
            string endedAtUtc)
        {
            if (status == GoldenSceneArtifactStatus.Captured)
                throw new ArgumentException(
                    "Use Captured when an artifact file was produced.",
                    nameof(status));
            return new GoldenSceneArtifactRecord(
                setup,
                runId,
                kind,
                status,
                string.Empty,
                format,
                string.Empty,
                0,
                startedAtUtc,
                endedAtUtc,
                diagnosticCode,
                reason);
        }

        public string ToJson()
        {
            var json = new StringBuilder(768);
            AppendJson(json);
            return json.ToString();
        }

        internal void AppendJson(StringBuilder json)
        {
            json.Append('{');
            TelemetryJson.AppendString(
                json,
                "kind",
                GoldenSceneArtifactNaming.KindName(Kind),
                true);
            TelemetryJson.AppendString(json, "status", Status.ToString().ToLowerInvariant());
            TelemetryJson.AppendString(json, "sceneId", SceneId);
            TelemetryJson.AppendInteger(json, "seed", Seed);
            TelemetryJson.AppendString(json, "anchorId", AnchorId);
            TelemetryJson.AppendString(json, "runId", RunId);
            TelemetryJson.AppendString(
                json,
                "configurationFingerprint",
                ConfigurationFingerprint);
            TelemetryJson.AppendString(json, "relativePath", RelativePath);
            TelemetryJson.AppendString(json, "format", Format);
            TelemetryJson.AppendString(json, "sha256", Sha256);
            TelemetryJson.AppendInteger(json, "byteSize", ByteSize);
            TelemetryJson.AppendString(json, "startedAtUtc", StartedAtUtc);
            TelemetryJson.AppendString(json, "endedAtUtc", EndedAtUtc);
            TelemetryJson.AppendString(json, "diagnosticCode", DiagnosticCode);
            TelemetryJson.AppendString(json, "reason", Reason);
            json.Append('}');
        }

        private static bool IsLeafPath(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) &&
                   value.IndexOf('/') < 0 && value.IndexOf('\\') < 0;
        }
    }

    public sealed class GoldenSceneCaptureManifest
    {
        private static readonly GoldenSceneArtifactKind[] RequiredArtifactKinds =
        {
            GoldenSceneArtifactKind.Still,
            GoldenSceneArtifactKind.Video,
            GoldenSceneArtifactKind.Profiler,
            GoldenSceneArtifactKind.Telemetry
        };

        private GoldenSceneCaptureManifest(
            GoldenSceneIdentityRecord identity,
            GoldenSceneCaptureMediaSettings mediaSettings,
            string captureStartedAtUtc,
            string captureEndedAtUtc,
            string sourceManifestId,
            GoldenSceneAnchorConsistency anchorConsistency,
            IList<GoldenSceneArtifactRecord> artifacts)
        {
            Identity = identity;
            MediaSettings = mediaSettings;
            CaptureStartedAtUtc = captureStartedAtUtc;
            CaptureEndedAtUtc = captureEndedAtUtc;
            SourceManifestId = sourceManifestId;
            AnchorConsistency = anchorConsistency;
            Artifacts = new ReadOnlyCollection<GoldenSceneArtifactRecord>(artifacts.ToArray());
        }

        public GoldenSceneIdentityRecord Identity { get; }
        public GoldenSceneCaptureMediaSettings MediaSettings { get; }
        public string CaptureStartedAtUtc { get; }
        public string CaptureEndedAtUtc { get; }
        public string SourceManifestId { get; }
        public bool ThirdPartyMediaIncluded => false;
        public GoldenSceneAnchorConsistency AnchorConsistency { get; }
        public IReadOnlyList<GoldenSceneArtifactRecord> Artifacts { get; }
        public bool HasAllRequiredArtifacts =>
            Artifacts.Count == RequiredArtifactKinds.Length &&
            RequiredArtifactKinds.All(kind =>
                Artifacts.Count(artifact => artifact.Kind == kind) == 1);
        public int RequiredVideoFrameCount => (int)Math.Ceiling(
            MediaSettings.VideoFrameRate * MediaSettings.VideoDurationSeconds);
        public bool DurationRequirementMet
        {
            get
            {
                GoldenSceneArtifactRecord video = Artifacts.FirstOrDefault(artifact =>
                    artifact.Kind == GoldenSceneArtifactKind.Video &&
                    artifact.Status == GoldenSceneArtifactStatus.Captured);
                return video != null &&
                       (GoldenSceneCaptureValidation.ParseUtc(video.EndedAtUtc) -
                        GoldenSceneCaptureValidation.ParseUtc(video.StartedAtUtc)).TotalSeconds >=
                       MediaSettings.VideoDurationSeconds;
            }
        }
        public bool VideoFrameRequirementMet =>
            AnchorConsistency.VideoFrameCount >= RequiredVideoFrameCount;
        public bool IsComplete =>
            DurationRequirementMet &&
            VideoFrameRequirementMet &&
            AnchorConsistency.IsConsistent &&
            HasAllRequiredArtifacts &&
            Artifacts.All(artifact => artifact.Status == GoldenSceneArtifactStatus.Captured);

        public static bool TryCreate(
            GoldenSceneIdentityRecord identity,
            GoldenSceneSetup setup,
            GoldenSceneCaptureMediaSettings mediaSettings,
            string captureStartedAtUtc,
            string captureEndedAtUtc,
            string sourceManifestId,
            bool thirdPartyMediaIncluded,
            GoldenSceneAnchorConsistency anchorConsistency,
            IEnumerable<GoldenSceneArtifactRecord> artifacts,
            out GoldenSceneCaptureManifest manifest,
            out string diagnosticCode)
        {
            manifest = null;
            if (identity == null)
                return Fail("AL-GS-CAPTURE-IDENTITY-MISSING", out diagnosticCode);
            if (!GoldenSceneCapturePolicy.TryValidate(
                    setup,
                    mediaSettings,
                    sourceManifestId,
                    thirdPartyMediaIncluded,
                    out diagnosticCode)) return false;
            if (!Matches(identity, setup))
                return Fail("AL-GS-CAPTURE-IDENTITY-SETUP-MISMATCH", out diagnosticCode);
            if (anchorConsistency == null)
                return Fail("AL-GS-CAPTURE-ANCHOR-RECORD-MISSING", out diagnosticCode);
            if (!GoldenSceneCaptureValidation.TryUtcRange(
                    captureStartedAtUtc,
                    captureEndedAtUtc))
                return Fail("AL-GS-CAPTURE-TIME-INVALID", out diagnosticCode);

            var artifactList = artifacts == null
                ? new List<GoldenSceneArtifactRecord>()
                : artifacts.ToList();
            if (artifactList.Count == 0 || artifactList.Any(artifact => artifact == null))
                return Fail("AL-GS-CAPTURE-ARTIFACTS-MISSING", out diagnosticCode);
            foreach (GoldenSceneArtifactRecord artifact in artifactList)
            {
                if (!string.Equals(artifact.SceneId, setup.Scene.Id, StringComparison.Ordinal) ||
                    artifact.Seed != setup.Seed ||
                    !string.Equals(artifact.AnchorId, setup.Anchor.Id, StringComparison.Ordinal) ||
                    !string.Equals(artifact.RunId, identity.RunId, StringComparison.Ordinal) ||
                    !string.Equals(
                        artifact.ConfigurationFingerprint,
                        setup.ConfigurationFingerprint,
                        StringComparison.Ordinal))
                {
                    return Fail("AL-GS-CAPTURE-ARTIFACT-LINKAGE-MISMATCH", out diagnosticCode);
                }
            }

            manifest = new GoldenSceneCaptureManifest(
                identity,
                mediaSettings,
                captureStartedAtUtc,
                captureEndedAtUtc,
                sourceManifestId,
                anchorConsistency,
                artifactList);
            diagnosticCode = manifest.IsComplete
                ? "AL-GS-CAPTURE-MANIFEST-COMPLETE"
                : "AL-GS-CAPTURE-MANIFEST-FAILURES-RECORDED";
            return true;
        }

        public string ToJson()
        {
            var json = new StringBuilder(8192);
            json.Append('{');
            TelemetryJson.AppendString(json, "schemaVersion", "1.0.0", true);
            TelemetryJson.AppendString(json, "runId", Identity.RunId);
            TelemetryJson.AppendString(json, "captureId", Identity.CaptureId);
            TelemetryJson.AppendString(json, "sceneId", Identity.SceneId);
            TelemetryJson.AppendInteger(json, "seed", Identity.Seed);
            TelemetryJson.AppendString(json, "anchorId", Identity.AnchorId);
            TelemetryJson.AppendString(
                json,
                "configurationFingerprint",
                Identity.ConfigurationFingerprint);
            TelemetryJson.AppendString(json, "captureStartedAtUtc", CaptureStartedAtUtc);
            TelemetryJson.AppendString(json, "captureEndedAtUtc", CaptureEndedAtUtc);
            TelemetryJson.AppendNumber(
                json,
                "captureDurationSeconds",
                (GoldenSceneCaptureValidation.ParseUtc(CaptureEndedAtUtc) -
                 GoldenSceneCaptureValidation.ParseUtc(CaptureStartedAtUtc)).TotalSeconds);
            TelemetryJson.AppendBoolean(json, "isComplete", IsComplete);
            TelemetryJson.AppendBoolean(
                json,
                "hasAllRequiredArtifacts",
                HasAllRequiredArtifacts);
            TelemetryJson.AppendBoolean(
                json,
                "durationRequirementMet",
                DurationRequirementMet);
            TelemetryJson.AppendInteger(
                json,
                "requiredVideoFrameCount",
                RequiredVideoFrameCount);
            TelemetryJson.AppendBoolean(
                json,
                "videoFrameRequirementMet",
                VideoFrameRequirementMet);
            TelemetryJson.AppendString(json, "sourceManifestId", SourceManifestId);
            TelemetryJson.AppendBoolean(
                json,
                "thirdPartyMediaIncluded",
                ThirdPartyMediaIncluded);
            TelemetryJson.AppendString(
                json,
                "rightsBoundary",
                "URL-and-observation sources only; no third-party media is imported or committed.");
            TelemetryJson.Prefix(json, "identity");
            json.Append(Identity.ToJson());
            TelemetryJson.Prefix(json, "mediaSettings");
            MediaSettings.AppendJson(json);
            TelemetryJson.Prefix(json, "anchorConsistency");
            AnchorConsistency.AppendJson(json);
            TelemetryJson.Prefix(json, "artifacts");
            json.Append('[');
            for (int index = 0; index < Artifacts.Count; index++)
            {
                if (index > 0) json.Append(',');
                Artifacts[index].AppendJson(json);
            }
            json.Append(']');
            json.Append('}');
            return json.ToString();
        }

        private static bool Matches(GoldenSceneIdentityRecord identity, GoldenSceneSetup setup)
        {
            return setup != null &&
                   string.Equals(identity.SceneId, setup.Scene.Id, StringComparison.Ordinal) &&
                   identity.Seed == setup.Seed &&
                   string.Equals(identity.AnchorId, setup.Anchor.Id, StringComparison.Ordinal) &&
                   string.Equals(
                       identity.ConfigurationFingerprint,
                       setup.ConfigurationFingerprint,
                       StringComparison.Ordinal);
        }

        private static bool Fail(string code, out string diagnosticCode)
        {
            diagnosticCode = code;
            return false;
        }
    }

    internal static class GoldenSceneCaptureValidation
    {
        public static bool IsCanonicalSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            return value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));
        }

        public static bool TryUtcRange(string startedAtUtc, string endedAtUtc)
        {
            return TryParseUtc(startedAtUtc, out DateTimeOffset started) &&
                   TryParseUtc(endedAtUtc, out DateTimeOffset ended) &&
                   ended >= started;
        }

        public static void RequireUtc(string value, string parameterName)
        {
            if (!TryParseUtc(value, out _))
                throw new ArgumentException(
                    "Timestamp must be canonical UTC round-trip format.",
                    parameterName);
        }

        public static DateTimeOffset ParseUtc(string value)
        {
            return DateTimeOffset.ParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        private static bool TryParseUtc(string value, out DateTimeOffset parsed)
        {
            return DateTimeOffset.TryParseExact(
                       value,
                       "O",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                       out parsed) &&
                   parsed.Offset == TimeSpan.Zero &&
                   value.EndsWith("Z", StringComparison.Ordinal);
        }
    }
}
