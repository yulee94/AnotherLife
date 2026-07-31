using System;
using System.Collections.Generic;
using AL.Battle.Computation;
using AL.Battle.Contracts;

namespace AL.Tests.EditMode.Battle
{
    internal static class BattleContractTestData
    {
        public const string GameId = "another_life";
        public const string CatalogSetId = "catalog.battle.test";
        public const string ContentVersion = "battle_content_v1";
        public const string SourceRevision = "test_revision_1";
        public const string Seed = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        public static BattleComputationRequest Request(
            BattleKind kind = BattleKind.Pvp,
            BattleExecutionMode mode = BattleExecutionMode.Authoritative,
            string battleTypeId = null,
            string resultId = null,
            string seedHex = Seed,
            string determinismVersion = BattleTechnicalLimits.SupportedDeterminismVersion,
            BattleCatalogSnapshot catalog = null,
            BattleArmySnapshot attacker = null,
            BattleOpponentSnapshot opponent = null,
            BattleContextSnapshot context = null,
            BattleRulesProfile rules = null,
            BattleRewardProfile rewards = null)
        {
            catalog = catalog ?? Catalog();
            attacker = attacker ?? Army(catalog, "army.attacker", 100, 40, 60, 10);
            opponent = opponent ?? Opponent(kind, catalog);
            context = context ?? Context(kind);
            rules = rules ?? BattleMigrationProfiles.CreateRules(
                CatalogSetId,
                ContentVersion,
                SourceRevision);
            rewards = rewards ?? BattleMigrationProfiles.CreateRewards(
                CatalogSetId,
                ContentVersion,
                SourceRevision);
            resultId = resultId ?? (mode == BattleExecutionMode.Preview
                ? "preview:result.test"
                : "result.test");
            return new BattleComputationRequest(
                GameId,
                CatalogSetId,
                "profile.test",
                "request.test",
                "battle.test",
                resultId,
                BattleTechnicalLimits.ExpectedResultConsumerId,
                mode,
                kind,
                battleTypeId ?? TypeId(kind),
                determinismVersion,
                seedHex,
                catalog,
                attacker,
                opponent,
                context,
                rules,
                rewards);
        }

        public static BattleComputationRequest Copy(
            BattleComputationRequest source,
            string gameId = null,
            string catalogSetId = null,
            string profileId = null,
            string requestId = null,
            string battleId = null,
            string resultId = null,
            string consumerId = null,
            BattleExecutionMode? mode = null,
            BattleKind? kind = null,
            string typeId = null,
            string determinismVersion = null,
            string seedHex = null,
            BattleCatalogSnapshot catalog = null,
            BattleArmySnapshot attacker = null,
            BattleOpponentSnapshot opponent = null,
            BattleContextSnapshot context = null,
            BattleRulesProfile rules = null,
            BattleRewardProfile rewards = null)
        {
            return new BattleComputationRequest(
                gameId ?? source.GameId,
                catalogSetId ?? source.CatalogSetId,
                profileId ?? source.ProfileId,
                requestId ?? source.BattleRequestId,
                battleId ?? source.BattleId,
                resultId ?? source.BattleResultId,
                consumerId ?? source.ExpectedResultConsumerId,
                mode ?? source.ExecutionMode,
                kind ?? source.BattleKind,
                typeId ?? source.BattleTypeId,
                determinismVersion ?? source.DeterminismVersion,
                seedHex ?? source.SeedHex,
                catalog ?? source.Catalog,
                attacker ?? source.AttackerArmy,
                opponent ?? source.Opponent,
                context ?? source.Context,
                rules ?? source.Rules,
                rewards ?? source.Rewards);
        }

        public static BattleCatalogSnapshot Catalog()
        {
            return BattleMigrationProfiles.CreateCatalog(
                GameId,
                CatalogSetId,
                ContentVersion,
                SourceRevision);
        }

