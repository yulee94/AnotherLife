using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AL.Core.Interfaces.WorldState
{
    public enum WorldStateDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public enum WorldEventCategory
    {
        NarrativeSignal = 0,
        RealmCondition = 1,
        WarzoneCondition = 2,
        ProductionCondition = 3,
        SystemCondition = 4
    }

    public enum WorldEventScope
    {
        Global = 0
    }

    public enum WorldEventCancellationPolicy
    {
        NotCancellable = 0,
        CancellableByOwningSource = 1,
        CancellableByApprovedRecovery = 2
    }

    public enum WorldEventSupersessionPolicy
    {
        RejectWhileExclusiveInstanceActive = 0
    }

    public enum WorldEventInstanceState
    {
        None = 0,
        Scheduled = 1,
        Active = 2,
        Ended = 3,
        Cancelled = 4,
        Failed = 5,
        Superseded = 6
    }

    public enum WorldEventCompletionReason
    {
        None = 0,
        NaturalExpiry = 1,
        CancelledByOwner = 2,
        CancelledByRecovery = 3,
        ActivationFailed = 4,
        RemovalFailed = 5,
        DefinitionUnsupported = 6,
        ClockInvalid = 7
    }

    public enum WorldEffectOperation
    {
        Multiplier = 0,
        AdditiveModifier = 1,
        CapabilityBlock = 2,
        PresentationProfile = 3
    }

    public enum WorldEffectParameterKind
    {
        Integer = 0,
        Number = 1,
        Boolean = 2,
        Reference = 3
    }

    public enum WorldStateSnapshotStatus
    {
        AvailableNoActiveEvent = 0,
        AvailableActive = 1,
        AvailableReadOnly = 2,
        UnavailableNoCurrentSave = 3,
        UnavailableMalformedState = 4,
        UnavailableCatalog = 5,
        UnsupportedDefinitionVersion = 6,
        RecoveryRequired = 7
    }

    public enum WorldStateTransitionKind
    {
        Start = 0,
        End = 1,
        Cancel = 2
    }

    public enum WorldStatePlanningStatus
    {
        Prepared = 0,
        NoChangeAlreadyInState = 1,
        AlreadyCommitted = 2,
        RejectedNoCurrentSave = 3,
        RejectedReadOnlyProfile = 4,
        RejectedDefinitionUnavailable = 5,
        RejectedUnsupportedDefinition = 6,
        RejectedInvalidRequest = 7,
        RejectedInvalidDuration = 8,
        RejectedActiveExclusiveInstance = 9,
        RejectedNoActiveInstance = 10,
        RejectedWrongInstance = 11,
        RejectedCancellationNotAllowed = 12,
        RejectedConsumerUnavailable = 13,
        RejectedEffectPreparation = 14,
        RejectedClockInvalid = 15,
        RejectedStaleSnapshot = 16,
        RejectedCorrelationRequired = 17,
        RejectedCorrelationConflict = 18,
        RejectedOverflow = 19,
        RejectedMalformedSnapshot = 20
    }

    public enum WorldStateDefinitionValidationStatus
    {
        Valid = 0,
        InvalidId = 1,
        DuplicateId = 2,
        AliasCollision = 3,
        InvalidEnvelope = 4,
        UnsupportedVersion = 5,
        InvalidDurationPolicy = 6,
        InvalidExclusivePolicy = 7,
        InvalidCancellationPolicy = 8,
        InvalidEffect = 9,
        MissingRequiredConsumer = 10,
        InvalidNotificationReference = 11,
        InvalidContentReference = 12
    }

    public enum WorldStateInstanceValidationStatus
    {
        Valid = 0,
        PreservedUnsupportedFuture = 1,
        Invalid = 2
    }

    public enum WorldEffectPreparationStatus
    {
        Prepared = 0,
        NoChange = 1,
        RejectedUnsupportedEffect = 2,
        RejectedInvalidParameter = 3,
        RejectedDomainUnavailable = 4,
        RejectedMalformedDomain = 5,
        RejectedOverflow = 6,
        RejectedConflict = 7,
        RejectedDependencyUnavailable = 8
    }

    public enum WorldEffectApplyStatus
    {
        Applied = 0,
        RejectedInvalidPlan = 1,
        RejectedConsumerUnavailable = 2,
        RejectedStaleTarget = 3,
        RejectedApply = 4
    }

    public enum WorldStateLedgerResultStatus
    {
        None = 0,
        Committed = 1
    }

    public enum WorldStateStandaloneCommitStatus
    {
        AppliedCommitted = 0,
        AlreadyCommitted = 1,
        NoChange = 2,
        RejectedValidation = 3,
        RejectedStale = 4,
        PersistenceFailedPreviousPreserved = 5,
        NotificationFailedAfterCommit = 6
    }

    public enum WorldStatePersistenceStatus
    {
        Verified = 0,
        Failed = 1
    }

    public sealed class WorldStateDiagnostic
    {
        public WorldStateDiagnostic(
            WorldStateDiagnosticSeverity severity,
            string code,
            string subjectId,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public WorldStateDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    public sealed class WorldEffectParameter
    {
        public WorldEffectParameter(
            string name,
            WorldEffectParameterKind kind,
            long integerValue,
            double numberValue,
            bool booleanValue,
            string referenceValue)
        {
            Name = name ?? string.Empty;
            Kind = kind;
            IntegerValue = integerValue;
            NumberValue = numberValue;
            BooleanValue = booleanValue;
            ReferenceValue = referenceValue ?? string.Empty;
        }

        public string Name { get; }
        public WorldEffectParameterKind Kind { get; }
        public long IntegerValue { get; }
        public double NumberValue { get; }
        public bool BooleanValue { get; }
        public string ReferenceValue { get; }

        public static WorldEffectParameter Integer(string name, long value)
        {
            return new WorldEffectParameter(
                name,
                WorldEffectParameterKind.Integer,
                value,
                0d,
                false,
                string.Empty);
        }

        public static WorldEffectParameter Number(string name, double value)
        {
            return new WorldEffectParameter(
                name,
                WorldEffectParameterKind.Number,
                0L,
                value,
                false,
                string.Empty);
        }

        public static WorldEffectParameter Boolean(string name, bool value)
        {
            return new WorldEffectParameter(
                name,
                WorldEffectParameterKind.Boolean,
                0L,
                0d,
                value,
                string.Empty);
        }

        public static WorldEffectParameter Reference(string name, string value)
        {
            return new WorldEffectParameter(
                name,
                WorldEffectParameterKind.Reference,
                0L,
                0d,
                false,
                value);
        }
    }

    public sealed class WorldEffectDescriptor
    {
        public WorldEffectDescriptor(
            string effectId,
            int schemaVersion,
            string consumerId,
            WorldEffectOperation operation,
            IEnumerable<WorldEffectParameter> parameters,
            bool required,
            int applicationOrder,
            int removalOrder,
            string sourceRevision)
        {
            EffectId = effectId ?? string.Empty;
            SchemaVersion = schemaVersion;
            ConsumerId = consumerId ?? string.Empty;
            Operation = operation;
            Parameters = WorldStateCollections.Freeze(
                parameters,
                WorldStateTechnicalLimits.MaximumParametersPerEffect);
            Required = required;
            ApplicationOrder = applicationOrder;
            RemovalOrder = removalOrder;
            SourceRevision = sourceRevision ?? string.Empty;
        }

        public string EffectId { get; }
        public int SchemaVersion { get; }
        public string ConsumerId { get; }
        public WorldEffectOperation Operation { get; }
        public IReadOnlyList<WorldEffectParameter> Parameters { get; }
        public bool Required { get; }
        public int ApplicationOrder { get; }
        public int RemovalOrder { get; }
        public string SourceRevision { get; }
    }

    public sealed class WorldEventDurationPolicy
    {
        public WorldEventDurationPolicy(
            long minimumDurationSeconds,
            long maximumDurationSeconds,
            long defaultDurationSeconds,
            bool callerMayOverrideDuration)
        {
            MinimumDurationSeconds = minimumDurationSeconds;
            MaximumDurationSeconds = maximumDurationSeconds;
            DefaultDurationSeconds = defaultDurationSeconds;
            CallerMayOverrideDuration = callerMayOverrideDuration;
        }

        public long MinimumDurationSeconds { get; }
        public long MaximumDurationSeconds { get; }
        public long DefaultDurationSeconds { get; }
        public bool CallerMayOverrideDuration { get; }
    }

    public sealed class WorldEventDefinition
    {
        public WorldEventDefinition(
            string definitionId,
            int schemaVersion,
            string contentVersion,
            string sourceRevision,
            IEnumerable<string> legacyAliases,
            WorldEventCategory category,
            WorldEventScope scope,
            string exclusiveGroup,
            int priority,
            WorldEventDurationPolicy durationPolicy,
            WorldEventCancellationPolicy cancellationPolicy,
            WorldEventSupersessionPolicy supersessionPolicy,
            bool presentationOnly,
            IEnumerable<WorldEffectDescriptor> effectDescriptors,
            IEnumerable<string> requiredConsumerIds,
            IEnumerable<string> optionalConsumerIds,
            IEnumerable<string> allowedSourceSystemIds,
            string startNotificationDefinitionId,
            string endNotificationDefinitionId,
            string cancelNotificationDefinitionId,
            string contentReference)
        {
            DefinitionId = definitionId ?? string.Empty;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            LegacyAliases = WorldStateCollections.Freeze(
                legacyAliases,
                WorldStateTechnicalLimits.MaximumAliasesPerDefinition);
            Category = category;
            Scope = scope;
            ExclusiveGroup = exclusiveGroup ?? string.Empty;
            Priority = priority;
            DurationPolicy = durationPolicy;
            CancellationPolicy = cancellationPolicy;
            SupersessionPolicy = supersessionPolicy;
            PresentationOnly = presentationOnly;
            EffectDescriptors = WorldStateCollections.Freeze(
                effectDescriptors,
                WorldStateTechnicalLimits.MaximumEffectsPerDefinition);
            RequiredConsumerIds = WorldStateCollections.Freeze(
                requiredConsumerIds,
                WorldStateTechnicalLimits.MaximumConsumersPerDefinition);
            OptionalConsumerIds = WorldStateCollections.Freeze(
                optionalConsumerIds,
                WorldStateTechnicalLimits.MaximumConsumersPerDefinition);
            AllowedSourceSystemIds = WorldStateCollections.Freeze(
                allowedSourceSystemIds,
                WorldStateTechnicalLimits.MaximumSourceSystemsPerDefinition);
            StartNotificationDefinitionId = startNotificationDefinitionId ?? string.Empty;
            EndNotificationDefinitionId = endNotificationDefinitionId ?? string.Empty;
            CancelNotificationDefinitionId = cancelNotificationDefinitionId ?? string.Empty;
            ContentReference = contentReference ?? string.Empty;
        }

        public string DefinitionId { get; }
        public int SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public IReadOnlyList<string> LegacyAliases { get; }
        public WorldEventCategory Category { get; }
        public WorldEventScope Scope { get; }
        public string ExclusiveGroup { get; }
        public int Priority { get; }
        public WorldEventDurationPolicy DurationPolicy { get; }
        public WorldEventCancellationPolicy CancellationPolicy { get; }
        public WorldEventSupersessionPolicy SupersessionPolicy { get; }
        public bool PresentationOnly { get; }
        public IReadOnlyList<WorldEffectDescriptor> EffectDescriptors { get; }
        public IReadOnlyList<string> RequiredConsumerIds { get; }
        public IReadOnlyList<string> OptionalConsumerIds { get; }
        public IReadOnlyList<string> AllowedSourceSystemIds { get; }
        public string StartNotificationDefinitionId { get; }
        public string EndNotificationDefinitionId { get; }
        public string CancelNotificationDefinitionId { get; }
        public string ContentReference { get; }
    }

    public sealed class WorldResolvedEffectSummary
    {
        public WorldResolvedEffectSummary(
            string effectId,
            string consumerId,
            WorldEffectOperation operation,
            IEnumerable<WorldEffectParameter> parameters,
            string parameterHash,
            int consumerPlanSchemaVersion,
            bool required,
            int removalOrder)
        {
            EffectId = effectId ?? string.Empty;
            ConsumerId = consumerId ?? string.Empty;
            Operation = operation;
            Parameters = WorldStateCollections.Freeze(
                parameters,
                WorldStateTechnicalLimits.MaximumParametersPerEffect);
            ParameterHash = parameterHash ?? string.Empty;
            ConsumerPlanSchemaVersion = consumerPlanSchemaVersion;
            Required = required;
            RemovalOrder = removalOrder;
        }

        public string EffectId { get; }
        public string ConsumerId { get; }
        public WorldEffectOperation Operation { get; }
        public IReadOnlyList<WorldEffectParameter> Parameters { get; }
        public string ParameterHash { get; }
        public int ConsumerPlanSchemaVersion { get; }
        public bool Required { get; }
        public int RemovalOrder { get; }
    }

    public sealed class WorldEventInstance
    {
        public WorldEventInstance(
            string instanceId,
            string definitionId,
            int definitionSchemaVersion,
            string definitionContentVersion,
            string definitionSourceRevision,
            string correlationId,
            string operationId,
            string sourceSystemId,
            string exclusiveGroup,
            WorldEventInstanceState state,
            long scheduledAtUtcSeconds,
            long startedAtUtcSeconds,
            long expectedEndAtUtcSeconds,
            long completedAtUtcSeconds,
            WorldEventCompletionReason completionReason,
            long revision,
            IEnumerable<WorldResolvedEffectSummary> resolvedEffects,
            long committedEffectRevision)
        {
            InstanceId = instanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            DefinitionSchemaVersion = definitionSchemaVersion;
            DefinitionContentVersion = definitionContentVersion ?? string.Empty;
            DefinitionSourceRevision = definitionSourceRevision ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            ExclusiveGroup = exclusiveGroup ?? string.Empty;
            State = state;
            ScheduledAtUtcSeconds = scheduledAtUtcSeconds;
            StartedAtUtcSeconds = startedAtUtcSeconds;
            ExpectedEndAtUtcSeconds = expectedEndAtUtcSeconds;
            CompletedAtUtcSeconds = completedAtUtcSeconds;
            CompletionReason = completionReason;
            Revision = revision;
            ResolvedEffects = WorldStateCollections.Freeze(
                resolvedEffects,
                WorldStateTechnicalLimits.MaximumEffectsPerDefinition);
            CommittedEffectRevision = committedEffectRevision;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public int DefinitionSchemaVersion { get; }
        public string DefinitionContentVersion { get; }
        public string DefinitionSourceRevision { get; }
        public string CorrelationId { get; }
        public string OperationId { get; }
        public string SourceSystemId { get; }
        public string ExclusiveGroup { get; }
        public WorldEventInstanceState State { get; }
        public long ScheduledAtUtcSeconds { get; }
        public long StartedAtUtcSeconds { get; }
        public long ExpectedEndAtUtcSeconds { get; }
        public long CompletedAtUtcSeconds { get; }
        public WorldEventCompletionReason CompletionReason { get; }
        public long Revision { get; }
        public IReadOnlyList<WorldResolvedEffectSummary> ResolvedEffects { get; }
        public long CommittedEffectRevision { get; }
    }

    public sealed class WorldStateOperationReceipt
    {
        public WorldStateOperationReceipt(
            string operationId,
            string correlationId,
            string semanticHash,
            WorldStateTransitionKind transitionKind,
            string instanceId,
            long committedRevision,
            WorldEventInstance resultingInstance)
        {
            OperationId = operationId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            TransitionKind = transitionKind;
            InstanceId = instanceId ?? string.Empty;
            CommittedRevision = committedRevision;
            ResultingInstance = resultingInstance;
        }

        public string OperationId { get; }
        public string CorrelationId { get; }
        public string SemanticHash { get; }
        public WorldStateTransitionKind TransitionKind { get; }
        public string InstanceId { get; }
        public long CommittedRevision { get; }
        public WorldEventInstance ResultingInstance { get; }
    }

    public sealed class WorldStateSnapshot
    {
        public WorldStateSnapshot(
            WorldStateSnapshotStatus status,
            long snapshotRevision,
            string policyRevision,
            string catalogRevision,
            IEnumerable<WorldEventInstance> activeInstances,
            IEnumerable<WorldEventInstance> completedHistory,
            long committedEffectRevision,
            bool profileWritable,
            long lastTrustedUtcSeconds,
            IEnumerable<WorldStateOperationReceipt> operationReceipts,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            SnapshotRevision = snapshotRevision;
            PolicyRevision = policyRevision ?? string.Empty;
            CatalogRevision = catalogRevision ?? string.Empty;
            ActiveInstances = WorldStateCollections.Freeze(
                activeInstances,
                WorldStateTechnicalLimits.MaximumActiveInstances);
            CompletedHistory = WorldStateCollections.Freeze(
                completedHistory,
                WorldStateTechnicalLimits.MaximumCompletedHistory + 1);
            CommittedEffectRevision = committedEffectRevision;
            ProfileWritable = profileWritable;
            LastTrustedUtcSeconds = lastTrustedUtcSeconds;
            OperationReceipts = WorldStateCollections.Freeze(
                operationReceipts,
                WorldStateTechnicalLimits.MaximumOperationReceipts + 1);
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public WorldStateSnapshotStatus Status { get; }
        public long SnapshotRevision { get; }
        public string PolicyRevision { get; }
        public string CatalogRevision { get; }
        public IReadOnlyList<WorldEventInstance> ActiveInstances { get; }
        public WorldEventInstance ActiveInstance =>
            ActiveInstances.Count == 1 ? ActiveInstances[0] : null;
        public IReadOnlyList<WorldEventInstance> CompletedHistory { get; }
        public long CommittedEffectRevision { get; }
        public bool ProfileWritable { get; }
        public long LastTrustedUtcSeconds { get; }
        public IReadOnlyList<WorldStateOperationReceipt> OperationReceipts { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
    }

    public sealed class WorldStateStartRequest
    {
        public WorldStateStartRequest(
            string definitionId,
            string instanceId,
            string correlationId,
            string operationId,
            string sourceSystemId,
            long requestedStartAtUtcSeconds,
            long? requestedDurationSeconds,
            long expectedSnapshotRevision)
        {
            DefinitionId = definitionId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            RequestedStartAtUtcSeconds = requestedStartAtUtcSeconds;
            RequestedDurationSeconds = requestedDurationSeconds;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
        }

        public string DefinitionId { get; }
        public string InstanceId { get; }
        public string CorrelationId { get; }
        public string OperationId { get; }
        public string SourceSystemId { get; }
        public long RequestedStartAtUtcSeconds { get; }
        public long? RequestedDurationSeconds { get; }
        public long ExpectedSnapshotRevision { get; }
    }

    public sealed class WorldStateEndRequest
    {
        public WorldStateEndRequest(
            string instanceId,
            string correlationId,
            string operationId,
            string sourceSystemId,
            long observedNowUtcSeconds,
            long expectedSnapshotRevision)
        {
            InstanceId = instanceId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            ObservedNowUtcSeconds = observedNowUtcSeconds;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
        }

        public string InstanceId { get; }
        public string CorrelationId { get; }
        public string OperationId { get; }
        public string SourceSystemId { get; }
        public long ObservedNowUtcSeconds { get; }
        public long ExpectedSnapshotRevision { get; }
    }

    public sealed class WorldStateCancelRequest
    {
        public WorldStateCancelRequest(
            string instanceId,
            string correlationId,
            string operationId,
            string sourceSystemId,
            WorldEventCompletionReason completionReason,
            long requestedAtUtcSeconds,
            long expectedSnapshotRevision)
        {
            InstanceId = instanceId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            OperationId = operationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            CompletionReason = completionReason;
            RequestedAtUtcSeconds = requestedAtUtcSeconds;
            ExpectedSnapshotRevision = expectedSnapshotRevision;
        }

        public string InstanceId { get; }
        public string CorrelationId { get; }
        public string OperationId { get; }
        public string SourceSystemId { get; }
        public WorldEventCompletionReason CompletionReason { get; }
        public long RequestedAtUtcSeconds { get; }
        public long ExpectedSnapshotRevision { get; }
    }

    public sealed class WorldEffectPlan
    {
        public WorldEffectPlan(
            string consumerId,
            string effectId,
            int consumerPlanVersion,
            WorldStateTransitionKind transitionKind,
            string expectedConsumerRevision,
            string parameterHash,
            IEnumerable<WorldEffectParameter> parameters,
            bool required,
            int order)
        {
            ConsumerId = consumerId ?? string.Empty;
            EffectId = effectId ?? string.Empty;
            ConsumerPlanVersion = consumerPlanVersion;
            TransitionKind = transitionKind;
            ExpectedConsumerRevision = expectedConsumerRevision ?? string.Empty;
            ParameterHash = parameterHash ?? string.Empty;
            Parameters = WorldStateCollections.Freeze(
                parameters,
                WorldStateTechnicalLimits.MaximumParametersPerEffect);
            Required = required;
            Order = order;
        }

        public string ConsumerId { get; }
        public string EffectId { get; }
        public int ConsumerPlanVersion { get; }
        public WorldStateTransitionKind TransitionKind { get; }
        public string ExpectedConsumerRevision { get; }
        public string ParameterHash { get; }
        public IReadOnlyList<WorldEffectParameter> Parameters { get; }
        public bool Required { get; }
        public int Order { get; }
    }

    public sealed class WorldEffectPreparationResult
    {
        public WorldEffectPreparationResult(
            WorldEffectPreparationStatus status,
            WorldEffectPlan plan,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public WorldEffectPreparationStatus Status { get; }
        public WorldEffectPlan Plan { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
    }

    public sealed class WorldStateNotificationIntent
    {
        public WorldStateNotificationIntent(
            string definitionId,
            string correlationId,
            string instanceId)
        {
            DefinitionId = definitionId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
        }

        public string DefinitionId { get; }
        public string CorrelationId { get; }
        public string InstanceId { get; }
    }

    public sealed class WorldStateLedgerEntry
    {
        public WorldStateLedgerEntry(
            string operationId,
            string correlationId,
            string instanceId,
            string definitionId,
            WorldStateTransitionKind transitionKind,
            long previousRevision,
            long newRevision,
            WorldStateLedgerResultStatus resultStatus,
            long committedAtUtcSeconds,
            string semanticHash)
        {
            OperationId = operationId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            InstanceId = instanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            TransitionKind = transitionKind;
            PreviousRevision = previousRevision;
            NewRevision = newRevision;
            ResultStatus = resultStatus;
            CommittedAtUtcSeconds = committedAtUtcSeconds;
            SemanticHash = semanticHash ?? string.Empty;
        }

        public string OperationId { get; }
        public string CorrelationId { get; }
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public WorldStateTransitionKind TransitionKind { get; }
        public long PreviousRevision { get; }
        public long NewRevision { get; }
        public WorldStateLedgerResultStatus ResultStatus { get; }
        public long CommittedAtUtcSeconds { get; }
        public string SemanticHash { get; }
    }

    public sealed class WorldStateTransitionEvent
    {
        public WorldStateTransitionEvent(
            string instanceId,
            string definitionId,
            WorldStateTransitionKind transitionKind,
            WorldEventInstanceState previousState,
            WorldEventInstanceState newState,
            long previousRevision,
            long newRevision,
            string operationId,
            string correlationId,
            string sourceSystemId,
            long committedAtUtcSeconds,
            long committedEffectRevision)
        {
            InstanceId = instanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            TransitionKind = transitionKind;
            PreviousState = previousState;
            NewState = newState;
            PreviousRevision = previousRevision;
            NewRevision = newRevision;
            OperationId = operationId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            CommittedAtUtcSeconds = committedAtUtcSeconds;
            CommittedEffectRevision = committedEffectRevision;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public WorldStateTransitionKind TransitionKind { get; }
        public WorldEventInstanceState PreviousState { get; }
        public WorldEventInstanceState NewState { get; }
        public long PreviousRevision { get; }
        public long NewRevision { get; }
        public string OperationId { get; }
        public string CorrelationId { get; }
        public string SourceSystemId { get; }
        public long CommittedAtUtcSeconds { get; }
        public long CommittedEffectRevision { get; }
    }

    public sealed class WorldStateTransitionPlan
    {
        public WorldStateTransitionPlan(
            string planId,
            WorldStateTransitionKind transitionKind,
            long previousSnapshotRevision,
            long expectedNewRevision,
            WorldEventInstance instanceBefore,
            WorldEventInstance instanceAfter,
            IEnumerable<WorldEffectPlan> preparedEffectPlans,
            string operationId,
            string correlationId,
            string sourceSystemId,
            IEnumerable<WorldStateNotificationIntent> notificationIntents,
            WorldStateLedgerEntry ledgerEntry,
            WorldStateTransitionEvent postCommitEvent,
            string policyRevision,
            string catalogRevision,
            string semanticHash,
            string planHash,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            PlanId = planId ?? string.Empty;
            TransitionKind = transitionKind;
            PreviousSnapshotRevision = previousSnapshotRevision;
            ExpectedNewRevision = expectedNewRevision;
            InstanceBefore = instanceBefore;
            InstanceAfter = instanceAfter;
            PreparedEffectPlans = WorldStateCollections.Freeze(
                preparedEffectPlans,
                WorldStateTechnicalLimits.MaximumEffectsPerDefinition);
            OperationId = operationId ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            SourceSystemId = sourceSystemId ?? string.Empty;
            NotificationIntents = WorldStateCollections.Freeze(
                notificationIntents,
                WorldStateTechnicalLimits.MaximumNotificationIntents);
            LedgerEntry = ledgerEntry;
            PostCommitEvent = postCommitEvent;
            PolicyRevision = policyRevision ?? string.Empty;
            CatalogRevision = catalogRevision ?? string.Empty;
            SemanticHash = semanticHash ?? string.Empty;
            PlanHash = planHash ?? string.Empty;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public string PlanId { get; }
        public WorldStateTransitionKind TransitionKind { get; }
        public long PreviousSnapshotRevision { get; }
        public long ExpectedNewRevision { get; }
        public WorldEventInstance InstanceBefore { get; }
        public WorldEventInstance InstanceAfter { get; }
        public IReadOnlyList<WorldEffectPlan> PreparedEffectPlans { get; }
        public string OperationId { get; }
        public string CorrelationId { get; }
        public string SourceSystemId { get; }
        public IReadOnlyList<WorldStateNotificationIntent> NotificationIntents { get; }
        public WorldStateLedgerEntry LedgerEntry { get; }
        public WorldStateTransitionEvent PostCommitEvent { get; }
        public string PolicyRevision { get; }
        public string CatalogRevision { get; }
        public string SemanticHash { get; }
        public string PlanHash { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
    }

    public sealed class WorldStatePlanningResult
    {
        public WorldStatePlanningResult(
            WorldStatePlanningStatus status,
            WorldStateTransitionPlan plan,
            WorldStateOperationReceipt existingReceipt,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            ExistingReceipt = existingReceipt;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);

            if ((status == WorldStatePlanningStatus.Prepared) != (plan != null))
            {
                throw new ArgumentException(
                    "Only a prepared result may expose a transition plan.");
            }

            if ((status == WorldStatePlanningStatus.AlreadyCommitted) !=
                (existingReceipt != null))
            {
                throw new ArgumentException(
                    "Only an exact replay may expose an existing receipt.");
            }
        }

        public WorldStatePlanningStatus Status { get; }
        public WorldStateTransitionPlan Plan { get; }
        public WorldStateOperationReceipt ExistingReceipt { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
    }

    public sealed class WorldStatePersistenceResult
    {
        public WorldStatePersistenceResult(
            WorldStatePersistenceStatus status,
            AL.Data.Runtime.WorldStatePersistentState persisted,
            string diagnostic)
        {
            Status = status;
            Persisted = persisted;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public WorldStatePersistenceStatus Status { get; }
        public AL.Data.Runtime.WorldStatePersistentState Persisted { get; }
        public string Diagnostic { get; }
        public bool IsVerified => Status == WorldStatePersistenceStatus.Verified;
    }

    public sealed class WorldStateStandaloneCommitResult
    {
        public WorldStateStandaloneCommitResult(
            WorldStateStandaloneCommitStatus status,
            WorldStateTransitionPlan plan,
            WorldStateSnapshot snapshotBefore,
            WorldStateSnapshot snapshotAfter,
            WorldStateTransitionEvent committedEvent,
            int persistAttemptCount,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            Plan = plan;
            SnapshotBefore = snapshotBefore;
            SnapshotAfter = snapshotAfter;
            CommittedEvent = committedEvent;
            PersistAttemptCount = persistAttemptCount;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public WorldStateStandaloneCommitStatus Status { get; }
        public WorldStateTransitionPlan Plan { get; }
        public WorldStateSnapshot SnapshotBefore { get; }
        public WorldStateSnapshot SnapshotAfter { get; }
        public WorldStateTransitionEvent CommittedEvent { get; }
        public int PersistAttemptCount { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
    }

    public sealed class WorldStateDefinitionValidationResult
    {
        public WorldStateDefinitionValidationResult(
            WorldStateDefinitionValidationStatus status,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public WorldStateDefinitionValidationStatus Status { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
        public bool IsValid => Status == WorldStateDefinitionValidationStatus.Valid;
    }

    public sealed class WorldStateInstanceValidationResult
    {
        public WorldStateInstanceValidationResult(
            WorldStateInstanceValidationStatus status,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public WorldStateInstanceValidationStatus Status { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
        public bool IsValid => Status == WorldStateInstanceValidationStatus.Valid;
    }

    public interface IWorldStateMutationTarget
    {
        long WorldStateRevision { get; }
        long EffectRevision { get; }
    }

    public sealed class WorldEffectApplyResult
    {
        public WorldEffectApplyResult(
            WorldEffectApplyStatus status,
            IWorldStateMutationTarget candidate,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            Candidate = candidate;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public WorldEffectApplyStatus Status { get; }
        public IWorldStateMutationTarget Candidate { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
    }

    public sealed class WorldStateEffectExecutionResult
    {
        public WorldStateEffectExecutionResult(
            WorldEffectApplyStatus status,
            IWorldStateMutationTarget candidate,
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            Status = status;
            Candidate = candidate;
            Diagnostics = WorldStateCollections.Freeze(
                diagnostics,
                WorldStateTechnicalLimits.MaximumDiagnostics);
        }

        public WorldEffectApplyStatus Status { get; }
        public IWorldStateMutationTarget Candidate { get; }
        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
    }

    public static class WorldStateTechnicalLimits
    {
        public const int CurrentDefinitionSchemaVersion = 1;
        public const int CurrentEffectSchemaVersion = 1;
        public const int MaximumIdentifierUtf8Bytes = 128;
        public const int MaximumVersionUtf8Bytes = 96;
        public const int MaximumReferenceUtf8Bytes = 192;
        public const int MaximumDefinitions = 256;
        public const int MaximumAliasesPerDefinition = 32;
        public const int MaximumEffectsPerDefinition = 32;
        public const int MaximumParametersPerEffect = 32;
        public const int MaximumConsumersPerDefinition = 32;
        public const int MaximumSourceSystemsPerDefinition = 32;
        public const int MaximumActiveInstances = 2;
        public const int MaximumCompletedHistory = 50;
        public const int MaximumOperationReceipts = 256;
        public const int MaximumNotificationIntents = 3;
        public const int MaximumDiagnostics = 256;
        public const int MaximumPriority = 1000;
        public const long MaximumDurationSeconds = int.MaxValue;
        public const string ExclusiveGroupGlobalPrimary = "global_primary";
        public const string ApprovedRecoverySourceSystemId =
            "al_world_source_recovery";
    }

    internal static class WorldStateCollections
    {
        public static IReadOnlyList<T> Freeze<T>(
            IEnumerable<T> source,
            int maximumCount)
        {
            if (source == null)
            {
                return Array.Empty<T>();
            }

            var copy = new List<T>(Math.Min(maximumCount + 1, 64));
            using (IEnumerator<T> enumerator = source.GetEnumerator())
            {
                while (copy.Count <= maximumCount && enumerator.MoveNext())
                {
                    copy.Add(enumerator.Current);
                }
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }
}
