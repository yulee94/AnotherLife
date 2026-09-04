using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.Kingdom;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class ResearchTroopCatalogContainmentTests
    {
        [SetUp]
        public void ClearServiceLocator()
        {
            ServicesDictionary().Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServicesDictionary().Clear();
        }

        [Test]
        public void MissingResearchReadDoesNotSeedState()
        {
            var fixture = CreateResearchFixture();

            ResearchState missing = fixture.Research.GetResearchState("Steel Forging");

            Assert.That(missing, Is.Null);
            Assert.That(fixture.Save.CurrentSave.Researches, Is.Empty);
            Assert.That(fixture.Save.SaveCount, Is.Zero);
        }

        [Test]
        public void DuplicatePersistedResearchIdsAreNotSelected()
        {
            var fixture = CreateResearchFixture();
            fixture.Save.CurrentSave.Researches.Add(
                new ResearchState { ResearchId = "Steel Forging", Level = 1 });
            fixture.Save.CurrentSave.Researches.Add(
                new ResearchState { ResearchId = "Steel Forging", Level = 4 });

            ResearchState selected = fixture.Research.GetResearchState("Steel Forging");

            Assert.That(selected, Is.Null);
            Assert.That(fixture.Save.CurrentSave.Researches.Count, Is.EqualTo(2));
            Assert.That(fixture.Save.SaveCount, Is.Zero);
        }

        [Test]
        public void ResearchQueryReturnsCatalogUnavailableWithoutSeeding()
        {
            var fixture = CreateResearchFixture();

            object result = InvokeRequired(
                fixture.Research,
                "QueryResearch",
                new object[] { "Steel Forging" });

            AssertStatus(result, "CatalogUnavailable");
            Assert.That(GetString(result, "DiagnosticCode"), Is.EqualTo("AL-RSCH-CATALOG-UNAVAILABLE"));
            Assert.That(GetString(result, "Family"), Is.EqualTo("research"));
            Assert.That(fixture.Save.CurrentSave.Researches, Is.Empty);
            Assert.That(fixture.Save.SaveCount, Is.Zero);
        }

        [Test]
        public void BlankResearchQueryReturnsCatalogInvalid()
        {
            var fixture = CreateResearchFixture();

            foreach (string request in new[] { null, string.Empty, "  " })
            {
                object result = InvokeRequired(
                    fixture.Research,
                    "QueryResearch",
                    new object[] { request });
                AssertStatus(result, "CatalogInvalid");
                Assert.That(
                    GetString(result, "DiagnosticCode"),
                    Is.EqualTo("AL-RSCH-CATALOG-INVALID"),
                    request ?? "<null>");
                Assert.That(fixture.Research.GetResearchState(request), Is.Null);
            }

            Assert.That(fixture.Save.CurrentSave.Researches, Is.Empty);
        }

        [Test]
        public void HostileResearchIdsStayCatalogUnavailable()
        {
            var fixture = CreateResearchFixture();
            string[] hostile =
            {
                "steel_forging",
                "ManaShrine",
                "../research",
                "Steel Forging\n",
                new string('x', 4096)
            };

            foreach (string request in hostile)
            {
                object result = InvokeRequired(
                    fixture.Research,
                    "QueryResearch",
                    new object[] { request });
                AssertStatus(result, "CatalogUnavailable", request);
            }

            Assert.That(fixture.Save.CurrentSave.Researches, Is.Empty);
        }

        [Test]
        public void WritableResearchStartDoesNotMutateResourcesProgressionSaveQuestOrNotification()
        {
            var fixture = CreateWritableResearchFixture();
            var quests = new TrackingQuestService();
            ServiceLocator.Register<IQuestService>(quests);
            long goldBefore = Balance(fixture.Save.CurrentSave, ResourceType.Gold);

            object result = InvokeRequired(
                fixture.Research,
                "TryStartResearch",
                new object[] { "Steel Forging" });

            AssertStatus(result, "CatalogUnavailable");
            Assert.That(GetBool(result, "Changed"), Is.False);
            Assert.That(GetBool(result, "Persisted"), Is.False);
            Assert.That(fixture.Save.CurrentSave.Researches, Is.Empty);
            Assert.That(Balance(fixture.Save.CurrentSave, ResourceType.Gold), Is.EqualTo(goldBefore));
            Assert.That(fixture.Save.SaveCount, Is.Zero);
            Assert.That(quests.UpdateCount, Is.Zero);
            Assert.That(fixture.Research.GetStatBonus(StatType.Attack), Is.EqualTo(0f));
        }

        [Test]
        public void TroopQueryReturnsCatalogUnavailableWithoutSeeding()
        {
            var fixture = CreateTrainingFixture();

            object result = InvokeRequired(
                fixture.Training,
                "QueryTroop",
                new object[] { TroopType.Infantry });

            AssertStatus(result, "CatalogUnavailable");
            Assert.That(GetString(result, "DiagnosticCode"), Is.EqualTo("AL-TRP-CATALOG-UNAVAILABLE"));
            Assert.That(GetString(result, "Family"), Is.EqualTo("troops"));
            Assert.That(fixture.Save.CurrentSave.Troops, Is.Null.Or.Empty);
            Assert.That(fixture.Training.GetTroopCount(TroopType.Infantry), Is.Zero);
            Assert.That(fixture.Save.SaveCount, Is.Zero);
        }

        [Test]
        public void InvalidTroopQueryReturnsCatalogInvalid()
        {
            var fixture = CreateTrainingFixture();

            object result = InvokeRequired(
                fixture.Training,
                "QueryTroop",
                new object[] { (TroopType)99 });

            AssertStatus(result, "CatalogInvalid");
            Assert.That(GetString(result, "DiagnosticCode"), Is.EqualTo("AL-TRP-CATALOG-INVALID"));
            Assert.That(fixture.Save.CurrentSave.Troops, Is.Null.Or.Empty);
        }

        [Test]
        public void TrainingStartDoesNotMutateResourcesTroopsSaveOrQuest()
        {
            var fixture = CreateTrainingFixture();
            var quests = new TrackingQuestService();
            ServiceLocator.Register<IQuestService>(quests);
            long foodBefore = Balance(fixture.Save.CurrentSave, ResourceType.Food);

            object result = InvokeRequired(
                fixture.Training,
                "TryStartTraining",
                new[] { (object)TroopType.Infantry, 25 });

            AssertStatus(result, "CatalogUnavailable");
            Assert.That(GetBool(result, "Changed"), Is.False);
            Assert.That(GetBool(result, "Persisted"), Is.False);
            Assert.That(fixture.Save.CurrentSave.Troops, Is.Null.Or.Empty);
            Assert.That(Balance(fixture.Save.CurrentSave, ResourceType.Food), Is.EqualTo(foodBefore));
            Assert.That(fixture.Save.SaveCount, Is.Zero);
            Assert.That(quests.UpdateCount, Is.Zero);
        }

        [Test]
        public void OldSaveResearchAndTroopRowsStayUnmutatedAndUnavailable()
        {
            var researchFixture = CreateWritableResearchFixture();
            researchFixture.Save.CurrentSave.Researches.Add(
                new ResearchState
                {
                    ResearchId = "Steel Forging",
                    Level = 2,
                    IsResearching = true,
                    CompleteTimestamp = 1
                });
            var trainingFixture = CreateTrainingFixture(researchFixture.Save);
            trainingFixture.Save.CurrentSave.Troops = new List<TroopInventoryData>
            {
                new TroopInventoryData { Type = TroopType.Infantry, Count = 12, WoundedCount = 1 }
            };
            long goldBefore = Balance(researchFixture.Save.CurrentSave, ResourceType.Gold);
            long foodBefore = Balance(researchFixture.Save.CurrentSave, ResourceType.Food);

            ResearchState persisted = researchFixture.Research.GetResearchState("Steel Forging");
            object researchQuery = InvokeRequired(
                researchFixture.Research,
                "QueryResearch",
                new object[] { "Steel Forging" });
            object researchStart = InvokeRequired(
                researchFixture.Research,
                "TryStartResearch",
                new object[] { "Steel Forging" });
            object researchComplete = InvokeRequired(
                researchFixture.Research,
                "TryCompleteResearch",
                new object[] { "Steel Forging" });
            object troopQuery = InvokeRequired(
                trainingFixture.Training,
                "QueryTroop",
                new object[] { TroopType.Infantry });
            object troopStart = InvokeRequired(
                trainingFixture.Training,
                "TryStartTraining",
                new[] { (object)TroopType.Infantry, 3 });

            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted.Level, Is.EqualTo(2));
            persisted.Level = 99;
            Assert.That(
                researchFixture.Save.CurrentSave.Researches.Single().Level,
                Is.EqualTo(2));
            AssertStatus(researchQuery, "CatalogUnavailable");
            AssertStatus(researchStart, "CatalogUnavailable");
            AssertStatus(researchComplete, "CatalogUnavailable");
            AssertStatus(troopQuery, "CatalogUnavailable");
            AssertStatus(troopStart, "CatalogUnavailable");
            Assert.That(researchFixture.Save.CurrentSave.Researches.Single().IsResearching, Is.True);
            Assert.That(trainingFixture.Save.CurrentSave.Troops.Single().Count, Is.EqualTo(12));
            Assert.That(Balance(researchFixture.Save.CurrentSave, ResourceType.Gold), Is.EqualTo(goldBefore));
            Assert.That(Balance(researchFixture.Save.CurrentSave, ResourceType.Food), Is.EqualTo(foodBefore));
            Assert.That(researchFixture.Save.SaveCount, Is.Zero);
        }

        [Test]
        public void MissingResearchPresentationIsUnavailableNotLevelZero()
        {
            MethodInfo formatter = typeof(KingdomSceneController).GetMethod(
                "FormatResearch",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(formatter, Is.Not.Null);
            string text = (string)formatter.Invoke(null, new object[] { "Steel Forging", null });
            Assert.That(text, Does.Contain("UNAVAILABLE"));
            Assert.That(text, Does.Not.Contain("Level 0"));
        }

        [Test]
        public void DemoMusterDoesNotAnnounceSuccessWithoutTypedResult()
        {
            string source = File.ReadAllText(
                Path.Combine(
                    UnityEngine.Application.dataPath,
                    "AL",
                    "Scripts",
                    "Utilities",
                    "DemoInitializer.cs"));
            Assert.That(source, Does.Contain("TryStartTraining"));
            Assert.That(source, Does.Not.Contain("Infantry muster order resolved"));
        }

        [Test]
        public void CompatibilityStartWrappersDoNotSeedOrSpend()
        {
            var research = CreateWritableResearchFixture();
            var training = CreateTrainingFixture();
            long goldBefore = Balance(research.Save.CurrentSave, ResourceType.Gold);
            long foodBefore = Balance(training.Save.CurrentSave, ResourceType.Food);

            research.Research.StartResearch("Steel Forging");
            research.Research.CompleteResearch("Steel Forging");
            training.Training.StartTraining(TroopType.Infantry, 25);
            training.Training.CompleteTraining(TroopType.Infantry);

            Assert.That(research.Save.CurrentSave.Researches, Is.Empty);
            Assert.That(training.Save.CurrentSave.Troops, Is.Null.Or.Empty);
            Assert.That(Balance(research.Save.CurrentSave, ResourceType.Gold), Is.EqualTo(goldBefore));
            Assert.That(Balance(training.Save.CurrentSave, ResourceType.Food), Is.EqualTo(foodBefore));
            Assert.That(research.Save.SaveCount, Is.Zero);
            Assert.That(training.Save.SaveCount, Is.Zero);
        }

        private static ResearchFixture CreateResearchFixture()
        {
            var save = new FakeSaveGameService();
            save.CurrentSave.Researches = new List<ResearchState>();
            save.CurrentSave.Resources = CreateWallet();
            var resources = new LocalResourceService(save);
            var research = new LocalResearchService(save, resources);
            return new ResearchFixture(save, research, resources);
        }

        private static ResearchFixture CreateWritableResearchFixture()
        {
            var save = new FakeSaveGameService();
            save.CurrentSave.Researches = new List<ResearchState>();
            save.CurrentSave.Resources = CreateWallet();
            LocalResourceService resources = CreateWritableResourceService(save);
            LocalResearchService research = CreateWritableResearchService(save, resources);
            return new ResearchFixture(save, research, resources);
        }

        private static TrainingFixture CreateTrainingFixture(FakeSaveGameService save = null)
        {
            save ??= new FakeSaveGameService();
            save.CurrentSave.Resources = CreateWallet();
            var resources = new LocalResourceService(save);
            var training = new LocalTrainingService(save, resources);
            return new TrainingFixture(save, training);
        }

        private static LocalResearchService CreateWritableResearchService(
            ISaveGameService save,
            IResourceService resources)
        {
            Type gateType = typeof(LocalResourceService).Assembly.GetType(
                "AL.Services.Local.EconomyWriteAuthorityGate",
                true);
            ConstructorInfo constructor = typeof(LocalResearchService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(ISaveGameService),
                    typeof(IResourceService),
                    gateType
                },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (LocalResearchService)constructor.Invoke(
                new[]
                {
                    save,
                    resources,
                    CreateWritableGate(save)
                });
        }

        private static LocalResourceService CreateWritableResourceService(ISaveGameService save)
        {
            Type gateType = typeof(LocalResourceService).Assembly.GetType(
                "AL.Services.Local.EconomyWriteAuthorityGate",
                true);
            ConstructorInfo constructor = typeof(LocalResourceService).GetConstructor(
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
                    CreateWritableGate(save),
                    null
                });
        }

        private static object CreateWritableGate(ISaveGameService save)
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
            return constructor.Invoke(new object[] { save, new WritableAuthorityProvider() });
        }

        private static object InvokeRequired(object target, string methodName, object[] args)
        {
            Type[] types = args.Select(argument => argument == null ? typeof(string) : argument.GetType())
                .ToArray();
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                types,
                null);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, args);
        }

        private static void AssertStatus(object result, string expected, string context = null)
        {
            Assert.That(result, Is.Not.Null, context);
            object status = result.GetType().GetProperty("Status").GetValue(result);
            Assert.That(status.ToString(), Is.EqualTo(expected), context);
        }

        private static string GetString(object result, string property)
        {
            return (string)result.GetType().GetProperty(property).GetValue(result);
        }

        private static bool GetBool(object result, string property)
        {
            return (bool)result.GetType().GetProperty(property).GetValue(result);
        }

        private static List<ResourceData> CreateWallet()
        {
            return new List<ResourceData>
            {
                new ResourceData { Type = ResourceType.Gold, Amount = 100000 },
                new ResourceData { Type = ResourceType.Food, Amount = 100000 },
                new ResourceData { Type = ResourceType.Wood, Amount = 100000 },
                new ResourceData { Type = ResourceType.Stone, Amount = 100000 }
            };
        }

        private static long Balance(SaveGameData save, ResourceType resourceType)
        {
            return save.Resources.Single(resource => resource.Type == resourceType).Amount;
        }

        private static IDictionary<Type, object> ServicesDictionary()
        {
            FieldInfo field = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IDictionary<Type, object>)field.GetValue(null);
        }

        private sealed class ResearchFixture
        {
            public ResearchFixture(
                FakeSaveGameService save,
                LocalResearchService research,
                LocalResourceService resources)
            {
                Save = save;
                Research = research;
                Resources = resources;
            }

            public FakeSaveGameService Save { get; }
            public LocalResearchService Research { get; }
            public LocalResourceService Resources { get; }
        }

        private sealed class TrainingFixture
        {
            public TrainingFixture(FakeSaveGameService save, LocalTrainingService training)
            {
                Save = save;
                Training = training;
            }

            public FakeSaveGameService Save { get; }
            public LocalTrainingService Training { get; }
        }

        private sealed class WritableAuthorityProvider : IProfileWriteAuthorityProvider
        {
            private static readonly ProfileWriteAuthoritySnapshot Snapshot =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    "alp_0123456789abcdef0123456789abcdef",
                    "0123456789abcdef0000000000000001",
                    new string('a', SaveAuthorityTechnicalLimits.Sha256Characters),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority() => Snapshot;
        }

        private sealed class TrackingQuestService : IQuestService
        {
            internal int UpdateCount { get; private set; }

            public event Action<QuestState> OnQuestUpdated;
            public event Action<QuestState> OnQuestCompleted;

            public IEnumerable<QuestState> GetActiveQuests() => Array.Empty<QuestState>();

            public void UpdateProgress(QuestType type, int amount)
            {
                UpdateCount++;
                OnQuestUpdated?.Invoke(new QuestState { QuestId = "containment", CurrentValue = amount });
            }

            public void ClaimReward(string questId)
            {
            }

            public void TriggerHiddenQuest(string conditionId, TriggerCondition conditionType)
            {
            }
        }

        private sealed class FakeSaveGameService : ISaveGameService
        {
            public SaveGameData CurrentSave { get; set; } =
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
            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
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
