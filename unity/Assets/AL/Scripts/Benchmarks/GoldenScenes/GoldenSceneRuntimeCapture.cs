using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Profiling;

namespace AL.Benchmarks.GoldenScenes
{
    public interface IGoldenSceneCaptureClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public interface IGoldenSceneStillCaptureFacility
    {
        bool TryCapture(
            string outputPath,
            Camera camera,
            GoldenSceneSetup setup,
            GoldenSceneCaptureMediaSettings mediaSettings,
            out string failureReason);
    }

    public interface IGoldenSceneVideoCaptureFacility
    {
        bool IsSupported { get; }
        string Format { get; }
        string Extension { get; }
        string UnsupportedReason { get; }

        bool TryBegin(
            string outputPath,
            Camera camera,
            GoldenSceneSetup setup,
            GoldenSceneCaptureMediaSettings mediaSettings,
            out string failureReason);

        bool TryCaptureFrame(Camera camera, out string failureReason);
        bool TryEnd(out string failureReason);
    }

    public interface IGoldenSceneProfilerCaptureFacility
    {
        bool IsSupported { get; }
        string Format { get; }
        string Extension { get; }
        string UnsupportedReason { get; }

        bool TryBegin(string outputPath, out string failureReason);
        bool TryEnd(out string failureReason);
    }

    public sealed class GoldenSceneSystemCaptureClock : IGoldenSceneCaptureClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public sealed class GoldenSceneUnsupportedVideoCaptureFacility : IGoldenSceneVideoCaptureFacility
    {
        public GoldenSceneUnsupportedVideoCaptureFacility(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("An unsupported reason is required.", nameof(reason));
            UnsupportedReason = reason;
        }

        public bool IsSupported => false;
        public string Format => "video/mp4";
        public string Extension => "mp4";
        public string UnsupportedReason { get; }

        public bool TryBegin(
            string outputPath,
            Camera camera,
            GoldenSceneSetup setup,
            GoldenSceneCaptureMediaSettings mediaSettings,
            out string failureReason)
        {
            failureReason = UnsupportedReason;
            return false;
        }

        public bool TryCaptureFrame(Camera camera, out string failureReason)
        {
            failureReason = UnsupportedReason;
            return false;
        }

        public bool TryEnd(out string failureReason)
        {
            failureReason = UnsupportedReason;
            return false;
        }
    }

    public sealed class GoldenSceneFfmpegVideoCaptureFacility : IGoldenSceneVideoCaptureFacility
    {
        private const int FinalizationTimeoutMilliseconds = 30000;
        private readonly string executablePath;
        private readonly string windowTitle;
        private readonly bool isWindowsPlayer;
        private System.Diagnostics.Process process;

        public GoldenSceneFfmpegVideoCaptureFacility(
            string executablePath,
            string windowTitle,
            bool isWindowsPlayer)
        {
            this.executablePath = executablePath ?? string.Empty;
            this.windowTitle = windowTitle ?? string.Empty;
            this.isWindowsPlayer = isWindowsPlayer;
        }

