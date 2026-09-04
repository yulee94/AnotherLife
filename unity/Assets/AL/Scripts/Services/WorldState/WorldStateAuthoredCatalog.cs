using System;
using AL.Core.Interfaces.WorldState;

namespace AL.Services.WorldState
{
    public static class WorldStateAuthoredCatalog
    {
        public const string PolicyRevision = "al_world_policy_v1";
        public const string CatalogRevision = "al_world_event_content_0.1.0";
        public const string VeilOmenDefinitionId = "al_world_event_veil_omen";
        public const string SourceSystemId = "al_world_source_durable";
        public const string PresentationConsumerId = "al_world_consumer_presentation";
        public const string PresentationEffectId = "al_world_effect_veil_omen_presentation";
        public const string ContentVersion = "content_v1";
        public const string SourceRevision = "al_narrative_world_event_source_v001";
        public const string ContentReference = "world.event.veil_omen";
        public const string StartNotificationId = "al_notify_world_event_started";
        public const string EndNotificationId = "al_notify_world_event_ended";
        public const string CancelNotificationId = "al_notify_world_event_cancelled";
        public const long MinimumDurationSeconds = 60L;
        public const long MaximumDurationSeconds = 86400L;
        public const long DefaultDurationSeconds = 3600L;

        public static WorldEventDefinition VeilOmen()
        {
            var effect = new WorldEffectDescriptor(
                PresentationEffectId,
                WorldStateTechnicalLimits.CurrentEffectSchemaVersion,
                PresentationConsumerId,
                WorldEffectOperation.PresentationProfile,
                new[] { WorldEffectParameter.Reference("profile", ContentReference) },
                true,
                0,
                0,
                "effect_source_veil_omen_v1");
            return new WorldEventDefinition(
                VeilOmenDefinitionId,
                WorldStateTechnicalLimits.CurrentDefinitionSchemaVersion,
                ContentVersion,
                SourceRevision,
                new[] { "Omen" },
                WorldEventCategory.NarrativeSignal,
                WorldEventScope.Global,
                WorldStateTechnicalLimits.ExclusiveGroupGlobalPrimary,
                100,
                new WorldEventDurationPolicy(
                    MinimumDurationSeconds,
                    MaximumDurationSeconds,
                    DefaultDurationSeconds,
                    true),
                WorldEventCancellationPolicy.CancellableByOwningSource,
                WorldEventSupersessionPolicy.RejectWhileExclusiveInstanceActive,
                true,
                new[] { effect },
                new[] { PresentationConsumerId },
                Array.Empty<string>(),
                new[] { SourceSystemId },
                StartNotificationId,
                EndNotificationId,
                CancelNotificationId,
                ContentReference);
        }

        public static IWorldStateDefinitionResolver CreateResolver()
        {
            return new AuthoredDefinitionResolver(VeilOmen());
        }

        public static IWorldEffectConsumer CreatePresentationConsumer()
        {
            return new WorldStatePresentationConsumer();
        }

        private sealed class AuthoredDefinitionResolver : IWorldStateDefinitionResolver
        {
            private readonly WorldEventDefinition _veilOmen;

            public AuthoredDefinitionResolver(WorldEventDefinition veilOmen)
            {
                _veilOmen = veilOmen;
            }

            public bool IsAvailable => true;

            public bool TryResolve(string definitionId, out WorldEventDefinition definition)
            {
                definition = null;
                if (string.Equals(
                        definitionId,
                        _veilOmen.DefinitionId,
                        StringComparison.Ordinal))
                {
                    definition = _veilOmen;
                    return true;
                }

                return false;
            }
        }
    }

    public sealed class WorldStatePresentationConsumer : IWorldEffectConsumer
    {
        public string ConsumerId => WorldStateAuthoredCatalog.PresentationConsumerId;

        public bool IsAvailable => true;

        public WorldEffectPreparationResult PrepareActivate(
            WorldEventInstance instance,
            WorldEffectDescriptor descriptor,
            WorldStateSnapshot snapshot)
        {
            if (descriptor == null ||
                !string.Equals(
                    descriptor.ConsumerId,
                    ConsumerId,
                    StringComparison.Ordinal) ||
                descriptor.Operation != WorldEffectOperation.PresentationProfile)
            {
                return new WorldEffectPreparationResult(
                    WorldEffectPreparationStatus.RejectedUnsupportedEffect,
                    null,
                    Array.Empty<WorldStateDiagnostic>());
            }

            return new WorldEffectPreparationResult(
                WorldEffectPreparationStatus.Prepared,
                new WorldEffectPlan(
                    ConsumerId,
                    descriptor.EffectId,
                    1,
                    WorldStateTransitionKind.Start,
                    "al_world_consumer_presentation_v1",
                    WorldStateHash.Parameters(descriptor.Parameters),
                    descriptor.Parameters,
                    descriptor.Required,
                    descriptor.ApplicationOrder),
                Array.Empty<WorldStateDiagnostic>());
        }

        public WorldEffectPreparationResult PrepareRemove(
            WorldEventInstance instance,
            WorldResolvedEffectSummary resolvedEffect,
            WorldStateTransitionKind transitionKind,
            WorldStateSnapshot snapshot)
        {
            if (resolvedEffect == null ||
                !string.Equals(
                    resolvedEffect.ConsumerId,
                    ConsumerId,
                    StringComparison.Ordinal))
            {
                return new WorldEffectPreparationResult(
                    WorldEffectPreparationStatus.RejectedUnsupportedEffect,
                    null,
                    Array.Empty<WorldStateDiagnostic>());
            }

            return new WorldEffectPreparationResult(
                WorldEffectPreparationStatus.Prepared,
                new WorldEffectPlan(
                    ConsumerId,
                    resolvedEffect.EffectId,
                    1,
                    transitionKind,
                    "al_world_consumer_presentation_v1",
                    resolvedEffect.ParameterHash,
                    resolvedEffect.Parameters,
                    resolvedEffect.Required,
                    resolvedEffect.RemovalOrder),
                Array.Empty<WorldStateDiagnostic>());
        }

        public WorldEffectApplyResult Apply(
            WorldEffectPlan plan,
            IWorldStateMutationTarget target)
        {
            if (plan == null ||
                target == null ||
                !string.Equals(plan.ConsumerId, ConsumerId, StringComparison.Ordinal))
            {
                return new WorldEffectApplyResult(
                    WorldEffectApplyStatus.RejectedApply,
                    target,
                    Array.Empty<WorldStateDiagnostic>());
            }

            return new WorldEffectApplyResult(
                WorldEffectApplyStatus.Applied,
                target,
                Array.Empty<WorldStateDiagnostic>());
        }
    }
}
