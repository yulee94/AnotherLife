using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmCatalogAndSelectionTests
    {
        private string _json;
        private RealmCatalogSnapshot _catalog;
        private readonly List<RealmDefinition> _definitions = new List<RealmDefinition>(4);

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "AL", "StreamingAssets", "GameData", "al_realm_catalog.json");
            _json = File.ReadAllText(path);
            RealmCatalogLoadResult result = RealmCatalogRuntime.Parse(_json);
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            _catalog = result.Snapshot;
            _definitions.Add(CreateDefinition(RealmId.Crownlands));
            _definitions.Add(CreateDefinition(RealmId.Stonehold));
            _definitions.Add(CreateDefinition(RealmId.Eldergrove));
            _definitions.Add(CreateDefinition(RealmId.Umbral));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _definitions.Count; i++)
                Object.DestroyImmediate(_definitions[i]);
            _definitions.Clear();
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
        public void ParserRejectsContradictoryLockPolicyAndInvalidGemIdentifiers()
        {
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("not_supported_after_commit", "allow_after_commit")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("same_realm_account_storage", "cross_realm_storage")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("realm_unselected", "realm_locked")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-POLICY-MISMATCH"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("gem_stonehold_forge", "gem_crownlands_sun")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-INVALID-REALM"));
            Assert.That(
                RealmCatalogRuntime.Parse(_json.Replace("gem_umbral_ember", "Gem_Umbral_Ember")).TechnicalCode,
                Is.EqualTo("AL-REALM-CATALOG-INVALID-REALM"));
        }

        [Test]
        public void FirstCommitPersistsAndDifferentRealmIsRejectedWithoutSecondSave()
        {
            var save = new FakeSaveService();
            var service = new LocalRealmService(save, new FakeGameDataService(_definitions), _catalog);
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
            var service = new LocalRealmService(save, new FakeGameDataService(_definitions), _catalog);
            Assert.That(service.TrySelectRealm(new RealmSelectionRequest("", RealmId.Crownlands)).Status, Is.EqualTo(RealmSelectionStatus.InvalidTransaction));
            Assert.That(service.TrySelectRealm(new RealmSelectionRequest("tx_none", RealmId.None)).Status, Is.EqualTo(RealmSelectionStatus.InvalidRealm));
            RealmSelectionResult failed = service.TrySelectRealm(new RealmSelectionRequest("tx_fail", RealmId.Eldergrove));
            Assert.That(failed.Status, Is.EqualTo(RealmSelectionStatus.SaveFailedPreviousPreserved));
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
            Assert.That(service.Identity.Status, Is.EqualTo(RealmIdentityStatus.Uncommitted));
        }

        [Test]
        public void MissingRuntimeDefinitionCannotCommitOrProduceSplitCurrentRealmProperties()
        {
            var uncommitted = new FakeSaveService();
            var missingDefinitionService = new LocalRealmService(
                uncommitted,
                new FakeGameDataService(_definitions, RealmId.Umbral),
                _catalog);

            RealmSelectionResult missing = missingDefinitionService.TrySelectRealm(
                new RealmSelectionRequest("tx_missing_definition", RealmId.Umbral));

            Assert.That(missing.Status, Is.EqualTo(RealmSelectionStatus.RealmDefinitionUnavailable));
            Assert.That(uncommitted.SaveCount, Is.Zero);
            Assert.That(uncommitted.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));

            var committed = new FakeSaveService(RealmId.Umbral);
            var inconsistentService = new LocalRealmService(
                committed,
                new FakeGameDataService(_definitions, RealmId.Umbral),
                _catalog);

            Assert.That(inconsistentService.Identity.Status, Is.EqualTo(RealmIdentityStatus.CatalogUnavailable));
            Assert.That(inconsistentService.CurrentRealmId, Is.EqualTo(RealmId.None));
            Assert.That(inconsistentService.CurrentRealm, Is.Null);
        }

        [Test]
        public void NullGameDataServiceFailsClosedWithoutMutation()
        {
            var save = new FakeSaveService();
            var service = new LocalRealmService(save, null, _catalog);

            RealmSelectionResult result = service.TrySelectRealm(
                new RealmSelectionRequest("tx_null_game_data", RealmId.Crownlands));

            Assert.That(result.Status, Is.EqualTo(RealmSelectionStatus.RealmDefinitionUnavailable));
            Assert.That(save.SaveCount, Is.Zero);
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
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
            public FakeSaveService(RealmId selectedRealm = RealmId.None)
            {
                CurrentSave = NewSave();
                CurrentSave.SelectedRealm = selectedRealm;
            }

            public SaveGameData CurrentSave { get; private set; }
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
            private readonly IEnumerable<RealmDefinition> _definitions;
            private readonly RealmId _missingRealm;

            public FakeGameDataService(IEnumerable<RealmDefinition> definitions, RealmId missingRealm = RealmId.None)
            {
                _definitions = definitions;
                _missingRealm = missingRealm;
            }

            public RealmDefinition GetRealm(RealmId id)
            {
                if (id == _missingRealm) return null;
                foreach (RealmDefinition definition in _definitions)
                    if (definition.Id == id) return definition;
                return null;
            }

            public IEnumerable<RealmDefinition> GetAllRealms() => _definitions;
            public BuildingDefinition GetBuilding(string id) => null;
            public TroopDefinition GetTroop(string id) => null;
            public ChampionDefinition GetChampion(string id) => null;
            public SkillDefinition GetSkill(string id) => null;
        }

        private static RealmDefinition CreateDefinition(RealmId id)
        {
            var definition = ScriptableObject.CreateInstance<RealmDefinition>();
            definition.Id = id;
            definition.RealmName = id.ToString();
            return definition;
        }
    }
}
