using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs;
using AL.Data.Definitions;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataBuildingProgressionRegistryTests
    {
        private static readonly string[] StableIds =
        {
            "town_hall", "farm", "lumber_mill", "quarry", "gold_mine",
            "barracks", "academy", "market", "storehouse", "forge",
            "stable", "workshop", "embassy", "wall", "watchtower"
        };

        private static readonly string[] LegacyBuildingIds =
        {
            "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine",
            "Barracks", "Academy", "Market", "Storehouse", "Forge",
            "Stable", "Workshop", "Embassy", "Wall", "Watchtower"
        };

        private static readonly string[] NameReferences =
        {
            "building.town_hall.name",
            "building.farm.name",
            "building.lumber_mill.name",
            "building.quarry.name",
            "building.gold_mine.name",
            "building.barracks.name",
            "building.academy.name",
            "building.market.name",
            "building.storehouse.name",
            "building.forge.name",
            "building.stable.name",
            "building.workshop.name",
            "building.embassy.name",
            "building.wall.name",
            "building.watchtower.name"
        };

        private static readonly string[] CostProfileStableIds =
        {
            "building_upgrade_cost_town_hall",
            "building_upgrade_cost_farm",
            "building_upgrade_cost_lumber_mill",
            "building_upgrade_cost_quarry",
            "building_upgrade_cost_gold_mine",
            "building_upgrade_cost_barracks",
            "building_upgrade_cost_academy",
            "building_upgrade_cost_market",
            "building_upgrade_cost_storehouse",
            "building_upgrade_cost_forge",
            "building_upgrade_cost_stable",
            "building_upgrade_cost_workshop",
            "building_upgrade_cost_embassy",
            "building_upgrade_cost_wall",
            "building_upgrade_cost_watchtower"
        };

        private static readonly int[] ScalePercents =
        {
            140, 80, 80, 90, 100,
            110, 120, 90, 85, 115,
            100, 110, 120, 95, 100
        };

        private static readonly string[][] ResourceStableIds =
        {
            new[] { "stone", "wood", "gold" },
            new[] { "wood", "stone" },
            new[] { "wood", "stone" },
            new[] { "wood", "stone" },
            new[] { "wood", "stone" },
            new[] { "stone", "wood", "gold" },
            new[] { "stone", "wood", "mana_stone" },
            new[] { "wood", "stone", "gold" },
            new[] { "wood", "stone" },
            new[] { "stone", "wood", "ore" },
            new[] { "wood", "stone", "gold" },
            new[] { "stone", "wood", "ore" },
            new[] { "wood", "stone", "gold" },
            new[] { "stone", "wood", "gold" },
            new[] { "stone", "wood", "gold" }
        };

        private static readonly int[][] ResourcePercents =
        {
            new[] { 45, 35, 20 },
            new[] { 70, 30 },
            new[] { 70, 30 },
            new[] { 40, 60 },
            new[] { 40, 60 },
            new[] { 55, 30, 15 },
            new[] { 40, 25, 35 },
            new[] { 45, 25, 30 },
            new[] { 60, 40 },
            new[] { 45, 25, 30 },
            new[] { 55, 25, 20 },
            new[] { 45, 25, 30 },
            new[] { 45, 25, 30 },
            new[] { 55, 30, 15 },
            new[] { 55, 30, 15 }
        };

        private static readonly long[] BaseBudgets =
        {
            100L, 175L, 300L, 475L, 700L,
            1000L, 1400L, 1900L, 2500L, 3250L
        };

        private static readonly int[] Durations =
        {
            10, 30, 120, 300, 900,
            1800, 3600, 7200, 14400, 28800
        };

        [Test]
        public void RegistryPublishesExactImmutableAuthoredOrder()
        {
            Assert.AreEqual(1, GameDataBuildingProgressionRegistry.Version);
            Assert.AreEqual(
                15,
                GameDataBuildingProgressionRegistry.BuildingCount);
            Assert.AreEqual(
                10,
                GameDataBuildingProgressionRegistry.TargetLevelCount);
            Assert.AreEqual(0, GameDataBuildingProgressionRegistry.InitialLevel);
            Assert.AreEqual(10, GameDataBuildingProgressionRegistry.MaximumLevel);

            CollectionAssert.AreEqual(
                StableIds,
                GameDataBuildingProgressionRegistry.StableIds.ToArray());
            CollectionAssert.AreEqual(
                LegacyBuildingIds,
                GameDataBuildingProgressionRegistry.LegacyBuildingIds.ToArray());
            CollectionAssert.AreEqual(
                NameReferences,
                GameDataBuildingProgressionRegistry.NameReferences.ToArray());
            CollectionAssert.AreEqual(
                CostProfileStableIds,
                GameDataBuildingProgressionRegistry
                    .CostProfileStableIds
                    .ToArray());

            Assert.AreEqual(
                StableIds.Length,
                GameDataBuildingProgressionRegistry.Entries.Count);
            Assert.AreEqual(
                StableIds.Length,
                GameDataBuildingProgressionRegistry.CostProfiles.Count);
            for (var index = 0; index < StableIds.Length; index++)
            {
                var reference =
                    GameDataBuildingProgressionRegistry.Entries[index];
                var costProfile =
                    GameDataBuildingProgressionRegistry.CostProfiles[index];
                Assert.AreEqual(index, reference.Order);
                Assert.AreEqual(1, reference.Version);
                Assert.AreEqual(StableIds[index], reference.StableId);
                Assert.AreEqual(
                    LegacyBuildingIds[index],
                    reference.LegacyBuildingId);
                Assert.AreEqual(
                    NameReferences[index],
                    reference.NameReference);
                Assert.AreEqual(0, reference.InitialLevel);
                Assert.AreEqual(10, reference.MaximumLevel);
                Assert.AreEqual(
                    CostProfileStableIds[index],
                    reference.CostProfileStableId);
                Assert.AreEqual(index, costProfile.Order);
                Assert.AreEqual(1, costProfile.Version);
                Assert.AreEqual(
                    CostProfileStableIds[index],
                    costProfile.StableId);
                Assert.AreEqual(
                    GameDataBuildingProgressionRegistry.DurationProfileStableId,
                    reference.DurationProfileStableId);
                Assert.AreEqual(
                    GameDataBuildingProgressionRegistry
                        .PrerequisiteProfileStableId,
                    reference.PrerequisiteProfileStableId);
                Assert.AreEqual(
                    GameDataBuildingProgressionRegistry
                        .RealmEligibilityProfileStableId,
                    reference.RealmEligibilityProfileStableId);
            }

            AssertReadOnly(
                GameDataBuildingProgressionRegistry.Entries,
                GameDataBuildingProgressionRegistry.Entries[1]);
            AssertReadOnly(
                GameDataBuildingProgressionRegistry.CostProfiles,
                GameDataBuildingProgressionRegistry.CostProfiles[1]);
            AssertReadOnly(
                GameDataBuildingProgressionRegistry.StableIds,
                "replacement");
            AssertReadOnly(
                GameDataBuildingProgressionRegistry.LegacyBuildingIds,
                "Replacement");
            AssertReadOnly(
                GameDataBuildingProgressionRegistry.NameReferences,
                "replacement.name");
            AssertReadOnly(
                GameDataBuildingProgressionRegistry.CostProfileStableIds,
                "replacement_profile");
        }

        [Test]
        public void ExactResolversRejectNormalizationAndUnavailableAnchors()
        {
            for (var index = 0; index < StableIds.Length; index++)
            {
                GameDataBuildingProgressionReference canonical;
                GameDataBuildingProgressionReference alias;
                Assert.AreEqual(
                    GameDataBuildingIdentityResolutionStatus.ExactCanonical,
                    GameDataBuildingProgressionRegistry.Resolve(
                        StableIds[index],
                        out canonical));
                Assert.AreEqual(
                    GameDataBuildingIdentityResolutionStatus.ResolvedLegacyAlias,
                    GameDataBuildingProgressionRegistry.Resolve(
                        LegacyBuildingIds[index],
                        out alias));
                Assert.AreSame(canonical, alias);
                Assert.True(
                    GameDataBuildingProgressionRegistry
                        .IsApprovedBuildingRelation(
                            canonical.StableId,
                            canonical.LegacyBuildingId,
                            canonical.NameReference,
                            canonical.InitialLevel,
                            canonical.MaximumLevel,
                            canonical.CostProfileStableId,
                            canonical.DurationProfileStableId,
                            canonical.PrerequisiteProfileStableId,
                            canonical.RealmEligibilityProfileStableId));
            }

            foreach (var invalid in new[]
                     {
                         null,
                         string.Empty,
                         " ",
                         "Townhall",
                         "townhall",
                         "TOWN_HALL",
                         "town-hall",
                         " town_hall",
                         "town_hall ",
                         "ManaShrine",
                         "Mine",
                         "unknown_building"
                     })
            {
                GameDataBuildingProgressionReference reference;
                Assert.AreEqual(
                    GameDataBuildingIdentityResolutionStatus.Unknown,
                    GameDataBuildingProgressionRegistry.Resolve(
                        invalid,
                        out reference),
                    invalid);
                Assert.IsNull(reference, invalid);
            }

            GameDataBuildingCostProfile profile;
            Assert.False(
                GameDataBuildingProgressionRegistry
                    .TryGetCostProfileByStableId(
                        "Building_upgrade_cost_town_hall",
                        out profile));
            Assert.IsNull(profile);

            var townHall = GameDataBuildingProgressionRegistry.Entries[0];
            var farm = GameDataBuildingProgressionRegistry.Entries[1];
            Assert.False(
                GameDataBuildingProgressionRegistry.IsApprovedBuildingRelation(
                    townHall.StableId,
                    townHall.LegacyBuildingId,
                    townHall.NameReference,
                    townHall.InitialLevel,
                    townHall.MaximumLevel,
                    farm.CostProfileStableId,
                    townHall.DurationProfileStableId,
                    townHall.PrerequisiteProfileStableId,
                    townHall.RealmEligibilityProfileStableId));
        }

        [Test]
        public void ProfilesPrecomputeAllOneHundredFiftyExactVectors()
        {
            var vectorCount = 0;
            for (var profileIndex = 0;
                 profileIndex <
                 GameDataBuildingProgressionRegistry.CostProfiles.Count;
                 profileIndex++)
            {
                var profile =
                    GameDataBuildingProgressionRegistry
                        .CostProfiles[profileIndex];
                Assert.AreEqual(ScalePercents[profileIndex], profile.ScalePercent);
                CollectionAssert.AreEqual(
                    ResourceStableIds[profileIndex],
                    profile.Shares
                        .Select(share => share.ResourceStableId)
                        .ToArray());
                CollectionAssert.AreEqual(
                    ResourcePercents[profileIndex],
                    profile.Shares
                        .Select(share => share.Percent)
                        .ToArray());
                Assert.AreEqual(10, profile.Levels.Count);

                for (var levelIndex = 0;
                     levelIndex < BaseBudgets.Length;
                     levelIndex++)
                {
                    var targetLevel = levelIndex + 1;
                    GameDataBuildingCostLevel level;
                    Assert.True(profile.TryGetLevel(targetLevel, out level));
                    Assert.AreEqual(targetLevel, level.TargetLevel);
                    Assert.AreEqual(BaseBudgets[levelIndex], level.BaseBudget);
                    var expectedBudget =
                        (BaseBudgets[levelIndex] *
                         (long)ScalePercents[profileIndex] +
                         99L) /
                        100L;
                    Assert.AreEqual(expectedBudget, level.Budget);
                    CollectionAssert.AreEqual(
                        ResourceStableIds[profileIndex],
                        level.Costs
                            .Select(cost => cost.ResourceStableId)
                            .ToArray());
                    CollectionAssert.AreEqual(
                        ExpectedAmounts(
                            expectedBudget,
                            ResourcePercents[profileIndex]),
                        level.Costs
                            .Select(cost => cost.Amount)
                            .ToArray());
                    Assert.AreEqual(
                        expectedBudget,
                        level.Costs.Sum(cost => cost.Amount));
                    Assert.True(level.Costs.All(cost => cost.Amount > 0));
                    AssertReadOnly(
                        level.Costs,
                        level.Costs[0]);
                    vectorCount++;
                }

                GameDataBuildingCostLevel invalidLevel;
                Assert.False(profile.TryGetLevel(0, out invalidLevel));
                Assert.IsNull(invalidLevel);
                Assert.False(profile.TryGetLevel(11, out invalidLevel));
                Assert.IsNull(invalidLevel);
                AssertReadOnly(profile.Shares, profile.Shares[0]);
                AssertReadOnly(profile.Levels, profile.Levels[0]);
            }

            Assert.AreEqual(150, vectorCount);
        }

        [Test]
        public void DurationPrerequisiteAndRealmEligibilityProfilesAreExact()
        {
            var duration =
                GameDataBuildingProgressionRegistry.DurationProfile;
            Assert.AreEqual(1, duration.Version);
            Assert.AreEqual(
                "building_upgrade_duration_common",
                duration.StableId);
            CollectionAssert.AreEqual(
                Enumerable.Range(1, 10),
                duration.Levels.Select(level => level.TargetLevel));
            CollectionAssert.AreEqual(
                Durations,
                duration.Levels.Select(level => level.DurationSeconds));
            AssertReadOnly(duration.Levels, duration.Levels[0]);

            GameDataBuildingDurationLevel durationLevel;
            Assert.False(duration.TryGetLevel(0, out durationLevel));
            Assert.IsNull(durationLevel);
            Assert.False(duration.TryGetLevel(11, out durationLevel));
            Assert.IsNull(durationLevel);
            for (var targetLevel = 1; targetLevel <= 10; targetLevel++)
            {
                Assert.True(duration.TryGetLevel(targetLevel, out durationLevel));
                Assert.AreEqual(
                    Durations[targetLevel - 1],
                    durationLevel.DurationSeconds);
            }

            var prerequisite =
                GameDataBuildingProgressionRegistry.PrerequisiteProfile;
            Assert.AreEqual(1, prerequisite.Version);
            Assert.AreEqual(
                "building_prerequisite_none",
                prerequisite.StableId);
            Assert.IsEmpty(prerequisite.RequiredBuildingStableIds);
            AssertReadOnly(
                prerequisite.RequiredBuildingStableIds,
                "replacement");

            var eligibility =
                GameDataBuildingProgressionRegistry.RealmEligibilityProfile;
            Assert.AreEqual(1, eligibility.Version);
            Assert.AreEqual(
                "building_realm_eligibility_all",
                eligibility.StableId);
            CollectionAssert.AreEqual(
                GameDataRealmReferences.StableIds,
                eligibility.EligibleRealmStableIds);
            foreach (var realmId in GameDataRealmReferences.StableIds)
            {
                Assert.True(eligibility.IsEligible(realmId), realmId);
            }

            foreach (var invalidRealm in new[]
                     {
                         null,
                         string.Empty,
                         "Stonehold",
                         " stonehold",
                         "stonehold ",
                         "unknown_realm"
                     })
            {
                Assert.False(eligibility.IsEligible(invalidRealm), invalidRealm);
            }

            AssertReadOnly(
                eligibility.EligibleRealmStableIds,
                "replacement_realm");
        }

        [Test]
        public void RegistryVectorsRemainExactWithMergedLiveConstructionSource()
        {
            var gameData = new LocalGameDataService();
            var vectorCount = 0;
            foreach (var reference in GameDataBuildingProgressionRegistry.Entries)
            {
                BuildingDefinition live =
                    gameData.GetBuilding(reference.LegacyBuildingId);
                Assert.NotNull(live, reference.LegacyBuildingId);
                Assert.AreEqual(
                    reference.MaximumLevel,
                    live.MaxLevel,
                    reference.StableId);
                Assert.AreEqual(
                    GameDataBuildingProgressionRegistry.TargetLevelCount,
                    live.ConstructionLevels.Count,
                    reference.StableId);

                GameDataBuildingCostProfile profile;
                Assert.True(
                    GameDataBuildingProgressionRegistry
                        .TryGetCostProfileByStableId(
                            reference.CostProfileStableId,
                            out profile));
                for (var levelIndex = 0;
                     levelIndex < live.ConstructionLevels.Count;
                     levelIndex++)
                {
                    var liveLevel = live.ConstructionLevels[levelIndex];
                    GameDataBuildingCostLevel registeredCost;
                    GameDataBuildingDurationLevel registeredDuration;
                    Assert.True(
                        profile.TryGetLevel(
                            liveLevel.TargetLevel,
                            out registeredCost));
                    Assert.True(
                        GameDataBuildingProgressionRegistry.DurationProfile
                            .TryGetLevel(
                                liveLevel.TargetLevel,
                                out registeredDuration));
                    Assert.AreEqual(
                        liveLevel.DurationSeconds,
                        registeredDuration.DurationSeconds,
                        reference.StableId);
                    CollectionAssert.AreEqual(
                        liveLevel.Costs.Select(ToStableResourceId),
                        registeredCost.Costs
                            .Select(cost => cost.ResourceStableId),
                        reference.StableId);
                    CollectionAssert.AreEqual(
                        liveLevel.Costs.Select(cost => cost.Amount),
                        registeredCost.Costs.Select(cost => cost.Amount),
                        reference.StableId);
                    vectorCount++;
                }
            }

            Assert.AreEqual(150, vectorCount);
        }

        [Test]
        public void RegistryUsesCheckedArithmeticAndCarriesNoBlockedAuthority()
        {
            var sourcePath = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Data",
                "Catalogs",
                "SixFamily",
                "GameDataBuildingProgressionRegistry.cs");
            var source = File.ReadAllText(sourcePath);
            foreach (var expression in new[]
                     {
                         "checked(checked(baseBudget * scalePercent) + 99L)",
                         "checked(budget * shares[shareIndex].Percent)",
                         "checked(budget - assigned)",
                         "checked(assigned + amount)"
                     })
            {
                StringAssert.Contains(expression, source, expression);
            }

            var propertyNames =
                typeof(GameDataBuildingProgressionReference)
                    .GetProperties()
                    .Select(property => property.Name)
                    .ToArray();
            Assert.False(
                propertyNames.Any(name =>
                    name.IndexOf("Production", StringComparison.Ordinal) >= 0),
                "C4B must not define production profiles.");
            Assert.False(
                propertyNames.Any(name =>
                    name.IndexOf("Asset", StringComparison.Ordinal) >= 0),
                "C4B must not define asset authority.");
        }

        private static long[] ExpectedAmounts(
            long budget,
            IReadOnlyList<int> percentages)
        {
            var amounts = new long[percentages.Count];
            long assigned = 0L;
            for (var index = 0; index < percentages.Count; index++)
            {
                amounts[index] =
                    index == percentages.Count - 1
                        ? budget - assigned
                        : Math.Max(
                            1L,
                            budget * percentages[index] / 100L);
                assigned += amounts[index];
            }

            return amounts;
        }

        private static string ToStableResourceId(
            BuildingConstructionCostDefinition cost)
        {
            GameDataWalletResourceReference reference;
            Assert.True(
                GameDataWalletResourceReferences.TryGetByLegacyName(
                    cost.ResourceType.ToString(),
                    out reference),
                cost.ResourceType.ToString());
            return reference.StableId;
        }

        private static void AssertReadOnly<T>(
            IReadOnlyList<T> values,
            T replacement)
        {
            var list = (IList)values;
            Assert.True(list.IsReadOnly);
            if (values.Count > 0)
            {
                Assert.Throws<NotSupportedException>(
                    () => list[0] = replacement);
            }
            else
            {
                Assert.Throws<NotSupportedException>(
                    () => list.Add(replacement));
            }
        }
    }
}
