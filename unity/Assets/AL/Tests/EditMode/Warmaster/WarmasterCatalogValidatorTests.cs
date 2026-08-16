using System.Collections.Generic;
using AL.Warmaster.Catalog;
using NUnit.Framework;

namespace AL.Tests.EditMode.Warmaster
{
    public sealed class WarmasterCatalogValidatorTests
    {
        [Test]
        public void Validate_AcceptsCompleteActiveCatalog()
        {
            WarmasterCatalogValidationResult result = WarmasterCatalogValidator.Validate(CreateValidCatalog());

            Assert.That(result.Status, Is.EqualTo(WarmasterCatalogValidationStatus.Valid));
            Assert.That(result.CanPurchase, Is.True);
            Assert.That(result.CanGrantCompletionReward, Is.True);
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(result.Snapshot.Pieces.Count, Is.EqualTo(WarmasterCatalogContract.PieceIds.Count));
        }

        [Test]
        public void Validate_RejectsDuplicatePieceIds()
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Pieces[1].Id = catalog.Pieces[0].Id;

            AssertRejected(catalog, WarmasterCatalogValidationStatus.DuplicateId);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Validate_RejectsNonpositivePrices(int price)
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Pieces[0].WarzoneCreditPrice = price;

            AssertRejected(catalog, WarmasterCatalogValidationStatus.InvalidBalanceInput);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(11)]
        public void Validate_RejectsInvalidCompletionThreshold(int threshold)
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Set.RequiredPieceCount = threshold;

            AssertRejected(catalog, WarmasterCatalogValidationStatus.InvalidBalanceInput);
        }

        [Test]
        public void Validate_RejectsInvalidPerPieceThreshold()
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Pieces[0].RequiredOwnedPieceCount = catalog.Set.RequiredPieceCount;

            AssertRejected(catalog, WarmasterCatalogValidationStatus.InvalidBalanceInput);
        }

        [Test]
        public void Validate_RejectsIncompletePieceEntry()
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Pieces[0].EquipmentSlotId = null;

            AssertRejected(catalog, WarmasterCatalogValidationStatus.IncompleteEntry);
        }

        [Test]
        public void Validate_RejectsMissingApprovedBalanceInput()
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Pieces[0].WarzoneCreditPrice = null;

            AssertRejected(catalog, WarmasterCatalogValidationStatus.IncompleteEntry);
        }

        [Test]
        public void Validate_RejectsInactiveCatalogBeforePurchasesOrRewards()
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Activation = WarmasterCatalogActivation.Inactive;

            AssertRejected(catalog, WarmasterCatalogValidationStatus.Inactive);
        }

        [Test]
        public void Validate_RejectsMissingCanonicalPiece()
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Pieces.RemoveAt(catalog.Pieces.Count - 1);
            catalog.Set.PieceIds.RemoveAt(catalog.Set.PieceIds.Count - 1);

            AssertRejected(catalog, WarmasterCatalogValidationStatus.IncompleteCatalog);
        }

        [Test]
        public void Validate_RejectsUnknownPieceId()
        {
            WarmasterCatalogInput catalog = CreateValidCatalog();
            catalog.Pieces[0].Id = "warmaster_piece_99";
            catalog.Set.PieceIds[0] = "warmaster_piece_99";

            AssertRejected(catalog, WarmasterCatalogValidationStatus.UnknownId);
        }

        private static void AssertRejected(
            WarmasterCatalogInput catalog,
            WarmasterCatalogValidationStatus expectedStatus)
        {
            WarmasterCatalogValidationResult result = WarmasterCatalogValidator.Validate(catalog);

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.CanPurchase, Is.False);
            Assert.That(result.CanGrantCompletionReward, Is.False);
            Assert.That(result.Snapshot, Is.Null);
        }

        private static WarmasterCatalogInput CreateValidCatalog()
        {
            var pieceIds = new List<string>(WarmasterCatalogContract.PieceIds);
            var pieces = new List<WarmasterPieceInput>();
            for (int index = 0; index < pieceIds.Count; index++)
            {
                pieces.Add(new WarmasterPieceInput
                {
                    Id = pieceIds[index],
                    SetId = WarmasterCatalogContract.TrueWarmasterSetId,
                    WarzoneCreditPrice = 100 + index,
                    RequiredOwnedPieceCount = index,
                    PurchaseExperienceAward = 1,
                    EquipmentSlotId = "warmaster_slot_" + (index + 1).ToString("00"),
                    StatModifiers = new List<WarmasterStatModifierInput>
                    {
                        new WarmasterStatModifierInput { StatId = "test_stat", Amount = 1 }
                    }
                });
            }

            return new WarmasterCatalogInput
            {
                CatalogId = WarmasterCatalogContract.CatalogId,
                SchemaVersion = WarmasterCatalogContract.SchemaVersion,
                Revision = "test-approved-v1",
                Activation = WarmasterCatalogActivation.Active,
                Set = new WarmasterSetInput
                {
                    Id = WarmasterCatalogContract.TrueWarmasterSetId,
                    RequiredPieceCount = pieceIds.Count,
                    PieceIds = pieceIds,
                    CompletionRewardId = "true_warmaster_entitlement"
                },
                Pieces = pieces
            };
        }
    }
}
