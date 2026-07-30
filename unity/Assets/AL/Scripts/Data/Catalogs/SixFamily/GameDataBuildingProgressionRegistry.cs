using System;
using System.Collections.Generic;

namespace AL.Data.Catalogs
{
    public enum GameDataBuildingIdentityResolutionStatus
    {
        Unknown = 0,
        ExactCanonical = 1,
        ResolvedLegacyAlias = 2
    }

    public sealed class GameDataBuildingResourceShare
    {
        internal GameDataBuildingResourceShare(
            string resourceStableId,
            int percent)
        {
            ResourceStableId = resourceStableId;
            Percent = percent;
        }

        public string ResourceStableId { get; }
        public int Percent { get; }
    }

    public sealed class GameDataBuildingResourceAmount
    {
        internal GameDataBuildingResourceAmount(
            string resourceStableId,
            long amount)
        {
            ResourceStableId = resourceStableId;
            Amount = amount;
        }

        public string ResourceStableId { get; }
        public long Amount { get; }
    }

    public sealed class GameDataBuildingCostLevel
    {
        internal GameDataBuildingCostLevel(
            int targetLevel,
            long baseBudget,
            long budget,
            IEnumerable<GameDataBuildingResourceAmount> costs)
        {
            TargetLevel = targetLevel;
            BaseBudget = baseBudget;
            Budget = budget;
            Costs = ImmutableCollections.Freeze(costs);
        }

        public int TargetLevel { get; }
        public long BaseBudget { get; }
        public long Budget { get; }
        public IReadOnlyList<GameDataBuildingResourceAmount> Costs { get; }
    }

    public sealed class GameDataBuildingCostProfile
    {
        internal GameDataBuildingCostProfile(
            int order,
            int version,
            string stableId,
            int scalePercent,
            IEnumerable<GameDataBuildingResourceShare> shares,
            IEnumerable<GameDataBuildingCostLevel> levels)
        {
            Order = order;
            Version = version;
            StableId = stableId;
            ScalePercent = scalePercent;
            Shares = ImmutableCollections.Freeze(shares);
            Levels = ImmutableCollections.Freeze(levels);
        }

        public int Order { get; }
        public int Version { get; }
        public string StableId { get; }
        public int ScalePercent { get; }
        public IReadOnlyList<GameDataBuildingResourceShare> Shares { get; }
        public IReadOnlyList<GameDataBuildingCostLevel> Levels { get; }

        public bool TryGetLevel(
            int targetLevel,
            out GameDataBuildingCostLevel level)
        {
            if (targetLevel <= 0 || targetLevel > Levels.Count)
            {
                level = null;
                return false;
            }

            level = Levels[targetLevel - 1];
            return level.TargetLevel == targetLevel;
        }
    }

    public sealed class GameDataBuildingDurationLevel
    {
        internal GameDataBuildingDurationLevel(
            int targetLevel,
            int durationSeconds)
        {
            TargetLevel = targetLevel;
            DurationSeconds = durationSeconds;
        }

        public int TargetLevel { get; }
        public int DurationSeconds { get; }
    }

    public sealed class GameDataBuildingDurationProfile
    {
        internal GameDataBuildingDurationProfile(
            int version,
            string stableId,
            IEnumerable<GameDataBuildingDurationLevel> levels)
        {
            Version = version;
            StableId = stableId;
            Levels = ImmutableCollections.Freeze(levels);
        }

        public int Version { get; }
        public string StableId { get; }
        public IReadOnlyList<GameDataBuildingDurationLevel> Levels { get; }

        public bool TryGetLevel(
            int targetLevel,
            out GameDataBuildingDurationLevel level)
        {
            if (targetLevel <= 0 || targetLevel > Levels.Count)
            {
                level = null;
                return false;
            }

            level = Levels[targetLevel - 1];
            return level.TargetLevel == targetLevel;
        }
    }

    public sealed class GameDataBuildingPrerequisiteProfile
    {
        internal GameDataBuildingPrerequisiteProfile(
            int version,
            string stableId,
            IEnumerable<string> requiredBuildingStableIds)
        {
            Version = version;
            StableId = stableId;
            RequiredBuildingStableIds =
                ImmutableCollections.Freeze(requiredBuildingStableIds);
        }

