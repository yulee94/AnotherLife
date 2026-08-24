using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.UI.Kingdom;
using AL.UI.RealmSelection;
using AL.UI.SharedMenu;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    /// <summary>
    /// Lifecycle contract for the committed production scenes after the accepted #153/#241 scene-lifecycle
    /// contract (#223 "Required tests" lifecycle family). Drives Boot -> RealmSelection -> Kingdom ->
    /// ChampionArena -> Kingdom by PATH (name-based LoadScene cannot resolve outside Build Settings
    /// in-editor) and asserts: exactly one Bootloader owner per step, offline load exactly once, tick and
    /// pause-save ownership continue across transitions via #241 standby-reclaim, and duplicate-owner
    /// determinism (distinct per-scene owner ids, single live owner each step).
    ///
    /// The scene controllers are quiesced in the sceneLoaded callback (which runs after Awake/OnEnable but
    /// before Start): disabling the controller prevents interactive scene work and the heavy
    /// Kingdom/ChampionArena world build, leaving the Bootloader owner and startup marker intact.
    ///
    /// Isolation: the offline save service is replaced with an in-memory controllable double via the
    /// OfflineServiceStack save-factory seam, so NO developer profile is read or written (decision D5).
    /// Evidence produced by this test is classified "produced, pending #127 PlayMode-harness acceptance".
    /// </summary>
    public sealed class ProductionSceneLifecycleTests
    {
        private const string BootPath = "Assets/AL/Scenes/Boot.unity";
        private const string RealmSelectionPath = "Assets/AL/Scenes/RealmSelection.unity";
        private const string KingdomPath = "Assets/AL/Scenes/Kingdom.unity";
        private const string ChampionArenaPath = "Assets/AL/Scenes/ChampionArena.unity";

        private const float LoadTimeoutSeconds = 30f;

        private static readonly string[] ControllerTypeNames =
        {
            "AL.UI.BootController",
            "AL.UI.RealmSelection.RealmSelectionController",
            "AL.UI.Kingdom.KingdomSceneController",
            "AL.ChampionMode.ChampionArenaSceneController"
        };

        private bool _originalIgnoreFailingMessages;
        private LogTap _logs;
        private object _controllableSave;
        private object _countingResource;
        private bool _quiesceSceneControllers;
        private readonly Dictionary<string, int> _expectedActivations = new Dictionary<string, int>();

        private static void CapturePresentationCamera(string outputPath)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "The kingdom presentation camera must exist.");
            const int width = 1600;
            const int height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static void CapturePresentationCameraWithHud(string outputPath)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "The kingdom presentation camera must exist.");
            Canvas[] overlays = Object.FindObjectsOfType<Canvas>()
                .Where(canvas =>
                    canvas != null &&
                    canvas.isActiveAndEnabled &&
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .ToArray();
            Camera[] originalCameras = overlays.Select(canvas => canvas.worldCamera).ToArray();
            float[] originalPlaneDistances = overlays.Select(canvas => canvas.planeDistance).ToArray();
            try
            {
                foreach (Canvas overlay in overlays)
                {
                    overlay.renderMode = RenderMode.ScreenSpaceCamera;
                    overlay.worldCamera = camera;
                    overlay.planeDistance = 1f;
                }

                Canvas.ForceUpdateCanvases();
                CapturePresentationCamera(outputPath);
            }
            finally
            {
                for (int index = 0; index < overlays.Length; index++)
                {
                    overlays[index].renderMode = RenderMode.ScreenSpaceOverlay;
                    overlays[index].worldCamera = originalCameras[index];
                    overlays[index].planeDistance = originalPlaneDistances[index];
                }

                Canvas.ForceUpdateCanvases();
            }
        }

        private static void CaptureHudEvidence(string outputPath)
        {
            string captureDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                Directory.CreateDirectory(captureDirectory);
            }

            CapturePresentationCameraWithHud(outputPath);
            Assert.That(
                File.Exists(outputPath),
                Is.True,
                "The requested private-kingdom HUD capture must be written.");
        }

        [SetUp]
        public void SetUp()
        {
            _expectedActivations.Clear();
            _quiesceSceneControllers = true;
            _originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            // Expected reverse-ordering handoff logs one [BOOT_STACK_RUNTIME_OWNER_REJECTED] error per
            // transition; classify logs ourselves rather than letting the runner auto-fail on them.
            LogAssert.ignoreFailingMessages = true;

            ClearServiceLocator();
            ResetStackOverrides();

            _controllableSave = NewInternal("AL.Core.ControllableSaveGameService");
            _countingResource = NewInternal("AL.Core.CountingResourceService");
            Func<object> saveFactory = () => _controllableSave;
            Func<object, object> resourceFactory = _ => _countingResource;
            SetStackOverride("SaveGameFactoryOverride", saveFactory);
            SetStackOverride("ResourceFactoryOverride", resourceFactory);

            _logs = new LogTap();
            _logs.Start();

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Corrected #127 ordering: stop the collector, then unload the gameplay scene(s) so the owning
            // Bootloader is destroyed (OnDestroy releases its claim) BEFORE clearing the ServiceLocator.
            // Clearing services while a live owner remains would trip the drift-LogError / standby writer.
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _logs?.Stop();
            _logs = null;

            yield return UnloadIntoEmptyScene();
            ResetStackOverrides();
            ClearServiceLocator();
            LogAssert.ignoreFailingMessages = _originalIgnoreFailingMessages;
        }

        // Quiesce scene controllers before their Start runs: sceneLoaded fires after Awake/OnEnable but
        // before Start, and a disabled Behaviour never receives Start. The Bootloader owner (separate root)
        // and the SceneStartupMarker stay enabled.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_quiesceSceneControllers)
            {
                return;
            }

            foreach (string typeName in ControllerTypeNames)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(typeName))
                    .FirstOrDefault(candidate => candidate != null);
                if (type == null)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren(type, true))
                    {
                        if (component is Behaviour behaviour)
                        {
                            behaviour.enabled = false;
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator ProductionSceneFlowKeepsSingleOwnerLoadsOnceAndKeepsTickAndSaveOwnership()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production scene lifecycle test drives editor path-based PlayMode loads.");
            yield break;
#else
            var ownerIds = new List<string>();

            // Boot: first load builds the stack and loads offline progress exactly once.
            yield return LoadAndSettle(BootPath);
            AssertSingleOwner("al_scene_boot", "Boot", BootPath, ownerIds);
            Assert.AreEqual(1, LoadCount(), "Boot must load offline progress exactly once.");
            int ticksAfterBoot = TickCount();

            // Boot -> RealmSelection: owner is destroyed on unload and reclaimed without a second load.
            yield return LoadAndSettle(RealmSelectionPath);
            AssertSingleOwner("al_scene_realm_selection", "RealmSelection", RealmSelectionPath, ownerIds);
            Assert.AreEqual(1, LoadCount(), "No second offline load across Boot->RealmSelection.");

            // RealmSelection -> Kingdom.
            yield return LoadAndSettle(KingdomPath);
            AssertSingleOwner("al_scene_kingdom", "Kingdom", KingdomPath, ownerIds);
            int ticksAfterKingdom = TickCount();
            Assert.Greater(ticksAfterKingdom, ticksAfterBoot, "Production tick ownership must continue after transitions.");

            // Kingdom -> ChampionArena (deferred gameplay scene still drives lifecycle by path).
            yield return LoadAndSettle(ChampionArenaPath);
            AssertSingleOwner("al_scene_champion_arena", "ChampionArena", ChampionArenaPath, ownerIds);
            Assert.AreEqual(1, LoadCount(), "No second offline load across the full flow.");

            // ChampionArena -> Kingdom (return).
            yield return LoadAndSettle(KingdomPath);
            AssertSingleOwner("al_scene_kingdom", "Kingdom", KingdomPath, ownerIds);

            // Pause-save ownership continues on the current owner.
            SeedCurrentSave();
            int savesBeforePause = SaveCount();
            InvokeOnCurrentOwner("OnApplicationPause", true);
            Assert.Greater(SaveCount(), savesBeforePause, "Pause-save ownership must continue on the current owner.");

            // Quit-save ownership (owner-gated) continues analogously.
            SeedCurrentSave();
            int savesBeforeQuit = SaveCount();
            InvokeOnCurrentOwner("OnApplicationQuit");
            Assert.Greater(SaveCount(), savesBeforeQuit, "Quit-save ownership must continue on the current owner.");

            int ticksFinal = TickCount();
            Assert.Greater(ticksFinal, ticksAfterKingdom, "Tick ownership must persist through the return to Kingdom.");
            Assert.AreEqual(1, LoadCount(), "Offline progress must be loaded exactly once across the whole flow.");

            // Duplicate-owner determinism: each scene had exactly one owner, and each owner id is distinct
            // (a fresh per-scene Bootloader reclaimed after the previous owner's OnDestroy).
            Assert.AreEqual(ownerIds.Count, ownerIds.Distinct().Count(), "Each scene must have a distinct single owner id.");

            // No marker mismatch anywhere, and the only tolerated error is the reverse-ordering handoff.
            Assert.IsFalse(_logs.Logs.Any(l => l.Contains("[AL-SCENE-ACTIVE-MISMATCH]")), "A startup marker reported a mismatch.");
            var unexpected = _logs.Errors
                .Where(message => !message.Contains("BOOT_STACK_RUNTIME_OWNER_REJECTED"))
                .ToList();
            Assert.IsEmpty(unexpected, "Unexpected severe logs:\n" + string.Join("\n", unexpected));
#endif
        }

        [UnityTest]
        public IEnumerator SharedMenuDrivesChampionToPrivateKingdomAndBackWithOwnerAndWriteAuthority()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Private kingdom round-trip drives editor PlayMode scene loads.");
            yield break;
#else
            SaveGameData save = SeedLordshipSave(RealmId.Stonehold);
            _quiesceSceneControllers = false;
            var ownerIds = new List<string>();

            yield return LoadAndSettle(ChampionArenaPath);
            AssertExclusiveScene(ChampionArenaPath, KingdomPath);
            AssertSingleOwner(
                "al_scene_champion_arena",
                SharedMenuIds.AdventureScene,
                ChampionArenaPath,
                ownerIds);
            AssertInnerRealmControlIsLive();

            SharedMenuModeSwitchHost adventureHost =
                SharedMenuModeSwitchHost.EnsureForSceneName(SharedMenuIds.AdventureScene);
            Assert.That(adventureHost, Is.Not.Null);
            adventureHost.Open();
            Assert.That(adventureHost.Overlay.KingdomButton.name, Is.EqualTo(SharedMenuIds.KingdomManagementModule));
            Assert.That(adventureHost.Overlay.KingdomButton.interactable, Is.True);
            Assert.That(adventureHost.CommitFromMenu(), Is.True);

            yield return WaitForActiveScene(KingdomPath);
            AssertExclusiveScene(KingdomPath, ChampionArenaPath);
            AssertSingleOwner(
                "al_scene_kingdom",
                SharedMenuIds.KingdomScene,
                KingdomPath,
                ownerIds);
            string privateKingdomCapture =
                Environment.GetEnvironmentVariable("AL_PRIVATE_KINGDOM_CAPTURE");
            if (!string.IsNullOrWhiteSpace(privateKingdomCapture))
            {
                yield return null;
                string captureDirectory = Path.GetDirectoryName(privateKingdomCapture);
                if (!string.IsNullOrWhiteSpace(captureDirectory))
                {
                    Directory.CreateDirectory(captureDirectory);
                }
                CapturePresentationCamera(privateKingdomCapture);
                Assert.That(
                    File.Exists(privateKingdomCapture),
                    Is.True,
                    "The requested private-kingdom evidence capture must be written.");
            }
            string privateKingdomHudCapture =
                Environment.GetEnvironmentVariable("AL_PRIVATE_KINGDOM_HUD_CAPTURE");
            if (!string.IsNullOrWhiteSpace(privateKingdomHudCapture))
            {
                CaptureHudEvidence(privateKingdomHudCapture);
            }
            string privateKingdomMapCapture =
                Environment.GetEnvironmentVariable("AL_PRIVATE_KINGDOM_MAP_CAPTURE");
            if (!string.IsNullOrWhiteSpace(privateKingdomMapCapture))
            {
                GameObject mapButtonObject = GameObject.Find("MAP");
                Assert.That(mapButtonObject, Is.Not.Null, "The private-kingdom map button must exist.");
                Button mapButton = mapButtonObject.GetComponent<Button>();
                Assert.That(mapButton, Is.Not.Null);
                Assert.That(mapButton.interactable, Is.True);
                mapButton.onClick.Invoke();
                yield return null;
                CaptureHudEvidence(privateKingdomMapCapture);
            }
            Assert.That(Object.FindObjectOfType<ChampionController>(), Is.Null);
            Assert.That(CrossModeSession.HasActiveRoundTrip, Is.True);
            Assert.That(CrossModeSession.AdventureScene, Is.EqualTo(SharedMenuIds.AdventureScene));

            SharedMenuModeSwitchHost kingdomHost =
                SharedMenuModeSwitchHost.EnsureForSceneName(SharedMenuIds.KingdomScene);
            Assert.That(kingdomHost, Is.Not.Null);
            kingdomHost.Open();
            Assert.That(kingdomHost.Overlay.KingdomButton.interactable, Is.True);
            Assert.That(kingdomHost.CommitFromMenu(), Is.True);

            yield return WaitForActiveScene(ChampionArenaPath);
            AssertExclusiveScene(ChampionArenaPath, KingdomPath);
            AssertSingleOwner(
                "al_scene_champion_arena",
                SharedMenuIds.AdventureScene,
                ChampionArenaPath,
                ownerIds);
            AssertInnerRealmControlIsLive();

            SaveGameData roundTripped = (SaveGameData)InstanceField(_controllableSave, "_currentSave");
            Assert.That(roundTripped, Is.SameAs(save), "The same inner-realm save session must survive both loads.");
            Assert.That(roundTripped.SelectedRealm, Is.EqualTo(RealmId.Stonehold));
            Assert.That(roundTripped.ChampionCustomization.LastResultId, Is.EqualTo(ProofOfWorthIds.StoneholdVariantId));
            Assert.That(LoadCount(), Is.EqualTo(1), "The round-trip must not reload offline progress.");

            int savesBeforePause = SaveCount();
            InvokeOnCurrentOwner("OnApplicationPause", true);
            Assert.That(
                SaveCount(),
                Is.GreaterThan(savesBeforePause),
                "The returned ChampionArena Bootloader must retain lifecycle write authority.");
            Assert.That(ownerIds.Distinct().Count(), Is.EqualTo(ownerIds.Count));

            Assert.That(_logs.Logs.Any(line => line.Contains("[AL-SCENE-ACTIVE-MISMATCH]")), Is.False);
            var unexpected = _logs.Errors
                .Where(message => !message.Contains("BOOT_STACK_RUNTIME_OWNER_REJECTED"))
                .ToList();
            Assert.IsEmpty(unexpected, "Unexpected severe logs:\n" + string.Join("\n", unexpected));
#endif
        }

        [UnityTest]
        public IEnumerator BootWaitsForExplicitContinueThenReachesRealmSelectionWithFourControls()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production scene transition test drives editor path-based PlayMode loads.");
            yield break;
