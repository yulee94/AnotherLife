using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class CombatActionPlannerTests
    {
        [Test]
        public void TransitionMatrixIsTotalReadOnlyAndTerminalStatesHaveNoEdges()
        {
            CombatActionState[] states =
                (CombatActionState[])Enum.GetValues(typeof(CombatActionState));
            Assert.AreEqual(
                states.Length * states.Length,
                CombatActionPlanner.TransitionMatrix.Count);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatActionTransitionRule>)
                    CombatActionPlanner.TransitionMatrix).Add(null));

            Assert.True(CombatActionPlanner.IsTransitionAllowed(
                CombatActionState.Requested,
                CombatActionState.Validated));
            Assert.True(CombatActionPlanner.IsTransitionAllowed(
                CombatActionState.Resolving,
                CombatActionState.Completed));
            Assert.False(CombatActionPlanner.IsTransitionAllowed(
                CombatActionState.Requested,
                CombatActionState.Completed));
            Assert.False(CombatActionPlanner.IsTransitionAllowed(
                CombatActionState.Validated,
                CombatActionState.Resolving));

            foreach (CombatActionState from in states)
            {
                foreach (CombatActionState to in states)
                {
                    CombatActionTransitionRule rule =
                        CombatActionPlanner.TransitionMatrix.Single(
                            candidate =>
                                candidate.From == from &&
                                candidate.To == to);
                    Assert.AreEqual(
                        CombatActionPlanner.IsTransitionAllowed(from, to),
                        rule.Allowed,
                        from + " -> " + to);
                }
            }

            foreach (CombatActionState terminal in states.Where(
                         CombatActionPlanner.IsTerminal))
            {
                Assert.False(
                    states.Any(
                        target =>
                            CombatActionPlanner.IsTransitionAllowed(
                                terminal,
                                target)),
                    terminal.ToString());
            }
        }

        [Test]
        public void RequestChecksIdentityEligibilitySourceControlAndDuplicateConflict()
        {
            CombatActionResourcePolicy policy = Policy();
            CombatActionRegistrySnapshot registry = Registry();
            CombatActionRequest request = Request();
            CombatActionEligibilitySnapshot eligibility = Eligibility();

            CombatActionRequestPlanResult accepted =
                CombatActionPlanner.RequestAction(
                    registry,
                    request,
                    policy,
                    eligibility);
            Assert.AreEqual(CombatActionPlanStatus.Applied, accepted.Status);
            Assert.AreEqual(1, accepted.Registry.Actions.Count);
            Assert.AreEqual(CombatActionState.Requested, accepted.Action.State);
            Assert.AreEqual(1, accepted.Receipts.Count);
            Assert.AreEqual(
                CombatActionReceiptKind.ActionRequested,
                accepted.Receipts[0].Kind);
            Assert.AreEqual(
                Id("session-1"),
                accepted.Receipts[0].EncounterSessionId);
            Assert.AreEqual(
                Id("attempt-1"),
                accepted.Receipts[0].EncounterAttemptId);
            Assert.AreEqual(
                Id("actor-1"),
                accepted.Receipts[0].ActorParticipantId);
            Assert.AreEqual(
                "action-r0000000000000000",
                accepted.Receipts[0].BeforeActionRevision);
            Assert.AreEqual(
                "action-r0000000000000000",
                accepted.Receipts[0].AfterActionRevision);
            Assert.AreEqual(0, registry.Actions.Count);

            CombatActionRequestPlanResult duplicate =
                CombatActionPlanner.RequestAction(
                    accepted.Registry,
                    request,
                    policy,
                    eligibility);
            Assert.AreEqual(
                CombatActionPlanStatus.DuplicateExact,
                duplicate.Status);
            Assert.AreSame(accepted.Action, duplicate.Action);
            Assert.AreEqual(0, duplicate.Receipts.Count);
            Assert.AreEqual(1, duplicate.ExistingReceipts.Count);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatActionReceipt>)
                    duplicate.ExistingReceipts).Add(null));

            CombatActionRequest changed = Request(
                targetIntentId: "target-other");
            CombatActionRequestPlanResult conflict =
                CombatActionPlanner.RequestAction(
                    accepted.Registry,
                    changed,
                    policy,
                    eligibility);
            Assert.AreEqual(
                CombatActionPlanStatus.CorrelationConflict,
                conflict.Status);

            CombatActionRequest rejectedRequest = Request(
                actionId: "rejected-action");
            CombatActionRequestPlanResult rejected =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    rejectedRequest,
                    policy,
                    Eligibility(skillAvailable: false));
            Assert.AreEqual(
                CombatActionPlanStatus.SkillUnavailable,
                rejected.Status);
            Assert.AreEqual(1, rejected.Registry.RejectedCorrelations.Count);
            Assert.NotNull(rejected.RejectedCorrelation);
            CombatActionRequestPlanResult rejectedReplay =
                CombatActionPlanner.RequestAction(
                    rejected.Registry,
                    rejectedRequest,
                    policy,
                    Eligibility());
            Assert.AreEqual(
                CombatActionPlanStatus.SkillUnavailable,
                rejectedReplay.Status);
            Assert.AreSame(
                rejected.RejectedCorrelation,
                rejectedReplay.RejectedCorrelation);
            Assert.AreEqual(
                CombatActionPlanStatus.CorrelationConflict,
                CombatActionPlanner.RequestAction(
                    rejected.Registry,
                    Request(
                        actionId: "rejected-action",
                        targetIntentId: "changed-rejected-target"),
                    policy,
                    Eligibility()).Status);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatActionRejectedCorrelationReceipt>)
                    rejected.Registry.RejectedCorrelations).Add(null));

            Assert.True(CombatActionPlanner.IsSourceAllowedForControl(
                CombatActionSource.ManualInput,
                CombatantControlState.Manual));
            Assert.True(CombatActionPlanner.IsSourceAllowedForControl(
                CombatActionSource.AssistAI,
                CombatantControlState.Assist));
            Assert.True(CombatActionPlanner.IsSourceAllowedForControl(
                CombatActionSource.FullAutoAI,
                CombatantControlState.Auto));
            Assert.True(CombatActionPlanner.IsSourceAllowedForControl(
                CombatActionSource.EncounterScript,
                CombatantControlState.EncounterLocked));
            Assert.False(CombatActionPlanner.IsSourceAllowedForControl(
                CombatActionSource.ManualInput,
                CombatantControlState.Assist));
            Assert.False(CombatActionPlanner.IsSourceAllowedForControl(
                CombatActionSource.EncounterScript,
                CombatantControlState.Manual));

            Assert.AreEqual(
                CombatActionPlanStatus.ControlLocked,
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(source: CombatActionSource.ManualInput),
                    policy,
                    Eligibility(control: CombatantControlState.Assist)).Status);
            Assert.AreEqual(
                CombatActionPlanStatus.Applied,
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(source: CombatActionSource.EncounterScript),
                    policy,
                    Eligibility(
                        control: CombatantControlState.EncounterLocked)).Status);
            Assert.AreEqual(
                CombatActionPlanStatus.ActorDefeated,
                CombatActionPlanner.RequestAction(
                    Registry(),
                    request,
                    policy,
                    Eligibility(life: CombatantLifeState.Defeated)).Status);
            Assert.AreEqual(
                CombatActionPlanStatus.StaleRevision,
                CombatActionPlanner.RequestAction(
                    Registry(),
                    request,
                    policy,
                    Eligibility(actorRevision: "actor-r-other")).Status);
        }

        [Test]
        public void CompletePathEmitsOneResourceCooldownEffectAndTerminalReceipt()
        {
            CombatActionResourcePolicy policy = Policy(
                manaCost: 20L,
                reservationPoint: CombatActionPolicyPoint.ResourceReserved,
                commitPoint: CombatActionPolicyPoint.Committed,
                cooldownPoint: CombatActionPolicyPoint.Completed,
                cooldownDuration: 50L);
            CombatActionRequest request = Request();
            CombatActionRequestPlanResult accepted =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    request,
                    policy,
                    Eligibility(availableMana: 100L));
            CombatActionRegistrySnapshot registry = accepted.Registry;

            Transition(
                ref registry,
                "validate",
                CombatActionState.Validated,
                CombatActionTerminalReason.None,
                101L);
            CombatActionTransitionPlanResult reserve = Transition(
                ref registry,
                "reserve",
                CombatActionState.ResourceReserved,
                CombatActionTerminalReason.None,
                102L);
            Assert.AreEqual(
                1,
                reserve.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.ManaReserved));
            Transition(
                ref registry,
                "windup",
                CombatActionState.Windup,
                CombatActionTerminalReason.None,
                103L);
            CombatActionTransitionPlanResult commit = Transition(
                ref registry,
                "commit",
                CombatActionState.Committed,
                CombatActionTerminalReason.None,
                104L);
            Assert.AreEqual(
                1,
                commit.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.ManaCommitted));
            Transition(
                ref registry,
                "resolve",
                CombatActionState.Resolving,
                CombatActionTerminalReason.None,
                105L);
            CombatActionTransitionRequest completionRequest =
                CurrentTransitionRequest(
                    registry,
                    "complete",
                    CombatActionState.Completed,
                    CombatActionTerminalReason.Completed,
                    106L);
            CombatActionTransitionPlanResult completed =
                CombatActionPlanner.PlanTransition(
                    registry,
                    completionRequest);
            registry = completed.Registry;

            Assert.AreEqual(CombatActionPlanStatus.Applied, completed.Status);
            Assert.AreEqual(CombatActionState.Completed, completed.Action.State);
            Assert.AreEqual(0, registry.Actions.Count);
            Assert.AreEqual(1, registry.TerminalCorrelations.Count);
            Assert.AreEqual(
                CombatActionManaOutcome.Committed,
                completed.Action.ManaOutcome);
            Assert.AreEqual(
                CombatActionEffectOutcome.Applied,
                completed.Action.EffectOutcome);
            Assert.True(completed.Action.CooldownStarted);
            Assert.True(completed.Action.TerminalReceiptEmitted);
            Assert.True(
                completed.Receipts.All(
                    receipt =>
                        receipt.EncounterSessionId == Id("session-1") &&
                        receipt.EncounterAttemptId == Id("attempt-1") &&
                        receipt.ActorParticipantId == Id("actor-1") &&
                        receipt.BeforeActionRevision ==
                            completionRequest.ExpectedActionRevision &&
                        receipt.AfterActionRevision ==
                            completed.Action.Revision));
            Assert.AreEqual(
                1,
                completed.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.ManaReserved));
            Assert.AreEqual(
                1,
                completed.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.ManaCommitted));
            Assert.AreEqual(
                1,
                completed.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.EffectApplied));
            Assert.AreEqual(
                1,
                completed.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.CooldownStarted));
            Assert.AreEqual(
                1,
                completed.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.Terminal));
            int effectIndex = completed.Receipts.ToList().FindIndex(
                receipt => receipt.Kind == CombatActionReceiptKind.EffectApplied);
            int cooldownIndex = completed.Receipts.ToList().FindIndex(
                receipt => receipt.Kind == CombatActionReceiptKind.CooldownStarted);
            Assert.GreaterOrEqual(effectIndex, 0);
            Assert.Greater(cooldownIndex, effectIndex);

            CombatActionReceipt cooldownReceipt =
                completed.Action.Receipts.Single(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.CooldownStarted);
            Assert.NotNull(cooldownReceipt.Cooldown);
            Assert.AreEqual(Id("actor-1"), cooldownReceipt.Cooldown.ActorParticipantId);
            Assert.AreEqual(Id("skill-1"), cooldownReceipt.Cooldown.SkillId);
            Assert.AreEqual(
                Version("skill-version-1"),
                cooldownReceipt.Cooldown.SkillContentVersion);
            Assert.AreEqual(106L, cooldownReceipt.Cooldown.StartEncounterTimeMicros);
            Assert.AreEqual(156L, cooldownReceipt.Cooldown.EndEncounterTimeMicros);
            Assert.AreEqual(Id("action-1"), cooldownReceipt.Cooldown.SourceActionId);

            CombatActionTransitionPlanResult exactTerminalDuplicate =
                CombatActionPlanner.PlanTransition(
                    registry,
                    completionRequest);
            Assert.AreEqual(
                CombatActionPlanStatus.DuplicateExact,
                exactTerminalDuplicate.Status);
            Assert.AreEqual(0, exactTerminalDuplicate.Receipts.Count);
            Assert.AreEqual(4, exactTerminalDuplicate.ExistingReceipts.Count);
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatActionReceipt>)
                    exactTerminalDuplicate.ExistingReceipts).Add(null));

            CombatActionTransitionRequest exactEarlier =
                new CombatActionTransitionRequest(
                    Id("validate"),
                    Id("action-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    CombatActionState.Validated,
                    "action-r0000000000000000",
                    CombatActionTerminalReason.None,
                    100L,
                    101L);
            Assert.AreEqual(
                CombatActionPlanStatus.DuplicateExact,
                CombatActionPlanner.PlanTransition(
                    registry,
                    exactEarlier).Status);
            CombatActionTransitionRequest changedEarlier =
                new CombatActionTransitionRequest(
                    Id("validate"),
                    Id("action-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    CombatActionState.CancelledBeforeCommit,
                    "action-r0000000000000000",
                    CombatActionTerminalReason.ManualCancellation,
                    100L,
                    101L);
            Assert.AreEqual(
                CombatActionPlanStatus.CorrelationConflict,
                CombatActionPlanner.PlanTransition(
                    registry,
                    changedEarlier).Status);

            CombatActionTransitionPlanResult postTerminal =
                CombatActionPlanner.PlanTransition(
                    registry,
                    new CombatActionTransitionRequest(
                        Id("post-terminal"),
                        Id("action-1"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        CombatActionState.Failed,
                        completed.Action.Revision,
                        CombatActionTerminalReason.EffectFailed,
                        100L,
                        107L));
            Assert.AreEqual(
                CombatActionPlanStatus.TerminalState,
                postTerminal.Status);
            Assert.AreEqual(0, postTerminal.Receipts.Count);
        }

        [Test]
        public void CancelBeforeCommitReleasesOnceAndCannotResolveAfterTerminal()
        {
            CombatActionResourcePolicy policy = Policy(
                manaCost: 20L,
                reservationPoint: CombatActionPolicyPoint.ResourceReserved,
                commitPoint: CombatActionPolicyPoint.Committed,
                cooldownPoint: CombatActionPolicyPoint.Completed,
                cooldownDuration: 50L,
                interruptibleWindup: true);
            CombatActionRegistrySnapshot registry =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(),
                    policy,
                    Eligibility()).Registry;
            Transition(
                ref registry,
                "cancel-validate",
                CombatActionState.Validated,
                CombatActionTerminalReason.None,
                101L);
            Transition(
                ref registry,
                "cancel-reserve",
                CombatActionState.ResourceReserved,
                CombatActionTerminalReason.None,
                102L);
            Transition(
                ref registry,
                "cancel-windup",
                CombatActionState.Windup,
                CombatActionTerminalReason.None,
                103L);
            CombatActionTransitionPlanResult cancelled = Transition(
                ref registry,
                "cancel-terminal",
                CombatActionState.CancelledBeforeCommit,
                CombatActionTerminalReason.ManualCancellation,
                104L);

            Assert.AreEqual(
                CombatActionManaOutcome.Released,
                cancelled.Action.ManaOutcome);
            Assert.False(cancelled.Action.CooldownStarted);
            Assert.AreEqual(
                CombatActionEffectOutcome.None,
                cancelled.Action.EffectOutcome);
            Assert.AreEqual(
                1,
                cancelled.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.ManaReleased));
            Assert.AreEqual(
                1,
                cancelled.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.Terminal));
            Assert.AreEqual(
                0,
                cancelled.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.CooldownStarted));
            Assert.AreEqual(0, registry.Actions.Count);
        }

        [Test]
        public void InterruptAfterCommitUsesConfiguredRefundAndCooldownExactlyOnce()
        {
            CombatActionResourcePolicy policy = Policy(
                manaCost: 20L,
                reservationPoint: CombatActionPolicyPoint.ResourceReserved,
                commitPoint: CombatActionPolicyPoint.Committed,
                cooldownPoint: CombatActionPolicyPoint.Completed,
                cooldownDuration: 50L,
                refundInterruption: true,
                cooldownInterruption: true,
                interruptibleResolution: true);
            CombatActionRegistrySnapshot registry =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(),
                    policy,
                    Eligibility()).Registry;
            Transition(
                ref registry,
                "interrupt-validate",
                CombatActionState.Validated,
                CombatActionTerminalReason.None,
                101L);
            Transition(
                ref registry,
                "interrupt-reserve",
                CombatActionState.ResourceReserved,
                CombatActionTerminalReason.None,
                102L);
            Transition(
                ref registry,
                "interrupt-windup",
                CombatActionState.Windup,
                CombatActionTerminalReason.None,
                103L);
            Transition(
                ref registry,
                "interrupt-commit",
                CombatActionState.Committed,
                CombatActionTerminalReason.None,
                104L);
            Transition(
                ref registry,
                "interrupt-resolve",
                CombatActionState.Resolving,
                CombatActionTerminalReason.None,
                105L);
            CombatActionTransitionPlanResult interrupted = Transition(
                ref registry,
                "interrupt-terminal",
                CombatActionState.InterruptedAfterCommit,
                CombatActionTerminalReason.Interrupted,
                106L);

            Assert.AreEqual(
                CombatActionManaOutcome.Refunded,
                interrupted.Action.ManaOutcome);
            Assert.AreEqual(
                1,
                interrupted.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.ManaRefunded));
            Assert.AreEqual(
                1,
                interrupted.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.CooldownStarted));
            Assert.AreEqual(
                1,
                interrupted.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.Terminal));
            Assert.AreEqual(
                0,
                interrupted.Action.Receipts.Count(
                    receipt =>
                        receipt.Kind ==
                        CombatActionReceiptKind.EffectApplied));
        }

        [Test]
        public void RequestPointCommitRoutesEarlyTerminationToInterruptedState()
        {
            CombatActionResourcePolicy policy = Policy(
                manaCost: 20L,
                reservationPoint: CombatActionPolicyPoint.RequestAccepted,
                commitPoint: CombatActionPolicyPoint.RequestAccepted,
                cooldownPoint: CombatActionPolicyPoint.None,
                cooldownDuration: 0L,
                refundCancellation: true,
                refundInterruption: false);
            CombatActionRequestPlanResult accepted =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(),
                    policy,
                    Eligibility());
            Assert.True(accepted.Action.ManaCommitted);
            Assert.False(accepted.Action.ManaReserved);

            CombatActionTransitionRequest invalidCancel =
                CurrentTransitionRequest(
                    accepted.Registry,
                    "committed-cancel",
                    CombatActionState.CancelledBeforeCommit,
                    CombatActionTerminalReason.ManualCancellation,
                    101L);
            Assert.AreEqual(
                CombatActionPlanStatus.PolicyViolation,
                CombatActionPlanner.PlanTransition(
                    accepted.Registry,
                    invalidCancel).Status);

            CombatActionTransitionPlanResult interrupted =
                CombatActionPlanner.PlanTransition(
                    accepted.Registry,
                    CurrentTransitionRequest(
                        accepted.Registry,
                        "committed-interrupt",
                        CombatActionState.InterruptedAfterCommit,
                        CombatActionTerminalReason.ManualCancellation,
                        101L));
            Assert.AreEqual(CombatActionPlanStatus.Applied, interrupted.Status);
            Assert.AreEqual(
                CombatActionState.InterruptedAfterCommit,
                interrupted.Action.State);
            Assert.AreEqual(
                CombatActionManaOutcome.Refunded,
                interrupted.Action.ManaOutcome);
        }

        [Test]
        public void TerminalReasonsOwnPolicyAndForcedTerminationBypassesWindows()
        {
            CombatActionResourcePolicy preCommitPolicy = Policy(
                manaCost: 20L,
                reservationPoint: CombatActionPolicyPoint.ResourceReserved,
                commitPoint: CombatActionPolicyPoint.Committed,
                interruptibleWindup: false);
            CombatActionRegistrySnapshot preCommit =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(),
                    preCommitPolicy,
                    Eligibility()).Registry;
            Transition(
                ref preCommit,
                "forced-pre-validate",
                CombatActionState.Validated,
                CombatActionTerminalReason.None,
                101L);
            Transition(
                ref preCommit,
                "forced-pre-reserve",
                CombatActionState.ResourceReserved,
                CombatActionTerminalReason.None,
                102L);
            Transition(
                ref preCommit,
                "forced-pre-windup",
                CombatActionState.Windup,
                CombatActionTerminalReason.None,
                103L);

            Assert.AreEqual(
                CombatActionPlanStatus.PolicyViolation,
                CombatActionPlanner.PlanTransition(
                    preCommit,
                    CurrentTransitionRequest(
                        preCommit,
                        "blocked-manual-windup",
                        CombatActionState.CancelledBeforeCommit,
                        CombatActionTerminalReason.ManualCancellation,
                        104L)).Status);
            Assert.AreEqual(
                CombatActionPlanStatus.InvalidRequest,
                CombatActionPlanner.PlanTransition(
                    preCommit,
                    CurrentTransitionRequest(
                        preCommit,
                        "invalid-failure-reason",
                        CombatActionState.Failed,
                        CombatActionTerminalReason.ActorDefeated,
                        104L)).Status);
            CombatActionTransitionPlanResult defeated =
                CombatActionPlanner.PlanTransition(
                    preCommit,
                    CurrentTransitionRequest(
                        preCommit,
                        "forced-defeat-windup",
                        CombatActionState.CancelledBeforeCommit,
                        CombatActionTerminalReason.ActorDefeated,
                        104L));
            Assert.AreEqual(CombatActionPlanStatus.Applied, defeated.Status);
            Assert.AreEqual(
                CombatActionManaOutcome.Released,
                defeated.Action.ManaOutcome);

            CombatActionResourcePolicy committedPolicy = Policy(
                manaCost: 20L,
                reservationPoint: CombatActionPolicyPoint.RequestAccepted,
                commitPoint: CombatActionPolicyPoint.RequestAccepted,
                refundInterruption: true,
                interruptibleWindup: false);
            CombatActionRegistrySnapshot committed =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(),
                    committedPolicy,
                    Eligibility()).Registry;
            Transition(
                ref committed,
                "forced-post-validate",
                CombatActionState.Validated,
                CombatActionTerminalReason.None,
                101L);
            Transition(
                ref committed,
                "forced-post-reserve",
                CombatActionState.ResourceReserved,
                CombatActionTerminalReason.None,
                102L);
            Transition(
                ref committed,
                "forced-post-windup",
                CombatActionState.Windup,
                CombatActionTerminalReason.None,
                103L);
            Assert.AreEqual(
                CombatActionPlanStatus.PolicyViolation,
                CombatActionPlanner.PlanTransition(
                    committed,
                    CurrentTransitionRequest(
                        committed,
                        "blocked-post-manual",
                        CombatActionState.InterruptedAfterCommit,
                        CombatActionTerminalReason.ManualCancellation,
                        104L)).Status);
            CombatActionTransitionPlanResult encounterTerminated =
                CombatActionPlanner.PlanTransition(
                    committed,
                    CurrentTransitionRequest(
                        committed,
                        "forced-encounter-windup",
                        CombatActionState.InterruptedAfterCommit,
                        CombatActionTerminalReason.EncounterTerminated,
                        104L));
            Assert.AreEqual(
                CombatActionPlanStatus.Applied,
                encounterTerminated.Status);
            Assert.AreEqual(
                CombatActionManaOutcome.Refunded,
                encounterTerminated.Action.ManaOutcome);
        }

        [Test]
        public void InsufficientResourceAndStaleTransitionDoNotMutateAction()
        {
            CombatActionResourcePolicy policy = Policy(
                manaCost: 20L,
                reservationPoint: CombatActionPolicyPoint.ResourceReserved,
                commitPoint: CombatActionPolicyPoint.Committed);
            CombatActionRegistrySnapshot registry =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(),
                    policy,
                    Eligibility()).Registry;
            Transition(
                ref registry,
                "limited-validate",
                CombatActionState.Validated,
                CombatActionTerminalReason.None,
                101L);
            CombatActionSnapshot before = registry.Actions[0];
            CombatActionTransitionRequest insufficient =
                new CombatActionTransitionRequest(
                    Id("limited-reserve"),
                    Id("action-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    CombatActionState.ResourceReserved,
                    before.Revision,
                    CombatActionTerminalReason.None,
                    19L,
                    102L);
            CombatActionTransitionPlanResult result =
                CombatActionPlanner.PlanTransition(registry, insufficient);
            Assert.AreEqual(
                CombatActionPlanStatus.InsufficientResource,
                result.Status);
            Assert.AreSame(registry, result.Registry);
            Assert.AreSame(before, result.Action);
            Assert.AreEqual(0, result.Receipts.Count);

            CombatActionTransitionRequest stale =
                new CombatActionTransitionRequest(
                    Id("limited-stale"),
                    Id("action-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    CombatActionState.ResourceReserved,
                    "action-r9999999999999999",
                    CombatActionTerminalReason.None,
                    100L,
                    102L);
            Assert.AreEqual(
                CombatActionPlanStatus.StaleRevision,
                CombatActionPlanner.PlanTransition(registry, stale).Status);

            CombatActionTransitionPlanResult forward =
                CombatActionPlanner.PlanTransition(
                    registry,
                    new CombatActionTransitionRequest(
                        Id("time-forward"),
                        Id("action-1"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        CombatActionState.ResourceReserved,
                        before.Revision,
                        CombatActionTerminalReason.None,
                        100L,
                        200L));
            Assert.AreEqual(CombatActionPlanStatus.Applied, forward.Status);
            CombatActionTransitionPlanResult backward =
                CombatActionPlanner.PlanTransition(
                    forward.Registry,
                    new CombatActionTransitionRequest(
                        Id("time-backward"),
                        Id("action-1"),
                        Id("session-1"),
                        Id("attempt-1"),
                        Id("actor-1"),
                        CombatActionState.Windup,
                        forward.Action.Revision,
                        CombatActionTerminalReason.None,
                        100L,
                        199L));
            Assert.AreEqual(
                CombatActionPlanStatus.InvalidRequest,
                backward.Status);
        }

        [Test]
        public void TerminalCorrelationsAllowMoreThanSixtyFourSequentialActions()
        {
            CombatActionResourcePolicy policy = Policy();
            CombatActionRegistrySnapshot registry = Registry();
            CombatActionRequest latestRequest = null;
            CombatActionTransitionRequest latestTransition = null;

            for (int index = 0; index < 65; index++)
            {
                latestRequest = Request(
                    actionId: "sequential-action-" + index,
                    targetIntentId: "sequential-target-" + index);
                CombatActionRequestPlanResult requested =
                    CombatActionPlanner.RequestAction(
                        registry,
                        latestRequest,
                        policy,
                        Eligibility());
                Assert.AreEqual(
                    CombatActionPlanStatus.Applied,
                    requested.Status,
                    "request " + index);
                registry = requested.Registry;
                latestTransition = CurrentTransitionRequest(
                    registry,
                    "sequential-terminal-" + index,
                    CombatActionState.CancelledBeforeCommit,
                    CombatActionTerminalReason.ManualCancellation,
                    101L);
                CombatActionTransitionPlanResult terminal =
                    CombatActionPlanner.PlanTransition(
                        registry,
                        latestTransition);
                Assert.AreEqual(
                    CombatActionPlanStatus.Applied,
                    terminal.Status,
                    "terminal " + index);
                registry = terminal.Registry;
                Assert.AreEqual(0, registry.Actions.Count);
            }

            Assert.AreEqual(65, registry.TerminalCorrelations.Count);

            CombatActionRequestPlanResult duplicate =
                CombatActionPlanner.RequestAction(
                    registry,
                    latestRequest,
                    policy,
                    Eligibility());
            Assert.AreEqual(
                CombatActionPlanStatus.DuplicateExact,
                duplicate.Status);
            Assert.NotNull(duplicate.TerminalCorrelation);
            Assert.AreEqual(
                CombatActionState.CancelledBeforeCommit,
                duplicate.TerminalCorrelation.TerminalState);
            Assert.AreEqual(1, duplicate.ExistingReceipts.Count);

            CombatActionTransitionPlanResult transitionDuplicate =
                CombatActionPlanner.PlanTransition(
                    registry,
                    latestTransition);
            Assert.AreEqual(
                CombatActionPlanStatus.DuplicateExact,
                transitionDuplicate.Status);
            Assert.AreEqual(2, transitionDuplicate.ExistingReceipts.Count);
        }

        [Test]
        public void RejectedCorrelationExhaustionFailsClosedForEveryUnseenAction()
        {
            CombatActionResourcePolicy policy = Policy();
            CombatActionRegistrySnapshot registry = Registry();
            CombatActionRequest latestRejected = null;
            for (int index = 0;
                 index < CombatActionRegistrySnapshot.MaximumRejectedCorrelations;
                 index++)
            {
                latestRejected = Request(
                    actionId: "rejected-sequential-" + index,
                    targetIntentId: "rejected-target-" + index);
                CombatActionRequestPlanResult rejected =
                    CombatActionPlanner.RequestAction(
                        registry,
                        latestRejected,
                        policy,
                        Eligibility(skillAvailable: false));
                Assert.AreEqual(
                    CombatActionPlanStatus.SkillUnavailable,
                    rejected.Status,
                    "rejection " + index);
                registry = rejected.Registry;
            }

            Assert.AreEqual(
                CombatActionRegistrySnapshot.MaximumRejectedCorrelations,
                registry.RejectedCorrelations.Count);
            CombatActionRequest unseen = Request(
                actionId: "unseen-after-rejection-capacity");
            CombatActionRequestPlanResult blocked =
                CombatActionPlanner.RequestAction(
                    registry,
                    unseen,
                    policy,
                    Eligibility());
            Assert.AreEqual(
                CombatActionPlanStatus.CapacityReached,
                blocked.Status);
            Assert.AreSame(registry, blocked.Registry);
            Assert.IsNull(blocked.Action);
            Assert.AreEqual(
                CombatActionPlanStatus.CapacityReached,
                CombatActionPlanner.RequestAction(
                    registry,
                    Request(
                        actionId: "unseen-after-rejection-capacity",
                        targetIntentId: "changed-unseen-target"),
                    policy,
                    Eligibility()).Status);

            Assert.AreEqual(
                CombatActionPlanStatus.SkillUnavailable,
                CombatActionPlanner.RequestAction(
                    registry,
                    latestRejected,
                    policy,
                    Eligibility()).Status);
            Assert.AreEqual(
                CombatActionPlanStatus.CorrelationConflict,
                CombatActionPlanner.RequestAction(
                    registry,
                    Request(
                        actionId: latestRejected.ActionId.Value,
                        targetIntentId: "changed-retained-target"),
                    policy,
                    Eligibility()).Status);
        }

        [Test]
        public void CooldownPlannerKeysQueriesAndCorrelationsExactly()
        {
            Assert.True(CombatCooldownRegistrySnapshot.TryCreate(
                Id("session-1"),
                Id("attempt-1"),
                out CombatCooldownRegistrySnapshot registry));
            CombatCooldownQueryResult none = CombatCooldownPlanner.Query(
                registry,
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                Id("skill-1"),
                Version("skill-version-1"),
                100L,
                true);
            Assert.AreEqual(CombatCooldownQueryStatus.None, none.Status);

            CombatCooldownStartRequest request =
                new CombatCooldownStartRequest(
                    Id("cooldown-operation-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("skill-1"),
                    Version("skill-version-1"),
                    Id("action-1"),
                    100L,
                    50L,
                    registry.Revision);
            CombatCooldownStartResult started =
                CombatCooldownPlanner.Start(registry, request);
            Assert.AreEqual(CombatCooldownStartStatus.Started, started.Status);
            registry = started.Registry;
            Assert.AreEqual(100L, started.Cooldown.StartEncounterTimeMicros);
            Assert.AreEqual(150L, started.Cooldown.EndEncounterTimeMicros);
            Assert.AreEqual(50L, started.Cooldown.DurationMicros);
            Assert.AreEqual(Id("actor-1"), started.Cooldown.ActorParticipantId);
            Assert.AreEqual(Id("skill-1"), started.Cooldown.SkillId);
            Assert.AreEqual(
                Version("skill-version-1"),
                started.Cooldown.SkillContentVersion);
            Assert.AreEqual(Id("action-1"), started.Cooldown.SourceActionId);
            Assert.False(string.IsNullOrEmpty(started.Cooldown.StateRevision));

            Assert.AreEqual(
                CombatCooldownQueryStatus.Active,
                QueryCooldown(registry, 100L).Status);
            Assert.AreEqual(
                CombatCooldownQueryStatus.Active,
                QueryCooldown(registry, 149L).Status);
            Assert.AreEqual(
                CombatCooldownQueryStatus.Completed,
                QueryCooldown(registry, 150L).Status);
            Assert.AreEqual(
                CombatCooldownQueryStatus.InvalidClock,
                QueryCooldown(registry, 99L).Status);
            Assert.AreEqual(
                CombatCooldownQueryStatus.Unavailable,
                CombatCooldownPlanner.Query(
                    registry,
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("skill-1"),
                    Version("skill-version-1"),
                    100L,
                    false).Status);
            Assert.AreEqual(
                CombatCooldownQueryStatus.Unknown,
                CombatCooldownPlanner.Query(
                    registry,
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("skill-1"),
                    Version("skill-version-other"),
                    100L,
                    true).Status);

            CombatCooldownStartRequest activeVersionDrift =
                new CombatCooldownStartRequest(
                    Id("cooldown-operation-version-drift"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("skill-1"),
                    Version("skill-version-other"),
                    Id("action-version-drift"),
                    110L,
                    50L,
                    registry.Revision);
            Assert.AreEqual(
                CombatCooldownStartStatus.CorrelationConflict,
                CombatCooldownPlanner.Start(
                    registry,
                    activeVersionDrift).Status);

            CombatCooldownStartResult duplicate =
                CombatCooldownPlanner.Start(registry, request);
            Assert.AreEqual(
                CombatCooldownStartStatus.DuplicateExact,
                duplicate.Status);
            Assert.AreSame(started.Cooldown, duplicate.Cooldown);
            Assert.NotNull(duplicate.ExistingReceipt);

            CombatCooldownStartRequest changedReuse =
                new CombatCooldownStartRequest(
                    Id("cooldown-operation-1"),
                    Id("session-1"),
                    Id("attempt-1"),
                    Id("actor-1"),
                    Id("skill-1"),
                    Version("skill-version-1"),
                    Id("action-1"),
                    101L,
                    50L,
                    request.ExpectedRegistryRevision);
            Assert.AreEqual(
                CombatCooldownStartStatus.CorrelationConflict,
                CombatCooldownPlanner.Start(
                    registry,
                    changedReuse).Status);

            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatCooldownSnapshot>)
                    registry.Cooldowns).Add(null));
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatCooldownCorrelationReceipt>)
                    registry.CorrelationReceipts).Add(null));
        }

        [Test]
        public void PolicyRejectsImpossiblePointOrderingAndCollectionsAreReadOnly()
        {
            Assert.False(CombatContractVersion.TryCreate(
                new string('v', CombatTechnicalLimits.MaximumVersionUtf8Bytes + 1),
                out _));
            Assert.False(CombatActionResourcePolicy.TryCreate(
                Id("invalid-policy"),
                10L,
                CombatActionPolicyPoint.Completed,
                CombatActionPolicyPoint.Committed,
                CombatActionPolicyPoint.None,
                0L,
                false,
                false,
                false,
                false,
                false,
                false,
                true,
                true,
                out _));

            CombatActionRequestPlanResult accepted =
                CombatActionPlanner.RequestAction(
                    Registry(),
                    Request(),
                    Policy(),
                    Eligibility());
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatActionSnapshot>)
                    accepted.Registry.Actions).Add(null));
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatActionReceipt>)
                    accepted.Action.Receipts).Add(null));
            Assert.Throws<NotSupportedException>(
                () => ((IList<CombatActionReceipt>)
                    accepted.Receipts).Add(null));
        }

        private static CombatCooldownQueryResult QueryCooldown(
            CombatCooldownRegistrySnapshot registry,
            long encounterTime)
        {
            return CombatCooldownPlanner.Query(
                registry,
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                Id("skill-1"),
                    Version("skill-version-1"),
                encounterTime,
                true);
        }

        private static CombatActionTransitionPlanResult Transition(
            ref CombatActionRegistrySnapshot registry,
            string transitionId,
            CombatActionState target,
            CombatActionTerminalReason reason,
            long encounterTime,
            long availableMana = 100L)
        {
            CombatActionTransitionPlanResult result =
                CombatActionPlanner.PlanTransition(
                    registry,
                    CurrentTransitionRequest(
                        registry,
                        transitionId,
                        target,
                        reason,
                        encounterTime,
                        availableMana));
            Assert.AreEqual(
                CombatActionPlanStatus.Applied,
                result.Status,
                transitionId);
            registry = result.Registry;
            return result;
        }

        private static CombatActionTransitionRequest CurrentTransitionRequest(
            CombatActionRegistrySnapshot registry,
            string transitionId,
            CombatActionState target,
            CombatActionTerminalReason reason,
            long encounterTime,
            long availableMana = 100L)
        {
            CombatActionSnapshot action = registry.Actions.Single();
            return new CombatActionTransitionRequest(
                Id(transitionId),
                action.Request.ActionId,
                action.Request.EncounterSessionId,
                action.Request.EncounterAttemptId,
                action.Request.ActorParticipantId,
                target,
                action.Revision,
                reason,
                availableMana,
                encounterTime);
        }

        private static CombatActionRegistrySnapshot Registry()
        {
            Assert.True(CombatActionRegistrySnapshot.TryCreate(
                Id("session-1"),
                Id("attempt-1"),
                out CombatActionRegistrySnapshot registry));
            return registry;
        }

        private static CombatActionRequest Request(
            string actionId = "action-1",
            string targetIntentId = "target-1",
            CombatActionSource source = CombatActionSource.ManualInput)
        {
            return new CombatActionRequest(
                Id(actionId),
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                Id("skill-1"),
                Version("skill-version-1"),
                Id(targetIntentId),
                source,
                "actor-r1",
                "encounter-r1",
                100L);
        }

        private static CombatActionEligibilitySnapshot Eligibility(
            CombatantLifeState life = CombatantLifeState.Alive,
            CombatantControlState control = CombatantControlState.Manual,
            string actorRevision = "actor-r1",
            string encounterRevision = "encounter-r1",
            long availableMana = 100L,
            bool skillAvailable = true)
        {
            return new CombatActionEligibilitySnapshot(
                Id("session-1"),
                Id("attempt-1"),
                Id("actor-1"),
                actorRevision,
                encounterRevision,
                life,
                control,
                true,
                skillAvailable,
                true,
                true,
                false,
                availableMana);
        }

        private static CombatActionResourcePolicy Policy(
            long manaCost = 0L,
            CombatActionPolicyPoint reservationPoint =
                CombatActionPolicyPoint.None,
            CombatActionPolicyPoint commitPoint =
                CombatActionPolicyPoint.None,
            CombatActionPolicyPoint cooldownPoint =
                CombatActionPolicyPoint.None,
            long cooldownDuration = 0L,
            bool refundCancellation = false,
            bool refundInterruption = false,
            bool refundFailure = false,
            bool cooldownCancellation = false,
            bool cooldownInterruption = false,
            bool cooldownFailure = false,
            bool interruptibleWindup = true,
            bool interruptibleResolution = true)
        {
            Assert.True(CombatActionResourcePolicy.TryCreate(
                Id(
                    "policy-" +
                    manaCost +
                    "-" +
                    reservationPoint +
                    "-" +
                    commitPoint +
                    "-" +
                    cooldownPoint),
                manaCost,
                reservationPoint,
                commitPoint,
                cooldownPoint,
                cooldownDuration,
                refundCancellation,
                refundInterruption,
                refundFailure,
                cooldownCancellation,
                cooldownInterruption,
                cooldownFailure,
                interruptibleWindup,
                interruptibleResolution,
                out CombatActionResourcePolicy policy));
            return policy;
        }

        private static CombatStableId Id(string value)
        {
            Assert.True(CombatStableId.TryCreate(value, out CombatStableId id));
            return id;
        }

        private static CombatContractVersion Version(string value)
        {
            Assert.True(CombatContractVersion.TryCreate(
                value,
                out CombatContractVersion version));
            return version;
        }
    }
}
