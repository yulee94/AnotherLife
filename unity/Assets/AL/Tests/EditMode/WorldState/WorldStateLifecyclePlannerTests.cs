using System;
using System.Collections;
using System.Linq;
using AL.Core.Interfaces.WorldState;
using AL.Services.WorldState;
using NUnit.Framework;

namespace AL.Tests.EditMode.WorldState
{
    public class WorldStateLifecyclePlannerTests
    {
        [Test]
        public void StartProducesCompleteImmutablePlanWithoutMutatingSnapshot()
        {
            WorldStateSnapshot snapshot = WorldStateTestFixtures.EmptySnapshot();
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStateLifecyclePlanner planner =
                WorldStateTestFixtures.Planner(consumers: consumer);

            WorldStatePlanningResult result = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(),
                snapshot);

            Assert.AreEqual(WorldStatePlanningStatus.Prepared, result.Status);
            Assert.NotNull(result.Plan);
            Assert.IsNull(result.ExistingReceipt);
            Assert.AreEqual(WorldStateTransitionKind.Start, result.Plan.TransitionKind);
            Assert.AreEqual(3L, result.Plan.PreviousSnapshotRevision);
            Assert.AreEqual(4L, result.Plan.ExpectedNewRevision);
            Assert.IsNull(result.Plan.InstanceBefore);
            Assert.AreEqual(
                WorldEventInstanceState.Active,
                result.Plan.InstanceAfter.State);
            Assert.AreEqual(
                WorldStateTestFixtures.NowUtcSeconds + 600L,
                result.Plan.InstanceAfter.ExpectedEndAtUtcSeconds);
            Assert.AreEqual(1, result.Plan.PreparedEffectPlans.Count);
            Assert.AreEqual(1, result.Plan.NotificationIntents.Count);
            Assert.AreEqual(
                WorldStateLedgerResultStatus.Committed,
                result.Plan.LedgerEntry.ResultStatus);
            Assert.AreEqual(
                WorldStateTestFixtures.NowUtcSeconds,
                result.Plan.LedgerEntry.CommittedAtUtcSeconds);
            Assert.AreEqual(64, result.Plan.PlanHash.Length);
            Assert.AreEqual(64, result.Plan.SemanticHash.Length);
            Assert.AreEqual(1, consumer.PreparationOrder.Count);
            Assert.AreEqual(0, snapshot.ActiveInstances.Count);
            Assert.AreEqual(3L, snapshot.SnapshotRevision);
            Assert.Throws<NotSupportedException>(() =>
                ((IList)result.Plan.PreparedEffectPlans).Clear());
        }