#else
            SeedCurrentSave();
            _quiesceSceneControllers = false;
            yield return LoadAndSettle(BootPath);

            float readyStarted = Time.realtimeSinceStartup;
            Button continueButton = null;
            while (continueButton == null ||
                   !continueButton.gameObject.activeInHierarchy ||
                   !continueButton.interactable)
            {
                if (Time.realtimeSinceStartup - readyStarted > LoadTimeoutSeconds)
                {
                    Assert.Fail("Boot did not expose the truthful Finished Loading action.");
                }

                GameObject canvas = GameObject.Find("LaunchReadinessCanvas");
                continueButton = canvas == null
                    ? null
                    : canvas.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(button => button.name == "FinishedLoadingAction");
                yield return null;
            }

            Assert.That(
                SceneManager.GetActiveScene().path,
                Is.EqualTo(BootPath),
                "Readiness must never auto-route without a fresh explicit action.");
            Text status = GameObject.Find("LaunchReadinessCanvas")
                .GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.name == "ReadinessStatus");
            Assert.That(status, Is.Not.Null);
            Assert.That(status.text, Is.EqualTo("Finished Loading"));
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BootPath));

            continueButton.onClick.Invoke();
            continueButton.onClick.Invoke();
            float transitionStarted = Time.realtimeSinceStartup;
            while (!string.Equals(
                       SceneManager.GetActiveScene().path,
                       RealmSelectionPath,
                       StringComparison.Ordinal))
            {
                if (Time.realtimeSinceStartup - transitionStarted > LoadTimeoutSeconds)
                {
                    Assert.Fail("Explicit Continue did not transition to RealmSelection.");
                }

                yield return null;
            }

            float catalogStarted = Time.realtimeSinceStartup;
            while (RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.NotStarted ||
                   RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.Loading)
            {
                if (Time.realtimeSinceStartup - catalogStarted > LoadTimeoutSeconds)
                {
                    Assert.Fail("Realm catalog loading timed out: " + RealmCatalogRuntime.TechnicalCode);
                }

                yield return null;
            }

            // Let RealmSelectionController.Start build the production UI and presentation camera.
            yield return null;
            yield return null;

            Assert.That(RealmCatalogRuntime.Status, Is.EqualTo(RealmCatalogRuntimeStatus.Ready));
            Assert.That(RealmCatalogRuntime.TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-READY"));
            Assert.That(RealmCatalogRuntime.Current, Is.Not.Null);
            Assert.That(LoadCount(), Is.EqualTo(1), "Boot must load the in-memory profile exactly once.");
            Assert.That(
                RealmCatalogRuntime.Current.Realms.Select(realm => realm.Id).ToArray(),
                Is.EqualTo(new[] { "crownlands", "stonehold", "eldergrove", "umbral" }));

            RealmSelectionController controller = Object.FindObjectOfType<RealmSelectionController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.isActiveAndEnabled, Is.True);

            GameObject realmCanvas = GameObject.Find("RealmSelectionCanvas");
            Assert.That(realmCanvas, Is.Not.Null, "The authored production realm UI must be active.");
            string[] realmNames = { "Crownlands", "Stonehold", "Eldergrove", "Umbral" };
            Button[] realmButtons = realmCanvas.GetComponentsInChildren<Button>(true)
                .Where(button => realmNames.Contains(button.name, StringComparer.Ordinal))
                .ToArray();
            Assert.That(realmButtons, Has.Length.EqualTo(4));
            Assert.That(
                realmButtons.Select(button => button.name).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(4),
                "Each catalog realm must produce one distinct authored control.");
            Assert.That(
                realmButtons.All(button => button.interactable),
                Is.True,
                "All four realm choices must be interactable before a selection is committed.");
            Assert.That(
                realmCanvas.GetComponentsInChildren<Button>(true)
                    .Any(button => button.name == RealmSelectionCommitOverlay.ConfirmButtonName),
                Is.True,
                "The production realm UI must carry an explicit binding action.");

            Camera[] presentationCameras = Camera.allCameras
                .Where(camera =>
                    camera != null &&
                    camera.isActiveAndEnabled &&
                    camera.targetTexture == null &&
                    camera.targetDisplay == 0)
                .ToArray();
            Assert.That(presentationCameras, Has.Length.EqualTo(1));
            Assert.That(presentationCameras[0].name, Is.EqualTo("RealmSelectionCamera"));
            Assert.That(presentationCameras[0].cullingMask, Is.Zero);

            Assert.That(
                _logs.Logs.Where(message =>
                    message.IndexOf("AL-REALM-DEFINITION-UNAVAILABLE", StringComparison.Ordinal) >= 0),
                Is.Empty,
                "Realm Selection must not emit the catalog-unavailable failure after Boot.");
