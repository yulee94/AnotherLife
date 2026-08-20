using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AL.Battle.Computation;
using AL.Battle.Contracts;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Battle.Simulator
{
    /// <summary>
    /// Compatibility adapter that routes the legacy <see cref="IBattleSimulator"/>
    /// contract through the deterministic fixed-point engine
    /// (<see cref="DeterministicBattleComputation"/>). Combat outcomes are resolved
    /// with checked fixed-point arithmetic and SHA-256 entropy; no floating-point
    /// arithmetic or <see cref="System.Random"/> participates in the outcome.
    ///
    /// All live inputs (research) are snapshotted before computation. Any request
    /// that fails validation fails closed: no fallback to the retired float
    /// simulator is ever performed.
    /// </summary>
    public sealed class FixedPointBattleSimulator : IBattleSimulator
    {
        private const string GameId = "another_life";
        private const string CatalogSetId = "catalog.battle.runtime";
        private const string ContentVersion = "battle_content_v1";
        private const string SourceRevision = "fixed_point_adapter_v1";
        private const long NeutralMicros = 1_000_000L;

        public BattleReport Simulate(BattleRequest request)
        {
            request = request ?? new BattleRequest();
            try
            {
                BattleComputationRequest computation = BuildRequest(request);
                BattleComputationResult result = DeterministicBattleComputation.Compute(computation);
                if (!result.IsSuccess)
                {
                    return FailureReport(result);
                }

                return MapReport(result.Value);
            }
            catch (Exception exception)
            {
                // Fail closed. The retired float simulator is never re-entered.
                var report = new BattleReport
                {
                    IsWinner = false,
                    Summary = "Battle simulation failed: " + exception.Message,
                    ChampionContribution =
                        "Deterministic engine rejected the request; no legacy fallback was used.",
                    AttackerLosses = new List<TroopStack>(),
                    DefenderLosses = new List<TroopStack>(),
                    RoundReports = new List<BattleRoundReport>(),
                    AttackerDetailedLosses = new List<TroopLossReport>(),
                    DefenderDetailedLosses = new List<TroopLossReport>(),
                    Loot = new List<ResourceData>()
                };
                return report;
            }
        }

        private BattleComputationRequest BuildRequest(BattleRequest request)
        {
            BattleCatalogSnapshot catalog = BattleMigrationProfiles.CreateCatalog(
                GameId,
                CatalogSetId,
                ContentVersion,
                SourceRevision);
            BattleArmySnapshot attacker = BuildArmy(catalog, "army.attacker", request.AttackerTroops);
            BattleOpponentSnapshot opponent = BuildOpponent(catalog, request);

            BattleKind kind = MapKind(request.Type);
            BattleRealm attackerRealm = MapRealm(request.AttackerRealm);
            BattleRealm opponentRealm = MapRealm(request.DefenderRealm);

            BattleModifierSnapshot attackerModifiers = BattleMigrationProfiles.CreateModifier(
                "modifier.attacker",
                CatalogSetId,
                ContentVersion,
                SourceRevision,
                MapMoraleMicros(request.AttackerMorale),
                MapResearchMicros(StatType.Attack),
                NeutralMicros);
            BattleModifierSnapshot opponentModifiers = opponent.Kind == BattleOpponentKind.Army
                ? BattleMigrationProfiles.CreateModifier(
                    "modifier.opponent",
                    CatalogSetId,
                    ContentVersion,
                    SourceRevision,
                    MapMoraleMicros(request.DefenderMorale),
                    MapResearchMicros(StatType.Defense),
                    NeutralMicros)
                : BattleMigrationProfiles.CreateModifier(
                    "modifier.opponent.boss",
                    CatalogSetId,
                    ContentVersion,
                    SourceRevision,
                    NeutralMicros,
                    NeutralMicros,
                    NeutralMicros);

            BattleRealm contextOpponentRealm =
                opponent.Kind == BattleOpponentKind.Boss ? BattleRealm.Neutral : opponentRealm;
            BattleRealmContextKind contextKind = ResolveContextKind(kind, contextOpponentRealm);

            BattleContextSnapshot context = BattleMigrationProfiles.CreateContext(
                "context.runtime",
                CatalogSetId,
                ContentVersion,
                SourceRevision,
                "encounter.runtime",
                contextKind,
                attackerRealm,
                contextOpponentRealm,
                MapTerrain(request.TerrainId),
                attackerModifiers,
                opponentModifiers);

            return new BattleComputationRequest(
                GameId,
                CatalogSetId,
                "profile.runtime",
                "request.runtime",
                "battle.runtime",
                "result.runtime",
                BattleTechnicalLimits.ExpectedResultConsumerId,
                BattleExecutionMode.Authoritative,
                kind,
                MapTypeId(kind),
                BattleTechnicalLimits.SupportedDeterminismVersion,
                DeriveSeedHex(request.RandomSeed),
                catalog,
                attacker,
                opponent,
                context,
                BattleMigrationProfiles.CreateRules(CatalogSetId, ContentVersion, SourceRevision),
                BattleMigrationProfiles.CreateRewards(CatalogSetId, ContentVersion, SourceRevision));
        }

        private BattleArmySnapshot BuildArmy(
            BattleCatalogSnapshot catalog,
            string id,
            List<TroopStack> troops)
        {
            // Canonical, sorted, duplicate-merged troop stacks.
            var merged = new SortedDictionary<string, long>(StringComparer.Ordinal);
            if (troops != null)
            {
                for (int index = 0; index < troops.Count; index++)
                {
                    TroopStack stack = troops[index];
                    if (stack == null || stack.Count <= 0)
                    {
                        continue;
                    }

                    string troopId = TroopId(stack.Type);
                    if (merged.TryGetValue(troopId, out long existing))
                    {
                        merged[troopId] = checked(existing + stack.Count);
                    }
                    else
                    {
                        merged.Add(troopId, stack.Count);
                    }
                }
            }

            var counts = merged
                .Select(pair => new KeyValuePair<string, long>(pair.Key, pair.Value))
                .ToList();
            return BattleMigrationProfiles.CreateArmy(
                id,
                CatalogSetId,
                ContentVersion,
                SourceRevision,
                catalog,
                counts);
        }

        private BattleOpponentSnapshot BuildOpponent(
            BattleCatalogSnapshot catalog,
            BattleRequest request)
        {
            if (request.Type != BattleType.Boss)
            {
                return new BattleOpponentSnapshot(
                    BattleOpponentKind.Army,
                    BuildArmy(catalog, "army.opponent", request.DefenderTroops),
                    null,
                    0L);
            }

            // The legacy request carried no boss power; the retired simulator
            // treated the defender troops as the boss force. We migrate that
            // shape to an explicit boss whose power is the defender army's base
            // power, preserving the boss battle kind and its rules/rewards.
            long bossPower = BasePower(request.DefenderTroops);
            var identity = new BattleSnapshotIdentity(
                "boss.runtime",
                BattleTechnicalLimits.SupportedSchemaVersion,
                ContentVersion,
                SourceRevision,
                new string('0', 64),
                CatalogSetId);
            BattleSnapshotIdentity bossIdentity = identity.WithSha256(
                BattleCanonicalHash.Boss(identity, bossPower));
            return new BattleOpponentSnapshot(
                BattleOpponentKind.Boss,
                null,
                bossIdentity,
                bossPower);
        }

        private static long BasePower(List<TroopStack> troops)
        {
            long power = 0L;
            if (troops == null)
            {
                return power;
            }

            for (int index = 0; index < troops.Count; index++)
            {
                TroopStack stack = troops[index];
                if (stack == null || stack.Count <= 0)
                {
                    continue;
                }

                power = checked(power + checked(stack.Count * BasePower(stack.Type)));
            }

            return power;
        }

        private static int BasePower(TroopType type)
        {
            return type switch
            {
                TroopType.Infantry => 10,
                TroopType.Cavalry => 15,
                TroopType.Ranged => 12,
                TroopType.Siege => 20,
                _ => 10
            };
        }

        private static BattleKind MapKind(BattleType type)
        {
            return type switch
            {
                BattleType.PvE => BattleKind.Pve,
                BattleType.PvP => BattleKind.Pvp,
                BattleType.Boss => BattleKind.Boss,
                BattleType.Warzone => BattleKind.Warzone,
                _ => BattleKind.Pve
            };
        }

        private static string MapTypeId(BattleKind kind)
        {
            return kind switch
            {
                BattleKind.Pve => BattleTechnicalLimits.PveBattleTypeId,
                BattleKind.Pvp => BattleTechnicalLimits.PvpBattleTypeId,
                BattleKind.Boss => BattleTechnicalLimits.BossBattleTypeId,
                BattleKind.Warzone => BattleTechnicalLimits.WarzoneBattleTypeId,
                _ => BattleTechnicalLimits.PveBattleTypeId
            };
        }

        private static BattleRealm MapRealm(RealmId realm)
        {
            return realm switch
            {
                RealmId.Stonehold => BattleRealm.Stonehold,
                RealmId.Eldergrove => BattleRealm.Eldergrove,
                RealmId.Crownlands => BattleRealm.Crownlands,
                RealmId.Umbral => BattleRealm.Umbral,
                _ => BattleRealm.Neutral
            };
        }

        private static BattleRealmContextKind ResolveContextKind(
            BattleKind kind,
            BattleRealm opponentRealm)
        {
            if (kind == BattleKind.Boss)
            {
                return BattleRealmContextKind.BossEncounter;
            }

            if (kind == BattleKind.Pve ||
                (kind == BattleKind.Warzone && opponentRealm == BattleRealm.Neutral))
            {
                return BattleRealmContextKind.NeutralEncounter;
            }

            return BattleRealmContextKind.RealmVersusRealm;
        }

        private static BattleTerrainProfile MapTerrain(string terrainId)
        {
            if (string.IsNullOrWhiteSpace(terrainId))
            {
                return BattleTerrainProfile.Neutral;
            }

            string terrain = terrainId.ToLowerInvariant();
            if (terrain.Contains("mountain") || terrain.Contains("cave"))
            {
                return BattleTerrainProfile.MountainCave;
            }

            if (terrain.Contains("forest"))
            {
                return BattleTerrainProfile.Forest;
            }

            if (terrain.Contains("road") || terrain.Contains("field"))
            {
                return BattleTerrainProfile.RoadField;
            }

            if (terrain.Contains("volcanic") || terrain.Contains("shadow"))
            {
                return BattleTerrainProfile.VolcanicShadow;
            }

            return BattleTerrainProfile.Neutral;
        }

        private static long MapMoraleMicros(float morale)
        {
            float effective = morale <= 0f ? 1f : morale;
            effective = Math.Max(0.65f, Math.Min(1.30f, effective));
            return (long)Math.Round((double)effective * 1_000_000.0, MidpointRounding.ToEven);
        }

        private static long MapResearchMicros(StatType statType)
        {
            float bonus = 0f;
            if (ServiceLocator.TryGet<IResearchService>(out IResearchService research))
            {
                bonus = research.GetStatBonus(statType);
            }

            double multiplier = Math.Max(0.5, Math.Min(2.0, 1.0 + bonus));
            return (long)Math.Round(multiplier * 1_000_000.0, MidpointRounding.ToEven);
        }

        private static string DeriveSeedHex(int randomSeed)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(
                randomSeed.ToString(CultureInfo.InvariantCulture));
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(bytes));
            }
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string TroopId(TroopType type)
        {
            return type switch
            {
                TroopType.Cavalry => "troop.cavalry",
                TroopType.Infantry => "troop.infantry",
                TroopType.Ranged => "troop.ranged",
                TroopType.Siege => "troop.siege",
                _ => "troop.infantry"
            };
        }

        private static TroopType TroopTypeFromId(string troopDefinitionId)
        {
            switch (troopDefinitionId)
            {
                case "troop.cavalry": return TroopType.Cavalry;
                case "troop.ranged": return TroopType.Ranged;
                case "troop.siege": return TroopType.Siege;
                default: return TroopType.Infantry;
            }
        }

        private static BattleReport MapReport(BattleComputedResult result)
        {
            return new BattleReport
            {
                IsWinner = result.Outcome == BattleOutcome.AttackerVictory,
                Rounds = result.Rounds.Count,
                AttackerPower = ToInt(result.AttackerPower),
                DefenderPower = ToInt(result.OpponentPower),
                AttackerLosses = ToLegacyLosses(result.AttackerLosses),
                DefenderLosses = ToLegacyLosses(result.OpponentLosses),
                RoundReports = MapRounds(result.Rounds),
                AttackerDetailedLosses = MapDetailedLosses(result.AttackerLosses),
                DefenderDetailedLosses = MapDetailedLosses(result.OpponentLosses),
                WarzoneCreditsEarned = result.RewardProposal?.Credits ?? 0,
                Loot = MapLoot(result.RewardProposal),
                XpGained = result.RewardProposal?.Experience ?? 0,
                ChampionContribution =
                    "Commander bonus captured in the deterministic modifier snapshot.",
                RealmPerkContribution =
                    "Realm multiplier resolved through the deterministic engine.",
                TerrainContribution =
                    "Terrain multiplier resolved through the deterministic engine.",
                Summary = BuildSummary(result),
                ComputationSha256 = result.ComputationSha256
            };
        }

        private static int ToInt(long value)
        {
            if (value < int.MinValue)
            {
                return int.MinValue;
            }

            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)value;
        }

        private static List<TroopStack> ToLegacyLosses(IReadOnlyList<BattleTroopLoss> losses)
        {
            var legacy = new List<TroopStack>(losses.Count);
            for (int index = 0; index < losses.Count; index++)
            {
                BattleTroopLoss loss = losses[index];
                legacy.Add(new TroopStack
                {
                    Type = TroopTypeFromId(loss.TroopDefinitionId),
                    Count = ToInt(loss.Killed)
                });
            }

            return legacy;
        }

        private static List<TroopLossReport> MapDetailedLosses(
            IReadOnlyList<BattleTroopLoss> losses)
        {
            var detailed = new List<TroopLossReport>(losses.Count);
            for (int index = 0; index < losses.Count; index++)
            {
                BattleTroopLoss loss = losses[index];
                detailed.Add(new TroopLossReport
                {
                    Type = TroopTypeFromId(loss.TroopDefinitionId),
                    Killed = ToInt(loss.Killed),
                    Wounded = ToInt(loss.Wounded),
                    Survived = ToInt(loss.Survived),
                    DamageTaken = 0f
                });
            }

            return detailed;
        }

        private static List<BattleRoundReport> MapRounds(IReadOnlyList<BattleRoundResult> rounds)
        {
            var legacy = new List<BattleRoundReport>(rounds.Count);
            for (int index = 0; index < rounds.Count; index++)
            {
                BattleRoundResult round = rounds[index];
                legacy.Add(new BattleRoundReport
                {
                    Round = round.RoundIndex,
                    AttackerDamage = round.DamageToOpponentMicros / 1_000_000f,
                    DefenderDamage = round.DamageToAttackerMicros / 1_000_000f,
                    Note = BuildRoundNote(round)
                });
            }

            return legacy;
        }

        private static string BuildRoundNote(BattleRoundResult round)
        {
            if (round.DamageToOpponentMicros > round.DamageToAttackerMicros * 6 / 5)
            {
                return $"Round {round.RoundIndex}: attacker pressure broke the line.";
            }

            if (round.DamageToAttackerMicros > round.DamageToOpponentMicros * 6 / 5)
            {
                return $"Round {round.RoundIndex}: defender counterattack landed hard.";
            }

            return $"Round {round.RoundIndex}: both armies traded evenly.";
        }

        private static List<ResourceData> MapLoot(BattleRewardProposal rewards)
        {
            var loot = new List<ResourceData>();
            if (rewards == null)
            {
                return loot;
            }

            if (rewards.Food > 0)
            {
                loot.Add(new ResourceData { Type = ResourceType.Food, Amount = rewards.Food });
            }

            if (rewards.Gold > 0)
            {
                loot.Add(new ResourceData { Type = ResourceType.Gold, Amount = rewards.Gold });
            }

            return loot;
        }

        private static string BuildSummary(BattleComputedResult result)
        {
            string outcome = result.Outcome == BattleOutcome.AttackerVictory
                ? "Victory"
                : "Defeat";
            string credits = result.RewardProposal != null && result.RewardProposal.Credits > 0
                ? $" Earned {result.RewardProposal.Credits} Warzone Credits."
                : string.Empty;
            return $"{outcome} after {result.Rounds.Count} rounds. " +
                   $"Attacker power {result.AttackerPower}, " +
                   $"defender power {result.OpponentPower}.{credits}";
        }

        private static BattleReport FailureReport(BattleComputationResult result)
        {
            string detail = result != null && result.Diagnostics.Count > 0
                ? string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code))
                : "unknown validation failure";
            return new BattleReport
            {
                IsWinner = false,
                Summary = $"Battle simulation rejected ({detail}); no float fallback was used.",
                ChampionContribution =
                    "Deterministic engine rejected the request; no legacy fallback was used.",
                RealmPerkContribution = string.Empty,
                TerrainContribution = string.Empty,
                AttackerLosses = new List<TroopStack>(),
                DefenderLosses = new List<TroopStack>(),
                RoundReports = new List<BattleRoundReport>(),
                AttackerDetailedLosses = new List<TroopLossReport>(),
                DefenderDetailedLosses = new List<TroopLossReport>(),
                Loot = new List<ResourceData>(),
                ComputationSha256 = string.Empty
            };
        }
    }
}
