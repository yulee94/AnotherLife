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
            request ??= new BattleRequest();
            request.AttackerTroops ??= new List<TroopStack>();
            request.DefenderTroops ??= new List<TroopStack>();

            int seed = request.RandomSeed == 0 ? 12345 : request.RandomSeed;
            var random = new System.Random(seed);
            Debug.Log($"Simulating {request.Type} battle with seed {seed}...");

            float attackBonus = 1.0f + TryGetResearchBonus(StatType.Attack);
            float defenseBonus = 1.0f + TryGetResearchBonus(StatType.Defense);

            int attackerBasePower = CalculateArmyPower(request.AttackerTroops);
            int defenderBasePower = CalculateArmyPower(request.DefenderTroops);
            float attackerCounter = GetCounterMultiplier(request.AttackerTroops, request.DefenderTroops, request.Type);
            float defenderCounter = GetCounterMultiplier(request.DefenderTroops, request.AttackerTroops, request.Type);
            float attackerRealm = GetRealmMultiplier(request.AttackerRealm, request.AttackerTroops, request.Type, true);
            float defenderRealm = GetRealmMultiplier(request.DefenderRealm, request.DefenderTroops, request.Type, false);
            float attackerTerrain = GetTerrainMultiplier(request.TerrainId, request.AttackerRealm, request.AttackerTroops);
            float defenderTerrain = GetTerrainMultiplier(request.TerrainId, request.DefenderRealm, request.DefenderTroops);
            float attackerMorale = Mathf.Clamp(request.AttackerMorale <= 0f ? 1f : request.AttackerMorale, 0.65f, 1.30f);
            float defenderMorale = Mathf.Clamp(request.DefenderMorale <= 0f ? 1f : request.DefenderMorale, 0.65f, 1.30f);

            int attackerPower = Mathf.Max(1, Mathf.RoundToInt(attackerBasePower * attackBonus * attackerCounter * attackerRealm * attackerTerrain * attackerMorale));
            int defenderPower = Mathf.Max(1, Mathf.RoundToInt(defenderBasePower * defenseBonus * defenderCounter * defenderRealm * defenderTerrain * defenderMorale));

            var roundReports = ResolveRounds(attackerPower, defenderPower, random, out float attackerDamageTaken, out float defenderDamageTaken);
            bool attackerWins = defenderDamageTaken / defenderPower >= attackerDamageTaken / attackerPower;
            int rounds = roundReports.Count;

            var attackerDetailedLosses = CalculateDetailedLosses(request.AttackerTroops, attackerDamageTaken, attackerPower, attackerWins);
            var defenderDetailedLosses = CalculateDetailedLosses(request.DefenderTroops, defenderDamageTaken, defenderPower, !attackerWins);
            int credits = CalculateWarzoneCredits(request.Type, attackerWins, rounds, defenderPower);

            var report = new BattleReport
            {
                IsWinner = attackerWins,
                Rounds = rounds,
                AttackerPower = attackerPower,
                DefenderPower = defenderPower,
                AttackerLosses = ToLegacyLosses(attackerDetailedLosses),
                DefenderLosses = ToLegacyLosses(defenderDetailedLosses),
                RoundReports = roundReports,
                AttackerDetailedLosses = attackerDetailedLosses,
                DefenderDetailedLosses = defenderDetailedLosses,
                WarzoneCreditsEarned = credits,
                Loot = BuildLoot(attackerWins, random),
                XpGained = attackerWins ? Mathf.Max(8, defenderPower / 18) : Mathf.Max(3, defenderPower / 36),
                ChampionContribution = "Commander bonus placeholder applied as baseline morale discipline.",
                RealmPerkContribution = $"Attacker realm x{attackerRealm:0.00}, defender realm x{defenderRealm:0.00}.",
                TerrainContribution = string.IsNullOrWhiteSpace(request.TerrainId)
                    ? "Neutral terrain."
                    : $"Terrain {request.TerrainId}: attacker x{attackerTerrain:0.00}, defender x{defenderTerrain:0.00}.",
                Summary = BuildSummary(attackerWins, rounds, attackerPower, defenderPower, credits)
            };

            if (attackerWins)
            {
                TryUpdateWinQuest();
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

        private float GetCounterMultiplier(List<TroopStack> source, List<TroopStack> target, BattleType battleType)
        {
            float bonus = 1.0f;

            int sourceInfantry = GetTotalCount(source, TroopType.Infantry);
            int sourceCavalry = GetTotalCount(source, TroopType.Cavalry);
            int sourceRanged = GetTotalCount(source, TroopType.Ranged);
            int sourceSiege = GetTotalCount(source, TroopType.Siege);

            int targetInfantry = GetTotalCount(target, TroopType.Infantry);
            int targetCavalry = GetTotalCount(target, TroopType.Cavalry);
            int targetRanged = GetTotalCount(target, TroopType.Ranged);

            if (sourceInfantry > 0 && targetCavalry > 0) bonus += 0.18f;
            if (sourceCavalry > 0 && targetRanged > 0) bonus += 0.18f;
            if (sourceRanged > 0 && targetInfantry > 0) bonus += 0.18f;
            if (sourceSiege > 0 && (battleType == BattleType.Boss || battleType == BattleType.Warzone)) bonus += 0.12f;

            return bonus;
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

        private List<BattleRoundReport> ResolveRounds(int attackerPower, int defenderPower, System.Random random, out float attackerDamageTaken, out float defenderDamageTaken)
        {
            var rounds = new List<BattleRoundReport>();
            float attackerRemaining = attackerPower;
            float defenderRemaining = defenderPower;
            attackerDamageTaken = 0f;
            defenderDamageTaken = 0f;

            for (int round = 1; round <= 20; round++)
            {
                float attackerRoll = 0.08f + (float)random.NextDouble() * 0.08f;
                float defenderRoll = 0.08f + (float)random.NextDouble() * 0.08f;
                float attackerDamage = Mathf.Max(1f, attackerRemaining * attackerRoll);
                float defenderDamage = Mathf.Max(1f, defenderRemaining * defenderRoll);

                defenderRemaining = Mathf.Max(0f, defenderRemaining - attackerDamage);
                attackerRemaining = Mathf.Max(0f, attackerRemaining - defenderDamage);
                defenderDamageTaken += attackerDamage;
                attackerDamageTaken += defenderDamage;

                rounds.Add(new BattleRoundReport
                {
                    Round = round,
                    AttackerDamage = attackerDamage,
                    DefenderDamage = defenderDamage,
                    Note = BuildRoundNote(round, attackerDamage, defenderDamage)
                });

                if (attackerRemaining <= 0f || defenderRemaining <= 0f)
                {
                    break;
                }
            }

            return rounds;
        }

        private List<TroopLossReport> CalculateDetailedLosses(List<TroopStack> original, float damageTaken, int ownPower, bool won)
        {
            var losses = new List<TroopLossReport>();
            if (original == null || original.Count == 0)
            {
                return losses;
            }

            float pressure = Mathf.Clamp01(damageTaken / Mathf.Max(1, ownPower));
            float casualtyRatio = won ? pressure * 0.38f : Mathf.Clamp01(pressure * 0.70f + 0.08f);

            foreach (var stack in original)
            {
                int count = Mathf.Max(0, stack.Count);
                float troopShare = ownPower <= 0 ? 0f : (count * GetTroopBasePower(stack.Type)) / (float)ownPower;
                float vulnerability = GetVulnerability(stack.Type);
                int affected = Mathf.Clamp(Mathf.RoundToInt(count * casualtyRatio * vulnerability), 0, count);
                int killed = Mathf.RoundToInt(affected * (won ? 0.35f : 0.55f));
                int wounded = affected - killed;

                losses.Add(new TroopLossReport
                {
                    Type = stack.Type,
                    Killed = killed,
                    Wounded = wounded,
                    Survived = Mathf.Max(0, count - killed - wounded),
                    DamageTaken = damageTaken * troopShare
                });
            }

            return losses;
        }

        private static List<TroopStack> ToLegacyLosses(List<TroopLossReport> detailedLosses)
        {
            var losses = new List<TroopStack>();
            foreach (var loss in detailedLosses)
            {
                losses.Add(new TroopStack
                {
                    Type = loss.Type,
                    Count = loss.Killed
                });
            }

            return losses;
        }

        private float TryGetResearchBonus(StatType statType)
        {
            try
            {
                return ServiceLocator.Get<IResearchService>().GetStatBonus(statType);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        private static float GetRealmMultiplier(RealmId realmId, List<TroopStack> troops, BattleType battleType, bool isAttacker)
        {
            if (realmId == RealmId.None)
            {
                return 1f;
            }

            return realmId switch
            {
                RealmId.Stonehold => 1f + (GetTotalCountStatic(troops, TroopType.Siege) > 0 ? 0.10f : 0.06f),
                RealmId.Eldergrove => 1f + (GetTotalCountStatic(troops, TroopType.Ranged) > 0 ? 0.10f : 0.05f),
                RealmId.Crownlands => 1.06f,
                RealmId.Umbral => 1f + (isAttacker || battleType == BattleType.PvP ? 0.09f : 0.04f),
                _ => 1f
            };
        }

        private static float GetTerrainMultiplier(string terrainId, RealmId realmId, List<TroopStack> troops)
        {
            if (string.IsNullOrWhiteSpace(terrainId))
            {
                return 1f;
            }

            string terrain = terrainId.ToLowerInvariant();
            if (terrain.Contains("mountain") || terrain.Contains("cave"))
            {
                return realmId == RealmId.Stonehold ? 1.08f : 1.0f;
            }

            if (terrain.Contains("forest"))
            {
                return realmId == RealmId.Eldergrove || GetTotalCountStatic(troops, TroopType.Ranged) > 0 ? 1.07f : 0.98f;
            }

            if (terrain.Contains("road") || terrain.Contains("field"))
            {
                return realmId == RealmId.Crownlands || GetTotalCountStatic(troops, TroopType.Cavalry) > 0 ? 1.05f : 1.0f;
            }

            if (terrain.Contains("volcanic") || terrain.Contains("shadow"))
            {
                return realmId == RealmId.Umbral ? 1.08f : 0.97f;
            }

            return 1f;
        }

        private static int GetTotalCountStatic(List<TroopStack> troops, TroopType type)
        {
            int total = 0;
            if (troops == null)
            {
                return 0;
            }

            foreach (var stack in troops)
            {
                if (stack.Type == type)
                {
                    total += stack.Count;
                }
            }

            return total;
        }

        private static float GetVulnerability(TroopType type)
        {
            return type switch
            {
                TroopType.Cavalry => 0.92f,
                TroopType.Ranged => 1.08f,
                TroopType.Siege => 1.18f,
                _ => 1f
            };
        }

        private static int CalculateWarzoneCredits(BattleType battleType, bool attackerWins, int rounds, int defenderPower)
        {
            if (battleType != BattleType.PvP && battleType != BattleType.Warzone && battleType != BattleType.Boss)
            {
                return 0;
            }

            int baseCredits = attackerWins ? 12 : 4;
            return baseCredits + Mathf.Clamp(defenderPower / 120, 0, 40) + Mathf.Clamp(rounds / 2, 0, 10);
        }

        private static List<ResourceData> BuildLoot(bool attackerWins, System.Random random)
        {
            var loot = new List<ResourceData>();
            if (!attackerWins)
            {
                return loot;
            }

            loot.Add(new ResourceData { Type = ResourceType.Food, Amount = 40 + random.Next(0, 26) });
            loot.Add(new ResourceData { Type = ResourceType.Gold, Amount = 12 + random.Next(0, 14) });
            return loot;
        }

        private static string BuildRoundNote(int round, float attackerDamage, float defenderDamage)
        {
            if (attackerDamage > defenderDamage * 1.2f)
            {
                return $"Round {round}: attacker pressure broke the line.";
            }

            if (defenderDamage > attackerDamage * 1.2f)
            {
                return $"Round {round}: defender counterattack landed hard.";
            }

            return $"Round {round}: both armies traded evenly.";
        }

        private static string BuildSummary(bool attackerWins, int rounds, int attackerPower, int defenderPower, int warzoneCredits)
        {
            string outcome = attackerWins ? "Victory" : "Defeat";
            string credits = warzoneCredits > 0 ? $" Earned {warzoneCredits} Warzone Credits." : string.Empty;
            return $"{outcome} after {rounds} rounds. Attacker power {attackerPower}, defender power {defenderPower}.{credits}";
        }

        private static void TryUpdateWinQuest()
        {
            try
            {
                ServiceLocator.Get<IQuestService>().UpdateProgress(QuestType.WinBattle, 1);
            }
            catch (Exception)
            {
                // Battle simulation can run in isolated tests without quest services.
            }
        }
    }
}
