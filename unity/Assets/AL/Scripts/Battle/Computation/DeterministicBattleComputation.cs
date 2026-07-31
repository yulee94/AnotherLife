using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AL.Battle.Contracts;
using AL.Battle.Validation;

namespace AL.Battle.Computation
{
    public static class DeterministicBattleComputation
    {
        public static BattleComputationResult Compute(BattleComputationRequest request)
        {
            try
            {
                BattleValidationResult validation = BattleRequestValidator.Validate(request);
                if (!validation.IsValid)
                    return Failure(validation.Status, validation.Diagnostics);

                var definitions = request.Catalog.TroopDefinitions.ToDictionary(
                    item => item.Identity.Id,
                    item => item,
                    StringComparer.Ordinal);
                var contributions = new List<BattleContribution>();
                long attackerPower = ComputeArmyPower(
                    request.AttackerArmy,
                    request.Opponent.Army,
                    request.Context.AttackerRealm,
                    request.Context.TerrainProfile,
                    request.Context.AttackerModifiers,
                    request.BattleKind,
                    true,
                    request.Rules,
                    definitions,
                    "attacker",
                    contributions);
                long opponentPower = request.Opponent.Kind == BattleOpponentKind.Boss
                    ? ComputeBossPower(request, contributions)
                    : ComputeArmyPower(
                        request.Opponent.Army,
                        request.AttackerArmy,
                        request.Context.OpponentRealm,
                        request.Context.TerrainProfile,
                        request.Context.OpponentModifiers,
                        request.BattleKind,
                        false,
                        request.Rules,
                        definitions,
                        "opponent",
                        contributions);

                if (!IsFinalPowerValid(attackerPower) || !IsFinalPowerValid(opponentPower))
                    return Failure(
                        BattleComputationStatus.ArithmeticOverflow,
                        One("AL-BATTLE-POWER-LIMIT", "result.power"));

                List<BattleRoundResult> rounds = ResolveRounds(
                    request,
                    attackerPower,
                    opponentPower,
                    out long attackerDamageTaken,
                    out long opponentDamageTaken);
                bool attackerWins = BattleFixedPoint.RatioAtLeast(
                    opponentDamageTaken,
                    checked(opponentPower * BattleTechnicalLimits.MicrosPerUnit),
                    attackerDamageTaken,
                    checked(attackerPower * BattleTechnicalLimits.MicrosPerUnit));
                BattleOutcome outcome = attackerWins
                    ? BattleOutcome.AttackerVictory
                    : BattleOutcome.OpponentVictory;
                string outcomeTechnicalId = OutcomeTechnicalId(
                    attackerDamageTaken,
                    opponentDamageTaken,
                    attackerPower,
                    opponentPower);

                List<BattleTroopLoss> attackerLosses = CalculateLosses(
                    request.AttackerArmy,
                    attackerDamageTaken,
                    attackerPower,
                    attackerWins,
                    request.Rules,
                    definitions);
                List<BattleTroopLoss> opponentLosses = request.Opponent.Kind == BattleOpponentKind.Boss
                    ? new List<BattleTroopLoss>()
                    : CalculateLosses(
                        request.Opponent.Army,
                        opponentDamageTaken,
                        opponentPower,
                        !attackerWins,
                        request.Rules,
                        definitions);
                BattleRewardProposal rewards = ComputeRewards(
                    request,
                    attackerWins,
                    opponentPower,
                    rounds.Count);

                var provisional = NewResult(
                    request,
                    outcome,
                    outcomeTechnicalId,
                    attackerPower,
                    opponentPower,
                    rounds,
                    attackerLosses,
                    opponentLosses,
                    rewards,
                    contributions,
                    string.Empty);
                string computationHash = BattleCanonicalHash.Result(provisional);
                BattleComputedResult value = NewResult(
                    request,
                    outcome,
                    outcomeTechnicalId,
                    attackerPower,
                    opponentPower,
                    rounds,
                    attackerLosses,
                    opponentLosses,
                    rewards,
                    contributions,
                    computationHash);
                if (!ValidateComputedResult(request, value))
                    return Failure(
                        BattleComputationStatus.InternalInvariantFailure,
                        One("AL-BATTLE-RESULT-INVARIANT", "result"));

                BattleComputationStatus status = request.ExecutionMode == BattleExecutionMode.Preview
                    ? BattleComputationStatus.ComputedPreview
                    : BattleComputationStatus.Computed;
                return new BattleComputationResult(status, value, Array.Empty<BattleDiagnostic>());
            }
            catch (OverflowException)
            {
                return Failure(
                    BattleComputationStatus.ArithmeticOverflow,
                    One("AL-BATTLE-ARITHMETIC-OVERFLOW", "request"));
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return Failure(
                    BattleComputationStatus.DeterminismFailure,
                    One("AL-BATTLE-DETERMINISM-FAILURE", "request.determinismVersion"));
            }
        }

