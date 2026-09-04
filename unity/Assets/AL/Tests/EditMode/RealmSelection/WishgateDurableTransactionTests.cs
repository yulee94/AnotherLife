using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class WishgateDurableTransactionTests
    {
        private const string EntitlementId = "wishgate_entitlement_001";
        private const string RewardId = "wishgate_reward_renewal";
        private const string ApplicationId = "wishgate_application_001";
        private const long Now = 1000;

        [Test]
        public void FullLifecycleAppliesRewardOnceAndReloadedCommitIsReplayable()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                WishgateDurableDependencies deps = CreateDeps(new CountingApplicator());
                SeedCatalogGems(save, deps.Catalog);

                WishgateCommitResult earn = Commit(
                    save,
                    deps,
                    WishgateOperation.Earn,
                    "wishgate_earn_operation",
                    "wishgate_earn_event",
                    0,
                    0);
                Assert.AreEqual(WishgateCommitStatus.Committed, earn.Status);
                Assert.True(earn.Persisted);
                Assert.False(earn.IsFinalVerifiedCommit);
                Assert.AreEqual(WishgateEntitlementPhase.Earned, earn.Phase);
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);

                WishgateCommitResult select = Commit(
                    save,
                    deps,
                    WishgateOperation.SelectReward,
                    "wishgate_select_operation",
                    "wishgate_select_event",
                    1,
                    1);
                Assert.AreEqual(WishgateCommitStatus.Committed, select.Status);
                Assert.False(select.IsFinalVerifiedCommit);
                Assert.AreEqual(WishgateEntitlementPhase.RewardSelected, select.Phase);
                Assert.AreEqual(RewardId, select.RewardId);
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);

                WishgateCommitResult apply = Commit(
                    save,
                    deps,
                    WishgateOperation.ApplyReward,
                    "wishgate_apply_operation",
                    "wishgate_apply_event",
                    2,
                    2);
                Assert.AreEqual(WishgateCommitStatus.Committed, apply.Status);
                Assert.False(apply.IsFinalVerifiedCommit);
                Assert.AreEqual(WishgateEntitlementPhase.RewardAppliedPendingCommit, apply.Phase);
                Assert.AreEqual(1, save.CurrentSave.WarzoneCredits);

                WishgateCommitResult duplicateApply = Commit(
                    save,
                    deps,
                    WishgateOperation.ApplyReward,
                    "wishgate_apply_operation",
                    "wishgate_apply_event",
                    2,
                    2);
                Assert.AreEqual(WishgateCommitStatus.Replayed, duplicateApply.Status);
                Assert.False(duplicateApply.MutationOccurred);
                Assert.AreEqual(1, save.CurrentSave.WarzoneCredits);

                WishgateCommitResult commit = Commit(
                    save,
                    deps,
                    WishgateOperation.Commit,
                    "wishgate_commit_operation",
                    "wishgate_commit_event",
                    3,
                    3);
                Assert.AreEqual(WishgateCommitStatus.Committed, commit.Status);
                Assert.True(commit.IsFinalVerifiedCommit);
                Assert.AreEqual(64, commit.PostCommitNotificationCorrelationId.Length);
                Assert.AreEqual(64, commit.ReceiptHash.Length);
                Assert.AreEqual(1, save.CurrentSave.WarzoneCredits);

                byte[] afterCommit = File.ReadAllBytes(Path.Combine(root, "save.json"));
                LocalSaveGameService reloaded = CreateService(root);
                reloaded.Load();
                WishgateCommitResult replay = WishgateSaveAuthority.TryCommit(
                    reloaded,
                    new WishgateCommitRequest(
                        WishgateOperation.Commit,
                        "wishgate_commit_operation",
                        "wishgate_commit_event",
                        "wishgate_commit_correlation",
                        WishgateEngineeringIds.ActorId,
                        EntitlementId,
                        string.Empty,
                        RewardId,
                        ApplicationId,
                        Now,
                        4,
                        4),
                    deps);
                Assert.AreEqual(WishgateCommitStatus.Replayed, replay.Status);
                Assert.True(replay.IsFinalVerifiedCommit);
                Assert.False(replay.MutationOccurred);
                Assert.AreEqual(1, reloaded.CurrentSave.WarzoneCredits);
                CollectionAssert.AreEqual(
                    afterCommit,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SelectionDoesNotConsumeEntitlementOrGrantReward()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                WishgateDurableDependencies deps = CreateDeps(new CountingApplicator());
                SeedCatalogGems(save, deps.Catalog);
                Commit(save, deps, WishgateOperation.Earn, "e1", "ee1", 0, 0);
                WishgateCommitResult select = Commit(
                    save,
                    deps,
                    WishgateOperation.SelectReward,
                    "s1",
                    "se1",
                    1,
                    1);

                Assert.AreEqual(WishgateEntitlementPhase.RewardSelected, select.Phase);
                Assert.False(select.IsFinalVerifiedCommit);
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
                Assert.AreEqual(
                    WishgateEntitlementPhase.RewardSelected,
                    (WishgateEntitlementPhase)save.CurrentSave.WishgateTransaction.Phase);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void UnknownRewardDuplicatePressAndPrototypePathNeverGrantOrReportSuccess()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                var applicator = new CountingApplicator();
                WishgateDurableDependencies deps = CreateDeps(applicator);
                SeedCatalogGems(save, deps.Catalog);
                Commit(save, deps, WishgateOperation.Earn, "e1", "ee1", 0, 0);

                var unknownAuthority = new FakeAuthority
                {
                    RewardStatus = WishgateLookupStatus.Unknown
                };
                WishgateDurableDependencies unknown = new WishgateDurableDependencies(
                    deps.Catalog,
                    deps.Clock,
                    unknownAuthority,
                    applicator);
                WishgateCommitResult unknownReward = Commit(
                    save,
                    unknown,
                    WishgateOperation.SelectReward,
                    "s-unknown",
                    "se-unknown",
                    1,
                    1);
                Assert.AreEqual(WishgateCommitStatus.RejectedUnsupported, unknownReward.Status);
                Assert.False(unknownReward.IsFinalVerifiedCommit);
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);

                var service = new LocalRealmGemService(save);
                service.MarkWishgateEarned("complete_eight_gems");
                service.ChooseWishReward("warmaster_credits");
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
                Assert.AreEqual(
                    WishgateEntitlementPhase.Earned,
                    (WishgateEntitlementPhase)save.CurrentSave.WishgateTransaction.Phase);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SaveFailureLeavesEntitlementAndGrantUnchanged()
        {
            string root = CreateRoot();
            try
            {
                var gated = new GatedSaveFileOperations();
                LocalSaveGameService save = CreateService(root, gated);
                save.Load();
                if (save.CurrentSave == null ||
                    save.GetCurrentAuthority() == null ||
                    save.GetCurrentAuthority().Status != ProfileWriteAuthorityStatus.Writable)
                {
                    WriteSchemaTwo(root, "alp_0123456789abcdef0123456789abcdef");
                    save = CreateService(root, gated);
                    save.Load();
                }

                WishgateDurableDependencies deps = CreateDeps(new CountingApplicator());
                SeedCatalogGems(save, deps.Catalog);
                gated.FailDurableWrites = true;
                bool priorIgnore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                WishgateCommitResult failed;
                try
                {
                    failed = Commit(save, deps, WishgateOperation.Earn, "e-fail", "ee-fail", 0, 0);
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = priorIgnore;
                }

                Assert.AreNotEqual(WishgateCommitStatus.Committed, failed.Status);
                Assert.False(failed.IsFinalVerifiedCommit);
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
                Assert.True(
                    save.CurrentSave.WishgateTransaction == null ||
                    save.CurrentSave.WishgateTransaction.Version <= 0 ||
                    save.CurrentSave.WishgateTransaction.Phase ==
                        (int)WishgateEntitlementPhase.Unearned);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ProductionAuthorityRejectsWishEmphasesAndMissingFfaWin()
        {
            var authority = new WishgateCatalogTransactionAuthority();
            Assert.AreEqual(
                WishgateLookupStatus.Unknown,
                authority.ResolveReward("wish_emphasis_renewal"));
            Assert.AreEqual(
                WishgateLookupStatus.Found,
                authority.ResolveEarnReason(WishgateEngineeringIds.EarnAllRealmGemSignatures));

            RealmGemCatalogSnapshot catalog = LoadCatalog();
            var custody = new RealmGemCustodySnapshot(
                RealmGemCustodySnapshotStatus.Available,
                1,
                catalog.Entries.SelectHome());
            WishgateDecisionStatus eligibility = authority.EvaluateEligibility(
                new WishgateTransactionRequest(
                    WishgateOperation.Earn,
                    "op",
                    "ev",
                    "corr",
                    WishgateEngineeringIds.ActorId,
                    EntitlementId,
                    WishgateEngineeringIds.EarnAllRealmGemSignatures,
                    string.Empty,
                    string.Empty,
                    Now,
                    0,
                    0),
                catalog,
                custody);
            Assert.AreEqual(WishgateDecisionStatus.Rejected, eligibility);
        }

        [Test]
        public void LegacySaveWithoutTransactionStateRemainsUnearnedAndLoadable()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                Assert.NotNull(save.CurrentSave);
                WishgateTransactionSnapshot snapshot =
                    WishgateDurableTransaction.ProjectSnapshot(save.CurrentSave);
                Assert.AreEqual(WishgateSnapshotStatus.Available, snapshot.Status);
                Assert.AreEqual(WishgateEntitlementPhase.Unearned, snapshot.Entitlement.Phase);
                Assert.AreEqual(0, snapshot.Revision);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static WishgateCommitResult Commit(
            LocalSaveGameService save,
            WishgateDurableDependencies deps,
            WishgateOperation operation,
            string operationId,
            string eventId,
            long snapshotRevision,
            long entitlementRevision)
        {
            string earnReason = operation == WishgateOperation.Earn
                ? WishgateEngineeringIds.EarnAllRealmGemSignatures
                : string.Empty;
            string reward = operation == WishgateOperation.Earn ? string.Empty : RewardId;
            string application = operation == WishgateOperation.ApplyReward ||
                                 operation == WishgateOperation.Commit
                ? ApplicationId
                : string.Empty;
            return WishgateSaveAuthority.TryCommit(
                save,
                new WishgateCommitRequest(
                    operation,
                    operationId,
                    eventId,
                    operationId + "_correlation",
                    WishgateEngineeringIds.ActorId,
                    EntitlementId,
                    earnReason,
                    reward,
                    application,
                    Now,
                    snapshotRevision,
                    entitlementRevision),
                deps);
        }

        private static WishgateDurableDependencies CreateDeps(IWishgateRewardApplicator applicator)
        {
            return new WishgateDurableDependencies(
                LoadCatalog(),
                new FakeClock(Now),
                new FakeAuthority(),
                applicator);
        }

        private static RealmGemCatalogSnapshot LoadCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult parsed = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.True(parsed.IsSuccess, parsed.TechnicalCode);
            RealmGemCatalogBuildResult built = RealmGemCatalogResolver.Build(parsed.Snapshot);
            Assert.True(built.IsReady, built.TechnicalCode);
            return built.Snapshot;
        }

        private static void SeedCatalogGems(
            LocalSaveGameService save,
            RealmGemCatalogSnapshot catalog)
        {
            ProfileWriteAuthoritySnapshot before = save.GetCurrentAuthority();
            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)save).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(before),
                    "al.test.seed-wishgate-gems.v1",
                    "al.test.seed-wishgate-gems.1",
                    candidate =>
                    {
                        candidate.RealmGems = new List<RealmGemState>();
                        for (int i = 0; i < catalog.Entries.Count; i++)
                        {
                            RealmGemCatalogEntry entry = catalog.Entries[i];
                            candidate.RealmGems.Add(new RealmGemState
                            {
                                GemId = entry.Id,
                                HomeRealm = entry.HomeRealm,
                                GemIndex = entry.SaveSlotIndex,
                                IsAtHome = true
                            });
                        }

                        return SaveCandidateMutationPreparation.Prepared();
                    });
            Assert.NotNull(bound);
            Assert.True(bound.CommitResult.IsCommitted);
        }

        private static LocalSaveGameService CreateWritable(string root)
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
            return save;
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
                "AnotherLife-WishgateDurable",
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

        private sealed class CountingApplicator : IWishgateRewardApplicator
        {
            public bool TryApply(
                SaveGameData candidate,
                WishgateRewardApplicationIntent intent,
                out string diagnosticCode)
            {
                diagnosticCode = string.Empty;
                if (candidate == null || intent == null)
                {
                    diagnosticCode = WishgateCommitCodes.RewardApplyFailed;
                    return false;
                }

                WishgateTransactionState state =
                    candidate.WishgateTransaction ?? new WishgateTransactionState();
                if (string.Equals(
                        state.AppliedRewardApplicationId,
                        intent.RewardApplicationId,
                        StringComparison.Ordinal))
                {
                    candidate.WishgateTransaction = state;
                    return true;
                }

                candidate.WarzoneCredits += 1;
                state.AppliedRewardApplicationId = intent.RewardApplicationId;
                candidate.WishgateTransaction = state;
                return true;
            }
        }

        private sealed class FakeClock : IWishgateTransactionClock
        {
            public FakeClock(long utcSeconds)
            {
                UtcSeconds = utcSeconds;
            }

            public long UtcSeconds { get; }

            public bool TryGetUtcSeconds(out long utcSeconds)
            {
                utcSeconds = UtcSeconds;
                return UtcSeconds > 0;
            }
        }

        private sealed class FakeAuthority : IWishgateTransactionAuthority
        {
            public WishgateLookupStatus EarnReasonStatus { get; set; } = WishgateLookupStatus.Found;
            public WishgateLookupStatus RewardStatus { get; set; } = WishgateLookupStatus.Found;
            public WishgateDecisionStatus EligibilityStatus { get; set; } =
                WishgateDecisionStatus.Accepted;
            public WishgateDecisionStatus AuthorizationStatus { get; set; } =
                WishgateDecisionStatus.Accepted;

            public WishgateLookupStatus ResolveEarnReason(string earnReasonId) => EarnReasonStatus;

            public WishgateLookupStatus ResolveReward(string rewardId) => RewardStatus;

            public WishgateDecisionStatus EvaluateEligibility(
                WishgateTransactionRequest request,
                RealmGemCatalogSnapshot realmGemCatalog,
                RealmGemCustodySnapshot custodySnapshot) =>
                EligibilityStatus;

            public WishgateDecisionStatus Authorize(
                WishgateTransactionRequest request,
                WishgateEntitlementState currentEntitlement) =>
                AuthorizationStatus;
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

    internal static class WishgateCustodyTestExtensions
    {
        public static IEnumerable<RealmGemCustodyRecord> SelectHome(
            this IReadOnlyList<RealmGemCatalogEntry> entries)
        {
            var records = new List<RealmGemCustodyRecord>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                RealmGemCatalogEntry entry = entries[i];
                records.Add(new RealmGemCustodyRecord(
                    entry.Id,
                    entry.HomeRealmId,
                    entry.HomeRealm,
                    entry.SaveSlotIndex,
                    RealmGemCustodyState.AtHome,
                    string.Empty,
                    0,
                    1,
                    true));
            }

            return records;
        }
    }
}