        public bool IsSupported =>
            isWindowsPlayer &&
            File.Exists(executablePath) &&
            string.Equals(Path.GetFileName(executablePath), "ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
            IsSafeValue(windowTitle);
        public string Format => "video/mp4";
        public string Extension => "mp4";
        public string UnsupportedReason => IsSupported
            ? string.Empty
            : "Windows Player capture requires an explicit existing ffmpeg.exe path and a safe Player window title.";

        public bool TryBegin(
            string outputPath,
            Camera camera,
            GoldenSceneSetup setup,
            GoldenSceneCaptureMediaSettings mediaSettings,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!IsSupported)
            {
                failureReason = UnsupportedReason;
                return false;
            }
            if (process != null)
            {
                failureReason = "An FFmpeg capture process is already assigned to this facility.";
                return false;
            }
            if (camera == null || setup == null || mediaSettings == null)
            {
                failureReason = "Camera, golden-scene setup, and media settings are required.";
                return false;
            }
            if (!string.Equals(Path.GetExtension(outputPath), ".mp4", StringComparison.OrdinalIgnoreCase) ||
                !IsSafeValue(outputPath))
            {
                failureReason = "FFmpeg output must be a safe MP4 path.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = BuildArguments(outputPath, windowTitle, mediaSettings),
                    WorkingDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true
                };
                process = new System.Diagnostics.Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    process.Dispose();
                    process = null;
                    failureReason = "FFmpeg did not start.";
                    return false;
                }
                if (process.HasExited)
                {
                    failureReason = ProcessFailure(process, "FFmpeg exited during startup.");
                    process.Dispose();
                    process = null;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                process?.Dispose();
                process = null;
                failureReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        public bool TryCaptureFrame(Camera camera, out string failureReason)
        {
            failureReason = string.Empty;
            if (camera == null)
            {
                failureReason = "Capture camera is missing.";
                return false;
            }
            if (process == null)
            {
                failureReason = "FFmpeg capture was not started.";
                return false;
            }
            try
            {
                if (!process.HasExited) return true;
                failureReason = ProcessFailure(process, "FFmpeg exited before capture completed.");
                return false;
            }
            catch (Exception exception)
            {
                failureReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        public bool TryEnd(out string failureReason)
        {
            failureReason = string.Empty;
            if (process == null)
            {
                failureReason = "FFmpeg capture was not started.";
                return false;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("q");
                    process.StandardInput.Flush();
                    if (!process.WaitForExit(FinalizationTimeoutMilliseconds))
                    {
                        process.Kill();
                        process.WaitForExit();
                        failureReason = "FFmpeg did not finalize within 30 seconds and was terminated.";
                        return false;
                    }
                }
                if (process.ExitCode != 0)
                {
                    failureReason = ProcessFailure(process, "FFmpeg returned a non-zero exit code.");
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                process.Dispose();
                process = null;
            }
        }

        internal static string BuildArguments(
            string outputPath,
            string windowTitle,
            GoldenSceneCaptureMediaSettings mediaSettings)
        {
            if (!IsSafeValue(outputPath) || !IsSafeValue(windowTitle))
                throw new ArgumentException("FFmpeg paths and window titles cannot contain quotes or line breaks.");
            if (mediaSettings == null) throw new ArgumentNullException(nameof(mediaSettings));
            string frameRate = mediaSettings.VideoFrameRate.ToString(CultureInfo.InvariantCulture);
            string scale = "scale=" +
                           mediaSettings.Width.ToString(CultureInfo.InvariantCulture) + ":" +
                           mediaSettings.Height.ToString(CultureInfo.InvariantCulture) +
                           ":flags=lanczos,setsar=1";
            return "-hide_banner -loglevel error -y -f gdigrab -framerate " + frameRate +
                   " -draw_mouse 0 -i " + Quote("title=" + windowTitle) +
                   " -vf " + Quote(scale) +
                   " -an -c:v mpeg4 -q:v 3 -pix_fmt yuv420p -movflags +faststart " +
                   Quote(outputPath);
        }

        private static string ProcessFailure(System.Diagnostics.Process value, string fallback)
        {
            string stderr = string.Empty;
            try { stderr = value.StandardError.ReadToEnd().Trim(); }
            catch (Exception) { }
            return string.IsNullOrWhiteSpace(stderr)
                ? fallback + " exitCode=" + value.ExitCode.ToString(CultureInfo.InvariantCulture)
                : stderr;
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        private static bool IsSafeValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOfAny(new[] { '\"', '\r', '\n' }) < 0;
        }
    }

    public sealed class GoldenSceneUnsupportedProfilerCaptureFacility : IGoldenSceneProfilerCaptureFacility
    {
        public GoldenSceneUnsupportedProfilerCaptureFacility(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("An unsupported reason is required.", nameof(reason));
            UnsupportedReason = reason;
        }

        public bool IsSupported => false;
        public string Format => "application/vnd.unity.profiler";
        public string Extension => "raw";
        public string UnsupportedReason { get; }

        public bool TryBegin(string outputPath, out string failureReason)
        {
            failureReason = UnsupportedReason;
            return false;
        }

        public bool TryEnd(out string failureReason)
        {
            failureReason = UnsupportedReason;
            return false;
        }
    }

    public sealed class GoldenSceneNativeProfilerCaptureFacility : IGoldenSceneProfilerCaptureFacility
    {
        private bool active;

        public bool IsSupported => Profiler.supported;
        public string Format => "application/vnd.unity.profiler";
        public string Extension => "raw";
        public string UnsupportedReason =>
            IsSupported
                ? string.Empty
                : "Unity native Profiler binary logging is unavailable in this Player/build configuration.";

        public bool TryBegin(string outputPath, out string failureReason)
        {
            failureReason = string.Empty;
            if (!IsSupported)
            {
                failureReason = UnsupportedReason;
                return false;
            }
            if (active || Profiler.enabled)
            {
                failureReason = "A Unity Profiler capture is already active; this run will not replace it.";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                Profiler.logFile = outputPath;
                Profiler.enableBinaryLog = true;
                Profiler.enabled = true;
                active = Profiler.enabled;
                if (!active)
                {
                    ResetProfiler();
                    failureReason =
                        "Unity rejected native Profiler binary logging in this Player/build configuration.";
                }
                return active;
            }
            catch (Exception exception)
            {
                ResetProfiler();
                failureReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        public bool TryEnd(out string failureReason)
        {
            failureReason = string.Empty;
            if (!active)
            {
                failureReason = "No Unity native Profiler capture is active.";
                return false;
            }

            try
            {
                Profiler.enabled = false;
                active = false;
                Profiler.enableBinaryLog = false;
                Profiler.logFile = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.GetType().Name + ": " + exception.Message;
                ResetProfiler();
                return false;
            }
        }

        private void ResetProfiler()
        {
            active = false;
            try { Profiler.enabled = false; }
            catch (Exception) { }
            try { Profiler.enableBinaryLog = false; }
            catch (Exception) { }
            try { Profiler.logFile = string.Empty; }
            catch (Exception) { }
        }
    }

    public sealed class GoldenScenePngStillCaptureFacility : IGoldenSceneStillCaptureFacility
    {
        private sealed class CanvasState
        {
            public Canvas Canvas;
            public bool Enabled;
            public RenderMode RenderMode;
            public Camera WorldCamera;
            public float PlaneDistance;
        }

        private sealed class CanvasCaptureScope : IDisposable
        {
            private readonly List<CanvasState> states;

            public CanvasCaptureScope(List<CanvasState> states)
            {
                this.states = states;
            }

            public void Dispose()
            {
                RestoreCanvasStates(states);
            }
        }

        internal static IDisposable PrepareCanvases(Camera camera, bool includeUi)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            return new CanvasCaptureScope(CaptureCanvasStates(camera, includeUi));
        }

        public bool TryCapture(
            string outputPath,
            Camera camera,
            GoldenSceneSetup setup,
            GoldenSceneCaptureMediaSettings mediaSettings,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (camera == null)
            {
                failureReason = "Capture camera is missing.";
                return false;
            }
            if (setup == null || mediaSettings == null)
            {
                failureReason = "Capture setup and media settings are required.";
                return false;
            }

            var target = new RenderTexture(
                mediaSettings.Width,
                mediaSettings.Height,
                24,
                RenderTextureFormat.ARGB32)
            {
                antiAliasing = Mathf.Min(4, Mathf.Max(1, QualitySettings.antiAliasing))
            };
            var image = new Texture2D(
                mediaSettings.Width,
                mediaSettings.Height,
                TextureFormat.RGB24,
                false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            IDisposable canvasScope = PrepareCanvases(camera, mediaSettings.IncludesUi);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
                GoldenSceneCameraState.Apply(camera, setup);
                if (!GoldenSceneCameraAnchorVerifier.Matches(camera, setup))
                {
                    failureReason = "Resolved camera anchor could not be applied before still capture.";
                    return false;
                }

                target.Create();
                camera.targetTexture = target;
                RenderTexture.active = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                if (!GoldenSceneCameraAnchorVerifier.Matches(camera, setup))
                {
                    failureReason =
                        "Camera drifted during still capture; the image was discarded rather than certified.";
                    return false;
                }
                image.ReadPixels(
                    new Rect(0f, 0f, mediaSettings.Width, mediaSettings.Height),
                    0,
                    0,
                    false);
                image.Apply(false, false);
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length <= 0)
                {
                    failureReason = "PNG still capture did not produce a non-empty artifact.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                canvasScope.Dispose();
                target.Release();
                DestroyObject(image);
                DestroyObject(target);
                if (!string.IsNullOrEmpty(failureReason) && File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); }
                    catch (Exception) { }
                }
                GoldenSceneCameraState.Apply(camera, setup);
            }
        }

        private static List<CanvasState> CaptureCanvasStates(Camera camera, bool includeUi)
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var states = new List<CanvasState>(canvases.Length);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null) continue;
                states.Add(new CanvasState
                {
                    Canvas = canvas,
                    Enabled = canvas.enabled,
                    RenderMode = canvas.renderMode,
                    WorldCamera = canvas.worldCamera,
                    PlaneDistance = canvas.planeDistance
                });
                if (!includeUi)
                {
                    canvas.enabled = false;
                    continue;
                }
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                }
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Clamp(
                    1f,
                    camera.nearClipPlane + 0.01f,
                    camera.farClipPlane - 0.01f);
            }
            return states;
        }

        private static void RestoreCanvasStates(IEnumerable<CanvasState> states)
        {
            foreach (CanvasState state in states)
            {
                if (state.Canvas == null) continue;
                state.Canvas.renderMode = state.RenderMode;
                state.Canvas.worldCamera = state.WorldCamera;
                state.Canvas.planeDistance = state.PlaneDistance;
                state.Canvas.enabled = state.Enabled;
            }
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }

    public sealed class GoldenSceneRuntimeCaptureSession
    {
        private readonly GoldenSceneSetup setup;
        private readonly GoldenSceneIdentityRecord identity;
        private readonly GoldenSceneCaptureMediaSettings mediaSettings;
        private readonly string outputRoot;
        private readonly IGoldenSceneStillCaptureFacility stillFacility;
        private readonly IGoldenSceneVideoCaptureFacility videoFacility;
        private readonly IGoldenSceneProfilerCaptureFacility profilerFacility;
        private readonly IGoldenSceneCaptureClock clock;
        private readonly List<GoldenSceneArtifactRecord> artifacts =
            new List<GoldenSceneArtifactRecord>();

        private string captureStartedAtUtc;
        private string videoStartedAtUtc;
        private string profilerStartedAtUtc;
        private string videoPath;
        private string profilerPath;
        private bool begun;
        private bool complete;
        private bool videoActive;
        private bool profilerActive;
        private IDisposable videoCanvasScope;
        private string videoFailureReason = string.Empty;
        private string videoFailureCode = string.Empty;
        private int stillCaptureCount;
        private int videoFrameCount;
        private int driftFailureCount;

        public GoldenSceneRuntimeCaptureSession(
            GoldenSceneSetup setup,
            GoldenSceneIdentityRecord identity,
            GoldenSceneCaptureMediaSettings mediaSettings,
            string outputRoot,
            IGoldenSceneStillCaptureFacility stillFacility,
            IGoldenSceneVideoCaptureFacility videoFacility,
            IGoldenSceneProfilerCaptureFacility profilerFacility,
            IGoldenSceneCaptureClock clock)
        {
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            this.identity = identity ?? throw new ArgumentNullException(nameof(identity));
            this.mediaSettings = mediaSettings ?? throw new ArgumentNullException(nameof(mediaSettings));
            this.outputRoot = ValidateOutputRoot(outputRoot);
            this.stillFacility = stillFacility ?? throw new ArgumentNullException(nameof(stillFacility));
            this.videoFacility = videoFacility ?? throw new ArgumentNullException(nameof(videoFacility));
            this.profilerFacility = profilerFacility ?? throw new ArgumentNullException(nameof(profilerFacility));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (!GoldenSceneCapturePolicy.TryValidate(
                    setup,
                    mediaSettings,
                    GoldenSceneCapturePolicy.RequiredSourceManifestId,
                    false,
                    out string policyDiagnostic))
            {
                throw new ArgumentException(policyDiagnostic, nameof(mediaSettings));
            }
            if (!MatchesIdentity())
                throw new ArgumentException(
                    "AL-GS-CAPTURE-IDENTITY-SETUP-MISMATCH",
                    nameof(identity));
        }

        public string OutputDirectory { get; private set; } = string.Empty;
        public string ManifestPath { get; private set; } = string.Empty;
        public bool IsActive => begun && !complete;
        public GoldenSceneSetup Setup => setup;
        public GoldenSceneCaptureMediaSettings MediaSettings => mediaSettings;
        public GoldenSceneCaptureManifest Manifest { get; private set; }

        public void Begin(Camera camera)
        {
            if (begun) throw new InvalidOperationException("Capture session has already begun.");
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            begun = true;
            captureStartedAtUtc = UtcNow();
            OutputDirectory = Path.Combine(
                outputRoot,
                GoldenSceneArtifactNaming.BuildDirectoryName(setup, identity.RunId));
            Directory.CreateDirectory(OutputDirectory);

            GoldenSceneCameraState.Apply(camera, setup);
            if (!GoldenSceneCameraAnchorVerifier.Matches(camera, setup))
                throw new InvalidOperationException("AL-GS-CAPTURE-ANCHOR-APPLY-FAILED");

            BeginProfiler();
            BeginVideo(camera);
            CaptureStill(camera);
        }

        public void CaptureVideoFrame(Camera camera)
        {
            RequireActive();
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            GoldenSceneCameraState.Apply(camera, setup);
            if (!GoldenSceneCameraAnchorVerifier.Matches(camera, setup))
            {
                RecordVideoFailure(
                    "AL-GS-VIDEO-ANCHOR-APPLY-FAILED",
                    "Resolved camera anchor could not be applied before a video frame.");
                driftFailureCount++;
                return;
            }
            if (!videoActive) return;

            videoFrameCount++;
            bool captured;
            string failureReason;
            try
            {
                captured = videoFacility.TryCaptureFrame(camera, out failureReason);
            }
            catch (Exception exception)
            {
                captured = false;
                failureReason = exception.GetType().Name + ": " + exception.Message;
            }
            if (!captured)
            {
                RecordVideoFailure(
                    "AL-GS-VIDEO-FRAME-FAILED",
                    RequiredReason(failureReason, "Video facility failed to capture a frame."));
            }
            if (!GoldenSceneCameraAnchorVerifier.Matches(camera, setup))
            {
                RecordVideoFailure(
                    "AL-GS-VIDEO-CAMERA-DRIFT",
                    "Camera drifted during video capture; the video was discarded rather than certified.");
                driftFailureCount++;
            }
            GoldenSceneCameraState.Apply(camera, setup);
        }

        public GoldenSceneCaptureManifest Complete(
            Camera camera,
            GoldenSceneTelemetryReport telemetryReport)
        {
            RequireActive();
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            EndVideo();
            EndProfiler();
            ExportTelemetry(telemetryReport);
            string endedAtUtc = UtcNow();
            GoldenSceneCameraState.Apply(camera, setup);
            if (!GoldenSceneCameraAnchorVerifier.Matches(camera, setup)) driftFailureCount++;

            var consistency = new GoldenSceneAnchorConsistency(
                stillCaptureCount,
                videoFrameCount,
                driftFailureCount);
            if (!GoldenSceneCaptureManifest.TryCreate(
                    identity,
                    setup,
                    mediaSettings,
                    captureStartedAtUtc,
                    endedAtUtc,
                    GoldenSceneCapturePolicy.RequiredSourceManifestId,
                    false,
                    consistency,
                    artifacts,
                    out GoldenSceneCaptureManifest manifest,
                    out string diagnostic))
            {
                throw new InvalidOperationException(diagnostic);
            }

            Manifest = manifest;
            ManifestPath = Path.Combine(
                OutputDirectory,
                GoldenSceneArtifactNaming.BuildFileName(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Manifest,
                    "json"));
            File.WriteAllText(ManifestPath, Manifest.ToJson(), new System.Text.UTF8Encoding(false));
            complete = true;
            return Manifest;
        }

        private void CaptureStill(Camera camera)
        {
            stillCaptureCount++;
            string startedAtUtc = UtcNow();
            string relativePath = GoldenSceneArtifactNaming.BuildFileName(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Still,
                "png");
            string path = Path.Combine(OutputDirectory, relativePath);
            GoldenSceneCameraState.Apply(camera, setup);
            bool captured;
            string failureReason;
            try
            {
                captured = stillFacility.TryCapture(
                    path,
                    camera,
                    setup,
                    mediaSettings,
                    out failureReason);
            }
            catch (Exception exception)
            {
                captured = false;
                failureReason = exception.GetType().Name + ": " + exception.Message;
            }
            string endedAtUtc = UtcNow();
            if (captured && GoldenSceneCameraAnchorVerifier.Matches(camera, setup) &&
                TryFile(path, out string sha256, out long byteSize))
            {
                artifacts.Add(GoldenSceneArtifactRecord.Captured(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Still,
                    relativePath,
                    "image/png",
                    sha256,
                    byteSize,
                    startedAtUtc,
                    endedAtUtc));
                return;
            }

            if (!GoldenSceneCameraAnchorVerifier.Matches(camera, setup)) driftFailureCount++;
            DeletePartial(path);
            artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Still,
                GoldenSceneArtifactStatus.Error,
                "image/png",
                "AL-GS-STILL-CAPTURE-FAILED",
                RequiredReason(failureReason, "Still capture did not produce a valid PNG artifact."),
                startedAtUtc,
                endedAtUtc));
            GoldenSceneCameraState.Apply(camera, setup);
        }

        private void BeginVideo(Camera camera)
        {
            videoStartedAtUtc = UtcNow();
            if (!videoFacility.IsSupported)
            {
                artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Video,
                    GoldenSceneArtifactStatus.Unsupported,
                    videoFacility.Format,
                    "AL-GS-VIDEO-UNSUPPORTED",
                    RequiredReason(
                        videoFacility.UnsupportedReason,
                        "No supported runtime video facility is installed."),
                    videoStartedAtUtc,
                    videoStartedAtUtc));
                return;
            }

            string relativePath = GoldenSceneArtifactNaming.BuildFileName(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Video,
                videoFacility.Extension);
            videoPath = Path.Combine(OutputDirectory, relativePath);
            string failureReason;
            videoCanvasScope = GoldenScenePngStillCaptureFacility.PrepareCanvases(
                camera,
                mediaSettings.IncludesUi);
            try
            {
                videoActive = videoFacility.TryBegin(
                    videoPath,
                    camera,
                    setup,
                    mediaSettings,
                    out failureReason);
            }
            catch (Exception exception)
            {
                videoActive = false;
                failureReason = exception.GetType().Name + ": " + exception.Message;
            }
            if (!videoActive)
            {
                DisposeVideoCanvasScope();
                artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Video,
                    GoldenSceneArtifactStatus.Error,
                    videoFacility.Format,
                    "AL-GS-VIDEO-START-FAILED",
                    RequiredReason(failureReason, "Runtime video facility failed to start."),
                    videoStartedAtUtc,
                    UtcNow()));
                DeletePartial(videoPath);
            }
        }

        private void EndVideo()
        {
            if (!videoActive) return;
            bool ended;
            string failureReason;
            try
            {
                ended = videoFacility.TryEnd(out failureReason);
            }
            catch (Exception exception)
            {
                ended = false;
                failureReason = exception.GetType().Name + ": " + exception.Message;
            }
            videoActive = false;
            DisposeVideoCanvasScope();
            string endedAtUtc = UtcNow();
            if (!ended)
            {
                RecordVideoFailure(
                    "AL-GS-VIDEO-END-FAILED",
                    RequiredReason(failureReason, "Runtime video facility failed to finalize."));
            }

            if (string.IsNullOrEmpty(videoFailureCode) &&
                TryFile(videoPath, out string sha256, out long byteSize))
            {
                artifacts.Add(GoldenSceneArtifactRecord.Captured(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Video,
                    Path.GetFileName(videoPath),
                    videoFacility.Format,
                    sha256,
                    byteSize,
                    videoStartedAtUtc,
                    endedAtUtc));
                return;
            }

            DeletePartial(videoPath);
            artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Video,
                GoldenSceneArtifactStatus.Error,
                videoFacility.Format,
                string.IsNullOrEmpty(videoFailureCode)
                    ? "AL-GS-VIDEO-ARTIFACT-MISSING"
                    : videoFailureCode,
                RequiredReason(
                    videoFailureReason,
                    "Runtime video facility did not produce a valid artifact."),
                videoStartedAtUtc,
                endedAtUtc));
        }

        private void BeginProfiler()
        {
            profilerStartedAtUtc = UtcNow();
            if (!profilerFacility.IsSupported)
            {
                artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Profiler,
                    GoldenSceneArtifactStatus.Unsupported,
                    profilerFacility.Format,
                    "AL-GS-PROFILER-UNSUPPORTED",
                    RequiredReason(
                        profilerFacility.UnsupportedReason,
                        "Unity native Profiler capture is unsupported."),
                    profilerStartedAtUtc,
                    profilerStartedAtUtc));
                return;
            }

            string relativePath = GoldenSceneArtifactNaming.BuildFileName(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Profiler,
                profilerFacility.Extension);
            profilerPath = Path.Combine(OutputDirectory, relativePath);
            string failureReason;
            try
            {
                profilerActive = profilerFacility.TryBegin(profilerPath, out failureReason);
            }
            catch (Exception exception)
            {
                profilerActive = false;
                failureReason = exception.GetType().Name + ": " + exception.Message;
            }
            if (!profilerActive)
            {
                artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Profiler,
                    GoldenSceneArtifactStatus.Error,
                    profilerFacility.Format,
                    "AL-GS-PROFILER-START-FAILED",
                    RequiredReason(failureReason, "Unity native Profiler capture failed to start."),
                    profilerStartedAtUtc,
                    UtcNow()));
                DeletePartial(profilerPath);
            }
        }

        private void EndProfiler()
        {
            if (!profilerActive) return;
            bool ended;
            string failureReason;
            try
            {
                ended = profilerFacility.TryEnd(out failureReason);
            }
            catch (Exception exception)
            {
                ended = false;
                failureReason = exception.GetType().Name + ": " + exception.Message;
            }
            profilerActive = false;
            string endedAtUtc = UtcNow();
            if (ended && TryFile(profilerPath, out string sha256, out long byteSize))
            {
                artifacts.Add(GoldenSceneArtifactRecord.Captured(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Profiler,
                    Path.GetFileName(profilerPath),
                    profilerFacility.Format,
                    sha256,
                    byteSize,
                    profilerStartedAtUtc,
                    endedAtUtc));
                return;
            }

            DeletePartial(profilerPath);
            artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Profiler,
                GoldenSceneArtifactStatus.Error,
                profilerFacility.Format,
                ended ? "AL-GS-PROFILER-ARTIFACT-MISSING" : "AL-GS-PROFILER-END-FAILED",
                RequiredReason(
                    failureReason,
                    "Unity native Profiler capture did not produce a valid raw artifact."),
                profilerStartedAtUtc,
                endedAtUtc));
        }

        private void ExportTelemetry(GoldenSceneTelemetryReport telemetryReport)
        {
            string startedAtUtc = UtcNow();
            if (telemetryReport == null)
            {
                artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Telemetry,
                    GoldenSceneArtifactStatus.Unsupported,
                    "application/json",
                    "AL-GS-TELEMETRY-NOT-PROVIDED",
                    "No completed project telemetry report was supplied to this capture session.",
                    startedAtUtc,
                    startedAtUtc));
                return;
            }

            string relativePath = GoldenSceneArtifactNaming.BuildFileName(
                setup,
                identity.RunId,
                GoldenSceneArtifactKind.Telemetry,
                "json");
            string path = Path.Combine(OutputDirectory, relativePath);
            try
            {
                File.WriteAllText(path, telemetryReport.ToJson(), new System.Text.UTF8Encoding(false));
                if (!TryFile(path, out string sha256, out long byteSize))
                    throw new IOException("Telemetry artifact is empty or unreadable.");
                artifacts.Add(GoldenSceneArtifactRecord.Captured(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Telemetry,
                    relativePath,
                    "application/json",
                    sha256,
                    byteSize,
                    startedAtUtc,
                    UtcNow()));
            }
            catch (Exception exception)
            {
                DeletePartial(path);
                artifacts.Add(GoldenSceneArtifactRecord.Unavailable(
                    setup,
                    identity.RunId,
                    GoldenSceneArtifactKind.Telemetry,
                    GoldenSceneArtifactStatus.Error,
                    "application/json",
                    "AL-GS-TELEMETRY-EXPORT-FAILED",
                    exception.GetType().Name + ": " + exception.Message,
                    startedAtUtc,
                    UtcNow()));
            }
        }

        private void RecordVideoFailure(string code, string reason)
        {
            if (string.IsNullOrEmpty(videoFailureCode))
            {
                videoFailureCode = code;
                videoFailureReason = reason;
            }
        }

        private void DisposeVideoCanvasScope()
        {
            if (videoCanvasScope == null) return;
            videoCanvasScope.Dispose();
            videoCanvasScope = null;
        }

        private bool MatchesIdentity()
        {
            return string.Equals(identity.SceneId, setup.Scene.Id, StringComparison.Ordinal) &&
                   identity.Seed == setup.Seed &&
                   string.Equals(identity.AnchorId, setup.Anchor.Id, StringComparison.Ordinal) &&
                   string.Equals(
                       identity.ConfigurationFingerprint,
                       setup.ConfigurationFingerprint,
                       StringComparison.Ordinal);
        }

        private void RequireActive()
        {
            if (!begun || complete)
                throw new InvalidOperationException("Capture session is not active.");
        }

        private string UtcNow()
        {
            return clock.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        private static string ValidateOutputRoot(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Capture output root is required.", nameof(value));
            string fullPath = Path.GetFullPath(value);
            string assetsPath = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string candidate = fullPath
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (candidate.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Generated evidence cannot be written under Assets.",
                    nameof(value));
            }
            return fullPath;
        }

        private static bool TryFile(string path, out string sha256, out long byteSize)
        {
            sha256 = string.Empty;
            byteSize = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var info = new FileInfo(path);
            if (info.Length <= 0) return false;
            byteSize = info.Length;
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(stream);
                sha256 = BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
            return GoldenSceneCaptureValidation.IsCanonicalSha256(sha256);
        }

        private static void DeletePartial(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try { File.Delete(path); }
            catch (Exception) { }
        }

        private static string RequiredReason(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }

    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    public sealed class GoldenSceneRuntimeCapture : MonoBehaviour
    {
        private GoldenSceneRuntimeCaptureSession session;
        private Camera captureCamera;
        private double startedAtRealtime;
        private double nextVideoFrameAt;
        private GoldenSceneRuntimeTelemetryCollector telemetryCollector;

        public event Action<GoldenSceneCaptureManifest> ManifestReady;
        public event Action<string> CaptureFailed;

        public bool IsCapturing => session != null && session.IsActive;
        public bool AutoComplete { get; set; } = true;
        public GoldenSceneCaptureManifest LatestManifest { get; private set; }
        public string ManifestPath => session?.ManifestPath ?? string.Empty;
        public static string DefaultOutputRoot =>
            Path.Combine(Application.persistentDataPath, "BenchmarkEvidence");

        public void BeginCapture(
            Camera camera,
            GoldenSceneSetup setup,
            GoldenSceneIdentityRecord identity,
            GoldenSceneCaptureMediaSettings mediaSettings,
            string outputRoot = null,
            IGoldenSceneVideoCaptureFacility videoFacility = null,
            IGoldenSceneProfilerCaptureFacility profilerFacility = null,
            GoldenSceneRuntimeTelemetryCollector telemetry = null)
        {
            if (IsCapturing)
                throw new InvalidOperationException("A golden-scene capture is already active.");
            captureCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            telemetryCollector = telemetry;
            session = new GoldenSceneRuntimeCaptureSession(
                setup,
                identity,
                mediaSettings,
                string.IsNullOrWhiteSpace(outputRoot) ? DefaultOutputRoot : outputRoot,
                new GoldenScenePngStillCaptureFacility(),
                videoFacility ?? new GoldenSceneUnsupportedVideoCaptureFacility(
                    "No licensed runtime video encoder is registered for this Player/platform."),
                profilerFacility ?? new GoldenSceneNativeProfilerCaptureFacility(),
                new GoldenSceneSystemCaptureClock());
            session.Begin(captureCamera);
            startedAtRealtime = Time.realtimeSinceStartupAsDouble;
            nextVideoFrameAt = 0d;
            LatestManifest = null;
        }

        public GoldenSceneCaptureManifest CompleteCapture()
        {
            if (!IsCapturing)
                throw new InvalidOperationException("No golden-scene capture is active.");
            try
            {
                LatestManifest = session.Complete(
                    captureCamera,
                    telemetryCollector == null ? null : telemetryCollector.LatestReport);
                ManifestReady?.Invoke(LatestManifest);
                return LatestManifest;
            }
            catch (Exception exception)
            {
                CaptureFailed?.Invoke(exception.GetType().Name + ": " + exception.Message);
                throw;
            }
        }

        private void LateUpdate()
        {
            if (!IsCapturing) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - startedAtRealtime;
            GoldenSceneCameraState.Apply(captureCamera, session.Setup);
            if (elapsed >= nextVideoFrameAt)
            {
                session.CaptureVideoFrame(captureCamera);
                nextVideoFrameAt += 1d / Math.Max(1, session.MediaSettings.VideoFrameRate);
            }
            if (AutoComplete && elapsed >= session.MediaSettings.VideoDurationSeconds)
                CompleteCapture();
        }

        private void OnDisable()
        {
            if (!IsCapturing || captureCamera == null) return;
            try
            {
                CompleteCapture();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[AL-GS-CAPTURE-FINALIZE-FAILED] " +
                    exception.GetType().Name + ": " + exception.Message);
            }
        }
    }
}
