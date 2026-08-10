using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.RealmSelection;
using AL.UI.RealmSelection;
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
    /// before Start): disabling the controller prevents BootController's name-based auto-transition and the
    /// heavy Kingdom/ChampionArena world build, leaving the Bootloader owner and startup marker intact.
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
        public IEnumerator BootAutomaticallyReachesRealmSelectionWithReadyCatalogAndFourControls()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Production scene transition test drives editor path-based PlayMode loads.");
            yield break;
#else
            _quiesceSceneControllers = false;
            yield return LoadAndSettle(BootPath);

            float transitionStarted = Time.realtimeSinceStartup;
            while (!string.Equals(
                       SceneManager.GetActiveScene().path,
                       RealmSelectionPath,
                       StringComparison.Ordinal))
            {
                if (Time.realtimeSinceStartup - transitionStarted > LoadTimeoutSeconds)
                {
                    Assert.Fail("Boot did not transition to the canonical RealmSelection scene.");
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

            // Let RealmSelectionController.Start build the fallback UI and presentation camera.
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

            GameObject fallbackCanvas = GameObject.Find("RealmSelectionCanvas");
            Assert.That(fallbackCanvas, Is.Not.Null, "The production fallback realm UI must be active.");
            Button[] realmButtons = fallbackCanvas.GetComponentsInChildren<Button>(true);
            Assert.That(realmButtons, Has.Length.EqualTo(4));
            Assert.That(
                realmButtons.Select(button => button.name).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(4),
                "Each catalog realm must produce one distinct fallback control.");
            Assert.That(
                realmButtons.All(button => button.interactable),
                Is.True,
                "All four realm choices must be interactable before a selection is committed.");

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
#endif

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
