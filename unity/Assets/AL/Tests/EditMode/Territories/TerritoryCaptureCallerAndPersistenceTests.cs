using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.RealmWar.Territories;
using AL.RealmWar.Territories.Contracts;
using AL.RealmWar.Warzone;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode.Territories
{
    public sealed class TerritoryCaptureCallerAndPersistenceTests
    {

        [Test]
        public void ProductionCaptureCommitsReloadsAndReplaysWithoutFarming()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                var requestBuilder =
                    TerritoryCaptureTransactionService.CreateForTests(save);
                TerritoryCaptureTransactionRequest request = Request(
                    requestBuilder,
                    save.CurrentSave,
                    "capture-T5-durable");
                var warzone = new WarzoneService(save);
                int publications = 0;
                warzone.OnTerritoryCaptured += (_, __) => publications++;

                TerritoryCaptureApplicationResult committed =
                    warzone.ApplyCaptureTransaction(request);

                Assert.AreEqual(
                    TerritoryApplyDisposition.Committed,
                    committed.Disposition,
                    string.Join(
                        " | ",
                        committed.Diagnostics.Select(item =>
                            item.Code + ": " + item.Message)));
                Assert.AreEqual(1, publications);
                Assert.AreEqual(
                    RealmId.Crownlands,
                    Owner(save.CurrentSave, "T5"));
                Assert.AreEqual(100, save.CurrentSave.WarzoneCredits);
                Assert.AreEqual(
                    1,
                    save.CurrentSave.Quests.Single(item => item.QuestId == "Q5")
                        .CurrentValue);

                LocalSaveGameService reloaded = CreateService(root);
                reloaded.Load();
                var reloadedWarzone = new WarzoneService(reloaded);
                int replayPublications = 0;
                reloadedWarzone.OnTerritoryCaptured += (_, __) =>
                    replayPublications++;

                TerritoryCaptureApplicationResult replay =
                    reloadedWarzone.ApplyCaptureTransaction(request);

                Assert.AreEqual(
                    TerritoryApplyDisposition.Replayed,
                    replay.Disposition);
                Assert.AreEqual(0, replayPublications);
                Assert.AreEqual(
                    RealmId.Crownlands,
                    Owner(reloaded.CurrentSave, "T5"));
                Assert.AreEqual(100, reloaded.CurrentSave.WarzoneCredits);
                Assert.AreEqual(
                    1,
                    reloaded.CurrentSave.Quests
                        .Single(item => item.QuestId == "Q5")
                        .CurrentValue);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ReplayRechecksDurableAuthorityBeforeReportingCommittedHistory()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                TerritoryCaptureTransactionRequest request = Request(
                    TerritoryCaptureTransactionService.CreateForTests(save),
                    save.CurrentSave,
                    "capture-T5-replay-authority");
                var warzone = new WarzoneService(save);
                Assert.AreEqual(
                    TerritoryApplyDisposition.Committed,
                    warzone.ApplyCaptureTransaction(request).Disposition);
                File.AppendAllText(Path.Combine(root, "save.json"), " ");

                TerritoryCaptureApplicationResult replay =
                    ApplyIgnoringFailureLogs(warzone, request);

                Assert.AreEqual(TerritoryApplyDisposition.RolledBack, replay.Disposition);
                Assert.That(
                    replay.Diagnostics.Select(item => item.Code),
                    Does.Contain("CaptureRolledBack"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ProductionCaptureRejectsNonCommandAuthorizationBeforeMutation()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                TerritoryCaptureTransactionRequest accepted = Request(
                    TerritoryCaptureTransactionService.CreateForTests(save),
                    save.CurrentSave,
                    "capture-T5-fake-authorization");
                TerritoryCaptureTransactionRequest rejectedRequest =
                    WithAuthorizationSource(
                        accepted,
                        TerritoryCaptureAuthorizationSource.FakeTestOutcome);
                var warzone = new WarzoneService(save);
                int publications = 0;
                warzone.OnTerritoryCaptured += (_, __) => publications++;

                TerritoryCaptureApplicationResult result =
                    warzone.ApplyCaptureTransaction(rejectedRequest);

                Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
                Assert.AreEqual(0, publications);
                Assert.AreEqual(RealmId.None, Owner(save.CurrentSave, "T5"));
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain("AuthorizationSourceUnavailable"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ProfileBoundStoreRejectsNonCommandAuthorizationDirectly()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                var planner = TerritoryPhaseBPlanner.CreateCurrentBaseline();
                TerritoryCaptureTransactionRequest accepted = Request(
                    TerritoryCaptureTransactionService.CreateForTests(save),
                    save.CurrentSave,
                    "capture-T5-direct-fake-authorization");

                TerritoryCaptureApplicationResult result =
                    ((IProfileBoundTerritoryCaptureCandidateStore)save)
                    .TryCommitProfileBoundTerritoryCapture(
                        WithAuthorizationSource(
                            accepted,
                            TerritoryCaptureAuthorizationSource.FakeTestOutcome),
                        planner);

                Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
                Assert.AreEqual(RealmId.None, Owner(save.CurrentSave, "T5"));
                Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
                Assert.That(
                    result.Diagnostics.Select(item => item.Code),
                    Does.Contain("AuthorizationSourceUnavailable"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void RejectedCandidateCommitCannotReturnStagedCommittedResult()
        {
            MethodInfo resolve = typeof(LocalSaveGameService).GetMethod(
                "ResolveRejectedTerritoryCaptureCommit",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(
                resolve,
                "Rejected durable outcomes need an explicit fail-closed mapping seam.");
            var staged = new TerritoryCaptureApplicationResult(
                TerritoryApplyDisposition.Committed,
                null,
                null,
                null,
                Array.Empty<TerritoryDiagnostic>());

            var result = (TerritoryCaptureApplicationResult)resolve.Invoke(
                null,
                new object[] { "T5", staged, null, "store rejected" });

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
            Assert.Null(result.Event);
            Assert.That(
                result.Diagnostics.Select(item => item.Code),
                Does.Contain("CaptureCommitRejected"));
        }

        [Test]
        public void UncertainProfileReceiptCannotAuthorizeCommittedOrReplayResult()
        {
            MethodInfo verify = typeof(LocalSaveGameService).GetMethod(
                "HasCommittedTerritoryCaptureAuthorityReceipt",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(
                verify,
                "Territory publication must check the profile-bound authority receipt.");
            ConstructorInfo receiptConstructor = typeof(ProfileMutationReceipt)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            var uncertainReceipt = (ProfileMutationReceipt)receiptConstructor.Invoke(
                new object[]
                {
                    ProfileMutationReceiptStatus.CommitUncertain,
                    (ulong)1,
                    "profile",
                    "expected-generation",
                    string.Empty,
                    string.Empty,
                    TerritoryCaptureSaveAuthority.OperationId,
                    "result",
                    string.Empty,
                    true,
                    new[] { "AL-TEST-UNCERTAIN" }
                });
            var bound = new ProfileBoundSaveCandidateCommitResult(
                new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Duplicate,
                    null,
                    "uncertain authority"),
                uncertainReceipt);

            Assert.False((bool)verify.Invoke(null, new object[] { bound }));
        }

        [Test]
        public void CallerForwardsOnlyExternallySuppliedAcceptedAuthorization()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                var builder = TerritoryCaptureTransactionService.CreateForTests(save);
                TerritoryCaptureTransactionRequest expected =
                    Request(builder, save.CurrentSave, "calleraccepted00000000000000000001");
                var tracking = new TrackingTerritoryService();
                var accepted = new TerritoryCaptureAcceptedCommandResult(
                    expected.CaptureRequest.OperationId,
                    expected.CaptureRequest.TerritoryId,
                    expected.CaptureRequest.ExpectedPreviousOwner,
                    expected.CaptureRequest.ExpectedRevision,
                    expected.CaptureRequest.Authorization,
                    expected.AuthorizationEvaluationUtcTicks);

                TerritoryCaptureCaller.ApplyAcceptedResult(tracking, save, accepted);

                Assert.AreEqual(1, tracking.ApplyCount);
                Assert.AreSame(
                    expected.CaptureRequest.Authorization,
                    tracking.Request.CaptureRequest.Authorization);
                Assert.AreEqual(
                    RealmId.Crownlands,
                    tracking.Request.CaptureRequest.CommittedProfileRealm);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void CallerRejectsMissingAcceptedResultWithoutInvokingTerritoryService()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                var tracking = new TrackingTerritoryService();

                TerritoryCaptureApplicationResult result =
                    TerritoryCaptureCaller.ApplyAcceptedResult(
                        tracking,
                        save,
                        null);

                Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
                Assert.AreEqual(0, tracking.ApplyCount);
                Assert.That(
                    TerritoryCapturePresentation.Describe(result),
                    Does.StartWith("Capture rejected:"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void ApprovalSaveWrapperExposesProfileBoundTerritoryCaptureAuthority()
        {
            Assert.That(
                typeof(IProfileBoundTerritoryCaptureCandidateStore).IsAssignableFrom(
                    typeof(MvpApprovalTransactionalSaveGameService)),
                Is.True,
                "Approval/device acceptance must retain the same profile-bound capture boundary as local saves.");
        }

        [Test]
        [Platform("Win")]
        [Parallelizable(ParallelScope.None)]
        public void ApprovalSaveWrapperCommitsAndVerifiesReplayThroughDurableAuthority()
        {
            string root = CreateRoot();
            string normalRoot = Path.Combine(root, "normal-approval-capture");
            Directory.CreateDirectory(normalRoot);
            string previousRegistryOverride =
                MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests;
            string registryPath =
                @"Software\AnotherLife\Tests\MvpApprovalVfsV1\" +
                Guid.NewGuid().ToString("N");
            MvpApprovalVirtualStore store = null;
            try
            {
                MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests = registryPath;
                Assert.True(
                    MvpApprovalSlotPlan.TryCreate(
                        normalRoot,
                        out MvpApprovalSlotPlan plan,
                        out string planFailure),
                    planFailure);
                Assert.True(
                    MvpApprovalVirtualStore.TryPrepare(
                        plan,
                        out store,
                        out string storeFailure),
                    storeFailure);
                var files = new MvpApprovalSaveFileOperations(plan.SaveRoot, store);
                var inner = new LocalSaveGameService(plan.SaveRoot, files);
                var service = new MvpApprovalTransactionalSaveGameService(store, inner);
                service.Load();
                RealmSelectionResult realm =
                    ((IProfileBoundRealmSelectionCandidateStore)service)
                    .TryCommitProfileBoundRealmSelection(
                        new RealmSelectionRequest(
                            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                            RealmId.Crownlands));
                Assert.AreEqual(RealmSelectionStatus.Committed, realm.Status);
                Assert.AreEqual(
                    MvpApprovalStartNewDisposition.Succeeded,
                    service.ExecuteReset(candidate =>
                    {
                        SeedTerritories(candidate);
                        return MvpApprovalStartNewDisposition.Succeeded;
                    }));
                var builder = TerritoryCaptureTransactionService.CreateForTests(service);
                TerritoryCaptureTransactionRequest request = Request(
                    builder,
                    service.CurrentSave,
                    "capture-T5-approval-wrapper");
                int commitsBefore = store.CommitCountForTests;

                TerritoryCaptureApplicationResult committed =
                    ((IProfileBoundTerritoryCaptureCandidateStore)service)
                    .TryCommitProfileBoundTerritoryCapture(request, builder.Planner);
                TerritoryCaptureApplicationResult replay =
                    ((IProfileBoundTerritoryCaptureCandidateStore)service)
                    .TryCommitProfileBoundTerritoryCapture(request, builder.Planner);

                Assert.AreEqual(TerritoryApplyDisposition.Committed, committed.Disposition);
                Assert.AreEqual(TerritoryApplyDisposition.Replayed, replay.Disposition);
                Assert.AreEqual(commitsBefore + 1, store.CommitCountForTests);
                Assert.AreEqual(100, service.CurrentSave.WarzoneCredits);
                Assert.False(service.PersistenceFrozen);
            }
            finally
            {
                store?.DeletePersistentDataForTests();
                store?.Revoke();
                WindowsRegistryValueStore.DeleteTestSubKeyAndFlush(registryPath);
                MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests =
                    previousRegistryOverride;
                DeleteRoot(root);
            }
        }

        [TestCase(TerritoryApplyDisposition.Committed, "Territory secured")]
        [TestCase(TerritoryApplyDisposition.Replayed, "Capture already committed")]
        [TestCase(TerritoryApplyDisposition.NoChange, "No territory change")]
        [TestCase(TerritoryApplyDisposition.RolledBack, "Capture rolled back")]
        [TestCase(TerritoryApplyDisposition.CommitUncertain, "Capture save is uncertain")]
        [TestCase(TerritoryApplyDisposition.Rejected, "Capture rejected")]
        public void PresentationReflectsActualApplicationDisposition(
            TerritoryApplyDisposition disposition,
            string expectedPrefix)
        {
            var result = new TerritoryCaptureApplicationResult(
                disposition,
                null,
                null,
                null,
                Array.Empty<TerritoryDiagnostic>());

            Assert.That(
                TerritoryCapturePresentation.Describe(result),
                Does.StartWith(expectedPrefix));
        }

        [Test]
        public void DurableWriteFailureRollsBackAndReloadsPreviousTerritoryState()
        {
            string root = CreateRoot();
            try
            {
                var files = new GatedSaveFileOperations();
                LocalSaveGameService save = CreateWritable(root, files);
                TerritoryCaptureTransactionRequest request = Request(
                    TerritoryCaptureTransactionService.CreateForTests(save),
                    save.CurrentSave,
                    "capture-T5-rollback");
                string primaryBefore = File.ReadAllText(
                    Path.Combine(root, "save.json"));
                files.FailDurableWrites = true;
                TerritoryCaptureApplicationResult result =
                    ApplyIgnoringFailureLogs(new WarzoneService(save), request);

                Assert.AreEqual(
                    TerritoryApplyDisposition.RolledBack,
                    result.Disposition);
                Assert.AreEqual(RealmId.None, Owner(save.CurrentSave, "T5"));
                Assert.AreEqual(
                    primaryBefore,
                    File.ReadAllText(Path.Combine(root, "save.json")));

                LocalSaveGameService reloaded = CreateService(root);
                reloaded.Load();
                Assert.AreEqual(RealmId.None, Owner(reloaded.CurrentSave, "T5"));
                Assert.AreEqual(0, reloaded.CurrentSave.WarzoneCredits);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void PostCommitCleanupFailureReturnsUncertainAndPublishesNoEvent()
        {
            string root = CreateRoot();
            try
            {
                var files = new GatedSaveFileOperations();
                LocalSaveGameService save = CreateWritable(root, files);
                TerritoryCaptureTransactionRequest request = Request(
                    TerritoryCaptureTransactionService.CreateForTests(save),
                    save.CurrentSave,
                    "capture-T5-uncertain");
                var warzone = new WarzoneService(save);
                int publications = 0;
                warzone.OnTerritoryCaptured += (_, __) => publications++;
                files.FailPreviousDelete = true;

                TerritoryCaptureApplicationResult result =
                    ApplyIgnoringFailureLogs(warzone, request);

                Assert.AreEqual(
                    TerritoryApplyDisposition.CommitUncertain,
                    result.Disposition);
                Assert.AreEqual(0, publications);
                Assert.NotNull(result.Receipt);
                Assert.AreEqual(
                    TerritoryOperationDurability.CommitUncertain,
                    result.Receipt.Durability);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.CommitUncertain,
                    save.GetCurrentAuthority().Status);

                files.FailPreviousDelete = false;
                LocalSaveGameService reloaded = CreateService(root);
                reloaded.Load();
                Assert.Null(reloaded.CurrentSave);
                Assert.AreEqual(
                    SaveLoadStatus.RecoveryRequired,
                    reloaded.LastLoadStatus);
                Assert.NotNull(reloaded.ReadOnlyCandidateSnapshot);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.RecoveryRequired,
                    reloaded.GetCurrentAuthority().Status);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void NoChangeAndStaleResultsPersistNothingAcrossReload()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateWritable(root);
                var builder = TerritoryCaptureTransactionService.CreateForTests(save);
                var warzone = new WarzoneService(save);
                int publications = 0;
                warzone.OnTerritoryCaptured += (_, __) => publications++;
                string primaryBefore = File.ReadAllText(
                    Path.Combine(root, "save.json"));

                TerritoryCaptureApplicationResult noChange =
                    warzone.ApplyCaptureTransaction(
                        Request(
                            builder,
                            save.CurrentSave,
                            "capture-T3-no-change",
                            "T3"));
                TerritoryCaptureApplicationResult stale =
                    warzone.ApplyCaptureTransaction(
                        Request(
                            builder,
                            save.CurrentSave,
                            "capture-T5-stale",
                            "T5",
                            RealmId.Stonehold));

                Assert.AreEqual(
                    TerritoryApplyDisposition.NoChange,
                    noChange.Disposition);
                Assert.AreEqual(
                    TerritoryApplyDisposition.Rejected,
                    stale.Disposition);
                Assert.AreEqual(0, publications);
                Assert.AreEqual(
                    primaryBefore,
                    File.ReadAllText(Path.Combine(root, "save.json")));

                LocalSaveGameService reloaded = CreateService(root);
                reloaded.Load();
                Assert.AreEqual(RealmId.Crownlands, Owner(reloaded.CurrentSave, "T3"));
                Assert.AreEqual(RealmId.None, Owner(reloaded.CurrentSave, "T5"));
                Assert.AreEqual(0, reloaded.CurrentSave.WarzoneCredits);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static TerritoryCaptureTransactionRequest Request(
            TerritoryCaptureTransactionService service,
            SaveGameData save,
            string operationId,
            string territoryId = "T5",
            RealmId? expectedPreviousOwner = null)
        {
            IReadOnlyList<TerritoryStateRecord> states =
                TerritoryCaptureTransactionService.ReadStates(
                    save,
                    service.Planner.Catalog);
            TerritoryQueryResult query = service.Planner.BuildQuery(
                states,
                RealmId.Crownlands,
                TerritoryCaptureTransactionService.LocalProfileSessionId);
            TerritoryStateRecord current =
                states.Single(item => item.Id == territoryId);
            RealmId expectedOwner = expectedPreviousOwner ?? current.Owner;
            var authorization = new TerritoryCaptureAuthorization(
                "auth-" + operationId,
                TerritoryCaptureAuthorizationSource.CommandResult,
                TerritoryCaptureTransactionService.LocalProfileSessionId,
                territoryId,
                RealmId.Crownlands,
                expectedOwner,
                current.Revision,
                "source-result-" + operationId,
                TerritorySemanticHasher.HashFrames(
                    "command-capture-outcome",
                    operationId),
                long.MaxValue,
                TerritoryAuthorizationUsePolicy.SingleUse);
            return new TerritoryCaptureTransactionRequest(
                new TerritoryCaptureRequest(
                    operationId,
                    territoryId,
                    RealmId.Crownlands,
                    RealmId.Crownlands,
                    expectedOwner,
                    current.Revision,
                    authorization),
                service.Planner.Catalog.Identity,
                query.StateRevisionHash,
                TerritoryCaptureTransactionService.LocalProfileSessionId,
                1);
        }

        private static TerritoryCaptureTransactionRequest WithAuthorizationSource(
            TerritoryCaptureTransactionRequest request,
            TerritoryCaptureAuthorizationSource source)
        {
            TerritoryCaptureRequest capture = request.CaptureRequest;
            TerritoryCaptureAuthorization authorization = capture.Authorization;
            return new TerritoryCaptureTransactionRequest(
                new TerritoryCaptureRequest(
                    capture.OperationId,
                    capture.TerritoryId,
                    capture.CommittedProfileRealm,
                    capture.ExpectedCapturerRealm,
                    capture.ExpectedPreviousOwner,
                    capture.ExpectedRevision,
                    new TerritoryCaptureAuthorization(
                        authorization.AuthorizationId,
                        source,
                        authorization.ProfileSessionId,
                        authorization.TerritoryId,
                        authorization.CapturerRealm,
                        authorization.ExpectedPreviousOwner,
                        authorization.ExpectedRevision,
                        authorization.SourceResultId,
                        authorization.SourceResultHash,
                        authorization.ExpiresAtUtcTicks,
                        authorization.UsePolicy)),
                request.ExpectedCatalogIdentity,
                request.ExpectedStateRevisionHash,
                request.ProfileSessionId,
                request.AuthorizationEvaluationUtcTicks);
        }

        private static LocalSaveGameService CreateWritable(string root)
        {
            return CreateWritable(root, new SystemSaveFileOperations());
        }

        private static LocalSaveGameService CreateWritable(
            string root,
            ISaveFileOperations fileOperations)
        {
            LocalSaveGameService save = CreateService(root, fileOperations);
            save.CreateNewSave(RealmId.None);
            Assert.NotNull(
                save.CurrentSave,
                save.LastLoadStatus + ": " + save.LastLoadMessage);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                save.GetCurrentAuthority().Status);
            RealmSelectionResult realm =
                ((IProfileBoundRealmSelectionCandidateStore)save)
                .TryCommitProfileBoundRealmSelection(
                    new RealmSelectionRequest(
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        RealmId.Crownlands));
            Assert.AreEqual(RealmSelectionStatus.Committed, realm.Status);
            SeedTerritories(save);
            return save;
        }

        private static void SeedTerritories(LocalSaveGameService save)
        {
            ProfileWriteAuthoritySnapshot before = save.GetCurrentAuthority();
            ProfileBoundSaveCandidateCommitResult bound =
                ((IProfileBoundSaveGameCandidateStore)save).TryCommitCandidate(
                    ProfileAuthorityExpectation.From(before),
                    "al.test.seed-territory-capture.v1",
                    "al.test.seed-territory-capture.1",
                    candidate =>
                    {
                        candidate.WarzoneCredits = 0;
                        candidate.Territories = BaselineTerritories();
                        candidate.Quests = new List<QuestState>
                        {
                            new QuestState
                            {
                                QuestId = "Q5",
                                CurrentValue = 0,
                                IsCompleted = false,
                                IsClaimed = false
                            }
                        };
                        return SaveCandidateMutationPreparation.Prepared();
                    });
            Assert.NotNull(bound);
            Assert.True(bound.CommitResult.IsCommitted, bound.CommitResult.Message);
        }

        private static List<TerritoryData> BaselineTerritories() =>
            new List<TerritoryData>
            {
                Territory("T1", "Iron Peaks", RealmId.Stonehold, ResourceType.Stone, 50, true),
                Territory("T2", "Silver Woods", RealmId.Eldergrove, ResourceType.Wood, 40, false),
                Territory("T3", "Golden Plains", RealmId.Crownlands, ResourceType.Gold, 20, false),
                Territory("T4", "Shadow Vale", RealmId.Umbral, ResourceType.Food, 60, true),
                Territory("T5", "Neutral Borderlands", RealmId.None, ResourceType.Gold, 10, false)
            };

        private static TerritoryData Territory(
            string id,
            string name,
            RealmId owner,
            ResourceType bonusType,
            long bonusAmount,
            bool fortress) =>
            new TerritoryData
            {
                Id = id,
                Name = name,
                OwnerRealm = owner,
                BonusType = bonusType,
                BonusAmount = bonusAmount,
                IsFortress = fortress
            };

        private static RealmId Owner(SaveGameData save, string territoryId) =>
            save.Territories.Single(item => item.Id == territoryId).OwnerRealm;

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
            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root, fileOperations });
        }

        private static TerritoryCaptureApplicationResult ApplyIgnoringFailureLogs(
            WarzoneService warzone,
            TerritoryCaptureTransactionRequest request)
        {
            bool previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                return warzone.ApplyCaptureTransaction(request);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
            }
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-TerritoryCapture",
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

        private sealed class TrackingTerritoryService : ITerritoryService
        {
            public int ApplyCount { get; private set; }
            public TerritoryCaptureTransactionRequest Request { get; private set; }

            public event Action<string, RealmId> OnTerritoryCaptured;

            public IEnumerable<TerritoryData> GetTerritories() =>
                Array.Empty<TerritoryData>();

            public void CaptureTerritory(string territoryId, RealmId capturer)
            {
            }

            public TerritoryCaptureApplicationResult ApplyCaptureTransaction(
                TerritoryCaptureTransactionRequest request)
            {
                ApplyCount++;
                Request = request;
                return new TerritoryCaptureApplicationResult(
                    TerritoryApplyDisposition.Rejected,
                    null,
                    null,
                    null,
                    Array.Empty<TerritoryDiagnostic>());
            }

            public long CalculatePassiveIncome(ResourceType type) => 0;
        }

        private sealed class GatedSaveFileOperations : ISaveFileOperations
        {
            private readonly SystemSaveFileOperations _inner =
                new SystemSaveFileOperations();

            public bool FailDurableWrites;
            public bool FailPreviousDelete;

            public bool FileExists(string path) => _inner.FileExists(path);

            public void CreateDirectory(string path) =>
                _inner.CreateDirectory(path);

            public SaveFileReadResult ReadAllBytesBounded(
                string path,
                int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);

            public SaveFileWriteResult WriteAllTextDurable(
                string path,
                string contents)
            {
                return FailDurableWrites
                    ? new SaveFileWriteResult(
                        false,
                        false,
                        "AL-TEST-TERRITORY-WRITE-FAILED")
                    : _inner.WriteAllTextDurable(path, contents);
            }

            public void Copy(
                string sourcePath,
                string destinationPath,
                bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);

            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);

            public void Delete(string path)
            {
                if (FailPreviousDelete &&
                    string.Equals(
                        Path.GetFileName(path),
                        "save.previous.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("AL-TEST-TERRITORY-DELETE-FAILED");
                }

                _inner.Delete(path);
            }

            public IEnumerable<string> EnumerateFiles(
                string directoryPath,
                string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);

            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);

            public bool IsReparsePoint(string path) =>
                _inner.IsReparsePoint(path);
        }
    }
}
