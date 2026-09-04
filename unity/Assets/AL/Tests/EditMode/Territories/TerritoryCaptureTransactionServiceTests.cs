using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmWar.Territories;
using AL.RealmWar.Territories.Contracts;
using AL.RealmWar.Warzone;
using NUnit.Framework;

namespace AL.Tests.EditMode.Territories
{
    public sealed class TerritoryCaptureTransactionServiceTests
    {
        [Test]
        public void CatalogBackedCaptureCommitsOwnerCreditsReceiptAndOutboxOnce()
        {
            FakeSaveGameService save = WritableSave();
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureTransactionRequest request = Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-first");
            TerritoryCaptureApplicationResult result = service.ApplyCapture(request);

            Assert.AreEqual(TerritoryApplyDisposition.Committed, result.Disposition);
            Assert.AreEqual(RealmId.Crownlands, Owner(save, "T5"));
            Assert.AreEqual(100, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(1, save.SaveCount);
            Assert.NotNull(result.Receipt);
            Assert.AreEqual("capture-T5-first", result.Receipt.OperationId);
            Assert.AreEqual(TerritoryOperationDurability.Committed, result.Receipt.Durability);
            Assert.NotNull(result.Event);
            Assert.AreEqual(RealmId.None, result.Event.PreviousOwner);
            Assert.AreEqual(RealmId.Crownlands, result.Event.NewOwner);
            Assert.AreEqual(1, result.Event.NewRevision);
            Assert.NotNull(save.CurrentSave.TerritoryCaptureLedger);
            Assert.AreEqual(1, save.CurrentSave.TerritoryCaptureLedger.Receipts.Count);
            Assert.AreEqual(1, save.CurrentSave.TerritoryCaptureLedger.Outbox.Count);
            Assert.AreEqual(1, save.CurrentSave.Quests.Single(item => item.QuestId == "Q5").CurrentValue);
        }

        [Test]
        public void SameOwnerRepeatIsExplicitNoChangeAndDoesNotMutateRewards()
        {
            FakeSaveGameService save = WritableSave();
            var service = TerritoryCaptureTransactionService.CreateForTests(save);
            service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-first"));
            int credits = save.CurrentSave.WarzoneCredits;
            int saves = save.SaveCount;
            int quest = save.CurrentSave.Quests.Single(item => item.QuestId == "Q5").CurrentValue;

            TerritoryCaptureApplicationResult result = service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-second"));

            Assert.AreEqual(TerritoryApplyDisposition.NoChange, result.Disposition);
            Assert.AreEqual(TerritoryCaptureStatus.NoChangeSameOwner, result.Plan.Status);
            Assert.AreEqual(RealmId.Crownlands, Owner(save, "T5"));
            Assert.AreEqual(credits, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(quest, save.CurrentSave.Quests.Single(item => item.QuestId == "Q5").CurrentValue);
            Assert.AreEqual(saves, save.SaveCount);
            Assert.AreEqual(1, save.CurrentSave.TerritoryCaptureLedger.Receipts.Count);
        }

        [Test]
        public void ReplayOfCommittedOperationIdDoesNotTouchSaveOrRewards()
        {
            FakeSaveGameService save = WritableSave();
            var service = TerritoryCaptureTransactionService.CreateForTests(save);
            TerritoryCaptureTransactionRequest request = Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-replay");
            TerritoryCaptureApplicationResult first = service.ApplyCapture(request);
            int credits = save.CurrentSave.WarzoneCredits;
            int saves = save.SaveCount;

            TerritoryCaptureApplicationResult replay = service.ApplyCapture(request);

            Assert.AreEqual(TerritoryApplyDisposition.Committed, first.Disposition);
            Assert.AreEqual(TerritoryApplyDisposition.Replayed, replay.Disposition);
            Assert.AreEqual(first.Receipt.ReceiptId, replay.Receipt.ReceiptId);
            Assert.AreEqual(credits, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(saves, save.SaveCount);
        }

        [Test]
        public void StaleExpectedRevisionDoesNotMutate()
        {
            FakeSaveGameService save = WritableSave();
            var service = TerritoryCaptureTransactionService.CreateForTests(save);
            TerritoryCaptureTransactionRequest staleRequest = Request(
                service,
                save,
                "T5",
                RealmId.Umbral,
                "capture-T5-stale");
            service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-first"));
            int credits = save.CurrentSave.WarzoneCredits;

            TerritoryCaptureApplicationResult stale = service.ApplyCapture(staleRequest);

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, stale.Disposition);
            Assert.AreEqual(RealmId.Crownlands, Owner(save, "T5"));
            Assert.AreEqual(credits, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(1, save.SaveCount);
        }

        [Test]
        public void MalformedAndUnknownTargetsAreExplicitAndNonMutating()
        {
            FakeSaveGameService save = WritableSave();
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult blank = service.ApplyCapture(Request(
                service,
                save,
                " ",
                RealmId.Crownlands,
                "capture-blank"));
            TerritoryCaptureApplicationResult unknown = service.ApplyCapture(Request(
                service,
                save,
                "T99",
                RealmId.Crownlands,
                "capture-unknown"));
            TerritoryCaptureApplicationResult noRealm = service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.None,
                "capture-none"));

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, blank.Disposition);
            Assert.AreEqual(TerritoryApplyDisposition.Rejected, unknown.Disposition);
            Assert.AreEqual(TerritoryApplyDisposition.Rejected, noRealm.Disposition);
            Assert.AreEqual(RealmId.None, Owner(save, "T5"));
            Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(0, save.SaveCount);
            Assert.IsNull(save.CurrentSave.TerritoryCaptureLedger);
        }

