using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmCatalogAndSelectionTests
    {
        private string _json;
        private RealmCatalogSnapshot _catalog;

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "AL", "StreamingAssets", "GameData", "al_realm_catalog.json");
            _json = File.ReadAllText(path);
            RealmCatalogLoadResult result = RealmCatalogRuntime.Parse(_json);
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            _catalog = result.Snapshot;
        }

        [Test]
        public void CanonicalCatalogPublishesExactlyFourStableRuntimeMappings()
        {
            Assert.That(_catalog.Version, Is.EqualTo("0.1.0"));
            Assert.That(_catalog.Realms, Has.Count.EqualTo(4));
            RealmCatalogEntry entry;
            Assert.That(_catalog.TryGet(RealmId.Crownlands, out entry), Is.True);
            Assert.That(entry.Id, Is.EqualTo("crownlands"));
            Assert.That(_catalog.TryGet(RealmId.Stonehold, out entry), Is.True);
            Assert.That(_catalog.TryGet(RealmId.Eldergrove, out entry), Is.True);
            Assert.That(_catalog.TryGet(RealmId.Umbral, out entry), Is.True);
        }

        [Test]
        public void ParserRejectsUnsupportedVersionPolicyAndDuplicateRealm()
        {
            Assert.That(RealmCatalogRuntime.Parse(_json.Replace("\"0.1.0\"", "\"9.0.0\"")).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-UNSUPPORTED"));
            Assert.That(RealmCatalogRuntime.Parse(_json.Replace("same_realm_only", "cross_realm")).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(RealmCatalogRuntime.Parse(_json.Replace("\"id\": \"stonehold\"", "\"id\": \"crownlands\"")).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-INVALID-REALM"));

            string swappedRuntimeIds = _json
                .Replace("\"legacyRuntimeId\": \"Crownlands\"", "\"legacyRuntimeId\": \"SwapTemporary\"")
                .Replace("\"legacyRuntimeId\": \"Umbral\"", "\"legacyRuntimeId\": \"Crownlands\"")
                .Replace("\"legacyRuntimeId\": \"SwapTemporary\"", "\"legacyRuntimeId\": \"Umbral\"");
            Assert.That(RealmCatalogRuntime.Parse(swappedRuntimeIds).TechnicalCode, Is.EqualTo("AL-REALM-CATALOG-INVALID-REALM"));
        }

        [Test]
        public void FirstCommitPersistsAndDifferentRealmIsRejectedWithoutSecondSave()
        {
            var save = new FakeSaveService();
            var service = new LocalRealmService(save, new FakeGameDataService(), _catalog);
            RealmSelectionResult first = service.TrySelectRealm(new RealmSelectionRequest("tx_first", RealmId.Stonehold));
            RealmSelectionResult repeated = service.TrySelectRealm(new RealmSelectionRequest("tx_same", RealmId.Stonehold));
            RealmSelectionResult different = service.TrySelectRealm(new RealmSelectionRequest("tx_other", RealmId.Umbral));
            Assert.That(first.Status, Is.EqualTo(RealmSelectionStatus.Committed));
            Assert.That(repeated.Status, Is.EqualTo(RealmSelectionStatus.AlreadyCommittedSameRealm));
            Assert.That(different.Status, Is.EqualTo(RealmSelectionStatus.RejectedDifferentRealm));
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.Stonehold));
            Assert.That(save.SaveCount, Is.EqualTo(1));
            Assert.That(service.Identity.Status, Is.EqualTo(RealmIdentityStatus.CommittedValid));
        }

        [Test]
        public void InvalidRequestsAndSaveFailureNeverBecomeAuthoritative()
        {
            var save = new FakeSaveService { FailSave = true };
            var service = new LocalRealmService(save, new FakeGameDataService(), _catalog);
            Assert.That(service.TrySelectRealm(new RealmSelectionRequest("", RealmId.Crownlands)).Status, Is.EqualTo(RealmSelectionStatus.InvalidTransaction));
            Assert.That(service.TrySelectRealm(new RealmSelectionRequest("tx_none", RealmId.None)).Status, Is.EqualTo(RealmSelectionStatus.InvalidRealm));
            RealmSelectionResult failed = service.TrySelectRealm(new RealmSelectionRequest("tx_fail", RealmId.Eldergrove));
            Assert.That(failed.Status, Is.EqualTo(RealmSelectionStatus.SaveFailedPreviousPreserved));
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
            Assert.That(service.Identity.Status, Is.EqualTo(RealmIdentityStatus.Uncommitted));
        }

        [Test]
        public void SubCharactersMustMatchCommittedAccountRealm()
        {
            var committed = new RealmIdentitySnapshot(RealmIdentityStatus.CommittedValid, RealmId.Umbral, "0.1.0", "test");
            var uncommitted = new RealmIdentitySnapshot(RealmIdentityStatus.Uncommitted, RealmId.None, "0.1.0", "test");
            Assert.That(RealmCharacterConstraint.Evaluate(committed, RealmId.Umbral), Is.EqualTo(RealmCharacterEligibility.Allowed));
            Assert.That(RealmCharacterConstraint.Evaluate(committed, RealmId.Crownlands), Is.EqualTo(RealmCharacterEligibility.RejectedDifferentRealm));
            Assert.That(RealmCharacterConstraint.Evaluate(uncommitted, RealmId.Umbral), Is.EqualTo(RealmCharacterEligibility.AccountRealmUnavailable));
        }

        private sealed class FakeSaveService : ISaveGameService
        {
            public SaveGameData CurrentSave { get; private set; } = NewSave();
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage => string.Empty;
            public int SaveCount { get; private set; }
            public bool FailSave { get; set; }
            public void Save() { SaveCount++; LastSaveStatus = FailSave ? SaveOperationStatus.SaveFailedPreviousPreserved : SaveOperationStatus.SavedPrimary; }
            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) { CurrentSave = NewSave(); CurrentSave.SelectedRealm = realmId; Save(); }
            public void DeleteSave() { CurrentSave = null; }
            private static SaveGameData NewSave() => new SaveGameData { SaveFormatId = SaveGameData.CurrentSaveFormatId, SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion, ProfileInitializationVersion = SaveGameData.CurrentProfileInitializationVersion, SelectedRealm = RealmId.None };
        }

        private sealed class FakeGameDataService : IGameDataService
        {
            public RealmDefinition GetRealm(RealmId id) => null;
            public IEnumerable<RealmDefinition> GetAllRealms() => new RealmDefinition[0];
            public BuildingDefinition GetBuilding(string id) => null;
            public TroopDefinition GetTroop(string id) => null;
            public ChampionDefinition GetChampion(string id) => null;
            public SkillDefinition GetSkill(string id) => null;
        }
    }
}
