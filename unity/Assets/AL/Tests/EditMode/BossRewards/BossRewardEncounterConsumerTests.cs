using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public sealed class BossRewardEncounterConsumerTests
    {
        private const string ProfileId = "profile_encounter_consumer_test";
        private const string EncounterId = "encounter_consumer_test";
        private const string CompletionId = "completion_consumer_test";
        private const string ResultId = "result_consumer_test";
        private const string SaveRevision = "save_revision_consumer_1";
        private const string EconomyRevision = "economy_revision_consumer_1";
        private const string InventoryRevision = "inventory_revision_consumer_1";
        private const string LedgerRevision = "ledger_revision_consumer_1";

        [Test]
        public void PinnedRepresentativeBossDefeatPreparesWithoutCommittedMutationOrFabricatedFallback()
        {
            BossRewardEncounterConsumerResult result =
                BossRewardEncounterConsumer.Consume(Request());

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Prepared, result.Status);
            Assert.IsFalse(result.AllowsMutation);
            Assert.AreEqual(
                BossRewardSourceCatalog.MutationActivation,
                result.MutationActivation);
            Assert.IsNotNull(result.Receipt);
            Assert.AreEqual(250, result.Receipt.WarzoneCredits);
            Assert.AreEqual(1, result.Receipt.DropCount);
            Assert.AreEqual(ResultId, result.Receipt.RewardResultId);
            Assert.AreEqual(
                BossRewardSourceCatalog.RepresentativeBossId,
                result.Receipt.BossDefinitionId);
            Assert.AreEqual(string.Empty, result.DiagnosticCode);
            Assert.AreEqual(2, result.OutboxIntents.Count);
            StringAssert.DoesNotContain("ember_crown_shard", result.Receipt.RewardResultId);
            StringAssert.DoesNotContain("Ember Crown", result.DiagnosticCode);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.OutboxIntents).Clear());
        }

        [Test]
        public void UnknownBossIsUnavailableWithoutFabricatedFallback()
        {
            BossRewardEncounterConsumerResult result =
                BossRewardEncounterConsumer.Consume(
                    Request(bossDefinitionId: "boss_unknown_placeholder"));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Unavailable, result.Status);
            Assert.IsNull(result.Receipt);
            Assert.AreEqual(0, result.OutboxIntents.Count);
            Assert.AreEqual(
                BossRewardCandidateApplication.UnknownBossCode,
                result.DiagnosticCode);
            Assert.IsFalse(result.AllowsMutation);
        }

        [Test]
        public void ExactLedgerReplayIsDuplicateWithoutNewOutbox()
        {
            BossRewardEncounterConsumerResult first =
                BossRewardEncounterConsumer.Consume(Request());
            Assert.AreEqual(BossRewardEncounterConsumerStatus.Prepared, first.Status);

            BossRewardEncounterConsumerResult replay =
                BossRewardEncounterConsumer.Consume(
                    Request(ledger: LedgerFromPrepared(first)));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Duplicate, replay.Status);
            Assert.IsNotNull(replay.Receipt);
            Assert.AreEqual(first.Receipt.ComputationHash, replay.Receipt.ComputationHash);
            Assert.AreEqual(0, replay.OutboxIntents.Count);
            Assert.IsFalse(replay.AllowsMutation);
        }

        [Test]
        public void ConflictingResultIdentityFailsClosed()
        {
            BossRewardEncounterConsumerResult first =
                BossRewardEncounterConsumer.Consume(Request());
            BossRewardEncounterConsumerResult conflict =
                BossRewardEncounterConsumer.Consume(
                    Request(
                        rewardResultId: "result_other_test",
                        ledger: LedgerFromPrepared(first)));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Failed, conflict.Status);
            Assert.IsNull(conflict.Receipt);
            Assert.AreEqual(0, conflict.OutboxIntents.Count);
            Assert.IsFalse(conflict.AllowsMutation);
        }

        [Test]
        public void MissingSourceIsUnavailable()
        {
            BossRewardEncounterConsumerResult result =
                BossRewardEncounterConsumer.Consume(Request(missingSource: true));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Unavailable, result.Status);
            Assert.IsNull(result.Receipt);
            Assert.IsFalse(result.AllowsMutation);
        }

        [Test]
        public void PendingRecoveryFailsClosedWithoutReceipt()
        {
            BossRewardEncounterConsumerResult first =
                BossRewardEncounterConsumer.Consume(Request());
            BossRewardAppliedLedgerRecord pending = CloneRecord(
                BossRewardLedgerRecordState.PendingRecovery);

            BossRewardEncounterConsumerResult uncertain =
                BossRewardEncounterConsumer.Consume(Request(ledger: Ledger(pending)));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Failed, uncertain.Status);
            Assert.IsNull(uncertain.Receipt);
            Assert.AreEqual(0, uncertain.OutboxIntents.Count);
        }

        [Test]
        public void InvalidOpaqueIdentityFailsClosed()
        {
            BossRewardEncounterConsumerResult result =
                BossRewardEncounterConsumer.Consume(Request(rewardResultId: " "));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Failed, result.Status);
            Assert.IsNull(result.Receipt);
        }

        [Test]
        public void UnavailableSaveSnapshotFailsClosed()
        {
            BossRewardEncounterConsumerResult result =
                BossRewardEncounterConsumer.Consume(Request(isSaveAvailable: false));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Failed, result.Status);
            Assert.IsNull(result.Receipt);
            Assert.IsFalse(result.AllowsMutation);
        }

        [Test]
        public void PinnedSabotageFailsThenRestorePrepares()
        {
            byte[] original = ReadCatalogBytes();
            byte[] sabotaged = Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(original).Replace(
                    BossRewardSourceCatalog.RepresentativeBossId,
                    "boss_stonehold_fault_crowned_colossuX"));

            BossRewardEncounterConsumerResult failed =
                BossRewardEncounterConsumer.Consume(Request(sourceBytes: sabotaged));
            BossRewardEncounterConsumerResult restored =
                BossRewardEncounterConsumer.Consume(Request(sourceBytes: original));

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Failed, failed.Status);
            Assert.IsNull(failed.Receipt);
            Assert.AreEqual(BossRewardEncounterConsumerStatus.Prepared, restored.Status);
            Assert.AreEqual(250, restored.Receipt.WarzoneCredits);
        }

        [Test]
        public void NullRequestFailsClosed()
        {
            BossRewardEncounterConsumerResult result =
                BossRewardEncounterConsumer.Consume(null);

            Assert.AreEqual(BossRewardEncounterConsumerStatus.Failed, result.Status);
            Assert.IsNull(result.Receipt);
            Assert.AreEqual(0, result.OutboxIntents.Count);
            Assert.IsFalse(result.AllowsMutation);
        }

        [Test]
        public void PublicSurfaceExposesNoCommittedMutationStatus()
        {
            string[] names = Enum.GetNames(typeof(BossRewardEncounterConsumerStatus));
            CollectionAssert.DoesNotContain(names, "Committed");
            CollectionAssert.DoesNotContain(names, "CandidatePrepared");
            CollectionAssert.AreEquivalent(
                new[] { "Prepared", "Duplicate", "NoReward", "Unavailable", "Failed" },
                names);
        }

        private static BossRewardCandidateApplicationRequest Request(
            byte[] sourceBytes = null,
            string profileId = ProfileId,
            string encounterId = EncounterId,
            string encounterCompletionId = CompletionId,
            string rewardResultId = ResultId,
            string bossDefinitionId = BossRewardSourceCatalog.RepresentativeBossId,
            bool requirePinnedSource = true,
            bool isSaveAvailable = true,
            bool missingSource = false,
            BossRewardLedgerSnapshot ledger = null)
        {
            return new BossRewardCandidateApplicationRequest(
                missingSource ? null : (sourceBytes ?? ReadCatalogBytes()),
                profileId,
                encounterId,
                encounterCompletionId,
                rewardResultId,
                bossDefinitionId,
                requirePinnedSource,
                isSaveAvailable,
                SaveRevision,
                EconomyRevision,
                InventoryRevision,
                LedgerRevision,
                new BossRewardEconomySnapshot(true, 10, int.MaxValue, EconomyRevision),
                Array.Empty<OwnedEquipmentSnapshot>(),
                ledger ?? EmptyLedger(),
                null,
                100);
        }

        private static BossRewardLedgerSnapshot EmptyLedger()
        {
            return new BossRewardLedgerSnapshot(
                BossRewardSourceCatalog.GameId,
                ProfileId,
                BossRewardLedgerStatus.Empty,
                LedgerRevision,
                Array.Empty<BossRewardAppliedLedgerRecord>(),
                Array.Empty<BossRewardDiagnostic>(),
                true);
        }

        private static BossRewardLedgerSnapshot Ledger(BossRewardAppliedLedgerRecord record)
        {
            return new BossRewardLedgerSnapshot(
                BossRewardSourceCatalog.GameId,
                ProfileId,
                BossRewardLedgerStatus.Valid,
                LedgerRevision,
                new[] { record },
                Array.Empty<BossRewardDiagnostic>(),
                true);
        }

        private static BossRewardLedgerSnapshot LedgerFromPrepared(
            BossRewardEncounterConsumerResult prepared)
        {
            BossRewardCandidateApplicationResult candidate =
                BossRewardCandidateApplication.Prepare(Request());
            return Ledger(candidate.Plan.LedgerRecord);
        }

        private static BossRewardAppliedLedgerRecord CloneRecord(
            BossRewardLedgerRecordState state)
        {
            BossRewardCandidateApplicationResult candidate =
                BossRewardCandidateApplication.Prepare(Request());
            BossRewardAppliedLedgerRecord record = candidate.Plan.LedgerRecord;
            return new BossRewardAppliedLedgerRecord(
                record.GameId,
                record.CatalogSetId,
                record.ProfileId,
                record.RewardResultId,
                record.EncounterId,
                record.EncounterCompletionId,
                record.BossDefinitionId,
                record.BossDefinitionContentVersion,
                record.RewardProfileId,
                record.RewardProfileContentVersion,
                record.RewardProfileSha256,
                record.ComputationHash,
                record.WarzoneCredits,
                record.IsExplicitNoReward,
                record.DeterminismVersion,
                record.CommittedDrops,
                record.CommittedUtcSeconds,
                record.ApplicationPolicyVersion,
                record.NotificationCorrelationIds,
                state);
        }

        private static byte[] ReadCatalogBytes()
        {
            string[] candidates =
            {
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets",
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    "al_boss_reward_source_catalog.json"),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "unity",
                    "Assets",
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    "al_boss_reward_source_catalog.json")
            };
            for (int index = 0; index < candidates.Length; index++)
            {
                if (File.Exists(candidates[index]))
                    return File.ReadAllBytes(candidates[index]);
            }

            Assert.Fail("boss reward source catalog was not found");
            return Array.Empty<byte>();
        }
    }
}
