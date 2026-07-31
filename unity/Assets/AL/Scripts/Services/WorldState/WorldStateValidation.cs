using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AL.Core.Interfaces.WorldState;

namespace AL.Services.WorldState
{
    public sealed class WorldEffectConsumerRegistry
    {
        private readonly IReadOnlyDictionary<string, IWorldEffectConsumer> _consumers;

        public WorldEffectConsumerRegistry(
            IEnumerable<IWorldEffectConsumer> consumers)
        {
            var diagnostics = new List<WorldStateDiagnostic>();
            var map = new Dictionary<string, IWorldEffectConsumer>(
                StringComparer.Ordinal);
            int count = 0;

            if (consumers != null)
            {
                foreach (IWorldEffectConsumer consumer in consumers)
                {
                    count++;
                    if (count > WorldStateTechnicalLimits.MaximumConsumersPerDefinition)
                    {
                        diagnostics.Add(WorldStateValidator.Error(
                            "AL-WST-CONSUMER-LIMIT",
                            string.Empty,
                            "The consumer registry exceeds the bounded limit."));
                        break;
                    }

                    if (consumer == null ||
                        !WorldStateValidator.IsConsumerId(consumer.ConsumerId))
                    {
                        diagnostics.Add(WorldStateValidator.Error(
                            "AL-WST-CONSUMER-ID",
                            consumer?.ConsumerId,
                            "A registered consumer has an invalid identity."));
                        continue;
                    }

                    if (!map.TryAdd(consumer.ConsumerId, consumer))
                    {
                        diagnostics.Add(WorldStateValidator.Error(
                            "AL-WST-CONSUMER-DUPLICATE",
                            consumer.ConsumerId,
                            "Duplicate consumer identities are not allowed."));
                    }
                }
            }

            _consumers = new ReadOnlyDictionary<string, IWorldEffectConsumer>(map);
            Diagnostics = new ReadOnlyCollection<WorldStateDiagnostic>(
                diagnostics
                    .OrderBy(item => item.Code, StringComparer.Ordinal)
                    .ThenBy(item => item.SubjectId, StringComparer.Ordinal)
                    .ToList());
        }

        public IReadOnlyList<WorldStateDiagnostic> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0;

        public bool TryGet(
            string consumerId,
            out IWorldEffectConsumer consumer)
        {
            consumer = null;
            return IsValid &&
                   !string.IsNullOrEmpty(consumerId) &&
                   _consumers.TryGetValue(consumerId, out consumer);
        }

        public bool TryGetAvailable(
            string consumerId,
            out IWorldEffectConsumer consumer)
        {
            if (!TryGet(consumerId, out consumer))
            {
                return false;
            }

            try
            {
                return consumer.IsAvailable;
            }
            catch
            {
                consumer = null;
                return false;
            }
        }
    }

    public static class WorldStateValidator
    {
        private static readonly Regex DefinitionIdPattern = new Regex(
            @"\Aal_world_event_[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex EffectIdPattern = new Regex(
            @"\Aal_world_effect_[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex ConsumerIdPattern = new Regex(
            @"\Aal_world_consumer_[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex SourceSystemIdPattern = new Regex(
            @"\Aal_world_source_[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex NotificationIdPattern = new Regex(
            @"\Aal_notify_[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex ParameterNamePattern = new Regex(
            @"\A[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex ContentReferencePattern = new Regex(
            @"\A[a-z][a-z0-9]*(?:[._][a-z0-9]+)+\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex VersionPattern = new Regex(
            @"\A[A-Za-z0-9][A-Za-z0-9._-]*\z",
            RegexOptions.CultureInvariant);

        private static readonly Regex LegacyAliasPattern = new Regex(
            @"\A[A-Za-z][A-Za-z0-9_.-]*\z",
            RegexOptions.CultureInvariant);

        public static WorldStateDefinitionValidationResult ValidateDefinition(
            WorldEventDefinition definition,
            WorldEffectConsumerRegistry consumers)
        {
            return ValidateDefinitions(
                definition == null
                    ? Array.Empty<WorldEventDefinition>()
                    : new[] { definition },
                consumers,
                requireExactlyOne: true);
        }

        public static WorldStateDefinitionValidationResult ValidateDefinitions(
            IEnumerable<WorldEventDefinition> definitions,
            WorldEffectConsumerRegistry consumers)
        {
            return ValidateDefinitions(definitions, consumers, requireExactlyOne: false);
        }

        private static WorldStateDefinitionValidationResult ValidateDefinitions(
            IEnumerable<WorldEventDefinition> definitions,
            WorldEffectConsumerRegistry consumers,
            bool requireExactlyOne)
        {
            List<WorldEventDefinition> rows = CopyBounded(
                definitions,
                WorldStateTechnicalLimits.MaximumDefinitions,
                out bool definitionLimitExceeded);
            var diagnostics = new List<WorldStateDiagnostic>();
            WorldStateDefinitionValidationStatus status =
                WorldStateDefinitionValidationStatus.Valid;

            if (definitionLimitExceeded ||
                rows.Count == 0 ||
                (requireExactlyOne && rows.Count != 1) ||
                rows.Any(row => row == null))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidEnvelope,
                    "AL-WST-DEFINITION-ENVELOPE",
                    string.Empty,
                    "The definition set is empty, oversized, or contains a null row.");
            }