#endif
        }

        [UnityTest]
        public IEnumerator BootWithCommittedRealmStillRequiresContinueAndCannotBypassOnboarding()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production scene transition test drives editor path-based PlayMode loads.");
            yield break;
#else
            SeedCurrentSave();
            object seededSave = InstanceField(_controllableSave, "_currentSave");
            FieldInfo selectedRealm = seededSave.GetType().GetField(
                "SelectedRealm",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(selectedRealm, Is.Not.Null);
            selectedRealm.SetValue(
                seededSave,
                Enum.Parse(selectedRealm.FieldType, "Crownlands"));

            _quiesceSceneControllers = false;
            yield return LoadAndSettle(BootPath);

            float readyStarted = Time.realtimeSinceStartup;
            Button continueButton = null;
            while (continueButton == null ||
                   !continueButton.gameObject.activeInHierarchy ||
                   !continueButton.interactable)
            {
                if (Time.realtimeSinceStartup - readyStarted > LoadTimeoutSeconds)
                {
                    Assert.Fail("Committed realm profile did not reach the explicit launch gate.");
                }

                GameObject canvas = GameObject.Find("LaunchReadinessCanvas");
                continueButton = canvas == null
                    ? null
                    : canvas.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(button => button.name == "FinishedLoadingAction");
                yield return null;
            }

            Assert.That(
                SceneManager.GetActiveScene().path,
                Is.EqualTo(BootPath),
                "A committed realm is not authority to bypass the explicit launch action.");
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BootPath));

            continueButton.onClick.Invoke();
            float transitionStarted = Time.realtimeSinceStartup;
            while (!string.Equals(
                       SceneManager.GetActiveScene().path,
                       RealmSelectionPath,
                       StringComparison.Ordinal))
            {
                if (Time.realtimeSinceStartup - transitionStarted > LoadTimeoutSeconds)
                {
                    Assert.Fail("Committed realm profile did not route to the remaining onboarding boundary.");
                }

                yield return null;
            }

            Assert.That(
                Object.FindObjectOfType<RealmSelectionController>(),
                Is.Not.Null,
                "Realm-only evidence must route to onboarding, never Kingdom.");
            Assert.That(
                Object.FindObjectOfType<KingdomSceneController>(),
                Is.Null,
                "Kingdom must not activate from realm-only evidence.");

            var unexpected = _logs.Errors
                .Where(message => !message.Contains("BOOT_STACK_RUNTIME_OWNER_REJECTED"))
                .ToList();
            Assert.IsEmpty(
                unexpected,
                "Realm-only launch containment emitted unexpected severe logs:\n" +
                string.Join("\n", unexpected));