        private static long ComputeArmyPower(
            BattleArmySnapshot army,
            BattleArmySnapshot opposingArmy,
            BattleRealm realm,
            BattleTerrainProfile terrain,
            BattleModifierSnapshot modifier,
            BattleKind kind,
            bool isAttacker,
            BattleRulesProfile rules,
            IReadOnlyDictionary<string, BattleTroopDefinition> definitions,
            string contributionPrefix,
            List<BattleContribution> contributions)
        {
            long basePower = 0;
            for (int index = 0; index < army.Stacks.Count; index++)
            {
                BattleTroopStack stack = army.Stacks[index];
                BattleTroopDefinition definition = definitions[stack.TroopDefinitionId];
                basePower = checked(basePower + checked(stack.Count * definition.BasePower));
            }

            long counter = CounterMultiplier(army, opposingArmy, kind, rules, definitions);
            long realmMultiplier = RealmMultiplier(army, realm, kind, isAttacker, definitions);
            long terrainMultiplier = TerrainMultiplier(army, realm, terrain, definitions);
            var multipliers = new[]
            {
                counter,
                realmMultiplier,
                terrainMultiplier,
                modifier.ResearchMicros,
                modifier.CommanderMicros,
                modifier.MoraleMicros
            };
            long finalPower = BattleFixedPoint.MultiplyAndRoundOnce(basePower, multipliers);
            contributions.Add(new BattleContribution(
                contributionPrefix + ".base_power",
                checked(basePower * BattleTechnicalLimits.MicrosPerUnit)));
            contributions.Add(new BattleContribution(contributionPrefix + ".counter", counter));
            contributions.Add(new BattleContribution(contributionPrefix + ".realm", realmMultiplier));
            contributions.Add(new BattleContribution(contributionPrefix + ".terrain", terrainMultiplier));
            contributions.Add(new BattleContribution(
                contributionPrefix + ".research",
                modifier.ResearchMicros));
            contributions.Add(new BattleContribution(
                contributionPrefix + ".commander",
                modifier.CommanderMicros));
            contributions.Add(new BattleContribution(
                contributionPrefix + ".morale",
                modifier.MoraleMicros));
            return finalPower;
        }

        private static long ComputeBossPower(
            BattleComputationRequest request,
            List<BattleContribution> contributions)
        {
            long finalPower = BattleFixedPoint.MultiplyAndRoundOnce(
                request.Opponent.BossPower,
                new[]
                {
                    request.Context.OpponentModifiers.ResearchMicros,
                    request.Context.OpponentModifiers.CommanderMicros,
                    request.Context.OpponentModifiers.MoraleMicros
                });
            contributions.Add(new BattleContribution(
                "opponent.base_power",
                checked(request.Opponent.BossPower * BattleTechnicalLimits.MicrosPerUnit)));
            contributions.Add(new BattleContribution(
                "opponent.research",
                request.Context.OpponentModifiers.ResearchMicros));
            contributions.Add(new BattleContribution(
                "opponent.commander",
                request.Context.OpponentModifiers.CommanderMicros));
            contributions.Add(new BattleContribution(
                "opponent.morale",
                request.Context.OpponentModifiers.MoraleMicros));
            return finalPower;
        }

