using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Strict, non-wired schemas for the three flattened WIRE families.
    /// Records are kind-discriminated, so only <c>kind</c> is required; every
    /// other field is optional and declared so unknown keys fail closed.
    /// SKIP families are intentionally absent.
    /// </summary>
    public static class GameDataWireFamilySchemas
    {
        public const int SchemaVersion = 1;
        public const int MaximumStringReferences = 64;
        public const double MaximumCatalogInteger = int.MaxValue;
        public const double MaximumCatalogFloat = float.MaxValue;

        private static readonly IReadOnlyList<string> orderedFamilies =
            new ReadOnlyCollection<string>(new[]
            {
                "realm_specialized",
                "character_customization",
                "skill_weather"
            });

        public static IReadOnlyList<string> FamilyOrder => orderedFamilies;

        public static readonly IReadOnlyList<string> RealmSpecializedKinds =
            new ReadOnlyCollection<string>(new[]
            {
                "engineering_handoff",
                "localization_draft",
                "narrative_continuity",
                "realm",
                "realm_order",
                "selection_policy"
            });

        public static readonly IReadOnlyList<string> CharacterCustomizationKinds =
            new ReadOnlyCollection<string>(new[]
            {
                "accent_color",
                "armor_style",
                "body_preset",
                "character_slot",
                "eye_color",
                "face_mark",
                "forge_preset",
                "hair_color",
                "hair_style",
                "offhand_style",
                "primary_color",
                "quality_targets",
                "realm_customization",
                "skin_color",
                "weapon_style"
            });

        public static readonly IReadOnlyList<string> SkillWeatherKinds =
            new ReadOnlyCollection<string>(new[]
            {
                "combat_control_profile",
                "effect_module",
                "protected_cue_policy",
                "skill_cast_binding",
                "skill_effect",
                "skill_loadout",
                "weather_profile"
            });

        public static GameDataCatalogSchemaRegistry CreateRegistry()
        {
            return new GameDataCatalogSchemaRegistry(new[]
            {
                CreateRealmSpecializedSchema(),
                CreateCharacterCustomizationSchema(),
                CreateSkillWeatherSchema()
            });
        }

        private static GameDataCatalogFamilySchema CreateRealmSpecializedSchema()
        {
            return RequiredFamily(
                "realm_specialized",
                new[]
                {
                    RequiredKind(RealmSpecializedKinds),
                    OptionalText("selection_mode"),
                    OptionalText("realm_lock_scope"),
                    OptionalText("sub_character_policy"),
                    OptionalText("shared_storage_policy"),
                    OptionalText("cross_realm_creation_policy"),
                    OptionalText("realm_change_policy"),
                    OptionalText("uncommitted_profile_state"),
                    OptionalText("committed_profile_state"),
                    OptionalText("narrative_warning_key"),
                    OptionalText("source_packet_id"),
                    OptionalText("source_mode"),
                    OptionalText("account_lock_summary"),
                    OptionalText("selection_warning_meaning"),
                    OptionalText("uncommitted_meaning"),
                    OptionalText("committed_meaning"),
                    OptionalText("handoff_status"),
                    OptionalStringArray("realm_ids"),
                    OptionalText("legacy_runtime_id"),
                    OptionalText("display_name"),
                    OptionalText("people_name"),
                    OptionalText("adjective"),
                    OptionalText("language_id"),
                    OptionalText("capital_id"),
                    OptionalText("inner_realm_id"),
                    OptionalText("outer_warzone_id"),
                    OptionalText("main_gate_id"),
                    OptionalText("dragon_id"),
                    OptionalStringArray("realm_gem_ids"),
                    OptionalText("sigil"),
                    OptionalObject(
                        "palette",
                        OptionalText("primary"),
                        OptionalText("secondary"),
                        OptionalText("accent")),
                    OptionalObject(
                        "naming_conventions",
                        OptionalStringArray("character_titles"),
                        OptionalStringArray("settlement_terms"),
                        OptionalText("asset_prefix")),
                    OptionalObject(
                        "lore",
                        OptionalText("summary"),
                        OptionalStringArray("identity_pillars"),
                        OptionalText("player_promise")),
                    OptionalObject(
                        "continuity_hooks",
                        OptionalText("account_identity"),
                        OptionalText("sub_character_inheritance"),
                        OptionalText("inner_realm_tone"),
                        OptionalText("outer_warzone_motif"),
                        OptionalText("realm_gem_meaning")),
                    OptionalObject(
                        "starting_hooks",
                        OptionalText("realm_selection_line_key"),
                        OptionalText("first_quest_arc_id"),
                        OptionalStringArray("starter_class_bias")),
                    OptionalObject(
                        "visual_identity",
                        OptionalText("mark_name"),
                        OptionalText("silhouette_language"),
                        OptionalText("material_language")),
                    OptionalInteger("sort_order", 0, MaximumCatalogInteger),
                    OptionalText("key"),
                    OptionalText("text"),
                    OptionalText("consumer"),
                    OptionalBoolean("parse_on_launch"),
                    OptionalStringArray("required_validation"),
                    OptionalStringArray("non_goals_for_this_catalog")
                });
        }

        private static GameDataCatalogFamilySchema CreateCharacterCustomizationSchema()
        {
            return RequiredFamily(
                "character_customization",
                new[]
                {
                    RequiredKind(CharacterCustomizationKinds),
                    OptionalText("legacy_id"),
                    OptionalText("slot"),
                    OptionalText("display_name"),
                    OptionalNumberArray("scale", 3, 3),
                    OptionalNumberArray("rgb", 3, 4),
                    OptionalText("summary"),
                    OptionalText("body_preset_id"),
                    OptionalText("hair_style_id"),
                    OptionalText("armor_style_id"),
                    OptionalText("face_mark_id"),
                    OptionalText("weapon_style_id"),
                    OptionalText("offhand_style_id"),
                    OptionalNumberArray("primary_color", 3, 4),
                    OptionalNumberArray("hair_color", 3, 4),
                    OptionalNumberArray("skin_color", 3, 4),
                    OptionalNumberArray("eye_color", 3, 4),
                    OptionalNumberArray("accent_color", 3, 4),
                    OptionalBoolean("cape_enabled"),
                    OptionalBoolean("helmet_enabled"),
                    OptionalStringArray("material_keys"),
                    OptionalStringArray("customization_focus"),
                    OptionalText("hero_triangles"),
                    OptionalText("medium_triangles"),
                    OptionalText("low_triangles"),
                    OptionalText("far_representation")
                });
        }

        private static GameDataCatalogFamilySchema CreateSkillWeatherSchema()
        {
            return RequiredFamily(
                "skill_weather",
                new[]
                {
                    RequiredKind(SkillWeatherKinds),
                    OptionalInteger("slot", 0, 3),
                    OptionalText("display_name"),
                    OptionalText("role"),
                    OptionalText("vfx_key"),
                    OptionalNumber("cooldown_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("mana_cost", 0, MaximumCatalogFloat),
                    OptionalNumber("cast_time_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("range_meters", 0, MaximumCatalogFloat),
                    OptionalNumber("power", 0, MaximumCatalogFloat),
                    OptionalNumber("bot_damage_multiplier", 0, MaximumCatalogFloat),
                    OptionalText("control_kind"),
                    OptionalNumber("control_duration_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("control_severity", 0, MaximumCatalogFloat),
                    OptionalBoolean("cleanse_soft_control"),
                    OptionalNumber("control_ward_seconds", 0, MaximumCatalogFloat),
                    OptionalText("motion_anticipation_id"),
                    OptionalText("motion_cast_id"),
                    OptionalText("motion_channel_id"),
                    OptionalText("motion_release_id"),
                    OptionalText("motion_recovery_id"),
                    OptionalText("telegraph_module_id"),
                    OptionalText("active_effect_module_id"),
                    OptionalText("impact_module_id"),
                    OptionalText("cleanup_module_id"),
                    OptionalText("accessibility_variant_id"),
                    OptionalText("skill_id"),
                    OptionalText("actor_family"),
                    OptionalText("module_kind"),
                    OptionalText("shape_id"),
                    OptionalText("prefab_id"),
                    OptionalStringArray("protected_cues"),
                    OptionalText("reduced_motion_variant_id"),
                    OptionalBoolean("ownership_readable"),
                    OptionalText("seed_policy"),
                    OptionalStringArray("never_hide"),
                    OptionalStringArray("scalable"),
                    OptionalBoolean("reduced_motion"),
                    OptionalBoolean("photosensitivity"),
                    OptionalNumber("jump_height_meters", 0, MaximumCatalogFloat),
                    OptionalNumber("gravity_meters_per_second_squared", -MaximumCatalogFloat, 0),
                    OptionalNumber("coyote_time_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("jump_buffer_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("air_control_multiplier", 0, 1),
                    OptionalNumber("resolve_min_duration_multiplier", 0, 1),
                    OptionalNumber("resolve_gain_per_second", 0, MaximumCatalogFloat),
                    OptionalNumber("resolve_decay_delay_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("resolve_decay_per_second", 0, MaximumCatalogFloat),
                    OptionalNumber("hard_control_max_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("hard_control_immunity_seconds", 0, MaximumCatalogFloat),
                    OptionalNumber("default_control_resistance", 0, 1),
                    OptionalText("key"),
                    OptionalText("realm"),
                    OptionalText("use"),
                    OptionalStringArray("colors"),
                    OptionalStringArray("particles"),
                    OptionalNumberArray("color", 3, 4),
                    OptionalNumberArray("particle_start_color", 3, 4),
                    OptionalNumberArray("particle_end_color", 3, 4),
                    OptionalInteger("max_particles", 0, MaximumCatalogInteger),
                    OptionalNumber("radius", 0, MaximumCatalogFloat),
                    OptionalNumber("fall_speed", 0, MaximumCatalogFloat),
                    OptionalNumber("particle_size", 0, MaximumCatalogFloat),
                    OptionalNumber("particle_lifetime", 0, MaximumCatalogFloat),
                    OptionalNumber("emission_rate_multiplier", 0, MaximumCatalogFloat),
                    OptionalNumber("horizontal_drift", 0, MaximumCatalogFloat),
                    OptionalNumber("noise_strength", 0, MaximumCatalogFloat),
                    OptionalNumber("noise_frequency", 0, MaximumCatalogFloat),
                    OptionalObject(
                        "wind",
                        OptionalNumber("yaw_degrees", -MaximumCatalogFloat, MaximumCatalogFloat),
                        OptionalNumber("main", 0, MaximumCatalogFloat),
                        OptionalNumber("turbulence", 0, MaximumCatalogFloat),
                        OptionalNumber("pulse_amplitude", 0, MaximumCatalogFloat),
                        OptionalNumber("pulse_frequency", 0, MaximumCatalogFloat)),
                    OptionalObject(
                        "lighting",
                        OptionalBoolean("apply_fog"),
                        OptionalNumberArray("fog_color", 3, 4),
                        OptionalNumber("fog_density", 0, MaximumCatalogFloat),
                        OptionalNumberArray("ambient_color", 3, 4),
                        OptionalNumberArray("directional_light_color", 3, 4),
                        OptionalNumber("directional_light_intensity", 0, MaximumCatalogFloat)),
                    OptionalObject(
                        "lightning",
                        OptionalBoolean("enabled"),
                        OptionalNumberArray("color", 3, 4),
                        OptionalNumber("flash_intensity", 0, MaximumCatalogFloat),
                        OptionalNumber("duration", 0, MaximumCatalogFloat),
                        OptionalNumber("min_delay", 0, MaximumCatalogFloat),
                        OptionalNumber("max_delay", 0, MaximumCatalogFloat))
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

        private static GameDataCatalogFieldRule RequiredKind(IEnumerable<string> allowedValues)
        {
            return new GameDataCatalogFieldRule(
                "kind",
                GameDataValueKind.String,
                true,
                nonBlank: true,
                stableId: true,
                allowedStringValues: allowedValues);
        }

        private static GameDataCatalogFieldRule OptionalText(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                false,
                nonBlank: true);
        }

        private static GameDataCatalogFieldRule OptionalBoolean(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Boolean,
                false);
        }

        private static GameDataCatalogFieldRule OptionalInteger(
            string name,
            double minimum,
            double maximum)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Number,
                false,
                integerOnly: true,
                minimumNumber: minimum,
                maximumNumber: maximum);
        }

        private static GameDataCatalogFieldRule OptionalNumber(
            string name,
            double minimum,
            double maximum)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Number,
                false,
                minimumNumber: minimum,
                maximumNumber: maximum);
        }

        private static GameDataCatalogFieldRule OptionalStringArray(string name)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Array,
                false,
                minimumItems: 0,
                maximumItems: MaximumStringReferences,
                itemRule: new GameDataCatalogFieldRule(
                    "$item",
                    GameDataValueKind.String,
                    true,
                    nonBlank: true));
        }

        private static GameDataCatalogFieldRule OptionalNumberArray(
            string name,
            int minimumItems,
            int maximumItems)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Array,
                false,
                minimumItems: minimumItems,
                maximumItems: maximumItems,
                itemRule: new GameDataCatalogFieldRule(
                    "$item",
                    GameDataValueKind.Number,
                    true,
                    minimumNumber: 0,
                    maximumNumber: MaximumCatalogFloat));
        }

        private static GameDataCatalogFieldRule OptionalObject(
            string name,
            params GameDataCatalogFieldRule[] fields)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.Object,
                false,
                objectFields: fields);
        }
    }
}
