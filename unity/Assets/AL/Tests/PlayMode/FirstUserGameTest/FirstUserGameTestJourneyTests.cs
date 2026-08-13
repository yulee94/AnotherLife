#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.ChampionMode;
using AL.ChampionMode.Skills;
using AL.Data.Runtime;
using AL.Development;
using AL.Editor.Development.FirstUserGameTest;
using AL.UI;
using AL.UI.FirstUserIdentity;
using AL.UI.RealmSelection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AL.Tests.PlayMode.FirstUserGameTest
{
    [TestFixture]
    public sealed class FirstUserGameTestJourneyTests
    {
        private const float TimeoutSeconds = 30f;
        private const string CombatAudioRootName = "ChampionRuntimeCombatAudio";
        private const string CombatFeedbackRootName = "ChampionRuntimeCombatFeedback";
        private const string DontDestroyOnLoadScenePath = "DontDestroyOnLoad";
        private const string RuntimeVfxPoolRootName = "RuntimeVfxPool";

        private readonly List<string> _generatedRoots = new List<string>();
        private readonly Dictionary<FieldInfo, object> _originalFactoryValues =
            new Dictionary<FieldInfo, object>();
        private readonly Dictionary<object, object> _originalServices =
            new Dictionary<object, object>();
        private readonly Dictionary<int, string> _ownedBootloaderScenePaths =
            new Dictionary<int, string>();
        private readonly HashSet<int> _ownedRootInstanceIds = new HashSet<int>();
        private readonly HashSet<int> _ownedSceneHandles = new HashSet<int>();
        private readonly HashSet<int> _preexistingSceneHandles = new HashSet<int>();
        private readonly Dictionary<string, GameObject> _ownedLateCombatRoots =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<string> _visitedScenes = new List<string>();
        private readonly List<string> _severeLogs = new List<string>();
        private readonly List<string> _tutorialSessionIds = new List<string>();

        private FieldInfo _servicesField;
        private Type _stackType;
        private bool _originalIgnoreFailingMessages;
        private float _originalTimeScale;
        private float _originalFixedDeltaTime;
        private string _ownershipFailure;
        private ReadOnlyPersistentInventory _persistentBefore;

        [SetUp]
        public void SetUp()
        {
            _ownedBootloaderScenePaths.Clear();
            _ownedRootInstanceIds.Clear();
            _ownedSceneHandles.Clear();
            _preexistingSceneHandles.Clear();
            _ownedLateCombatRoots.Clear();
            _ownershipFailure = string.Empty;
            Bootloader[] preexistingBootloaders =
                UnityEngine.Object.FindObjectsOfType<Bootloader>(includeInactive: true);
            Assert.That(
                preexistingBootloaders,
                Is.Empty,
                "The fixture refuses to take scene ownership while a pre-existing Bootloader exists.");
            RecordPreexistingRunnerScenes();
            Assert.That(
                TryVerifyCombatRuntimeIsSterile(out string combatRuntimeMessage),
                Is.True,
                combatRuntimeMessage);
            _originalTimeScale = Time.timeScale;
            _originalFixedDeltaTime = Time.fixedDeltaTime;

            _stackType = typeof(Bootloader).Assembly.GetType(
                "AL.Core.OfflineServiceStack",
                throwOnError: true);
            _servicesField = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(_servicesField, Is.Not.Null);

            SnapshotAndClearRuntimeState();
            _generatedRoots.Clear();
            _visitedScenes.Clear();
            _severeLogs.Clear();
            _tutorialSessionIds.Clear();
            _persistentBefore = ReadOnlyPersistentInventory.Capture(Application.persistentDataPath);

            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += HandleLog;
            SceneManager.sceneLoaded += RecordScene;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            string cleanupFailure = string.Empty;
            SceneManager.sceneLoaded -= RecordScene;
            Application.logMessageReceived -= HandleLog;

            if (!string.IsNullOrEmpty(_ownershipFailure))
            {
                cleanupFailure += _ownershipFailure + "\n";
            }

            if (!TryVerifyOnlyFixtureOwnedBootloaders(out string ownershipMessage))
            {
                cleanupFailure += ownershipMessage + "\n";
            }

            if (FirstUserGameTestRuntimeHost.Active != null)
            {
                FirstUserGameTestRuntimeHost.DisposeActiveForTests();
            }

            yield return null;

            DestroyOwnedCombatRuntimeState(
                message => cleanupFailure += message + "\n");

            DestroyFixtureOwnedSceneRoots(
                message => cleanupFailure += message + "\n");

            EditorGameTestModeBootstrap.Disarm();
            yield return null;
            foreach (string root in _generatedRoots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                EditorGameTestModePlan plan = CreatePlanForExistingRoot(root);
                if (!EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                        plan,
                        out _,
                        out string cleanupMessage))
                {
                    cleanupFailure += cleanupMessage + "\n";
                }
            }

            foreach (string sessionId in _tutorialSessionIds)
            {
                FirstUserGameTestTutorialSessionStore.EraseForTests(sessionId);
            }

            RestoreRuntimeState();
            LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;

            ReadOnlyPersistentInventory persistentAfter =
                ReadOnlyPersistentInventory.Capture(Application.persistentDataPath);
            if (!_persistentBefore.Equals(persistentAfter))
            {
                cleanupFailure +=
                    "The real persistentDataPath inventory changed during the isolated Game Test. " +
                    _persistentBefore.DescribeDifference(persistentAfter) + "\n";
            }

            Bootloader[] remainingBootloaders =
                UnityEngine.Object.FindObjectsOfType<Bootloader>(includeInactive: true);
            EventSystem[] remainingEventSystems =
                UnityEngine.Object.FindObjectsOfType<EventSystem>(includeInactive: true);
            Camera[] remainingCameras =
                UnityEngine.Object.FindObjectsOfType<Camera>(includeInactive: true);
            if (remainingBootloaders.Length != 0 ||
                remainingEventSystems.Length != 0 ||
                remainingCameras.Length != 0 ||
                GameObject.Find(FirstUserGameTestRuntimeHost.HostObjectName) != null ||
                GameObject.Find(FirstUserGameTestRuntimeHost.FailureCanvasName) != null ||
                GameObject.Find(FirstUserGameTestRuntimeHost.DestinationRootName) != null)
            {
                cleanupFailure +=
                    "PlayMode cleanup retained project scene objects. " +
                    "Bootloaders=" + remainingBootloaders.Length +
                    ", EventSystems=" + remainingEventSystems.Length +
                    ", Cameras=" + remainingCameras.Length + ".\n";
            }

            if (!TryVerifyCombatRuntimeIsSterile(out string combatRuntimeMessage))
            {
                cleanupFailure += combatRuntimeMessage + "\n";
            }

            foreach (int ownedSceneHandle in _ownedSceneHandles)
            {
                if (TryGetLoadedSceneByHandle(ownedSceneHandle, out Scene ownedScene) &&
                    ownedScene.GetRootGameObjects().Length != 0)
                {
                    cleanupFailure +=
                        "Fixture-owned scene retained project roots after teardown: " +
                        ownedScene.path + ".\n";
                }
            }

            if (!string.IsNullOrEmpty(cleanupFailure))
            {
                Assert.Fail(cleanupFailure);
            }
        }

        [UnityTest]
        public IEnumerator FreshJourneyVerifiesDevelopmentEvidenceAndEntersOnlyControllableIsolatedArena()
        {
            EditorGameTestModePlan plan = CreateOwnedPlan();
            Assert.That(EditorGameTestModeBootstrap.TryArm(
                plan,
                out _,
                out string armMessage), Is.True, armMessage);
            AsyncOperation bootLoad = EditorSceneManager.LoadSceneAsyncInPlayMode(
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(bootLoad, Is.Not.Null);
            yield return WaitForOperation(bootLoad, "Boot scene load");
            AssertFixtureSceneOwnershipIsClean();
            FirstUserGameTestRuntimeHost host =
                FirstUserGameTestRuntimeHost.Install(plan.SessionId);
            Assert.That(host, Is.Not.Null);

            GameObject launchCanvas = null;
            Button continueButton = null;
            float readyStarted = Time.realtimeSinceStartup;
            while (continueButton == null || !continueButton.isActiveAndEnabled ||
                   !continueButton.interactable)
            {
                if (Time.realtimeSinceStartup - readyStarted > TimeoutSeconds)
                {
                    Assert.Fail("Boot did not expose a truthful explicit Continue action.");
                }

                launchCanvas = GameObject.Find("LaunchReadinessCanvas");
                continueButton = launchCanvas == null
                    ? null
                    : launchCanvas.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(button => button.name == "FinishedLoadingAction");
                yield return null;
            }

            AssertExactObjectUnderOwnedRoot(
                launchCanvas,
                "LaunchReadinessCanvas",
                EditorGameTestModeBootstrap.ExpectedBootScenePath);
            yield return null;
            continueButton.onClick.Invoke();
            continueButton.onClick.Invoke();
            yield return WaitForScene(FirstUserGameTestRuntimeHost.RealmSelectionPath);
            AssertFixtureSceneOwnershipIsClean();
            yield return null;

            Assert.That(_visitedScenes, Does.Not.Contain(FirstUserGameTestRuntimeHost.KingdomPath));
            RealmSelectionController productionRealm =
                UnityEngine.Object.FindObjectOfType<RealmSelectionController>(includeInactive: true);
            Assert.That(productionRealm, Is.Not.Null);
            Assert.That(productionRealm.enabled, Is.False,
                "The production commit/Kingdom controller must be suppressed before Start.");
            AssertSingleEventSystem();

            FirstUserIdentityDraftPresenter presenter = host.IdentityPresenter;
            Assert.That(presenter, Is.Not.Null);
            AdoptExactLateRoot(
                presenter.transform.root.gameObject,
                "FirstUserGameTestIdentityCanvas",
                FirstUserGameTestRuntimeHost.RealmSelectionPath);
            presenter.GetRealmChoiceButton(RealmId.Eldergrove).onClick.Invoke();
            presenter.ConfirmRealmButton.onClick.Invoke();
            presenter.GetClassFamilyChoiceButton(ClassFamily.Ranger).onClick.Invoke();
            presenter.ConfirmDraftButton.onClick.Invoke();
            yield return null;

            FirstUserGameTestCustomizationPanel panel = host.CustomizationPanel;
            Assert.That(panel, Is.Not.Null);
            AdoptExactLateRoot(
                panel.transform.root.gameObject,
                FirstUserGameTestRuntimeHost.CustomizationCanvasName,
                FirstUserGameTestRuntimeHost.RealmSelectionPath);
            panel.SelectForTests("average");
            panel.HandleInput.text = "Eldergrove Scout";
            Assert.That(panel.ConfirmButton.interactable, Is.True);
            panel.ConfirmButton.onClick.Invoke();
            panel.ConfirmButton.onClick.Invoke();

            yield return WaitForScene(FirstUserGameTestRuntimeHost.ChampionArenaPath);
            AssertFixtureSceneOwnershipIsClean();
            float destinationStarted = Time.realtimeSinceStartup;
            while (host.DestinationMarker == null || !host.DestinationMarker.IsReady)
            {
                if (Time.realtimeSinceStartup - destinationStarted > TimeoutSeconds)
                {
                    Assert.Fail("The isolated controllable destination did not become ready: " + host.LastFailure);
                }

                yield return null;
            }

            AdoptExactLateRoot(
                host.DestinationMarker.transform.root.gameObject,
                FirstUserGameTestRuntimeHost.DestinationRootName,
                FirstUserGameTestRuntimeHost.ChampionArenaPath);

            Assert.That(_visitedScenes, Does.Not.Contain(FirstUserGameTestRuntimeHost.KingdomPath));
            ChampionArenaSceneController productionArena =
                UnityEngine.Object.FindObjectOfType<ChampionArenaSceneController>(includeInactive: true);
            Assert.That(productionArena, Is.Not.Null);
            Assert.That(productionArena.enabled, Is.False);
            Assert.That(host.DestinationMarker.Selection.Identity.Realm, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(host.DestinationMarker.Selection.Identity.Race, Is.EqualTo(FirstUserRace.Elves));
            Assert.That(host.DestinationMarker.Selection.Identity.ClassFamily, Is.EqualTo(ClassFamily.Ranger));
            Assert.That(host.DestinationMarker.Selection.CustomizationId, Is.EqualTo("average"));
            Assert.That(host.DestinationMarker.Selection.DevelopmentHandle, Is.EqualTo("Eldergrove Scout"));
            Assert.That(host.DestinationLoadRequestCount, Is.EqualTo(1),
                "Duplicate Confirm input must remain an inert one-shot replay boundary.");
            Assert.That(
                _visitedScenes.Count(path => string.Equals(
                    path,
                    FirstUserGameTestRuntimeHost.ChampionArenaPath,
                    StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(
                AssetDatabase.AssetPathToGUID(FirstUserGameTestRuntimeHost.ChampionArenaPath),
                Is.EqualTo(FirstUserGameTestRuntimeHost.ChampionArenaGuid));
            AssertSingleEventSystem();

            FirstUserGameTestTutorialPresenter tutorial =
                host.DestinationMarker.TutorialPresenter;
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(tutorial.State.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.Move));
            Assert.That(tutorial.TitleAction.interactable, Is.False);
            Assert.That(tutorial.ObjectiveAction.interactable, Is.False);
            string initialTutorialCopy = string.Join(
                "\n",
                tutorial.GetComponentsInChildren<Text>(true).Select(text => text.text));
            foreach (string machineToken in new[]
                     {
                         "TUTORIAL_",
                         "OBJ_",
                         "EVENT_",
                         "OMEN_1",
                         "ACTION_",
                         "RESULT_"
                     })
            {
                Assert.That(initialTutorialCopy, Does.Not.Contain(machineToken));
            }

            Assert.That(EditorGameTestModeBootstrap.TryVerifyActiveRuntime(
                out _,
                out string runtimeMessage), Is.True, runtimeMessage);

            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            Assert.That(save.CurrentSave, Is.Not.Null);
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None),
                "Development realm evidence must never self-attest a production realm commit.");
            Assert.That(File.Exists(Path.Combine(plan.IsolatedSaveRoot, "save.json")), Is.True);
            Assert.That(save, Is.InstanceOf<IProfileWriteAuthorityProvider>());
            Assert.That(
                ProfileWriteAuthorityProviderGuard.IsCurrentWritable(
                    (IProfileWriteAuthorityProvider)save),
                Is.False,
                "The isolated development verifier must never manufacture production Writable authority.");

            IDictionary registeredServices = (IDictionary)_servicesField.GetValue(null);
            registeredServices[typeof(ISaveGameService)] = new SaveGameServiceIdentityProxy(save);
            Assert.That(host.ReverifyVerifiedDevelopmentBoundaryForTests(), Is.False,
                "Replacing the exact isolated service instance must fail closed.");
            registeredServices[typeof(ISaveGameService)] = save;
            Assert.That(host.ReverifyVerifiedDevelopmentBoundaryForTests(), Is.True,
                "Restoring the exact marker-bound service must restore the verified development boundary.");

            int questCountBefore = save.CurrentSave.Quests.Count;
            string nvsBefore = JsonUtility.ToJson(save.CurrentSave.Nvs01Progress);

            host.DestinationMarker.AttackButton.onClick.Invoke();
            Assert.That(tutorial.State.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.Move),
                "An out-of-order basic attack cannot progress the tutorial.");

            Vector3 before = host.DestinationMarker.Controller.transform.position;
            host.DestinationMarker.MoveForwardButton.onClick.Invoke();
            Assert.That(tutorial.MovementIntentPendingForTests, Is.True,
                "The same button path must enter the player-originated movement admission latch.");
            float movementDeadline = Time.realtimeSinceStartup + 2f;
            while (tutorial.State.Step == FirstUserGameTestTutorialStep.Move &&
                   Time.realtimeSinceStartup < movementDeadline)
            {
                yield return null;
            }

            Vector3 horizontalMovement =
                host.DestinationMarker.Controller.transform.position - before;
            horizontalMovement.y = 0f;
            Assert.That(
                horizontalMovement.magnitude,
                Is.GreaterThan(0.05f),
                "The isolated destination must expose real horizontal controllable movement; gravity is not evidence.");
            Assert.That(tutorial.State.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.BasicAttack));
            Assert.That(tutorial.State.MovementConfirmationCount, Is.EqualTo(1));
            host.DestinationMarker.MoveForwardButton.onClick.Invoke();
            yield return null;
            Assert.That(tutorial.State.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.BasicAttack));
            Assert.That(tutorial.State.MovementConfirmationCount, Is.EqualTo(1));

            host.DestinationMarker.AttackButton.onClick.Invoke();
            host.DestinationMarker.AttackButton.onClick.Invoke();
            Assert.That(tutorial.State.Step,
                Is.EqualTo(FirstUserGameTestTutorialStep.Complete));
            Assert.That(tutorial.State.BasicAttackConfirmationCount, Is.EqualTo(1));
            Assert.That(tutorial.State.CompletionEventCount, Is.EqualTo(1));
            Assert.That(tutorial.State.CompletionEventId,
                Is.EqualTo(FirstUserGameTestTutorialContract.TutorialCompletedEventId));
            Assert.That(tutorial.State.OmenOfferCount, Is.EqualTo(1));
            Assert.That(tutorial.State.ForegroundQuestId,
                Is.EqualTo(FirstUserGameTestTutorialContract.OmenQuestId));
            Assert.That(tutorial.State.ForegroundQuestState,
                Is.EqualTo(FirstUserGameTestTutorialContract.OmenOfferedState));
            Assert.That(tutorial.ChampionInputSuppressed, Is.True,
                "The offered UI must own the isolated raw-input boundary before it is actionable.");
            Assert.That(host.DestinationMarker.Controller.enabled, Is.False,
                "ChampionController.Update must be disabled before a real pointer can reach follow UI.");
            Assert.That(tutorial.EvaluateChampionControllerInputForTests(followUiActive: true),
                Is.False,
                "The PlayMode path must exercise the same raw-input admission decision as runtime.");

            yield return WaitForAndAdoptExactCombatRuntimeRoots();
            float combatSettledAt = Time.realtimeSinceStartup + 0.75f;
            while (Time.realtimeSinceStartup < combatSettledAt)
            {
                yield return null;
            }

            float offerReadyDeadline = Time.realtimeSinceStartup + 2f;
            while ((!tutorial.TitleAction.interactable ||
                    !tutorial.ObjectiveAction.interactable) &&
                   Time.realtimeSinceStartup < offerReadyDeadline)
            {
                yield return null;
            }

            Assert.That(tutorial.TitleAction.interactable, Is.True);
            Assert.That(tutorial.ObjectiveAction.interactable, Is.True);
            Assert.That(tutorial.TryInspectChampionInputForTests(
                out _,
                out bool attackInProgressBeforeFollow), Is.True);
            Assert.That(attackInProgressBeforeFollow, Is.False,
                "The accepted tutorial attack must settle before follow controls become actionable.");
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(tutorial.TitleAction.gameObject),
                "The offered objective title must receive initial semantic focus.");
            string offeredCopy = string.Join(
                "\n",
                tutorial.GetComponentsInChildren<Text>(true).Select(text => text.text));
            foreach (string machineToken in new[]
                     {
                         "TUTORIAL_",
                         "OBJ_",
                         "EVENT_",
                         "OMEN_1",
                         "ACTION_",
                         "RESULT_"
                     })
            {
                Assert.That(offeredCopy, Does.Not.Contain(machineToken));
            }

            FirstUserGameTestTutorialState completedState = tutorial.State;
            Vector3 beforeFollow = host.DestinationMarker.Controller.transform.position;
            string combatBeforeFollow = CaptureCombatRuntimeObservation();
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(
                tutorial.TitleAction.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
            Assert.That(tutorial.LastFollowResult.ResultId,
                Is.EqualTo(FirstUserGameTestTutorialContract.ActiveObjectiveFocusedResultId));
            EventSystem.current.SetSelectedGameObject(tutorial.ObjectiveAction.gameObject);
            ExecuteEvents.Execute(
                tutorial.ObjectiveAction.gameObject,
                new BaseEventData(EventSystem.current),
                ExecuteEvents.submitHandler);
            Assert.That(tutorial.LastFollowResult.ResultId,
                Is.EqualTo(FirstUserGameTestTutorialContract.ActiveObjectiveFocusedResultId));
            Assert.That(tutorial.State.ValueEquals(completedState), Is.True,
                "Pointer and submit follow actions cannot mutate tutorial or quest state.");
            Assert.That(host.DestinationMarker.Controller.transform.position,
                Is.EqualTo(beforeFollow),
                "Following the offered objective cannot move or teleport the player.");
            Assert.That(tutorial.TryInspectChampionInputForTests(
                out _,
                out bool attackInProgressAfterFollow), Is.True);
            Assert.That(attackInProgressAfterFollow, Is.False,
                "Pointer/submit follow activation cannot request another basic attack.");
            Assert.That(tutorial.State.BasicAttackConfirmationCount, Is.EqualTo(1));
            Assert.That(CaptureCombatRuntimeObservation(), Is.EqualTo(combatBeforeFollow),
                "Follow activation cannot change combat audio/VFX ownership or active counts.");
            Assert.That(save.CurrentSave.Quests.Count, Is.EqualTo(questCountBefore));
            Assert.That(JsonUtility.ToJson(save.CurrentSave.Nvs01Progress), Is.EqualTo(nvsBefore));
            AssertSingleEventSystem();

            ReadOnlyPersistentInventory during =
                ReadOnlyPersistentInventory.Capture(Application.persistentDataPath);
            Assert.That(during, Is.EqualTo(_persistentBefore),
                "The real persistentDataPath must stay byte-for-byte unchanged.");
            Assert.That(
                _severeLogs.Where(message =>
                    !message.StartsWith(
                        "[BOOT_STACK_RUNTIME_OWNER_REJECTED]",
                        StringComparison.Ordinal)),
                Is.Empty,
                "Only the known Bootloader reverse-order ownership handoff may be severe.\n" +
                string.Join("\n", _severeLogs));
            Assert.That(
                _severeLogs.Where(message => message.Contains(
                        "BOOT_STACK_RUNTIME_OWNER_REJECTED"))
                    .All(message => message.StartsWith(
                        "[BOOT_STACK_RUNTIME_OWNER_REJECTED]",
                        StringComparison.Ordinal)),
                Is.True,
                "Any ownership-handoff log must use the exact known typed classification.");
            Assert.That(_ownedBootloaderScenePaths.Count, Is.EqualTo(3));
            Assert.That(
                _ownedBootloaderScenePaths.Values,
                Is.EquivalentTo(new[]
                {
                    EditorGameTestModeBootstrap.ExpectedBootScenePath,
                    FirstUserGameTestRuntimeHost.RealmSelectionPath,
                    FirstUserGameTestRuntimeHost.ChampionArenaPath
                }),
                "The ownership ledger must bind one exact Bootloader instance to each test-loaded scene.");
        }

        private IEnumerator WaitForScene(string path)
        {
            float started = Time.realtimeSinceStartup;
            while (!string.Equals(SceneManager.GetActiveScene().path, path, StringComparison.Ordinal))
            {
                if (Time.realtimeSinceStartup - started > TimeoutSeconds)
                {
                    Assert.Fail(
                        "Timed out waiting for scene " + path + ". Visited: " +
                        string.Join(", ", _visitedScenes));
                }

                yield return null;
            }
        }

        private static IEnumerator WaitForOperation(AsyncOperation operation, string label)
        {
            float started = Time.realtimeSinceStartup;
            while (operation != null && !operation.isDone)
            {
                if (Time.realtimeSinceStartup - started > TimeoutSeconds)
                {
                    Assert.Fail(label + " timed out.");
                }

                yield return null;
            }
        }

        private void AssertSingleEventSystem()
        {
            EventSystem[] eventSystems =
                UnityEngine.Object.FindObjectsOfType<EventSystem>(includeInactive: true)
                    .Where(system => system.gameObject.activeInHierarchy)
                    .ToArray();
            BaseInputModule[] modules =
                UnityEngine.Object.FindObjectsOfType<BaseInputModule>(includeInactive: true)
                    .Where(module => module.gameObject.activeInHierarchy)
                    .ToArray();
            Assert.That(eventSystems, Has.Length.EqualTo(1));
            Assert.That(modules, Has.Length.EqualTo(1));
            Assert.That(modules[0].gameObject, Is.SameAs(eventSystems[0].gameObject));
        }

        private void RecordScene(Scene scene, LoadSceneMode mode)
        {
            if (!string.IsNullOrEmpty(scene.path))
            {
                _visitedScenes.Add(scene.path);
                RecordFixtureSceneOwnership(scene);
            }
        }

        private void RecordPreexistingRunnerScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded)
                {
                    _preexistingSceneHandles.Add(scene.handle);
                }
            }
        }

        private static bool TryGetLoadedSceneByHandle(int sceneHandle, out Scene scene)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (candidate.isLoaded && candidate.handle == sceneHandle)
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        private void RecordFixtureSceneOwnership(Scene scene)
        {
            if (!IsFixtureScenePath(scene.path))
            {
                _ownershipFailure =
                    "An unexpected scene was loaded and was not adopted by the fixture: " + scene.path;
                return;
            }

            _ownedSceneHandles.Add(scene.handle);
            RecordFixtureSceneRoots(scene);
            Bootloader[] bootloaders = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Bootloader>(true))
                .ToArray();
            if (bootloaders.Length != 1 || bootloaders[0].gameObject.scene != scene)
            {
                _ownershipFailure =
                    "Expected exactly one scene-local Bootloader in fixture scene " +
                    scene.path + ", found " + bootloaders.Length + ".";
                return;
            }

            _ownedBootloaderScenePaths[bootloaders[0].GetInstanceID()] = scene.path;
        }

        private void RecordFixtureSceneRoots(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                !_ownedSceneHandles.Contains(scene.handle) ||
                !IsFixtureScenePath(scene.path))
            {
                _ownershipFailure =
                    "Refusing to record roots for an unowned or unexpected scene: " + scene.path;
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                _ownedRootInstanceIds.Add(root.GetInstanceID());
            }
        }

        private void AdoptExactLateRoot(
            GameObject candidate,
            string expectedName,
            string expectedScenePath)
        {
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate.transform.parent, Is.Null);
            Assert.That(candidate.name, Is.EqualTo(expectedName));
            Scene scene = candidate.scene;
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(scene.path, Is.EqualTo(expectedScenePath));
            Assert.That(_ownedSceneHandles.Contains(scene.handle), Is.True);
            Assert.That(
                _ownedRootInstanceIds.Contains(candidate.GetInstanceID()),
                Is.False,
                "The host-created root must be absent from the initial scene-load snapshot.");
            _ownedRootInstanceIds.Add(candidate.GetInstanceID());
        }

        private void AssertExactObjectUnderOwnedRoot(
            GameObject candidate,
            string expectedName,
            string expectedScenePath)
        {
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate.name, Is.EqualTo(expectedName));
            Scene scene = candidate.scene;
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            Assert.That(scene.path, Is.EqualTo(expectedScenePath));
            Assert.That(_ownedSceneHandles.Contains(scene.handle), Is.True);
            GameObject root = candidate.transform.root.gameObject;
            Assert.That(root, Is.Not.SameAs(candidate));
            Assert.That(
                _ownedRootInstanceIds.Contains(root.GetInstanceID()),
                Is.True,
                "The exact late child must remain contained by a fixture-owned scene root.");
        }

        private void AssertFixtureSceneOwnershipIsClean()
        {
            Assert.That(_ownershipFailure, Is.Empty);
            Assert.That(TryVerifyOnlyFixtureOwnedBootloaders(out string message), Is.True, message);
        }

        private bool TryVerifyOnlyFixtureOwnedBootloaders(out string message)
        {
            foreach (Bootloader bootloader in
                     UnityEngine.Object.FindObjectsOfType<Bootloader>(includeInactive: true))
            {
                Scene scene = bootloader.gameObject.scene;
                if (!_ownedBootloaderScenePaths.TryGetValue(
                        bootloader.GetInstanceID(),
                        out string ownedPath) ||
                    !_ownedSceneHandles.Contains(scene.handle) ||
                    !string.Equals(ownedPath, scene.path, StringComparison.Ordinal) ||
                    !IsFixtureScenePath(scene.path))
                {
                    message =
                        "Refusing teardown because Bootloader " + bootloader.GetInstanceID() +
                        " in scene " + scene.path + " is not fixture-owned.";
                    return false;
                }
            }

            message = string.Empty;
            return true;
        }

        private static bool IsFixtureScenePath(string path)
        {
            return string.Equals(
                       path,
                       EditorGameTestModeBootstrap.ExpectedBootScenePath,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       path,
                       FirstUserGameTestRuntimeHost.RealmSelectionPath,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       path,
                       FirstUserGameTestRuntimeHost.ChampionArenaPath,
                       StringComparison.Ordinal);
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            {
                _severeLogs.Add(condition ?? string.Empty);
            }
        }

        private IEnumerator WaitForAndAdoptExactCombatRuntimeRoots()
        {
            float started = Time.realtimeSinceStartup;
            GameObject audioRoot = null;
            GameObject vfxRoot = null;
            while (audioRoot == null || vfxRoot == null)
            {
                if (Time.realtimeSinceStartup - started > TimeoutSeconds)
                {
                    Assert.Fail(
                        "Basic-attack exercise did not create both exact combat runtime roots.");
                }

                audioRoot = FindSingleExactSceneRoot(CombatAudioRootName);
                vfxRoot = FindSingleExactSceneRoot(RuntimeVfxPoolRootName);
                yield return null;
            }

            AdoptExactLateCombatRoot(
                audioRoot,
                CombatAudioRootName,
                typeof(AudioSource));
            AdoptExactLateCombatRoot(
                vfxRoot,
                RuntimeVfxPoolRootName,
                typeof(RuntimeVfxPool));
            GameObject feedbackRoot = FindSingleExactSceneRoot(CombatFeedbackRootName);
            if (feedbackRoot != null)
            {
                AdoptExactLateCombatRoot(
                    feedbackRoot,
                    CombatFeedbackRootName,
                    typeof(MonoBehaviour));
            }

            AssertCombatStaticOwnersMatchExactRoots(audioRoot, vfxRoot, feedbackRoot);
        }

        private void AdoptExactLateCombatRoot(
            GameObject candidate,
            string expectedName,
            Type expectedComponentType)
        {
            Assert.That(candidate, Is.Not.Null);
            Assert.That(candidate.name, Is.EqualTo(expectedName));
            Assert.That(candidate.transform.parent, Is.Null);
            Assert.That(candidate.scene.IsValid(), Is.True);
            Assert.That(candidate.scene.path, Is.EqualTo(DontDestroyOnLoadScenePath),
                "The exact late combat root must live only in Unity's DontDestroyOnLoad scene.");
            Assert.That(candidate.GetComponent(expectedComponentType), Is.Not.Null);
            Assert.That(_ownedLateCombatRoots.ContainsKey(expectedName), Is.False);
            _ownedLateCombatRoots.Add(expectedName, candidate);
        }

        private static GameObject FindSingleExactSceneRoot(string expectedName)
        {
            GameObject[] matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(candidate =>
                    candidate != null &&
                    candidate.scene.IsValid() &&
                    candidate.transform.parent == null &&
                    string.Equals(candidate.name, expectedName, StringComparison.Ordinal))
                .ToArray();
            Assert.That(
                matches,
                Has.Length.LessThanOrEqualTo(1),
                "The isolated test refuses ambiguous exact runtime roots named " + expectedName + ".");
            return matches.Length == 1 ? matches[0] : null;
        }

        private static void AssertCombatStaticOwnersMatchExactRoots(
            GameObject audioRoot,
            GameObject vfxRoot,
            GameObject feedbackRoot)
        {
            AudioSource audioSource = GetRequiredStaticField<AudioSource>(
                typeof(RuntimeCombatAudio),
                "_source");
            RuntimeVfxPool vfxPool = GetRequiredStaticField<RuntimeVfxPool>(
                typeof(RuntimeVfxPool),
                "_instance");
            var feedbackHost = GetRequiredStaticField<Component>(
                typeof(RuntimeCombatFeedback),
                "_host");
            Assert.That(audioSource, Is.Not.Null);
            Assert.That(vfxPool, Is.Not.Null);
            Assert.That(audioSource.gameObject, Is.SameAs(audioRoot));
            Assert.That(vfxPool.gameObject, Is.SameAs(vfxRoot));
            Assert.That(
                feedbackHost == null ? null : feedbackHost.gameObject,
                Is.SameAs(feedbackRoot));
        }

        private static string CaptureCombatRuntimeObservation()
        {
            AudioSource audioSource = GetRequiredStaticField<AudioSource>(
                typeof(RuntimeCombatAudio),
                "_source");
            RuntimeVfxPool vfxPool = GetRequiredStaticField<RuntimeVfxPool>(
                typeof(RuntimeVfxPool),
                "_instance");
            var clips = GetRequiredStaticField<IDictionary>(
                typeof(RuntimeCombatAudio),
                "Clips");
            var pools = GetRequiredStaticField<IDictionary>(
                typeof(RuntimeVfxPool),
                "Pools");
            var activeCounts = GetRequiredStaticField<IDictionary>(
                typeof(RuntimeVfxPool),
                "ActiveCounts");
            int activeTotal = 0;
            foreach (DictionaryEntry entry in activeCounts)
            {
                activeTotal += Convert.ToInt32(
                    entry.Value,
                    CultureInfo.InvariantCulture);
            }

            return string.Join(
                "|",
                audioSource == null ? 0 : audioSource.gameObject.GetInstanceID(),
                audioSource != null && audioSource.isPlaying ? 1 : 0,
                audioSource == null || audioSource.clip == null
                    ? 0
                    : audioSource.clip.GetInstanceID(),
                clips.Count,
                vfxPool == null ? 0 : vfxPool.gameObject.GetInstanceID(),
                pools.Count,
                activeCounts.Count,
                activeTotal,
                FindSingleExactSceneRoot(CombatFeedbackRootName) == null
                    ? 0
                    : FindSingleExactSceneRoot(CombatFeedbackRootName).GetInstanceID());
        }

        private void DestroyOwnedCombatRuntimeState(Action<string> recordFailure)
        {
            if (_ownedLateCombatRoots.Count == 0)
            {
                if (!TryVerifyCombatRuntimeIsSterile(out string untouchedMessage))
                {
                    recordFailure(
                        "Refusing to alter unadopted combat runtime state. " + untouchedMessage);
                }

                return;
            }

            bool feedbackWasAdopted = _ownedLateCombatRoots.TryGetValue(
                CombatFeedbackRootName,
                out GameObject feedbackRoot);
            if ((_ownedLateCombatRoots.Count != 2 && _ownedLateCombatRoots.Count != 3) ||
                !_ownedLateCombatRoots.TryGetValue(CombatAudioRootName, out GameObject audioRoot) ||
                !_ownedLateCombatRoots.TryGetValue(RuntimeVfxPoolRootName, out GameObject vfxRoot) ||
                audioRoot == null || vfxRoot == null ||
                FindSingleExactSceneRoot(CombatAudioRootName) != audioRoot ||
                FindSingleExactSceneRoot(RuntimeVfxPoolRootName) != vfxRoot ||
                feedbackWasAdopted &&
                (feedbackRoot == null ||
                 FindSingleExactSceneRoot(CombatFeedbackRootName) != feedbackRoot) ||
                !feedbackWasAdopted &&
                FindSingleExactSceneRoot(CombatFeedbackRootName) != null)
            {
                recordFailure(
                    "Refusing combat cleanup because the exact adopted runtime-root ledger drifted.");
                return;
            }

            FieldInfo sourceField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatAudio),
                "_source");
            FieldInfo clipsField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatAudio),
                "Clips");
            FieldInfo instanceField = GetRequiredStaticFieldInfo(
                typeof(RuntimeVfxPool),
                "_instance");
            FieldInfo poolsField = GetRequiredStaticFieldInfo(
                typeof(RuntimeVfxPool),
                "Pools");
            FieldInfo activeCountsField = GetRequiredStaticFieldInfo(
                typeof(RuntimeVfxPool),
                "ActiveCounts");
            FieldInfo feedbackHostField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatFeedback),
                "_host");
            FieldInfo hitPauseRoutineField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatFeedback),
                "_hitPauseRoutine");

            var source = sourceField.GetValue(null) as AudioSource;
            var instance = instanceField.GetValue(null) as RuntimeVfxPool;
            var feedbackHost = feedbackHostField.GetValue(null) as MonoBehaviour;
            var hitPauseRoutine = hitPauseRoutineField.GetValue(null) as Coroutine;
            var clips = clipsField.GetValue(null) as IDictionary;
            var pools = poolsField.GetValue(null) as IDictionary;
            var activeCounts = activeCountsField.GetValue(null) as IDictionary;
            if (source == null || source.gameObject != audioRoot ||
                instance == null || instance.gameObject != vfxRoot ||
                clips == null || pools == null || activeCounts == null ||
                activeCounts.Values.Cast<object>().Any(value => Convert.ToInt32(value) != 0) ||
                pools.Values.Cast<IEnumerable>().SelectMany(queue => queue.Cast<object>())
                    .OfType<GameObject>()
                    .Any(effect => effect != null && effect.transform.root.gameObject != vfxRoot) ||
                feedbackWasAdopted &&
                (feedbackHost == null || feedbackHost.gameObject != feedbackRoot) ||
                !feedbackWasAdopted && (feedbackHost != null || hitPauseRoutine != null))
            {
                recordFailure(
                    "Refusing combat cleanup because an exact static owner or pooled child was not fixture-owned.");
                return;
            }

            var ownedClips = clips.Values.Cast<object>().OfType<AudioClip>().ToArray();
            sourceField.SetValue(null, null);
            instanceField.SetValue(null, null);
            if (hitPauseRoutine != null && feedbackHost != null)
            {
                feedbackHost.StopCoroutine(hitPauseRoutine);
            }

            hitPauseRoutineField.SetValue(null, null);
            feedbackHostField.SetValue(null, null);
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            clips.Clear();
            pools.Clear();
            activeCounts.Clear();
            foreach (AudioClip clip in ownedClips)
            {
                if (clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }

            UnityEngine.Object.DestroyImmediate(audioRoot);
            UnityEngine.Object.DestroyImmediate(vfxRoot);
            if (feedbackRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(feedbackRoot);
            }

            _ownedLateCombatRoots.Clear();
        }

        private static bool TryVerifyCombatRuntimeIsSterile(out string message)
        {
            FieldInfo sourceField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatAudio),
                "_source");
            FieldInfo clipsField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatAudio),
                "Clips");
            FieldInfo instanceField = GetRequiredStaticFieldInfo(
                typeof(RuntimeVfxPool),
                "_instance");
            FieldInfo poolsField = GetRequiredStaticFieldInfo(
                typeof(RuntimeVfxPool),
                "Pools");
            FieldInfo activeCountsField = GetRequiredStaticFieldInfo(
                typeof(RuntimeVfxPool),
                "ActiveCounts");
            FieldInfo feedbackHostField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatFeedback),
                "_host");
            FieldInfo hitPauseRoutineField = GetRequiredStaticFieldInfo(
                typeof(RuntimeCombatFeedback),
                "_hitPauseRoutine");
            var clips = clipsField.GetValue(null) as IDictionary;
            var pools = poolsField.GetValue(null) as IDictionary;
            var activeCounts = activeCountsField.GetValue(null) as IDictionary;

            bool clean = sourceField.GetValue(null) == null &&
                         instanceField.GetValue(null) == null &&
                         clips != null && clips.Count == 0 &&
                         pools != null && pools.Count == 0 &&
                         activeCounts != null && activeCounts.Count == 0 &&
                         feedbackHostField.GetValue(null) == null &&
                         hitPauseRoutineField.GetValue(null) == null &&
                         FindSingleExactSceneRoot(CombatAudioRootName) == null &&
                         FindSingleExactSceneRoot(RuntimeVfxPoolRootName) == null &&
                         FindSingleExactSceneRoot(CombatFeedbackRootName) == null;
            message = clean
                ? string.Empty
                : "Combat audio/VFX roots or static singleton state were not sterile.";
            return clean;
        }

        private static T GetRequiredStaticField<T>(Type owner, string fieldName)
            where T : class
        {
            return GetRequiredStaticFieldInfo(owner, fieldName).GetValue(null) as T;
        }

        private static FieldInfo GetRequiredStaticFieldInfo(Type owner, string fieldName)
        {
            FieldInfo field = owner.GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, owner.FullName + "." + fieldName);
            return field;
        }

        private EditorGameTestModePlan CreateOwnedPlan()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            _tutorialSessionIds.Add(sessionId);
            string temporaryRoot = Path.GetTempPath();
            string isolatedRoot = EditorGameTestModeBootstrap.BuildExpectedIsolatedRoot(
                temporaryRoot,
                sessionId);
            Assert.That(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                temporaryRoot,
                Application.persistentDataPath,
                isolatedRoot,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out EditorGameTestModePlan plan,
                out _,
                out string message), Is.True, message);
            Assert.That(EditorGameTestModeBootstrap.TryCreateOwnedRoot(
                plan,
                out _,
                out string creationMessage), Is.True, creationMessage);
            _generatedRoots.Add(plan.IsolatedSaveRoot);
            return plan;
        }

        private EditorGameTestModePlan CreatePlanForExistingRoot(string root)
        {
            string sessionId = Path.GetFileName(root);
            Assert.That(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                Path.GetTempPath(),
                Application.persistentDataPath,
                root,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out EditorGameTestModePlan plan,
                out _,
                out string message), Is.True, message);
            return plan;
        }

        private void SnapshotAndClearRuntimeState()
        {
            EditorGameTestModeBootstrap.Disarm();
            _originalFactoryValues.Clear();
            foreach (string fieldName in new[]
            {
                "GameDataFactoryOverride", "SaveGameFactoryOverride", "ResourceFactoryOverride",
                "NotificationFactoryOverride", "BossLootFactoryOverride"
            })
            {
                FieldInfo field = _stackType.GetField(
                    fieldName,
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null)
                {
                    _originalFactoryValues[field] = field.GetValue(null);
                    field.SetValue(null, null);
                }
            }

            IDictionary services = (IDictionary)_servicesField.GetValue(null);
            _originalServices.Clear();
            foreach (DictionaryEntry entry in services)
            {
                _originalServices[entry.Key] = entry.Value;
            }

            services.Clear();
        }

        private void RestoreRuntimeState()
        {
            IDictionary services = (IDictionary)_servicesField.GetValue(null);
            services.Clear();
            foreach (KeyValuePair<object, object> entry in _originalServices)
            {
                services[entry.Key] = entry.Value;
            }

            foreach (KeyValuePair<FieldInfo, object> entry in _originalFactoryValues)
            {
                entry.Key.SetValue(null, entry.Value);
            }
        }

        private void DestroyFixtureOwnedSceneRoots(Action<string> recordFailure)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded || _preexistingSceneHandles.Contains(scene.handle))
                {
                    continue;
                }

                if (!_ownedSceneHandles.Contains(scene.handle))
                {
                    recordFailure(
                        "Refusing to alter non-fixture-owned scene during teardown: " +
                        scene.path + " (" + scene.name + ").");
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (!_ownedRootInstanceIds.Contains(root.GetInstanceID()))
                    {
                        recordFailure(
                            "Refusing to destroy an unrecorded root in fixture scene " +
                            scene.path + ": " + root.name + ".");
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private sealed class SaveGameServiceIdentityProxy : ISaveGameService
        {
            private readonly ISaveGameService _source;

            internal SaveGameServiceIdentityProxy(ISaveGameService source)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public SaveGameData CurrentSave => _source.CurrentSave;
            public SaveLoadStatus LastLoadStatus => _source.LastLoadStatus;
            public string LastLoadMessage => _source.LastLoadMessage;
            public SaveOperationStatus LastSaveStatus => _source.LastSaveStatus;
            public string LastSaveMessage => _source.LastSaveMessage;
            public void Save() => throw new InvalidOperationException("Identity proxy cannot write.");
            public void Load() => throw new InvalidOperationException("Identity proxy cannot load.");
            public bool HasSave() => _source.HasSave();
            public void CreateNewSave(RealmId realmId) =>
                throw new InvalidOperationException("Identity proxy cannot create a save.");
            public void DeleteSave() =>
                throw new InvalidOperationException("Identity proxy cannot delete a save.");
        }

        private sealed class ReadOnlyPersistentInventory : IEquatable<ReadOnlyPersistentInventory>
        {
            private const int MaximumEntries = 4096;
            private const long MaximumBytes = 536870912L;

            private ReadOnlyPersistentInventory(string digest, int fileCount, long byteCount)
            {
                Digest = digest;
                FileCount = fileCount;
                ByteCount = byteCount;
            }

            private string Digest { get; }
            private int FileCount { get; }
            private long ByteCount { get; }

            internal static ReadOnlyPersistentInventory Capture(string root)
            {
                string normalizedRoot = Path.GetFullPath(root);
                if (!Directory.Exists(normalizedRoot))
                {
                    return new ReadOnlyPersistentInventory("missing", 0, 0L);
                }

                var files = new List<string>();
                var pending = new Stack<string>();
                pending.Push(normalizedRoot);
                while (pending.Count > 0)
                {
                    string directory = pending.Pop();
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException(
                            "persistentDataPath inventory refuses reparse point: " + directory);
                    }

                    foreach (string child in Directory.GetDirectories(directory)
                                 .OrderBy(path => path, StringComparer.Ordinal))
                    {
                        pending.Push(child);
                    }

                    files.AddRange(Directory.GetFiles(directory));
                    if (files.Count > MaximumEntries)
                    {
                        throw new InvalidOperationException(
                            "persistentDataPath inventory exceeds the bounded entry count.");
                    }
                }

                files.Sort(StringComparer.Ordinal);
                long totalBytes = 0L;
                using (var manifest = new MemoryStream())
                {
                    foreach (string path in files)
                    {
                        var info = new FileInfo(path);
                        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            throw new InvalidOperationException(
                                "persistentDataPath inventory refuses reparse file: " + path);
                        }

                        totalBytes = checked(totalBytes + info.Length);
                        if (totalBytes > MaximumBytes)
                        {
                            throw new InvalidOperationException(
                                "persistentDataPath inventory exceeds the bounded byte count.");
                        }

                        string relative = path.Substring(normalizedRoot.Length)
                            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Replace('\\', '/');
                        WriteUtf8(manifest, relative);
                        WriteUtf8(manifest, info.Length.ToString(CultureInfo.InvariantCulture));
                        using (FileStream input = File.Open(
                                   path,
                                   FileMode.Open,
                                   FileAccess.Read,
                                   FileShare.ReadWrite | FileShare.Delete))
                        using (SHA256 fileSha = SHA256.Create())
                        {
                            WriteUtf8(manifest, ToHex(fileSha.ComputeHash(input)));
                        }
                    }

                    using (SHA256 manifestSha = SHA256.Create())
                    {
                        return new ReadOnlyPersistentInventory(
                            ToHex(manifestSha.ComputeHash(manifest.ToArray())),
                            files.Count,
                            totalBytes);
                    }
                }
            }

            public bool Equals(ReadOnlyPersistentInventory other)
            {
                return other != null &&
                       FileCount == other.FileCount &&
                       ByteCount == other.ByteCount &&
                       string.Equals(Digest, other.Digest, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) =>
                obj is ReadOnlyPersistentInventory other && Equals(other);

            public override int GetHashCode() =>
                (Digest ?? string.Empty).GetHashCode() ^ FileCount ^ ByteCount.GetHashCode();

            internal string DescribeDifference(ReadOnlyPersistentInventory other)
            {
                return "before=" + FileCount + "/" + ByteCount + "/" + Digest +
                       "; after=" + (other == null
                           ? "null"
                           : other.FileCount + "/" + other.ByteCount + "/" + other.Digest);
            }

            private static void WriteUtf8(Stream stream, string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                stream.WriteByte((byte)(bytes.Length >> 24));
                stream.WriteByte((byte)(bytes.Length >> 16));
                stream.WriteByte((byte)(bytes.Length >> 8));
                stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }

            private static string ToHex(byte[] bytes)
            {
                const string alphabet = "0123456789abcdef";
                var chars = new char[bytes.Length * 2];
                for (int index = 0; index < bytes.Length; index++)
                {
                    chars[index * 2] = alphabet[bytes[index] >> 4];
                    chars[index * 2 + 1] = alphabet[bytes[index] & 0x0f];
                }

                return new string(chars);
            }
        }
    }
}
#endif
