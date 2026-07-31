using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AL.Core.Interfaces.WorldState;

namespace AL.Services.WorldState
{
    public sealed class WorldStateLifecyclePlanner
    {
        private readonly IWorldStateDefinitionResolver _definitions;
        private readonly IWorldStateClock _clock;
        private readonly WorldEffectConsumerRegistry _consumers;

        public WorldStateLifecyclePlanner(
            IWorldStateDefinitionResolver definitions,
            IWorldStateClock clock,
            WorldEffectConsumerRegistry consumers)
        {
            _definitions = definitions;
            _clock = clock;
            _consumers = consumers ??
                         new WorldEffectConsumerRegistry(
                             Array.Empty<IWorldEffectConsumer>());
        }

        public WorldStatePlanningResult PlanStart(
            WorldStateStartRequest request,
            WorldStateSnapshot snapshot)
        {
            if (request == null)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-START-REQUEST",
                    string.Empty,
                    "Start request is null.");
            }

            string semanticHash = WorldStateHash.Compute(
                "start",
                request.DefinitionId,
                request.InstanceId,
                request.CorrelationId,
                request.OperationId,
                request.SourceSystemId,
                request.RequestedStartAtUtcSeconds.ToString(
                    CultureInfo.InvariantCulture),
                request.RequestedDurationSeconds?.ToString(
                    CultureInfo.InvariantCulture) ?? "default",
                request.ExpectedSnapshotRevision.ToString(
                    CultureInfo.InvariantCulture));

            if (!WorldStateValidator.IsDefinitionId(request.DefinitionId) ||
                !WorldStateValidator.IsOpaqueId(request.InstanceId) ||
                !WorldStateValidator.IsOpaqueId(request.CorrelationId) ||
                !WorldStateValidator.IsOpaqueId(request.OperationId) ||
                !WorldStateValidator.IsSourceSystemId(request.SourceSystemId) ||
                request.RequestedStartAtUtcSeconds <= 0L ||
                request.ExpectedSnapshotRevision < 0L)
            {
                return Reject(
                    string.IsNullOrEmpty(request.CorrelationId)
                        ? WorldStatePlanningStatus.RejectedCorrelationRequired
                        : WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-START-REQUEST",
                    request.InstanceId,
                    "Start request identity, time, or revision is invalid.");
            }

            WorldStatePlanningResult replaySnapshotGate =
                ValidateReplaySnapshot(snapshot);
            if (replaySnapshotGate != null)
            {
                return replaySnapshotGate;
            }

            WorldStatePlanningResult replay = ClassifyReplay(
                snapshot,
                request.OperationId,
                request.CorrelationId,
                semanticHash,
                WorldStateTransitionKind.Start);
            if (replay != null)
            {
                return replay;
            }

            WorldStatePlanningResult snapshotGate = ValidatePlanningSnapshot(
                snapshot,
                request.ExpectedSnapshotRevision);
            if (snapshotGate != null)
            {
                return snapshotGate;
            }

            if (!TryReadClock(out long nowUtcSeconds))
            {
                return RejectClock(request.InstanceId);
            }

            if (request.RequestedStartAtUtcSeconds != nowUtcSeconds ||
                IsBackwardClock(snapshot, nowUtcSeconds))
            {
                return RejectClock(request.InstanceId);
            }

