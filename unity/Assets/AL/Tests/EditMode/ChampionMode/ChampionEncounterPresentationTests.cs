using System;
using System.Collections.Generic;
using System.IO;
using AL.ChampionMode.Encounter;
using AL.Core.SaveAuthority;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionEncounterPresentationTests
    {
        [Test]
        public void ReceiptBackedVictoryPresentsClearWithNvsRealmAndRewardIdentity()
        {
            var commit = new RecordingCommit();
            ChampionEncounterConsequencePlan applied = ApplyAuthoritative(
                AuthoritativeVictory(),
                commit);
            ChampionEncounterPresentationPlan presented =
                ChampionEncounterPresentationGateway.Present(applied);

            Assert.That(applied.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.Applied));
            Assert.That(
                presented.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Clear));
            Assert.That(presented.Receipt, Is.SameAs(applied.Receipt));
            Assert.That(presented.RealmId, Is.EqualTo("stonehold"));
            Assert.That(
                presented.NvsCorrelationId,
                Is.EqualTo("nvs.corr.stonehold.guardian"));
            Assert.That(presented.NvsQuestId, Is.EqualTo("quest.nvs.stonehold"));
            Assert.That(
                presented.RewardResultId,
                Is.EqualTo("reward.op.stonehold.guardian"));
            Assert.That(presented.VisiblyPractice, Is.False);
            Assert.That(presented.ShowsCommittedReward, Is.True);
            Assert.That(presented.ShowsCommittedProgression, Is.True);
            Assert.That(presented.DiagnosticCode, Is.EqualTo(string.Empty));
            Assert.That(commit.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateExactReplayPresentsTheCommittedReceiptWithoutMutation()
        {
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();
            ChampionEncounterConsequenceRequest request = AuthoritativeVictory();
            ChampionEncounterConsequencePlan first = ChampionEncounterConsequenceGateway.Apply(
                request,
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan replay = ChampionEncounterConsequenceGateway.Apply(
                request,
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterPresentationPlan presented =
                ChampionEncounterPresentationGateway.Present(replay);

            Assert.That(replay.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.DuplicateExact));
            Assert.That(
                presented.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Clear));
            Assert.That(presented.Receipt, Is.SameAs(first.Receipt));
            Assert.That(presented.NvsCorrelationId, Is.EqualTo(first.Receipt.NvsCorrelationId));
            Assert.That(presented.RealmId, Is.EqualTo(first.Receipt.RealmId));
            Assert.That(commit.CallCount, Is.EqualTo(1));
            Assert.That(receipts.Count, Is.EqualTo(1));
        }

        [Test]
        public void PracticeAndFirstSessionLabeledPracticeStayVisiblyPracticeWithoutCommittedValue()
        {
            var commit = new RecordingCommit();
            ChampionEncounterConsequencePlan practice = ApplyAuthoritative(
                AuthoritativeVictory(
                    mode: ChampionEncounterMode.Practice,
                    labeledPractice: false),
                commit);
            ChampionEncounterConsequencePlan labeled = ApplyAuthoritative(
                AuthoritativeVictory(
                    mode: ChampionEncounterMode.AuthoritativeQuest,
                    labeledPractice: true),
                commit);
            ChampionEncounterConsequencePlan firstSession =
                ChampionEncounterConsequenceGateway.ApplyBossDefeat(
                    true,
                    AL.Core.RealmId.Stonehold,
                    null);

            ChampionEncounterPresentationPlan practiceView =
                ChampionEncounterPresentationGateway.Present(practice);
            ChampionEncounterPresentationPlan labeledView =
                ChampionEncounterPresentationGateway.Present(labeled);
            ChampionEncounterPresentationPlan firstSessionView =
                ChampionEncounterPresentationGateway.Present(firstSession);

            Assert.That(
                practiceView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Practice));
            Assert.That(
                labeledView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Practice));
            Assert.That(
                firstSessionView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Practice));
            Assert.That(practiceView.VisiblyPractice, Is.True);
            Assert.That(labeledView.VisiblyPractice, Is.True);
            Assert.That(firstSessionView.VisiblyPractice, Is.True);
            Assert.That(practiceView.ShowsCommittedReward, Is.False);
            Assert.That(labeledView.ShowsCommittedReward, Is.False);
            Assert.That(firstSessionView.ShowsCommittedReward, Is.False);
            Assert.That(practiceView.ShowsCommittedProgression, Is.False);
            Assert.That(labeledView.ShowsCommittedProgression, Is.False);
            Assert.That(firstSessionView.ShowsCommittedProgression, Is.False);
            Assert.That(practiceView.Receipt, Is.Null);
            Assert.That(practiceView.RewardResultId, Is.EqualTo(string.Empty));
            Assert.That(
                practiceView.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.PracticeSuppressedCode));
            Assert.That(commit.CallCount, Is.Zero);
            Assert.That(commit.MutatedSave, Is.False);
        }

        [Test]
        public void MissingInvalidUnavailableAuthorityPresentsTypedUnavailableWithoutMutation()
        {
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
                new RecordingRewardAuthority(),
                new UnavailableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan uncommitted = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(realmId: string.Empty),
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan missingNvs = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(nvsCorrelationId: string.Empty),
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                commit,
                receipts);

            ChampionEncounterPresentationPlan missingView =
                ChampionEncounterPresentationGateway.Present(missing);
            ChampionEncounterPresentationPlan unavailableView =
                ChampionEncounterPresentationGateway.Present(unavailable);
            ChampionEncounterPresentationPlan uncommittedView =
                ChampionEncounterPresentationGateway.Present(uncommitted);
            ChampionEncounterPresentationPlan missingNvsView =
                ChampionEncounterPresentationGateway.Present(missingNvs);
            ChampionEncounterPresentationPlan pendingView =
                ChampionEncounterPresentationGateway.Present(null);

            Assert.That(
                missingView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Unavailable));
            Assert.That(
                unavailableView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Unavailable));
            Assert.That(
                uncommittedView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Unavailable));
            Assert.That(
                missingNvsView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Unavailable));
            Assert.That(
                pendingView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Pending));
            Assert.That(
                missingView.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.InvalidDependencyCode));
            Assert.That(
                unavailableView.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.ProfileWriteUnavailableCode));
            Assert.That(
                missingNvsView.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.NvsIdentityInvalidCode));
            Assert.That(
                pendingView.DiagnosticCode,
                Is.EqualTo(ChampionEncounterPresentationGateway.PendingCode));
            Assert.That(missingView.ShowsCommittedReward, Is.False);
            Assert.That(unavailableView.ShowsCommittedReward, Is.False);
            Assert.That(pendingView.ShowsCommittedReward, Is.False);
            Assert.That(missingView.ShowsCommittedProgression, Is.False);
            Assert.That(pendingView.VisiblyPractice, Is.False);
            Assert.That(missingView.Receipt, Is.Null);
            Assert.That(pendingView.Receipt, Is.Null);
            Assert.That(commit.MutatedSave, Is.False);
            Assert.That(receipts, Is.Empty);
        }

        [Test]
        public void AuthoritativeDefeatPresentsDefeatWithoutCommittedReward()
        {
            var commit = new RecordingCommit();
            ChampionEncounterConsequencePlan applied = ApplyAuthoritative(
                AuthoritativeVictory(
                    outcome: ChampionEncounterConsequenceOutcome.ChampionDefeat,
                    rewardOperationId: string.Empty),
                commit);
            ChampionEncounterPresentationPlan presented =
                ChampionEncounterPresentationGateway.Present(applied);

            Assert.That(
                presented.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Defeat));
            Assert.That(
                presented.Receipt.Outcome,
                Is.EqualTo(ChampionEncounterConsequenceOutcome.ChampionDefeat));
            Assert.That(presented.ShowsCommittedReward, Is.False);
            Assert.That(presented.ShowsCommittedProgression, Is.True);
            Assert.That(presented.VisiblyPractice, Is.False);
            Assert.That(presented.RewardResultId, Is.EqualTo(string.Empty));
            Assert.That(presented.NvsCorrelationId, Is.EqualTo("nvs.corr.stonehold.guardian"));
            Assert.That(presented.RealmId, Is.EqualTo("stonehold"));
            Assert.That(commit.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void CorrelationConflictAndRejectedApplicationPresentFailureWithoutRewards()
        {
            var commit = new RecordingCommit();
            var receipts = new List<ChampionEncounterConsequenceReceipt>();
            ChampionEncounterConsequencePlan first = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(),
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan conflict = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(
                    nvsCorrelationId: "nvs.corr.conflicted",
                    rewardOperationId: "reward.op.conflicted"),
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                commit,
                receipts);
            ChampionEncounterConsequencePlan rejected = ChampionEncounterConsequenceGateway.Apply(
                AuthoritativeVictory(encounterResultId: "result.stonehold.rejected"),
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                new RecordingCommit { Accept = false },
                receipts);

            ChampionEncounterPresentationPlan conflictView =
                ChampionEncounterPresentationGateway.Present(conflict);
            ChampionEncounterPresentationPlan rejectedView =
                ChampionEncounterPresentationGateway.Present(rejected);

            Assert.That(first.Status, Is.EqualTo(ChampionEncounterConsequenceStatus.Applied));
            Assert.That(
                conflictView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Failure));
            Assert.That(
                rejectedView.Status,
                Is.EqualTo(ChampionEncounterPresentationStatus.Failure));
            Assert.That(conflictView.ShowsCommittedReward, Is.False);
            Assert.That(rejectedView.ShowsCommittedReward, Is.False);
            Assert.That(conflictView.ShowsCommittedProgression, Is.False);
            Assert.That(conflictView.Receipt, Is.Null);
            Assert.That(
                conflictView.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.CorrelationConflictCode));
            Assert.That(
                rejectedView.DiagnosticCode,
                Is.EqualTo(ChampionEncounterConsequenceGateway.ApplicationRejectedCode));
        }

        [Test]
        public void ProductionHudBindsPresentationToC4ReceiptsNotBossNullOrLoot()
        {
            string gateway = ReadSource("Encounter", "ChampionEncounterPresentationGateway.cs");
            string controller = ReadSource("ChampionArenaSceneController.cs");

            Assert.That(gateway, Does.Contain("ChampionEncounterConsequencePlan"));
            Assert.That(gateway, Does.Not.Contain("ISaveGameService"));
            Assert.That(gateway, Does.Not.Contain("LocalBossLootService"));
            Assert.That(gateway, Does.Not.Contain("IBossLootService"));
            Assert.That(gateway, Does.Not.Contain("BossLootResult"));
            Assert.That(controller, Does.Contain("ChampionEncounterPresentationGateway.Present"));
            Assert.That(controller, Does.Contain("AuthoritativeEncounterPresentationPlan"));
            Assert.That(controller, Does.Not.Contain("_lastBossLootResult"));
            Assert.That(controller, Does.Not.Contain("LootRolled"));
            Assert.That(controller, Does.Not.Contain("Loot roll complete"));
            Assert.That(controller, Does.Not.Contain("_boss == null || _boss.IsDead"));
            Assert.That(controller, Does.Not.Contain("WARZONE CREDITS +"));
        }

        private static ChampionEncounterConsequencePlan ApplyAuthoritative(
            ChampionEncounterConsequenceRequest request,
            RecordingCommit commit)
        {
            return ChampionEncounterConsequenceGateway.Apply(
                request,
                new RecordingRewardAuthority(),
                new WritableProfileWrite(),
                commit,
                new List<ChampionEncounterConsequenceReceipt>());
        }

        private static ChampionEncounterConsequenceRequest AuthoritativeVictory(
            ChampionEncounterMode mode = ChampionEncounterMode.AuthoritativeQuest,
            ChampionEncounterConsequenceOutcome outcome =
                ChampionEncounterConsequenceOutcome.ChampionVictory,
            string realmId = "stonehold",
            string nvsCorrelationId = "nvs.corr.stonehold.guardian",
            string rewardOperationId = "reward.op.stonehold.guardian",
            bool labeledPractice = false,
            string encounterResultId = "result.stonehold.guardian")
        {
            return new ChampionEncounterConsequenceRequest(
                encounterResultId,
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
            public ChampionEncounterBossRewardPlan Plan(
                ChampionEncounterConsequenceRequest request)
            {
                return new ChampionEncounterBossRewardPlan(
                    ChampionEncounterBossRewardStatus.Issued,
                    "reward.op.stonehold.guardian",
                    string.Empty);
            }
        }

        private sealed class RecordingCommit : IChampionEncounterProfileCommit
        {
            public int CallCount { get; private set; }

            public bool MutatedSave { get; private set; }

            public bool Accept { get; set; } = true;

            public bool TryCommit(ChampionEncounterConsequenceCandidate candidate)
            {
                CallCount++;
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
