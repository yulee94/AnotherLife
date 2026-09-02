using System.Linq;
using AL.Editor.Motion;
using AL.Motion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.Animation
{
    public sealed class MotionRoundTripAcceptanceTests
    {
        [Test]
        public void ImportBindingsExposeImmutableCompleteConstructionForFreshImports()
        {
            var preset = ScriptableObject.CreateInstance<MotionImportPreset>();
            try
            {
                var motionEvent = new MotionImportEvent(
                    "rmc_event_phase_enter_v001",
                    1,
                    0,
                    new MotionStaticPayload { Phase = "idle.neutral" });
                var clip = new MotionImportClip(
                    "rmc_clip_humanoid_idle_neutral_v001",
                    "idle.neutral",
                    "ANIM_rmc_clip_humanoid_idle_neutral_v001",
                    1,
                    31,
                    true,
                    MotionRootMode.InPlace,
                    new[] { motionEvent });
                var binding = new MotionImportBinding(
                    "Assets/AL/Generated/MotionRoundTrip/champion_motion.fbx",
                    preset,
                    new[] { clip });

                Assert.That(binding.AssetPath, Does.EndWith("champion_motion.fbx"));
                Assert.That(binding.Clips, Has.Count.EqualTo(1));
                Assert.That(binding.Clips[0].Events, Has.Count.EqualTo(1));
                Assert.That(binding.Clips[0].Events[0].EventOrdinal, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void FreshChampionNpcAndFantasyBeastImportsPassRuntimeAcceptance()
        {
            MotionRoundTripAcceptanceReport report =
                MotionRoundTripAcceptanceBuilder.BuildForTests(renderReviewImages: false);

            Assert.That(report.Status, Is.EqualTo("passed"), report.FormatFailures());
            Assert.That(report.Representatives, Has.Length.EqualTo(3));
            Assert.That(
                report.Representatives.Select(value => value.SubjectKind),
                Is.EquivalentTo(new[] { "champion", "npc", "beast" }));
            Assert.That(report.Representatives.All(value => value.FreshImport), Is.True);
            Assert.That(
                report.Representatives.All(value => value.Runtime.ControllerConfigured),
                Is.True);
            Assert.That(report.Representatives.All(value => value.Runtime.GraphValid), Is.True);
            Assert.That(
                report.Representatives.All(value => value.Runtime.TPoseDetected == false),
                Is.True);
            Assert.That(
                report.Representatives.All(value => value.Animation.MissingMotionKeys.Length == 0),
                Is.True);
            Assert.That(
                report.Representatives.All(value => value.Animation.MissingEvents.Length == 0),
                Is.True);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(report.ScenePath),
                Is.Not.Null,
                report.ScenePath);
        }
    }
}
