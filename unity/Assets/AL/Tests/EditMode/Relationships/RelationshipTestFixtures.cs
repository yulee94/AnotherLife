using System.Collections.Generic;
using System.IO;
using AL.Core.Interfaces.Relationships;
using AL.Services.Relationships;
using UnityEngine;

namespace AL.Tests.EditMode.Relationships
{
    internal static class RelationshipTestFixtures
    {
        public const string CorrelationId = "rel-correlation-001";
        public const string OperationId = "rel-operation-001";
        public const string SourceSystemId = "al_relationship_test";
        public const string Valerius = "npc_valerius";
        public const string ValeriusAlias = "NPC_VALERIUS";
        public const string AdvisorValerius = "ADVISOR_VALERIUS";
        public const string Gruff = "npc_gruff";
        public const string VeilWatch = "faction_veil_watch";
        public const string VeilWatchAlias = "FACTION_VEIL_WATCH";
        public const string HumanCouncilAlias = "FACT_HUMAN_COUNCIL";
        public const string CrownlandsCouncil = "faction_crownlands_radiant_council";
        public const string CatalogPath =
            "AL/StreamingAssets/GameData/al_relationship_authority_content_catalog.json";

        public static RelationshipIdentityRecord[] NpcRecords()
        {
            return new[]
            {
                Npc(Valerius, true, "NPC_VALERIUS", "ADVISOR_VALERIUS"),
                Npc(Gruff, true, "ADVISOR_GRUFF", "NPC_SMITH_GRUFF"),
                Npc("npc_molly", true, "ADVISOR_MOLLY", "NPC_ARCHIVIST_MOLLY"),
                Npc("npc_xerath", true, "ADVISOR_XERATH", "NPC_VOID_SEER_XERATH"),
                Npc("npc_vaeloryn", false, "NPC_VAELORYN"),
                Npc("npc_edras_veyr", false, "NPC_EDRAS_VEYR")
            };
        }

        public static RelationshipIdentityRecord[] FactionRecords()
        {
            return new[]
            {
                Faction(CrownlandsCouncil, "FACT_HUMAN_COUNCIL"),
                Faction("faction_stonehold_assembly", "FACT_DWARVEN_FORGE"),
                Faction("faction_eldergrove_wardens", "FACT_ELVEN_GLADE"),
                Faction("faction_umbral_cabal", "FACT_DARK_ELF_RIFT"),
                Faction(VeilWatch, "FACTION_VEIL_WATCH")
            };
        }

        public static InjectedRelationshipIdentityResolver Identities(
            RelationshipCatalogAvailability availability =
                RelationshipCatalogAvailability.Available,
            int schemaVersion = RelationshipTechnicalLimits.CurrentSchemaVersion,
            IEnumerable<RelationshipIdentityRecord> npcs = null,
            IEnumerable<RelationshipIdentityRecord> factions = null)
        {
            return new InjectedRelationshipIdentityResolver(
                availability,
                RelationshipTechnicalLimits.FixtureCatalogRevision,
                schemaVersion,
                npcs ?? NpcRecords(),
                factions ?? FactionRecords());
        }

        public static InjectedRelationshipPolicyResolver Policies(
            RelationshipCatalogAvailability availability =
                RelationshipCatalogAvailability.Available,
            RelationshipPolicySnapshot policy = null)
        {
            return new InjectedRelationshipPolicyResolver(
                availability,
                policy ?? InjectedRelationshipPolicyResolver.CreateLegacyFixturePolicy(
                    RelationshipTechnicalLimits.FixtureCatalogRevision));
        }

        public static RelationshipMutationPlanner Planner(
            IRelationshipIdentityResolver identities = null,
            IRelationshipPolicyResolver policies = null)
        {
            return new RelationshipMutationPlanner(
                identities ?? Identities(),
                policies ?? Policies());
        }

        public static RelationshipSnapshot Snapshot(
            RelationshipRawState raw = null,
            IRelationshipIdentityResolver identities = null,
            IRelationshipPolicyResolver policies = null)
        {
            return RelationshipSnapshotBuilder.Build(
                raw ?? RelationshipRawState.EmptyWritable(),
                identities ?? Identities(),
                policies ?? Policies());
        }

        public static string ApprovedCatalogJson()
        {
            string path = Path.Combine(Application.dataPath, CatalogPath);
            return File.ReadAllText(path);
        }

        public static string BootloaderSource()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL/Scripts/Core/Bootloader.cs");
            return File.ReadAllText(path);
        }

        public static string GameDataServiceSource()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL/Scripts/Services/Local/LocalGameDataService.cs");
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            return File.ReadAllText(path);
        }

        private static RelationshipIdentityRecord Npc(
            string id,
            bool enabled,
            params string[] aliases)
        {
            return new RelationshipIdentityRecord(
                id,
                aliases,
                enabled,
                "relationship.npc." + id.Replace("npc_", string.Empty) + ".name");
        }

        private static RelationshipIdentityRecord Faction(string id, params string[] aliases)
        {
            return new RelationshipIdentityRecord(
                id,
                aliases,
                true,
                "relationship.faction." + id.Replace("faction_", string.Empty) + ".name");
        }
    }
}
