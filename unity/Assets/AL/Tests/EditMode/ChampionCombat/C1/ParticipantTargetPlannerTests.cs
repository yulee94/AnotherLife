using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.C1;
using NUnit.Framework;

namespace AL.Tests.EditMode.ChampionCombat.C1
{
    public sealed class ParticipantTargetPlannerTests
    {
        [Test]
        public void EnemyCandidatesResolveByStableHandleAndDeduplicateParticipant()
        {
            var source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                x: 0f,
                isTargetEligible: false);
            var enemy = Participant(
                "participant.enemy",
                "handle.enemy",
                7L,
                "team.red",
                x: 4f);
            ParticipantTargetPlan plan = Resolve(
                new[] { source, enemy },
                Request(
                    new[]
                    {
                        Observation("handle.enemy", 7L, 4f),
                        Observation("handle.enemy", 7L, 4.2f)
                    },
                    maximumRange: 10f));

            Assert.AreEqual(ParticipantTargetPlanStatus.Resolved, plan.Status);
            CollectionAssert.AreEqual(
                new[] { "participant.enemy" },
                plan.ResolvedParticipantIds);
            CollectionAssert.AreEqual(
                new[]
                {
                    ParticipantTargetCandidateStatus.Accepted,
                    ParticipantTargetCandidateStatus
                        .DuplicateParticipantSuppressed
                },
                plan.CandidateReceipts.Select(value => value.Status));
            Assert.IsEmpty(plan.Diagnostics);
        }

        [Test]
        public void ActionBoundHitLedgerPreventsCrossCallOmissionAndReapplication()
        {
            var participants = new[]
            {
                Participant(
                    "participant.source",
                    "handle.source",
                    1L,
                    "team.blue"),
                Participant(
                    "participant.enemy",
                    "handle.enemy",
                    1L,
                    "team.red",
                    x: 1f)
            };
            CombatActionTargetPolicySnapshot policy =
                TargetPolicy("action.target", "skill.one");
            Assert.True(
                ParticipantTargetPlanner.TryCreateInitialHitLedger(
                    policy,
                    out CombatActionHitLedgerSnapshot initial));

            ParticipantTargetPlan first =
                ParticipantTargetPlanner.Resolve(
                    participants,
                    Request(new[]
                    {
                        Observation("handle.enemy", 1L, 1f)
                    }),
                    policy,
                    initial,
                    CurrentAction(policy),
                    ActiveEncounter(policy));

            Assert.AreEqual(ParticipantTargetPlanStatus.Resolved, first.Status);
            Assert.AreEqual(0L, first.HitLedgerReceipt.BeforeRevision);
            Assert.AreEqual(1L, first.HitLedgerReceipt.AfterRevision);
            CollectionAssert.AreEqual(
                new[] { "participant.enemy" },
                first.AfterHitLedger.ParticipantIds);
            CollectionAssert.AreEqual(
                new[] { "participant.enemy" },
                first.HitLedgerReceipt.AddedParticipantIds);
            Assert.AreNotEqual(
                first.BeforeHitLedger.StateHash,
                first.AfterHitLedger.StateHash);

            ParticipantTargetPlan omitted =
                ParticipantTargetPlanner.Resolve(
                    participants,
                    Request(
                        new[]
                        {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        expectedHitLedgerRevision: 1L),
                    policy,
                    first.AfterHitLedger,
                    CurrentAction(policy),
                    ActiveEncounter(policy));
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedHitLedgerMismatch,
                omitted.Status);
            Assert.IsEmpty(omitted.ResolvedParticipantIds);
            Assert.IsNull(omitted.AfterHitLedger);

            ParticipantTargetPlan repeated =
                ParticipantTargetPlanner.Resolve(
                    participants,
                    Request(
                        new[]
                        {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        actionHitLedger:
                            new[] { "participant.enemy" },
                        expectedHitLedgerRevision: 1L),
                    policy,
                    first.AfterHitLedger,
                    CurrentAction(policy),
                    ActiveEncounter(policy));
            Assert.AreEqual(
                ParticipantTargetPlanStatus.ResolvedNoTargets,
                repeated.Status);
            CollectionAssert.AreEqual(
                new[]
                {
                    ParticipantTargetCandidateStatus.RejectedAlreadyHit
                },
                repeated.CandidateReceipts.Select(value => value.Status));
            Assert.AreEqual(1L, repeated.AfterHitLedger.Revision);
            Assert.AreEqual(
                first.AfterHitLedger.StateHash,
                repeated.AfterHitLedger.StateHash);
            Assert.IsEmpty(
                repeated.HitLedgerReceipt.AddedParticipantIds);

            ParticipantTargetPlan stale =
                ParticipantTargetPlanner.Resolve(
                    participants,
                    Request(
                        new FakeTargetHandleObservation[0],
                        actionHitLedger:
                            new[] { "participant.enemy" },
                        expectedHitLedgerRevision: 0L),
                    policy,
                    first.AfterHitLedger,
                    CurrentAction(policy),
                    ActiveEncounter(policy));
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedStaleHitLedger,
                stale.Status);

            CombatActionTargetPolicySnapshot otherPolicy =
                TargetPolicy("action.other", "skill.one");
            ParticipantTargetPlan rebound =
                ParticipantTargetPlanner.Resolve(
                    participants,
                    Request(
                        new FakeTargetHandleObservation[0],
                        actionId: "action.other"),
                    otherPolicy,
                    first.AfterHitLedger,
                    CurrentAction(otherPolicy),
                    ActiveEncounter(otherPolicy));
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidHitLedger,
                rebound.Status);

