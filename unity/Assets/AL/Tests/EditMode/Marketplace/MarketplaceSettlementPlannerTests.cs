using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.Marketplace;
using NUnit.Framework;

namespace AL.Tests.EditMode.Marketplace
{
    public sealed class MarketplaceSettlementPlannerTests
    {
        private const string Seller = "account.seller.1";
        private const string Buyer = "account.buyer.1";
        private const string ItemId = "item.gear.1";
        private const string ListingId = "listing.1";
        private const string CatalogHash =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const long ListedAt = 1_000_000L;
        private const long HourMs = 3_600_000L;

        [Test]
        public void BindingExampleDebitsTenCreditsNineAndDestroysOne()
        {
            MarketplacePlanningResult settled = Settle(10L, buyerBalance: 10L, sellerBalance: 0L);

            AssertPrepared(settled);
            Assert.That(settled.Plan.Quote.BuyerDebit, Is.EqualTo(10L));
            Assert.That(settled.Plan.Quote.SellerProceeds, Is.EqualTo(9L));
            Assert.That(settled.Plan.Quote.TaxDestroyed, Is.EqualTo(1L));
            Assert.That(Wallet(settled.Plan.After, Buyer).Balance, Is.EqualTo(0L));
            Assert.That(Wallet(settled.Plan.After, Seller).Balance, Is.EqualTo(9L));
            Assert.That(Item(settled.Plan.After, ItemId).OwnerAccountId, Is.EqualTo(Buyer));
            Assert.That(Item(settled.Plan.After, ItemId).Custody, Is.EqualTo(MarketplaceItemCustody.Available));
            Assert.That(
                Listing(settled.Plan.After, ListingId).Status,
                Is.EqualTo(MarketplaceListingStatus.Sold));
            Assert.That(
                settled.Plan.After.Wallets.Sum(row => row.Balance),
                Is.EqualTo(9L));
        }

        [TestCase(11L, 1L, 10L)]
        [TestCase(19L, 1L, 18L)]
        [TestCase(20L, 2L, 18L)]
        [TestCase(99L, 9L, 90L)]
        public void FloorDivisionRoundsTowardDestroyedTax(
            long price,
            long tax,
            long proceeds)
        {
            MarketplacePlanningResult settled = Settle(price, buyerBalance: price, sellerBalance: 0L);

            AssertPrepared(settled);
            Assert.That(settled.Plan.Quote.TaxDestroyed, Is.EqualTo(tax));
            Assert.That(settled.Plan.Quote.SellerProceeds, Is.EqualTo(proceeds));
            Assert.That(settled.Plan.Quote.TaxDestroyed + settled.Plan.Quote.SellerProceeds, Is.EqualTo(price));
        }

        [Test]
        public void PriceBelowCatalogMinimumIsRejected()
        {
            MarketplacePlanningResult listed = List(9L);
            AssertRejected(
                listed,
                MarketplacePlanningStatus.InvalidRequest,
                MarketplaceDiagnosticCodes.PriceBelowMinimum);
        }

        [Test]
        public void PriceAboveCatalogMaximumOverflowsClosed()
        {
            MarketplacePolicySnapshot policy = Policy(maximumListingPrice: 20L);
            var planner = new MarketplaceSettlementPlanner(policy);
            MarketplacePlanningResult listed = planner.Plan(
                Request(MarketplaceOperation.List, Seller, 21L, string.Empty),
                EmptyAuthority(policy, Seller));

            AssertRejected(
                listed,
                MarketplacePlanningStatus.Overflow,
                MarketplaceDiagnosticCodes.PriceAboveMaximum);
        }

        [Test]
        public void SellerCreditOverflowFailsClosedWithoutMutation()
        {
            MarketplacePlanningResult settled = Settle(
                10L,
                buyerBalance: 10L,
                sellerBalance: long.MaxValue - 8L);

            AssertRejected(
                settled,
                MarketplacePlanningStatus.Overflow,
                MarketplaceDiagnosticCodes.ArithmeticOverflow);
        }

