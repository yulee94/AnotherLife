using System;
using System.Linq;
using System.Reflection;
using AL.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AL.Tests.EditMode
{
    public class LaunchCinematicPlaybackCoordinatorTests
    {
        [Test]
        public void ValidAttemptWaitsForPreparationAndFirstVisibleFrame()
        {
            var coordinator = new LaunchCinematicPlaybackCoordinator();

            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);

            Assert.IsTrue(attempt.Accepted);
            Assert.AreEqual(LaunchCinematicPlaybackState.Preparing, coordinator.State);
            Assert.IsTrue(coordinator.TryMarkPrepared(attempt.Generation));
            Assert.AreEqual(
                LaunchCinematicPlaybackState.AwaitingFirstFrame,
                coordinator.State);
            Assert.IsTrue(coordinator.TryMarkFirstFrameVisible(attempt.Generation, 0));
            Assert.AreEqual(LaunchCinematicPlaybackState.Playing, coordinator.State);
            Assert.AreEqual(0, coordinator.TerminalCount);
        }

        [Test]
        public void ExactSkipBoundaryIsRequiredAndCompletesOnce()
        {
            var coordinator = PlayingCoordinator(out int generation);

            Assert.IsFalse(coordinator.TryAdvanceFrame(generation, 119));
            Assert.IsFalse(coordinator.TrySkip(generation));
            Assert.IsTrue(coordinator.TryAdvanceFrame(generation, 120));
            Assert.AreEqual(LaunchCinematicPlaybackState.SkipEligible, coordinator.State);
            Assert.IsTrue(coordinator.TrySkip(generation));
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.Skipped,
                coordinator.Terminal.Reason);
            Assert.AreEqual(1, coordinator.TerminalCount);

            Assert.IsFalse(coordinator.TryComplete(generation));
            Assert.IsFalse(coordinator.TryFail(generation, "late-error"));
            Assert.IsFalse(coordinator.TrySkip(generation));
            Assert.AreEqual(1, coordinator.TerminalCount);
        }

        [Test]
        public void CompleteErrorAndTimeoutCallbacksAreIdempotent()
        {
            var coordinator = PlayingCoordinator(out int generation);

            Assert.IsTrue(coordinator.TryComplete(generation));
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.Completed,
                coordinator.Terminal.Reason);
            Assert.IsFalse(coordinator.TryFail(generation, "decoder-error"));
            Assert.IsFalse(coordinator.TryPrepareTimedOut(generation, 99f));
            Assert.AreEqual(1, coordinator.TerminalCount);
        }

        [Test]
        public void PrepareTimeoutUsesManifestLimitAndFailsToFallback()
        {
            var coordinator = new LaunchCinematicPlaybackCoordinator();
            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);

            Assert.IsFalse(coordinator.TryPrepareTimedOut(attempt.Generation, 7.999f));
            Assert.IsTrue(coordinator.TryPrepareTimedOut(attempt.Generation, 8f));
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.PrepareTimedOut,
                coordinator.Terminal.Reason);
            Assert.AreEqual(LaunchCinematicPlaybackState.Fallback, coordinator.State);
        }

        [Test]
        public void PlaybackProgressMustAdvanceAndStallUsesManifestLimit()
        {
            var coordinator = PlayingCoordinator(out int generation);

            Assert.IsFalse(
                coordinator.TryObservePlaybackFrame(generation, 0),
                "A duplicate frame is not decoder progress.");
            Assert.IsTrue(coordinator.TryObservePlaybackFrame(generation, 1));
            Assert.IsFalse(coordinator.TryPlaybackStalled(generation, 7.999f));
            Assert.IsTrue(coordinator.TryPlaybackStalled(generation, 8f));
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.PlaybackStalled,
                coordinator.Terminal.Reason);
            Assert.AreEqual("playback-stalled", coordinator.Terminal.Detail);
            Assert.AreEqual(LaunchCinematicPlaybackState.Fallback, coordinator.State);
            Assert.AreEqual(1, coordinator.TerminalCount);

            Assert.IsFalse(coordinator.TryPlaybackStalled(generation, 99f));
            Assert.IsFalse(coordinator.TryObservePlaybackFrame(generation, 2));
            Assert.AreEqual(1, coordinator.TerminalCount);
        }

        [Test]
        public void PlaybackStallFromRetiredGenerationCannotFailReplacement()
        {
            var coordinator = new LaunchCinematicPlaybackCoordinator();
            LaunchCinematicPlaybackAttempt first = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);
            LaunchCinematicPlaybackAttempt replacement = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);

            Assert.IsFalse(coordinator.TryPlaybackStalled(first.Generation, 99f));
            Assert.IsTrue(coordinator.TryMarkPrepared(replacement.Generation));
            Assert.IsTrue(
                coordinator.TryMarkFirstFrameVisible(replacement.Generation, 0));
            Assert.AreEqual(LaunchCinematicPlaybackState.Playing, coordinator.State);
            Assert.AreEqual(0, coordinator.TerminalCount);
        }

        [Test]
        public void ReducedMotionNeverEntersMediaPreparation()
        {
            var coordinator = new LaunchCinematicPlaybackCoordinator();

            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: true);

            Assert.IsFalse(attempt.Accepted);
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.ReducedMotionFallback,
                coordinator.Terminal.Reason);
            Assert.AreEqual(LaunchCinematicPlaybackState.Fallback, coordinator.State);
            Assert.AreEqual(1, coordinator.TerminalCount);
        }

        [Test]
        public void ManifestFallbackOnlyFlagPreventsAutoplay()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.ReducedMotionFallbackOnly = true;
            var coordinator = new LaunchCinematicPlaybackCoordinator();

            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);

            Assert.IsFalse(attempt.Accepted);
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.ManifestFallbackOnly,
                coordinator.Terminal.Reason);
        }

        [Test]
        public void InvalidRecordFallsBackWithoutStartingDecoder()
        {
            var coordinator = new LaunchCinematicPlaybackCoordinator();

            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                null,
                LaunchCinematicPlatform.Android,
                releaseBuild: true,
                reducedMotion: false);

            Assert.IsFalse(attempt.Accepted);
            Assert.IsFalse(attempt.Validation.IsValid);
            Assert.That(
                attempt.Validation.Diagnostics.Select(diagnostic => diagnostic.Code),
                Contains.Item("AL-LAUNCH-MEDIA-ABSENT"));
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.MediaUnavailable,
                coordinator.Terminal.Reason);
        }

        [Test]
        public void ReplacedAttemptMakesEveryOldCallbackInert()
        {
            var coordinator = new LaunchCinematicPlaybackCoordinator();
            LaunchCinematicPlaybackAttempt first = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);
            LaunchCinematicPlaybackAttempt replacement = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);

            Assert.Greater(replacement.Generation, first.Generation);
            Assert.IsFalse(coordinator.TryMarkPrepared(first.Generation));
            Assert.IsFalse(coordinator.TryFail(first.Generation, "stale-error"));
            Assert.IsTrue(coordinator.TryMarkPrepared(replacement.Generation));
            Assert.IsTrue(
                coordinator.TryMarkFirstFrameVisible(replacement.Generation, 120));
            Assert.AreEqual(LaunchCinematicPlaybackState.SkipEligible, coordinator.State);
        }

        [Test]
        public void CallerMutationAfterBeginCannotMoveSkipOrTimeoutBoundaries()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            var coordinator = new LaunchCinematicPlaybackCoordinator();
            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);

            record.SkipEligibilityFrame = 1;
            record.PrepareTimeoutSeconds = 1f;
            record.CinematicId = "mutated_after_validation";
            record.StreamingAssetsPath = "../forged.mp4";
            record.Width = 1;
            record.Height = 1;

            Assert.IsFalse(coordinator.TryPrepareTimedOut(attempt.Generation, 1f));
            Assert.IsTrue(coordinator.TryMarkPrepared(attempt.Generation));
            Assert.IsTrue(
                coordinator.TryMarkFirstFrameVisible(attempt.Generation, 1));
            Assert.AreEqual(LaunchCinematicPlaybackState.Playing, coordinator.State);
            Assert.AreEqual("launch_omen_01", attempt.CinematicId);
            Assert.AreEqual(
                "LaunchCinematic/Desktop/launch_omen_01.mp4",
                attempt.StreamingAssetsPath);
            Assert.AreEqual(1920, attempt.Width);
            Assert.AreEqual(1080, attempt.Height);
        }

        [Test]
        public void BackgroundingOrDisableFailsActiveMediaToFallbackOnce()
        {
            var coordinator = PlayingCoordinator(out int generation);

            Assert.IsTrue(coordinator.TryFail(generation, "application-paused"));
            Assert.AreEqual(
                LaunchCinematicPlaybackTerminalReason.PlaybackFailed,
                coordinator.Terminal.Reason);
            Assert.AreEqual("application-paused", coordinator.Terminal.Detail);
            Assert.IsFalse(coordinator.TryFail(generation, "component-disabled"));
            Assert.AreEqual(1, coordinator.TerminalCount);
        }

        [Test]
        public void HostPublishesRejectedAttemptOnceWithoutStartingPlayback()
        {
            GameObject root = CreateHostObject(out LaunchCinematicVideoPlayerHost host);
            int terminalCount = 0;
            LaunchCinematicPlaybackTerminal terminal = default;
            host.Terminated += value =>
            {
                terminalCount++;
                terminal = value;
            };

            try
            {
                Assert.IsFalse(
                    host.TryBegin(
                        ValidRecord(),
                        LaunchCinematicPlatform.Desktop,
                        releaseBuild: true,
                        reducedMotion: true));
                Assert.AreEqual(1, terminalCount);
                Assert.AreEqual(
                    LaunchCinematicPlaybackTerminalReason.ReducedMotionFallback,
                    terminal.Reason);
                Assert.AreEqual(
                    LaunchCinematicPlaybackState.Fallback,
                    host.State);
                Assert.IsFalse(host.TrySkip());

                InvokeLifecycle(host, "OnDisable");
                Assert.AreEqual(1, terminalCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.AreEqual(1, terminalCount);
        }

        [TestCase("OnApplicationPause", true, "application-paused")]
        [TestCase("OnApplicationFocus", false, "application-focus-lost")]
        [TestCase("OnDisable", null, "component-disabled")]
        [TestCase("OnDestroy", null, "component-destroyed")]
        public void HostLifecycleFailurePublishesOnceAndReleasesOwnedResources(
            string callbackName,
            bool? callbackValue,
            string expectedDetail)
        {
            GameObject root = CreateHostObject(out LaunchCinematicVideoPlayerHost host);
            VideoPlayer player = root.GetComponent<VideoPlayer>();
            RawImage surface = root.GetComponent<RawImage>();
            var renderTexture = new RenderTexture(8, 8, 0);
            int terminalCount = 0;
            LaunchCinematicPlaybackTerminal terminal = default;
            host.Terminated += value =>
            {
                terminalCount++;
                terminal = value;
            };

            try
            {
                SetField(host, "_surface", surface);
                LaunchCinematicPlaybackCoordinator coordinator =
                    GetField<LaunchCinematicPlaybackCoordinator>(host, "_coordinator");
                LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                    ValidRecord(),
                    LaunchCinematicPlatform.Desktop,
                    releaseBuild: true,
                    reducedMotion: false);
                SetField(host, "_activeGeneration", attempt.Generation);
                SetField(host, "_ownedRenderTexture", renderTexture);
                player.targetTexture = renderTexture;
                player.url = "file:///launch-cinematic-test.mp4";
                surface.texture = renderTexture;
                surface.enabled = true;

                if (callbackValue.HasValue)
                {
                    InvokeLifecycle(host, callbackName, callbackValue.Value);
                }
                else
                {
                    InvokeLifecycle(host, callbackName);
                }

                Assert.AreEqual(1, terminalCount);
                Assert.AreEqual(
                    LaunchCinematicPlaybackTerminalReason.PlaybackFailed,
                    terminal.Reason);
                Assert.AreEqual(expectedDetail, terminal.Detail);
                Assert.AreEqual(LaunchCinematicPlaybackState.Fallback, host.State);
                Assert.IsNull(player.targetTexture);
                Assert.AreEqual(string.Empty, player.url);
                Assert.IsNull(surface.texture);
                Assert.IsFalse(surface.enabled);
                Assert.IsTrue(renderTexture == null);

                if (callbackValue.HasValue)
                {
                    InvokeLifecycle(host, callbackName, callbackValue.Value);
                }
                else
                {
                    InvokeLifecycle(host, callbackName);
                }

                Assert.AreEqual(1, terminalCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.AreEqual(1, terminalCount);
        }

        [Test]
        public void HostKeepsFallbackVisibleUntilCurrentFirstFrameArrives()
        {
            GameObject root = CreateHostObject(out LaunchCinematicVideoPlayerHost host);
            VideoPlayer player = root.GetComponent<VideoPlayer>();
            RawImage surface = root.GetComponent<RawImage>();

            try
            {
                SetField(host, "_surface", surface);
                LaunchCinematicPlaybackCoordinator coordinator =
                    GetField<LaunchCinematicPlaybackCoordinator>(host, "_coordinator");
                LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                    ValidRecord(),
                    LaunchCinematicPlatform.Desktop,
                    releaseBuild: true,
                    reducedMotion: false);
                SetField(host, "_activeGeneration", attempt.Generation);

                Assert.IsTrue(
                    (bool)InvokePrivate(host, "TryCreateRenderTarget", attempt));
                Assert.IsNotNull(surface.texture);
                Assert.IsFalse(
                    surface.enabled,
                    "Render-target allocation is not first-frame readiness.");

                Assert.IsTrue(coordinator.TryMarkPrepared(attempt.Generation));
                InvokePrivate(host, "OnFrameReady", player, 0L);

                Assert.AreEqual(
                    LaunchCinematicPlaybackState.Playing,
                    coordinator.State);
                Assert.IsTrue(
                    surface.enabled,
                    "The current attempt's first rendered frame may replace the fallback.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HostPublishesPlaybackStallOnceAndReleasesPlayer()
        {
            GameObject root = CreateHostObject(out LaunchCinematicVideoPlayerHost host);
            int terminalCount = 0;
            LaunchCinematicPlaybackTerminal terminal = default;
            host.Terminated += value =>
            {
                terminalCount++;
                terminal = value;
            };

            try
            {
                LaunchCinematicPlaybackCoordinator coordinator =
                    GetField<LaunchCinematicPlaybackCoordinator>(host, "_coordinator");
                LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                    ValidRecord(),
                    LaunchCinematicPlatform.Desktop,
                    releaseBuild: true,
                    reducedMotion: false);
                Assert.IsTrue(coordinator.TryMarkPrepared(attempt.Generation));
                Assert.IsTrue(
                    coordinator.TryMarkFirstFrameVisible(attempt.Generation, 0));
                SetField(host, "_activeGeneration", attempt.Generation);
                SetField(
                    host,
                    "_lastPlaybackProgressAt",
                    Time.realtimeSinceStartup - 9f);

                InvokeLifecycle(host, "Update");

                Assert.AreEqual(1, terminalCount);
                Assert.AreEqual(
                    LaunchCinematicPlaybackTerminalReason.PlaybackStalled,
                    terminal.Reason);
                Assert.AreEqual("playback-stalled", terminal.Detail);
                Assert.AreEqual(LaunchCinematicPlaybackState.Fallback, host.State);
                Assert.AreEqual(
                    0,
                    GetField<int>(host, "_activeGeneration"));

                InvokeLifecycle(host, "Update");
                Assert.AreEqual(1, terminalCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.AreEqual(1, terminalCount);
        }

        [Test]
        public void HostConvertsSynchronousDecoderExceptionToFallbackOnce()
        {
            GameObject root = CreateHostObject(out LaunchCinematicVideoPlayerHost host);
            int operationCount = 0;
            int terminalCount = 0;
            LaunchCinematicPlaybackTerminal terminal = default;
            host.Terminated += value =>
            {
                terminalCount++;
                terminal = value;
            };

            try
            {
                LaunchCinematicPlaybackCoordinator coordinator =
                    GetField<LaunchCinematicPlaybackCoordinator>(host, "_coordinator");
                LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                    ValidRecord(),
                    LaunchCinematicPlatform.Desktop,
                    releaseBuild: true,
                    reducedMotion: false);
                SetField(host, "_activeGeneration", attempt.Generation);
                Action failingOperation = () =>
                {
                    operationCount++;
                    throw new System.InvalidOperationException("test-only decoder failure");
                };

                bool accepted = false;
                Assert.DoesNotThrow(
                    () => accepted = (bool)InvokePrivate(
                        host,
                        "TryRunDecoderOperation",
                        failingOperation,
                        "decoder-start-failed"));

                Assert.IsFalse(accepted);
                Assert.AreEqual(1, operationCount);
                Assert.AreEqual(1, terminalCount);
                Assert.AreEqual(
                    LaunchCinematicPlaybackTerminalReason.PlaybackFailed,
                    terminal.Reason);
                Assert.AreEqual("decoder-start-failed", terminal.Detail);
                Assert.AreEqual(LaunchCinematicPlaybackState.Fallback, host.State);
                Assert.AreEqual(0, GetField<int>(host, "_activeGeneration"));

                Assert.IsFalse(
                    (bool)InvokePrivate(
                        host,
                        "TryRunDecoderOperation",
                        failingOperation,
                        "late-decoder-failure"));
                Assert.AreEqual(1, operationCount);
                Assert.AreEqual(1, terminalCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.AreEqual(1, terminalCount);
        }

        [Test]
        public void HostCleanupGuardContainsPlatformExceptionAndDoesNotRetry()
        {
            GameObject root = CreateHostObject(out LaunchCinematicVideoPlayerHost host);
            int operationCount = 0;
            Action failingOperation = () =>
            {
                operationCount++;
                throw new System.InvalidOperationException("test-only cleanup failure");
            };

            try
            {
                bool released = true;
                Assert.DoesNotThrow(
                    () => released = (bool)InvokePrivate(
                        host,
                        "TryRunCleanupOperation",
                        failingOperation));

                Assert.IsFalse(released);
                Assert.AreEqual(1, operationCount);
                Assert.IsTrue(
                    (bool)InvokePrivate(
                        host,
                        "TryRunCleanupOperation",
                        new Action(() => operationCount++)));
                Assert.AreEqual(2, operationCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(
            "/Applications/AnotherLife/StreamingAssets",
            "LaunchCinematic/Desktop/launch_omen_01.mp4",
            "/Applications/AnotherLife/StreamingAssets/LaunchCinematic/Desktop/launch_omen_01.mp4")]
        [TestCase(
            "jar:file:///data/app/al.apk!/assets",
            "LaunchCinematic/Android/launch_omen_01.mp4",
            "jar:file:///data/app/al.apk!/assets/LaunchCinematic/Android/launch_omen_01.mp4")]
        public void PackagedMediaPathPreservesPlatformStreamingAssetsRoot(
            string root,
            string relativePath,
            string expected)
        {
            Assert.IsTrue(
                LaunchCinematicMediaPath.TryResolve(
                    root,
                    relativePath,
                    out string actual));
            Assert.AreEqual(expected, actual);
        }

        [TestCase("")]
        [TestCase("../launch.mp4")]
        [TestCase("C:/LaunchCinematic/launch.mp4")]
        [TestCase("https://example.com/launch.mp4")]
        [TestCase("LaunchCinematic/../launch.mp4")]
        public void PackagedMediaPathRejectsMissingOrUntrustedRelativePath(
            string relativePath)
        {
            Assert.IsFalse(
                LaunchCinematicMediaPath.TryResolve(
                    "/Applications/AnotherLife/StreamingAssets",
                    relativePath,
                    out string actual));
            Assert.AreEqual(string.Empty, actual);
        }

        private static LaunchCinematicPlaybackCoordinator PlayingCoordinator(
            out int generation)
        {
            var coordinator = new LaunchCinematicPlaybackCoordinator();
            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: false);
            generation = attempt.Generation;
            Assert.IsTrue(coordinator.TryMarkPrepared(generation));
            Assert.IsTrue(coordinator.TryMarkFirstFrameVisible(generation, 0));
            return coordinator;
        }

        private static GameObject CreateHostObject(
            out LaunchCinematicVideoPlayerHost host)
        {
            var root = new GameObject(
                "LaunchCinematicVideoPlayerHostTests",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(VideoPlayer),
                typeof(AudioSource),
                typeof(LaunchCinematicVideoPlayerHost));
            host = root.GetComponent<LaunchCinematicVideoPlayerHost>();
            return root;
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field " + fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field " + fieldName);
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(
            LaunchCinematicVideoPlayerHost host,
            string methodName,
            params object[] arguments)
        {
            InvokePrivate(host, methodName, arguments);
        }

        private static object InvokePrivate(
            LaunchCinematicVideoPlayerHost host,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = typeof(LaunchCinematicVideoPlayerHost).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing lifecycle callback " + methodName);
            return method.Invoke(host, arguments);
        }

        private static LaunchCinematicRuntimeRecord ValidRecord()
        {
            return new LaunchCinematicRuntimeRecord
            {
                Schema = LaunchCinematicRuntimeRecord.ExpectedSchema,
                Version = LaunchCinematicRuntimeRecord.ExpectedVersion,
                CinematicId = "launch_omen_01",
                Platform = LaunchCinematicPlatform.Desktop,
                StreamingAssetsPath = "LaunchCinematic/Desktop/launch_omen_01.mp4",
                Container = "mp4",
                CodecProfile = "h264-high",
                Width = 1920,
                Height = 1080,
                FramesPerSecond = 24,
                FrameCount = 1440,
                DurationSeconds = 60f,
                ByteLength = 95000000,
                Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                PrepareTimeoutSeconds = 8f,
                SkipEligibilityFrame = 120,
                ApprovedForProduction = true,
                ProbeEvidenceApproved = true,
                ReducedMotionFallbackOnly = false
            };
        }
    }
}
