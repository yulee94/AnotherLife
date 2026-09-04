using System;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode.Encounter;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionEncounterBossRewardCandidateAuthorityTests
    {
        private const string ProfileId = "profile_encounter_consumer_test";
        private const string EncounterId = "encounter_consumer_test";
        private const string AttemptId = "completion_consumer_test";
        private const string ResultId = "result_consumer_test";
        private const string SaveRevision = "save_revision_consumer_1";
        private const string EconomyRevision = "economy_revision_consumer_1";
        private const string InventoryRevision = "inventory_revision_consumer_1";
        private const string LedgerRevision = "ledger_revision_consumer_1";

        [Test]
        public void PinnedRepresentativeVictoryIssuesTypedPlanWithoutMutationOrFabricatedFallback()
        {
            var authority = CreateAuthority();
            ChampionEncounterBossRewardPlan plan = authority.Plan(VictoryRequest());

            Assert.AreEqual(ChampionEncounterBossRewardStatus.Issued, plan.Status);
            Assert.AreEqual(ResultId, plan.RewardResultId);
            Assert.AreEqual(string.Empty, plan.DiagnosticCode);
            StringAssert.DoesNotContain("ember_crown_shard", plan.RewardResultId);
            StringAssert.DoesNotContain("Ember Crown", plan.DiagnosticCode);
        }

        [Test]
        public void UnknownBossIsUnavailableWithoutFabricatedReward()
        {
            var authority = CreateAuthority(
                bossDefinitionId: "boss_unknown_placeholder");
            ChampionEncounterBossRewardPlan plan = authority.Plan(VictoryRequest());

            Assert.AreEqual(ChampionEncounterBossRewardStatus.Unavailable, plan.Status);
            Assert.AreEqual(string.Empty, plan.RewardResultId);
            Assert.AreEqual(
                BossRewardCandidateApplication.UnknownBossCode,
                plan.DiagnosticCode);
        }

        [Test]
        public void DuplicateExactReplayDoesNotFabricateASecondReward()
        {
            var firstAuthority = CreateAuthority();
            ChampionEncounterBossRewardPlan first = firstAuthority.Plan(VictoryRequest());
            Assert.AreEqual(ChampionEncounterBossRewardStatus.Issued, first.Status);

            BossRewardCandidateApplicationResult prepared =
                BossRewardCandidateApplication.Prepare(CandidateRequest());
            var replayAuthority = CreateAuthority(ledger: Ledger(prepared.Plan.LedgerRecord));
            ChampionEncounterBossRewardPlan replay = replayAuthority.Plan(VictoryRequest());

            Assert.AreEqual(ChampionEncounterBossRewardStatus.DuplicateExact, replay.Status);
            Assert.AreEqual(first.RewardResultId, replay.RewardResultId);
        }

        [Test]
        public void CorrelationConflictFailsClosed()
        {
            BossRewardCandidateApplicationResult prepared =
                BossRewardCandidateApplication.Prepare(CandidateRequest());
            var authority = CreateAuthority(ledger: Ledger(prepared.Plan.LedgerRecord));
            ChampionEncounterBossRewardPlan plan = authority.Plan(
                VictoryRequest(rewardOperationId: "result_other_test"));

            Assert.AreEqual(
                ChampionEncounterBossRewardStatus.CorrelationConflict,
                plan.Status);
            Assert.AreEqual(string.Empty, plan.RewardResultId);
        }

        [Test]
        public void NullAndMissingSourceFailClosed()
        {
            var authority = CreateAuthority();
            ChampionEncounterBossRewardPlan missing = authority.Plan(null);
            var unavailable = CreateAuthority(missingSource: true);
            ChampionEncounterBossRewardPlan source = unavailable.Plan(VictoryRequest());

            Assert.AreEqual(ChampionEncounterBossRewardStatus.Invalid, missing.Status);
            Assert.AreEqual(ChampionEncounterBossRewardStatus.Unavailable, source.Status);
            Assert.AreEqual(string.Empty, missing.RewardResultId);
            Assert.AreEqual(string.Empty, source.RewardResultId);
        }

        [Test]
        public void ProductionAuthorityDoesNotReferenceLegacyLootFallback()
        {
            string source = ReadAuthoritySource();
            Assert.That(source, Does.Contain("BossRewardEncounterConsumer.Consume"));
            Assert.That(source, Does.Not.Contain("IBossLootService"));
            Assert.That(source, Does.Not.Contain("LocalBossLootService"));
            Assert.That(source, Does.Not.Contain("ember_crown_shard"));
            Assert.That(source, Does.Not.Contain("AddCredits"));
        }

        private static ChampionEncounterBossRewardCandidateAuthority CreateAuthority(
            string bossDefinitionId = BossRewardSourceCatalog.RepresentativeBossId,
            bool missingSource = false,
            BossRewardLedgerSnapshot ledger = null)
        {
            return new ChampionEncounterBossRewardCandidateAuthority(
                missingSource ? null : ReadCatalogBytes(),
                bossDefinitionId,
                true,
                SaveRevision,
                EconomyRevision,
                InventoryRevision,
                LedgerRevision,
                new BossRewardEconomySnapshot(true, 10, int.MaxValue, EconomyRevision),
                Array.Empty<OwnedEquipmentSnapshot>(),
                ledger ?? EmptyLedger(),
                100);
        }

        private static ChampionEncounterConsequenceRequest VictoryRequest(
            string rewardOperationId = ResultId)
        {
            return new ChampionEncounterConsequenceRequest(
                ResultId,
                EncounterId,
                AttemptId,
                ChampionEncounterMode.AuthoritativeQuest,
                ChampionEncounterConsequenceOutcome.ChampionVictory,
                "stonehold",
                "nvs.corr.consumer",
                "quest.nvs.consumer",
                rewardOperationId,
                "source-fingerprint-consumer",
                ProfileId,
                false);
        }

        private static BossRewardCandidateApplicationRequest CandidateRequest()
        {
            return new BossRewardCandidateApplicationRequest(
                ReadCatalogBytes(),
                ProfileId,
                EncounterId,
                AttemptId,
                ResultId,
                BossRewardSourceCatalog.RepresentativeBossId,
                true,
                true,
                SaveRevision,
                EconomyRevision,
                InventoryRevision,
                LedgerRevision,
                new BossRewardEconomySnapshot(true, 10, int.MaxValue, EconomyRevision),
                Array.Empty<OwnedEquipmentSnapshot>(),
                EmptyLedger(),
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

        private static string ReadAuthoritySource()
        {
            string[] candidates =
            {
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets",
                    "AL",
                    "Scripts",
                    "ChampionMode",
                    "Encounter",
                    "ChampionEncounterBossRewardCandidateAuthority.cs"),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "unity",
                    "Assets",
                    "AL",
                    "Scripts",
                    "ChampionMode",
                    "Encounter",
                    "ChampionEncounterBossRewardCandidateAuthority.cs")
            };
            for (int index = 0; index < candidates.Length; index++)
            {
                if (File.Exists(candidates[index]))
                    return File.ReadAllText(candidates[index]);
            }

            Assert.Fail("champion boss-reward candidate authority was not found");
            return string.Empty;
        }
    }
}