        [Test]
        public void ExactReplayReturnsCommittedReceipt()
        {
            MarketplacePlanningResult listed = List(10L);
            MarketplaceSettlementRequest request = Request(
                MarketplaceOperation.List,
                Seller,
                10L,
                string.Empty);
            MarketplacePlanningResult replay = new MarketplaceSettlementPlanner(Policy()).Plan(
                request,
                listed.Plan.After);

            Assert.That(replay.Status, Is.EqualTo(MarketplacePlanningStatus.AlreadyCommitted));
            Assert.That(replay.ReplayedReceipt.OperationId, Is.EqualTo(request.OperationId));
            Assert.That(replay.Plan, Is.Null);
        }

        [Test]
        public void SameOperationDifferentFingerprintConflicts()
        {
            MarketplacePlanningResult listed = List(10L);
            MarketplaceSettlementRequest colliding = new MarketplaceSettlementRequest(
                "op.list.1",
                MarketplaceOperation.List,
                Seller,
                ListingId,
                ItemId,
                Seller,
                string.Empty,
                20L,
                MarketplaceWalletKind.Oathmark,
                ListedAt,
                listed.Plan.After.Revision,
                Policy().Binding);
            MarketplacePlanningResult conflict = new MarketplaceSettlementPlanner(Policy()).Plan(
                colliding,
                listed.Plan.After);

            AssertRejected(
                conflict,
                MarketplacePlanningStatus.Conflict,
                MarketplaceDiagnosticCodes.OperationConflict);
        }

        [Test]
        public void NonOwnerCannotList()
        {
            MarketplacePolicySnapshot policy = Policy();
            var planner = new MarketplaceSettlementPlanner(policy);
            MarketplaceAuthoritySnapshot snapshot = new MarketplaceAuthoritySnapshot(
                MarketplaceAuthorityStatus.Available,
                1L,
                policy.Binding,
                Array.Empty<MarketplaceWalletSnapshot>(),
                new[] { new MarketplaceItemSnapshot(ItemId, Buyer, MarketplaceItemCustody.Available) },
                Array.Empty<MarketplaceListingSnapshot>(),
                Array.Empty<MarketplaceOperationReceipt>(),
                true);
            MarketplacePlanningResult listed = planner.Plan(
                Request(MarketplaceOperation.List, Seller, 10L, string.Empty),
                snapshot);

            AssertRejected(
                listed,
                MarketplacePlanningStatus.OwnershipRejected,
                MarketplaceDiagnosticCodes.OwnershipRejected);
        }

        [Test]
        public void SellerMayCancelOnlyBeforeReservation()
        {
            MarketplacePlanningResult listed = List(10L);
            MarketplacePlanningResult cancelled = new MarketplaceSettlementPlanner(Policy()).Plan(
                Request("op.cancel.1", MarketplaceOperation.Cancel, Seller, 10L, string.Empty, listed.Plan.After.Revision),
                listed.Plan.After);
            AssertPrepared(cancelled);
            Assert.That(
                Listing(cancelled.Plan.After, ListingId).Status,
                Is.EqualTo(MarketplaceListingStatus.Cancelled));
            Assert.That(
                Item(cancelled.Plan.After, ItemId).Custody,
                Is.EqualTo(MarketplaceItemCustody.Available));

            MarketplacePlanningResult reserved = Reserve(listed, 10L);
            MarketplacePlanningResult blocked = new MarketplaceSettlementPlanner(Policy()).Plan(
                Request("op.cancel.2", MarketplaceOperation.Cancel, Seller, 10L, string.Empty, reserved.Plan.After.Revision),
                reserved.Plan.After);
            AssertRejected(
                blocked,
                MarketplacePlanningStatus.CancellationRejected,
                MarketplaceDiagnosticCodes.CancellationRejected);
        }

        [Test]
        public void ExpiredListingCannotBeReserved()
        {
            MarketplacePlanningResult listed = List(10L);
            MarketplaceSettlementRequest reserve = new MarketplaceSettlementRequest(
                "op.reserve.1",
                MarketplaceOperation.Reserve,
                Buyer,
                ListingId,
                ItemId,
                Seller,
                Buyer,
                10L,
                MarketplaceWalletKind.Oathmark,
                ListedAt + (72L * HourMs),
                listed.Plan.After.Revision,
                Policy().Binding);
            MarketplacePlanningResult expired = new MarketplaceSettlementPlanner(Policy()).Plan(
                reserve,
                WithBuyerWallet(listed.Plan.After, 10L));

            AssertRejected(
                expired,
                MarketplacePlanningStatus.Expired,
                MarketplaceDiagnosticCodes.ListingExpired);
        }

