using System.Collections.Generic;
using AL.Battle.Simulator;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using NUnit.Framework;

namespace AL.Tests.EditMode.Battle
{
    public sealed class FixedPointBattleSimulatorTests
    {
        [Test]
        public void AdapterProducesIdenticalResultsForFixedSeedAcrossRuns()
        {
            ServiceLocator.Register<IResearchService>(new ZeroResearchService());
            var simulator = new FixedPointBattleSimulator();

            BattleReport first = simulator.Simulate(PveRequest(seed: 12345));
            BattleReport second = simulator.Simulate(PveRequest(seed: 12345));

            Assert.False(string.IsNullOrEmpty(first.ComputationSha256));
            Assert.AreEqual(64, first.ComputationSha256.Length);
            Assert.AreEqual(first.ComputationSha256, second.ComputationSha256);
            Assert.AreEqual(first.IsWinner, second.IsWinner);
            Assert.AreEqual(first.Rounds, second.Rounds);
            Assert.AreEqual(first.AttackerPower, second.AttackerPower);
            Assert.AreEqual(first.DefenderPower, second.DefenderPower);
            Assert.AreEqual(first.WarzoneCreditsEarned, second.WarzoneCreditsEarned);
            Assert.AreEqual(first.Summary, second.Summary);
        }

        [Test]
        public void AdapterHonorsSeedAndProducesDifferentHashesForDifferentSeeds()
        {
            ServiceLocator.Register<IResearchService>(new ZeroResearchService());
            var simulator = new FixedPointBattleSimulator();

            BattleReport first = simulator.Simulate(PveRequest(seed: 12345));
            BattleReport second = simulator.Simulate(PveRequest(seed: 54321));

            Assert.False(string.IsNullOrEmpty(first.ComputationSha256));
            Assert.False(string.IsNullOrEmpty(second.ComputationSha256));
            Assert.AreNotEqual(first.ComputationSha256, second.ComputationSha256);
        }

        [Test]
        public void AdapterRoutesPveThroughDeterministicEngineWithoutFloatFallback()
        {
            ServiceLocator.Register<IResearchService>(new ZeroResearchService());
            var simulator = new FixedPointBattleSimulator();

            BattleReport report = simulator.Simulate(PveRequest(seed: 12345));

            Assert.False(string.IsNullOrEmpty(report.ComputationSha256));
            Assert.Greater(report.Rounds, 0);
            Assert.Greater(report.AttackerPower, 0);
            Assert.Greater(report.DefenderPower, 0);
            Assert.NotNull(report.AttackerLosses);
            Assert.NotNull(report.DefenderLosses);
            Assert.False(string.IsNullOrEmpty(report.Summary));
        }

        [Test]
        public void AdapterFailsClosedOnInvalidRequestWithoutFloatFallback()
        {
            ServiceLocator.Register<IResearchService>(new ZeroResearchService());
            var simulator = new FixedPointBattleSimulator();
            var request = new BattleRequest
            {
                Type = BattleType.PvE,
                AttackerTroops = new List<TroopStack>(),
                DefenderTroops = new List<TroopStack>()
            };

            BattleReport report = simulator.Simulate(request);

            Assert.True(string.IsNullOrEmpty(report.ComputationSha256));
            Assert.False(report.IsWinner);
            Assert.That(report.Summary, Does.Contain("rejected"));
        }

        private static BattleRequest PveRequest(int seed)
        {
            return new BattleRequest
            {
                Type = BattleType.PvE,
                RandomSeed = seed,
                AttackerRealm = RealmId.Crownlands,
                DefenderRealm = RealmId.None,
                AttackerTroops = new List<TroopStack>
                {
                    new TroopStack { Type = TroopType.Infantry, Count = 60 },
                    new TroopStack { Type = TroopType.Ranged, Count = 40 }
                },
                DefenderTroops = new List<TroopStack>
                {
                    new TroopStack { Type = TroopType.Infantry, Count = 45 },
                    new TroopStack { Type = TroopType.Cavalry, Count = 20 }
                }
            };
        }

        private sealed class ZeroResearchService : IResearchService
        {
            public ResearchState GetResearchState(string researchId)
            {
                return null;
            }

            public IEnumerable<ResearchState> GetAllResearchStates()
            {
                return new ResearchState[0];
            }

            public void StartResearch(string researchId)
            {
            }

            public void CompleteResearch(string researchId)
            {
            }

            public float GetStatBonus(StatType statType)
            {
                return 0f;
            }
        }
    }
}