        public static BattleArmySnapshot Army(
            BattleCatalogSnapshot catalog,
            string id,
            long infantry,
            long cavalry,
            long ranged,
            long siege)
        {
            var counts = new List<KeyValuePair<string, long>>();
            if (cavalry > 0) counts.Add(Pair("troop.cavalry", cavalry));
            if (infantry > 0) counts.Add(Pair("troop.infantry", infantry));
            if (ranged > 0) counts.Add(Pair("troop.ranged", ranged));
            if (siege > 0) counts.Add(Pair("troop.siege", siege));
            return BattleMigrationProfiles.CreateArmy(
                id,
                CatalogSetId,
                ContentVersion,
                SourceRevision,
                catalog,
                counts);
        }

        public static BattleOpponentSnapshot Opponent(
            BattleKind kind,
            BattleCatalogSnapshot catalog)
        {
            if (kind == BattleKind.Boss)
            {
                var identity = new BattleSnapshotIdentity(
                    "boss.test",
                    BattleTechnicalLimits.SupportedSchemaVersion,
                    ContentVersion,
                    SourceRevision,
                    new string('0', 64),
                    CatalogSetId);
                const long bossPower = 5_000L;
                return new BattleOpponentSnapshot(
                    BattleOpponentKind.Boss,
                    null,
                    identity.WithSha256(BattleCanonicalHash.Boss(identity, bossPower)),
                    bossPower);
            }

            return new BattleOpponentSnapshot(
                BattleOpponentKind.Army,
                Army(catalog, "army.opponent", 80, 70, 40, 5),
                null,
                0L);
        }

        public static BattleOpponentSnapshot BossArmyOpponent(BattleCatalogSnapshot catalog)
        {
            var identity = new BattleSnapshotIdentity(
                "boss.test",
                BattleTechnicalLimits.SupportedSchemaVersion,
                ContentVersion,
                SourceRevision,
                new string('0', 64),
                CatalogSetId);
            return new BattleOpponentSnapshot(
                BattleOpponentKind.Army,
                Army(catalog, "army.boss", 120, 20, 60, 15),
                identity.WithSha256(BattleCanonicalHash.BossDefinition(identity)),
                0L);
        }

        public static BattleContextSnapshot Context(BattleKind kind)
        {
            BattleModifierSnapshot attacker = BattleMigrationProfiles.CreateModifier(
                "modifier.attacker",
                CatalogSetId,
                ContentVersion,
                SourceRevision,
                1_050_000L,
                1_060_000L,
                1_020_000L);
            BattleModifierSnapshot opponent = BattleMigrationProfiles.CreateModifier(
                "modifier.opponent",
                CatalogSetId,
                ContentVersion,
                SourceRevision,
                980_000L,
                1_030_000L,
                1_000_000L);
            BattleRealmContextKind contextKind;
            BattleRealm opponentRealm;
            switch (kind)
            {
                case BattleKind.Pve:
                    contextKind = BattleRealmContextKind.NeutralEncounter;
                    opponentRealm = BattleRealm.Neutral;
                    break;
                case BattleKind.Boss:
                    contextKind = BattleRealmContextKind.BossEncounter;
                    opponentRealm = BattleRealm.Neutral;
                    break;
                default:
                    contextKind = BattleRealmContextKind.RealmVersusRealm;
                    opponentRealm = BattleRealm.Eldergrove;
                    break;
            }

            return BattleMigrationProfiles.CreateContext(
                "context.test",
                CatalogSetId,
                ContentVersion,
                SourceRevision,
                "encounter.test",
                contextKind,
                BattleRealm.Stonehold,
                opponentRealm,
                BattleTerrainProfile.Forest,
                attacker,
                opponent);
        }