        private static long CounterMultiplier(
            BattleArmySnapshot source,
            BattleArmySnapshot target,
            BattleKind kind,
            BattleRulesProfile rules,
            IReadOnlyDictionary<string, BattleTroopDefinition> definitions)
        {
            long multiplier = BattleTechnicalLimits.MicrosPerUnit;
            if (target != null)
            {
                if (Has(source, BattleTroopArchetype.Infantry, definitions) &&
                    Has(target, BattleTroopArchetype.Cavalry, definitions))
                    multiplier = checked(multiplier + rules.CounterMultiplierMicros);
                if (Has(source, BattleTroopArchetype.Cavalry, definitions) &&
                    Has(target, BattleTroopArchetype.Ranged, definitions))
                    multiplier = checked(multiplier + rules.CounterMultiplierMicros);
                if (Has(source, BattleTroopArchetype.Ranged, definitions) &&
                    Has(target, BattleTroopArchetype.Infantry, definitions))
                    multiplier = checked(multiplier + rules.CounterMultiplierMicros);
            }

            if (Has(source, BattleTroopArchetype.Siege, definitions) &&
                (kind == BattleKind.Boss || kind == BattleKind.Warzone))
                multiplier = checked(multiplier + rules.BossSiegeMultiplierMicros);
            return multiplier;
        }

        private static long RealmMultiplier(
            BattleArmySnapshot army,
            BattleRealm realm,
            BattleKind kind,
            bool isAttacker,
            IReadOnlyDictionary<string, BattleTroopDefinition> definitions)
        {
            switch (realm)
            {
                case BattleRealm.Stonehold:
                    return Has(army, BattleTroopArchetype.Siege, definitions) ? 1_100_000L : 1_060_000L;
                case BattleRealm.Eldergrove:
                    return Has(army, BattleTroopArchetype.Ranged, definitions) ? 1_100_000L : 1_050_000L;
                case BattleRealm.Crownlands:
                    return 1_060_000L;
                case BattleRealm.Umbral:
                    return isAttacker || kind == BattleKind.Pvp ? 1_090_000L : 1_040_000L;
                default:
                    return BattleTechnicalLimits.MicrosPerUnit;
            }
        }

        private static long TerrainMultiplier(
            BattleArmySnapshot army,
            BattleRealm realm,
            BattleTerrainProfile terrain,
            IReadOnlyDictionary<string, BattleTroopDefinition> definitions)
        {
            switch (terrain)
            {
                case BattleTerrainProfile.MountainCave:
                    return realm == BattleRealm.Stonehold ? 1_080_000L : 1_000_000L;
                case BattleTerrainProfile.Forest:
                    return realm == BattleRealm.Eldergrove ||
                           Has(army, BattleTroopArchetype.Ranged, definitions)
                        ? 1_070_000L
                        : 980_000L;
                case BattleTerrainProfile.RoadField:
                    return realm == BattleRealm.Crownlands ||
                           Has(army, BattleTroopArchetype.Cavalry, definitions)
                        ? 1_050_000L
                        : 1_000_000L;
                case BattleTerrainProfile.VolcanicShadow:
                    return realm == BattleRealm.Umbral ? 1_080_000L : 970_000L;
                default:
                    return 1_000_000L;
            }
        }