        [Test]
        public void StartRejectsSecondGlobalPrimaryWithoutPreparingEffects()
        {
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStateLifecyclePlanner planner =
                WorldStateTestFixtures.Planner(consumers: consumer);

            WorldStatePlanningResult result = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(),
                WorldStateTestFixtures.ActiveSnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedActiveExclusiveInstance,
                result.Status);
            Assert.IsNull(result.Plan);
            Assert.IsEmpty(consumer.PreparationOrder);
        }

        [TestCase(59L)]
        [TestCase(3601L)]
        public void StartRejectsDurationOutsideDefinitionPolicy(long duration)
        {
            WorldStatePlanningResult result =
                WorldStateTestFixtures.Planner().PlanStart(
                    WorldStateTestFixtures.StartRequest(duration: duration),
                    WorldStateTestFixtures.EmptySnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedInvalidDuration,
                result.Status);
        }

        [Test]
        public void StartRejectsUnapprovedOverrideWhenCallerCannotOverride()
        {
            WorldEventDefinition definition = WorldStateTestFixtures.Definition(
                durationPolicy: new WorldEventDurationPolicy(
                    60L,
                    3600L,
                    600L,
                    false));
            WorldStateLifecyclePlanner planner =
                WorldStateTestFixtures.Planner(definition);

            WorldStatePlanningResult result = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(duration: 601L),
                WorldStateTestFixtures.EmptySnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedInvalidDuration,
                result.Status);
        }

        [Test]
        public void StartRejectsUnauthorizedSourceAndStaleRevision()
        {
            WorldStateLifecyclePlanner planner = WorldStateTestFixtures.Planner();

            WorldStatePlanningResult source = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(
                    sourceSystemId: "al_world_source_unknown"),
                WorldStateTestFixtures.EmptySnapshot());
            WorldStatePlanningResult stale = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(expectedRevision: 2L),
                WorldStateTestFixtures.EmptySnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedInvalidRequest,
                source.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedStaleSnapshot,
                stale.Status);
        }

        [Test]
        public void StartRejectsUnavailableCatalogReadOnlySnapshotAndClockMismatch()
        {
            WorldEventDefinition definition = WorldStateTestFixtures.Definition();
            var resolver = new FakeDefinitionResolver(definition)
            {
                IsAvailable = false
            };
            var clock = new FakeClock(WorldStateTestFixtures.NowUtcSeconds);
            var planner = new WorldStateLifecyclePlanner(
                resolver,
                clock,
                new WorldEffectConsumerRegistry(new[]
                {
                    new FakeConsumer(WorldStateTestFixtures.ConsumerId)
                }));

            WorldStatePlanningResult unavailable = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(),
                WorldStateTestFixtures.EmptySnapshot());
            WorldStatePlanningResult readOnly =
                WorldStateTestFixtures.Planner().PlanStart(
                    WorldStateTestFixtures.StartRequest(),
                    WorldStateTestFixtures.EmptySnapshot(
                        writable: false,
                        status: WorldStateSnapshotStatus.AvailableReadOnly));
            WorldStatePlanningResult mismatchedClock =
                WorldStateTestFixtures.Planner(
                    clock: new FakeClock(
                        WorldStateTestFixtures.NowUtcSeconds + 1L)).PlanStart(
                    WorldStateTestFixtures.StartRequest(),
                    WorldStateTestFixtures.EmptySnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                unavailable.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedReadOnlyProfile,
                readOnly.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedClockInvalid,
                mismatchedClock.Status);
        }

        [Test]
        public void BackwardOrThrowingClockFailsClosed()
        {
            var throwingClock = new FakeClock(WorldStateTestFixtures.NowUtcSeconds)
            {
                ThrowOnRead = true
            };
            WorldStatePlanningResult throwing =
                WorldStateTestFixtures.Planner(clock: throwingClock).PlanStart(
                    WorldStateTestFixtures.StartRequest(),
                    WorldStateTestFixtures.EmptySnapshot());
            WorldStatePlanningResult backward =
                WorldStateTestFixtures.Planner().PlanStart(
                    WorldStateTestFixtures.StartRequest(),
                    WorldStateTestFixtures.EmptySnapshot(
                        lastTrustedUtc: WorldStateTestFixtures.NowUtcSeconds + 1L));

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedClockInvalid,
                throwing.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedClockInvalid,
                backward.Status);
        }

        [Test]
        public void ExactStartReplayReturnsReceiptBeforeActiveConflict()
        {
            WorldStateStartRequest request = WorldStateTestFixtures.StartRequest();
            WorldEventInstance active = WorldStateTestFixtures.ActiveInstance();
            var receipt = new WorldStateOperationReceipt(
                request.OperationId,
                request.CorrelationId,
                WorldStateTestFixtures.StartSemanticHash(request),
                WorldStateTransitionKind.Start,
                active.InstanceId,
                4L,
                active);
            WorldStateSnapshot snapshot = WorldStateTestFixtures.ActiveSnapshot(
                active,
                revision: 4L,
                receipts: new[] { receipt });

            WorldStatePlanningResult result =
                WorldStateTestFixtures.Planner().PlanStart(request, snapshot);

            Assert.AreEqual(
                WorldStatePlanningStatus.AlreadyCommitted,
                result.Status);
            Assert.AreSame(receipt, result.ExistingReceipt);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void ExactReplayCannotBypassMalformedOrFutureSnapshot()
        {
            WorldStateStartRequest request =
                WorldStateTestFixtures.StartRequest();
            WorldEventInstance active =
                WorldStateTestFixtures.ActiveInstance();
            var receipt = new WorldStateOperationReceipt(
                request.OperationId,
                request.CorrelationId,
                WorldStateTestFixtures.StartSemanticHash(request),
                WorldStateTransitionKind.Start,
                active.InstanceId,
                4L,
                active);
            WorldStateSnapshot malformed =
                WorldStateTestFixtures.ActiveSnapshot(
                    active,
                    revision: 4L,
                    receipts: new[] { receipt },
                    extraActive: new[]
                    {
                        WorldStateTestFixtures.ActiveInstance(
                            instanceId: "world-instance-second")
                    });

            var future = new WorldEventInstance(
                active.InstanceId,
                active.DefinitionId,
                2,
                "content_v2",
                "source_v2",
                active.CorrelationId,
                active.OperationId,
                active.SourceSystemId,
                active.ExclusiveGroup,
                active.State,
                active.ScheduledAtUtcSeconds,
                active.StartedAtUtcSeconds,
                active.ExpectedEndAtUtcSeconds,
                active.CompletedAtUtcSeconds,
                active.CompletionReason,
                active.Revision,
                active.ResolvedEffects,
                active.CommittedEffectRevision);
            var futureReceipt = new WorldStateOperationReceipt(
                request.OperationId,
                request.CorrelationId,
                WorldStateTestFixtures.StartSemanticHash(request),
                WorldStateTransitionKind.Start,
                future.InstanceId,
                4L,
                future);
            WorldStateSnapshot futureSnapshot =
                WorldStateTestFixtures.ActiveSnapshot(
                    future,
                    revision: 4L,
                    receipts: new[] { futureReceipt });

            WorldStateLifecyclePlanner planner =
                WorldStateTestFixtures.Planner();
            WorldStatePlanningResult malformedResult =
                planner.PlanStart(request, malformed);
            WorldStatePlanningResult futureResult =
                planner.PlanStart(request, futureSnapshot);

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedMalformedSnapshot,
                malformedResult.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedReadOnlyProfile,
                futureResult.Status);
        }

        [Test]
        public void CorrelationReuseWithDifferentPayloadRejectsVisibly()
        {
            WorldStateStartRequest first = WorldStateTestFixtures.StartRequest();
            WorldEventInstance active = WorldStateTestFixtures.ActiveInstance();
            var receipt = new WorldStateOperationReceipt(
                first.OperationId,
                first.CorrelationId,
                WorldStateTestFixtures.StartSemanticHash(first),
                WorldStateTransitionKind.Start,
                active.InstanceId,
                4L,
                active);
            WorldStateStartRequest conflicting = WorldStateTestFixtures.StartRequest(
                duration: 601L);

            WorldStatePlanningResult result =
                WorldStateTestFixtures.Planner().PlanStart(
                    conflicting,
                    WorldStateTestFixtures.ActiveSnapshot(
                        active,
                        revision: 4L,
                        receipts: new[] { receipt }));

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedCorrelationConflict,
                result.Status);
            Assert.AreEqual("AL-WST-CORRELATION", result.Diagnostics[0].Code);
        }

        [Test]
        public void RequiredPreparationFailureRejectsBeforePlanPublication()
        {
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId)
            {
                ActivationStatus =
                    WorldEffectPreparationStatus.RejectedDomainUnavailable
            };

            WorldStatePlanningResult result =
                WorldStateTestFixtures.Planner(consumers: consumer).PlanStart(
                    WorldStateTestFixtures.StartRequest(),
                    WorldStateTestFixtures.EmptySnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedEffectPreparation,
                result.Status);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void MissingOptionalConsumerProducesExplicitOmissionAndNoEffectClaim()
        {
            WorldEffectDescriptor optional = WorldStateTestFixtures.Effect(
                effectId: WorldStateTestFixtures.OptionalEffectId,
                consumerId: WorldStateTestFixtures.OptionalConsumerId,
                required: false);
            WorldEventDefinition definition = WorldStateTestFixtures.Definition(
                effects: new[] { optional },
                requiredConsumers: Array.Empty<string>(),
                optionalConsumers: new[]
                {
                    WorldStateTestFixtures.OptionalConsumerId
                });
            var planner = new WorldStateLifecyclePlanner(
                new FakeDefinitionResolver(definition),
                new FakeClock(WorldStateTestFixtures.NowUtcSeconds),
                new WorldEffectConsumerRegistry(
                    Array.Empty<IWorldEffectConsumer>()));

            WorldStatePlanningResult result = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(),
                WorldStateTestFixtures.EmptySnapshot());

            Assert.AreEqual(WorldStatePlanningStatus.Prepared, result.Status);
            Assert.IsEmpty(result.Plan.PreparedEffectPlans);
            Assert.IsEmpty(result.Plan.InstanceAfter.ResolvedEffects);
            Assert.IsTrue(result.Diagnostics.Any(item =>
                item.Code == "AL-WST-CONSUMER-OPTIONAL-OMITTED"));
        }

        [Test]
        public void StartUsesCheckedUtcArithmetic()
        {
            long now = long.MaxValue - 10L;
            WorldEventDefinition definition = WorldStateTestFixtures.Definition(
                durationPolicy: new WorldEventDurationPolicy(1L, 100L, 60L, true));
            WorldStatePlanningResult result = WorldStateTestFixtures.Planner(
                definition,
                new FakeClock(now)).PlanStart(
                WorldStateTestFixtures.StartRequest(
                    requestedStartAt: now,
                    duration: 60L),
                WorldStateTestFixtures.EmptySnapshot(lastTrustedUtc: now - 1L));

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedOverflow,
                result.Status);
        }

        [Test]
        public void NaturalEndAtBoundaryPreparesRemovalAndDistinctCompletion()
        {
            WorldEventDefinition definition = WorldStateTestFixtures.Definition();
            WorldEventInstance active = WorldStateTestFixtures.ActiveInstance(
                definition,
                expectedEndAt: WorldStateTestFixtures.NowUtcSeconds);
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStatePlanningResult result = WorldStateTestFixtures.Planner(
                definition,
                consumers: consumer).PlanEnd(
                WorldStateTestFixtures.EndRequest(),
                WorldStateTestFixtures.ActiveSnapshot(active));

            Assert.AreEqual(WorldStatePlanningStatus.Prepared, result.Status);
            Assert.AreEqual(WorldStateTransitionKind.End, result.Plan.TransitionKind);
            Assert.AreEqual(
                WorldEventInstanceState.Ended,
                result.Plan.InstanceAfter.State);
            Assert.AreEqual(
                WorldEventCompletionReason.NaturalExpiry,
                result.Plan.InstanceAfter.CompletionReason);
            Assert.AreEqual(4L, result.Plan.ExpectedNewRevision);
            Assert.AreEqual(2L, result.Plan.InstanceAfter.Revision);
            Assert.AreEqual("remove:" + WorldStateTestFixtures.EffectId,
                consumer.PreparationOrder.Single());
        }

        [Test]
        public void EndBeforeBoundaryIsPureNoChange()
        {
            WorldStatePlanningResult result =
                WorldStateTestFixtures.Planner().PlanEnd(
                    WorldStateTestFixtures.EndRequest(),
                    WorldStateTestFixtures.ActiveSnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.NoChangeAlreadyInState,
                result.Status);
            Assert.IsNull(result.Plan);
        }

        [Test]
        public void EndRejectsWrongInstanceAndRequiredRemovalFailure()
        {
            WorldEventInstance due = WorldStateTestFixtures.ActiveInstance(
                expectedEndAt: WorldStateTestFixtures.NowUtcSeconds);
            WorldStatePlanningResult wrong =
                WorldStateTestFixtures.Planner().PlanEnd(
                    WorldStateTestFixtures.EndRequest(
                        instanceId: "world-instance-wrong"),
                    WorldStateTestFixtures.ActiveSnapshot(due));
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId)
            {
                RemovalStatus =
                    WorldEffectPreparationStatus.RejectedMalformedDomain
            };
            WorldStatePlanningResult failed =
                WorldStateTestFixtures.Planner(consumers: consumer).PlanEnd(
                    WorldStateTestFixtures.EndRequest(),
                    WorldStateTestFixtures.ActiveSnapshot(due));

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedWrongInstance,
                wrong.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedEffectPreparation,
                failed.Status);
        }

        [Test]
        public void OwnerCancellationIsDistinctFromNaturalEnd()
        {
            WorldEventInstance active = WorldStateTestFixtures.ActiveInstance();
            WorldStatePlanningResult result =
                WorldStateTestFixtures.Planner().PlanCancel(
                    WorldStateTestFixtures.CancelRequest(),
                    WorldStateTestFixtures.ActiveSnapshot(active));

            Assert.AreEqual(WorldStatePlanningStatus.Prepared, result.Status);
            Assert.AreEqual(
                WorldStateTransitionKind.Cancel,
                result.Plan.TransitionKind);
            Assert.AreEqual(
                WorldEventInstanceState.Cancelled,
                result.Plan.InstanceAfter.State);
            Assert.AreEqual(
                WorldEventCompletionReason.CancelledByOwner,
                result.Plan.InstanceAfter.CompletionReason);
            Assert.AreEqual(
                "al_notify_world_event_cancelled",
                result.Plan.NotificationIntents.Single().DefinitionId);
        }

        [Test]
        public void CancellationExactReplayAndConflictAreExplicit()
        {
            WorldStateCancelRequest request =
                WorldStateTestFixtures.CancelRequest();
            WorldStateSnapshot initial =
                WorldStateTestFixtures.ActiveSnapshot();
            WorldStateLifecyclePlanner planner =
                WorldStateTestFixtures.Planner();
            WorldStatePlanningResult first =
                planner.PlanCancel(request, initial);
            var receipt = new WorldStateOperationReceipt(
                first.Plan.OperationId,
                first.Plan.CorrelationId,
                first.Plan.SemanticHash,
                WorldStateTransitionKind.Cancel,
                first.Plan.InstanceAfter.InstanceId,
                first.Plan.ExpectedNewRevision,
                first.Plan.InstanceAfter);
            WorldStateSnapshot committed =
                WorldStateTestFixtures.EmptySnapshot(
                    revision: first.Plan.ExpectedNewRevision,
                    effectRevision:
                        first.Plan.InstanceAfter.CommittedEffectRevision,
                    receipts: new[] { receipt },
                    history: new[] { first.Plan.InstanceAfter });

            WorldStatePlanningResult replay =
                planner.PlanCancel(request, committed);
            WorldStatePlanningResult conflict =
                planner.PlanCancel(
                    WorldStateTestFixtures.CancelRequest(
                        now: WorldStateTestFixtures.NowUtcSeconds + 1L),
                    committed);

            Assert.AreEqual(
                WorldStatePlanningStatus.AlreadyCommitted,
                replay.Status);
            Assert.AreSame(receipt, replay.ExistingReceipt);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedCorrelationConflict,
                conflict.Status);
        }

        [Test]
        public void CancellationPolicyRejectsNotCancellableAndUnauthorizedSource()
        {
            WorldEventDefinition notCancellable = WorldStateTestFixtures.Definition(
                cancellationPolicy: WorldEventCancellationPolicy.NotCancellable);
            WorldEventInstance notCancellableActive =
                WorldStateTestFixtures.ActiveInstance(notCancellable);
            WorldStatePlanningResult blocked = WorldStateTestFixtures.Planner(
                notCancellable).PlanCancel(
                WorldStateTestFixtures.CancelRequest(),
                WorldStateTestFixtures.ActiveSnapshot(notCancellableActive));
            WorldStatePlanningResult unauthorized =
                WorldStateTestFixtures.Planner().PlanCancel(
                    WorldStateTestFixtures.CancelRequest(
                        sourceSystemId: "al_world_source_unknown"),
                    WorldStateTestFixtures.ActiveSnapshot());

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedCancellationNotAllowed,
                blocked.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedCancellationNotAllowed,
                unauthorized.Status);
        }

        [Test]
        public void ApprovedRecoveryCancellationRequiresExactSourceAndReason()
        {
            WorldEventDefinition recovery = WorldStateTestFixtures.Definition(
                cancellationPolicy:
                    WorldEventCancellationPolicy.CancellableByApprovedRecovery);
            WorldEventInstance active = WorldStateTestFixtures.ActiveInstance(recovery);
            WorldStatePlanningResult accepted = WorldStateTestFixtures.Planner(
                recovery).PlanCancel(
                WorldStateTestFixtures.CancelRequest(
                    reason: WorldEventCompletionReason.CancelledByRecovery,
                    sourceSystemId:
                        WorldStateTechnicalLimits.ApprovedRecoverySourceSystemId),
                WorldStateTestFixtures.ActiveSnapshot(active));
            WorldStatePlanningResult wrongReason = WorldStateTestFixtures.Planner(
                recovery).PlanCancel(
                WorldStateTestFixtures.CancelRequest(
                    reason: WorldEventCompletionReason.CancelledByOwner,
                    sourceSystemId:
                        WorldStateTechnicalLimits.ApprovedRecoverySourceSystemId),
                WorldStateTestFixtures.ActiveSnapshot(active));

            Assert.AreEqual(WorldStatePlanningStatus.Prepared, accepted.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedCancellationNotAllowed,
                wrongReason.Status);
        }

        [Test]
        public void ReconcileBeforeEndIsNoChangeAndAtEndUsesStableIdentity()
        {
            WorldStatePlanningResult before =
                WorldStateTestFixtures.Planner().PlanReconcile(
                    WorldStateTestFixtures.ActiveSnapshot());
            WorldEventInstance due = WorldStateTestFixtures.ActiveInstance(
                expectedEndAt: WorldStateTestFixtures.NowUtcSeconds);
            WorldStateSnapshot dueSnapshot =
                WorldStateTestFixtures.ActiveSnapshot(due);
            WorldStatePlanningResult first =
                WorldStateTestFixtures.Planner().PlanReconcile(dueSnapshot);
            WorldStatePlanningResult second =
                WorldStateTestFixtures.Planner().PlanReconcile(dueSnapshot);

            Assert.AreEqual(
                WorldStatePlanningStatus.NoChangeAlreadyInState,
                before.Status);
            Assert.AreEqual(WorldStatePlanningStatus.Prepared, first.Status);
            Assert.AreEqual(first.Plan.OperationId, second.Plan.OperationId);
            Assert.AreEqual(first.Plan.CorrelationId, second.Plan.CorrelationId);
            Assert.AreEqual(first.Plan.PlanHash, second.Plan.PlanHash);
        }

        [Test]
        public void NaturalEndReplayIgnoresLaterObservationTime()
        {
            WorldEventInstance due = WorldStateTestFixtures.ActiveInstance(
                expectedEndAt: WorldStateTestFixtures.NowUtcSeconds);
            WorldStateSnapshot firstSnapshot =
                WorldStateTestFixtures.ActiveSnapshot(due);
            WorldStatePlanningResult first =
                WorldStateTestFixtures.Planner().PlanReconcile(firstSnapshot);
            var receipt = new WorldStateOperationReceipt(
                first.Plan.OperationId,
                first.Plan.CorrelationId,
                first.Plan.SemanticHash,
                WorldStateTransitionKind.End,
                due.InstanceId,
                first.Plan.ExpectedNewRevision,
                first.Plan.InstanceAfter);
            WorldStateSnapshot retrySnapshot =
                WorldStateTestFixtures.EmptySnapshot(
                    revision: first.Plan.ExpectedNewRevision,
                    effectRevision:
                        first.Plan.InstanceAfter.CommittedEffectRevision,
                    history: new[] { first.Plan.InstanceAfter },
                    receipts: new[] { receipt });
            WorldStateLifecyclePlanner laterPlanner =
                WorldStateTestFixtures.Planner(
                    clock: new FakeClock(
                        WorldStateTestFixtures.NowUtcSeconds + 60L));

            WorldStatePlanningResult replay =
                laterPlanner.PlanEnd(
                    new WorldStateEndRequest(
                        due.InstanceId,
                        first.Plan.CorrelationId,
                        first.Plan.OperationId,
                        due.SourceSystemId,
                        WorldStateTestFixtures.NowUtcSeconds + 60L,
                        firstSnapshot.SnapshotRevision),
                    retrySnapshot);

            Assert.AreEqual(
                WorldStatePlanningStatus.AlreadyCommitted,
                replay.Status);
            Assert.AreSame(receipt, replay.ExistingReceipt);
            Assert.IsNull(replay.Plan);
        }

        [Test]
        public void PreparationOrderIsDeterministicForStartAndRemoval()
        {
            WorldEffectDescriptor second = WorldStateTestFixtures.Effect(
                effectId: "al_world_effect_second_modifier",
                applicationOrder: 1,
                removalOrder: 0);
            WorldEffectDescriptor first = WorldStateTestFixtures.Effect(
                applicationOrder: 0,
                removalOrder: 1);
            WorldEventDefinition definition = WorldStateTestFixtures.Definition(
                effects: new[] { second, first },
                requiredConsumers: new[] { WorldStateTestFixtures.ConsumerId });
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStateLifecyclePlanner planner = WorldStateTestFixtures.Planner(
                definition,
                consumers: consumer);

            WorldStatePlanningResult start = planner.PlanStart(
                WorldStateTestFixtures.StartRequest(),
                WorldStateTestFixtures.EmptySnapshot());
            consumer.PreparationOrder.Clear();
            WorldEventInstance due = WorldStateTestFixtures.ActiveInstance(
                definition,
                expectedEndAt: WorldStateTestFixtures.NowUtcSeconds);
            WorldStatePlanningResult end = planner.PlanEnd(
                WorldStateTestFixtures.EndRequest(),
                WorldStateTestFixtures.ActiveSnapshot(due));

            Assert.AreEqual(WorldStatePlanningStatus.Prepared, start.Status);
            Assert.AreEqual(WorldStatePlanningStatus.Prepared, end.Status);
            CollectionAssert.AreEqual(
                new[]
                {
                    "remove:al_world_effect_second_modifier",
                    "remove:" + WorldStateTestFixtures.EffectId
                },
                consumer.PreparationOrder);
            CollectionAssert.AreEqual(
                new[]
                {
                    "al_world_effect_second_modifier",
                    WorldStateTestFixtures.EffectId
                },
                end.Plan.PreparedEffectPlans.Select(item => item.EffectId));
        }

        [Test]
        public void MalformedOrFutureSnapshotNeverProducesPlan()
        {
            WorldStateSnapshot malformed = WorldStateTestFixtures.ActiveSnapshot(
                extraActive: new[]
                {
                    WorldStateTestFixtures.ActiveInstance(
                        instanceId: "world-instance-extra")
                });
            WorldEventInstance current = WorldStateTestFixtures.ActiveInstance();
            var future = new WorldEventInstance(
                current.InstanceId,
                current.DefinitionId,
                2,
                "content_v2",
                "source_v2",
                current.CorrelationId,
                current.OperationId,
                current.SourceSystemId,
                current.ExclusiveGroup,
                current.State,
                current.ScheduledAtUtcSeconds,
                current.StartedAtUtcSeconds,
                current.ExpectedEndAtUtcSeconds,
                0L,
                WorldEventCompletionReason.None,
                current.Revision,
                current.ResolvedEffects,
                current.CommittedEffectRevision);

            WorldStatePlanningResult malformedResult =
                WorldStateTestFixtures.Planner().PlanStart(
                    WorldStateTestFixtures.StartRequest(),
                    malformed);
            WorldStatePlanningResult futureResult =
                WorldStateTestFixtures.Planner().PlanEnd(
                    WorldStateTestFixtures.EndRequest(),
                    WorldStateTestFixtures.ActiveSnapshot(future));

            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedMalformedSnapshot,
                malformedResult.Status);
            Assert.AreEqual(
                WorldStatePlanningStatus.RejectedReadOnlyProfile,
                futureResult.Status);
            Assert.IsNull(malformedResult.Plan);
            Assert.IsNull(futureResult.Plan);
        }
    }
}