        public int Version { get; }
        public string StableId { get; }
        public IReadOnlyList<string> RequiredBuildingStableIds { get; }
    }

    public sealed class GameDataBuildingRealmEligibilityProfile
    {
        private readonly HashSet<string> eligibleRealmStableIds;

        internal GameDataBuildingRealmEligibilityProfile(
            int version,
            string stableId,
            IEnumerable<string> realmStableIds)
        {
            Version = version;
            StableId = stableId;
            EligibleRealmStableIds = ImmutableCollections.Freeze(realmStableIds);
            eligibleRealmStableIds =
                new HashSet<string>(EligibleRealmStableIds, StringComparer.Ordinal);
        }

        public int Version { get; }
        public string StableId { get; }
        public IReadOnlyList<string> EligibleRealmStableIds { get; }

        public bool IsEligible(string realmStableId)
        {
            return realmStableId != null &&
                   eligibleRealmStableIds.Contains(realmStableId);
        }
    }

    public sealed class GameDataBuildingProgressionReference
    {
        internal GameDataBuildingProgressionReference(
            int order,
            int version,
            string stableId,
            string legacyBuildingId,
            string nameReference,
            int initialLevel,
            int maximumLevel,
            string costProfileStableId,
            string durationProfileStableId,
            string prerequisiteProfileStableId,
            string realmEligibilityProfileStableId)
        {
            Order = order;
            Version = version;
            StableId = stableId;
            LegacyBuildingId = legacyBuildingId;
            NameReference = nameReference;
            InitialLevel = initialLevel;
            MaximumLevel = maximumLevel;
            CostProfileStableId = costProfileStableId;
            DurationProfileStableId = durationProfileStableId;
            PrerequisiteProfileStableId = prerequisiteProfileStableId;
            RealmEligibilityProfileStableId = realmEligibilityProfileStableId;
        }

        public int Order { get; }
        public int Version { get; }
        public string StableId { get; }
        public string LegacyBuildingId { get; }
        public string NameReference { get; }
        public int InitialLevel { get; }
        public int MaximumLevel { get; }
        public string CostProfileStableId { get; }
        public string DurationProfileStableId { get; }
        public string PrerequisiteProfileStableId { get; }
        public string RealmEligibilityProfileStableId { get; }
    }

    /// <summary>
    /// Exact, non-published building progression authority accepted from Phase C4A.
    /// It is a pure immutable registry and is not wired to a loader, service, save, or runtime.
    /// </summary>
    public static class GameDataBuildingProgressionRegistry
    {
        public const int Version = 1;
        public const int BuildingCount = 15;
        public const int TargetLevelCount = 10;
        public const int InitialLevel = 0;
        public const int MaximumLevel = 10;
        public const string DurationProfileStableId =
            "building_upgrade_duration_common";
        public const string PrerequisiteProfileStableId =
            "building_prerequisite_none";
        public const string RealmEligibilityProfileStableId =
            "building_realm_eligibility_all";

        private static readonly long[] baseBudgets =
        {
            100L, 175L, 300L, 475L, 700L,
            1000L, 1400L, 1900L, 2500L, 3250L
        };

        private static readonly int[] durationSeconds =
        {
            10, 30, 120, 300, 900,
            1800, 3600, 7200, 14400, 28800
        };

        private static readonly IReadOnlyList<GameDataBuildingProgressionReference>
            entries;
        private static readonly IReadOnlyList<GameDataBuildingCostProfile>
            costProfiles;
        private static readonly IReadOnlyList<string> stableIds;
        private static readonly IReadOnlyList<string> legacyBuildingIds;
        private static readonly IReadOnlyList<string> nameReferences;
        private static readonly IReadOnlyList<string> costProfileStableIds;
        private static readonly Dictionary<string, GameDataBuildingProgressionReference>
            entriesByStableId;
        private static readonly Dictionary<string, GameDataBuildingProgressionReference>
            entriesByLegacyBuildingId;
        private static readonly Dictionary<string, GameDataBuildingCostProfile>
            costProfilesByStableId;

