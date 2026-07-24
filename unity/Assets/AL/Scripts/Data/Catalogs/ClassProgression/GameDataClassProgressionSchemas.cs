using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Strict, non-wired schemas for the accepted class-identity progression spine.
    /// These schemas contain no records and do not make class skills executable.
    /// </summary>
    public static class GameDataClassProgressionSchemas
    {
        public const int SchemaVersion = 1;
        public const int ExpectedSourceCount = 1;
        public const int ExpectedFamilyCount = 4;
        public const int ExpectedClassCount = 16;
        public const int ExpectedResourceCount = 16;
        public const int ExpectedTreeCount = 16;
        public const int ExpectedBranchCount = 48;
        public const int ExpectedMilestoneCount = 80;
        public const int ExpectedMasteryTrialCount = 16;
        public const int ExpectedWarmasterCount = 16;
        public const int BranchesPerClass = 3;
        public const int MilestonesPerClass = 5;
        public const int ActiveSkillSlots = 4;
        public const int WarmasterPieceSlots = 10;
        public const int VisibleFromLevel = 1;
        public const int LaunchLevelCap = 50;
        public const int MaximumRoleReferences = 8;
        public const int MaximumContributionReferences = 16;
        public const int MaximumEquipmentReferences = 8;

        public const string SourceRecordId = "class_source_v001";
        public const string PacketId = "ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES";
        public const string PacketVersion =
            "anotherlife-class-identity-skill-trees-2026-07-24-v001";
        public const string PacketSha256 =
            "065fddf51ab28c4c104f263b7d9f7dc11bd53daaa00aef2b4453cba63d759a75";
        public const string AuthoredRevision = "11bac67f4c4d0be042fd0659692ae5fc9ca16b80";
        public const string ValidatedRevision = "2529a170426f1e0cc8c145233e2daf1ca0ac5f6d";
        public const string CatalogSetId = "class_progression_identity_v001";
        public const string CatalogSourceMode = "authored";
        public const string IdentityScope = "identity_spine_only";
        public const string SourceProjectionSha256 =
            "d5ae844106f633ef5f92b1f78ec3b65d26513eb84cba0a8768c9636044ede745";

        private static readonly IReadOnlyList<string> orderedFamilies =
            new ReadOnlyCollection<string>(new[]
            {
                "class_sources",
                "class_families",
                "playable_classes",
                "class_resources",
                "class_skill_trees",
                "class_skill_branches",
                "class_milestone_skills",
                "class_mastery_trials",
                "class_warmaster_identities"
            });

        /// <summary>
        /// Binding manifest order. The common registry exposes ordinal key order.
        /// </summary>
        public static IReadOnlyList<string> FamilyOrder => orderedFamilies;

        public static GameDataCatalogSchemaRegistry CreateRegistry()
        {
            return new GameDataCatalogSchemaRegistry(new[]
            {
                CreateSourceSchema(),
                CreateFamilySchema(),
                CreateClassSchema(),
                CreateResourceSchema(),
                CreateTreeSchema(),
                CreateBranchSchema(),
                CreateMilestoneSchema(),
                CreateMasteryTrialSchema(),
                CreateWarmasterSchema()
            });
        }

        private static GameDataCatalogFamilySchema CreateSourceSchema()
        {
            return RequiredFamily(
                "class_sources",
                new[]
                {
                    Text("packet_id"),
                    Text("packet_version"),
                    Text("packet_sha256"),
                    Text("source_projection_sha256"),
                    Text("authored_revision"),
                    Text("validated_revision"),
                    TextArray("component_ids", ExpectedFamilyCount, ExpectedFamilyCount),
                    StableReferenceArray(
                        "component_family_ids",
                        ExpectedFamilyCount,
                        ExpectedFamilyCount,
                        "class_families"),
                    TextArray("component_paths", ExpectedFamilyCount, ExpectedFamilyCount),
                    TextArray("component_sha256s", ExpectedFamilyCount, ExpectedFamilyCount),
                    CanonicalEnum("content_scope", IdentityScope),
                    Boolean("production_eligible")
                });
        }

        private static GameDataCatalogFamilySchema CreateFamilySchema()
        {
            return RequiredFamily(
                "class_families",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    Integer("source_order", 0, ExpectedFamilyCount - 1),
                    LegacyEnum("legacy_enum_name", "Warrior", "Mage", "Ranger", "Assassin"),
                    Integer("legacy_enum_value", 0, ExpectedFamilyCount - 1),
                    Text("name_ref"),
                    Text("name_text"),
                    Text("identity_source_text"),
                    CanonicalEnumArray(
                        "realm_ids",
                        ExpectedFamilyCount,
                        ExpectedFamilyCount,
                        "stonehold",
                        "eldergrove",
                        "crownlands",
                        "umbral"),
                    StableReferenceArray(
                        "class_ids",
                        4,
                        4,
                        "playable_classes")
                });
        }

        private static GameDataCatalogFamilySchema CreateClassSchema()
        {
            return RequiredFamily(
                "playable_classes",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    StableReference("family_id", "class_families"),
                    Integer("source_order", 0, ExpectedClassCount - 1),
                    Integer("family_order", 0, 3),
                    LegacyEnum(
                        "legacy_subclass_name",
                        "Vanguard",
                        "Guardian",
                        "Berserker",
                        "Pyromancer",
                        "Cryomancer",
                        "Archmage",
                        "Sharpshooter",
                        "Stalker",
                        "Beastmaster",
                        "Shadowblade",
                        "Infiltrator",
                        "Nightstalker",
                        "Paladin",
                        "Necromancer",
                        "Slayer",
                        "Druid"),
                    Integer("legacy_subclass_value", 1, ExpectedClassCount),
                    Text("name_ref"),
                    Text("name_text"),
                    Text("identity_source_text"),
                    StableReference("primary_role_id"),
                    StableReferenceArray(
                        "secondary_role_ids",
                        0,
                        MaximumRoleReferences),
                    StableReferenceArray(
                        "contribution_ids",
                        1,
                        MaximumContributionReferences),
                    StableReference("equipment_armor_id"),
                    StableReferenceArray(
                        "equipment_main_hand_ids",
                        1,
                        MaximumEquipmentReferences),
                    StableReferenceArray(
                        "equipment_off_hand_ids",
                        0,
                        MaximumEquipmentReferences),
                    Text("silhouette_source_text"),
                    StableReference("resource_id", "class_resources"),
                    StableReference("tree_id", "class_skill_trees"),
                    StableReference("mastery_trial_id", "class_mastery_trials"),
                    StableReference("warmaster_set_id", "class_warmaster_identities")
                });
        }

        private static GameDataCatalogFamilySchema CreateResourceSchema()
        {
            return RequiredFamily(
                "class_resources",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    StableReference("class_id", "playable_classes"),
                    Text("name_ref"),
                    Text("name_text"),
                    Text("gain_source_text"),
                    Text("spend_source_text")
                });
        }

        private static GameDataCatalogFamilySchema CreateTreeSchema()
        {
            return RequiredFamily(
                "class_skill_trees",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    StableReference("class_id", "playable_classes"),
                    Integer("visible_level", VisibleFromLevel, VisibleFromLevel),
                    CanonicalEnum("branch_policy", "non_exclusive"),
                    StableReferenceArray(
                        "branch_ids",
                        BranchesPerClass,
                        BranchesPerClass,
                        "class_skill_branches"),
                    StableReferenceArray(
                        "milestone_skill_ids",
                        MilestonesPerClass,
                        MilestonesPerClass,
                        "class_milestone_skills"),
                    IntegerArray(
                        "milestone_levels",
                        MilestonesPerClass,
                        MilestonesPerClass,
                        VisibleFromLevel,
                        LaunchLevelCap),
                    StableReference("capstone_skill_id", "class_milestone_skills"),
                    Integer("active_slot_count", ActiveSkillSlots, ActiveSkillSlots),
                    CanonicalEnum("completeness", IdentityScope),
                    Boolean("production_eligible")
                });
        }

        private static GameDataCatalogFamilySchema CreateBranchSchema()
        {
            return RequiredFamily(
                "class_skill_branches",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    StableReference("class_id", "playable_classes"),
                    StableReference("tree_id", "class_skill_trees"),
                    Integer("branch_order", 0, BranchesPerClass - 1),
                    Text("name_ref"),
                    Text("name_text"),
                    Text("identity_source_text")
                });
        }

        private static GameDataCatalogFamilySchema CreateMilestoneSchema()
        {
            return RequiredFamily(
                "class_milestone_skills",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    StableReference("class_id", "playable_classes"),
                    StableReference("tree_id", "class_skill_trees"),
                    Integer("milestone_level", VisibleFromLevel, LaunchLevelCap),
                    Text("name_ref"),
                    Text("name_text"),
                    Text("identity_source_text"),
                    CanonicalEnum("identity_scope", "class_milestone"),
                    Boolean("production_eligible")
                });
        }

        private static GameDataCatalogFamilySchema CreateMasteryTrialSchema()
        {
            return RequiredFamily(
                "class_mastery_trials",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    StableReference("class_id", "playable_classes"),
                    Text("name_ref"),
                    Text("name_text"),
                    Text("summary_source_text"),
                    Text("availability_source_text"),
                    Text("boundary_source_text"),
                    Integer("minimum_level", LaunchLevelCap, LaunchLevelCap),
                    Boolean("is_optional"),
                    Boolean("is_recoverable"),
                    Boolean("is_critical_path"),
                    Boolean("gates_capstone"),
                    Boolean("gates_warmaster")
                });
        }

        private static GameDataCatalogFamilySchema CreateWarmasterSchema()
        {
            return RequiredFamily(
                "class_warmaster_identities",
                new[]
                {
                    StableReference("source_id", "class_sources"),
                    ComponentId("source_component_id"),
                    StableReference("class_id", "playable_classes"),
                    StableReference("title_id"),
                    Text("title_name_ref"),
                    Text("title_name_text"),
                    Text("set_name_ref"),
                    Text("set_name_text"),
                    StableReference("relic_id"),
                    Text("relic_name_ref"),
                    Text("relic_name_text"),
                    StableReference("ultimate_skill_id"),
                    Text("ultimate_name_ref"),
                    Text("ultimate_name_text"),
                    Text("identity_source_text"),
                    Text("counterplay_source_text"),
                    CanonicalEnumArray(
                        "piece_slot_ids",
                        WarmasterPieceSlots,
                        WarmasterPieceSlots,
                        "weapon",
                        "helm",
                        "chest",
                        "gloves",
                        "boots",
                        "cape",
                        "ring",
                        "amulet",
                        "mount_armor",
                        "class_relic"),
                    Integer("minimum_level", LaunchLevelCap, LaunchLevelCap),
                    Boolean("requires_realm_contract"),
                    Boolean("requires_committed_warzone_points"),
                    Boolean("requires_complete_set"),
                    CanonicalEnum("active_slot_policy", "standard_four_slot"),
                    Boolean("production_eligible")
                });
        }

        private static GameDataCatalogFamilySchema RequiredFamily(
            string family,
            IEnumerable<GameDataCatalogFieldRule> fields)
        {
            return new GameDataCatalogFamilySchema(
                family,
                new[] { SchemaVersion },
                fields,
                allowEmptyRecords: false);
        }

        private static GameDataCatalogFieldRule Text(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true);
        }

        private static GameDataCatalogFieldRule ComponentId(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                allowedStringValues: new[]
                {
                    "CLASS_FAMILY_WARRIOR",
                    "CLASS_FAMILY_MAGE",
                    "CLASS_FAMILY_RANGER",
                    "CLASS_FAMILY_ASSASSIN"
                });
        }

        private static GameDataCatalogFieldRule StableReference(
            string name,
            string referenceFamily = null)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                stableId: true,
                referenceFamily: referenceFamily);
        }

        private static GameDataCatalogFieldRule LegacyEnum(
            string name,
            params string[] values)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                allowedStringValues: values);
        }

        private static GameDataCatalogFieldRule CanonicalEnum(
            string name,
            params string[] values)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                stableId: true,
                allowedStringValues: values);
        }

        private static GameDataCatalogFieldRule Boolean(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Boolean,
                true);
        }

        private static GameDataCatalogFieldRule Integer(
            string name,
            double minimum,
            double maximum)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Number,
                true,
                integerOnly: true,
                minimumNumber: minimum,
                maximumNumber: maximum);
        }

        private static GameDataCatalogFieldRule TextArray(
            string name,
            int minimumItems,
            int maximumItems)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Array,
                true,
                minimumItems: minimumItems,
                maximumItems: maximumItems,
                itemRule: new GameDataCatalogFieldRule(
                    "$item",
                    GameDataValueKind.String,
                    true,
                    nonBlank: true));
        }

        private static GameDataCatalogFieldRule StableReferenceArray(
            string name,
            int minimumItems,
            int maximumItems,
            string referenceFamily = null)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Array,
                true,
                minimumItems: minimumItems,
                maximumItems: maximumItems,
                itemRule: new GameDataCatalogFieldRule(
                    "$item",
                    GameDataValueKind.String,
                    true,
                    nonBlank: true,
                    stableId: true,
                    referenceFamily: referenceFamily));
        }

        private static GameDataCatalogFieldRule CanonicalEnumArray(
            string name,
            int minimumItems,
            int maximumItems,
            params string[] values)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Array,
                true,
                minimumItems: minimumItems,
                maximumItems: maximumItems,
                itemRule: new GameDataCatalogFieldRule(
                    "$item",
                    GameDataValueKind.String,
                    true,
                    nonBlank: true,
                    stableId: true,
                    allowedStringValues: values));
        }

        private static GameDataCatalogFieldRule IntegerArray(
            string name,
            int minimumItems,
            int maximumItems,
            double minimum,
            double maximum)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Array,
                true,
                minimumItems: minimumItems,
                maximumItems: maximumItems,
                itemRule: new GameDataCatalogFieldRule(
                    "$item",
                    GameDataValueKind.Number,
                    true,
                    integerOnly: true,
                    minimumNumber: minimum,
                    maximumNumber: maximum));
        }
    }
}
