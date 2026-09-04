using System;
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

namespace AL.Tests.EditMode
{
    public sealed class ProfileBoundDeathPenaltyTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void BelowMaxSubtractsFivePointsOnceAndReplayIsIdempotent()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root, 49, 50L);
                DeathPenaltyCommitRequest request = Request("op-below-max-0001", "evt-below-max-0001");
                DeathPenaltyCommitResult first = DeathPenaltySaveAuthority.TryCommit(save, request);
                Assert.AreEqual(DeathPenaltyCommitStatus.CommittedBelowMax, first.Status);
                Assert.True(first.MutationOccurred);
                Assert.True(first.Persisted);
                Assert.True(first.AllowsRevive);
                Assert.AreEqual(49, first.AfterLevel);
                Assert.AreEqual(45L, first.AfterInLevelExperienceUnits);
                Assert.AreEqual(49, save.CurrentSave.ChampionProgression.CurrentLevel);
                Assert.AreEqual(45L, save.CurrentSave.ChampionProgression.InLevelExperienceUnits);
                Assert.AreEqual(
                    DeathPenaltyAuthorityState.OutcomeBelowMaxCommitted,
                    save.CurrentSave.DeathPenalty.Outcome);
                byte[] afterFirst = File.ReadAllBytes(Path.Combine(root, "save.json"));

                DeathPenaltyCommitResult replay = DeathPenaltySaveAuthority.TryCommit(save, request);
                Assert.AreEqual(DeathPenaltyCommitStatus.ReplayedBelowMax, replay.Status);
                Assert.False(replay.MutationOccurred);
                Assert.False(replay.Persisted);
                Assert.True(replay.AllowsRevive);
                Assert.AreEqual(45L, save.CurrentSave.ChampionProgression.InLevelExperienceUnits);
                CollectionAssert.AreEqual(
                    afterFirst,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void FloorAtZeroNeverReducesLevelAndLegacySaveMigrates()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root, 7, 1L);
                DeathPenaltyCommitResult result = DeathPenaltySaveAuthority.TryCommit(
                    save,
                    Request("op-floor-zero-0001", "evt-floor-zero-0001"));
                Assert.AreEqual(DeathPenaltyCommitStatus.CommittedBelowMax, result.Status);
                Assert.AreEqual(7, result.AfterLevel);
                Assert.AreEqual(0L, result.AfterInLevelExperienceUnits);

                string migratedRoot = CreateRoot();
                try
                {
                    LocalSaveGameService migrated = CreateWritable(migratedRoot, 0);
                    DeathPenaltyCommitResult installed = DeathPenaltySaveAuthority.TryCommit(
                        migrated,
                        Request("op-migrate-0001", "evt-migrate-0001"));
                    Assert.AreEqual(DeathPenaltyCommitStatus.CommittedBelowMax, installed.Status);
                    Assert.AreEqual(1, migrated.CurrentSave.ChampionProgression.CurrentLevel);
                    Assert.AreEqual(0L, migrated.CurrentSave.ChampionProgression.InLevelExperienceUnits);
                    Assert.AreEqual(50, migrated.CurrentSave.ChampionProgression.MaximumLevel);
                }
                finally
                {
                    DeleteRoot(migratedRoot);
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void MaxLevelPreservesXpAndReturnsPaymentRequiredWithoutRevive()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root, 50, 73L);
                DeathPenaltyCommitRequest request = Request("op-max-level-0001", "evt-max-level-0001");
                DeathPenaltyCommitResult first = DeathPenaltySaveAuthority.TryCommit(save, request);
                Assert.AreEqual(DeathPenaltyCommitStatus.OathmarkPaymentRequired, first.Status);
                Assert.True(first.MutationOccurred);
                Assert.False(first.AllowsRevive);
                Assert.AreEqual(50, first.AfterLevel);
                Assert.AreEqual(73L, first.AfterInLevelExperienceUnits);
                Assert.AreEqual(73L, save.CurrentSave.ChampionProgression.InLevelExperienceUnits);
                Assert.AreEqual(
                    DeathPenaltyAuthorityState.OutcomeOathmarkPaymentRequired,
                    save.CurrentSave.DeathPenalty.Outcome);

                DeathPenaltyCommitResult replay = DeathPenaltySaveAuthority.TryCommit(save, request);
                Assert.AreEqual(
                    DeathPenaltyCommitStatus.ReplayedOathmarkPaymentRequired,
                    replay.Status);
                Assert.False(replay.MutationOccurred);
                Assert.False(replay.AllowsRevive);
                Assert.AreEqual(73L, save.CurrentSave.ChampionProgression.InLevelExperienceUnits);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void WrongProfileStaleCollisionReadOnlyAndForwardMutateNothing()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root, 49, 50L);
                string profileId = save.CurrentSave.ProfileId;
                string generation = save.GetCurrentAuthority().VerifiedGenerationFingerprint;
                DeathPenaltyCommitResult committed = DeathPenaltySaveAuthority.TryCommit(
                    save,
                    Request("op-guard-0001", "evt-guard-0001"));
                Assert.AreEqual(DeathPenaltyCommitStatus.CommittedBelowMax, committed.Status);
                long xp = save.CurrentSave.ChampionProgression.InLevelExperienceUnits;
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));

                DeathPenaltyCommitResult wrongProfile = DeathPenaltySaveAuthority.TryCommit(
                    save,
                    new DeathPenaltyCommitRequest(
                        "op-guard-0002",
                        "evt-guard-0002",
                        DeathPenaltyIds.InnerCombatSessionId,
                        DeathPenaltyIds.InnerEncounterAttemptId,
                        DeathPenaltyIds.InstanceId("Stonehold"),
                        0L,
                        "alp_ffffffffffffffffffffffffffffffff",
                        string.Empty));
                Assert.AreEqual(DeathPenaltyCommitStatus.RejectedWrongProfile, wrongProfile.Status);
                Assert.False(wrongProfile.MutationOccurred);
                Assert.False(wrongProfile.AllowsRevive);

                DeathPenaltyCommitResult stale = DeathPenaltySaveAuthority.TryCommit(
                    save,
                    new DeathPenaltyCommitRequest(
                        "op-guard-0003",
                        "evt-guard-0003",
                        DeathPenaltyIds.InnerCombatSessionId,
                        DeathPenaltyIds.InnerEncounterAttemptId,
                        DeathPenaltyIds.InstanceId("Stonehold"),
                        0L,
                        profileId,
                        new string('0', 64)));
                Assert.AreEqual(DeathPenaltyCommitStatus.RejectedStale, stale.Status);
                Assert.False(stale.MutationOccurred);

                DeathPenaltyCommitResult collision = DeathPenaltySaveAuthority.TryCommit(
                    save,
                    Request("op-guard-0001", "evt-guard-DIFFERENT"));
                Assert.AreEqual(DeathPenaltyCommitStatus.RejectedCollision, collision.Status);
                Assert.False(collision.MutationOccurred);
                Assert.AreEqual(xp, save.CurrentSave.ChampionProgression.InLevelExperienceUnits);

                SaveGameData future = JsonUtility.FromJson<SaveGameData>(
                    File.ReadAllText(Path.Combine(root, "save.json")));
                future.SaveSchemaVersion = 3;
                string forwardRoot = CreateRoot();
                try
                {
                    byte[] forwardBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(future, true));
                    File.WriteAllBytes(Path.Combine(forwardRoot, "save.json"), forwardBytes);
                    File.WriteAllBytes(Path.Combine(forwardRoot, "save.backup.json"), forwardBytes);
                    LocalSaveGameService forward = CreateService(forwardRoot);
                    forward.Load();
                    DeathPenaltyCommitResult denied = DeathPenaltySaveAuthority.TryCommit(
                        forward,
                        Request("op-forward-0001", "evt-forward-0001"));
                    Assert.False(denied.MutationOccurred);
                    Assert.False(denied.AllowsRevive);
                    Assert.AreNotEqual(DeathPenaltyCommitStatus.CommittedBelowMax, denied.Status);
                }
                finally
                {
                    DeleteRoot(forwardRoot);
                }

                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ProductionPathRevivesOnlyAfterBelowMaxCommit()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root, 49, 50L);
                _root = new GameObject("DeathPenaltyHost");
                _root.transform.position = new Vector3(18f, 1.1f, 22f);
                ChampionCombat combat = _root.AddComponent<ChampionCombat>();
                ChampionController controller = _root.AddComponent<ChampionController>();
                combat.TakeDamage(combat.MaxHealth);
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
                    Request("op-prod-0001", "evt-prod-0001"),
                    plan,
                    combat,
                    controller,
                    out penalty);
                Assert.True(stoodUp);
                Assert.True(penalty.AllowsRevive);
                Assert.AreEqual(45L, penalty.AfterInLevelExperienceUnits);
                Assert.False(combat.IsDead);

                string maxRoot = CreateRoot();
                LocalSaveGameService maxSave = CreateWritable(maxRoot, 50, 80L);
                GameObject maxHost = new GameObject("DeathPenaltyMaxHost");
                try
                {
                    ChampionCombat maxCombat = maxHost.AddComponent<ChampionCombat>();
                    ChampionController maxController = maxHost.AddComponent<ChampionController>();
                    maxCombat.TakeDamage(maxCombat.MaxHealth);
                    DeathPenaltyCommitResult maxPenalty;
                    bool maxStood = DeathPenaltyProductionPath.TryCommitPenaltyThenApply(
                        maxSave,
                        Request("op-prod-max-0001", "evt-prod-max-0001"),
                        plan,
                        maxCombat,
                        maxController,
                        out maxPenalty);
                    Assert.False(maxStood);
                    Assert.False(maxPenalty.AllowsRevive);
                    Assert.AreEqual(
                        DeathPenaltyCommitStatus.OathmarkPaymentRequired,
                        maxPenalty.Status);
                    Assert.True(maxCombat.IsDead);
                    Assert.AreEqual(80L, maxSave.CurrentSave.ChampionProgression.InLevelExperienceUnits);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(maxHost);
                    DeleteRoot(maxRoot);
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SaveFailureLeavesPriorProgression()
        {
            string root = CreateRoot();
            try
            {
                var gated = new GatedSaveFileOperations();
                LocalSaveGameService save = CreateService(root, gated);
                save.Load();
                Assert.NotNull(save.CurrentSave);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    save.GetCurrentAuthority().Status);
                SeedProgression(save, 49, 50L);
                gated.FailDurableWrites = true;
                bool priorIgnore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                DeathPenaltyCommitResult failed;
                try
                {
                    failed = DeathPenaltySaveAuthority.TryCommit(
                        save,
                        Request("op-fail-0001", "evt-fail-0001"));
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = priorIgnore;
                }

                Assert.False(failed.AllowsRevive);
                Assert.AreNotEqual(DeathPenaltyCommitStatus.CommittedBelowMax, failed.Status);
                Assert.AreEqual(50L, save.CurrentSave.ChampionProgression.InLevelExperienceUnits);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static DeathPenaltyCommitRequest Request(string operationId, string deathEventId)
        {
            return new DeathPenaltyCommitRequest(
                operationId,
                deathEventId,
                DeathPenaltyIds.InnerCombatSessionId,
                DeathPenaltyIds.InnerEncounterAttemptId,
                DeathPenaltyIds.InstanceId("Stonehold"));
        }

        private static LocalSaveGameService CreateWritable(
            string root,
            int level = 1,
            long experience = 0L)
        {
            LocalSaveGameService save = CreateService(root);
            save.Load();
            if (save.CurrentSave == null ||
                save.GetCurrentAuthority() == null ||
                save.GetCurrentAuthority().Status != ProfileWriteAuthorityStatus.Writable)
            {
                WriteSchemaTwo(root, "alp_0123456789abcdef0123456789abcdef");
                save = CreateService(root);
                save.Load();
            }

            Assert.NotNull(save.CurrentSave);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                save.GetCurrentAuthority().Status);
            if (level > 0)
            {
                SeedProgression(save, level, experience);
            }

            return save;
        }

        private static void SeedProgression(
            LocalSaveGameService save,
            int level,
            long experience)
        {
            ProfileWriteAuthoritySnapshot before = save.GetCurrentAuthority();
            string profileId = save.CurrentSave.ProfileId;
            ProfileBoundSaveCandidateCommitResult bound =
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
                            CurrentLevel = level,
                            MaximumLevel = DeathPenaltyIds.DefaultMaximumLevel,
                            InLevelExperienceUnits = experience,
                            ExperienceUnitsPerLevel =
                                DeathPenaltyIds.DefaultExperienceUnitsPerLevel,
                            ProgressionRevision = "al.prog.initial",
                            LevelCapPolicyId = DeathPenaltyIds.LevelCapPolicyId,
                            LevelCapPolicyRevision = DeathPenaltyIds.LevelCapPolicyRevision
                        };
                        return SaveCandidateMutationPreparation.Prepared();
                    });
            Assert.NotNull(bound);
            Assert.NotNull(bound.CommitResult);
            Assert.True(bound.CommitResult.IsCommitted);
            Assert.AreEqual(level, save.CurrentSave.ChampionProgression.CurrentLevel);
            Assert.AreEqual(
                experience,
                save.CurrentSave.ChampionProgression.InLevelExperienceUnits);
        }

        private static void WriteSchemaTwo(string root, string profileId)
        {
            var save = new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = 2,
                ProfileInitializationVersion = 1,
                ProfileId = profileId,
                SelectedRealm = RealmId.None,
                CurrentChapterId = "C1",
                Resources = new List<ResourceData>
                {
                    new ResourceData { Type = ResourceType.Food, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Wood, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Stone, Amount = 500 },
                    new ResourceData { Type = ResourceType.Gold, Amount = 500 },
                    new ResourceData { Type = ResourceType.ManaStone, Amount = 150 },
                    new ResourceData { Type = ResourceType.Ore, Amount = 150 }
                }
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));
            File.WriteAllBytes(Path.Combine(root, "save.json"), bytes);
            File.WriteAllBytes(Path.Combine(root, "save.backup.json"), bytes);
        }

        private static LocalSaveGameService CreateService(string root)
        {
            return CreateService(root, new SystemSaveFileOperations());
        }

        private static LocalSaveGameService CreateService(
            string root,
            ISaveFileOperations fileOperations)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(ISaveFileOperations) },
                null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(new object[] { root, fileOperations });
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-ProfileBoundDeath",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private sealed class GatedSaveFileOperations : ISaveFileOperations
        {
            private readonly SystemSaveFileOperations _inner = new SystemSaveFileOperations();
            public bool FailDurableWrites;

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public SaveFileReadResult ReadAllBytesBounded(string path, int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);

            public SaveFileWriteResult WriteAllTextDurable(string path, string contents)
            {
                if (FailDurableWrites)
                {
                    return new SaveFileWriteResult(false, false, "AL-TEST-WRITE-FAILED");
                }

                return _inner.WriteAllTextDurable(path, contents);
            }

            public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);

            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);

            public void Replace(string sourcePath, string destinationPath, string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);

            public void Delete(string path) => _inner.Delete(path);

            public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);

            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);

            public bool IsReparsePoint(string path) => _inner.IsReparsePoint(path);
        }
    }
}
