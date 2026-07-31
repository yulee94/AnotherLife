namespace AL.Core.Interfaces.WorldState
{
    public interface IWorldStateClock
    {
        long UtcNowSeconds { get; }
    }

    public interface IWorldStateDefinitionResolver
    {
        bool IsAvailable { get; }

        bool TryResolve(
            string definitionId,
            out WorldEventDefinition definition);
    }

    public interface IWorldEffectConsumer
    {
        string ConsumerId { get; }
        bool IsAvailable { get; }

        WorldEffectPreparationResult PrepareActivate(
            WorldEventInstance instance,
            WorldEffectDescriptor descriptor,
            WorldStateSnapshot snapshot);

        WorldEffectPreparationResult PrepareRemove(
            WorldEventInstance instance,
            WorldResolvedEffectSummary resolvedEffect,
            WorldStateTransitionKind transitionKind,
            WorldStateSnapshot snapshot);

        WorldEffectApplyResult Apply(
            WorldEffectPlan plan,
            IWorldStateMutationTarget target);
    }
}
