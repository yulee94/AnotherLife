using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Battle.Simulator
{
    public class DeterministicBattleSimulator : IBattleSimulator
    {
        public BattleReport Simulate(BattleRequest request)
        {
            Debug.Log($"Simulating {request.Type} battle...");

            var research = ServiceLocator.Get<IResearchService>();
            float attackBonus = 1.0f + research.GetStatBonus(StatType.Attack);
            float defenseBonus = 1.0f + research.GetStatBonus(StatType.Defense);

            int attackerPower = (int)(CalculateArmyPower(request.AttackerTroops) * attackBonus);
            int defenderPower = (int)(CalculateArmyPower(request.DefenderTroops) * defenseBonus);

            // Add troop counter modifiers
            attackerPower = ApplyCounters(attackerPower, request.AttackerTroops, request.DefenderTroops);
            defenderPower = ApplyCounters(defenderPower, request.DefenderTroops, request.AttackerTroops);

            bool attackerWins = attackerPower > defenderPower;

            var report = new BattleReport
            {
                IsWinner = attackerWins,
                AttackerLosses = CalculateLosses(request.AttackerTroops, defenderPower, attackerPower),
                DefenderLosses = CalculateLosses(request.DefenderTroops, attackerPower, defenderPower),
                Summary = attackerWins ? "Victory! The enemy forces were crushed." : "Defeat! Our forces were overwhelmed."
            };

            if (attackerWins)
            {
                ServiceLocator.Get<IQuestService>().UpdateProgress(QuestType.WinBattle, 1);
            }

            return report;
        }

        private int CalculateArmyPower(List<TroopStack> troops)
        {
            int power = 0;
            foreach (var stack in troops)
            {
                power += stack.Count * GetTroopBasePower(stack.Type);
            }
            return power;
        }

        private int GetTroopBasePower(TroopType type)
        {
            return type switch
            {
                TroopType.Infantry => 10,
                TroopType.Cavalry => 15,
                TroopType.Ranged => 12,
                TroopType.Siege => 20,
                _ => 1
            };
        }

        private int ApplyCounters(int basePower, List<TroopStack> source, List<TroopStack> target)
        {
            float bonus = 1.0f;

            int sourceInfantry = GetTotalCount(source, TroopType.Infantry);
            int sourceCavalry = GetTotalCount(source, TroopType.Cavalry);
            int sourceRanged = GetTotalCount(source, TroopType.Ranged);

            int targetInfantry = GetTotalCount(target, TroopType.Infantry);
            int targetCavalry = GetTotalCount(target, TroopType.Cavalry);
            int targetRanged = GetTotalCount(target, TroopType.Ranged);

            // RPS: Infantry > Cavalry > Ranged > Infantry
            if (sourceInfantry > 0 && targetCavalry > 0) bonus += 0.2f;
            if (sourceCavalry > 0 && targetRanged > 0) bonus += 0.2f;
            if (sourceRanged > 0 && targetInfantry > 0) bonus += 0.2f;

            return (int)(basePower * bonus);
        }

        private int GetTotalCount(List<TroopStack> troops, TroopType type)
        {
            int total = 0;
            if (troops == null) return 0;
            foreach (var stack in troops)
            {
                if (stack.Type == type) total += stack.Count;
            }
            return total;
        }

        private List<TroopStack> CalculateLosses(List<TroopStack> original, int enemyPower, int ownPower)
        {
            var losses = new List<TroopStack>();
            if (original == null) return losses;

            float lossRatio = Mathf.Clamp01((float)enemyPower / (ownPower + enemyPower + 1));

            foreach (var stack in original)
            {
                losses.Add(new TroopStack
                {
                    Type = stack.Type,
                    Count = (int)(stack.Count * lossRatio * 0.5f)
                });
            }
            return losses;
        }
    }
}
