using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using AL.ChampionMode.DeathPenalty;

namespace AL.Tests.EditMode.ChampionDeathPenalty
{
    public sealed class DeathPenaltyPlannerTests
    {
        private const string AccountId = "test.account.1";
        private const string ProfileId = "test.profile.1";
        private const string CharacterId = "test.character.1";
        private const string ProgressionRevision = "test.progression.rev.1";
        private const string LevelCapPolicyId = "test.level-cap.policy";
        private const string LevelCapPolicyRevision = "test.level-cap.rev.1";
        private const string TechnicalCurrencyId = "test.player.main.currency";
        private const string ProviderId = "test.player.wallet.provider";
        private const string BindingRevision = "test.player.wallet.binding.rev.1";
        private const string WalletRevision = "test.player.wallet.rev.1";
        private const string RevivalRevision = "test.revival.rev.1";
        private const string DeathStateRevision = "test.death-state.rev.1";
        private const string ReplayLedgerVersion = "test.replay-ledger-v1";
        private const string ReplayLedgerRevision = "test.replay-ledger.rev.1";
        private const string PolicyVersion = "death-penalty-test-v1";

        [TestCase(50L, 45L, true)]
        [TestCase(5L, 0L, true)]
        [TestCase(1L, 0L, true)]
        [TestCase(0L, 0L, false)]
        public void BelowMaximumSubtractsFivePercentagePointsAndFloorsAtZero(
            long before,
            long expected,
            bool expectsWrite)
        {
            DeathPenaltyPlan plan = Plan(
                Request(),
                Progression(49, 50, before, 100L),
                null,
                Policy(),
                null);

            Assert.That(plan.Status, Is.EqualTo(DeathPenaltyPlanStatus.ReadyToCommit));
            Assert.That(plan.Proposal.Branch, Is.EqualTo(DeathPenaltyBranch.InLevelExperiencePenalty));
            Assert.That(plan.Proposal.BeforeLevel, Is.EqualTo(49));
            Assert.That(plan.Proposal.AfterLevel, Is.EqualTo(49));
            Assert.That(plan.Proposal.BeforeInLevelExperienceUnits, Is.EqualTo(before));
            Assert.That(plan.Proposal.AfterInLevelExperienceUnits, Is.EqualTo(expected));
            Assert.That(plan.Proposal.RequiresProgressionWrite, Is.EqualTo(expectsWrite));
            Assert.That(plan.Proposal.RequiresOathmarkWalletDebit, Is.False);
            Assert.That(plan.Proposal.RequiresAtomicRevival, Is.False);
            Assert.That(plan.Proposal.OathmarkDebitUnits, Is.Zero);
            Assert.That(plan.Proposal.OathmarkBinding, Is.Null);
        }

        [Test]
        public void FivePointsUsesExactInjectedScaleInsteadOfMultiplyingCurrentXp()
        {
            DeathPenaltyPlan plan = Plan(
                Request(),
                Progression(49, 50, 5000L, 10000L),
                null,
                Policy(),
                null);

            Assert.That(plan.Proposal.AfterInLevelExperienceUnits, Is.EqualTo(4500L));
            Assert.That(plan.Proposal.AfterInLevelExperienceUnits, Is.Not.EqualTo(4750L));
        }

        [Test]
        public void ScaleThatCannotRepresentFivePointsFailsClosedWithoutRounding()
        {
            DeathPenaltyPlan plan = Plan(
                Request(),
                Progression(49, 50, 50L, 99L),
                null,
                Policy(),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedInvalidProgression,
                DeathPenaltyDiagnosticCodes.InvalidProgression);
        }

        [Test]
        public void FullProgressBelowCapIsRejectedAsPendingLevelUpState()
        {
            DeathPenaltyPlan plan = Plan(
                Request(),
                Progression(49, 50, 100L, 100L),
                null,
                Policy(),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedInvalidProgression,
                DeathPenaltyDiagnosticCodes.InvalidProgression);
        }

        [Test]
        public void BelowMaximumNeverConsultsOrConsumesTheCurrencyBranch()
        {
            var prohibitedDomainWallet = Wallet(
                999L,
                OathmarkWalletAvailability.Malformed,
                Binding(PlayerCurrencyDomain.TwoPointFiveDimensionalKingdom));

            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: false),
                Progression(49, 50, 50L),
                prohibitedDomainWallet,
                Policy(cost: null),
                null);

