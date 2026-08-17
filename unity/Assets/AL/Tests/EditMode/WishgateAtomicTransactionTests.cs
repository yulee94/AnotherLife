using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmGems;
using AL.Services.Local;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class WishgateAtomicTransactionTests
    {
        private const string ActorId = "actor_crownlands_01";
        private const string ZoneId = "zone_accordant_isle";
        private const string RewardId = "warmaster_credits";

        [Test]
        public void EligibleRequestCommitsEntitlementReceiptAndRewardInOneSave()
        {
            SaveGameData save = NewSave();
            save.WarzoneCredits = 17;
            var fixture = new ConfigurableSaveService(save);
            LocalRealmGemService service = CreateService(fixture);

            WishgateRewardResult result = service.ApplyWishgateReward(Request("wish-op-001"));

            Assert.That(result.Status, Is.EqualTo(WishgateRewardStatus.Committed));
            Assert.That(result.OperationId, Is.EqualTo("wish-op-001"));
            Assert.That(result.RewardId, Is.EqualTo(RewardId));
            Assert.That(result.WarzoneCreditsAwarded, Is.EqualTo(300));
            Assert.That(save.WarzoneCredits, Is.EqualTo(317));
            Assert.That(save.Wishgate.IsEarned, Is.True);
            Assert.That(save.Wishgate.LastRewardId, Is.EqualTo(RewardId));
            Assert.That(save.Wishgate.CommittedReward.OperationId, Is.EqualTo("wish-op-001"));
            Assert.That(save.Wishgate.CommittedReward.CommitUncertain, Is.False);
            Assert.That(fixture.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void SameOperationReplayAndNewServiceReturnOriginalCommittedResult()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            WishgateRewardResult committed = CreateService(fixture).ApplyWishgateReward(Request("wish-op-replay"));

            WishgateRewardResult replay = CreateService(fixture).ApplyWishgateReward(Request("wish-op-replay"));

            Assert.That(committed.Status, Is.EqualTo(WishgateRewardStatus.Committed));
            Assert.That(replay.Status, Is.EqualTo(WishgateRewardStatus.AlreadyCommitted));
            Assert.That(replay.OperationId, Is.EqualTo(committed.OperationId));
            Assert.That(replay.CommittedTimestamp, Is.EqualTo(committed.CommittedTimestamp));
            Assert.That(replay.WarzoneCreditsAwarded, Is.EqualTo(committed.WarzoneCreditsAwarded));
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
            Assert.That(fixture.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void DifferentOperationForConsumedEntitlementIsRejectedWithoutLeakingReceipt()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            CreateService(fixture).ApplyWishgateReward(Request("wish-op-original"));

            WishgateRewardResult replay = CreateService(fixture).ApplyWishgateReward(Request("wish-op-other"));

            Assert.That(replay.Status, Is.EqualTo(WishgateRewardStatus.EntitlementAlreadyConsumed));
            Assert.That(replay.OperationId, Is.Empty);
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
            Assert.That(fixture.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void ReusedOperationWithDifferentPayloadIsRejectedWithoutMutation()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            LocalRealmGemService service = CreateService(fixture);
            service.ApplyWishgateReward(Request("wish-op-collision"));

            WishgateRewardResult collision = service.ApplyWishgateReward(
                new WishgateRewardRequest("wish-op-collision", ActorId, ZoneId, "different_reward"));

            Assert.That(collision.Status, Is.EqualTo(WishgateRewardStatus.IdempotencyConflict));
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
            Assert.That(save.Wishgate.LastRewardId, Is.EqualTo(RewardId));
            Assert.That(fixture.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void SaveFailureRollsBackEveryProvisionalMutation()
        {
            SaveGameData save = NewSave();
            save.WarzoneCredits = 41;
            var fixture = new ConfigurableSaveService(save)
            {
                NextSaveStatus = SaveOperationStatus.SaveFailedPreviousPreserved
            };

            WishgateRewardResult result = CreateService(fixture).ApplyWishgateReward(Request("wish-op-fail"));

            Assert.That(result.Status, Is.EqualTo(WishgateRewardStatus.SaveFailedRolledBack));
            Assert.That(save.WarzoneCredits, Is.EqualTo(41));
            Assert.That(save.Wishgate.IsEarned, Is.False);
            Assert.That(save.Wishgate.LastRewardId, Is.Null.Or.Empty);
            Assert.That(save.Wishgate.HasCommittedReward, Is.False);
            Assert.That(save.Wishgate.CommittedReward, Is.Null);
            Assert.That(fixture.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void CommitUncertainKeepsCandidateIntactForRecoveryAndRetry()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save)
            {
                NextSaveStatus = SaveOperationStatus.CommitUncertain
            };
            LocalRealmGemService service = CreateService(fixture);

            WishgateRewardResult uncertain = service.ApplyWishgateReward(Request("wish-op-uncertain"));
            WishgateRewardResult retry = service.ApplyWishgateReward(Request("wish-op-uncertain"));
            var recoveredFixture = new ConfigurableSaveService(save);
            WishgateRewardResult recovered = CreateService(recoveredFixture)
                .ApplyWishgateReward(Request("wish-op-uncertain"));

            Assert.That(uncertain.Status, Is.EqualTo(WishgateRewardStatus.CommitUncertain));
            Assert.That(retry.Status, Is.EqualTo(WishgateRewardStatus.CommitUncertain));
            Assert.That(recovered.Status, Is.EqualTo(WishgateRewardStatus.AlreadyCommitted));
            Assert.That(recovered.OperationId, Is.EqualTo("wish-op-uncertain"));
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
            Assert.That(save.Wishgate.CommittedReward, Is.Not.Null);
            Assert.That(fixture.SaveCount, Is.EqualTo(2));
            Assert.That(recoveredFixture.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void InvalidAuthorityPreventsAllMutationsAndPersistence()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            var invalidAuthority = new FixedAuthorityProvider(Authority(actorEligible: false));
            LocalRealmGemService service = CreateService(fixture, invalidAuthority);

            WishgateRewardResult result = service.ApplyWishgateReward(Request("wish-op-denied"));

            Assert.That(result.Status, Is.EqualTo(WishgateRewardStatus.IneligibleActor));
            Assert.That(save.WarzoneCredits, Is.Zero);
            Assert.That(save.Wishgate.IsEarned, Is.False);
            Assert.That(save.Wishgate.CommittedReward, Is.Null);
            Assert.That(fixture.SaveCount, Is.Zero);
        }

        [Test]
        public void ConcurrentRequestsAgainstSameEntitlementCommitExactlyOnce()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            LocalRealmGemService first = CreateService(fixture);
            LocalRealmGemService second = CreateService(fixture);

            WishgateRewardResult[] results = Task.WhenAll(
                Task.Run(() => first.ApplyWishgateReward(Request("wish-op-concurrent-a"))),
                Task.Run(() => second.ApplyWishgateReward(Request("wish-op-concurrent-b"))))
                .GetAwaiter()
                .GetResult();

            Assert.That(results.Count(result => result.Status == WishgateRewardStatus.Committed), Is.EqualTo(1));
            Assert.That(results.Count(result => result.Status == WishgateRewardStatus.EntitlementAlreadyConsumed), Is.EqualTo(1));
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
            Assert.That(fixture.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void TamperedCommittedReceiptFailsClosed()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            LocalRealmGemService service = CreateService(fixture);
            service.ApplyWishgateReward(Request("wish-op-tamper"));
            save.Wishgate.CommittedReward.WarzoneCreditsAwarded = 999;

            WishgateRewardResult result = service.ApplyWishgateReward(Request("wish-op-tamper"));

            Assert.That(result.Status, Is.EqualTo(WishgateRewardStatus.InvalidState));
            Assert.That(result.TechnicalCode, Is.EqualTo("AL-RGW-REWARD-RECEIPT-MALFORMED"));
            Assert.That(fixture.SaveCount, Is.EqualTo(2));
        }

        private static WishgateRewardRequest Request(string operationId) =>
            new WishgateRewardRequest(operationId, ActorId, ZoneId, RewardId);

        private static LocalRealmGemService CreateService(
            ConfigurableSaveService fixture,
            IRealmGemWishgateAuthorityProvider authority = null) =>
            new LocalRealmGemService(
                fixture,
                RewardCatalog,
                authority ?? new FixedAuthorityProvider(Authority()),
                () => fixture.CurrentSave);

        private static RealmGemWishgateAuthoritySnapshot Authority(bool actorEligible = true)
        {
            RealmGemWishgateCatalogSnapshot catalog = RewardCatalog();
            return new RealmGemWishgateAuthoritySnapshot(
                catalog.AuthorityId,
                catalog.AuthorityVersion,
                ActorId,
                actorEligible,
                RealmId.Crownlands,
                ZoneId,
                RealmId.None,
                true,
                true,
                8);
        }

        private static RealmGemWishgateCatalogSnapshot RewardCatalog()
        {
            RealmGemWishgateCatalogSnapshot production = LoadProductionCatalog();
            return new RealmGemWishgateCatalogSnapshot(
                production.CatalogId,
                production.ContentVersion,
                production.AuthorityId,
                production.AuthorityVersion,
                production.SourceCatalogId,
                production.SourcePacketId,
                production.SourceSha256,
                production.CustodyAuthorityAvailable,
                production.RealmGems,
                new WishgateCatalogEntry(
                    production.Wishgate.Id,
                    production.Wishgate.EntryZoneId,
                    production.Wishgate.RequiredGemCount,
                    true,
                    true,
                    new[] { RewardId }));
        }

        private static RealmGemWishgateCatalogSnapshot LoadProductionCatalog()
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

        private static SaveGameData NewSave() => new SaveGameData
        {
            SaveFormatId = SaveGameData.CurrentSaveFormatId,
            SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
            ProfileInitializationVersion = SaveGameData.CurrentProfileInitializationVersion,
            SelectedRealm = RealmId.Crownlands,
            RealmGems = new List<RealmGemState>(),
            Wishgate = new WishgateState()
        };

        private sealed class FixedAuthorityProvider : IRealmGemWishgateAuthorityProvider
        {
            private readonly RealmGemWishgateAuthoritySnapshot _snapshot;
            public FixedAuthorityProvider(RealmGemWishgateAuthoritySnapshot snapshot) { _snapshot = snapshot; }
            public RealmGemWishgateAuthoritySnapshot Resolve(string actorId, string zoneId) => _snapshot;
        }

        private sealed class ConfigurableSaveService : ISaveGameService
        {
            private readonly object _gate = new object();

            public ConfigurableSaveService(SaveGameData save) { CurrentSave = save; }
            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus { get; private set; }
            public string LastSaveMessage => string.Empty;
            public SaveOperationStatus NextSaveStatus { get; set; } = SaveOperationStatus.SavedPrimary;
            public int SaveCount { get; private set; }

            public void Save()
            {
                lock (_gate)
                {
                    SaveCount++;
                    LastSaveStatus = NextSaveStatus;
                }
            }

            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) { }
            public void DeleteSave() { CurrentSave = null; }
        }
    }
}
