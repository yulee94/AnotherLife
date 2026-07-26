using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class ChampionEncounterPlannerTests
    {
        private const long Unit = CombatTechnicalLimits.MicrosPerUnit;

        [TestCase(CombatEncounterMode.Practice)]
        [TestCase(CombatEncounterMode.DevelopmentDemo)]
        public void PracticeAndDemoResolveOnlyNeutralRewardlessSessionContext(
            CombatEncounterMode mode)
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(mode);
            ChampionEncounterRequest request = Request(mode);
            ChampionEncounterRequestPlan plan =
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new ChampionEncounterRequestCorrelation[0]);

            Assert.AreEqual(ChampionEncounterRequestStatus.Resolved, plan.Status);
            Assert.False(plan.Resolved.HasDurableResultAuthority);
            Assert.False(plan.Resolved.RewardEligible);

            ChampionEncounterRequest withReward =
                CopyRequest(request, rewardOperationId: "reward.forbidden");
            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedRewardProhibited,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    withReward,
                    new ChampionEncounterRequestCorrelation[0]).Status);

            ChampionEncounterRequest withQuest =
                CopyRequest(request, questContextId: "quest.forbidden");
            Assert.AreEqual(
                ChampionEncounterRequestStatus
                    .RejectedQuestContextProhibited,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    withQuest,
                    new ChampionEncounterRequestCorrelation[0]).Status);

            ChampionEncounterRequest inventedRealm =
                CopyRequest(
                    request,
                    committedRealmId: "realm.uncommitted");
            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedRealmMismatch,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    inventedRealm,
                    new ChampionEncounterRequestCorrelation[0]).Status);
        }

        [Test]
        public void AuthoritativeBossRequiresCommittedRealmRewardAndNoQuestAuthority()
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(CombatEncounterMode.AuthoritativeBoss);
            ChampionEncounterRequest request =
                Request(CombatEncounterMode.AuthoritativeBoss);
            ChampionEncounterRequestPlan plan =
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new ChampionEncounterRequestCorrelation[0]);
            Assert.AreEqual(ChampionEncounterRequestStatus.Resolved, plan.Status);
            Assert.True(plan.Resolved.HasDurableResultAuthority);
            Assert.False(plan.Resolved.RewardEligible);

            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedRealmRequired,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    CopyRequest(
                        request,
                        committedRealmId: string.Empty,
                        realmVersion: string.Empty),
                    new ChampionEncounterRequestCorrelation[0]).Status);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedRealmMismatch,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    CopyRequest(
                        request,
                        committedRealmId: "realm.unknown"),
                    new ChampionEncounterRequestCorrelation[0]).Status);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedRewardRequired,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    CopyRequest(request, rewardOperationId: string.Empty),
                    new ChampionEncounterRequestCorrelation[0]).Status);
            Assert.AreEqual(
                ChampionEncounterRequestStatus
                    .RejectedQuestContextProhibited,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    CopyRequest(
                        request,
                        questContextId: "quest.invented"),
                    new ChampionEncounterRequestCorrelation[0]).Status);
        }

        [Test]
        public void AuthoritativeQuestRequiresProgressionContextAndRejectsFallbackSource()
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(CombatEncounterMode.AuthoritativeQuest);
            ChampionEncounterRequest request =
                Request(CombatEncounterMode.AuthoritativeQuest);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.Resolved,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new ChampionEncounterRequestCorrelation[0]).Status);

            Assert.AreEqual(
                ChampionEncounterRequestStatus
                    .RejectedQuestContextRequired,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    CopyRequest(request, questContextId: string.Empty),
                    new ChampionEncounterRequestCorrelation[0]).Status);

            ChampionEncounterDefinitionSnapshot fallback =
                Definition(
                    CombatEncounterMode.AuthoritativeQuest,
                    usesDevelopmentFallback: true);
            Assert.AreEqual(
                ChampionEncounterRequestStatus
                    .RejectedDevelopmentFallback,
                ChampionEncounterPlanner.PlanRequest(
                    fallback,
                    request,
                    new ChampionEncounterRequestCorrelation[0]).Status);
        }

        [Test]
        public void RequestDuplicateIsExactAndIdentityReuseConflictIsRejected()
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(CombatEncounterMode.AuthoritativeBoss);
            ChampionEncounterRequest request =
                Request(CombatEncounterMode.AuthoritativeBoss);
            var correlations = new[]
            {
                new ChampionEncounterRequestCorrelation(
                    definition,
                    request,
                    true)
            };
            ChampionEncounterRequestPlan duplicate =
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    correlations);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.DuplicateExact,
                duplicate.Status);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.DuplicateExact,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new[]
                    {
                        new ChampionEncounterRequestCorrelation(
                            definition,
                            request,
                            false)
                    }).Status);

            ChampionEncounterRequest changed =
                CopyRequest(request, rewardOperationId: "reward.changed");
            Assert.AreEqual(
                ChampionEncounterRequestStatus.CorrelationConflict,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    changed,
                    correlations).Status);

            ChampionEncounterRequest sameResultDifferentAttempt =
                CopyRequest(
                    request,
                    attemptId: "attempt.changed",
                    rewardOperationId: "reward.changed");
            Assert.AreEqual(
                ChampionEncounterRequestStatus.CorrelationConflict,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    sameResultDifferentAttempt,
                    correlations).Status);

            ChampionEncounterDefinitionSnapshot authorityDrift =
                CopyDefinition(
                    definition,
                    combatRulesProfileId:
                        "combat.rules.changed");
            Assert.AreEqual(
                ChampionEncounterRequestStatus.CorrelationConflict,
                ChampionEncounterPlanner.PlanRequest(
                    authorityDrift,
                    request,
                    correlations).Status);

            ChampionEncounterRequest rewardReuse =
                CopyRequest(
                    request,
                    sessionId: "session.reward-reuse",
                    attemptId: "attempt.reward-reuse",
                    resultId: "result.reward-reuse");
            Assert.AreEqual(
                ChampionEncounterRequestStatus.CorrelationConflict,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    rewardReuse,
                    new[]
                    {
                        new ChampionEncounterRequestCorrelation(
                            definition,
                            request,
                            false)
                    }).Status);
        }

        [Test]
        public void RequestCorrelationCapacityAllowsExactButRejectsNewIdentity()
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(CombatEncounterMode.AuthoritativeBoss);
            ChampionEncounterRequest template =
                Request(CombatEncounterMode.AuthoritativeBoss);
            var correlations =
                new List<ChampionEncounterRequestCorrelation>(
                    ChampionEncounterPlanner.MaximumRequestCorrelations);
            for (int index = 0;
                 index <
                 ChampionEncounterPlanner.MaximumRequestCorrelations;
                 index++)
            {
                ChampionEncounterRequest retained =
                    CopyRequest(
                        template,
                        sessionId: "session.capacity." + index,
                        attemptId: "attempt.capacity." + index,
                        resultId: "result.capacity." + index,
                        rewardOperationId:
                            "reward.capacity." + index);
                correlations.Add(
                    new ChampionEncounterRequestCorrelation(
                        definition,
                        retained,
                        false));
            }

            ChampionEncounterRequest exact =
                correlations[0].Request;
            Assert.AreEqual(
                ChampionEncounterRequestStatus.DuplicateExact,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    exact,
                    correlations).Status);

            ChampionEncounterRequest unseen = CopyRequest(
                template,
                sessionId: "session.capacity.unseen",
                attemptId: "attempt.capacity.unseen",
                resultId: "result.capacity.unseen",
                rewardOperationId: "reward.capacity.unseen");
            Assert.AreEqual(
                ChampionEncounterRequestStatus
                    .RejectedCorrelationLimit,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    unseen,
                    correlations).Status);
        }

        [Test]
        public void RequestVersionAndCorrelationLedgerAreFullyValidated()
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(CombatEncounterMode.Practice);
            ChampionEncounterRequest request =
                Request(CombatEncounterMode.Practice);
            string overlongVersion = new string(
                'v',
                CombatTechnicalLimits.MaximumVersionUtf8Bytes + 1);
            Assert.True(CombatPrimitiveValidation.IsStableId(overlongVersion));
            Assert.False(CombatPrimitiveValidation.IsVersion(overlongVersion));
            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedInvalidRequest,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    CopyRequest(
                        request,
                        encounterContentVersion: overlongVersion),
                    new ChampionEncounterRequestCorrelation[0]).Status);

            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedInvalidRequest,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new[]
                    {
                        new ChampionEncounterRequestCorrelation(
                            null,
                            null,
                            false)
                    }).Status);

            var existing =
                new ChampionEncounterRequestCorrelation(
                    definition,
                    request,
                    true);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedInvalidRequest,
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new[] { existing, existing }).Status);
        }

        [Test]
        public void RequestFingerprintIsCultureInvariantAndDelimiterSafe()
        {
            ChampionEncounterDefinitionSnapshot firstDefinition =
                Definition(
                    CombatEncounterMode.Practice,
                    gameId: "game|alpha",
                    catalogSetId: "catalog:beta");
            ChampionEncounterRequest firstRequest = Request(
                CombatEncounterMode.Practice,
                gameId: "game|alpha",
                catalogSetId: "catalog:beta");
            ChampionEncounterDefinitionSnapshot secondDefinition =
                Definition(
                    CombatEncounterMode.Practice,
                    gameId: "game",
                    catalogSetId: "alpha|catalog:beta");
            ChampionEncounterRequest secondRequest = Request(
                CombatEncounterMode.Practice,
                gameId: "game",
                catalogSetId: "alpha|catalog:beta");

            CultureInfo prior = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture =
                    CultureInfo.GetCultureInfo("fr-FR");
                string first = ChampionEncounterPlanner.PlanRequest(
                    firstDefinition,
                    firstRequest,
                    new ChampionEncounterRequestCorrelation[0])
                    .Resolved.SemanticFingerprint;
                Thread.CurrentThread.CurrentCulture =
                    CultureInfo.GetCultureInfo("ar-SA");
                string firstOtherCulture =
                    ChampionEncounterPlanner.PlanRequest(
                        firstDefinition,
                        firstRequest,
                        new ChampionEncounterRequestCorrelation[0])
                    .Resolved.SemanticFingerprint;
                string second = ChampionEncounterPlanner.PlanRequest(
                    secondDefinition,
                    secondRequest,
                    new ChampionEncounterRequestCorrelation[0])
                    .Resolved.SemanticFingerprint;
                Assert.AreEqual(first, firstOtherCulture);
                Assert.AreNotEqual(first, second);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = prior;
            }
        }

        [Test]
        public void EveryAllowedLifecycleTransitionAdvancesOneRevision()
        {
            var cases = new[]
            {
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Created,
                    CombatEncounterState.Validating),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Validating,
                    CombatEncounterState.Ready),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Validating,
                    CombatEncounterState.Failed),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Ready,
                    CombatEncounterState.Intro),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Ready,
                    CombatEncounterState.Cancelled),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Intro,
                    CombatEncounterState.Active),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Intro,
                    CombatEncounterState.Failed),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Intro,
                    CombatEncounterState.Cancelled),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Active,
                    CombatEncounterState.Resolving),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Active,
                    CombatEncounterState.Failed),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Active,
                    CombatEncounterState.Cancelled),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Resolving,
                    CombatEncounterState.Completed),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Resolving,
                    CombatEncounterState.Failed),
                Case(
                    CombatEncounterMode.Practice,
                    CombatEncounterState.Resolving,
                    CombatEncounterState.RecoveryRequired),
                Case(
                    CombatEncounterMode.AuthoritativeBoss,
                    CombatEncounterState.Resolving,
                    CombatEncounterState.CompletionPendingCommit),
                Case(
                    CombatEncounterMode.AuthoritativeBoss,
                    CombatEncounterState.CompletionPendingCommit,
                    CombatEncounterState.Completed),
                Case(
                    CombatEncounterMode.AuthoritativeBoss,
                    CombatEncounterState.CompletionPendingCommit,
                    CombatEncounterState.Failed),
                Case(
                    CombatEncounterMode.AuthoritativeBoss,
                    CombatEncounterState.CompletionPendingCommit,
                    CombatEncounterState.RecoveryRequired)
            };

            for (int index = 0; index < cases.Length; index++)
            {
                TransitionCase value = cases[index];
                ChampionEncounterStateSnapshot before = State(
                    value.Mode,
                    value.From,
                    Terminal(value.From),
                    revision: 10L,
                    elapsed: 2L * Unit);
                ChampionEncounterTransitionRequest request =
                    Transition(
                        "transition." + index,
                        before,
                        value.To,
                        Terminal(value.To),
                        3L * Unit);
                ChampionEncounterTransitionPlan plan =
                    ChampionEncounterPlanner.PlanTransition(
                        before,
                        request,
                        new ChampionEncounterTransitionReceipt[0]);
                Assert.AreEqual(
                    ChampionEncounterTransitionStatus.Applied,
                    plan.Status,
                    "case " + index);
                Assert.AreEqual(
                    before.Revision + 1L,
                    plan.After.Revision,
                    "case " + index);
                Assert.AreEqual(value.To, plan.After.State, "case " + index);
                Assert.AreEqual(
                    1,
                    plan.TechnicalEvents.Count(eventName =>
                        eventName == "EncounterStateChanged"),
                    "case " + index);
            }
        }

        [Test]
        public void ModeSpecificProhibitedAndTerminalLateTransitionsDoNotMutate()
        {
            ChampionEncounterStateSnapshot practiceResolving = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Resolving);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedModePolicy,
                PlanTransition(
                    practiceResolving,
                    CombatEncounterState.CompletionPendingCommit).Status);

            ChampionEncounterStateSnapshot authoritativeResolving = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Resolving);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedModePolicy,
                PlanTransition(
                    authoritativeResolving,
                    CombatEncounterState.Completed).Status);

            ChampionEncounterStateSnapshot created = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Created);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedTransition,
                PlanTransition(
                    created,
                    CombatEncounterState.Active).Status);

            ChampionEncounterStateSnapshot completed = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Completed,
                ChampionEncounterTerminalOutcome.ChampionVictory,
                revision: 8L);
            ChampionEncounterTransitionPlan late =
                PlanTransition(
                    completed,
                    CombatEncounterState.Failed);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.NoChangeTerminal,
                late.Status);
            Assert.AreSame(completed, late.After);
            Assert.IsEmpty(late.TechnicalEvents);

            ChampionEncounterTransitionPlan disposed =
                ChampionEncounterPlanner.PlanTransition(
                    completed,
                    Transition(
                        "transition.dispose-later",
                        completed,
                        CombatEncounterState.Disposed,
                        ChampionEncounterTerminalOutcome.ChampionVictory,
                        9L * Unit),
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.Applied,
                disposed.Status);
            Assert.AreEqual(
                9L * Unit,
                disposed.After.EncounterElapsedMicros);
            Assert.AreEqual(
                completed.TerminalOutcome,
                disposed.After.TerminalOutcome);
            Assert.AreEqual(
                9L * Unit,
                disposed.Receipt.Request.AtEncounterMicros);
            CollectionAssert.AreEqual(
                new[] { "EncounterStateChanged", "EncounterDisposed" },
                disposed.TechnicalEvents);

            ChampionEncounterStateSnapshot disposedAtMaximum =
                State(
                    CombatEncounterMode.AuthoritativeBoss,
                    CombatEncounterState.Disposed,
                    ChampionEncounterTerminalOutcome.ChampionVictory,
                    revision: long.MaxValue,
                    elapsed: 9L * Unit);
            ChampionEncounterTransitionPlan disposedNoOp =
                ChampionEncounterPlanner.PlanTransition(
                    disposedAtMaximum,
                    Transition(
                        "transition.disposed.no-op",
                        disposedAtMaximum,
                        CombatEncounterState.Disposed,
                        ChampionEncounterTerminalOutcome
                            .ChampionVictory,
                        10L * Unit),
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.NoChangeTerminal,
                disposedNoOp.Status);
            Assert.AreSame(
                disposedAtMaximum,
                disposedNoOp.After);
            Assert.IsEmpty(disposedNoOp.TechnicalEvents);
            Assert.IsEmpty(
                disposedNoOp.TechnicalEventReceipts);
        }

        [Test]
        public void TransitionReplayConflictClockAndLedgerValidationFailClosed()
        {
            ChampionEncounterStateSnapshot created = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Created);
            ChampionEncounterTransitionRequest request = Transition(
                "transition.replay",
                created,
                CombatEncounterState.Validating);
            ChampionEncounterTransitionPlan applied =
                ChampionEncounterPlanner.PlanTransition(
                    created,
                    request,
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.DuplicateExact,
                ChampionEncounterPlanner.PlanTransition(
                    applied.After,
                    request,
                    new[] { applied.Receipt }).Status);

            ChampionEncounterStateSnapshot foreignCreated = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Created,
                sessionId: "session.foreign",
                attemptId: "attempt.foreign");
            ChampionEncounterTransitionRequest foreignRequest =
                Transition(
                    "transition.foreign",
                    foreignCreated,
                    CombatEncounterState.Validating);
            ChampionEncounterTransitionPlan foreignApplied =
                ChampionEncounterPlanner.PlanTransition(
                    foreignCreated,
                    foreignRequest,
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedWrongEncounter,
                ChampionEncounterPlanner.PlanTransition(
                    created,
                    foreignRequest,
                    new[] { foreignApplied.Receipt }).Status);

            ChampionEncounterTransitionRequest changed =
                Transition(
                    request.TransitionId,
                    created,
                    CombatEncounterState.Failed,
                    ChampionEncounterTerminalOutcome.RuntimeFailure);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.CorrelationConflict,
                ChampionEncounterPlanner.PlanTransition(
                    applied.After,
                    changed,
                    new[] { applied.Receipt }).Status);

            var forged = new ChampionEncounterTransitionReceipt(
                null,
                "1:x",
                ChampionEncounterTransitionStatus.Applied,
                CombatEncounterState.Created,
                CombatEncounterState.Validating,
                0L,
                1L);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedInvalidRequest,
                ChampionEncounterPlanner.PlanTransition(
                    created,
                    request,
                    new[] { forged }).Status);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedInvalidRequest,
                ChampionEncounterPlanner.PlanTransition(
                    applied.After,
                    changed,
                    new[] { applied.Receipt, applied.Receipt }).Status);

            string overlong = new string('x', 9000);
            var forgedOverlongRequest =
                new ChampionEncounterTransitionRequest(
                    overlong,
                    created.EncounterSessionId,
                    created.EncounterAttemptId,
                    CombatEncounterState.Validating,
                    ChampionEncounterTerminalOutcome.None,
                    0L,
                    created.Revision);
            var forgedOverlongReceipt =
                new ChampionEncounterTransitionReceipt(
                    forgedOverlongRequest,
                    "1:x",
                    ChampionEncounterTransitionStatus.Applied,
                    CombatEncounterState.Created,
                    CombatEncounterState.Validating,
                    0L,
                    1L);
            ChampionEncounterTransitionPlan forgedOverlongPlan = null;
            Assert.DoesNotThrow(() =>
                forgedOverlongPlan =
                    ChampionEncounterPlanner.PlanTransition(
                        created,
                        request,
                        new[] { forgedOverlongReceipt }));
            Assert.AreEqual(
                ChampionEncounterTransitionStatus
                    .RejectedInvalidRequest,
                forgedOverlongPlan.Status);
            Assert.AreSame(created, forgedOverlongPlan.After);
            Assert.IsEmpty(forgedOverlongPlan.TechnicalEvents);

            ChampionEncounterStateSnapshot later = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Created,
                elapsed: 5L * Unit);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedInvalidClock,
                ChampionEncounterPlanner.PlanTransition(
                    later,
                    Transition(
                        "transition.backwards",
                        later,
                        CombatEncounterState.Validating,
                        at: 4L * Unit),
                    new ChampionEncounterTransitionReceipt[0]).Status);
        }

        [Test]
        public void TransitionReplayRejectsForkedAndSplicedAppliedEdges()
        {
            ChampionEncounterStateSnapshot created = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Created);
            ChampionEncounterTransitionPlan branchA =
                ChampionEncounterPlanner.PlanTransition(
                    created,
                    Transition(
                        "transition.branch.a",
                        created,
                        CombatEncounterState.Validating),
                    new ChampionEncounterTransitionReceipt[0]);
            ChampionEncounterTransitionPlan branchB =
                ChampionEncounterPlanner.PlanTransition(
                    created,
                    Transition(
                        "transition.branch.b",
                        created,
                        CombatEncounterState.Validating),
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus
                    .RejectedInvalidRequest,
                ChampionEncounterPlanner.PlanTransition(
                    branchA.After,
                    Transition(
                        "transition.after-fork",
                        branchA.After,
                        CombatEncounterState.Ready),
                    new[] { branchA.Receipt, branchB.Receipt })
                    .Status);

            var incompatibleValidating =
                new ChampionEncounterStateSnapshot(
                    branchA.After.EncounterSessionId,
                    branchA.After.EncounterAttemptId,
                    branchA.After.EncounterResultId,
                    branchA.After.RewardOperationId,
                    branchA.After.SourceSnapshotHash,
                    branchA.After.ChampionCombatProfileId,
                    branchA.After.BossCombatProfileId,
                    branchA.After.ParentEncounterAttemptId,
                    branchA.After.Mode,
                    CombatEncounterState.Validating,
                    ChampionEncounterTerminalOutcome.None,
                    Unit,
                    branchA.After.Revision);
            ChampionEncounterTransitionPlan spliced =
                ChampionEncounterPlanner.PlanTransition(
                    incompatibleValidating,
                    Transition(
                        "transition.splice",
                        incompatibleValidating,
                        CombatEncounterState.Ready,
                        at: Unit),
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus
                    .RejectedInvalidRequest,
                ChampionEncounterPlanner.PlanTransition(
                    spliced.After,
                    Transition(
                        "transition.after-splice",
                        spliced.After,
                        CombatEncounterState.Intro,
                        at: Unit),
                    new[] { branchA.Receipt, spliced.Receipt })
                    .Status);
        }

        [Test]
        public void TransitionReceiptCapacityAllowsExactButRejectsUnseen()
        {
            ChampionEncounterStateSnapshot terminal = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Completed,
                ChampionEncounterTerminalOutcome.ChampionVictory);
            var receipts =
                new List<ChampionEncounterTransitionReceipt>(
                    ChampionEncounterPlanner.MaximumTransitionReceipts);
            for (int index = 0;
                 index <
                 ChampionEncounterPlanner.MaximumTransitionReceipts;
                 index++)
            {
                ChampionEncounterTransitionPlan noChange =
                    ChampionEncounterPlanner.PlanTransition(
                        terminal,
                        Transition(
                            "transition.capacity." + index,
                            terminal,
                            CombatEncounterState.Failed,
                            ChampionEncounterTerminalOutcome
                                .RuntimeFailure),
                        new ChampionEncounterTransitionReceipt[0]);
                Assert.AreEqual(
                    ChampionEncounterTransitionStatus
                        .NoChangeTerminal,
                    noChange.Status);
                receipts.Add(noChange.Receipt);
            }

            Assert.AreEqual(
                ChampionEncounterTransitionStatus.DuplicateExact,
                ChampionEncounterPlanner.PlanTransition(
                    terminal,
                    receipts[0].Request,
                    receipts).Status);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.CapacityReached,
                ChampionEncounterPlanner.PlanTransition(
                    terminal,
                    Transition(
                        "transition.capacity.unseen",
                        terminal,
                        CombatEncounterState.Failed,
                        ChampionEncounterTerminalOutcome
                            .RuntimeFailure),
                    receipts).Status);
        }

        [Test]
        public void RetryUsesNewAttemptResultRewardAndValidatesEverySnapshot()
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(
                    CombatEncounterMode.AuthoritativeBoss,
                    allowsRetryAfterCompleted: true);
            ChampionEncounterRequest previousRequest =
                Request(CombatEncounterMode.AuthoritativeBoss);
            ResolvedChampionEncounterSnapshot previous =
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    previousRequest,
                    new ChampionEncounterRequestCorrelation[0]).Resolved;
            ChampionEncounterRequest retryRequest = CopyRequest(
                previousRequest,
                attemptId: "attempt.retry",
                resultId: "result.retry",
                rewardOperationId: "reward.retry");
            ResolvedChampionEncounterSnapshot retry =
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    retryRequest,
                    new ChampionEncounterRequestCorrelation[0]).Resolved;
            ChampionEncounterStateSnapshot terminal = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Completed,
                ChampionEncounterTerminalOutcome.ChampionVictory,
                sessionId: previousRequest.EncounterSessionId,
                attemptId: previousRequest.EncounterAttemptId,
                resultId: previousRequest.EncounterResultId);

            ChampionEncounterTransitionPlan planned =
                ChampionEncounterPlanner.PlanRetry(
                    terminal,
                    previous,
                    retry,
                    null);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RetryPlanned,
                planned.Status);
            Assert.AreEqual(
                CombatEncounterState.Created,
                planned.After.State);
            Assert.AreEqual(0L, planned.After.Revision);
            Assert.AreEqual(
                previousRequest.EncounterAttemptId,
                planned.After.ParentEncounterAttemptId);
            ChampionEncounterTechnicalEventReceipt retryEvent =
                planned.TechnicalEventReceipts.Single();
            Assert.AreEqual(
                ChampionEncounterTechnicalEventKind.RetryPlanned,
                retryEvent.Kind);
            Assert.True(retryEvent.IsCrossAttempt);
            Assert.AreEqual(
                terminal.EncounterAttemptId,
                retryEvent.PreviousEncounterAttemptId);
            Assert.AreEqual(
                planned.After.EncounterAttemptId,
                retryEvent.EncounterAttemptId);
            Assert.AreEqual(
                terminal.Revision,
                retryEvent.BeforeRevision);
            Assert.AreEqual(
                planned.After.Revision,
                retryEvent.AfterRevision);

            Assert.AreEqual(
                ChampionEncounterTransitionStatus.DuplicateExact,
                ChampionEncounterPlanner.PlanRetry(
                    terminal,
                    previous,
                    retry,
                    planned.After).Status);

            ChampionEncounterDefinitionSnapshot driftedDefinition =
                CopyDefinition(
                    definition,
                    combatRulesProfileId:
                        "rules.retry-drift");
            ResolvedChampionEncounterSnapshot driftedRetry =
                Resolve(driftedDefinition, retryRequest);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus
                    .RejectedRetryIdentity,
                ChampionEncounterPlanner.PlanRetry(
                    terminal,
                    previous,
                    driftedRetry,
                    null).Status);

            ChampionEncounterStateSnapshot changedRewardExisting =
                State(
                    CombatEncounterMode.AuthoritativeBoss,
                    CombatEncounterState.Created,
                    sessionId:
                        planned.After.EncounterSessionId,
                    attemptId:
                        planned.After.EncounterAttemptId,
                    resultId:
                        planned.After.EncounterResultId,
                    parentAttemptId:
                        planned.After.ParentEncounterAttemptId,
                    rewardOperationId: "reward.changed");
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.CorrelationConflict,
                ChampionEncounterPlanner.PlanRetry(
                    terminal,
                    previous,
                    retry,
                    changedRewardExisting).Status);

            var malformedExisting = new ChampionEncounterStateSnapshot(
                planned.After.EncounterSessionId,
                planned.After.EncounterAttemptId,
                planned.After.EncounterResultId,
                planned.After.RewardOperationId,
                planned.After.SourceSnapshotHash,
                planned.After.ChampionCombatProfileId,
                planned.After.BossCombatProfileId,
                planned.After.ParentEncounterAttemptId,
                planned.After.Mode,
                planned.After.State,
                planned.After.TerminalOutcome,
                1L,
                1L);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedInvalidState,
                ChampionEncounterPlanner.PlanRetry(
                    terminal,
                    previous,
                    retry,
                    malformedExisting).Status);

            ChampionEncounterStateSnapshot unrelatedTerminal = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Completed,
                ChampionEncounterTerminalOutcome.ChampionVictory,
                sessionId: previousRequest.EncounterSessionId,
                attemptId: previousRequest.EncounterAttemptId,
                resultId: "result.unrelated");
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedRetryIdentity,
                ChampionEncounterPlanner.PlanRetry(
                    unrelatedTerminal,
                    previous,
                    retry,
                    null).Status);

            var forged = new ResolvedChampionEncounterSnapshot(
                definition,
                retryRequest,
                "forged",
                new string('f', 64),
                true,
                false);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedInvalidState,
                ChampionEncounterPlanner.PlanRetry(
                    terminal,
                    previous,
                    forged,
                    null).Status);

            ChampionEncounterStateSnapshot recovery = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.RecoveryRequired,
                ChampionEncounterTerminalOutcome.RecoveryRequired,
                sessionId: previousRequest.EncounterSessionId,
                attemptId: previousRequest.EncounterAttemptId,
                resultId: previousRequest.EncounterResultId);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.RejectedRecoveryPending,
                ChampionEncounterPlanner.PlanRetry(
                    recovery,
                    previous,
                    retry,
                    null).Status);
        }

        [Test]
        public void ComputedOutcomeIsBoundedImmutableAndMatchesTerminalMeaning()
        {
            ChampionEncounterStateSnapshot resolving = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Resolving,
                elapsed: 12L * Unit,
                includeFrozenOutcome: false);
            ChampionEncounterResolutionEvidence victoryEvidence =
                ResolutionEvidence(
                    resolving,
                    ChampionEncounterOutcome.ChampionVictory);
            var metrics = new List<EncounterMetricSnapshot>();
            ChampionEncounterOutcomePlan plan =
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    victoryEvidence,
                    metrics,
                    new string('a', 64),
                    string.Empty);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.Computed,
                plan.Status);
            Assert.AreEqual(
                ChampionEncounterOutcome.ChampionVictory,
                plan.ComputedOutcome.Outcome);
            Assert.AreEqual(
                resolving.EncounterResultId,
                plan.ComputedOutcome.EncounterResultId);
            Assert.AreEqual(
                12L * Unit,
                plan.ComputedOutcome.EncounterDurationMicros);
            Assert.True(plan.ComputedOutcome.RewardEligible);
            Assert.AreSame(
                victoryEvidence,
                plan.ComputedOutcome.ResolutionEvidence);
            Assert.AreEqual(
                resolving.Revision + 1L,
                plan.After.Revision);
            CollectionAssert.AreEqual(
                new[] { "EncounterOutcomeComputed" },
                plan.TechnicalEvents);
            Assert.True(
                CombatPrimitiveValidation.IsSha256(
                    plan.ComputedOutcome.OutcomeHash));
            Assert.AreEqual(0, plan.ComputedOutcome.Metrics.Count);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)plan.ComputedOutcome.Metrics).Clear());

            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus
                    .RejectedInvalidMetric,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    victoryEvidence,
                    new[]
                    {
                        new EncounterMetricSnapshot(
                            "metric.unissued",
                            CombatScalarKind.Damage,
                            Unit,
                            "unit.micros")
                    },
                    resolving.SourceSnapshotHash,
                    string.Empty).Status);

            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus
                    .CorrelationConflict,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    plan.After,
                    ResolutionEvidence(
                        resolving,
                        ChampionEncounterOutcome.ChampionDefeat),
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    string.Empty).Status);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.RejectedInvalidOutcome,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    null,
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    new string('b', 64)).Status);

            ChampionEncounterStateSnapshot failed = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Failed,
                ChampionEncounterTerminalOutcome.ValidationFailure);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.RejectedInvalidState,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    failed,
                    null,
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    string.Empty).Status);
        }

        [Test]
        public void ComputedOutcomeHashIsDeterministicAndRejectsTampering()
        {
            ChampionEncounterStateSnapshot resolving = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Resolving,
                elapsed: 9L * Unit,
                includeFrozenOutcome: false);
            ChampionEncounterResolutionEvidence evidence =
                ResolutionEvidence(
                    resolving,
                    ChampionEncounterOutcome.ChampionVictory);
            ChampionEncounterOutcomePlan first =
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    evidence,
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    string.Empty);
            ChampionEncounterOutcomePlan second =
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    evidence,
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    string.Empty);
            Assert.AreEqual(
                first.ComputedOutcome.OutcomeHash,
                second.ComputedOutcome.OutcomeHash);
            Assert.IsEmpty(first.ComputedOutcome.Metrics);

            string tampered = first.ComputedOutcome.OutcomeHash[0] == 'a'
                ? "b" + first.ComputedOutcome.OutcomeHash.Substring(1)
                : "a" + first.ComputedOutcome.OutcomeHash.Substring(1);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.RejectedInvalidHash,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    evidence,
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    tampered).Status);
        }

        [Test]
        public void ComputedOutcomeRejectsMetricHashIdentityAndStateForgery()
        {
            ChampionEncounterStateSnapshot resolving = State(
                CombatEncounterMode.Practice,
                CombatEncounterState.Resolving,
                includeFrozenOutcome: false);
            ChampionEncounterResolutionEvidence evidence =
                ResolutionEvidence(
                    resolving,
                    ChampionEncounterOutcome.ChampionDefeat);
            var duplicateMetrics = new[]
            {
                new EncounterMetricSnapshot(
                    "metric.same",
                    CombatScalarKind.Damage,
                    1L,
                    "unit.micros"),
                new EncounterMetricSnapshot(
                    "metric.same",
                    CombatScalarKind.Damage,
                    2L,
                    "unit.micros")
            };
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.RejectedInvalidMetric,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    evidence,
                    duplicateMetrics,
                    new string('a', 64),
                    new string('b', 64)).Status);

            var tooManyMetrics = Enumerable.Range(
                    0,
                    ChampionEncounterPlanner.MaximumOutcomeMetrics + 1)
                .Select(index => new EncounterMetricSnapshot(
                    "metric." + index,
                    CombatScalarKind.Damage,
                    1L,
                    "unit.micros"))
                .ToList();
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.RejectedMetricLimit,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    evidence,
                    tooManyMetrics,
                    new string('a', 64),
                    new string('b', 64)).Status);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.RejectedInvalidHash,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    evidence,
                    new EncounterMetricSnapshot[0],
                    "not-a-hash",
                    new string('b', 64)).Status);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus
                    .RejectedInvalidOutcome,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    new ChampionEncounterResolutionEvidence(
                        evidence.EncounterSessionId,
                        evidence.EncounterAttemptId,
                        evidence.SourceSnapshotHash,
                        evidence.ChampionCombatProfileId,
                        evidence.BossCombatProfileId,
                        "participant.same",
                        "participant.same",
                        evidence.ChampionLifeState,
                        evidence.BossLifeState,
                        evidence.ChampionResourceRevision,
                        evidence.BossStateRevision,
                        evidence.ExpectedEncounterRevision,
                        evidence.ResolutionElapsedMicros,
                        evidence.Outcome,
                        evidence.EvidenceHash,
                        false),
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    new string('b', 64)).Status);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.RejectedInvalidState,
                ChampionEncounterPlanner.PlanComputedOutcome(
                    State(
                        CombatEncounterMode.Practice,
                        CombatEncounterState.Active),
                    evidence,
                    new EncounterMetricSnapshot[0],
                    new string('a', 64),
                    new string('b', 64)).Status);
        }

        [TestCase(
            ChampionEncounterOutcome.ChampionVictory,
            ChampionEncounterTerminalOutcome.ChampionVictory,
            true)]
        [TestCase(
            ChampionEncounterOutcome.ChampionDefeat,
            ChampionEncounterTerminalOutcome.ChampionDefeat,
            false)]
        public void AuthoritativeOutcomeFreezesBeforeCommitAndCompletion(
            ChampionEncounterOutcome outcome,
            ChampionEncounterTerminalOutcome terminalOutcome,
            bool rewardEligible)
        {
            ChampionEncounterStateSnapshot resolving = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Resolving,
                revision: 5L,
                elapsed: 5L * Unit,
                includeFrozenOutcome: false);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus
                    .RejectedTerminalConflict,
                ChampionEncounterPlanner.PlanTransition(
                    resolving,
                    Transition(
                        "transition.pending.unfrozen",
                        resolving,
                        CombatEncounterState
                            .CompletionPendingCommit,
                        at: 6L * Unit),
                    new ChampionEncounterTransitionReceipt[0])
                    .Status);

            ChampionEncounterOutcomePlan frozen =
                ChampionEncounterPlanner.PlanComputedOutcome(
                    resolving,
                    ResolutionEvidence(resolving, outcome),
                    new EncounterMetricSnapshot[0],
                    resolving.SourceSnapshotHash,
                    string.Empty);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.Computed,
                frozen.Status);
            Assert.AreEqual(
                rewardEligible,
                frozen.ComputedOutcome.RewardEligible);
            Assert.AreEqual(
                5L * Unit,
                frozen.ComputedOutcome.EncounterDurationMicros);
            Assert.AreEqual(
                ChampionEncounterTechnicalEventKind.OutcomeComputed,
                frozen.TechnicalEventReceipts.Single().Kind);

            ChampionEncounterTransitionPlan pending =
                ChampionEncounterPlanner.PlanTransition(
                    frozen.After,
                    Transition(
                        "transition.pending.frozen",
                        frozen.After,
                        CombatEncounterState
                            .CompletionPendingCommit,
                        at: 6L * Unit),
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.Applied,
                pending.Status);
            Assert.AreEqual(
                6L * Unit,
                pending.After.EncounterElapsedMicros);
            Assert.AreSame(
                frozen.ComputedOutcome,
                pending.After.FrozenOutcome);

            ChampionEncounterTransitionPlan completed =
                ChampionEncounterPlanner.PlanTransition(
                    pending.After,
                    Transition(
                        "transition.completed.frozen",
                        pending.After,
                        CombatEncounterState.Completed,
                        terminalOutcome,
                        7L * Unit),
                    new ChampionEncounterTransitionReceipt[0]);
            Assert.AreEqual(
                ChampionEncounterTransitionStatus.Applied,
                completed.Status);
            Assert.AreEqual(
                7L * Unit,
                completed.After.EncounterElapsedMicros);
            Assert.AreSame(
                frozen.ComputedOutcome,
                completed.After.FrozenOutcome);
            Assert.AreEqual(
                5L * Unit,
                completed.After.FrozenOutcome
                    .EncounterDurationMicros);
            Assert.AreEqual(
                frozen.ComputedOutcome.OutcomeHash,
                completed.After.FrozenOutcome.OutcomeHash);
            Assert.AreEqual(
                completed.Before.Revision,
                completed.TechnicalEventReceipts[0]
                    .BeforeRevision);
            Assert.AreEqual(
                completed.After.Revision,
                completed.TechnicalEventReceipts[0]
                    .AfterRevision);
        }

        [Test]
        public void ResolutionEvidenceRejectsUnissuedLifeAndSourceForks()
        {
            ChampionEncounterStateSnapshot resolving = State(
                CombatEncounterMode.AuthoritativeBoss,
                CombatEncounterState.Resolving,
                elapsed: 3L * Unit,
                includeFrozenOutcome: false);
            ChampionEncounterResolutionEvidence valid =
                ResolutionEvidence(
                    resolving,
                    ChampionEncounterOutcome.ChampionVictory);
            ChampionEncounterResolutionEvidence[] forged =
            {
                CopyEvidence(valid, plannerIssued: false),
                CopyEvidence(
                    valid,
                    championLifeState:
                        CombatantLifeState.Alive,
                    bossLifeState:
                        CombatantLifeState.Alive),
                CopyEvidence(
                    valid,
                    championLifeState:
                        CombatantLifeState.Defeated,
                    bossLifeState:
                        CombatantLifeState.Defeated),
                CopyEvidence(
                    valid,
                    sourceSnapshotHash: new string('f', 64)),
                CopyEvidence(
                    valid,
                    bossCombatProfileId:
                        "boss.combat.fork")
            };

            foreach (
                ChampionEncounterResolutionEvidence value in forged)
            {
                Assert.AreEqual(
                    ChampionEncounterOutcomePlanStatus
                        .RejectedInvalidOutcome,
                    ChampionEncounterPlanner.PlanComputedOutcome(
                        resolving,
                        value,
                        new EncounterMetricSnapshot[0],
                        resolving.SourceSnapshotHash,
                        string.Empty).Status);
            }
        }

        [Test]
        public void InitialConstructionRevalidatesResolvedAndPracticeRealmAuthority()
        {
            ChampionEncounterDefinitionSnapshot definition =
                Definition(CombatEncounterMode.Practice);
            ChampionEncounterRequest request =
                Request(CombatEncounterMode.Practice);
            ResolvedChampionEncounterSnapshot resolved =
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new ChampionEncounterRequestCorrelation[0]).Resolved;
            Assert.NotNull(
                ChampionEncounterPlanner.CreateInitialState(resolved));

            var forged = new ResolvedChampionEncounterSnapshot(
                definition,
                request,
                "forged",
                new string('f', 64),
                false,
                false);
            Assert.IsNull(
                ChampionEncounterPlanner.CreateInitialState(forged));

            ChampionEncounterDefinitionSnapshot fallbackAuthority =
                Definition(
                    CombatEncounterMode.AuthoritativeBoss,
                    usesDevelopmentFallback: true);
            ChampionEncounterRequest authoritativeRequest =
                Request(CombatEncounterMode.AuthoritativeBoss);
            var forgedFallback =
                new ResolvedChampionEncounterSnapshot(
                    fallbackAuthority,
                    authoritativeRequest,
                    resolved.SemanticFingerprint,
                    resolved.SourceSnapshotHash,
                    true,
                    false);
            Assert.IsNull(
                ChampionEncounterPlanner.CreateInitialState(
                    forgedFallback));

            var ambiguousPractice =
                new ChampionEncounterDefinitionSnapshot(
                    definition.GameId,
                    definition.CatalogSetId,
                    definition.RequiredProfileId,
                    definition.EncounterDefinitionId,
                    definition.SchemaVersion,
                    definition.ContentVersion,
                    definition.Mode,
                    definition.ChampionDefinitionId,
                    definition.ChampionCombatProfileId,
                    definition.SkillLoadoutId,
                    definition.BossDefinitionId,
                    definition.BossCombatProfileId,
                    definition.CombatRulesProfileId,
                    definition.ArenaProfileId,
                    definition.NeutralRealmContextId,
                    "realm-v1",
                    new[] { "realm.stone" },
                    definition.ExpectedProfileRevision,
                    false,
                    false,
                    true,
                    true);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.RejectedInvalidDefinition,
                ChampionEncounterPlanner.PlanRequest(
                    ambiguousPractice,
                    request,
                    new ChampionEncounterRequestCorrelation[0]).Status);
        }

        [Test]
        public void RetryRejectsEveryImmutableSourceRealmAndQuestContextDrift()
        {
            ChampionEncounterDefinitionSnapshot previousDefinition =
                Definition(
                    CombatEncounterMode.AuthoritativeQuest,
                    allowsRetryAfterCompleted: true);
            ChampionEncounterRequest previousRequest =
                Request(CombatEncounterMode.AuthoritativeQuest);
            ResolvedChampionEncounterSnapshot previous =
                Resolve(previousDefinition, previousRequest);
            ChampionEncounterRequest baseRetry = CopyRequest(
                previousRequest,
                attemptId: "attempt.retry.context",
                resultId: "result.retry.context",
                rewardOperationId: "reward.retry.context");
            ChampionEncounterStateSnapshot terminal = State(
                CombatEncounterMode.AuthoritativeQuest,
                CombatEncounterState.Completed,
                ChampionEncounterTerminalOutcome.ChampionVictory,
                sessionId: previousRequest.EncounterSessionId,
                attemptId: previousRequest.EncounterAttemptId,
                resultId: previousRequest.EncounterResultId);

            var variants = new List<ResolvedChampionEncounterSnapshot>
            {
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        contentVersion: "encounter-v2"),
                    CopyRequest(
                        baseRetry,
                        encounterContentVersion: "encounter-v2")),
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        championDefinitionId: "champion.changed"),
                    CopyRequest(
                        baseRetry,
                        championDefinitionId: "champion.changed")),
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        championCombatProfileId:
                            "champion.combat.changed"),
                    CopyRequest(
                        baseRetry,
                        championCombatProfileId:
                            "champion.combat.changed")),
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        skillLoadoutId: "loadout.changed"),
                    CopyRequest(
                        baseRetry,
                        skillLoadoutId: "loadout.changed")),
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        bossDefinitionId: "boss.changed"),
                    CopyRequest(
                        baseRetry,
                        bossDefinitionId: "boss.changed")),
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        bossCombatProfileId: "boss.combat.changed"),
                    CopyRequest(
                        baseRetry,
                        bossCombatProfileId: "boss.combat.changed")),
                Resolve(
                    previousDefinition,
                    CopyRequest(
                        baseRetry,
                        committedRealmId: "realm.forest")),
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        realmDefinitionVersion: "realm-v2"),
                    CopyRequest(
                        baseRetry,
                        realmVersion: "realm-v2")),
                Resolve(
                    previousDefinition,
                    CopyRequest(
                        baseRetry,
                        questContextId: "quest.context.changed")),
                Resolve(
                    previousDefinition,
                    CopyRequest(
                        baseRetry,
                        resumeToken: "resume.changed")),
                Resolve(
                    CopyDefinition(
                        previousDefinition,
                        profileRevision: "profile-r2"),
                    CopyRequest(
                        baseRetry,
                        expectedProfileRevision: "profile-r2"))
            };

            for (int index = 0; index < variants.Count; index++)
            {
                Assert.AreEqual(
                    ChampionEncounterTransitionStatus
                        .RejectedRetryIdentity,
                    ChampionEncounterPlanner.PlanRetry(
                        terminal,
                        previous,
                        variants[index],
                        null).Status,
                    "drift case " + index);
            }
        }

        private static ChampionEncounterRequestPlan Plan(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request)
        {
            return ChampionEncounterPlanner.PlanRequest(
                definition,
                request,
                new ChampionEncounterRequestCorrelation[0]);
        }

        private static ChampionEncounterTransitionPlan PlanTransition(
            ChampionEncounterStateSnapshot state,
            CombatEncounterState target,
            ChampionEncounterTerminalOutcome? terminal = null)
        {
            return ChampionEncounterPlanner.PlanTransition(
                state,
                Transition(
                    "transition." + state.State + "." + target,
                    state,
                    target,
                    terminal ?? Terminal(target)),
                new ChampionEncounterTransitionReceipt[0]);
        }

        private static ChampionEncounterTransitionRequest Transition(
            string id,
            ChampionEncounterStateSnapshot state,
            CombatEncounterState target,
            ChampionEncounterTerminalOutcome terminal =
                ChampionEncounterTerminalOutcome.None,
            long? at = null)
        {
            return new ChampionEncounterTransitionRequest(
                id,
                state.EncounterSessionId,
                state.EncounterAttemptId,
                target,
                terminal,
                at ?? state.EncounterElapsedMicros,
                state.Revision);
        }

        private static ChampionEncounterStateSnapshot State(
            CombatEncounterMode mode,
            CombatEncounterState state,
            ChampionEncounterTerminalOutcome terminal =
                ChampionEncounterTerminalOutcome.None,
            long revision = 0L,
            long elapsed = 0L,
            string sessionId = "session.encounter",
            string attemptId = "attempt.encounter",
            string resultId = "result.encounter",
            string parentAttemptId = "",
            string rewardOperationId = null,
            bool includeFrozenOutcome = true)
        {
            bool authoritative =
                mode == CombatEncounterMode.AuthoritativeBoss ||
                mode == CombatEncounterMode.AuthoritativeQuest;
            var snapshot = new ChampionEncounterStateSnapshot(
                sessionId,
                attemptId,
                resultId,
                authoritative
                    ? rewardOperationId ?? "reward.test"
                    : string.Empty,
                new string('a', 64),
                "champion.combat.test",
                "boss.combat.test",
                parentAttemptId,
                mode,
                state,
                terminal,
                elapsed,
                revision);
            if (!includeFrozenOutcome ||
                (state != CombatEncounterState.Resolving &&
                 state !=
                     CombatEncounterState.CompletionPendingCommit &&
                 state != CombatEncounterState.Completed &&
                 state != CombatEncounterState.Disposed))
            {
                return snapshot;
            }

            ChampionEncounterOutcome outcome =
                terminal == ChampionEncounterTerminalOutcome
                    .ChampionDefeat
                    ? ChampionEncounterOutcome.ChampionDefeat
                    : ChampionEncounterOutcome.ChampionVictory;
            ChampionEncounterStateSnapshot resolving =
                state == CombatEncounterState.Resolving
                    ? snapshot
                    : new ChampionEncounterStateSnapshot(
                        sessionId,
                        attemptId,
                        resultId,
                        authoritative
                            ? rewardOperationId ?? "reward.test"
                            : string.Empty,
                        snapshot.SourceSnapshotHash,
                        snapshot.ChampionCombatProfileId,
                        snapshot.BossCombatProfileId,
                        parentAttemptId,
                        mode,
                        CombatEncounterState.Resolving,
                        ChampionEncounterTerminalOutcome.None,
                        elapsed,
                        Math.Max(0L, revision - 1L));
            ChampionEncounterOutcomePlan frozen =
                FreezeOutcome(resolving, outcome);
            Assert.AreEqual(
                ChampionEncounterOutcomePlanStatus.Computed,
                frozen.Status);
            long finalRevision = Math.Max(
                revision,
                frozen.After.Revision);
            return new ChampionEncounterStateSnapshot(
                sessionId,
                attemptId,
                resultId,
                authoritative
                    ? rewardOperationId ?? "reward.test"
                    : string.Empty,
                snapshot.SourceSnapshotHash,
                snapshot.ChampionCombatProfileId,
                snapshot.BossCombatProfileId,
                parentAttemptId,
                mode,
                state,
                terminal,
                elapsed,
                finalRevision,
                frozen.ComputedOutcome);
        }

        private static ChampionEncounterTerminalOutcome Terminal(
            CombatEncounterState state)
        {
            switch (state)
            {
                case CombatEncounterState.Completed:
                    return ChampionEncounterTerminalOutcome.ChampionVictory;
                case CombatEncounterState.Failed:
                    return ChampionEncounterTerminalOutcome.RuntimeFailure;
                case CombatEncounterState.Cancelled:
                    return ChampionEncounterTerminalOutcome.Cancelled;
                case CombatEncounterState.RecoveryRequired:
                    return ChampionEncounterTerminalOutcome.RecoveryRequired;
                default:
                    return ChampionEncounterTerminalOutcome.None;
            }
        }

        private static ChampionEncounterOutcomePlan FreezeOutcome(
            ChampionEncounterStateSnapshot resolving,
            ChampionEncounterOutcome outcome)
        {
            return ChampionEncounterPlanner.PlanComputedOutcome(
                resolving,
                ResolutionEvidence(resolving, outcome),
                new EncounterMetricSnapshot[0],
                resolving.SourceSnapshotHash,
                string.Empty);
        }

        private static ChampionEncounterResolutionEvidence
            ResolutionEvidence(
                ChampionEncounterStateSnapshot resolving,
                ChampionEncounterOutcome outcome)
        {
            Assert.AreEqual(
                CombatEncounterState.Resolving,
                resolving.State);
            Assert.True(
                CombatStableId.TryCreate(
                    resolving.EncounterSessionId,
                    out CombatStableId sessionId));
            Assert.True(
                CombatStableId.TryCreate(
                    resolving.EncounterAttemptId,
                    out CombatStableId attemptId));
            Assert.True(
                CombatStableId.TryCreate(
                    "participant.champion",
                    out CombatStableId championId));
            long championHealth =
                outcome == ChampionEncounterOutcome.ChampionDefeat
                    ? 0L
                    : 10L * Unit;
            Assert.True(
                CombatantResourceSnapshot.TryCreate(
                    sessionId,
                    attemptId,
                    championId,
                    championHealth,
                    10L * Unit,
                    10L * Unit,
                    10L * Unit,
                    out CombatantResourceSnapshot champion));

            BossStatePolicy bossPolicy = EvidenceBossPolicy();
            BossStateSnapshot boss = BossStatePlanner.CreateInitial(
                bossPolicy,
                resolving.EncounterSessionId,
                resolving.EncounterAttemptId,
                "participant.boss").After;
            if (outcome ==
                ChampionEncounterOutcome.ChampionVictory)
            {
                BossStateTransitionPlan defeated =
                    BossStatePlanner.PlanTransition(
                        bossPolicy,
                        boss,
                        new BossStateOperationRequest(
                             "operation.evidence.defeat",
                             boss.EncounterSessionId,
                             boss.EncounterAttemptId,
                             "action.evidence.defeat",
                             "participant.champion",
                             "behavior.evidence.damage",
                             BossStateOperationKind.Damage,
                            boss.CurrentHealthMicros,
                            boss.EncounterElapsedMicros,
                            boss.Revision),
                        new BossStateOperationReceipt[0]);
                Assert.AreEqual(
                    BossStateTransitionStatus.AppliedAndDefeated,
                    defeated.Status);
                boss = defeated.After;
            }

            CombatantLifeState championLife =
                champion.LifeState;
            var registry = new[]
            {
                EvidenceParticipant(
                    "participant.champion",
                    resolving.ChampionCombatProfileId,
                    CombatParticipantRole.Champion,
                    championLife,
                    resolving),
                EvidenceParticipant(
                    "participant.boss",
                    resolving.BossCombatProfileId,
                    CombatParticipantRole.Boss,
                    boss.LifeState,
                    resolving)
            };
            Assert.True(
                ChampionEncounterPlanner.TryCreateResolutionEvidence(
                    resolving,
                    registry,
                    champion,
                    boss,
                    out ChampionEncounterResolutionEvidence evidence));
            Assert.AreEqual(outcome, evidence.Outcome);
            return evidence;
        }

        private static CombatParticipantRegistration
            EvidenceParticipant(
                string participantId,
                string actorProfileId,
                CombatParticipantRole role,
                CombatantLifeState lifeState,
                ChampionEncounterStateSnapshot encounter)
        {
            return new CombatParticipantRegistration(
                participantId,
                actorProfileId,
                role,
                role == CombatParticipantRole.Champion
                    ? "team.champion"
                    : "team.boss",
                "realm.test",
                lifeState,
                lifeState == CombatantLifeState.Defeated
                    ? CombatantControlState.Defeated
                    : CombatantControlState.Manual,
                string.Empty,
                lifeState == CombatantLifeState.Alive,
                "handle." + participantId,
                1L,
                "target.profile.test",
                encounter.EncounterSessionId,
                encounter.EncounterAttemptId,
                0f,
                0f,
                0f,
                "unit.meter");
        }

        private static ChampionEncounterResolutionEvidence
            CopyEvidence(
                ChampionEncounterResolutionEvidence source,
                CombatantLifeState? championLifeState = null,
                CombatantLifeState? bossLifeState = null,
                string sourceSnapshotHash = null,
                string bossCombatProfileId = null,
                bool plannerIssued = true)
        {
            return new ChampionEncounterResolutionEvidence(
                source.EncounterSessionId,
                source.EncounterAttemptId,
                sourceSnapshotHash ?? source.SourceSnapshotHash,
                source.ChampionCombatProfileId,
                bossCombatProfileId ??
                    source.BossCombatProfileId,
                source.ChampionParticipantId,
                source.BossParticipantId,
                championLifeState ?? source.ChampionLifeState,
                bossLifeState ?? source.BossLifeState,
                source.ChampionResourceRevision,
                source.BossStateRevision,
                source.ExpectedEncounterRevision,
                source.ResolutionElapsedMicros,
                source.Outcome,
                source.EvidenceHash,
                plannerIssued);
        }

        private static BossStatePolicy EvidenceBossPolicy()
        {
            return new BossStatePolicy(
                "boss.combat.test",
                BossStatePlanner.CurrentPolicyVersion,
                10L * Unit,
                5L * Unit,
                Unit,
                false,
                0L,
                false,
                0L,
                Unit,
                Unit,
                new[]
                {
                    new BossPhaseDefinition(
                        "boss.phase.evidence",
                        Unit,
                        Unit)
                });
        }

        private static TransitionCase Case(
            CombatEncounterMode mode,
            CombatEncounterState from,
            CombatEncounterState to)
        {
            return new TransitionCase(mode, from, to);
        }

        private sealed class TransitionCase
        {
            public TransitionCase(
                CombatEncounterMode mode,
                CombatEncounterState from,
                CombatEncounterState to)
            {
                Mode = mode;
                From = from;
                To = to;
            }

            public CombatEncounterMode Mode { get; }
            public CombatEncounterState From { get; }
            public CombatEncounterState To { get; }
        }

        private static ChampionEncounterDefinitionSnapshot Definition(
            CombatEncounterMode mode,
            bool usesDevelopmentFallback = false,
            bool allowsRetryAfterCompleted = false,
            string gameId = "anotherlife",
            string catalogSetId = "catalog.combat.test")
        {
            return new ChampionEncounterDefinitionSnapshot(
                gameId,
                catalogSetId,
                "profile.test",
                "encounter.test",
                CombatTechnicalLimits.SupportedSchemaVersion,
                "encounter-v1",
                mode,
                "champion.test",
                "champion.combat.test",
                "loadout.test",
                "boss.test",
                "boss.combat.test",
                "rules.test",
                "arena.test",
                "realm.neutral",
                mode == CombatEncounterMode.Practice ||
                mode == CombatEncounterMode.DevelopmentDemo
                    ? string.Empty
                    : "realm-v1",
                mode == CombatEncounterMode.Practice ||
                mode == CombatEncounterMode.DevelopmentDemo
                    ? new string[0]
                    : new[] { "realm.stone", "realm.forest" },
                "profile-r1",
                usesDevelopmentFallback,
                allowsRetryAfterCompleted,
                true,
                true);
        }

        private static ChampionEncounterRequest Request(
            CombatEncounterMode mode,
            string gameId = "anotherlife",
            string catalogSetId = "catalog.combat.test")
        {
            bool authoritative =
                mode == CombatEncounterMode.AuthoritativeBoss ||
                mode == CombatEncounterMode.AuthoritativeQuest;
            return new ChampionEncounterRequest(
                gameId,
                catalogSetId,
                "profile.test",
                "encounter.test",
                "encounter-v1",
                "session.test",
                "attempt.test",
                "result.test",
                mode,
                "champion.test",
                "champion.combat.test",
                "loadout.test",
                "boss.test",
                "boss.combat.test",
                authoritative ? "realm.stone" : "realm.neutral",
                authoritative ? "realm-v1" : string.Empty,
                mode == CombatEncounterMode.AuthoritativeQuest
                    ? "quest.context.test"
                    : string.Empty,
                authoritative ? "reward.test" : string.Empty,
                string.Empty,
                "profile-r1");
        }

        private static ChampionEncounterRequest CopyRequest(
            ChampionEncounterRequest source,
            string sessionId = null,
            string attemptId = null,
            string resultId = null,
            string rewardOperationId = null,
            string questContextId = null,
            string committedRealmId = null,
            string realmVersion = null,
            string encounterContentVersion = null,
            string championDefinitionId = null,
            string championCombatProfileId = null,
            string skillLoadoutId = null,
            string bossDefinitionId = null,
            string bossCombatProfileId = null,
            string resumeToken = null,
            string expectedProfileRevision = null)
        {
            return new ChampionEncounterRequest(
                source.GameId,
                source.CatalogSetId,
                source.ProfileId,
                source.EncounterDefinitionId,
                encounterContentVersion ??
                    source.EncounterDefinitionContentVersion,
                sessionId ?? source.EncounterSessionId,
                attemptId ?? source.EncounterAttemptId,
                resultId ?? source.EncounterResultId,
                source.Mode,
                championDefinitionId ?? source.ChampionDefinitionId,
                championCombatProfileId ??
                    source.ChampionCombatProfileId,
                skillLoadoutId ?? source.SkillLoadoutId,
                bossDefinitionId ?? source.BossDefinitionId,
                bossCombatProfileId ??
                    source.BossCombatProfileId,
                committedRealmId ?? source.CommittedRealmId,
                realmVersion ?? source.CommittedRealmDefinitionVersion,
                questContextId ?? source.QuestOrProgressionContextId,
                rewardOperationId ?? source.RewardOperationId,
                resumeToken ?? source.ResumeToken,
                expectedProfileRevision ??
                    source.ExpectedProfileRevision);
        }

        private static ChampionEncounterDefinitionSnapshot CopyDefinition(
            ChampionEncounterDefinitionSnapshot source,
            string contentVersion = null,
            string championDefinitionId = null,
            string championCombatProfileId = null,
            string skillLoadoutId = null,
            string bossDefinitionId = null,
            string bossCombatProfileId = null,
            string combatRulesProfileId = null,
            string realmDefinitionVersion = null,
            string profileRevision = null)
        {
            return new ChampionEncounterDefinitionSnapshot(
                source.GameId,
                source.CatalogSetId,
                source.RequiredProfileId,
                source.EncounterDefinitionId,
                source.SchemaVersion,
                contentVersion ?? source.ContentVersion,
                source.Mode,
                championDefinitionId ?? source.ChampionDefinitionId,
                championCombatProfileId ??
                    source.ChampionCombatProfileId,
                skillLoadoutId ?? source.SkillLoadoutId,
                bossDefinitionId ?? source.BossDefinitionId,
                bossCombatProfileId ?? source.BossCombatProfileId,
                combatRulesProfileId ??
                    source.CombatRulesProfileId,
                source.ArenaProfileId,
                source.NeutralRealmContextId,
                realmDefinitionVersion ??
                    source.RequiredRealmDefinitionVersion,
                source.AllowedAuthoritativeRealmIds.ToArray(),
                profileRevision ?? source.ExpectedProfileRevision,
                source.UsesDevelopmentFallbackSource,
                source.AllowsRetryAfterCompleted,
                source.AllowsRetryAfterFailed,
                source.AllowsRetryAfterCancelled);
        }

        private static ResolvedChampionEncounterSnapshot Resolve(
            ChampionEncounterDefinitionSnapshot definition,
            ChampionEncounterRequest request)
        {
            ChampionEncounterRequestPlan plan =
                ChampionEncounterPlanner.PlanRequest(
                    definition,
                    request,
                    new ChampionEncounterRequestCorrelation[0]);
            Assert.AreEqual(
                ChampionEncounterRequestStatus.Resolved,
                plan.Status);
            return plan.Resolved;
        }
    }
}
