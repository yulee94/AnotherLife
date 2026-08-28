using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Services.Local;
using AL.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode
{
    public sealed class MvpApprovalBootControllerTests
    {

        private string _root;
        private string _testRegistrySubKey;
        private GameObject _controllerObject;

        [SetUp]
        public void SetUp()
        {
            _testRegistrySubKey =
                @"Software\AnotherLife\Tests\MvpApprovalVfsV1\" +
                Guid.NewGuid().ToString("N");
            MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests =
                _testRegistrySubKey;
            ResetRuntime();
            SetSaveFactory(null);
            _root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-MvpApprovalBootTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_controllerObject);
            }
            ResetRuntime();
            SetSaveFactory(null);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            WindowsRegistryValueStore.DeleteTestSubKeyAndFlush(
                _testRegistrySubKey);
#endif
            MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests = null;
            _testRegistrySubKey = null;
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void NormalBootExposesNoApprovalActionAndKeepsContinueCopy()
        {
            SetApprovalFlavor(false);
            BootController controller = CreateControllerWithReadyState();

            Invoke(controller, "BuildRuntimeSplash");
            Invoke(controller, "RefreshPresentation", true);

            Assert.That(FindDescendant("StartNewMvpJourneyAction"), Is.Null);
            Assert.That(ButtonLabel(FindDescendant("FinishedLoadingAction")), Is.EqualTo("Continue"));
        }

        [Test]
        public void FirstStartNewActivationOnlyRequestsConfirmationAndKeepCurrentCancels()
        {
            string normalRoot = Path.Combine(_root, "normal");
            Directory.CreateDirectory(normalRoot);
            File.WriteAllBytes(Path.Combine(normalRoot, "save.json"), new byte[] { 3, 1, 4, 1, 5, 9 });
            Assert.That(MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure), Is.True, failure);
            var save = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            save.Load();
            save.CreateNewSave(RealmId.Crownlands);
            object currentBefore = save.CurrentSave;
            bool approvalSaveBefore = save.HasSave();
            IReadOnlyDictionary<string, byte[]> normalBefore = Snapshot(normalRoot);

            SetApprovalFlavor(true);
            BootController controller = CreateControllerWithReadyState();
            Invoke(controller, "BuildRuntimeSplash");
            Invoke(controller, "RefreshPresentation", true);
            SetField(controller, "_readyFrame", -1);
            GameObject primary = FindDescendant("FinishedLoadingAction");
            GameObject secondary = FindDescendant("StartNewMvpJourneyAction");
            Assert.That(primary, Is.Not.Null);
            Assert.That(secondary, Is.Not.Null);
            Assert.That(ButtonLabel(primary), Is.EqualTo("Continue MVP Journey"));
            Assert.That(ButtonLabel(secondary), Is.EqualTo("Start New MVP Journey"));

            Invoke(controller, "OnStartNewMvpRequested");

            Assert.That(ButtonLabel(primary), Is.EqualTo("Keep Current Journey"));
            Assert.That(ButtonLabel(secondary), Is.EqualTo("Confirm Start New"));
            Assert.That(save.CurrentSave, Is.SameAs(currentBefore));
            Assert.That(save.HasSave(), Is.EqualTo(approvalSaveBefore));
            Assert.That(Directory.Exists(MvpApprovalSlotRuntime.ActivePlan.ApprovalRoot), Is.False);
            AssertSnapshotsEqual(normalBefore, Snapshot(normalRoot));

            Invoke(controller, "OnContinueRequested");

            Assert.That(ButtonLabel(primary), Is.EqualTo("Continue MVP Journey"));
            Assert.That(ButtonLabel(secondary), Is.EqualTo("Start New MVP Journey"));
            Assert.That(save.CurrentSave, Is.SameAs(currentBefore));
            Assert.That(save.HasSave(), Is.EqualTo(approvalSaveBefore));
            Assert.That(Directory.Exists(MvpApprovalSlotRuntime.ActivePlan.ApprovalRoot), Is.False);
            AssertSnapshotsEqual(normalBefore, Snapshot(normalRoot));
        }

        [Test]
        public void ConfirmedStartNewResetsBeforeBeginningExactlyOneFreshRoute()
        {
            string normalRoot = Path.Combine(_root, "normal-confirmed");
            Directory.CreateDirectory(normalRoot);
            File.WriteAllBytes(Path.Combine(normalRoot, "sentinel.bin"), new byte[] { 2, 7, 1, 8 });
            IReadOnlyDictionary<string, byte[]> normalBefore = Snapshot(normalRoot);
            Assert.That(MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure), Is.True, failure);
            var save = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            save.Load();
            save.CreateNewSave(RealmId.Crownlands);

            SetApprovalFlavor(true);
            BootController controller = CreateControllerWithReadyState();
            SetField(controller, "_suppressDestinationLoadForTests", true);
            Invoke(controller, "BuildRuntimeSplash");
            Invoke(controller, "RefreshPresentation", true);
            SetField(controller, "_readyFrame", -1);
            Invoke(controller, "OnStartNewMvpRequested");
            SetField(controller, "_readyFrame", -1);

            Invoke(controller, "OnStartNewMvpRequested");

            Assert.That(save.LastLoadStatus, Is.EqualTo(SaveLoadStatus.CreatedNew), save.LastLoadMessage);
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
            var readiness = (LaunchReadinessCoordinator)GetField(controller, "_readiness");
            Assert.That(readiness.Snapshot.State, Is.EqualTo(LaunchReadinessState.Transitioning));
            AssertSnapshotsEqual(normalBefore, Snapshot(normalRoot));
        }

        [Test]
        [Platform("Win")]
        public void BootApprovalInstallKeepsRegistryAuthorityOnExactTestLeaf()
        {
            string normalRoot = Path.Combine(_root, "normal-test-leaf-guard");
            Directory.CreateDirectory(normalRoot);
            string fingerprint = ComputeFingerprint(normalRoot);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);

            Assert.That(
                MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests,
                Is.EqualTo(_testRegistrySubKey));
            Assert.That(
                WindowsRegistryValueStore.TryRead(
                    _testRegistrySubKey,
                    fingerprint,
                    out string valueDuring),
                Is.True);
            Assert.That(valueDuring, Is.Not.Empty);
        }

        private BootController CreateControllerWithReadyState()
        {
            _controllerObject = new GameObject("MvpApprovalBootControllerTest");
            var controller = _controllerObject.AddComponent<BootController>();
            SetField(controller, "_readiness", ReadyCoordinator());
            SetField(controller, "_launchLifecycle", new LaunchCinematicLifecycle());
            SetField(controller, "_readyFrame", -1);
            return controller;
        }

        private static LaunchReadinessCoordinator ReadyCoordinator()
        {
            var coordinator = new LaunchReadinessCoordinator();
            int generation = coordinator.AttemptGeneration;
            Assert.That(coordinator.TryPublishBootLoad(new LaunchBootLoadEvidence(
                generation, "approval-stack", 1, SaveLoadStatus.LoadedPrimary, 1, 1)), Is.True);
            Assert.That(coordinator.TryPublishCatalog(new LaunchCatalogEvidence(
                generation, 7, "0.1.0", 4)), Is.True);
            Assert.That(coordinator.TryEstablishMedia(
                generation, LaunchMediaPresentation.StaticFallbackEstablished), Is.True);
            Assert.That(coordinator.TryPublishDestination(new LaunchDestinationEvidence(
                generation, "RealmSelection")), Is.True);
            return coordinator;
        }

        private GameObject FindDescendant(string name)
        {
            return _controllerObject.GetComponentsInChildren<Transform>(true)
                .Select(item => item.gameObject)
                .FirstOrDefault(item => string.Equals(item.name, name, StringComparison.Ordinal));
        }

        private static string ButtonLabel(GameObject button)
        {
            Assert.That(button, Is.Not.Null);
            Text label = button.GetComponentInChildren<Text>(true);
            Assert.That(label, Is.Not.Null);
            return label.text;
        }

        private static void Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidate => candidate.Name == methodName &&
                                     candidate.GetParameters().Length == arguments.Length);
            method.Invoke(target, arguments);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static object GetField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(target);
        }

        private static IReadOnlyDictionary<string, byte[]> Snapshot(string root) =>
            Directory.GetFiles(root)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    MvpApprovalSlotPlan.SaveRootGuardFileName,
                    StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal);

        private static string ComputeFingerprint(string root)
        {
            MethodInfo method = typeof(MvpApprovalVirtualStore).GetMethod(
                "Fingerprint",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { root });
        }

        private static void AssertSnapshotsEqual(
            IReadOnlyDictionary<string, byte[]> expected,
            IReadOnlyDictionary<string, byte[]> actual)
        {
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys);
            foreach (string key in expected.Keys)
            {
                CollectionAssert.AreEqual(expected[key], actual[key], key);
            }
        }

        private static Type OfflineStackType => typeof(Bootloader).Assembly.GetType(
            "AL.Core.OfflineServiceStack",
            throwOnError: true);

        private static FieldInfo SaveFactoryField => OfflineStackType.GetField(
            "SaveGameFactoryOverride",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static object GetSaveFactory() => SaveFactoryField.GetValue(null);
        private static void SetSaveFactory(object value) => SaveFactoryField.SetValue(null, value);

        private static void SetApprovalFlavor(bool value)
        {
            MethodInfo method = typeof(MvpApprovalSlotRuntime).GetMethod(
                "SetApprovalFlavorForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { (bool?)value });
        }

        private static void ResetRuntime()
        {
            MethodInfo method = typeof(MvpApprovalSlotRuntime).GetMethod(
                "ResetForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            method?.Invoke(null, null);
        }
    }
}