#endif
        }

        [UnityTest]
        public IEnumerator RealmSelectionCameraIsDestroyedBeforeKingdomBuildsItsPresentationCamera()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production scene transition test drives editor path-based PlayMode loads.");
            yield break;
#else
            _quiesceSceneControllers = false;
            yield return LoadAndSettle(RealmSelectionPath);

            // Let RealmSelectionController.Start build the production presentation camera.
            yield return null;
            yield return null;

            Camera realmCamera = Camera.allCameras.Single(camera =>
                camera != null &&
                camera.isActiveAndEnabled &&
                camera.gameObject.scene == SceneManager.GetActiveScene() &&
                camera.targetTexture == null &&
                camera.targetDisplay == 0);
            Assert.That(realmCamera.name, Is.EqualTo("RealmSelectionCamera"));

            SeedCurrentSave();
            object seededSave = InstanceField(_controllableSave, "_currentSave");
            FieldInfo selectedRealm = seededSave.GetType().GetField(
                "SelectedRealm",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(selectedRealm, Is.Not.Null);
            selectedRealm.SetValue(
                seededSave,
                Enum.Parse(selectedRealm.FieldType, "Crownlands"));

            yield return LoadAndSettle(KingdomPath);
            yield return null;
            yield return null;

            Assert.That(
                realmCamera == null,
                Is.True,
                "The Realm Selection presentation camera must be destroyed with its unloaded scene.");

            KingdomSceneController controller = Object.FindObjectOfType<KingdomSceneController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.isActiveAndEnabled, Is.True);
            Assert.That(InstanceField(controller, "_runtimeInitialized"), Is.EqualTo(true));
            Assert.That(GameObject.Find("KingdomCanvas"), Is.Not.Null);

            Camera[] kingdomCameras = Camera.allCameras
                .Where(camera =>
                    camera != null &&
                    camera.isActiveAndEnabled &&
                    camera.gameObject.scene == SceneManager.GetActiveScene() &&
                    camera.targetTexture == null &&
                    camera.targetDisplay == 0)
                .ToArray();
            Assert.That(kingdomCameras, Has.Length.EqualTo(1));
            Assert.That(kingdomCameras[0].orthographic, Is.True);

            var unexpected = _logs.Errors
                .Where(message => !message.Contains("BOOT_STACK_RUNTIME_OWNER_REJECTED"))
                .ToList();
            Assert.IsEmpty(
                unexpected,
                "Realm Selection to Kingdom emitted unexpected severe logs:\n" +
                string.Join("\n", unexpected));
