#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Development;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private FieldInfo _servicesField;
        private Type _stackType;
        private string _unrelatedTemporarySiblingRoot;
        private string _unrelatedTemporarySiblingPath;

        [SetUp]
        public void SetUp()
        {
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

        [TearDown]
        public void TearDown()
        {
            string cleanupFailure = string.Empty;
            try
            {
                foreach (Bootloader bootloader in UnityEngine.Object.FindObjectsOfType<Bootloader>(
                             includeInactive: true))
                {
                    if (bootloader != null)
                    {
                        UnityEngine.Object.DestroyImmediate(bootloader.gameObject);
                    }
                }

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
            void RecordScene(Scene scene, LoadSceneMode _) => visitedScenes.Add(scene.path);
            SceneManager.sceneLoaded += RecordScene;
            try
            {
                AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    EditorGameTestModeBootstrap.ExpectedBootScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                Assert.IsNotNull(load);

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
            return owner.AddComponent<Bootloader>();
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
    }
}
#endif
