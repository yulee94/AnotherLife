using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AL.ChampionMode.Control;
using AL.ChampionMode.Death;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class DeathPenaltyProductionPathPlayModeTests
    {
        [UnityTest]
        public IEnumerator BelowMaxDeathPersistsXpPenaltyThenRevives()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-DeathPenaltyPlayMode",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            GameObject host = new GameObject("DeathPenaltyPlayModeHost");
            try
            {
                LocalSaveGameService save = CreateService(root);
                save.Load();
                Assert.NotNull(save.CurrentSave);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    save.GetCurrentAuthority().Status);
                ProfileWriteAuthoritySnapshot before = save.GetCurrentAuthority();
                string profileId = save.CurrentSave.ProfileId;
                ((IProfileBoundSaveGameCandidateStore)save).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(before),
                    "al.test.seed-progression.v1",
                    "al.test.seed-progression.1",
                    candidate =>
                    {
                        candidate.ChampionProgression = new ChampionProgressionState
                        {
                            Version = ChampionProgressionState.CurrentVersion,
                            ProfileId = profileId,
                            CharacterId = profileId,
                            AccountId = profileId,
                            CurrentLevel = 49,
                            MaximumLevel = DeathPenaltyIds.DefaultMaximumLevel,
                            InLevelExperienceUnits = 50L,
                            ExperienceUnitsPerLevel =
                                DeathPenaltyIds.DefaultExperienceUnitsPerLevel,
                            ProgressionRevision = "al.prog.initial",
                            LevelCapPolicyId = DeathPenaltyIds.LevelCapPolicyId,
                            LevelCapPolicyRevision = DeathPenaltyIds.LevelCapPolicyRevision
                        };
                        return SaveCandidateMutationPreparation.Prepared();
                    });

                host.transform.position = new Vector3(18f, 1.1f, 22f);
                ChampionCombat combat = host.AddComponent<ChampionCombat>();
                ChampionController controller = host.AddComponent<ChampionController>();
                combat.TakeDamage(combat.MaxHealth);
                Assert.True(combat.IsDead);

                InnerRealmDeathRespawnPlan plan = InnerRealmDeathRespawnPlanner.Plan(
                    new InnerRealmDeathRespawnRequest(
                        RealmId.Crownlands,
                        new InnerRealmVec3(18f, 1.1f, 22f),
                        InnerRealmDeathZoneKind.Inner,
                        new[]
                        {
                            InnerRealmSafeSite.UnnamedCapital(
                                RealmId.Crownlands,
                                new InnerRealmVec3(0f, 1.1f, -7.4f))
                        }));
                DeathPenaltyCommitResult penalty;
                bool stoodUp = DeathPenaltyProductionPath.TryCommitPenaltyThenApply(
                    save,
                    new DeathPenaltyCommitRequest(
                        DeathPenaltyIds.NewOperationId(),
                        DeathPenaltyIds.NewDeathEventId(),
                        DeathPenaltyIds.InnerCombatSessionId,
                        DeathPenaltyIds.InnerEncounterAttemptId,
                        DeathPenaltyIds.InstanceId("Crownlands")),
                    plan,
                    combat,
                    controller,
                    out penalty);
                Assert.True(stoodUp);
                Assert.True(penalty.AllowsRevive);
                Assert.AreEqual(DeathPenaltyCommitStatus.CommittedBelowMax, penalty.Status);
                Assert.AreEqual(45L, penalty.AfterInLevelExperienceUnits);
                Assert.False(combat.IsDead);
                yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static LocalSaveGameService CreateService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(ISaveFileOperations) },
                null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root, new SystemSaveFileOperations() });
        }
    }
}
