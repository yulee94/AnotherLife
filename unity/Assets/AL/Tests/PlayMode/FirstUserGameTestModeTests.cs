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
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    [TestFixture]
    public sealed class FirstUserGameTestModeTests
    {
        private readonly List<string> _generatedRoots = new List<string>();
        private readonly Dictionary<FieldInfo, object> _originalFactoryValues =
            new Dictionary<FieldInfo, object>();
        private readonly Dictionary<object, object> _originalServices =
            new Dictionary<object, object>();
        private FieldInfo _servicesField;
        private Type _stackType;
        private string _boundarySentinelRoot;
        private string _boundarySentinelPath;

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

            _boundarySentinelRoot = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-TestBoundary-Sentinel-" + Guid.NewGuid().ToString("N"));
            _boundarySentinelPath = Path.Combine(_boundarySentinelRoot, "sentinel.bin");
            Directory.CreateDirectory(_boundarySentinelRoot);
            File.WriteAllBytes(_boundarySentinelPath, new byte[] { 9, 4, 2, 7, 1 });
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                EditorGameTestModeBootstrap.Disarm();

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
                            TestContext.Out.WriteLine(cleanupMessage);
                        }
                    }
                }

                if (File.Exists(_boundarySentinelPath))
                {
                    File.Delete(_boundarySentinelPath);
                }

                if (Directory.Exists(_boundarySentinelRoot))
                {
                    Directory.Delete(_boundarySentinelRoot, recursive: false);
                }
            }
            catch (Exception ex)
            {
                TestContext.Out.WriteLine("Test cleanup retained evidence: " + ex.Message);
            }
            finally
            {
                RestoreRuntimeState();
            }
        }

        [UnityTest]
        public IEnumerator BootloaderPublishesAndLoadsOnlyTheIsolatedFreshProfile()
        {
            EditorGameTestModePlan plan = CreateOwnedPlan();
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(plan, out _, out string armMessage), armMessage);

            BootloaderInitializationResult initialization = Bootloader.InitializeIfMissing();
            Assert.IsTrue(initialization.Succeeded, initialization.Message);
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            save.Load();

            Assert.AreEqual(SaveLoadStatus.CreatedNew, save.LastLoadStatus);
            Assert.IsNotNull(save.CurrentSave);
            Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
            Assert.IsTrue(File.Exists(Path.Combine(plan.IsolatedSaveRoot, "save.json")));
            Assert.IsTrue(EditorGameTestModeBootstrap.TryVerifyActiveRuntime(
                out _,
                out string verifyMessage), verifyMessage);
            CollectionAssert.AreEqual(
                new byte[] { 9, 4, 2, 7, 1 },
                File.ReadAllBytes(_boundarySentinelPath));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ConsecutiveFreshSessionsUseDifferentRootsAndProfiles()
        {
            EditorGameTestModePlan first = CreateOwnedPlan();
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(first, out _, out string firstMessage), firstMessage);
            Assert.IsTrue(Bootloader.InitializeIfMissing().Succeeded);
            ISaveGameService firstSave = ServiceLocator.Get<ISaveGameService>();
            firstSave.Load();
            Assert.IsTrue(File.Exists(Path.Combine(first.IsolatedSaveRoot, "save.json")));

            ClearSubjectRuntimeState();

            EditorGameTestModePlan second = CreateOwnedPlan();
            Assert.AreNotEqual(first.SessionId, second.SessionId);
            Assert.AreNotEqual(first.IsolatedSaveRoot, second.IsolatedSaveRoot);
            Assert.IsTrue(EditorGameTestModeBootstrap.TryArm(second, out _, out string secondMessage), secondMessage);
            Assert.IsTrue(Bootloader.InitializeIfMissing().Succeeded);
            ISaveGameService secondSave = ServiceLocator.Get<ISaveGameService>();
            secondSave.Load();

            Assert.AreEqual(SaveLoadStatus.CreatedNew, secondSave.LastLoadStatus);
            Assert.AreNotSame(firstSave, secondSave);
            Assert.IsTrue(File.Exists(Path.Combine(second.IsolatedSaveRoot, "save.json")));
            CollectionAssert.AreEqual(
                new byte[] { 9, 4, 2, 7, 1 },
                File.ReadAllBytes(_boundarySentinelPath));

            yield return null;
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
                File.ReadAllBytes(_boundarySentinelPath));
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
