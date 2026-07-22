using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    // Containment corrections for #178 (release-controller mutation containment). These tests prove the
    // corrected KingdomSceneController / ChampionArenaSceneController / KingdomCommandPolicy behavior:
    //   * blocking-issue diagnostics match accepted reconnection contracts rather than transient
    //     GitHub open/closed state (#137 is required for every mutation family except Champion and
    //     permanently-unavailable catalog entries remain per D9);
    //   * the hidden per-second progression, the controller-owned load, and the Champion proximity
    //     credit grant are gone from production source;
    //   * building the read-only UI and refreshing/updating/toggling it (and selecting every command)
    //     seeds no profile state and never loads or saves (isolated real-service + controllable-save
    //     spy, save-state equality).
    //
    // The test assembly cannot reference the predefined Assembly-CSharp, so all production types are
    // reached by reflection, mirroring BootloaderServiceStackIntegrityTests.
    public sealed class KingdomReleaseContainmentTests
    {
        private const int SaveHardeningIssue = 137;

        [SetUp]
        public void ClearServiceLocator()
        {
            ServicesDictionary().Clear();
        }

        [TearDown]
        public void TearDown()
        {
            SetStackOverride("GameDataFactoryOverride", null);
            SetStackOverride("SaveGameFactoryOverride", null);
            SetStackOverride("ResourceFactoryOverride", null);
            SetStackOverride("NotificationFactoryOverride", null);
            SetStackOverride("BossLootFactoryOverride", null);
            ServicesDictionary().Clear();
        }

        // ---------------------------------------------------------------------
        // (1) Blocking-issue diagnostics reflect accepted reconnection contracts
        // ---------------------------------------------------------------------

        [Test]
        public void BlockingIssueIdsMatchAcceptedReconnectionContracts()
        {
            object context = CommittedRealmContext();

            AssertBlockers(context, "TownHallUpgrade", 137, 163, 165, 183);
            AssertBlockers(context, "FarmUpgrade", 137, 163, 165, 183);
            AssertBlockers(context, "LumberMillUpgrade", 137, 163, 165, 183);
            AssertBlockers(context, "QuarryUpgrade", 137, 163, 165, 183);
            AssertBlockers(context, "GoldMineUpgrade", 137, 163, 165, 183);
            AssertBlockers(context, "ManaShrineUpgrade", 137, 163, 165, 183);
            AssertBlockers(context, "MineUpgrade", 137, 163, 165, 183);
            AssertBlockers(context, "InfantryTraining", 137, 163, 165, 183);
            AssertBlockers(context, "RangedTraining", 137, 163, 165, 183);
            AssertBlockers(context, "QuestClaim", 133, 137, 152);
            AssertBlockers(context, "SteelResearch", 137, 163, 165, 183);
            AssertBlockers(context, "ArmorResearch", 137, 163, 165, 183);
            AssertBlockers(context, "WarmasterPurchase", 137, 163, 171, 183);
            AssertBlockers(context, "BorderlandsCapture", 137, 163, 166, 173);
            AssertBlockers(context, "ChampionDeploy", 150, 173, 180);
        }

        [Test]
        public void EveryMutationFamilyExceptChampionDeclaresSaveHardeningDependency()
        {
            string championId = CommandId("ChampionDeploy");

            foreach (object descriptor in CreateDescriptors(CommittedRealmContext()))
            {
                if (Category(descriptor) == "Presentation" || Id(descriptor) == championId)
                {
                    continue;
                }

                Assert.That(
                    BlockingIssueIds(descriptor),
                    Has.Member(SaveHardeningIssue),
                    $"Mutation command '{Id(descriptor)}' must declare #137 (save hardening) per spec section 15.");
            }
        }

        [Test]
        public void ChampionDeploymentDeclaresItsGatesAndStaysUnavailable()
        {
            object committed = Resolve(CommandId("ChampionDeploy"), CommittedRealmContext());
            Assert.That(Availability(committed), Is.Not.EqualTo("Available"));
            Assert.That(BlockingIssueIds(committed), Is.EquivalentTo(new[] { 150, 173, 180 }));
            Assert.That(BlockingIssueIds(committed), Has.No.Member(SaveHardeningIssue));

            object noRealm = Resolve(CommandId("ChampionDeploy"), NoRealmContext());
            Assert.That(Availability(noRealm), Is.EqualTo("UnavailableInvalidContext"));
        }

        [Test]
        public void PermanentlyUnavailableCatalogCommandsStayInDeckButInert()
        {
            object[] deck = CreateDeckDescriptors(CommittedRealmContext());

            object manaShrine = deck.SingleOrDefault(command => Id(command) == CommandId("ManaShrineUpgrade"));
            object mine = deck.SingleOrDefault(command => Id(command) == CommandId("MineUpgrade"));

            Assert.That(manaShrine, Is.Not.Null, "building.mana_shrine.upgrade must remain a visible deck entry (D9).");
            Assert.That(mine, Is.Not.Null, "building.mine.upgrade must remain a visible deck entry (D9).");
            Assert.That(IsInteractable(manaShrine), Is.False);
            Assert.That(IsInteractable(mine), Is.False);
        }

        // ---------------------------------------------------------------------
        // (2) Removed mutation paths are absent from production source
        // ---------------------------------------------------------------------

        [Test]
        public void KingdomControllerRemovesHiddenProgressionSeedingReadsAndControllerOwnedLoad()
        {
            string source = ReadProductionSource("UI", "Kingdom", "KingdomSceneController.cs");

            string[] forbiddenCallTokens =
            {
                ".CompleteUpgrade(",
                ".CompleteResearch(",
                "Bootloader.InitializeIfMissing",
                "save.Load(",
                ".GetBuildingState(",
                ".GetResearchState(",
                ".GetStatBonus(",
                ".GetActiveQuests(",
                ".GetTerritories(",
                ".GetRealmGems(",
                ".GetWishgateState(",
                ".GetTroopCount("
            };

            foreach (string token in forbiddenCallTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }

            // The controller must consume the merged #241 marker-validated ready profile.
            Assert.That(source, Does.Contain("IOfflineServiceStackMarker"));
            Assert.That(source, Does.Contain("OfflineStackLoadState"));
        }

        [Test]
        public void ChampionArenaControllerRemovesDirectCreditGrantAndItsTimerField()
        {
            string source = ReadProductionSource("ChampionMode", "ChampionArenaSceneController.cs");
            Assert.That(source, Does.Not.Contain("AddCredits"));

            // The proximity credit-grant loop (and only that loop) used _warzoneCreditTimer; its removal
            // is a structural proof the grant path is gone, not merely commented out.
            Type championType = GetRuntimeType("AL.ChampionMode.ChampionArenaSceneController");
            Assert.That(
                championType.GetField("_warzoneCreditTimer", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null,
                "The proximity credit-grant timer field must be removed with the grant loop.");
        }

        [Test]
        public void KingdomVisualizerContainsNoStateSeedingReadPath()
        {
            string source = ReadProductionSource("Kingdom", "Visuals", "KingdomVisualizer.cs");

            Assert.That(source, Does.Not.Contain("_buildingService.GetBuildingState("));
            Assert.That(source, Does.Not.Contain("_territoryService.GetTerritories("));
        }

        // ---------------------------------------------------------------------
        // (3) Read-only UI lifecycle seeds no profile state and never loads/saves
        // ---------------------------------------------------------------------

        [Test]
        public void ControllerConsumesMarkerReadyProfileAndDoesNotReloadOrSave()
        {
            object fake = InstallControllableSave();
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");

            // Simulate the scene's Bootloader owner having completed the single marker-validated load.
            object marker = GetService(MarkerInterface());
            const string owner = "kingdom-containment-test-owner";
            Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", owner));
            Assert.True((bool)Invoke(marker, "TryBeginLoad", owner));
            Invoke(marker, "MarkLoadSucceeded", owner);

            Invoke(fake, "SeedCurrentSave");
            SetCommittedRealm(fake, "Stonehold");

            int loadBefore = (int)GetInstanceField(fake, "LoadCount");
            int saveBefore = (int)GetInstanceField(fake, "SaveCount");

            Type controllerType = GetRuntimeType("AL.UI.Kingdom.KingdomSceneController");
            var host = new GameObject("KingdomContainmentControllerTest");
            var controller = host.AddComponent(controllerType);
            HashSet<int> existingRootIds = CaptureSceneRootIds();
            try
            {
                bool ready = (bool)controllerType
                    .GetMethod("TryConsumeReadyProfile", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);
                Assert.True(ready, "A succeeded marker load with a present save must be consumed as ready.");
                SetInstanceField(controller, "_profileReady", ready);

                // Drive the visualizer's service-backed read paths without constructing its graphics
                // materials in EditMode. Rendering itself has no domain authority; these are the paths
                // that previously seeded buildings and territories as presentation fallbacks.
                Type visualizerType = GetRuntimeType("AL.Kingdom.Visuals.KingdomVisualizer");
                var board = new GameObject("Kingdom_2_5D_Board");
                var visualizer = board.AddComponent(visualizerType);
                Invoke(visualizer, "EnsureServices");
                Invoke(visualizer, "BuildVisualHash");
                Invoke(visualizer, "CreateTerritoryOutposts");
                Invoke(controller, "BuildRuntimeUi");
                Invoke(controller, "Refresh");
                Invoke(controller, "Update");
                Invoke(controller, "Update");
                Invoke(controller, "ToggleDashboard");
                Invoke(controller, "Refresh");
                Invoke(controller, "ToggleDashboard");

                object save = GetInstanceProperty(fake, "CurrentSave");
                AssertEmpty(save, "Buildings");
                AssertEmpty(save, "Researches");
                AssertEmpty(save, "Quests");
                AssertEmpty(save, "Troops");
                AssertEmpty(save, "RealmGems");
                AssertEmpty(save, "Territories");

                Assert.AreEqual(loadBefore, (int)GetInstanceField(fake, "LoadCount"),
                    "Read-only UI must not trigger a second profile load.");
                Assert.AreEqual(saveBefore, (int)GetInstanceField(fake, "SaveCount"),
                    "Read-only UI must not save.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                DestroyNewSceneRoots(existingRootIds);
            }
        }

        [Test]
        public void SelectingEveryCommandDescriptorPerformsNoLoadSaveOrSeed()
        {
            object fake = InstallControllableSave();
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");

            object marker = GetService(MarkerInterface());
            const string owner = "kingdom-reachability-test-owner";
            Assert.True((bool)Invoke(marker, "TryClaimRuntimeOwner", owner));
            Assert.True((bool)Invoke(marker, "TryBeginLoad", owner));
            Invoke(marker, "MarkLoadSucceeded", owner);

            Invoke(fake, "SeedCurrentSave");
            SetCommittedRealm(fake, "Stonehold");

            int loadBefore = (int)GetInstanceField(fake, "LoadCount");
            int saveBefore = (int)GetInstanceField(fake, "SaveCount");

            Type controllerType = GetRuntimeType("AL.UI.Kingdom.KingdomSceneController");
            var host = new GameObject("KingdomReachabilityControllerTest");
            var controller = host.AddComponent(controllerType);
            try
            {
                SetInstanceField(controller, "_profileReady", true);
                Invoke(controller, "BuildRuntimeUi");

                // Drive the command-selection handler for every descriptor the deck can present, under
                // both a realm-committed all-capabilities-true context (maximizes interactable commands)
                // and a no-realm context (the invalid-context path). This closes the hierarchy/UnityEvents
                // reachability vector: no command's selection is wired to a domain mutation, so selecting
                // any of them loads, saves, and seeds nothing regardless of availability.
                MethodInfo handle = controllerType.GetMethod(
                    "HandleCommandSelected", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handle, Is.Not.Null, "HandleCommandSelected must exist to be exercised.");

                foreach (object context in new[] { AllCapabilitiesTrueContext(), NoRealmContext() })
                {
                    foreach (object descriptor in CreateDescriptors(context))
                    {
                        handle.Invoke(controller, new[] { descriptor });
                    }
                }

                object save = GetInstanceProperty(fake, "CurrentSave");
                AssertEmpty(save, "Buildings");
                AssertEmpty(save, "Researches");
                AssertEmpty(save, "Quests");
                AssertEmpty(save, "Troops");
                AssertEmpty(save, "RealmGems");
                AssertEmpty(save, "Territories");

                Assert.AreEqual(loadBefore, (int)GetInstanceField(fake, "LoadCount"),
                    "Selecting a command must not load a profile.");
                Assert.AreEqual(saveBefore, (int)GetInstanceField(fake, "SaveCount"),
                    "Selecting a command must not save.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                var canvas = GameObject.Find("KingdomCanvas");
                if (canvas != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvas);
                }
            }
        }

        [Test]
        public void ControllerDoesNotTreatProfileAsReadyWhenMarkerLoadHasNotSucceeded()
        {
            object fake = InstallControllableSave();
            AssertState(InitializeIfMissing(), "CreatedCompleteStack");
            Invoke(fake, "SeedCurrentSave");

            // No Bootloader owner ran, so marker.LoadState is NotStarted.
            Type controllerType = GetRuntimeType("AL.UI.Kingdom.KingdomSceneController");
            bool ready = (bool)controllerType
                .GetMethod("TryConsumeReadyProfile", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);

            Assert.False(ready, "Without a succeeded marker load the controller must not treat the profile as ready.");

            var host = new GameObject("KingdomUnavailableProfileControllerTest");
            var controller = host.AddComponent(controllerType);
            HashSet<int> existingRootIds = CaptureSceneRootIds();
            try
            {
                Invoke(controller, "Start");

                Assert.That(
                    GameObject.Find("Kingdom_2_5D_Board"),
                    Is.Null,
                    "An unavailable profile must not construct a service-backed world visualizer.");

                object save = GetInstanceProperty(fake, "CurrentSave");
                AssertEmpty(save, "Buildings");
                AssertEmpty(save, "Researches");
                AssertEmpty(save, "Quests");
                AssertEmpty(save, "Troops");
                AssertEmpty(save, "RealmGems");
                AssertEmpty(save, "Territories");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                DestroyNewSceneRoots(existingRootIds);
            }
        }

        // ---------------------------------------------------------------------
        // Policy reflection helpers
        // ---------------------------------------------------------------------

        private static object CommittedRealmContext()
        {
            return CreateContext(hasCommittedRealm: true, capabilities: CreateCapabilities());
        }

        private static object NoRealmContext()
        {
            return CreateContext(hasCommittedRealm: false, capabilities: CreateCapabilities());
        }

        private static object AllCapabilitiesTrueContext()
        {
            object capabilities = Activator.CreateInstance(
                PolicyType("AL.UI.Kingdom.KingdomCommandCapabilities"),
                true, true, true, true, true, true, true);
            return CreateContext(hasCommittedRealm: true, capabilities: capabilities);
        }

        private static object CreateCapabilities()
        {
            return Activator.CreateInstance(
                PolicyType("AL.UI.Kingdom.KingdomCommandCapabilities"),
                false, false, false, false, false, false, false);
        }

        private static object CreateContext(bool hasCommittedRealm, object capabilities)
        {
            return Activator.CreateInstance(
                PolicyType("AL.UI.Kingdom.KingdomCommandContext"),
                hasCommittedRealm,
                capabilities);
        }

        private static object[] CreateDescriptors(object context)
        {
            return InvokePolicyList("CreateDescriptors", context);
        }

        private static object[] CreateDeckDescriptors(object context)
        {
            return InvokePolicyList("CreateDeckDescriptors", context);
        }

        private static object Resolve(string id, object context)
        {
            return PolicyType("AL.UI.Kingdom.KingdomCommandPolicy")
                .GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { id, context });
        }

        private static object[] InvokePolicyList(string methodName, object context)
        {
            var result = PolicyType("AL.UI.Kingdom.KingdomCommandPolicy")
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { context });
            return ((IEnumerable)result).Cast<object>().ToArray();
        }

        private static string CommandId(string fieldName)
        {
            return (string)PolicyType("AL.UI.Kingdom.KingdomCommandPolicy")
                .GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
                .GetRawConstantValue();
        }

        private static string Id(object descriptor) => (string)Property(descriptor, "Id");
        private static string Category(object descriptor) => Property(descriptor, "Category").ToString();
        private static string Availability(object descriptor) => Property(descriptor, "Availability").ToString();
        private static bool IsInteractable(object descriptor) => (bool)Property(descriptor, "IsInteractable");

        private static int[] BlockingIssueIds(object descriptor)
        {
            return ((IEnumerable)Property(descriptor, "BlockingIssueIds")).Cast<int>().ToArray();
        }

        private static void AssertBlockers(object context, string commandFieldName, params int[] expected)
        {
            object descriptor = Resolve(CommandId(commandFieldName), context);
            Assert.That(
                BlockingIssueIds(descriptor),
                Is.EquivalentTo(expected),
                $"{commandFieldName} blockers must match the accepted reconnection contract.");
        }

        private static object Property(object instance, string name)
        {
            return instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(instance);
        }

        private static Type PolicyType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, typeName);
            return type;
        }

        // ---------------------------------------------------------------------
        // Stack / marker / save reflection helpers (mirrors BootloaderServiceStackIntegrityTests)
        // ---------------------------------------------------------------------

        private static string ReadProductionSource(params string[] relativeSegments)
        {
            var segments = new List<string> { Application.dataPath, "AL", "Scripts" };
            segments.AddRange(relativeSegments);
            return File.ReadAllText(Path.Combine(segments.ToArray()));
        }

        private static object InstallControllableSave()
        {
            object fake = Activator.CreateInstance(GetRuntimeType("AL.Core.ControllableSaveGameService"), true);
            Func<object> factory = () => fake;
            SetStackOverride("SaveGameFactoryOverride", factory);
            return fake;
        }

        private static void SetCommittedRealm(object fake, string realmName)
        {
            object save = GetInstanceProperty(fake, "CurrentSave");
            Assert.That(save, Is.Not.Null, "Seed the controllable save before committing a realm.");
            object realmId = Enum.Parse(GetRuntimeType("AL.Core.RealmId"), realmName);
            save.GetType().GetField("SelectedRealm", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(save, realmId);
        }

        private static void AssertEmpty(object save, string fieldName)
        {
            var list = (ICollection)save.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.Public)
                .GetValue(save);
            Assert.AreEqual(0, list?.Count ?? 0, $"Read-only UI seeded {fieldName} state.");
        }

        private static HashSet<int> CaptureSceneRootIds()
        {
            return new HashSet<int>(
                UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene()
                    .GetRootGameObjects()
                    .Select(root => root.GetInstanceID()));
        }

        private static void DestroyNewSceneRoots(HashSet<int> existingRootIds)
        {
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!existingRootIds.Contains(root.GetInstanceID()))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static object InitializeIfMissing()
        {
            return GetRuntimeType("AL.Core.Bootloader")
                .GetMethod("InitializeIfMissing", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
        }

        private static void AssertState(object result, string expected)
        {
            Assert.AreEqual(expected, result.GetType().GetProperty("State").GetValue(result).ToString());
        }

        private static Type MarkerInterface() => GetRuntimeType("AL.Core.IOfflineServiceStackMarker");

        private static object GetService(Type serviceType)
        {
            return GetRuntimeType("AL.Core.ServiceLocator")
                .GetMethod("Get", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(serviceType)
                .Invoke(null, null);
        }

        private static IDictionary ServicesDictionary()
        {
            return (IDictionary)GetRuntimeType("AL.Core.ServiceLocator")
                .GetField("Services", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
        }

        private static void SetStackOverride(string fieldName, object value)
        {
            GetRuntimeType("AL.Core.OfflineServiceStack")
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            return target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(target, args);
        }

        private static object GetInstanceField(object target, string name)
        {
            return target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .GetValue(target);
        }

        private static void SetInstanceField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SetValue(target, value);
        }

        private static object GetInstanceProperty(object target, string name)
        {
            return target.GetType()
                .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp")
                ?.GetType(typeName);

            Assert.NotNull(type, $"Expected runtime type {typeName} in Assembly-CSharp.");
            return type;
        }
    }
}
