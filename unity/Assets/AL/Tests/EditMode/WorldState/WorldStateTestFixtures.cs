using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Core.Interfaces.WorldState;
using AL.Services.WorldState;

namespace AL.Tests.EditMode.WorldState
{
    internal static class WorldStateTestFixtures
    {
        public const long NowUtcSeconds = 1800000000L;
        public const string DefinitionId = "al_world_event_test_condition";
        public const string InstanceId = "world-instance-001";
        public const string CorrelationId = "world-correlation-001";
        public const string OperationId = "world-operation-001";
        public const string SourceSystemId = "al_world_source_test";
        public const string ConsumerId = "al_world_consumer_test";
        public const string OptionalConsumerId = "al_world_consumer_optional";
        public const string EffectId = "al_world_effect_test_modifier";
        public const string OptionalEffectId = "al_world_effect_optional_modifier";
        public const string PolicyRevision = "world_policy_v1";
        public const string CatalogRevision = "world_catalog_v1";

        public static WorldEffectParameter[] Parameters(double value = 1.25d)
        {
            return new[]
            {
                WorldEffectParameter.Number("multiplier", value),
                WorldEffectParameter.Reference("profile", "world.profile.test")
            };
        }

        public static WorldEffectDescriptor Effect(
            string effectId = EffectId,
            string consumerId = ConsumerId,
            bool required = true,
            int applicationOrder = 0,
            int removalOrder = 0,
            IEnumerable<WorldEffectParameter> parameters = null,
            int schemaVersion = 1)
        {
            return new WorldEffectDescriptor(
                effectId,
                schemaVersion,
                consumerId,
                WorldEffectOperation.Multiplier,
                parameters ?? Parameters(),
                required,
                applicationOrder,
                removalOrder,
                "effect_source_v1");
        }

        public static WorldEventDefinition Definition(
            string definitionId = DefinitionId,
            int schemaVersion = 1,
            string contentVersion = "content_v1",
            string sourceRevision = "source_v1",
            IEnumerable<string> aliases = null,
            WorldEventCategory category = WorldEventCategory.RealmCondition,
            WorldEventScope scope = WorldEventScope.Global,
            string exclusiveGroup = "global_primary",
            int priority = 100,
            WorldEventDurationPolicy durationPolicy = null,
            WorldEventCancellationPolicy cancellationPolicy =
                WorldEventCancellationPolicy.CancellableByOwningSource,
            WorldEventSupersessionPolicy supersessionPolicy =
                WorldEventSupersessionPolicy.RejectWhileExclusiveInstanceActive,
            bool presentationOnly = false,
            IEnumerable<WorldEffectDescriptor> effects = null,
            IEnumerable<string> requiredConsumers = null,
            IEnumerable<string> optionalConsumers = null,
            IEnumerable<string> allowedSources = null,
            string startNotification = "al_notify_world_event_started",
            string endNotification = "al_notify_world_event_ended",
            string cancelNotification = "al_notify_world_event_cancelled",
            string contentReference = "world.event.test")
        {
            WorldEffectDescriptor[] effectRows =
                (effects ?? new[] { Effect() }).ToArray();
            return new WorldEventDefinition(
                definitionId,
                schemaVersion,
                contentVersion,
                sourceRevision,
                aliases ?? new[] { "LegacyTestCondition" },
                category,
                scope,
                exclusiveGroup,
                priority,
                durationPolicy ?? new WorldEventDurationPolicy(60L, 3600L, 600L, true),
                cancellationPolicy,
                supersessionPolicy,
                presentationOnly,
                effectRows,
                requiredConsumers ?? effectRows
                    .Where(item => item.Required)
                    .Select(item => item.ConsumerId)
                    .Distinct(StringComparer.Ordinal),
                optionalConsumers ?? effectRows
                    .Where(item => !item.Required)
                    .Select(item => item.ConsumerId)
                    .Distinct(StringComparer.Ordinal),
                allowedSources ?? new[] { SourceSystemId },
                startNotification,
                endNotification,
                cancelNotification,
                contentReference);
        }

        public static WorldResolvedEffectSummary Summary(
            WorldEffectDescriptor descriptor = null)
        {
            descriptor ??= Effect();
            return new WorldResolvedEffectSummary(
                descriptor.EffectId,
                descriptor.ConsumerId,
                descriptor.Operation,
                descriptor.Parameters,
                ParameterHash(descriptor.Parameters),
                1,
                descriptor.Required,
                descriptor.RemovalOrder);
        }

