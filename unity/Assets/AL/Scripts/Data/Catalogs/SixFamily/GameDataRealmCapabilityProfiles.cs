using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    public enum GameDataRealmCapabilityCondition
    {
        Constant = 0,
        OwnArmyHasSiege = 1,
        OwnArmyHasRanged = 2,
        OwnSideIsAttackerOrBattleIsPvp = 3
    }

    public sealed class GameDataRealmCapabilityProfile
    {
        internal GameDataRealmCapabilityProfile(
            int order,
            string stableId,
            string realmStableId,
            GameDataRealmCapabilityCondition condition,
            string conditionToken,
            int matchedMultiplierMillionths,
            int defaultMultiplierMillionths)
        {
            Order = order;
            StableId = stableId;
            RealmStableId = realmStableId;
            Condition = condition;
            ConditionToken = conditionToken;
            MatchedMultiplierMillionths = matchedMultiplierMillionths;
            DefaultMultiplierMillionths = defaultMultiplierMillionths;
        }

        public int Order { get; }
        public string StableId { get; }
        public string RealmStableId { get; }
        public GameDataRealmCapabilityCondition Condition { get; }
        public string ConditionToken { get; }
        public int MatchedMultiplierMillionths { get; }
        public int DefaultMultiplierMillionths { get; }

        public int EvaluateMultiplierMillionths(
            bool ownArmyHasSiege,
            bool ownArmyHasRanged,
            bool ownSideIsAttacker,
            bool battleIsPvp)
        {
            bool matched;
            switch (Condition)
            {
                case GameDataRealmCapabilityCondition.Constant:
                    matched = true;
                    break;
                case GameDataRealmCapabilityCondition.OwnArmyHasSiege:
                    matched = ownArmyHasSiege;
                    break;
                case GameDataRealmCapabilityCondition.OwnArmyHasRanged:
                    matched = ownArmyHasRanged;
                    break;
                case GameDataRealmCapabilityCondition.OwnSideIsAttackerOrBattleIsPvp:
                    matched = ownSideIsAttacker || battleIsPvp;
                    break;
                default:
                    throw new InvalidOperationException(
                        "The realm capability profile contains an unsupported condition.");
            }

            return matched ? MatchedMultiplierMillionths : DefaultMultiplierMillionths;
        }
    }

    /// <summary>
    /// Exact, non-published realm battle profiles accepted from Phase C3F.
    /// Values are fixed-point millionths and resolution is ordinal and non-normalizing.
    /// </summary>
    public static class GameDataRealmCapabilityProfiles
    {
        public const int Version = 1;
        public const int ProfileCount = 4;
        public const int NeutralMultiplierMillionths = 1000000;

        private static readonly IReadOnlyList<GameDataRealmCapabilityProfile> entries;
        private static readonly IReadOnlyList<string> stableIds;
        private static readonly Dictionary<string, GameDataRealmCapabilityProfile>
            entriesByStableId;
        private static readonly Dictionary<string, GameDataRealmCapabilityProfile>
            entriesByRealmStableId;

        static GameDataRealmCapabilityProfiles()
        {
            var mutableEntries = new[]
            {
                Profile(
                    0,
                    "battle_realm_crownlands",
                    "crownlands",
                    GameDataRealmCapabilityCondition.Constant,
                    1060000,
                    1060000),
                Profile(
                    1,
                    "battle_realm_stonehold",
                    "stonehold",
                    GameDataRealmCapabilityCondition.OwnArmyHasSiege,
                    1100000,
                    1060000),
                Profile(
                    2,
                    "battle_realm_eldergrove",
                    "eldergrove",
                    GameDataRealmCapabilityCondition.OwnArmyHasRanged,
                    1100000,
                    1050000),
                Profile(
                    3,
                    "battle_realm_umbral",
                    "umbral",
                    GameDataRealmCapabilityCondition.OwnSideIsAttackerOrBattleIsPvp,
                    1090000,
                    1040000)
            };

            if (mutableEntries.Length != ProfileCount ||
                GameDataRealmReferences.Entries.Count != ProfileCount)
            {
                throw new InvalidOperationException(
                    "Realm capability authority must contain exactly one profile per realm.");
            }

            entriesByStableId =
                new Dictionary<string, GameDataRealmCapabilityProfile>(
                    StringComparer.Ordinal);
            entriesByRealmStableId =
                new Dictionary<string, GameDataRealmCapabilityProfile>(
                    StringComparer.Ordinal);
            var mutableStableIds = new string[mutableEntries.Length];

            for (var index = 0; index < mutableEntries.Length; index++)
            {
                var entry = mutableEntries[index];
                string expectedConditionToken;
                GameDataRealmReference realmReference;
                if (entry.Order != index ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.StableId) ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.RealmStableId) ||
                    !TryGetConditionToken(entry.Condition, out expectedConditionToken) ||
                    !string.Equals(
                        entry.ConditionToken,
                        expectedConditionToken,
                        StringComparison.Ordinal) ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.ConditionToken) ||
                    entry.MatchedMultiplierMillionths <= 0 ||
                    entry.DefaultMultiplierMillionths <= 0 ||
                    !GameDataRealmReferences.TryGetByStableId(
                        entry.RealmStableId,
                        out realmReference) ||
                    realmReference.Order != index ||
                    !entriesByStableId.TryAdd(entry.StableId, entry) ||
                    !entriesByRealmStableId.TryAdd(entry.RealmStableId, entry))
                {
                    throw new InvalidOperationException(
                        "Realm capability authority contains invalid or duplicate identity.");
                }

                mutableStableIds[index] = entry.StableId;
            }

            entries = ImmutableCollections.Freeze(mutableEntries);
            stableIds = ImmutableCollections.Freeze(mutableStableIds);
        }

        public static IReadOnlyList<GameDataRealmCapabilityProfile> Entries => entries;
        public static IReadOnlyList<string> StableIds => stableIds;

        public static bool TryGetByStableId(
            string stableId,
            out GameDataRealmCapabilityProfile profile)
        {
            return entriesByStableId.TryGetValue(stableId ?? string.Empty, out profile);
        }

        public static bool TryGetByRealmStableId(
            string realmStableId,
            out GameDataRealmCapabilityProfile profile)
        {
            return entriesByRealmStableId.TryGetValue(
                realmStableId ?? string.Empty,
                out profile);
        }

        public static bool IsApprovedRealmRelation(
            string realmStableId,
            IReadOnlyList<string> profileStableIds)
        {
            GameDataRealmCapabilityProfile profile;
            return profileStableIds != null &&
                   profileStableIds.Count == 1 &&
                   TryGetByRealmStableId(realmStableId, out profile) &&
                   string.Equals(
                       profile.StableId,
                       profileStableIds[0],
                       StringComparison.Ordinal);
        }

        public static bool TryGetConditionToken(
            GameDataRealmCapabilityCondition condition,
            out string token)
        {
            switch (condition)
            {
                case GameDataRealmCapabilityCondition.Constant:
                    token = "constant";
                    return true;
                case GameDataRealmCapabilityCondition.OwnArmyHasSiege:
                    token = "own_army_has_siege";
                    return true;
                case GameDataRealmCapabilityCondition.OwnArmyHasRanged:
                    token = "own_army_has_ranged";
                    return true;
                case GameDataRealmCapabilityCondition.OwnSideIsAttackerOrBattleIsPvp:
                    token = "own_side_is_attacker_or_battle_is_pvp";
                    return true;
                default:
                    token = null;
                    return false;
            }
        }

        private static GameDataRealmCapabilityProfile Profile(
            int order,
            string stableId,
            string realmStableId,
            GameDataRealmCapabilityCondition condition,
            int matchedMultiplierMillionths,
            int defaultMultiplierMillionths)
        {
            string conditionToken;
            if (!TryGetConditionToken(condition, out conditionToken))
            {
                throw new ArgumentOutOfRangeException(nameof(condition));
            }

            return new GameDataRealmCapabilityProfile(
                order,
                stableId,
                realmStableId,
                condition,
                conditionToken,
                matchedMultiplierMillionths,
                defaultMultiplierMillionths);
        }
    }
}
