using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AL.Battle.Contracts;

namespace AL.Battle.Profiles
{
    public sealed class BattleSourceTroopCount
    {
        public BattleSourceTroopCount(string troopDefinitionId, long count)
        {
            TroopDefinitionId = troopDefinitionId;
            Count = count;
        }

        public string TroopDefinitionId { get; }
        public long Count { get; }
    }

    public sealed class BattleEquipmentSourceProfile
    {
        private readonly ReadOnlyCollection<BattleSnapshotIdentity> _equippedDefinitions;

        public BattleEquipmentSourceProfile(
            BattleSnapshotIdentity identity,
            IEnumerable<BattleSnapshotIdentity> equippedDefinitions,
            long commanderMultiplierMicros)
        {
            Identity = identity;
            _equippedDefinitions = Freeze(equippedDefinitions);
            CommanderMultiplierMicros = commanderMultiplierMicros;
        }

        public BattleSnapshotIdentity Identity { get; }
        public IReadOnlyList<BattleSnapshotIdentity> EquippedDefinitions => _equippedDefinitions;
        public long CommanderMultiplierMicros { get; }

        private static ReadOnlyCollection<BattleSnapshotIdentity> Freeze(
            IEnumerable<BattleSnapshotIdentity> source)
        {
            return Array.AsReadOnly(source == null
                ? Array.Empty<BattleSnapshotIdentity>()
                : new List<BattleSnapshotIdentity>(source).ToArray());
        }
    }

    public sealed class BattleProgressionSourceProfile
    {
        public BattleProgressionSourceProfile(
            BattleSnapshotIdentity identity,
            long moraleMicros,
            long researchMultiplierMicros)
        {
            Identity = identity;
            MoraleMicros = moraleMicros;
            ResearchMultiplierMicros = researchMultiplierMicros;
        }

        public BattleSnapshotIdentity Identity { get; }
        public long MoraleMicros { get; }
        public long ResearchMultiplierMicros { get; }
    }

    public sealed class BattleParticipantSourceProfile
    {
        private readonly ReadOnlyCollection<BattleSourceTroopCount> _troops;

        public BattleParticipantSourceProfile(
            BattleSnapshotIdentity identity,
            BattleRealm realm,
            BattleSnapshotIdentity armyIdentity,
            IEnumerable<BattleSourceTroopCount> troops,
            BattleEquipmentSourceProfile equipment,
            BattleProgressionSourceProfile progression)
        {
            Identity = identity;
            Realm = realm;
            ArmyIdentity = armyIdentity;
            _troops = Array.AsReadOnly(troops == null
                ? Array.Empty<BattleSourceTroopCount>()
                : new List<BattleSourceTroopCount>(troops).ToArray());
            Equipment = equipment;
            Progression = progression;
        }

        public BattleSnapshotIdentity Identity { get; }
        public BattleRealm Realm { get; }
        public BattleSnapshotIdentity ArmyIdentity { get; }
        public IReadOnlyList<BattleSourceTroopCount> Troops => _troops;
        public BattleEquipmentSourceProfile Equipment { get; }
        public BattleProgressionSourceProfile Progression { get; }
    }

    public sealed class BattleOpponentSourceProfile
    {
        private BattleOpponentSourceProfile(
            BattleOpponentKind kind,
            BattleParticipantSourceProfile armyParticipant,
            BattleSnapshotIdentity bossIdentity,
            long bossPower)
        {
            Kind = kind;
            ArmyParticipant = armyParticipant;
            BossIdentity = bossIdentity;
            BossPower = bossPower;
        }

        public BattleOpponentKind Kind { get; }
        public BattleParticipantSourceProfile ArmyParticipant { get; }
        public BattleSnapshotIdentity BossIdentity { get; }
        public long BossPower { get; }

        public static BattleOpponentSourceProfile ForArmy(
            BattleParticipantSourceProfile participant)
        {
            return new BattleOpponentSourceProfile(
                BattleOpponentKind.Army,
                participant,
                null,
                0L);
        }

        public static BattleOpponentSourceProfile ForBoss(
            BattleSnapshotIdentity bossIdentity,
            long bossPower)
        {
            return new BattleOpponentSourceProfile(
                BattleOpponentKind.Boss,
                null,
                bossIdentity,
                bossPower);
        }
    }

    public sealed class BattleConfigurationSourceProfile
    {
        public BattleConfigurationSourceProfile(
            string gameId,
            string catalogSetId,
            string battleRequestId,
            string battleId,
            string battleResultId,
            string expectedResultConsumerId,
            BattleExecutionMode executionMode,
            BattleKind battleKind,
            string battleTypeId,
            string determinismVersion,
            string seedHex,
            string contextId,
            string encounterId,
            BattleRealmContextKind contextKind,
            BattleTerrainProfile terrainProfile,
            BattleCatalogSnapshot catalog,
            BattleRulesProfile rules,
            BattleRewardProfile rewards)
        {
            GameId = gameId;
            CatalogSetId = catalogSetId;
            BattleRequestId = battleRequestId;
            BattleId = battleId;
            BattleResultId = battleResultId;
            ExpectedResultConsumerId = expectedResultConsumerId;
            ExecutionMode = executionMode;
            BattleKind = battleKind;
            BattleTypeId = battleTypeId;
            DeterminismVersion = determinismVersion;
            SeedHex = seedHex;
            ContextId = contextId;
            EncounterId = encounterId;
            ContextKind = contextKind;
            TerrainProfile = terrainProfile;
            Catalog = catalog;
            Rules = rules;
            Rewards = rewards;
        }

        public string GameId { get; }
        public string CatalogSetId { get; }
        public string BattleRequestId { get; }
        public string BattleId { get; }
        public string BattleResultId { get; }
        public string ExpectedResultConsumerId { get; }
        public BattleExecutionMode ExecutionMode { get; }
        public BattleKind BattleKind { get; }
        public string BattleTypeId { get; }
        public string DeterminismVersion { get; }
        public string SeedHex { get; }
        public string ContextId { get; }
        public string EncounterId { get; }
        public BattleRealmContextKind ContextKind { get; }
        public BattleTerrainProfile TerrainProfile { get; }
        public BattleCatalogSnapshot Catalog { get; }
        public BattleRulesProfile Rules { get; }
        public BattleRewardProfile Rewards { get; }
    }

    public sealed class BattleAuthoritativeSourceState
    {
        public BattleAuthoritativeSourceState(
            BattleParticipantSourceProfile player,
            BattleOpponentSourceProfile opponent,
            BattleConfigurationSourceProfile configuration)
        {
            Player = player;
            Opponent = opponent;
            Configuration = configuration;
        }

        public BattleParticipantSourceProfile Player { get; }
        public BattleOpponentSourceProfile Opponent { get; }
        public BattleConfigurationSourceProfile Configuration { get; }
    }

    public sealed class BattleSourceProfileResult
    {
        private readonly ReadOnlyCollection<BattleDiagnostic> _diagnostics;

        internal BattleSourceProfileResult(
            BattleComputationRequest request,
            IEnumerable<BattleDiagnostic> diagnostics)
        {
            Request = request;
            _diagnostics = Array.AsReadOnly(diagnostics == null
                ? Array.Empty<BattleDiagnostic>()
                : new List<BattleDiagnostic>(diagnostics).ToArray());
        }

        public BattleComputationRequest Request { get; }
        public IReadOnlyList<BattleDiagnostic> Diagnostics => _diagnostics;
        public bool IsSuccess => Request != null && _diagnostics.Count == 0;
    }
}