        public static WorldEventInstance ActiveInstance(
            WorldEventDefinition definition = null,
            string instanceId = InstanceId,
            long startedAt = NowUtcSeconds - 100L,
            long expectedEndAt = NowUtcSeconds + 100L,
            long revision = 1L,
            long effectRevision = 1L,
            IEnumerable<WorldResolvedEffectSummary> summaries = null)
        {
            definition ??= Definition();
            return new WorldEventInstance(
                instanceId,
                definition.DefinitionId,
                definition.SchemaVersion,
                definition.ContentVersion,
                definition.SourceRevision,
                CorrelationId,
                OperationId,
                SourceSystemId,
                definition.ExclusiveGroup,
                WorldEventInstanceState.Active,
                startedAt,
                startedAt,
                expectedEndAt,
                0L,
                WorldEventCompletionReason.None,
                revision,
                summaries ?? definition.EffectDescriptors.Select(Summary),
                effectRevision);
        }

        public static WorldEventInstance CompletedInstance(
            WorldEventDefinition definition = null,
            string instanceId = "world-instance-completed")
        {
            WorldEventInstance active = ActiveInstance(
                definition,
                instanceId,
                NowUtcSeconds - 1000L,
                NowUtcSeconds - 500L);
            return new WorldEventInstance(
                active.InstanceId,
                active.DefinitionId,
                active.DefinitionSchemaVersion,
                active.DefinitionContentVersion,
                active.DefinitionSourceRevision,
                active.CorrelationId,
                active.OperationId,
                active.SourceSystemId,
                active.ExclusiveGroup,
                WorldEventInstanceState.Ended,
                active.ScheduledAtUtcSeconds,
                active.StartedAtUtcSeconds,
                active.ExpectedEndAtUtcSeconds,
                NowUtcSeconds - 500L,
                WorldEventCompletionReason.NaturalExpiry,
                2L,
                active.ResolvedEffects,
                2L);
        }

        public static WorldStateSnapshot EmptySnapshot(
            long revision = 3L,
            long effectRevision = 2L,
            long lastTrustedUtc = NowUtcSeconds - 10L,
            bool writable = true,
            WorldStateSnapshotStatus status =
                WorldStateSnapshotStatus.AvailableNoActiveEvent,
            IEnumerable<WorldStateOperationReceipt> receipts = null,
            IEnumerable<WorldEventInstance> history = null)
        {
            return new WorldStateSnapshot(
                status,
                revision,
                PolicyRevision,
                CatalogRevision,
                Array.Empty<WorldEventInstance>(),
                history ?? Array.Empty<WorldEventInstance>(),
                effectRevision,
                writable,
                lastTrustedUtc,
                receipts ?? Array.Empty<WorldStateOperationReceipt>(),
                Array.Empty<WorldStateDiagnostic>());
        }

        public static WorldStateSnapshot ActiveSnapshot(
            WorldEventInstance active = null,
            long revision = 3L,
            long lastTrustedUtc = NowUtcSeconds - 10L,
            bool writable = true,
            WorldStateSnapshotStatus status =
                WorldStateSnapshotStatus.AvailableActive,
            IEnumerable<WorldStateOperationReceipt> receipts = null,
            IEnumerable<WorldEventInstance> extraActive = null)
        {
            active ??= ActiveInstance();
            var rows = new List<WorldEventInstance> { active };
            if (extraActive != null)
            {
                rows.AddRange(extraActive);
            }

            return new WorldStateSnapshot(
                status,
                revision,
                PolicyRevision,
                CatalogRevision,
                rows,
                Array.Empty<WorldEventInstance>(),
                active.CommittedEffectRevision,
                writable,
                lastTrustedUtc,
                receipts ?? Array.Empty<WorldStateOperationReceipt>(),
                Array.Empty<WorldStateDiagnostic>());
        }

        public static WorldStateStartRequest StartRequest(
            string definitionId = DefinitionId,
            string instanceId = InstanceId,
            string correlationId = CorrelationId,
            string operationId = OperationId,
            string sourceSystemId = SourceSystemId,
            long requestedStartAt = NowUtcSeconds,
            long? duration = 600L,
            long expectedRevision = 3L)
        {
            return new WorldStateStartRequest(
                definitionId,
                instanceId,
                correlationId,
                operationId,
                sourceSystemId,
                requestedStartAt,
                duration,
                expectedRevision);
        }

