using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public class BossRewardInventoryTests
    {
        [Test]
        public void EmptyInventoryIsValidAndImmutable()
        {
            OwnedEquipmentQueryResult result = BossRewardTestFixtures.Inventory();

            Assert.AreEqual(OwnedEquipmentQueryStatus.Empty, result.Status);
            Assert.IsTrue(result.CanApplyRewards);
            Assert.IsEmpty(result.Items);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.Items).Add(BossRewardTestFixtures.Owned()));
        }

        [Test]
        public void ValidRowsAreDetachedFromSourceOrdering()
        {
            BossEquipmentDefinitionSnapshot alpha =
                BossRewardTestFixtures.Equipment(BossRewardTestFixtures.AlphaId);
            BossEquipmentDefinitionSnapshot beta =
                BossRewardTestFixtures.Equipment(
                    BossRewardTestFixtures.BetaId,
                    attackBonus: 5,
                    defenseBonus: 6,
                    healthBonus: 7,
                    rawSha256: BossRewardTestFixtures.ShaA);
            var rows = new List<OwnedEquipmentSnapshot>
            {
                BossRewardTestFixtures.Owned(beta, id: BossRewardTestFixtures.BetaId),
                BossRewardTestFixtures.Owned(alpha)
            };

            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                rows,
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);
            rows.Clear();

            Assert.AreEqual(OwnedEquipmentQueryStatus.Valid, result.Status);
            Assert.AreEqual(2, result.Items.Count);
            Assert.AreEqual(
                BossRewardTestFixtures.AlphaId,
                result.Items[0].EquipmentDefinitionId);
            Assert.AreEqual(
                BossRewardTestFixtures.BetaId,
                result.Items[1].EquipmentDefinitionId);
        }

        [Test]
        public void OpaqueCompletionAndResultProvenanceArePreserved()
        {
            const string completionId = "완료-1";
            const string resultId = completionId + ":boss_reward";
            BossEquipmentDefinitionSnapshot definition =
                BossRewardTestFixtures.Equipment();
            var row = new OwnedEquipmentSnapshot(
                definition.EquipmentDefinitionId,
                definition.ContentVersion,
                BossRewardComputation.ComputeAcquisitionSnapshotFingerprint(
                    definition),
                definition.SlotId,
                definition.AttackBonus,
                definition.DefenseBonus,
                definition.HealthBonus,
                definition.StackPolicyId,
                1,
                10,
                20,
                BossRewardTestFixtures.BossId,
                completionId,
                resultId,
                BossRewardTestFixtures.InventorySchemaVersion,
                true);

            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                new[] { row },
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(OwnedEquipmentQueryStatus.Valid, result.Status);
            Assert.AreEqual(
                completionId,
                result.Items[0].LastSourceEncounterCompletionId);
            Assert.AreEqual(
                resultId,
                result.Items[0].LastAppliedRewardResultId);
        }

        [Test]
        public void NullCollectionAndNullRowAreDistinct()
        {
            OwnedEquipmentQueryResult nullCollection =
                BossRewardInventoryValidator.Validate(
                    null,
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(),
                    BossRewardTestFixtures.InventorySchemaVersion);
            OwnedEquipmentQueryResult nullRow =
                BossRewardInventoryValidator.Validate(
                    new OwnedEquipmentSnapshot[] { null },
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(),
                    BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedNullCollection,
                nullCollection.Status);
            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedNullEntry,
                nullRow.Status);
            Assert.IsFalse(nullCollection.CanApplyRewards);
            Assert.IsFalse(nullRow.CanApplyRewards);
        }

        [Test]
        public void DuplicateIdentityDisablesMutationWithoutRepair()
        {
            OwnedEquipmentSnapshot row = BossRewardTestFixtures.Owned();
            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                new[] { row, row },
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedDuplicateId,
                result.Status);
            Assert.AreEqual(2, result.Items.Count);
            Assert.IsFalse(result.CanApplyRewards);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveQuantityIsMalformed(int quantity)
        {
            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                new[] { BossRewardTestFixtures.Owned(quantity: quantity) },
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedQuantity,
                result.Status);
        }

        [Test]
        public void UnknownFutureDefinitionIsPreservedButUnsupported()
        {
            BossEquipmentDefinitionSnapshot future =
                BossRewardTestFixtures.Equipment("equipment_future");
            OwnedEquipmentSnapshot row = BossRewardTestFixtures.Owned(
                future,
                id: "equipment_future",
                supported: false);

            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                new[] { row },
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.PreservedUnknownFutureDefinition,
                result.Status);
            Assert.AreEqual("equipment_future", result.Items[0].EquipmentDefinitionId);
            Assert.IsFalse(result.Items[0].IsSupportedDefinition);
            Assert.IsTrue(result.CanApplyRewards);
            Assert.IsFalse(result.Diagnostics[0].BlocksOperation);
        }

        [Test]
        public void UnknownRequiredDefinitionIsMalformed()
        {
            BossEquipmentDefinitionSnapshot missing =
                BossRewardTestFixtures.Equipment("equipment_missing");
            OwnedEquipmentSnapshot row = BossRewardTestFixtures.Owned(
                missing,
                id: "equipment_missing",
                supported: true);

            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                new[] { row },
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedUnknownRequiredDefinition,
                result.Status);
        }

        [Test]
        public void SnapshotDriftIsRejectedWithoutOverwrite()
        {
            OwnedEquipmentSnapshot row = BossRewardTestFixtures.Owned(
                fingerprint:
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");

            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                new[] { row },
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedSnapshot,
                result.Status);
            Assert.AreEqual(
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                result.Items[0].AcquisitionSnapshotFingerprint);
        }

        [Test]
        public void ReversedOrNegativeTimestampIsMalformed()
        {
            BossEquipmentDefinitionSnapshot definition =
                BossRewardTestFixtures.Equipment();
            var row = new OwnedEquipmentSnapshot(
                definition.EquipmentDefinitionId,
                definition.ContentVersion,
                BossRewardComputation.ComputeAcquisitionSnapshotFingerprint(definition),
                definition.SlotId,
                definition.AttackBonus,
                definition.DefenseBonus,
                definition.HealthBonus,
                definition.StackPolicyId,
                1,
                20,
                10,
                BossRewardTestFixtures.BossId,
                BossRewardTestFixtures.CompletionId,
                BossRewardTestFixtures.ResultId,
                BossRewardTestFixtures.InventorySchemaVersion,
                true);

            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                new[] { row },
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedTimestamp,
                result.Status);
        }

        [Test]
        public void UnsupportedSchemaAndUnavailableDomainAreTyped()
        {
            OwnedEquipmentQueryResult unsupported =
                BossRewardInventoryValidator.Validate(
                    new[] { BossRewardTestFixtures.Owned() },
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(),
                    "future_schema");
            OwnedEquipmentQueryResult unavailable =
                BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(),
                    BossRewardTestFixtures.InventorySchemaVersion,
                    false);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.UnsupportedVersion,
                unsupported.Status);
            Assert.AreEqual(
                OwnedEquipmentQueryStatus.Unavailable,
                unavailable.Status);
        }

        [Test]
        public void CoherentFutureCatalogSchemaIsNotInterpretedAsCurrent()
        {
            BossRewardCatalogSnapshot source = BossRewardTestFixtures.Catalog();
            var futureCatalog = new BossRewardCatalogSnapshot(
                source.GameId,
                source.CatalogSetId,
                "boss_reward_schema_v2",
                source.Revision,
                source.Bindings,
                source.Profiles,
                source.EquipmentDefinitions,
                source.AnnouncementPolicyIds);

            OwnedEquipmentQueryResult result =
                BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    futureCatalog,
                    BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.UnsupportedVersion,
                result.Status);
            Assert.AreEqual(
                "AL-BOSS-REWARD-INVENTORY-CATALOG-SCHEMA-UNSUPPORTED",
                result.Diagnostics[0].Code);
        }

        [Test]
        public void CatalogErrorSelectionIgnoresDefinitionPermutation()
        {
            BossEquipmentDefinitionSnapshot duplicate =
                BossRewardTestFixtures.Equipment("equipment_mike");
            BossEquipmentDefinitionSnapshot malformed =
                BossRewardTestFixtures.Equipment(
                    "equipment_zeta",
                    rawSha256: "not_a_sha256");
            var forwardDefinitions = new[]
            {
                malformed,
                duplicate,
                duplicate
            };
            BossEquipmentDefinitionSnapshot[] reverseDefinitions =
                forwardDefinitions.Reverse().ToArray();

            OwnedEquipmentQueryResult forward =
                BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(
                        equipment: forwardDefinitions),
                    BossRewardTestFixtures.InventorySchemaVersion);
            OwnedEquipmentQueryResult reverse =
                BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(
                        equipment: reverseDefinitions),
                    BossRewardTestFixtures.InventorySchemaVersion);

            AssertEquivalent(forward, reverse);
            Assert.AreEqual(OwnedEquipmentQueryStatus.Unavailable, forward.Status);
            Assert.AreEqual(1, forward.Diagnostics.Count);
            Assert.AreEqual(
                "equipment_mike",
                forward.Diagnostics[0].RecordId);
        }

        [Test]
        public void NullCatalogErrorWinsCanonicallyAcrossDefinitionPermutation()
        {
            BossEquipmentDefinitionSnapshot duplicate =
                BossRewardTestFixtures.Equipment("equipment_mike");
            BossEquipmentDefinitionSnapshot malformed =
                BossRewardTestFixtures.Equipment(
                    "equipment_zeta",
                    rawSha256: "not_a_sha256");
            var forwardDefinitions =
                new BossEquipmentDefinitionSnapshot[]
                {
                    malformed,
                    duplicate,
                    null,
                    duplicate
                };

            OwnedEquipmentQueryResult forward =
                BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(
                        equipment: forwardDefinitions),
                    BossRewardTestFixtures.InventorySchemaVersion);
            OwnedEquipmentQueryResult reverse =
                BossRewardInventoryValidator.Validate(
                    Array.Empty<OwnedEquipmentSnapshot>(),
                    BossRewardTestFixtures.InventoryRevision,
                    BossRewardTestFixtures.Catalog(
                        equipment: forwardDefinitions.Reverse()),
                    BossRewardTestFixtures.InventorySchemaVersion);

            AssertEquivalent(forward, reverse);
            Assert.AreEqual(1, forward.Diagnostics.Count);
            Assert.AreEqual(string.Empty, forward.Diagnostics[0].RecordId);
        }

        [Test]
        public void MalformedRowStatusAndItemsIgnoreInputPermutation()
        {
            BossEquipmentDefinitionSnapshot alpha =
                BossRewardTestFixtures.Equipment(
                    BossRewardTestFixtures.AlphaId);
            BossEquipmentDefinitionSnapshot beta =
                BossRewardTestFixtures.Equipment(
                    BossRewardTestFixtures.BetaId,
                    attackBonus: 5,
                    defenseBonus: 6,
                    healthBonus: 7,
                    rawSha256: BossRewardTestFixtures.ShaA);
            BossEquipmentDefinitionSnapshot gamma =
                BossRewardTestFixtures.Equipment("equipment_gamma");
            BossEquipmentDefinitionSnapshot delta =
                BossRewardTestFixtures.Equipment("equipment_delta");
            BossEquipmentDefinitionSnapshot epsilon =
                BossRewardTestFixtures.Equipment("equipment_epsilon");
            BossRewardCatalogSnapshot catalog =
                BossRewardTestFixtures.Catalog(
                    equipment: new[]
                    {
                        alpha,
                        beta,
                        gamma,
                        delta,
                        epsilon
                    });

            OwnedEquipmentSnapshot unsupported = CopyRow(
                BossRewardTestFixtures.Owned(alpha),
                schemaVersion: "owned_equipment_v2");
            OwnedEquipmentSnapshot quantity =
                BossRewardTestFixtures.Owned(
                    beta,
                    quantity: 0,
                    id: BossRewardTestFixtures.BetaId);
            OwnedEquipmentSnapshot timestamp = CopyRow(
                BossRewardTestFixtures.Owned(
                    gamma,
                    id: gamma.EquipmentDefinitionId),
                firstAcquiredUtcSeconds: 20,
                lastAcquiredUtcSeconds: 10);
            OwnedEquipmentSnapshot provenance = CopyRow(
                BossRewardTestFixtures.Owned(
                    delta,
                    id: delta.EquipmentDefinitionId),
                lastSourceBossDefinitionId: "boss-invalid");
            OwnedEquipmentSnapshot snapshot = CopyRow(
                BossRewardTestFixtures.Owned(
                    epsilon,
                    id: epsilon.EquipmentDefinitionId),
                acquisitionSnapshotFingerprint:
                    "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
            BossEquipmentDefinitionSnapshot missingDefinition =
                BossRewardTestFixtures.Equipment("equipment_missing");
            OwnedEquipmentSnapshot missing =
                BossRewardTestFixtures.Owned(
                    missingDefinition,
                    id: missingDefinition.EquipmentDefinitionId);
            BossEquipmentDefinitionSnapshot futureDefinition =
                BossRewardTestFixtures.Equipment("equipment_future");
            OwnedEquipmentSnapshot future =
                BossRewardTestFixtures.Owned(
                    futureDefinition,
                    id: futureDefinition.EquipmentDefinitionId,
                    supported: false);
            var invalidId = new OwnedEquipmentSnapshot(
                "equipment-invalid",
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                string.Empty,
                1,
                0,
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                BossRewardTestFixtures.InventorySchemaVersion,
                false);
            OwnedEquipmentSnapshot duplicateAlpha =
                BossRewardTestFixtures.Owned(alpha, quantity: 1);
            var forwardRows = new OwnedEquipmentSnapshot[]
            {
                future,
                null,
                invalidId,
                duplicateAlpha,
                missing,
                quantity,
                snapshot,
                timestamp,
                provenance,
                unsupported
            };

            OwnedEquipmentQueryResult forward =
                BossRewardInventoryValidator.Validate(
                    forwardRows,
                    BossRewardTestFixtures.InventoryRevision,
                    catalog,
                    BossRewardTestFixtures.InventorySchemaVersion);
            OwnedEquipmentQueryResult reverse =
                BossRewardInventoryValidator.Validate(
                    forwardRows.Reverse(),
                    BossRewardTestFixtures.InventoryRevision,
                    catalog,
                    BossRewardTestFixtures.InventorySchemaVersion);

            AssertEquivalent(forward, reverse);
            Assert.AreEqual(
                OwnedEquipmentQueryStatus.UnsupportedVersion,
                forward.Status);
            Assert.IsFalse(forward.CanApplyRewards);
            OwnedEquipmentSnapshot[] alphaItems = forward.Items
                .Where(item =>
                    item.EquipmentDefinitionId ==
                    BossRewardTestFixtures.AlphaId)
                .ToArray();
            Assert.AreEqual(2, alphaItems.Length);
            Assert.AreEqual(
                1,
                alphaItems[0].Quantity);
            Assert.AreEqual(
                2,
                alphaItems[1].Quantity);
        }

        [Test]
        public void DiagnosticOverflowIsBoundedAndTyped()
        {
            var rows = new OwnedEquipmentSnapshot[
                BossRewardTechnicalLimits.MaximumDiagnostics + 1];

            OwnedEquipmentQueryResult result = BossRewardInventoryValidator.Validate(
                rows,
                BossRewardTestFixtures.InventoryRevision,
                BossRewardTestFixtures.Catalog(),
                BossRewardTestFixtures.InventorySchemaVersion);

            Assert.AreEqual(
                OwnedEquipmentQueryStatus.MalformedNullEntry,
                result.Status);
            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                result.Diagnostics.Count);
            Assert.IsTrue(result.Diagnostics.Any(item =>
                item.Code ==
                "AL-BOSS-REWARD-TRANSACTION-DIAGNOSTIC-LIMIT"));
        }

        private static OwnedEquipmentSnapshot CopyRow(
            OwnedEquipmentSnapshot source,
            string acquisitionSnapshotFingerprint = null,
            int? quantity = null,
            long? firstAcquiredUtcSeconds = null,
            long? lastAcquiredUtcSeconds = null,
            string lastSourceBossDefinitionId = null,
            string schemaVersion = null)
        {
            return new OwnedEquipmentSnapshot(
                source.EquipmentDefinitionId,
                source.EquipmentDefinitionContentVersion,
                acquisitionSnapshotFingerprint ??
                source.AcquisitionSnapshotFingerprint,
                source.SlotId,
                source.AttackBonus,
                source.DefenseBonus,
                source.HealthBonus,
                source.StackPolicyId,
                quantity ?? source.Quantity,
                firstAcquiredUtcSeconds ?? source.FirstAcquiredUtcSeconds,
                lastAcquiredUtcSeconds ?? source.LastAcquiredUtcSeconds,
                lastSourceBossDefinitionId ??
                source.LastSourceBossDefinitionId,
                source.LastSourceEncounterCompletionId,
                source.LastAppliedRewardResultId,
                schemaVersion ?? source.SchemaVersion,
                source.IsSupportedDefinition);
        }

        private static void AssertEquivalent(
            OwnedEquipmentQueryResult expected,
            OwnedEquipmentQueryResult actual)
        {
            Assert.AreEqual(expected.Status, actual.Status);
            Assert.AreEqual(expected.CanApplyRewards, actual.CanApplyRewards);
            Assert.AreEqual(expected.InventoryRevision, actual.InventoryRevision);
            Assert.AreEqual(expected.Items.Count, actual.Items.Count);
            for (int index = 0; index < expected.Items.Count; index++)
            {
                OwnedEquipmentSnapshot left = expected.Items[index];
                OwnedEquipmentSnapshot right = actual.Items[index];
                Assert.AreEqual(
                    left.EquipmentDefinitionId,
                    right.EquipmentDefinitionId);
                Assert.AreEqual(
                    left.EquipmentDefinitionContentVersion,
                    right.EquipmentDefinitionContentVersion);
                Assert.AreEqual(
                    left.AcquisitionSnapshotFingerprint,
                    right.AcquisitionSnapshotFingerprint);
                Assert.AreEqual(left.SlotId, right.SlotId);
                Assert.AreEqual(left.AttackBonus, right.AttackBonus);
                Assert.AreEqual(left.DefenseBonus, right.DefenseBonus);
                Assert.AreEqual(left.HealthBonus, right.HealthBonus);
                Assert.AreEqual(left.StackPolicyId, right.StackPolicyId);
                Assert.AreEqual(left.Quantity, right.Quantity);
                Assert.AreEqual(
                    left.FirstAcquiredUtcSeconds,
                    right.FirstAcquiredUtcSeconds);
                Assert.AreEqual(
                    left.LastAcquiredUtcSeconds,
                    right.LastAcquiredUtcSeconds);
                Assert.AreEqual(
                    left.LastSourceBossDefinitionId,
                    right.LastSourceBossDefinitionId);
                Assert.AreEqual(
                    left.LastSourceEncounterCompletionId,
                    right.LastSourceEncounterCompletionId);
                Assert.AreEqual(
                    left.LastAppliedRewardResultId,
                    right.LastAppliedRewardResultId);
                Assert.AreEqual(left.SchemaVersion, right.SchemaVersion);
                Assert.AreEqual(
                    left.IsSupportedDefinition,
                    right.IsSupportedDefinition);
            }
            Assert.AreEqual(
                expected.Diagnostics.Count,
                actual.Diagnostics.Count);
            for (int index = 0; index < expected.Diagnostics.Count; index++)
            {
                BossRewardDiagnostic left = expected.Diagnostics[index];
                BossRewardDiagnostic right = actual.Diagnostics[index];
                Assert.AreEqual(left.Severity, right.Severity);
                Assert.AreEqual(left.Code, right.Code);
                Assert.AreEqual(left.RecordId, right.RecordId);
                Assert.AreEqual(left.FieldPath, right.FieldPath);
                Assert.AreEqual(left.Domain, right.Domain);
                Assert.AreEqual(left.OperationId, right.OperationId);
                Assert.AreEqual(
                    left.BlocksOperation,
                    right.BlocksOperation);
                Assert.AreEqual(left.SchemaVersion, right.SchemaVersion);
                Assert.AreEqual(left.ContentVersion, right.ContentVersion);
                Assert.AreEqual(
                    left.SafeDeveloperMessage,
                    right.SafeDeveloperMessage);
            }
        }
    }
}