            Assert.IsEmpty(
                typeof(CombatActionHitLedgerSnapshot).GetConstructors());
            Assert.IsEmpty(
                typeof(ParticipantTargetHitLedgerReceipt).GetConstructors());
            Assert.IsEmpty(
                typeof(ParticipantTargetPlan).GetConstructors());
            Assert.Throws<NotSupportedException>(
                () => ((IList)first.AfterHitLedger.ParticipantIds).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList)first.HitLedgerReceipt.AddedParticipantIds)
                    .Clear());
        }

        [Test]
        public void ResolutionRequiresResolvingActionAndActiveEncounterWithoutLedgerMutation()
        {
            CombatActionSnapshot requested =
                AcceptedAction("action.target", "skill.one");
            CombatActionTargetPolicySnapshot policy =
                TargetPolicy(requested);
            Assert.True(
                ParticipantTargetPlanner.TryCreateInitialHitLedger(
                    policy,
                    out CombatActionHitLedgerSnapshot ledger));
            var participants = new[]
            {
                Participant(
                    "participant.source",
                    "handle.source",
                    1L,
                    "team.blue"),
                Participant(
                    "participant.enemy",
                    "handle.enemy",
                    1L,
                    "team.red",
                    x: 1f)
            };

            ParticipantTargetPlan preResolving =
                ParticipantTargetPlanner.Resolve(
                    participants,
                    Request(
                        new[] { Observation("handle.enemy", 1L, 1f) },
                        expectedActionRevision: requested.Revision),
                    policy,
                    ledger,
                    requested,
                    ActiveEncounter(policy));
            AssertRejectedWithoutLedgerPublication(
                preResolving,
                ParticipantTargetPlanStatus.RejectedActionUnavailable,
                ledger);

            var terminalCases = new[]
            {
                new ActionTerminalCase(
                    CombatActionState.Completed,
                    CombatActionTerminalReason.Completed),
                new ActionTerminalCase(
                    CombatActionState.Failed,
                    CombatActionTerminalReason.EffectFailed)
            };
            foreach (ActionTerminalCase terminalCase in terminalCases)
            {
                CombatActionSnapshot terminal =
                    TerminalAction(
                        policy,
                        terminalCase.State,
                        terminalCase.Reason);
                ParticipantTargetPlan rejected =
                    ParticipantTargetPlanner.Resolve(
                        participants,
                        Request(
                            new[]
                            {
                                Observation("handle.enemy", 1L, 1f)
                            },
                            expectedActionRevision: terminal.Revision),
                        policy,
                        ledger,
                        terminal,
                        ActiveEncounter(policy));
                AssertRejectedWithoutLedgerPublication(
                    rejected,
                    ParticipantTargetPlanStatus.RejectedActionUnavailable,
                    ledger);
            }

            CombatActionSnapshot resolving = CurrentAction(policy);
            CombatEncounterState[] unavailableStates =
                Enum.GetValues(typeof(CombatEncounterState))
                    .Cast<CombatEncounterState>()
                    .Where(state => state != CombatEncounterState.Active)
                    .ToArray();
            foreach (CombatEncounterState state in unavailableStates)
            {
                ChampionEncounterTerminalOutcome outcome =
                    state == CombatEncounterState.Completed
                        ? ChampionEncounterTerminalOutcome
                            .ChampionVictory
                        : ChampionEncounterTerminalOutcome.None;
                ParticipantTargetPlan rejected =
                    ParticipantTargetPlanner.Resolve(
                        participants,
                        Request(new[]
                        {
                            Observation("handle.enemy", 1L, 1f)
                        }),
                        policy,
                        ledger,
                        resolving,
                        ActiveEncounter(policy, state, outcome));
                AssertRejectedWithoutLedgerPublication(
                    rejected,
                    ParticipantTargetPlanStatus
                        .RejectedEncounterUnavailable,
                    ledger);
            }
        }

        [Test]
        public void GenerationAttemptLifeTeamAndHitLedgerChecksFailPerCandidate()
        {
            var participants = new[]
            {
                Participant(
                    "participant.source",
                    "handle.source",
                    1L,
                    "team.blue"),
                Participant(
                    "participant.stale",
                    "handle.stale",
                    2L,
                    "team.red"),
                Participant(
                    "participant.other-attempt",
                    "handle.other-attempt",
                    1L,
                    "team.red",
                    attemptId: "attempt.other"),
                Participant(
                    "participant.dead",
                    "handle.dead",
                    1L,
                    "team.red",
                    lifeState: CombatantLifeState.Defeated,
                    controlState: CombatantControlState.Defeated),
                Participant(
                    "participant.ally",
                    "handle.ally",
                    1L,
                    "team.blue"),
                Participant(
                    "participant.hit",
                    "handle.hit",
                    1L,
                    "team.red")
            };
            CombatActionTargetPolicySnapshot policy =
                TargetPolicy("action.target", "skill.one");
            Assert.True(
                ParticipantTargetPlanner.TryCreateInitialHitLedger(
                    policy,
                    out CombatActionHitLedgerSnapshot initialLedger));
            ParticipantTargetPlan firstHit =
                ParticipantTargetPlanner.Resolve(
                    participants,
                    Request(new[]
                    {
                        Observation("handle.hit", 1L, 1f)
                    }),
                    policy,
                    initialLedger,
                    CurrentAction(policy),
                    ActiveEncounter(policy));
            Assert.AreEqual(
                ParticipantTargetPlanStatus.Resolved,
                firstHit.Status);

            ParticipantTargetPlan plan =
                ParticipantTargetPlanner.Resolve(
                participants,
                Request(
                    new[]
                    {
                        Observation("handle.stale", 1L, 1f),
                        Observation("handle.other-attempt", 1L, 1f),
                        Observation("handle.dead", 1L, 1f),
                        Observation("handle.ally", 1L, 1f),
                        Observation("handle.hit", 1L, 1f)
                    },
                    actionHitLedger: new[] { "participant.hit" },
                    expectedHitLedgerRevision:
                        firstHit.AfterHitLedger.Revision),
                policy,
                firstHit.AfterHitLedger,
                CurrentAction(policy),
                ActiveEncounter(policy));

            CollectionAssert.AreEqual(
                new[]
                {
                    ParticipantTargetCandidateStatus
                        .RejectedStaleHandleGeneration,
                    ParticipantTargetCandidateStatus.RejectedWrongAttempt,
                    ParticipantTargetCandidateStatus
                        .RejectedIneligibleLifeState,
                    ParticipantTargetCandidateStatus.RejectedTeamRule,
                    ParticipantTargetCandidateStatus.RejectedAlreadyHit
                },
                plan.CandidateReceipts.Select(value => value.Status));
            Assert.AreEqual(
                ParticipantTargetPlanStatus.ResolvedNoTargets,
                plan.Status);
        }

        [Test]
        public void SourceUsesLifeAndControlNotTargetEligibility()
        {
            CombatParticipantRegistration untargetableSource = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                isTargetEligible: false,
                controlState: CombatantControlState.Manual);
            CombatParticipantRegistration enemy = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 1f);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.Resolved,
                Resolve(
                    new[] { untargetableSource, enemy },
                    Request(
                        new[] { Observation("handle.enemy", 1L, 1f) }))
                    .Status);

            CombatParticipantRegistration disabled = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                controlState: CombatantControlState.Disabled);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedSourceNotAlive,
                Resolve(
                    new[] { disabled, enemy },
                    Request(
                        new[] { Observation("handle.enemy", 1L, 1f) }))
                    .Status);

            CombatParticipantRegistration scripted = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                controlState: CombatantControlState.EncounterLocked);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.Resolved,
                Resolve(
                    new[] { scripted, enemy },
                    Request(
                        new[] { Observation("handle.enemy", 1L, 1f) },
                        actionSource:
                            CombatActionSource.EncounterScript))
                    .Status);
        }

        [Test]
        public void ActionLockedSourceRequiresExactStableActionOwner()
        {
            CombatParticipantRegistration exact = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                controlState: CombatantControlState.ActionLocked,
                actionLockOwnerActionId: "action.target");
            CombatParticipantRegistration enemy = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 1f);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.Resolved,
                Resolve(
                    new[] { exact, enemy },
                    Request(
                        new[] { Observation("handle.enemy", 1L, 1f) }))
                .Status);

            CombatParticipantRegistration mismatch = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                controlState: CombatantControlState.ActionLocked,
                actionLockOwnerActionId: "action.other");
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedSourceNotAlive,
                Resolve(
                    new[] { mismatch, enemy },
                    Request(
                        new[] { Observation("handle.enemy", 1L, 1f) }))
                .Status);

            CombatParticipantRegistration missingOwner = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                controlState: CombatantControlState.ActionLocked);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRegistry,
                Resolve(
                    new[] { missingOwner, enemy },
                    Request(new FakeTargetHandleObservation[0])).Status);

            CombatParticipantRegistration ownerWithoutLock = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                actionLockOwnerActionId: "action.target");
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRegistry,
                Resolve(
                    new[] { ownerWithoutLock, enemy },
                    Request(new FakeTargetHandleObservation[0])).Status);
        }

        [Test]
        public void ActionTargetPolicyCannotBeCallerWidenedOrRebound()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            CombatParticipantRegistration ally = Participant(
                "participant.ally",
                "handle.ally",
                1L,
                "team.blue",
                x: 1f);
            CombatParticipantRegistration enemy = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 1f);

            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, ally },
                    Request(
                        new[] { Observation("handle.ally", 1L, 1f) },
                        teamRule: CombatTargetTeamRule.Ally),
                    TargetPolicy("action.target", "skill.one")).Status);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    Request(
                        new[] { Observation("handle.enemy", 1L, 1f) }),
                    TargetPolicy("action.other", "skill.one")).Status);

            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    ParticipantRequest(
                        "action.target.friendly",
                        "participant.enemy",
                        CombatTargetTeamRule.Enemy,
                        new[] {
                            Observation("handle.enemy", 1L, 1f)
                        }),
                    TargetPolicy(
                        "action.target.friendly",
                        "skill.three")).Status);

            Assert.AreEqual(
                ParticipantTargetPlanStatus.Resolved,
                ResolveWithPolicy(
                    new[] { source, ally },
                    ParticipantRequest(
                        "action.target.friendly",
                        "participant.ally",
                        CombatTargetTeamRule.Ally,
                        new[] {
                            Observation("handle.ally", 1L, 1f)
                        }),
                    TargetPolicy(
                        "action.target.friendly",
                        "skill.three")).Status);

            Assert.False(
                CombatActionTargetPolicyFactory.TryCreate(
                    AcceptedAction(
                        "action.target",
                        "skill.unknown"),
                    ValidatedLoadout(),
                    out _));
            Assert.IsEmpty(
                typeof(CombatActionTargetPolicySnapshot)
                    .GetConstructors());
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    Request(new FakeTargetHandleObservation[0]),
                    null).Status);
        }

        [Test]
        public void TargetPolicyCannotBeRemintedAfterActionAdmissionRevision()
        {
            CombatActionSnapshot requested =
                AcceptedAction("action.target", "skill.one");
            var transition = new CombatActionTransitionRequest(
                StableId("transition.target.validate"),
                requested.Request.ActionId,
                requested.Request.EncounterSessionId,
                requested.Request.EncounterAttemptId,
                requested.Request.ActorParticipantId,
                CombatActionState.Validated,
                requested.Revision,
                CombatActionTerminalReason.None,
                0L,
                1L);
            CombatActionTransitionPlanResult advanced =
                CombatActionPlanner.PlanTransition(
                    requested,
                    transition);

            Assert.AreEqual(
                CombatActionPlanStatus.Applied,
                advanced.Status);
            Assert.AreEqual(
                CombatActionState.Validated,
                advanced.Action.State);
            Assert.False(
                CombatActionTargetPolicyFactory.TryCreate(
                    advanced.Action,
                    ValidatedLoadout(),
                    out _));
        }

        [Test]
        public void TargetPolicyBindsAcceptedActionAttemptActorRevisionAndLoadoutOwner()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            CombatParticipantRegistration enemy = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 1f);
            ParticipantTargetRequest request = Request(
                new[] { Observation("handle.enemy", 1L, 1f) });

            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    request,
                    TargetPolicy(AcceptedAction(
                        "action.target",
                        "skill.one",
                        attemptId: "attempt.foreign"))).Status);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    request,
                    TargetPolicy(AcceptedAction(
                        "action.target",
                        "skill.one",
                        actorParticipantId:
                            "participant.foreign"))).Status);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedActionUnavailable,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    Request(
                        new[] {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        expectedActionRevision: "action-r999"),
                    TargetPolicy(
                        "action.target",
                        "skill.one")).Status);

            CombatParticipantRegistration wrongOwner = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue",
                actorProfileId: "champion.profile.foreign");
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { wrongOwner, enemy },
                    request,
                    TargetPolicy(
                        "action.target",
                        "skill.one")).Status);
        }

        [Test]
        public void TargetPolicyOwnsRangeShapeAreaAndLineOfSight()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            CombatParticipantRegistration enemy = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 1f);
            CombatActionTargetPolicySnapshot hostilePolicy =
                TargetPolicy("action.target", "skill.one");

            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    Request(
                        new[] {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        maximumRange: 11f),
                    hostilePolicy).Status);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    Request(
                        new[] {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        requireLineOfSight: false),
                    hostilePolicy).Status);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    Request(
                        new[] {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        areaProfileId: "area.changed"),
                    hostilePolicy).Status);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                ResolveWithPolicy(
                    new[] { source, enemy },
                    Request(
                        new[] {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        intentKind:
                            CombatTargetIntentKind.ParticipantId,
                        areaProfileId: string.Empty),
                    hostilePolicy).Status);

            ParticipantTargetRequest widenedPoint =
                PointRequest(1f, 0f, 0f, 6f);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                Resolve(
                    new[] { source },
                    widenedPoint).Status);
        }

        [Test]
        public void RangeUsesRegisteredPositionNotSpoofableColliderObservation()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            CombatParticipantRegistration far = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 11f);
            ParticipantTargetPlan spoofedNear =
                Resolve(
                    new[] { source, far },
                    Request(
                        new[]
                        {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        maximumRange: 10f));
            Assert.AreEqual(
                ParticipantTargetCandidateStatus.RejectedOutOfRange,
                spoofedNear.CandidateReceipts.Single().Status);

            CombatParticipantRegistration near = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 4f);
            ParticipantTargetPlan distantChildCollider =
                Resolve(
                    new[] { source, near },
                    Request(
                        new[]
                        {
                            Observation("handle.enemy", 1L, 100f)
                        },
                        maximumRange: 10f));
            Assert.AreEqual(
                ParticipantTargetCandidateStatus.Accepted,
                distantChildCollider.CandidateReceipts.Single().Status);
            CollectionAssert.AreEqual(
                new[] { "participant.enemy" },
                distantChildCollider.ResolvedParticipantIds);
        }

        [Test]
        public void CandidateMustMatchThePolicyBoundTargetingProfile()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            CombatParticipantRegistration mismatched = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 1f,
                targetingProfileId: "target.profile.foreign");

            ParticipantTargetPlan plan = Resolve(
                new[] { source, mismatched },
                Request(new[]
                {
                    Observation("handle.enemy", 1L, 1f)
                }));

            Assert.AreEqual(
                ParticipantTargetPlanStatus.ResolvedNoTargets,
                plan.Status);
            Assert.AreEqual(
                ParticipantTargetCandidateStatus
                    .RejectedTargetingProfile,
                plan.CandidateReceipts.Single().Status);
            Assert.IsEmpty(plan.ResolvedParticipantIds);
        }

        [Test]
        public void LineOfSightInvalidPositionAndUnknownHandlesAreRejected()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            CombatParticipantRegistration enemy = Participant(
                "participant.enemy",
                "handle.enemy",
                1L,
                "team.red",
                x: 1f);
            ParticipantTargetPlan plan = Resolve(
                new[] { source, enemy },
                Request(
                    new[]
                    {
                        Observation(
                            "handle.enemy",
                            1L,
                            1f,
                            hasLineOfSight: false),
                        Observation(
                            "handle.enemy",
                            1L,
                            float.NaN),
                        Observation(
                            "handle.enemy",
                            1L,
                            CombatPrimitiveValidation.MaximumUnits(
                                CombatScalarKind.WorldDistance) + 1f),
                        Observation("handle.unknown", 1L, 1f)
                    },
                    requireLineOfSight: true));

            CollectionAssert.AreEqual(
                new[]
                {
                    ParticipantTargetCandidateStatus.RejectedLineOfSight,
                    ParticipantTargetCandidateStatus.RejectedInvalidPosition,
                    ParticipantTargetCandidateStatus.RejectedInvalidPosition,
                    ParticipantTargetCandidateStatus.RejectedUnknownHandle
                },
                plan.CandidateReceipts.Select(value => value.Status));
        }

        [Test]
        public void SelfPointAndDirectionIntentsRemainUnityIndependent()
        {
            CombatParticipantRegistration selfSource = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            ParticipantTargetRequest self = new ParticipantTargetRequest(
                "action.self",
                "session.target",
                "attempt.target",
                "participant.source",
                "skill.two",
                CombatContractTestData.ContentVersion,
                "action-r0000000000000004",
                CombatActionSource.ManualInput,
                CombatTargetIntentKind.Self,
                CombatTargetTeamRule.Self,
                string.Empty,
                string.Empty,
                0f,
                0f,
                0f,
                "unit.meter",
                0f,
                "unit.meter",
                false,
                new FakeTargetHandleObservation[0],
                new string[0]);
            ParticipantTargetPlan selfPlan =
                Resolve(
                    new[] { selfSource },
                    self);
            Assert.AreEqual(ParticipantTargetPlanStatus.Resolved, selfPlan.Status);
            CollectionAssert.AreEqual(
                new[] { "participant.source" },
                selfPlan.ResolvedParticipantIds);

            ParticipantTargetRequest ambiguousSelf =
                new ParticipantTargetRequest(
                    "action.self.ambiguous",
                    "session.target",
                    "attempt.target",
                    "participant.source",
                    "skill.two",
                    CombatContractTestData.ContentVersion,
                    "action-r0000000000000004",
                    CombatActionSource.ManualInput,
                    CombatTargetIntentKind.Self,
                    CombatTargetTeamRule.Self,
                    string.Empty,
                    string.Empty,
                    0f,
                    0f,
                    0f,
                    "unit.meter",
                    0f,
                    "unit.meter",
                    false,
                    new[] {
                        Observation("handle.source", 1L, 0f)
                    },
                    new string[0]);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRequest,
                Resolve(
                    new[] { selfSource },
                    ambiguousSelf).Status);

            CombatParticipantRegistration vectorSource = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            ParticipantTargetRequest point = new ParticipantTargetRequest(
                "action.point",
                "session.target",
                "attempt.target",
                "participant.source",
                "skill.four",
                CombatContractTestData.ContentVersion,
                "action-r0000000000000004",
                CombatActionSource.ManualInput,
                CombatTargetIntentKind.Point,
                CombatTargetTeamRule.Any,
                string.Empty,
                string.Empty,
                1f,
                2f,
                3f,
                "unit.meter",
                5f,
                "unit.meter",
                false,
                new FakeTargetHandleObservation[0],
                new string[0]);
            ParticipantTargetPlan pointPlan =
                Resolve(
                    new[] { vectorSource },
                    point);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.ResolvedIntentOnly,
                pointPlan.Status);
            Assert.True(pointPlan.HasResolvedVector);
            Assert.AreEqual(3f, pointPlan.ResolvedVector.Z);

            ParticipantTargetRequest invalidDirection =
                new ParticipantTargetRequest(
                    "action.direction",
                    "session.target",
                    "attempt.target",
                    "participant.source",
                    "skill.four",
                    CombatContractTestData.ContentVersion,
                    "action-r0000000000000004",
                    CombatActionSource.ManualInput,
                    CombatTargetIntentKind.Direction,
                    CombatTargetTeamRule.Any,
                    string.Empty,
                    string.Empty,
                    0f,
                    0f,
                    0f,
                    "unit.meter",
                    0f,
                    "unit.meter",
                    false,
                    new FakeTargetHandleObservation[0],
                    new string[0]);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRequest,
                Resolve(
                    new[] { vectorSource },
                    invalidDirection).Status);

            ParticipantTargetRequest ambiguousDirection =
                new ParticipantTargetRequest(
                    "action.direction.ambiguous",
                    "session.target",
                    "attempt.target",
                    "participant.source",
                    "skill.four",
                    CombatContractTestData.ContentVersion,
                    "action-r0000000000000004",
                    CombatActionSource.ManualInput,
                    CombatTargetIntentKind.Direction,
                    CombatTargetTeamRule.Any,
                    string.Empty,
                    string.Empty,
                    1f,
                    0f,
                    0f,
                    "unit.meter",
                    0f,
                    "unit.meter",
                    false,
                    new[] { Observation("handle.source", 1L, 0f) },
                    new string[0]);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRequest,
                Resolve(
                    new[] { vectorSource },
                    ambiguousDirection).Status);

            Assert.False(
                typeof(ParticipantTargetPlanner).Assembly
                    .GetReferencedAssemblies()
                    .Any(reference =>
                        reference.Name.StartsWith(
                            "UnityEngine",
                            StringComparison.Ordinal)));
        }

        [Test]
        public void PointIntentValidatesRangeUnitsAndRejectsAmbiguousHitInput()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            ParticipantTargetRequest outOfRange =
                PointRequest(10f, 0f, 0f, 5f);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedOutOfRange,
                Resolve(
                    new[] { source },
                    outOfRange).Status);

            ParticipantTargetRequest invalidRange =
                PointRequest(1f, 0f, 0f, float.NaN);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedTargetingPolicy,
                Resolve(
                    new[] { source },
                    invalidRange).Status);

            ParticipantTargetRequest ambiguous =
                new ParticipantTargetRequest(
                    "action.point.ambiguous",
                    "session.target",
                    "attempt.target",
                    "participant.source",
                    "skill.four",
                    CombatContractTestData.ContentVersion,
                    "action-r0000000000000004",
                    CombatActionSource.ManualInput,
                    CombatTargetIntentKind.Point,
                    CombatTargetTeamRule.Any,
                    string.Empty,
                    string.Empty,
                    1f,
                    0f,
                    0f,
                    "unit.meter",
                    5f,
                    "unit.meter",
                    false,
                    new[] { Observation("handle.source", 1L, 0f) },
                    new string[0]);
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRequest,
                Resolve(
                    new[] { source },
                    ambiguous).Status);
        }

        [Test]
        public void ParticipantAndAreaIntentsRejectEveryUnusedVectorOrSecondaryIdentity()
        {
            var participants = new[]
            {
                Participant(
                    "participant.source",
                    "handle.source",
                    1L,
                    "team.blue"),
                Participant(
                    "participant.enemy",
                    "handle.enemy",
                    1L,
                    "team.red",
                    x: 1f),
                Participant(
                    "participant.ally",
                    "handle.ally",
                    1L,
                    "team.blue",
                    x: 1f)
            };
            float overLimit =
                CombatPrimitiveValidation.MaximumUnits(
                    CombatScalarKind.WorldDistance) + 1f;
            float[] invalidComponents =
            {
                float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
                overLimit,
                -overLimit,
                1f
            };

            foreach (float component in invalidComponents)
            {
                ParticipantTargetRequest area = Request(
                    new[] { Observation("handle.enemy", 1L, 1f) },
                    targetX: component);
                Assert.AreEqual(
                    ParticipantTargetPlanStatus.RejectedInvalidRequest,
                    Resolve(participants, area).Status,
                    "area:" + component);

                ParticipantTargetRequest participant =
                    ParticipantRequest(
                        "action.target.friendly",
                        "participant.ally",
                        CombatTargetTeamRule.Ally,
                        new[]
                        {
                            Observation("handle.ally", 1L, 1f)
                        },
                        targetX: component);
                Assert.AreEqual(
                    ParticipantTargetPlanStatus.RejectedInvalidRequest,
                    Resolve(participants, participant).Status,
                    "participant:" + component);
            }

            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRequest,
                Resolve(
                    participants,
                    Request(
                        new[]
                        {
                            Observation("handle.enemy", 1L, 1f)
                        },
                        targetParticipantId:
                            "participant.enemy")).Status);
        }

        [Test]
        public void RegistryRejectsUninitializedOrIncoherentLifeControlPairs()
        {
            var invalid = new[]
            {
                Participant(
                    "participant.bad.uninitialized",
                    "handle.bad.uninitialized",
                    1L,
                    "team.red",
                    lifeState: CombatantLifeState.Uninitialized,
                    controlState: CombatantControlState.Disabled),
                Participant(
                    "participant.bad.alive",
                    "handle.bad.alive",
                    1L,
                    "team.red",
                    lifeState: CombatantLifeState.Alive,
                    controlState: CombatantControlState.Disposed),
                Participant(
                    "participant.bad.defeated",
                    "handle.bad.defeated",
                    1L,
                    "team.red",
                    lifeState: CombatantLifeState.Defeated,
                    controlState: CombatantControlState.Manual),
                Participant(
                    "participant.bad.disposed",
                    "handle.bad.disposed",
                    1L,
                    "team.red",
                    lifeState: CombatantLifeState.Disposed,
                    controlState: CombatantControlState.Defeated)
            };
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            foreach (CombatParticipantRegistration row in invalid)
            {
                Assert.AreEqual(
                    ParticipantTargetPlanStatus.RejectedInvalidRegistry,
                    Resolve(
                        new[] { source, row },
                        Request(new FakeTargetHandleObservation[0]))
                    .Status,
                    row.ParticipantId);
            }
        }

        [Test]
        public void RegistryCandidateAndHitLedgerTechnicalBoundsReject()
        {
            var oversizedRegistry =
                new List<CombatParticipantRegistration>();
            for (int index = 0;
                 index <
                 CombatTargetingTechnicalLimits.MaximumParticipants + 1;
                 index++)
            {
                oversizedRegistry.Add(Participant(
                    "participant." + index,
                    "handle." + index,
                    1L,
                    index == 0 ? "team.blue" : "team.red"));
            }

            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRegistry,
                Resolve(
                    oversizedRegistry,
                    Request(new FakeTargetHandleObservation[0])).Status);

            var normalRegistry = new[]
            {
                Participant(
                    "participant.source",
                    "handle.source",
                    1L,
                    "team.blue"),
                Participant(
                    "participant.enemy",
                    "handle.enemy",
                    1L,
                    "team.red",
                    x: 1f)
            };
            var tooManyCandidates =
                Enumerable.Range(
                        0,
                        CombatTargetingTechnicalLimits.MaximumCandidates + 1)
                    .Select(_ => Observation("handle.enemy", 1L, 1f))
                    .ToList();
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRequest,
                Resolve(
                    normalRegistry,
                    Request(tooManyCandidates)).Status);

            var tooManyHits =
                Enumerable.Range(
                        0,
                        CombatTargetingTechnicalLimits
                            .MaximumActionHitLedgerEntries + 1)
                    .Select(index => "participant.hit." + index)
                    .ToList();
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRequest,
                Resolve(
                    normalRegistry,
                    Request(
                        new FakeTargetHandleObservation[0],
                        actionHitLedger: tooManyHits)).Status);
        }

        [Test]
        public void DuplicateRegistryIdentityFailsClosedAndInputsAreCopied()
        {
            CombatParticipantRegistration source = Participant(
                "participant.source",
                "handle.source",
                1L,
                "team.blue");
            CombatParticipantRegistration duplicate = Participant(
                "participant.source",
                "handle.other",
                1L,
                "team.red");
            Assert.AreEqual(
                ParticipantTargetPlanStatus.RejectedInvalidRegistry,
                Resolve(
                    new[] { source, duplicate },
                    Request(new FakeTargetHandleObservation[0])).Status);

            var candidates = new List<FakeTargetHandleObservation>
            {
                Observation("handle.enemy", 1L, 1f)
            };
            ParticipantTargetRequest request = Request(candidates);
            candidates.Clear();
            Assert.AreEqual(1, request.Candidates.Count);
            Assert.Throws<NotSupportedException>(
                () => ((IList)request.Candidates).Clear());
        }

        private static ParticipantTargetPlan Resolve(
            IList<CombatParticipantRegistration> participantRegistry,
            ParticipantTargetRequest request)
        {
            string skillId;
            if (request.ActionId.StartsWith(
                    "action.self",
                    StringComparison.Ordinal))
            {
                skillId = "skill.two";
            }
            else if (request.ActionId.Contains("friendly"))
            {
                skillId = "skill.three";
            }
            else if (request.ActionId.StartsWith(
                         "action.point",
                         StringComparison.Ordinal) ||
                     request.ActionId.StartsWith(
                         "action.direction",
                         StringComparison.Ordinal))
            {
                skillId = "skill.four";
            }
            else
            {
                skillId = "skill.one";
            }

            return ResolveWithPolicy(
                participantRegistry,
                request,
                TargetPolicy(
                    request.ActionId,
                    skillId,
                    request.ActionSource));
        }

        private static ParticipantTargetPlan ResolveWithPolicy(
            IList<CombatParticipantRegistration> participantRegistry,
            ParticipantTargetRequest request,
            CombatActionTargetPolicySnapshot policy)
        {
            CombatActionHitLedgerSnapshot hitLedger = null;
            if (policy != null)
            {
                Assert.True(
                    ParticipantTargetPlanner.TryCreateInitialHitLedger(
                        policy,
                        out hitLedger));
            }

            return ParticipantTargetPlanner.Resolve(
                participantRegistry,
                request,
                policy,
                hitLedger,
                policy == null
                    ? null
                    : CurrentAction(policy),
                policy == null
                    ? null
                    : ActiveEncounter(policy));
        }

        private static CombatActionSnapshot CurrentAction(
            CombatActionTargetPolicySnapshot policy)
        {
            CombatActionSnapshot current = AcceptedAction(
                policy.ActionId,
                policy.SkillDefinitionId,
                policy.ActionSource,
                policy.EncounterSessionId,
                policy.EncounterAttemptId,
                policy.ActorParticipantId);
            CombatActionState[] states =
            {
                CombatActionState.Validated,
                CombatActionState.Windup,
                CombatActionState.Committed,
                CombatActionState.Resolving
            };
            for (int index = 0; index < states.Length; index++)
            {
                var request = new CombatActionTransitionRequest(
                    StableId("transition.target.current." + index),
                    current.Request.ActionId,
                    current.Request.EncounterSessionId,
                    current.Request.EncounterAttemptId,
                    current.Request.ActorParticipantId,
                    states[index],
                    current.Revision,
                    CombatActionTerminalReason.None,
                    0L,
                    index + 1L);
                CombatActionTransitionPlanResult transition =
                    CombatActionPlanner.PlanTransition(
                        current,
                        request);
                Assert.AreEqual(
                    CombatActionPlanStatus.Applied,
                    transition.Status,
                    states[index].ToString());
                current = transition.Action;
            }

            return current;
        }

        private static CombatActionSnapshot TerminalAction(
            CombatActionTargetPolicySnapshot policy,
            CombatActionState state,
            CombatActionTerminalReason reason)
        {
            CombatActionSnapshot current = CurrentAction(policy);
            var request = new CombatActionTransitionRequest(
                StableId("transition.target.terminal." + state),
                current.Request.ActionId,
                current.Request.EncounterSessionId,
                current.Request.EncounterAttemptId,
                current.Request.ActorParticipantId,
                state,
                current.Revision,
                reason,
                0L,
                current.LastEncounterTimeMicros + 1L);
            CombatActionTransitionPlanResult transition =
                CombatActionPlanner.PlanTransition(current, request);
            Assert.AreEqual(
                CombatActionPlanStatus.Applied,
                transition.Status,
                state.ToString());
            return transition.Action;
        }

        private static void AssertRejectedWithoutLedgerPublication(
            ParticipantTargetPlan plan,
            ParticipantTargetPlanStatus expectedStatus,
            CombatActionHitLedgerSnapshot ledger)
        {
            Assert.AreEqual(expectedStatus, plan.Status);
            Assert.IsEmpty(plan.ResolvedParticipantIds);
            Assert.IsEmpty(plan.CandidateReceipts);
            Assert.IsNull(plan.BeforeHitLedger);
            Assert.IsNull(plan.AfterHitLedger);
            Assert.IsNull(plan.HitLedgerReceipt);
            Assert.AreEqual(0L, ledger.Revision);
            Assert.IsEmpty(ledger.ParticipantIds);
        }

        private static ChampionEncounterStateSnapshot ActiveEncounter(
            CombatActionTargetPolicySnapshot policy,
            CombatEncounterState state =
                CombatEncounterState.Active,
            ChampionEncounterTerminalOutcome terminalOutcome =
                ChampionEncounterTerminalOutcome.None)
        {
            return new ChampionEncounterStateSnapshot(
                policy.EncounterSessionId,
                policy.EncounterAttemptId,
                "result.target",
                string.Empty,
                CombatContractTestData.HashA,
                policy.LoadoutOwnerProfileId,
                "boss.profile.target",
                string.Empty,
                CombatEncounterMode.Practice,
                state,
                terminalOutcome,
                0L,
                1L);
        }

        private static CombatActionTargetPolicySnapshot TargetPolicy(
            string actionId,
            string skillId,
            CombatActionSource source = CombatActionSource.ManualInput)
        {
            Assert.True(
                CombatActionTargetPolicyFactory.TryCreate(
                    AcceptedAction(actionId, skillId, source),
                    ValidatedLoadout(
                        actionId.StartsWith(
                            "action.direction",
                            StringComparison.Ordinal)),
                    out CombatActionTargetPolicySnapshot policy));
            return policy;
        }

        private static CombatActionTargetPolicySnapshot TargetPolicy(
            CombatActionSnapshot acceptedAction)
        {
            Assert.True(
                CombatActionTargetPolicyFactory.TryCreate(
                    acceptedAction,
                    ValidatedLoadout(),
                    out CombatActionTargetPolicySnapshot policy));
            return policy;
        }

        private static CombatActionSnapshot AcceptedAction(
            string actionId,
            string skillId,
            CombatActionSource source = CombatActionSource.ManualInput,
            string sessionId = "session.target",
            string attemptId = "attempt.target",
            string actorParticipantId = "participant.source")
        {
            Assert.True(
                CombatActionRegistrySnapshot.TryCreate(
                    StableId(sessionId),
                    StableId(attemptId),
                    out CombatActionRegistrySnapshot registry));
            var request = new CombatActionRequest(
                StableId(actionId),
                StableId(sessionId),
                StableId(attemptId),
                StableId(actorParticipantId),
                StableId(skillId),
                ContractVersion(
                    CombatContractTestData.ContentVersion),
                StableId("target.intent"),
                source,
                "actor-r1",
                "encounter-r1",
                0L);
            Assert.True(
                CombatActionResourcePolicy.TryCreate(
                    StableId("policy.target.zero"),
                    0L,
                    CombatActionPolicyPoint.None,
                    CombatActionPolicyPoint.None,
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
                    out CombatActionResourcePolicy resourcePolicy));
            var eligibility = new CombatActionEligibilitySnapshot(
                StableId(sessionId),
                StableId(attemptId),
                StableId(actorParticipantId),
                "actor-r1",
                "encounter-r1",
                CombatantLifeState.Alive,
                source == CombatActionSource.EncounterScript
                    ? CombatantControlState.EncounterLocked
                    : source == CombatActionSource.AssistAI
                        ? CombatantControlState.Assist
                        : source == CombatActionSource.FullAutoAI
                            ? CombatantControlState.Auto
                            : CombatantControlState.Manual,
                true,
                true,
                true,
                true,
                false,
                0L);
            CombatActionRequestPlanResult result =
                CombatActionPlanner.RequestAction(
                    registry,
                    request,
                    resourcePolicy,
                    eligibility);
            Assert.AreEqual(
                CombatActionPlanStatus.Applied,
                result.Status);
            Assert.NotNull(result.Action);
            return result.Action;
        }

        private static CombatStableId StableId(string value)
        {
            Assert.True(
                CombatStableId.TryCreate(
                    value,
                    out CombatStableId id));
            return id;
        }

        private static CombatContractVersion ContractVersion(
            string value)
        {
            Assert.True(
                CombatContractVersion.TryCreate(
                    value,
                    out CombatContractVersion version));
            return version;
        }

        private static ValidatedCombatSkillLoadoutSnapshot ValidatedLoadout(
            bool directionProfile = false)
        {
            var skills =
                new Dictionary<string, CombatSkillDefinition>(
                    StringComparer.Ordinal)
                {
                    ["skill.one"] =
                        CombatContractTestData.CreateSkill(
                            "skill.one",
                            rangeMicros:
                                10L *
                                CombatTechnicalLimits.MicrosPerUnit),
                    ["skill.two"] =
                        CombatContractTestData.CreateSkill(
                            "skill.two",
                            behaviorProfileId:
                                "combat.behavior.heal",
                            targetingProfileId:
                                "combat.target.self",
                            rangeMicros: 0L),
                    ["skill.three"] =
                        CombatContractTestData.CreateSkill(
                            "skill.three",
                            behaviorProfileId:
                                "combat.behavior.heal",
                            targetingProfileId:
                                "combat.target.friendly",
                            rangeMicros:
                                5L *
                                CombatTechnicalLimits.MicrosPerUnit),
                    ["skill.four"] =
                        CombatContractTestData.CreateSkill(
                            "skill.four",
                            behaviorProfileId:
                                "combat.behavior.utility",
                            targetingProfileId:
                                "combat.target.any",
                            manaCostMicros: 0L,
                            castDurationMicros: 0L,
                            cooldownDurationMicros: 0L,
                            rangeMicros: directionProfile
                                ? 0L
                                : 5L *
                                  CombatTechnicalLimits.MicrosPerUnit,
                            powerMicros: 0L,
                            botPowerMultiplierMicros: 0L)
                };
            CombatSkillLoadoutValidationResult result =
                CombatSkillLoadoutValidator.Validate(
                    CombatContractTestData.CreateLoadout(),
                    skills,
                    CombatContractTestData
                        .CreateExpectedSkillHashes(),
                    TargetReferences(directionProfile),
                    CombatContractTestData.HashA);
            Assert.True(
                result.IsValid,
                string.Join(
                    ",",
                    result.Diagnostics.Select(value => value.Code)));
            return result.Snapshot;
        }

        private static CombatContractReferenceCatalog TargetReferences(
            bool directionProfile)
        {
            if (!directionProfile)
            {
                return CombatContractTestData.CreateReferences();
            }

            return new CombatContractReferenceCatalog(
                CombatContractTestData.CatalogSetId,
                CombatContractTestData.SchemaVersion,
                new[] { CombatContractTestData.ContentVersion },
                new[] { CombatContractTestData.ChampionProfileId },
                new[]
                {
                    new CombatSkillBehaviorReference(
                        "combat.behavior.damage",
                        CombatSkillBehaviorKind.Damage),
                    new CombatSkillBehaviorReference(
                        "combat.behavior.heal",
                        CombatSkillBehaviorKind.Healing),
                    new CombatSkillBehaviorReference(
                        "combat.behavior.break",
                        CombatSkillBehaviorKind.BreakDamage),
                    new CombatSkillBehaviorReference(
                        "combat.behavior.utility",
                        CombatSkillBehaviorKind.Utility)
                },
                new[]
                {
                    new CombatTargetingReference(
                        "combat.target.hostile",
                        CombatTargetDisposition.Hostile,
                        CombatTargetIntentKind.AreaProfile,
                        "unit.meter",
                        "area.standard",
                        true,
                        "target.profile.standard"),
                    new CombatTargetingReference(
                        "combat.target.self",
                        CombatTargetDisposition.Self,
                        CombatTargetIntentKind.Self,
                        "unit.meter",
                        string.Empty,
                        false),
                    new CombatTargetingReference(
                        "combat.target.friendly",
                        CombatTargetDisposition.Friendly,
                        CombatTargetIntentKind.ParticipantId,
                        "unit.meter",
                        string.Empty,
                        true,
                        "target.profile.standard"),
                    new CombatTargetingReference(
                        "combat.target.any",
                        CombatTargetDisposition.Any,
                        CombatTargetIntentKind.Direction,
                        "unit.meter",
                        string.Empty,
                        false)
                },
                new[] { "combat.resource.standard" },
                new[] { "combat.cooldown.standard" },
                new[] { "combat.presentation.test" },
                new[] { "combat.movement.test" },
                new[] { "combat.dodge.test" },
                new[] { "combat.availability.test" });
        }

        private static CombatParticipantRegistration Participant(
            string participantId,
            string handleId,
            long generation,
            string teamId,
            float x = 0f,
            string sessionId = "session.target",
            string attemptId = "attempt.target",
            CombatantLifeState lifeState = CombatantLifeState.Alive,
            CombatantControlState? controlState = null,
            bool isTargetEligible = true,
            string actionLockOwnerActionId = "",
            string targetingProfileId = "target.profile.standard",
            string actorProfileId = null)
        {
            return new CombatParticipantRegistration(
                participantId,
                actorProfileId ??
                (participantId == "participant.source"
                    ? CombatContractTestData.ChampionProfileId
                    : "actor.profile." + participantId),
                participantId == "participant.source"
                    ? CombatParticipantRole.Champion
                    : CombatParticipantRole.Enemy,
                teamId,
                "realm.neutral",
                lifeState,
                controlState ?? (
                    lifeState == CombatantLifeState.Defeated
                        ? CombatantControlState.Defeated
                        : lifeState == CombatantLifeState.Disposed
                            ? CombatantControlState.Disposed
                            : CombatantControlState.Manual),
                actionLockOwnerActionId,
                isTargetEligible,
                handleId,
                generation,
                targetingProfileId,
                sessionId,
                attemptId,
                x,
                0f,
                0f,
                "unit.meter");
        }

        private static FakeTargetHandleObservation Observation(
            string handleId,
            long generation,
            float x,
            bool hasLineOfSight = true)
        {
            return new FakeTargetHandleObservation(
                handleId,
                generation,
                x,
                0f,
                0f,
                "unit.meter",
                hasLineOfSight);
        }

        private static ParticipantTargetRequest Request(
            IList<FakeTargetHandleObservation> candidates,
            float maximumRange = 10f,
            bool requireLineOfSight = true,
            IList<string> actionHitLedger = null,
            CombatActionSource actionSource =
                CombatActionSource.ManualInput,
            string actionId = "action.target",
            string skillId = "skill.one",
            string expectedActionRevision =
                "action-r0000000000000004",
            long expectedHitLedgerRevision = 0L,
            CombatTargetIntentKind intentKind =
                CombatTargetIntentKind.AreaProfile,
            string areaProfileId = "area.standard",
            CombatTargetTeamRule teamRule =
                CombatTargetTeamRule.Enemy,
            float targetX = 0f,
            float targetY = 0f,
            float targetZ = 0f,
            string targetParticipantId = "")
        {
            return new ParticipantTargetRequest(
                actionId,
                "session.target",
                "attempt.target",
                "participant.source",
                skillId,
                CombatContractTestData.ContentVersion,
                expectedActionRevision,
                actionSource,
                intentKind,
                teamRule,
                targetParticipantId,
                areaProfileId,
                targetX,
                targetY,
                targetZ,
                "unit.meter",
                maximumRange,
                "unit.meter",
                requireLineOfSight,
                candidates,
                actionHitLedger ?? new string[0],
                expectedHitLedgerRevision);
        }

        private static ParticipantTargetRequest PointRequest(
            float x,
            float y,
            float z,
            float maximumRange)
        {
            return new ParticipantTargetRequest(
                "action.point.range",
                "session.target",
                "attempt.target",
                "participant.source",
                "skill.four",
                CombatContractTestData.ContentVersion,
                "action-r0000000000000004",
                CombatActionSource.ManualInput,
                CombatTargetIntentKind.Point,
                CombatTargetTeamRule.Any,
                string.Empty,
                string.Empty,
                x,
                y,
                z,
                "unit.meter",
                maximumRange,
                "unit.meter",
                false,
                new FakeTargetHandleObservation[0],
                new string[0]);
        }

        private static ParticipantTargetRequest ParticipantRequest(
            string actionId,
            string targetParticipantId,
            CombatTargetTeamRule teamRule,
            IList<FakeTargetHandleObservation> candidates,
            float targetX = 0f,
            float targetY = 0f,
            float targetZ = 0f)
        {
            return new ParticipantTargetRequest(
                actionId,
                "session.target",
                "attempt.target",
                "participant.source",
                "skill.three",
                CombatContractTestData.ContentVersion,
                "action-r0000000000000004",
                CombatActionSource.ManualInput,
                CombatTargetIntentKind.ParticipantId,
                teamRule,
                targetParticipantId,
                string.Empty,
                targetX,
                targetY,
                targetZ,
                "unit.meter",
                5f,
                "unit.meter",
                true,
                candidates,
                new string[0]);
        }

        private sealed class ActionTerminalCase
        {
            internal ActionTerminalCase(
                CombatActionState state,
                CombatActionTerminalReason reason)
            {
                State = state;
                Reason = reason;
            }

            internal CombatActionState State { get; }
            internal CombatActionTerminalReason Reason { get; }
        }
    }
}
