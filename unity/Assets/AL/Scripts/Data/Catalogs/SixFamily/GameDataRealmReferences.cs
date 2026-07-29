using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    public sealed class GameDataRealmReference
    {
        internal GameDataRealmReference(
            int order,
            string stableId,
            string legacyRealmName,
            int legacyRealmValue,
            string nameReference,
            string descriptionReference,
            string innerRealmId,
            string mainGateId,
            string outerWarzoneId,
            string rareResourceStableId,
            string assetReference,
            string assetGuid,
            string assetSha256)
        {
            Order = order;
            StableId = stableId;
            LegacyRealmName = legacyRealmName;
            LegacyRealmValue = legacyRealmValue;
            NameReference = nameReference;
            DescriptionReference = descriptionReference;
            InnerRealmId = innerRealmId;
            MainGateId = mainGateId;
            OuterWarzoneId = outerWarzoneId;
            RareResourceStableId = rareResourceStableId;
            AssetReference = assetReference;
            AssetGuid = assetGuid;
            AssetSha256 = assetSha256;
        }

        public int Order { get; }
        public string StableId { get; }
        public string LegacyRealmName { get; }
        public int LegacyRealmValue { get; }
        public string NameReference { get; }
        public string DescriptionReference { get; }
        public string InnerRealmId { get; }
        public string MainGateId { get; }
        public string OuterWarzoneId { get; }
        public string RareResourceStableId { get; }
        public string AssetReference { get; }
        public string AssetGuid { get; }
        public string AssetSha256 { get; }
    }

    /// <summary>
    /// Exact, non-published realm identity and reference tuples accepted from Phase C3A.
    /// Order is authored order and every resolver is ordinal, rejecting, and non-normalizing.
    /// </summary>
    public static class GameDataRealmReferences
    {
        public const int Version = 1;
        public const int RealmCount = 4;

        private static readonly IReadOnlyList<GameDataRealmReference> entries;
        private static readonly IReadOnlyList<string> stableIds;
        private static readonly IReadOnlyList<string> innerRealmIds;
        private static readonly IReadOnlyList<string> mainGateIds;
        private static readonly IReadOnlyList<string> outerWarzoneIds;
        private static readonly IReadOnlyList<string> assetReferences;
        private static readonly Dictionary<string, GameDataRealmReference> entriesByStableId;

        static GameDataRealmReferences()
        {
            var mutableEntries = new[]
            {
                Realm(
                    0,
                    "crownlands",
                    "Crownlands",
                    3,
                    "realm.crownlands.name",
                    "realm.crownlands.description",
                    "inner_crownlands",
                    "gate_crownlands_meridian",
                    "warzone_crownlands",
                    "royal_sigil",
                    "Assets/AL/Art/Heraldry/RuntimeExports/" +
                    "S_ArcaneAxis_Crownlands_Flat_256_v001.png",
                    "ba4dfcc7b514049f79f6ec3424193b46",
                    "f5c7e351ec930aac69f6df02d03034bc38c465ed8dfa787dd4feba044f33f82b"),
                Realm(
                    1,
                    "stonehold",
                    "Stonehold",
                    1,
                    "realm.stonehold.name",
                    "realm.stonehold.description",
                    "inner_stonehold",
                    "gate_stonehold_faultline",
                    "warzone_stonehold",
                    "deep_ore",
                    "Assets/AL/Art/Heraldry/RuntimeExports/" +
                    "S_ArcaneAxis_Stonehold_Flat_256_v001.png",
                    "94d8d9e2cf04a4b769c213a13c164b8e",
                    "53d220dc8b938d212963286133ca39e1968fa1421126559dd56bdfde9c437946"),
                Realm(
                    2,
                    "eldergrove",
                    "Eldergrove",
                    2,
                    "realm.eldergrove.name",
                    "realm.eldergrove.description",
                    "inner_eldergrove",
                    "gate_eldergrove_greenveil",
                    "warzone_eldergrove",
                    "world_sap",
                    "Assets/AL/Art/Heraldry/RuntimeExports/" +
                    "S_ArcaneAxis_Eldergrove_Flat_256_v001.png",
                    "53001b27fd9d14914984211765be4391",
                    "1d45fc8fba82ebb3fdc1c4f819026ea8e45b11c248378371c7b2b6923c6e0cac"),
                Realm(
                    3,
                    "umbral",
                    "Umbral",
                    4,
                    "realm.umbral.name",
                    "realm.umbral.description",
                    "inner_umbral",
                    "gate_umbral_ashvein",
                    "warzone_umbral",
                    "dark_crystal",
                    "Assets/AL/Art/Heraldry/RuntimeExports/" +
                    "S_ArcaneAxis_Umbral_Flat_256_v001.png",
                    "a426041e03b0742999a34b8b5e198406",
                    "a9daefa3ea6445ba2db680dad92a456db75becebec8848c678b29d5ea2c85aaa")
            };
            if (mutableEntries.Length != RealmCount)
            {
                throw new InvalidOperationException(
                    "The realm reference authority must contain exactly four identities.");
            }

            entriesByStableId =
                new Dictionary<string, GameDataRealmReference>(StringComparer.Ordinal);
            var legacyNames = new HashSet<string>(StringComparer.Ordinal);
            var legacyValues = new HashSet<int>();
            var nameReferences = new HashSet<string>(StringComparer.Ordinal);
            var descriptionReferences = new HashSet<string>(StringComparer.Ordinal);
            var uniqueInnerRealmIds = new HashSet<string>(StringComparer.Ordinal);
            var uniqueMainGateIds = new HashSet<string>(StringComparer.Ordinal);
            var uniqueOuterWarzoneIds = new HashSet<string>(StringComparer.Ordinal);
            var rareResourceIds = new HashSet<string>(StringComparer.Ordinal);
            var uniqueAssetReferences = new HashSet<string>(StringComparer.Ordinal);
            var assetGuids = new HashSet<string>(StringComparer.Ordinal);
            var assetHashes = new HashSet<string>(StringComparer.Ordinal);
            var mutableStableIds = new string[mutableEntries.Length];
            var mutableInnerRealmIds = new string[mutableEntries.Length];
            var mutableMainGateIds = new string[mutableEntries.Length];
            var mutableOuterWarzoneIds = new string[mutableEntries.Length];
            var mutableAssetReferences = new string[mutableEntries.Length];

            for (var index = 0; index < mutableEntries.Length; index++)
            {
                var entry = mutableEntries[index];
                GameDataWalletResourceReference resource;
                if (entry.Order != index ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.StableId) ||
                    string.IsNullOrWhiteSpace(entry.LegacyRealmName) ||
                    entry.LegacyRealmValue <= 0 ||
                    !IsCanonicalContentReference(entry.NameReference) ||
                    !IsCanonicalContentReference(entry.DescriptionReference) ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.InnerRealmId) ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.MainGateId) ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.OuterWarzoneId) ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(
                        entry.RareResourceStableId) ||
                    !IsCanonicalAssetReference(entry.AssetReference) ||
                    !IsLowerHex(entry.AssetGuid, 32) ||
                    !IsLowerHex(entry.AssetSha256, 64) ||
                    !GameDataWalletResourceReferences.TryGetByStableId(
                        entry.RareResourceStableId,
                        out resource) ||
                    resource.Classification !=
                    GameDataWalletResourceClassification.OptionalRare ||
                    !entriesByStableId.TryAdd(entry.StableId, entry) ||
                    !legacyNames.Add(entry.LegacyRealmName) ||
                    !legacyValues.Add(entry.LegacyRealmValue) ||
                    !nameReferences.Add(entry.NameReference) ||
                    !descriptionReferences.Add(entry.DescriptionReference) ||
                    !uniqueInnerRealmIds.Add(entry.InnerRealmId) ||
                    !uniqueMainGateIds.Add(entry.MainGateId) ||
                    !uniqueOuterWarzoneIds.Add(entry.OuterWarzoneId) ||
                    !rareResourceIds.Add(entry.RareResourceStableId) ||
                    !uniqueAssetReferences.Add(entry.AssetReference) ||
                    !assetGuids.Add(entry.AssetGuid) ||
                    !assetHashes.Add(entry.AssetSha256))
                {
                    throw new InvalidOperationException(
                        "The realm reference authority contains invalid or duplicate identity.");
                }

                mutableStableIds[index] = entry.StableId;
                mutableInnerRealmIds[index] = entry.InnerRealmId;
                mutableMainGateIds[index] = entry.MainGateId;
                mutableOuterWarzoneIds[index] = entry.OuterWarzoneId;
                mutableAssetReferences[index] = entry.AssetReference;
            }

            entries = ImmutableCollections.Freeze(mutableEntries);
            stableIds = ImmutableCollections.Freeze(mutableStableIds);
            innerRealmIds = ImmutableCollections.Freeze(mutableInnerRealmIds);
            mainGateIds = ImmutableCollections.Freeze(mutableMainGateIds);
            outerWarzoneIds = ImmutableCollections.Freeze(mutableOuterWarzoneIds);
            assetReferences = ImmutableCollections.Freeze(mutableAssetReferences);
        }

        public static IReadOnlyList<GameDataRealmReference> Entries => entries;
        public static IReadOnlyList<string> StableIds => stableIds;
        public static IReadOnlyList<string> InnerRealmIds => innerRealmIds;
        public static IReadOnlyList<string> MainGateIds => mainGateIds;
        public static IReadOnlyList<string> OuterWarzoneIds => outerWarzoneIds;
        public static IReadOnlyList<string> AssetReferences => assetReferences;

        public static bool TryGetByStableId(
            string stableId,
            out GameDataRealmReference reference)
        {
            return entriesByStableId.TryGetValue(stableId ?? string.Empty, out reference);
        }

        public static bool TryGetByLegacyIdentity(
            string legacyRealmName,
            int legacyRealmValue,
            out GameDataRealmReference reference)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var candidate = entries[index];
                if (candidate.LegacyRealmValue == legacyRealmValue &&
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

        public static bool IsApprovedRareResourceRelation(
            string realmStableId,
            string legacyRealmName,
            int legacyRealmValue,
            string resourceStableId)
        {
            GameDataRealmReference reference;
            return TryGetByStableId(realmStableId, out reference) &&
                   reference.LegacyRealmValue == legacyRealmValue &&
                   string.Equals(
                       reference.LegacyRealmName,
                       legacyRealmName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.RareResourceStableId,
                       resourceStableId,
                       StringComparison.Ordinal);
        }

        public static bool IsApprovedWorldAssetRelation(
            string realmStableId,
            string legacyRealmName,
            int legacyRealmValue,
            string nameReference,
            string descriptionReference,
            string innerRealmId,
            string mainGateId,
            string outerWarzoneId,
            string assetReference)
        {
            GameDataRealmReference reference;
            return TryGetByStableId(realmStableId, out reference) &&
                   reference.LegacyRealmValue == legacyRealmValue &&
                   string.Equals(
                       reference.LegacyRealmName,
                       legacyRealmName,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.NameReference,
                       nameReference,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.DescriptionReference,
                       descriptionReference,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.InnerRealmId,
                       innerRealmId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.MainGateId,
                       mainGateId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.OuterWarzoneId,
                       outerWarzoneId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.AssetReference,
                       assetReference,
                       StringComparison.Ordinal);
        }

        private static GameDataRealmReference Realm(
            int order,
            string stableId,
            string legacyRealmName,
            int legacyRealmValue,
            string nameReference,
            string descriptionReference,
            string innerRealmId,
            string mainGateId,
            string outerWarzoneId,
            string rareResourceStableId,
            string assetReference,
            string assetGuid,
            string assetSha256)
        {
            return new GameDataRealmReference(
                order,
                stableId,
                legacyRealmName,
                legacyRealmValue,
                nameReference,
                descriptionReference,
                innerRealmId,
                mainGateId,
                outerWarzoneId,
                rareResourceStableId,
                assetReference,
                assetGuid,
                assetSha256);
        }

        private static bool IsCanonicalContentReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var segments = value.Split('.');
            if (segments.Length < 2)
            {
                return false;
            }

            for (var index = 0; index < segments.Length; index++)
            {
                if (!GameDataCatalogIdentifiers.IsCanonicalStableId(segments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCanonicalAssetReference(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.StartsWith("Assets/", StringComparison.Ordinal) &&
                   value.EndsWith(".png", StringComparison.Ordinal) &&
                   value.IndexOf('\\') < 0 &&
                   value.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private static bool IsLowerHex(string value, int expectedLength)
        {
            if (value == null || value.Length != expectedLength)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