            Assert.That(plan.Status, Is.EqualTo(DeathPenaltyPlanStatus.ReadyToCommit));
            Assert.That(plan.Proposal.AfterInLevelExperienceUnits, Is.EqualTo(45L));
            Assert.That(plan.Proposal.OathmarkBinding, Is.Null);
            Assert.That(plan.Proposal.OathmarkDebitUnits, Is.Zero);
            Assert.That(plan.Proposal.BeforeOathmarkBalance, Is.Zero);
            Assert.That(plan.Proposal.AfterOathmarkBalance, Is.Zero);
        }

        [Test]
        public void MaximumLevelKeepsExperienceByteStableAndPlansAtomicOathmarkRevival()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 73L),
                Wallet(25L),
                Policy(10L),
                null);

            Assert.That(plan.Status, Is.EqualTo(DeathPenaltyPlanStatus.ReadyToCommit));
            Assert.That(plan.Proposal.Branch, Is.EqualTo(DeathPenaltyBranch.MaxLevelOathmarkRevive));
            Assert.That(plan.Proposal.BeforeLevel, Is.EqualTo(50));
            Assert.That(plan.Proposal.AfterLevel, Is.EqualTo(50));
            Assert.That(plan.Proposal.BeforeInLevelExperienceUnits, Is.EqualTo(73L));
            Assert.That(plan.Proposal.AfterInLevelExperienceUnits, Is.EqualTo(73L));
            Assert.That(plan.Proposal.RequiresProgressionWrite, Is.False);
            Assert.That(plan.Proposal.OathmarkDebitUnits, Is.EqualTo(10L));
            Assert.That(plan.Proposal.BeforeOathmarkBalance, Is.EqualTo(25L));
            Assert.That(plan.Proposal.AfterOathmarkBalance, Is.EqualTo(15L));
            Assert.That(plan.Proposal.RequiresOathmarkWalletDebit, Is.True);
            Assert.That(plan.Proposal.RequiresAtomicRevival, Is.True);
            Assert.That(plan.Proposal.OathmarkBinding.Domain,
                Is.EqualTo(PlayerCurrencyDomain.ThreeDimensionalPlayerMain));
            Assert.That(plan.Proposal.OathmarkBinding.IsSoleMainCurrency, Is.True);
        }

        [Test]
        public void MaximumLevelExactBalanceCanReachZeroWithoutDebt()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 100L),
                Wallet(10L),
                Policy(10L),
                null);

            Assert.That(plan.Status, Is.EqualTo(DeathPenaltyPlanStatus.ReadyToCommit));
            Assert.That(plan.Proposal.AfterOathmarkBalance, Is.Zero);
        }

        [Test]
        public void MaximumLevelMissingCostFailsClosed()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 20L),
                Wallet(100L),
                Policy(cost: null),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedOathmarkConfigurationUnavailable,
                DeathPenaltyDiagnosticCodes.OathmarkConfigurationUnavailable);
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        public void MaximumLevelInvalidCostFailsClosed(long cost)
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 20L),
                Wallet(100L),
                Policy(cost),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedInvalidPolicy,
                DeathPenaltyDiagnosticCodes.InvalidPolicy);
        }

        [Test]
        public void MaximumLevelInsufficientOathmarksProducesNoMutationProposal()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 80L),
                Wallet(9L),
                Policy(10L),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedInsufficientOathmarks,
                DeathPenaltyDiagnosticCodes.InsufficientOathmarks);
        }

        [TestCase(OathmarkWalletAvailability.Unknown)]
        [TestCase(OathmarkWalletAvailability.AvailableReadOnly)]
        [TestCase(OathmarkWalletAvailability.Unavailable)]
        [TestCase(OathmarkWalletAvailability.Malformed)]
        public void MaximumLevelUnavailableOrNonWritableWalletFailsClosed(
            OathmarkWalletAvailability availability)
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 80L),
                Wallet(100L, availability),
                Policy(10L),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedOathmarkWalletUnavailable,
                DeathPenaltyDiagnosticCodes.OathmarkWalletUnavailable);
        }

        [TestCase(PlayerCurrencyDomain.Unknown)]
        [TestCase(PlayerCurrencyDomain.TwoPointFiveDimensionalKingdom)]
        [TestCase(PlayerCurrencyDomain.LegacyCompatibility)]
        [TestCase(PlayerCurrencyDomain.GuildOrRealm)]
        public void NonPlayerMainDomainsCannotSatisfyRevival(
            PlayerCurrencyDomain domain)
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 80L),
                Wallet(100L, binding: Binding(domain)),
                Policy(10L),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedInvalidOathmarkBinding,
                DeathPenaltyDiagnosticCodes.InvalidOathmarkBinding);
        }

        [Test]
        public void ParallelPlayerWalletOrInvalidIntegerScaleCannotSatisfyRevival()
        {
            DeathPenaltyPlan parallel = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 80L),
                Wallet(100L, binding: Binding(
                    PlayerCurrencyDomain.ThreeDimensionalPlayerMain,
                    isSoleMainCurrency: false)),
                Policy(10L),
                null);
            DeathPenaltyPlan invalidScale = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 80L),
                Wallet(100L, binding: Binding(
                    PlayerCurrencyDomain.ThreeDimensionalPlayerMain,
                    integerUnitScale: 0L)),
                Policy(10L),
                null);

            AssertRejected(
                parallel,
                DeathPenaltyPlanStatus.RejectedInvalidOathmarkBinding,
                DeathPenaltyDiagnosticCodes.InvalidOathmarkBinding);
            AssertRejected(
                invalidScale,
                DeathPenaltyPlanStatus.RejectedInvalidOathmarkBinding,
                DeathPenaltyDiagnosticCodes.InvalidOathmarkBinding);
        }

        [Test]
        public void MaxLevelRequiresExplicitInjectedTechnicalBindingExpectations()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: false),
                Progression(50, 50, 80L),
                Wallet(100L),
                Policy(10L),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedInvalidOathmarkBinding,
                DeathPenaltyDiagnosticCodes.InvalidOathmarkBinding);
        }

        [Test]
        public void StaleWalletOrBindingFailsClosed()
        {
            DeathPenaltyPlan staleRevision = Plan(
                Request(
                    withWalletExpectation: true,
                    expectedWalletRevision: "test.player.wallet.rev.old"),
                Progression(50, 50, 80L),
                Wallet(100L),
                Policy(10L),
                null);
            DeathPenaltyPlan wrongProvider = Plan(
                Request(
                    withWalletExpectation: true,
                    expectedProviderId: "test.other.provider"),
                Progression(50, 50, 80L),
                Wallet(100L),
                Policy(10L),
                null);

            AssertRejected(
                staleRevision,
                DeathPenaltyPlanStatus.RejectedStaleOathmarkWallet,
                DeathPenaltyDiagnosticCodes.StaleOathmarkWallet);
            AssertRejected(
                wrongProvider,
                DeathPenaltyPlanStatus.RejectedStaleOathmarkWallet,
                DeathPenaltyDiagnosticCodes.StaleOathmarkWallet);
        }

        [Test]
        public void CrossAccountProfileAndCharacterSnapshotsFailClosed()
        {
            DeathPenaltyPlan foreignProgression = Plan(
                Request(),
                Progression(49, 50, 50L, accountId: "test.account.foreign"),
                null,
                Policy(),
                null);
            DeathPenaltyPlan foreignWallet = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 50L),
                Wallet(100L, profileId: "test.profile.foreign"),
                Policy(10L),
                null);

            AssertRejected(
                foreignProgression,
                DeathPenaltyPlanStatus.RejectedIdentityMismatch,
                DeathPenaltyDiagnosticCodes.IdentityMismatch);
            AssertRejected(
                foreignWallet,
                DeathPenaltyPlanStatus.RejectedIdentityMismatch,
                DeathPenaltyDiagnosticCodes.IdentityMismatch);
        }

        [Test]
        public void StaleProgressionAndLevelCapPolicyFailClosed()
        {
            DeathPenaltyPlan staleProgression = Plan(
                Request(expectedProgressionRevision: "test.progression.old"),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            DeathPenaltyPlan staleCap = Plan(
                Request(expectedLevelCapPolicyRevision: "test.level-cap.old"),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);

            AssertRejected(
                staleProgression,
                DeathPenaltyPlanStatus.RejectedStaleProgression,
                DeathPenaltyDiagnosticCodes.StaleProgression);
            AssertRejected(
                staleCap,
                DeathPenaltyPlanStatus.RejectedLevelCapPolicyMismatch,
                DeathPenaltyDiagnosticCodes.LevelCapPolicyMismatch);
        }

        [Test]
        public void UnknownCommitWithoutReceiptFailsReconciliationAndNeverPlansReplacement()
        {
            DeathPenaltyPlan plan = Plan(
                Request(),
                Progression(
                    49,
                    50,
                    45L,
                    progressionRevision: "test.progression.rev.2"),
                null,
                Policy(),
                null);

            AssertRejected(
                plan,
                DeathPenaltyPlanStatus.RejectedStaleProgression,
                DeathPenaltyDiagnosticCodes.StaleProgression);
        }

        [Test]
        public void ReplayLedgerMustBeAvailableCompleteCurrentAndExplicit()
        {
            DeathPenaltyRequest request = Request();
            DeathPenaltyProgressionSnapshot progression =
                Progression(49, 50, 50L);
            DeathPenaltyDeathStateSnapshot deathState = DeathState(request);
            DeathPenaltyPolicySnapshot policy = Policy();

            DeathPenaltyPlan missing = DeathPenaltyPlanner.Plan(
                request,
                deathState,
                progression,
                null,
                policy,
                null);
            DeathPenaltyPlan unavailable = DeathPenaltyPlanner.Plan(
                request,
                deathState,
                progression,
                null,
                policy,
                ReplayLedger(
                    DeathPenaltyReplayLedgerAvailability.Unavailable));
            DeathPenaltyPlan incomplete = DeathPenaltyPlanner.Plan(
                request,
                deathState,
                progression,
                null,
                policy,
                ReplayLedger(isComplete: false));
            DeathPenaltyPlan stale = DeathPenaltyPlanner.Plan(
                request,
                deathState,
                progression,
                null,
                policy,
                ReplayLedger(revision: "test.replay-ledger.rev.stale"));
            DeathPenaltyPlan missingCollection = DeathPenaltyPlanner.Plan(
                request,
                deathState,
                progression,
                null,
                policy,
                new DeathPenaltyReplayLedgerSnapshot(
                    DeathPenaltyReplayLedgerAvailability.Available,
                    true,
                    ReplayLedgerVersion,
                    ReplayLedgerRevision,
                    null));

            AssertRejected(
                missing,
                DeathPenaltyPlanStatus.RejectedReplayLedgerUnavailable,
                DeathPenaltyDiagnosticCodes.ReplayLedgerUnavailable);
            AssertRejected(
                unavailable,
                DeathPenaltyPlanStatus.RejectedReplayLedgerUnavailable,
                DeathPenaltyDiagnosticCodes.ReplayLedgerUnavailable);
            AssertRejected(
                incomplete,
                DeathPenaltyPlanStatus.RejectedReplayLedgerIncomplete,
                DeathPenaltyDiagnosticCodes.ReplayLedgerIncomplete);
            AssertRejected(
                stale,
                DeathPenaltyPlanStatus.RejectedReplayLedgerStale,
                DeathPenaltyDiagnosticCodes.ReplayLedgerStale);
            AssertRejected(
                missingCollection,
                DeathPenaltyPlanStatus.RejectedReplayLedgerInvalid,
                DeathPenaltyDiagnosticCodes.ReplayLedgerInvalid);
        }

        [Test]
        public void CurrentAuthoritativeDeathStateIsRequiredAndMustMatchExactly()
        {
            DeathPenaltyRequest request = Request();
            DeathPenaltyProgressionSnapshot progression =
                Progression(49, 50, 50L);
            DeathPenaltyPolicySnapshot policy = Policy();
            DeathPenaltyReplayLedgerSnapshot ledger = ReplayLedger();

            DeathPenaltyPlan missing = DeathPenaltyPlanner.Plan(
                request,
                null,
                progression,
                null,
                policy,
                ledger);
            DeathPenaltyPlan unavailable = DeathPenaltyPlanner.Plan(
                request,
                DeathState(
                    request,
                    DeathPenaltyAuthoritativeDeathStatus.Unknown),
                progression,
                null,
                policy,
                ledger);
            DeathPenaltyPlan foreign = DeathPenaltyPlanner.Plan(
                request,
                DeathState(
                    request,
                    accountOverride: "test.account.foreign"),
                progression,
                null,
                policy,
                ledger);
            DeathPenaltyPlan stale = DeathPenaltyPlanner.Plan(
                request,
                DeathState(
                    request,
                    revisionOverride: "test.death-state.rev.stale"),
                progression,
                null,
                policy,
                ledger);
            DeathPenaltyPlan resolved = DeathPenaltyPlanner.Plan(
                request,
                DeathState(
                    request,
                    DeathPenaltyAuthoritativeDeathStatus.Resolved),
                progression,
                null,
                policy,
                ledger);

            AssertRejected(
                missing,
                DeathPenaltyPlanStatus.RejectedDeathStateUnavailable,
                DeathPenaltyDiagnosticCodes.DeathStateUnavailable);
            AssertRejected(
                unavailable,
                DeathPenaltyPlanStatus.RejectedDeathStateUnavailable,
                DeathPenaltyDiagnosticCodes.DeathStateUnavailable);
            AssertRejected(
                foreign,
                DeathPenaltyPlanStatus.RejectedDeathStateMismatch,
                DeathPenaltyDiagnosticCodes.DeathStateMismatch);
            AssertRejected(
                stale,
                DeathPenaltyPlanStatus.RejectedStaleDeathState,
                DeathPenaltyDiagnosticCodes.StaleDeathState);
            AssertRejected(
                resolved,
                DeathPenaltyPlanStatus.RejectedDeathAlreadyResolved,
                DeathPenaltyDiagnosticCodes.DeathAlreadyResolved);
        }

        [Test]
        public void ExactReceiptReplayReturnsTheSameReceiptWithoutAnotherProposal()
        {
            DeathPenaltyRequest request = Request();
            DeathPenaltyPolicySnapshot policy = Policy();
            DeathPenaltyPlan initial = Plan(
                request,
                Progression(49, 50, 50L),
                null,
                policy,
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyPlan replay = DeathPenaltyPlanner.Plan(
                request,
                DeathState(
                    request,
                    DeathPenaltyAuthoritativeDeathStatus.Resolved),
                null,
                null,
                policy,
                ReplayLedger(receipts: new[] { receipt }));

            Assert.That(replay.Status, Is.EqualTo(DeathPenaltyPlanStatus.ReplayedCommitted));
            Assert.That(replay.ReplayReceipt, Is.SameAs(receipt));
            Assert.That(replay.ReplayReceipt.ReceiptHash, Is.EqualTo(receipt.ReceiptHash));
            Assert.That(replay.Proposal, Is.Null);
            Assert.That(replay.HasMutationProposal, Is.False);
        }

        [Test]
        public void DeadAwaitingPenaltyRejectsMatchingBelowMaxReceipt()
        {
            DeathPenaltyRequest request = Request();
            DeathPenaltyPolicySnapshot policy = Policy();
            DeathPenaltyPlan initial = Plan(
                request,
                Progression(49, 50, 50L),
                null,
                policy,
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyPlan inconsistent = Plan(
                request,
                null,
                null,
                policy,
                new[] { receipt });

            AssertRejected(
                inconsistent,
                DeathPenaltyPlanStatus
                    .RejectedDeathStateReceiptInconsistent,
                DeathPenaltyDiagnosticCodes
                    .DeathStateReceiptInconsistent);
        }

        [Test]
        public void DeadAwaitingPenaltyRejectsMatchingMaxLevelReceipt()
        {
            DeathPenaltyRequest request =
                Request(withWalletExpectation: true);
            DeathPenaltyPolicySnapshot policy = Policy(10L);
            DeathPenaltyPlan initial = Plan(
                request,
                Progression(50, 50, 87L),
                Wallet(100L),
                policy,
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, ProgressionRevision),
                    AppliedWallet(initial, "test.player.wallet.rev.2"),
                    AppliedAtomicRevival(
                        initial,
                        "test.player.wallet.rev.2"),
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyPlan inconsistent = Plan(
                request,
                null,
                null,
                policy,
                new[] { receipt });

            AssertRejected(
                inconsistent,
                DeathPenaltyPlanStatus
                    .RejectedDeathStateReceiptInconsistent,
                DeathPenaltyDiagnosticCodes
                    .DeathStateReceiptInconsistent);
        }

        [Test]
        public void ResolvedDeathCanReconcileExactReceiptButCannotPlanReplacement()
        {
            DeathPenaltyRequest originalRequest = Request();
            DeathPenaltyPolicySnapshot policy = Policy();
            DeathPenaltyPlan initial = Plan(
                originalRequest,
                Progression(49, 50, 50L),
                null,
                policy,
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyRequest reconciliationRequest = Request(
                expectedDeathStateRevision: "test.death-state.rev.2",
                expectedReplayLedgerRevision:
                    "test.replay-ledger.rev.2");
            DeathPenaltyPlan replay = DeathPenaltyPlanner.Plan(
                reconciliationRequest,
                DeathState(
                    reconciliationRequest,
                    DeathPenaltyAuthoritativeDeathStatus.Resolved),
                null,
                null,
                policy,
                ReplayLedger(
                    revision: "test.replay-ledger.rev.2",
                    receipts: new[] { receipt }));

            Assert.That(
                replay.Status,
                Is.EqualTo(DeathPenaltyPlanStatus.ReplayedCommitted));
            Assert.That(replay.ReplayReceipt, Is.SameAs(receipt));
            Assert.That(replay.HasMutationProposal, Is.False);
        }

        [Test]
        public void SameOperationWithDifferentDeathFingerprintIsCollision()
        {
            DeathPenaltyRequest firstRequest = Request();
            DeathPenaltyPolicySnapshot policy = Policy();
            DeathPenaltyPlan initial = Plan(
                firstRequest,
                Progression(49, 50, 50L),
                null,
                policy,
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyPlan collision = Plan(
                Request(deathEventId: "test.death.event.2"),
                null,
                null,
                policy,
                new[] { receipt });

            AssertRejected(
                collision,
                DeathPenaltyPlanStatus.RejectedOperationCollision,
                DeathPenaltyDiagnosticCodes.OperationCollision);
        }

        [Test]
        public void SameBelowMaxDeathUnderDifferentOperationIsDeathCollision()
        {
            DeathPenaltyRequest firstRequest = Request();
            DeathPenaltyPlan initial = Plan(
                firstRequest,
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyPlan collision = Plan(
                Request(operationId: "test.death.operation.2"),
                Progression(49, 50, 50L),
                null,
                Policy(),
                new[] { receipt });

            AssertRejected(
                collision,
                DeathPenaltyPlanStatus.RejectedDeathEventCollision,
                DeathPenaltyDiagnosticCodes.DeathEventCollision);
        }

        [Test]
        public void SameMaxLevelDeathUnderDifferentOperationIsDeathCollision()
        {
            DeathPenaltyRequest firstRequest =
                Request(withWalletExpectation: true);
            DeathPenaltyPlan initial = Plan(
                firstRequest,
                Progression(50, 50, 50L),
                Wallet(100L),
                Policy(10L),
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, ProgressionRevision),
                    AppliedWallet(initial, "test.player.wallet.rev.2"),
                    AppliedAtomicRevival(
                        initial,
                        "test.player.wallet.rev.2"),
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyPlan collision = Plan(
                Request(
                    withWalletExpectation: true,
                    operationId: "test.death.operation.2"),
                Progression(50, 50, 50L),
                Wallet(100L),
                Policy(10L),
                new[] { receipt });

            AssertRejected(
                collision,
                DeathPenaltyPlanStatus.RejectedDeathEventCollision,
                DeathPenaltyDiagnosticCodes.DeathEventCollision);
        }

        [Test]
        public void DuplicateDeathFingerprintsInvalidateTheWholeReplayLedger()
        {
            DeathPenaltyPlan first = Plan(
                Request(operationId: "test.death.operation.1"),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            DeathPenaltyPlan second = Plan(
                Request(operationId: "test.death.operation.2"),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    first,
                    AppliedProgression(first, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt firstReceipt),
                Is.True);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    second,
                    AppliedProgression(second, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt secondReceipt),
                Is.True);

            DeathPenaltyPlan invalid = Plan(
                Request(
                    operationId: "test.death.operation.3",
                    deathEventId: "test.death.event.3"),
                Progression(49, 50, 50L),
                null,
                Policy(),
                new[] { firstReceipt, secondReceipt });

            AssertRejected(
                invalid,
                DeathPenaltyPlanStatus.RejectedReplayLedgerInvalid,
                DeathPenaltyDiagnosticCodes.ReplayLedgerInvalid);
        }

        [Test]
        public void SameOperationWithDifferentCostPolicyIsCollision()
        {
            DeathPenaltyRequest request = Request(withWalletExpectation: true);
            DeathPenaltyPlan initial = Plan(
                request,
                Progression(50, 50, 50L),
                Wallet(100L),
                Policy(10L),
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, ProgressionRevision),
                    AppliedWallet(initial, "test.player.wallet.rev.2"),
                    AppliedAtomicRevival(
                        initial,
                        "test.player.wallet.rev.2"),
                    out DeathPenaltyReceipt receipt),
                Is.True);

            DeathPenaltyPlan collision = Plan(
                request,
                null,
                null,
                Policy(11L),
                new[] { receipt });

            AssertRejected(
                collision,
                DeathPenaltyPlanStatus.RejectedOperationCollision,
                DeathPenaltyDiagnosticCodes.OperationCollision);
        }

        [Test]
        public void DuplicateOrTamperedReceiptsFailTheWholeReplayLedger()
        {
            DeathPenaltyPlan initial = Plan(
                Request(),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    initial,
                    AppliedProgression(initial, "test.progression.rev.2"),
                    null,
                    null,
                    out DeathPenaltyReceipt receipt),
                Is.True);
            var tampered = new DeathPenaltyReceipt(
                receipt.Proposal,
                receipt.AfterProgressionRevision,
                receipt.AfterOathmarkWalletRevision,
                receipt.AfterRevivalRevision,
                receipt.AtomicCommitRevision,
                receipt.AtomicRevivalFingerprint,
                receipt.RevivalCommitted,
                new string('0', 64));

            DeathPenaltyPlan duplicate = Plan(
                Request(),
                null,
                null,
                Policy(),
                new[] { receipt, receipt });
            DeathPenaltyPlan invalid = Plan(
                Request(),
                null,
                null,
                Policy(),
                new[] { tampered });

            AssertRejected(
                duplicate,
                DeathPenaltyPlanStatus.RejectedReplayLedgerInvalid,
                DeathPenaltyDiagnosticCodes.ReplayLedgerInvalid);
            AssertRejected(
                invalid,
                DeathPenaltyPlanStatus.RejectedReplayLedgerInvalid,
                DeathPenaltyDiagnosticCodes.ReplayLedgerInvalid);
        }

        [Test]
        public void MaxReceiptCommitsDebitAndRevivalTogetherWithXpUnchanged()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 87L),
                Wallet(100L),
                Policy(10L),
                null);

            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    AppliedProgression(plan, ProgressionRevision),
                    AppliedWallet(plan, "test.player.wallet.rev.2"),
                    AppliedAtomicRevival(
                        plan,
                        "test.player.wallet.rev.2"),
                    out DeathPenaltyReceipt receipt),
                Is.True);
            Assert.That(receipt.Branch, Is.EqualTo(DeathPenaltyBranch.MaxLevelOathmarkRevive));
            Assert.That(receipt.Proposal.BeforeInLevelExperienceUnits, Is.EqualTo(87L));
            Assert.That(receipt.Proposal.AfterInLevelExperienceUnits, Is.EqualTo(87L));
            Assert.That(receipt.Proposal.OathmarkDebitUnits, Is.EqualTo(10L));
            Assert.That(receipt.RevivalCommitted, Is.True);
            Assert.That(receipt.AfterProgressionRevision, Is.EqualTo(ProgressionRevision));
            Assert.That(receipt.AfterOathmarkWalletRevision, Is.EqualTo("test.player.wallet.rev.2"));
            Assert.That(receipt.AfterRevivalRevision, Is.EqualTo("test.revival.rev.2"));
            Assert.That(receipt.AtomicCommitRevision, Is.EqualTo("test.atomic.commit.rev.1"));
            Assert.That(receipt.AtomicRevivalFingerprint, Has.Length.EqualTo(64));
            Assert.That(DeathPenaltyPlanner.ValidateReceipt(receipt), Is.True);
        }

        [Test]
        public void MaxReceiptRejectsDebitWithoutRevivalAndRevivalWithoutDebit()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 87L),
                Wallet(100L),
                Policy(10L),
                null);
            DeathPenaltyProgressionSnapshot afterProgression =
                AppliedProgression(plan, ProgressionRevision);

            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    AppliedWallet(plan, "test.player.wallet.rev.2"),
                    null,
                    out _),
                Is.False,
                "A wallet debit alone cannot prove that revival happened.");
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    AppliedWallet(plan, "test.player.wallet.rev.2"),
                    AppliedAtomicRevival(
                        plan,
                        "test.player.wallet.rev.2",
                        status:
                            DeathPenaltyAtomicRevivalStatus
                                .WalletDebitedWithoutRevival,
                        isAliveAfter: false),
                    out _),
                Is.False,
                "An explicitly partial debit cannot become revival evidence.");
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    AppliedWallet(
                        plan,
                        "test.player.wallet.rev.2",
                        balanceOverride: plan.Proposal.BeforeOathmarkBalance),
                    AppliedAtomicRevival(
                        plan,
                        "test.player.wallet.rev.2",
                        status:
                            DeathPenaltyAtomicRevivalStatus
                                .RevivalCommittedWithoutDebit,
                        afterBalanceOverride:
                            plan.Proposal.BeforeOathmarkBalance),
                    out _),
                Is.False,
                "Revival without the exact Oathmark debit cannot commit.");
        }

        [Test]
        public void MaxReceiptRejectsForeignStaleOrDifferentOperationRevivalEvidence()
        {
            DeathPenaltyPlan plan = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 87L),
                Wallet(100L),
                Policy(10L),
                null);
            DeathPenaltyProgressionSnapshot afterProgression =
                AppliedProgression(plan, ProgressionRevision);
            OathmarkWalletSnapshot afterWallet =
                AppliedWallet(plan, "test.player.wallet.rev.2");

            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    afterWallet,
                    AppliedAtomicRevival(
                        plan,
                        afterWallet.WalletRevision,
                        accountOverride: "test.account.foreign"),
                    out _),
                Is.False);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    afterWallet,
                    AppliedAtomicRevival(
                        plan,
                        afterWallet.WalletRevision,
                        beforeRevivalRevisionOverride:
                            "test.revival.rev.stale"),
                    out _),
                Is.False);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    afterWallet,
                    AppliedAtomicRevival(
                        plan,
                        afterWallet.WalletRevision,
                        operationOverride: "test.death.operation.foreign"),
                    out _),
                Is.False);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    afterProgression,
                    afterWallet,
                    AppliedAtomicRevival(
                        plan,
                        afterWallet.WalletRevision,
                        deathFingerprintOverride: new string('0', 64)),
                    out _),
                Is.False);
        }

        [Test]
        public void ReceiptIssuanceRejectsPartialOrContradictoryCommitRevisions()
        {
            DeathPenaltyPlan belowMax = Plan(
                Request(),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            DeathPenaltyPlan max = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 50L),
                Wallet(100L),
                Policy(10L),
                null);

            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    belowMax,
                    AppliedProgression(belowMax, ProgressionRevision),
                    null,
                    null,
                    out _),
                Is.False);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    max,
                    AppliedProgression(max, ProgressionRevision),
                    null,
                    AppliedAtomicRevival(
                        max,
                        "test.player.wallet.rev.2"),
                    out _),
                Is.False);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    max,
                    AppliedProgression(
                        max,
                        "test.progression.rev.changed"),
                    AppliedWallet(max, "test.player.wallet.rev.2"),
                    AppliedAtomicRevival(
                        max,
                        "test.player.wallet.rev.2"),
                    out _),
                Is.False);
        }

        [Test]
        public void ReceiptIssuanceRejectsForgedAfterValuesAndCrossIdentity()
        {
            DeathPenaltyPlan belowMax = Plan(
                Request(),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            DeathPenaltyPlan max = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 63L),
                Wallet(100L),
                Policy(10L),
                null);

            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    belowMax,
                    AppliedProgression(
                        belowMax,
                        "test.progression.rev.2",
                        experienceOverride: 44L),
                    null,
                    null,
                    out _),
                Is.False,
                "Wrong applied XP must not become commit evidence.");
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    belowMax,
                    AppliedProgression(
                        belowMax,
                        "test.progression.rev.2",
                        accountOverride: "test.account.foreign"),
                    null,
                    null,
                    out _),
                Is.False,
                "Cross-account progression must not become commit evidence.");
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    max,
                    AppliedProgression(
                        max,
                        ProgressionRevision,
                        experienceOverride: 58L),
                    AppliedWallet(max, "test.player.wallet.rev.2"),
                    AppliedAtomicRevival(
                        max,
                        "test.player.wallet.rev.2"),
                    out _),
                Is.False,
                "Maximum-level XP must remain byte-stable.");
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    max,
                    AppliedProgression(max, ProgressionRevision),
                    AppliedWallet(
                        max,
                        "test.player.wallet.rev.2",
                        balanceOverride: 91L),
                    AppliedAtomicRevival(
                        max,
                        "test.player.wallet.rev.2"),
                    out _),
                Is.False,
                "Wrong wallet debit must not become atomic revival evidence.");
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    max,
                    AppliedProgression(max, ProgressionRevision),
                    AppliedWallet(
                        max,
                        "test.player.wallet.rev.2",
                        profileOverride: "test.profile.foreign"),
                    AppliedAtomicRevival(
                        max,
                        "test.player.wallet.rev.2"),
                    out _),
                Is.False,
                "Cross-profile wallet must not become commit evidence.");
        }

        [Test]
        public void ZeroProgressReceiptDoesNotFabricateAProgressionRevision()
        {
            DeathPenaltyPlan plan = Plan(
                Request(),
                Progression(49, 50, 0L),
                null,
                Policy(),
                null);

            Assert.That(plan.Proposal.RequiresProgressionWrite, Is.False);
            Assert.That(
                DeathPenaltyPlanner.TryVerifyAdapterCommitAndCreateReceipt(
                    plan,
                    AppliedProgression(plan, ProgressionRevision),
                    null,
                    null,
                    out DeathPenaltyReceipt receipt),
                Is.True);
            Assert.That(receipt.AfterProgressionRevision, Is.EqualTo(ProgressionRevision));
            Assert.That(receipt.RevivalCommitted, Is.False);
        }

        [Test]
        public void EquivalentInputsProduceDeterministicFingerprintsAndPlans()
        {
            DeathPenaltyPlan first = Plan(
                Request(),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);
            DeathPenaltyPlan second = Plan(
                Request(),
                Progression(49, 50, 50L),
                null,
                Policy(),
                null);

            Assert.That(second.Proposal.DeathFingerprint, Is.EqualTo(first.Proposal.DeathFingerprint));
            Assert.That(second.Proposal.RequestFingerprint, Is.EqualTo(first.Proposal.RequestFingerprint));
            Assert.That(second.Proposal.PlanHash, Is.EqualTo(first.Proposal.PlanHash));
        }

        [Test]
        public void PlayerFacingNameDoesNotDefineAnyTechnicalWalletIdentity()
        {
            Assert.That(OathmarkPlayerCurrencySemantics.SingularDisplayName, Is.EqualTo("Oathmark"));
            Assert.That(OathmarkPlayerCurrencySemantics.PluralDisplayName, Is.EqualTo("Oathmarks"));
            Assert.That(OathmarkPlayerCurrencySemantics.CoinPresentationHasWalletAuthority, Is.False);

            DeathPenaltyPlan noTechnicalBinding = Plan(
                Request(withWalletExpectation: true),
                Progression(50, 50, 50L),
                Wallet(100L, binding: new OathmarkWalletBinding(
                    string.Empty,
                    ProviderId,
                    BindingRevision,
                    PlayerCurrencyDomain.ThreeDimensionalPlayerMain,
                    true,
                    1L)),
                Policy(10L),
                null);
            AssertRejected(
                noTechnicalBinding,
                DeathPenaltyPlanStatus.RejectedInvalidOathmarkBinding,
                DeathPenaltyDiagnosticCodes.InvalidOathmarkBinding);
        }

        [Test]
        public void PublicSurfaceCannotConstructOrMintAReceipt()
        {
            ConstructorInfo[] publicConstructors =
                typeof(DeathPenaltyReceipt).GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance);
            MethodInfo[] publicPlannerMethods =
                typeof(DeathPenaltyPlanner).GetMethods(
                    BindingFlags.Public | BindingFlags.Static);

            Assert.That(publicConstructors, Is.Empty);
            foreach (MethodInfo method in publicPlannerMethods)
            {
                Assert.That(
                    method.ReturnType,
                    Is.Not.EqualTo(typeof(DeathPenaltyReceipt)),
                    method.Name);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Type parameterType = parameter.ParameterType.IsByRef
                        ? parameter.ParameterType.GetElementType()
                        : parameter.ParameterType;
                    Assert.That(
                        parameterType,
                        Is.Not.EqualTo(typeof(DeathPenaltyReceipt)),
                        method.Name);
                }
            }

            Assert.That(
                typeof(DeathPenaltyPlanner).GetMethod(
                    "TryVerifyAdapterCommitAndCreateReceipt",
                    BindingFlags.Public | BindingFlags.Static),
                Is.Null);
        }

        [Test]
        public void InvalidProgressionCannotLowerLevelOrProduceNegativeExperience()
        {
            DeathPenaltyPlan aboveCap = Plan(
                Request(),
                Progression(51, 50, 0L),
                null,
                Policy(),
                null);
            DeathPenaltyPlan negative = Plan(
                Request(),
                Progression(49, 50, -1L),
                null,
                Policy(),
                null);

            AssertRejected(
                aboveCap,
                DeathPenaltyPlanStatus.RejectedInvalidProgression,
                DeathPenaltyDiagnosticCodes.InvalidProgression);
            AssertRejected(
                negative,
                DeathPenaltyPlanStatus.RejectedInvalidProgression,
                DeathPenaltyDiagnosticCodes.InvalidProgression);
        }

        private static DeathPenaltyPlan Plan(
            DeathPenaltyRequest request,
            DeathPenaltyProgressionSnapshot progression,
            OathmarkWalletSnapshot wallet,
            DeathPenaltyPolicySnapshot policy,
            IEnumerable<DeathPenaltyReceipt> retainedReceipts)
        {
            return DeathPenaltyPlanner.Plan(
                request,
                DeathState(request),
                progression,
                wallet,
                policy,
                ReplayLedger(receipts: retainedReceipts));
        }

        private static DeathPenaltyRequest Request(
            bool withWalletExpectation = false,
            string operationId = "test.death.operation.1",
            string deathEventId = "test.death.event.1",
            string expectedProgressionRevision = ProgressionRevision,
            string expectedLevelCapPolicyRevision = LevelCapPolicyRevision,
            string expectedProviderId = ProviderId,
            string expectedWalletRevision = WalletRevision,
            string expectedDeathStateRevision = DeathStateRevision,
            string expectedReplayLedgerRevision = ReplayLedgerRevision)
        {
            return new DeathPenaltyRequest(
                operationId,
                AccountId,
                ProfileId,
                CharacterId,
                deathEventId,
                "test.combat.session.1",
                "test.encounter.attempt.1",
                "test.instance.1",
                7L,
                expectedProgressionRevision,
                LevelCapPolicyId,
                expectedLevelCapPolicyRevision,
                expectedDeathStateRevision,
                ReplayLedgerVersion,
                expectedReplayLedgerRevision,
                withWalletExpectation ? TechnicalCurrencyId : null,
                withWalletExpectation ? expectedProviderId : null,
                withWalletExpectation ? BindingRevision : null,
                withWalletExpectation ? expectedWalletRevision : null,
                withWalletExpectation ? RevivalRevision : null);
        }

        private static DeathPenaltyDeathStateSnapshot DeathState(
            DeathPenaltyRequest request,
            DeathPenaltyAuthoritativeDeathStatus status =
                DeathPenaltyAuthoritativeDeathStatus.DeadAwaitingPenalty,
            string accountOverride = null,
            string profileOverride = null,
            string characterOverride = null,
            string deathEventOverride = null,
            string revisionOverride = null)
        {
            return new DeathPenaltyDeathStateSnapshot(
                status,
                accountOverride ?? request.AccountId,
                profileOverride ?? request.ProfileId,
                characterOverride ?? request.CharacterId,
                deathEventOverride ?? request.DeathEventId,
                request.CombatSessionId,
                request.EncounterAttemptId,
                request.InstanceId,
                request.DeathOrdinal,
                revisionOverride ?? request.ExpectedDeathStateRevision);
        }

        private static DeathPenaltyReplayLedgerSnapshot ReplayLedger(
            DeathPenaltyReplayLedgerAvailability availability =
                DeathPenaltyReplayLedgerAvailability.Available,
            bool isComplete = true,
            string version = ReplayLedgerVersion,
            string revision = ReplayLedgerRevision,
            IEnumerable<DeathPenaltyReceipt> receipts = null)
        {
            return new DeathPenaltyReplayLedgerSnapshot(
                availability,
                isComplete,
                version,
                revision,
                receipts ?? Array.Empty<DeathPenaltyReceipt>());
        }

        private static DeathPenaltyProgressionSnapshot Progression(
            int currentLevel,
            int maximumLevel,
            long experience,
            long scale = 100L,
            string accountId = AccountId,
            string profileId = ProfileId,
            string characterId = CharacterId,
            string progressionRevision = ProgressionRevision)
        {
            return new DeathPenaltyProgressionSnapshot(
                accountId,
                profileId,
                characterId,
                currentLevel,
                maximumLevel,
                experience,
                scale,
                progressionRevision,
                LevelCapPolicyId,
                LevelCapPolicyRevision);
        }

        private static OathmarkWalletBinding Binding(
            PlayerCurrencyDomain domain =
                PlayerCurrencyDomain.ThreeDimensionalPlayerMain,
            bool isSoleMainCurrency = true,
            long integerUnitScale = 1L)
        {
            return new OathmarkWalletBinding(
                TechnicalCurrencyId,
                ProviderId,
                BindingRevision,
                domain,
                isSoleMainCurrency,
                integerUnitScale);
        }

        private static OathmarkWalletSnapshot Wallet(
            long balance,
            OathmarkWalletAvailability availability =
                OathmarkWalletAvailability.AvailableWritable,
            OathmarkWalletBinding binding = null,
            string accountId = AccountId,
            string profileId = ProfileId,
            string characterId = CharacterId,
            string walletRevision = WalletRevision)
        {
            return new OathmarkWalletSnapshot(
                accountId,
                profileId,
                characterId,
                binding ?? Binding(),
                availability,
                balance,
                walletRevision);
        }

        private static DeathPenaltyPolicySnapshot Policy(long? cost = null)
        {
            return new DeathPenaltyPolicySnapshot(PolicyVersion, cost);
        }

        private static DeathPenaltyProgressionSnapshot AppliedProgression(
            DeathPenaltyPlan plan,
            string revision,
            long? experienceOverride = null,
            string accountOverride = null,
            string profileOverride = null,
            string characterOverride = null)
        {
            DeathPenaltyCommitProposal proposal = plan.Proposal;
            return new DeathPenaltyProgressionSnapshot(
                accountOverride ?? proposal.AccountId,
                profileOverride ?? proposal.ProfileId,
                characterOverride ?? proposal.CharacterId,
                proposal.AfterLevel,
                proposal.MaximumLevel,
                experienceOverride ??
                    proposal.AfterInLevelExperienceUnits,
                proposal.ExperienceUnitsPerLevel,
                revision,
                proposal.LevelCapPolicyId,
                proposal.LevelCapPolicyRevision);
        }

        private static OathmarkWalletSnapshot AppliedWallet(
            DeathPenaltyPlan plan,
            string revision,
            long? balanceOverride = null,
            string accountOverride = null,
            string profileOverride = null,
            string characterOverride = null,
            OathmarkWalletBinding bindingOverride = null)
        {
            DeathPenaltyCommitProposal proposal = plan.Proposal;
            return new OathmarkWalletSnapshot(
                accountOverride ?? proposal.AccountId,
                profileOverride ?? proposal.ProfileId,
                characterOverride ?? proposal.CharacterId,
                bindingOverride ?? proposal.OathmarkBinding,
                OathmarkWalletAvailability.AvailableWritable,
                balanceOverride ?? proposal.AfterOathmarkBalance,
                revision);
        }

        private static DeathPenaltyAtomicRevivalSnapshot AppliedAtomicRevival(
            DeathPenaltyPlan plan,
            string afterWalletRevision,
            DeathPenaltyAtomicRevivalStatus status =
                DeathPenaltyAtomicRevivalStatus.CommittedAtomically,
            string operationOverride = null,
            string requestFingerprintOverride = null,
            string deathFingerprintOverride = null,
            string accountOverride = null,
            string profileOverride = null,
            string characterOverride = null,
            string beforeWalletRevisionOverride = null,
            string beforeRevivalRevisionOverride = null,
            string afterRevivalRevision = "test.revival.rev.2",
            string atomicCommitRevision = "test.atomic.commit.rev.1",
            long? debitOverride = null,
            long? beforeBalanceOverride = null,
            long? afterBalanceOverride = null,
            bool wasDeadBefore = true,
            bool isAliveAfter = true)
        {
            DeathPenaltyCommitProposal proposal = plan.Proposal;
            OathmarkWalletBinding binding = proposal.OathmarkBinding;
            return new DeathPenaltyAtomicRevivalSnapshot(
                status,
                operationOverride ?? proposal.OperationId,
                requestFingerprintOverride ?? proposal.RequestFingerprint,
                deathFingerprintOverride ?? proposal.DeathFingerprint,
                accountOverride ?? proposal.AccountId,
                profileOverride ?? proposal.ProfileId,
                characterOverride ?? proposal.CharacterId,
                binding.TechnicalCurrencyId,
                binding.ProviderId,
                binding.BindingRevision,
                debitOverride ?? proposal.OathmarkDebitUnits,
                beforeBalanceOverride ?? proposal.BeforeOathmarkBalance,
                afterBalanceOverride ?? proposal.AfterOathmarkBalance,
                beforeWalletRevisionOverride ??
                    proposal.BeforeOathmarkWalletRevision,
                afterWalletRevision,
                beforeRevivalRevisionOverride ??
                    proposal.BeforeRevivalRevision,
                afterRevivalRevision,
                atomicCommitRevision,
                wasDeadBefore,
                isAliveAfter);
        }

        private static void AssertRejected(
            DeathPenaltyPlan plan,
            DeathPenaltyPlanStatus expectedStatus,
            string expectedDiagnostic)
        {
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Status, Is.EqualTo(expectedStatus));
            Assert.That(plan.DiagnosticCode, Is.EqualTo(expectedDiagnostic));
            Assert.That(plan.Proposal, Is.Null);
            Assert.That(plan.ReplayReceipt, Is.Null);
            Assert.That(plan.CanCommit, Is.False);
            Assert.That(plan.IsCommittedReplay, Is.False);
            Assert.That(plan.HasMutationProposal, Is.False);
        }
    }
}