#endif
        }

#if UNITY_EDITOR
        private IEnumerator LoadAndSettle(string path)
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
            Assert.NotNull(load, "Expected a scene load operation for " + path);

            float started = Time.realtimeSinceStartup;
            while (!load.isDone)
            {
                if (Time.realtimeSinceStartup - started > LoadTimeoutSeconds)
                {
                    Assert.Fail($"Timed out loading {path}.");
                }

                yield return null;
            }

            // Let the marker emit and the Bootloader reclaim ownership from standby.
            yield return null;
            yield return null;
            yield return null;
        }

        private IEnumerator WaitForActiveScene(string path)
        {
            float started = Time.realtimeSinceStartup;
            while (!string.Equals(SceneManager.GetActiveScene().path, path, StringComparison.Ordinal))
            {
                if (Time.realtimeSinceStartup - started > LoadTimeoutSeconds)
                {
                    Assert.Fail($"Timed out waiting for active scene {path}.");
                }

                yield return null;
            }

            yield return null;
            yield return null;
            yield return null;
        }
#endif

        private static void AssertExclusiveScene(string expectedPath, string excludedPath)
        {
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(expectedPath));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "LoadSceneMode.Single must leave one world loaded.");
            Assert.That(
                SceneManager.GetSceneByPath(excludedPath).isLoaded,
                Is.False,
                excludedPath + " must be mutually exclusive with " + expectedPath);
            Assert.That(SceneManager.GetSceneByName(SharedMenuIds.BootScene).isLoaded, Is.False);
            Assert.That(SceneManager.GetSceneByName(SharedMenuIds.WarzoneScene).isLoaded, Is.False);
        }

        private static void AssertInnerRealmControlIsLive()
        {
            ChampionController controller = Object.FindObjectOfType<ChampionController>();
            Assert.That(controller, Is.Not.Null, "ChampionArena must restore direct 3D champion control.");
            Assert.That(controller.isActiveAndEnabled, Is.True);
            Assert.That(controller.gameObject.name, Is.EqualTo(FirstSessionChampionStart.PlayerObjectName));
            Assert.That(Object.FindObjectOfType<KingdomSceneController>(), Is.Null);
        }

        private void AssertSingleOwner(string sceneId, string sceneName, string path, List<string> ownerIds)
        {
            List<Behaviour> bootloaders = FindBootloaders();
            Assert.AreEqual(1, bootloaders.Count, $"{sceneId}: expected exactly one Bootloader owner.");

            Behaviour owner = bootloaders[0];
            string ownerId = (string)InstanceField(owner, "_runtimeOwnerId");
            Assert.IsTrue((bool)InstanceField(owner, "_runtimeActive"), $"{sceneId}: Bootloader must be the active runtime owner.");

            object marker = GetMarker();
            Assert.NotNull(marker, $"{sceneId}: offline stack marker must be registered.");
            Assert.AreEqual(ownerId, (string)InstanceProp(marker, "RuntimeOwnerId"), $"{sceneId}: single owner must hold the marker claim.");

            // Exactly one [AL-SCENE-ACTIVE] line per activation: the cumulative count for this exact
            // scene must equal the number of times we have loaded it (a double-emit regression goes red).
            _expectedActivations.TryGetValue(sceneId, out int previous);
            _expectedActivations[sceneId] = previous + 1;
            string prefix = $"[AL-SCENE-ACTIVE] id={sceneId} name={sceneName} path={path}";
            int matching = _logs.Logs.Count(line => line.StartsWith(prefix, StringComparison.Ordinal));
            Assert.AreEqual(_expectedActivations[sceneId], matching,
                $"{sceneId}: expected exactly one stable [AL-SCENE-ACTIVE] marker log per activation of {path}.");

            ownerIds.Add(ownerId);
        }

        // ---- reflection + scene helpers -------------------------------------

        private static List<Behaviour> FindBootloaders()
        {
            Type bootloaderType = RuntimeType("AL.Core.Bootloader");
            var found = new List<Behaviour>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren(bootloaderType, true))
                    {
                        if (component is Behaviour behaviour && behaviour != null)
                        {
                            found.Add(behaviour);
                        }
                    }
                }
            }

            return found;
        }

        private void InvokeOnCurrentOwner(string method, params object[] args)
        {
            Behaviour owner = FindBootloaders().Single();
            owner.GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(owner, args);
        }

        private int LoadCount() => (int)InstanceField(_controllableSave, "LoadCount");

        private int SaveCount() => (int)InstanceField(_controllableSave, "SaveCount");

        private int TickCount() => (int)InstanceField(_countingResource, "TickCount");

        private void SeedCurrentSave()
        {
            _controllableSave.GetType()
                .GetMethod("SeedCurrentSave", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(_controllableSave, null);

            object save = InstanceField(_controllableSave, "_currentSave");
            Type saveType = save.GetType();
            saveType.GetField("SaveFormatId", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(save, "anotherlife.local-save");
            saveType.GetField("SaveSchemaVersion", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(save, 1);
            saveType.GetField("ProfileInitializationVersion", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(save, 1);
        }

        private SaveGameData SeedLordshipSave(RealmId realm)
        {
            SeedCurrentSave();
            SaveGameData save = (SaveGameData)InstanceField(_controllableSave, "_currentSave");
            save.SelectedRealm = realm;
            save.ChampionCustomization = new ChampionCustomizationState
            {
                ClassFamilyId = "warrior",
                IdentityConfirmed = true,
                Username = "RoundTripTester"
            };
            Assert.That(
                ProofOfWorthLordship.TryWriteMark(save, ProofOfWorthLordship.ResolveMarkId(realm)),
                Is.True);
            return save;
        }

        private static object GetMarker()
        {
            IDictionary services = GetServiceLocatorDictionary();
            if (services == null)
            {
                return null;
            }

            foreach (object key in services.Keys)
            {
                if (key is Type type && type.FullName == "AL.Core.IOfflineServiceStackMarker")
                {
                    return services[key];
                }
            }

            return null;
        }

        private static IEnumerator UnloadIntoEmptyScene()
        {
            Scene empty = SceneManager.CreateScene("ProductionSceneLifecycleCleanup_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene == empty || !scene.isLoaded)
                {
                    continue;
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }
        }

        private static Type RuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Expected loaded runtime type {typeName}.");
            return type;
        }

        private static object NewInternal(string typeName)
        {
            return Activator.CreateInstance(RuntimeType(typeName), true);
        }

        private static void SetStackOverride(string fieldName, object value)
        {
            RuntimeType("AL.Core.OfflineServiceStack")
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, value);
        }

        private static void ResetStackOverrides()
        {
            foreach (string field in new[]
            {
                "GameDataFactoryOverride", "SaveGameFactoryOverride", "ResourceFactoryOverride",
                "NotificationFactoryOverride", "BossLootFactoryOverride"
            })
            {
                SetStackOverride(field, null);
            }
        }

        private static object InstanceField(object target, string name)
        {
            return target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .GetValue(target);
        }

        private static object InstanceProp(object target, string name)
        {
            return target.GetType()
                .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void ClearServiceLocator()
        {
            GetServiceLocatorDictionary()?.Clear();
        }

        private static IDictionary GetServiceLocatorDictionary()
        {
            Type serviceLocator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("AL.Core.ServiceLocator"))
                .FirstOrDefault(type => type != null);
            FieldInfo servicesField = serviceLocator?.GetField("Services", BindingFlags.Static | BindingFlags.NonPublic);
            return servicesField?.GetValue(null) as IDictionary;
        }

        private sealed class LogTap
        {
            private readonly List<string> _logs = new List<string>();
            private readonly List<string> _errors = new List<string>();
            private bool _started;

            public IReadOnlyList<string> Logs => _logs;
            public IReadOnlyList<string> Errors => _errors;

            public void Start()
            {
                if (_started)
                {
                    return;
                }

                Application.logMessageReceived += Handle;
                _started = true;
            }

            public void Stop()
            {
                if (!_started)
                {
                    return;
                }

                Application.logMessageReceived -= Handle;
                _started = false;
            }

            private void Handle(string condition, string stackTrace, LogType type)
            {
                _logs.Add(condition);
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                {
                    _errors.Add(condition);
                }
            }
        }
    }
}