        [Test]
        public void ExpireAfterSeventyTwoHoursReleasesEscrow()
        {
            MarketplacePlanningResult listed = List(10L);
            MarketplaceSettlementRequest expire = new MarketplaceSettlementRequest(
                "op.expire.1",
                MarketplaceOperation.Expire,
                Seller,
                ListingId,
                ItemId,
                Seller,
                string.Empty,
                10L,
                MarketplaceWalletKind.Oathmark,
                ListedAt + (72L * HourMs),
                listed.Plan.After.Revision,
                Policy().Binding);
            MarketplacePlanningResult expired = new MarketplaceSettlementPlanner(Policy()).Plan(
                expire,
                listed.Plan.After);

            AssertPrepared(expired);
            Assert.That(
                Listing(expired.Plan.After, ListingId).Status,
                Is.EqualTo(MarketplaceListingStatus.Expired));
            Assert.That(
                Item(expired.Plan.After, ItemId).Custody,
                Is.EqualTo(MarketplaceItemCustody.Available));
        }

        [TestCase(MarketplaceWalletKind.LegacyGold)]
        [TestCase(MarketplaceWalletKind.KingdomResource)]
        [TestCase(MarketplaceWalletKind.GuildTreasury)]
        [TestCase(MarketplaceWalletKind.RealmResource)]
        [TestCase(MarketplaceWalletKind.WarzoneCredits)]
        [TestCase(MarketplaceWalletKind.Premium)]
        [TestCase(MarketplaceWalletKind.RealMoney)]
        public void ForbiddenWalletsCannotList(MarketplaceWalletKind kind)
        {
            MarketplacePolicySnapshot policy = Policy();
            var planner = new MarketplaceSettlementPlanner(policy);
            MarketplaceSettlementRequest request = new MarketplaceSettlementRequest(
                "op.list.1",
                MarketplaceOperation.List,
                Seller,
                ListingId,
                ItemId,
                Seller,
                string.Empty,
                10L,
                kind,
                ListedAt,
                1L,
                policy.Binding);
            MarketplacePlanningResult listed = planner.Plan(request, EmptyAuthority(policy, Seller));

            AssertRejected(
                listed,
                MarketplacePlanningStatus.ForbiddenWallet,
                MarketplaceDiagnosticCodes.ForbiddenWallet);
        }

        [Test]
        public void FractionalWalletIsNotAuthoritativeOathmark()
        {
            MarketplacePolicySnapshot policy = Policy();
            var planner = new MarketplaceSettlementPlanner(policy);
            MarketplacePlanningResult listed = List(10L);
            var fractional = new MarketplaceWalletSnapshot(
                Buyer,
                MarketplaceWalletKind.Oathmark,
                "oathmark",
                100L,
                1000L,
                "wallet.buyer.frac");
            var snapshot = new MarketplaceAuthoritySnapshot(
                listed.Plan.After.Status,
                listed.Plan.After.Revision,
                listed.Plan.After.CatalogBinding,
                new[] { fractional },
                listed.Plan.After.Items,
                listed.Plan.After.Listings,
                listed.Plan.After.Receipts,
                true);
            MarketplacePlanningResult reserved = planner.Plan(
                Request("op.reserve.1", MarketplaceOperation.Reserve, Buyer, 10L, Buyer, listed.Plan.After.Revision),
                snapshot);

            AssertRejected(
                reserved,
                MarketplacePlanningStatus.Malformed,
                MarketplaceDiagnosticCodes.AuthorityMalformed);
        }

