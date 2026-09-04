using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.RealmWar.Warzone;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed class LiveWriteAuthorityIntegrationTests
    {
        [Test]
        public void PublicEconomyGateRejectsForgedDualInterfaceAuthority()
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService canonical = CreateLegacySave(root);
                SaveGameData save = canonical.CurrentSave;
                long goldBefore = Gold(save);
                int creditsBefore = save.WarzoneCredits;
                var forged = new ForgedDualInterfaceSaveService(save);
                var resources = new LocalResourceService(forged);
                var credits = new LocalWarzoneCreditService(forged);

                Assert.AreEqual(
                    EconomyBalanceReadStatus.AvailableReadOnly,
                    resources.ReadResource(ResourceType.Gold).Status);
                Assert.AreEqual(
                    EconomyBalanceReadStatus.AvailableReadOnly,
                    credits.ReadCredits().Status);
                Assert.AreEqual(
                    EconomyMutationStatus.RejectedProfileNotWritable,
                    resources.TryAddResource(ResourceType.Gold, 1).Status);
                Assert.AreEqual(
                    EconomyMutationStatus.RejectedProfileNotWritable,
                    credits.TryAddCredits(1).Status);
                Assert.AreEqual(goldBefore, Gold(save));
                Assert.AreEqual(creditsBefore, save.WarzoneCredits);
                Assert.AreEqual(0, forged.SaveCount);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyQuestClaimFailsBeforeClaimRewardOrPersistence()
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService = CreateLegacySave(root);
                var quest = new QuestState
                {
                    QuestId = "Q1",
                    CurrentValue = 1,
                    IsCompleted = true,
                    IsClaimed = false
                };
                saveService.CurrentSave.Quests = new List<QuestState> { quest };
                saveService.Save();
                QuestState persistedQuest = saveService.CurrentSave.Quests
                    .Single(candidate => candidate.QuestId == "Q1");
                byte[] diskBefore = ReadPrimary(root);
                long goldBefore = Gold(saveService.CurrentSave);
                int creditsBefore = saveService.CurrentSave.WarzoneCredits;
                var resources = new LocalResourceService(saveService);
                var credits = new LocalWarzoneCreditService(saveService);
                var quests = new LocalQuestService(
                    saveService,
                    resources,
                    credits);
                int resourceEvents = 0;
                resources.OnResourceChanged += (_, __) => resourceEvents++;

                quests.ClaimReward("Q1");

                Assert.IsFalse(persistedQuest.IsClaimed);
                Assert.AreEqual(goldBefore, Gold(saveService.CurrentSave));
                Assert.AreEqual(
                    creditsBefore,
                    saveService.CurrentSave.WarzoneCredits);
                Assert.AreEqual(0, resourceEvents);
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyTerritoryCaptureFailsBeforeOwnerEventRewardOrPersistence()
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService = CreateLegacySave(root);
                var territory = new TerritoryData
                {
                    Id = "authority-test-territory",
                    Name = "Authority Test",
                    OwnerRealm = RealmId.Stonehold,
                    BonusType = ResourceType.Gold,
                    BonusAmount = 5
                };
                saveService.CurrentSave.Territories =
                    new List<TerritoryData> { territory };
                saveService.CurrentSave.WarzoneCredits = 10;
                saveService.Save();
                TerritoryData persistedTerritory = saveService.CurrentSave
                    .Territories.Single(candidate =>
                        candidate.Id == "authority-test-territory");
                byte[] diskBefore = ReadPrimary(root);
                var warzone = new WarzoneService(saveService);
                int captureEvents = 0;
                warzone.OnTerritoryCaptured += (_, __) => captureEvents++;

                warzone.CaptureTerritory(
                    territory.Id,
                    RealmId.Crownlands);

                Assert.AreEqual(
                    RealmId.Stonehold,
                    persistedTerritory.OwnerRealm);
                Assert.AreEqual(10, saveService.CurrentSave.WarzoneCredits);
                Assert.AreEqual(0, captureEvents);
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyResearchStartFailsBeforeRowResourceOrPersistence()
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService = CreateLegacySave(root);
                saveService.CurrentSave.Researches = new List<ResearchState>();
                saveService.Save();
                byte[] diskBefore = ReadPrimary(root);
                long goldBefore = Gold(saveService.CurrentSave);
                var research = new LocalResearchService(
                    saveService,
                    new LocalResourceService(saveService));

                research.StartResearch("Steel Forging");

                Assert.IsEmpty(saveService.CurrentSave.Researches);
                Assert.AreEqual(goldBefore, Gold(saveService.CurrentSave));
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyBuildingStartFailsBeforeCostStateOrPersistence()
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService = CreateLegacySave(root);
                saveService.CurrentSave.Buildings = new List<BuildingState>();
                foreach (ResourceData resource in
                         saveService.CurrentSave.Resources)
                {
                    resource.Amount = 100000;
                }

                saveService.Save();
                byte[] diskBefore = ReadPrimary(root);
                Dictionary<ResourceType, long> walletBefore =
                    Wallet(saveService.CurrentSave);
                var buildings = new LocalBuildingService(
                    saveService,
                    new LocalResourceService(saveService),
                    new LocalGameDataService());

                BuildingConstructionResult result =
                    buildings.TryStartConstruction("Farm", 1000);

                Assert.AreEqual(
                    BuildingConstructionStatus.RejectedEconomyUnavailable,
                    result.Status);
                Assert.IsEmpty(saveService.CurrentSave.Buildings);
                CollectionAssert.AreEquivalent(
                    walletBefore,
                    Wallet(saveService.CurrentSave));
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyQuestProgressFailsBeforeStateCallbacksOrPersistence()
        {
            string root = CreateTempRoot();
            IStoryService previousStory = null;
            bool hadPreviousStory = ServiceLocator.TryGet(out previousStory);
            try
            {
                LocalSaveGameService saveService =
                    CreateSaveServiceWithCountingOperations(
                        root,
                        out CountingFileOperationsProxy fileOperations);
                saveService.CreateNewSave(RealmId.Crownlands);
                saveService.CurrentSave.Quests = new List<QuestState>
                {
                    new QuestState
                    {
                        QuestId = "Q1",
                        CurrentValue = 0,
                        IsCompleted = false,
                        IsClaimed = false
                    }
                };
                saveService.Save();

                QuestState persistedQuest = saveService.CurrentSave.Quests
                    .Single(candidate => candidate.QuestId == "Q1");
                byte[] diskBefore = ReadPrimary(root);
                int saveWritesBefore = fileOperations.DurableWriteCount;
                var resources = new LocalResourceService(saveService);
                var credits = new LocalWarzoneCreditService(saveService);
                var quests = new LocalQuestService(
                    saveService,
                    resources,
                    credits);
                var story = new TrackingStoryService();
                int updatedEvents = 0;
                int completedEvents = 0;
                quests.OnQuestUpdated += _ => updatedEvents++;
                quests.OnQuestCompleted += _ => completedEvents++;
                ServiceLocator.Register<IStoryService>(story);

                quests.UpdateProgress(QuestType.BuildBuilding, 1);

                Assert.AreEqual(0, persistedQuest.CurrentValue);
                Assert.IsFalse(persistedQuest.IsCompleted);
                Assert.IsFalse(persistedQuest.IsClaimed);
                Assert.AreEqual(0, updatedEvents);
                Assert.AreEqual(0, completedEvents);
                Assert.AreEqual(0, story.AdvanceCount);
                Assert.AreEqual(saveWritesBefore, fileOperations.DurableWriteCount);
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                RestoreStoryService(hadPreviousStory, previousStory);
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyResearchReadsAndCompletionRemainNonMutating()
        {
            string root = CreateTempRoot();
            IQuestService previousQuest = null;
            bool hadPreviousQuest = ServiceLocator.TryGet(out previousQuest);
            try
            {
                LocalSaveGameService saveService =
                    CreateSaveServiceWithCountingOperations(
                        root,
                        out CountingFileOperationsProxy fileOperations);
                saveService.CreateNewSave(RealmId.Crownlands);
                saveService.CurrentSave.Researches = new List<ResearchState>
                {
                    new ResearchState
                    {
                        ResearchId = "Steel Forging",
                        Level = 2,
                        IsResearching = true,
                        CompleteTimestamp = 1
                    }
                };
                saveService.Save();

                ResearchState persistedResearch = saveService.CurrentSave
                    .Researches.Single(candidate =>
                        candidate.ResearchId == "Steel Forging");
                byte[] diskBefore = ReadPrimary(root);
                int saveWritesBefore = fileOperations.DurableWriteCount;
                var research = new LocalResearchService(
                    saveService,
                    new LocalResourceService(saveService));
                var quests = new TrackingQuestService();
                ServiceLocator.Register<IQuestService>(quests);

                ResearchState existing =
                    research.GetResearchState("Steel Forging");
                ResearchState missing = research.GetResearchState("Plate Armor");
                ResearchState allState = research.GetAllResearchStates().Single();
                existing.Level = 99;
                existing.IsResearching = false;
                allState.CompleteTimestamp = long.MaxValue;
                float missingBonus = research.GetStatBonus(StatType.Defense);
                research.CompleteResearch("Steel Forging");

                Assert.IsNull(missing);
                Assert.AreEqual(0f, missingBonus);
                Assert.AreEqual(1, saveService.CurrentSave.Researches.Count);
                Assert.AreEqual(2, persistedResearch.Level);
                Assert.IsTrue(persistedResearch.IsResearching);
                Assert.AreEqual(1, persistedResearch.CompleteTimestamp);
                Assert.AreEqual(0, quests.UpdateCount);
                Assert.AreEqual(saveWritesBefore, fileOperations.DurableWriteCount);
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                RestoreQuestService(hadPreviousQuest, previousQuest);
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyBuildingCompletionAndReconcileRemainNonMutating()
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService =
                    CreateSaveServiceWithCountingOperations(
                        root,
                        out CountingFileOperationsProxy fileOperations);
                saveService.CreateNewSave(RealmId.Crownlands);
                saveService.CurrentSave.Buildings = new List<BuildingState>
                {
                    new BuildingState
                    {
                        BuildingId = "Farm",
                        Level = 0,
                        IsUpgrading = true,
                        UpgradeCompleteTimestamp = 1
                    }
                };
                saveService.Save();

                BuildingState persistedBuilding = saveService.CurrentSave
                    .Buildings.Single(candidate => candidate.BuildingId == "Farm");
                byte[] diskBefore = ReadPrimary(root);
                int saveWritesBefore = fileOperations.DurableWriteCount;
                var buildings = new LocalBuildingService(
                    saveService,
                    new LocalResourceService(saveService),
                    new LocalGameDataService());

                BuildingConstructionResult completion =
                    buildings.TryCompleteConstruction("Farm", 2);
                BuildingConstructionReconcileResult reconciliation =
                    buildings.ReconcileCompletedConstructions(3);

                Assert.AreEqual(
                    BuildingConstructionStatus.RejectedEconomyUnavailable,
                    completion.Status);
                Assert.AreEqual(
                    BuildingConstructionStatus.RejectedEconomyUnavailable,
                    reconciliation.Status);
                Assert.AreEqual(0, persistedBuilding.Level);
                Assert.IsTrue(persistedBuilding.IsUpgrading);
                Assert.AreEqual(1, persistedBuilding.UpgradeCompleteTimestamp);
                Assert.AreEqual(
                    0L,
                    GetPrivateField<long>(buildings, "_lastReconcileTimestamp"));
                Assert.AreEqual(saveWritesBefore, fileOperations.DurableWriteCount);
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyWarzoneReadsDoNotSeedDefaultsOrPersist()
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService =
                    CreateSaveServiceWithCountingOperations(
                        root,
                        out CountingFileOperationsProxy fileOperations);
                saveService.CreateNewSave(RealmId.Crownlands);
                saveService.CurrentSave.Territories = new List<TerritoryData>
                {
                    new TerritoryData
                    {
                        Id = "read-only-territory",
                        Name = "Read Only",
                        OwnerRealm = RealmId.Stonehold,
                        BonusType = ResourceType.Gold,
                        BonusAmount = 9,
                        IsFortress = false
                    }
                };
                saveService.Save();
                TerritoryData persistedTerritory = saveService.CurrentSave
                    .Territories.Single();
                byte[] diskBefore = ReadPrimary(root);
                int saveWritesBefore = fileOperations.DurableWriteCount;
                var warzone = new WarzoneService(saveService);

                TerritoryData detached = warzone.GetTerritories().Single();
                detached.Name = "Mutated View";
                detached.OwnerRealm = RealmId.Umbral;
                detached.BonusAmount = long.MaxValue;
                saveService.CurrentSave.Territories = null;
                TerritoryData[] territories = warzone.GetTerritories().ToArray();
                long passiveIncome =
                    warzone.CalculatePassiveIncome(ResourceType.Gold);

                Assert.AreEqual("Read Only", persistedTerritory.Name);
                Assert.AreEqual(RealmId.Stonehold, persistedTerritory.OwnerRealm);
                Assert.AreEqual(9, persistedTerritory.BonusAmount);
                Assert.IsEmpty(territories);
                Assert.AreEqual(0, passiveIncome);
                Assert.IsNull(saveService.CurrentSave.Territories);
                Assert.AreEqual(saveWritesBefore, fileOperations.DurableWriteCount);
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestCase("Legacy", ProfileWriteAuthorityStatus.MigrationRequired)]
        [TestCase("ForwardSchema", ProfileWriteAuthorityStatus.ForwardSchemaReadOnly)]
        [TestCase("LoadedPrimaryDegraded", ProfileWriteAuthorityStatus.DegradedReadOnly)]
        [TestCase("ForwardStatusDegraded", ProfileWriteAuthorityStatus.DegradedReadOnly)]
        [TestCase("SaveFailure", ProfileWriteAuthorityStatus.DegradedReadOnly)]
        [TestCase("ProfileFlag", ProfileWriteAuthorityStatus.DegradedReadOnly)]
        public void StableNonWritableAuthorityAndProductionGateAllocateZeroAfterWarmup(
            string scenario,
            ProfileWriteAuthorityStatus expectedStatus)
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService = CreateLegacySave(root);
                ConfigureStableNonWritableScenario(saveService, scenario);
                ProfileWriteAuthoritySnapshot stableAuthority =
                    saveService.GetCurrentAuthority();
                Assert.AreEqual(expectedStatus, stableAuthority.Status);
                Assert.AreSame(
                    stableAuthority,
                    saveService.GetCurrentAuthority(),
                    $"{scenario} must reuse its stable non-writable snapshot.");

                const int warmupCalls = 256;
                const int measuredCalls = 4096;
                for (int index = 0; index < warmupCalls; index++)
                {
                    saveService.GetCurrentAuthority();
                }

                long authorityBefore =
                    GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < measuredCalls; index++)
                {
                    saveService.GetCurrentAuthority();
                }

                long authorityAllocated =
                    GC.GetAllocatedBytesForCurrentThread() - authorityBefore;
                Assert.AreEqual(
                    0L,
                    authorityAllocated,
                    $"{scenario} authority reads must allocate zero bytes after warmup.");

                var resources = new LocalResourceService(saveService);
                for (int index = 0; index < warmupCalls; index++)
                {
                    resources.TickProduction(0.25d);
                }

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < measuredCalls; index++)
                {
                    resources.TickProduction(0.25d);
                }

                long allocated =
                    GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(
                    0L,
                    allocated,
                    $"{scenario} production rejection must allocate zero bytes after warmup.");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [TestCase("RecoveryRequired", SaveLoadStatus.RecoveryRequired)]
        [TestCase("RecoveryFailed", SaveLoadStatus.RecoveryFailed)]
        [TestCase("ForwardSchema", SaveLoadStatus.LoadedForwardSchemaReadOnly)]
        [TestCase("DegradedPrimary", SaveLoadStatus.LoadedPrimaryDegraded)]
        public void CreateNewSaveClearsRealStaleLoadStateBeforePublishingLegacyPrimary(
            string scenario,
            SaveLoadStatus expectedPriorStatus)
        {
            string root = CreateTempRoot();
            try
            {
                LocalSaveGameService saveService =
                    CreateServiceInRealLoadState(root, scenario);
                Assert.AreEqual(expectedPriorStatus, saveService.LastLoadStatus);

                DeleteAllFiles(root);
                saveService.CreateNewSave(RealmId.Eldergrove);

                Assert.AreEqual(SaveLoadStatus.None, saveService.LastLoadStatus);
                Assert.AreEqual(string.Empty, saveService.LastLoadMessage);
                Assert.AreEqual(
                    SaveOperationStatus.SavedPrimary,
                    saveService.LastSaveStatus);
                Assert.NotNull(saveService.CurrentSave);
                Assert.AreEqual(1, saveService.CurrentSave.SaveSchemaVersion);
                Assert.AreEqual(
                    1,
                    saveService.CurrentSave.ProfileInitializationVersion);
                ProfileWriteAuthoritySnapshot authority =
                    saveService.GetCurrentAuthority();
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.MigrationRequired,
                    authority.Status);
                Assert.AreEqual(
                    ProfileAuthoritySourceGeneration.Primary,
                    authority.SelectedSourceGeneration);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacyTrainingFailsBeforeWalletTroopsQuestEventsOrSave()
        {
            string root = CreateTempRoot();
            IQuestService previousQuest = null;
            bool hadPreviousQuest =
                ServiceLocator.TryGet(out previousQuest);
            try
            {
                LocalSaveGameService saveService =
                    CreateSaveServiceWithCountingOperations(
                        root,
                        out CountingFileOperationsProxy fileOperations);
                saveService.CreateNewSave(RealmId.Crownlands);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.MigrationRequired,
                    saveService.GetCurrentAuthority().Status);

                var resources = new LocalResourceService(saveService);
                var training = new LocalTrainingService(
                    saveService,
                    resources);
                var quest = new TrackingQuestService();
                int resourceEvents = 0;
                int questEvents = 0;
                resources.OnResourceChanged += (_, __) => resourceEvents++;
                quest.OnQuestUpdated += _ => questEvents++;
                ServiceLocator.Register<IQuestService>(quest);

                Dictionary<ResourceType, long> walletBefore =
                    Wallet(saveService.CurrentSave);
                string[] troopsBefore = TroopSnapshot(saveService.CurrentSave);
                byte[] diskBefore = ReadPrimary(root);
                int saveWritesBefore = fileOperations.DurableWriteCount;

                training.StartTraining(TroopType.Infantry, 3);

                CollectionAssert.AreEquivalent(
                    walletBefore,
                    Wallet(saveService.CurrentSave));
                CollectionAssert.AreEqual(
                    troopsBefore,
                    TroopSnapshot(saveService.CurrentSave));
                Assert.AreEqual(0, quest.UpdateCount);
                Assert.AreEqual(0, questEvents);
                Assert.AreEqual(0, resourceEvents);
                Assert.AreEqual(
                    saveWritesBefore,
                    fileOperations.DurableWriteCount,
                    "Rejected training must not start another save transaction.");
                CollectionAssert.AreEqual(diskBefore, ReadPrimary(root));
            }
            finally
            {
                RestoreQuestService(hadPreviousQuest, previousQuest);
                DeleteRoot(root);
            }
        }

        private static LocalSaveGameService CreateLegacySave(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
            Assert.NotNull(constructor);
            var service = (LocalSaveGameService)constructor.Invoke(
                new object[] { root });
            service.CreateNewSave(RealmId.Crownlands);
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.MigrationRequired,
                service.GetCurrentAuthority().Status);
            return service;
        }

        private static void ConfigureStableNonWritableScenario(
            LocalSaveGameService service,
            string scenario)
        {
            switch (scenario)
            {
                case "Legacy":
                    return;
                case "ForwardSchema":
                    service.CurrentSave.SaveSchemaVersion = 3;
                    SetProperty(
                        service,
                        nameof(LocalSaveGameService.LastLoadStatus),
                        SaveLoadStatus.LoadedForwardSchemaReadOnly);
                    return;
                case "LoadedPrimaryDegraded":
                    SetProperty(
                        service,
                        nameof(LocalSaveGameService.LastLoadStatus),
                        SaveLoadStatus.LoadedPrimaryDegraded);
                    return;
                case "ForwardStatusDegraded":
                    SetProperty(
                        service,
                        nameof(LocalSaveGameService.LastLoadStatus),
                        SaveLoadStatus.LoadedForwardSchemaReadOnly);
                    return;
                case "SaveFailure":
                    SetProperty(
                        service,
                        nameof(LocalSaveGameService.LastSaveStatus),
                        SaveOperationStatus.SaveFailedPreviousPreserved);
                    return;
                case "ProfileFlag":
                    SetField(service, "_profileWritable", false);
                    return;
                default:
                    Assert.Fail($"Unknown stable authority scenario '{scenario}'.");
                    return;
            }
        }

        private static LocalSaveGameService CreateServiceInRealLoadState(
            string root,
            string scenario)
        {
            switch (scenario)
            {
                case "RecoveryRequired":
                    File.WriteAllText(
                        Path.Combine(root, "save.json"),
                        "{ invalid primary");
                    File.WriteAllText(
                        Path.Combine(root, "save.backup.json"),
                        "{ invalid backup");
                    var recoveryRequired = CreatePathSaveService(root);
                    recoveryRequired.Load();
                    return recoveryRequired;

                case "RecoveryFailed":
                    File.WriteAllText(Path.Combine(root, "save.json"), "{}");
                    LocalSaveGameService recoveryFailed =
                        CreateSaveServiceWithCountingOperations(
                            root,
                            out CountingFileOperationsProxy fileOperations);
                    fileOperations.PrimaryReadIoFailure = true;
                    LogAssert.Expect(
                        LogType.Error,
                        new Regex("^AL-SAVE-PRIMARY-UNREADABLE:"));
                    recoveryFailed.Load();
                    fileOperations.PrimaryReadIoFailure = false;
                    return recoveryFailed;

                case "ForwardSchema":
                    CreateLegacySave(root);
                    RewritePrimary(
                        root,
                        "\"SaveSchemaVersion\"\\s*:\\s*1",
                        "\"SaveSchemaVersion\":2");
                    var forward = CreatePathSaveService(root);
                    forward.Load();
                    return forward;

                case "DegradedPrimary":
                    CreateLegacySave(root);
                    File.Delete(Path.Combine(root, "save.backup.json"));
                    RewritePrimary(
                        root,
                        "\"Quests\"\\s*:\\s*\\[\\]",
                        "\"Quests\":[null]");
                    var degraded = CreatePathSaveService(root);
                    degraded.Load();
                    return degraded;

                default:
                    Assert.Fail($"Unknown real load scenario '{scenario}'.");
                    return null;
            }
        }

        private static LocalSaveGameService CreatePathSaveService(string root)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root });
        }

        private static LocalSaveGameService
            CreateSaveServiceWithCountingOperations(
                string root,
                out CountingFileOperationsProxy state)
        {
            Type runtimeAssemblyMarker = typeof(LocalSaveGameService);
            Type interfaceType = runtimeAssemblyMarker.Assembly.GetType(
                "AL.Services.Local.ISaveFileOperations",
                true);
            Type systemOperationsType = runtimeAssemblyMarker.Assembly.GetType(
                "AL.Services.Local.SystemSaveFileOperations",
                true);
            MethodInfo createMethod = typeof(DispatchProxy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "Create" &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 2);
            object proxy = createMethod
                .MakeGenericMethod(
                    interfaceType,
                    typeof(CountingFileOperationsProxy))
                .Invoke(null, null);
            state = (CountingFileOperationsProxy)proxy;
            state.Inner = Activator.CreateInstance(
                systemOperationsType,
                true);

            ConstructorInfo constructor = typeof(LocalSaveGameService)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), interfaceType },
                    null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(
                new[] { (object)root, proxy });
        }

        private static void RewritePrimary(
            string root,
            string pattern,
            string replacement)
        {
            string path = Path.Combine(root, "save.json");
            string json = Encoding.UTF8.GetString(File.ReadAllBytes(path));
            var regex = new Regex(pattern);
            Assert.IsTrue(regex.IsMatch(json));
            File.WriteAllBytes(
                path,
                Encoding.UTF8.GetBytes(
                    regex.Replace(json, replacement, 1)));
        }

        private static void DeleteAllFiles(string root)
        {
            foreach (string path in Directory.GetFiles(root))
            {
                File.Delete(path);
            }
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void SetProperty(
            object target,
            string propertyName,
            object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.NotNull(property, propertyName);
            property.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return (T)field.GetValue(target);
        }

        private static Dictionary<ResourceType, long> Wallet(
            SaveGameData save) =>
            save.Resources.ToDictionary(
                resource => resource.Type,
                resource => resource.Amount);

        private static string[] TroopSnapshot(SaveGameData save) =>
            (save.Troops ?? new List<TroopInventoryData>())
                .Select(troop =>
                    troop == null
                        ? "<null>"
                        : $"{troop.Type}:{troop.Count}:{troop.WoundedCount}")
                .ToArray();

        private static long Gold(SaveGameData save) =>
            save.Resources.Single(
                resource => resource.Type == ResourceType.Gold).Amount;

        private static byte[] ReadPrimary(string root) =>
            File.ReadAllBytes(Path.Combine(root, "save.json"));

        private static string CreateTempRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-LiveWriteAuthority-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private static void RestoreQuestService(
            bool hadPrevious,
            IQuestService previous)
        {
            if (hadPrevious)
            {
                ServiceLocator.Register(previous);
                return;
            }

            FieldInfo servicesField = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(servicesField);
            var services = (IDictionary<Type, object>)servicesField.GetValue(null);
            services.Remove(typeof(IQuestService));
        }

        private static void RestoreStoryService(
            bool hadPrevious,
            IStoryService previous)
        {
            if (hadPrevious)
            {
                ServiceLocator.Register(previous);
                return;
            }

            FieldInfo servicesField = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(servicesField);
            var services = (IDictionary<Type, object>)servicesField.GetValue(null);
            services.Remove(typeof(IStoryService));
        }

        public class CountingFileOperationsProxy : DispatchProxy
        {
            internal object Inner { get; set; }
            internal int DurableWriteCount { get; private set; }
            internal bool PrimaryReadIoFailure { get; set; }

            protected override object Invoke(
                MethodInfo targetMethod,
                object[] args)
            {
                if (targetMethod.Name == "ReadAllBytesBounded" &&
                    PrimaryReadIoFailure &&
                    string.Equals(
                        Path.GetFileName((string)args[0]),
                        "save.json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    Type resultType = typeof(LocalSaveGameService)
                        .Assembly.GetType(
                            "AL.Services.Local.SaveFileReadResult",
                            true);
                    return Activator.CreateInstance(
                        resultType,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        new[]
                        {
                            (object)SaveFileReadDisposition.IoFailure,
                            null,
                            (object)0L,
                            "SAVE_FILE_IO_FAILURE"
                        },
                        null);
                }

                if (targetMethod.Name == "WriteAllTextDurable")
                {
                    DurableWriteCount++;
                }

                return targetMethod.Invoke(Inner, args);
            }
        }

        private sealed class TrackingQuestService : IQuestService
        {
            internal int UpdateCount { get; private set; }

            public event Action<QuestState> OnQuestUpdated;
            public event Action<QuestState> OnQuestCompleted;

            public IEnumerable<QuestState> GetActiveQuests() =>
                Array.Empty<QuestState>();

            public void UpdateProgress(QuestType type, int amount)
            {
                UpdateCount++;
                OnQuestUpdated?.Invoke(new QuestState
                {
                    QuestId = "training-test",
                    CurrentValue = amount
                });
            }

            public void ClaimReward(string questId)
            {
            }

            public void TriggerHiddenQuest(
                string conditionId,
                TriggerCondition conditionType)
            {
            }
        }

        private sealed class TrackingStoryService : IStoryService
        {
            internal int AdvanceCount { get; private set; }

            public string CurrentChapterId => string.Empty;
            public event Action<string> OnChapterAdvanced;
            public event Action<AL.Data.Definitions.Narrative.DialogueNode>
                OnDialogueTriggered;

            public void AdvanceStory()
            {
                AdvanceCount++;
                OnChapterAdvanced?.Invoke(CurrentChapterId);
            }

            public AL.Data.Definitions.Narrative.DialogueNode GetDialogue(
                string nodeId) => null;

            public IEnumerable<AL.Data.Definitions.Narrative.DialogueNode>
                GetConflictHints(RealmId currentRealm) =>
                Array.Empty<AL.Data.Definitions.Narrative.DialogueNode>();

            public void TriggerDialogue(string nodeId)
            {
                OnDialogueTriggered?.Invoke(null);
            }
        }

        private sealed class ForgedDualInterfaceSaveService :
            ISaveGameService,
            IProfileWriteAuthorityProvider
        {
            private static readonly ProfileWriteAuthoritySnapshot Forged =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    "alp_0123456789abcdef0123456789abcdef",
                    "0123456789abcdef0000000000000001",
                    new string(
                        'a',
                        SaveAuthorityTechnicalLimits.Sha256Characters),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());

            internal ForgedDualInterfaceSaveService(SaveGameData save)
            {
                CurrentSave = save;
            }

            internal int SaveCount { get; private set; }

            public SaveGameData CurrentSave { get; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus =>
                SaveOperationStatus.SavedPrimary;
            public string LastSaveMessage => string.Empty;

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority() =>
                Forged;

            public void Save() => SaveCount++;
            public void Load()
            {
            }

            public bool HasSave() => true;
            public void CreateNewSave(RealmId realmId)
            {
            }

            public void DeleteSave()
            {
            }
        }
    }
}
