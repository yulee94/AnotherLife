using System;
using System.Collections.Generic;
using AL.Battle.Contracts;

namespace AL.Battle.Computation
{
    public static class BattleMigrationProfiles
    {
        public static BattleCatalogSnapshot CreateCatalog(
            string gameId,
            string catalogSetId,
            string contentVersion,
            string sourceRevision)
        {
            var definitions = new[]
            {
                CreateDefinition("troop.cavalry", BattleTroopArchetype.Cavalry, 15, catalogSetId, contentVersion, sourceRevision),
                CreateDefinition("troop.infantry", BattleTroopArchetype.Infantry, 10, catalogSetId, contentVersion, sourceRevision),
                CreateDefinition("troop.ranged", BattleTroopArchetype.Ranged, 12, catalogSetId, contentVersion, sourceRevision),
                CreateDefinition("troop.siege", BattleTroopArchetype.Siege, 20, catalogSetId, contentVersion, sourceRevision)
            };
            BattleSnapshotIdentity identity = Identity(
                gameId + ".battle_catalog",
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleCatalogSnapshot(identity, definitions);
            return new BattleCatalogSnapshot(
                identity.WithSha256(BattleCanonicalHash.Catalog(provisional)),
                definitions);
        }

        public static BattleRulesProfile CreateRules(
            string catalogSetId,
            string contentVersion,
            string sourceRevision)
        {
            BattleSnapshotIdentity identity = Identity(
                BattleTechnicalLimits.MigrationRulesProfileId,
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleRulesProfile(
                identity,
                20,
                80_000L,
                160_000L,
                180_000L,
                120_000L,
                380_000L,
                700_000L,
                80_000L,
                350_000L,
                550_000L,
                1_000_000L,
                920_000L,
                1_080_000L,
                1_180_000L);
            return new BattleRulesProfile(
                identity.WithSha256(BattleCanonicalHash.Rules(provisional)),
                provisional.MaximumRounds,
                provisional.MinimumDamageRateMicros,
                provisional.MaximumDamageRateExclusiveMicros,
                provisional.CounterMultiplierMicros,
                provisional.BossSiegeMultiplierMicros,
                provisional.WinnerCasualtyPressureMicros,
                provisional.LoserCasualtyPressureMicros,
                provisional.LoserCasualtyFloorMicros,
                provisional.WinnerKilledShareMicros,
                provisional.LoserKilledShareMicros,
                provisional.InfantryVulnerabilityMicros,
                provisional.CavalryVulnerabilityMicros,
                provisional.RangedVulnerabilityMicros,
                provisional.SiegeVulnerabilityMicros);
        }

        public static BattleRewardProfile CreateRewards(
            string catalogSetId,
            string contentVersion,
            string sourceRevision)
        {
            BattleSnapshotIdentity identity = Identity(
                BattleTechnicalLimits.MigrationRewardProfileId,
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleRewardProfile(
                identity,
                12,
                4,
                120,
                40,
                2,
                10,
                40,
                26,
                12,
                14,
                18,
                36,
                8,
                3);
            return new BattleRewardProfile(
                identity.WithSha256(BattleCanonicalHash.Rewards(provisional)),
                provisional.WinCreditsBase,
                provisional.LossCreditsBase,
                provisional.PowerCreditDivisor,
                provisional.PowerCreditMaximum,
                provisional.RoundsCreditDivisor,
                provisional.RoundsCreditMaximum,
                provisional.WinFoodBase,
                provisional.WinFoodRandomExclusive,
                provisional.WinGoldBase,
                provisional.WinGoldRandomExclusive,
                provisional.WinXpDivisor,
                provisional.LossXpDivisor,
                provisional.WinXpMinimum,
                provisional.LossXpMinimum);
        }

        public static BattleModifierSnapshot CreateModifier(
            string id,
            string catalogSetId,
            string contentVersion,
            string sourceRevision,
            long moraleMicros,
            long researchMicros,
            long commanderMicros)
        {
            BattleSnapshotIdentity identity = Identity(
                id,
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleModifierSnapshot(
                identity,
                moraleMicros,
                researchMicros,
                commanderMicros);
            return new BattleModifierSnapshot(
                identity.WithSha256(BattleCanonicalHash.Modifier(provisional)),
                moraleMicros,
                researchMicros,
                commanderMicros);
        }

        public static BattleTerrainSnapshot CreateTerrain(
            BattleTerrainProfile profile,
            string catalogSetId,
            string contentVersion,
            string sourceRevision)
        {
            BattleSnapshotIdentity identity = Identity(
                BattleTerrainProfiles.GetId(profile),
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleTerrainSnapshot(identity, profile);
            return new BattleTerrainSnapshot(
                identity.WithSha256(BattleCanonicalHash.Terrain(provisional)),
                profile);
        }

        public static BattleArmySnapshot CreateArmy(
            string id,
            string catalogSetId,
            string contentVersion,
            string sourceRevision,
            BattleCatalogSnapshot catalog,
            IEnumerable<KeyValuePair<string, long>> countsByTroopId)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (countsByTroopId == null) throw new ArgumentNullException(nameof(countsByTroopId));
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> pair in countsByTroopId)
            {
                if (counts.ContainsKey(pair.Key))
                    throw new ArgumentException("Troop IDs must be unique.", nameof(countsByTroopId));
                counts.Add(pair.Key, pair.Value);
            }

            var stacks = new List<BattleTroopStack>(counts.Count);
            foreach (KeyValuePair<string, long> pair in counts)
            {
                BattleTroopDefinition definition = FindDefinition(catalog, pair.Key);
                if (definition == null)
                    throw new ArgumentException("Unknown troop definition ID.", nameof(countsByTroopId));
                stacks.Add(new BattleTroopStack(
                    pair.Key,
                    definition.Identity.ContentVersion,
                    definition.Identity.Sha256,
                    pair.Value));
            }

            BattleSnapshotIdentity identity = Identity(
                id,
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleArmySnapshot(identity, stacks);
            return new BattleArmySnapshot(
                identity.WithSha256(BattleCanonicalHash.Army(provisional)),
                stacks);
        }

        public static BattleContextSnapshot CreateContext(
            string id,
            string catalogSetId,
            string contentVersion,
            string sourceRevision,
            string encounterId,
            BattleRealmContextKind contextKind,
            BattleRealm attackerRealm,
            BattleRealm opponentRealm,
            BattleTerrainProfile terrainProfile,
            BattleModifierSnapshot attackerModifiers,
            BattleModifierSnapshot opponentModifiers)
        {
            BattleTerrainSnapshot terrain = CreateTerrain(
                terrainProfile,
                catalogSetId,
                contentVersion,
                sourceRevision);
            BattleSnapshotIdentity identity = Identity(
                id,
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleContextSnapshot(
                identity,
                encounterId,
                contextKind,
                attackerRealm,
                opponentRealm,
                terrain,
                attackerModifiers,
                opponentModifiers);
            return new BattleContextSnapshot(
                identity.WithSha256(BattleCanonicalHash.Context(provisional)),
                encounterId,
                contextKind,
                attackerRealm,
                opponentRealm,
                terrain,
                attackerModifiers,
                opponentModifiers);
        }

        private static BattleTroopDefinition CreateDefinition(
            string id,
            BattleTroopArchetype archetype,
            int basePower,
            string catalogSetId,
            string contentVersion,
            string sourceRevision)
        {
            BattleSnapshotIdentity identity = Identity(
                id,
                contentVersion,
                sourceRevision,
                catalogSetId);
            var provisional = new BattleTroopDefinition(identity, archetype, basePower);
            return new BattleTroopDefinition(
                identity.WithSha256(BattleCanonicalHash.TroopDefinition(provisional)),
                archetype,
                basePower);
        }

        private static BattleSnapshotIdentity Identity(
            string id,
            string contentVersion,
            string sourceRevision,
            string catalogSetId)
        {
            return new BattleSnapshotIdentity(
                id,
                BattleTechnicalLimits.SupportedSchemaVersion,
                contentVersion,
                sourceRevision,
                new string('0', 64),
                catalogSetId);
        }

        private static BattleTroopDefinition FindDefinition(
            BattleCatalogSnapshot catalog,
            string id)
        {
            for (int index = 0; index < catalog.TroopDefinitions.Count; index++)
            {
                BattleTroopDefinition definition = catalog.TroopDefinitions[index];
                if (string.Equals(definition.Identity.Id, id, StringComparison.Ordinal))
                    return definition;
            }

            return null;
        }
    }
}