        [Test]
        public void UnknownOutcomeReceiptFailsClosed()
        {
            MarketplacePlanningResult listed = List(10L);
            var uncertain = new MarketplaceAuthoritySnapshot(
                MarketplaceAuthorityStatus.CommitUncertain,
                listed.Plan.After.Revision,
                listed.Plan.After.CatalogBinding,
                listed.Plan.After.Wallets,
                listed.Plan.After.Items,
                listed.Plan.After.Listings,
                listed.Plan.After.Receipts,
                true);
            MarketplacePlanningResult blocked = new MarketplaceSettlementPlanner(Policy()).Plan(
                Request("op.reserve.1", MarketplaceOperation.Reserve, Buyer, 10L, Buyer, uncertain.Revision),
                uncertain);

            AssertRejected(
                blocked,
                MarketplacePlanningStatus.CommitUncertain,
                MarketplaceDiagnosticCodes.CommitUncertain);
        }

        [Test]
        public void PlannerAssemblyHasNoEngineSaveOrNetworkActivation()
        {
            Assembly assembly = typeof(MarketplaceSettlementPlanner).Assembly;
            string[] referenced = assembly.GetReferencedAssemblies()
                .Select(name => name.Name)
                .ToArray();

            Assert.That(assembly.GetName().Name, Is.EqualTo("AL.MarketplaceSettlement"));
            Assert.That(referenced, Does.Not.Contain("UnityEngine"));
            Assert.That(referenced, Does.Not.Contain("UnityEditor"));
            foreach (Type type in assembly.GetTypes())
            {
                StringAssert.DoesNotContain("Save", type.Name);
                StringAssert.DoesNotContain("Network", type.Name);
                StringAssert.DoesNotContain("Repair", type.Name);
                StringAssert.DoesNotContain("Consumable", type.Name);
            }
        }

        private static MarketplacePlanningResult List(long price)
        {
            MarketplacePolicySnapshot policy = Policy();
            var planner = new MarketplaceSettlementPlanner(policy);
            return planner.Plan(
                Request(MarketplaceOperation.List, Seller, price, string.Empty),
                EmptyAuthority(policy, Seller));
        }

        private static MarketplacePlanningResult Reserve(
            MarketplacePlanningResult listed,
            long price)
        {
            MarketplaceAuthoritySnapshot withBuyer = WithBuyerWallet(listed.Plan.After, price);
            return new MarketplaceSettlementPlanner(Policy()).Plan(
                Request("op.reserve.1", MarketplaceOperation.Reserve, Buyer, price, Buyer, withBuyer.Revision),
                withBuyer);
        }

        private static MarketplacePlanningResult Settle(long price, long buyerBalance, long sellerBalance)
        {
            MarketplacePolicySnapshot policy = Policy();
            var planner = new MarketplaceSettlementPlanner(policy);
            MarketplacePlanningResult listed = planner.Plan(
                Request(MarketplaceOperation.List, Seller, price, string.Empty),
                Authority(policy, sellerBalance));
            AssertPrepared(listed);
            MarketplaceAuthoritySnapshot withBuyer = WithBuyerWallet(listed.Plan.After, buyerBalance);
            MarketplacePlanningResult reserved = planner.Plan(
                Request("op.reserve.1", MarketplaceOperation.Reserve, Buyer, price, Buyer, withBuyer.Revision),
                withBuyer);
            AssertPrepared(reserved);
            return planner.Plan(
                Request("op.settle.1", MarketplaceOperation.Settle, Buyer, price, Buyer, reserved.Plan.After.Revision),
                reserved.Plan.After);
        }

        private static void AssertPrepared(MarketplacePlanningResult result)
        {
            Assert.That(result.Status, Is.EqualTo(MarketplacePlanningStatus.Prepared), Diagnostics(result));
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Plan.Receipt.TaxDestroyed, Is.EqualTo(result.Plan.Quote.TaxDestroyed));
            Assert.That(result.Plan.Receipt.SellerProceeds, Is.EqualTo(result.Plan.Quote.SellerProceeds));
        }