        static GameDataBuildingProgressionRegistry()
        {
            DurationProfile = CreateDurationProfile();
            PrerequisiteProfile =
                new GameDataBuildingPrerequisiteProfile(
                    Version,
                    PrerequisiteProfileStableId,
                    new string[0]);
            RealmEligibilityProfile =
                new GameDataBuildingRealmEligibilityProfile(
                    Version,
                    RealmEligibilityProfileStableId,
                    GameDataRealmReferences.StableIds);

            var mutableCostProfiles = new[]
            {
                CostProfile(
                    0,
                    "building_upgrade_cost_town_hall",
                    140,
                    Share("stone", 45),
                    Share("wood", 35),
                    Share("gold", 20)),
                CostProfile(
                    1,
                    "building_upgrade_cost_farm",
                    80,
                    Share("wood", 70),
                    Share("stone", 30)),
                CostProfile(
                    2,
                    "building_upgrade_cost_lumber_mill",
                    80,
                    Share("wood", 70),
                    Share("stone", 30)),
                CostProfile(
                    3,
                    "building_upgrade_cost_quarry",
                    90,
                    Share("wood", 40),
                    Share("stone", 60)),
                CostProfile(
                    4,
                    "building_upgrade_cost_gold_mine",
                    100,
                    Share("wood", 40),
                    Share("stone", 60)),
                CostProfile(
                    5,
                    "building_upgrade_cost_barracks",
                    110,
                    Share("stone", 55),
                    Share("wood", 30),
                    Share("gold", 15)),
                CostProfile(
                    6,
                    "building_upgrade_cost_academy",
                    120,
                    Share("stone", 40),
                    Share("wood", 25),
                    Share("mana_stone", 35)),
                CostProfile(
                    7,
                    "building_upgrade_cost_market",
                    90,
                    Share("wood", 45),
                    Share("stone", 25),
                    Share("gold", 30)),
                CostProfile(
                    8,
                    "building_upgrade_cost_storehouse",
                    85,
                    Share("wood", 60),
                    Share("stone", 40)),
                CostProfile(
                    9,
                    "building_upgrade_cost_forge",
                    115,
                    Share("stone", 45),
                    Share("wood", 25),
                    Share("ore", 30)),
                CostProfile(
                    10,
                    "building_upgrade_cost_stable",
                    100,
                    Share("wood", 55),
                    Share("stone", 25),
                    Share("gold", 20)),
                CostProfile(
                    11,
                    "building_upgrade_cost_workshop",
                    110,
                    Share("stone", 45),
                    Share("wood", 25),
                    Share("ore", 30)),
                CostProfile(
                    12,
                    "building_upgrade_cost_embassy",
                    120,
                    Share("wood", 45),
                    Share("stone", 25),
                    Share("gold", 30)),
                CostProfile(
                    13,
                    "building_upgrade_cost_wall",
                    95,
                    Share("stone", 55),
                    Share("wood", 30),
                    Share("gold", 15)),
                CostProfile(
                    14,
                    "building_upgrade_cost_watchtower",
                    100,
                    Share("stone", 55),
                    Share("wood", 30),
                    Share("gold", 15))
            };

            var mutableEntries = new[]
            {
                Building(0, "town_hall", "TownHall", "building.town_hall.name",
                    mutableCostProfiles[0].StableId),
                Building(1, "farm", "Farm", "building.farm.name",
                    mutableCostProfiles[1].StableId),
                Building(2, "lumber_mill", "LumberMill", "building.lumber_mill.name",
                    mutableCostProfiles[2].StableId),
                Building(3, "quarry", "Quarry", "building.quarry.name",
                    mutableCostProfiles[3].StableId),
                Building(4, "gold_mine", "GoldMine", "building.gold_mine.name",
                    mutableCostProfiles[4].StableId),
                Building(5, "barracks", "Barracks", "building.barracks.name",
                    mutableCostProfiles[5].StableId),
                Building(6, "academy", "Academy", "building.academy.name",
                    mutableCostProfiles[6].StableId),
                Building(7, "market", "Market", "building.market.name",
                    mutableCostProfiles[7].StableId),
                Building(8, "storehouse", "Storehouse", "building.storehouse.name",
                    mutableCostProfiles[8].StableId),
                Building(9, "forge", "Forge", "building.forge.name",
                    mutableCostProfiles[9].StableId),
                Building(10, "stable", "Stable", "building.stable.name",
                    mutableCostProfiles[10].StableId),
                Building(11, "workshop", "Workshop", "building.workshop.name",
                    mutableCostProfiles[11].StableId),
                Building(12, "embassy", "Embassy", "building.embassy.name",
                    mutableCostProfiles[12].StableId),
                Building(13, "wall", "Wall", "building.wall.name",
                    mutableCostProfiles[13].StableId),
                Building(14, "watchtower", "Watchtower", "building.watchtower.name",
                    mutableCostProfiles[14].StableId)
            };

            if (mutableEntries.Length != BuildingCount ||
                mutableCostProfiles.Length != BuildingCount ||
                DurationProfile.Levels.Count != TargetLevelCount ||
                PrerequisiteProfile.RequiredBuildingStableIds.Count != 0 ||
                RealmEligibilityProfile.EligibleRealmStableIds.Count !=
                GameDataRealmReferences.RealmCount)
            {
                throw new InvalidOperationException(
                    "Building progression authority has an invalid bounded shape.");
            }

            entriesByStableId =
                new Dictionary<string, GameDataBuildingProgressionReference>(
                    StringComparer.Ordinal);
            entriesByLegacyBuildingId =
                new Dictionary<string, GameDataBuildingProgressionReference>(
                    StringComparer.Ordinal);
            costProfilesByStableId =
                new Dictionary<string, GameDataBuildingCostProfile>(
                    StringComparer.Ordinal);

            var mutableStableIds = new string[BuildingCount];
            var mutableLegacyBuildingIds = new string[BuildingCount];
            var mutableNameReferences = new string[BuildingCount];
            var mutableCostProfileStableIds = new string[BuildingCount];

            for (var index = 0; index < BuildingCount; index++)
            {
                var entry = mutableEntries[index];
                var costProfile = mutableCostProfiles[index];
                if (entry.Order != index ||
                    entry.Version != Version ||
                    costProfile.Order != index ||
                    costProfile.Version != Version ||
                    !GameDataCatalogIdentifiers.IsCanonicalStableId(entry.StableId) ||
                    !IsExactLegacyBuildingId(entry.LegacyBuildingId) ||
                    !IsCanonicalContentReference(entry.NameReference) ||
                    entry.InitialLevel != InitialLevel ||
                    entry.MaximumLevel != MaximumLevel ||
                    !string.Equals(
                        entry.CostProfileStableId,
                        costProfile.StableId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.DurationProfileStableId,
                        DurationProfile.StableId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.PrerequisiteProfileStableId,
                        PrerequisiteProfile.StableId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.RealmEligibilityProfileStableId,
                        RealmEligibilityProfile.StableId,
                        StringComparison.Ordinal) ||
                    !entriesByStableId.TryAdd(entry.StableId, entry) ||
                    !entriesByLegacyBuildingId.TryAdd(
                        entry.LegacyBuildingId,
                        entry) ||
                    !costProfilesByStableId.TryAdd(
                        costProfile.StableId,
                        costProfile))
                {
                    throw new InvalidOperationException(
                        "Building progression authority contains invalid or duplicate identity.");
                }

                mutableStableIds[index] = entry.StableId;
                mutableLegacyBuildingIds[index] = entry.LegacyBuildingId;
                mutableNameReferences[index] = entry.NameReference;
                mutableCostProfileStableIds[index] = costProfile.StableId;
            }

            entries = ImmutableCollections.Freeze(mutableEntries);
            costProfiles = ImmutableCollections.Freeze(mutableCostProfiles);
            stableIds = ImmutableCollections.Freeze(mutableStableIds);
            legacyBuildingIds = ImmutableCollections.Freeze(mutableLegacyBuildingIds);
            nameReferences = ImmutableCollections.Freeze(mutableNameReferences);
            costProfileStableIds =
                ImmutableCollections.Freeze(mutableCostProfileStableIds);
        }

