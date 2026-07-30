using System;
using AL.RealmWar.Territories.Runtime;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class TerritoryLoadDegradationPlannerTests
    {
        [Test]
        public void OneHundredUsersAtHeavyLoadRemainRepresentedWithinApprovedCaps()
        {
            TerritoryLoadPlan plan = TerritoryLoadDegradationPlanner.CreatePlan(
                100,
                TerritoryLoadLevel.Heavy);

            Assert.AreEqual(12, plan.FullDetailCount);
            Assert.AreEqual(20, plan.MediumDetailCount);
            Assert.AreEqual(20, plan.LowDetailCount);
            Assert.AreEqual(48, plan.ImpostorCount);
            Assert.AreEqual(0, plan.CulledCount);
            Assert.AreEqual(100, plan.RepresentedCount);
            Assert.AreEqual(100, plan.AssignedCount);
            Assert.AreEqual(32, plan.Budget.AnimatedCapacity);
        }

        [Test]
        public void UsersBeyondSafetyContractAreExplicitlyCulled()
        {
            TerritoryLoadPlan plan = TerritoryLoadDegradationPlanner.CreatePlan(
                125,
                TerritoryLoadLevel.Critical);

            Assert.AreEqual(TerritoryLoadDegradationPlanner.SafeRepresentedUserCapacity, plan.RepresentedCount);
            Assert.AreEqual(25, plan.CulledCount);
            Assert.AreEqual(125, plan.AssignedCount);
        }

        [Test]
        public void DegradationBudgetsAreMonotonic()
        {
            TerritoryLoadBudget normal =
                TerritoryLoadDegradationPlanner.CreateBudget(TerritoryLoadLevel.Normal);
            TerritoryLoadBudget elevated =
                TerritoryLoadDegradationPlanner.CreateBudget(TerritoryLoadLevel.Elevated);
            TerritoryLoadBudget heavy =
                TerritoryLoadDegradationPlanner.CreateBudget(TerritoryLoadLevel.Heavy);
            TerritoryLoadBudget critical =
                TerritoryLoadDegradationPlanner.CreateBudget(TerritoryLoadLevel.Critical);

            Assert.Greater(normal.AnimatedCapacity, elevated.AnimatedCapacity);
            Assert.Greater(elevated.AnimatedCapacity, heavy.AnimatedCapacity);
            Assert.Greater(heavy.AnimatedCapacity, critical.AnimatedCapacity);
            Assert.Greater(normal.DecorativeVfxMultiplier, elevated.DecorativeVfxMultiplier);
            Assert.Greater(elevated.DecorativeVfxMultiplier, heavy.DecorativeVfxMultiplier);
            Assert.Greater(heavy.DecorativeVfxMultiplier, critical.DecorativeVfxMultiplier);
            Assert.Greater(normal.WeatherMultiplier, elevated.WeatherMultiplier);
            Assert.Greater(elevated.WeatherMultiplier, heavy.WeatherMultiplier);
            Assert.Greater(heavy.WeatherMultiplier, critical.WeatherMultiplier);
            Assert.Less(normal.EnvironmentLodTransitionMultiplier, elevated.EnvironmentLodTransitionMultiplier);
            Assert.Less(elevated.EnvironmentLodTransitionMultiplier, heavy.EnvironmentLodTransitionMultiplier);
            Assert.Less(heavy.EnvironmentLodTransitionMultiplier, critical.EnvironmentLodTransitionMultiplier);
            Assert.IsTrue(normal.DecorativeLightsEnabled);
            Assert.IsFalse(heavy.DecorativeLightsEnabled);
            Assert.IsFalse(critical.DecorativeLightsEnabled);
        }

        [TestCase(69, 16f, TerritoryLoadLevel.Normal)]
        [TestCase(70, 16f, TerritoryLoadLevel.Elevated)]
        [TestCase(100, 16f, TerritoryLoadLevel.Heavy)]
        [TestCase(101, 16f, TerritoryLoadLevel.Critical)]
        [TestCase(10, 37f, TerritoryLoadLevel.Elevated)]
        [TestCase(10, 46f, TerritoryLoadLevel.Heavy)]
        [TestCase(10, 59f, TerritoryLoadLevel.Critical)]
        public void RequiredLevelUsesWorstUserOrFramePressure(
            int users,
            float frameTimeMilliseconds,
            TerritoryLoadLevel expected)
        {
            TerritoryLoadLevel actual = TerritoryLoadDegradationPlanner.EvaluateRequiredLevel(
                users,
                frameTimeMilliseconds,
                33.333f);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void PlannerIsDeterministicForSameInput()
        {
            TerritoryLoadPlan first =
                TerritoryLoadDegradationPlanner.CreatePlan(100, TerritoryLoadLevel.Heavy);
            TerritoryLoadPlan second =
                TerritoryLoadDegradationPlanner.CreatePlan(100, TerritoryLoadLevel.Heavy);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [Test]
        public void StateMachineDegradesQuicklyAndRecoversOneStepAtATime()
        {
            var stateMachine = new TerritoryLoadStateMachine(0.5f, 3f);

            Assert.IsFalse(stateMachine.Step(TerritoryLoadLevel.Heavy, 0.49f));
            Assert.AreEqual(TerritoryLoadLevel.Normal, stateMachine.CurrentLevel);
            Assert.IsTrue(stateMachine.Step(TerritoryLoadLevel.Heavy, 0.01f));
            Assert.AreEqual(TerritoryLoadLevel.Heavy, stateMachine.CurrentLevel);

            Assert.IsFalse(stateMachine.Step(TerritoryLoadLevel.Normal, 2.99f));
            Assert.AreEqual(TerritoryLoadLevel.Heavy, stateMachine.CurrentLevel);
            Assert.IsTrue(stateMachine.Step(TerritoryLoadLevel.Normal, 0.01f));
            Assert.AreEqual(TerritoryLoadLevel.Elevated, stateMachine.CurrentLevel);
            Assert.IsTrue(stateMachine.Step(TerritoryLoadLevel.Normal, 3f));
            Assert.AreEqual(TerritoryLoadLevel.Normal, stateMachine.CurrentLevel);
        }

        [Test]
        public void InvalidSamplesFailClosed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TerritoryLoadDegradationPlanner.CreatePlan(-1, TerritoryLoadLevel.Normal));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TerritoryLoadDegradationPlanner.EvaluateRequiredLevel(-1, 16f, 33.333f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TerritoryLoadDegradationPlanner.EvaluateRequiredLevel(1, float.NaN, 33.333f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TerritoryLoadDegradationPlanner.EvaluateRequiredLevel(1, 16f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TerritoryLoadDegradationPlanner.EvaluateUserLevel(-1));
        }
    }
}