            if (!TryResolveDefinition(
                    request.DefinitionId,
                    out WorldEventDefinition definition))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                    "AL-WST-DEFINITION-UNAVAILABLE",
                    request.DefinitionId,
                    "Requested definition is unavailable.");
            }

            WorldStateDefinitionValidationResult definitionValidation =
                WorldStateValidator.ValidateDefinition(definition, _consumers);
            if (!definitionValidation.IsValid)
            {
                return new WorldStatePlanningResult(
                    definitionValidation.Status ==
                        WorldStateDefinitionValidationStatus.UnsupportedVersion
                        ? WorldStatePlanningStatus.RejectedUnsupportedDefinition
                        : WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                    null,
                    null,
                    definitionValidation.Diagnostics);
            }

            if (snapshot.ActiveInstances.Count != 0)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedActiveExclusiveInstance,
                    "AL-WST-EXCLUSIVE-ACTIVE",
                    snapshot.ActiveInstance?.InstanceId,
                    "The global-primary group already has an active instance.");
            }

            if (!definition.AllowedSourceSystemIds.Contains(
                    request.SourceSystemId,
                    StringComparer.Ordinal))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-SOURCE",
                    request.SourceSystemId,
                    "Source system is not allowed to start this definition.");
            }

            if (!TrySelectDuration(
                    definition.DurationPolicy,
                    request.RequestedDurationSeconds,
                    out long durationSeconds))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedInvalidDuration,
                    "AL-WST-DURATION",
                    request.DefinitionId,
                    "Requested duration is outside the definition policy.");
            }

            long expectedEndAtUtcSeconds;
            long expectedNewRevision;
            long expectedEffectRevision;
            try
            {
                expectedEndAtUtcSeconds = checked(nowUtcSeconds + durationSeconds);
                expectedNewRevision = checked(snapshot.SnapshotRevision + 1L);
                expectedEffectRevision = checked(
                    snapshot.CommittedEffectRevision + 1L);
            }
            catch (OverflowException)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedOverflow,
                    "AL-WST-DURATION-OVERFLOW",
                    request.InstanceId,
                    "Start time, duration, or revision overflows.");
            }

            var provisionalInstance = new WorldEventInstance(
                request.InstanceId,
                definition.DefinitionId,
                definition.SchemaVersion,
                definition.ContentVersion,
                definition.SourceRevision,
                request.CorrelationId,
                request.OperationId,
                request.SourceSystemId,
                definition.ExclusiveGroup,
                WorldEventInstanceState.Active,
                nowUtcSeconds,
                nowUtcSeconds,
                expectedEndAtUtcSeconds,
                0L,
                WorldEventCompletionReason.None,
                1L,
                Array.Empty<WorldResolvedEffectSummary>(),
                expectedEffectRevision);

            WorldStatePlanningResult preparation = PrepareActivation(
                definition,
                provisionalInstance,
                snapshot,
                out IReadOnlyList<WorldEffectPlan> effectPlans,
                out IReadOnlyList<WorldResolvedEffectSummary> resolvedEffects,
                out IReadOnlyList<WorldStateDiagnostic> preparationDiagnostics);
            if (preparation != null)
            {
                return preparation;
            }

            var activeInstance = new WorldEventInstance(
                provisionalInstance.InstanceId,
                provisionalInstance.DefinitionId,
                provisionalInstance.DefinitionSchemaVersion,
                provisionalInstance.DefinitionContentVersion,
                provisionalInstance.DefinitionSourceRevision,
                provisionalInstance.CorrelationId,
                provisionalInstance.OperationId,
                provisionalInstance.SourceSystemId,
                provisionalInstance.ExclusiveGroup,
                provisionalInstance.State,
                provisionalInstance.ScheduledAtUtcSeconds,
                provisionalInstance.StartedAtUtcSeconds,
                provisionalInstance.ExpectedEndAtUtcSeconds,
                provisionalInstance.CompletedAtUtcSeconds,
                provisionalInstance.CompletionReason,
                provisionalInstance.Revision,
                resolvedEffects,
                provisionalInstance.CommittedEffectRevision);

            return BuildPreparedPlan(
                WorldStateTransitionKind.Start,
                snapshot,
                null,
                activeInstance,
                effectPlans,
                definition.StartNotificationDefinitionId,
                request.OperationId,
                request.CorrelationId,
                request.SourceSystemId,
                nowUtcSeconds,
                semanticHash,
                expectedNewRevision,
                preparationDiagnostics);
        }

        public WorldStatePlanningResult PlanEnd(
            WorldStateEndRequest request,
            WorldStateSnapshot snapshot)
        {
            if (request == null)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-END-REQUEST",
                    string.Empty,
                    "End request is null.");
            }

            string semanticHash = WorldStateHash.Compute(
                "end",
                request.InstanceId,
                request.CorrelationId,
                request.OperationId,
                request.SourceSystemId,
                request.ExpectedSnapshotRevision.ToString(
                    CultureInfo.InvariantCulture));

            if (!IsValidTransitionRequest(
                    request.InstanceId,
                    request.CorrelationId,
                    request.OperationId,
                    request.SourceSystemId,
                    request.ObservedNowUtcSeconds,
                    request.ExpectedSnapshotRevision))
            {
                return Reject(
                    string.IsNullOrEmpty(request.CorrelationId)
                        ? WorldStatePlanningStatus.RejectedCorrelationRequired
                        : WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-END-REQUEST",
                    request.InstanceId,
                    "End request identity, time, or revision is invalid.");
            }

            WorldStatePlanningResult replaySnapshotGate =
                ValidateReplaySnapshot(snapshot);
            if (replaySnapshotGate != null)
            {
                return replaySnapshotGate;
            }

            WorldStatePlanningResult replay = ClassifyReplay(
                snapshot,
                request.OperationId,
                request.CorrelationId,
                semanticHash,
                WorldStateTransitionKind.End);
            if (replay != null)
            {
                return replay;
            }

            WorldStatePlanningResult snapshotGate = ValidatePlanningSnapshot(
                snapshot,
                request.ExpectedSnapshotRevision);
            if (snapshotGate != null)
            {
                return snapshotGate;
            }

            if (!TryReadClock(out long nowUtcSeconds) ||
                nowUtcSeconds != request.ObservedNowUtcSeconds ||
                IsBackwardClock(snapshot, nowUtcSeconds))
            {
                return RejectClock(request.InstanceId);
            }

            WorldEventInstance active = snapshot.ActiveInstance;
            if (active == null)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedNoActiveInstance,
                    "AL-WST-END-NO-ACTIVE",
                    request.InstanceId,
                    "There is no active instance to end.");
            }

            if (!string.Equals(
                    active.InstanceId,
                    request.InstanceId,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedWrongInstance,
                    "AL-WST-END-WRONG-INSTANCE",
                    request.InstanceId,
                    "End request does not target the active instance.");
            }

            if (!string.Equals(
                    request.SourceSystemId,
                    active.SourceSystemId,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    request.SourceSystemId,
                    WorldStateTechnicalLimits.ApprovedRecoverySourceSystemId,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-END-SOURCE",
                    request.SourceSystemId,
                    "End request source does not own the active instance.");
            }

            if (nowUtcSeconds < active.ExpectedEndAtUtcSeconds)
            {
                return NoChange(
                    "AL-WST-END-NOT-DUE",
                    active.InstanceId,
                    "The active instance has not reached its end instant.");
            }

            if (!TryResolveDefinition(active.DefinitionId, out WorldEventDefinition definition) ||
                definition.SchemaVersion != active.DefinitionSchemaVersion ||
                !string.Equals(
                    definition.ContentVersion,
                    active.DefinitionContentVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    definition.SourceRevision,
                    active.DefinitionSourceRevision,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                    "AL-WST-DEFINITION-INSTANCE",
                    active.DefinitionId,
                    "Exact committed definition is unavailable for removal.");
            }


            WorldStateDefinitionValidationResult endDefinitionValidation =
                WorldStateValidator.ValidateDefinition(definition, _consumers);
            if (!endDefinitionValidation.IsValid)
            {
                return new WorldStatePlanningResult(
                    WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                    null,
                    null,
                    endDefinitionValidation.Diagnostics);
            }

            WorldStatePlanningResult removal = PrepareRemoval(
                active,
                snapshot,
                WorldStateTransitionKind.End,
                out IReadOnlyList<WorldEffectPlan> effectPlans,
                out IReadOnlyList<WorldStateDiagnostic> removalDiagnostics);
            if (removal != null)
            {
                return removal;
            }

            if (!TryBuildCompletedInstance(
                    active,
                    WorldEventInstanceState.Ended,
                    WorldEventCompletionReason.NaturalExpiry,
                    nowUtcSeconds,
                    out WorldEventInstance ended) ||
                !TryIncrement(snapshot.SnapshotRevision, out long expectedNewRevision))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedOverflow,
                    "AL-WST-REVISION-OVERFLOW",
                    active.InstanceId,
                    "End revision overflows.");
            }

            return BuildPreparedPlan(
                WorldStateTransitionKind.End,
                snapshot,
                active,
                ended,
                effectPlans,
                definition.EndNotificationDefinitionId,
                request.OperationId,
                request.CorrelationId,
                request.SourceSystemId,
                nowUtcSeconds,
                semanticHash,
                expectedNewRevision,
                removalDiagnostics);
        }

        public WorldStatePlanningResult PlanCancel(
            WorldStateCancelRequest request,
            WorldStateSnapshot snapshot)
        {
            if (request == null)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-CANCEL-REQUEST",
                    string.Empty,
                    "Cancel request is null.");
            }

            string semanticHash = WorldStateHash.Compute(
                "cancel",
                request.InstanceId,
                request.CorrelationId,
                request.OperationId,
                request.SourceSystemId,
                ((int)request.CompletionReason).ToString(
                    CultureInfo.InvariantCulture),
                request.RequestedAtUtcSeconds.ToString(
                    CultureInfo.InvariantCulture),
                request.ExpectedSnapshotRevision.ToString(
                    CultureInfo.InvariantCulture));

            if (!IsValidTransitionRequest(
                    request.InstanceId,
                    request.CorrelationId,
                    request.OperationId,
                    request.SourceSystemId,
                    request.RequestedAtUtcSeconds,
                    request.ExpectedSnapshotRevision) ||
                (request.CompletionReason !=
                    WorldEventCompletionReason.CancelledByOwner &&
                 request.CompletionReason !=
                    WorldEventCompletionReason.CancelledByRecovery))
            {
                return Reject(
                    string.IsNullOrEmpty(request.CorrelationId)
                        ? WorldStatePlanningStatus.RejectedCorrelationRequired
                        : WorldStatePlanningStatus.RejectedInvalidRequest,
                    "AL-WST-CANCEL-REQUEST",
                    request.InstanceId,
                    "Cancel request identity, reason, time, or revision is invalid.");
            }

            WorldStatePlanningResult replaySnapshotGate =
                ValidateReplaySnapshot(snapshot);
            if (replaySnapshotGate != null)
            {
                return replaySnapshotGate;
            }

            WorldStatePlanningResult replay = ClassifyReplay(
                snapshot,
                request.OperationId,
                request.CorrelationId,
                semanticHash,
                WorldStateTransitionKind.Cancel);
            if (replay != null)
            {
                return replay;
            }

            WorldStatePlanningResult snapshotGate = ValidatePlanningSnapshot(
                snapshot,
                request.ExpectedSnapshotRevision);
            if (snapshotGate != null)
            {
                return snapshotGate;
            }

            if (!TryReadClock(out long nowUtcSeconds) ||
                nowUtcSeconds != request.RequestedAtUtcSeconds ||
                IsBackwardClock(snapshot, nowUtcSeconds))
            {
                return RejectClock(request.InstanceId);
            }

            WorldEventInstance active = snapshot.ActiveInstance;
            if (active == null)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedNoActiveInstance,
                    "AL-WST-CANCEL-NO-ACTIVE",
                    request.InstanceId,
                    "There is no active instance to cancel.");
            }

            if (!string.Equals(
                    active.InstanceId,
                    request.InstanceId,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedWrongInstance,
                    "AL-WST-CANCEL-WRONG-INSTANCE",
                    request.InstanceId,
                    "Cancel request does not target the active instance.");
            }

            if (!TryResolveDefinition(active.DefinitionId, out WorldEventDefinition definition) ||
                definition.SchemaVersion != active.DefinitionSchemaVersion ||
                !string.Equals(
                    definition.ContentVersion,
                    active.DefinitionContentVersion,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    definition.SourceRevision,
                    active.DefinitionSourceRevision,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                    "AL-WST-DEFINITION-INSTANCE",
                    active.DefinitionId,
                    "Exact committed definition is unavailable for cancellation.");
            }


            WorldStateDefinitionValidationResult cancelDefinitionValidation =
                WorldStateValidator.ValidateDefinition(definition, _consumers);
            if (!cancelDefinitionValidation.IsValid)
            {
                return new WorldStatePlanningResult(
                    WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                    null,
                    null,
                    cancelDefinitionValidation.Diagnostics);
            }

            if (!CanCancel(definition, active, request))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedCancellationNotAllowed,
                    "AL-WST-CANCELLATION",
                    active.InstanceId,
                    "Cancellation source or reason is not authorized.");
            }

            WorldStatePlanningResult removal = PrepareRemoval(
                active,
                snapshot,
                WorldStateTransitionKind.Cancel,
                out IReadOnlyList<WorldEffectPlan> effectPlans,
                out IReadOnlyList<WorldStateDiagnostic> removalDiagnostics);
            if (removal != null)
            {
                return removal;
            }

            if (!TryBuildCompletedInstance(
                    active,
                    WorldEventInstanceState.Cancelled,
                    request.CompletionReason,
                    nowUtcSeconds,
                    out WorldEventInstance cancelled) ||
                !TryIncrement(snapshot.SnapshotRevision, out long expectedNewRevision))
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedOverflow,
                    "AL-WST-REVISION-OVERFLOW",
                    active.InstanceId,
                    "Cancellation revision overflows.");
            }

            return BuildPreparedPlan(
                WorldStateTransitionKind.Cancel,
                snapshot,
                active,
                cancelled,
                effectPlans,
                definition.CancelNotificationDefinitionId,
                request.OperationId,
                request.CorrelationId,
                request.SourceSystemId,
                nowUtcSeconds,
                semanticHash,
                expectedNewRevision,
                removalDiagnostics);
        }

        public WorldStatePlanningResult PlanReconcile(
            WorldStateSnapshot snapshot)
        {
            WorldStatePlanningResult snapshotGate = ValidatePlanningSnapshot(
                snapshot,
                snapshot?.SnapshotRevision ?? -1L);
            if (snapshotGate != null)
            {
                return snapshotGate;
            }

            if (!TryReadClock(out long nowUtcSeconds) ||
                IsBackwardClock(snapshot, nowUtcSeconds))
            {
                return RejectClock(snapshot?.ActiveInstance?.InstanceId);
            }

            WorldEventInstance active = snapshot.ActiveInstance;
            if (active == null)
            {
                return NoChange(
                    "AL-WST-RECONCILE-NO-ACTIVE",
                    string.Empty,
                    "No active instance requires reconciliation.");
            }

            if (nowUtcSeconds < active.ExpectedEndAtUtcSeconds)
            {
                return NoChange(
                    "AL-WST-RECONCILE-NOT-DUE",
                    active.InstanceId,
                    "Active instance has not reached its end instant.");
            }

            string identityHash = WorldStateHash.Compute(
                "natural-end",
                active.InstanceId,
                active.Revision.ToString(CultureInfo.InvariantCulture),
                active.ExpectedEndAtUtcSeconds.ToString(
                    CultureInfo.InvariantCulture));
            string operationId = "al_world_reconcile_" +
                                 identityHash.Substring(0, 40);
            string correlationId = "al_world_correlation_" +
                                   identityHash.Substring(0, 40);
            return PlanEnd(
                new WorldStateEndRequest(
                    active.InstanceId,
                    correlationId,
                    operationId,
                    active.SourceSystemId,
                    nowUtcSeconds,
                    snapshot.SnapshotRevision),
                snapshot);
        }

        private WorldStatePlanningResult PrepareActivation(
            WorldEventDefinition definition,
            WorldEventInstance instance,
            WorldStateSnapshot snapshot,
            out IReadOnlyList<WorldEffectPlan> plans,
            out IReadOnlyList<WorldResolvedEffectSummary> summaries,
            out IReadOnlyList<WorldStateDiagnostic> diagnostics)
        {
            var preparedPlans = new List<WorldEffectPlan>();
            var resolvedEffects = new List<WorldResolvedEffectSummary>();
            var collectedDiagnostics = new List<WorldStateDiagnostic>();

            foreach (WorldEffectDescriptor descriptor in
                     definition.EffectDescriptors
                         .OrderBy(item => item.ApplicationOrder)
                         .ThenBy(item => item.EffectId, StringComparer.Ordinal))
            {
                if (!_consumers.TryGetAvailable(
                        descriptor.ConsumerId,
                        out IWorldEffectConsumer consumer))
                {
                    if (descriptor.Required)
                    {
                        plans = Array.Empty<WorldEffectPlan>();
                        summaries = Array.Empty<WorldResolvedEffectSummary>();
                        diagnostics = Array.Empty<WorldStateDiagnostic>();
                        return Reject(
                            WorldStatePlanningStatus.RejectedConsumerUnavailable,
                            "AL-WST-CONSUMER",
                            descriptor.ConsumerId,
                            "Required activation consumer is unavailable.");
                    }

                    collectedDiagnostics.Add(new WorldStateDiagnostic(
                        WorldStateDiagnosticSeverity.Warning,
                        "AL-WST-CONSUMER-OPTIONAL-OMITTED",
                        descriptor.ConsumerId,
                        "Optional activation consumer is unavailable."));
                    continue;
                }

                WorldEffectPreparationResult result;
                try
                {
                    result = consumer.PrepareActivate(
                        instance,
                        descriptor,
                        snapshot);
                }
                catch
                {
                    result = null;
                }

                if (!IsSuccessfulPreparation(result, descriptor, WorldStateTransitionKind.Start))
                {
                    if (descriptor.Required)
                    {
                        plans = Array.Empty<WorldEffectPlan>();
                        summaries = Array.Empty<WorldResolvedEffectSummary>();
                        diagnostics = Array.Empty<WorldStateDiagnostic>();
                        return Reject(
                            WorldStatePlanningStatus.RejectedEffectPreparation,
                            "AL-WST-EFFECT-PREPARE",
                            descriptor.EffectId,
                            "Required effect activation could not be prepared.");
                    }

                    collectedDiagnostics.Add(new WorldStateDiagnostic(
                        WorldStateDiagnosticSeverity.Warning,
                        "AL-WST-EFFECT-OPTIONAL-OMITTED",
                        descriptor.EffectId,
                        "Optional effect activation could not be prepared."));
                    continue;
                }

                collectedDiagnostics.AddRange(result.Diagnostics);
                if (result.Status == WorldEffectPreparationStatus.Prepared)
                {
                    preparedPlans.Add(result.Plan);
                }

                string parameterHash = WorldStateHash.Parameters(descriptor.Parameters);
                resolvedEffects.Add(new WorldResolvedEffectSummary(
                    descriptor.EffectId,
                    descriptor.ConsumerId,
                    descriptor.Operation,
                    descriptor.Parameters,
                    parameterHash,
                    result.Plan?.ConsumerPlanVersion ?? 1,
                    descriptor.Required,
                    descriptor.RemovalOrder));
            }

            plans = preparedPlans;
            summaries = resolvedEffects;
            diagnostics = WorldStateValidator.OrderDiagnostics(collectedDiagnostics);
            return null;
        }

        private WorldStatePlanningResult PrepareRemoval(
            WorldEventInstance instance,
            WorldStateSnapshot snapshot,
            WorldStateTransitionKind transitionKind,
            out IReadOnlyList<WorldEffectPlan> plans,
            out IReadOnlyList<WorldStateDiagnostic> diagnostics)
        {
            var preparedPlans = new List<WorldEffectPlan>();
            var collectedDiagnostics = new List<WorldStateDiagnostic>();

            foreach (WorldResolvedEffectSummary summary in
                     instance.ResolvedEffects
                         .OrderBy(item => item.RemovalOrder)
                         .ThenBy(item => item.EffectId, StringComparer.Ordinal))
            {
                if (!_consumers.TryGetAvailable(
                        summary.ConsumerId,
                        out IWorldEffectConsumer consumer))
                {
                    if (summary.Required)
                    {
                        plans = Array.Empty<WorldEffectPlan>();
                        diagnostics = Array.Empty<WorldStateDiagnostic>();
                        return Reject(
                            WorldStatePlanningStatus.RejectedConsumerUnavailable,
                            "AL-WST-CONSUMER-REMOVE",
                            summary.ConsumerId,
                            "Required removal consumer is unavailable.");
                    }

                    collectedDiagnostics.Add(new WorldStateDiagnostic(
                        WorldStateDiagnosticSeverity.Warning,
                        "AL-WST-CONSUMER-OPTIONAL-REMOVE-OMITTED",
                        summary.ConsumerId,
                        "Optional removal consumer is unavailable."));
                    continue;
                }

                WorldEffectPreparationResult result;
                try
                {
                    result = consumer.PrepareRemove(
                        instance,
                        summary,
                        transitionKind,
                        snapshot);
                }
                catch
                {
                    result = null;
                }

                if (!IsSuccessfulPreparation(result, summary, transitionKind))
                {
                    if (summary.Required)
                    {
                        plans = Array.Empty<WorldEffectPlan>();
                        diagnostics = Array.Empty<WorldStateDiagnostic>();
                        return Reject(
                            WorldStatePlanningStatus.RejectedEffectPreparation,
                            "AL-WST-EFFECT-REMOVE",
                            summary.EffectId,
                            "Required effect removal could not be prepared.");
                    }

                    collectedDiagnostics.Add(new WorldStateDiagnostic(
                        WorldStateDiagnosticSeverity.Warning,
                        "AL-WST-EFFECT-OPTIONAL-REMOVE-OMITTED",
                        summary.EffectId,
                        "Optional effect removal could not be prepared."));
                    continue;
                }

                collectedDiagnostics.AddRange(result.Diagnostics);
                if (result.Status == WorldEffectPreparationStatus.Prepared)
                {
                    preparedPlans.Add(result.Plan);
                }
            }

            plans = preparedPlans;
            diagnostics = WorldStateValidator.OrderDiagnostics(collectedDiagnostics);
            return null;
        }

        private static bool IsSuccessfulPreparation(
            WorldEffectPreparationResult result,
            WorldEffectDescriptor descriptor,
            WorldStateTransitionKind transitionKind)
        {
            if (result == null ||
                (result.Status != WorldEffectPreparationStatus.Prepared &&
                 result.Status != WorldEffectPreparationStatus.NoChange))
            {
                return false;
            }

            if (result.Status == WorldEffectPreparationStatus.NoChange)
            {
                return result.Plan == null;
            }

            return IsValidPlan(
                result.Plan,
                descriptor.ConsumerId,
                descriptor.EffectId,
                transitionKind,
                descriptor.Required,
                descriptor.ApplicationOrder,
                WorldStateHash.Parameters(descriptor.Parameters));
        }

        private static bool IsSuccessfulPreparation(
            WorldEffectPreparationResult result,
            WorldResolvedEffectSummary summary,
            WorldStateTransitionKind transitionKind)
        {
            if (result == null ||
                (result.Status != WorldEffectPreparationStatus.Prepared &&
                 result.Status != WorldEffectPreparationStatus.NoChange))
            {
                return false;
            }

            if (result.Status == WorldEffectPreparationStatus.NoChange)
            {
                return result.Plan == null;
            }

            return IsValidPlan(
                result.Plan,
                summary.ConsumerId,
                summary.EffectId,
                transitionKind,
                summary.Required,
                summary.RemovalOrder,
                summary.ParameterHash);
        }

        internal static bool IsValidPlan(
            WorldEffectPlan plan,
            string consumerId,
            string effectId,
            WorldStateTransitionKind transitionKind,
            bool required,
            int order,
            string parameterHash)
        {
            return plan != null &&
                   string.Equals(
                       plan.ConsumerId,
                       consumerId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       plan.EffectId,
                       effectId,
                       StringComparison.Ordinal) &&
                   plan.ConsumerPlanVersion > 0 &&
                   plan.TransitionKind == transitionKind &&
                   WorldStateValidator.IsOpaqueId(plan.ExpectedConsumerRevision) &&
                   string.Equals(
                       plan.ParameterHash,
                       parameterHash,
                       StringComparison.Ordinal) &&
                   WorldStateValidator.AreValidParameters(plan.Parameters) &&
                   plan.Required == required &&
                   plan.Order == order;
        }

        private WorldStatePlanningResult ValidatePlanningSnapshot(
            WorldStateSnapshot snapshot,
            long expectedRevision)
        {
            if (snapshot == null)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedNoCurrentSave,
                    "AL-WST-SNAPSHOT-NULL",
                    string.Empty,
                    "No world-state snapshot is available.");
            }

            WorldStateInstanceValidationResult validation =
                WorldStateValidator.ValidateSnapshot(snapshot);
            if (validation.Status == WorldStateInstanceValidationStatus.Invalid)
            {
                return new WorldStatePlanningResult(
                    WorldStatePlanningStatus.RejectedMalformedSnapshot,
                    null,
                    null,
                    validation.Diagnostics);
            }

            if (validation.Status ==
                WorldStateInstanceValidationStatus.PreservedUnsupportedFuture)
            {
                return new WorldStatePlanningResult(
                    WorldStatePlanningStatus.RejectedReadOnlyProfile,
                    null,
                    null,
                    validation.Diagnostics);
            }

            if (snapshot.Status ==
                WorldStateSnapshotStatus.UnavailableNoCurrentSave)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedNoCurrentSave,
                    "AL-WST-SNAPSHOT-UNAVAILABLE",
                    string.Empty,
                    "No current save is available.");
            }

            if (!snapshot.ProfileWritable ||
                snapshot.Status == WorldStateSnapshotStatus.AvailableReadOnly ||
                snapshot.Status == WorldStateSnapshotStatus.RecoveryRequired ||
                snapshot.Status ==
                    WorldStateSnapshotStatus.UnsupportedDefinitionVersion)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedReadOnlyProfile,
                    "AL-WST-SNAPSHOT-READONLY",
                    string.Empty,
                    "World-state snapshot is read-only or requires recovery.");
            }

            if (snapshot.Status == WorldStateSnapshotStatus.UnavailableCatalog)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedDefinitionUnavailable,
                    "AL-WST-CATALOG-UNAVAILABLE",
                    string.Empty,
                    "World-state catalog is unavailable.");
            }

            if (snapshot.Status ==
                    WorldStateSnapshotStatus.UnavailableMalformedState ||
                snapshot.SnapshotRevision != expectedRevision)
            {
                return Reject(
                    snapshot.SnapshotRevision != expectedRevision
                        ? WorldStatePlanningStatus.RejectedStaleSnapshot
                        : WorldStatePlanningStatus.RejectedMalformedSnapshot,
                    snapshot.SnapshotRevision != expectedRevision
                        ? "AL-WST-STALE"
                        : "AL-WST-SNAPSHOT-MALFORMED",
                    string.Empty,
                    snapshot.SnapshotRevision != expectedRevision
                        ? "Expected snapshot revision is stale."
                        : "World-state snapshot is malformed.");
            }

            return null;
        }

        private static WorldStatePlanningResult ValidateReplaySnapshot(
            WorldStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return Reject(
                    WorldStatePlanningStatus.RejectedNoCurrentSave,
                    "AL-WST-SNAPSHOT-NULL",
                    string.Empty,
                    "No world-state snapshot is available.");
            }

            WorldStateInstanceValidationResult validation =
                WorldStateValidator.ValidateSnapshot(snapshot);
            if (validation.Status == WorldStateInstanceValidationStatus.Invalid)
            {
                return new WorldStatePlanningResult(
                    WorldStatePlanningStatus.RejectedMalformedSnapshot,
                    null,
                    null,
                    validation.Diagnostics);
            }

            if (validation.Status ==
                WorldStateInstanceValidationStatus.PreservedUnsupportedFuture)
            {
                return new WorldStatePlanningResult(
                    WorldStatePlanningStatus.RejectedReadOnlyProfile,
                    null,
                    null,
                    validation.Diagnostics);
            }

            return null;
        }

        private static WorldStatePlanningResult ClassifyReplay(
            WorldStateSnapshot snapshot,
            string operationId,
            string correlationId,
            string semanticHash,
            WorldStateTransitionKind transitionKind)
        {
            if (snapshot == null)
            {
                return null;
            }

            List<WorldStateOperationReceipt> matches = snapshot.OperationReceipts
                .Where(receipt => receipt != null &&
                    (string.Equals(
                         receipt.OperationId,
                         operationId,
                         StringComparison.Ordinal) ||
                     string.Equals(
                         receipt.CorrelationId,
                         correlationId,
                         StringComparison.Ordinal)))
                .ToList();

            if (matches.Count == 0)
            {
                return null;
            }

            if (matches.Count == 1)
            {
                WorldStateOperationReceipt receipt = matches[0];
                if (string.Equals(
                        receipt.OperationId,
                        operationId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        receipt.CorrelationId,
                        correlationId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        receipt.SemanticHash,
                        semanticHash,
                        StringComparison.Ordinal) &&
                    receipt.TransitionKind == transitionKind)
                {
                    return new WorldStatePlanningResult(
                        WorldStatePlanningStatus.AlreadyCommitted,
                        null,
                        receipt,
                        Array.Empty<WorldStateDiagnostic>());
                }
            }

            return Reject(
                WorldStatePlanningStatus.RejectedCorrelationConflict,
                "AL-WST-CORRELATION",
                operationId,
                "Operation or correlation identity conflicts with prior semantics.");
        }

        private WorldStatePlanningResult BuildPreparedPlan(
            WorldStateTransitionKind transitionKind,
            WorldStateSnapshot snapshot,
            WorldEventInstance instanceBefore,
            WorldEventInstance instanceAfter,
            IEnumerable<WorldEffectPlan> effectPlans,
            string notificationDefinitionId,
            string operationId,
            string correlationId,
            string sourceSystemId,
            long nowUtcSeconds,
            string semanticHash,
            long expectedNewRevision,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            string planIdentityHash = WorldStateHash.Compute(
                "plan-id",
                operationId,
                semanticHash,
                expectedNewRevision.ToString(CultureInfo.InvariantCulture));
            string planId = "al_world_plan_" + planIdentityHash;
            var notificationIntents = string.IsNullOrEmpty(notificationDefinitionId)
                ? Array.Empty<WorldStateNotificationIntent>()
                : new[]
                {
                    new WorldStateNotificationIntent(
                        notificationDefinitionId,
                        "al_world_notification_" +
                        WorldStateHash.Compute(
                            correlationId,
                            transitionKind.ToString()).Substring(0, 40),
                        instanceAfter.InstanceId)
                };
            var ledgerEntry = new WorldStateLedgerEntry(
                operationId,
                correlationId,
                instanceAfter.InstanceId,
                instanceAfter.DefinitionId,
                transitionKind,
                snapshot.SnapshotRevision,
                expectedNewRevision,
                WorldStateLedgerResultStatus.Committed,
                nowUtcSeconds,
                semanticHash);
            var postCommitEvent = new WorldStateTransitionEvent(
                instanceAfter.InstanceId,
                instanceAfter.DefinitionId,
                transitionKind,
                instanceBefore?.State ?? WorldEventInstanceState.None,
                instanceAfter.State,
                snapshot.SnapshotRevision,
                expectedNewRevision,
                operationId,
                correlationId,
                sourceSystemId,
                nowUtcSeconds,
                instanceAfter.CommittedEffectRevision);

            IReadOnlyList<WorldEffectPlan> orderedPlans = (effectPlans ??
                    Array.Empty<WorldEffectPlan>())
                .OrderBy(item => item.Order)
                .ThenBy(item => item.ConsumerId, StringComparer.Ordinal)
                .ThenBy(item => item.EffectId, StringComparer.Ordinal)
                .ToList();
            string planHash = BuildPlanHash(
                planId,
                transitionKind,
                snapshot,
                instanceAfter,
                orderedPlans,
                semanticHash,
                expectedNewRevision);

            var plan = new WorldStateTransitionPlan(
                planId,
                transitionKind,
                snapshot.SnapshotRevision,
                expectedNewRevision,
                instanceBefore,
                instanceAfter,
                orderedPlans,
                operationId,
                correlationId,
                sourceSystemId,
                notificationIntents,
                ledgerEntry,
                postCommitEvent,
                snapshot.PolicyRevision,
                snapshot.CatalogRevision,
                semanticHash,
                planHash,
                diagnostics);
            return new WorldStatePlanningResult(
                WorldStatePlanningStatus.Prepared,
                plan,
                null,
                diagnostics);
        }

        private static string BuildPlanHash(
            string planId,
            WorldStateTransitionKind transitionKind,
            WorldStateSnapshot snapshot,
            WorldEventInstance instanceAfter,
            IEnumerable<WorldEffectPlan> plans,
            string semanticHash,
            long expectedNewRevision)
        {
            var values = new List<string>
            {
                planId,
                ((int)transitionKind).ToString(CultureInfo.InvariantCulture),
                snapshot.SnapshotRevision.ToString(CultureInfo.InvariantCulture),
                expectedNewRevision.ToString(CultureInfo.InvariantCulture),
                snapshot.PolicyRevision,
                snapshot.CatalogRevision,
                instanceAfter.InstanceId,
                instanceAfter.DefinitionId,
                ((int)instanceAfter.State).ToString(CultureInfo.InvariantCulture),
                instanceAfter.Revision.ToString(CultureInfo.InvariantCulture),
                semanticHash
            };
            foreach (WorldEffectPlan plan in plans)
            {
                values.Add(plan.ConsumerId);
                values.Add(plan.EffectId);
                values.Add(plan.ParameterHash);
                values.Add(plan.Order.ToString(CultureInfo.InvariantCulture));
            }

            return WorldStateHash.Compute(values.ToArray());
        }

        private bool TryResolveDefinition(
            string definitionId,
            out WorldEventDefinition definition)
        {
            definition = null;
            if (_definitions == null)
            {
                return false;
            }

            try
            {
                return _definitions.IsAvailable &&
                       _definitions.TryResolve(definitionId, out definition) &&
                       definition != null &&
                       string.Equals(
                           definition.DefinitionId,
                           definitionId,
                           StringComparison.Ordinal);
            }
            catch
            {
                definition = null;
                return false;
            }
        }

        private bool TryReadClock(out long nowUtcSeconds)
        {
            nowUtcSeconds = 0L;
            if (_clock == null)
            {
                return false;
            }

            try
            {
                nowUtcSeconds = _clock.UtcNowSeconds;
                return nowUtcSeconds > 0L;
            }
            catch
            {
                nowUtcSeconds = 0L;
                return false;
            }
        }

        private static bool IsBackwardClock(
            WorldStateSnapshot snapshot,
            long nowUtcSeconds)
        {
            return snapshot.LastTrustedUtcSeconds > 0L &&
                   nowUtcSeconds < snapshot.LastTrustedUtcSeconds;
        }

        private static bool TrySelectDuration(
            WorldEventDurationPolicy policy,
            long? requestedDurationSeconds,
            out long durationSeconds)
        {
            durationSeconds = 0L;
            if (policy == null)
            {
                return false;
            }

            durationSeconds = requestedDurationSeconds ??
                              policy.DefaultDurationSeconds;
            if (requestedDurationSeconds.HasValue &&
                !policy.CallerMayOverrideDuration &&
                durationSeconds != policy.DefaultDurationSeconds)
            {
                return false;
            }

            return durationSeconds >= policy.MinimumDurationSeconds &&
                   durationSeconds <= policy.MaximumDurationSeconds &&
                   durationSeconds <=
                   WorldStateTechnicalLimits.MaximumDurationSeconds;
        }

        private static bool IsValidTransitionRequest(
            string instanceId,
            string correlationId,
            string operationId,
            string sourceSystemId,
            long requestedAtUtcSeconds,
            long expectedSnapshotRevision)
        {
            return WorldStateValidator.IsOpaqueId(instanceId) &&
                   WorldStateValidator.IsOpaqueId(correlationId) &&
                   WorldStateValidator.IsOpaqueId(operationId) &&
                   WorldStateValidator.IsSourceSystemId(sourceSystemId) &&
                   requestedAtUtcSeconds > 0L &&
                   expectedSnapshotRevision >= 0L;
        }

        private static bool CanCancel(
            WorldEventDefinition definition,
            WorldEventInstance active,
            WorldStateCancelRequest request)
        {
            if (definition.CancellationPolicy ==
                    WorldEventCancellationPolicy.CancellableByOwningSource)
            {
                return request.CompletionReason ==
                           WorldEventCompletionReason.CancelledByOwner &&
                       string.Equals(
                           request.SourceSystemId,
                           active.SourceSystemId,
                           StringComparison.Ordinal);
            }

            if (definition.CancellationPolicy ==
                    WorldEventCancellationPolicy.CancellableByApprovedRecovery)
            {
                return request.CompletionReason ==
                           WorldEventCompletionReason.CancelledByRecovery &&
                       string.Equals(
                           request.SourceSystemId,
                           WorldStateTechnicalLimits.ApprovedRecoverySourceSystemId,
                           StringComparison.Ordinal);
            }

            return false;
        }

        private static bool TryBuildCompletedInstance(
            WorldEventInstance active,
            WorldEventInstanceState state,
            WorldEventCompletionReason reason,
            long completedAtUtcSeconds,
            out WorldEventInstance completed)
        {
            completed = null;
            try
            {
                long instanceRevision = checked(active.Revision + 1L);
                long effectRevision = checked(active.CommittedEffectRevision + 1L);
                completed = new WorldEventInstance(
                    active.InstanceId,
                    active.DefinitionId,
                    active.DefinitionSchemaVersion,
                    active.DefinitionContentVersion,
                    active.DefinitionSourceRevision,
                    active.CorrelationId,
                    active.OperationId,
                    active.SourceSystemId,
                    active.ExclusiveGroup,
                    state,
                    active.ScheduledAtUtcSeconds,
                    active.StartedAtUtcSeconds,
                    active.ExpectedEndAtUtcSeconds,
                    completedAtUtcSeconds,
                    reason,
                    instanceRevision,
                    active.ResolvedEffects,
                    effectRevision);
                return true;
            }
            catch (OverflowException)
            {
                completed = null;
                return false;
            }
        }

        private static bool TryIncrement(long value, out long incremented)
        {
            try
            {
                incremented = checked(value + 1L);
                return true;
            }
            catch (OverflowException)
            {
                incremented = 0L;
                return false;
            }
        }

        private static WorldStatePlanningResult RejectClock(string subjectId)
        {
            return Reject(
                WorldStatePlanningStatus.RejectedClockInvalid,
                "AL-WST-CLOCK",
                subjectId,
                "UTC clock is unavailable, mismatched, or moved backward.");
        }

        private static WorldStatePlanningResult NoChange(
            string code,
            string subjectId,
            string message)
        {
            return new WorldStatePlanningResult(
                WorldStatePlanningStatus.NoChangeAlreadyInState,
                null,
                null,
                new[]
                {
                    new WorldStateDiagnostic(
                        WorldStateDiagnosticSeverity.Info,
                        code,
                        subjectId,
                        message)
                });
        }

        private static WorldStatePlanningResult Reject(
            WorldStatePlanningStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new WorldStatePlanningResult(
                status,
                null,
                null,
                new[] { WorldStateValidator.Error(code, subjectId, message) });
        }
    }
}