        private static List<BattleRoundResult> ResolveRounds(
            BattleComputationRequest request,
            long attackerPower,
            long opponentPower,
            out long attackerDamageTaken,
            out long opponentDamageTaken)
        {
            long attackerRemaining = checked(attackerPower * BattleTechnicalLimits.MicrosPerUnit);
            long opponentRemaining = checked(opponentPower * BattleTechnicalLimits.MicrosPerUnit);
            attackerDamageTaken = 0;
            opponentDamageTaken = 0;
            var rounds = new List<BattleRoundResult>(request.Rules.MaximumRounds);

            for (int roundIndex = 1; roundIndex <= request.Rules.MaximumRounds; roundIndex++)
            {
                string roundNamespace = roundIndex.ToString(CultureInfo.InvariantCulture);
                long attackerRate = BattleFixedPoint.MapUInt32(
                    BattleDeterminism.DrawUInt32(
                        request,
                        "round." + roundNamespace + ".attacker_damage_rate",
                        roundIndex),
                    request.Rules.MinimumDamageRateMicros,
                    request.Rules.MaximumDamageRateExclusiveMicros);
                long opponentRate = BattleFixedPoint.MapUInt32(
                    BattleDeterminism.DrawUInt32(
                        request,
                        "round." + roundNamespace + ".defender_damage_rate",
                        roundIndex),
                    request.Rules.MinimumDamageRateMicros,
                    request.Rules.MaximumDamageRateExclusiveMicros);
                long proposedToOpponent = Math.Max(
                    BattleTechnicalLimits.MicrosPerUnit,
                    BattleFixedPoint.MultiplyAndRound(attackerRemaining, attackerRate));
                long proposedToAttacker = Math.Max(
                    BattleTechnicalLimits.MicrosPerUnit,
                    BattleFixedPoint.MultiplyAndRound(opponentRemaining, opponentRate));
                long damageToOpponent = Math.Min(opponentRemaining, proposedToOpponent);
                long damageToAttacker = Math.Min(attackerRemaining, proposedToAttacker);

                long nextAttacker = checked(attackerRemaining - damageToAttacker);
                long nextOpponent = checked(opponentRemaining - damageToOpponent);
                attackerDamageTaken = checked(attackerDamageTaken + damageToAttacker);
                opponentDamageTaken = checked(opponentDamageTaken + damageToOpponent);
                rounds.Add(new BattleRoundResult(
                    roundIndex,
                    attackerRate,
                    opponentRate,
                    damageToOpponent,
                    damageToAttacker,
                    nextAttacker,
                    nextOpponent));
                attackerRemaining = nextAttacker;
                opponentRemaining = nextOpponent;
                if (attackerRemaining == 0 || opponentRemaining == 0)
                    break;
            }

            return rounds;
        }

        private static List<BattleTroopLoss> CalculateLosses(
            BattleArmySnapshot army,
            long damageTakenMicros,
            long ownPower,
            bool won,
            BattleRulesProfile rules,
            IReadOnlyDictionary<string, BattleTroopDefinition> definitions)
        {
            long startingPowerMicros = checked(ownPower * BattleTechnicalLimits.MicrosPerUnit);
            long pressureMicros = Math.Min(
                BattleTechnicalLimits.MicrosPerUnit,
                BattleFixedPoint.RatioMicros(damageTakenMicros, startingPowerMicros));
            long casualtyRatio = won
                ? BattleFixedPoint.MultiplyAndRound(
                    pressureMicros,
                    rules.WinnerCasualtyPressureMicros)
                : Math.Min(
                    BattleTechnicalLimits.MicrosPerUnit,
                    checked(BattleFixedPoint.MultiplyAndRound(
                        pressureMicros,
                        rules.LoserCasualtyPressureMicros) + rules.LoserCasualtyFloorMicros));
            long killedShare = won
                ? rules.WinnerKilledShareMicros
                : rules.LoserKilledShareMicros;
            var losses = new List<BattleTroopLoss>(army.Stacks.Count);
            for (int index = 0; index < army.Stacks.Count; index++)
            {
                BattleTroopStack stack = army.Stacks[index];
                BattleTroopArchetype archetype = definitions[stack.TroopDefinitionId].Archetype;
                long vulnerability = Vulnerability(archetype, rules);
                long affected = Math.Min(
                    stack.Count,
                    BattleFixedPoint.MultiplyAndRoundOnce(
                        stack.Count,
                        new[] { casualtyRatio, vulnerability }));
                long killed = Math.Min(
                    affected,
                    BattleFixedPoint.MultiplyAndRound(affected, killedShare));
                long wounded = checked(affected - killed);
                long survived = checked(stack.Count - affected);
                losses.Add(new BattleTroopLoss(
                    stack.TroopDefinitionId,
                    killed,
                    wounded,
                    survived));
            }

            return losses;
        }

