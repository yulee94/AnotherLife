#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Development;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AL.Tests.PlayMode
{
    [TestFixture]
    public sealed class FirstUserGameTestModeTests
    {
        private const string RealmSelectionScenePath = "Assets/AL/Scenes/RealmSelection.unity";
        private const string KingdomScenePath = "Assets/AL/Scenes/Kingdom.unity";
        private const float SceneTransitionTimeoutSeconds = 30f;

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
        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private FieldInfo _servicesField;
        private Type _stackType;
        private string _ownershipFailure;
        private string _unrelatedTemporarySiblingRoot;
        private string _unrelatedTemporarySiblingPath;

        [SetUp]
        public void SetUp()
        {
            _ownedBootloaderScenePaths.Clear();
            _ownedRootInstanceIds.Clear();
            _ownedSceneHandles.Clear();
            _preexistingSceneHandles.Clear();
            _ownershipFailure = string.Empty;
            Bootloader[] preexistingBootloaders =
                UnityEngine.Object.FindObjectsOfType<Bootloader>(includeInactive: true);
            Assert.That(
                preexistingBootloaders,
                Is.Empty,
                "The fixture refuses to take ownership while a pre-existing Bootloader exists.");
            RecordPreexistingRunnerScenes();

            _stackType = typeof(Bootloader).Assembly.GetType(
                "AL.Core.OfflineServiceStack",
                throwOnError: true);
            _servicesField = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(_servicesField);
            SnapshotAndClearRuntimeState();
            _generatedRoots.Clear();

            _createdObjects.Clear();
            _unrelatedTemporarySiblingRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-TestBoundary-Sentinel-" + Guid.NewGuid().ToString("N"));
            _unrelatedTemporarySiblingPath = Path.Combine(
                _unrelatedTemporarySiblingRoot,
                "sentinel.bin");
            Directory.CreateDirectory(_unrelatedTemporarySiblingRoot);
            File.WriteAllBytes(
                _unrelatedTemporarySiblingPath,
                new byte[] { 9, 4, 2, 7, 1 });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            string cleanupFailure = string.Empty;

            if (!string.IsNullOrEmpty(_ownershipFailure))
            {
                cleanupFailure += _ownershipFailure + "\n";
            }

            if (!TryVerifyOnlyFixtureOwnedBootloaders(out string ownershipMessage))
            {
                cleanupFailure += ownershipMessage + "\n";
            }

            DestroyFixtureOwnedSceneRoots(
                message => cleanupFailure += message + "\n");

            try
            {
                EditorGameTestModeBootstrap.Disarm();

                foreach (GameObject createdObject in _createdObjects)
                {
                    if (createdObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(createdObject);
                    }
                }

                foreach (string root in _generatedRoots)
                {
                    if (Directory.Exists(root))
                    {
                        EditorGameTestModePlan plan = CreatePlanForExistingRoot(root);
                        if (!EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                                plan,
                                out _,
                                out string cleanupMessage))
                        {
                            cleanupFailure += cleanupMessage + "\n";
                        }
                    }
                }

                if (File.Exists(_unrelatedTemporarySiblingPath))
                {
                    File.Delete(_unrelatedTemporarySiblingPath);
                }

                if (Directory.Exists(_unrelatedTemporarySiblingRoot))
                {
                    Directory.Delete(_unrelatedTemporarySiblingRoot, recursive: false);
                }
            }
            catch (Exception ex)
            {
                cleanupFailure += "Test cleanup retained evidence: " + ex.Message + "\n";
            }
            finally
            {
                RestoreRuntimeState();
            }

            yield return null;

            Bootloader[] remainingBootloaders =
                UnityEngine.Object.FindObjectsOfType<Bootloader>(includeInactive: true);
            EventSystem[] remainingEventSystems =
                UnityEngine.Object.FindObjectsOfType<EventSystem>(includeInactive: true);
            Camera[] remainingCameras =
                UnityEngine.Object.FindObjectsOfType<Camera>(includeInactive: true);
            if (remainingBootloaders.Length != 0 ||
                remainingEventSystems.Length != 0 ||
                remainingCameras.Length != 0)
            {
                cleanupFailure +=
                    "PlayMode cleanup retained project scene objects. " +
                    "Bootloaders=" + remainingBootloaders.Length +
                    ", EventSystems=" + remainingEventSystems.Length +
                    ", Cameras=" + remainingCameras.Length + ".\n";
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
        public IEnumerator BootloaderPublishesAndLoadsOnlyTheIsolatedFreshProfile()
        {
            EditorGameTestModePlan plan = CreateOwnedPlan();
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(plan, out _, out string armMessage), armMessage);

            CreateBootloaderOwner("IsolatedBootOwner");
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();

            Assert.AreEqual(SaveLoadStatus.CreatedNew, save.LastLoadStatus);
            Assert.IsNotNull(save.CurrentSave);
            Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
            Assert.IsTrue(File.Exists(Path.Combine(plan.IsolatedSaveRoot, "save.json")));
            Assert.IsTrue(EditorGameTestModeBootstrap.TryVerifyActiveRuntime(
                out _,
                out string verifyMessage), verifyMessage);
            CollectionAssert.AreEqual(
                new byte[] { 9, 4, 2, 7, 1 },
                File.ReadAllBytes(_unrelatedTemporarySiblingPath));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ConsecutiveFreshSessionsUseDifferentRootsAndProfiles()
        {
            EditorGameTestModePlan first = CreateOwnedPlan();
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(first, out _, out string firstMessage), firstMessage);
            CreateBootloaderOwner("FirstIsolatedBootOwner");
            ISaveGameService firstSave = ServiceLocator.Get<ISaveGameService>();
            Assert.IsTrue(File.Exists(Path.Combine(first.IsolatedSaveRoot, "save.json")));

            ClearSubjectRuntimeState();

            EditorGameTestModePlan second = CreateOwnedPlan();
            Assert.AreNotEqual(first.SessionId, second.SessionId);
            Assert.AreNotEqual(first.IsolatedSaveRoot, second.IsolatedSaveRoot);
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(second, out _, out string secondMessage), secondMessage);
            CreateBootloaderOwner("SecondIsolatedBootOwner");
            ISaveGameService secondSave = ServiceLocator.Get<ISaveGameService>();

            Assert.AreEqual(SaveLoadStatus.CreatedNew, secondSave.LastLoadStatus);
            Assert.AreNotSame(firstSave, secondSave);
            Assert.IsTrue(File.Exists(Path.Combine(second.IsolatedSaveRoot, "save.json")));
            CollectionAssert.AreEqual(
                new byte[] { 9, 4, 2, 7, 1 },
                File.ReadAllBytes(_unrelatedTemporarySiblingPath));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionBootWithFreshIsolatedProfileStopsAtRealmSelection()
        {
            EditorGameTestModePlan plan = CreateOwnedPlan();
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(
                plan,
                out _,
                out string armMessage), armMessage);

            var visitedScenes = new List<string>();
            void RecordScene(Scene scene, LoadSceneMode _)
            {
                visitedScenes.Add(scene.path);
                RecordFixtureSceneOwnership(scene);
            }
            SceneManager.sceneLoaded += RecordScene;
            try
            {
                AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    EditorGameTestModeBootstrap.ExpectedBootScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                Assert.IsNotNull(load);

                while (!load.isDone)
                {
                    yield return null;
                }
                AssertFixtureSceneOwnershipIsClean();

                GameObject launchCanvas = null;
                Button continueButton = null;
                float readinessStarted = Time.realtimeSinceStartup;
                while (continueButton == null ||
                       !continueButton.gameObject.activeInHierarchy ||
                       !continueButton.interactable)
                {
                    if (Time.realtimeSinceStartup - readinessStarted >
                        SceneTransitionTimeoutSeconds)
                    {
                        Assert.Fail("Production Boot did not expose the truthful Finished Loading action.");
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

                Assert.AreEqual(
                    EditorGameTestModeBootstrap.ExpectedBootScenePath,
                    SceneManager.GetActiveScene().path,
                    "Boot must not auto-route before a fresh explicit action.");
                continueButton.onClick.Invoke();
                continueButton.onClick.Invoke();

                float started = Time.realtimeSinceStartup;
                while (!string.Equals(
                           SceneManager.GetActiveScene().path,
                           RealmSelectionScenePath,
                           StringComparison.Ordinal))
                {
                    if (Time.realtimeSinceStartup - started > SceneTransitionTimeoutSeconds)
                    {
                        Assert.Fail(
                            "Production Boot did not reach the currently implemented RealmSelection stop. Visited: " +
                            string.Join(", ", visitedScenes));
                    }

                    yield return null;
                }

                yield return null;
                yield return null;
                RecordFixtureSceneRoots(SceneManager.GetActiveScene());
                AssertFixtureSceneOwnershipIsClean();

                Assert.IsFalse(
                    visitedScenes.Contains(KingdomScenePath),
                    "A fresh isolated profile must not fabricate realm authority or enter Kingdom.");
                Assert.IsTrue(visitedScenes.Contains(
                    EditorGameTestModeBootstrap.ExpectedBootScenePath));
                Assert.IsTrue(visitedScenes.Contains(RealmSelectionScenePath));

                ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
                Assert.AreEqual(SaveLoadStatus.CreatedNew, save.LastLoadStatus);
                Assert.IsNotNull(save.CurrentSave);
                Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
                Assert.IsTrue(File.Exists(Path.Combine(plan.IsolatedSaveRoot, "save.json")));
                Assert.IsTrue(EditorGameTestModeBootstrap.TryVerifyActiveRuntime(
                    out _,
                    out string verifyMessage), verifyMessage);
                CollectionAssert.AreEqual(
                    new byte[] { 9, 4, 2, 7, 1 },
                    File.ReadAllBytes(_unrelatedTemporarySiblingPath));
            }
            finally
            {
                SceneManager.sceneLoaded -= RecordScene;
            }
        }

        [Test]
        public void RuntimeVerificationRejectsBeforeProductionBootOwnsTheLoad()
        {
            EditorGameTestModePlan plan = CreateOwnedPlan();
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(
                plan,
                out _,
                out string armMessage), armMessage);
            Assert.IsTrue(Bootloader.InitializeIfMissing().Succeeded);

            Assert.IsFalse(EditorGameTestModeBootstrap.TryVerifyActiveRuntime(
                out EditorGameTestModeFailure failure,
                out _));
            Assert.AreEqual(EditorGameTestModeFailure.OfflineStackLoadIncomplete, failure);
            Assert.IsFalse(File.Exists(Path.Combine(plan.IsolatedSaveRoot, "save.json")));
        }

        private EditorGameTestModePlan CreateOwnedPlan()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            string temporaryRoot = Path.GetTempPath();
            string isolatedRoot = EditorGameTestModeBootstrap.BuildExpectedIsolatedRoot(
                temporaryRoot,
                sessionId);
            Assert.IsTrue(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                temporaryRoot,
                UnityEngine.Application.persistentDataPath,
                isolatedRoot,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out EditorGameTestModePlan plan,
                out _,
                out string message), message);

            Assert.IsTrue(EditorGameTestModeBootstrap.TryCreateOwnedRoot(
                plan,
                out _,
                out string creationMessage), creationMessage);
            _generatedRoots.Add(plan.IsolatedSaveRoot);
            return plan;
        }

        [Test]
        public void FailClosedRecoveryPreventsBootloaderFromPublishingAnySaveService()
        {
            EditorGameTestModeBootstrap.EnterFailClosedState(
                Guid.NewGuid().ToString("N"),
                "tampered recovery metadata");
            Assert.IsNotNull(UnityEngine.GameObject.Find("[AL] Isolated Game Test Mode"));

            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "[BOOT_STACK_CONSTRUCTION_FAILED] Could not construct offline service stack: AL-ISOLATED-TEST-FAIL-CLOSED: tampered recovery metadata");
            BootloaderInitializationResult initialization = Bootloader.InitializeIfMissing();

            Assert.IsFalse(initialization.Succeeded);
            Assert.IsFalse(ServiceLocator.TryGet<ISaveGameService>(out _));
            CollectionAssert.AreEqual(
                new byte[] { 9, 4, 2, 7, 1 },
                File.ReadAllBytes(_unrelatedTemporarySiblingPath));
        }

        private EditorGameTestModePlan CreatePlanForExistingRoot(string root)
        {
            string sessionId = Path.GetFileName(root);
            Assert.IsTrue(EditorGameTestModeBootstrap.TryCreatePlan(
                sessionId,
                Path.GetTempPath(),
                UnityEngine.Application.persistentDataPath,
                root,
                EditorGameTestModeBootstrap.ExpectedBootScenePath,
                EditorGameTestModeBootstrap.ExpectedBootSceneGuid,
                fullDomainReload: true,
                fullSceneReload: true,
                out EditorGameTestModePlan plan,
                out _,
                out string message), message);
            return plan;
        }

        private Bootloader CreateBootloaderOwner(string objectName)
        {
            var owner = new GameObject(objectName);
            _createdObjects.Add(owner);
            Bootloader bootloader = owner.AddComponent<Bootloader>();
            _ownedBootloaderScenePaths[bootloader.GetInstanceID()] = owner.scene.path;
            return bootloader;
        }

        private void SnapshotAndClearRuntimeState()
        {
            EditorGameTestModeBootstrap.Disarm();
            _originalFactoryValues.Clear();
            foreach (string fieldName in new[]
                     {
                         "GameDataFactoryOverride",
                         "SaveGameFactoryOverride",
                         "ResourceFactoryOverride",
                         "NotificationFactoryOverride",
                         "BossLootFactoryOverride"
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

        private void ClearSubjectRuntimeState()
        {
            foreach (GameObject createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
            EditorGameTestModeBootstrap.Disarm();
            foreach (FieldInfo field in _originalFactoryValues.Keys)
            {
                field.SetValue(null, null);
            }

            ((IDictionary)_servicesField.GetValue(null)).Clear();
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
            Assert.That(_ownedRootInstanceIds.Contains(candidate.GetInstanceID()), Is.False);
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
            Assert.That(_ownedRootInstanceIds.Contains(root.GetInstanceID()), Is.True,
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
                bool isCreatedObject = _createdObjects.Any(
                    created => created != null && created == bootloader.gameObject);
                bool isOwnedSceneBootloader =
                    _ownedBootloaderScenePaths.TryGetValue(
                        bootloader.GetInstanceID(),
                        out string ownedPath) &&
                    _ownedSceneHandles.Contains(scene.handle) &&
                    string.Equals(ownedPath, scene.path, StringComparison.Ordinal) &&
                    IsFixtureScenePath(scene.path);
                if (!isCreatedObject && !isOwnedSceneBootloader)
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

        private static bool IsFixtureScenePath(string path)
        {
            return string.Equals(
                       path,
                       EditorGameTestModeBootstrap.ExpectedBootScenePath,
                       StringComparison.Ordinal) ||
                   string.Equals(path, RealmSelectionScenePath, StringComparison.Ordinal);
        }
    }
}
#endif
