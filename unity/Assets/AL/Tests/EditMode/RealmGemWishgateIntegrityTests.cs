using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
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
        public void DropGemCannotLeaveGemBothHomeAndDropped()
        {
            var save = NewSave();
            save.RealmGems.Add(new RealmGemState
            {
                GemId = "gem_crownlands_sun",
                HomeRealm = RealmId.Crownlands,
                GemIndex = 1,
                IsAtHome = true
            });
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            service.DropGem("gem_crownlands_sun");

            RealmGemState backing = save.RealmGems.Single();
            Assert.That(backing.IsAtHome, Is.False);
            Assert.That(backing.IsDropped, Is.True);
            Assert.That(backing.CarrierId, Is.Null);
            Assert.That(backing.LastDroppedTimestamp, Is.GreaterThan(0));
            Assert.That(fixture.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void WishgateRejectsBlankInputsAndMissingSaveWithoutMutation()
        {
            var nullFixture = new FakeSaveService(null);
            var nullService = new LocalRealmGemService(nullFixture);
            Assert.DoesNotThrow(() => nullService.MarkWishgateEarned("complete_eight_gems"));
            Assert.DoesNotThrow(() => nullService.ChooseWishReward("wish_reward"));
            Assert.That(nullFixture.SaveCount, Is.Zero);

            var save = NewSave();
            var fixture = new FakeSaveService(save);
            var service = new LocalRealmGemService(fixture);

            service.MarkWishgateEarned(" ");
            Assert.That(save.Wishgate.IsEarned, Is.False);
            Assert.That(fixture.SaveCount, Is.Zero);

            service.MarkWishgateEarned("complete_eight_gems");
            service.ChooseWishReward("");

            Assert.That(save.Wishgate.IsEarned, Is.True);
            Assert.That(save.Wishgate.LastRewardId, Is.Null);
            Assert.That(fixture.SaveCount, Is.EqualTo(1));
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
