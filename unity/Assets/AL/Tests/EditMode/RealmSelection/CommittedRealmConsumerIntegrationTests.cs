using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.Narrative.Nvs01;
using AL.RealmSelection;
using AL.RealmWar.Warzone;
using AL.Services.Local;
using AL.UI.RealmSelection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class CommittedRealmConsumerIntegrationTests
    {
        private RealmCatalogSnapshot _catalog;
        private readonly List<RealmDefinition> _definitions = new List<RealmDefinition>();

        [SetUp]
        public void SetUp()
        {
            RealmSelectionEventDelivery.ResetSubscribers();
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult parsed = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.That(parsed.IsSuccess, Is.True, parsed.TechnicalCode);
            _catalog = parsed.Snapshot;
            _definitions.Add(CreateDefinition(RealmId.Crownlands));
            _definitions.Add(CreateDefinition(RealmId.Stonehold));
            _definitions.Add(CreateDefinition(RealmId.Eldergrove));
            _definitions.Add(CreateDefinition(RealmId.Umbral));
        }

        [TearDown]
        public void TearDown()
        {
            RealmSelectionEventDelivery.ResetSubscribers();
            for (int i = 0; i < _definitions.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_definitions[i]);
            }

            _definitions.Clear();
        }

        [Test]
        public void CatalogQueryDistinguishesFoundUnknownUnavailableAndUnplayable()
        {
            Assert.That(
                RealmAuthorityQuery.Evaluate(_catalog, RealmId.Stonehold).Status,
                Is.EqualTo(RealmAuthorityQueryStatus.FoundValid));
            Assert.That(
                RealmAuthorityQuery.Evaluate(_catalog, (RealmId)999).Status,
                Is.EqualTo(RealmAuthorityQueryStatus.NotPlayable));
            Assert.That(
                RealmAuthorityQuery.Evaluate(_catalog, RealmId.None).Status,
                Is.EqualTo(RealmAuthorityQueryStatus.NotPlayable));
            Assert.That(
                RealmAuthorityQuery.Evaluate(null, RealmId.Stonehold).Status,
                Is.EqualTo(RealmAuthorityQueryStatus.UnavailableCatalog));
        }

        [Test]
        public void ProductionIdentityCommitsFromCatalogWithoutGameDataFallback()
        {
            var save = new FakeSaveService();
            var service = new LocalRealmService(save, null, _catalog);

            RealmSelectionResult result = service.TrySelectRealm(
                new RealmSelectionRequest("tx_catalog_only_umbral_01", RealmId.Umbral));

            Assert.That(result.Status, Is.EqualTo(RealmSelectionStatus.ProfileUnavailable));
            Assert.That(result.TechnicalCode, Is.EqualTo("AL-REALM-TYPED-CANDIDATE-STORE-UNAVAILABLE"));
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.None));
            Assert.That(save.SaveCount, Is.Zero);

            var persisted = new FakeSaveService(RealmId.Umbral)
            {
                CurrentSave =
                {
                    RealmSelection = Receipt(RealmId.Umbral, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                }
            };
            var identityService = new LocalRealmService(
                persisted,
                new FakeGameDataService(),
                _catalog);

            Assert.That(identityService.Identity.IsCommittedValid, Is.True);
            Assert.That(identityService.CurrentRealmId, Is.EqualTo(RealmId.Umbral));
            Assert.That(identityService.CurrentRealm, Is.Null);
        }

        [Test]
        public void MissingCatalogStillFailsClosedAndDoesNotSubstituteCrownlands()
        {
            var save = new FakeSaveService(RealmId.Umbral);
            var service = new LocalRealmService(save, new FakeGameDataService(_definitions.ToArray()), null);

            Assert.That(service.Identity.Status, Is.EqualTo(RealmIdentityStatus.CatalogUnavailable));
            Assert.That(service.CurrentRealmId, Is.EqualTo(RealmId.None));
            Assert.That(
                service.TrySelectRealm(new RealmSelectionRequest("tx_no_catalog_umbral_01", RealmId.Umbral)).Status,
                Is.EqualTo(RealmSelectionStatus.RealmDefinitionUnavailable));
            Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.Umbral));
        }

        [Test]
        public void FirstCommitPublishesOneEventAndIsolatesSubscriberFailure()
        {
            string root = CreateRoot();
            try
            {
                LocalSaveGameService save = CreateUncommittedWritable(root);
                LocalRealmService realm = CreateRealmService(save);
                var calls = new List<string>();
                RealmSelectionEventDelivery.Committed += evt =>
                {
                    calls.Add("throw:" + evt.NewRealmId + ":" + evt.EventId);
                    throw new InvalidOperationException("subscriber");
                };
                RealmSelectionEventDelivery.Committed += evt =>
                {
                    calls.Add("later:" + evt.NewRealmId + ":" + evt.EventId);
                };

                LogAssert.Expect(LogType.Warning, new Regex("AL-REALM-EVENT-HANDLER"));
                var request = new RealmSelectionRequest(
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    RealmId.Stonehold);
                RealmSelectionResult first = realm.TrySelectRealm(request);
                Assert.That(first.Status, Is.EqualTo(RealmSelectionStatus.Committed));
                Assert.That(save.CurrentSave.SelectedRealm, Is.EqualTo(RealmId.Stonehold));
                Assert.That(calls.Count, Is.EqualTo(2));
                Assert.That(calls[0], Does.StartWith("throw:Stonehold:"));
                Assert.That(calls[1], Does.StartWith("later:Stonehold:"));
                string eventId = save.CurrentSave.RealmSelection.EventId;
                byte[] afterFirst = File.ReadAllBytes(Path.Combine(root, "save.json"));

                RealmSelectionResult replay = realm.TrySelectRealm(request);
                Assert.That(replay.Status, Is.EqualTo(RealmSelectionStatus.AlreadyCommittedSameRealm));
                Assert.That(calls.Count, Is.EqualTo(2));
                CollectionAssert.AreEqual(
                    afterFirst,
                    File.ReadAllBytes(Path.Combine(root, "save.json")));

                RealmSelectionResult different = realm.TrySelectRealm(
                    new RealmSelectionRequest(
                        "cccccccccccccccccccccccccccccccc",
                        RealmId.Crownlands));
                Assert.That(different.Status, Is.EqualTo(RealmSelectionStatus.RejectedDifferentRealm));
                Assert.That(calls.Count, Is.EqualTo(2));
                Assert.That(save.CurrentSave.RealmSelection.EventId, Is.EqualTo(eventId));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void NvsEligibilityRequiresPersistedCommittedReceipt()
        {
            var identity = new RealmIdentitySnapshot(
                RealmIdentityStatus.CommittedValid,
                RealmId.Eldergrove,
                RealmCatalogRuntime.SupportedVersion,
                "AL-REALM-COMMITTED-VALID");

            Nvs01RealmContext preview = Nvs01RealmContextAdapter.FromPersistedIdentity(
                identity,
                null,
                _catalog);
            Assert.That(preview.IsCommittedValid, Is.False);

            Nvs01RealmContext mismatch = Nvs01RealmContextAdapter.FromPersistedIdentity(
                identity,
                Receipt(RealmId.Crownlands, "dddddddddddddddddddddddddddddddd"),
                _catalog);
            Assert.That(mismatch.IsCommittedValid, Is.False);

            Nvs01RealmContext valid = Nvs01RealmContextAdapter.FromPersistedIdentity(
                identity,
                Receipt(RealmId.Eldergrove, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"),
                _catalog);
            Assert.That(valid.IsCommittedValid, Is.True);
            Assert.That(valid.RealmId, Is.EqualTo("eldergrove"));
        }

        [Test]
        public void ConsumersUseCommittedAuthorityAndNeverRawSelectedRealm()
        {
            var uncommitted = new SaveGameData
            {
                SelectedRealm = RealmId.Crownlands,
                Territories = new List<TerritoryData>
                {
                    new TerritoryData
                    {
                        Id = "T-gold",
                        OwnerRealm = RealmId.Crownlands,
                        BonusType = ResourceType.Gold,
                        BonusAmount = 20
                    }
                },
                RealmGems = new List<RealmGemState>
                {
                    new RealmGemState
                    {
                        GemId = "gem_crownlands_sun",
                        HomeRealm = RealmId.Crownlands,
                        GemIndex = 1,
                        IsAtHome = true
                    }
                }
            };
            var uncommittedSave = new FakeSaveService { CurrentSave = uncommitted };
            var warzone = WarzoneService.CreateForTests(uncommittedSave, _catalog);
            Assert.That(warzone.CalculatePassiveIncome(ResourceType.Gold), Is.EqualTo(0));

            CommittedRealmAuthority authority;
            Assert.That(
                CommittedRealmConsumer.TryResolve(
                    new RealmIdentitySnapshot(
                        RealmIdentityStatus.CommittedValid,
                        RealmId.Crownlands,
                        RealmCatalogRuntime.SupportedVersion,
                        "AL-REALM-COMMITTED-VALID"),
                    null,
                    _catalog,
                    out authority),
                Is.False);

            uncommitted.RealmSelection = Receipt(
                RealmId.Stonehold,
                "ffffffffffffffffffffffffffffffff");
            Assert.That(warzone.CalculatePassiveIncome(ResourceType.Gold), Is.EqualTo(0));

            var committed = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                RealmSelection = Receipt(RealmId.Stonehold, "11111111111111111111111111111111"),
                Territories = new List<TerritoryData>
                {
                    new TerritoryData
                    {
                        Id = "T-stone",
                        OwnerRealm = RealmId.Stonehold,
                        BonusType = ResourceType.Gold,
                        BonusAmount = 15
                    },
                    new TerritoryData
                    {
                        Id = "T-crown",
                        OwnerRealm = RealmId.Crownlands,
                        BonusType = ResourceType.Gold,
                        BonusAmount = 99
                    }
                }
            };
            var committedSave = new FakeSaveService { CurrentSave = committed };
            Assert.That(
                WarzoneService.CreateForTests(committedSave, _catalog).CalculatePassiveIncome(ResourceType.Gold),
                Is.EqualTo(15));

            Assert.That(
                CommittedRealmConsumer.TryResolve(
                    new RealmIdentitySnapshot(
                        RealmIdentityStatus.CommittedValid,
                        RealmId.Stonehold,
                        RealmCatalogRuntime.SupportedVersion,
                        "AL-REALM-COMMITTED-VALID"),
                    committed.RealmSelection,
                    _catalog,
                    out authority),
                Is.True);
            Assert.That(authority.RealmId, Is.EqualTo(RealmId.Stonehold));
            Assert.That(authority.CatalogId, Is.EqualTo("stonehold"));
            Assert.That(authority.RealmGemIds, Does.Contain("gem_stonehold_forge"));
        }

        [Test]
        public void LocalizedLockFailureAndSuccessFeedbackAreVisible()
        {
            string lockWarning;
            Assert.That(
                RealmSelectionFeedback.TryResolveLockWarning(_catalog, out lockWarning),
                Is.True);
            Assert.That(lockWarning, Does.Contain("bound to the chosen realm"));

            RealmSelectionFeedbackPresentation success = RealmSelectionFeedback.FromResult(
                new RealmSelectionResult(
                    RealmSelectionStatus.Committed,
                    RealmId.Umbral,
                    RealmId.Umbral,
                    true,
                    true,
                    "AL-REALM-COMMITTED"),
                _catalog);
            Assert.That(success.IsSuccess, Is.True);
            Assert.That(success.LocalizationKey, Is.EqualTo("realm.umbral.selection.line"));
            Assert.That(success.Text, Does.Contain("veil"));

            RealmSelectionFeedbackPresentation locked = RealmSelectionFeedback.FromResult(
                new RealmSelectionResult(
                    RealmSelectionStatus.RejectedDifferentRealm,
                    RealmId.Crownlands,
                    RealmId.Umbral,
                    false,
                    false,
                    "AL-REALM-DIFFERENT-REALM-REJECTED"),
                _catalog);
            Assert.That(locked.IsSuccess, Is.False);
            Assert.That(locked.LocalizationKey, Is.EqualTo("realm.lock.warning"));
            Assert.That(locked.Text, Does.Contain("reset").IgnoreCase);
        }

        [Test]
        public void ResetPolicyForbidsAutomaticReplacementAndRequiresVerifiedDeletion()
        {
            Assert.That(RealmSelectionResetPolicy.AllowsAutomaticProfileReplacement, Is.False);
            Assert.That(RealmSelectionResetPolicy.RequiresVerifiedDeletion, Is.True);
            Assert.That(
                RealmSelectionResetPolicy.PathId,
                Is.EqualTo(RealmSelectionResetPolicy.ExplicitDeleteSavePath));
        }

        private LocalRealmService CreateRealmService(LocalSaveGameService save)
        {
            return new LocalRealmService(
                save,
                new FakeGameDataService(_definitions.ToArray()),
                _catalog);
        }

        private static RealmSelectionAuthorityState Receipt(RealmId realm, string transactionId)
        {
            var authority = new RealmSelectionAuthorityState
            {
                Version = RealmSelectionAuthority.CurrentVersion,
                Committed = true,
                SelectedRealm = (int)realm,
                ProfileId = "alp_0123456789abcdef0123456789abcdef",
                TransactionId = transactionId,
                CorrelationId = transactionId,
                OperationId = RealmSelectionAuthority.OperationId,
                EventId = RealmSelectionAuthority.EventId(transactionId),
                CatalogVersion = RealmCatalogRuntime.SupportedVersion,
                Provenance = RealmSelectionAuthority.InitialProvenance,
                Revision = 1
            };
            authority.ReceiptFingerprint = RealmSelectionAuthority.ComputeReceiptFingerprint(
                authority.ProfileId,
                realm,
                authority.TransactionId,
                authority.CorrelationId,
                authority.OperationId,
                authority.EventId,
                authority.Provenance,
                authority.Revision);
            return authority;
        }

        private static RealmDefinition CreateDefinition(RealmId id)
        {
            var definition = ScriptableObject.CreateInstance<RealmDefinition>();
            definition.Id = id;
            definition.RealmName = id.ToString();
            return definition;
        }

        private static LocalSaveGameService CreateUncommittedWritable(string root)
        {
            LocalSaveGameService save = CreateService(root);
            save.Load();
            if (save.CurrentSave != null &&
                save.CurrentSave.SelectedRealm == RealmId.None)
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
            ConstructorInfo constructor = typeof(LocalSaveGameService).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(ISaveFileOperations) },
                null);
            Assert.NotNull(constructor);
            return (LocalSaveGameService)constructor.Invoke(
                new object[] { root, new SystemSaveFileOperations() });
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
                "AnotherLife-CommittedRealmConsumer",
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

        private sealed class FakeSaveService : ISaveGameService
        {
            public FakeSaveService()
                : this(RealmId.None)
            {
            }

            public FakeSaveService(RealmId selected)
            {
                CurrentSave = new SaveGameData { SelectedRealm = selected };
            }

            public SaveGameData CurrentSave { get; set; }
            public SaveLoadStatus LastLoadStatus { get; private set; }
            public string LastLoadMessage { get; private set; } = string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage { get; private set; } = string.Empty;
            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
                LastSaveStatus = SaveOperationStatus.SavedPrimary;
            }

            public void Load()
            {
            }

            public bool HasSave() => CurrentSave != null;

            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }

            public void DeleteSave()
            {
                CurrentSave = null;
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
                Array.Empty<ChampionDefinition>();
            public SkillDefinition GetSkill(string id) => null;
        }
    }
}