        public static IReadOnlyList<GameDataBuildingProgressionReference> Entries =>
            entries;
        public static IReadOnlyList<GameDataBuildingCostProfile> CostProfiles =>
            costProfiles;
        public static IReadOnlyList<string> StableIds => stableIds;
        public static IReadOnlyList<string> LegacyBuildingIds => legacyBuildingIds;
        public static IReadOnlyList<string> NameReferences => nameReferences;
        public static IReadOnlyList<string> CostProfileStableIds =>
            costProfileStableIds;
        public static GameDataBuildingDurationProfile DurationProfile { get; }
        public static GameDataBuildingPrerequisiteProfile PrerequisiteProfile { get; }
        public static GameDataBuildingRealmEligibilityProfile
            RealmEligibilityProfile { get; }

        public static bool TryGetByStableId(
            string stableId,
            out GameDataBuildingProgressionReference reference)
        {
            return entriesByStableId.TryGetValue(
                stableId ?? string.Empty,
                out reference);
        }

        public static bool TryGetByLegacyBuildingId(
            string legacyBuildingId,
            out GameDataBuildingProgressionReference reference)
        {
            return entriesByLegacyBuildingId.TryGetValue(
                legacyBuildingId ?? string.Empty,
                out reference);
        }

        public static GameDataBuildingIdentityResolutionStatus Resolve(
            string value,
            out GameDataBuildingProgressionReference reference)
        {
            if (TryGetByStableId(value, out reference))
            {
                return GameDataBuildingIdentityResolutionStatus.ExactCanonical;
            }

            if (TryGetByLegacyBuildingId(value, out reference))
            {
                return GameDataBuildingIdentityResolutionStatus.ResolvedLegacyAlias;
            }

            reference = null;
            return GameDataBuildingIdentityResolutionStatus.Unknown;
        }

