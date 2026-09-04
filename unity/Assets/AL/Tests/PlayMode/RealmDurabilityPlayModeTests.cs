using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class RealmDurabilityPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            RealmSelectionEventDelivery.ResetSubscribers();
        }

        [TearDown]
        public void TearDown()
        {
            RealmSelectionEventDelivery.ResetSubscribers();
        }

        [UnityTest]
        public IEnumerator FirstCommitThenReloadKeepsReceiptAndRejectsCrownlands()
        {
            string root = CreateRoot();
            try
            {
                RealmCatalogSnapshot catalog = LoadCatalog();
                LocalSaveGameService save = CreateUncommittedWritable(root);
                var realm = new LocalRealmService(save, null, catalog);
                RealmSelectionResult first = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        RealmDurabilityPlayerAcceptance.CommitTransactionId,
                        RealmId.Stonehold));
                Assert.AreEqual(RealmSelectionStatus.Committed, first.Status);
                Assert.AreEqual(RealmId.Stonehold, save.CurrentSave.SelectedRealm);
                string transactionId = save.CurrentSave.RealmSelection.TransactionId;
                string fingerprint = save.CurrentSave.RealmSelection.ReceiptFingerprint;
                yield return null;

                LocalSaveGameService restarted = CreateService(root);
                restarted.Load();
                Assert.AreEqual(RealmId.Stonehold, restarted.CurrentSave.SelectedRealm);
                Assert.AreEqual(transactionId, restarted.CurrentSave.RealmSelection.TransactionId);
                Assert.AreEqual(fingerprint, restarted.CurrentSave.RealmSelection.ReceiptFingerprint);
                Assert.AreNotEqual(RealmId.Crownlands, restarted.CurrentSave.SelectedRealm);

                var restartedRealm = new LocalRealmService(restarted, null, catalog);
                Assert.True(restartedRealm.Identity.IsCommittedValid);
                Assert.AreEqual(RealmId.Stonehold, restartedRealm.CurrentRealmId);
                RealmSelectionResult rejected = restartedRealm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                        RealmId.Crownlands));
                Assert.AreEqual(RealmSelectionStatus.RejectedDifferentRealm, rejected.Status);
                Assert.False(rejected.MutationOccurred);
                Assert.AreEqual(RealmId.Stonehold, restarted.CurrentSave.SelectedRealm);
                yield return null;
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [UnityTest]
        public IEnumerator DuplicateReplayAfterReloadIsIdempotent()
        {
            string root = CreateRoot();
            try
            {
                RealmCatalogSnapshot catalog = LoadCatalog();
                LocalSaveGameService save = CreateUncommittedWritable(root);
                var realm = new LocalRealmService(save, null, catalog);
                realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        RealmDurabilityPlayerAcceptance.CommitTransactionId,
                        RealmId.Stonehold));
                byte[] afterFirst = File.ReadAllBytes(Path.Combine(root, "save.json"));
                yield return null;

                LocalSaveGameService restarted = CreateService(root);
                restarted.Load();
                var restartedRealm = new LocalRealmService(restarted, null, catalog);
                RealmSelectionResult replay = restartedRealm.TrySelectRealm(
                    new RealmSelectionRequest(
                        RealmDurabilityPlayerAcceptance.CommitTransactionId,
                        RealmId.Stonehold));
                Assert.AreEqual(RealmSelectionStatus.AlreadyCommittedSameRealm, replay.Status);
                Assert.False(replay.MutationOccurred);
                Assert.False(replay.Persisted);
                CollectionAssert.AreEqual(
                    afterFirst,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));

                RealmSelectionResult sameRealm = restartedRealm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "cccccccccccccccccccccccccccccccc",
                        RealmId.Stonehold));
                Assert.AreEqual(RealmSelectionStatus.AlreadyCommittedSameRealm, sameRealm.Status);
                Assert.False(sameRealm.MutationOccurred);
                CollectionAssert.AreEqual(
                    afterFirst,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
                yield return null;
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [UnityTest]
        public IEnumerator NvsEligibilityRequiresReceiptOnReloadedProfile()
        {
            string root = CreateRoot();
            try
            {
                RealmCatalogSnapshot catalog = LoadCatalog();
                LocalSaveGameService save = CreateUncommittedWritable(root);
                var uncommitted = new LocalRealmService(save, null, catalog);
                Nvs01RealmContext preview = Nvs01RealmContextAdapter.FromPersistedIdentity(
                    uncommitted.Identity,
                    save.CurrentSave.RealmSelection,
                    catalog);
                Assert.False(preview.IsCommittedValid);

                uncommitted.TrySelectRealm(
                    new RealmSelectionRequest(
                        RealmDurabilityPlayerAcceptance.CommitTransactionId,
                        RealmId.Stonehold));
                yield return null;

                LocalSaveGameService restarted = CreateService(root);
                restarted.Load();
                var restartedRealm = new LocalRealmService(restarted, null, catalog);
                Nvs01RealmContext valid = Nvs01RealmContextAdapter.FromPersistedIdentity(
                    restartedRealm.Identity,
                    restarted.CurrentSave.RealmSelection,
                    catalog);
                Assert.True(valid.IsCommittedValid);
                Assert.AreEqual("stonehold", valid.RealmId);

                Nvs01RealmContext mismatch = Nvs01RealmContextAdapter.FromPersistedIdentity(
                    restartedRealm.Identity,
                    null,
                    catalog);
                Assert.False(mismatch.IsCommittedValid);
                yield return null;
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [UnityTest]
        public IEnumerator FaultMatrixLeavesPriorUncommittedState()
        {
            string root = CreateRoot();
            try
            {
                RealmCatalogSnapshot catalog = LoadCatalog();
                LocalSaveGameService save = CreateUncommittedWritable(root);
                var realm = new LocalRealmService(save, null, catalog);
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));

                RealmSelectionResult none = realm.TrySelectRealm(
                    new RealmSelectionRequest("dddddddddddddddddddddddddddddddd", RealmId.None));
                Assert.AreEqual(RealmSelectionStatus.InvalidRealm, none.Status);
                Assert.False(none.MutationOccurred);

                RealmSelectionResult undefined = realm.TrySelectRealm(
                    new RealmSelectionRequest("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", (RealmId)999));
                Assert.AreEqual(RealmSelectionStatus.InvalidRealm, undefined.Status);
                Assert.False(undefined.MutationOccurred);
                Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
                Assert.AreNotEqual(RealmId.Crownlands, save.CurrentSave.SelectedRealm);
                CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(root, "save.json")));
                yield return null;
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [UnityTest]
        public IEnumerator SaveFailureDoesNotCommitOrSubstituteCrownlands()
        {
            string root = CreateRoot();
            try
            {
                RealmCatalogSnapshot catalog = LoadCatalog();
                var gated = new GatedSaveFileOperations();
                LocalSaveGameService save = CreateService(root, gated);
                save.Load();
                Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
                gated.FailDurableWrites = true;
                var realm = new LocalRealmService(save, null, catalog);
                bool priorIgnore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                RealmSelectionResult failed;
                try
                {
                    failed = realm.TrySelectRealm(
                        new RealmSelectionRequest(
                            "abababababababababababababababab",
                            RealmId.Stonehold));
                }
                finally
                {
                    LogAssert.ignoreFailingMessages = priorIgnore;
                }

                Assert.False(failed.MutationOccurred);
                Assert.False(failed.Persisted);
                Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
                Assert.AreNotEqual(RealmId.Crownlands, save.CurrentSave.SelectedRealm);
                Assert.AreNotEqual(RealmSelectionStatus.Committed, failed.Status);
                yield return null;
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [UnityTest]
        public IEnumerator DuplicateEventMatrixIsolatesSubscriberFailure()
        {
            string root = CreateRoot();
            try
            {
                RealmCatalogSnapshot catalog = LoadCatalog();
                LocalSaveGameService save = CreateUncommittedWritable(root);
                var realm = new LocalRealmService(save, null, catalog);
                var calls = new List<string>();
                RealmSelectionEventDelivery.Committed += evt =>
                {
                    calls.Add("throw:" + evt.NewRealmId);
                    throw new InvalidOperationException("subscriber");
                };
                RealmSelectionEventDelivery.Committed += evt =>
                {
                    calls.Add("later:" + evt.NewRealmId);
                };

                LogAssert.Expect(LogType.Warning, new Regex("AL-REALM-EVENT-HANDLER"));
                RealmSelectionResult first = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        RealmDurabilityPlayerAcceptance.CommitTransactionId,
                        RealmId.Stonehold));
                Assert.AreEqual(RealmSelectionStatus.Committed, first.Status);
                Assert.AreEqual(2, calls.Count);
                yield return null;

                RealmSelectionResult replay = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        RealmDurabilityPlayerAcceptance.CommitTransactionId,
                        RealmId.Stonehold));
                Assert.AreEqual(RealmSelectionStatus.AlreadyCommittedSameRealm, replay.Status);
                Assert.AreEqual(2, calls.Count);

                RealmSelectionResult different = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                        RealmId.Crownlands));
                Assert.AreEqual(RealmSelectionStatus.RejectedDifferentRealm, different.Status);
                Assert.AreEqual(2, calls.Count);
                Assert.AreEqual(RealmId.Stonehold, save.CurrentSave.SelectedRealm);
                yield return null;
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [UnityTest]
        public IEnumerator PlayerAcceptanceLifecycleEmitsPassedMarkerWithoutCrownlands()
        {
            string root = CreateRoot();
            try
            {
                yield return null;
                RealmDurabilityAcceptanceResult result = RealmDurabilityPlayerAcceptance.Run(
                    root,
                    RealmDurabilityPlayerAcceptance.LifecyclePhase);
                Assert.True(result.Passed, result.TechnicalCode);
                Assert.AreEqual(RealmDurabilityPlayerAcceptance.PassedMarker, result.Marker);
                Assert.AreEqual(RealmId.Stonehold, result.CommittedRealmId);
                Assert.AreNotEqual(RealmId.Crownlands, result.CommittedRealmId);
                Assert.True(result.NvsEligible);

                RealmDurabilityAcceptanceResult reloaded = RealmDurabilityPlayerAcceptance.Run(
                    root,
                    RealmDurabilityPlayerAcceptance.ReloadPhase);
                Assert.True(reloaded.Passed, reloaded.TechnicalCode);
                Assert.AreEqual(RealmId.Stonehold, reloaded.CommittedRealmId);
                yield return null;
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private static RealmCatalogSnapshot LoadCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult parsed = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.True(parsed.IsSuccess, parsed.TechnicalCode);
            return parsed.Snapshot;
        }

        private static LocalSaveGameService CreateUncommittedWritable(string root)
        {
            LocalSaveGameService save = CreateService(root);
            save.Load();
            if (save.CurrentSave != null &&
                save.CurrentSave.SelectedRealm == RealmId.None &&
                save.GetCurrentAuthority().Status == ProfileWriteAuthorityStatus.Writable)
            {
                return save;
            }

            WriteSchemaTwo(root, RealmId.None, "alp_0123456789abcdef0123456789abcdef");
            save = CreateService(root);
            save.Load();
            Assert.NotNull(save.CurrentSave);
            Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
            return save;
        }

        private static LocalSaveGameService CreateService(string root)
        {
            return CreateService(root, new SystemSaveFileOperations());
        }

        private static LocalSaveGameService CreateService(string root, ISaveFileOperations fileOperations)
        {
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(ISaveFileOperations) },
                null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(new object[] { root, fileOperations });
        }

        private static void WriteSchemaTwo(string root, RealmId realm, string profileId)
        {
            var save = new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = 2,
                ProfileInitializationVersion = 1,
                ProfileId = profileId,
                SelectedRealm = realm,
                CurrentChapterId = "C1"
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));
            File.WriteAllBytes(Path.Combine(root, "save.json"), bytes);
            File.WriteAllBytes(Path.Combine(root, "save.backup.json"), bytes);
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-RealmDurabilityPlayMode",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private sealed class GatedSaveFileOperations : ISaveFileOperations
        {
            private readonly SystemSaveFileOperations _inner = new SystemSaveFileOperations();
            public bool FailDurableWrites;

            public bool FileExists(string path) => _inner.FileExists(path);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public SaveFileReadResult ReadAllBytesBounded(string path, int maximumBytes) =>
                _inner.ReadAllBytesBounded(path, maximumBytes);

            public SaveFileWriteResult WriteAllTextDurable(string path, string contents)
            {
                if (FailDurableWrites)
                {
                    return new SaveFileWriteResult(false, false, "AL-TEST-WRITE-FAILED");
                }

                return _inner.WriteAllTextDurable(path, contents);
            }

            public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
                _inner.Copy(sourcePath, destinationPath, overwrite);

            public void Move(string sourcePath, string destinationPath) =>
                _inner.Move(sourcePath, destinationPath);

            public void Replace(string sourcePath, string destinationPath, string backupPath) =>
                _inner.Replace(sourcePath, destinationPath, backupPath);

            public void Delete(string path) => _inner.Delete(path);

            public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern) =>
                _inner.EnumerateFiles(directoryPath, searchPattern);

            public DateTime GetCreationTimeUtc(string path) => _inner.GetCreationTimeUtc(path);

            public bool IsReparsePoint(string path) => _inner.IsReparsePoint(path);
        }
    }
}