        public static WorldStateEndRequest EndRequest(
            long now = NowUtcSeconds,
            long expectedRevision = 3L,
            string instanceId = InstanceId,
            string correlationId = "world-correlation-end-001",
            string operationId = "world-operation-end-001",
            string sourceSystemId = SourceSystemId)
        {
            return new WorldStateEndRequest(
                instanceId,
                correlationId,
                operationId,
                sourceSystemId,
                now,
                expectedRevision);
        }

        public static WorldStateCancelRequest CancelRequest(
            WorldEventCompletionReason reason =
                WorldEventCompletionReason.CancelledByOwner,
            long now = NowUtcSeconds,
            long expectedRevision = 3L,
            string instanceId = InstanceId,
            string correlationId = "world-correlation-cancel-001",
            string operationId = "world-operation-cancel-001",
            string sourceSystemId = SourceSystemId)
        {
            return new WorldStateCancelRequest(
                instanceId,
                correlationId,
                operationId,
                sourceSystemId,
                reason,
                now,
                expectedRevision);
        }

        public static WorldStateLifecyclePlanner Planner(
            WorldEventDefinition definition = null,
            FakeClock clock = null,
            FakeConsumer consumers = null)
        {
            definition ??= Definition();
            consumers ??= new FakeConsumer(ConsumerId);

            return new WorldStateLifecyclePlanner(
                new FakeDefinitionResolver(definition),
                clock ?? new FakeClock(NowUtcSeconds),
                new WorldEffectConsumerRegistry(new[] { consumers }));
        }

        public static string StartSemanticHash(WorldStateStartRequest request)
        {
            return Hash(
                "start",
                request.DefinitionId,
                request.InstanceId,
                request.CorrelationId,
                request.OperationId,
                request.SourceSystemId,
                request.RequestedStartAtUtcSeconds.ToString(CultureInfo.InvariantCulture),
                request.RequestedDurationSeconds?.ToString(CultureInfo.InvariantCulture) ??
                    "default",
                request.ExpectedSnapshotRevision.ToString(CultureInfo.InvariantCulture));
        }

        public static string ParameterHash(
            IEnumerable<WorldEffectParameter> parameters)
        {
            var values = new List<string>();
            foreach (WorldEffectParameter parameter in parameters
                         .OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                values.Add(parameter.Name);
                values.Add(((int)parameter.Kind).ToString(CultureInfo.InvariantCulture));
                values.Add(parameter.IntegerValue.ToString(CultureInfo.InvariantCulture));
                values.Add(parameter.NumberValue.ToString("R", CultureInfo.InvariantCulture));
                values.Add(parameter.BooleanValue ? "1" : "0");
                values.Add(parameter.ReferenceValue);
            }

            return Hash(values.ToArray());
        }