        public static bool TryGetCostProfileByStableId(
            string stableId,
            out GameDataBuildingCostProfile profile)
        {
            return costProfilesByStableId.TryGetValue(
                stableId ?? string.Empty,
                out profile);
        }

        public static bool IsApprovedBuildingRelation(
            string stableId,
            string legacyBuildingId,
            string nameReference,
            int initialLevel,
            int maximumLevel,
            string costProfileStableId,
            string durationProfileStableId,
            string prerequisiteProfileStableId,
            string realmEligibilityProfileStableId)
        {
            GameDataBuildingProgressionReference reference;
            return TryGetByStableId(stableId, out reference) &&
                   string.Equals(
                       reference.LegacyBuildingId,
                       legacyBuildingId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.NameReference,
                       nameReference,
                       StringComparison.Ordinal) &&
                   reference.InitialLevel == initialLevel &&
                   reference.MaximumLevel == maximumLevel &&
                   string.Equals(
                       reference.CostProfileStableId,
                       costProfileStableId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.DurationProfileStableId,
                       durationProfileStableId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.PrerequisiteProfileStableId,
                       prerequisiteProfileStableId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       reference.RealmEligibilityProfileStableId,
                       realmEligibilityProfileStableId,
                       StringComparison.Ordinal);
        }

        private static GameDataBuildingProgressionReference Building(
            int order,
            string stableId,
            string legacyBuildingId,
            string nameReference,
            string costProfileStableId)
        {
            return new GameDataBuildingProgressionReference(
                order,
                Version,
                stableId,
                legacyBuildingId,
                nameReference,
                InitialLevel,
                MaximumLevel,
                costProfileStableId,
                DurationProfileStableId,
                PrerequisiteProfileStableId,
                RealmEligibilityProfileStableId);
        }

