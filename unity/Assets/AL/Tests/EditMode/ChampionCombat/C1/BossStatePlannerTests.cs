using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class BossStatePlannerTests
    {
        private const long Unit = CombatTechnicalLimits.MicrosPerUnit;

        [Test]
        public void HealthPhaseAndEnrageTransitionsAreNonCumulativeAndOnceOnly()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial =
                CreateInitial(policy).After;

            BossStateTransitionPlan phase = Plan(
                policy,
                initial,
                Request(
                    "damage-phase",
                    BossStateOperationKind.Damage,
                    45L * Unit,
                    initial));
            Assert.AreEqual(BossStateTransitionStatus.Applied, phase.Status);
            Assert.AreEqual(55L * Unit, phase.After.CurrentHealthMicros);
            Assert.AreEqual("boss.phase.two", phase.After.PhaseId);
            Assert.AreEqual(12L * Unit, phase.After.EffectiveAttackPowerMicros);
            CollectionAssert.Contains(
                phase.TechnicalEvents,
                "BossPhaseChanged:boss.phase.two");

            BossStateTransitionPlan threshold = Plan(
                policy,
                phase.After,
                Request(
                    "damage-enrage",
                    BossStateOperationKind.Damage,
                    35L * Unit,
                    phase.After));
            Assert.AreEqual(
                BossEnrageState.TriggeredByHealth,
                threshold.After.EnrageState);
            Assert.AreEqual("boss.phase.three", threshold.After.PhaseId);
            Assert.AreEqual(15L * Unit, threshold.After.EffectiveAttackPowerMicros);
            CollectionAssert.AreEqual(
                new[]
                {
                    "BossHealthChanged",
                    "BossPhaseChanged:boss.phase.three",
                    "BossEnrageChanged:TriggeredByHealth"
                },
                threshold.TechnicalEvents);

            BossStateTransitionPlan active = Plan(
                policy,
                threshold.After,
                Request(
                    "enrage-active",
                    BossStateOperationKind.ActivateEnrage,
                    0L,
                    threshold.After));
            Assert.AreEqual(BossEnrageState.Active, active.After.EnrageState);
            Assert.AreEqual(30L * Unit, active.After.EffectiveAttackPowerMicros);

            BossStateTransitionPlan repeated = Plan(
                policy,
                active.After,
                Request(
                    "enrage-active-again",
                    BossStateOperationKind.ActivateEnrage,
                    0L,
                    active.After));
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeTerminal,
                repeated.Status);
            Assert.AreEqual(
                active.After.Revision,
                repeated.After.Revision);
            Assert.AreEqual(
                active.After.RetainedOperationCount + 1,
                repeated.After.RetainedOperationCount);
            Assert.AreEqual(30L * Unit, repeated.After.EffectiveAttackPowerMicros);
            Assert.IsEmpty(repeated.TechnicalEvents);
        }

        [Test]
        public void BreakLifecycleUsesEncounterClockAndRejectsDuplicateTrigger()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;

            BossStateTransitionPlan broken = Plan(
                policy,
                initial,
                Request(
                    "guard-break",
                    BossStateOperationKind.GuardDamage,
                    20L * Unit,
                    initial));
            Assert.AreEqual(BossGuardState.Broken, broken.After.GuardState);
            Assert.AreEqual(0L, broken.After.CurrentGuardMicros);
            Assert.AreEqual(5L * Unit, broken.After.BreakRecoveryAtMicros);
            CollectionAssert.Contains(
                broken.TechnicalEvents,
                "BossBreakChanged:Broken");

            BossStateTransitionPlan duplicateTrigger = Plan(
                policy,
                broken.After,
                Request(
                    "guard-break-again",
                    BossStateOperationKind.GuardDamage,
                    1L,
                    broken.After));
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeAlreadyBroken,
                duplicateTrigger.Status);
            Assert.AreEqual(
                broken.After.Revision,
                duplicateTrigger.After.Revision);
            Assert.AreEqual(
                broken.After.RetainedOperationCount + 1,
                duplicateTrigger.After.RetainedOperationCount);
            Assert.IsEmpty(duplicateTrigger.TechnicalEvents);

            BossStateTransitionPlan recovering = Plan(
                policy,
                broken.After,
                Request(
                    "clock-break-ready",
                    BossStateOperationKind.AdvanceEncounterClock,
                    0L,
                    broken.After,
                    5L * Unit));
            Assert.AreEqual(
                BossGuardState.Recovering,
                recovering.After.GuardState);
            CollectionAssert.Contains(
                recovering.TechnicalEvents,
                "BossBreakChanged:Recovering");

            BossStateTransitionPlan stable = Plan(
                policy,
                recovering.After,
                Request(
                    "guard-recovered",
                    BossStateOperationKind.CompleteBreakRecovery,
                    0L,
                    recovering.After,
                    5L * Unit));
            Assert.AreEqual(BossGuardState.Stable, stable.After.GuardState);
            Assert.AreEqual(
                policy.MaxGuardMicros,
                stable.After.CurrentGuardMicros);
            Assert.AreEqual(0L, stable.After.BreakRecoveryAtMicros);
        }

        [Test]
        public void GuardStateEventOnlyEmitsWhenGuardStateActuallyChanges()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateTransitionPlan depleted = Plan(
                policy,
                initial,
                Request(
                    "guard-depleted",
                    BossStateOperationKind.GuardDamage,
                    1L,
                    initial));
            CollectionAssert.AreEqual(
                new[]
                {
                    "BossGuardChanged",
                    "BossBreakChanged:Depleted"
                },
                depleted.TechnicalEvents);

            BossStateTransitionPlan stillDepleted = Plan(
                policy,
                depleted.After,
                Request(
                    "guard-still-depleted",
                    BossStateOperationKind.GuardDamage,
                    1L,
                    depleted.After));
            CollectionAssert.AreEqual(
                new[] { "BossGuardChanged" },
                stillDepleted.TechnicalEvents);
        }

        [Test]
        public void PhaseAndHealthEnrageThresholdsUseExactCrossMultiplication()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateTransitionPlan phaseAbove = Plan(
                policy,
                initial,
                Request(
                    "phase-above",
                    BossStateOperationKind.Damage,
                    40L * Unit - 1L,
                    initial));
            Assert.AreEqual("boss.phase.one", phaseAbove.After.PhaseId);

            BossStateSnapshot exactInitial = CreateInitial(policy).After;
            BossStateTransitionPlan phaseExact = Plan(
                policy,
                exactInitial,
                Request(
                    "phase-exact",
                    BossStateOperationKind.Damage,
                    40L * Unit,
                    exactInitial));
            Assert.AreEqual("boss.phase.two", phaseExact.After.PhaseId);

            BossStateSnapshot belowInitial = CreateInitial(policy).After;
            BossStateTransitionPlan phaseBelow = Plan(
                policy,
                belowInitial,
                Request(
                    "phase-below",
                    BossStateOperationKind.Damage,
                    40L * Unit + 1L,
                    belowInitial));
            Assert.AreEqual("boss.phase.two", phaseBelow.After.PhaseId);

            BossStateSnapshot enrageAboveInitial =
                CreateInitial(policy).After;
            BossStateTransitionPlan enrageAbove = Plan(
                policy,
                enrageAboveInitial,
                Request(
                    "enrage-above",
                    BossStateOperationKind.Damage,
                    75L * Unit - 1L,
                    enrageAboveInitial));
            Assert.AreEqual(
                BossEnrageState.Dormant,
                enrageAbove.After.EnrageState);

            BossStateSnapshot enrageExactInitial =
                CreateInitial(policy).After;
            BossStateTransitionPlan enrageExact = Plan(
                policy,
                enrageExactInitial,
                Request(
                    "enrage-exact",
                    BossStateOperationKind.Damage,
                    75L * Unit,
                    enrageExactInitial));
            Assert.AreEqual(
                BossEnrageState.TriggeredByHealth,
                enrageExact.After.EnrageState);

            BossStateSnapshot enrageBelowInitial =
                CreateInitial(policy).After;
            BossStateTransitionPlan enrageBelow = Plan(
                policy,
                enrageBelowInitial,
                Request(
                    "enrage-below",
                    BossStateOperationKind.Damage,
                    75L * Unit + 1L,
                    enrageBelowInitial));
            Assert.AreEqual(
                BossEnrageState.TriggeredByHealth,
                enrageBelow.After.EnrageState);
        }

        [Test]
        public void TimedEnrageTriggersOnceAndComposesFromBaseSnapshot()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateTransitionPlan trigger = Plan(
                policy,
                initial,
                Request(
                    "clock-enrage",
                    BossStateOperationKind.AdvanceEncounterClock,
                    0L,
                    initial,
                    30L * Unit));

            Assert.AreEqual(
                BossEnrageState.TriggeredByTime,
                trigger.After.EnrageState);
            Assert.AreEqual(10L * Unit, trigger.After.EffectiveAttackPowerMicros);
            CollectionAssert.Contains(
                trigger.TechnicalEvents,
                "BossEnrageChanged:TriggeredByTime");

            BossStateTransitionPlan active = Plan(
                policy,
                trigger.After,
                Request(
                    "time-enrage-active",
                    BossStateOperationKind.ActivateEnrage,
                    0L,
                    trigger.After,
                    30L * Unit));
            Assert.AreEqual(20L * Unit, active.After.EffectiveAttackPowerMicros);

            BossStateTransitionPlan later = Plan(
                policy,
                active.After,
                Request(
                    "clock-later",
                    BossStateOperationKind.AdvanceEncounterClock,
                    0L,
                    active.After,
                    31L * Unit));
            Assert.AreEqual(20L * Unit, later.After.EffectiveAttackPowerMicros);
            Assert.False(later.TechnicalEvents.Any(
                value => value.StartsWith(
                    "BossEnrageChanged",
                    StringComparison.Ordinal)));
        }

        [Test]
        public void DefeatIsSingleTerminalTransitionWithoutLatePhaseOrEnrage()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateTransitionPlan defeated = Plan(
                policy,
                initial,
                Request(
                    "lethal",
                    BossStateOperationKind.Damage,
                    1_000L * Unit,
                    initial));

            Assert.AreEqual(
                BossStateTransitionStatus.AppliedAndDefeated,
                defeated.Status);
            Assert.AreEqual(
                CombatantLifeState.Defeated,
                defeated.After.LifeState);
            Assert.AreEqual(0L, defeated.After.CurrentHealthMicros);
            CollectionAssert.AreEqual(
                new[] { "BossHealthChanged", "BossDefeated" },
                defeated.TechnicalEvents);
            Assert.AreEqual("boss.phase.one", defeated.After.PhaseId);
            Assert.AreEqual(BossEnrageState.Dormant, defeated.After.EnrageState);

            BossStateTransitionPlan late = Plan(
                policy,
                defeated.After,
                Request(
                    "late-damage",
                    BossStateOperationKind.Damage,
                    1L,
                    defeated.After));
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeTerminal,
                late.Status);
            Assert.AreEqual(
                defeated.After.Revision,
                late.After.Revision);
            Assert.AreEqual(
                defeated.After.RetainedOperationCount + 1,
                late.After.RetainedOperationCount);
            Assert.IsEmpty(late.TechnicalEvents);

            BossStateTransitionPlan disposed = Plan(
                policy,
                defeated.After,
                Request(
                    "dispose",
                    BossStateOperationKind.Dispose,
                    0L,
                    defeated.After));
            Assert.AreEqual(
                CombatantLifeState.Disposed,
                disposed.After.LifeState);
            CollectionAssert.AreEqual(
                new[] { "BossDisposed" },
                disposed.TechnicalEvents);
        }

        [Test]
        public void ReplayIsExactConflictSafeAndCultureDelimiterIndependent()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(
                policy,
                "session|one",
                "attempt:one").After;
            BossStateOperationRequest request = Request(
                "damage|same",
                BossStateOperationKind.Damage,
                1_234_567L,
                initial);
            CultureInfo prior = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture =
                    CultureInfo.GetCultureInfo("fr-FR");
                BossStateTransitionPlan first = Plan(policy, initial, request);

                Thread.CurrentThread.CurrentCulture =
                    CultureInfo.GetCultureInfo("ar-SA");
                BossStateTransitionPlan duplicate =
                    BossStatePlanner.PlanTransition(
                        policy,
                        first.After,
                        request,
                        new[] { first.Receipt });
                Assert.AreEqual(
                    BossStateTransitionStatus.DuplicateExact,
                    duplicate.Status);
                Assert.AreSame(first.Receipt, duplicate.Receipt);
                Assert.IsEmpty(duplicate.TechnicalEvents);

                BossStateOperationRequest changed =
                    new BossStateOperationRequest(
                        request.OperationId,
                        request.EncounterSessionId,
                        request.EncounterAttemptId,
                        request.SourceActionOrOperationId,
                        request.SourceParticipantId,
                        request.SourceBehaviorId,
                        request.Kind,
                        request.AmountMicros + 1L,
                        request.AtEncounterMicros,
                        request.ExpectedRevision);
                Assert.AreEqual(
                    BossStateTransitionStatus.CorrelationConflict,
                    BossStatePlanner.PlanTransition(
                        policy,
                        first.After,
                        changed,
                        new[] { first.Receipt }).Status);

                BossStateSnapshot alternate = CreateInitial(
                    policy,
                    "session",
                    "one|attempt:one").After;
                BossStateTransitionPlan alternatePlan = Plan(
                    policy,
                    alternate,
                    Request(
                        "damage|same",
                        BossStateOperationKind.Damage,
                        1_234_567L,
                        alternate));
                Assert.AreNotEqual(
                    first.Receipt.RequestFingerprint,
                    alternatePlan.Receipt.RequestFingerprint);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = prior;
            }
        }

        [Test]
        public void MalformedDuplicateReplayLedgerAndUninitializedStateFailClosed()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateOperationRequest request = Request(
                "damage-ledger",
                BossStateOperationKind.Damage,
                1L,
                initial);
            var forged = new BossStateOperationReceipt(
                null,
                "not-canonical",
                BossStateTransitionStatus.Applied,
                0L,
                1L);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                BossStatePlanner.PlanTransition(
                    policy,
                    initial,
                    request,
                    new[] { forged }).Status);

            BossStateTransitionPlan applied = Plan(
                policy,
                initial,
                Request(
                    "ledger-valid",
                    BossStateOperationKind.Damage,
                    1L,
                    initial));
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                BossStatePlanner.PlanTransition(
                    policy,
                    applied.After,
                    request,
                    new[] { applied.Receipt, applied.Receipt }).Status);

            var uninitialized = new BossStateSnapshot(
                initial.BossProfileId,
                initial.PolicyFingerprint,
                initial.EncounterSessionId,
                initial.EncounterAttemptId,
                initial.ParticipantId,
                CombatantLifeState.Uninitialized,
                initial.CurrentHealthMicros,
                initial.CurrentGuardMicros,
                initial.GuardState,
                initial.BreakRecoveryAtMicros,
                initial.EnrageState,
                initial.PhaseIndex,
                initial.PhaseId,
                initial.EffectiveAttackPowerMicros,
                initial.EncounterElapsedMicros,
                initial.Revision);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidState,
                BossStatePlanner.PlanTransition(
                    policy,
                    uninitialized,
                    request,
                    new BossStateOperationReceipt[0]).Status);
        }

        [Test]
        public void PolicyRejectsInvalidBreakEnragePhaseAndArithmeticFields()
        {
            BossStatePolicy[] invalid =
            {
                CreatePolicy(maxHealth: 0L),
                CreatePolicy(maxGuard: -1L),
                CreatePolicy(breakDuration: 0L),
                CreatePolicy(healthEnrageRatio: Unit),
                CreatePolicy(timedEnrageAt: -1L),
                CreatePolicy(
                    phases: new[]
                    {
                        new BossPhaseDefinition(
                            "boss.phase.one",
                            Unit,
                            Unit),
                        new BossPhaseDefinition(
                            "boss.phase.two",
                            Unit,
                            Unit)
                    }),
                CreatePolicy(
                    phases: new[]
                    {
                        new BossPhaseDefinition(
                            "boss.phase.one",
                            Unit,
                            Unit),
                        new BossPhaseDefinition(
                            "boss.phase.one",
                            500_000L,
                            Unit)
                    }),
                CreatePolicy(enrageMultiplier: 0L),
                CreatePolicy(
                    healthEnrageEnabled: false,
                    healthEnrageRatio: 1L),
                CreatePolicy(
                    timedEnrageEnabled: false,
                    timedEnrageAt: 1L)
            };

            foreach (BossStatePolicy policy in invalid)
            {
                CombatValidationResult validation =
                    BossStatePlanner.ValidatePolicy(policy);
                Assert.False(validation.IsValid);
                Assert.True(validation.Diagnostics.Any(
                    diagnostic =>
                        diagnostic.Domain ==
                            CombatDiagnosticDomain.BossProfile));
                Assert.AreEqual(
                    BossStateTransitionStatus.RejectedInvalidPolicy,
                    CreateInitial(policy).Status);
            }
        }

        [Test]
        public void WrongAttemptStaleRevisionAndNegativeOrOverCeilingDamageReject()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;

            Assert.AreEqual(
                BossStateTransitionStatus.RejectedWrongEncounter,
                Plan(
                    policy,
                    initial,
                    new BossStateOperationRequest(
                        "wrong-attempt",
                        initial.EncounterSessionId,
                        "attempt.other",
                        "action.wrong-attempt",
                        "participant.source",
                        "behavior.damage",
                        BossStateOperationKind.Damage,
                        1L,
                        0L,
                        initial.Revision)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedStaleRevision,
                Plan(
                    policy,
                    initial,
                    new BossStateOperationRequest(
                        "stale",
                        initial.EncounterSessionId,
                        initial.EncounterAttemptId,
                        "action.stale",
                        "participant.source",
                        "behavior.damage",
                        BossStateOperationKind.Damage,
                        1L,
                        0L,
                        initial.Revision + 1L)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedNegativeAmount,
                Plan(
                    policy,
                    initial,
                    Request(
                        "negative",
                        BossStateOperationKind.Damage,
                        -1L,
                        initial)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidAmount,
                Plan(
                    policy,
                    initial,
                    Request(
                        "too-large",
                        BossStateOperationKind.Damage,
                        CombatTechnicalLimits
                            .HealthManaDamageHealingAttackPowerMaximumMicros +
                        1L,
                    initial)).Status);
        }

        [Test]
        public void NonAmountOperationsRequireZeroAndEnrageProvenanceIsValidated()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateOperationKind[] noAmountKinds =
            {
                BossStateOperationKind.AdvanceEncounterClock,
                BossStateOperationKind.ActivateEnrage,
                BossStateOperationKind.CompleteBreakRecovery,
                BossStateOperationKind.Dispose
            };
            foreach (BossStateOperationKind kind in noAmountKinds)
            {
                Assert.AreEqual(
                    BossStateTransitionStatus.RejectedInvalidRequest,
                    Plan(
                        policy,
                        initial,
                        Request(
                            "unexpected-amount-" + kind,
                            kind,
                            1L,
                            initial)).Status,
                    kind.ToString());
            }

            var forgedTriggered = new BossStateSnapshot(
                initial.BossProfileId,
                initial.PolicyFingerprint,
                initial.EncounterSessionId,
                initial.EncounterAttemptId,
                initial.ParticipantId,
                initial.LifeState,
                initial.CurrentHealthMicros,
                initial.CurrentGuardMicros,
                initial.GuardState,
                initial.BreakRecoveryAtMicros,
                BossEnrageState.TriggeredByHealth,
                initial.PhaseIndex,
                initial.PhaseId,
                initial.EffectiveAttackPowerMicros,
                initial.EncounterElapsedMicros,
                initial.Revision);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidState,
                Plan(
                    policy,
                    forgedTriggered,
                    Request(
                        "forged-trigger",
                        BossStateOperationKind.Damage,
                        1L,
                        forgedTriggered)).Status);
        }

        [Test]
        public void ReplayReceiptMustBelongToSameAttemptAndTerminalNoOpIgnoresMaxRevision()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot first = CreateInitial(policy).After;
            BossStateSnapshot other = CreateInitial(
                policy,
                "session.other",
                "attempt.other").After;
            BossStateTransitionPlan otherApplied = Plan(
                policy,
                other,
                Request(
                    "other-operation",
                    BossStateOperationKind.Damage,
                    1L,
                    other));
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                BossStatePlanner.PlanTransition(
                    policy,
                    first,
                    Request(
                        "current-operation",
                        BossStateOperationKind.Damage,
                        1L,
                        first),
                    new[] { otherApplied.Receipt }).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedWrongEncounter,
                BossStatePlanner.PlanTransition(
                    policy,
                    first,
                    otherApplied.Receipt.Request,
                    new[] { otherApplied.Receipt }).Status);

            var defeatedAtMaximumRevision = new BossStateSnapshot(
                first.BossProfileId,
                first.PolicyFingerprint,
                first.EncounterSessionId,
                first.EncounterAttemptId,
                first.ParticipantId,
                CombatantLifeState.Defeated,
                0L,
                first.CurrentGuardMicros,
                first.GuardState,
                first.BreakRecoveryAtMicros,
                first.EnrageState,
                first.PhaseIndex,
                first.PhaseId,
                first.EffectiveAttackPowerMicros,
                first.EncounterElapsedMicros,
                long.MaxValue);
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeTerminal,
                BossStatePlanner.PlanTransition(
                    policy,
                    defeatedAtMaximumRevision,
                    Request(
                        "terminal-max-revision",
                        BossStateOperationKind.Damage,
                        1L,
                        defeatedAtMaximumRevision),
                    new BossStateOperationReceipt[0]).Status);
        }

        [Test]
        public void TypedEventsCarryOperationAndSourceCorrelation()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateTransitionPlan initialized = CreateInitial(policy);
            Assert.AreEqual(1, initialized.TechnicalEventReceipts.Count);
            BossStateTechnicalEventReceipt initialization =
                initialized.TechnicalEventReceipts[0];
            Assert.AreEqual(
                BossStateTechnicalEventKind.StateInitialized,
                initialization.Kind);
            Assert.AreEqual(string.Empty, initialization.OperationId);
            Assert.AreEqual(
                string.Empty,
                initialization.SourceActionOrOperationId);
            Assert.AreEqual(
                initialized.After.PolicyFingerprint,
                initialization.PolicyFingerprint);

            BossStateOperationRequest request = Request(
                "typed-event",
                BossStateOperationKind.Damage,
                45L * Unit,
                initialized.After,
                sourceActionOrOperationId: "action.typed-event",
                sourceParticipantId: "participant.champion",
                sourceBehaviorId: "behavior.basic-attack");
            BossStateTransitionPlan applied = Plan(
                policy,
                initialized.After,
                request);

            Assert.AreEqual(
                applied.TechnicalEvents.Count,
                applied.TechnicalEventReceipts.Count);
            for (int index = 0;
                 index < applied.TechnicalEventReceipts.Count;
                 index++)
            {
                BossStateTechnicalEventReceipt receipt =
                    applied.TechnicalEventReceipts[index];
                Assert.AreEqual(request.OperationId, receipt.OperationId);
                Assert.AreEqual(
                    request.SourceActionOrOperationId,
                    receipt.SourceActionOrOperationId);
                Assert.AreEqual(
                    request.SourceParticipantId,
                    receipt.SourceParticipantId);
                Assert.AreEqual(
                    request.SourceBehaviorId,
                    receipt.SourceBehaviorId);
                Assert.AreEqual(initialized.After.Revision, receipt.BeforeRevision);
                Assert.AreEqual(applied.After.Revision, receipt.AfterRevision);
                Assert.AreEqual(index, receipt.Sequence);
                Assert.AreEqual(
                    applied.TechnicalEvents[index],
                    receipt.EventName);
            }

            BossStateTransitionPlan noChange = Plan(
                policy,
                applied.After,
                Request(
                    "typed-no-change",
                    BossStateOperationKind.Damage,
                    0L,
                    applied.After));
            Assert.IsEmpty(noChange.TechnicalEvents);
            Assert.IsEmpty(noChange.TechnicalEventReceipts);
        }

        [Test]
        public void SourceIdentityShapeAndCorrelationAreFailClosed()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;

            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                Plan(
                    policy,
                    initial,
                    Request(
                        "missing-source",
                        BossStateOperationKind.Damage,
                        1L,
                        initial,
                        sourceActionOrOperationId: string.Empty,
                        sourceParticipantId: string.Empty,
                        sourceBehaviorId: string.Empty)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                Plan(
                    policy,
                    initial,
                    Request(
                        "partial-source",
                        BossStateOperationKind.GuardDamage,
                        1L,
                        initial,
                        sourceBehaviorId: string.Empty)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                Plan(
                    policy,
                    initial,
                    Request(
                        "unexpected-source",
                        BossStateOperationKind.AdvanceEncounterClock,
                        0L,
                        initial,
                        sourceActionOrOperationId: "operation.source",
                        sourceParticipantId: "participant.source",
                        sourceBehaviorId: "behavior.source")).Status);

            BossStateOperationRequest firstRequest = Request(
                "source-conflict",
                BossStateOperationKind.Damage,
                1L,
                initial);
            BossStateTransitionPlan first =
                Plan(policy, initial, firstRequest);
            BossStateOperationRequest changedSource = Request(
                firstRequest.OperationId,
                BossStateOperationKind.Damage,
                firstRequest.AmountMicros,
                initial,
                sourceActionOrOperationId: "action.changed");
            Assert.AreEqual(
                BossStateTransitionStatus.CorrelationConflict,
                BossStatePlanner.PlanTransition(
                    policy,
                    first.After,
                    changedSource,
                    new[] { first.Receipt }).Status);
        }

        [Test]
        public void SnapshotBoundHistoryPreventsOmittedNoChangeReuseAndForeignBranches()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateOperationRequest retainedNoChange = Request(
                "retained.no-change",
                BossStateOperationKind.Damage,
                0L,
                initial);
            BossStateTransitionPlan noChange = Plan(
                policy,
                initial,
                retainedNoChange);
            Assert.True(noChange.UpdatesSnapshot);
            Assert.True(noChange.MutatesState);
            Assert.False(noChange.AdvancesDomainRevision);
            Assert.True(noChange.RetainsCorrelation);
            Assert.AreEqual(
                initial.Revision,
                noChange.After.Revision);
            Assert.AreEqual(
                initial.RetainedOperationCount + 1,
                noChange.After.RetainedOperationCount);
            Assert.AreNotSame(initial, noChange.After);

            BossStateTransitionPlan duplicate =
                BossStatePlanner.PlanTransition(
                    policy,
                    noChange.After,
                    retainedNoChange,
                    new BossStateOperationReceipt[0]);
            Assert.AreEqual(
                BossStateTransitionStatus.DuplicateExact,
                duplicate.Status);
            Assert.False(duplicate.UpdatesSnapshot);
            Assert.False(duplicate.RetainsCorrelation);

            BossStateTransitionPlan later = Plan(
                policy,
                noChange.After,
                Request(
                    "retained.later",
                    BossStateOperationKind.Damage,
                    1L,
                    noChange.After));
            Assert.AreEqual(2, later.After.RetainedOperationCount);
            BossStateOperationRequest changedOld = Request(
                retainedNoChange.OperationId,
                BossStateOperationKind.Damage,
                1L,
                initial);
            Assert.AreEqual(
                BossStateTransitionStatus.CorrelationConflict,
                BossStatePlanner.PlanTransition(
                    policy,
                    later.After,
                    changedOld,
                    new BossStateOperationReceipt[0]).Status);

            BossStateTransitionPlan mainOne = Plan(
                policy,
                initial,
                Request(
                    "main.one",
                    BossStateOperationKind.Damage,
                    1L,
                    initial));
            BossStateTransitionPlan mainTwo = Plan(
                policy,
                mainOne.After,
                Request(
                    "main.two",
                    BossStateOperationKind.Damage,
                    1L,
                    mainOne.After));
            BossStateTransitionPlan foreignBranch = Plan(
                policy,
                initial,
                Request(
                    "foreign.branch",
                    BossStateOperationKind.Damage,
                    2L,
                    initial));
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                BossStatePlanner.PlanTransition(
                    policy,
                    mainTwo.After,
                    Request(
                        foreignBranch.Receipt.OperationId,
                        BossStateOperationKind.Damage,
                        foreignBranch.Receipt.Request.AmountMicros,
                        initial),
                    new[] { foreignBranch.Receipt }).Status);
        }

        [Test]
        public void PolicyAndStateFingerprintsAreBoundedSha256AndPreventSubstitution()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            Assert.True(
                CombatPrimitiveValidation.IsSha256(
                    initial.PolicyFingerprint));

            BossStatePolicy substituted =
                CreatePolicy(timedEnrageAt: 31L * Unit);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidState,
                Plan(
                    substituted,
                    initial,
                    Request(
                        "substituted-policy",
                        BossStateOperationKind.Damage,
                        0L,
                        initial)).Status);

            var phases = new List<BossPhaseDefinition>();
            for (int index = 0;
                 index < BossStatePlanner.MaximumPhases;
                 index++)
            {
                phases.Add(new BossPhaseDefinition(
                    MaximumStableId("phase", index),
                    Unit - index,
                    Unit));
            }

            var maximumPolicy = new BossStatePolicy(
                MaximumStableId("boss-profile"),
                BossStatePlanner.CurrentPolicyVersion,
                100L * Unit,
                20L * Unit,
                5L * Unit,
                true,
                250_000L,
                true,
                30L * Unit,
                10L * Unit,
                2L * Unit,
                phases);
            BossStateTransitionPlan maximumInitial = null;
            Assert.DoesNotThrow(() =>
                maximumInitial = BossStatePlanner.CreateInitial(
                    maximumPolicy,
                    MaximumStableId("session"),
                    MaximumStableId("attempt"),
                    MaximumStableId("participant")));
            Assert.AreEqual(
                BossStateTransitionStatus.Initialized,
                maximumInitial.Status);
            Assert.True(
                CombatPrimitiveValidation.IsSha256(
                    maximumInitial.After.PolicyFingerprint));

            var maximumRequest = new BossStateOperationRequest(
                MaximumStableId("operation"),
                maximumInitial.After.EncounterSessionId,
                maximumInitial.After.EncounterAttemptId,
                MaximumStableId("source-action"),
                MaximumStableId("source-participant"),
                MaximumStableId("source-behavior"),
                BossStateOperationKind.Damage,
                0L,
                maximumInitial.After.EncounterElapsedMicros,
                maximumInitial.After.Revision);
            BossStateTransitionPlan maximumPlan = null;
            Assert.DoesNotThrow(() =>
                maximumPlan = Plan(
                    maximumPolicy,
                    maximumInitial.After,
                    maximumRequest));
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeZero,
                maximumPlan.Status);
            Assert.True(
                CombatPrimitiveValidation.IsSha256(
                    maximumPlan.Receipt.RequestFingerprint));
            Assert.True(
                CombatPrimitiveValidation.IsSha256(
                    maximumPlan.Receipt.BeforeStateFingerprint));
            Assert.True(
                CombatPrimitiveValidation.IsSha256(
                    maximumPlan.Receipt.AfterStateFingerprint));
        }

        [Test]
        public void ReplayCapacityAllowsExactRetryAndRejectsUnseenOperation()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot current = CreateInitial(policy).After;
            for (int index = 0;
                 index < BossStatePlanner.MaximumReplayReceipts;
                 index++)
            {
                BossStateTransitionPlan retained = Plan(
                    policy,
                    current,
                    Request(
                        "capacity." +
                        index.ToString("D4", CultureInfo.InvariantCulture),
                        BossStateOperationKind.Damage,
                        0L,
                        current));
                Assert.AreEqual(
                    BossStateTransitionStatus.NoChangeZero,
                    retained.Status);
                current = retained.After;
            }
            Assert.AreEqual(
                BossStatePlanner.MaximumReplayReceipts,
                current.RetainedOperationCount);

            BossStateOperationRequest exact = Request(
                "capacity.0000",
                BossStateOperationKind.Damage,
                0L,
                current,
                sourceActionOrOperationId: "action.capacity.0000");
            Assert.AreEqual(
                BossStateTransitionStatus.DuplicateExact,
                BossStatePlanner.PlanTransition(
                    policy,
                    current,
                    exact,
                    new BossStateOperationReceipt[0]).Status);

            BossStateTransitionPlan unseen =
                BossStatePlanner.PlanTransition(
                    policy,
                    current,
                    Request(
                        "capacity.unseen",
                        BossStateOperationKind.Damage,
                        0L,
                        current),
                    new BossStateOperationReceipt[0]);
            Assert.AreEqual(
                BossStateTransitionStatus.CapacityReached,
                unseen.Status);
            Assert.IsNull(unseen.Receipt);
            Assert.IsEmpty(unseen.TechnicalEvents);
        }

        [Test]
        public void ReplayLedgerRejectsForksAndSplicedStateChains()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            BossStateTransitionPlan branchA = Plan(
                policy,
                initial,
                Request(
                    "branch.a",
                    BossStateOperationKind.Damage,
                    1L * Unit,
                    initial));
            BossStateTransitionPlan branchB = Plan(
                policy,
                initial,
                Request(
                    "branch.b",
                    BossStateOperationKind.Damage,
                    2L * Unit,
                    initial));
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                BossStatePlanner.PlanTransition(
                    policy,
                    branchA.After,
                    Request(
                        "branch.after",
                        BossStateOperationKind.Damage,
                        0L,
                        branchA.After),
                    new[] { branchA.Receipt, branchB.Receipt }).Status);

            var alternateAtRevisionOne = new BossStateSnapshot(
                initial.BossProfileId,
                initial.PolicyFingerprint,
                initial.EncounterSessionId,
                initial.EncounterAttemptId,
                initial.ParticipantId,
                CombatantLifeState.Alive,
                98L * Unit,
                initial.CurrentGuardMicros,
                initial.GuardState,
                initial.BreakRecoveryAtMicros,
                initial.EnrageState,
                initial.PhaseIndex,
                initial.PhaseId,
                initial.EffectiveAttackPowerMicros,
                initial.EncounterElapsedMicros,
                1L);
            BossStateTransitionPlan splice = Plan(
                policy,
                alternateAtRevisionOne,
                Request(
                    "splice.second",
                    BossStateOperationKind.Damage,
                    1L * Unit,
                    alternateAtRevisionOne));
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                BossStatePlanner.PlanTransition(
                    policy,
                    splice.After,
                    Request(
                        "splice.after",
                        BossStateOperationKind.Damage,
                        0L,
                        splice.After),
                    new[] { branchA.Receipt, splice.Receipt }).Status);
        }

        [Test]
        public void MaximumRevisionPreservesNoOpsAndRejectsOnlyMutations()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot initial = CreateInitial(policy).After;
            var aliveMaximum = new BossStateSnapshot(
                initial.BossProfileId,
                initial.PolicyFingerprint,
                initial.EncounterSessionId,
                initial.EncounterAttemptId,
                initial.ParticipantId,
                initial.LifeState,
                initial.CurrentHealthMicros,
                initial.CurrentGuardMicros,
                initial.GuardState,
                initial.BreakRecoveryAtMicros,
                initial.EnrageState,
                initial.PhaseIndex,
                initial.PhaseId,
                initial.EffectiveAttackPowerMicros,
                initial.EncounterElapsedMicros,
                long.MaxValue);

            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeZero,
                Plan(
                    policy,
                    aliveMaximum,
                    Request(
                        "max.zero",
                        BossStateOperationKind.Damage,
                        0L,
                        aliveMaximum)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeZero,
                Plan(
                    policy,
                    aliveMaximum,
                    Request(
                        "max.clock-same",
                        BossStateOperationKind.AdvanceEncounterClock,
                        0L,
                        aliveMaximum)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeNotReady,
                Plan(
                    policy,
                    aliveMaximum,
                    Request(
                        "max.enrage-not-ready",
                        BossStateOperationKind.ActivateEnrage,
                        0L,
                        aliveMaximum)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeNotReady,
                Plan(
                    policy,
                    aliveMaximum,
                    Request(
                        "max.recovery-not-ready",
                        BossStateOperationKind.CompleteBreakRecovery,
                        0L,
                        aliveMaximum)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.ArithmeticFailure,
                Plan(
                    policy,
                    aliveMaximum,
                    Request(
                        "max.damage",
                        BossStateOperationKind.Damage,
                        1L,
                        aliveMaximum)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.ArithmeticFailure,
                Plan(
                    policy,
                    aliveMaximum,
                    Request(
                        "max.dispose",
                        BossStateOperationKind.Dispose,
                        0L,
                        aliveMaximum)).Status);

            var brokenMaximum = new BossStateSnapshot(
                initial.BossProfileId,
                initial.PolicyFingerprint,
                initial.EncounterSessionId,
                initial.EncounterAttemptId,
                initial.ParticipantId,
                initial.LifeState,
                initial.CurrentHealthMicros,
                0L,
                BossGuardState.Broken,
                1L,
                initial.EnrageState,
                initial.PhaseIndex,
                initial.PhaseId,
                initial.EffectiveAttackPowerMicros,
                initial.EncounterElapsedMicros,
                long.MaxValue);
            Assert.AreEqual(
                BossStateTransitionStatus.NoChangeAlreadyBroken,
                Plan(
                    policy,
                    brokenMaximum,
                    Request(
                        "max.already-broken",
                        BossStateOperationKind.GuardDamage,
                        1L,
                        brokenMaximum)).Status);

            var defeatedMaximum = new BossStateSnapshot(
                initial.BossProfileId,
                initial.PolicyFingerprint,
                initial.EncounterSessionId,
                initial.EncounterAttemptId,
                initial.ParticipantId,
                CombatantLifeState.Defeated,
                0L,
                initial.CurrentGuardMicros,
                initial.GuardState,
                initial.BreakRecoveryAtMicros,
                initial.EnrageState,
                initial.PhaseIndex,
                initial.PhaseId,
                initial.EffectiveAttackPowerMicros,
                initial.EncounterElapsedMicros,
                long.MaxValue);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedNegativeAmount,
                Plan(
                    policy,
                    defeatedMaximum,
                    Request(
                        "terminal.negative",
                        BossStateOperationKind.Damage,
                        -1L,
                        defeatedMaximum)).Status);
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidAmount,
                Plan(
                    policy,
                    defeatedMaximum,
                    Request(
                        "terminal.over-limit",
                        BossStateOperationKind.Damage,
                        CombatTechnicalLimits
                            .HealthManaDamageHealingAttackPowerMaximumMicros +
                        1L,
                        defeatedMaximum)).Status);
        }

        [Test]
        public void ForgedOverlongReplayRequestIsTypedRejectionWithoutThrowing()
        {
            BossStatePolicy policy = CreatePolicy();
            BossStateSnapshot current = CreateInitial(policy).After;
            string overlong = new string('x', 9_000);
            var forgedRequest = new BossStateOperationRequest(
                overlong,
                current.EncounterSessionId,
                current.EncounterAttemptId,
                "action.overlong",
                "participant.source",
                "behavior.damage",
                BossStateOperationKind.Damage,
                1L,
                current.EncounterElapsedMicros,
                current.Revision);
            var forgedReceipt = new BossStateOperationReceipt(
                forgedRequest,
                "1:x",
                BossStateTransitionStatus.Applied,
                current.Revision,
                current.Revision + 1L);

            BossStateTransitionPlan plan = null;
            Assert.DoesNotThrow(() =>
                plan = BossStatePlanner.PlanTransition(
                    policy,
                    current,
                    Request(
                        "current-operation",
                        BossStateOperationKind.Damage,
                        1L,
                        current),
                    new[] { forgedReceipt }));
            Assert.AreEqual(
                BossStateTransitionStatus.RejectedInvalidRequest,
                plan.Status);
            Assert.AreSame(current, plan.After);
            Assert.IsEmpty(plan.TechnicalEvents);
        }

        private static BossStateTransitionPlan CreateInitial(
            BossStatePolicy policy,
            string sessionId = "session.boss",
            string attemptId = "attempt.boss")
        {
            return BossStatePlanner.CreateInitial(
                policy,
                sessionId,
                attemptId,
                "participant.boss");
        }

        private static BossStateTransitionPlan Plan(
            BossStatePolicy policy,
            BossStateSnapshot state,
            BossStateOperationRequest request)
        {
            return BossStatePlanner.PlanTransition(
                policy,
                state,
                request,
                new BossStateOperationReceipt[0]);
        }

        private static BossStateOperationRequest Request(
            string id,
            BossStateOperationKind kind,
            long amount,
            BossStateSnapshot state,
            long? at = null,
            string sourceActionOrOperationId = null,
            string sourceParticipantId = null,
            string sourceBehaviorId = null)
        {
            bool carriesSource =
                kind == BossStateOperationKind.Damage ||
                kind == BossStateOperationKind.GuardDamage;
            return new BossStateOperationRequest(
                id,
                state.EncounterSessionId,
                state.EncounterAttemptId,
                sourceActionOrOperationId ??
                    (carriesSource ? "action." + id : string.Empty),
                sourceParticipantId ??
                    (carriesSource
                        ? "participant.source"
                        : string.Empty),
                sourceBehaviorId ??
                    (carriesSource
                        ? "behavior." +
                          kind.ToString().ToLowerInvariant()
                        : string.Empty),
                kind,
                amount,
                at ?? state.EncounterElapsedMicros,
                state.Revision);
        }

        private static BossStatePolicy CreatePolicy(
            long maxHealth = 100L * Unit,
            long maxGuard = 20L * Unit,
            long breakDuration = 5L * Unit,
            bool healthEnrageEnabled = true,
            long healthEnrageRatio = 250_000L,
            bool timedEnrageEnabled = true,
            long timedEnrageAt = 30L * Unit,
            long enrageMultiplier = 2L * Unit,
            IList<BossPhaseDefinition> phases = null)
        {
            return new BossStatePolicy(
                "boss.profile.test",
                BossStatePlanner.CurrentPolicyVersion,
                maxHealth,
                maxGuard,
                breakDuration,
                healthEnrageEnabled,
                healthEnrageRatio,
                timedEnrageEnabled,
                timedEnrageAt,
                10L * Unit,
                enrageMultiplier,
                phases ?? new[]
                {
                    new BossPhaseDefinition(
                        "boss.phase.one",
                        Unit,
                        Unit),
                    new BossPhaseDefinition(
                        "boss.phase.two",
                        600_000L,
                        1_200_000L),
                    new BossPhaseDefinition(
                        "boss.phase.three",
                        300_000L,
                        1_500_000L)
                });
        }

        private static string MaximumStableId(
            string prefix,
            int index = 0)
        {
            string head =
                prefix +
                "." +
                index.ToString(CultureInfo.InvariantCulture) +
                ".";
            return head + new string(
                'x',
                CombatTechnicalLimits.MaximumStableIdUtf8Bytes -
                head.Length);
        }
    }
}
