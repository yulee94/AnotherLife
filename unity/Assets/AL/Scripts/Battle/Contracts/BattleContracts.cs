using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Battle.Contracts
{
    public static class BattleTechnicalLimits
    {
        public const long MicrosPerUnit = 1_000_000L;
        public const long MaximumArmyCount = 100_000_000L;
        public const long MaximumFinalPower = 1_000_000_000L;
        public const int MaximumRounds = 20;
        public const int MaximumCollectionCount = 256;
        public const int MaximumIdUtf8Bytes = 128;
        public const int MaximumVersionUtf8Bytes = 64;
        public const int MaximumSourceRevisionUtf8Bytes = 128;
        public const string SupportedSchemaVersion = "battle_contract_v1";
        public const string SupportedDeterminismVersion = "battle_sha256_v1";
        public const string PveBattleTypeId = "battle.pve";
        public const string PvpBattleTypeId = "battle.pvp";
        public const string BossBattleTypeId = "battle.boss";
        public const string WarzoneBattleTypeId = "battle.warzone";
        public const string PreviewResultPrefix = "preview:";
        public const string MigrationRulesProfileId = "battle.rules.migration_v1";
        public const string MigrationRewardProfileId = "battle.rewards.migration_v1";
        public const string ExpectedResultConsumerId = "consumer.battle_result_application";
        public const string NeutralTerrainProfileId = "terrain.neutral";
        public const string MountainCaveTerrainProfileId = "terrain.mountain_cave";
        public const string ForestTerrainProfileId = "terrain.forest";
        public const string RoadFieldTerrainProfileId = "terrain.road_field";
        public const string VolcanicShadowTerrainProfileId = "terrain.volcanic_shadow";
    }

    public enum BattleExecutionMode
    {
        Unknown = 0,
        Preview = 1,
        Authoritative = 2
    }

    public enum BattleKind
    {
        Unknown = 0,
        Pve = 1,
        Pvp = 2,
        Boss = 3,
        Warzone = 4
    }

    public enum BattleOpponentKind
    {
        Unknown = 0,
        Army = 1,
        Boss = 2
    }

    public enum BattleTroopArchetype
    {
        Unknown = 0,
        Infantry = 1,
        Cavalry = 2,
        Ranged = 3,
        Siege = 4
    }

    public enum BattleRealm
    {
        Unknown = 0,
        Neutral = 1,
        Stonehold = 2,
        Eldergrove = 3,
        Crownlands = 4,
        Umbral = 5
    }

    public enum BattleRealmContextKind
    {
        Unknown = 0,
        RealmVersusRealm = 1,
        NeutralEncounter = 2,
        BossEncounter = 3
    }

    public enum BattleTerrainProfile
    {
        Unknown = 0,
        Neutral = 1,
        MountainCave = 2,
        Forest = 3,
        RoadField = 4,
        VolcanicShadow = 5
    }

    public static class BattleTerrainProfiles
    {
        public static string GetId(BattleTerrainProfile profile)
        {
            switch (profile)
            {
                case BattleTerrainProfile.Neutral:
                    return BattleTechnicalLimits.NeutralTerrainProfileId;
                case BattleTerrainProfile.MountainCave:
                    return BattleTechnicalLimits.MountainCaveTerrainProfileId;
                case BattleTerrainProfile.Forest:
                    return BattleTechnicalLimits.ForestTerrainProfileId;
                case BattleTerrainProfile.RoadField:
                    return BattleTechnicalLimits.RoadFieldTerrainProfileId;
                case BattleTerrainProfile.VolcanicShadow:
                    return BattleTechnicalLimits.VolcanicShadowTerrainProfileId;
                default:
                    return "terrain.unknown";
            }
        }
    }

    public enum BattleOutcome
    {
        Unknown = 0,
        AttackerVictory = 1,
        OpponentVictory = 2
    }

    public enum BattleComputationStatus
    {
        Computed = 0,
        ComputedPreview = 1,
        ExplicitNoReward = 2,
        InvalidRequest = 3,
        UnsupportedExecutionMode = 4,
        UnsupportedBattleType = 5,
        CatalogUnavailable = 6,
        UnsupportedVersion = 7,
        InvalidArmy = 8,
        InvalidOpponent = 9,
        InvalidRealmContext = 10,
        InvalidTerrainProfile = 11,
        InvalidModifierSnapshot = 12,
        InvalidRulesProfile = 13,
        InvalidRewardProfile = 14,
        ArithmeticOverflow = 15,
        DeterminismFailure = 16,
        InternalInvariantFailure = 17
    }

    public sealed class BattleDiagnostic
    {
        public BattleDiagnostic(string code, string fieldPath)
        {
            Code = code ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
        }

        public string Code { get; }
        public string FieldPath { get; }
    }

    public sealed class BattleSnapshotIdentity
    {
        public BattleSnapshotIdentity(
            string id,
            string schemaVersion,
            string contentVersion,
            string sourceRevision,
            string sha256,
            string catalogSetId)
        {
            Id = id;
            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            SourceRevision = sourceRevision;
            Sha256 = sha256;
            CatalogSetId = catalogSetId;
        }

        public string Id { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public string SourceRevision { get; }
        public string Sha256 { get; }
        public string CatalogSetId { get; }

        public BattleSnapshotIdentity WithSha256(string sha256)
        {
            return new BattleSnapshotIdentity(
                Id,
                SchemaVersion,
                ContentVersion,
                SourceRevision,
                sha256,
                CatalogSetId);
        }
    }

    public sealed class BattleTroopDefinition
    {
        public BattleTroopDefinition(
            BattleSnapshotIdentity identity,
            BattleTroopArchetype archetype,
            int basePower)
        {
            Identity = identity;
            Archetype = archetype;
            BasePower = basePower;
        }

        public BattleSnapshotIdentity Identity { get; }
        public BattleTroopArchetype Archetype { get; }
        public int BasePower { get; }
    }

    public sealed class BattleCatalogSnapshot
    {
        private readonly ReadOnlyCollection<BattleTroopDefinition> _troopDefinitions;

        public BattleCatalogSnapshot(
            BattleSnapshotIdentity identity,
            IEnumerable<BattleTroopDefinition> troopDefinitions)
        {
            Identity = identity;
            _troopDefinitions = BattleContractCopies.Freeze(troopDefinitions);
        }

        public BattleSnapshotIdentity Identity { get; }
        public IReadOnlyList<BattleTroopDefinition> TroopDefinitions => _troopDefinitions;
    }

    public sealed class BattleTroopStack
    {
        public BattleTroopStack(
            string troopDefinitionId,
            string troopContentVersion,
            string troopDefinitionSha256,
            long count)
        {
            TroopDefinitionId = troopDefinitionId;
            TroopContentVersion = troopContentVersion;
            TroopDefinitionSha256 = troopDefinitionSha256;
            Count = count;
        }

        public string TroopDefinitionId { get; }
        public string TroopContentVersion { get; }
        public string TroopDefinitionSha256 { get; }
        public long Count { get; }
    }

    public sealed class BattleArmySnapshot
    {
        private readonly ReadOnlyCollection<BattleTroopStack> _stacks;

        public BattleArmySnapshot(
            BattleSnapshotIdentity identity,
            IEnumerable<BattleTroopStack> stacks)
        {
            Identity = identity;
            _stacks = BattleContractCopies.Freeze(stacks);
        }

        public BattleSnapshotIdentity Identity { get; }
        public IReadOnlyList<BattleTroopStack> Stacks => _stacks;
    }

    public sealed class BattleModifierSnapshot
    {
        public BattleModifierSnapshot(
            BattleSnapshotIdentity identity,
            long moraleMicros,
            long researchMicros,
            long commanderMicros)
        {
            Identity = identity;
            MoraleMicros = moraleMicros;
            ResearchMicros = researchMicros;
            CommanderMicros = commanderMicros;
        }

        public BattleSnapshotIdentity Identity { get; }
        public long MoraleMicros { get; }
        public long ResearchMicros { get; }
        public long CommanderMicros { get; }
    }

    public sealed class BattleTerrainSnapshot
    {
        public BattleTerrainSnapshot(
            BattleSnapshotIdentity identity,
            BattleTerrainProfile profile)
        {
            Identity = identity;
            Profile = profile;
        }

        public BattleSnapshotIdentity Identity { get; }
        public BattleTerrainProfile Profile { get; }
    }

    public sealed class BattleContextSnapshot
    {
        public BattleContextSnapshot(
            BattleSnapshotIdentity identity,
            string encounterId,
            BattleRealmContextKind contextKind,
            BattleRealm attackerRealm,
            BattleRealm opponentRealm,
            BattleTerrainSnapshot terrain,
            BattleModifierSnapshot attackerModifiers,
            BattleModifierSnapshot opponentModifiers)
        {
            Identity = identity;
            EncounterId = encounterId;
            ContextKind = contextKind;
            AttackerRealm = attackerRealm;
            OpponentRealm = opponentRealm;
            Terrain = terrain;
            AttackerModifiers = attackerModifiers;
            OpponentModifiers = opponentModifiers;
        }

        public BattleSnapshotIdentity Identity { get; }
        public string EncounterId { get; }
        public BattleRealmContextKind ContextKind { get; }
        public BattleRealm AttackerRealm { get; }
        public BattleRealm OpponentRealm { get; }
        public BattleTerrainSnapshot Terrain { get; }
        public BattleTerrainProfile TerrainProfile => Terrain?.Profile ?? BattleTerrainProfile.Unknown;
        public BattleModifierSnapshot AttackerModifiers { get; }
        public BattleModifierSnapshot OpponentModifiers { get; }
    }

    public sealed class BattleRulesProfile
    {
        public BattleRulesProfile(
            BattleSnapshotIdentity identity,
            int maximumRounds,
            long minimumDamageRateMicros,
            long maximumDamageRateExclusiveMicros,
            long counterMultiplierMicros,
            long bossSiegeMultiplierMicros,
            long winnerCasualtyPressureMicros,
            long loserCasualtyPressureMicros,
            long loserCasualtyFloorMicros,
            long winnerKilledShareMicros,
            long loserKilledShareMicros,
            long infantryVulnerabilityMicros,
            long cavalryVulnerabilityMicros,
            long rangedVulnerabilityMicros,
            long siegeVulnerabilityMicros)
        {
            Identity = identity;
            MaximumRounds = maximumRounds;
            MinimumDamageRateMicros = minimumDamageRateMicros;
            MaximumDamageRateExclusiveMicros = maximumDamageRateExclusiveMicros;
            CounterMultiplierMicros = counterMultiplierMicros;
            BossSiegeMultiplierMicros = bossSiegeMultiplierMicros;
            WinnerCasualtyPressureMicros = winnerCasualtyPressureMicros;
            LoserCasualtyPressureMicros = loserCasualtyPressureMicros;
            LoserCasualtyFloorMicros = loserCasualtyFloorMicros;
            WinnerKilledShareMicros = winnerKilledShareMicros;
            LoserKilledShareMicros = loserKilledShareMicros;
            InfantryVulnerabilityMicros = infantryVulnerabilityMicros;
            CavalryVulnerabilityMicros = cavalryVulnerabilityMicros;
            RangedVulnerabilityMicros = rangedVulnerabilityMicros;
            SiegeVulnerabilityMicros = siegeVulnerabilityMicros;
        }

        public BattleSnapshotIdentity Identity { get; }
        public int MaximumRounds { get; }
        public long MinimumDamageRateMicros { get; }
        public long MaximumDamageRateExclusiveMicros { get; }
        public long CounterMultiplierMicros { get; }
        public long BossSiegeMultiplierMicros { get; }
        public long WinnerCasualtyPressureMicros { get; }
        public long LoserCasualtyPressureMicros { get; }
        public long LoserCasualtyFloorMicros { get; }
        public long WinnerKilledShareMicros { get; }
        public long LoserKilledShareMicros { get; }
        public long InfantryVulnerabilityMicros { get; }
        public long CavalryVulnerabilityMicros { get; }
        public long RangedVulnerabilityMicros { get; }
        public long SiegeVulnerabilityMicros { get; }
    }

    public sealed class BattleRewardProfile
    {
        public BattleRewardProfile(
            BattleSnapshotIdentity identity,
            int winCreditsBase,
            int lossCreditsBase,
            int powerCreditDivisor,
            int powerCreditMaximum,
            int roundsCreditDivisor,
            int roundsCreditMaximum,
            int winFoodBase,
            int winFoodRandomExclusive,
            int winGoldBase,
            int winGoldRandomExclusive,
            int winXpDivisor,
            int lossXpDivisor,
            int winXpMinimum,
            int lossXpMinimum)
        {
            Identity = identity;
            WinCreditsBase = winCreditsBase;
            LossCreditsBase = lossCreditsBase;
            PowerCreditDivisor = powerCreditDivisor;
            PowerCreditMaximum = powerCreditMaximum;
            RoundsCreditDivisor = roundsCreditDivisor;
            RoundsCreditMaximum = roundsCreditMaximum;
            WinFoodBase = winFoodBase;
            WinFoodRandomExclusive = winFoodRandomExclusive;
            WinGoldBase = winGoldBase;
            WinGoldRandomExclusive = winGoldRandomExclusive;
            WinXpDivisor = winXpDivisor;
            LossXpDivisor = lossXpDivisor;
            WinXpMinimum = winXpMinimum;
            LossXpMinimum = lossXpMinimum;
        }

        public BattleSnapshotIdentity Identity { get; }
        public int WinCreditsBase { get; }
        public int LossCreditsBase { get; }
        public int PowerCreditDivisor { get; }
        public int PowerCreditMaximum { get; }
        public int RoundsCreditDivisor { get; }
        public int RoundsCreditMaximum { get; }
        public int WinFoodBase { get; }
        public int WinFoodRandomExclusive { get; }
        public int WinGoldBase { get; }
        public int WinGoldRandomExclusive { get; }
        public int WinXpDivisor { get; }
        public int LossXpDivisor { get; }
        public int WinXpMinimum { get; }
        public int LossXpMinimum { get; }
    }

    public sealed class BattleOpponentSnapshot
    {
        public BattleOpponentSnapshot(
            BattleOpponentKind kind,
            BattleArmySnapshot army,
            BattleSnapshotIdentity bossIdentity,
            long bossPower)
        {
            Kind = kind;
            Army = army;
            BossIdentity = bossIdentity;
            BossPower = bossPower;
        }

        public BattleOpponentKind Kind { get; }
        public BattleArmySnapshot Army { get; }
        public BattleSnapshotIdentity BossIdentity { get; }
        public long BossPower { get; }
    }

    public sealed class BattleComputationRequest
    {
        public BattleComputationRequest(
            string gameId,
            string catalogSetId,
            string profileId,
            string battleRequestId,
            string battleId,
            string battleResultId,
            string expectedResultConsumerId,
            BattleExecutionMode executionMode,
            BattleKind battleKind,
            string battleTypeId,
            string determinismVersion,
            string seedHex,
            BattleCatalogSnapshot catalog,
            BattleArmySnapshot attackerArmy,
            BattleOpponentSnapshot opponent,
            BattleContextSnapshot context,
            BattleRulesProfile rules,
            BattleRewardProfile rewards)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            ProfileId = profileId;
            BattleRequestId = battleRequestId;
            BattleId = battleId;
            BattleResultId = battleResultId;
            ExpectedResultConsumerId = expectedResultConsumerId;
            ExecutionMode = executionMode;
            BattleKind = battleKind;
            BattleTypeId = battleTypeId;
            DeterminismVersion = determinismVersion;
            SeedHex = seedHex;
            Catalog = catalog;
            AttackerArmy = attackerArmy;
            Opponent = opponent;
            Context = context;
            Rules = rules;
            Rewards = rewards;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string ProfileId { get; }
        public string BattleRequestId { get; }
        public string BattleId { get; }
        public string BattleResultId { get; }
        public string ExpectedResultConsumerId { get; }
        public BattleExecutionMode ExecutionMode { get; }
        public BattleKind BattleKind { get; }
        public string BattleTypeId { get; }
        public string DeterminismVersion { get; }
        public string SeedHex { get; }
        public BattleCatalogSnapshot Catalog { get; }
        public BattleArmySnapshot AttackerArmy { get; }
        public BattleOpponentSnapshot Opponent { get; }
        public BattleContextSnapshot Context { get; }
        public BattleRulesProfile Rules { get; }
        public BattleRewardProfile Rewards { get; }
    }

    public sealed class BattleRoundResult
    {
        internal BattleRoundResult(
            int roundIndex,
            long attackerRateMicros,
            long opponentRateMicros,
            long damageToOpponentMicros,
            long damageToAttackerMicros,
            long attackerRemainingPowerMicros,
            long opponentRemainingPowerMicros)
        {
            RoundIndex = roundIndex;
            AttackerRateMicros = attackerRateMicros;
            OpponentRateMicros = opponentRateMicros;
            DamageToOpponentMicros = damageToOpponentMicros;
            DamageToAttackerMicros = damageToAttackerMicros;
            AttackerRemainingPowerMicros = attackerRemainingPowerMicros;
            OpponentRemainingPowerMicros = opponentRemainingPowerMicros;
        }

        public int RoundIndex { get; }
        public long AttackerRateMicros { get; }
        public long OpponentRateMicros { get; }
        public long DamageToOpponentMicros { get; }
        public long DamageToAttackerMicros { get; }
        public long AttackerRemainingPowerMicros { get; }
        public long OpponentRemainingPowerMicros { get; }
    }

    public sealed class BattleTroopLoss
    {
        internal BattleTroopLoss(
            string troopDefinitionId,
            long killed,
            long wounded,
            long survived)
        {
            TroopDefinitionId = troopDefinitionId;
            Killed = killed;
            Wounded = wounded;
            Survived = survived;
        }

        public string TroopDefinitionId { get; }
        public long Killed { get; }
        public long Wounded { get; }
        public long Survived { get; }
    }

    public sealed class BattleRewardProposal
    {
        internal BattleRewardProposal(int credits, int food, int gold, int experience)
        {
            Credits = credits;
            Food = food;
            Gold = gold;
            Experience = experience;
        }

        public int Credits { get; }
        public int Food { get; }
        public int Gold { get; }
        public int Experience { get; }
        public bool IsProposed => true;
    }

    public sealed class BattleContribution
    {
        internal BattleContribution(string technicalId, long valueMicros)
        {
            TechnicalId = technicalId;
            ValueMicros = valueMicros;
        }

        public string TechnicalId { get; }
        public long ValueMicros { get; }
    }

    public sealed class BattleComputedResult
    {
        private readonly ReadOnlyCollection<BattleRoundResult> _rounds;
        private readonly ReadOnlyCollection<BattleTroopLoss> _attackerLosses;
        private readonly ReadOnlyCollection<BattleTroopLoss> _opponentLosses;
        private readonly ReadOnlyCollection<BattleContribution> _contributions;

        internal BattleComputedResult(
            string gameId,
            string catalogSetId,
            string profileId,
            string battleRequestId,
            string battleId,
            string battleResultId,
            string expectedResultConsumerId,
            BattleExecutionMode executionMode,
            BattleKind battleKind,
            string battleTypeId,
            BattleOutcome outcome,
            string outcomeTechnicalId,
            long attackerPower,
            long opponentPower,
            IEnumerable<BattleRoundResult> rounds,
            IEnumerable<BattleTroopLoss> attackerLosses,
            IEnumerable<BattleTroopLoss> opponentLosses,
            BattleRewardProposal rewardProposal,
            IEnumerable<BattleContribution> contributions,
            string catalogSha256,
            string attackerArmySha256,
            string opponentSha256,
            string contextSha256,
            string rulesSha256,
            string rewardProfileSha256,
            string determinismVersion,
            string seedHex,
            string computationSha256)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            ProfileId = profileId;
            BattleRequestId = battleRequestId;
            BattleId = battleId;
            BattleResultId = battleResultId;
            ExpectedResultConsumerId = expectedResultConsumerId;
            ExecutionMode = executionMode;
            BattleKind = battleKind;
            BattleTypeId = battleTypeId;
            Outcome = outcome;
            OutcomeTechnicalId = outcomeTechnicalId;
            AttackerPower = attackerPower;
            OpponentPower = opponentPower;
            _rounds = BattleContractCopies.Freeze(rounds);
            _attackerLosses = BattleContractCopies.Freeze(attackerLosses);
            _opponentLosses = BattleContractCopies.Freeze(opponentLosses);
            RewardProposal = rewardProposal;
            _contributions = BattleContractCopies.Freeze(contributions);
            CatalogSha256 = catalogSha256;
            AttackerArmySha256 = attackerArmySha256;
            OpponentSha256 = opponentSha256;
            ContextSha256 = contextSha256;
            RulesSha256 = rulesSha256;
            RewardProfileSha256 = rewardProfileSha256;
            DeterminismVersion = determinismVersion;
            SeedHex = seedHex;
            ComputationSha256 = computationSha256;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string ProfileId { get; }
        public string BattleRequestId { get; }
        public string BattleId { get; }
        public string BattleResultId { get; }
        public string ExpectedResultConsumerId { get; }
        public BattleExecutionMode ExecutionMode { get; }
        public BattleKind BattleKind { get; }
        public string BattleTypeId { get; }
        public BattleOutcome Outcome { get; }
        public string OutcomeTechnicalId { get; }
        public long AttackerPower { get; }
        public long OpponentPower { get; }
        public IReadOnlyList<BattleRoundResult> Rounds => _rounds;
        public IReadOnlyList<BattleTroopLoss> AttackerLosses => _attackerLosses;
        public IReadOnlyList<BattleTroopLoss> OpponentLosses => _opponentLosses;
        public BattleRewardProposal RewardProposal { get; }
        public IReadOnlyList<BattleContribution> Contributions => _contributions;
        public string CatalogSha256 { get; }
        public string AttackerArmySha256 { get; }
        public string OpponentSha256 { get; }
        public string ContextSha256 { get; }
        public string RulesSha256 { get; }
        public string RewardProfileSha256 { get; }
        public string DeterminismVersion { get; }
        public string SeedHex { get; }
        public string ComputationSha256 { get; }
        public bool IsPreviewApplicationProhibited => ExecutionMode == BattleExecutionMode.Preview;
        public bool IsAuthoritativeProposal => ExecutionMode == BattleExecutionMode.Authoritative;
    }

    public sealed class BattleComputationResult
    {
        private readonly ReadOnlyCollection<BattleDiagnostic> _diagnostics;

        internal BattleComputationResult(
            BattleComputationStatus status,
            BattleComputedResult value,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            Status = status;
            Value = value;
            _diagnostics = BattleContractCopies.Freeze(diagnostics);
        }

        public BattleComputationStatus Status { get; }
        public BattleComputedResult Value { get; }
        public IReadOnlyList<BattleDiagnostic> Diagnostics => _diagnostics;
        public bool IsSuccess =>
            Status == BattleComputationStatus.Computed ||
            Status == BattleComputationStatus.ComputedPreview ||
            Status == BattleComputationStatus.ExplicitNoReward;
    }

    internal static class BattleContractCopies
    {
        public static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> source)
        {
            if (source == null)
                return Array.AsReadOnly(Array.Empty<T>());
            var values = source as T[];
            if (values != null)
                return Array.AsReadOnly((T[])values.Clone());
            return Array.AsReadOnly(new List<T>(source).ToArray());
        }
    }
}
