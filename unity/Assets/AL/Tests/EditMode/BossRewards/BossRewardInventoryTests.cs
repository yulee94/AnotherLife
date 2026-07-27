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
            Assert.IsFalse(result.CanApplyRewards);
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
    }
}
