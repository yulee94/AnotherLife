using System;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode.Encounter;
using AL.Core;
using AL.Core.SaveAuthority;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionEncounterConsequenceTests
    {
        [Test]
        public void AuthoritativeQuestVictoryAppliesOnceThroughRewardAndProfileAuthorities()
        {
            var reward = new RecordingRewardAuthority();
            var profile = new WritableProfileWrite();
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();
            ChampionEncounterConsequenceRequest request = AuthoritativeVictory();

            ChampionEncounterConsequencePlan first = ChampionEncounterConsequenceGateway.Apply(
                request,
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan replay = ChampionEncounterConsequenceGateway.Apply(
                request,
                reward,
                profile,
                commit,
                receipts);

            Assert.That(first.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.Applied));
            Assert.That(first.Receipt, Is.Not.Null);
            Assert.That(first.Receipt.RealmId, Is.EqualTo("stonehold"));
            Assert.That(first.Receipt.NvsCorrelationId, Is.EqualTo(request.NvsCorrelationId));
            Assert.That(first.Receipt.NvsQuestId, Is.EqualTo(request.NvsQuestId));
            Assert.That(first.Receipt.RewardResultId, Is.EqualTo("reward.op.stonehold.guardian"));
            Assert.That(first.Receipt.Mode, Is.EqualTo(ChampionEncounterMode.AuthoritativeQuest));
            Assert.That(replay.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.DuplicateExact));
            Assert.That(replay.Receipt, Is.SameAs(first.Receipt));
            Assert.That(reward.CallCount, Is.EqualTo(1));
            Assert.That(commit.CallCount, Is.EqualTo(1));
            Assert.That(commit.MutatedSave, Is.True);
            Assert.That(receipts.Count, Is.EqualTo(1));
        }

        [Test]
        public void ChangedResultIdentityReuseIsCorrelationConflict()
        {
            var reward = new RecordingRewardAuthority();
            var profile = new WritableProfileWrite();
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();
            ChampionEncounterConsequencePlan first = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(),
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan conflict = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(
                    nvsCorrelationId: "nvs.corr.conflicted",
                    rewardOperationId: "reward.op.conflicted"),
                reward,
                profile,
                commit,
                receipts);

            Assert.That(first.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.Applied));
            Assert.That(
                conflict.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.CorrelationConflict));
            Assert.That(
                conflict.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.CorrelationConflictCode));
            Assert.That(conflict.Receipt, Is.Null);
            Assert.That(reward.CallCount, Is.EqualTo(1));
            Assert.That(commit.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void PracticeAndFirstSessionLabeledPracticeSuppressDurableConsequences()
        {
            var reward = new RecordingRewardAuthority();
            var profile = new WritableProfileWrite();
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();

            ChampionEncounterConsequencePlan practice = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(
                    mode: ChampionEncounterMode.Practice,
                    labeledPractice: false),
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan labeled = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(
                    mode: ChampionEncounterMode.AuthoritativeQuest,
                    labeledPractice: true),
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan firstSession =
                ChampionEncounterConsequenceGateway.ApplyBossDefeat(
                    true,
                    RealmId.Stonehold,
                    null);

            Assert.That(
                practice.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.PracticeSuppressed));
            Assert.That(
                labeled.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.PracticeSuppressed));
            Assert.That(
                firstSession.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.PracticeSuppressed));
            Assert.That(
                practice.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.PracticeSuppressedCode));
            Assert.That(practice.Receipt, Is.Null);
            Assert.That(labeled.Receipt, Is.Null);
            Assert.That(firstSession.Receipt, Is.Null);
            Assert.That(reward.CallCount, Is.Zero);
            Assert.That(commit.CallCount, Is.Zero);
            Assert.That(commit.MutatedSave, Is.False);
            Assert.That(receipts, Is.Empty);
        }

        [Test]
        public void MissingInvalidUnavailableAuthoritiesFailClosedWithoutMutation()
        {
            var reward = new RecordingRewardAuthority();
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();
            ChampionEncounterConsequenceRequest request = AuthoritativeVictory();

            ChampionEncounterConsequencePlan missing = ChampionEncounterConsequenceGateway.Apply(
                request,
                null,
                new WritableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan unavailable = ChampionEncounterConsequenceGateway.Apply(
                request,
                reward,
                new UnavailableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan rewardFail = ChampionEncounterConsequenceGateway.Apply(
                request,
                new RecordingRewardAuthority
                {
                    Result = new ChampionEncounterBossRewardPlan(
                        ChampionEncounterBossRewardStatus.Unavailable,
                        string.Empty,
                        ChampionEncounterConsequenceGateway.RewardUnavailableCode)
                },
                new WritableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan commitFail = ChampionEncounterConsequenceGateway.Apply(
                request,
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                new RecordingCommit { Accept = false },
                receipts);

            Assert.That(
                missing.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.InvalidDependency));
            Assert.That(
                unavailable.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.ProfileWriteUnavailable));
            Assert.That(
                rewardFail.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.RewardAuthorityUnavailable));
            Assert.That(
                commitFail.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.ApplicationRejected));
            Assert.That(missing.Receipt, Is.Null);
            Assert.That(unavailable.Receipt, Is.Null);
            Assert.That(rewardFail.Receipt, Is.Null);
            Assert.That(commitFail.Receipt, Is.Null);
            Assert.That(commit.MutatedSave, Is.False);
            Assert.That(receipts, Is.Empty);
        }

        [Test]
        public void NonQuestModesUncommittedRealmAndMissingNvsRejectWithoutMutation()
        {
            var reward = new RecordingRewardAuthority();
            var profile = new WritableProfileWrite();
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();

            ChampionEncounterConsequencePlan boss = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(mode: ChampionEncounterMode.AuthoritativeBoss),
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan demo = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(mode: ChampionEncounterMode.DevelopmentDemo),
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan uncommitted = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(realmId: string.Empty),
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan missingNvs = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(nvsCorrelationId: string.Empty),
                reward,
                profile,
                commit,
                receipts);
            ChampionEncounterConsequencePlan harness =
                ChampionEncounterConsequenceGateway.ApplyBossDefeat(
                    false,
                    RealmId.Stonehold,
                    null);

            Assert.That(boss.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.ModeRejected));
            Assert.That(demo.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.ModeRejected));
            Assert.That(
                uncommitted.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.InvalidInput));
            Assert.That(
                missingNvs.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.InvalidInput));
            Assert.That(
                missingNvs.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.NvsIdentityInvalidCode));
            Assert.That(
                harness.Status,
                Is.EqualTo(ChampionEncounterConsequenceStatus.InvalidInput));
            Assert.That(reward.CallCount, Is.Zero);
            Assert.That(commit.CallCount, Is.Zero);
            Assert.That(commit.MutatedSave, Is.False);
        }

        [Test]
        public void AuthoritativeQuestDefeatCommitsResultWithoutIssuingRewards()
        {
            var reward = new RecordingRewardAuthority();
            var profile = new WritableProfileWrite();
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();
            ChampionEncounterConsequenceRequest request = AuthoritativeVictory(
                outcome: ChampionEncounterConsequenceOutcome.ChampionDefeat,
                rewardOperationId: string.Empty);

            ChampionEncounterConsequencePlan plan = ChampionEncounterConsequenceGateway.Apply(
                request,
                reward,
                profile,
                commit,
                receipts);

            Assert.That(plan.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.Applied));
            Assert.That(plan.Receipt.Outcome, Is.EqualTo(ChampionEncounterConsequenceOutcome.ChampionDefeat));
            Assert.That(plan.Receipt.RewardResultId, Is.EqualTo(string.Empty));
            Assert.That(plan.Receipt.NvsCorrelationId, Is.EqualTo(request.NvsCorrelationId));
            Assert.That(reward.CallCount, Is.Zero);
            Assert.That(commit.CallCount, Is.EqualTo(1));
            Assert.That(commit.LastCandidate.RewardPlan, Is.Null);
        }

        [Test]
        public void ProductionSourcesStayFailClosedAndDoNotPresentC5()
        {
            string gateway = ReadSource("Encounter", "ChampionEncounterConsequenceGateway.cs");
            string controller = ReadSource("ChampionArenaSceneController.cs");
            string firstFight = ReadSource("FirstFightCatalog.cs");

            Assert.That(gateway, Does.Contain("IProfileWriteAuthorityProvider"));
            Assert.That(gateway, Does.Contain("IChampionEncounterBossRewardAuthority"));
            Assert.That(gateway, Does.Not.Contain("ISaveGameService"));
            Assert.That(gateway, Does.Not.Contain("LocalBossLootService"));
            Assert.That(gateway, Does.Not.Contain("IBossLootService"));
            Assert.That(controller, Does.Contain("ChampionEncounterConsequenceGateway.ApplyBossDefeat"));
            Assert.That(controller, Does.Not.Contain("LootRolled"));
            Assert.That(firstFight, Does.Not.Contain("AuthoritativeQuest"));
            Assert.That(gateway, Does.Contain("Presentation remains C5"));
        }

        private static ChampionEncounterConsequenceRequest AuthoritativeVictory(
            ChampionEncounterMode mode = ChampionEncounterMode.AuthoritativeQuest,
            ChampionEncounterConsequenceOutcome outcome =
                ChampionEncounterConsequenceOutcome.ChampionVictory,
            string realmId = "stonehold",
            string nvsCorrelationId = "nvs.corr.stonehold.guardian",
            string rewardOperationId = "reward.op.stonehold.guardian",
            bool labeledPractice = false)
        {
            return new ChampionEncounterConsequenceRequest(
                "result.stonehold.guardian",
                "encounter.stonehold.guardian",
                "attempt.stonehold.guardian",
                mode,
                outcome,
                realmId,
                nvsCorrelationId,
                "quest.nvs.stonehold",
                rewardOperationId,
                "source-fingerprint-stonehold",
                WritableProfileWrite.ProfileId,
                labeledPractice);
        }

        private static string ReadSource(params string[] parts)
        {
            var segments = new List<string>
            {
                Application.dataPath,
                "AL",
                "Scripts",
                "ChampionMode"
            };
            segments.AddRange(parts);
            string path = Path.Combine(segments.ToArray());
            Assert.That(File.Exists(path), Is.True, path);
            return File.ReadAllText(path);
        }

        private sealed class RecordingRewardAuthority : IChampionEncounterBossRewardAuthority
        {
            public int CallCount { get; private set; }

            public ChampionEncounterBossRewardPlan Result { get; set; } =
                new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.Issued,
                    "reward.op.stonehold.guardian",
                    string.Empty);

            public ChampionEncounterBossRewardPlan Plan(
                ChampionEncounterConsequenceRequest request)
            {
                CallCount++;
                return Result;
            }
        }

        private sealed class RecordingCommit : IChampionEncounterProfileCommit
        {
            public int CallCount { get; private set; }
            public bool MutatedSave { get; private set; }
            public bool Accept { get; set; } = true;
            public ChampionEncounterConsequenceCandidate LastCandidate { get; private set; }

            public bool TryCommit(ChampionEncounterConsequenceCandidate candidate)
            {
                CallCount++;
                LastCandidate = candidate;
                if (!Accept)
                {
                    return false;
                }

                MutatedSave = true;
                return true;
            }
        }

        private sealed class WritableProfileWrite : IProfileWriteAuthorityProvider
        {
            public const string ProfileId = "alp_0123456789abcdef0123456789abcdef";

            private static readonly ProfileWriteAuthoritySnapshot Snapshot =
                ProfileWriteAuthoritySnapshotFactory.Writable(
                    ProfileId,
                    "0123456789abcdef0000000000000001",
                    new string('a', SaveAuthorityTechnicalLimits.Sha256Characters),
                    ProfileAuthoritySourceGeneration.Primary,
                    Array.Empty<string>());

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority() => Snapshot;
        }

        private sealed class UnavailableProfileWrite : IProfileWriteAuthorityProvider
        {
            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                    SaveAuthorityDiagnosticCodes.ProviderMissing);
            }
        }
    }
}
