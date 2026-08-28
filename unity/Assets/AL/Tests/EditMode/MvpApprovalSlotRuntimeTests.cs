using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AL.Core;
using AL.Core.Interfaces;
using AL.ChampionMode.Quests;
using AL.ChampionMode.Tutorial;
using AL.ChampionMode;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.CharacterCreation;
using AL.UI.SharedMenu;
using Microsoft.Win32.SafeHandles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    [Parallelizable(ParallelScope.None)]
    public sealed class MvpApprovalSlotRuntimeTests
    {
        private string _root;
        private static string _testRegistrySubKey;

        [SetUp]
        public void SetUp()
        {
            _testRegistrySubKey = @"Software\AnotherLife\Tests\MvpApprovalVfsV1\" +
                                  Guid.NewGuid().ToString("N");
            MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests =
                _testRegistrySubKey;
            ResetRuntime();
            SetSaveFactory(null);
            _root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-MvpApprovalSlotTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            WindowsNamedMutex.CurrentUserSidOverrideForTests = null;
            WindowsNamedMutex.WaitOverrideForTests = null;
            WindowsNamedMutex.CloseHandleObserverForTests = null;
            MvpApprovalVirtualStore.BeforePersistForTests = null;
            ResetRuntime();
            if ((Application.platform == RuntimePlatform.WindowsEditor ||
                 Application.platform == RuntimePlatform.WindowsPlayer) &&
                !string.IsNullOrEmpty(_testRegistrySubKey) &&
                !string.IsNullOrEmpty(_root))
            {
                WindowsRegistryValueStore.DeleteTestSubKeyAndFlush(
                    _testRegistrySubKey);
            }
            MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests = null;
            _testRegistrySubKey = null;
            SetSaveFactory(null);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void TestRegistryCleanupGuardAcceptsOnlyExactUniqueLeaf()
        {
            MethodInfo guard = typeof(MvpApprovalVirtualStore).GetMethod(
                "IsTestRegistryLeafPathForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(guard, Is.Not.Null,
                "Approval cleanup needs an independently testable path guard.");

            const string parent = @"Software\AnotherLife\Tests\MvpApprovalVfsV1";
            Assert.That((bool)guard.Invoke(null, new object[] { null }), Is.False);
            Assert.That((bool)guard.Invoke(null, new object[] { string.Empty }), Is.False);
            Assert.That((bool)guard.Invoke(null, new object[] { parent }), Is.False);
            Assert.That((bool)guard.Invoke(
                null,
                new object[] { @"Software\AnotherLife\MvpApprovalVfsV1" }), Is.False);
            Assert.That((bool)guard.Invoke(
                null,
                new object[] { parent + @"\run\nested" }), Is.False);
            Assert.That((bool)guard.Invoke(
                null,
                new object[] { parent + @"\not-a-guid" }), Is.False);
            Assert.That((bool)guard.Invoke(
                null,
                new object[] { parent + @"\Boot." + Guid.NewGuid().ToString("N") }), Is.False);
            Assert.That((bool)guard.Invoke(
                null,
                new object[] { parent + @"\" + Guid.NewGuid().ToString("N") }), Is.True);
        }

        [Test]
        public void InvalidTestRegistryOverrideIsRejectedBeforeNativeRegistryAccess()
        {
            MethodInfo resolver = typeof(MvpApprovalVirtualStore).GetMethod(
                "ResolveRegistrySubKeyPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolver, Is.Not.Null);

            string original = MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests;
            string parent = @"Software\AnotherLife\Tests\MvpApprovalVfsV1";
            string[] unsafeOverrides =
            {
                @"Software\AnotherLife\MvpApprovalVfsV1",
                @"Software\AnotherLife\Unrelated",
                parent,
                parent + @"\not-a-guid",
                parent + @"\" + Guid.NewGuid().ToString("N") + @"\nested"
            };

            try
            {
                foreach (string unsafeOverride in unsafeOverrides)
                {
                    MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests =
                        unsafeOverride;
                    TargetInvocationException exception =
                        Assert.Throws<TargetInvocationException>(() =>
                            resolver.Invoke(null, null));
                    Assert.That(
                        exception.InnerException,
                        Is.TypeOf<ArgumentException>(),
                        unsafeOverride);
                }
            }
            finally
            {
                MvpApprovalVirtualStore.RegistrySubKeyPathOverrideForTests = original;
            }
        }

        [Test]
        public void LegacySaveWithoutFirstWorldProgressLoadsWithoutChangingBytes()
        {
            string saveRoot = Path.Combine(_root, "legacy-save");
            Directory.CreateDirectory(saveRoot);
            LocalSaveGameService writer = CreateLocalSave(saveRoot);
            writer.Load();
            string primaryPath = Path.Combine(saveRoot, "save.json");
            string backupPath = Path.Combine(saveRoot, "save.backup.json");
            string legacyJson = RemoveFirstWorldProgress(File.ReadAllText(primaryPath));
            File.WriteAllText(primaryPath, legacyJson, new System.Text.UTF8Encoding(false));
            File.WriteAllText(backupPath, legacyJson, new System.Text.UTF8Encoding(false));
            byte[] before = File.ReadAllBytes(primaryPath);

            LocalSaveGameService reader = CreateLocalSave(saveRoot);
            reader.Load();

            Assert.That(reader.CurrentSave, Is.Not.Null, reader.LastLoadMessage);
            FirstWorldProgressSnapshot progress = ReadFirstWorld(reader.CurrentSave);
            Assert.That(
                progress.ReadDisposition,
                Is.EqualTo(FirstWorldProgressReadDisposition.LegacyDefault));
            Assert.That(progress.Tutorial, Is.Not.Null);
            Assert.That(
                progress.Tutorial.TeachingBeat,
                Is.EqualTo(FirstWorldEntryTeachingBeat.CameraLook));
            Assert.That(progress.Proof, Is.Null);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(primaryPath));
        }

        [Test]
        public void ApprovalSaveRoundTripsTypedTutorialAndProofWithoutSidecarOrNormalMutation()
        {
            string normalRoot = Path.Combine(_root, "normal");
            Directory.CreateDirectory(normalRoot);
            string sentinel = Path.Combine(normalRoot, "save.json");
            byte[] normalBytes = { 0x14, 0x73, 0xa2, 0xff };
            File.WriteAllBytes(sentinel, normalBytes);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);
            Assert.That(MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure), Is.True, failure);
            ISaveGameService service =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);
            FirstWorldProgressSnapshot committed = CompleteTutorial(service);
            Assert.That(committed.IsTutorialComplete, Is.True);
            Assert.That(committed.Proof, Is.Not.Null);

            service.Load();
            FirstWorldProgressSnapshot restored = ReadFirstWorld(service.CurrentSave);
            Assert.That(restored.Tutorial.IsComplete, Is.True);
            Assert.That(restored.Tutorial.MovementConfirmationCount, Is.EqualTo(1));
            Assert.That(restored.Tutorial.BasicAttackConfirmationCount, Is.EqualTo(1));
            Assert.That(restored.Proof.Phase, Is.EqualTo(ProofOfWorthPhase.OmenOffered));
            Assert.That(restored.Proof.Realm, Is.EqualTo(RealmId.Crownlands));
            Assert.That(Directory.Exists(MvpApprovalSlotRuntime.ActivePlan.ApprovalRoot), Is.False);
            AssertSnapshotsEqual(before, Snapshot(normalRoot));
        }

        [Test]
        public void ApprovalEnvelopeReloadsAfterRuntimeReconstructionWithoutNormalMutation()
        {
            string normalRoot = Path.Combine(_root, "normal-runtime-reconstruction");
            Directory.CreateDirectory(normalRoot);
            File.WriteAllBytes(
                Path.Combine(normalRoot, "save.json"),
                new byte[] { 0x55, 0xaa, 0x13, 0x37 });
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService writer =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            writer.Load();
            writer.CreateNewSave(RealmId.Crownlands);
            CommitTutorial(
                writer,
                FirstWorldTutorialProgressCommand.CameraLookAccepted);

            MethodInfo reconstruct = typeof(MvpApprovalSlotRuntime).GetMethod(
                "ResetRuntimePreservingStoreForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reconstruct, Is.Not.Null);
            reconstruct.Invoke(null, null);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out failure),
                Is.True,
                failure);
            ISaveGameService reader =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            reader.Load();
            FirstWorldProgressSnapshot restored = ReadFirstWorld(reader.CurrentSave);

            Assert.That(reader.LastLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(restored.Tutorial, Is.Not.Null);
            Assert.That(
                restored.Tutorial.Step,
                Is.EqualTo(FirstWorldEntryTutorialStep.Move));
            Assert.That(Directory.Exists(MvpApprovalSlotRuntime.ActivePlan.ApprovalRoot), Is.False);
            AssertSnapshotsEqual(before, Snapshot(normalRoot));
        }

        [Test]
        public void ApprovalResetDeletesSaveBackedProgressAndLeavesNormalRootByteExact()
        {
            string normalRoot = Path.Combine(_root, "normal");
            Directory.CreateDirectory(normalRoot);
            string sentinel = Path.Combine(normalRoot, "normal-save-sentinel.bin");
            File.WriteAllBytes(sentinel, new byte[] { 0xde, 0xad, 0xbe, 0xef });
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);
            Assert.That(MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure), Is.True, failure);
            ISaveGameService service =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);
            CommitTutorial(
                service,
                FirstWorldTutorialProgressCommand.CameraLookAccepted);

            MvpApprovalStartNewDisposition disposition =
                MvpApprovalSlotRuntime.TryStartNewJourney(out failure);

            Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.Succeeded), failure);
            FirstWorldProgressSnapshot restored = ReadFirstWorld(service.CurrentSave);
            Assert.That(
                restored.ReadDisposition,
                Is.EqualTo(FirstWorldProgressReadDisposition.LegacyDefault));
            Assert.That(restored.Tutorial, Is.Not.Null);
            Assert.That(restored.Proof, Is.Null);
            Assert.That(MvpApprovalSlotRuntime.IsDeleteAuthorized(service), Is.False);
            AssertSnapshotsEqual(before, Snapshot(normalRoot));
        }

        private static string RemoveFirstWorldProgress(string json)
        {
            int start = json.IndexOf(",\"FirstWorldProgress\":", StringComparison.Ordinal);
            if (start < 0)
            {
                return json;
            }

            int end = json.IndexOf(",\"WarzoneCredits\":", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            return json.Remove(start, end - start);
        }

        private static FirstWorldProgressSnapshot ReadFirstWorld(
            SaveGameData save)
        {
            Assert.That(
                FirstWorldProgressSaveCodec.TryRead(
                    save,
                    out FirstWorldProgressSnapshot snapshot,
                    out string message),
                Is.True,
                message);
            return snapshot;
        }

        private static FirstWorldProgressCommitResult CommitTutorial(
            ISaveGameService service,
            FirstWorldTutorialProgressCommand command)
        {
            Assert.That(
                FirstWorldProgressSaveAuthority.TryRead(
                    service,
                    out FirstWorldProgressSnapshot expected,
                    out string readMessage),
                Is.True,
                readMessage);
            FirstWorldProgressCommitResult result =
                FirstWorldProgressSaveAuthority.TryAdvanceTutorial(
                    service,
                    expected,
                    command);
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.Persisted, Is.True, result.Message);
            return result;
        }

        private static FirstWorldProgressSnapshot CompleteTutorial(
            ISaveGameService service)
        {
            FirstWorldTutorialProgressCommand[] commands =
            {
                FirstWorldTutorialProgressCommand.CameraLookAccepted,
                FirstWorldTutorialProgressCommand.MovementAccepted,
                FirstWorldTutorialProgressCommand.GuideInteractionAccepted,
                FirstWorldTutorialProgressCommand.BasicAttackAccepted
            };
            FirstWorldProgressSnapshot snapshot = null;
            for (int index = 0; index < commands.Length; index++)
            {
                snapshot = CommitTutorial(service, commands[index]).Snapshot;
            }

            return snapshot;
        }

        [Test]
        public void ApprovalPlanDerivesExactSiblingRootWithoutPersistentDataOverlap()
        {
            string normalRoot = Path.Combine(_root, "NormalProfile", "trailing", "..");

            Assert.That(
                MvpApprovalSlotPlan.TryCreate(normalRoot, out MvpApprovalSlotPlan plan, out string failure),
                Is.True,
                failure);

            string expectedNormal = Path.GetFullPath(Path.Combine(_root, "NormalProfile"));
            string expectedApproval = expectedNormal + ".mvp-approval-slot-v1";
            Assert.That(plan.NormalRoot, Is.EqualTo(expectedNormal).IgnoreCase);
            Assert.That(plan.ApprovalRoot, Is.EqualTo(expectedApproval).IgnoreCase);
            Assert.That(plan.SaveRoot, Is.EqualTo(Path.Combine(expectedApproval, "profile")).IgnoreCase);
            Assert.That(IsSameOrDescendant(plan.ApprovalRoot, plan.NormalRoot), Is.False);
            Assert.That(IsSameOrDescendant(plan.NormalRoot, plan.ApprovalRoot), Is.False);
            Assert.That(IsSameOrDescendant(plan.SaveRoot, plan.NormalRoot), Is.False);
        }

        [Test]
        public void ApprovalInstallAndPersistenceCreateNoFilesystemArtifacts()
        {
            string normalRoot = Path.Combine(_root, "normal-vfs-boundary");
            Directory.CreateDirectory(normalRoot);
            File.WriteAllBytes(
                Path.Combine(normalRoot, "save.json"),
                new byte[] { 0x31, 0x41, 0x59, 0x26 });
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);
            Assert.That(
                MvpApprovalSlotPlan.TryCreate(normalRoot, out MvpApprovalSlotPlan plan, out string planFailure),
                Is.True,
                planFailure);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService service =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);

            Assert.That(Directory.Exists(plan.ApprovalRoot), Is.False);
            Assert.That(Directory.Exists(plan.SaveRoot), Is.False);
            Assert.That(File.Exists(plan.MarkerPath), Is.False);
            Assert.That(File.Exists(plan.SaveRootGuardPath), Is.False);
            AssertSnapshotsEqual(before, Snapshot(normalRoot));
        }

        [Test]
        public void NormalModeInstallIsStrictNoOp()
        {
            string untouchedNormalRoot = Path.Combine(_root, "normal-must-not-be-observed");
            object sentinelFactory = new Func<object>(() => new object());
            SetSaveFactory(sentinelFactory);

            bool installed = MvpApprovalSlotRuntime.TryInstall(
                approvalFlavor: false,
                normalRoot: untouchedNormalRoot,
                out string failure);

            Assert.That(installed, Is.True, failure);
            Assert.That(Directory.Exists(untouchedNormalRoot), Is.False);
            Assert.That(Directory.Exists(untouchedNormalRoot + ".mvp-approval-slot-v1"), Is.False);
            Assert.That(GetSaveFactory(), Is.SameAs(sentinelFactory));
            Assert.That(MvpApprovalSlotRuntime.IsDeleteAuthorized(null), Is.False);
        }

        [Test]
        public void ForeignFilesystemMarkerIsIgnoredWithoutNormalMutation()
        {
            string normalRoot = Path.Combine(_root, "normal");
            Directory.CreateDirectory(normalRoot);
            string sentinel = Path.Combine(normalRoot, "normal-save-sentinel.bin");
            byte[] sentinelBytes = { 0x00, 0x11, 0x7f, 0x80, 0xff };
            File.WriteAllBytes(sentinel, sentinelBytes);
            Assert.That(MvpApprovalSlotPlan.TryCreate(normalRoot, out MvpApprovalSlotPlan plan, out _), Is.True);
            Directory.CreateDirectory(plan.ApprovalRoot);
            Directory.CreateDirectory(plan.SaveRoot);
            File.WriteAllText(plan.MarkerPath, "foreign-owner-v9");

            bool installed = MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure);

            Assert.That(installed, Is.True, failure);
            Delegate approvalFactory = (Delegate)GetSaveFactory();
            Assert.That(approvalFactory, Is.Not.Null);
            var service = (ISaveGameService)approvalFactory.DynamicInvoke();
            service.Load();
            Assert.That(service.LastLoadStatus, Is.EqualTo(SaveLoadStatus.CreatedNew));
            CollectionAssert.AreEqual(sentinelBytes, File.ReadAllBytes(sentinel));
            Assert.That(File.ReadAllText(plan.MarkerPath), Is.EqualTo("foreign-owner-v9"));
        }

        [Test]
        public void GuardedFactoryReturnsOneExactLocalSaveBoundToProfileChild()
        {
            string normalRoot = Path.Combine(_root, "normal");
            Directory.CreateDirectory(normalRoot);
            string sentinel = Path.Combine(normalRoot, "save.json");
            byte[] normalBytes = { 9, 8, 7, 6, 5, 4 };
            File.WriteAllBytes(sentinel, normalBytes);

            Assert.That(MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure), Is.True, failure);
            Delegate factory = (Delegate)GetSaveFactory();
            var first = (ISaveGameService)factory.DynamicInvoke();
            var second = (ISaveGameService)factory.DynamicInvoke();

            Assert.That(second, Is.SameAs(first));
            first.Load();
            Assert.That(first.LastLoadStatus, Is.EqualTo(SaveLoadStatus.CreatedNew), first.LastLoadMessage);
            Assert.That(MvpApprovalSlotRuntime.ActiveService, Is.SameAs(first));
            Assert.That(first.HasSave(), Is.True);
            Assert.That(Directory.Exists(MvpApprovalSlotRuntime.ActivePlan.SaveRoot), Is.False);
            CollectionAssert.AreEqual(normalBytes, File.ReadAllBytes(sentinel));
        }

        [Test]
        [Platform("Win")]
        public void NonThrowingFailedResetRollsBackDirtyApprovalEnvelope()
        {
            string normalRoot = Path.Combine(_root, "normal-failed-reset-rollback");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotPlan.TryCreate(normalRoot, out MvpApprovalSlotPlan plan, out string planFailure),
                Is.True,
                planFailure);
            Assert.That(
                MvpApprovalVirtualStore.TryPrepare(plan, out MvpApprovalVirtualStore store, out string storeFailure),
                Is.True,
                storeFailure);

            var fileOperations = new MvpApprovalSaveFileOperations(plan.SaveRoot, store);
            var inner = new LocalSaveGameService(plan.SaveRoot, fileOperations);
            var service = new MvpApprovalTransactionalSaveGameService(store, inner);
            string primaryArtifact = Path.Combine(plan.SaveRoot, "save.json");
            string rejectedArtifact = Path.Combine(plan.SaveRoot, "save.temp.json");
            try
            {
                service.Load();
                int commitsBefore = store.CommitCountForTests;
                SaveOperationDisposition loadSave = service.LastSaveDisposition;
                Assert.That(
                    fileOperations.FileExists(primaryArtifact),
                    Is.True,
                    $"Load save status={service.LastSaveStatus}; disposition={loadSave?.Status}; " +
                    $"primary={loadSave?.CandidatePrimaryVerified}; backup={loadSave?.RequiredBackupVerified}; " +
                    $"cleanup={loadSave?.CleanupVerified}.");

                MvpApprovalStartNewDisposition result =
                    service.ExecuteReset(candidate =>
                    {
                        fileOperations.Move(primaryArtifact, rejectedArtifact);
                        return MvpApprovalStartNewDisposition.Failed;
                    });

                Assert.That(result, Is.EqualTo(MvpApprovalStartNewDisposition.Failed));
                Assert.That(store.CommitCountForTests, Is.EqualTo(commitsBefore));
                Assert.That(fileOperations.FileExists(primaryArtifact), Is.True);
                Assert.That(fileOperations.FileExists(rejectedArtifact), Is.False);
                Assert.That(service.PersistenceFrozen, Is.False);
            }
            finally
            {
                store.DeletePersistentDataForTests();
                store.Revoke();
            }
        }

        [Test]
        [Platform("Win")]
        public void SucceededResetWithInnerCommitUncertainRollsBackAndFreezes()
        {
            string normalRoot = Path.Combine(_root, "normal-uncertain-reset-rollback");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotPlan.TryCreate(normalRoot, out MvpApprovalSlotPlan plan, out string planFailure),
                Is.True,
                planFailure);
            Assert.That(
                MvpApprovalVirtualStore.TryPrepare(plan, out MvpApprovalVirtualStore store, out string storeFailure),
                Is.True,
                storeFailure);

            var fileOperations = new MvpApprovalSaveFileOperations(plan.SaveRoot, store);
            var inner = new LocalSaveGameService(plan.SaveRoot, fileOperations);
            var service = new MvpApprovalTransactionalSaveGameService(store, inner);
            string primaryArtifact = Path.Combine(plan.SaveRoot, "save.json");
            string uncertainArtifact = Path.Combine(plan.SaveRoot, "save.temp.json");
            try
            {
                service.Load();
                int commitsBefore = store.CommitCountForTests;

                Assert.Throws<InvalidOperationException>(() =>
                    service.ExecuteReset(candidate =>
                    {
                        fileOperations.Move(primaryArtifact, uncertainArtifact);
                        SetInnerCommitUncertain(candidate);
                        return MvpApprovalStartNewDisposition.Succeeded;
                    }));

                Assert.That(store.CommitCountForTests, Is.EqualTo(commitsBefore));
                Assert.That(fileOperations.FileExists(primaryArtifact), Is.True);
                Assert.That(fileOperations.FileExists(uncertainArtifact), Is.False);
                Assert.That(service.PersistenceFrozen, Is.True);
                Assert.That(service.LastSaveStatus, Is.EqualTo(SaveOperationStatus.CommitUncertain));
                Assert.Throws<InvalidOperationException>(() => service.HasSave());
            }
            finally
            {
                store.DeletePersistentDataForTests();
                store.Revoke();
            }
        }

        [Test]
        public void GuardedFactoryReturnsTransactionalApprovalServiceAndCommitsEachMutationOnce()
        {
            string normalRoot = Path.Combine(_root, "normal-transactional-service");
            Directory.CreateDirectory(normalRoot);
            File.WriteAllBytes(
                Path.Combine(normalRoot, "save.json"),
                new byte[] { 0x21, 0x34, 0x55, 0x89 });
            IReadOnlyDictionary<string, byte[]> normalBefore = Snapshot(normalRoot);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            object created = ((Delegate)GetSaveFactory()).DynamicInvoke();
            Assert.That(
                created.GetType().Name,
                Is.EqualTo("MvpApprovalTransactionalSaveGameService"));
            var service = (ISaveGameService)created;

            object store = typeof(MvpApprovalSlotRuntime)
                .GetField("_activeStore", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            PropertyInfo commitCount = store?.GetType().GetProperty(
                "CommitCountForTests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(commitCount, Is.Not.Null);

            service.Load();
            int afterLoad = (int)commitCount.GetValue(store);
            service.CreateNewSave(RealmId.Crownlands);
            Assert.That((int)commitCount.GetValue(store), Is.EqualTo(afterLoad + 1));

            int beforeTutorial = (int)commitCount.GetValue(store);
            CommitTutorial(
                service,
                FirstWorldTutorialProgressCommand.CameraLookAccepted);
            Assert.That(
                (int)commitCount.GetValue(store),
                Is.EqualTo(beforeTutorial + 1));
            Assert.That(Directory.Exists(MvpApprovalSlotRuntime.ActivePlan.ApprovalRoot), Is.False);
            AssertSnapshotsEqual(normalBefore, Snapshot(normalRoot));
        }

        [Test]
        public void VisibleRegistryWriteWithFailedFlushIsCommitUncertainAndFreezesService()
        {
            string normalRoot = Path.Combine(_root, "normal-flush-failure");
            Directory.CreateDirectory(normalRoot);
            string sentinel = Path.Combine(normalRoot, "save.json");
            byte[] normalBytes = { 0x13, 0x37, 0x42 };
            File.WriteAllBytes(sentinel, normalBytes);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();

            SetRegistryFlushOverride(_ => 5);
            try
            {
                Assert.Catch<IOException>(() =>
                    service.CreateNewSave(RealmId.Crownlands));
                Assert.That(
                    service.LastSaveStatus,
                    Is.EqualTo(SaveOperationStatus.CommitUncertain));
                SaveOperationDisposition disposition =
                    ((ISaveOperationDispositionProvider)service).LastSaveDisposition;
                Assert.That(
                    disposition.Status,
                    Is.EqualTo(SaveOperationStatus.CommitUncertain));
                Assert.That(disposition.MayHaveMutated, Is.True);
                Assert.That(disposition.CandidatePrimaryVerified, Is.False);
                Assert.That(disposition.RequiredBackupVerified, Is.False);
                Assert.That(disposition.CleanupVerified, Is.False);
                Assert.That(
                    MvpApprovalSlotRuntime.CanStartNewJourney(out string resetFailure),
                    Is.False,
                    resetFailure);
                Assert.Throws<InvalidOperationException>(() => service.HasSave());
                CollectionAssert.AreEqual(normalBytes, File.ReadAllBytes(sentinel));
            }
            finally
            {
                SetRegistryFlushOverride(null);
            }
        }

        [Test]
        [Platform("Win")]
        public void ValidButDifferentPostFlushReadbackIsCommitUncertainAndFreezesService()
        {
            string normalRoot = Path.Combine(_root, "normal-readback-mismatch");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            object store = ActiveStore();
            string valueName = ReadPrivateString(store, "_registryValueName");
            bool replaced = false;

            SetRegistryFlushOverride(_ =>
            {
                SetRegistryFlushOverride(null);
                if (!WindowsRegistryValueStore.TryRead(
                        _testRegistrySubKey,
                        valueName,
                        out string intended))
                {
                    return 5;
                }

                const string marker = "\"contentsBase64\":\"";
                int payloadStart = intended.IndexOf(marker, StringComparison.Ordinal);
                if (payloadStart < 0)
                {
                    return 5;
                }

                payloadStart += marker.Length;
                int payloadEnd = intended.IndexOf('"', payloadStart);
                if (payloadEnd < 0)
                {
                    return 5;
                }

                string alternate = intended.Substring(0, payloadStart) +
                                   Convert.ToBase64String(Encoding.UTF8.GetBytes("{}")) +
                                   intended.Substring(payloadEnd);
                WindowsRegistryValueStore.WriteAndFlush(
                    _testRegistrySubKey,
                    valueName,
                    alternate);
                replaced = true;
                return 0;
            });

            try
            {
                Assert.Catch<IOException>(() =>
                    service.CreateNewSave(RealmId.Crownlands));
                Assert.That(replaced, Is.True);
                Assert.That(
                    service.LastSaveStatus,
                    Is.EqualTo(SaveOperationStatus.CommitUncertain));
                Assert.That(
                    MvpApprovalSlotRuntime.CanStartNewJourney(out _),
                    Is.False);
                Assert.Throws<InvalidOperationException>(() => service.HasSave());
            }
            finally
            {
                SetRegistryFlushOverride(null);
            }
        }

        [Test]
        [Platform("Win")]
        public void ApprovalLifecycleCheckpointUsesTransactionalWrapperException()
        {
            string normalRoot = Path.Combine(_root, "normal-lifecycle-checkpoint");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)OfflineServiceStack
                .SaveGameFactoryOverride();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);
            service.CurrentSave.CurrentChapterId = "approval-lifecycle-checkpoint";

            ProfileMutationContainment.InvokeLifecycleSave(service);

            Assert.That(service.LastSaveStatus, Is.EqualTo(SaveOperationStatus.SavedPrimary));
            MethodInfo reconstruct = typeof(MvpApprovalSlotRuntime).GetMethod(
                "ResetRuntimePreservingStoreForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reconstruct, Is.Not.Null);
            reconstruct.Invoke(null, null);
            SetSaveFactory(null);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out failure),
                Is.True,
                failure);
            var restored = (ISaveGameService)OfflineServiceStack
                .SaveGameFactoryOverride();
            restored.Load();
            Assert.That(
                restored.CurrentSave.CurrentChapterId,
                Is.EqualTo("approval-lifecycle-checkpoint"));
        }

        [Test]
        [Platform("Win")]
        public void RuntimeTestCleanupRemovesApprovalRegistrySubkeyWhenLastValueIsDeleted()
        {
            string normalRoot = Path.Combine(_root, "normal-registry-cleanup");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();

            ResetRuntime();
            SetSaveFactory(null);

            Assert.That(ApprovalRegistrySubKeyExists(), Is.False);
        }

        [Test]
        [Platform("Win")]
        public void EmptyApprovalRegistrySubkeyDeletionAccessDeniedFailsClosed()
        {
            string normalRoot = Path.Combine(_root, "normal-registry-delete-denied");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            object store = ActiveStore();
            string valueName = ReadPrivateString(store, "_registryValueName");

            WindowsRegistryValueStore.DeleteKeyOverrideForTests = (_, _) => 5;
            try
            {
                Assert.Catch<IOException>(() =>
                    WindowsRegistryValueStore.DeleteAndFlush(
                        _testRegistrySubKey,
                        valueName));
            }
            finally
            {
                WindowsRegistryValueStore.DeleteKeyOverrideForTests = null;
                WindowsRegistryValueStore.DeleteAndFlush(
                    _testRegistrySubKey,
                    valueName);
            }

            Assert.That(ApprovalRegistrySubKeyExists(), Is.False);
        }

        [Test]
        [Platform("Win")]
        public void NativeSidLoaderFailureInstallsFailClosedFactoryWithoutEscaping()
        {
            string normalRoot = Path.Combine(_root, "normal-native-loader-failure");
            Directory.CreateDirectory(normalRoot);
            WindowsNamedMutex.CurrentUserSidOverrideForTests = () =>
                throw new DllNotFoundException("injected native loader failure");
            bool installed = true;
            string failure = string.Empty;

            Assert.DoesNotThrow(() =>
                installed = MvpApprovalSlotRuntime.TryInstall(
                    true,
                    normalRoot,
                    out failure));

            Assert.That(installed, Is.False);
            Assert.That(failure, Is.Not.Empty);
            Assert.That(OfflineServiceStack.SaveGameFactoryOverride, Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() =>
                OfflineServiceStack.SaveGameFactoryOverride());
        }

        [Test]
        [Platform("Win")]
        public void NativeMutexWaitExceptionClosesCreatedHandleExactlyOnce()
        {
            int closed = 0;
            WindowsNamedMutex.WaitOverrideForTests = (_, __) =>
                throw new EntryPointNotFoundException("injected wait failure");
            WindowsNamedMutex.CloseHandleObserverForTests = _ => closed++;

            Assert.Throws<EntryPointNotFoundException>(() =>
                WindowsNamedMutex.Acquire(
                    @"Global\AnotherLife.MvpApprovalVfsV1.WaitFault." +
                    Guid.NewGuid().ToString("N"),
                    WindowsNamedMutex.GetCurrentUserSid(),
                    50));
            Assert.That(closed, Is.EqualTo(1));
        }

        [Test]
        [Platform("Win")]
        public void AbandonedApprovalStoreMutexFailsClosedWithoutInstallingRuntime()
        {
            int closed = 0;
            WindowsNamedMutex.WaitOverrideForTests = (_, __) => 0x00000080u;
            WindowsNamedMutex.CloseHandleObserverForTests = _ => closed++;
            string normalRoot = Path.Combine(_root, "normal-abandoned-store-mutex");
            Directory.CreateDirectory(normalRoot);

            bool installed = true;
            string failure = string.Empty;
            Assert.DoesNotThrow(() =>
                installed = MvpApprovalSlotRuntime.TryInstall(
                    true,
                    normalRoot,
                    out failure));

            Assert.That(installed, Is.False);
            Assert.That(failure, Does.Contain("IOException"));
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(MvpApprovalSlotRuntime.ActivePlan, Is.Null);
        }

        [Test]
        [Platform("Win")]
        public void AbandonedRegistryMutationMutexFailsBeforeWritingValue()
        {
            const string valueName = "abandoned-mutation";
            int closed = 0;
            WindowsNamedMutex.WaitOverrideForTests = (_, __) => 0x00000080u;
            WindowsNamedMutex.CloseHandleObserverForTests = _ => closed++;

            try
            {
                Assert.Throws<IOException>(() =>
                    WindowsRegistryValueStore.WriteAndFlush(
                        _testRegistrySubKey,
                        valueName,
                        "must-not-persist"));
            }
            finally
            {
                WindowsNamedMutex.WaitOverrideForTests = null;
            }

            Assert.That(closed, Is.EqualTo(1));
            Assert.That(
                WindowsRegistryValueStore.TryRead(
                    _testRegistrySubKey,
                    valueName,
                    out _),
                Is.False);
        }

        [Test]
        [Platform("Win")]
        public void RevokeCannotReturnWhileTransactionIsPausedBeforePersistence()
        {
            string normalRoot = Path.Combine(_root, "normal-revoke-linearization");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)OfflineServiceStack
                .SaveGameFactoryOverride();
            service.Load();
            var store = (MvpApprovalVirtualStore)ActiveStore();
            using var reachedPersist = new ManualResetEventSlim(false);
            using var releasePersist = new ManualResetEventSlim(false);
            MvpApprovalVirtualStore.BeforePersistForTests = () =>
            {
                reachedPersist.Set();
                releasePersist.Wait();
            };

            Task commit = Task.Run(() => service.CreateNewSave(RealmId.Crownlands));
            Assert.That(reachedPersist.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Task revoke = Task.Run(store.Revoke);
            try
            {
                Assert.That(revoke.Wait(200), Is.False,
                    "Revoke returned before the active transaction linearized.");
            }
            finally
            {
                releasePersist.Set();
            }

            Assert.That(commit.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(revoke.Wait(TimeSpan.FromSeconds(5)), Is.True);
        }

        [Test]
        [Platform("Win")]
        public void ResetPreservesForeignFingerprintValueInSharedApprovalKey()
        {
            string normalRoot = Path.Combine(_root, "normal-foreign-registry-value");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            const string ForeignValue = "foreign-profile-fingerprint";
            WindowsRegistryValueStore.WriteAndFlush(
                _testRegistrySubKey,
                ForeignValue,
                "foreign");

            try
            {
                ResetRuntime();
                Assert.That(ApprovalRegistryValueExists(ForeignValue), Is.True);
            }
            finally
            {
                WindowsRegistryValueStore.DeleteAndFlush(
                    _testRegistrySubKey,
                    ForeignValue);
            }
        }

        [Test]
        [Platform("Win")]
        public void RuntimeSynchronizationTimesOutBeforeAContendingMonitorCanWaitForever()
        {
            object sync = typeof(MvpApprovalSlotRuntime).GetField(
                "Sync",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            Assert.That(sync, Is.Not.Null);
            using var held = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            Task holder = Task.Run(() =>
            {
                Monitor.Enter(sync);
                try
                {
                    held.Set();
                    release.Wait();
                }
                finally
                {
                    Monitor.Exit(sync);
                }
            });
            Assert.That(held.Wait(TimeSpan.FromSeconds(2)), Is.True);

            try
            {
                Task<bool> contender = Task.Run(() =>
                    MvpApprovalSlotRuntime.CanStartNewJourney(out _));
                Assert.That(
                    contender.Wait(TimeSpan.FromMilliseconds(6500)),
                    Is.True,
                    "Runtime synchronization must time out before an unbounded managed wait.");
                Assert.That(contender.Result, Is.False);
            }
            finally
            {
                release.Set();
                Assert.That(holder.Wait(TimeSpan.FromSeconds(2)), Is.True);
            }
        }

        [Test]
        [Platform("Win")]
        public void TryInstallTimeoutDoesNotMutateOrRevokeRuntimeOwnedByLockHolder()
        {
            string normalRoot = Path.Combine(_root, "normal-install-lock-timeout");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string initialFailure),
                Is.True,
                initialFailure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            object planBefore = MvpApprovalSlotRuntime.ActivePlan;
            object serviceBefore = MvpApprovalSlotRuntime.ActiveService;
            object factoryBefore = GetSaveFactory();
            object sync = typeof(MvpApprovalSlotRuntime).GetField(
                "Sync",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            Assert.That(sync, Is.Not.Null);
            using var held = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            Task holder = Task.Run(() =>
            {
                Monitor.Enter(sync);
                try
                {
                    held.Set();
                    release.Wait();
                }
                finally
                {
                    Monitor.Exit(sync);
                }
            });
            Assert.That(held.Wait(TimeSpan.FromSeconds(2)), Is.True);

            bool installed = true;
            string failure = string.Empty;
            try
            {
                Task contender = Task.Run(() =>
                    installed = MvpApprovalSlotRuntime.TryInstall(
                        true,
                        normalRoot,
                        out failure));
                Assert.That(
                    contender.Wait(TimeSpan.FromMilliseconds(6500)),
                    Is.True,
                    "TryInstall must return after its bounded runtime-lock wait.");
            }
            finally
            {
                release.Set();
                Assert.That(holder.Wait(TimeSpan.FromSeconds(2)), Is.True);
            }

            Assert.That(installed, Is.False);
            Assert.That(failure, Does.Contain("busy"));
            Assert.That(GetSaveFactory(), Is.SameAs(factoryBefore));
            Assert.That(MvpApprovalSlotRuntime.ActivePlan, Is.SameAs(planBefore));
            Assert.That(MvpApprovalSlotRuntime.ActiveService, Is.SameAs(serviceBefore));
            Assert.DoesNotThrow(() => service.HasSave());
        }

        [Test]
        [Platform("Win")]
        public void TryStartReturnsFailedInsteadOfThrowingOnRuntimeLockTimeout()
        {
            object sync = typeof(MvpApprovalSlotRuntime).GetField(
                "Sync",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            Assert.That(sync, Is.Not.Null);
            using var held = new ManualResetEventSlim(false);
            using var release = new ManualResetEventSlim(false);
            Task holder = Task.Run(() =>
            {
                Monitor.Enter(sync);
                try
                {
                    held.Set();
                    release.Wait();
                }
                finally
                {
                    Monitor.Exit(sync);
                }
            });
            Assert.That(held.Wait(TimeSpan.FromSeconds(2)), Is.True);

            try
            {
                Task<MvpApprovalStartNewDisposition> contender = Task.Run(() =>
                    MvpApprovalSlotRuntime.TryStartNewJourney(out _));
                bool completed = false;
                Assert.DoesNotThrow(() =>
                    completed = contender.Wait(TimeSpan.FromMilliseconds(6500)));
                Assert.That(completed, Is.True);
                Assert.That(
                    contender.Result,
                    Is.EqualTo(MvpApprovalStartNewDisposition.Failed));
            }
            finally
            {
                release.Set();
                Assert.That(holder.Wait(TimeSpan.FromSeconds(2)), Is.True);
            }
        }

        [Test]
        [Platform("Win")]
        public void ApprovalMutexIsUserQualifiedAndHasNoUnboundedManagedGlobalGate()
        {
            string normalRoot = Path.Combine(_root, "normal-mutex-identity");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            object store = ActiveStore();
            string mutexName = (string)store.GetType()
                .GetField("_mutexName", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(store);
            string sid = ReadPrivateString(store, "_userSid");

            Assert.That(sid, Is.Not.Null.And.Not.Empty);
            StringAssert.Contains(sid, mutexName);
            Assert.That(
                store.GetType().GetField(
                    "GlobalSync",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null,
                "The named mutex must be the bounded same-process and cross-session gate.");
            Assert.That(
                typeof(MvpApprovalTransactionalSaveGameService).GetField(
                    "_sync",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null,
                "The transactional wrapper must not add an unbounded managed gate.");
        }

        [Test]
        [Platform("Win")]
        public void HostileNamedObjectTypeSquatFailsClosedWithoutPoisoningLaterOperations()
        {
            string normalRoot = Path.Combine(_root, "normal-mutex-type-squat");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            object store = ActiveStore();
            string mutexName = (string)store.GetType()
                .GetField("_mutexName", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(store);

            using (var hostile = new EventWaitHandle(
                       false,
                       EventResetMode.AutoReset,
                       mutexName))
            {
                Assert.Catch<IOException>(() => service.Load());
            }

            Assert.DoesNotThrow(() => service.Load());
            Assert.That(service.CurrentSave, Is.Not.Null, service.LastLoadMessage);
        }

        [Test]
        [Platform("Win")]
        public void NonCanonicalPersistedApprovalInventoryIsRejectedFailClosed()
        {
            string normalRoot = Path.Combine(_root, "normal-noncanonical-envelope");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            object store = ActiveStore();
            string owner = ReadPrivateString(store, "_ownerFingerprint");
            string valueName = ReadPrivateString(store, "_registryValueName");
            WindowsRegistryValueStore.WriteAndFlush(
                _testRegistrySubKey,
                valueName,
                "{\"version\":1,\"ownerFingerprint\":\"" + owner +
                "\",\"entries\":[{\"name\":\"SAVE.JSON\",\"contentsBase64\":\"\"}]}");

            try
            {
                MethodInfo reconstruct = typeof(MvpApprovalSlotRuntime).GetMethod(
                    "ResetRuntimePreservingStoreForTests",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(reconstruct, Is.Not.Null);
                reconstruct.Invoke(null, null);
                SetSaveFactory(null);

                Assert.That(
                    MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out failure),
                    Is.False);
                StringAssert.Contains("virtual store", failure.ToLowerInvariant());
            }
            finally
            {
                WindowsRegistryValueStore.DeleteAndFlush(
                    _testRegistrySubKey,
                    valueName);
            }
        }

        [Test]
        public void QuarantineOrderingIsAbstractedFromPhysicalFileMetadata()
        {
            MethodInfo metadata = typeof(ISaveFileOperations).GetMethod(
                "GetCreationTimeUtc",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(metadata, Is.Not.Null);
            Assert.That(
                typeof(MvpApprovalSaveFileOperations).GetMethod(
                    "GetCreationTimeUtc",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
        }

        [Test]
        [Platform("Win")]
        public void ThrowingResetCallbackRollsBackWorkingEnvelopeBeforeReconstruction()
        {
            string normalRoot = Path.Combine(_root, "normal-callback-rollback");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);
            string beforeChapter = service.CurrentSave.CurrentChapterId;

            MvpApprovalSlotRuntime.BeforeFreshLoadForTests = () =>
                throw new InvalidOperationException("injected reset callback failure");
            try
            {
                Assert.That(
                    MvpApprovalSlotRuntime.TryStartNewJourney(out failure),
                    Is.EqualTo(MvpApprovalStartNewDisposition.Failed));
            }
            finally
            {
                MvpApprovalSlotRuntime.BeforeFreshLoadForTests = null;
            }

            Assert.That(service.HasSave(), Is.True);
            Assert.That(service.CurrentSave, Is.Not.Null);
            Assert.That(service.CurrentSave.CurrentChapterId, Is.EqualTo(beforeChapter));

            MethodInfo reconstruct = typeof(MvpApprovalSlotRuntime).GetMethod(
                "ResetRuntimePreservingStoreForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reconstruct, Is.Not.Null);
            reconstruct.Invoke(null, null);
            SetSaveFactory(null);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out failure),
                Is.True,
                failure);
            var restored = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            restored.Load();

            Assert.That(restored.HasSave(), Is.True);
            Assert.That(restored.CurrentSave.CurrentChapterId, Is.EqualTo(beforeChapter));
        }

        [Test]
        [Platform("Win")]
        public void NonThrowingRollbackReloadFailureFreezesApprovalPersistence()
        {
            string normalRoot = Path.Combine(_root, "normal-rollback-reload-failure");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotPlan.TryCreate(
                    normalRoot,
                    out MvpApprovalSlotPlan plan,
                    out string planFailure),
                Is.True,
                planFailure);
            Assert.That(
                MvpApprovalVirtualStore.TryPrepare(
                    plan,
                    out MvpApprovalVirtualStore store,
                    out string storeFailure),
                Is.True,
                storeFailure);

            var fileOperations = new RollbackReadFailureFileOperations(
                new MvpApprovalSaveFileOperations(plan.SaveRoot, store));
            var inner = new LocalSaveGameService(plan.SaveRoot, fileOperations);
            var service = new MvpApprovalTransactionalSaveGameService(store, inner);
            try
            {
                service.Load();
                Assert.That(service.CurrentSave, Is.Not.Null, service.LastLoadMessage);

                LogAssert.Expect(
                    LogType.Error,
                    "AL-SAVE-PRIMARY-UNREADABLE: The primary generation could not be read consistently; all generations were preserved.");
                Assert.Throws<InvalidOperationException>(() =>
                    service.ExecuteReset<int>(candidate =>
                    {
                        candidate.CurrentSave.CurrentChapterId = "must-be-rolled-back";
                        fileOperations.FailReads = true;
                        throw new InvalidOperationException("injected operation failure");
                    }));

                Assert.That(service.LastLoadStatus, Is.EqualTo(SaveLoadStatus.RecoveryFailed));
                Assert.That(service.CurrentSave, Is.Null);
                Assert.That(service.PersistenceFrozen, Is.True);
                Assert.That(
                    service.LastSaveStatus,
                    Is.EqualTo(SaveOperationStatus.CommitUncertain));
                Assert.Throws<InvalidOperationException>(() => service.HasSave());
            }
            finally
            {
                fileOperations.FailReads = false;
                store.DeletePersistentDataForTests();
                store.Revoke();
            }
        }

        [Test]
        [Platform("Win")]
        public void BusyApprovalStoreMutexFailsClosedWithoutThrowing()
        {
            string normalRoot = Path.Combine(_root, "normal-busy-mutex");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string installFailure),
                Is.True,
                installFailure);
            _ = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();

            FieldInfo activeStoreField = typeof(MvpApprovalSlotRuntime).GetField(
                "_activeStore",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(activeStoreField, Is.Not.Null);
            object activeStore = activeStoreField.GetValue(null);
            Assert.That(activeStore, Is.Not.Null);
            FieldInfo mutexNameField = activeStore.GetType().GetField(
                "_mutexName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mutexNameField, Is.Not.Null);
            string mutexName = (string)mutexNameField.GetValue(activeStore);

            using var acquired = new System.Threading.ManualResetEventSlim(false);
            using var release = new System.Threading.ManualResetEventSlim(false);
            Exception holderFailure = null;
            var holder = new System.Threading.Thread(() =>
            {
                try
                {
                    using var mutex = new System.Threading.Mutex(false, mutexName);
                    mutex.WaitOne();
                    acquired.Set();
                    release.Wait();
                    mutex.ReleaseMutex();
                }
                catch (Exception exception)
                {
                    holderFailure = exception;
                    acquired.Set();
                }
            })
            {
                IsBackground = true
            };

            holder.Start();
            try
            {
                Assert.That(acquired.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(holderFailure, Is.Null);
                bool canStart = true;
                string failure = string.Empty;
                Assert.DoesNotThrow(() =>
                    canStart = MvpApprovalSlotRuntime.CanStartNewJourney(out failure));
                Assert.That(canStart, Is.False);
                Assert.That(failure, Does.Contain("mutex"));
            }
            finally
            {
                release.Set();
                holder.Join(TimeSpan.FromSeconds(2));
            }

            Assert.That(holderFailure, Is.Null);
        }

        [Test]
        public void StartNewDeletesOnlyApprovalArtifactsThenCreatesFreshCurrentProfile()
        {
            string normalRoot = Path.Combine(_root, "normal");
            Directory.CreateDirectory(normalRoot);
            string sentinel = Path.Combine(normalRoot, "save.json");
            byte[] normalBytes = { 0xde, 0xad, 0xbe, 0xef };
            File.WriteAllBytes(sentinel, normalBytes);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);

            Assert.That(MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string installFailure), Is.True, installFailure);
            var service = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);
            Assert.That(service.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.Crownlands));
            CommitTutorial(
                service,
                FirstWorldTutorialProgressCommand.CameraLookAccepted);

            var foreign = CreateLocalSave(Path.Combine(_root, "foreign"));
            foreign.Load();
            foreign.DeleteSave();
            Assert.That(foreign.CurrentSave, Is.Not.Null, "A foreign isolated service must stay contained.");

            MvpApprovalStartNewDisposition disposition =
                MvpApprovalSlotRuntime.TryStartNewJourney(out string resetFailure);

            Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.Succeeded), resetFailure);
            Assert.That(service.CurrentSave, Is.Not.Null);
            Assert.That(service.LastLoadStatus, Is.EqualTo(SaveLoadStatus.CreatedNew), service.LastLoadMessage);
            Assert.That(service.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
            Assert.That(MvpLoopSaveCodec.Read(service.CurrentSave).ShouldSkipCreate, Is.False);
            Assert.That(MvpApprovalSlotRuntime.IsDeleteAuthorized(service), Is.False);
            Assert.That(
                service.CurrentSave.FirstWorldProgress == null ||
                service.CurrentSave.FirstWorldProgress.Version == 0,
                Is.True);
            AssertSnapshotsEqual(before, Snapshot(normalRoot));
        }

        [Test]
        public void AuthorizedResetRejectsPostValidationJunctionSwapWithoutNormalMutation()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Directory-junction swap evidence is Windows-specific.");
            }

            string normalRoot = Path.Combine(_root, "normal-junction-target");
            LocalSaveGameService normal = CreateLocalSave(normalRoot);
            normal.Load();
            normal.CreateNewSave(RealmId.Crownlands);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService approval =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            approval.Load();
            MvpApprovalSlotPlan plan = MvpApprovalSlotRuntime.ActivePlan;
            bool junctionSwapSucceeded = false;
            SetBeforeDeleteHook(() =>
            {
                try
                {
                    Directory.Delete(plan.SaveRoot, true);
                    CreateDirectoryJunction(plan.SaveRoot, normalRoot);
                    junctionSwapSucceeded = true;
                }
                catch (IOException)
                {
                }
            });

            IReadOnlyDictionary<string, byte[]> after;
            MvpApprovalStartNewDisposition disposition;
            try
            {
                disposition = MvpApprovalSlotRuntime.TryStartNewJourney(out failure);
                after = Snapshot(normalRoot);
            }
            finally
            {
                SetBeforeDeleteHook(null);
                if (Directory.Exists(plan.SaveRoot) &&
                    (File.GetAttributes(plan.SaveRoot) & FileAttributes.ReparsePoint) != 0)
                {
                    Directory.Delete(plan.SaveRoot, false);
                }
            }

            Assert.That(junctionSwapSucceeded, Is.False);
            Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.Succeeded), failure);
            AssertSnapshotsEqual(before, after);
        }

        [Test]
        public void AuthorizedResetRejectsOrdinaryDirectoryReplacementWithoutNormalMutation()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Directory identity replacement evidence is Windows-specific.");
            }

            string normalRoot = Path.Combine(_root, "normal-directory-target");
            LocalSaveGameService normal = CreateLocalSave(normalRoot);
            normal.Load();
            normal.CreateNewSave(RealmId.Crownlands);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService approval =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            approval.Load();
            MvpApprovalSlotPlan plan = MvpApprovalSlotRuntime.ActivePlan;
            string displacedApproval = plan.SaveRoot + ".displaced";
            bool replacementSucceeded = false;
            SetBeforeDeleteHook(() =>
            {
                try
                {
                    Directory.Move(plan.SaveRoot, displacedApproval);
                    Directory.Move(normalRoot, plan.SaveRoot);
                    replacementSucceeded = true;
                }
                catch (IOException)
                {
                }
            });

            IReadOnlyDictionary<string, byte[]> after;
            MvpApprovalStartNewDisposition disposition;
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                disposition = MvpApprovalSlotRuntime.TryStartNewJourney(out _);
                after = Snapshot(normalRoot);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
                SetBeforeDeleteHook(null);
                if (Directory.Exists(plan.SaveRoot) && !Directory.Exists(normalRoot))
                {
                    Directory.Move(plan.SaveRoot, normalRoot);
                }
                if (Directory.Exists(displacedApproval) && !Directory.Exists(plan.SaveRoot))
                {
                    Directory.Move(displacedApproval, plan.SaveRoot);
                }
            }

            Assert.That(replacementSucceeded, Is.False);
            Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.Succeeded));
            AssertSnapshotsEqual(before, after);
        }

        [Test]
        public void AuthorizationRevocationCannotFallbackToPathDeleteAfterEntryGate()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Directory-junction swap evidence is Windows-specific.");
            }

            string normalRoot = Path.Combine(_root, "normal-revocation-target");
            LocalSaveGameService normal = CreateLocalSave(normalRoot);
            normal.Load();
            normal.CreateNewSave(RealmId.Crownlands);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService approval =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            approval.Load();
            MvpApprovalSlotPlan plan = MvpApprovalSlotRuntime.ActivePlan;
            string conflictingNormalRoot = Path.Combine(_root, "conflicting-normal");
            Directory.CreateDirectory(conflictingNormalRoot);
            bool conflictingInstallSucceeded = true;
            SetBeforeDeleteArtifactsHook(() =>
            {
                conflictingInstallSucceeded = MvpApprovalSlotRuntime.TryInstall(
                    true,
                    conflictingNormalRoot,
                    out _);
            });

            IReadOnlyDictionary<string, byte[]> after;
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                MvpApprovalSlotRuntime.TryStartNewJourney(out _);
                after = Snapshot(normalRoot);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnore;
                SetBeforeDeleteArtifactsHook(null);
            }

            Assert.That(conflictingInstallSucceeded, Is.False);
            AssertSnapshotsEqual(before, after);
        }

        [Test]
        public void FreshLoadLeaseRejectsNormalRootReplacementWithoutMutation()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Directory identity replacement evidence is Windows-specific.");
            }

            string normalRoot = Path.Combine(_root, "empty-normal-load-target");
            Directory.CreateDirectory(normalRoot);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService approval =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            approval.Load();
            approval.CreateNewSave(RealmId.Crownlands);
            MvpApprovalSlotPlan plan = MvpApprovalSlotRuntime.ActivePlan;
            string displacedApproval = plan.SaveRoot + ".load-displaced";
            bool replacementSucceeded = false;
            SetBeforeFreshLoadHook(() =>
            {
                try
                {
                    Directory.Move(plan.SaveRoot, displacedApproval);
                    Directory.Move(normalRoot, plan.SaveRoot);
                    replacementSucceeded = true;
                }
                catch (IOException)
                {
                }
            });

            MvpApprovalStartNewDisposition disposition;
            try
            {
                disposition = MvpApprovalSlotRuntime.TryStartNewJourney(out failure);
            }
            finally
            {
                SetBeforeFreshLoadHook(null);
            }

            Assert.That(replacementSucceeded, Is.False);
            Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.Succeeded), failure);
            AssertSnapshotsEqual(before, Snapshot(normalRoot));
        }

        [Test]
        public void FreshLoadLeaseRejectsInPlaceSaveRootJunctionWithoutNormalMutation()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("In-place directory reparse evidence is Windows-specific.");
            }

            string normalRoot = Path.Combine(_root, "empty-normal-reparse-target");
            Directory.CreateDirectory(normalRoot);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService approval =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            approval.Load();
            approval.CreateNewSave(RealmId.Crownlands);
            MvpApprovalSlotPlan plan = MvpApprovalSlotRuntime.ActivePlan;
            bool inPlaceJunctionSucceeded = false;
            SetBeforeFreshLoadHook(() =>
            {
                inPlaceJunctionSucceeded = TrySetDirectoryJunctionInPlace(
                    plan.SaveRoot,
                    normalRoot);
            });

            MvpApprovalStartNewDisposition disposition;
            try
            {
                disposition = MvpApprovalSlotRuntime.TryStartNewJourney(out failure);
            }
            finally
            {
                SetBeforeFreshLoadHook(null);
                ResetRuntime();
                if (Directory.Exists(plan.SaveRoot) &&
                    (File.GetAttributes(plan.SaveRoot) & FileAttributes.ReparsePoint) != 0)
                {
                    Directory.Delete(plan.SaveRoot, false);
                    Directory.CreateDirectory(plan.SaveRoot);
                }
            }

            Assert.That(inPlaceJunctionSucceeded, Is.False);
            Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.Succeeded), failure);
            AssertSnapshotsEqual(before, Snapshot(normalRoot));
        }

        [Test]
        public void PreExistingFilesystemSlotWithoutGuardIsIgnoredWithoutMutation()
        {
            string normalRoot = Path.Combine(_root, "normal-missing-guard");
            Directory.CreateDirectory(normalRoot);
            string sentinel = Path.Combine(normalRoot, "save.json");
            byte[] normalBytes = { 0x31, 0x41, 0x59, 0x26 };
            File.WriteAllBytes(sentinel, normalBytes);
            Assert.That(
                MvpApprovalSlotPlan.TryCreate(normalRoot, out MvpApprovalSlotPlan plan, out string planFailure),
                Is.True,
                planFailure);
            Directory.CreateDirectory(plan.ApprovalRoot);
            Directory.CreateDirectory(plan.SaveRoot);
            File.WriteAllText(plan.MarkerPath, MvpApprovalSlotPlan.MarkerContents);

            bool installed = MvpApprovalSlotRuntime.TryInstall(
                true,
                normalRoot,
                out string failure);

            Assert.That(installed, Is.True, failure);
            Assert.That(File.Exists(plan.SaveRootGuardPath), Is.False);
            CollectionAssert.AreEqual(normalBytes, File.ReadAllBytes(sentinel));
        }

        [Test]
        [Platform("Win")]
        public void InstalledSaveRootGuardCannotBeRewritten()
        {
            string normalRoot = Path.Combine(_root, "normal-immutable-guard");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            _ = (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();

            const uint genericWrite = 0x40000000;
            const uint shareReadWriteDelete = 0x00000007;
            const uint openExisting = 3;
            const uint openReparsePoint = 0x00200000;
            bool rewritten = false;
            using (SafeFileHandle handle = CreateFileForReparseTest(
                       MvpApprovalSlotRuntime.ActivePlan.SaveRootGuardPath,
                       genericWrite,
                       shareReadWriteDelete,
                       IntPtr.Zero,
                       openExisting,
                       openReparsePoint,
                       IntPtr.Zero))
            {
                if (handle != null && !handle.IsInvalid)
                {
                    using var stream = new FileStream(handle, FileAccess.Write);
                    byte[] foreign = Encoding.UTF8.GetBytes("foreign-guard");
                    stream.SetLength(0);
                    stream.Write(foreign, 0, foreign.Length);
                    stream.Flush(true);
                    rewritten = true;
                }
            }

            Assert.That(rewritten, Is.False);
            Assert.That(MvpApprovalSlotRuntime.CanStartNewJourney(out failure), Is.True, failure);
        }

        [Test]
        [Platform("Win")]
        public void ApprovalCopyIgnoresHardLinkedFilesystemDestinationWithoutMutatingNormalFile()
        {
            string normalRoot = Path.Combine(_root, "normal-hard-link");
            Directory.CreateDirectory(normalRoot);
            string normalSentinel = Path.Combine(normalRoot, "normal-sentinel.json");
            byte[] normalBytes = Encoding.UTF8.GetBytes("NORMAL-SAVE-MUST-REMAIN");
            File.WriteAllBytes(normalSentinel, normalBytes);

            Assert.That(MvpApprovalSlotPlan.TryCreate(normalRoot, out MvpApprovalSlotPlan plan, out _), Is.True);
            Directory.CreateDirectory(plan.SaveRoot);
            string primaryPath = Path.Combine(plan.SaveRoot, "save.json");
            string backupPath = Path.Combine(plan.SaveRoot, "save.backup.json");
            Assert.That(
                CreateHardLink(backupPath, normalSentinel, IntPtr.Zero),
                Is.True,
                "CreateHardLinkW failed with Win32 " + Marshal.GetLastWin32Error() + ".");

            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService service =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();

            FieldInfo operationsField = typeof(LocalSaveGameService).GetField(
                "_fileOperations",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(operationsField, Is.Not.Null);
            object operations = operationsField.GetValue(InnerLocalSave(service));
            MethodInfo copy = operations.GetType().GetMethod(
                "Copy",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(copy, Is.Not.Null);

            Exception observed = null;
            try
            {
                copy.Invoke(operations, new object[] { primaryPath, backupPath, true });
            }
            catch (TargetInvocationException exception)
            {
                observed = exception.InnerException;
            }

            CollectionAssert.AreEqual(normalBytes, File.ReadAllBytes(normalSentinel));
            Assert.That(observed, Is.TypeOf<InvalidOperationException>());
            CollectionAssert.AreEqual(normalBytes, File.ReadAllBytes(backupPath));
        }

        [Test]
        public void FileDispositionInfoUsesNativeOneByteBooleanAbi()
        {
            Type deletionType = RuntimeType(
                "AL.Services.Local.WindowsOwnedArtifactDeletion");
            Type dispositionType = deletionType.GetNestedType(
                "FileDispositionInformation",
                BindingFlags.NonPublic);

            Assert.That(dispositionType, Is.Not.Null);
            Assert.That(
                System.Runtime.InteropServices.Marshal.SizeOf(dispositionType),
                Is.EqualTo(1));
        }

        [Test]
        public void ValidatedArtifactCannotBeRelocatedIntoNormalRootBeforeDisposition()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Artifact relocation evidence is Windows-specific.");
            }

            string normalRoot = Path.Combine(_root, "normal-artifact-target");
            LocalSaveGameService normal = CreateLocalSave(normalRoot);
            normal.Load();
            normal.CreateNewSave(RealmId.Crownlands);
            IReadOnlyDictionary<string, byte[]> before = Snapshot(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService approval =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            approval.Load();
            approval.CreateNewSave(RealmId.Crownlands);
            string relocated = Path.Combine(normalRoot, "relocated-approval-artifact.json");
            bool relocationSucceeded = false;
            SetAfterArtifactValidationHook(artifact =>
            {
                try
                {
                    File.Move(artifact, relocated);
                    relocationSucceeded = true;
                }
                catch (IOException)
                {
                }
            });

            try
            {
                MvpApprovalStartNewDisposition disposition =
                    MvpApprovalSlotRuntime.TryStartNewJourney(out failure);

                Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.Succeeded), failure);
                Assert.That(relocationSucceeded, Is.False);
                AssertSnapshotsEqual(before, Snapshot(normalRoot));
            }
            finally
            {
                SetAfterArtifactValidationHook(null);
            }
        }

        [Test]
        public void FailedLoadResetClearsTransientJourneyStateBeforeRequestingBootReload()
        {
            string normalRoot = Path.Combine(_root, "normal-failed-load");
            Directory.CreateDirectory(normalRoot);
            Assert.That(
                MvpApprovalSlotRuntime.TryInstall(true, normalRoot, out string failure),
                Is.True,
                failure);
            ISaveGameService service =
                (ISaveGameService)((Delegate)GetSaveFactory()).DynamicInvoke();
            service.Load();
            service.CreateNewSave(RealmId.Crownlands);

            AL.Data.Runtime.SliceRunState.ConfirmChampion(new ChampionState
            {
                Username = "StaleResetHero",
                Family = ClassFamily.Warrior,
                Realm = RealmId.Crownlands
            });
            CharacterCreationIdentity.RememberPersisted("StaleResetHero");
            FirstSessionChampionStart.EnableEncounterHarness();
            CrossModeSession.RememberAdventure(SharedMenuIds.AdventureScene);
            CrossModeSession.ArmTeachingReturn();

            Type markerInterface = RuntimeType("AL.Core.IOfflineServiceStackMarker");
            object marker = CreateFailedStackMarker(service);
            RegisterService(markerInterface, marker);
            RegisterService(typeof(ISaveGameService), service);
            try
            {
                MvpApprovalStartNewDisposition disposition =
                    MvpApprovalSlotRuntime.TryStartNewJourney(out failure);

                Assert.That(disposition, Is.EqualTo(MvpApprovalStartNewDisposition.ReloadBootRequired), failure);
                Assert.That(AL.Data.Runtime.SliceRunState.HasConfirmedChampion, Is.False);
                Assert.That(CharacterCreationIdentity.ClaimedUsernames, Is.Empty);
                Assert.That(FirstSessionChampionStart.IsFirstSessionLanding, Is.True);
                Assert.That(CrossModeSession.HasActiveRoundTrip, Is.False);
                Assert.That(CrossModeSession.HasPendingTeachingReturn, Is.False);
            }
            finally
            {
                RemoveService(markerInterface);
                RemoveService(typeof(ISaveGameService));
                AL.Data.Runtime.SliceRunState.Reset();
                CharacterCreationIdentity.ResetClaims();
                FirstSessionChampionStart.ResetToFirstSessionLanding();
                CrossModeSession.Reset();
            }
        }

        private static void CreateDirectoryJunction(string junctionPath, string targetPath)
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c mklink /J \"" + junctionPath + "\" \"" + targetPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }))
            {
                Assert.That(process, Is.Not.Null);
                process.WaitForExit();
                Assert.That(
                    process.ExitCode,
                    Is.Zero,
                    process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
            }

            Assert.That(
                File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint,
                Is.EqualTo(FileAttributes.ReparsePoint));
        }

        private static bool TrySetDirectoryJunctionInPlace(
            string directoryPath,
            string targetPath)
        {
            const uint genericWrite = 0x40000000;
            const uint shareRead = 0x00000001;
            const uint shareWrite = 0x00000002;
            const uint shareDelete = 0x00000004;
            const uint openExisting = 3;
            const uint fileFlagOpenReparsePoint = 0x00200000;
            const uint fileFlagBackupSemantics = 0x02000000;
            const uint fsctlSetReparsePoint = 0x000900A4;
            const uint mountPointTag = 0xA0000003;

            using (SafeFileHandle handle = CreateFileForReparseTest(
                       directoryPath,
                       genericWrite,
                       shareRead | shareWrite | shareDelete,
                       IntPtr.Zero,
                       openExisting,
                       fileFlagOpenReparsePoint | fileFlagBackupSemantics,
                       IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    return false;
                }

                string printName = Path.GetFullPath(targetPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                byte[] substituteName = Encoding.Unicode.GetBytes("\\??\\" + printName);
                byte[] visibleName = Encoding.Unicode.GetBytes(printName);
                byte[] pathBuffer = new byte[
                    substituteName.Length + sizeof(char) + visibleName.Length + sizeof(char)];
                Buffer.BlockCopy(substituteName, 0, pathBuffer, 0, substituteName.Length);
                Buffer.BlockCopy(
                    visibleName,
                    0,
                    pathBuffer,
                    substituteName.Length + sizeof(char),
                    visibleName.Length);

                ushort reparseDataLength = checked((ushort)(8 + pathBuffer.Length));
                byte[] buffer = new byte[8 + reparseDataLength];
                Buffer.BlockCopy(BitConverter.GetBytes(mountPointTag), 0, buffer, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(reparseDataLength), 0, buffer, 4, 2);
                Buffer.BlockCopy(BitConverter.GetBytes((ushort)0), 0, buffer, 6, 2);
                Buffer.BlockCopy(BitConverter.GetBytes((ushort)0), 0, buffer, 8, 2);
                Buffer.BlockCopy(
                    BitConverter.GetBytes(checked((ushort)substituteName.Length)),
                    0,
                    buffer,
                    10,
                    2);
                Buffer.BlockCopy(
                    BitConverter.GetBytes(checked((ushort)(substituteName.Length + sizeof(char)))),
                    0,
                    buffer,
                    12,
                    2);
                Buffer.BlockCopy(
                    BitConverter.GetBytes(checked((ushort)visibleName.Length)),
                    0,
                    buffer,
                    14,
                    2);
                Buffer.BlockCopy(pathBuffer, 0, buffer, 16, pathBuffer.Length);

                return DeviceIoControlForReparseTest(
                    handle,
                    fsctlSetReparsePoint,
                    buffer,
                    (uint)buffer.Length,
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero);
            }
        }

        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateFileW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern SafeFileHandle CreateFileForReparseTest(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControlForReparseTest(
            SafeFileHandle device,
            uint controlCode,
            byte[] inputBuffer,
            uint inputBufferSize,
            IntPtr outputBuffer,
            uint outputBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateHardLinkW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(
            string fileName,
            string existingFileName,
            IntPtr securityAttributes);

        private static LocalSaveGameService CreateLocalSave(string path)
        {
            Directory.CreateDirectory(path);
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);
            Assert.That(constructor, Is.Not.Null);
            return (LocalSaveGameService)constructor.Invoke(new object[] { path });
        }

        private static LocalSaveGameService InnerLocalSave(ISaveGameService service)
        {
            Assert.That(service, Is.Not.Null);
            PropertyInfo property = service.GetType().GetProperty(
                "InnerService",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (LocalSaveGameService)property.GetValue(service);
        }

        private static object ActiveStore()
        {
            object store = typeof(MvpApprovalSlotRuntime)
                .GetField("_activeStore", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            Assert.That(store, Is.Not.Null);
            return store;
        }

        private static string ReadPrivateString(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (string)field.GetValue(instance);
        }

        private static void SetRegistryFlushOverride(Func<IntPtr, int> callback)
        {
            Type storeType = RuntimeType("AL.Services.Local.WindowsRegistryValueStore");
            FieldInfo field = storeType.GetField(
                "FlushOverrideForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, callback);
        }

        private static bool ApprovalRegistrySubKeyExists()
        {
            IntPtr currentUser = new IntPtr(unchecked((int)0x80000001));
            int opened = RegOpenKeyEx(
                currentUser,
                _testRegistrySubKey,
                0,
                0x0001,
                out IntPtr key);
            if (opened != 0)
            {
                return false;
            }

            RegCloseKey(key);
            return true;
        }


        private static bool ApprovalRegistryValueExists(string valueName)
        {
            IntPtr currentUser = new IntPtr(unchecked((int)0x80000001));
            int opened = RegOpenKeyEx(
                currentUser,
                _testRegistrySubKey,
                0,
                0x0001,
                out IntPtr key);
            if (opened != 0)
            {
                return false;
            }

            try
            {
                uint length = 0;
                int queried = RegQueryValueEx(
                    key,
                    valueName,
                    IntPtr.Zero,
                    out _,
                    null,
                    ref length);
                return queried == 0 || queried == 234;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(
            IntPtr key,
            string subKey,
            uint options,
            uint desiredAccess,
            out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(
            IntPtr key,
            string valueName,
            IntPtr reserved,
            out uint valueType,
            byte[] data,
            ref uint dataLength);


        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr key);

        private static IReadOnlyDictionary<string, byte[]> Snapshot(string root) =>
            Directory.GetFiles(root)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal);

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

        private static bool IsSameOrDescendant(string candidate, string parent)
        {
            string normalizedCandidate = Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedParent = Path.GetFullPath(parent)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase) ||
                   normalizedCandidate.StartsWith(
                       normalizedParent + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static Type OfflineStackType => typeof(Bootloader).Assembly.GetType(
            "AL.Core.OfflineServiceStack",
            throwOnError: true);

        private static FieldInfo SaveFactoryField => OfflineStackType.GetField(
            "SaveGameFactoryOverride",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static object GetSaveFactory() => SaveFactoryField.GetValue(null);

        private static void SetSaveFactory(object factory) => SaveFactoryField.SetValue(null, factory);

        private static void SetBeforeDeleteHook(Action hook)
        {
            FieldInfo field = typeof(MvpApprovalSlotRuntime).GetField(
                "BeforeAuthorizedDeleteForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, hook);
        }

        private static void SetBeforeDeleteArtifactsHook(Action hook)
        {
            FieldInfo field = typeof(LocalSaveGameService).GetField(
                "BeforeDeleteArtifactsForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, hook);
        }

        private static void SetBeforeFreshLoadHook(Action hook)
        {
            FieldInfo field = typeof(MvpApprovalSlotRuntime).GetField(
                "BeforeFreshLoadForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, hook);
        }

        private static void SetAfterArtifactValidationHook(Action<string> hook)
        {
            Type deletionType = RuntimeType(
                "AL.Services.Local.WindowsOwnedArtifactDeletion");
            FieldInfo field = deletionType.GetField(
                "AfterArtifactValidationForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(null, hook);
        }

        private static object CreateFailedStackMarker(ISaveGameService saveService)
        {
            Type markerType = RuntimeType("AL.Core.LocalOfflineServiceStackMarker");
            var expected = new Dictionary<Type, object>
            {
                { typeof(ISaveGameService), saveService }
            };
            object marker = Activator.CreateInstance(
                markerType,
                1,
                "mvp-approval-failed-load",
                expected,
                saveService,
                null);
            Invoke(marker, "TryClaimRuntimeOwner", "mvp-approval-test-owner");
            Invoke(marker, "TryBeginLoad", "mvp-approval-test-owner");
            Invoke(marker, "MarkLoadFailed", "mvp-approval-test-owner");
            return marker;
        }

        private static void Invoke(object target, string method, string value)
        {
            MethodInfo info = target.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, new object[] { value });
        }

        private static Type RuntimeType(string name) =>
            typeof(Bootloader).Assembly.GetType(name, throwOnError: true);

        private static void RegisterService(Type contract, object service)
        {
            MethodInfo register = typeof(ServiceLocator).GetMethod(
                "Register",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(register, Is.Not.Null);
            register.MakeGenericMethod(contract).Invoke(null, new[] { service });
        }

        private static void RemoveService(Type contract)
        {
            FieldInfo field = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var services = (IDictionary<Type, object>)field.GetValue(null);
            services.Remove(contract);
        }

        private sealed class RollbackReadFailureFileOperations : ISaveFileOperations
        {
            private readonly ISaveFileOperations _inner;

            internal RollbackReadFailureFileOperations(ISaveFileOperations inner)
            {
                _inner = inner;
            }

            internal bool FailReads { get; set; }

            public bool FileExists(string path) => _inner.FileExists(path);

            public void CreateDirectory(string path) => _inner.CreateDirectory(path);

            public SaveFileReadResult ReadAllBytesBounded(
                string path,
                int maximumBytes) =>
                FailReads
                    ? new SaveFileReadResult(
                        SaveFileReadDisposition.IoFailure,
                        null,
                        0,
                        "SAVE_FILE_IO_FAILURE")
                    : _inner.ReadAllBytesBounded(path, maximumBytes);

            public SaveFileWriteResult WriteAllTextDurable(
                string path,
                string contents) =>
                _inner.WriteAllTextDurable(path, contents);

            public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);

            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);

            public void Replace(
                string sourcePath,
                string destinationPath,
                string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);

            public void Delete(string path) => _inner.Delete(path);

            public IEnumerable<string> EnumerateFiles(
                string directoryPath,
                string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);

            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);

            public bool IsReparsePoint(string path) => _inner.IsReparsePoint(path);
        }

        private static void SetInnerCommitUncertain(LocalSaveGameService service)
        {
            FieldInfo status = typeof(LocalSaveGameService).GetField(
                "<LastSaveStatus>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo message = typeof(LocalSaveGameService).GetField(
                "<LastSaveMessage>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo disposition = typeof(LocalSaveGameService).GetField(
                "<LastSaveDisposition>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(status, Is.Not.Null);
            Assert.That(message, Is.Not.Null);
            Assert.That(disposition, Is.Not.Null);
            status.SetValue(service, SaveOperationStatus.CommitUncertain);
            message.SetValue(service, "INJECTED-INNER-COMMIT-UNCERTAIN");
            disposition.SetValue(
                service,
                new SaveOperationDisposition(
                    SaveOperationStatus.CommitUncertain,
                    mayHaveMutated: true,
                    candidatePrimaryVerified: false,
                    requiredBackupVerified: false,
                    previousAuthorityVerified: false,
                    cleanupVerified: false,
                    rollbackAttempted: false,
                    rollbackVerified: false,
                    diagnosticCodes: new[] { "INJECTED-INNER-COMMIT-UNCERTAIN" }));
        }

        private static void ResetRuntime()
        {
            MethodInfo reset = typeof(MvpApprovalSlotRuntime).GetMethod(
                "ResetForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            reset?.Invoke(null, null);
        }
    }
}
