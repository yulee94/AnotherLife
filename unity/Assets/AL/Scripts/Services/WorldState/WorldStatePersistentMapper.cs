using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces.WorldState;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.WorldState
{
    public static class WorldStatePersistentMapper
    {
        public static WorldStatePersistentState Empty()
        {
            return new WorldStatePersistentState
            {
                Version = WorldStatePersistentState.CurrentVersion,
                SnapshotRevision = 0L,
                EffectRevision = 0L,
                LastTrustedUtcSeconds = 0L,
                PolicyRevision = WorldStateAuthoredCatalog.PolicyRevision,
                CatalogRevision = WorldStateAuthoredCatalog.CatalogRevision,
                HasActiveInstance = false,
                ActiveInstance = new WorldStateInstanceRecord(),
                CompletedHistory = new List<WorldStateInstanceRecord>(),
                OperationReceipts = new List<WorldStateReceiptRecord>()
            };
        }

        public static WorldStatePersistentState Clone(WorldStatePersistentState source)
        {
            if (source == null)
            {
                return Empty();
            }

            WorldStatePersistentState copy =
                JsonUtility.FromJson<WorldStatePersistentState>(JsonUtility.ToJson(source));
            Normalize(copy);
            return copy;
        }

        public static WorldStatePersistentState FromSave(SaveGameData save)
        {
            if (save == null)
            {
                return Empty();
            }

            if (save.WorldState == null || save.WorldState.Version == 0)
            {
                return Empty();
            }

            return Clone(save.WorldState);
        }

        public static void Normalize(WorldStatePersistentState state)
        {
            if (state == null)
            {
                return;
            }

            state.PolicyRevision = state.PolicyRevision ?? string.Empty;
            state.CatalogRevision = state.CatalogRevision ?? string.Empty;
            state.ActiveInstance ??= new WorldStateInstanceRecord();
            state.CompletedHistory ??= new List<WorldStateInstanceRecord>();
            state.OperationReceipts ??= new List<WorldStateReceiptRecord>();
            NormalizeInstance(state.ActiveInstance);
            for (int i = 0; i < state.CompletedHistory.Count; i++)
            {
                NormalizeInstance(state.CompletedHistory[i]);
            }

            for (int i = 0; i < state.OperationReceipts.Count; i++)
            {
                WorldStateReceiptRecord receipt = state.OperationReceipts[i];
                if (receipt == null)
                {
                    continue;
                }

                receipt.OperationId = receipt.OperationId ?? string.Empty;
                receipt.CorrelationId = receipt.CorrelationId ?? string.Empty;
                receipt.SemanticHash = receipt.SemanticHash ?? string.Empty;
                receipt.InstanceId = receipt.InstanceId ?? string.Empty;
                receipt.ResultingInstance ??= new WorldStateInstanceRecord();
                NormalizeInstance(receipt.ResultingInstance);
            }
        }

        public static WorldStateSnapshot ToSnapshot(WorldStatePersistentState state)
        {
            WorldStatePersistentState current = Clone(state);
            var active = new List<WorldEventInstance>();
            if (current.HasActiveInstance)
            {
                active.Add(ToInstance(current.ActiveInstance));
            }

            WorldEventInstance[] history = current.CompletedHistory
                .Select(ToInstance)
                .ToArray();
            WorldStateOperationReceipt[] receipts = current.OperationReceipts
                .Select(ToReceipt)
                .ToArray();
            WorldStateSnapshotStatus status = current.HasActiveInstance
                ? WorldStateSnapshotStatus.AvailableActive
                : WorldStateSnapshotStatus.AvailableNoActiveEvent;
            return new WorldStateSnapshot(
                status,
                current.SnapshotRevision,
                current.PolicyRevision,
                current.CatalogRevision,
                active,
                history,
                current.EffectRevision,
                true,
                current.LastTrustedUtcSeconds,
                receipts,
                Array.Empty<WorldStateDiagnostic>());
        }

        public static void ApplyTransition(
            WorldStatePersistentState candidate,
            WorldStateTransitionPlan plan)
        {
            if (candidate == null || plan == null || plan.InstanceAfter == null)
            {
                return;
            }

            candidate.Version = WorldStatePersistentState.CurrentVersion;
            candidate.SnapshotRevision = plan.ExpectedNewRevision;
            candidate.EffectRevision = plan.InstanceAfter.CommittedEffectRevision;
            candidate.LastTrustedUtcSeconds = plan.LedgerEntry != null
                ? plan.LedgerEntry.CommittedAtUtcSeconds
                : candidate.LastTrustedUtcSeconds;
            candidate.PolicyRevision = plan.PolicyRevision;
            candidate.CatalogRevision = plan.CatalogRevision;

            WorldStateInstanceRecord after = FromInstance(plan.InstanceAfter);
            if (plan.TransitionKind == WorldStateTransitionKind.Start)
            {
                candidate.HasActiveInstance = true;
                candidate.ActiveInstance = after;
            }
            else
            {
                candidate.HasActiveInstance = false;
                candidate.ActiveInstance = new WorldStateInstanceRecord();
                candidate.CompletedHistory ??= new List<WorldStateInstanceRecord>();
                candidate.CompletedHistory.Add(after);
                while (candidate.CompletedHistory.Count >
                       WorldStateTechnicalLimits.MaximumCompletedHistory)
                {
                    candidate.CompletedHistory.RemoveAt(0);
                }
            }

            candidate.OperationReceipts ??= new List<WorldStateReceiptRecord>();
            candidate.OperationReceipts.Add(
                new WorldStateReceiptRecord
                {
                    OperationId = plan.OperationId,
                    CorrelationId = plan.CorrelationId,
                    SemanticHash = plan.SemanticHash,
                    TransitionKind = (int)plan.TransitionKind,
                    InstanceId = plan.InstanceAfter.InstanceId,
                    CommittedRevision = plan.ExpectedNewRevision,
                    ResultingInstance = FromInstance(plan.InstanceAfter)
                });
            while (candidate.OperationReceipts.Count >
                   WorldStateTechnicalLimits.MaximumOperationReceipts)
            {
                candidate.OperationReceipts.RemoveAt(0);
            }
        }

        public static bool SameJson(
            WorldStatePersistentState left,
            WorldStatePersistentState right)
        {
            return string.Equals(
                JsonUtility.ToJson(Clone(left)),
                JsonUtility.ToJson(Clone(right)),
                StringComparison.Ordinal);
        }

        private static void NormalizeInstance(WorldStateInstanceRecord instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.InstanceId = instance.InstanceId ?? string.Empty;
            instance.DefinitionId = instance.DefinitionId ?? string.Empty;
            instance.DefinitionContentVersion =
                instance.DefinitionContentVersion ?? string.Empty;
            instance.DefinitionSourceRevision =
                instance.DefinitionSourceRevision ?? string.Empty;
            instance.CorrelationId = instance.CorrelationId ?? string.Empty;
            instance.OperationId = instance.OperationId ?? string.Empty;
            instance.SourceSystemId = instance.SourceSystemId ?? string.Empty;
            instance.ExclusiveGroup = instance.ExclusiveGroup ?? string.Empty;
            instance.ResolvedEffects ??= new List<WorldStateResolvedEffectRecord>();
            for (int i = 0; i < instance.ResolvedEffects.Count; i++)
            {
                WorldStateResolvedEffectRecord effect = instance.ResolvedEffects[i];
                if (effect == null)
                {
                    continue;
                }

                effect.EffectId = effect.EffectId ?? string.Empty;
                effect.ConsumerId = effect.ConsumerId ?? string.Empty;
                effect.ParameterHash = effect.ParameterHash ?? string.Empty;
                effect.Parameters ??= new List<WorldStateEffectParameterRecord>();
                for (int p = 0; p < effect.Parameters.Count; p++)
                {
                    WorldStateEffectParameterRecord parameter = effect.Parameters[p];
                    if (parameter == null)
                    {
                        continue;
                    }

                    parameter.Name = parameter.Name ?? string.Empty;
                    parameter.ReferenceValue = parameter.ReferenceValue ?? string.Empty;
                }
            }
        }

        private static WorldEventInstance ToInstance(WorldStateInstanceRecord record)
        {
            NormalizeInstance(record);
            return new WorldEventInstance(
                record.InstanceId,
                record.DefinitionId,
                record.DefinitionSchemaVersion,
                record.DefinitionContentVersion,
                record.DefinitionSourceRevision,
                record.CorrelationId,
                record.OperationId,
                record.SourceSystemId,
                record.ExclusiveGroup,
                (WorldEventInstanceState)record.State,
                record.ScheduledAtUtcSeconds,
                record.StartedAtUtcSeconds,
                record.ExpectedEndAtUtcSeconds,
                record.CompletedAtUtcSeconds,
                (WorldEventCompletionReason)record.CompletionReason,
                record.Revision,
                record.ResolvedEffects.Select(ToResolved),
                record.CommittedEffectRevision);
        }

        private static WorldStateInstanceRecord FromInstance(WorldEventInstance instance)
        {
            var record = new WorldStateInstanceRecord
            {
                InstanceId = instance.InstanceId,
                DefinitionId = instance.DefinitionId,
                DefinitionSchemaVersion = instance.DefinitionSchemaVersion,
                DefinitionContentVersion = instance.DefinitionContentVersion,
                DefinitionSourceRevision = instance.DefinitionSourceRevision,
                CorrelationId = instance.CorrelationId,
                OperationId = instance.OperationId,
                SourceSystemId = instance.SourceSystemId,
                ExclusiveGroup = instance.ExclusiveGroup,
                State = (int)instance.State,
                ScheduledAtUtcSeconds = instance.ScheduledAtUtcSeconds,
                StartedAtUtcSeconds = instance.StartedAtUtcSeconds,
                ExpectedEndAtUtcSeconds = instance.ExpectedEndAtUtcSeconds,
                CompletedAtUtcSeconds = instance.CompletedAtUtcSeconds,
                CompletionReason = (int)instance.CompletionReason,
                Revision = instance.Revision,
                CommittedEffectRevision = instance.CommittedEffectRevision,
                ResolvedEffects = instance.ResolvedEffects.Select(FromResolved).ToList()
            };
            return record;
        }

        private static WorldResolvedEffectSummary ToResolved(
            WorldStateResolvedEffectRecord record)
        {
            return new WorldResolvedEffectSummary(
                record.EffectId,
                record.ConsumerId,
                (WorldEffectOperation)record.Operation,
                (record.Parameters ?? new List<WorldStateEffectParameterRecord>())
                    .Select(ToParameter),
                record.ParameterHash,
                record.ConsumerPlanSchemaVersion,
                record.Required,
                record.RemovalOrder);
        }

        private static WorldStateResolvedEffectRecord FromResolved(
            WorldResolvedEffectSummary summary)
        {
            return new WorldStateResolvedEffectRecord
            {
                EffectId = summary.EffectId,
                ConsumerId = summary.ConsumerId,
                Operation = (int)summary.Operation,
                ParameterHash = summary.ParameterHash,
                ConsumerPlanSchemaVersion = summary.ConsumerPlanSchemaVersion,
                Required = summary.Required,
                RemovalOrder = summary.RemovalOrder,
                Parameters = summary.Parameters.Select(FromParameter).ToList()
            };
        }

        private static WorldEffectParameter ToParameter(
            WorldStateEffectParameterRecord record)
        {
            return new WorldEffectParameter(
                record.Name,
                (WorldEffectParameterKind)record.Kind,
                record.IntegerValue,
                record.NumberValue,
                record.BooleanValue,
                record.ReferenceValue);
        }

        private static WorldStateEffectParameterRecord FromParameter(
            WorldEffectParameter parameter)
        {
            return new WorldStateEffectParameterRecord
            {
                Name = parameter.Name,
                Kind = (int)parameter.Kind,
                IntegerValue = parameter.IntegerValue,
                NumberValue = parameter.NumberValue,
                BooleanValue = parameter.BooleanValue,
                ReferenceValue = parameter.ReferenceValue
            };
        }

        private static WorldStateOperationReceipt ToReceipt(WorldStateReceiptRecord record)
        {
            return new WorldStateOperationReceipt(
                record.OperationId,
                record.CorrelationId,
                record.SemanticHash,
                (WorldStateTransitionKind)record.TransitionKind,
                record.InstanceId,
                record.CommittedRevision,
                ToInstance(record.ResultingInstance));
        }
    }
}
