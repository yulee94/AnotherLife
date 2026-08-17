using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.RealmWar.Warzone;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class CommittedRealmConsumerIntegrationTests
    {
        private readonly List<RealmDefinition> _definitions = new List<RealmDefinition>();
        private RealmCatalogSnapshot _catalog;

        [SetUp]
        public void SetUp()
        {
            string json = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "al_realm_catalog.json"));
            _catalog = RealmCatalogRuntime.Parse(json).Snapshot;
            _definitions.Add(CreateDefinition(RealmId.Crownlands));
            _definitions.Add(CreateDefinition(RealmId.Stonehold));
            _definitions.Add(CreateDefinition(RealmId.Eldergrove));
            _definitions.Add(CreateDefinition(RealmId.Umbral));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (RealmDefinition definition in _definitions)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
            _definitions.Clear();
        }

        [Test]
        public void SoftSelectedRealmCannotBecomeVisibleWithoutCommittedAuthority()
        {
            var store = new FakeProductionStore(
                RealmIdentityStatus.Uncommitted,
                RealmId.None)
            {
                SoftSelectedRealm = RealmId.Umbral
            };
            var service = new LocalRealmService(
                store,
                new FakeGameDataService(_definitions),
                _catalog);

            Assert.That(service.Identity.Status, Is.EqualTo(RealmIdentityStatus.Uncommitted));
            Assert.That(service.CurrentRealmId, Is.EqualTo(RealmId.None));
            Assert.That(service.CurrentRealm, Is.Null);
        }

        [Test]
        public void WarzoneIncomeUsesCommittedRealmInsteadOfSoftSelectedRealm()
        {
            var store = new FakeProductionStore(
                RealmIdentityStatus.CommittedValid,
                RealmId.Stonehold)
            {
                SoftSelectedRealm = RealmId.Umbral
            };
            store.CurrentSave.Territories = new List<TerritoryData>
            {
                new TerritoryData
                {
                    Id = "committed",
                    OwnerRealm = RealmId.Stonehold,
                    BonusType = ResourceType.Gold,
                    BonusAmount = 7
                },
                new TerritoryData
                {
                    Id = "soft",
                    OwnerRealm = RealmId.Umbral,
                    BonusType = ResourceType.Gold,
                    BonusAmount = 99
                }
            };
            var realmService = new LocalRealmService(
                store,
                new FakeGameDataService(_definitions),
                _catalog);
            var warzone = new WarzoneService(store, realmService);

            Assert.That(
                warzone.CalculatePassiveIncome(ResourceType.Gold),
                Is.EqualTo(7));
        }

        [Test]
        public void SupportedLegacyRequestUsesProfileBoundCommitAuthority()
        {
            var store = new FakeProductionStore(
                RealmIdentityStatus.Uncommitted,
                RealmId.None);
            var service = new LocalRealmService(
                store,
                new FakeGameDataService(_definitions),
                _catalog);

            RealmSelectionResult result = service.TrySelectRealm(
                new RealmSelectionRequest("legacy-ui-request", RealmId.Stonehold));

            Assert.That(store.LegacyCommitCalls, Is.Zero);
            Assert.That(store.TypedCommitCalls, Is.EqualTo(1));
            Assert.That(store.LastCommand.TransactionId, Does.Match("^rsel_[0-9a-f]{32}$"));
            Assert.That(store.LastCommand.RequestedCanonicalRealmId, Is.EqualTo("stonehold"));
            Assert.That(store.LastCommand.CatalogAuthorityId, Is.EqualTo("al_realm_catalog"));
            Assert.That(result.Status, Is.EqualTo(RealmSelectionStatus.Committed));
            Assert.That(result.Persisted, Is.True);
            Assert.That(service.CurrentRealmId, Is.EqualTo(RealmId.Stonehold));
        }

        [Test]
        public void DuplicateAndOutOfOrderEventsCannotSwitchCommittedRealm()
        {
            var store = new FakeProductionStore(
                RealmIdentityStatus.CommittedValid,
                RealmId.Eldergrove);
            var service = new LocalRealmService(
                store,
                new FakeGameDataService(_definitions),
                _catalog);

            store.Publish("rsevt_new", RealmId.Eldergrove, 9);
            store.Publish("rsevt_old", RealmId.Umbral, 3);
            store.Publish("rsevt_new", RealmId.Umbral, 9);

            Assert.That(service.Identity.RealmId, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(service.CurrentRealmId, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(service.CurrentRealm.Id, Is.EqualTo(RealmId.Eldergrove));
        }

        [Test]
        public void ReloadAndProfileSelectionRefreshFromCurrentCommittedAuthority()
        {
            var store = new FakeProductionStore(
                RealmIdentityStatus.Uncommitted,
                RealmId.None);
            var service = new LocalRealmService(
                store,
                new FakeGameDataService(_definitions),
                _catalog);

            Assert.That(service.CurrentRealmId, Is.EqualTo(RealmId.None));

            store.SetCommittedAuthority(RealmId.Crownlands);
            Assert.That(service.CurrentRealmId, Is.EqualTo(RealmId.Crownlands));

            store.SetCommittedAuthority(RealmId.Stonehold);
            Assert.That(service.CurrentRealmId, Is.EqualTo(RealmId.Stonehold));
            Assert.That(service.CurrentRealm.Id, Is.EqualTo(RealmId.Stonehold));
        }

        private static RealmDefinition CreateDefinition(RealmId id)
        {
            RealmDefinition definition = ScriptableObject.CreateInstance<RealmDefinition>();
            definition.Id = id;
            definition.RealmName = id.ToString();
            return definition;
        }

        private sealed class FakeGameDataService : IGameDataService
        {
            private readonly IEnumerable<RealmDefinition> _definitions;

            internal FakeGameDataService(IEnumerable<RealmDefinition> definitions)
            {
                _definitions = definitions;
            }

            public RealmDefinition GetRealm(RealmId id)
            {
                foreach (RealmDefinition definition in _definitions)
                {
                    if (definition.Id == id) return definition;
                }
                return null;
            }

            public IEnumerable<RealmDefinition> GetAllRealms() => _definitions;
            public BuildingDefinition GetBuilding(string id) => null;
            public TroopDefinition GetTroop(string id) => null;
            public ChampionDefinition GetChampion(string id) => null;
            public SkillDefinition GetSkill(string id) => null;
        }

        private sealed class FakeProductionStore :
            ISaveGameService,
            IProfileBoundRealmSelectionStore,
            IProfileWriteAuthorityProvider,
            ILegacyRealmSelectionCandidateStore
        {
            private RealmIdentitySnapshot _committed;

            internal FakeProductionStore(RealmIdentityStatus status, RealmId realmId)
            {
                _committed = new RealmIdentitySnapshot(status, realmId, "0.1.0", "test");
                CurrentSave = new SaveGameData();
            }

            internal RealmId SoftSelectedRealm
            {
                set => CurrentSave.SelectedRealm = value;
            }

            internal int TypedCommitCalls { get; private set; }
            internal int LegacyCommitCalls { get; private set; }
            internal RealmSelectionCommand LastCommand { get; private set; }

            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.SavedPrimary;
            public string LastSaveMessage => string.Empty;
            public event Action<RealmSelectionCommittedEvent> RealmSelectionCommitted;

            public RealmIdentitySnapshot GetCommittedRealm() => _committed;

            public RealmSelectionCommitResult TryCommitRealmSelection(RealmSelectionCommand command)
            {
                TypedCommitCalls++;
                LastCommand = command;
                _committed = new RealmIdentitySnapshot(
                    RealmIdentityStatus.CommittedValid,
                    command.RequestedRealmId,
                    command.CatalogVersion,
                    "test");
                return new RealmSelectionCommitResult(
                    RealmSelectionCommitStatus.Committed,
                    command.Authority.ProfileId,
                    command.RequestedRealmId,
                    command.RequestedRealmId,
                    command.RequestedCanonicalRealmId,
                    command.TransactionId,
                    command.TransactionId,
                    new string('a', 64),
                    command.CatalogAuthorityId,
                    command.CatalogVersion,
                    1,
                    "rsevt_" + command.TransactionId.Substring(5),
                    true,
                    true,
                    new string('b', 64),
                    "test");
            }

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority() =>
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    "alp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    new string('c', 64),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());

            public RealmSelectionResult TryCommitLegacyRealmSelection(RealmSelectionRequest request)
            {
                LegacyCommitCalls++;
                return new RealmSelectionResult(
                    RealmSelectionStatus.Committed,
                    request.RequestedRealmId,
                    request.RequestedRealmId,
                    true,
                    true,
                    "legacy");
            }

            internal void Publish(string eventId, RealmId realmId, long revision)
            {
                RealmSelectionCommitted?.Invoke(new RealmSelectionCommittedEvent(
                    1,
                    eventId,
                    "alp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    "rsel_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    new string('a', 64),
                    realmId,
                    realmId.ToString().ToLowerInvariant(),
                    "al_realm_catalog",
                    "0.1.0",
                    revision,
                    1,
                    new string('b', 64),
                    RealmSelectionCommitProvenance.InitialSelection));
            }

            internal void SetCommittedAuthority(RealmId realmId)
            {
                _committed = new RealmIdentitySnapshot(
                    RealmIdentityStatus.CommittedValid,
                    realmId,
                    "0.1.0",
                    "test");
            }

            public void Save() { }
            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) => CurrentSave = new SaveGameData { SelectedRealm = realmId };
            public void DeleteSave() => CurrentSave = null;
        }
    }
}
