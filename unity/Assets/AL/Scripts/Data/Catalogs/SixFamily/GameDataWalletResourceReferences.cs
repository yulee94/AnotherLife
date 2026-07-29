using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    public enum GameDataWalletResourceClassification
    {
        Core = 0,
        OptionalRare = 1
    }

    public sealed class GameDataWalletResourceReference
    {
        internal GameDataWalletResourceReference(
            int walletIndex,
            string stableId,
            string legacyEnumName,
            int legacyEnumValue,
            GameDataWalletResourceClassification classification)
        {
            WalletIndex = walletIndex;
            StableId = stableId;
            LegacyEnumName = legacyEnumName;
            LegacyEnumValue = legacyEnumValue;
            Classification = classification;
        }

        public int WalletIndex { get; }
        public string StableId { get; }
        public string LegacyEnumName { get; }
        public int LegacyEnumValue { get; }
        public GameDataWalletResourceClassification Classification { get; }
    }

    public sealed class GameDataRealmRareResourceReference
    {
        internal GameDataRealmRareResourceReference(
            string realmStableId,
            string legacyRealmName,
            int legacyRealmValue,
            string resourceStableId)
        {
            RealmStableId = realmStableId;
            LegacyRealmName = legacyRealmName;
            LegacyRealmValue = legacyRealmValue;
            ResourceStableId = resourceStableId;
        }

        public string RealmStableId { get; }
        public string LegacyRealmName { get; }
        public int LegacyRealmValue { get; }
        public string ResourceStableId { get; }
    }

    /// <summary>
    /// Exact, balance-neutral resource identities consumed by the six-family realm schema.
    /// Record order is the persisted wallet order; resolution is ordinal and never normalized.
    /// </summary>
    public static class GameDataWalletResourceReferences
    {
        public const int Version = 1;
        public const int CoreResourceCount = 6;

        private static readonly IReadOnlyList<GameDataWalletResourceReference> entries;
        private static readonly IReadOnlyList<string> stableIds;
        private static readonly IReadOnlyList<GameDataRealmRareResourceReference> realmRareResources;
        private static readonly Dictionary<string, GameDataWalletResourceReference> entriesByStableId;
        private static readonly Dictionary<string, GameDataWalletResourceReference> entriesByLegacyName;
        private static readonly Dictionary<int, GameDataWalletResourceReference> entriesByLegacyValue;

        static GameDataWalletResourceReferences()
        {
            var mutableEntries = new[]
            {
                Resource(0, "food", "Food", GameDataWalletResourceClassification.Core),
                Resource(1, "wood", "Wood", GameDataWalletResourceClassification.Core),
                Resource(2, "stone", "Stone", GameDataWalletResourceClassification.Core),
                Resource(3, "gold", "Gold", GameDataWalletResourceClassification.Core),
                Resource(4, "mana_stone", "ManaStone", GameDataWalletResourceClassification.Core),
                Resource(5, "ore", "Ore", GameDataWalletResourceClassification.Core),
                Resource(6, "deep_ore", "DeepOre", GameDataWalletResourceClassification.OptionalRare),
                Resource(7, "world_sap", "WorldSap", GameDataWalletResourceClassification.OptionalRare),
                Resource(8, "royal_sigil", "RoyalSigil", GameDataWalletResourceClassification.OptionalRare),
                Resource(9, "dark_crystal", "DarkCrystal", GameDataWalletResourceClassification.OptionalRare)
            };

            entriesByStableId =
                new Dictionary<string, GameDataWalletResourceReference>(StringComparer.Ordinal);
            entriesByLegacyName =
                new Dictionary<string, GameDataWalletResourceReference>(StringComparer.Ordinal);
            entriesByLegacyValue =
                new Dictionary<int, GameDataWalletResourceReference>();
            var mutableStableIds = new string[mutableEntries.Length];

            for (var index = 0; index < mutableEntries.Length; index++)
            {
                var entry = mutableEntries[index];
                var expectedClassification =
                    index < CoreResourceCount
                        ? GameDataWalletResourceClassification.Core
                        : GameDataWalletResourceClassification.OptionalRare;
                if (entry.WalletIndex != index ||
                    entry.LegacyEnumValue != index ||
                    entry.Classification != expectedClassification ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.StableId) ||
                    string.IsNullOrWhiteSpace(entry.LegacyEnumName) ||
                    !entriesByStableId.TryAdd(entry.StableId, entry) ||
                    !entriesByLegacyName.TryAdd(entry.LegacyEnumName, entry) ||
                    !entriesByLegacyValue.TryAdd(entry.LegacyEnumValue, entry))
                {
                    throw new InvalidOperationException(
                        "The wallet resource reference authority contains invalid or duplicate identity.");
                }

                mutableStableIds[index] = entry.StableId;
            }

            var mutableRealmRareResources = new[]
            {
                RealmRare("crownlands", "Crownlands", 3, "royal_sigil"),
                RealmRare("stonehold", "Stonehold", 1, "deep_ore"),
                RealmRare("eldergrove", "Eldergrove", 2, "world_sap"),
                RealmRare("umbral", "Umbral", 4, "dark_crystal")
            };
            var realmIds = new HashSet<string>(StringComparer.Ordinal);
            var realmNames = new HashSet<string>(StringComparer.Ordinal);
            var realmValues = new HashSet<int>();
            var rareResourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < mutableRealmRareResources.Length; index++)
            {
                var relation = mutableRealmRareResources[index];
                GameDataWalletResourceReference resource;
                if (!GameDataCatalogIdentifiers.IsCanonicalStableId(relation.RealmStableId) ||
                    string.IsNullOrWhiteSpace(relation.LegacyRealmName) ||
                    relation.LegacyRealmValue <= 0 ||
                    !entriesByStableId.TryGetValue(relation.ResourceStableId, out resource) ||
                    resource.Classification != GameDataWalletResourceClassification.OptionalRare ||
                    !realmIds.Add(relation.RealmStableId) ||
                    !realmNames.Add(relation.LegacyRealmName) ||
                    !realmValues.Add(relation.LegacyRealmValue) ||
                    !rareResourceIds.Add(relation.ResourceStableId))
                {
                    throw new InvalidOperationException(
                        "The realm rare-resource reference authority contains invalid or duplicate identity.");
                }
            }

            entries = ImmutableCollections.Freeze(mutableEntries);
            stableIds = ImmutableCollections.Freeze(mutableStableIds);
            realmRareResources = ImmutableCollections.Freeze(mutableRealmRareResources);
        }

        public static IReadOnlyList<GameDataWalletResourceReference> Entries => entries;
        public static IReadOnlyList<string> StableIds => stableIds;
        public static IReadOnlyList<GameDataRealmRareResourceReference> RealmRareResources =>
            realmRareResources;

        public static bool TryGetByStableId(
            string stableId,
            out GameDataWalletResourceReference reference)
        {
            return entriesByStableId.TryGetValue(stableId ?? string.Empty, out reference);
        }

        public static bool TryGetByLegacyName(
            string legacyEnumName,
            out GameDataWalletResourceReference reference)
        {
            return entriesByLegacyName.TryGetValue(legacyEnumName ?? string.Empty, out reference);
        }

        public static bool TryGetByLegacyValue(
            int legacyEnumValue,
            out GameDataWalletResourceReference reference)
        {
            return entriesByLegacyValue.TryGetValue(legacyEnumValue, out reference);
        }

        public static bool TryGetRealmRareResource(
            string realmStableId,
            string legacyRealmName,
            int legacyRealmValue,
            out GameDataRealmRareResourceReference reference)
        {
            for (var index = 0; index < realmRareResources.Count; index++)
            {
                var candidate = realmRareResources[index];
                if (string.Equals(
                        candidate.RealmStableId,
                        realmStableId,
                        StringComparison.Ordinal) &&
                    candidate.LegacyRealmValue == legacyRealmValue &&
                    string.Equals(
                        candidate.LegacyRealmName,
                        legacyRealmName,
                        StringComparison.Ordinal))
                {
                    reference = candidate;
                    return true;
                }
            }

            reference = null;
            return false;
        }

        public static bool IsApprovedRealmRareResourceRelation(
            string realmStableId,
            string legacyRealmName,
            int legacyRealmValue,
            string resourceStableId)
        {
            GameDataRealmRareResourceReference relation;
            return TryGetRealmRareResource(
                       realmStableId,
                       legacyRealmName,
                       legacyRealmValue,
                       out relation) &&
                   string.Equals(
                       relation.ResourceStableId,
                       resourceStableId,
                       StringComparison.Ordinal);
        }

        private static GameDataWalletResourceReference Resource(
            int value,
            string stableId,
            string legacyEnumName,
            GameDataWalletResourceClassification classification)
        {
            return new GameDataWalletResourceReference(
                value,
                stableId,
                legacyEnumName,
                value,
                classification);
        }

        private static GameDataRealmRareResourceReference RealmRare(
            string realmStableId,
            string legacyRealmName,
            int legacyRealmValue,
            string resourceStableId)
        {
            return new GameDataRealmRareResourceReference(
                realmStableId,
                legacyRealmName,
                legacyRealmValue,
                resourceStableId);
        }
    }
}
