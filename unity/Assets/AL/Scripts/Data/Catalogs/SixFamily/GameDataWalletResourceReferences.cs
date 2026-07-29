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

            entries = ImmutableCollections.Freeze(mutableEntries);
            stableIds = ImmutableCollections.Freeze(mutableStableIds);
        }

        public static IReadOnlyList<GameDataWalletResourceReference> Entries => entries;
        public static IReadOnlyList<string> StableIds => stableIds;

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

    }
}
