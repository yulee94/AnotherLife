using System.Collections.Generic;
using System.Linq;
using System;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmGems;
using AL.Services.Local;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class RealmGemWishgateIntegrityTests
    {
        [Test]
        public void QueryMethodsReturnSnapshotsWithoutSeedingSaveState()
        {
            var save = NewSave();
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            List<RealmGemState> gems = service.GetRealmGems().ToList();
            WishgateState wishgate = service.GetWishgateState();

            Assert.That(gems, Is.Empty);
            Assert.That(save.RealmGems, Is.Empty);
            Assert.That(fixture.SaveCount, Is.Zero);
            Assert.That(wishgate, Is.Not.SameAs(save.Wishgate));

            wishgate.IsEarned = true;
            wishgate.EarnReason = "mutated_snapshot";

            Assert.That(save.Wishgate.IsEarned, Is.False);
            Assert.That(save.Wishgate.EarnReason, Is.Null);
        }

        [Test]
        public void RealmGemQueryCannotMutateBackingSaveEntries()
        {
            var save = NewSave();
            save.RealmGems.Add(new RealmGemState
            {
                GemId = "gem_crownlands_sun",
                HomeRealm = RealmId.Crownlands,
                GemIndex = 1,
                IsAtHome = true
            });
            var service = new LocalRealmGemService(new FakeSaveService(save));

            RealmGemState snapshot = service.GetRealmGems().Single();
            snapshot.GemId = "corrupt";
            snapshot.HomeRealm = RealmId.Umbral;
            snapshot.IsAtHome = false;
            snapshot.IsDropped = true;
            snapshot.CarrierId = "carrier";

            RealmGemState backing = save.RealmGems.Single();
            Assert.That(backing.GemId, Is.EqualTo("gem_crownlands_sun"));
            Assert.That(backing.HomeRealm, Is.EqualTo(RealmId.Crownlands));
            Assert.That(backing.IsAtHome, Is.True);
            Assert.That(backing.IsDropped, Is.False);
            Assert.That(backing.CarrierId, Is.Null);
        }

        [Test]
        public void DropGemIsContainedBeforeCustodyMutationOrSave()
        {
            var save = NewSave();
            save.RealmGems.Add(new RealmGemState
            {
                GemId = "gem_crownlands_sun",
                HomeRealm = RealmId.Crownlands,
                GemIndex = 1,
                IsAtHome = false,
                CarrierId = "carrier_a"
            });
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            service.DropGem("gem_crownlands_sun");

            RealmGemState backing = save.RealmGems.Single();
            Assert.That(backing.IsAtHome, Is.False);
            Assert.That(backing.IsDropped, Is.False);
            Assert.That(backing.CarrierId, Is.EqualTo("carrier_a"));
            Assert.That(backing.LastDroppedTimestamp, Is.Zero);
            Assert.That(fixture.SaveCount, Is.Zero);
        }

        [Test]
        public void RealmGemAndWishgateMutationsRequireReadyRecognizedCatalogAuthority()
        {
            var save = NewSave();
            var gem = new RealmGemState
            {
                GemId = "gem_crownlands_sun",
                HomeRealm = RealmId.Crownlands,
                GemIndex = 1,
                IsAtHome = true
            };
            save.RealmGems.Add(gem);
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture, () => null);

            Assert.That(service.PickUpGem(gem.GemId, "carrier"), Is.False);
            WishgateRewardResult reward = service.ApplyWishgateReward(
                new WishgateRewardRequest(
                    "wish-op-missing-catalog",
                    "carrier",
                    "zone_accordant_isle",
                    "unknown_reward"));

            Assert.That(gem.IsAtHome, Is.True);
            Assert.That(save.Wishgate.IsEarned, Is.False);
            Assert.That(reward.IsCommitted, Is.False);
            Assert.That(fixture.SaveCount, Is.Zero);
        }

        [Test]
        public void RealmGemMutationsRejectStateThatContradictsReadyCatalogEntry()
        {
            RealmGemWishgateCatalogSnapshot catalog = LoadValidRuntimeCatalog();
            var save = NewSave();
            save.RealmGems.Add(new RealmGemState
            {
                GemId = "gem_crownlands_sun",
                HomeRealm = RealmId.Umbral,
                GemIndex = 1,
                IsAtHome = true
            });
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture, () => catalog);

            Assert.That(service.PickUpGem("gem_crownlands_sun", "carrier"), Is.False);
            Assert.That(fixture.SaveCount, Is.Zero);
        }

        [Test]
        public void RealmGemMutationsDoNotSeedEmptyOrNullCollections()
        {
            var save = NewSave();
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            Assert.That(service.PickUpGem("missing", "carrier"), Is.False);
            service.DropGem("missing");
            service.ReturnGemHome("missing");
            Assert.That(save.RealmGems, Is.Empty);

            save.RealmGems = null;
            Assert.That(service.PickUpGem("missing", "carrier"), Is.False);
            service.DropGem("missing");
            service.ReturnGemHome("missing");
            Assert.That(save.RealmGems, Is.Null);
            Assert.That(fixture.SaveCount, Is.Zero);
        }

        [Test]
        public void RealmGemCustodyWritersRemainContainedWithoutStateDrift()
        {
            var save = NewSave();
            var gem = new RealmGemState
            {
                GemId = "gem_crownlands_sun",
                HomeRealm = RealmId.Crownlands,
                GemIndex = 1,
                IsAtHome = true
            };
            save.RealmGems.Add(gem);
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            Assert.That(service.PickUpGem(gem.GemId, "carrier_a"), Is.False);
            Assert.That(service.PickUpGem(gem.GemId, "carrier_a"), Is.False);
            Assert.That(service.PickUpGem(gem.GemId, "carrier_b"), Is.False);
            Assert.That(fixture.SaveCount, Is.Zero);

            service.DropGem(gem.GemId);
            long droppedAt = gem.LastDroppedTimestamp;
            service.DropGem(gem.GemId);
            Assert.That(gem.IsAtHome, Is.True);
            Assert.That(gem.IsDropped, Is.False);
            Assert.That(gem.LastDroppedTimestamp, Is.EqualTo(droppedAt));
            Assert.That(fixture.SaveCount, Is.Zero);

            Assert.That(service.PickUpGem(gem.GemId, "carrier_b"), Is.False);
            service.ReturnGemHome(gem.GemId);
            service.ReturnGemHome(gem.GemId);

            Assert.That(gem.IsAtHome, Is.True);
            Assert.That(gem.IsDropped, Is.False);
            Assert.That(gem.CarrierId, Is.Null);
            Assert.That(gem.LastDroppedTimestamp, Is.Zero);
            Assert.That(fixture.SaveCount, Is.Zero);
        }

        [Test]
        public void RealmGemRejectsDuplicateContradictoryAndFutureTimestampState()
        {
            var save = NewSave();
            var first = new RealmGemState
            {
                GemId = "gem_duplicate",
                HomeRealm = RealmId.Crownlands,
                GemIndex = 1,
                IsAtHome = true
            };
            save.RealmGems.Add(first);
            save.RealmGems.Add(new RealmGemState
            {
                GemId = "gem_duplicate",
                HomeRealm = RealmId.Crownlands,
                GemIndex = 1,
                IsAtHome = true
            });
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            Assert.That(service.PickUpGem("gem_duplicate", "carrier"), Is.False);
            save.RealmGems.RemoveAt(1);

            first.IsDropped = true;
            Assert.That(service.PickUpGem(first.GemId, "carrier"), Is.False);

            first.IsAtHome = false;
            first.LastDroppedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60;
            Assert.That(service.PickUpGem(first.GemId, "carrier"), Is.False);
            service.ReturnGemHome(first.GemId);

            Assert.That(fixture.SaveCount, Is.Zero);
        }

        [Test]
        public void WishgateRewardWriterRejectsMissingAndMalformedContextWhileContained()
        {
            var nullFixture = new FakeSaveService(null);
            var nullService = new LocalRealmGemService(nullFixture);
            WishgateRewardResult missingState = nullService.ApplyWishgateReward(
                new WishgateRewardRequest(
                    "wish-op-no-state",
                    "actor",
                    "zone_accordant_isle",
                    "warmaster_credits"));
            Assert.That(missingState.Status, Is.EqualTo(WishgateRewardStatus.InvalidState));
            Assert.That(nullFixture.SaveCount, Is.Zero);

            var save = NewSave();
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            WishgateRewardResult malformed = service.ApplyWishgateReward(
                new WishgateRewardRequest("", "actor", "zone_accordant_isle", ""));

            Assert.That(malformed.Status, Is.EqualTo(WishgateRewardStatus.MissingContext));
            Assert.That(save.Wishgate.IsEarned, Is.False);
            Assert.That(save.Wishgate.EarnReason, Is.Null);
            Assert.That(save.Wishgate.LastRewardId, Is.Null);
            Assert.That(save.Wishgate.LastRewardChosenTimestamp, Is.Zero);
            Assert.That(fixture.SaveCount, Is.Zero);
        }

        private static RealmGemWishgateCatalogSnapshot LoadValidRuntimeCatalog()
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "StreamingAssets",
                RealmGemWishgateRuntimeCatalog.RelativePath);
            RealmGemWishgateCatalogLoadResult result =
                RealmGemWishgateRuntimeCatalog.Parse(System.IO.File.ReadAllText(path));
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            return result.Snapshot;
        }

        private static SaveGameData NewSave()
        {
            return new SaveGameData
            {
                SaveFormatId = SaveGameData.CurrentSaveFormatId,
                SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
                ProfileInitializationVersion = SaveGameData.CurrentProfileInitializationVersion,
                SelectedRealm = RealmId.None,
                RealmGems = new List<RealmGemState>(),
                Wishgate = new WishgateState()
            };
        }

        private sealed class FakeSaveService : ISaveGameService
        {
            public FakeSaveService(SaveGameData currentSave)
            {
                CurrentSave = currentSave;
            }

            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage => string.Empty;
            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
                LastSaveStatus = SaveOperationStatus.SavedPrimary;
            }

            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) { CurrentSave = NewSave(); CurrentSave.SelectedRealm = realmId; }
            public void DeleteSave() { CurrentSave = null; }
        }
    }
}
