using System.IO;
using System.Linq;
using AL.UI;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public class LaunchCinematicCatalogTests
    {
        [Test]
        public void PackagedCatalogIsUnapprovedAndForcesReducedMotionFallback()
        {
            string path = LaunchCinematicCatalog.ResolveCatalogPath();
            Assert.IsTrue(File.Exists(path), path);

            Assert.IsTrue(
                LaunchCinematicCatalog.TryLoadForPlatform(
                    LaunchCinematicPlatform.Desktop,
                    out LaunchCinematicRuntimeRecord desktop));
            Assert.IsFalse(desktop.ApprovedForProduction);
            Assert.IsFalse(desktop.ProbeEvidenceApproved);
            Assert.IsTrue(desktop.ReducedMotionFallbackOnly);
            Assert.AreEqual(1920, desktop.Width);
            Assert.AreEqual(1080, desktop.Height);
            Assert.AreEqual(1440, desktop.FrameCount);
            Assert.AreEqual(60f, desktop.DurationSeconds);
            Assert.IsTrue(string.IsNullOrEmpty(desktop.Sha256));

            LaunchCinematicValidationResult validation = LaunchCinematicRuntimeValidator.Validate(
                desktop,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true);
            Assert.IsFalse(validation.IsValid);
            Assert.That(validation.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-UNAPPROVED"));

            var coordinator = new LaunchCinematicPlaybackCoordinator();
            LaunchCinematicPlaybackAttempt attempt = coordinator.Begin(
                desktop,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: desktop.ReducedMotionFallbackOnly);
            Assert.IsFalse(attempt.Accepted);
            Assert.AreEqual(LaunchCinematicPlaybackTerminalReason.ReducedMotionFallback, coordinator.Terminal.Reason);
        }

        [Test]
        public void PackagedAndroidCatalogStaysBlockedWithoutAnEncodeHash()
        {
            Assert.IsTrue(
                LaunchCinematicCatalog.TryLoadForPlatform(
                    LaunchCinematicPlatform.Android,
                    out LaunchCinematicRuntimeRecord android));
            Assert.AreEqual("h264-main", android.CodecProfile);
            Assert.AreEqual(1280, android.Width);
            Assert.AreEqual(720, android.Height);
            Assert.AreEqual(0, android.ByteLength);
            Assert.IsTrue(string.IsNullOrEmpty(android.Sha256));

            LaunchCinematicValidationResult validation = LaunchCinematicRuntimeValidator.Validate(
                android,
                LaunchCinematicPlatform.Android,
                releaseBuild: true);
            Assert.IsFalse(validation.IsValid);
            Assert.That(validation.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-UNAPPROVED"));
            Assert.That(validation.Diagnostics.Select(d => d.Code), Contains.Item("AL-LAUNCH-HASH"));
        }

        [Test]
        public void BootBindingEstablishesStaticFallbackForThePackagedCatalog()
        {
            Assert.IsTrue(
                LaunchCinematicCatalog.TryLoadForPlatform(
                    LaunchCinematicPlatform.Desktop,
                    out LaunchCinematicRuntimeRecord record));

            var lifecycle = new LaunchCinematicLifecycle();
            string reason = LaunchCinematicBootBinding.EstablishStaticFallback(
                lifecycle,
                record,
                LaunchCinematicPlatform.Desktop,
                releaseBuild: true,
                reducedMotion: record.ReducedMotionFallbackOnly);

            Assert.AreEqual(LaunchCinematicState.Fallback, lifecycle.State);
            Assert.AreEqual("approved-media-unavailable", reason);
        }

        [Test]
        public void EditorCatalogResolverDoesNotUseTheDefaultStreamingAssetsRoot()
        {
            string directory = LaunchCinematicCatalog.ResolveGameDataDirectory();
            StringAssert.Contains("AL", directory.Replace('\\', '/'));
            StringAssert.Contains("StreamingAssets/GameData", directory.Replace('\\', '/'));
            StringAssert.DoesNotContain("Assets/StreamingAssets/GameData", directory.Replace('\\', '/'));
        }
    }
}
