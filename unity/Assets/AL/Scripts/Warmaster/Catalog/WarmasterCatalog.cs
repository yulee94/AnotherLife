using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Warmaster.Catalog
{
    public static class WarmasterCatalogContract
    {
        public const string CatalogId = "al_warmaster_catalog";
        public const int SchemaVersion = 1;
        public const string TrueWarmasterSetId = "prototype_true_warmaster";

        private static readonly ReadOnlyCollection<string> CanonicalPieceIds =
            Array.AsReadOnly(new[]
            {
                "warmaster_piece_01",
                "warmaster_piece_02",
                "warmaster_piece_03",
                "warmaster_piece_04",
                "warmaster_piece_05",
                "warmaster_piece_06",
                "warmaster_piece_07",
                "warmaster_piece_08",
                "warmaster_piece_09",
                "warmaster_piece_10"
            });

        public static IReadOnlyList<string> PieceIds => CanonicalPieceIds;
    }

    public enum WarmasterCatalogActivation
    {
        Inactive = 0,
        Active = 1
    }

    public enum WarmasterCatalogValidationStatus
    {
        Valid = 0,
        MissingCatalog = 1,
        Inactive = 2,
        UnsupportedVersion = 3,
        InvalidIdentity = 4,
        DuplicateId = 5,
        UnknownId = 6,
        IncompleteCatalog = 7,
        IncompleteEntry = 8,
        InvalidBalanceInput = 9,
        InvalidEquipmentInput = 10
    }

    public sealed class WarmasterCatalogInput
    {
        public string CatalogId;
        public int SchemaVersion;
        public string Revision;
        public WarmasterCatalogActivation Activation;
        public WarmasterSetInput Set;
        public List<WarmasterPieceInput> Pieces;
    }

    public sealed class WarmasterSetInput
    {
        public string Id;
        public int? RequiredPieceCount;
        public List<string> PieceIds;
        public string CompletionRewardId;
    }

    public sealed class WarmasterPieceInput
    {
        public string Id;
        public string SetId;
        public int? WarzoneCreditPrice;
        public int? RequiredOwnedPieceCount;
        public int? PurchaseExperienceAward;
        public string EquipmentSlotId;
        public List<WarmasterStatModifierInput> StatModifiers;
    }

    public sealed class WarmasterStatModifierInput
    {
        public string StatId;
        public int? Amount;
    }

    public sealed class WarmasterCatalogValidationResult
    {
        private WarmasterCatalogValidationResult(
            WarmasterCatalogValidationStatus status,
            WarmasterCatalogSnapshot snapshot)
        {
            Status = status;
            Snapshot = snapshot;
        }

        public WarmasterCatalogValidationStatus Status { get; }
        public WarmasterCatalogSnapshot Snapshot { get; }
        public bool CanPurchase => Status == WarmasterCatalogValidationStatus.Valid && Snapshot != null;
        public bool CanGrantCompletionReward => CanPurchase;

        internal static WarmasterCatalogValidationResult Reject(WarmasterCatalogValidationStatus status) =>
            new WarmasterCatalogValidationResult(status, null);

        internal static WarmasterCatalogValidationResult Accept(WarmasterCatalogSnapshot snapshot) =>
            new WarmasterCatalogValidationResult(WarmasterCatalogValidationStatus.Valid, snapshot);
    }

    public sealed class WarmasterCatalogSnapshot
    {
        internal WarmasterCatalogSnapshot(
            string revision,
            int requiredPieceCount,
            string completionRewardId,
            IList<WarmasterPieceSnapshot> pieces)
        {
            Revision = revision;
            RequiredPieceCount = requiredPieceCount;
            CompletionRewardId = completionRewardId;
            Pieces = new ReadOnlyCollection<WarmasterPieceSnapshot>(pieces);
        }

        public string CatalogId => WarmasterCatalogContract.CatalogId;
        public int SchemaVersion => WarmasterCatalogContract.SchemaVersion;
        public string Revision { get; }
        public string SetId => WarmasterCatalogContract.TrueWarmasterSetId;
        public int RequiredPieceCount { get; }
        public string CompletionRewardId { get; }
        public IReadOnlyList<WarmasterPieceSnapshot> Pieces { get; }
    }

    public sealed class WarmasterPieceSnapshot
    {
        internal WarmasterPieceSnapshot(
            string id,
            int warzoneCreditPrice,
            int requiredOwnedPieceCount,
            int purchaseExperienceAward,
            string equipmentSlotId,
            IList<WarmasterStatModifierSnapshot> statModifiers)
        {
            Id = id;
            WarzoneCreditPrice = warzoneCreditPrice;
            RequiredOwnedPieceCount = requiredOwnedPieceCount;
            PurchaseExperienceAward = purchaseExperienceAward;
            EquipmentSlotId = equipmentSlotId;
            StatModifiers = new ReadOnlyCollection<WarmasterStatModifierSnapshot>(statModifiers);
        }

        public string Id { get; }
        public string SetId => WarmasterCatalogContract.TrueWarmasterSetId;
        public int WarzoneCreditPrice { get; }
        public int RequiredOwnedPieceCount { get; }
        public int PurchaseExperienceAward { get; }
        public string EquipmentSlotId { get; }
        public IReadOnlyList<WarmasterStatModifierSnapshot> StatModifiers { get; }
    }

    public sealed class WarmasterStatModifierSnapshot
    {
        internal WarmasterStatModifierSnapshot(string statId, int amount)
        {
            StatId = statId;
            Amount = amount;
        }

        public string StatId { get; }
        public int Amount { get; }
    }

    public static class WarmasterCatalogValidator
    {
        public static WarmasterCatalogValidationResult Validate(WarmasterCatalogInput catalog)
        {
            if (catalog == null)
            {
                return Reject(WarmasterCatalogValidationStatus.MissingCatalog);
            }

            if (catalog.Activation != WarmasterCatalogActivation.Active)
            {
                return Reject(WarmasterCatalogValidationStatus.Inactive);
            }

            if (catalog.SchemaVersion != WarmasterCatalogContract.SchemaVersion)
            {
                return Reject(WarmasterCatalogValidationStatus.UnsupportedVersion);
            }

            if (!EqualsOrdinal(catalog.CatalogId, WarmasterCatalogContract.CatalogId) ||
                string.IsNullOrWhiteSpace(catalog.Revision) ||
                catalog.Set == null ||
                !EqualsOrdinal(catalog.Set.Id, WarmasterCatalogContract.TrueWarmasterSetId))
            {
                return Reject(WarmasterCatalogValidationStatus.InvalidIdentity);
            }

            if (catalog.Pieces == null || catalog.Set.PieceIds == null ||
                catalog.Pieces.Count != WarmasterCatalogContract.PieceIds.Count ||
                catalog.Set.PieceIds.Count != WarmasterCatalogContract.PieceIds.Count)
            {
                return Reject(WarmasterCatalogValidationStatus.IncompleteCatalog);
            }

            if (!catalog.Set.RequiredPieceCount.HasValue ||
                catalog.Set.RequiredPieceCount.Value <= 0 ||
                catalog.Set.RequiredPieceCount.Value > catalog.Pieces.Count)
            {
                return Reject(WarmasterCatalogValidationStatus.InvalidBalanceInput);
            }

            if (string.IsNullOrWhiteSpace(catalog.Set.CompletionRewardId))
            {
                return Reject(WarmasterCatalogValidationStatus.IncompleteEntry);
            }

            var memberIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string memberId in catalog.Set.PieceIds)
            {
                if (string.IsNullOrWhiteSpace(memberId))
                {
                    return Reject(WarmasterCatalogValidationStatus.IncompleteEntry);
                }

                if (!memberIds.Add(memberId))
                {
                    return Reject(WarmasterCatalogValidationStatus.DuplicateId);
                }

                if (!IsCanonicalPieceId(memberId))
                {
                    return Reject(WarmasterCatalogValidationStatus.UnknownId);
                }
            }

            var pieceIds = new HashSet<string>(StringComparer.Ordinal);
            var slots = new HashSet<string>(StringComparer.Ordinal);
            var snapshots = new List<WarmasterPieceSnapshot>(catalog.Pieces.Count);
            foreach (WarmasterPieceInput piece in catalog.Pieces)
            {
                WarmasterCatalogValidationResult invalid = ValidatePiece(
                    piece,
                    catalog.Set.RequiredPieceCount.Value,
                    memberIds,
                    pieceIds,
                    slots,
                    out WarmasterPieceSnapshot snapshot);
                if (invalid != null)
                {
                    return invalid;
                }

                snapshots.Add(snapshot);
            }

            if (pieceIds.Count != WarmasterCatalogContract.PieceIds.Count)
            {
                return Reject(WarmasterCatalogValidationStatus.IncompleteCatalog);
            }

            snapshots.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return WarmasterCatalogValidationResult.Accept(
                new WarmasterCatalogSnapshot(
                    catalog.Revision,
                    catalog.Set.RequiredPieceCount.Value,
                    catalog.Set.CompletionRewardId,
                    snapshots));
        }

        private static WarmasterCatalogValidationResult ValidatePiece(
            WarmasterPieceInput piece,
            int completionThreshold,
            HashSet<string> memberIds,
            HashSet<string> pieceIds,
            HashSet<string> slots,
            out WarmasterPieceSnapshot snapshot)
        {
            snapshot = null;
            if (piece == null || string.IsNullOrWhiteSpace(piece.Id) ||
                string.IsNullOrWhiteSpace(piece.SetId) ||
                string.IsNullOrWhiteSpace(piece.EquipmentSlotId) ||
                piece.StatModifiers == null || piece.StatModifiers.Count == 0 ||
                !piece.WarzoneCreditPrice.HasValue ||
                !piece.RequiredOwnedPieceCount.HasValue ||
                !piece.PurchaseExperienceAward.HasValue)
            {
                return Reject(WarmasterCatalogValidationStatus.IncompleteEntry);
            }

            if (!pieceIds.Add(piece.Id))
            {
                return Reject(WarmasterCatalogValidationStatus.DuplicateId);
            }

            if (!IsCanonicalPieceId(piece.Id) || !memberIds.Contains(piece.Id))
            {
                return Reject(WarmasterCatalogValidationStatus.UnknownId);
            }

            if (!EqualsOrdinal(piece.SetId, WarmasterCatalogContract.TrueWarmasterSetId))
            {
                return Reject(WarmasterCatalogValidationStatus.InvalidIdentity);
            }

            if (piece.WarzoneCreditPrice.Value <= 0 ||
                piece.RequiredOwnedPieceCount.Value < 0 ||
                piece.RequiredOwnedPieceCount.Value >= completionThreshold ||
                piece.PurchaseExperienceAward.Value < 0)
            {
                return Reject(WarmasterCatalogValidationStatus.InvalidBalanceInput);
            }

            if (!slots.Add(piece.EquipmentSlotId))
            {
                return Reject(WarmasterCatalogValidationStatus.InvalidEquipmentInput);
            }

            var modifiers = new List<WarmasterStatModifierSnapshot>(piece.StatModifiers.Count);
            var statIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WarmasterStatModifierInput modifier in piece.StatModifiers)
            {
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.StatId) || !modifier.Amount.HasValue)
                {
                    return Reject(WarmasterCatalogValidationStatus.IncompleteEntry);
                }

                if (!statIds.Add(modifier.StatId) || modifier.Amount.Value == 0)
                {
                    return Reject(WarmasterCatalogValidationStatus.InvalidEquipmentInput);
                }

                modifiers.Add(new WarmasterStatModifierSnapshot(modifier.StatId, modifier.Amount.Value));
            }

            snapshot = new WarmasterPieceSnapshot(
                piece.Id,
                piece.WarzoneCreditPrice.Value,
                piece.RequiredOwnedPieceCount.Value,
                piece.PurchaseExperienceAward.Value,
                piece.EquipmentSlotId,
                modifiers);
            return null;
        }

        private static bool IsCanonicalPieceId(string id)
        {
            foreach (string canonicalId in WarmasterCatalogContract.PieceIds)
            {
                if (EqualsOrdinal(id, canonicalId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EqualsOrdinal(string left, string right) =>
            string.Equals(left, right, StringComparison.Ordinal);

        private static WarmasterCatalogValidationResult Reject(WarmasterCatalogValidationStatus status) =>
            WarmasterCatalogValidationResult.Reject(status);
    }
}