        public static string Hash(params string[] values)
        {
            string canonical = string.Join(
                "\u001f",
                values.Select(value => value ?? string.Empty));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return string.Concat(hash.Select(value =>
                    value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }

    internal sealed class FakeClock : IWorldStateClock
    {
        public FakeClock(long nowUtcSeconds)
        {
            UtcNowSeconds = nowUtcSeconds;
        }

        public long UtcNowSeconds { get; set; }
        public bool ThrowOnRead { get; set; }

        long IWorldStateClock.UtcNowSeconds
        {
            get
            {
                if (ThrowOnRead)
                {
                    throw new InvalidOperationException("clock unavailable");
                }

                return UtcNowSeconds;
            }
        }
    }

    internal sealed class FakeDefinitionResolver : IWorldStateDefinitionResolver
    {
        private readonly Dictionary<string, WorldEventDefinition> _definitions;

        public FakeDefinitionResolver(params WorldEventDefinition[] definitions)
        {
            _definitions = (definitions ?? Array.Empty<WorldEventDefinition>())
                .Where(item => item != null)
                .ToDictionary(item => item.DefinitionId, StringComparer.Ordinal);
        }

        public bool IsAvailable { get; set; } = true;
        public bool ThrowOnResolve { get; set; }

        public bool TryResolve(
            string definitionId,
            out WorldEventDefinition definition)
        {
            if (ThrowOnResolve)
            {
                throw new InvalidOperationException("catalog unavailable");
            }

            return _definitions.TryGetValue(definitionId, out definition);
        }
    }

    internal sealed class FakeConsumer : IWorldEffectConsumer
    {
        public FakeConsumer(string consumerId)
        {
            ConsumerId = consumerId;
        }

        public string ConsumerId { get; }
        public bool IsAvailable { get; set; } = true;
        public WorldEffectPreparationStatus ActivationStatus { get; set; } =
            WorldEffectPreparationStatus.Prepared;
        public WorldEffectPreparationStatus RemovalStatus { get; set; } =
            WorldEffectPreparationStatus.Prepared;
        public WorldEffectApplyStatus ApplyStatus { get; set; } =
            WorldEffectApplyStatus.Applied;
        public bool ThrowOnPrepare { get; set; }
        public bool ThrowOnApply { get; set; }
        public string FailApplyForEffectId { get; set; } = string.Empty;
        public List<string> PreparationOrder { get; } = new List<string>();
        public int ApplyCount { get; private set; }

        public WorldEffectPreparationResult PrepareActivate(
            WorldEventInstance instance,
            WorldEffectDescriptor descriptor,
            WorldStateSnapshot snapshot)
        {
            if (ThrowOnPrepare)
            {
                throw new InvalidOperationException("prepare unavailable");
            }

            PreparationOrder.Add("activate:" + descriptor.EffectId);
            return BuildPreparation(
                ActivationStatus,
                descriptor.EffectId,
                descriptor.Parameters,
                descriptor.Required,
                descriptor.ApplicationOrder,
                WorldStateTransitionKind.Start);
        }

        public WorldEffectPreparationResult PrepareRemove(
            WorldEventInstance instance,
            WorldResolvedEffectSummary resolvedEffect,
            WorldStateTransitionKind transitionKind,
            WorldStateSnapshot snapshot)
        {
            if (ThrowOnPrepare)
            {
                throw new InvalidOperationException("prepare unavailable");
            }

            PreparationOrder.Add("remove:" + resolvedEffect.EffectId);
            return BuildPreparation(
                RemovalStatus,
                resolvedEffect.EffectId,
                resolvedEffect.Parameters,
                resolvedEffect.Required,
                resolvedEffect.RemovalOrder,
                transitionKind);
        }

        public WorldEffectApplyResult Apply(
            WorldEffectPlan plan,
            IWorldStateMutationTarget target)
        {
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("apply unavailable");
            }

            ApplyCount++;
            if (ApplyStatus != WorldEffectApplyStatus.Applied ||
                string.Equals(
                    plan.EffectId,
                    FailApplyForEffectId,
                    StringComparison.Ordinal))
            {
                return new WorldEffectApplyResult(
                    ApplyStatus == WorldEffectApplyStatus.Applied
                        ? WorldEffectApplyStatus.RejectedApply
                        : ApplyStatus,
                    null,
                    Array.Empty<WorldStateDiagnostic>());
            }

            var fake = (FakeMutationTarget)target;
            return new WorldEffectApplyResult(
                WorldEffectApplyStatus.Applied,
                fake.WithApplied(plan.EffectId),
                Array.Empty<WorldStateDiagnostic>());
        }

        private WorldEffectPreparationResult BuildPreparation(
            WorldEffectPreparationStatus status,
            string effectId,
            IEnumerable<WorldEffectParameter> parameters,
            bool required,
            int order,
            WorldStateTransitionKind transitionKind)
        {
            WorldEffectPlan plan = status == WorldEffectPreparationStatus.Prepared
                ? new WorldEffectPlan(
                    ConsumerId,
                    effectId,
                    1,
                    transitionKind,
                    "consumer_revision_1",
                    WorldStateTestFixtures.ParameterHash(parameters),
                    parameters,
                    required,
                    order)
                : null;
            return new WorldEffectPreparationResult(
                status,
                plan,
                Array.Empty<WorldStateDiagnostic>());
        }
    }

    internal sealed class FakeMutationTarget : IWorldStateMutationTarget
    {
        public FakeMutationTarget(
            long worldStateRevision,
            long effectRevision,
            IEnumerable<string> appliedEffects = null)
        {
            WorldStateRevision = worldStateRevision;
            EffectRevision = effectRevision;
            AppliedEffects = new ReadOnlyCollection<string>(
                (appliedEffects ?? Array.Empty<string>()).ToList());
        }

        public long WorldStateRevision { get; }
        public long EffectRevision { get; }
        public IReadOnlyList<string> AppliedEffects { get; }

        public FakeMutationTarget WithApplied(string effectId)
        {
            return new FakeMutationTarget(
                WorldStateRevision,
                EffectRevision,
                AppliedEffects.Concat(new[] { effectId }));
        }
    }
}