        [Test]
        public void SaveFailureRollsBackOwnershipAndRewards()
        {
            FakeSaveGameService save = WritableSave();
            save.NextSaveStatus = SaveOperationStatus.SaveFailedPreviousPreserved;
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult result = service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-save-fail"));

            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, result.Disposition);
            Assert.AreEqual(RealmId.None, Owner(save, "T5"));
            Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(0, save.CurrentSave.Quests.Single(item => item.QuestId == "Q5").CurrentValue);
            Assert.IsNull(save.CurrentSave.TerritoryCaptureLedger);
        }

        [Test]
        public void MissingSaveStatusRollsBackInsteadOfClaimingCommit()
        {
            FakeSaveGameService save = WritableSave();
            save.NextSaveStatus = SaveOperationStatus.None;
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult result = service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-status-none"));

            Assert.AreEqual(TerritoryApplyDisposition.RolledBack, result.Disposition);
            Assert.AreEqual(RealmId.None, Owner(save, "T5"));
            Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
            Assert.IsNull(save.CurrentSave.TerritoryCaptureLedger);
        }

        [Test]
        public void FakeAuthorizationSourceIsRejectedByProductionTransaction()
        {
            FakeSaveGameService save = WritableSave();
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult result = service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-fake-source",
                TerritoryCaptureAuthorizationSource.FakeTestOutcome));

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
            Assert.True(result.Diagnostics.Any(item =>
                item.Code == "AuthorizationSourceUnavailable"));
            Assert.AreEqual(RealmId.None, Owner(save, "T5"));
            Assert.AreEqual(0, save.SaveCount);
        }

        [Test]
        public void MissingPersistedTerritoryDoesNotHydrateOrMutate()
        {
            FakeSaveGameService save = WritableSave();
            save.CurrentSave.Territories.RemoveAll(item => item.Id == "T5");
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult result = service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-missing-state"));

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
            Assert.AreEqual(4, save.CurrentSave.Territories.Count);
            Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(0, save.SaveCount);
            Assert.IsNull(save.CurrentSave.TerritoryCaptureLedger);
        }

        [Test]
        public void UncertainCommitIsExplicitAndLeavesReconciliationState()
        {
            FakeSaveGameService save = WritableSave();
            save.NextSaveStatus = SaveOperationStatus.CommitUncertain;
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureTransactionRequest request = Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-uncertain");
            TerritoryCaptureApplicationResult result = service.ApplyCapture(request);
            TerritoryCaptureApplicationResult retry = service.ApplyCapture(request);

            Assert.AreEqual(TerritoryApplyDisposition.CommitUncertain, result.Disposition);
            Assert.AreEqual(
                TerritoryOperationDurability.CommitUncertain,
                result.Receipt.Durability);
            Assert.AreEqual(RealmId.Crownlands, Owner(save, "T5"));
            Assert.AreEqual(TerritoryApplyDisposition.Rejected, retry.Disposition);
            Assert.AreEqual(TerritoryCaptureStatus.CommitUncertain, retry.Plan.Status);
            Assert.AreEqual(1, save.SaveCount);
        }

        [Test]
        public void OldSaveWithoutLedgerMigratesOnFirstCommittedCapture()
        {
            FakeSaveGameService save = WritableSave();
            save.CurrentSave.TerritoryCaptureLedger = null;
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult result = service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-legacy"));

            Assert.AreEqual(TerritoryApplyDisposition.Committed, result.Disposition);
            Assert.NotNull(save.CurrentSave.TerritoryCaptureLedger);
            Assert.AreEqual(1, save.CurrentSave.TerritoryCaptureLedger.Version);
            Assert.AreEqual(RealmId.Crownlands, Owner(save, "T5"));
            Assert.AreEqual(0, Revision(save, "T1"));
            Assert.AreEqual(1, Revision(save, "T5"));
        }

        [Test]
        public void PassiveIncomeStillUsesCommittedRealmOwnerAfterCapture()
        {
            FakeSaveGameService save = WritableSave();
            save.CurrentSave.SelectedRealm = RealmId.Crownlands;
            var warzone = WarzoneService.CreateForTests(save);
            var service = TerritoryCaptureTransactionService.CreateForTests(save);

            long before = warzone.CalculatePassiveIncome(ResourceType.Gold);
            service.ApplyCapture(Request(
                service,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-income"));
            long after = warzone.CalculatePassiveIncome(ResourceType.Gold);

            Assert.AreEqual(20, before);
            Assert.AreEqual(30, after);
        }

        [Test]
        public void DirectTransactionServiceRejectsUnwritableSave()
        {
            FakeSaveGameService save = WritableSave();
            var requestBuilder =
                TerritoryCaptureTransactionService.CreateForTests(save);
            var service = new TerritoryCaptureTransactionService(save);
            TerritoryCaptureTransactionRequest request = Request(
                requestBuilder,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-direct-gated");

            TerritoryCaptureApplicationResult result = service.ApplyCapture(request);

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
            Assert.True(result.Diagnostics.Any(item => item.Code == "ProfileReadOnly"));
            Assert.AreEqual(RealmId.None, Owner(save, "T5"));
            Assert.AreEqual(0, save.SaveCount);
        }

        [Test]
        public void ProductionGateRejectsTypedCaptureBeforeAnyMutation()
        {
            FakeSaveGameService save = WritableSave();
            var warzone = new WarzoneService(save);
            var requestBuilder = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult result = warzone.ApplyCaptureTransaction(Request(
                requestBuilder,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-gated"));
            warzone.CaptureTerritory("T5", RealmId.Crownlands);

            Assert.AreEqual(TerritoryApplyDisposition.Rejected, result.Disposition);
            Assert.True(result.Diagnostics.Any(item => item.Code == "ProfileReadOnly"));
            Assert.AreEqual(RealmId.None, Owner(save, "T5"));
            Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(0, save.SaveCount);
        }

        [Test]
        public void LegacyCaptureWrapperDoesNotInferAuthorizationOrMutate()
        {
            FakeSaveGameService save = WritableSave();
            var warzone = WarzoneService.CreateForTests(save);

            warzone.CaptureTerritory("T5", RealmId.Crownlands);

            Assert.AreEqual(RealmId.None, Owner(save, "T5"));
            Assert.AreEqual(0, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(0, save.SaveCount);
            Assert.IsNull(save.CurrentSave.TerritoryCaptureLedger);
        }

        [Test]
        public void WritableWarzoneServiceSameOwnerDoesNotFarmCredits()
        {
            FakeSaveGameService save = WritableSave();
            var warzone = WarzoneService.CreateForTests(save);
            var requestBuilder = TerritoryCaptureTransactionService.CreateForTests(save);

            TerritoryCaptureApplicationResult first = warzone.ApplyCaptureTransaction(Request(
                requestBuilder,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-owned"));
            TerritoryCaptureApplicationResult repeat = warzone.ApplyCaptureTransaction(Request(
                requestBuilder,
                save,
                "T5",
                RealmId.Crownlands,
                "capture-T5-owned-again"));
            warzone.CaptureTerritory("T5", RealmId.Crownlands);

            Assert.AreEqual(TerritoryApplyDisposition.Committed, first.Disposition);
            Assert.AreEqual(TerritoryApplyDisposition.NoChange, repeat.Disposition);
            Assert.AreEqual(100, save.CurrentSave.WarzoneCredits);
            Assert.AreEqual(RealmId.Crownlands, Owner(save, "T5"));
            Assert.AreEqual(1, save.SaveCount);
        }

        private static TerritoryCaptureTransactionRequest Request(
            TerritoryCaptureTransactionService service,
            FakeSaveGameService save,
            string territoryId,
            RealmId capturer,
            string operationId,
            TerritoryCaptureAuthorizationSource source =
                TerritoryCaptureAuthorizationSource.CommandResult)
        {
            const string profileSessionId =
                TerritoryCaptureTransactionService.LocalProfileSessionId;
            IReadOnlyList<TerritoryStateRecord> states =
                TerritoryCaptureTransactionService.ReadStates(
                    save.CurrentSave,
                    service.Planner.Catalog);
            TerritoryQueryResult query = service.Planner.BuildQuery(
                states,
                capturer,
                profileSessionId);
            TerritoryStateRecord current = states.FirstOrDefault(item =>
                item != null &&
                item.Id == territoryId);
            RealmId previousOwner = current?.Owner ?? RealmId.None;
            long previousRevision = current?.Revision ?? 0;
            var authorization = new TerritoryCaptureAuthorization(
                "auth-" + operationId,
                source,
                profileSessionId,
                territoryId ?? string.Empty,
                capturer,
                previousOwner,
                previousRevision,
                "source-result-" + operationId,
                TerritorySemanticHasher.HashFrames(
                    "command-capture-outcome",
                    operationId),
                long.MaxValue,
                TerritoryAuthorizationUsePolicy.SingleUse);
            var capture = new TerritoryCaptureRequest(
                operationId,
                territoryId ?? string.Empty,
                capturer,
                capturer,
                previousOwner,
                previousRevision,
                authorization);
            return new TerritoryCaptureTransactionRequest(
                capture,
                service.Planner.Catalog.Identity,
                query.StateRevisionHash,
                profileSessionId,
                1);
        }

        private static FakeSaveGameService WritableSave()
        {
            var save = new FakeSaveGameService
            {
                CurrentSave = new SaveGameData
                {
                    SaveFormatId = SaveGameData.CurrentSaveFormatId,
                    SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
                    ProfileInitializationVersion =
                        SaveGameData.CurrentProfileInitializationVersion,
                    SelectedRealm = RealmId.Crownlands,
                    WarzoneCredits = 0,
                    Territories = BaselineTerritories(),
                    Quests = new List<QuestState>
                    {
                        new QuestState
                        {
                            QuestId = "Q5",
                            CurrentValue = 0,
                            IsCompleted = false,
                            IsClaimed = false
                        }
                    }
                }
            };
            return save;
        }

        private static List<TerritoryData> BaselineTerritories()
        {
            return new List<TerritoryData>
            {
                new TerritoryData
                {
                    Id = "T1",
                    Name = "Iron Peaks",
                    OwnerRealm = RealmId.Stonehold,
                    BonusType = ResourceType.Stone,
                    BonusAmount = 50,
                    IsFortress = true
                },
                new TerritoryData
                {
                    Id = "T2",
                    Name = "Silver Woods",
                    OwnerRealm = RealmId.Eldergrove,
                    BonusType = ResourceType.Wood,
                    BonusAmount = 40,
                    IsFortress = false
                },
                new TerritoryData
                {
                    Id = "T3",
                    Name = "Golden Plains",
                    OwnerRealm = RealmId.Crownlands,
                    BonusType = ResourceType.Gold,
                    BonusAmount = 20,
                    IsFortress = false
                },
                new TerritoryData
                {
                    Id = "T4",
                    Name = "Shadow Vale",
                    OwnerRealm = RealmId.Umbral,
                    BonusType = ResourceType.Food,
                    BonusAmount = 60,
                    IsFortress = true
                },
                new TerritoryData
                {
                    Id = "T5",
                    Name = "Neutral Borderlands",
                    OwnerRealm = RealmId.None,
                    BonusType = ResourceType.Gold,
                    BonusAmount = 10,
                    IsFortress = false
                }
            };
        }

        private static RealmId Owner(FakeSaveGameService save, string territoryId)
        {
            return save.CurrentSave.Territories
                .Single(item => item.Id == territoryId)
                .OwnerRealm;
        }

        private static long Revision(FakeSaveGameService save, string territoryId)
        {
            TerritoryOwnershipRevisionData row = save.CurrentSave.TerritoryCaptureLedger?
                .Revisions?
                .FirstOrDefault(item => item != null && item.TerritoryId == territoryId);
            return row?.Revision ?? 0;
        }

        private sealed class FakeSaveGameService : ISaveGameService
        {
            public SaveGameData CurrentSave { get; set; } = new SaveGameData();

            public SaveLoadStatus LastLoadStatus { get; private set; } =
                SaveLoadStatus.LoadedPrimary;

            public string LastLoadMessage { get; private set; } = string.Empty;

            public SaveOperationStatus LastSaveStatus { get; private set; } =
                SaveOperationStatus.SavedPrimary;

            public string LastSaveMessage { get; private set; } = string.Empty;

            public SaveOperationStatus NextSaveStatus { get; set; } =
                SaveOperationStatus.SavedPrimary;

            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
                LastSaveStatus = NextSaveStatus;
            }

            public void Load()
            {
                LastLoadStatus = SaveLoadStatus.LoadedPrimary;
            }

            public bool HasSave() => CurrentSave != null;

            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }

            public void DeleteSave()
            {
                CurrentSave = null;
            }
        }
    }
}
