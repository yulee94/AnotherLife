using AL.VerticalSlice;
using AL.VerticalSlice.Combat;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the greybox champion duel. The combat simulation is pure logic, so the
    /// win/lose/determinism/defend/special behaviour is exercised end-to-end without a scene. The
    /// thin MonoBehaviour (GreyboxCombatEncounter) is presentation glue over this sim.
    /// </summary>
    public class GreyboxCombatEncounterTests
    {
        private const int MaxTurns = 1000;

        [SetUp]
        public void SetUp()
        {
            SliceRunState.Reset();
        }

        private static CombatEncounterConfig AlwaysAttackConfig()
        {
            return new CombatEncounterConfig
            {
                OpponentSpecialChance = 0f,
                OpponentDefendChance = 0f,
                OpponentManaRegenPerTurn = 0,
                ManaRegenPerTurn = 0
            };
        }

        private static string PlayUntilFinished(CombatEncounterSim sim)
        {
            string log = string.Empty;
            while (!sim.IsFinished && sim.TurnNumber < MaxTurns)
            {
                log = sim.PerformPlayerAction(CombatAction.Attack);
            }

            return log;
        }

        [Test]
        public void WinPath_IsPlayableFromStartToFinish()
        {
            var champion = new SliceChampionProfile("champion_test", "Test Champion", "Vanguard", 100, 100, 1000, 0, 0.5f);
            var opponent = SliceOpponentProfile.CreateDefault(); // 240 HP

            var sim = new CombatEncounterSim(champion, opponent, AlwaysAttackConfig(), seed: 7);
            PlayUntilFinished(sim);

            Assert.That(sim.IsFinished, Is.True);
            Assert.That(sim.Outcome, Is.EqualTo(CombatEncounterOutcome.Win));

            SliceCombatResult result = sim.BuildResult("attempt-win");
            Assert.That(result.Won, Is.True);
            Assert.That(result.ChampionId, Is.EqualTo("champion_test"));
            Assert.That(result.OpponentId, Is.EqualTo("opponent_greybox_wraith"));
            Assert.That(result.DamageDealt, Is.GreaterThan(0));
            Assert.That(result.TurnsTaken, Is.GreaterThan(0));
            Assert.That(result.OpponentHealthRemaining, Is.EqualTo(0));
        }

        [Test]
        public void LosePath_IsPlayableFromStartToFinish()
        {
            var champion = SliceChampionProfile.CreateDefault(); // 300 HP
            var opponent = new SliceOpponentProfile("opponent_test", "Test Opponent", 100000, 0, 1000, 0, 0f, 0);

            var sim = new CombatEncounterSim(champion, opponent, AlwaysAttackConfig(), seed: 11);
            PlayUntilFinished(sim);

            Assert.That(sim.IsFinished, Is.True);
            Assert.That(sim.Outcome, Is.EqualTo(CombatEncounterOutcome.Lose));

            SliceCombatResult result = sim.BuildResult("attempt-lose");
            Assert.That(result.Lost, Is.True);
            Assert.That(result.ChampionHealthRemaining, Is.EqualTo(0));
            Assert.That(result.DamageTaken, Is.GreaterThan(0));
        }

        [Test]
        public void SameSeedAndActions_YieldDeterministicResult()
        {
            var champion = SliceChampionProfile.CreateDefault();
            var opponent = SliceOpponentProfile.CreateDefault();
            var config = new CombatEncounterConfig();

            CombatAction[] script =
            {
                CombatAction.Attack, CombatAction.Defend, CombatAction.Special,
                CombatAction.Attack, CombatAction.Special, CombatAction.Attack
            };

            var first = new CombatEncounterSim(champion, opponent, config, seed: 42);
            var second = new CombatEncounterSim(champion, opponent, config, seed: 42);
            foreach (CombatAction action in script)
            {
                first.PerformPlayerAction(action);
                second.PerformPlayerAction(action);
            }

            SliceCombatResult a = first.BuildResult("a");
            SliceCombatResult b = second.BuildResult("b");

            Assert.That(b.Outcome, Is.EqualTo(a.Outcome));
            Assert.That(b.DamageDealt, Is.EqualTo(a.DamageDealt));
            Assert.That(b.DamageTaken, Is.EqualTo(a.DamageTaken));
            Assert.That(b.TurnsTaken, Is.EqualTo(a.TurnsTaken));
            Assert.That(b.SpecialsUsed, Is.EqualTo(a.SpecialsUsed));
        }

        [Test]
        public void Defend_ReducesIncomingDamageToZero_WhenReductionIsFull()
        {
            var champion = new SliceChampionProfile("champion_test", "Test Champion", "Vanguard", 500, 100, 50, 0, 0.5f);
            var opponent = new SliceOpponentProfile("opponent_test", "Test Opponent", 100000, 0, 100, 0, 0f, 0);
            var config = AlwaysAttackConfig();
            config.DefendReduction = 1f;

            var sim = new CombatEncounterSim(champion, opponent, config, seed: 1);
            sim.PerformPlayerAction(CombatAction.Defend);

            // Opponent always attacks for 100, fully negated by the guard -> no health lost.
            Assert.That(sim.ChampionHealth, Is.EqualTo(500));
            Assert.That(sim.TurnNumber, Is.EqualTo(1));
            Assert.That(sim.IsChampionDefending, Is.False); // guard consumed by the incoming hit
        }

        [Test]
        public void Defend_ReducesIncomingDamage_WhenReductionIsPartial()
        {
            var champion = new SliceChampionProfile("champion_test", "Test Champion", "Vanguard", 500, 100, 50, 0, 0.5f);
            var opponent = new SliceOpponentProfile("opponent_test", "Test Opponent", 100000, 0, 100, 0, 0f, 0);
            var config = AlwaysAttackConfig();
            config.DefendReduction = 0.6f;

            var sim = new CombatEncounterSim(champion, opponent, config, seed: 1);
            sim.PerformPlayerAction(CombatAction.Defend);

            // 100 damage reduced by 60% -> 40 taken.
            Assert.That(sim.ChampionHealth, Is.EqualTo(460));
        }

        [Test]
        public void Special_SpendsMana_AndConsumesTurn()
        {
            var champion = new SliceChampionProfile("champion_test", "Test Champion", "Vanguard", 1000, 1000, 10, 200, 0.5f);
            var opponent = SliceOpponentProfile.CreateDefault();
            var config = AlwaysAttackConfig();
            config.SpecialManaCost = 30;

            var sim = new CombatEncounterSim(champion, opponent, config, seed: 3);
            int manaBefore = sim.ChampionMana;
            sim.PerformPlayerAction(CombatAction.Special);

            Assert.That(sim.LastActionConsumedTurn, Is.True);
            Assert.That(sim.ChampionMana, Is.EqualTo(manaBefore - 30));
        }

        [Test]
        public void Special_WithoutMana_DoesNotConsumeTurn()
        {
            var champion = new SliceChampionProfile("champion_test", "Test Champion", "Vanguard", 1000, 0, 10, 200, 0.5f);
            var opponent = SliceOpponentProfile.CreateDefault();
            var config = AlwaysAttackConfig();
            config.SpecialManaCost = 30;
            config.ManaRegenPerTurn = 0;

            var sim = new CombatEncounterSim(champion, opponent, config, seed: 5);
            int turnsBefore = sim.TurnNumber;
            sim.PerformPlayerAction(CombatAction.Special);

            Assert.That(sim.LastActionConsumedTurn, Is.False);
            Assert.That(sim.TurnNumber, Is.EqualTo(turnsBefore));
            Assert.That(sim.LastLog, Does.Contain("mana"));
        }

        [Test]
        public void Special_OnCooldown_DoesNotConsumeTurn()
        {
            var champion = new SliceChampionProfile("champion_test", "Test Champion", "Vanguard", 1000, 1000, 10, 200, 0.5f);
            var opponent = SliceOpponentProfile.CreateDefault();
            var config = AlwaysAttackConfig();
            config.SpecialManaCost = 30;
            config.SpecialCooldownTurns = 3;
            config.ManaRegenPerTurn = 0;

            var sim = new CombatEncounterSim(champion, opponent, config, seed: 5);
            sim.PerformPlayerAction(CombatAction.Special); // consumes turn, starts cooldown
            Assert.That(sim.LastActionConsumedTurn, Is.True);

            int turnsBefore = sim.TurnNumber;
            sim.PerformPlayerAction(CombatAction.Special); // on cooldown
            Assert.That(sim.LastActionConsumedTurn, Is.False);
            Assert.That(sim.TurnNumber, Is.EqualTo(turnsBefore));
            Assert.That(sim.LastLog, Does.Contain("recharging"));
        }

        [Test]
        public void RunState_RoundTripsSelectedChampionAndCombatResult()
        {
            SliceRunState.SelectedChampion = SliceChampionProfile.CreateDefault();

            var sim = new CombatEncounterSim(
                SliceRunState.SelectedChampion,
                SliceOpponentProfile.CreateDefault(),
                new CombatEncounterConfig { OpponentSpecialChance = 0f, OpponentDefendChance = 0f, ManaRegenPerTurn = 0 },
                seed: 99);

            // Keep attacking until someone falls; then write the result exactly as the encounter does.
            PlayUntilFinished(sim);
            SliceRunState.LastCombatResult = sim.BuildResult("attempt-runstate");

            Assert.That(SliceRunState.SelectedChampion, Is.Not.Null);
            Assert.That(SliceRunState.LastCombatResult, Is.Not.Null);
            Assert.That(SliceRunState.LastCombatResult.ChampionId, Is.EqualTo(SliceRunState.SelectedChampion.Id));
            Assert.That(SliceRunState.LastCombatResult.Outcome, Is.Not.EqualTo(CombatEncounterOutcome.None));
        }

        [Test]
        public void Defaults_AreFiniteAndPositive()
        {
            SliceChampionProfile champion = SliceChampionProfile.CreateDefault();
            SliceOpponentProfile opponent = SliceOpponentProfile.CreateDefault();

            Assert.That(champion.MaxHealth, Is.GreaterThan(0));
            Assert.That(champion.AttackPower, Is.GreaterThan(0));
            Assert.That(champion.SpecialPower, Is.GreaterThan(0));
            Assert.That(champion.DefendMitigation, Is.InRange(0f, 1f));
            Assert.That(opponent.MaxHealth, Is.GreaterThan(0));
            Assert.That(opponent.AttackPower, Is.GreaterThan(0));

            var config = new CombatEncounterConfig();
            Assert.That(config.SpecialManaCost, Is.GreaterThan(0));
            Assert.That(config.DefendReduction, Is.InRange(0f, 1f));
            Assert.That(config.Opponent, Is.Not.Null);
        }
    }
}
