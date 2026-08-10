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
    //   * blocking-issue diagnostics were derived from the GitHub issue state frozen at implementation
    //     time (2026-07-21) and stay honest against that snapshot (no closed issue listed as blocking,
    //     #137 required for every mutation family except Champion, permanently-unavailable catalog
    //     entries kept per D9). The snapshot is a fixed baseline captured in this test, not a live query;
    //     if an issue's state changes later this guard is re-derived deliberately, not silently.
    //   * the hidden per-second progression, the controller-owned load, and the Champion proximity
    //     credit grant are gone from production source;
    //   * building the read-only UI and refreshing/updating/toggling it (and selecting every command)
    //     seeds no profile state and never loads or saves (isolated real-service + controllable-save
    //     spy, save-state equality).
    //
    // The test assembly does not reference every production assembly, so production types are reached
    // by reflection, mirroring BootloaderServiceStackIntegrityTests.
    public sealed class KingdomReleaseContainmentTests
    {
        // GitHub issue state frozen at #178 containment implementation time (2026-07-21). This is a
        // fixed snapshot, not a live lookup: closed = {127, 152, 156, 223}. A blockingIssueId that names
        // any of these CLOSED issues is a diagnostics-honesty defect and fails BlockingIssueIds...().
        private static readonly int[] ClosedIssuesAtImplementation = { 127, 152, 156, 223 };

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
        // (1) Blocking-issue diagnostics reflect live issue state
        // ---------------------------------------------------------------------

        [Test]
        public void BlockingIssueIdsNeverReferenceAClosedIssue()
        {
            foreach (object context in new[] { CommittedRealmContext(), NoRealmContext() })
            {
                foreach (object descriptor in CreateDescriptors(context))
                {
                    foreach (int issueId in BlockingIssueIds(descriptor))
                    {
                        Assert.That(
                            ClosedIssuesAtImplementation,
                            Has.No.Member(issueId),
                            $"Command '{Id(descriptor)}' lists closed issue #{issueId} as a live blocker.");
                    }
                }
            }
        }

        [Test]
        public void DeferredMutationFamiliesDeclareSaveHardeningDependency()
        {
            string championId = CommandId("ChampionDeploy");

            foreach (object descriptor in CreateDescriptors(CommittedRealmContext()))
            {
                if (Category(descriptor) == "Presentation" ||
                    Category(descriptor) == "Build" ||
                    Id(descriptor) == championId)
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
            try
            {
                bool ready = (bool)controllerType
                    .GetMethod("TryConsumeReadyProfile", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);
                Assert.True(ready, "A succeeded marker load with a present save must be consumed as ready.");
                SetInstanceField(controller, "_profileReady", ready);

                // Build only the read-only UI and drive the read paths. The 2.5D world/visualizer is a
                // separate MonoBehaviour outside this correction's scope, so it is not constructed here;
                // that isolates the controller-owned refresh/update/toggle surface under test.
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

                // BuildRuntimeUi creates the Kingdom canvas as a separate scene root; destroy it so the
                // read-only UI objects do not leak into other tests in the run.
                var canvas = GameObject.Find("KingdomCanvas");
                if (canvas != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvas);
                }
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
                // reachability vector. The live building path is exercised against an empty wallet here:
                // gameplay authority rejects it before mutation, while deferred commands stay inert.
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
        }

        [Test]
        public void CameraSetupRepairsTheExistingTaggedObjectWithoutDuplicatingIt()
        {
            Type controllerType = GetRuntimeType("AL.UI.Kingdom.KingdomSceneController");
            MethodInfo configure = controllerType.GetMethod(
                "ConfigureKingdomCamera",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(configure, Is.Not.Null);

            GameObject[] preexisting = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (GameObject candidate in preexisting)
            {
                candidate.SetActive(false);
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            try
            {
                Assert.That(cameraObject.GetComponent<Camera>(), Is.Null);

                configure.Invoke(null, new object[] { cameraObject.scene });

                Camera camera = cameraObject.GetComponent<Camera>();
                Assert.That(
                    camera,
                    Is.Not.Null,
                    "Kingdom must repair the existing tagged presentation object instead of leaving it componentless.");
                Assert.That(camera.isActiveAndEnabled, Is.True);
                Assert.That(camera.orthographic, Is.True);
                Assert.That(
                    GameObject.FindGameObjectsWithTag("MainCamera"),
                    Is.EqualTo(new[] { cameraObject }),
                    "Camera recovery must not leave duplicate MainCamera-tagged objects.");
            }
            finally
            {
                foreach (GameObject candidate in GameObject.FindGameObjectsWithTag("MainCamera"))
                {
                    if (!preexisting.Contains(candidate))
                    {
                        UnityEngine.Object.DestroyImmediate(candidate);
                    }
                }

                foreach (GameObject candidate in preexisting)
                {
                    if (candidate != null)
                    {
                        candidate.SetActive(true);
                    }
                }
            }
        }

        [Test]
        public void UpdateIsInertUntilRuntimeInitializationCompletes()
        {
            Type controllerType = GetRuntimeType("AL.UI.Kingdom.KingdomSceneController");
            FieldInfo initialized = controllerType.GetField(
                "_runtimeInitialized",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                initialized,
                Is.Not.Null,
                "Kingdom needs an explicit initialization latch so a failed Start cannot enter its refresh loop.");

            var host = new GameObject("KingdomPartialInitializationTest");
            Component controller = host.AddComponent(controllerType);
            try
            {
                SetInstanceField(controller, "_profileReady", true);
                initialized.SetValue(controller, false);
                SetInstanceField(controller, "_lastLiveRefreshTimestamp", 37L);

                Assert.DoesNotThrow(() => Invoke(controller, "Update"));
                Assert.That(
                    GetInstanceField(controller, "_lastLiveRefreshTimestamp"),
                    Is.EqualTo(37L),
                    "A partial controller must not enter or advance the periodic Refresh path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
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
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);

            Assert.NotNull(type, $"Expected loaded runtime type {typeName}.");
            return type;
        }
    }
}