        private static void AssertRejected(
            MarketplacePlanningResult result,
            MarketplacePlanningStatus status,
            string code)
        {
            Assert.That(result.Status, Is.EqualTo(status), Diagnostics(result));
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(code));
        }

        private static string Diagnostics(MarketplacePlanningResult result)
        {
            return string.Join(
                "; ",
                result.Diagnostics.Select(row => row.Code + ":" + row.Message));
        }

        private static MarketplaceWalletSnapshot Wallet(
            MarketplaceAuthoritySnapshot snapshot,
            string accountId)
        {
            return snapshot.Wallets.Single(row => row.AccountId == accountId);
        }

        private static MarketplaceItemSnapshot Item(
            MarketplaceAuthoritySnapshot snapshot,
            string itemId)
        {
            return snapshot.Items.Single(row => row.ItemId == itemId);
        }

        private static MarketplaceListingSnapshot Listing(
            MarketplaceAuthoritySnapshot snapshot,
            string listingId)
        {
            return snapshot.Listings.Single(row => row.ListingId == listingId);
        }

        private static MarketplaceSettlementRequest Request(
            MarketplaceOperation operation,
            string actor,
            long price,
            string buyer)
        {
            return Request("op.list.1", operation, actor, price, buyer, 1L);
        }

        private static MarketplaceSettlementRequest Request(
            string operationId,
            MarketplaceOperation operation,
            string actor,
            long price,
            string buyer,
            long expectedRevision)
        {
            return new MarketplaceSettlementRequest(
                operationId,
                operation,
                actor,
                ListingId,
                ItemId,
                Seller,
                buyer,
                price,
                MarketplaceWalletKind.Oathmark,
                ListedAt,
                expectedRevision,
                Policy().Binding);
        }

        private static MarketplaceAuthoritySnapshot EmptyAuthority(
            MarketplacePolicySnapshot policy,
            string owner)
        {
            return new MarketplaceAuthoritySnapshot(
                MarketplaceAuthorityStatus.Available,
                1L,
                policy.Binding,
                Array.Empty<MarketplaceWalletSnapshot>(),
                new[] { new MarketplaceItemSnapshot(ItemId, owner, MarketplaceItemCustody.Available) },
                Array.Empty<MarketplaceListingSnapshot>(),
                Array.Empty<MarketplaceOperationReceipt>(),
                true);
        }

        private static MarketplaceAuthoritySnapshot Authority(
            MarketplacePolicySnapshot policy,
            long sellerBalance)
        {
            return new MarketplaceAuthoritySnapshot(
                MarketplaceAuthorityStatus.Available,
                1L,
                policy.Binding,
                new[] { OathmarkWallet(Seller, sellerBalance, "wallet.seller.1") },
                new[] { new MarketplaceItemSnapshot(ItemId, Seller, MarketplaceItemCustody.Available) },
                Array.Empty<MarketplaceListingSnapshot>(),
                Array.Empty<MarketplaceOperationReceipt>(),
                true);
        }

        private static MarketplaceAuthoritySnapshot WithBuyerWallet(
            MarketplaceAuthoritySnapshot source,
            long buyerBalance)
        {
            IEnumerable<MarketplaceWalletSnapshot> wallets = source.Wallets
                .Where(row => row.AccountId != Buyer)
                .Concat(new[] { OathmarkWallet(Buyer, buyerBalance, "wallet.buyer.1") })
                .OrderBy(row => row.AccountId, StringComparer.Ordinal);
            return new MarketplaceAuthoritySnapshot(
                source.Status,
                source.Revision,
                source.CatalogBinding,
                wallets,
                source.Items,
                source.Listings,
                source.Receipts,
                true);
        }

        private static MarketplaceWalletSnapshot OathmarkWallet(
            string accountId,
            long balance,
            string revision)
        {
            return new MarketplaceWalletSnapshot(
                accountId,
                MarketplaceWalletKind.Oathmark,
                "oathmark",
                1L,
                balance,
                revision);
        }

        private static MarketplacePolicySnapshot Policy(long maximumListingPrice = long.MaxValue)
        {
            return new MarketplacePolicySnapshot(
                MarketplaceCatalogStatus.Ready,
                new MarketplaceCatalogBinding(
                    1,
                    "1.0.0",
                    "oathmark_marketplace_policy_v1",
                    CatalogHash),
                "oathmark",
                1L,
                false,
                true,
                true,
                true,
                10L,
                maximumListingPrice,
                72L,
                10L,
                true,
                true);
        }
    }
}