        private static BattleRewardProposal ComputeRewards(
            BattleComputationRequest request,
            bool attackerWins,
            long opponentPower,
            int roundCount)
        {
            BattleRewardProfile profile = request.Rewards;
            bool creditBattle = request.BattleKind == BattleKind.Pvp ||
                                request.BattleKind == BattleKind.Warzone ||
                                request.BattleKind == BattleKind.Boss;
            int credits = 0;
            if (creditBattle)
            {
                int powerPart = Math.Min(
                    profile.PowerCreditMaximum,
                    checked((int)(opponentPower / profile.PowerCreditDivisor)));
                int roundsPart = Math.Min(
                    profile.RoundsCreditMaximum,
                    roundCount / profile.RoundsCreditDivisor);
                credits = checked(
                    (attackerWins ? profile.WinCreditsBase : profile.LossCreditsBase) +
                    powerPart + roundsPart);
            }

            int food = 0;
            int gold = 0;
            if (attackerWins)
            {
                food = checked(profile.WinFoodBase + (int)BattleFixedPoint.MapUInt32(
                    BattleDeterminism.DrawUInt32(request, "reward.food_amount", 0),
                    0,
                    profile.WinFoodRandomExclusive));
                gold = checked(profile.WinGoldBase + (int)BattleFixedPoint.MapUInt32(
                    BattleDeterminism.DrawUInt32(request, "reward.gold_amount", 0),
                    0,
                    profile.WinGoldRandomExclusive));
            }

            int experience = attackerWins
                ? Math.Max(profile.WinXpMinimum, checked((int)(opponentPower / profile.WinXpDivisor)))
                : Math.Max(profile.LossXpMinimum, checked((int)(opponentPower / profile.LossXpDivisor)));
            return new BattleRewardProposal(credits, food, gold, experience);
        }

        private static BattleComputedResult NewResult(
            BattleComputationRequest request,
            BattleOutcome outcome,
            string outcomeTechnicalId,
            long attackerPower,
            long opponentPower,
            IEnumerable<BattleRoundResult> rounds,
            IEnumerable<BattleTroopLoss> attackerLosses,
            IEnumerable<BattleTroopLoss> opponentLosses,
            BattleRewardProposal rewards,
            IEnumerable<BattleContribution> contributions,
            string computationHash)
        {
            return new BattleComputedResult(
                request.GameId,
                request.CatalogSetId,
                request.ProfileId,
                request.BattleRequestId,
                request.BattleId,
                request.BattleResultId,
                request.ExpectedResultConsumerId,
                request.ExecutionMode,
                request.BattleKind,
                request.BattleTypeId,
                outcome,
                outcomeTechnicalId,
                attackerPower,
                opponentPower,
                rounds,
                attackerLosses,
                opponentLosses,
                rewards,
                contributions,
                request.Catalog.Identity.Sha256,
                request.AttackerArmy.Identity.Sha256,
                BattleDeterminism.OpponentSha256(request.Opponent),
                request.Context.Identity.Sha256,
                request.Rules.Identity.Sha256,
                request.Rewards.Identity.Sha256,
                request.DeterminismVersion,
                request.SeedHex,
                computationHash);
        }

