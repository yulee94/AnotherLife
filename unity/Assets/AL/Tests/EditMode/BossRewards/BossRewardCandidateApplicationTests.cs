using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public sealed class BossRewardCandidateApplicationTests
    {
        private const string ProfileId = "profile_candidate_test";
        private const string EncounterId = "encounter_candidate_test";
        private const string CompletionId = "completion_candidate_test";
        private const string ResultId = "result_candidate_test";
        private const string SaveRevision = "save_revision_candidate_1";
        private const string EconomyRevision = "economy_revision_candidate_1";
        private const string InventoryRevision = "inventory_revision_candidate_1";
        private const string LedgerRevision = "ledger_revision_candidate_1";

        [Test]
        public void PinnedRepresentativeBossPreparesCandidateReceiptAndOutboxWithoutMutation()
        {
            BossRewardCandidateApplicationResult result =
                BossRewardCandidateApplication.Prepare(Request());

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.CandidatePrepared,
                result.Status);
            Assert.IsFalse(result.AllowsMutation);
            Assert.AreEqual(
                BossRewardSourceCatalog.MutationActivation,
                result.MutationActivation);
            Assert.IsNotNull(result.Plan);
            Assert.IsNotNull(result.Receipt);
            Assert.AreEqual(250, result.Plan.CreditOperation.Delta);
            Assert.AreEqual(1, result.Plan.InventoryOperations.Count);
            Assert.AreEqual(
                BossRewardSourceCatalog.RepresentativeEquipmentId,
                result.Plan.InventoryOperations[0].EquipmentDefinitionId);
            Assert.AreEqual(2, result.OutboxIntents.Count);
            Assert.AreEqual(250, result.Receipt.WarzoneCredits);
            Assert.AreEqual(1, result.Receipt.DropCount);
            Assert.AreEqual(ResultId, result.Receipt.RewardResultId);
            Assert.AreEqual(64, result.Receipt.PlanHash.Length);
            Assert.AreEqual(string.Empty, result.DiagnosticCode);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.OutboxIntents).Clear());
        }

        [Test]
        public void ExactLedgerReplayIsAlreadyCommittedWithoutNewOutbox()
        {
            BossRewardCandidateApplicationResult first =
                BossRewardCandidateApplication.Prepare(Request());
            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.CandidatePrepared,
                first.Status);

            BossRewardCandidateApplicationResult replay =
                BossRewardCandidateApplication.Prepare(
                    Request(ledger: Ledger(first.Plan.LedgerRecord)));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.AlreadyCommitted,
                replay.Status);
            Assert.IsNull(replay.Plan);
            Assert.IsNotNull(replay.Receipt);
            Assert.AreEqual(first.Receipt.ComputationHash, replay.Receipt.ComputationHash);
            Assert.AreEqual(0, replay.OutboxIntents.Count);
            Assert.IsFalse(replay.AllowsMutation);
            Assert.AreEqual(first.Plan.LedgerRecord.RewardResultId, replay.ExistingRecord.RewardResultId);
        }

        [Test]
        public void ConflictingResultIdentityFailsClosed()
        {
            BossRewardCandidateApplicationResult first =
                BossRewardCandidateApplication.Prepare(Request());
            BossRewardCandidateApplicationResult conflict =
                BossRewardCandidateApplication.Prepare(
                    Request(
                        rewardResultId: "result_other_test",
                        ledger: Ledger(first.Plan.LedgerRecord)));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.CorrelationConflict,
                conflict.Status);
            Assert.IsNull(conflict.Plan);
            Assert.IsNull(conflict.Receipt);
            Assert.AreEqual(0, conflict.OutboxIntents.Count);
            Assert.IsFalse(conflict.AllowsMutation);
        }

        [Test]
        public void UnknownBossDoesNotFabricateFallbackReward()
        {
            BossRewardCandidateApplicationResult result =
                BossRewardCandidateApplication.Prepare(
                    Request(bossDefinitionId: "boss_unknown_placeholder"));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.UnknownBoss,
                result.Status);
            Assert.IsNull(result.Plan);
            Assert.IsNull(result.Receipt);
            Assert.AreEqual(0, result.OutboxIntents.Count);
            Assert.AreEqual("AL-BOSS-REWARD-CANDIDATE-UNKNOWN-BOSS", result.DiagnosticCode);
        }

        [Test]
        public void MissingSourceFailsClosed()
        {
            BossRewardCandidateApplicationResult result =
                BossRewardCandidateApplication.Prepare(Request(missingSource: true));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.SourceUnavailable,
                result.Status);
            Assert.IsNull(result.Plan);
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

            BossRewardCandidateApplicationResult failed =
                BossRewardCandidateApplication.Prepare(Request(sourceBytes: sabotaged));
            BossRewardCandidateApplicationResult restored =
                BossRewardCandidateApplication.Prepare(Request(sourceBytes: original));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.CatalogRejected,
                failed.Status);
            Assert.IsNull(failed.Plan);
            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.CandidatePrepared,
                restored.Status);
            Assert.AreEqual(250, restored.Receipt.WarzoneCredits);
        }

        [Test]
        public void PendingRecoveryIsUncertainWithoutReceipt()
        {
            BossRewardCandidateApplicationResult first =
                BossRewardCandidateApplication.Prepare(Request());
            BossRewardAppliedLedgerRecord pending = CloneRecord(
                first.Plan.LedgerRecord,
                state: BossRewardLedgerRecordState.PendingRecovery);

            BossRewardCandidateApplicationResult uncertain =
                BossRewardCandidateApplication.Prepare(Request(ledger: Ledger(pending)));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.UncertainCommit,
                uncertain.Status);
            Assert.IsNull(uncertain.Plan);
            Assert.IsNull(uncertain.Receipt);
            Assert.AreEqual(0, uncertain.OutboxIntents.Count);
        }

        [Test]
        public void InvalidOpaqueIdentityFailsClosed()
        {
            BossRewardCandidateApplicationResult result =
                BossRewardCandidateApplication.Prepare(Request(rewardResultId: " "));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.InvalidRequest,
                result.Status);
            Assert.IsNull(result.Plan);
            Assert.IsNull(result.Receipt);
        }

        [Test]
        public void UnavailableSaveSnapshotIsPlanningRejected()
        {
            BossRewardCandidateApplicationResult result =
                BossRewardCandidateApplication.Prepare(Request(isSaveAvailable: false));

            Assert.AreEqual(
                BossRewardCandidateApplicationStatus.PlanningRejected,
                result.Status);
            Assert.IsNull(result.Plan);
            Assert.IsNull(result.Receipt);
            Assert.IsFalse(result.AllowsMutation);
        }

        [Test]
        public void IdenticalInputsProduceTheSamePlanHash()
        {
            BossRewardCandidateApplicationResult first =
                BossRewardCandidateApplication.Prepare(Request());
            BossRewardCandidateApplicationResult second =
                BossRewardCandidateApplication.Prepare(Request());

            Assert.AreEqual(first.Receipt.PlanHash, second.Receipt.PlanHash);
            Assert.AreEqual(first.Receipt.ComputationHash, second.Receipt.ComputationHash);
            CollectionAssert.AreEqual(
                first.OutboxIntents.Select(item => item.CorrelationId).ToArray(),
                second.OutboxIntents.Select(item => item.CorrelationId).ToArray());
        }

        [Test]
        public void PublicSurfaceExposesNoCommittedMutationStatus()
        {
            string[] names = Enum.GetNames(typeof(BossRewardCandidateApplicationStatus));
            CollectionAssert.DoesNotContain(names, "Committed");
            CollectionAssert.DoesNotContain(names, "ExplicitNoRewardCommitted");
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

        private static BossRewardAppliedLedgerRecord CloneRecord(
            BossRewardAppliedLedgerRecord source,
            string encounterId = null,
            string completionId = null,
            BossRewardLedgerRecordState? state = null)
        {
            return new BossRewardAppliedLedgerRecord(
                source.GameId,
                source.CatalogSetId,
                source.ProfileId,
                source.RewardResultId,
                encounterId ?? source.EncounterId,
                completionId ?? source.EncounterCompletionId,
                source.BossDefinitionId,
                source.BossDefinitionContentVersion,
                source.RewardProfileId,
                source.RewardProfileContentVersion,
                source.RewardProfileSha256,
                source.ComputationHash,
                source.WarzoneCredits,
                source.IsExplicitNoReward,
                source.DeterminismVersion,
                source.CommittedDrops,
                source.CommittedUtcSeconds,
                source.ApplicationPolicyVersion,
                source.NotificationCorrelationIds,
                state ?? source.State);
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