        private static GameDataBuildingCostProfile CostProfile(
            int order,
            string stableId,
            int scalePercent,
            params GameDataBuildingResourceShare[] shares)
        {
            if (!GameDataCatalogIdentifiers.IsCanonicalStableId(stableId) ||
                scalePercent <= 0 ||
                shares == null ||
                (shares.Length != 2 && shares.Length != 3))
            {
                throw new InvalidOperationException(
                    "Building cost profile identity or shape is invalid.");
            }

            var percentageTotal = 0;
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < shares.Length; index++)
            {
                var share = shares[index];
                GameDataWalletResourceReference resource;
                percentageTotal = checked(percentageTotal + share.Percent);
                if (share.Percent <= 0 ||
                    !GameDataWalletResourceReferences.TryGetByStableId(
                        share.ResourceStableId,
                        out resource) ||
                    resource.Classification !=
                    GameDataWalletResourceClassification.Core ||
                    !resourceIds.Add(share.ResourceStableId))
                {
                    throw new InvalidOperationException(
                        "Building cost profile contains an invalid resource share.");
                }
            }

            if (percentageTotal != 100)
            {
                throw new InvalidOperationException(
                    "Building cost profile percentages must total exactly 100.");
            }

            var levels = new GameDataBuildingCostLevel[TargetLevelCount];
            for (var index = 0; index < TargetLevelCount; index++)
            {
                var baseBudget = baseBudgets[index];
                var scaledBudget =
                    checked(checked(baseBudget * scalePercent) + 99L);
                var budget = scaledBudget / 100L;
                var amounts =
                    new GameDataBuildingResourceAmount[shares.Length];
                long assigned = 0L;
                for (var shareIndex = 0;
                     shareIndex < shares.Length;
                     shareIndex++)
                {
                    long amount;
                    if (shareIndex == shares.Length - 1)
                    {
                        amount = checked(budget - assigned);
                    }
                    else
                    {
                        amount = Math.Max(
                            1L,
                            checked(budget * shares[shareIndex].Percent) / 100L);
                    }

                    if (amount <= 0)
                    {
                        throw new InvalidOperationException(
                            "Building cost profile produced a non-positive amount.");
                    }

                    assigned = checked(assigned + amount);
                    amounts[shareIndex] =
                        new GameDataBuildingResourceAmount(
                            shares[shareIndex].ResourceStableId,
                            amount);
                }

                if (assigned != budget)
                {
                    throw new InvalidOperationException(
                        "Building cost profile amounts must sum to the exact budget.");
                }

                levels[index] =
                    new GameDataBuildingCostLevel(
                        index + 1,
                        baseBudget,
                        budget,
                        amounts);
            }

            return new GameDataBuildingCostProfile(
                order,
                Version,
                stableId,
                scalePercent,
                shares,
                levels);
        }

        private static GameDataBuildingDurationProfile CreateDurationProfile()
        {
            if (durationSeconds.Length != TargetLevelCount)
            {
                throw new InvalidOperationException(
                    "Building duration authority must contain ten levels.");
            }

            var levels =
                new GameDataBuildingDurationLevel[TargetLevelCount];
            for (var index = 0; index < TargetLevelCount; index++)
            {
                if (durationSeconds[index] <= 0)
                {
                    throw new InvalidOperationException(
                        "Building duration authority contains a non-positive duration.");
                }

                levels[index] =
                    new GameDataBuildingDurationLevel(
                        index + 1,
                        durationSeconds[index]);
            }

            return new GameDataBuildingDurationProfile(
                Version,
                DurationProfileStableId,
                levels);
        }

        private static GameDataBuildingResourceShare Share(
            string resourceStableId,
            int percent)
        {
            return new GameDataBuildingResourceShare(
                resourceStableId,
                percent);
        }

        private static bool IsExactLegacyBuildingId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 128 ||
                value[0] < 'A' ||
                value[0] > 'Z')
            {
                return false;
            }

            for (var index = 1; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= 'A' && character <= 'Z') ||
                      (character >= 'a' && character <= 'z') ||
                      (character >= '0' && character <= '9')))
                {
                    return false;
                }
            }

            return true;
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
                if (!GameDataCatalogIdentifiers.IsCanonicalStableId(
                        segments[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
