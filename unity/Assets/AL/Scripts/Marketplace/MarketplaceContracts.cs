using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Marketplace
{
    public enum MarketplaceCatalogStatus
    {
        Ready = 0,
        Unavailable = 1,
        UnsupportedVersion = 2,
        Malformed = 3,
        Incomplete = 4
    }

    public enum MarketplaceAuthorityStatus
    {
        Available = 0,
        Unavailable = 1,
        Malformed = 2,
        UnsupportedReadOnly = 3,
        CommitUncertain = 4
    }

    public enum MarketplaceWalletKind
    {
        Unknown = 0,
        Oathmark = 1,
        LegacyGold = 2,
        KingdomResource = 3,
        GuildTreasury = 4,
        RealmResource = 5,
        WarzoneCredits = 6,
        Premium = 7,
        RealMoney = 8
    }

    public enum MarketplaceItemCustody
    {
        Unknown = 0,
        Available = 1,
        Escrowed = 2
    }

    public enum MarketplaceListingStatus
    {
        Unknown = 0,
        ListedEscrowed = 1,
        Reserved = 2,
        Sold = 3,
        Cancelled = 4,
        Expired = 5
    }

    public enum MarketplaceOperation
    {
        Unknown = 0,
        List = 1,
        Reserve = 2,
        Settle = 3,
        Cancel = 4,
        Expire = 5
    }

    public enum MarketplacePlanningStatus
    {
        Prepared = 0,
        AlreadyCommitted = 1,
        InvalidRequest = 2,
        InvalidPolicy = 3,
        Unauthorized = 4,
        OwnershipRejected = 5,
        ReservationRejected = 6,
        Expired = 7,
        CancellationRejected = 8,
        InsufficientFunds = 9,
        Overflow = 10,
        ForbiddenWallet = 11,
        Conflict = 12,
        StaleAuthority = 13,
        StaleCatalog = 14,
        Unsupported = 15,
        Unavailable = 16,
        Malformed = 17,
        CommitUncertain = 18,
        ListingStateInvalid = 19
    }

    public static class MarketplaceDiagnosticCodes
    {
        public const string InvalidRequest = "AL-MARKET-REQUEST-INVALID";
        public const string InvalidPolicy = "AL-MARKET-POLICY-INVALID";
        public const string CatalogUnavailable = "AL-MARKET-CATALOG-UNAVAILABLE";
        public const string CatalogUnsupported = "AL-MARKET-CATALOG-UNSUPPORTED";
        public const string CatalogIncomplete = "AL-MARKET-CATALOG-INCOMPLETE";
        public const string CatalogMalformed = "AL-MARKET-CATALOG-MALFORMED";
        public const string CatalogStale = "AL-MARKET-CATALOG-STALE";
        public const string AuthorityUnavailable = "AL-MARKET-AUTHORITY-UNAVAILABLE";
        public const string AuthorityMalformed = "AL-MARKET-AUTHORITY-MALFORMED";
        public const string AuthorityUnsupported = "AL-MARKET-AUTHORITY-UNSUPPORTED";
        public const string CommitUncertain = "AL-MARKET-COMMIT-UNCERTAIN";
        public const string AuthorityStale = "AL-MARKET-AUTHORITY-STALE";
        public const string AuthorityRevisionOverflow = "AL-MARKET-AUTHORITY-REVISION-OVERFLOW";
        public const string ForbiddenWallet = "AL-MARKET-WALLET-FORBIDDEN";
        public const string PriceBelowMinimum = "AL-MARKET-PRICE-BELOW-MINIMUM";
        public const string PriceAboveMaximum = "AL-MARKET-PRICE-ABOVE-MAXIMUM";
        public const string ArithmeticOverflow = "AL-MARKET-ARITHMETIC-OVERFLOW";
        public const string OwnershipRejected = "AL-MARKET-OWNERSHIP-REJECTED";
        public const string ItemLocked = "AL-MARKET-ITEM-LOCKED";
        public const string ListingNotFound = "AL-MARKET-LISTING-NOT-FOUND";
        public const string ListingStateInvalid = "AL-MARKET-LISTING-STATE-INVALID";
        public const string ReservationRejected = "AL-MARKET-RESERVATION-REJECTED";
        public const string CancellationRejected = "AL-MARKET-CANCELLATION-REJECTED";
        public const string ListingExpired = "AL-MARKET-LISTING-EXPIRED";
        public const string InsufficientFunds = "AL-MARKET-FUNDS-INSUFFICIENT";
        public const string Replay = "AL-MARKET-REPLAY";
        public const string OperationConflict = "AL-MARKET-OPERATION-CONFLICT";
        public const string ReplayUnsupported = "AL-MARKET-REPLAY-UNSUPPORTED";
    }

    public sealed class MarketplaceCatalogBinding
    {
        public MarketplaceCatalogBinding(
            int schemaVersion,
            string contentVersion,
            string sourceRevision,
            string catalogHash)
        {
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            CatalogHash = catalogHash ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string CatalogHash { get; }
    }

    public sealed class MarketplacePolicySnapshot
    {
        public MarketplacePolicySnapshot(
            MarketplaceCatalogStatus status,
            MarketplaceCatalogBinding binding,
            string technicalCurrencyId,
            long integerUnitScale,
            bool fractionalUnits,
            bool conversionForbidden,
            bool premiumOrRealMoneyForbidden,
            bool soleMainCurrency,
            long minimumListingPrice,
            long maximumListingPrice,
            long listingDurationHours,
            long taxDivisor,
            bool taxDestroyed,
            bool isComplete)
        {
            Status = status;
            Binding = binding;
            TechnicalCurrencyId = technicalCurrencyId ?? string.Empty;
            IntegerUnitScale = integerUnitScale;
            FractionalUnits = fractionalUnits;
            ConversionForbidden = conversionForbidden;
            PremiumOrRealMoneyForbidden = premiumOrRealMoneyForbidden;
            SoleMainCurrency = soleMainCurrency;
            MinimumListingPrice = minimumListingPrice;
            MaximumListingPrice = maximumListingPrice;
            ListingDurationHours = listingDurationHours;
            TaxDivisor = taxDivisor;
            TaxDestroyed = taxDestroyed;
            IsComplete = isComplete;
        }

        public MarketplaceCatalogStatus Status { get; }
        public MarketplaceCatalogBinding Binding { get; }
        public string TechnicalCurrencyId { get; }
        public long IntegerUnitScale { get; }
        public bool FractionalUnits { get; }
        public bool ConversionForbidden { get; }
        public bool PremiumOrRealMoneyForbidden { get; }
        public bool SoleMainCurrency { get; }
        public long MinimumListingPrice { get; }
        public long MaximumListingPrice { get; }
        public long ListingDurationHours { get; }
        public long TaxDivisor { get; }
        public bool TaxDestroyed { get; }
        public bool IsComplete { get; }
    }

    public sealed class MarketplaceWalletSnapshot
    {
        public MarketplaceWalletSnapshot(
            string accountId,
            MarketplaceWalletKind kind,
            string technicalCurrencyId,
            long integerUnitScale,
            long balance,
            string walletRevision)
        {
            AccountId = accountId ?? string.Empty;
            Kind = kind;
            TechnicalCurrencyId = technicalCurrencyId ?? string.Empty;
            IntegerUnitScale = integerUnitScale;
            Balance = balance;
            WalletRevision = walletRevision ?? string.Empty;
        }

        public string AccountId { get; }
        public MarketplaceWalletKind Kind { get; }
        public string TechnicalCurrencyId { get; }
        public long IntegerUnitScale { get; }
        public long Balance { get; }
        public string WalletRevision { get; }
    }

    public sealed class MarketplaceItemSnapshot
    {
        public MarketplaceItemSnapshot(
            string itemId,
            string ownerAccountId,
            MarketplaceItemCustody custody)
        {
            ItemId = itemId ?? string.Empty;
            OwnerAccountId = ownerAccountId ?? string.Empty;
            Custody = custody;
        }

        public string ItemId { get; }
        public string OwnerAccountId { get; }
        public MarketplaceItemCustody Custody { get; }
    }

    public sealed class MarketplaceListingSnapshot
    {
        public MarketplaceListingSnapshot(
            string listingId,
            string itemId,
            string sellerAccountId,
            string reservedBuyerAccountId,
            long listedPrice,
            long listedAtUnixMs,
            MarketplaceListingStatus status)
        {
            ListingId = listingId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            SellerAccountId = sellerAccountId ?? string.Empty;
            ReservedBuyerAccountId = reservedBuyerAccountId ?? string.Empty;
            ListedPrice = listedPrice;
            ListedAtUnixMs = listedAtUnixMs;
            Status = status;
        }

        public string ListingId { get; }
        public string ItemId { get; }
        public string SellerAccountId { get; }
        public string ReservedBuyerAccountId { get; }
        public long ListedPrice { get; }
        public long ListedAtUnixMs { get; }
        public MarketplaceListingStatus Status { get; }
    }

    public sealed class MarketplaceSettlementQuote
    {
        public MarketplaceSettlementQuote(long listedPrice, long taxDestroyed, long sellerProceeds)
        {
            ListedPrice = listedPrice;
            TaxDestroyed = taxDestroyed;
            SellerProceeds = sellerProceeds;
            BuyerDebit = listedPrice;
        }

        public long ListedPrice { get; }
        public long BuyerDebit { get; }
        public long TaxDestroyed { get; }
        public long SellerProceeds { get; }
    }

    public sealed class MarketplaceOperationReceipt
    {
        public MarketplaceOperationReceipt(
            string operationId,
            MarketplaceOperation operation,
            string requestFingerprint,
            string listingId,
            string itemId,
            string sellerAccountId,
            string buyerAccountId,
            long listedPrice,
            long taxDestroyed,
            long sellerProceeds,
            long resultingAuthorityRevision,
            string planHash,
            bool isSupported)
        {
            OperationId = operationId ?? string.Empty;
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            ListingId = listingId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            SellerAccountId = sellerAccountId ?? string.Empty;
            BuyerAccountId = buyerAccountId ?? string.Empty;
            ListedPrice = listedPrice;
            TaxDestroyed = taxDestroyed;
            SellerProceeds = sellerProceeds;
            ResultingAuthorityRevision = resultingAuthorityRevision;
            PlanHash = planHash ?? string.Empty;
            IsSupported = isSupported;
        }

        public string OperationId { get; }
        public MarketplaceOperation Operation { get; }
        public string RequestFingerprint { get; }
        public string ListingId { get; }
        public string ItemId { get; }
        public string SellerAccountId { get; }
        public string BuyerAccountId { get; }
        public long ListedPrice { get; }
        public long TaxDestroyed { get; }
        public long SellerProceeds { get; }
        public long ResultingAuthorityRevision { get; }
        public string PlanHash { get; }
        public bool IsSupported { get; }
    }

    public sealed class MarketplaceAuthoritySnapshot
    {
        public MarketplaceAuthoritySnapshot(
            MarketplaceAuthorityStatus status,
            long revision,
            MarketplaceCatalogBinding catalogBinding,
            IEnumerable<MarketplaceWalletSnapshot> wallets,
            IEnumerable<MarketplaceItemSnapshot> items,
            IEnumerable<MarketplaceListingSnapshot> listings,
            IEnumerable<MarketplaceOperationReceipt> receipts,
            bool isComplete)
        {
            Status = status;
            Revision = revision;
            CatalogBinding = catalogBinding;
            Wallets = wallets == null ? null : Array.AsReadOnly(wallets.ToArray());
            Items = items == null ? null : Array.AsReadOnly(items.ToArray());
            Listings = listings == null ? null : Array.AsReadOnly(listings.ToArray());
            Receipts = receipts == null ? null : Array.AsReadOnly(receipts.ToArray());
            IsComplete = isComplete;
        }

        public MarketplaceAuthorityStatus Status { get; }
        public long Revision { get; }
        public MarketplaceCatalogBinding CatalogBinding { get; }
        public IReadOnlyList<MarketplaceWalletSnapshot> Wallets { get; }
        public IReadOnlyList<MarketplaceItemSnapshot> Items { get; }
        public IReadOnlyList<MarketplaceListingSnapshot> Listings { get; }
        public IReadOnlyList<MarketplaceOperationReceipt> Receipts { get; }
        public bool IsComplete { get; }
    }

    public sealed class MarketplaceSettlementRequest
    {
        public MarketplaceSettlementRequest(
            string operationId,
            MarketplaceOperation operation,
            string actorAccountId,
            string listingId,
            string itemId,
            string sellerAccountId,
            string buyerAccountId,
            long listedPrice,
            MarketplaceWalletKind offeredWalletKind,
            long nowUnixMs,
            long expectedAuthorityRevision,
            MarketplaceCatalogBinding expectedCatalogBinding)
        {
            OperationId = operationId ?? string.Empty;
            Operation = operation;
            ActorAccountId = actorAccountId ?? string.Empty;
            ListingId = listingId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            SellerAccountId = sellerAccountId ?? string.Empty;
            BuyerAccountId = buyerAccountId ?? string.Empty;
            ListedPrice = listedPrice;
            OfferedWalletKind = offeredWalletKind;
            NowUnixMs = nowUnixMs;
            ExpectedAuthorityRevision = expectedAuthorityRevision;
            ExpectedCatalogBinding = expectedCatalogBinding;
        }

        public string OperationId { get; }
        public MarketplaceOperation Operation { get; }
        public string ActorAccountId { get; }
        public string ListingId { get; }
        public string ItemId { get; }
        public string SellerAccountId { get; }
        public string BuyerAccountId { get; }
        public long ListedPrice { get; }
        public MarketplaceWalletKind OfferedWalletKind { get; }
        public long NowUnixMs { get; }
        public long ExpectedAuthorityRevision { get; }
        public MarketplaceCatalogBinding ExpectedCatalogBinding { get; }
    }

    public sealed class MarketplaceDiagnostic
    {
        public MarketplaceDiagnostic(string code, string subjectId, string message)
        {
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class MarketplaceSettlementPlan
    {
        public MarketplaceSettlementPlan(
            MarketplaceOperation operation,
            string requestFingerprint,
            MarketplaceAuthoritySnapshot before,
            MarketplaceAuthoritySnapshot after,
            MarketplaceOperationReceipt receipt,
            MarketplaceSettlementQuote quote,
            string planHash)
        {
            Operation = operation;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            Before = before;
            After = after;
            Receipt = receipt;
            Quote = quote;
            PlanHash = planHash ?? string.Empty;
        }

        public MarketplaceOperation Operation { get; }
        public string RequestFingerprint { get; }
        public MarketplaceAuthoritySnapshot Before { get; }
        public MarketplaceAuthoritySnapshot After { get; }
        public MarketplaceOperationReceipt Receipt { get; }
        public MarketplaceSettlementQuote Quote { get; }
        public string PlanHash { get; }
    }

    public sealed class MarketplacePlanningResult
    {
        public MarketplacePlanningResult(
            MarketplacePlanningStatus status,
            MarketplaceSettlementPlan plan,
            MarketplaceOperationReceipt replayedReceipt,
            IEnumerable<MarketplaceDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ReplayedReceipt = replayedReceipt;
            Diagnostics = Array.AsReadOnly(
                (diagnostics ?? Array.Empty<MarketplaceDiagnostic>()).ToArray());
        }

        public MarketplacePlanningStatus Status { get; }
        public MarketplaceSettlementPlan Plan { get; }
        public MarketplaceOperationReceipt ReplayedReceipt { get; }
        public IReadOnlyList<MarketplaceDiagnostic> Diagnostics { get; }
    }
}
