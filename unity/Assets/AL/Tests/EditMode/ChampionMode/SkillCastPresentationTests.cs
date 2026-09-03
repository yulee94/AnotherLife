using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.ChampionMode.Skills;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class SkillCastPresentationTests
    {
        [Test]
        public void PackagedCatalogTracesEverySkillToRequiredPresentation()
        {
            Assert.True(SkillCastTraceValidator.TryValidate(ReadPackaged(), out SkillCastTraceReport report));
            Assert.That(report.Passed, Is.True);
            Assert.That(report.Results.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(report.Results.All(result => result.Passed), Is.True);
            Assert.That(report.Results.All(result => string.IsNullOrEmpty(result.Failure)), Is.True);
        }

        [Test]
        public void RepresentativeRolesCoverMeleeRangedMagicSupportAreaBossAndBeast()
        {
            Assert.True(SkillCastPresentationCatalog.TryParse(
                ReadPackaged(),
                out SkillCastPresentationSnapshot snapshot));

            string[] requiredRoles =
            {
                "melee_damage",
                "ranged_damage",
                "magic_damage",
                "self_heal_guard",
                "area_damage",
                "boss_area_damage",
                "beast_area_damage"
            };

            for (int index = 0; index < requiredRoles.Length; index++)
            {
                string role = requiredRoles[index];
                Assert.That(
                    snapshot.Bindings.Any(binding => binding.Role == role),
                    Is.True,
                    role);
            }
        }

        [Test]
        public void ProtectedCuePolicyNeverHidesTimingTargetDangerObjectiveOwnershipOrAccessibility()
        {
            Assert.True(SkillCastPresentationCatalog.TryParse(
                ReadPackaged(),
                out SkillCastPresentationSnapshot snapshot));

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "timing",
                    "target",
                    "danger",
                    "objective",
                    "ownership",
                    "accessibility"
                },
                snapshot.Policy.NeverHide.ToArray());
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "density",
                    "overdraw",
                    "lights",
                    "secondary_particles"
                },
                snapshot.Policy.Scalable.ToArray());

            Assert.That(SkillCastEffectRouter.IsProtectedCue("timing"), Is.True);
            Assert.That(SkillCastEffectRouter.IsProtectedCue("target"), Is.True);
            Assert.That(SkillCastEffectRouter.IsProtectedCue("danger"), Is.True);
            Assert.That(SkillCastEffectRouter.IsProtectedCue("objective"), Is.True);
            Assert.That(SkillCastEffectRouter.IsProtectedCue("ownership"), Is.True);
            Assert.That(SkillCastEffectRouter.IsProtectedCue("accessibility"), Is.True);
            Assert.That(SkillCastEffectRouter.IsProtectedCue("density"), Is.False);
        }

        [Test]
        public void ReducedMotionAndPhotosensitivitySuppressCameraImpulseButKeepProtectedCues()
        {
            SkillCastPresentationSettings.ReducedMotion = true;
            SkillCastPresentationSettings.PhotosensitivitySafe = true;
            try
            {
                Assert.That(SkillCastEffectRouter.AllowsCameraImpulse(), Is.False);
                Assert.That(SkillCastEffectRouter.IsProtectedCue("timing"), Is.True);
                Assert.That(SkillCastEffectRouter.IsProtectedCue("danger"), Is.True);
            }
            finally
            {
                SkillCastPresentationSettings.ReducedMotion = false;
                SkillCastPresentationSettings.PhotosensitivitySafe = false;
                SkillCastPresentationSettings.DensityTier = 0;
            }
        }

        [Test]
        public void MissingMotionModuleFailsClosed()
        {
            string json = ReadPackaged().Replace(
                "\"motion_anticipation_id\": \"motion_skill_anticipation\"",
                "\"motion_anticipation_id\": \"missing_motion_module\"");
            Assert.False(SkillCastTraceValidator.TryValidate(json, out SkillCastTraceReport report));
            Assert.That(report.Passed, Is.False);
            Assert.That(
                report.Results.Any(result => result.Failure.Contains("missing_module")),
                Is.True);
        }

        [Test]
        public void EffectModulesExposeColorIndependentShapesAndPrefabIds()
        {
            Assert.True(SkillCastPresentationCatalog.TryParse(
                ReadPackaged(),
                out SkillCastPresentationSnapshot snapshot));

            var requiredKinds = new HashSet<string>
            {
                "telegraph",
                "trail",
                "projectile",
                "beam",
                "summon",
                "impact",
                "decal",
                "area",
                "buff",
                "debuff",
                "status",
                "shield",
                "heal",
                "environment",
                "motion"
            };

            foreach (SkillEffectModule module in snapshot.Modules.Values)
            {
                requiredKinds.Remove(module.ModuleKind);
                Assert.That(module.ShapeId, Is.Not.Empty, module.Id);
                Assert.That(module.PrefabId, Is.Not.Empty, module.Id);
                Assert.That(module.OwnershipReadable, Is.True, module.Id);
                Assert.That(module.ProtectedCues.Count, Is.GreaterThan(0), module.Id);
                Assert.That(
                    snapshot.Modules.ContainsKey(module.ReducedMotionVariantId),
                    Is.True,
                    module.Id);
            }

            Assert.That(requiredKinds, Is.Empty);
        }

        private static string ReadPackaged()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "skill_weather.v1.json"));
            return File.ReadAllText(path);
        }
    }
}
