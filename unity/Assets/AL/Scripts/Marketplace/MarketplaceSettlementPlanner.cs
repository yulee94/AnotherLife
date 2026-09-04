using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Marketplace
{
    /// <summary>
    /// Engine-free Marketplace settlement planner. It never reads or writes
    /// save, network, UI, repair, consumable, or earning-source runtime.
    /// </summary>
    public sealed class MarketplaceSettlementPlanner
    {
        private const int MaximumWallets = 4096;
        private const int MaximumItems = 4096;
        private const int MaximumListings = 4096;
        private const int MaximumReceipts = 8192;
        private const long MillisecondsPerHour = 3600000L;

        private readonly MarketplacePolicySnapshot policy;

        public MarketplaceSettlementPlanner(MarketplacePolicySnapshot policy)
        {
            this.policy = policy;
        }

        public MarketplacePlanningResult Plan(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    MarketplacePlanningStatus.InvalidRequest,
                    MarketplaceDiagnosticCodes.InvalidRequest,
                    request?.OperationId,
                    "Marketplace operation identity, fields, or revisions are invalid.");
            }

            MarketplacePlanningResult policyGate = ValidatePolicy(policy);
            if (policyGate != null)
            {
                return policyGate;
            }

            if (!BindingEquals(request.ExpectedCatalogBinding, policy.Binding))
            {
                return Reject(
                    MarketplacePlanningStatus.StaleCatalog,
                    MarketplaceDiagnosticCodes.CatalogStale,
                    request.OperationId,
                    "The request is not fenced to the accepted Marketplace policy catalog.");
            }

            MarketplacePlanningResult authorityGate = ValidateAuthority(snapshot);
            if (authorityGate != null)
            {
                return authorityGate;
            }

            string requestFingerprint = MarketplaceDeterminism.RequestFingerprint(request);
            MarketplacePlanningResult replay = ClassifyReplay(
                request,
                requestFingerprint,
                snapshot.Receipts);
            if (replay != null)
            {
                return replay;
            }

            if (snapshot.Revision != request.ExpectedAuthorityRevision)
            {
                return Reject(
                    MarketplacePlanningStatus.StaleAuthority,
                    MarketplaceDiagnosticCodes.AuthorityStale,
                    request.OperationId,
                    "Expected Marketplace authority revision is stale.");
            }

            if (snapshot.Revision == long.MaxValue)
            {
                return Reject(
                    MarketplacePlanningStatus.Overflow,
                    MarketplaceDiagnosticCodes.AuthorityRevisionOverflow,
                    request.OperationId,
                    "Marketplace authority revision cannot advance.");
            }

            if (request.OfferedWalletKind != MarketplaceWalletKind.Oathmark)
            {
                return Reject(
                    MarketplacePlanningStatus.ForbiddenWallet,
                    MarketplaceDiagnosticCodes.ForbiddenWallet,
                    request.OperationId,
                    "Only the authoritative Oathmark wallet may settle Marketplace value.");
            }

            switch (request.Operation)
            {
                case MarketplaceOperation.List:
                    return PlanList(request, snapshot, requestFingerprint);
                case MarketplaceOperation.Reserve:
                    return PlanReserve(request, snapshot, requestFingerprint);
                case MarketplaceOperation.Settle:
                    return PlanSettle(request, snapshot, requestFingerprint);
                case MarketplaceOperation.Cancel:
                    return PlanCancel(request, snapshot, requestFingerprint);
                case MarketplaceOperation.Expire:
                    return PlanExpire(request, snapshot, requestFingerprint);
                default:
                    return Reject(
                        MarketplacePlanningStatus.InvalidRequest,
                        MarketplaceDiagnosticCodes.InvalidRequest,
                        request.OperationId,
                        "Marketplace operation is unknown.");
            }
        }

        private MarketplacePlanningResult PlanList(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot,
            string requestFingerprint)
        {
            if (!IdsEqual(request.ActorAccountId, request.SellerAccountId) ||
                !string.IsNullOrEmpty(request.BuyerAccountId))
            {
                return Reject(
                    MarketplacePlanningStatus.InvalidRequest,
                    MarketplaceDiagnosticCodes.InvalidRequest,
                    request.OperationId,
                    "List requires the seller actor and no buyer.");
            }

            MarketplacePlanningResult quoteGate = TryQuote(request.ListedPrice, out MarketplaceSettlementQuote quote);
            if (quoteGate != null)
            {
                return quoteGate;
            }

            if (FindListing(snapshot, request.ListingId) != null)
            {
                return Reject(
                    MarketplacePlanningStatus.Conflict,
                    MarketplaceDiagnosticCodes.OperationConflict,
                    request.ListingId,
                    "Listing identity is already present.");
            }

            MarketplaceItemSnapshot item = FindItem(snapshot, request.ItemId);
            if (item == null ||
                !IdsEqual(item.OwnerAccountId, request.SellerAccountId) ||
                item.Custody != MarketplaceItemCustody.Available)
            {
                return Reject(
                    item != null && !IdsEqual(item.OwnerAccountId, request.SellerAccountId)
                        ? MarketplacePlanningStatus.OwnershipRejected
                        : MarketplacePlanningStatus.OwnershipRejected,
                    item != null && item.Custody != MarketplaceItemCustody.Available
                        ? MarketplaceDiagnosticCodes.ItemLocked
                        : MarketplaceDiagnosticCodes.OwnershipRejected,
                    request.ItemId,
                    "Seller must own an available item to list it.");
            }

            if (snapshot.Listings.Any(row => IdsEqual(row.ItemId, request.ItemId) &&
                                             (row.Status == MarketplaceListingStatus.ListedEscrowed ||
                                              row.Status == MarketplaceListingStatus.Reserved)))
            {
                return Reject(
                    MarketplacePlanningStatus.Conflict,
                    MarketplaceDiagnosticCodes.ItemLocked,
                    request.ItemId,
                    "Item is already escrowed on another listing.");
            }

            var listing = new MarketplaceListingSnapshot(
                request.ListingId,
                request.ItemId,
                request.SellerAccountId,
                string.Empty,
                request.ListedPrice,
                request.NowUnixMs,
                MarketplaceListingStatus.ListedEscrowed);
            var escrowed = new MarketplaceItemSnapshot(
                item.ItemId,
                item.OwnerAccountId,
                MarketplaceItemCustody.Escrowed);
            return Commit(
                request,
                snapshot,
                requestFingerprint,
                quote,
                ReplaceItem(snapshot.Items, escrowed),
                InsertListing(snapshot.Listings, listing),
                snapshot.Wallets);
        }

        private MarketplacePlanningResult PlanReserve(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot,
            string requestFingerprint)
        {
            if (!IdsEqual(request.ActorAccountId, request.BuyerAccountId) ||
                IdsEqual(request.BuyerAccountId, request.SellerAccountId))
            {
                return Reject(
                    MarketplacePlanningStatus.ReservationRejected,
                    MarketplaceDiagnosticCodes.ReservationRejected,
                    request.OperationId,
                    "Reserve requires a distinct buyer actor.");
            }

            MarketplaceListingSnapshot listing = FindListing(snapshot, request.ListingId);
            MarketplacePlanningResult listingGate = RequireOpenListing(request, listing);
            if (listingGate != null)
            {
                return listingGate;
            }

            if (listing.Status != MarketplaceListingStatus.ListedEscrowed)
            {
                return Reject(
                    MarketplacePlanningStatus.ReservationRejected,
                    MarketplaceDiagnosticCodes.ReservationRejected,
                    request.ListingId,
                    "Only an unreserved listed listing may be reserved.");
            }

            MarketplacePlanningResult expiry = RequireNotExpired(request, listing);
            if (expiry != null)
            {
                return expiry;
            }

            MarketplacePlanningResult quoteGate = TryQuote(listing.ListedPrice, out MarketplaceSettlementQuote quote);
            if (quoteGate != null)
            {
                return quoteGate;
            }

            MarketplacePlanningResult funds = RequireBuyerFunds(snapshot, request.BuyerAccountId, quote.BuyerDebit);
            if (funds != null)
            {
                return funds;
            }

            var reserved = new MarketplaceListingSnapshot(
                listing.ListingId,
                listing.ItemId,
                listing.SellerAccountId,
                request.BuyerAccountId,
                listing.ListedPrice,
                listing.ListedAtUnixMs,
                MarketplaceListingStatus.Reserved);
            return Commit(
                request,
                snapshot,
                requestFingerprint,
                quote,
                snapshot.Items,
                ReplaceListing(snapshot.Listings, reserved),
                snapshot.Wallets);
        }

        private MarketplacePlanningResult PlanSettle(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot,
            string requestFingerprint)
        {
            if (!IdsEqual(request.ActorAccountId, request.BuyerAccountId))
            {
                return Reject(
                    MarketplacePlanningStatus.Unauthorized,
                    MarketplaceDiagnosticCodes.ReservationRejected,
                    request.OperationId,
                    "Settle requires the reserved buyer.");
            }

            MarketplaceListingSnapshot listing = FindListing(snapshot, request.ListingId);
            MarketplacePlanningResult listingGate = RequireOpenListing(request, listing);
            if (listingGate != null)
            {
                return listingGate;
            }

            if (listing.Status != MarketplaceListingStatus.Reserved ||
                !IdsEqual(listing.ReservedBuyerAccountId, request.BuyerAccountId) ||
                !IdsEqual(listing.SellerAccountId, request.SellerAccountId) ||
                listing.ListedPrice != request.ListedPrice)
            {
                return Reject(
                    MarketplacePlanningStatus.ReservationRejected,
                    MarketplaceDiagnosticCodes.ReservationRejected,
                    request.ListingId,
                    "Settle requires an exact reserved buyer, seller, and listed price.");
            }

            MarketplacePlanningResult quoteGate = TryQuote(listing.ListedPrice, out MarketplaceSettlementQuote quote);
            if (quoteGate != null)
            {
                return quoteGate;
            }

            MarketplaceWalletSnapshot buyer = FindOathmarkWallet(snapshot, request.BuyerAccountId);
            MarketplaceWalletSnapshot seller = FindOathmarkWallet(snapshot, request.SellerAccountId);
            if (buyer == null || seller == null)
            {
                return Reject(
                    MarketplacePlanningStatus.ForbiddenWallet,
                    MarketplaceDiagnosticCodes.ForbiddenWallet,
                    request.OperationId,
                    "Buyer and seller must have authoritative Oathmark wallets.");
            }

            if (buyer.Balance < quote.BuyerDebit)
            {
                return Reject(
                    MarketplacePlanningStatus.InsufficientFunds,
                    MarketplaceDiagnosticCodes.InsufficientFunds,
                    request.BuyerAccountId,
                    "Buyer Oathmark balance cannot cover the listed price.");
            }

            if (seller.Balance > long.MaxValue - quote.SellerProceeds)
            {
                return Reject(
                    MarketplacePlanningStatus.Overflow,
                    MarketplaceDiagnosticCodes.ArithmeticOverflow,
                    request.SellerAccountId,
                    "Seller Oathmark credit would overflow signed 64-bit arithmetic.");
            }

            MarketplaceItemSnapshot item = FindItem(snapshot, listing.ItemId);
            if (item == null ||
                !IdsEqual(item.OwnerAccountId, listing.SellerAccountId) ||
                item.Custody != MarketplaceItemCustody.Escrowed)
            {
                return Reject(
                    MarketplacePlanningStatus.OwnershipRejected,
                    MarketplaceDiagnosticCodes.OwnershipRejected,
                    listing.ItemId,
                    "Escrowed item ownership does not match the listing seller.");
            }

            var sold = new MarketplaceListingSnapshot(
                listing.ListingId,
                listing.ItemId,
                listing.SellerAccountId,
                listing.ReservedBuyerAccountId,
                listing.ListedPrice,
                listing.ListedAtUnixMs,
                MarketplaceListingStatus.Sold);
            var transferred = new MarketplaceItemSnapshot(
                item.ItemId,
                request.BuyerAccountId,
                MarketplaceItemCustody.Available);
            var afterBuyer = new MarketplaceWalletSnapshot(
                buyer.AccountId,
                buyer.Kind,
                buyer.TechnicalCurrencyId,
                buyer.IntegerUnitScale,
                buyer.Balance - quote.BuyerDebit,
                NextRevision(buyer.WalletRevision));
            var afterSeller = new MarketplaceWalletSnapshot(
                seller.AccountId,
                seller.Kind,
                seller.TechnicalCurrencyId,
                seller.IntegerUnitScale,
                seller.Balance + quote.SellerProceeds,
                NextRevision(seller.WalletRevision));
            return Commit(
                request,
                snapshot,
                requestFingerprint,
                quote,
                ReplaceItem(snapshot.Items, transferred),
                ReplaceListing(snapshot.Listings, sold),
                ReplaceWallet(ReplaceWallet(snapshot.Wallets, afterBuyer), afterSeller));
        }

        private MarketplacePlanningResult PlanCancel(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot,
            string requestFingerprint)
        {
            if (!IdsEqual(request.ActorAccountId, request.SellerAccountId))
            {
                return Reject(
                    MarketplacePlanningStatus.Unauthorized,
                    MarketplaceDiagnosticCodes.CancellationRejected,
                    request.OperationId,
                    "Only the seller may cancel a listing.");
            }

            MarketplaceListingSnapshot listing = FindListing(snapshot, request.ListingId);
            MarketplacePlanningResult listingGate = RequireOpenListing(request, listing);
            if (listingGate != null)
            {
                return listingGate;
            }

            if (listing.Status == MarketplaceListingStatus.Reserved)
            {
                return Reject(
                    MarketplacePlanningStatus.CancellationRejected,
                    MarketplaceDiagnosticCodes.CancellationRejected,
                    request.ListingId,
                    "Seller may cancel only before buyer reservation.");
            }

            if (listing.Status != MarketplaceListingStatus.ListedEscrowed)
            {
                return Reject(
                    MarketplacePlanningStatus.CancellationRejected,
                    MarketplaceDiagnosticCodes.CancellationRejected,
                    request.ListingId,
                    "Only an unreserved listed listing may be cancelled.");
            }

            MarketplacePlanningResult quoteGate = TryQuote(listing.ListedPrice, out MarketplaceSettlementQuote quote);
            if (quoteGate != null)
            {
                return quoteGate;
            }

            return ReleaseListing(
                request,
                snapshot,
                requestFingerprint,
                quote,
                listing,
                MarketplaceListingStatus.Cancelled);
        }

        private MarketplacePlanningResult PlanExpire(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot,
            string requestFingerprint)
        {
            MarketplaceListingSnapshot listing = FindListing(snapshot, request.ListingId);
            MarketplacePlanningResult listingGate = RequireOpenListing(request, listing);
            if (listingGate != null)
            {
                return listingGate;
            }

            if (listing.Status == MarketplaceListingStatus.Reserved)
            {
                return Reject(
                    MarketplacePlanningStatus.ReservationRejected,
                    MarketplaceDiagnosticCodes.ReservationRejected,
                    request.ListingId,
                    "A reserved listing cannot expire until settlement or reservation release.");
            }

            if (listing.Status != MarketplaceListingStatus.ListedEscrowed)
            {
                return Reject(
                    MarketplacePlanningStatus.ListingStateInvalid,
                    MarketplaceDiagnosticCodes.ListingStateInvalid,
                    request.ListingId,
                    "Only an unreserved listed listing may expire.");
            }

            if (!IsExpired(request.NowUnixMs, listing.ListedAtUnixMs))
            {
                return Reject(
                    MarketplacePlanningStatus.InvalidRequest,
                    MarketplaceDiagnosticCodes.InvalidRequest,
                    request.ListingId,
                    "Listing duration has not elapsed.");
            }

            MarketplacePlanningResult quoteGate = TryQuote(listing.ListedPrice, out MarketplaceSettlementQuote quote);
            if (quoteGate != null)
            {
                return quoteGate;
            }

            return ReleaseListing(
                request,
                snapshot,
                requestFingerprint,
                quote,
                listing,
                MarketplaceListingStatus.Expired);
        }

        private MarketplacePlanningResult ReleaseListing(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot,
            string requestFingerprint,
            MarketplaceSettlementQuote quote,
            MarketplaceListingSnapshot listing,
            MarketplaceListingStatus terminal)
        {
            MarketplaceItemSnapshot item = FindItem(snapshot, listing.ItemId);
            if (item == null ||
                !IdsEqual(item.OwnerAccountId, listing.SellerAccountId) ||
                item.Custody != MarketplaceItemCustody.Escrowed)
            {
                return Reject(
                    MarketplacePlanningStatus.OwnershipRejected,
                    MarketplaceDiagnosticCodes.OwnershipRejected,
                    listing.ItemId,
                    "Escrowed item ownership does not match the listing seller.");
            }

            var released = new MarketplaceItemSnapshot(
                item.ItemId,
                item.OwnerAccountId,
                MarketplaceItemCustody.Available);
            var closed = new MarketplaceListingSnapshot(
                listing.ListingId,
                listing.ItemId,
                listing.SellerAccountId,
                listing.ReservedBuyerAccountId,
                listing.ListedPrice,
                listing.ListedAtUnixMs,
                terminal);
            return Commit(
                request,
                snapshot,
                requestFingerprint,
                quote,
                ReplaceItem(snapshot.Items, released),
                ReplaceListing(snapshot.Listings, closed),
                snapshot.Wallets);
        }

        private MarketplacePlanningResult TryQuote(long listedPrice, out MarketplaceSettlementQuote quote)
        {
            quote = null;
            if (listedPrice < policy.MinimumListingPrice)
            {
                return Reject(
                    MarketplacePlanningStatus.InvalidRequest,
                    MarketplaceDiagnosticCodes.PriceBelowMinimum,
                    MarketplaceDeterminism.Long(listedPrice),
                    "Listed price is below the catalog minimum.");
            }

            if (listedPrice > policy.MaximumListingPrice)
            {
                return Reject(
                    MarketplacePlanningStatus.Overflow,
                    MarketplaceDiagnosticCodes.PriceAboveMaximum,
                    MarketplaceDeterminism.Long(listedPrice),
                    "Listed price exceeds the catalog maximum.");
            }

            long tax = listedPrice / policy.TaxDivisor;
            long proceeds = listedPrice - tax;
            if (tax < 0L || proceeds < 0L || tax + proceeds != listedPrice)
            {
                return Reject(
                    MarketplacePlanningStatus.Overflow,
                    MarketplaceDiagnosticCodes.ArithmeticOverflow,
                    MarketplaceDeterminism.Long(listedPrice),
                    "Settlement split cannot be represented in signed 64-bit units.");
            }

            quote = new MarketplaceSettlementQuote(listedPrice, tax, proceeds);
            return null;
        }

        private MarketplacePlanningResult RequireOpenListing(
            MarketplaceSettlementRequest request,
            MarketplaceListingSnapshot listing)
        {
            if (listing == null)
            {
                return Reject(
                    MarketplacePlanningStatus.InvalidRequest,
                    MarketplaceDiagnosticCodes.ListingNotFound,
                    request.ListingId,
                    "Listing identity was not found.");
            }

            if (!IdsEqual(listing.ItemId, request.ItemId) ||
                !IdsEqual(listing.SellerAccountId, request.SellerAccountId))
            {
                return Reject(
                    MarketplacePlanningStatus.Conflict,
                    MarketplaceDiagnosticCodes.OperationConflict,
                    request.ListingId,
                    "Listing correlation identities do not match the request.");
            }

            return null;
        }

        private MarketplacePlanningResult RequireNotExpired(
            MarketplaceSettlementRequest request,
            MarketplaceListingSnapshot listing)
        {
            if (IsExpired(request.NowUnixMs, listing.ListedAtUnixMs))
            {
                return Reject(
                    MarketplacePlanningStatus.Expired,
                    MarketplaceDiagnosticCodes.ListingExpired,
                    listing.ListingId,
                    "Listing duration has elapsed.");
            }

            return null;
        }

        private bool IsExpired(long nowUnixMs, long listedAtUnixMs)
        {
            if (policy.ListingDurationHours > long.MaxValue / MillisecondsPerHour)
            {
                return true;
            }

            long durationMs = policy.ListingDurationHours * MillisecondsPerHour;
            if (listedAtUnixMs > long.MaxValue - durationMs)
            {
                return false;
            }

            return nowUnixMs >= listedAtUnixMs + durationMs;
        }

        private MarketplacePlanningResult RequireBuyerFunds(
            MarketplaceAuthoritySnapshot snapshot,
            string buyerAccountId,
            long debit)
        {
            MarketplaceWalletSnapshot buyer = FindOathmarkWallet(snapshot, buyerAccountId);
            if (buyer == null)
            {
                return Reject(
                    MarketplacePlanningStatus.ForbiddenWallet,
                    MarketplaceDiagnosticCodes.ForbiddenWallet,
                    buyerAccountId,
                    "Buyer must present an authoritative Oathmark wallet.");
            }

            if (buyer.Balance < debit)
            {
                return Reject(
                    MarketplacePlanningStatus.InsufficientFunds,
                    MarketplaceDiagnosticCodes.InsufficientFunds,
                    buyerAccountId,
                    "Buyer Oathmark balance cannot cover the listed price.");
            }

            return null;
        }

        private MarketplacePlanningResult Commit(
            MarketplaceSettlementRequest request,
            MarketplaceAuthoritySnapshot snapshot,
            string requestFingerprint,
            MarketplaceSettlementQuote quote,
            IReadOnlyList<MarketplaceItemSnapshot> items,
            IReadOnlyList<MarketplaceListingSnapshot> listings,
            IReadOnlyList<MarketplaceWalletSnapshot> wallets)
        {
            long resultingRevision = snapshot.Revision + 1L;
            string planHash = MarketplaceDeterminism.PlanHash(
                request,
                requestFingerprint,
                quote,
                resultingRevision);
            var receipt = new MarketplaceOperationReceipt(
                request.OperationId,
                request.Operation,
                requestFingerprint,
                request.ListingId,
                request.ItemId,
                request.SellerAccountId,
                request.BuyerAccountId,
                quote.ListedPrice,
                quote.TaxDestroyed,
                quote.SellerProceeds,
                resultingRevision,
                planHash,
                true);
            IReadOnlyList<MarketplaceOperationReceipt> receipts = snapshot.Receipts
                .Concat(new[] { receipt })
                .OrderBy(row => row.ResultingAuthorityRevision)
                .ThenBy(row => row.OperationId, StringComparer.Ordinal)
                .ToArray();
            var after = new MarketplaceAuthoritySnapshot(
                MarketplaceAuthorityStatus.Available,
                resultingRevision,
                policy.Binding,
                wallets.OrderBy(row => row.AccountId, StringComparer.Ordinal).ToArray(),
                items.OrderBy(row => row.ItemId, StringComparer.Ordinal).ToArray(),
                listings.OrderBy(row => row.ListingId, StringComparer.Ordinal).ToArray(),
                receipts,
                true);
            var plan = new MarketplaceSettlementPlan(
                request.Operation,
                requestFingerprint,
                snapshot,
                after,
                receipt,
                quote,
                planHash);
            return new MarketplacePlanningResult(
                MarketplacePlanningStatus.Prepared,
                plan,
                null,
                Array.Empty<MarketplaceDiagnostic>());
        }

        private MarketplacePlanningResult ValidatePolicy(MarketplacePolicySnapshot candidate)
        {
            if (candidate == null || candidate.Status == MarketplaceCatalogStatus.Unavailable)
            {
                return Reject(
                    MarketplacePlanningStatus.Unavailable,
                    MarketplaceDiagnosticCodes.CatalogUnavailable,
                    string.Empty,
                    "Oathmark Marketplace policy catalog is unavailable.");
            }

            if (candidate.Status == MarketplaceCatalogStatus.UnsupportedVersion)
            {
                return Reject(
                    MarketplacePlanningStatus.Unsupported,
                    MarketplaceDiagnosticCodes.CatalogUnsupported,
                    string.Empty,
                    "Oathmark Marketplace policy catalog version is unsupported.");
            }

            if (candidate.Status == MarketplaceCatalogStatus.Incomplete)
            {
                return Reject(
                    MarketplacePlanningStatus.Unavailable,
                    MarketplaceDiagnosticCodes.CatalogIncomplete,
                    string.Empty,
                    "Oathmark Marketplace policy catalog is incomplete.");
            }

            if (candidate.Status != MarketplaceCatalogStatus.Ready ||
                !candidate.IsComplete ||
                !IsValidBinding(candidate.Binding) ||
                !StringComparer.Ordinal.Equals(candidate.TechnicalCurrencyId, "oathmark") ||
                candidate.IntegerUnitScale != 1L ||
                candidate.FractionalUnits ||
                !candidate.ConversionForbidden ||
                !candidate.PremiumOrRealMoneyForbidden ||
                !candidate.SoleMainCurrency ||
                candidate.MinimumListingPrice <= 0L ||
                candidate.MaximumListingPrice < candidate.MinimumListingPrice ||
                candidate.ListingDurationHours <= 0L ||
                candidate.TaxDivisor <= 0L ||
                !candidate.TaxDestroyed)
            {
                return Reject(
                    MarketplacePlanningStatus.InvalidPolicy,
                    MarketplaceDiagnosticCodes.InvalidPolicy,
                    string.Empty,
                    "Oathmark Marketplace policy is incomplete or contradictory.");
            }

            return null;
        }

        private MarketplacePlanningResult ValidateAuthority(MarketplaceAuthoritySnapshot snapshot)
        {
            if (snapshot == null || snapshot.Status == MarketplaceAuthorityStatus.Unavailable)
            {
                return Reject(
                    MarketplacePlanningStatus.Unavailable,
                    MarketplaceDiagnosticCodes.AuthorityUnavailable,
                    string.Empty,
                    "Marketplace authority snapshot is unavailable.");
            }

            if (snapshot.Status == MarketplaceAuthorityStatus.CommitUncertain)
            {
                return Reject(
                    MarketplacePlanningStatus.CommitUncertain,
                    MarketplaceDiagnosticCodes.CommitUncertain,
                    string.Empty,
                    "Marketplace authority requires reconciliation before another operation.");
            }

            if (snapshot.Status == MarketplaceAuthorityStatus.UnsupportedReadOnly)
            {
                return Reject(
                    MarketplacePlanningStatus.Unsupported,
                    MarketplaceDiagnosticCodes.AuthorityUnsupported,
                    string.Empty,
                    "Unknown-future Marketplace authority is preserved read-only.");
            }

            if (snapshot.Status != MarketplaceAuthorityStatus.Available ||
                !snapshot.IsComplete ||
                snapshot.Revision < 0L ||
                !BindingEquals(snapshot.CatalogBinding, policy.Binding) ||
                snapshot.Wallets == null ||
                snapshot.Items == null ||
                snapshot.Listings == null ||
                snapshot.Receipts == null ||
                snapshot.Wallets.Count > MaximumWallets ||
                snapshot.Items.Count > MaximumItems ||
                snapshot.Listings.Count > MaximumListings ||
                snapshot.Receipts.Count > MaximumReceipts ||
                !IsStrictlyOrdered(snapshot.Wallets, row => row?.AccountId) ||
                !IsStrictlyOrdered(snapshot.Items, row => row?.ItemId) ||
                !IsStrictlyOrdered(snapshot.Listings, row => row?.ListingId))
            {
                return Reject(
                    MarketplacePlanningStatus.Malformed,
                    MarketplaceDiagnosticCodes.AuthorityMalformed,
                    string.Empty,
                    "Marketplace authority snapshot is malformed.");
            }

            var walletAccounts = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketplaceWalletSnapshot wallet in snapshot.Wallets)
            {
                if (wallet == null ||
                    !MarketplaceDeterminism.IsOpaqueId(wallet.AccountId) ||
                    !walletAccounts.Add(wallet.AccountId) ||
                    wallet.Balance < 0L ||
                    wallet.IntegerUnitScale != 1L ||
                    !MarketplaceDeterminism.IsOpaqueId(wallet.WalletRevision) ||
                    (wallet.Kind == MarketplaceWalletKind.Oathmark &&
                     !StringComparer.Ordinal.Equals(wallet.TechnicalCurrencyId, policy.TechnicalCurrencyId)) ||
                    (wallet.Kind != MarketplaceWalletKind.Oathmark &&
                     StringComparer.Ordinal.Equals(wallet.TechnicalCurrencyId, policy.TechnicalCurrencyId)))
                {
                    return Reject(
                        MarketplacePlanningStatus.Malformed,
                        MarketplaceDiagnosticCodes.AuthorityMalformed,
                        string.Empty,
                        "Marketplace wallets are malformed or mix currency identity.");
                }
            }

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketplaceItemSnapshot item in snapshot.Items)
            {
                if (item == null ||
                    !MarketplaceDeterminism.IsOpaqueId(item.ItemId) ||
                    !MarketplaceDeterminism.IsOpaqueId(item.OwnerAccountId) ||
                    !itemIds.Add(item.ItemId) ||
                    !Enum.IsDefined(typeof(MarketplaceItemCustody), item.Custody) ||
                    item.Custody == MarketplaceItemCustody.Unknown)
                {
                    return Reject(
                        MarketplacePlanningStatus.Malformed,
                        MarketplaceDiagnosticCodes.AuthorityMalformed,
                        string.Empty,
                        "Marketplace items are malformed.");
                }
            }

            var listingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketplaceListingSnapshot listing in snapshot.Listings)
            {
                if (listing == null ||
                    !MarketplaceDeterminism.IsOpaqueId(listing.ListingId) ||
                    !MarketplaceDeterminism.IsOpaqueId(listing.ItemId) ||
                    !MarketplaceDeterminism.IsOpaqueId(listing.SellerAccountId) ||
                    !listingIds.Add(listing.ListingId) ||
                    listing.ListedPrice < 0L ||
                    listing.ListedAtUnixMs < 0L ||
                    !Enum.IsDefined(typeof(MarketplaceListingStatus), listing.Status) ||
                    listing.Status == MarketplaceListingStatus.Unknown)
                {
                    return Reject(
                        MarketplacePlanningStatus.Malformed,
                        MarketplaceDiagnosticCodes.AuthorityMalformed,
                        string.Empty,
                        "Marketplace listings are malformed.");
                }
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (MarketplaceOperationReceipt receipt in snapshot.Receipts)
            {
                if (receipt == null ||
                    !MarketplaceDeterminism.IsOpaqueId(receipt.OperationId) ||
                    !operationIds.Add(receipt.OperationId) ||
                    (receipt.IsSupported && !MarketplaceDeterminism.IsSha256(receipt.RequestFingerprint)))
                {
                    return Reject(
                        MarketplacePlanningStatus.Malformed,
                        MarketplaceDiagnosticCodes.AuthorityMalformed,
                        string.Empty,
                        "Marketplace receipts are malformed.");
                }
            }

            return null;
        }

        private static MarketplacePlanningResult ClassifyReplay(
            MarketplaceSettlementRequest request,
            string requestFingerprint,
            IReadOnlyList<MarketplaceOperationReceipt> receipts)
        {
            MarketplaceOperationReceipt match = receipts.SingleOrDefault(row =>
                string.Equals(row.OperationId, request.OperationId, StringComparison.Ordinal));
            if (match == null)
            {
                return null;
            }

            if (!match.IsSupported)
            {
                return Reject(
                    MarketplacePlanningStatus.Unsupported,
                    MarketplaceDiagnosticCodes.ReplayUnsupported,
                    match.OperationId,
                    "Operation identity belongs to unknown-future Marketplace history.");
            }

            bool exact = match.Operation == request.Operation &&
                         string.Equals(match.RequestFingerprint, requestFingerprint, StringComparison.Ordinal) &&
                         string.Equals(match.ListingId, request.ListingId, StringComparison.Ordinal) &&
                         string.Equals(match.ItemId, request.ItemId, StringComparison.Ordinal) &&
                         string.Equals(match.SellerAccountId, request.SellerAccountId, StringComparison.Ordinal) &&
                         string.Equals(match.BuyerAccountId, request.BuyerAccountId, StringComparison.Ordinal) &&
                         match.ListedPrice == request.ListedPrice;
            return exact
                ? new MarketplacePlanningResult(
                    MarketplacePlanningStatus.AlreadyCommitted,
                    null,
                    match,
                    new[]
                    {
                        new MarketplaceDiagnostic(
                            MarketplaceDiagnosticCodes.Replay,
                            match.OperationId,
                            "Committed Marketplace receipt already satisfies this operation.")
                    })
                : Reject(
                    MarketplacePlanningStatus.Conflict,
                    MarketplaceDiagnosticCodes.OperationConflict,
                    match.OperationId,
                    "Operation identity is already bound to different Marketplace semantics.");
        }

        private static bool IsValidRequest(MarketplaceSettlementRequest request)
        {
            return request != null &&
                   MarketplaceDeterminism.IsOpaqueId(request.OperationId) &&
                   Enum.IsDefined(typeof(MarketplaceOperation), request.Operation) &&
                   request.Operation != MarketplaceOperation.Unknown &&
                   MarketplaceDeterminism.IsOpaqueId(request.ActorAccountId) &&
                   MarketplaceDeterminism.IsOpaqueId(request.ListingId) &&
                   MarketplaceDeterminism.IsOpaqueId(request.ItemId) &&
                   MarketplaceDeterminism.IsOpaqueId(request.SellerAccountId) &&
                   (string.IsNullOrEmpty(request.BuyerAccountId) ||
                    MarketplaceDeterminism.IsOpaqueId(request.BuyerAccountId)) &&
                   request.ListedPrice >= 0L &&
                   request.NowUnixMs >= 0L &&
                   request.ExpectedAuthorityRevision >= 0L &&
                   Enum.IsDefined(typeof(MarketplaceWalletKind), request.OfferedWalletKind) &&
                   request.OfferedWalletKind != MarketplaceWalletKind.Unknown &&
                   IsValidBinding(request.ExpectedCatalogBinding);
        }

        private static bool IsValidBinding(MarketplaceCatalogBinding binding)
        {
            return binding != null &&
                   binding.SchemaVersion == 1 &&
                   MarketplaceDeterminism.IsOpaqueId(binding.ContentVersion) &&
                   MarketplaceDeterminism.IsOpaqueId(binding.SourceRevision) &&
                   MarketplaceDeterminism.IsSha256(binding.CatalogHash);
        }

        private static bool BindingEquals(
            MarketplaceCatalogBinding left,
            MarketplaceCatalogBinding right)
        {
            return left != null &&
                   right != null &&
                   left.SchemaVersion == right.SchemaVersion &&
                   string.Equals(left.ContentVersion, right.ContentVersion, StringComparison.Ordinal) &&
                   string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CatalogHash, right.CatalogHash, StringComparison.Ordinal);
        }

        private MarketplaceWalletSnapshot FindOathmarkWallet(
            MarketplaceAuthoritySnapshot snapshot,
            string accountId)
        {
            return snapshot.Wallets.SingleOrDefault(row =>
                IdsEqual(row.AccountId, accountId) &&
                row.Kind == MarketplaceWalletKind.Oathmark &&
                StringComparer.Ordinal.Equals(row.TechnicalCurrencyId, policy.TechnicalCurrencyId) &&
                row.IntegerUnitScale == 1L);
        }

        private static MarketplaceItemSnapshot FindItem(
            MarketplaceAuthoritySnapshot snapshot,
            string itemId)
        {
            return snapshot.Items.SingleOrDefault(row => IdsEqual(row.ItemId, itemId));
        }

        private static MarketplaceListingSnapshot FindListing(
            MarketplaceAuthoritySnapshot snapshot,
            string listingId)
        {
            return snapshot.Listings.SingleOrDefault(row => IdsEqual(row.ListingId, listingId));
        }

        private static IReadOnlyList<MarketplaceItemSnapshot> ReplaceItem(
            IReadOnlyList<MarketplaceItemSnapshot> items,
            MarketplaceItemSnapshot candidate)
        {
            return items.Select(row =>
                    IdsEqual(row.ItemId, candidate.ItemId) ? candidate : row)
                .OrderBy(row => row.ItemId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<MarketplaceListingSnapshot> InsertListing(
            IReadOnlyList<MarketplaceListingSnapshot> listings,
            MarketplaceListingSnapshot candidate)
        {
            return listings.Concat(new[] { candidate })
                .OrderBy(row => row.ListingId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<MarketplaceListingSnapshot> ReplaceListing(
            IReadOnlyList<MarketplaceListingSnapshot> listings,
            MarketplaceListingSnapshot candidate)
        {
            return listings.Select(row =>
                    IdsEqual(row.ListingId, candidate.ListingId) ? candidate : row)
                .OrderBy(row => row.ListingId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<MarketplaceWalletSnapshot> ReplaceWallet(
            IReadOnlyList<MarketplaceWalletSnapshot> wallets,
            MarketplaceWalletSnapshot candidate)
        {
            return wallets.Select(row =>
                    IdsEqual(row.AccountId, candidate.AccountId) ? candidate : row)
                .OrderBy(row => row.AccountId, StringComparer.Ordinal)
                .ToArray();
        }

        private static string NextRevision(string current)
        {
            return current + ".next";
        }

        private static bool IdsEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static bool IsStrictlyOrdered<T>(IReadOnlyList<T> rows, Func<T, string> key)
        {
            string previous = null;
            foreach (T row in rows)
            {
                string current = key(row);
                if (string.IsNullOrEmpty(current) ||
                    (previous != null && string.CompareOrdinal(previous, current) >= 0))
                {
                    return false;
                }

                previous = current;
            }

            return true;
        }

        private static MarketplacePlanningResult Reject(
            MarketplacePlanningStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new MarketplacePlanningResult(
                status,
                null,
                null,
                new[] { new MarketplaceDiagnostic(code, subjectId, message) });
        }
    }
}
