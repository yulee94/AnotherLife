using System;
using System.Linq;
using AL.Core.Interfaces.WorldState;
using AL.Services.WorldState;
using NUnit.Framework;

namespace AL.Tests.EditMode.WorldState
{
    public class WorldStateEffectPlanExecutorTests
    {
        [Test]
        public void ApplyUsesIsolatedCandidateAndLeavesOriginalUnchanged()
        {
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStateTransitionPlan plan = PreparedStartPlan(consumer);
            var original = new FakeMutationTarget(3L, 2L);

            WorldStateEffectExecutionResult result =
                WorldStateEffectPlanExecutor.ApplyToIsolatedCandidate(
                    plan,
                    new WorldEffectConsumerRegistry(new[] { consumer }),
                    original);

            Assert.AreEqual(WorldEffectApplyStatus.Applied, result.Status);
            Assert.AreNotSame(original, result.Candidate);
            Assert.IsEmpty(original.AppliedEffects);
            CollectionAssert.AreEqual(
                new[] { WorldStateTestFixtures.EffectId },
                ((FakeMutationTarget)result.Candidate).AppliedEffects);
            Assert.AreEqual(1, consumer.ApplyCount);
        }

        [Test]
        public void StaleCandidateRejectsBeforeAnyConsumerApply()
        {
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStateTransitionPlan plan = PreparedStartPlan(consumer);
            var stale = new FakeMutationTarget(2L, 2L);

            WorldStateEffectExecutionResult result =
                WorldStateEffectPlanExecutor.ApplyToIsolatedCandidate(
                    plan,
                    new WorldEffectConsumerRegistry(new[] { consumer }),
                    stale);

            Assert.AreEqual(
                WorldEffectApplyStatus.RejectedStaleTarget,
                result.Status);
            Assert.AreSame(stale, result.Candidate);
            Assert.AreEqual(0, consumer.ApplyCount);
        }

        [Test]
        public void ApplyFailureAfterEarlierEffectReturnsOriginalCandidate()
        {
            WorldEffectDescriptor first = WorldStateTestFixtures.Effect(
                applicationOrder: 0,
                removalOrder: 1);
            WorldEffectDescriptor second = WorldStateTestFixtures.Effect(
                effectId: "al_world_effect_second_modifier",
                applicationOrder: 1,
                removalOrder: 0);
            WorldEventDefinition definition = WorldStateTestFixtures.Definition(
                effects: new[] { first, second },
                requiredConsumers: new[] { WorldStateTestFixtures.ConsumerId });
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId)
            {
                FailApplyForEffectId = second.EffectId
            };
            WorldStatePlanningResult prepared = WorldStateTestFixtures.Planner(
                definition,
                consumers: consumer).PlanStart(
                WorldStateTestFixtures.StartRequest(),
                WorldStateTestFixtures.EmptySnapshot());
            var original = new FakeMutationTarget(3L, 2L);

            WorldStateEffectExecutionResult result =
                WorldStateEffectPlanExecutor.ApplyToIsolatedCandidate(
                    prepared.Plan,
                    new WorldEffectConsumerRegistry(new[] { consumer }),
                    original);

            Assert.AreEqual(WorldEffectApplyStatus.RejectedApply, result.Status);
            Assert.AreSame(original, result.Candidate);
            Assert.IsEmpty(original.AppliedEffects);
            Assert.AreEqual(2, consumer.ApplyCount);
        }

        [Test]
        public void MissingConsumerAndMalformedPlanFailClosed()
        {
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStateTransitionPlan plan = PreparedStartPlan(consumer);
            var original = new FakeMutationTarget(3L, 2L);

            WorldStateEffectExecutionResult missing =
                WorldStateEffectPlanExecutor.ApplyToIsolatedCandidate(
                    plan,
                    new WorldEffectConsumerRegistry(
                        Array.Empty<IWorldEffectConsumer>()),
                    original);
            WorldStateEffectExecutionResult malformed =
                WorldStateEffectPlanExecutor.ApplyToIsolatedCandidate(
                    null,
                    new WorldEffectConsumerRegistry(new[] { consumer }),
                    original);

            Assert.AreEqual(
                WorldEffectApplyStatus.RejectedConsumerUnavailable,
                missing.Status);
            Assert.AreEqual(
                WorldEffectApplyStatus.RejectedInvalidPlan,
                malformed.Status);
            Assert.AreSame(original, missing.Candidate);
            Assert.AreSame(original, malformed.Candidate);
        }

        [Test]
        public void ExecutorPreservesDeterministicPreparedOrder()
        {
            WorldEffectDescriptor later = WorldStateTestFixtures.Effect(
                effectId: "al_world_effect_later_modifier",
                applicationOrder: 1,
                removalOrder: 0);
            WorldEffectDescriptor earlier = WorldStateTestFixtures.Effect(
                applicationOrder: 0,
                removalOrder: 1);
            WorldEventDefinition definition = WorldStateTestFixtures.Definition(
                effects: new[] { later, earlier },
                requiredConsumers: new[] { WorldStateTestFixtures.ConsumerId });
            var consumer = new FakeConsumer(WorldStateTestFixtures.ConsumerId);
            WorldStateTransitionPlan plan = WorldStateTestFixtures.Planner(
                definition,
                consumers: consumer).PlanStart(
                WorldStateTestFixtures.StartRequest(),
                WorldStateTestFixtures.EmptySnapshot()).Plan;

            WorldStateEffectExecutionResult result =
                WorldStateEffectPlanExecutor.ApplyToIsolatedCandidate(
                    plan,
                    new WorldEffectConsumerRegistry(new[] { consumer }),
                    new FakeMutationTarget(3L, 2L));

            Assert.AreEqual(WorldEffectApplyStatus.Applied, result.Status);
            CollectionAssert.AreEqual(
                new[]
                {
                    WorldStateTestFixtures.EffectId,
                    "al_world_effect_later_modifier"
                },
                ((FakeMutationTarget)result.Candidate).AppliedEffects);
            CollectionAssert.AreEqual(
                new[] { 0, 1 },
                plan.PreparedEffectPlans.Select(item => item.Order));
        }

        private static WorldStateTransitionPlan PreparedStartPlan(
            FakeConsumer consumer)
        {
            WorldStatePlanningResult result = WorldStateTestFixtures.Planner(
                consumers: consumer).PlanStart(
                WorldStateTestFixtures.StartRequest(),
                WorldStateTestFixtures.EmptySnapshot());
            Assert.AreEqual(WorldStatePlanningStatus.Prepared, result.Status);
            return result.Plan;
        }
    }
}