            foreach (IGrouping<string, WorldEventDefinition> duplicate in rows
                         .Where(row => row != null)
                         .GroupBy(row => row.DefinitionId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.DuplicateId,
                    "AL-WST-ID-DUPLICATE",
                    duplicate.Key,
                    "Definition identities must be unique ordinal values.");
            }

            foreach (WorldEventDefinition definition in rows
                         .Where(row => row != null)
                         .OrderBy(row => row.DefinitionId, StringComparer.Ordinal))
            {
                ValidateDefinitionRow(definition, consumers, diagnostics, ref status);
            }

            ValidateAliasSet(rows, diagnostics, ref status);

            return new WorldStateDefinitionValidationResult(
                status,
                OrderDiagnostics(diagnostics));
        }

        private static void ValidateDefinitionRow(
            WorldEventDefinition definition,
            WorldEffectConsumerRegistry consumers,
            ICollection<WorldStateDiagnostic> diagnostics,
            ref WorldStateDefinitionValidationStatus status)
        {
            string subject = definition.DefinitionId;
            if (!IsDefinitionId(definition.DefinitionId))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidId,
                    "AL-WST-ID",
                    subject,
                    "Definition ID does not match the v1 world-event grammar.");
            }

            if (definition.SchemaVersion !=
                    WorldStateTechnicalLimits.CurrentDefinitionSchemaVersion ||
                !IsBoundedVersion(definition.ContentVersion) ||
                !IsBoundedVersion(definition.SourceRevision))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.UnsupportedVersion,
                    "AL-WST-DEFINITION-VERSION",
                    subject,
                    "Definition schema/content/source version is unavailable.");
            }

            if (!Enum.IsDefined(typeof(WorldEventCategory), definition.Category) ||
                definition.Scope != WorldEventScope.Global ||
                definition.Priority < 0 ||
                definition.Priority > WorldStateTechnicalLimits.MaximumPriority ||
                definition.AllowedSourceSystemIds.Count == 0 ||
                definition.AllowedSourceSystemIds.Count >
                    WorldStateTechnicalLimits.MaximumSourceSystemsPerDefinition ||
                HasNullOrDuplicate(definition.AllowedSourceSystemIds) ||
                definition.AllowedSourceSystemIds.Any(id => !IsSourceSystemId(id)))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidEnvelope,
                    "AL-WST-DEFINITION",
                    subject,
                    "Definition category, scope, priority, or source allowlist is invalid.");
            }

            if (!string.Equals(
                    definition.ExclusiveGroup,
                    WorldStateTechnicalLimits.ExclusiveGroupGlobalPrimary,
                    StringComparison.Ordinal) ||
                definition.SupersessionPolicy !=
                    WorldEventSupersessionPolicy.RejectWhileExclusiveInstanceActive)
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidExclusivePolicy,
                    "AL-WST-EXCLUSIVE",
                    subject,
                    "Only the v1 global-primary reject policy is supported.");
            }

            WorldEventDurationPolicy duration = definition.DurationPolicy;
            if (duration == null ||
                duration.MinimumDurationSeconds <= 0L ||
                duration.MaximumDurationSeconds < duration.MinimumDurationSeconds ||
                duration.MaximumDurationSeconds >
                    WorldStateTechnicalLimits.MaximumDurationSeconds ||
                duration.DefaultDurationSeconds < duration.MinimumDurationSeconds ||
                duration.DefaultDurationSeconds > duration.MaximumDurationSeconds)
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidDurationPolicy,
                    "AL-WST-DURATION",
                    subject,
                    "Definition duration policy is not positive, ordered, and bounded.");
            }

            if (!Enum.IsDefined(
                    typeof(WorldEventCancellationPolicy),
                    definition.CancellationPolicy))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidCancellationPolicy,
                    "AL-WST-CANCELLATION",
                    subject,
                    "Definition cancellation policy is undefined.");
            }

            ValidateEffects(definition, consumers, diagnostics, ref status);

            if (!IsNotificationId(definition.StartNotificationDefinitionId) ||
                !IsNotificationId(definition.EndNotificationDefinitionId) ||
                (definition.CancellationPolicy !=
                    WorldEventCancellationPolicy.NotCancellable &&
                 !IsNotificationId(definition.CancelNotificationDefinitionId)) ||
                (definition.CancellationPolicy ==
                    WorldEventCancellationPolicy.NotCancellable &&
                 !string.IsNullOrEmpty(definition.CancelNotificationDefinitionId) &&
                 !IsNotificationId(definition.CancelNotificationDefinitionId)))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidNotificationReference,
                    "AL-WST-NOTIFICATION",
                    subject,
                    "Definition notification references are missing or malformed.");
            }

            if (!IsContentReference(definition.ContentReference))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidContentReference,
                    "AL-WST-CONTENT",
                    subject,
                    "Definition content reference is missing or malformed.");
            }
        }

        private static void ValidateAliasSet(
            IEnumerable<WorldEventDefinition> definitions,
            ICollection<WorldStateDiagnostic> diagnostics,
            ref WorldStateDefinitionValidationStatus status)
        {
            List<WorldEventDefinition> rows = definitions
                .Where(row => row != null)
                .ToList();
            var definitionIds = new HashSet<string>(
                rows.Select(row => row.DefinitionId),
                StringComparer.OrdinalIgnoreCase);
            var aliases = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (WorldEventDefinition definition in rows
                         .OrderBy(row => row.DefinitionId, StringComparer.Ordinal))
            {
                if (definition.LegacyAliases.Count >
                    WorldStateTechnicalLimits.MaximumAliasesPerDefinition)
                {
                    AddAliasError(
                        diagnostics,
                        ref status,
                        definition.DefinitionId,
                        "Definition alias list exceeds the bounded limit.");
                }

                foreach (string alias in definition.LegacyAliases
                             .OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (!IsLegacyAlias(alias) ||
                        definitionIds.Contains(alias) ||
                        !aliases.TryAdd(alias, definition.DefinitionId))
                    {
                        AddAliasError(
                            diagnostics,
                            ref status,
                            alias,
                            "Alias is invalid, shadows a definition, or collides.");
                    }
                }
            }
        }

        private static void ValidateEffects(
            WorldEventDefinition definition,
            WorldEffectConsumerRegistry consumers,
            ICollection<WorldStateDiagnostic> diagnostics,
            ref WorldStateDefinitionValidationStatus status)
        {
            IReadOnlyList<WorldEffectDescriptor> effects = definition.EffectDescriptors;
            if (effects.Count > WorldStateTechnicalLimits.MaximumEffectsPerDefinition ||
                effects.Any(effect => effect == null) ||
                HasDuplicate(
                    effects.Where(effect => effect != null).Select(effect => effect.EffectId)) ||
                HasDuplicate(
                    effects.Where(effect => effect != null)
                        .Select(effect => effect.ApplicationOrder.ToString(CultureInfo.InvariantCulture))) ||
                HasDuplicate(
                    effects.Where(effect => effect != null)
                        .Select(effect => effect.RemovalOrder.ToString(CultureInfo.InvariantCulture))) ||
                (!definition.PresentationOnly && effects.Count == 0))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidEffect,
                    "AL-WST-EFFECT",
                    definition.DefinitionId,
                    "Effects are missing, duplicated, oversized, or unordered.");
            }

            if (definition.RequiredConsumerIds.Count >
                    WorldStateTechnicalLimits.MaximumConsumersPerDefinition ||
                definition.OptionalConsumerIds.Count >
                    WorldStateTechnicalLimits.MaximumConsumersPerDefinition ||
                HasNullOrDuplicate(definition.RequiredConsumerIds) ||
                HasNullOrDuplicate(definition.OptionalConsumerIds) ||
                definition.RequiredConsumerIds.Intersect(
                    definition.OptionalConsumerIds,
                    StringComparer.Ordinal).Any() ||
                definition.RequiredConsumerIds.Any(id => !IsConsumerId(id)) ||
                definition.OptionalConsumerIds.Any(id => !IsConsumerId(id)))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidEffect,
                    "AL-WST-CONSUMER-POLICY",
                    definition.DefinitionId,
                    "Required/optional consumer policy is invalid.");
            }

            var requiredFromEffects = new HashSet<string>(
                effects
                    .Where(effect => effect != null && effect.Required)
                    .Select(effect => effect.ConsumerId),
                StringComparer.Ordinal);
            var optionalFromEffects = new HashSet<string>(
                effects
                    .Where(effect => effect != null && !effect.Required)
                    .Select(effect => effect.ConsumerId),
                StringComparer.Ordinal);

            if (!requiredFromEffects.SetEquals(definition.RequiredConsumerIds) ||
                !optionalFromEffects.SetEquals(definition.OptionalConsumerIds))
            {
                AddDefinitionError(
                    diagnostics,
                    ref status,
                    WorldStateDefinitionValidationStatus.InvalidEffect,
                    "AL-WST-CONSUMER-MAPPING",
                    definition.DefinitionId,
                    "Descriptor required flags do not match consumer policy.");
            }

            foreach (WorldEffectDescriptor effect in effects
                         .Where(row => row != null)
                         .OrderBy(row => row.ApplicationOrder)
                         .ThenBy(row => row.EffectId, StringComparer.Ordinal))
            {
                if (!IsEffectId(effect.EffectId) ||
                    effect.SchemaVersion !=
                        WorldStateTechnicalLimits.CurrentEffectSchemaVersion ||
                    !IsConsumerId(effect.ConsumerId) ||
                    !Enum.IsDefined(typeof(WorldEffectOperation), effect.Operation) ||
                    effect.ApplicationOrder < 0 ||
                    effect.RemovalOrder < 0 ||
                    !IsBoundedVersion(effect.SourceRevision) ||
                    !AreValidParameters(effect.Parameters))
                {
                    AddDefinitionError(
                        diagnostics,
                        ref status,
                        WorldStateDefinitionValidationStatus.InvalidEffect,
                        "AL-WST-EFFECT",
                        effect.EffectId,
                        "Effect identity, schema, ordering, parameters, or source is invalid.");
                }

                if (effect.Required &&
                    (consumers == null ||
                     !consumers.TryGetAvailable(
                         effect.ConsumerId,
                         out IWorldEffectConsumer _)))
                {
                    AddDefinitionError(
                        diagnostics,
                        ref status,
                        WorldStateDefinitionValidationStatus.MissingRequiredConsumer,
                        "AL-WST-CONSUMER",
                        effect.ConsumerId,
                        "A required effect consumer is unavailable.");
                }
            }
        }

        public static WorldStateInstanceValidationResult ValidateInstance(
            WorldEventInstance instance)
        {
            var diagnostics = new List<WorldStateDiagnostic>();
            if (instance == null)
            {
                diagnostics.Add(Error(
                    "AL-WST-INSTANCE-NULL",
                    string.Empty,
                    "World-event instance is null."));
                return new WorldStateInstanceValidationResult(
                    WorldStateInstanceValidationStatus.Invalid,
                    diagnostics);
            }

            if (instance.DefinitionSchemaVersion >
                    WorldStateTechnicalLimits.CurrentDefinitionSchemaVersion ||
                instance.State == WorldEventInstanceState.Scheduled ||
                instance.State == WorldEventInstanceState.Superseded)
            {
                diagnostics.Add(new WorldStateDiagnostic(
                    WorldStateDiagnosticSeverity.Warning,
                    "AL-WST-INSTANCE-FUTURE",
                    instance.InstanceId,
                    "Unsupported future instance is preserved read-only."));
                return new WorldStateInstanceValidationResult(
                    WorldStateInstanceValidationStatus.PreservedUnsupportedFuture,
                    diagnostics);
            }

            if (!IsOpaqueId(instance.InstanceId) ||
                !IsDefinitionId(instance.DefinitionId) ||
                instance.DefinitionSchemaVersion !=
                    WorldStateTechnicalLimits.CurrentDefinitionSchemaVersion ||
                !IsBoundedVersion(instance.DefinitionContentVersion) ||
                !IsBoundedVersion(instance.DefinitionSourceRevision) ||
                !IsOpaqueId(instance.CorrelationId) ||
                !IsOpaqueId(instance.OperationId) ||
                !IsSourceSystemId(instance.SourceSystemId) ||
                !string.Equals(
                    instance.ExclusiveGroup,
                    WorldStateTechnicalLimits.ExclusiveGroupGlobalPrimary,
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(WorldEventInstanceState), instance.State) ||
                instance.ScheduledAtUtcSeconds <= 0L ||
                instance.StartedAtUtcSeconds <= 0L ||
                instance.ExpectedEndAtUtcSeconds < instance.StartedAtUtcSeconds ||
                instance.Revision <= 0L ||
                instance.CommittedEffectRevision < 0L ||
                !AreValidResolvedEffects(instance.ResolvedEffects))
            {
                diagnostics.Add(Error(
                    "AL-WST-INSTANCE",
                    instance.InstanceId,
                    "Instance identity, version, timestamps, revision, or effects are invalid."));
            }

            bool active = instance.State == WorldEventInstanceState.Active;
            bool terminal = instance.State == WorldEventInstanceState.Ended ||
                            instance.State == WorldEventInstanceState.Cancelled ||
                            instance.State == WorldEventInstanceState.Failed;
            if ((active &&
                 (instance.CompletedAtUtcSeconds != 0L ||
                  instance.CompletionReason != WorldEventCompletionReason.None)) ||
                (terminal &&
                 (instance.CompletedAtUtcSeconds < instance.StartedAtUtcSeconds ||
                  instance.CompletionReason == WorldEventCompletionReason.None)) ||
                (!active && !terminal))
            {
                diagnostics.Add(Error(
                    "AL-WST-INSTANCE-STATE",
                    instance.InstanceId,
                    "Instance state is inconsistent with completion fields."));
            }

            return new WorldStateInstanceValidationResult(
                diagnostics.Count == 0
                    ? WorldStateInstanceValidationStatus.Valid
                    : WorldStateInstanceValidationStatus.Invalid,
                OrderDiagnostics(diagnostics));
        }

        public static WorldStateInstanceValidationResult ValidateSnapshot(
            WorldStateSnapshot snapshot)
        {
            var diagnostics = new List<WorldStateDiagnostic>();
            bool preservedFuture = false;
            if (snapshot == null)
            {
                diagnostics.Add(Error(
                    "AL-WST-SNAPSHOT-NULL",
                    string.Empty,
                    "World-state snapshot is null."));
                return new WorldStateInstanceValidationResult(
                    WorldStateInstanceValidationStatus.Invalid,
                    diagnostics);
            }

            if (!Enum.IsDefined(typeof(WorldStateSnapshotStatus), snapshot.Status) ||
                snapshot.SnapshotRevision < 0L ||
                !IsBoundedVersion(snapshot.PolicyRevision) ||
                !IsBoundedVersion(snapshot.CatalogRevision) ||
                snapshot.CommittedEffectRevision < 0L ||
                snapshot.LastTrustedUtcSeconds < 0L ||
                snapshot.ActiveInstances.Count > 1 ||
                snapshot.CompletedHistory.Count >
                    WorldStateTechnicalLimits.MaximumCompletedHistory ||
                snapshot.OperationReceipts.Count >
                    WorldStateTechnicalLimits.MaximumOperationReceipts)
            {
                diagnostics.Add(Error(
                    "AL-WST-SNAPSHOT",
                    string.Empty,
                    "Snapshot revision, count, or status is invalid."));
            }

            bool statusRequiresActive =
                snapshot.Status == WorldStateSnapshotStatus.AvailableActive;
            bool statusRequiresNone =
                snapshot.Status == WorldStateSnapshotStatus.AvailableNoActiveEvent;
            if ((statusRequiresActive && snapshot.ActiveInstances.Count != 1) ||
                (statusRequiresNone && snapshot.ActiveInstances.Count != 0) ||
                (snapshot.Status == WorldStateSnapshotStatus.AvailableReadOnly &&
                 snapshot.ProfileWritable))
            {
                diagnostics.Add(Error(
                    "AL-WST-SNAPSHOT-ACTIVE",
                    string.Empty,
                    "Snapshot status does not match active-instance cardinality."));
            }

            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldEventInstance instance in snapshot.ActiveInstances
                         .Concat(snapshot.CompletedHistory))
            {
                WorldStateInstanceValidationResult validation =
                    ValidateInstance(instance);
                diagnostics.AddRange(validation.Diagnostics);
                preservedFuture |= validation.Status ==
                                   WorldStateInstanceValidationStatus
                                       .PreservedUnsupportedFuture;
                if (instance != null && !instanceIds.Add(instance.InstanceId))
                {
                    diagnostics.Add(Error(
                        "AL-WST-INSTANCE-DUPLICATE",
                        instance.InstanceId,
                        "Instance identity appears more than once."));
                }
            }

            if (snapshot.ActiveInstances.Any(instance =>
                    instance != null &&
                    instance.State != WorldEventInstanceState.Active) ||
                snapshot.CompletedHistory.Any(instance =>
                    instance != null &&
                    instance.State == WorldEventInstanceState.Active) ||
                (snapshot.ActiveInstance != null &&
                 snapshot.ActiveInstance.CommittedEffectRevision !=
                    snapshot.CommittedEffectRevision))
            {
                diagnostics.Add(Error(
                    "AL-WST-SNAPSHOT-STATE",
                    string.Empty,
                    "Active and completed collections contain inconsistent states."));
            }

            ValidateReceipts(
                snapshot.OperationReceipts,
                snapshot.SnapshotRevision,
                snapshot.ActiveInstances,
                snapshot.CompletedHistory,
                diagnostics,
                ref preservedFuture);

            if (diagnostics.Any(item =>
                    item.Severity == WorldStateDiagnosticSeverity.Error))
            {
                return new WorldStateInstanceValidationResult(
                    WorldStateInstanceValidationStatus.Invalid,
                    OrderDiagnostics(diagnostics));
            }

            return new WorldStateInstanceValidationResult(
                preservedFuture
                    ? WorldStateInstanceValidationStatus.PreservedUnsupportedFuture
                    : WorldStateInstanceValidationStatus.Valid,
                OrderDiagnostics(diagnostics));
        }

        private static void ValidateReceipts(
            IEnumerable<WorldStateOperationReceipt> receipts,
            long snapshotRevision,
            IReadOnlyList<WorldEventInstance> activeInstances,
            IReadOnlyList<WorldEventInstance> completedHistory,
            ICollection<WorldStateDiagnostic> diagnostics,
            ref bool preservedFuture)
        {
            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            var correlationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldStateOperationReceipt receipt in receipts)
            {
                WorldStateInstanceValidationResult resultingValidation =
                    receipt?.ResultingInstance == null
                        ? null
                        : ValidateInstance(receipt.ResultingInstance);
                if (resultingValidation != null)
                {
                    foreach (WorldStateDiagnostic diagnostic in
                             resultingValidation.Diagnostics)
                    {
                        diagnostics.Add(diagnostic);
                    }
                    preservedFuture |= resultingValidation.Status ==
                        WorldStateInstanceValidationStatus
                            .PreservedUnsupportedFuture;
                }

                if (receipt == null ||
                    !IsOpaqueId(receipt.OperationId) ||
                    !IsOpaqueId(receipt.CorrelationId) ||
                    !IsSha256(receipt.SemanticHash) ||
                    !Enum.IsDefined(
                        typeof(WorldStateTransitionKind),
                        receipt.TransitionKind) ||
                    !IsOpaqueId(receipt.InstanceId) ||
                    receipt.CommittedRevision <= 0L ||
                    receipt.CommittedRevision > snapshotRevision ||
                    receipt.ResultingInstance == null ||
                    resultingValidation == null ||
                    resultingValidation.Status ==
                        WorldStateInstanceValidationStatus.Invalid ||
                    !string.Equals(
                        receipt.InstanceId,
                        receipt.ResultingInstance.InstanceId,
                        StringComparison.Ordinal) ||
                    !IsReceiptStateConsistent(receipt) ||
                    !IsReceiptSnapshotConsistent(
                        receipt,
                        snapshotRevision,
                        activeInstances,
                        completedHistory) ||
                    !operationIds.Add(receipt.OperationId) ||
                    !correlationIds.Add(receipt.CorrelationId))
                {
                    diagnostics.Add(Error(
                        "AL-WST-CORRELATION-LEDGER",
                        receipt?.OperationId,
                        "Operation receipt is malformed or duplicated."));
                }
            }
        }

        private static bool IsReceiptSnapshotConsistent(
            WorldStateOperationReceipt receipt,
            long snapshotRevision,
            IReadOnlyList<WorldEventInstance> activeInstances,
            IReadOnlyList<WorldEventInstance> completedHistory)
        {
            if (receipt?.ResultingInstance == null)
            {
                return false;
            }

            WorldEventInstance active = activeInstances.FirstOrDefault(instance =>
                instance != null &&
                string.Equals(
                    instance.InstanceId,
                    receipt.InstanceId,
                    StringComparison.Ordinal));
            WorldEventInstance completed = completedHistory.FirstOrDefault(instance =>
                instance != null &&
                string.Equals(
                    instance.InstanceId,
                    receipt.InstanceId,
                    StringComparison.Ordinal));

            if (receipt.TransitionKind != WorldStateTransitionKind.Start &&
                active != null)
            {
                return false;
            }

            if (receipt.CommittedRevision == snapshotRevision)
            {
                WorldEventInstance current = active ?? completed;
                return current != null &&
                       AreInstanceRecordsEquivalent(
                           receipt.ResultingInstance,
                           current);
            }

            if (active != null)
            {
                return receipt.TransitionKind == WorldStateTransitionKind.Start &&
                       AreInstanceRecordsEquivalent(
                           receipt.ResultingInstance,
                           active);
            }

            if (completed == null)
            {
                return true;
            }

            if (receipt.TransitionKind == WorldStateTransitionKind.Start)
            {
                return string.Equals(
                           receipt.ResultingInstance.InstanceId,
                           completed.InstanceId,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           receipt.ResultingInstance.DefinitionId,
                           completed.DefinitionId,
                           StringComparison.Ordinal) &&
                       completed.Revision > receipt.ResultingInstance.Revision;
            }

            return AreInstanceRecordsEquivalent(
                receipt.ResultingInstance,
                completed);
        }

        private static bool AreInstanceRecordsEquivalent(
            WorldEventInstance left,
            WorldEventInstance right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.InstanceId, right.InstanceId, StringComparison.Ordinal) &&
                   string.Equals(left.DefinitionId, right.DefinitionId, StringComparison.Ordinal) &&
                   left.DefinitionSchemaVersion == right.DefinitionSchemaVersion &&
                   string.Equals(left.DefinitionContentVersion, right.DefinitionContentVersion, StringComparison.Ordinal) &&
                   string.Equals(left.DefinitionSourceRevision, right.DefinitionSourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal) &&
                   string.Equals(left.OperationId, right.OperationId, StringComparison.Ordinal) &&
                   string.Equals(left.SourceSystemId, right.SourceSystemId, StringComparison.Ordinal) &&
                   string.Equals(left.ExclusiveGroup, right.ExclusiveGroup, StringComparison.Ordinal) &&
                   left.State == right.State &&
                   left.ScheduledAtUtcSeconds == right.ScheduledAtUtcSeconds &&
                   left.StartedAtUtcSeconds == right.StartedAtUtcSeconds &&
                   left.ExpectedEndAtUtcSeconds == right.ExpectedEndAtUtcSeconds &&
                   left.CompletedAtUtcSeconds == right.CompletedAtUtcSeconds &&
                   left.CompletionReason == right.CompletionReason &&
                   left.Revision == right.Revision &&
                   left.CommittedEffectRevision == right.CommittedEffectRevision &&
                   AreResolvedEffectsEquivalent(
                       left.ResolvedEffects,
                       right.ResolvedEffects);
        }

        private static bool AreResolvedEffectsEquivalent(
            IReadOnlyList<WorldResolvedEffectSummary> left,
            IReadOnlyList<WorldResolvedEffectSummary> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                WorldResolvedEffectSummary leftEffect = left[index];
                WorldResolvedEffectSummary rightEffect = right[index];
                if (leftEffect == null ||
                    rightEffect == null ||
                    !string.Equals(leftEffect.EffectId, rightEffect.EffectId, StringComparison.Ordinal) ||
                    !string.Equals(leftEffect.ConsumerId, rightEffect.ConsumerId, StringComparison.Ordinal) ||
                    leftEffect.Operation != rightEffect.Operation ||
                    !string.Equals(leftEffect.ParameterHash, rightEffect.ParameterHash, StringComparison.Ordinal) ||
                    leftEffect.ConsumerPlanSchemaVersion != rightEffect.ConsumerPlanSchemaVersion ||
                    leftEffect.Required != rightEffect.Required ||
                    leftEffect.RemovalOrder != rightEffect.RemovalOrder ||
                    !AreParametersEquivalent(
                        leftEffect.Parameters,
                        rightEffect.Parameters))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreParametersEquivalent(
            IReadOnlyList<WorldEffectParameter> left,
            IReadOnlyList<WorldEffectParameter> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                WorldEffectParameter leftParameter = left[index];
                WorldEffectParameter rightParameter = right[index];
                if (leftParameter == null ||
                    rightParameter == null ||
                    !string.Equals(leftParameter.Name, rightParameter.Name, StringComparison.Ordinal) ||
                    leftParameter.Kind != rightParameter.Kind ||
                    leftParameter.IntegerValue != rightParameter.IntegerValue ||
                    leftParameter.NumberValue != rightParameter.NumberValue ||
                    leftParameter.BooleanValue != rightParameter.BooleanValue ||
                    !string.Equals(leftParameter.ReferenceValue, rightParameter.ReferenceValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsReceiptStateConsistent(
            WorldStateOperationReceipt receipt)
        {
            if (receipt?.ResultingInstance == null)
            {
                return false;
            }

            switch (receipt.TransitionKind)
            {
                case WorldStateTransitionKind.Start:
                    return receipt.ResultingInstance.State ==
                           WorldEventInstanceState.Active;
                case WorldStateTransitionKind.End:
                    return receipt.ResultingInstance.State ==
                           WorldEventInstanceState.Ended;
                case WorldStateTransitionKind.Cancel:
                    return receipt.ResultingInstance.State ==
                           WorldEventInstanceState.Cancelled;
                default:
                    return false;
            }
        }

        private static bool AreValidResolvedEffects(
            IReadOnlyList<WorldResolvedEffectSummary> effects)
        {
            if (effects == null ||
                effects.Count > WorldStateTechnicalLimits.MaximumEffectsPerDefinition ||
                effects.Any(effect => effect == null) ||
                HasDuplicate(effects.Where(effect => effect != null)
                    .Select(effect => effect.EffectId)) ||
                HasDuplicate(effects.Where(effect => effect != null)
                    .Select(effect => effect.RemovalOrder.ToString(
                        CultureInfo.InvariantCulture))))
            {
                return false;
            }

            return effects.All(effect =>
                IsEffectId(effect.EffectId) &&
                IsConsumerId(effect.ConsumerId) &&
                Enum.IsDefined(typeof(WorldEffectOperation), effect.Operation) &&
                AreValidParameters(effect.Parameters) &&
                IsSha256(effect.ParameterHash) &&
                effect.ConsumerPlanSchemaVersion > 0 &&
                effect.RemovalOrder >= 0);
        }

        internal static bool AreValidParameters(
            IReadOnlyList<WorldEffectParameter> parameters)
        {
            if (parameters == null ||
                parameters.Count >
                    WorldStateTechnicalLimits.MaximumParametersPerEffect ||
                parameters.Any(parameter => parameter == null) ||
                HasDuplicate(parameters.Where(parameter => parameter != null)
                    .Select(parameter => parameter.Name)))
            {
                return false;
            }

            foreach (WorldEffectParameter parameter in parameters)
            {
                if (!ParameterNamePattern.IsMatch(parameter.Name) ||
                    !Enum.IsDefined(
                        typeof(WorldEffectParameterKind),
                        parameter.Kind))
                {
                    return false;
                }

                if (parameter.Kind == WorldEffectParameterKind.Number &&
                    (double.IsNaN(parameter.NumberValue) ||
                     double.IsInfinity(parameter.NumberValue)))
                {
                    return false;
                }

                if (parameter.Kind == WorldEffectParameterKind.Reference &&
                    !IsBounded(
                        parameter.ReferenceValue,
                        WorldStateTechnicalLimits.MaximumReferenceUtf8Bytes))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsDefinitionId(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumIdentifierUtf8Bytes) &&
                   DefinitionIdPattern.IsMatch(value);
        }

        internal static bool IsEffectId(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumIdentifierUtf8Bytes) &&
                   EffectIdPattern.IsMatch(value);
        }

        internal static bool IsConsumerId(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumIdentifierUtf8Bytes) &&
                   ConsumerIdPattern.IsMatch(value);
        }

        internal static bool IsSourceSystemId(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumIdentifierUtf8Bytes) &&
                   SourceSystemIdPattern.IsMatch(value);
        }

        internal static bool IsOpaqueId(string value)
        {
            if (!IsBounded(
                    value,
                    WorldStateTechnicalLimits.MaximumIdentifierUtf8Bytes))
            {
                return false;
            }

            return value.All(character =>
                !char.IsControl(character) && !char.IsWhiteSpace(character));
        }

        internal static bool IsSha256(string value)
        {
            return value != null &&
                   value.Length == 64 &&
                   value.All(character =>
                       character >= '0' && character <= '9' ||
                       character >= 'a' && character <= 'f');
        }

        internal static WorldStateDiagnostic Error(
            string code,
            string subjectId,
            string message)
        {
            return new WorldStateDiagnostic(
                WorldStateDiagnosticSeverity.Error,
                code,
                subjectId,
                message);
        }

        internal static IReadOnlyList<WorldStateDiagnostic> OrderDiagnostics(
            IEnumerable<WorldStateDiagnostic> diagnostics)
        {
            return new ReadOnlyCollection<WorldStateDiagnostic>(
                (diagnostics ?? Array.Empty<WorldStateDiagnostic>())
                    .Where(item => item != null)
                    .OrderByDescending(item => item.Severity)
                    .ThenBy(item => item.Code, StringComparer.Ordinal)
                    .ThenBy(item => item.SubjectId, StringComparer.Ordinal)
                    .ThenBy(item => item.Message, StringComparer.Ordinal)
                    .Take(WorldStateTechnicalLimits.MaximumDiagnostics)
                    .ToList());
        }

        private static bool IsNotificationId(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumIdentifierUtf8Bytes) &&
                   NotificationIdPattern.IsMatch(value);
        }

        private static bool IsContentReference(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumReferenceUtf8Bytes) &&
                   ContentReferencePattern.IsMatch(value);
        }

        private static bool IsBoundedVersion(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumVersionUtf8Bytes) &&
                   VersionPattern.IsMatch(value);
        }

        private static bool IsLegacyAlias(string value)
        {
            return IsBounded(
                       value,
                       WorldStateTechnicalLimits.MaximumIdentifierUtf8Bytes) &&
                   LegacyAliasPattern.IsMatch(value);
        }

        private static bool IsBounded(string value, int maximumUtf8Bytes)
        {
            return !string.IsNullOrEmpty(value) &&
                   Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes;
        }

        private static bool HasNullOrDuplicate(IEnumerable<string> values)
        {
            return values == null ||
                   values.Any(string.IsNullOrEmpty) ||
                   HasDuplicate(values);
        }

        private static bool HasDuplicate(IEnumerable<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return values.Any(value => !seen.Add(value ?? string.Empty));
        }

        private static void AddAliasError(
            ICollection<WorldStateDiagnostic> diagnostics,
            ref WorldStateDefinitionValidationStatus status,
            string subject,
            string message)
        {
            AddDefinitionError(
                diagnostics,
                ref status,
                WorldStateDefinitionValidationStatus.AliasCollision,
                "AL-WST-ID-ALIAS",
                subject,
                message);
        }

        private static void AddDefinitionError(
            ICollection<WorldStateDiagnostic> diagnostics,
            ref WorldStateDefinitionValidationStatus status,
            WorldStateDefinitionValidationStatus candidate,
            string code,
            string subject,
            string message)
        {
            if (status == WorldStateDefinitionValidationStatus.Valid ||
                (int)candidate < (int)status)
            {
                status = candidate;
            }

            diagnostics.Add(Error(code, subject, message));
        }

        private static List<T> CopyBounded<T>(
            IEnumerable<T> source,
            int maximumCount,
            out bool limitExceeded)
        {
            limitExceeded = false;
            var copy = new List<T>();
            if (source == null)
            {
                return copy;
            }

            using (IEnumerator<T> enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (copy.Count >= maximumCount)
                    {
                        limitExceeded = true;
                        break;
                    }

                    copy.Add(enumerator.Current);
                }
            }

            return copy;
        }
    }

    internal static class WorldStateHash
    {
        public static string Compute(params string[] values)
        {
            string canonical = string.Join(
                "\u001f",
                (values ?? Array.Empty<string>()).Select(value => value ?? string.Empty));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        public static string Parameters(
            IEnumerable<WorldEffectParameter> parameters)
        {
            var values = new List<string>();
            foreach (WorldEffectParameter parameter in
                     (parameters ?? Array.Empty<WorldEffectParameter>())
                     .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                values.Add(parameter.Name);
                values.Add(((int)parameter.Kind).ToString(CultureInfo.InvariantCulture));
                values.Add(parameter.IntegerValue.ToString(CultureInfo.InvariantCulture));
                values.Add(parameter.NumberValue.ToString("R", CultureInfo.InvariantCulture));
                values.Add(parameter.BooleanValue ? "1" : "0");
                values.Add(parameter.ReferenceValue);
            }

            return Compute(values.ToArray());
        }
    }
}