        public static BattleContextSnapshot ContextWith(
            BattleContextSnapshot source,
            BattleRealmContextKind? kind = null,
            BattleRealm? attackerRealm = null,
            BattleRealm? opponentRealm = null,
            BattleTerrainProfile? terrain = null,
            BattleModifierSnapshot attackerModifiers = null,
            BattleModifierSnapshot opponentModifiers = null,
            string hashOverride = null)
        {
            BattleTerrainSnapshot resolvedTerrain = terrain.HasValue
                ? BattleMigrationProfiles.CreateTerrain(
                    terrain.Value,
                    source.Identity.CatalogSetId,
                    source.Identity.ContentVersion,
                    source.Identity.SourceRevision)
                : source.Terrain;
            var identity = new BattleSnapshotIdentity(
                source.Identity.Id,
                source.Identity.SchemaVersion,
                source.Identity.ContentVersion,
                source.Identity.SourceRevision,
                new string('0', 64),
                source.Identity.CatalogSetId);
            var provisional = new BattleContextSnapshot(
                identity,
                source.EncounterId,
                kind ?? source.ContextKind,
                attackerRealm ?? source.AttackerRealm,
                opponentRealm ?? source.OpponentRealm,
                resolvedTerrain,
                attackerModifiers ?? source.AttackerModifiers,
                opponentModifiers ?? source.OpponentModifiers);
            return new BattleContextSnapshot(
                identity.WithSha256(hashOverride ?? BattleCanonicalHash.Context(provisional)),
                provisional.EncounterId,
                provisional.ContextKind,
                provisional.AttackerRealm,
                provisional.OpponentRealm,
                provisional.Terrain,
                provisional.AttackerModifiers,
                provisional.OpponentModifiers);
        }

        public static BattleTerrainSnapshot TerrainWith(
            BattleTerrainSnapshot source,
            BattleTerrainProfile? profile = null,
            string id = null,
            string hashOverride = null)
        {
            BattleTerrainProfile resolvedProfile = profile ?? source.Profile;
            var identity = new BattleSnapshotIdentity(
                id ?? BattleTerrainProfiles.GetId(resolvedProfile),
                source.Identity.SchemaVersion,
                source.Identity.ContentVersion,
                source.Identity.SourceRevision,
                new string('0', 64),
                source.Identity.CatalogSetId);
            var provisional = new BattleTerrainSnapshot(identity, resolvedProfile);
            return new BattleTerrainSnapshot(
                identity.WithSha256(hashOverride ?? BattleCanonicalHash.Terrain(provisional)),
                resolvedProfile);
        }

        public static BattleModifierSnapshot ModifierWith(
            BattleModifierSnapshot source,
            long? morale = null,
            long? research = null,
            long? commander = null,
            string hashOverride = null)
        {
            var identity = new BattleSnapshotIdentity(
                source.Identity.Id,
                source.Identity.SchemaVersion,
                source.Identity.ContentVersion,
                source.Identity.SourceRevision,
                new string('0', 64),
                source.Identity.CatalogSetId);
            var provisional = new BattleModifierSnapshot(
                identity,
                morale ?? source.MoraleMicros,
                research ?? source.ResearchMicros,
                commander ?? source.CommanderMicros);
            return new BattleModifierSnapshot(
                identity.WithSha256(hashOverride ?? BattleCanonicalHash.Modifier(provisional)),
                provisional.MoraleMicros,
                provisional.ResearchMicros,
                provisional.CommanderMicros);
        }

        public static BattleArmySnapshot ArmyWithStacks(
            BattleArmySnapshot source,
            IEnumerable<BattleTroopStack> stacks,
            string hashOverride = null)
        {
            var identity = new BattleSnapshotIdentity(
                source.Identity.Id,
                source.Identity.SchemaVersion,
                source.Identity.ContentVersion,
                source.Identity.SourceRevision,
                new string('0', 64),
                source.Identity.CatalogSetId);
            var provisional = new BattleArmySnapshot(identity, stacks);
            return new BattleArmySnapshot(
                identity.WithSha256(hashOverride ?? BattleCanonicalHash.Army(provisional)),
                provisional.Stacks);
        }

        public static string TypeId(BattleKind kind)
        {
            switch (kind)
            {
                case BattleKind.Pve: return BattleTechnicalLimits.PveBattleTypeId;
                case BattleKind.Pvp: return BattleTechnicalLimits.PvpBattleTypeId;
                case BattleKind.Boss: return BattleTechnicalLimits.BossBattleTypeId;
                case BattleKind.Warzone: return BattleTechnicalLimits.WarzoneBattleTypeId;
                default: return "battle.unknown";
            }
        }

        private static KeyValuePair<string, long> Pair(string id, long count)
        {
            return new KeyValuePair<string, long>(id, count);
        }
    }
}
