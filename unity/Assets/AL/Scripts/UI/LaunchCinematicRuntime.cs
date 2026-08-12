using System;
using System.Collections.Generic;

namespace AL.UI
{
    public enum LaunchCinematicState
    {
        Initializing,
        PreparingMedia,
        Playing,
        SkipEligible,
        Completing,
        AwaitingContinue,
        Transitioned,
        Failed,
        Fallback
    }

    public enum LaunchCinematicPlatform
    {
        Desktop,
        Android,
        Ios
    }

    public enum LaunchCinematicDiagnosticSeverity
    {
        Info,
        Error
    }

    public readonly struct LaunchCinematicDiagnostic
    {
        public LaunchCinematicDiagnostic(string code, string message, LaunchCinematicDiagnosticSeverity severity)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "AL-LAUNCH-UNKNOWN" : code;
            Message = message ?? string.Empty;
            Severity = severity;
        }

        public string Code { get; }
        public string Message { get; }
        public LaunchCinematicDiagnosticSeverity Severity { get; }
    }

    public sealed class LaunchCinematicRuntimeRecord
    {
        public const string ExpectedSchema = "al.launch.cinematic.runtime";
        public const int ExpectedVersion = 1;

        public string Schema { get; set; } = ExpectedSchema;
        public int Version { get; set; } = ExpectedVersion;
        public string CinematicId { get; set; } = "launch_omen_01";
        public LaunchCinematicPlatform Platform { get; set; } = LaunchCinematicPlatform.Desktop;
        public string StreamingAssetsPath { get; set; } = string.Empty;
        public string Container { get; set; } = "mp4";
        public string CodecProfile { get; set; } = "h264";
        public int Width { get; set; }
        public int Height { get; set; }
        public int FramesPerSecond { get; set; } = 24;
        public int FrameCount { get; set; }
        public float DurationSeconds { get; set; }
        public long ByteLength { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public float PrepareTimeoutSeconds { get; set; } = 8f;
        public int SkipEligibilityFrame { get; set; } = 120;
        public bool ApprovedForProduction { get; set; }
        public bool ProbeEvidenceApproved { get; set; }
        public bool ReducedMotionFallbackOnly { get; set; }
    }

    public sealed class LaunchCinematicValidationResult
    {
        private LaunchCinematicValidationResult(bool isValid, LaunchCinematicDiagnostic[] diagnostics)
        {
            IsValid = isValid;
            Diagnostics = diagnostics;
        }

        public bool IsValid { get; }
        public IReadOnlyList<LaunchCinematicDiagnostic> Diagnostics { get; }

        public static LaunchCinematicValidationResult Create(bool isValid, List<LaunchCinematicDiagnostic> diagnostics)
        {
            return new LaunchCinematicValidationResult(isValid, diagnostics.ToArray());
        }
    }

    public static class LaunchCinematicRuntimeValidator
    {
        private const int ApprovedFramesPerSecond = 24;
        private const int DesktopWidth = 1920;
        private const int DesktopHeight = 1080;
        private const int AndroidWidth = 1280;
        private const int AndroidHeight = 720;
        private const long DesktopMaximumBytes = 95000000;
        private const long AndroidMaximumBytes = 42000000;

        public static LaunchCinematicValidationResult Validate(
            LaunchCinematicRuntimeRecord record,
            LaunchCinematicPlatform buildPlatform,
            bool releaseBuild)
        {
            var diagnostics = new List<LaunchCinematicDiagnostic>();

            if (record == null)
            {
                diagnostics.Add(Error("AL-LAUNCH-MEDIA-ABSENT", "No approved launch cinematic runtime record is available."));
                return LaunchCinematicValidationResult.Create(false, diagnostics);
            }

            if (!string.Equals(record.Schema, LaunchCinematicRuntimeRecord.ExpectedSchema, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("AL-LAUNCH-SCHEMA", "Launch cinematic runtime schema identity is unsupported."));
            }

            if (record.Version != LaunchCinematicRuntimeRecord.ExpectedVersion)
            {
                diagnostics.Add(Error("AL-LAUNCH-VERSION", "Launch cinematic runtime version is unsupported."));
            }

            if (record.Platform != buildPlatform)
            {
                diagnostics.Add(Error("AL-LAUNCH-PLATFORM", "Launch cinematic runtime record does not match the current build platform."));
            }

            if (string.IsNullOrWhiteSpace(record.CinematicId))
            {
                diagnostics.Add(Error("AL-LAUNCH-ID", "Launch cinematic identity is required."));
            }

            ValidatePath(record.StreamingAssetsPath, diagnostics);
            ValidateMediaShape(record, buildPlatform, diagnostics);

            if (!record.ApprovedForProduction)
            {
                diagnostics.Add(Error("AL-LAUNCH-UNAPPROVED", "Launch cinematic encode is not approved for production."));
            }

            if (!record.ProbeEvidenceApproved)
            {
                diagnostics.Add(Error("AL-LAUNCH-PROBE", "Launch cinematic probe evidence is missing or unapproved."));
            }

            long maximumBytes = IsMobile(buildPlatform)
                ? AndroidMaximumBytes
                : DesktopMaximumBytes;
            if (record.ByteLength <= 0)
            {
                diagnostics.Add(Error("AL-LAUNCH-SIZE", "Launch cinematic byte length must be recorded after approval."));
            }
            else if (record.ByteLength > maximumBytes)
            {
                diagnostics.Add(Error("AL-LAUNCH-SIZE-CAP", "Launch cinematic byte length exceeds the approved platform package cap."));
            }

            if (!IsHexSha256(record.Sha256))
            {
                diagnostics.Add(Error("AL-LAUNCH-HASH", "Launch cinematic SHA-256 must be a lowercase 64-character hex digest."));
            }

            if (record.PrepareTimeoutSeconds <= 0f || record.PrepareTimeoutSeconds > 30f || float.IsNaN(record.PrepareTimeoutSeconds) || float.IsInfinity(record.PrepareTimeoutSeconds))
            {
                diagnostics.Add(Error("AL-LAUNCH-PREPARE-TIMEOUT", "Prepare timeout must be finite and bounded."));
            }

            if (record.SkipEligibilityFrame < 120 ||
                record.FrameCount > 0 && record.SkipEligibilityFrame >= record.FrameCount)
            {
                diagnostics.Add(Error("AL-LAUNCH-SKIP-FRAME", "Skip eligibility must start at frame 120 or later and before the cinematic ends."));
            }

            bool isValid = diagnostics.Count == 0;
            if (!isValid && !releaseBuild)
            {
                diagnostics.Add(new LaunchCinematicDiagnostic(
                    "AL-LAUNCH-FALLBACK-ALLOWED",
                    "Editor/development launch will use the brand/progress fallback because approved cinematic media is unavailable.",
                    LaunchCinematicDiagnosticSeverity.Info));
            }

            return LaunchCinematicValidationResult.Create(isValid, diagnostics);
        }

        private static void ValidatePath(string path, List<LaunchCinematicDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                diagnostics.Add(Error("AL-LAUNCH-PATH", "StreamingAssets path is required."));
                return;
            }

            string normalized = path.Replace('\\', '/');
            string[] segments = normalized.Split('/');
            bool hasDrivePrefix = normalized.Length >= 2 &&
                                  char.IsLetter(normalized[0]) &&
                                  normalized[1] == ':';
            bool hasTraversalSegment = Array.Exists(
                segments,
                segment => string.Equals(segment, "..", StringComparison.Ordinal));
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                hasDrivePrefix ||
                normalized.Contains("://", StringComparison.Ordinal) ||
                hasTraversalSegment)
            {
                diagnostics.Add(Error("AL-LAUNCH-PATH", "StreamingAssets path must be relative and cannot traverse outside the package."));
            }
        }

        private static void ValidateMediaShape(
            LaunchCinematicRuntimeRecord record,
            LaunchCinematicPlatform buildPlatform,
            List<LaunchCinematicDiagnostic> diagnostics)
        {
            if (!string.Equals(record.Container, "mp4", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Error("AL-LAUNCH-CONTAINER", "Launch cinematic container must be mp4."));
            }

            string expectedCodecProfile = IsMobile(buildPlatform)
                ? "h264-main"
                : "h264-high";
            if (!string.Equals(record.CodecProfile, expectedCodecProfile, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Error("AL-LAUNCH-CODEC", "Launch cinematic codec profile does not match the approved platform profile."));
            }

            if (record.Width <= 0 || record.Height <= 0 || record.FramesPerSecond <= 0 || record.FrameCount <= 0)
            {
                diagnostics.Add(Error("AL-LAUNCH-DIMENSIONS", "Launch cinematic dimensions, FPS, and frame count must be positive."));
            }

            int expectedWidth = IsMobile(buildPlatform) ? AndroidWidth : DesktopWidth;
            int expectedHeight = IsMobile(buildPlatform) ? AndroidHeight : DesktopHeight;
            if (record.Width != expectedWidth || record.Height != expectedHeight)
            {
                diagnostics.Add(Error("AL-LAUNCH-RESOLUTION", "Launch cinematic resolution does not match the approved platform container."));
            }

            if (record.FramesPerSecond != ApprovedFramesPerSecond)
            {
                diagnostics.Add(Error("AL-LAUNCH-FPS", "Launch cinematic frame rate must be 24 FPS."));
            }

            if (record.DurationSeconds < 59.5f || record.DurationSeconds > 60.5f || float.IsNaN(record.DurationSeconds) || float.IsInfinity(record.DurationSeconds))
            {
                diagnostics.Add(Error("AL-LAUNCH-DURATION", "Launch cinematic duration must match the approved one-minute runtime envelope."));
            }

            if (record.FramesPerSecond > 0 && record.FrameCount > 0)
            {
                float computedDuration = record.FrameCount / (float)record.FramesPerSecond;
                if (Math.Abs(computedDuration - record.DurationSeconds) > 0.5f)
                {
                    diagnostics.Add(Error("AL-LAUNCH-FRAME-COUNT", "Frame count and FPS do not match the declared duration."));
                }
            }
        }

        private static bool IsHexSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex = c >= '0' && c <= '9' || c >= 'a' && c <= 'f';
                if (!hex)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsMobile(LaunchCinematicPlatform platform)
        {
            return platform == LaunchCinematicPlatform.Android ||
                   platform == LaunchCinematicPlatform.Ios;
        }

        private static LaunchCinematicDiagnostic Error(string code, string message)
        {
            return new LaunchCinematicDiagnostic(code, message, LaunchCinematicDiagnosticSeverity.Error);
        }
    }

    public sealed class LaunchCinematicLifecycle
    {
        private bool _transitioned;

        public LaunchCinematicState State { get; private set; } = LaunchCinematicState.Initializing;
        public string TransitionReason { get; private set; } = string.Empty;
        public int TransitionCount { get; private set; }

        public void MarkPreparing()
        {
            if (!_transitioned)
            {
                State = LaunchCinematicState.PreparingMedia;
            }
        }

        public void MarkPlaying()
        {
            if (!_transitioned)
            {
                State = LaunchCinematicState.Playing;
            }
        }

        public bool TrySkip(int currentFrame, int eligibilityFrame)
        {
            if (_transitioned || currentFrame < eligibilityFrame)
            {
                return false;
            }

            State = LaunchCinematicState.SkipEligible;
            return CompleteOnce("skip");
        }

        public bool FailToFallback(string reason)
        {
            if (_transitioned)
            {
                return false;
            }

            State = LaunchCinematicState.Fallback;
            return CompleteOnce(string.IsNullOrWhiteSpace(reason) ? "fallback" : reason);
        }

        public void MarkFallbackReady(string reason)
        {
            if (_transitioned)
            {
                return;
            }

            TransitionReason = string.IsNullOrWhiteSpace(reason) ? "fallback-ready" : reason;
            State = LaunchCinematicState.Fallback;
        }

        public void MarkAwaitingContinue()
        {
            if (!_transitioned && State == LaunchCinematicState.Fallback)
            {
                State = LaunchCinematicState.AwaitingContinue;
            }
        }

        public bool TryContinue()
        {
            if (_transitioned || State != LaunchCinematicState.AwaitingContinue)
            {
                return false;
            }

            return CompleteOnce("continue");
        }

        public bool CompleteOnce(string reason)
        {
            if (_transitioned)
            {
                return false;
            }

            _transitioned = true;
            TransitionReason = string.IsNullOrWhiteSpace(reason) ? "complete" : reason;
            State = LaunchCinematicState.Transitioned;
            TransitionCount++;
            return true;
        }
    }
}
