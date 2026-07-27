using System;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public class BossRewardHardeningTests
    {
        [Test]
        public void HashConsistentNegativeCreditsAreRejected()
        {
            BossRewardComputedValue valid =
                BossRewardTestFixtures.Computation().Value;
            BossRewardComputationResult forged = ForgeComputation(
                valid,
                warzoneCredits: -1);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(forged),
                BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(
                BossRewardPlanningStatus.InvalidComputedResult,
                result.Status);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void HashConsistentPositiveGrantMustMatchCatalogAuthority()
        {
            BossRewardComputedValue valid =
                BossRewardTestFixtures.Computation().Value;
            BossRewardComputationResult forged = ForgeComputation(
                valid,
                warzoneCredits: valid.WarzoneCredits + 1);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(forged),
                BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(
                BossRewardPlanningStatus.InvalidComputedResult,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-TRANSACTION-CATALOG-AUTHORITY-MISMATCH",
                result.Diagnostics[0].Code);
        }

        [Test]
        public void ComputationCannotCrossSaveProfileOrCatalogAuthority()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            BossRewardPlanningResult profileMismatch =
                BossRewardApplicationPlanner.Plan(
                    BossRewardTestFixtures.ApplicationRequest(computation),
                    BossRewardTestFixtures.PlanningContext(
                        profileId: "profile_other"));

            BossRewardProfile changedProfile =
                BossRewardTestFixtures.Profile(credits: 26);
            BossRewardCatalogSnapshot changedCatalog =
                BossRewardTestFixtures.Catalog(changedProfile);
            BossRewardComputationResult changedComputation =
                BossRewardTestFixtures.Computation(catalog: changedCatalog);
            BossRewardPlanningResult catalogMismatch =
                BossRewardApplicationPlanner.Plan(
                    BossRewardTestFixtures.ApplicationRequest(changedComputation),
                    BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(
                BossRewardPlanningStatus.CatalogDrift,
                profileMismatch.Status);
            Assert.AreEqual(
                BossRewardPlanningStatus.InvalidComputedResult,
                catalogMismatch.Status);
        }

        [Test]
        public void HashConsistentInvalidAndDuplicateDropsAreRejected()
        {
            BossRewardComputedValue valid =
                BossRewardTestFixtures.Computation().Value;
            BossRewardComputedDrop original = valid.Drops[0];
            BossRewardComputationResult invalidQuantity = ForgeComputation(
                valid,
                drops: new[] { CloneDrop(original, quantity: 0) });
            BossRewardComputationResult duplicateIdentity = ForgeComputation(
                valid,
                drops: new[]
                {
                    CloneDrop(original),
                    CloneDrop(original)
                });

            BossRewardPlanningResult invalidResult =
                BossRewardApplicationPlanner.Plan(
                    BossRewardTestFixtures.ApplicationRequest(invalidQuantity),
                    BossRewardTestFixtures.PlanningContext());
            BossRewardPlanningResult duplicateResult =
                BossRewardApplicationPlanner.Plan(
                    BossRewardTestFixtures.ApplicationRequest(duplicateIdentity),
                    BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(
                BossRewardPlanningStatus.InvalidComputedResult,
                invalidResult.Status);
            Assert.AreEqual(
                BossRewardPlanningStatus.InvalidComputedResult,
                duplicateResult.Status);
        }

        [Test]
        public void NullDropEntryIsRejectedWithoutPlannerException()
        {
            BossRewardComputedValue valid =
                BossRewardTestFixtures.Computation().Value;
            BossRewardComputationResult forged = ForgeComputation(
                valid,
                drops: new BossRewardComputedDrop[] { null },
                recomputeHash: false);
            BossRewardPlanningResult result = null;

            Assert.DoesNotThrow(() =>
                result = BossRewardApplicationPlanner.Plan(
                    BossRewardTestFixtures.ApplicationRequest(forged),
                    BossRewardTestFixtures.PlanningContext()));
            Assert.NotNull(result);
            Assert.AreEqual(
                BossRewardPlanningStatus.InvalidComputedResult,
                result.Status);
        }

        [Test]
        public void NullDropCollectionIsRejectedAtImmutableBoundary()
        {
            BossRewardComputedValue valid =
                BossRewardTestFixtures.Computation().Value;

            Assert.Throws<ArgumentNullException>(() =>
                new BossRewardComputedValue(
                    valid.GameId,
                    valid.CatalogSetId,
                    valid.ProfileId,
                    valid.RewardResultId,
                    valid.EncounterId,
                    valid.EncounterCompletionId,
                    valid.BossDefinitionId,
                    valid.BossDefinitionContentVersion,
                    valid.RewardProfileId,
                    valid.RewardProfileContentVersion,
                    valid.RewardProfileSha256,
                    valid.WarzoneCredits,
                    valid.IsExplicitNoReward,
                    null,
                    valid.DeterminismVersion,
                    valid.ComputationHash));
        }

        [Test]
        public void ForgedInventoryStatusWrappersAreRejected()
        {
            OwnedEquipmentSnapshot row = BossRewardTestFixtures.Owned();
            var validButEmpty = new OwnedEquipmentQueryResult(
                OwnedEquipmentQueryStatus.Valid,
                Array.Empty<OwnedEquipmentSnapshot>(),
                Array.Empty<BossRewardDiagnostic>(),
                BossRewardTestFixtures.InventoryRevision);
            var emptyButPopulated = new OwnedEquipmentQueryResult(
                OwnedEquipmentQueryStatus.Empty,
                new[] { row },
                Array.Empty<BossRewardDiagnostic>(),
                BossRewardTestFixtures.InventoryRevision);
            var validButDuplicate = new OwnedEquipmentQueryResult(
                OwnedEquipmentQueryStatus.Valid,
                new[] { row, row },
                Array.Empty<BossRewardDiagnostic>(),
                BossRewardTestFixtures.InventoryRevision);

            AssertInventoryMalformed(validButEmpty);
            AssertInventoryMalformed(emptyButPopulated);
            AssertInventoryMalformed(validButDuplicate);
        }

        [Test]
        public void ForgedFutureInventorySchemaCannotBypassValidator()
        {
            OwnedEquipmentSnapshot source = BossRewardTestFixtures.Owned();
            var futureRow = new OwnedEquipmentSnapshot(
                source.EquipmentDefinitionId,
                source.EquipmentDefinitionContentVersion,
                source.AcquisitionSnapshotFingerprint,
                source.SlotId,
                source.AttackBonus,
                source.DefenseBonus,
                source.HealthBonus,
                source.StackPolicyId,
                source.Quantity,
                source.FirstAcquiredUtcSeconds,
                source.LastAcquiredUtcSeconds,
                source.LastSourceBossDefinitionId,
                source.LastSourceEncounterCompletionId,
                source.LastAppliedRewardResultId,
                "future_v9",
                true);
            var forged = new OwnedEquipmentQueryResult(
                OwnedEquipmentQueryStatus.Valid,
                new[] { futureRow },
                Array.Empty<BossRewardDiagnostic>(),
                BossRewardTestFixtures.InventoryRevision);

            AssertInventoryMalformed(forged);
        }

        [Test]
        public void UnrelatedMalformedInventoryRowCannotBypassCatalogValidation()
        {
            BossEquipmentDefinitionSnapshot definition =
                BossRewardTestFixtures.Equipment(
                    BossRewardTestFixtures.BetaId,
                    "equipment_v1",
                    5,
                    6,
                    7,
                    BossRewardTestFixtures.ShaA);
            var malformed = new OwnedEquipmentSnapshot(
                definition.EquipmentDefinitionId,
                definition.ContentVersion,
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
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
            var forged = new OwnedEquipmentQueryResult(
                OwnedEquipmentQueryStatus.Valid,
                new[] { malformed },
                Array.Empty<BossRewardDiagnostic>(),
                BossRewardTestFixtures.InventoryRevision);

            AssertInventoryMalformed(forged);
        }

        [Test]
        public void LedgerReceiptWithTamperedDropSemanticsIsMalformed()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            BossRewardAppliedLedgerRecord valid =
                BossRewardTestFixtures.LedgerRecord(computation.Value);
            BossRewardComputedDrop tamperedDrop = CloneDrop(
                computation.Value.Drops[0],
                slotId: "forged_slot");
            BossRewardAppliedLedgerRecord tampered = CopyLedgerWithDrops(
                valid,
                new[] { tamperedDrop });
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { tampered },
                Array.Empty<BossRewardDiagnostic>());

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                result.Status);
            Assert.IsNull(result.ExistingRecord);
        }

        [Test]
        public void UniqueInstancePolicyRejectsExistingIdentityStack()
        {
            BossEquipmentDefinitionSnapshot uniqueDefinition =
                BossRewardTestFixtures.Equipment(
                    stackPolicyId: BossRewardStackPolicies.UniqueInstance);
            BossRewardCatalogSnapshot catalog = BossRewardTestFixtures.Catalog(
                equipment: new[]
                {
                    uniqueDefinition,
                    BossRewardTestFixtures.Equipment(
                        BossRewardTestFixtures.BetaId,
                        "equipment_v1",
                        5,
                        6,
                        7,
                        BossRewardTestFixtures.ShaA)
                });
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation(catalog: catalog);
            BossRewardComputedValue valid = computation.Value;
            BossRewardComputedDrop uniqueDrop = valid.Drops[0];
            var existing = new OwnedEquipmentSnapshot(
                uniqueDrop.EquipmentDefinitionId,
                uniqueDrop.EquipmentDefinitionContentVersion,
                uniqueDrop.AcquisitionSnapshotFingerprint,
                uniqueDrop.SlotId,
                uniqueDrop.AttackBonus,
                uniqueDrop.DefenseBonus,
                uniqueDrop.HealthBonus,
                uniqueDrop.StackPolicyId,
                1,
                10,
                20,
                valid.BossDefinitionId,
                valid.EncounterCompletionId,
                "prior_result",
                BossRewardTestFixtures.InventorySchemaVersion,
                true);
            OwnedEquipmentQueryResult inventory =
                BossRewardTestFixtures.Inventory(
                    new[] { existing },
                    catalog);

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext(
                    inventory: inventory,
                    rewardCatalog: catalog));

            Assert.AreEqual(
                BossRewardPlanningStatus.DefinitionSnapshotConflict,
                result.Status);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void PlanHashChangesWhenInjectedTimestampChanges()
        {
            BossRewardApplicationRequest request =
                BossRewardTestFixtures.ApplicationRequest();
            BossRewardPlanningResult first = BossRewardApplicationPlanner.Plan(
                request,
                BossRewardTestFixtures.PlanningContext(plannedUtcSeconds: 100));
            BossRewardPlanningResult second = BossRewardApplicationPlanner.Plan(
                request,
                BossRewardTestFixtures.PlanningContext(plannedUtcSeconds: 101));

            Assert.AreEqual(BossRewardPlanningStatus.Ready, first.Status);
            Assert.AreEqual(BossRewardPlanningStatus.Ready, second.Status);
            Assert.AreNotEqual(first.Plan.PlanHash, second.Plan.PlanHash);
        }

        [Test]
        public void OversizedDerivedCorrelationIdentityBlocksPlanning()
        {
            string maximumResultId = new string(
                'r',
                BossRewardTechnicalLimits.MaximumIdentifierUtf8Bytes);
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation(
                    request: BossRewardTestFixtures.Request(
                        rewardResultId: maximumResultId));

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext());

            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-TRANSACTION-CORRELATION-ID-INVALID",
                result.Diagnostics[0].Code);
        }

        [Test]
        public void ExistingStackCannotRegressAcquisitionTimestamp()
        {
            OwnedEquipmentQueryResult inventory = BossRewardTestFixtures.Inventory(
                new[] { BossRewardTestFixtures.Owned() });

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(
                    inventory: inventory,
                    plannedUtcSeconds: 19));

            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-TRANSACTION-TIMESTAMP-REGRESSION",
                result.Diagnostics[0].Code);
        }

        [Test]
        public void LedgerCannotOmitRequiredNotificationCorrelations()
        {
            BossRewardComputationResult computation =
                BossRewardTestFixtures.Computation();
            BossRewardAppliedLedgerRecord source =
                BossRewardTestFixtures.LedgerRecord(computation.Value);
            var missingCorrelations = new BossRewardAppliedLedgerRecord(
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
                source.ComputationHash,
                source.WarzoneCredits,
                source.IsExplicitNoReward,
                source.DeterminismVersion,
                source.CommittedDrops,
                source.CommittedUtcSeconds,
                source.ApplicationPolicyVersion,
                Array.Empty<string>(),
                source.State);
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { missingCorrelations },
                Array.Empty<BossRewardDiagnostic>());

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(computation),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                result.Status);
        }

        [Test]
        public void LedgerRowsCannotCrossSaveProfileBoundary()
        {
            BossRewardComputationResult otherProfileComputation =
                BossRewardTestFixtures.Computation(
                    request: BossRewardTestFixtures.Request(
                        profileId: "profile_other"));
            BossRewardAppliedLedgerRecord otherProfileRecord =
                BossRewardTestFixtures.LedgerRecord(
                    otherProfileComputation.Value);
            var ledger = new BossRewardLedgerSnapshot(
                BossRewardLedgerStatus.Valid,
                BossRewardTestFixtures.LedgerRevision,
                new[] { otherProfileRecord },
                Array.Empty<BossRewardDiagnostic>());

            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(ledger: ledger));

            Assert.AreEqual(
                BossRewardPlanningStatus.InternalInvariantFailure,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-LEDGER-MALFORMED",
                result.Diagnostics[0].Code);
        }

        [Test]
        public void MalformedOrDuplicateCatalogDefinitionsFailTyped()
        {
            BossEquipmentDefinitionSnapshot definition =
                BossRewardTestFixtures.Equipment();
            BossRewardCatalogSnapshot nullDefinitionCatalog =
                BossRewardTestFixtures.Catalog(
                    equipment: new BossEquipmentDefinitionSnapshot[] { null });
            BossRewardCatalogSnapshot duplicateCatalog =
                BossRewardTestFixtures.Catalog(
                    equipment: new[] { definition, definition });
            OwnedEquipmentQueryResult nullDefinition = null;
            OwnedEquipmentQueryResult duplicateDefinition = null;

            Assert.DoesNotThrow(() =>
                nullDefinition = BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    nullDefinitionCatalog,
                    BossRewardTestFixtures.InventorySchemaVersion));
            Assert.DoesNotThrow(() =>
                duplicateDefinition = BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    duplicateCatalog,
                    BossRewardTestFixtures.InventorySchemaVersion));
            Assert.AreEqual(
                OwnedEquipmentQueryStatus.Unavailable,
                nullDefinition.Status);
            Assert.AreEqual(
                OwnedEquipmentQueryStatus.Unavailable,
                duplicateDefinition.Status);
        }

        private static void AssertInventoryMalformed(
            OwnedEquipmentQueryResult inventory)
        {
            BossRewardPlanningResult result = BossRewardApplicationPlanner.Plan(
                BossRewardTestFixtures.ApplicationRequest(),
                BossRewardTestFixtures.PlanningContext(inventory: inventory));

            Assert.AreEqual(
                BossRewardPlanningStatus.InventoryMalformed,
                result.Status);
            Assert.IsNull(result.Plan);
        }

        private static BossRewardComputationResult ForgeComputation(
            BossRewardComputedValue source,
            int? warzoneCredits = null,
            BossRewardComputedDrop[] drops = null,
            bool recomputeHash = true)
        {
            int credits = warzoneCredits ?? source.WarzoneCredits;
            BossRewardComputedDrop[] selectedDrops =
                drops ?? CopyDrops(source.Drops);
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
                credits,
                source.IsExplicitNoReward,
                selectedDrops,
                source.DeterminismVersion,
                BossRewardTestFixtures.ShaA);
            string hash = recomputeHash
                ? BossRewardComputation.RecomputeComputationHash(provisional)
                : BossRewardTestFixtures.ShaA;
            var forged = new BossRewardComputedValue(
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
                hash);
            return new BossRewardComputationResult(
                BossRewardComputationStatus.Computed,
                forged,
                Array.Empty<BossRewardDiagnostic>());
        }

        private static BossRewardComputedDrop CloneDrop(
            BossRewardComputedDrop source,
            int? quantity = null,
            string slotId = null,
            string stackPolicyId = null)
        {
            return new BossRewardComputedDrop(
                source.EquipmentDefinitionId,
                source.EquipmentDefinitionContentVersion,
                source.AcquisitionSnapshotFingerprint,
                slotId ?? source.SlotId,
                source.AttackBonus,
                source.DefenseBonus,
                source.HealthBonus,
                quantity ?? source.Quantity,
                stackPolicyId ?? source.StackPolicyId,
                source.AcquisitionAnnouncementPolicyId);
        }

        private static BossRewardComputedDrop[] CopyDrops(
            System.Collections.Generic.IReadOnlyList<BossRewardComputedDrop> source)
        {
            var copy = new BossRewardComputedDrop[source.Count];
            for (int index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return copy;
        }

        private static BossRewardAppliedLedgerRecord CopyLedgerWithDrops(
            BossRewardAppliedLedgerRecord source,
            BossRewardComputedDrop[] drops)
        {
            return new BossRewardAppliedLedgerRecord(
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
                source.ComputationHash,
                source.WarzoneCredits,
                source.IsExplicitNoReward,
                source.DeterminismVersion,
                drops,
                source.CommittedUtcSeconds,
                source.ApplicationPolicyVersion,
                source.NotificationCorrelationIds,
                source.State);
        }
    }
}