        private static bool ValidateComputedResult(
            BattleComputationRequest request,
            BattleComputedResult result)
        {
            if (!IsFinalPowerValid(result.AttackerPower) ||
                !IsFinalPowerValid(result.OpponentPower) ||
                result.Rounds.Count == 0 ||
                result.Rounds.Count > request.Rules.MaximumRounds ||
                result.RewardProposal == null ||
                !BattleRequestValidator.IsLowerSha256(result.ComputationSha256) ||
                !string.Equals(
                    BattleCanonicalHash.Result(result),
                    result.ComputationSha256,
                    StringComparison.Ordinal))
                return false;

            for (int index = 0; index < result.Rounds.Count; index++)
            {
                BattleRoundResult round = result.Rounds[index];
                if (round.RoundIndex != index + 1 ||
                    round.DamageToAttackerMicros < 0 ||
                    round.DamageToOpponentMicros < 0 ||
                    round.AttackerRemainingPowerMicros < 0 ||
                    round.OpponentRemainingPowerMicros < 0)
                    return false;
            }

            if (!LossesMatchArmy(result.AttackerLosses, request.AttackerArmy))
                return false;
            if (request.Opponent.Kind == BattleOpponentKind.Boss)
                return result.OpponentLosses.Count == 0;
            return LossesMatchArmy(result.OpponentLosses, request.Opponent.Army);
        }

        private static bool LossesMatchArmy(
            IReadOnlyList<BattleTroopLoss> losses,
            BattleArmySnapshot army)
        {
            if (losses.Count != army.Stacks.Count)
                return false;
            for (int index = 0; index < losses.Count; index++)
            {
                BattleTroopLoss loss = losses[index];
                BattleTroopStack stack = army.Stacks[index];
                if (!string.Equals(
                        loss.TroopDefinitionId,
                        stack.TroopDefinitionId,
                        StringComparison.Ordinal) ||
                    loss.Killed < 0 || loss.Wounded < 0 || loss.Survived < 0 ||
                    checked(loss.Killed + loss.Wounded + loss.Survived) != stack.Count)
                    return false;
            }

            return true;
        }

        private static string OutcomeTechnicalId(
            long attackerDamageTaken,
            long opponentDamageTaken,
            long attackerPower,
            long opponentPower)
        {
            bool attackerAtLeast = BattleFixedPoint.RatioAtLeast(
                opponentDamageTaken,
                checked(opponentPower * BattleTechnicalLimits.MicrosPerUnit),
                attackerDamageTaken,
                checked(attackerPower * BattleTechnicalLimits.MicrosPerUnit));
            bool opponentAtLeast = BattleFixedPoint.RatioAtLeast(
                attackerDamageTaken,
                checked(attackerPower * BattleTechnicalLimits.MicrosPerUnit),
                opponentDamageTaken,
                checked(opponentPower * BattleTechnicalLimits.MicrosPerUnit));
            if (attackerAtLeast && opponentAtLeast)
                return "even_trade";
            return attackerAtLeast ? "attacker_pressure" : "defender_pressure";
        }

        private static long Vulnerability(
            BattleTroopArchetype archetype,
            BattleRulesProfile rules)
        {
            switch (archetype)
            {
                case BattleTroopArchetype.Infantry: return rules.InfantryVulnerabilityMicros;
                case BattleTroopArchetype.Cavalry: return rules.CavalryVulnerabilityMicros;
                case BattleTroopArchetype.Ranged: return rules.RangedVulnerabilityMicros;
                case BattleTroopArchetype.Siege: return rules.SiegeVulnerabilityMicros;
                default: throw new ArgumentOutOfRangeException(nameof(archetype));
            }
        }

        private static bool Has(
            BattleArmySnapshot army,
            BattleTroopArchetype archetype,
            IReadOnlyDictionary<string, BattleTroopDefinition> definitions)
        {
            if (army == null)
                return false;
            for (int index = 0; index < army.Stacks.Count; index++)
            {
                if (definitions[army.Stacks[index].TroopDefinitionId].Archetype == archetype)
                    return true;
            }

            return false;
        }

        private static bool IsFinalPowerValid(long value)
        {
            return value > 0 && value <= BattleTechnicalLimits.MaximumFinalPower;
        }

        private static BattleComputationResult Failure(
            BattleComputationStatus status,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            return new BattleComputationResult(status, null, diagnostics);
        }

        private static BattleDiagnostic[] One(string code, string path)
        {
            return new[] { new BattleDiagnostic(code, path) };
        }
    }
}
