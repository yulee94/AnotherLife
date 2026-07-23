using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Strict, non-wired schemas for the first six game-data families.
    /// This registry defines production record shape only; it contains no records,
    /// performs no loading, and is not registered with a runtime service.
    /// </summary>
    public static class GameDataSixFamilySchemas
    {
        public const int SchemaVersion = 1;
        public const int MaximumProfileReferences = 64;
        public const int MaximumPrerequisiteReferences = 64;
        public const int MaximumChampionSkillReferences = 16;
        public const double MaximumCatalogInteger = int.MaxValue;
        public const double MaximumCatalogFloat = float.MaxValue;

        private static readonly IReadOnlyList<string> orderedFamilies =
            new ReadOnlyCollection<string>(new[]
            {
                "realms",
                "buildings",
                "research",
                "troops",
                "champions",
                "skills"
            });

        /// <summary>
        /// Binding manifest order. The common registry exposes schemas in ordinal key order,
        /// so generators must consume this list when emitting a manifest.
        /// </summary>
        public static IReadOnlyList<string> FamilyOrder => orderedFamilies;

        public static GameDataCatalogSchemaRegistry CreateRegistry()
        {
            return new GameDataCatalogSchemaRegistry(new[]
            {
                CreateRealmSchema(),
                CreateBuildingSchema(),
                CreateResearchSchema(),
                CreateTroopSchema(),
                CreateChampionSchema(),
                CreateSkillSchema()
            });
        }

        private static GameDataCatalogFamilySchema CreateRealmSchema()
        {
            return RequiredFamily(
                "realms",
                new[]
                {
                    LegacyEnum("legacy_realm_id", "Stonehold", "Eldergrove", "Crownlands", "Umbral"),
                    Integer("legacy_realm_value", 1, 4),
                    ContentReference("name_ref"),
                    ContentReference("description_ref"),
                    StableReference("rare_resource_id"),
                    StableReferenceArray("capability_profile_ids", 1, MaximumProfileReferences),
                    AssetReference("asset_ref")
                });
        }

        private static GameDataCatalogFamilySchema CreateBuildingSchema()
        {
            return RequiredFamily(
                "buildings",
                new[]
                {
                    ContentReference("name_ref"),
                    Integer("max_level", 1, MaximumCatalogInteger),
                    StableReferenceArray("production_profile_ids", 1, MaximumProfileReferences),
                    StableReference("cost_profile_id"),
                    StableReference("duration_profile_id"),
                    AssetReference("asset_ref")
                });
        }

        private static GameDataCatalogFamilySchema CreateResearchSchema()
        {
            return RequiredFamily(
                "research",
                new[]
                {
                    ContentReference("name_ref"),
                    Integer("max_level", 1, MaximumCatalogInteger),
                    StableReference("cost_profile_id"),
                    StableReference("duration_profile_id"),
                    StableReferenceArray("effect_ids", 1, MaximumProfileReferences),
                    StableReferenceArray(
                        "prerequisite_research_ids",
                        0,
                        MaximumPrerequisiteReferences,
                        "research")
                });
        }

        private static GameDataCatalogFamilySchema CreateTroopSchema()
        {
            return RequiredFamily(
                "troops",
                new[]
                {
                    LegacyEnum("legacy_troop_type", "Infantry", "Cavalry", "Ranged", "Siege"),
                    Integer("legacy_troop_value", 0, 3),
                    ContentReference("name_ref"),
                    Integer("base_attack", 0, MaximumCatalogInteger),
                    Integer("base_defense", 0, MaximumCatalogInteger),
                    StableReference("training_profile_id"),
                    AssetReference("asset_ref")
                });
        }

        private static GameDataCatalogFamilySchema CreateChampionSchema()
        {
            return RequiredFamily(
                "champions",
                new[]
                {
                    ContentReference("name_ref"),
                    StableReference("realm_id", "realms"),
                    CanonicalEnum("class_family_id", "warrior", "mage", "ranger", "assassin"),
                    AssetReference("portrait_asset_ref"),
                    AssetReference("model_asset_ref"),
                    StableReferenceArray(
                        "base_skill_ids",
                        1,
                        MaximumChampionSkillReferences,
                        "skills"),
                    StableReference("stat_profile_id")
                });
        }

        private static GameDataCatalogFamilySchema CreateSkillSchema()
        {
            return RequiredFamily(
                "skills",
                new[]
                {
                    ContentReference("name_ref"),
                    StableReference("behavior_profile_id"),
                    StableReference("presentation_profile_id"),
                    CanonicalEnum("target_type", "single", "aoe", "self", "ally", "enemy"),
                    Number("cooldown_seconds", 0, MaximumCatalogFloat),
                    Number("power", 0, MaximumCatalogFloat),
                    Number("mana_cost", 0, MaximumCatalogFloat),
                    Number("cast_time_seconds", 0, MaximumCatalogFloat),
                    Number("range_meters", 0, MaximumCatalogFloat),
                    AssetReference("vfx_asset_ref"),
                    AssetReference("audio_asset_ref")
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

        private static GameDataCatalogFieldRule ContentReference(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true);
        }

        private static GameDataCatalogFieldRule AssetReference(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true);
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
            params string[] allowedValues)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                allowedStringValues: allowedValues);
        }

        private static GameDataCatalogFieldRule CanonicalEnum(
            string name,
            params string[] allowedValues)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                stableId: true,
                allowedStringValues: allowedValues);
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

        private static GameDataCatalogFieldRule Number(
            string name,
            double minimum,
            double maximum)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Number,
                true,
                minimumNumber: minimum,
                maximumNumber: maximum);
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
    }
}
