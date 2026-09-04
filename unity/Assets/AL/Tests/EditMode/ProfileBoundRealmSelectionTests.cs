using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public sealed class ProfileBoundRealmSelectionTests
    {
        private readonly List<RealmDefinition> _createdRealms = new List<RealmDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (RealmDefinition realm in _createdRealms)
            {
                if (realm != null)
                {
                    UnityEngine.Object.DestroyImmediate(realm);
                }
            }

            _createdRealms.Clear();
        }

        [Test]
        public void FirstCommitPersistsReceiptOnceAndReplayIsIdempotent()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateUncommittedWritable(root);
                LocalRealmService realm = CreateRealmService(save);
                var request = new RealmSelectionRequest(
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    RealmId.Stonehold);

                RealmSelectionResult first = realm.TrySelectRealm(request);
                Assert.AreEqual(RealmSelectionStatus.Committed, first.Status);
                Assert.AreEqual("AL-REALM-COMMITTED", first.TechnicalCode);
                Assert.True(first.MutationOccurred);
                Assert.True(first.Persisted);
                Assert.AreEqual(RealmId.Stonehold, save.CurrentSave.SelectedRealm);
                AssertCommittedReceipt(
                    save.CurrentSave,
                    RealmId.Stonehold,
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    RealmSelectionAuthority.InitialProvenance);
                Assert.AreNotEqual(
                    string.Empty,
                    save.CurrentSave.RealmSelection.EventId);
                byte[] afterFirst = File.ReadAllBytes(Path.Combine(root, "save.json"));

                RealmSelectionResult replay = realm.TrySelectRealm(request);
                Assert.AreEqual(RealmSelectionStatus.AlreadyCommittedSameRealm, replay.Status);
                Assert.False(replay.MutationOccurred);
                Assert.False(replay.Persisted);
                Assert.AreEqual(RealmId.Stonehold, save.CurrentSave.SelectedRealm);
                CollectionAssert.AreEqual(
                    afterFirst,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));

                RealmSelectionResult sameRealm =
                    realm.TrySelectRealm(new RealmSelectionRequest(
                        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                        RealmId.Stonehold));
                Assert.AreEqual(
                    RealmSelectionStatus.AlreadyCommittedSameRealm,
                    sameRealm.Status);
                Assert.False(sameRealm.MutationOccurred);
                Assert.AreEqual(RealmId.Stonehold, save.CurrentSave.SelectedRealm);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void DifferentRealmNoneUndefinedAndCatalogMissMutateNothing()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateUncommittedWritable(root);
                LocalRealmService realm = CreateRealmService(save);
                RealmSelectionResult committed = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "cccccccccccccccccccccccccccccccc",
                        RealmId.Umbral));
                Assert.AreEqual(RealmSelectionStatus.Committed, committed.Status);
                string eventId = save.CurrentSave.RealmSelection.EventId;
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));

                RealmSelectionResult different = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "dddddddddddddddddddddddddddddddd",
                        RealmId.Crownlands));
                Assert.AreEqual(RealmSelectionStatus.RejectedDifferentRealm, different.Status);
                Assert.False(different.MutationOccurred);
                Assert.AreEqual(RealmId.Umbral, save.CurrentSave.SelectedRealm);
                Assert.AreEqual(eventId, save.CurrentSave.RealmSelection.EventId);

                RealmSelectionResult none = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                        RealmId.None));
                Assert.AreEqual(RealmSelectionStatus.InvalidRealm, none.Status);
                Assert.False(none.MutationOccurred);

                RealmSelectionResult undefined = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "ffffffffffffffffffffffffffffffff",
                        (RealmId)999));
                Assert.AreEqual(RealmSelectionStatus.InvalidRealm, undefined.Status);
                Assert.False(undefined.MutationOccurred);

                var missingCatalog = new LocalRealmService(
                    save,
                    new FakeGameDataService(
                        Realm(RealmId.Umbral)),
                    null);
                RealmSelectionResult missing = missingCatalog.TrySelectRealm(
                    new RealmSelectionRequest(
                        "11111111111111111111111111111111",
                        RealmId.Umbral));
                Assert.AreEqual(RealmSelectionStatus.RealmDefinitionUnavailable, missing.Status);
                Assert.False(missing.MutationOccurred);
                Assert.AreEqual(RealmId.Umbral, save.CurrentSave.SelectedRealm);
                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void WrongProfileStaleBaseAndCorrelationConflictMutateNothing()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateUncommittedWritable(root);
                LocalRealmService realm = CreateRealmService(save);
                string profileId = save.CurrentSave.ProfileId;
                string generation = save.GetCurrentAuthority().VerifiedGenerationFingerprint;
                realm.TrySelectRealm(new RealmSelectionRequest(
                    "22222222222222222222222222222222",
                    RealmId.Eldergrove,
                    "22222222222222222222222222222222",
                    profileId,
                    generation));
                RealmId beforeRealm = save.CurrentSave.SelectedRealm;
                byte[] before = File.ReadAllBytes(Path.Combine(root, "save.json"));

                RealmSelectionResult wrongProfile = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "33333333333333333333333333333333",
                        RealmId.Eldergrove,
                        "33333333333333333333333333333333",
                        "alp_ffffffffffffffffffffffffffffffff",
                        string.Empty));
                Assert.AreEqual(RealmSelectionStatus.ProfileUnavailable, wrongProfile.Status);
                Assert.AreEqual("AL-REALM-PROFILE-MISMATCH", wrongProfile.TechnicalCode);
                Assert.False(wrongProfile.MutationOccurred);

                RealmSelectionResult stale = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "44444444444444444444444444444444",
                        RealmId.Eldergrove,
                        "44444444444444444444444444444444",
                        profileId,
                        new string('0', 64)));
                Assert.AreEqual(RealmSelectionStatus.InvalidTransaction, stale.Status);
                Assert.AreEqual("AL-REALM-STALE-BASE", stale.TechnicalCode);
                Assert.False(stale.MutationOccurred);

                RealmSelectionResult conflict = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "55555555555555555555555555555555",
                        RealmId.Stonehold,
                        "22222222222222222222222222222222",
                        profileId,
                        string.Empty));
                Assert.AreEqual(RealmSelectionStatus.InvalidTransaction, conflict.Status);
                Assert.AreEqual("AL-REALM-CORRELATION-CONFLICT", conflict.TechnicalCode);
                Assert.False(conflict.MutationOccurred);
                Assert.AreEqual(beforeRealm, save.CurrentSave.SelectedRealm);
                CollectionAssert.AreEqual(
                    before,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void LegacySelectedRealmMigratesWithoutChangingValueOrSelectingCrownlands()
        {
            string root = CreateRoot();
            try
            {
                byte[] predecessor = File.ReadAllBytes(
                    Path.Combine(
                        Application.dataPath,
                        "AL",
                        "Tests",
                        "EditMode",
                        "Fixtures",
                        "SaveSchema1",
                        "current-schema-v1.json"));
                File.WriteAllBytes(Path.Combine(root, "save.json"), predecessor);
                LocalSaveGameService save = CreateService(root);
                save.Load();
                Assert.AreEqual(RealmId.Eldergrove, save.CurrentSave.SelectedRealm);
                Assert.True(IsAbsentAuthority(save.CurrentSave.RealmSelection));

                LocalRealmService realm = CreateRealmService(save);
                RealmSelectionResult crownlands = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "66666666666666666666666666666666",
                        RealmId.Crownlands));
                Assert.AreEqual(RealmSelectionStatus.RejectedDifferentRealm, crownlands.Status);
                Assert.False(crownlands.MutationOccurred);
                Assert.AreEqual(RealmId.Eldergrove, save.CurrentSave.SelectedRealm);
                Assert.True(IsAbsentAuthority(save.CurrentSave.RealmSelection));

                RealmSelectionResult migrate = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "77777777777777777777777777777777",
                        RealmId.Eldergrove));
                Assert.AreEqual(
                    RealmSelectionStatus.AlreadyCommittedSameRealm,
                    migrate.Status);
                Assert.AreEqual("AL-REALM-LEGACY-MIGRATED", migrate.TechnicalCode);
                Assert.True(migrate.MutationOccurred);
                Assert.AreEqual(RealmId.Eldergrove, save.CurrentSave.SelectedRealm);
                Assert.AreEqual(
                    RealmSelectionAuthority.LegacyMigrationProvenance,
                    save.CurrentSave.RealmSelection.Provenance);
                Assert.AreEqual(string.Empty, save.CurrentSave.RealmSelection.EventId);
                AssertCommittedReceipt(
                    save.CurrentSave,
                    RealmId.Eldergrove,
                    RealmSelectionAuthority.MigrationTransactionId(
                        save.CurrentSave.ProfileId,
                        RealmId.Eldergrove),
                    RealmSelectionAuthority.LegacyMigrationProvenance);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void PlayerReloadReconcilesDurableReceiptAndRejectsForwardWrites()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateUncommittedWritable(root);
                LocalRealmService realm = CreateRealmService(save);
                realm.TrySelectRealm(new RealmSelectionRequest(
                    "88888888888888888888888888888888",
                    RealmId.Stonehold));
                string profileId = save.CurrentSave.ProfileId;
                string transactionId = save.CurrentSave.RealmSelection.TransactionId;
                string fingerprint = save.CurrentSave.RealmSelection.ReceiptFingerprint;

                LocalSaveGameService restarted = CreateService(root);
                restarted.Load();
                Assert.AreEqual(RealmId.Stonehold, restarted.CurrentSave.SelectedRealm);
                Assert.AreEqual(profileId, restarted.CurrentSave.ProfileId);
                Assert.AreEqual(transactionId, restarted.CurrentSave.RealmSelection.TransactionId);
                Assert.AreEqual(
                    fingerprint,
                    restarted.CurrentSave.RealmSelection.ReceiptFingerprint);
                LocalRealmService restartedRealm = CreateRealmService(restarted);
                RealmSelectionResult replay = restartedRealm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "88888888888888888888888888888888",
                        RealmId.Stonehold));
                Assert.AreEqual(
                    RealmSelectionStatus.AlreadyCommittedSameRealm,
                    replay.Status);
                Assert.False(replay.MutationOccurred);

                SaveGameData future = JsonUtility.FromJson<SaveGameData>(
                    File.ReadAllText(Path.Combine(root, "save.json")));
                future.SaveSchemaVersion = 3;
                string forwardRoot = CreateRoot();
                try
                {
                    byte[] forwardBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(future, true));
                    File.WriteAllBytes(Path.Combine(forwardRoot, "save.json"), forwardBytes);
                    File.WriteAllBytes(Path.Combine(forwardRoot, "save.backup.json"), forwardBytes);
                    LocalSaveGameService forward = CreateService(forwardRoot);
                    forward.Load();
                    LocalRealmService forwardRealm = CreateRealmService(forward);
                    RealmSelectionResult denied = forwardRealm.TrySelectRealm(
                        new RealmSelectionRequest(
                            "99999999999999999999999999999999",
                            RealmId.Umbral));
                    Assert.AreEqual(RealmSelectionStatus.ProfileUnavailable, denied.Status);
                    Assert.False(denied.MutationOccurred);
                    Assert.AreNotEqual(RealmId.Crownlands, denied.CommittedRealmId);
                }
                finally
                {
                    DeleteRoot(forwardRoot);
                }
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SaveFailureAndCommitUncertaintyLeavePriorState()
        {
            string root = CreateRoot();
            try
            {
                var gated = new GatedSaveFileOperations();
                LocalSaveGameService save = CreateService(root, gated);
                save.Load();
                Assert.NotNull(save.CurrentSave);
                Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
                Assert.AreEqual(
                    ProfileWriteAuthorityStatus.Writable,
                    save.GetCurrentAuthority().Status);
                gated.FailDurableWrites = true;
                LocalRealmService realm = CreateRealmService(save);
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
                Assert.True(IsAbsentAuthority(save.CurrentSave.RealmSelection));
                Assert.AreNotEqual(RealmSelectionStatus.Committed, failed.Status);
                Assert.AreNotEqual(
                    RealmSelectionStatus.AlreadyCommittedSameRealm,
                    failed.Status);

                if (failed.Status != RealmSelectionStatus.CommitUncertain)
                {
                    gated.FailDurableWrites = false;
                    FieldInfo writable = typeof(LocalSaveGameService).GetField(
                        "_profileWritable",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo status = typeof(LocalSaveGameService).GetField(
                        "<LastSaveStatus>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.NotNull(writable);
                    writable.SetValue(save, false);
                    PropertyInfo lastSave = typeof(LocalSaveGameService).GetProperty(
                        "LastSaveStatus");
                    if (status != null)
                    {
                        status.SetValue(save, SaveOperationStatus.CommitUncertain);
                    }
                    else
                    {
                        MethodInfo setter = lastSave.GetSetMethod(true);
                        setter.Invoke(save, new object[] { SaveOperationStatus.CommitUncertain });
                    }
                }

                RealmSelectionResult uncertain = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd",
                        RealmId.Umbral));
                Assert.False(uncertain.MutationOccurred);
                Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
                Assert.AreNotEqual(RealmId.Crownlands, save.CurrentSave.SelectedRealm);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        private LocalRealmService CreateRealmService(ISaveGameService save)
        {
            string catalogPath = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult parsed = RealmCatalogRuntime.Parse(
                File.ReadAllText(catalogPath));
            Assert.True(parsed.IsSuccess, parsed.TechnicalCode);
            return new LocalRealmService(
                save,
                new FakeGameDataService(
                    Realm(RealmId.Crownlands),
                    Realm(RealmId.Stonehold),
                    Realm(RealmId.Eldergrove),
                    Realm(RealmId.Umbral)),
                parsed.Snapshot);
        }

        private RealmDefinition Realm(RealmId id)
        {
            RealmDefinition realm = ScriptableObject.CreateInstance<RealmDefinition>();
            realm.Id = id;
            realm.RealmName = id.ToString();
            _createdRealms.Add(realm);
            return realm;
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
            Assert.AreEqual(
                ProfileWriteAuthorityStatus.Writable,
                save.GetCurrentAuthority().Status);
            return save;
        }

        private static bool IsAbsentAuthority(RealmSelectionAuthorityState authority)
        {
            return authority == null ||
                   (!authority.Committed &&
                    authority.Version == 0 &&
                    authority.SelectedRealm == 0 &&
                    string.IsNullOrEmpty(authority.TransactionId));
        }

        private static void AssertCommittedReceipt(
            SaveGameData save,
            RealmId realm,
            string transactionId,
            string provenance)
        {
            Assert.NotNull(save.RealmSelection);
            Assert.True(save.RealmSelection.Committed);
            Assert.AreEqual((int)realm, save.RealmSelection.SelectedRealm);
            Assert.AreEqual(transactionId, save.RealmSelection.TransactionId);
            Assert.AreEqual(provenance, save.RealmSelection.Provenance);
            Assert.AreEqual(
                RealmSelectionAuthority.OperationId,
                save.RealmSelection.OperationId);
            Assert.AreEqual(
                RealmSelectionAuthority.ComputeReceiptFingerprint(
                    save.RealmSelection.ProfileId,
                    realm,
                    save.RealmSelection.TransactionId,
                    save.RealmSelection.CorrelationId,
                    save.RealmSelection.OperationId,
                    save.RealmSelection.EventId,
                    save.RealmSelection.Provenance,
                    save.RealmSelection.Revision),
                save.RealmSelection.ReceiptFingerprint);
        }

        private static LocalSaveGameService CreateService(string root)
        {
            return CreateService(root, new SystemSaveFileOperations());
        }

        private static LocalSaveGameService CreateService(
            string root,
            ISaveFileOperations fileOperations)
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
                CurrentChapterId = "C1",
                Resources = new List<ResourceData>
                {
                    new ResourceData { Type = ResourceType.Food, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Wood, Amount = 1000 },
                    new ResourceData { Type = ResourceType.Stone, Amount = 500 },
                    new ResourceData { Type = ResourceType.Gold, Amount = 500 },
                    new ResourceData { Type = ResourceType.ManaStone, Amount = 150 },
                    new ResourceData { Type = ResourceType.Ore, Amount = 150 }
                }
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(save, true));
            File.WriteAllBytes(Path.Combine(root, "save.json"), bytes);
            File.WriteAllBytes(Path.Combine(root, "save.backup.json"), bytes);
        }

        private static string CreateRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-ProfileBoundRealm",
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

        private sealed class FakeGameDataService : IGameDataService
        {
            private readonly Dictionary<RealmId, RealmDefinition> _realms =
                new Dictionary<RealmId, RealmDefinition>();

            public FakeGameDataService(params RealmDefinition[] realms)
            {
                foreach (RealmDefinition realm in realms)
                {
                    _realms[realm.Id] = realm;
                }
            }

            public RealmDefinition GetRealm(RealmId id)
            {
                return _realms.TryGetValue(id, out RealmDefinition realm) ? realm : null;
            }

            public IEnumerable<RealmDefinition> GetAllRealms() => _realms.Values;
            public BuildingDefinition GetBuilding(string id) => null;
            public TroopDefinition GetTroop(string id) => null;
            public ChampionDefinition GetChampion(string id) => null;
            public IEnumerable<ChampionDefinition> GetAllChampions() =>
                Enumerable.Empty<ChampionDefinition>();
            public SkillDefinition GetSkill(string id) => null;
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

            public DateTime GetCreationTimeUtc(string path) =>
                _inner.GetCreationTimeUtc(path);

            public bool IsReparsePoint(string path) => _inner.IsReparsePoint(path);
        }
    }
}
