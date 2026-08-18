using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public class RealmSelectionIntegrityTests
    {
        private const string ValidCatalogJson = @"{
  ""version"": ""0.1.0"",
  ""catalogId"": ""al_realm_catalog"",
  ""selectionPolicy"": {
    ""selectionMode"": ""one_realm_per_account"",
    ""realmLockScope"": ""account"",
    ""subCharacterPolicy"": ""same_realm_only"",
    ""sharedStoragePolicy"": ""same_realm_account_storage"",
    ""crossRealmCreationPolicy"": ""reject"",
    ""realmChangePolicy"": ""not_supported_after_commit"",
    ""uncommittedProfileState"": ""realm_unselected"",
    ""committedProfileState"": ""realm_locked""
  },
  ""realmOrder"": [""crownlands"", ""stonehold"", ""eldergrove"", ""umbral""],
  ""realms"": [
    { ""id"": ""crownlands"", ""legacyRuntimeId"": ""Crownlands"", ""displayName"": ""Crownlands"", ""realmGemIds"": [""gem_crownlands_sun"", ""gem_crownlands_oath""] },
    { ""id"": ""stonehold"", ""legacyRuntimeId"": ""Stonehold"", ""displayName"": ""Stonehold"", ""realmGemIds"": [""gem_stonehold_forge"", ""gem_stonehold_depth""] },
    { ""id"": ""eldergrove"", ""legacyRuntimeId"": ""Eldergrove"", ""displayName"": ""Eldergrove"", ""realmGemIds"": [""gem_eldergrove_root"", ""gem_eldergrove_moon""] },
    { ""id"": ""umbral"", ""legacyRuntimeId"": ""Umbral"", ""displayName"": ""Umbral"", ""realmGemIds"": [""gem_umbral_veil"", ""gem_umbral_ember""] }
  ]
}";

        private readonly List<RealmDefinition> _createdRealms = new List<RealmDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (RealmDefinition realm in _createdRealms)
            {
                if (realm != null)
                {
                    Object.DestroyImmediate(realm);
                }
            }

            _createdRealms.Clear();
        }

        [Test]
        public void CatalogParsesOneAccountRealmLockPolicy()
        {
            RealmCatalogLoadResult result = RealmCatalogRuntime.Parse(ValidCatalogJson);

            Assert.True(result.IsSuccess);
            Assert.AreEqual("AL-REALM-CATALOG-READY", result.TechnicalCode);
            Assert.AreEqual(4, result.Snapshot.Realms.Count);
            Assert.True(result.Snapshot.TryGet(RealmId.Crownlands, out RealmCatalogEntry crownlands));
            Assert.AreEqual("crownlands", crownlands.Id);
        }

        [TestCase(null, RealmId.Crownlands, "InvalidTransaction", "AL-REALM-TRANSACTION-INVALID")]
        [TestCase("", RealmId.Crownlands, "InvalidTransaction", "AL-REALM-TRANSACTION-INVALID")]
        [TestCase("tx", RealmId.None, "InvalidRealm", "AL-REALM-REQUEST-INVALID")]
        [TestCase("tx", (RealmId)999, "InvalidRealm", "AL-REALM-REQUEST-INVALID")]
        public void TrySelectRealmRejectsInvalidRequests(
            string transactionId,
            RealmId requestedRealm,
            string expectedStatus,
            string expectedCode)
        {
            var save = new FakeSaveGameService();
            var service = Service(save);

            RealmSelectionResult result = service.TrySelectRealm(new RealmSelectionRequest(transactionId, requestedRealm));

            Assert.AreEqual(expectedStatus, result.Status.ToString());
            Assert.AreEqual(expectedCode, result.TechnicalCode);
            Assert.False(result.MutationOccurred);
            Assert.False(result.AllowsNavigation);
            Assert.AreEqual(0, save.SaveCallCount);
            Assert.AreEqual(0, save.CreateCallCount);
        }

        [Test]
        public void UntypedSaveServiceCannotEnterSchemaOneRealmCoordinator()
        {
            var save = new FakeSaveGameService();
            var service = Service(save);

            RealmSelectionResult first = service.TrySelectRealm(new RealmSelectionRequest("tx-a", RealmId.Eldergrove));
            RealmSelectionResult duplicate = service.TrySelectRealm(new RealmSelectionRequest("tx-b", RealmId.Eldergrove));

            Assert.AreEqual(RealmSelectionStatus.ProfileUnavailable, first.Status);
            Assert.AreEqual(RealmSelectionStatus.ProfileUnavailable, duplicate.Status);
            Assert.False(first.AllowsNavigation);
            Assert.False(duplicate.AllowsNavigation);
            Assert.IsNull(save.CurrentSave);
            Assert.AreEqual(RealmId.None, service.CurrentRealmId);
            Assert.AreEqual(0, save.CreateCallCount);
            Assert.AreEqual(0, save.SaveCallCount);
        }

        [Test]
        public void DifferentRealmAfterCommitIsRejectedWithoutMutation()
        {
            var save = new FakeSaveGameService
            {
                CurrentSave = new SaveGameData { SelectedRealm = RealmId.Stonehold }
            };
            var service = Service(save);

            RealmSelectionResult result = service.TrySelectRealm(new RealmSelectionRequest("tx", RealmId.Crownlands));

            Assert.AreEqual(RealmSelectionStatus.ProfileUnavailable, result.Status);
            Assert.AreEqual("AL-REALM-TYPED-CANDIDATE-STORE-UNAVAILABLE", result.TechnicalCode);
            Assert.False(result.MutationOccurred);
            Assert.False(result.Persisted);
            Assert.AreEqual(RealmId.Stonehold, save.CurrentSave.SelectedRealm);
            Assert.AreEqual(0, save.SaveCallCount);
        }

        [Test]
        public void UntypedFailingSaveCannotBeUsedAsRealmTransactionFallback()
        {
            var save = new FakeSaveGameService
            {
                CurrentSave = new SaveGameData { SelectedRealm = RealmId.None },
                NextSaveStatus = SaveOperationStatus.SaveFailedPreviousPreserved
            };
            var service = Service(save);

            RealmSelectionResult result = service.TrySelectRealm(new RealmSelectionRequest("tx", RealmId.Umbral));

            Assert.AreEqual(RealmSelectionStatus.ProfileUnavailable, result.Status);
            Assert.AreEqual("AL-REALM-TYPED-CANDIDATE-STORE-UNAVAILABLE", result.TechnicalCode);
            Assert.False(result.AllowsNavigation);
            Assert.AreEqual(RealmId.None, save.CurrentSave.SelectedRealm);
            Assert.AreEqual(RealmId.None, service.CurrentRealmId);
            Assert.AreEqual(0, save.SaveCallCount);
        }

        [Test]
        public void IdentityDoesNotExposeUnresolvedPersistedRealmAsCurrentRealm()
        {
            var save = new FakeSaveGameService
            {
                CurrentSave = new SaveGameData { SelectedRealm = RealmId.Crownlands }
            };
            var service = new LocalRealmService(
                save,
                new FakeGameDataService(Realm(RealmId.Crownlands)),
                null);

            Assert.AreEqual(RealmIdentityStatus.CatalogUnavailable, service.Identity.Status);
            Assert.AreEqual(RealmId.None, service.CurrentRealmId);
            Assert.IsNull(service.CurrentRealm);
            Assert.AreEqual(RealmId.Crownlands, save.CurrentSave.SelectedRealm);
        }

        [Test]
        public void SubCharactersMustMatchCommittedAccountRealm()
        {
            var identity = new RealmIdentitySnapshot(
                RealmIdentityStatus.CommittedValid,
                RealmId.Umbral,
                RealmCatalogRuntime.SupportedVersion,
                "AL-REALM-COMMITTED-VALID");

            Assert.AreEqual(
                RealmCharacterEligibility.Allowed,
                RealmCharacterConstraint.Evaluate(identity, RealmId.Umbral));
            Assert.AreEqual(
                RealmCharacterEligibility.RejectedDifferentRealm,
                RealmCharacterConstraint.Evaluate(identity, RealmId.Crownlands));
            Assert.AreEqual(
                RealmCharacterEligibility.InvalidRequestedRealm,
                RealmCharacterConstraint.Evaluate(identity, RealmId.None));
        }

        private LocalRealmService Service(FakeSaveGameService save)
        {
            return new LocalRealmService(
                save,
                new FakeGameDataService(
                    Realm(RealmId.Crownlands),
                    Realm(RealmId.Stonehold),
                    Realm(RealmId.Eldergrove),
                    Realm(RealmId.Umbral)),
                RealmCatalogRuntime.Parse(ValidCatalogJson).Snapshot);
        }

        private RealmDefinition Realm(RealmId id)
        {
            RealmDefinition realm = ScriptableObject.CreateInstance<RealmDefinition>();
            realm.Id = id;
            realm.RealmName = id.ToString();
            _createdRealms.Add(realm);
            return realm;
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

            public IEnumerable<RealmDefinition> GetAllRealms()
            {
                return _realms.Values;
            }

            public BuildingDefinition GetBuilding(string id) => null;
            public TroopDefinition GetTroop(string id) => null;
            public ChampionDefinition GetChampion(string id) => null;
            public IEnumerable<ChampionDefinition> GetAllChampions() =>
                System.Linq.Enumerable.Empty<ChampionDefinition>();
            public SkillDefinition GetSkill(string id) => null;
        }

        private sealed class FakeSaveGameService : ISaveGameService
        {
            public SaveGameData CurrentSave { get; set; }
            public SaveLoadStatus LastLoadStatus { get; private set; }
            public string LastLoadMessage { get; private set; } = string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage { get; private set; } = string.Empty;
            public SaveOperationStatus NextSaveStatus { get; set; } = SaveOperationStatus.SavedPrimary;
            public int SaveCallCount { get; private set; }
            public int CreateCallCount { get; private set; }

            public void Save()
            {
                SaveCallCount++;
                LastSaveStatus = NextSaveStatus;
            }

            public void Load()
            {
                LastLoadStatus = SaveLoadStatus.LoadedPrimary;
            }

            public bool HasSave()
            {
                return CurrentSave != null;
            }

            public void CreateNewSave(RealmId realmId)
            {
                CreateCallCount++;
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
                Save();
            }

            public void DeleteSave()
            {
                CurrentSave = null;
            }
        }
    }
}
