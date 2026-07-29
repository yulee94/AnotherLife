using System;
using System.Collections;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public class BossRewardApplicationPlannerTests
    {
        [Test]
        public void FirstApplicationProducesCompleteImmutablePlan()
        {
            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(BossRewardPlanningStatus.Ready, result.Status);
            Assert.NotNull(result.Plan);
            Assert.AreEqual(10, result.Plan.CreditOperation.Previous);
            Assert.AreEqual(25, result.Plan.CreditOperation.Delta);
            Assert.AreEqual(35, result.Plan.CreditOperation.Next);
            Assert.AreEqual(1, result.Plan.InventoryOperations.Count);
            Assert.AreEqual(
                BossRewardInventoryOperationKind.Create,
                result.Plan.InventoryOperations[0].Kind);
            Assert.AreEqual(1, result.Plan.LedgerRecord.CommittedDrops.Count);
            Assert.AreEqual(2, result.Plan.DurableNotificationIntents.Count);
            Assert.AreEqual(1, result.Plan.PostCommitEvents.Count);
            Assert.AreEqual(64, result.Plan.PlanHash.Length);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.Plan.InventoryOperations).Clear());
        }

        [Test]
        public void ExistingCompatibleStackUsesCheckedUpdate()
        {
            OwnedEquipmentQueryResult inventory = BossRewardTestFixtures.Inventory(
                new[] { BossRewardTestFixtures.Owned(quantity: 2) });

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(inventory: inventory));

            Assert.AreEqual(BossRewardPlanningStatus.Ready, result.Status);
            BossRewardInventoryOperation operation = result.Plan.InventoryOperations[0];
            Assert.AreEqual(BossRewardInventoryOperationKind.Update, operation.Kind);
            Assert.AreEqual(2, operation.PreviousQuantity);
            Assert.AreEqual(1, operation.QuantityDelta);
            Assert.AreEqual(3, operation.NewQuantity);
            Assert.AreEqual(10, operation.CandidateRow.FirstAcquiredUtcSeconds);
            Assert.AreEqual(100, operation.CandidateRow.LastAcquiredUtcSeconds);
        }

        [Test]
        public void PreservedUnknownFutureRowDoesNotBlockKnownReward()
        {
            BossEquipmentDefinitionSnapshot future =
                BossRewardTestFixtures.Equipment("equipment_future");
            OwnedEquipmentQueryResult inventory =
                BossRewardTestFixtures.Inventory(
                    new[]
                    {
                        BossRewardTestFixtures.Owned(
                            future,
                            id: "equipment_future",
                            supported: false)
                    });

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(inventory: inventory));

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.PreservedUnknownFutureDefinition,
                inventory.Status);
            Assert.AreEqual(BossRewardPlanningStatus.Ready, result.Status);
            Assert.AreEqual(25, result.Plan.CreditOperation.Delta);
            Assert.AreEqual(1, result.Plan.InventoryOperations.Count);
            Assert.AreEqual(
                BossRewardTestFixtures.AlphaId,
                result.Plan.InventoryOperations[0].EquipmentDefinitionId);
        }

        [Test]
        public void ExactLedgerReplayReturnsStoredRecordWithoutPlan()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            BossRewardAppliedLedgerRecord record =
                BossRewardTestFixtures.LedgerRecord(computation.Value);
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { record },
                Array.Empty<BossRewardDiagnostic>(),
                true);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(
                BossRewardPlanningStatus.AlreadyCommitted,
                result.Status);
            Assert.IsNull(result.Plan);
            Assert.AreSame(record, result.ExistingRecord);
        }

        [Test]
        public void RetainedV1ReceiptRemainsReplayable()
        {
            BossRewardComputedValue source =
                BossRewardTestFixtures.Computation().Value;
            var provisional = new BossRewardComputedValue(
                source.GameId,
                source.CatalogSetId,
                source.ProfileId,
                source.RewardResultId,
                source.EncounterId,
                source.EncounterCompletionId,
                source.BossDefinitionId,
                source.BossDefinitionContentVersion,
                source.RewardProfileId,
                source.RewardProfileContentVersion,
                source.RewardProfileSha256,
                source.WarzoneCredits,
                source.IsExplicitNoReward,
                source.Drops,
                BossRewardTechnicalLimits.DeterminismVersionV1,
                BossRewardTestFixtures.ShaA);
            var value = new BossRewardComputedValue(
                provisional.GameId,
                provisional.CatalogSetId,
                provisional.ProfileId,
                provisional.RewardResultId,
                provisional.EncounterId,
                provisional.EncounterCompletionId,
                provisional.BossDefinitionId,
                provisional.BossDefinitionContentVersion,
                provisional.RewardProfileId,
                provisional.RewardProfileContentVersion,
                provisional.RewardProfileSha256,
                provisional.WarzoneCredits,
                provisional.IsExplicitNoReward,
                provisional.Drops,
                provisional.DeterminismVersion,
                BossRewardComputation.RecomputeComputationHash(provisional));
            var computation = new BossRewardComputationResult(
                BossRewardComputationStatus.Computed,
                value,
                Array.Empty<BossRewardDiagnostic>());
            BossRewardAppliedLedgerRecord sourceRecord =
                BossRewardTestFixtures.LedgerRecord(value);
            var retainedRecord = new BossRewardAppliedLedgerRecord(
                sourceRecord.GameId,
                sourceRecord.CatalogSetId,
                sourceRecord.ProfileId,
                sourceRecord.RewardResultId,
                sourceRecord.EncounterId,
                sourceRecord.EncounterCompletionId,
                sourceRecord.BossDefinitionId,
                sourceRecord.BossDefinitionContentVersion,
                sourceRecord.RewardProfileId,
                sourceRecord.RewardProfileContentVersion,
                sourceRecord.RewardProfileSha256,
                sourceRecord.ComputationHash,
                sourceRecord.WarzoneCredits,
                sourceRecord.IsExplicitNoReward,
                BossRewardTechnicalLimits.DeterminismVersionV1,
                sourceRecord.CommittedDrops,
                sourceRecord.CommittedUtcSeconds,
                BossRewardTechnicalLimits.ApplicationPolicyVersionV1,
                sourceRecord.NotificationCorrelationIds,
                sourceRecord.State);
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { retainedRecord },
                Array.Empty<BossRewardDiagnostic>(),
                true);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(
                    computation,
                    policyVersion:
                        BossRewardTechnicalLimits.ApplicationPolicyVersionV1),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(
                BossRewardPlanningStatus.AlreadyCommitted,
                result.Status);
            Assert.AreSame(retainedRecord, result.ExistingRecord);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void ExactReplayPrecedesStaleAndUnavailableMutationDomains()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            BossRewardAppliedLedgerRecord record =
                BossRewardTestFixtures.LedgerRecord(computation.Value);
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                "ledger_revision_after_commit",
                new[] { record },
                Array.Empty<BossRewardDiagnostic>(),
                true);
            var context = new BossRewardPlanningContext(
                true,
                "save_revision_after_commit",
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                "catalog_set_after_commit",
                null,
                new BossRewardEconomySnapshot(
                    false,
                    0,
                    0,
                    "economy_revision_after_commit"),
                null,
                ledger,
                Array.Empty<string>(),
                -1);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                context);

            Assert.AreEqual(
                BossRewardPlanningStatus.AlreadyCommitted,
                result.Status);
            Assert.AreSame(record, result.ExistingRecord);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void EncounterCompletionCannotBindASecondRewardResult()
        {
            BossRewardComputationResult first =
                BossRewardTestFixtures.Computation();
            BossRewardAppliedLedgerRecord committed =
                BossRewardTestFixtures.LedgerRecord(first.Value);
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { committed },
                Array.Empty<BossRewardDiagnostic>(),
                true);
            BossRewardComputationResult second =
                BossRewardTestFixtures.Computation(
                    request: BossRewardTestFixtures.Request(
                        rewardResultId: "result_second"));

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(second),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(
                BossRewardPlanningStatus.CorrelationConflict,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-LEDGER-COMPLETION-CONFLICT",
                result.Diagnostics[0].Code);
        }

        [Test]
        public void ResultIdReuseWithDifferentHashIsCorrelationConflict()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            BossRewardComputationResult conflictingComputation =
                BossRewardTestFixtures.Computation(
                    catalog: BossRewardTestFixtures.Catalog(
                        BossRewardTestFixtures.Profile(credits: 26)));
            BossRewardAppliedLedgerRecord record =
                BossRewardTestFixtures.LedgerRecord(conflictingComputation.Value);
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { record },
                Array.Empty<BossRewardDiagnostic>(),
                true);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(
                BossRewardPlanningStatus.CorrelationConflict,
                result.Status);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void PendingLedgerRecordRequiresRecovery()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[]
                {
                    BossRewardTestFixtures.LedgerRecord(
                        computation.Value,
                        state: BossRewardLedgerRecordState.PendingRecovery)
                },
                Array.Empty<BossRewardDiagnostic>(),
                true);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(BossRewardPlanningStatus.PendingRecovery, result.Status);
        }

        [Test]
        public void DuplicateOrNullLedgerRowsBlockPlanning()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            BossRewardAppliedLedgerRecord record =
                BossRewardTestFixtures.LedgerRecord(computation.Value);
            var duplicateLedger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { record, record },
                Array.Empty<BossRewardDiagnostic>(),
                true);
            var nullLedger = new BossRewardLedgerSnapshot(
                BossRewardTestFixtures.GameId,
                BossRewardTestFixtures.ProfileId,
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new BossRewardAppliedLedgerRecord[] { null },
                Array.Empty<BossRewardDiagnostic>(),
                true);

            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                BossRewardApplicationPlanner.Plan(
                    BossRewardTestFixtures.ApplicationRequest(computation),
                    BossRewardTestFixtures.PlanningContext(ledger: duplicateLedger)).Status);
            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                BossRewardApplicationPlanner.Plan(
                    BossRewardTestFixtures.ApplicationRequest(computation),
                    BossRewardTestFixtures.PlanningContext(ledger: nullLedger)).Status);
        }

        [Test]
        public void RevisionAndCatalogChangesReturnStaleTypedFailures()
        {
            BossRewardPlanningResult stale = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(
                    expectedInventoryRevision: "inventory_old"),
                BossRewardTestFixtures.PlanningContext());
            BossRewardPlanningResult drift = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(
                    expectedCatalogSetId: "catalog_old"),
                BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(BossRewardPlanningStatus.StalePlan, stale.Status);
            Assert.AreEqual(BossRewardPlanningStatus.CatalogDrift, drift.Status);
        }

        [Test]
        public void CreditAndQuantityOverflowAreTypedAndMutationFree()
        {
            var overflowingEconomy = new BossRewardEconomySnapshot(
                true,
                int.MaxValue - 10,
                int.MaxValue,
                BossRewardTestFixtures.EconomyRevision);
            BossRewardPlanningResult credit = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(economy: overflowingEconomy));

            OwnedEquipmentQueryResult inventory = BossRewardTestFixtures.Inventory(
                new[] { BossRewardTestFixtures.Owned(quantity: int.MaxValue) });
            BossRewardPlanningResult quantity = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(inventory: inventory));

            Assert.AreEqual(BossRewardPlanningStatus.CreditOverflow, credit.Status);
            Assert.AreEqual(
                BossRewardPlanningStatus.QuantityOverflow,
                quantity.Status);
            Assert.AreEqual(int.MaxValue, inventory.Items[0].Quantity);
        }

        [Test]
        public void ForgedSnapshotConflictIsRejectedBeforeStacking()
        {
            BossEquipmentDefinitionSnapshot definition =
                BossRewardTestFixtures.Equipment();
            var drifted = new OwnedEquipmentSnapshot(
                definition.EquipmentDefinitionId,
                "equipment_v2",
                BossRewardComputation.ComputeAcquisitionSnapshotFingerprint(definition),
                definition.SlotId,
                definition.AttackBonus,
                definition.DefenseBonus,
                definition.HealthBonus,
                definition.StackPolicyId,
                1,
                10,
                20,
                BossRewardTestFixtures.BossId,
                BossRewardTestFixtures.CompletionId,
                "prior_result",
                BossRewardTestFixtures.InventorySchemaVersion,
                true);
            var inventory = new OwnedEquipmentQueryResult(
                OwnedEquipmentQueryStatus.Valid,
                new[] { drifted },
                Array.Empty<BossRewardDiagnostic>(),
                BossRewardTestFixtures.InventoryRevision);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(inventory: inventory));

            Assert.AreEqual(
                BossRewardPlanningStatus.InventoryMalformed,
                result.Status);
        }

        [Test]
        public void MissingRequiredNotificationDefinitionBlocksPlan()
        {
            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(
                    notificationDefinitions: Array.Empty<string>()));

            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-NOTIFICATION-DEFINITION-UNAVAILABLE",
                result.Diagnostics[0].Code);
        }

        [Test]
        public void ExplicitNoRewardProducesLedgerOnlyPlan()
        {
            BossRewardProfile profile = BossRewardTestFixtures.Profile(
                entries: Array.Empty<BossRewardEntry>(),
                credits: 0,
                explicitNoReward: true);
            BossRewardCatalogSnapshot catalog =
                BossRewardTestFixtures.Catalog(profile);
            BossRewardComputationResult computation = BossRewardTestFixtures.Computation(
                catalog: catalog);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext(rewardCatalog: catalog));

            Assert.AreEqual(
                BossRewardPlanningStatus.ExplicitNoRewardReady,
                result.Status);
            Assert.IsTrue(result.Plan.LedgerRecord.IsExplicitNoReward);
            Assert.AreEqual(0, result.Plan.CreditOperation.Delta);
            Assert.IsEmpty(result.Plan.InventoryOperations);
        }

        [Test]
        public void TamperedComputedValueIsRejectedByCanonicalHash()
        {
            BossRewardComputationResult valid = BossRewardTestFixtures.Computation();
            BossRewardComputedValue value = valid.Value;
            var tampered = new BossRewardComputedValue(
                value.GameId,
                value.CatalogSetId,
                value.ProfileId,
                value.RewardResultId,
                value.EncounterId,
                value.EncounterCompletionId,
                value.BossDefinitionId,
                value.BossDefinitionContentVersion,
                value.RewardProfileId,
                value.RewardProfileContentVersion,
                value.RewardProfileSha256,
                value.WarzoneCredits + 1,
                value.IsExplicitNoReward,
                value.Drops,
                value.DeterminismVersion,
                value.ComputationHash);
            var computation = new BossRewardComputationResult(
                BossRewardComputationStatus.Computed,
                tampered,
                Array.Empty<BossRewardDiagnostic>());

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(
                BossRewardPlanningStatus.InvalidComputedResult,
                result.Status);
        }

        [Test]
        public void IdenticalSnapshotsProduceIdenticalPlanHash()
        {
            BossRewardApplicationRequest request =
                BossRewardTestFixtures.ApplicationRequest();
            BossRewardPlanningResult first = BossRewardApplicationPlanner.Plan(
                request,
                BossRewardTestFixtures.PlanningContext());
            BossRewardPlanningResult replay = BossRewardApplicationPlanner.Plan(
                request,
                BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(first.Plan.PlanHash, replay.Plan.PlanHash);
        }
    }
}
