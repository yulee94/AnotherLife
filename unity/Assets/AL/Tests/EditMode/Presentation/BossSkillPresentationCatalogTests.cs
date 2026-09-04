using System.IO;
using AL.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Presentation
{
    public sealed class BossSkillPresentationCatalogTests
    {
        [Test]
        public void PackagedCatalogParsesColossusAndFaultlineSlam()
        {
            Assert.True(BossSkillPresentationCatalog.TryParse(ReadPackaged(), out BossSkillPresentationSnapshot snapshot));
            Assert.That(snapshot.Boss.modelId, Is.EqualTo(BossSkillPresentationCatalog.ExpectedModelId));
            Assert.That(snapshot.Skill.skillId, Is.EqualTo(BossSkillPresentationCatalog.ExpectedSkillId));
            Assert.That(snapshot.Bosses.Length, Is.EqualTo(16));
            Assert.That(snapshot.Skills.Length, Is.EqualTo(4));
            Assert.False(snapshot.Boss.pooling.maxActive < 1);
        }

        [Test]
        public void StableModelAndSkillIdsResolveQualifiedProfilesOnly()
        {
            Assert.True(BossSkillPresentationCatalog.TryParse(ReadPackaged(), out BossSkillPresentationSnapshot snapshot));

            Assert.True(BossSkillPresentationCatalog.TryResolve(
                snapshot,
                "elite_stonehold_oreblind_delver",
                "npc_ranged_bolt",
                "low",
                "distant",
                out ResolvedPresentation resolved));
            Assert.That(resolved.ModelId, Is.EqualTo("elite_stonehold_oreblind_delver"));
            Assert.That(resolved.Gameplay.SkillId, Is.EqualTo("npc_ranged_bolt"));
            Assert.True(resolved.Gameplay.PresentationCannotMutate);
            Assert.True(resolved.ProtectedCuesPreserved);

            Assert.False(BossSkillPresentationCatalog.TryResolve(
                snapshot,
                "elite_stonehold_rimehorn_breaker",
                "npc_ranged_bolt",
                "low",
                "hero",
                out _));
            Assert.False(BossSkillPresentationCatalog.TryResolve(
                snapshot,
                "unknown_model",
                "npc_ranged_bolt",
                "low",
                "hero",
                out _));
        }

        [Test]
        public void QualityAndDistanceResolveWithoutMutatingGameplay()
        {
            Assert.True(BossSkillPresentationCatalog.TryParse(ReadPackaged(), out BossSkillPresentationSnapshot snapshot));
            FrozenGameplaySnapshot baseline = null;
            string[] qualities = { "low", "balanced", "high" };
            string[] distances = { "hero", "nearby", "distant" };
            for (int qualityIndex = 0; qualityIndex < qualities.Length; qualityIndex++)
            {
                for (int distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
                {
                    Assert.True(BossSkillPresentationCatalog.TryResolve(
                        snapshot,
                        qualities[qualityIndex],
                        distances[distanceIndex],
                        out ResolvedPresentation resolved));
                    if (baseline == null)
                    {
                        baseline = resolved.Gameplay;
                    }

                    Assert.That(resolved.Gameplay.SkillId, Is.EqualTo(baseline.SkillId));
                    Assert.That(resolved.Gameplay.Source, Is.EqualTo(baseline.Source));
                    Assert.True(resolved.Gameplay.PresentationCannotMutate);
                    Assert.True(resolved.ProtectedCuesPreserved);
                }
            }
        }

        [Test]
        public void SlotIndexOrItemGradeJsonFailsClosed()
        {
            string packaged = ReadPackaged();
            Assert.False(BossSkillPresentationCatalog.TryParse(
                packaged.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"slot\": 0"),
                out _));
            Assert.False(BossSkillPresentationCatalog.TryParse(
                packaged.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"ItemGrade\": \"legendary\""),
                out _));
        }

        [Test]
        public void PooledAcquireReleaseReusesInstance()
        {
            var pool = new PresentationPool(2, 4);
            int first = pool.Acquire();
            pool.Release(first);
            int second = pool.Acquire();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(pool.Created, Is.EqualTo(1));
            pool.Release(second);
            Assert.That(pool.Acquire(), Is.GreaterThan(0));
            Assert.That(pool.Acquire(), Is.GreaterThan(0));
            Assert.That(pool.Acquire(), Is.EqualTo(0));
        }

        private static string ReadPackaged()
        {
            return File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                BossSkillPresentationCatalog.CatalogFileName)));
        }
    }
}
