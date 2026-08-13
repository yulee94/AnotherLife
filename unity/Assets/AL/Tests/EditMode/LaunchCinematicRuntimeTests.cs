using System.Linq;
using AL.UI;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public class LaunchCinematicRuntimeTests
    {
        [Test]
        public void ValidRuntimeRecordPassesForMatchingPlatform()
        {
            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                ValidRecord(),
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true);

            Assert.IsTrue(result.IsValid, string.Join(", ", result.Diagnostics.Select(d => d.Code)));
        }

        [Test]
        public void ValidAndroidRuntimeRecordPassesForMatchingPlatform()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.Platform = LaunchCinematicPlatform.Android;
            record.CodecProfile = "h264-main";
            record.Width = 1280;
            record.Height = 720;
            record.ByteLength = 42000000;

            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                record,
                LaunchCinematicPlatform.Android,
                releaseBuild: true);

            Assert.IsTrue(result.IsValid, string.Join(", ", result.Diagnostics.Select(d => d.Code)));
        }

        [Test]
        public void MissingApprovedEncodeFailsClosed()
        {
            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                null,
                LaunchCinematicPlatform.Android,
                releaseBuild: false);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-MEDIA-ABSENT"));
        }

        [TestCase("../Movies/launch.mp4", "AL-LAUNCH-PATH")]
        [TestCase("/Movies/launch.mp4", "AL-LAUNCH-PATH")]
        [TestCase("file://Movies/launch.mp4", "AL-LAUNCH-PATH")]
        [TestCase("C:/Movies/launch.mp4", "AL-LAUNCH-PATH")]
        [TestCase("LaunchCinematic/../launch.mp4", "AL-LAUNCH-PATH")]
        [TestCase("LaunchCinematic/..", "AL-LAUNCH-PATH")]
        public void TraversalOrAbsolutePathIsRejected(string path, string expectedCode)
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.StreamingAssetsPath = path;

            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item(expectedCode));
        }

        [Test]
        public void BlankIdentityAndNullCodecFailWithoutThrowing()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.CinematicId = " ";
            record.CodecProfile = null;

            LaunchCinematicValidationResult result = null;
            Assert.DoesNotThrow(() =>
                result = LaunchCinematicRuntimeValidator.Validate(
                    record,
                    LaunchCinematicPlatform.Desktop,
                    releaseBuild: true));

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-ID"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-CODEC"));
        }

        [Test]
        public void PlatformResolutionCodecFrameRateAndSizeCapsAreEnforced()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.Platform = LaunchCinematicPlatform.Android;
            record.ByteLength = 42000001;

            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                record,
                LaunchCinematicPlatform.Android,
                releaseBuild: true);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-CODEC"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-RESOLUTION"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-SIZE-CAP"));

            record = ValidRecord();
            record.FramesPerSecond = 30;
            record.FrameCount = 1800;

            result = LaunchCinematicRuntimeValidator.Validate(
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-FPS"));
        }

        [Test]
        public void WrongPlatformUnsupportedVersionAndUnapprovedMediaAreRejected()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.Version = 2;
            record.Platform = LaunchCinematicPlatform.Android;
            record.ApprovedForProduction = false;
            record.ProbeEvidenceApproved = false;

            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-VERSION"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-PLATFORM"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-UNAPPROVED"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-PROBE"));
        }

        [Test]
        public void FrameCountDurationMismatchAndEarlySkipFrameAreRejected()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.FrameCount = 100;
            record.SkipEligibilityFrame = 119;

            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-FRAME-COUNT"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-MASTER-FRAMES"));
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-SKIP-FRAME"));
        }

        [TestCase(1439, 60f)]
        [TestCase(1441, 60f)]
        [TestCase(1440, 59.5f)]
        [TestCase(1440, 60.5f)]
        public void MasterRequiresExactly1440FramesAt24Fps(
            int frameCount,
            float durationSeconds)
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.FrameCount = frameCount;
            record.DurationSeconds = durationSeconds;

            LaunchCinematicValidationResult result =
                LaunchCinematicRuntimeValidator.Validate(
                    record,
                    LaunchCinematicPlatform.Desktop,
                    releaseBuild: true);

            Assert.IsFalse(result.IsValid);
            if (frameCount != 1440)
            {
                Assert.That(
                    result.Diagnostics.Select(d => d.Code),
                    Contains.Item("AL-LAUNCH-MASTER-FRAMES"));
            }

            if (durationSeconds != 60f)
            {
                Assert.That(
                    result.Diagnostics.Select(d => d.Code),
                    Contains.Item("AL-LAUNCH-DURATION"));
            }
        }

        [Test]
        public void SkipEligibilityAtOrAfterEndIsRejected()
        {
            LaunchCinematicRuntimeRecord record = ValidRecord();
            record.SkipEligibilityFrame = record.FrameCount;

            LaunchCinematicValidationResult result = LaunchCinematicRuntimeValidator.Validate(
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true);

            Assert.IsFalse(result.IsValid);
            Assert.That(result.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-SKIP-FRAME"));
        }

        [Test]
        public void MediaTerminalRequiresReadyExplicitContinueAndTransitionsExactlyOnce()
        {
            var lifecycle = new LaunchCinematicLifecycle();

            lifecycle.MarkPreparing();
            lifecycle.MarkPlaying();

            Assert.IsFalse(lifecycle.TrySkip(119, 120));
            Assert.IsTrue(lifecycle.TrySkip(120, 120));
            Assert.AreEqual(LaunchCinematicState.Fallback, lifecycle.State);
            Assert.AreEqual(0, lifecycle.TransitionCount);
            Assert.IsFalse(lifecycle.CompleteOnce("ended"));
            Assert.IsFalse(lifecycle.MarkAwaitingContinue(mandatoryReadinessReady: false));
            Assert.IsTrue(lifecycle.MarkAwaitingContinue(mandatoryReadinessReady: true));
            Assert.IsTrue(lifecycle.TryContinue(mandatoryReadinessReady: true));
            Assert.IsFalse(lifecycle.TryContinue(mandatoryReadinessReady: true));
            Assert.IsFalse(lifecycle.FailToFallback("error"));

            Assert.AreEqual(LaunchCinematicState.Transitioned, lifecycle.State);
            Assert.AreEqual("explicit-continue", lifecycle.TransitionReason);
            Assert.AreEqual(1, lifecycle.TransitionCount);
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
