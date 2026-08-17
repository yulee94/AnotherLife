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
            var publisher = new RecordingOutcomePublisher(fixture);
            LocalRealmGemService service = CreateService(fixture, publisher: publisher);

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
            Assert.That(save.Wishgate.CommittedReward.OutcomeNotificationDelivered, Is.True);
            Assert.That(publisher.Payloads, Has.Count.EqualTo(1));
            Assert.That(publisher.SaveCountsAtPublish, Is.EqualTo(new[] { 2 }));
            Assert.That(publisher.Payloads[0].OperationId, Is.EqualTo("wish-op-001"));
            Assert.That(publisher.Payloads[0].CatalogItemId, Is.EqualTo(RewardId));
            Assert.That(publisher.Payloads[0].ActorId, Is.EqualTo(ActorId));
            Assert.That(publisher.Payloads[0].RecipientId, Is.EqualTo(ActorId));
            Assert.That(publisher.Payloads[0].WarzoneCreditsAwarded, Is.EqualTo(300));
            Assert.That(publisher.Payloads[0].CommittedTimestamp, Is.EqualTo(result.CommittedTimestamp));
            Assert.That(publisher.Payloads[0].OutcomeIdentity, Is.Not.Empty);
            Assert.That(fixture.SaveCount, Is.EqualTo(3));
        }

        [Test]
        public void RolledBackAndUncertainTransactionsNeverPublishSuccess()
        {
            SaveGameData failedSave = NewSave();
            var failedFixture = new ConfigurableSaveService(failedSave)
            {
                NextSaveStatus = SaveOperationStatus.SaveFailedPreviousPreserved
            };
            var failedPublisher = new RecordingOutcomePublisher(failedFixture);

            WishgateRewardResult failed = CreateService(failedFixture, publisher: failedPublisher)
                .ApplyWishgateReward(Request("wish-op-no-notify-failed"));

            SaveGameData uncertainSave = NewSave();
            var uncertainFixture = new ConfigurableSaveService(uncertainSave)
            {
                NextSaveStatus = SaveOperationStatus.CommitUncertain
            };
            var uncertainPublisher = new RecordingOutcomePublisher(uncertainFixture);
            WishgateRewardResult uncertain = CreateService(uncertainFixture, publisher: uncertainPublisher)
                .ApplyWishgateReward(Request("wish-op-no-notify-uncertain"));

            Assert.That(failed.Status, Is.EqualTo(WishgateRewardStatus.SaveFailedRolledBack));
            Assert.That(uncertain.Status, Is.EqualTo(WishgateRewardStatus.CommitUncertain));
            Assert.That(failedPublisher.Payloads, Is.Empty);
            Assert.That(uncertainPublisher.Payloads, Is.Empty);
        }

        [Test]
        public void RestartRecoveryPublishesCommittedPendingOutcomeWithoutReissuingReward()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            var unavailablePublisher = new RecordingOutcomePublisher(fixture) { ThrowOnPublish = true };
            WishgateRewardResult committed = CreateService(fixture, publisher: unavailablePublisher)
                .ApplyWishgateReward(Request("wish-op-recover-notification"));
            string identity = unavailablePublisher.Payloads.Single().OutcomeIdentity;
            long timestamp = committed.CommittedTimestamp;

            var recoveredFixture = new ConfigurableSaveService(save);
            var recoveredPublisher = new RecordingOutcomePublisher(recoveredFixture);
            WishgateOutcomeDeliveryResult recovered = CreateService(
                    recoveredFixture,
                    publisher: recoveredPublisher)
                .RecoverPendingWishgateOutcome();

            Assert.That(committed.Status, Is.EqualTo(WishgateRewardStatus.Committed));
            Assert.That(recovered.Status, Is.EqualTo(WishgateOutcomeDeliveryStatus.Delivered));
            Assert.That(recoveredPublisher.Payloads, Has.Count.EqualTo(1));
            Assert.That(recoveredPublisher.Payloads[0].OutcomeIdentity, Is.EqualTo(identity));
            Assert.That(recoveredPublisher.Payloads[0].CommittedTimestamp, Is.EqualTo(timestamp));
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
            Assert.That(save.Wishgate.CommittedReward.OutcomeNotificationDelivered, Is.True);
        }

        [Test]
        public void DeliveryReceiptSaveFailureRetriesTheSameCommittedPayload()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            fixture.SaveStatuses.Enqueue(SaveOperationStatus.SavedPrimary);
            fixture.SaveStatuses.Enqueue(SaveOperationStatus.SavedPrimary);
            fixture.SaveStatuses.Enqueue(SaveOperationStatus.SaveFailedPreviousPreserved);
            var firstPublisher = new RecordingOutcomePublisher(fixture);

            WishgateRewardResult committed = CreateService(fixture, publisher: firstPublisher)
                .ApplyWishgateReward(Request("wish-op-delivery-retry"));

            var retryFixture = new ConfigurableSaveService(save);
            var retryPublisher = new RecordingOutcomePublisher(retryFixture);
            WishgateOutcomeDeliveryResult retry = CreateService(retryFixture, publisher: retryPublisher)
                .RecoverPendingWishgateOutcome();

            Assert.That(committed.Status, Is.EqualTo(WishgateRewardStatus.Committed));
            Assert.That(firstPublisher.Payloads, Has.Count.EqualTo(1));
            Assert.That(retryPublisher.Payloads, Has.Count.EqualTo(1));
            Assert.That(retryPublisher.Payloads[0].OutcomeIdentity,
                Is.EqualTo(firstPublisher.Payloads[0].OutcomeIdentity));
            Assert.That(retryPublisher.Payloads[0].PayloadFingerprint,
                Is.EqualTo(firstPublisher.Payloads[0].PayloadFingerprint));
            Assert.That(retry.Status, Is.EqualTo(WishgateOutcomeDeliveryStatus.Delivered));
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
        }

        [Test]
        public void PersistedCloneIsReacquiredBeforeCommitConfirmationAndPublication()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save) { CloneOnSuccessfulSave = true };
            var publisher = new RecordingOutcomePublisher(fixture);

            WishgateRewardResult result = CreateService(fixture, publisher: publisher)
                .ApplyWishgateReward(Request("wish-op-cloned-save"));

            Assert.That(result.Status, Is.EqualTo(WishgateRewardStatus.Committed));
            Assert.That(publisher.Payloads, Has.Count.EqualTo(1));
            Assert.That(fixture.CurrentSave.Wishgate.CommittedReward.CommitUncertain, Is.False);
            Assert.That(
                fixture.CurrentSave.Wishgate.CommittedReward.OutcomeNotificationDelivered,
                Is.True);
            Assert.That(fixture.CurrentSave.WarzoneCredits, Is.EqualTo(300));
        }

        [Test]
        public void ReentrantRecoveryCannotRepublishAnOutcomeInFlight()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            var publisher = new ReentrantOutcomePublisher();
            LocalRealmGemService service = CreateService(fixture, publisher: publisher);
            publisher.Service = service;

            WishgateRewardResult result = service.ApplyWishgateReward(Request("wish-op-reentrant"));

            Assert.That(result.Status, Is.EqualTo(WishgateRewardStatus.Committed));
            Assert.That(publisher.PublishCount, Is.EqualTo(1));
            Assert.That(publisher.ReentrantResult.Status,
                Is.EqualTo(WishgateOutcomeDeliveryStatus.DeliveryFailed));
            Assert.That(save.Wishgate.CommittedReward.OutcomeNotificationDelivered, Is.True);
        }

        [Test]
        public void RecoveryWithPersistedDeliveredReceiptDoesNotRepublish()
        {
            SaveGameData save = NewSave();
            var fixture = new ConfigurableSaveService(save);
            var firstPublisher = new RecordingOutcomePublisher(fixture);
            CreateService(fixture, publisher: firstPublisher)
                .ApplyWishgateReward(Request("wish-op-already-delivered"));

            var restartedFixture = new ConfigurableSaveService(save);
            var restartedPublisher = new RecordingOutcomePublisher(restartedFixture);
            WishgateOutcomeDeliveryResult recovered = CreateService(
                    restartedFixture,
                    publisher: restartedPublisher)
                .RecoverPendingWishgateOutcome();

            Assert.That(recovered.Status, Is.EqualTo(WishgateOutcomeDeliveryStatus.AlreadyDelivered));
            Assert.That(recovered.OperationId, Is.EqualTo("wish-op-already-delivered"));
            Assert.That(restartedPublisher.Payloads, Is.Empty);
            Assert.That(save.WarzoneCredits, Is.EqualTo(300));
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
            IRealmGemWishgateAuthorityProvider authority = null,
            IWishgateOutcomePublisher publisher = null) =>
            new LocalRealmGemService(
                fixture,
                RewardCatalog,
                authority ?? new FixedAuthorityProvider(Authority()),
                () => fixture.CurrentSave,
                publisher);

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
            public Queue<SaveOperationStatus> SaveStatuses { get; } = new Queue<SaveOperationStatus>();
            public bool CloneOnSuccessfulSave { get; set; }
            public int SaveCount { get; private set; }

            public void Save()
            {
                lock (_gate)
                {
                    SaveCount++;
                    LastSaveStatus = SaveStatuses.Count > 0 ? SaveStatuses.Dequeue() : NextSaveStatus;
                    if (CloneOnSuccessfulSave && LastSaveStatus == SaveOperationStatus.SavedPrimary)
                    {
                        CurrentSave = UnityEngine.JsonUtility.FromJson<SaveGameData>(
                            UnityEngine.JsonUtility.ToJson(CurrentSave));
                    }
                }
            }

            public void Load() { }
            public bool HasSave() => CurrentSave != null;
            public void CreateNewSave(RealmId realmId) { }
            public void DeleteSave() { CurrentSave = null; }
        }

        private sealed class RecordingOutcomePublisher : IWishgateOutcomePublisher
        {
            private readonly ConfigurableSaveService _fixture;

            public RecordingOutcomePublisher(ConfigurableSaveService fixture) { _fixture = fixture; }
            public List<WishgateCommittedOutcome> Payloads { get; } = new List<WishgateCommittedOutcome>();
            public List<int> SaveCountsAtPublish { get; } = new List<int>();
            public bool ThrowOnPublish { get; set; }

            public void Publish(WishgateCommittedOutcome payload)
            {
                Payloads.Add(payload);
                SaveCountsAtPublish.Add(_fixture.SaveCount);
                if (ThrowOnPublish) throw new InvalidOperationException("publisher unavailable");
            }
        }

        private sealed class ReentrantOutcomePublisher : IWishgateOutcomePublisher
        {
            public LocalRealmGemService Service { get; set; }
            public int PublishCount { get; private set; }
            public WishgateOutcomeDeliveryResult ReentrantResult { get; private set; }

            public void Publish(WishgateCommittedOutcome payload)
            {
                PublishCount++;
                ReentrantResult = Service.RecoverPendingWishgateOutcome();
            }
        }
    }
}
