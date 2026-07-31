using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces.WorldState;

namespace AL.Services.WorldState
{
    public static class WorldStateEffectPlanExecutor
    {
        public static WorldStateEffectExecutionResult ApplyToIsolatedCandidate(
            WorldStateTransitionPlan plan,
            WorldEffectConsumerRegistry consumers,
            IWorldStateMutationTarget isolatedCandidate)
        {
            if (plan == null ||
                consumers == null ||
                !consumers.IsValid ||
                isolatedCandidate == null ||
                !WorldStateValidator.IsSha256(plan.PlanHash) ||
                plan.InstanceAfter == null ||
                plan.LedgerEntry == null ||
                plan.PostCommitEvent == null)
            {
                return Reject(
                    WorldEffectApplyStatus.RejectedInvalidPlan,
                    isolatedCandidate,
                    "AL-WST-APPLY-PLAN",
                    "Effect execution requires a complete immutable plan and isolated candidate.");
            }

            long expectedPreviousEffectRevision;
            try
            {
                expectedPreviousEffectRevision = checked(
                    plan.InstanceAfter.CommittedEffectRevision - 1L);
            }
            catch (OverflowException)
            {
                return Reject(
                    WorldEffectApplyStatus.RejectedInvalidPlan,
                    isolatedCandidate,
                    "AL-WST-APPLY-REVISION",
                    "Planned effect revision is invalid.");
            }

            if (isolatedCandidate.WorldStateRevision !=
                    plan.PreviousSnapshotRevision ||
                isolatedCandidate.EffectRevision != expectedPreviousEffectRevision)
            {
                return Reject(
                    WorldEffectApplyStatus.RejectedStaleTarget,
                    isolatedCandidate,
                    "AL-WST-APPLY-STALE",
                    "Isolated candidate revisions do not match the plan.");
            }

            if (plan.PreparedEffectPlans
                    .GroupBy(item => item.Order)
                    .Any(group => group.Count() > 1))
            {
                return Reject(
                    WorldEffectApplyStatus.RejectedInvalidPlan,
                    isolatedCandidate,
                    "AL-WST-APPLY-ORDER",
                    "Effect plan order contains duplicates.");
            }

            IWorldStateMutationTarget current = isolatedCandidate;
            var diagnostics = new List<WorldStateDiagnostic>();
            foreach (WorldEffectPlan effectPlan in plan.PreparedEffectPlans
                         .OrderBy(item => item.Order)
                         .ThenBy(item => item.ConsumerId, StringComparer.Ordinal)
                         .ThenBy(item => item.EffectId, StringComparer.Ordinal))
            {
                if (effectPlan == null ||
                    effectPlan.TransitionKind != plan.TransitionKind ||
                    !WorldStateValidator.IsConsumerId(effectPlan.ConsumerId) ||
                    !WorldStateValidator.IsEffectId(effectPlan.EffectId) ||
                    effectPlan.ConsumerPlanVersion <= 0 ||
                    effectPlan.Order < 0 ||
                    !WorldStateValidator.IsSha256(effectPlan.ParameterHash) ||
                    !WorldStateValidator.AreValidParameters(effectPlan.Parameters))
                {
                    return Reject(
                        WorldEffectApplyStatus.RejectedInvalidPlan,
                        isolatedCandidate,
                        "AL-WST-APPLY-EFFECT-PLAN",
                        "Prepared effect plan is malformed.");
                }

                if (!consumers.TryGetAvailable(
                        effectPlan.ConsumerId,
                        out IWorldEffectConsumer consumer))
                {
                    return Reject(
                        WorldEffectApplyStatus.RejectedConsumerUnavailable,
                        isolatedCandidate,
                        "AL-WST-APPLY-CONSUMER",
                        "Prepared effect consumer is unavailable.");
                }

                WorldEffectApplyResult result;
                try
                {
                    result = consumer.Apply(effectPlan, current);
                }
                catch
                {
                    result = null;
                }

                if (result == null ||
                    result.Status != WorldEffectApplyStatus.Applied ||
                    result.Candidate == null ||
                    result.Candidate.WorldStateRevision !=
                        isolatedCandidate.WorldStateRevision)
                {
                    return Reject(
                        WorldEffectApplyStatus.RejectedApply,
                        isolatedCandidate,
                        "AL-WST-APPLY",
                        "Effect apply failed; the original isolated candidate is retained.");
                }

                diagnostics.AddRange(result.Diagnostics);
                current = result.Candidate;
            }

            return new WorldStateEffectExecutionResult(
                WorldEffectApplyStatus.Applied,
                current,
                WorldStateValidator.OrderDiagnostics(diagnostics));
        }

        private static WorldStateEffectExecutionResult Reject(
            WorldEffectApplyStatus status,
            IWorldStateMutationTarget original,
            string code,
            string message)
        {
            return new WorldStateEffectExecutionResult(
                status,
                original,
                new[] { WorldStateValidator.Error(code, string.Empty, message) });
        }
    }
}
