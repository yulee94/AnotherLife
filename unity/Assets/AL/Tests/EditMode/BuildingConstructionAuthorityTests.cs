using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class BuildingConstructionAuthorityTests
    {
        [Test]
        public void GameDataDefinesEveryLevelForEverySupportedBuilding()
        {
            var gameData = new LocalGameDataService();
            string[] buildingIds =
            {
                "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine",
                "Barracks", "Academy", "Market", "Storehouse", "Forge",
                "Stable", "Workshop", "Embassy", "Wall", "Watchtower"
            };

            foreach (string buildingId in buildingIds)
            {
                BuildingDefinition definition = gameData.GetBuilding(buildingId);
                Assert.That(definition, Is.Not.Null, buildingId);
                Assert.That(definition.MaxLevel, Is.EqualTo(10), buildingId);
                Assert.That(
                    definition.ConstructionLevels.Select(level => level.TargetLevel),
                    Is.EqualTo(Enumerable.Range(1, 10)),
                    buildingId);
                Assert.That(
                    definition.ConstructionLevels.All(level =>
                        level.DurationSeconds > 0 &&
                        level.Costs != null &&
                        level.Costs.Count >= 2 &&
                        level.Costs.All(cost => cost.Amount > 0)),
                    Is.True,
                    buildingId);
            }
        }

        [Test]
        public void LiveCommandDefinitionsMatchApprovedLevelOneToTenTuning()
        {
            var gameData = new LocalGameDataService();
            int[] baseBudgets =
            {
                100, 175, 300, 475, 700, 1000, 1400, 1900, 2500, 3250
            };
            int[] durations =
            {
                10, 30, 120, 300, 900, 1800, 3600, 7200, 14400, 28800
            };

            AssertProfile(
                gameData,
                "TownHall",
                140,
                baseBudgets,
                durations,
                ResourceType.Stone,
                ResourceType.Wood,
                ResourceType.Gold);
            AssertProfile(
                gameData,
                "Farm",
                80,
                baseBudgets,
                durations,
                ResourceType.Wood,
                ResourceType.Stone);
            AssertProfile(
                gameData,
                "LumberMill",
                80,
                baseBudgets,
                durations,
                ResourceType.Wood,
                ResourceType.Stone);
            AssertProfile(
                gameData,
                "Quarry",
                90,
                baseBudgets,
                durations,
                ResourceType.Wood,
                ResourceType.Stone);
            AssertProfile(
                gameData,
                "GoldMine",
                100,
                baseBudgets,
                durations,
                ResourceType.Wood,
                ResourceType.Stone);
            AssertProfile(
                gameData,
                "Barracks",
                110,
                baseBudgets,
                durations,
                ResourceType.Stone,
                ResourceType.Wood,
                ResourceType.Gold);
        }

        [Test]
        public void MissingStateQuotesLevelZeroWithoutSeeding()
        {
            var fixture = CreateFixture();

            BuildingConstructionQuote quote =
                fixture.Buildings.GetConstructionQuote("Farm");

            Assert.That(quote.Status, Is.EqualTo(BuildingConstructionStatus.Available));
            Assert.That(quote.ConfirmedLevel, Is.Zero);
            Assert.That(quote.TargetLevel, Is.EqualTo(1));
            Assert.That(fixture.Save.CurrentSave.Buildings, Is.Empty);
            Assert.That(fixture.Buildings.GetBuildingState("Farm"), Is.Null);
            Assert.That(fixture.Save.CurrentSave.Buildings, Is.Empty);
        }

        [Test]
        public void StartConstructionSpendsDefinitionCostsAndPersistsOneActiveOrder()
        {
            var fixture = CreateFixture();
            BuildingConstructionQuote quote =
                fixture.Buildings.GetConstructionQuote("TownHall");
            Dictionary<ResourceType, long> before = Balances(fixture.Save.CurrentSave);

            BuildingConstructionResult result =
                fixture.Buildings.TryStartConstruction("TownHall", 1000);

            Assert.That(result.Status, Is.EqualTo(BuildingConstructionStatus.Started));
            Assert.That(result.Changed, Is.True);
            Assert.That(result.Persisted, Is.True);
            Assert.That(fixture.Save.SaveCount, Is.EqualTo(1));
            BuildingState state = fixture.Save.CurrentSave.Buildings.Single();
            Assert.That(state.BuildingId, Is.EqualTo("TownHall"));
            Assert.That(state.Level, Is.Zero);
            Assert.That(state.IsUpgrading, Is.True);
            Assert.That(
                state.UpgradeCompleteTimestamp,
                Is.EqualTo(1000 + quote.DurationSeconds));
            foreach (BuildingConstructionCost cost in quote.Costs)
            {
                Assert.That(
                    Balance(fixture.Save.CurrentSave, cost.ResourceType),
                    Is.EqualTo(before[cost.ResourceType] - cost.Amount));
            }
        }

        [Test]
        public void RepeatedStartDoesNotSpendAgain()
        {
            var fixture = CreateFixture();
            Assert.That(
                fixture.Buildings.TryStartConstruction("Farm", 1000).Status,
                Is.EqualTo(BuildingConstructionStatus.Started));
            Dictionary<ResourceType, long> afterFirst = Balances(fixture.Save.CurrentSave);

            BuildingConstructionResult repeated =
                fixture.Buildings.TryStartConstruction("Farm", 1001);

            Assert.That(
                repeated.Status,
                Is.EqualTo(BuildingConstructionStatus.AlreadyInProgress));
            Assert.That(Balances(fixture.Save.CurrentSave), Is.EqualTo(afterFirst));
            Assert.That(fixture.Save.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void KnownSaveFailureRollsBackResourcesAndBuildingState()
        {
            var fixture = CreateFixture();
            fixture.Save.NextSaveStatus =
                SaveOperationStatus.SaveFailedPreviousPreserved;
            Dictionary<ResourceType, long> before = Balances(fixture.Save.CurrentSave);

            BuildingConstructionResult result =
                fixture.Buildings.TryStartConstruction("Barracks", 1000);

            Assert.That(
                result.Status,
                Is.EqualTo(BuildingConstructionStatus.SaveFailedRolledBack));
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Persisted, Is.False);
            Assert.That(fixture.Save.CurrentSave.Buildings, Is.Empty);
            Assert.That(Balances(fixture.Save.CurrentSave), Is.EqualTo(before));
        }

        [Test]
        public void PartialMultiResourceSpendIsRolledBackWhenLaterCostIsUnavailable()
        {
            var fixture = CreateFixture();
            fixture.Save.CurrentSave.Resources
                .Single(resource => resource.Type == ResourceType.Stone)
                .Amount = 0;
            Dictionary<ResourceType, long> before = Balances(fixture.Save.CurrentSave);

            BuildingConstructionResult result =
                fixture.Buildings.TryStartConstruction("Farm", 1000);

            Assert.That(
                result.Status,
                Is.EqualTo(BuildingConstructionStatus.RejectedInsufficientResources));
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Persisted, Is.False);
            Assert.That(fixture.Save.SaveCount, Is.Zero);
            Assert.That(fixture.Save.CurrentSave.Buildings, Is.Empty);
            Assert.That(Balances(fixture.Save.CurrentSave), Is.EqualTo(before));
        }

        [Test]
        public void CommitUncertainKeepsCandidateAndBlocksFurtherOrders()
        {
            var fixture = CreateFixture();
            fixture.Save.NextSaveStatus = SaveOperationStatus.CommitUncertain;
            Dictionary<ResourceType, long> before = Balances(fixture.Save.CurrentSave);
            BuildingConstructionQuote quote =
                fixture.Buildings.GetConstructionQuote("Farm");

            BuildingConstructionResult uncertain =
                fixture.Buildings.TryStartConstruction("Farm", 1000);
            BuildingConstructionResult blocked =
                fixture.Buildings.TryStartConstruction("Barracks", 1001);

            Assert.That(
                uncertain.Status,
                Is.EqualTo(BuildingConstructionStatus.CommitUncertain));
            Assert.That(uncertain.Changed, Is.True);
            Assert.That(uncertain.Persisted, Is.False);
            Assert.That(
                blocked.Status,
                Is.EqualTo(BuildingConstructionStatus.CommitUncertain));
            Assert.That(fixture.Save.CurrentSave.Buildings.Single().BuildingId, Is.EqualTo("Farm"));
            foreach (BuildingConstructionCost cost in quote.Costs)
            {
                Assert.That(
                    Balance(fixture.Save.CurrentSave, cost.ResourceType),
                    Is.EqualTo(before[cost.ResourceType] - cost.Amount));
            }
        }

        [Test]
        public void CompletionUsesAuthoritativeDeadlineAndClearsActiveOrder()
        {
            var fixture = CreateFixture();
            BuildingConstructionResult started =
                fixture.Buildings.TryStartConstruction("Farm", 1000);
            long deadline = started.Quote.CompleteTimestamp;

            BuildingConstructionResult early =
                fixture.Buildings.TryCompleteConstruction("Farm", deadline - 1);
            BuildingConstructionResult completed =
                fixture.Buildings.TryCompleteConstruction("Farm", deadline);

            Assert.That(early.Status, Is.EqualTo(BuildingConstructionStatus.NotReady));
            Assert.That(completed.Status, Is.EqualTo(BuildingConstructionStatus.Completed));
            Assert.That(completed.Persisted, Is.True);
            BuildingState state = fixture.Save.CurrentSave.Buildings.Single();
            Assert.That(state.Level, Is.EqualTo(1));
            Assert.That(state.IsUpgrading, Is.False);
            Assert.That(state.UpgradeCompleteTimestamp, Is.Zero);
            Assert.That(fixture.Save.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void RuntimeReconciliationCompletesAllDueOrdersInOneSave()
        {
            var fixture = CreateFixture();
            fixture.Save.CurrentSave.Buildings.Add(
                new BuildingState
                {
                    BuildingId = "Farm",
                    Level = 0,
                    IsUpgrading = true,
                    UpgradeCompleteTimestamp = 1000
                });
            fixture.Save.CurrentSave.Buildings.Add(
                new BuildingState
                {
                    BuildingId = "Barracks",
                    Level = 1,
                    IsUpgrading = true,
                    UpgradeCompleteTimestamp = 1000
                });

            BuildingConstructionReconcileResult result =
                fixture.Buildings.ReconcileCompletedConstructions(1000);

            Assert.That(result.Status, Is.EqualTo(BuildingConstructionStatus.Completed));
            Assert.That(result.CompletedBuildingIds, Is.EquivalentTo(new[] { "Farm", "Barracks" }));
            Assert.That(fixture.Save.SaveCount, Is.EqualTo(1));
            Assert.That(
                fixture.Save.CurrentSave.Buildings.Single(state => state.BuildingId == "Farm").Level,
                Is.EqualTo(1));
            Assert.That(
                fixture.Save.CurrentSave.Buildings.Single(state => state.BuildingId == "Barracks").Level,
                Is.EqualTo(2));
        }

        [Test]
        public void MaxLevelAndUnsupportedSlotsFailClosed()
        {
            var fixture = CreateFixture();
            fixture.Save.CurrentSave.Buildings.Add(
                new BuildingState
                {
                    BuildingId = "TownHall",
                    Level = 10
                });

            Assert.That(
                fixture.Buildings.GetConstructionQuote("TownHall").Status,
                Is.EqualTo(BuildingConstructionStatus.MaxLevel));
            Assert.That(
                fixture.Buildings.GetConstructionQuote("ManaShrine").Status,
                Is.EqualTo(BuildingConstructionStatus.RejectedUnsupportedBuilding));
        }

        private static Fixture CreateFixture()
        {
            var save = new FakeSaveGameService();
            foreach (ResourceType resourceType in ResourceRules.WalletResources)
            {
                save.CurrentSave.Resources.Add(
                    new ResourceData
                    {
                        Type = resourceType,
                        Amount = ResourceRules.IsRareResource(resourceType) ? 0 : 100000
                    });
            }

            LocalResourceService resources =
                CreateWritableResourceServiceForTests(save);
            LocalBuildingService buildings =
                CreateWritableBuildingServiceForTests(
                    save,
                    resources,
                    new LocalGameDataService());
            return new Fixture(save, buildings);
        }

        private static LocalBuildingService
            CreateWritableBuildingServiceForTests(
                ISaveGameService save,
                IResourceService resources,
                IGameDataService gameData)
        {
            Type gateType = typeof(LocalResourceService).Assembly.GetType(
                "AL.Services.Local.EconomyWriteAuthorityGate",
                true);
            ConstructorInfo constructor = typeof(LocalBuildingService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(ISaveGameService),
                        typeof(IResourceService),
                        typeof(IGameDataService),
                        gateType
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            return (LocalBuildingService)constructor.Invoke(
                new[]
                {
                    save,
                    resources,
                    gameData,
                    CreateWritableGateForTests(save)
                });
        }

        private static LocalResourceService
            CreateWritableResourceServiceForTests(ISaveGameService save)
        {
            Type gateType = typeof(LocalResourceService).Assembly.GetType(
                "AL.Services.Local.EconomyWriteAuthorityGate",
                true);
            ConstructorInfo constructor = typeof(LocalResourceService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(ISaveGameService),
                        gateType,
                        typeof(IEconomyProductionContributionProvider)
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            return (LocalResourceService)constructor.Invoke(
                new object[]
                {
                    save,
                    CreateWritableGateForTests(save),
                    null
                });
        }

        private static object CreateWritableGateForTests(
            ISaveGameService save)
        {
            Type gateType = typeof(LocalResourceService).Assembly.GetType(
                "AL.Services.Local.EconomyWriteAuthorityGate",
                true);
            ConstructorInfo constructor = gateType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(ISaveGameService),
                    typeof(IProfileWriteAuthorityProvider)
                },
                null);
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(
                new object[] { save, new WritableAuthorityProvider() });
        }

        private sealed class WritableAuthorityProvider :
            IProfileWriteAuthorityProvider
        {
            private static readonly ProfileWriteAuthoritySnapshot Snapshot =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    "alp_0123456789abcdef0123456789abcdef",
                    "0123456789abcdef0000000000000001",
                    new string(
                        'a',
                        SaveAuthorityTechnicalLimits.Sha256Characters),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority() =>
                Snapshot;
        }

        private static void AssertProfile(
            LocalGameDataService gameData,
            string buildingId,
            int scalePercent,
            IReadOnlyList<int> baseBudgets,
            IReadOnlyList<int> durations,
            params ResourceType[] resourceTypes)
        {
            BuildingDefinition definition = gameData.GetBuilding(buildingId);
            Assert.That(definition, Is.Not.Null, buildingId);
            Assert.That(definition.ConstructionLevels.Count, Is.EqualTo(10), buildingId);
            for (int index = 0; index < definition.ConstructionLevels.Count; index++)
            {
                BuildingConstructionLevelDefinition level =
                    definition.ConstructionLevels[index];
                long expectedBudget =
                    (baseBudgets[index] * (long)scalePercent + 99L) / 100L;
                Assert.That(level.TargetLevel, Is.EqualTo(index + 1), buildingId);
                Assert.That(level.DurationSeconds, Is.EqualTo(durations[index]), buildingId);
                Assert.That(
                    level.Costs.Select(cost => cost.ResourceType),
                    Is.EqualTo(resourceTypes),
                    buildingId);
                Assert.That(
                    level.Costs.Sum(cost => cost.Amount),
                    Is.EqualTo(expectedBudget),
                    buildingId);
            }
        }

        private static Dictionary<ResourceType, long> Balances(SaveGameData save)
        {
            return save.Resources.ToDictionary(resource => resource.Type, resource => resource.Amount);
        }

        private static long Balance(SaveGameData save, ResourceType resourceType)
        {
            return save.Resources.Single(resource => resource.Type == resourceType).Amount;
        }

        private sealed class Fixture
        {
            public Fixture(
                FakeSaveGameService save,
                LocalBuildingService buildings)
            {
                Save = save;
                Buildings = buildings;
            }

            public FakeSaveGameService Save { get; }
            public LocalBuildingService Buildings { get; }
        }

        private sealed class FakeSaveGameService : ISaveGameService
        {
            public SaveGameData CurrentSave { get; private set; } =
                new SaveGameData
                {
                    SaveFormatId = SaveGameData.CurrentSaveFormatId,
                    SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
                    ProfileInitializationVersion =
                        SaveGameData.CurrentProfileInitializationVersion
                };

            public SaveLoadStatus LastLoadStatus { get; private set; } =
                SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage { get; private set; } = string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; } =
                SaveOperationStatus.SavedPrimary;
            public string LastSaveMessage { get; private set; } = string.Empty;
            public SaveOperationStatus NextSaveStatus { get; set; } =
                SaveOperationStatus.SavedPrimary;
            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
                LastSaveStatus = NextSaveStatus;
            }

            public void Load()
            {
                LastLoadStatus = SaveLoadStatus.LoadedPrimary;
            }

            public bool HasSave() => CurrentSave != null;

            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }

            public void DeleteSave()
            {
                CurrentSave = null;
            }
        }
    }
}
