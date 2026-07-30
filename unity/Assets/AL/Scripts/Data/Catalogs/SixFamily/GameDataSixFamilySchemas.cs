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
                    StableReference(
                        "inner_realm_id",
                        allowedStringValues: GameDataRealmReferences.InnerRealmIds),
                    StableReference(
                        "main_gate_id",
                        allowedStringValues: GameDataRealmReferences.MainGateIds),
                    StableReference(
                        "outer_warzone_id",
                        allowedStringValues: GameDataRealmReferences.OuterWarzoneIds),
                    StableReference(
                        "rare_resource_id",
                        allowedStringValues: GameDataWalletResourceReferences.StableIds),
                    StableReferenceArray(
                        "capability_profile_ids",
                        1,
                        1,
                        allowedStringValues: GameDataRealmCapabilityProfiles.StableIds),
                    AssetReference(
                        "asset_ref",
                        GameDataRealmReferences.AssetReferences)
                },
                new[]
                {
                    new GameDataCatalogRecordConstraint(
                        "realm_rare_resource_reference",
                        "rare_resource_id",
                        "REALM-RARE-RESOURCE-REFERENCE",
                        "The realm name, numeric value, and rare-resource ID do not match the reviewed exact relation.",
                        ValidateRealmRareResourceRelation),
                    new GameDataCatalogRecordConstraint(
                        "realm_capability_profile_reference",
                        "capability_profile_ids",
                        "REALM-CAPABILITY-PROFILE-REFERENCE",
                        "The realm and capability-profile IDs do not match the reviewed exact relation.",
                        ValidateRealmCapabilityProfileRelation),
                    new GameDataCatalogRecordConstraint(
                        "realm_world_asset_reference",
                        "asset_ref",
                        "REALM-WORLD-ASSET-REFERENCE",
                        "The realm identity, content, world-boundary, and asset " +
                        "references do not match the reviewed exact relation.",
                        ValidateRealmWorldAssetRelation)
                });
        }

        private static GameDataCatalogFamilySchema CreateBuildingSchema()
        {
            return RequiredFamily(
                "buildings",
                new[]
                {
                    LegacyEnum(
                        "legacy_building_id",
                        GameDataBuildingProgressionRegistry.LegacyBuildingIds),
                    ContentReference(
                        "name_ref",
                        GameDataBuildingProgressionRegistry.NameReferences),
                    Integer(
                        "initial_level",
                        GameDataBuildingProgressionRegistry.InitialLevel,
                        GameDataBuildingProgressionRegistry.InitialLevel),
                    Integer(
                        "max_level",
                        GameDataBuildingProgressionRegistry.MaximumLevel,
                        GameDataBuildingProgressionRegistry.MaximumLevel),
                    StableReferenceArray("production_profile_ids", 1, MaximumProfileReferences),
                    StableReference(
                        "cost_profile_id",
                        allowedStringValues:
                        GameDataBuildingProgressionRegistry.CostProfileStableIds),
                    StableReference(
                        "duration_profile_id",
                        allowedStringValues: new[]
                        {
                            GameDataBuildingProgressionRegistry.DurationProfileStableId
                        }),
                    StableReference(
                        "prerequisite_profile_id",
                        allowedStringValues: new[]
                        {
                            GameDataBuildingProgressionRegistry.PrerequisiteProfileStableId
                        }),
                    StableReference(
                        "realm_eligibility_profile_id",
                        allowedStringValues: new[]
                        {
                            GameDataBuildingProgressionRegistry
                                .RealmEligibilityProfileStableId
                        }),
                    AssetReference("asset_ref")
                },
                new[]
                {
                    new GameDataCatalogRecordConstraint(
                        "building_progression_reference",
                        "cost_profile_id",
                        "BUILDING-PROGRESSION-REFERENCE",
                        "The building identity, content, level, and progression-profile " +
                        "references do not match the reviewed exact relation.",
                        ValidateBuildingProgressionRelation)
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
            IEnumerable<GameDataCatalogFieldRule> fields,
            IEnumerable<GameDataCatalogRecordConstraint> recordConstraints = null)
        {
            return new GameDataCatalogFamilySchema(
                family,
                new[] { SchemaVersion },
                fields,
                allowEmptyRecords: false,
                recordConstraints: recordConstraints);
        }

        private static GameDataCatalogFieldRule ContentReference(
            string name,
            IEnumerable<string> allowedStringValues = null)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                allowedStringValues: allowedStringValues);
        }

        private static GameDataCatalogFieldRule AssetReference(
            string name,
            IEnumerable<string> allowedStringValues = null)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                allowedStringValues: allowedStringValues);
        }

        private static GameDataCatalogFieldRule StableReference(
            string name,
            string referenceFamily = null,
            IEnumerable<string> allowedStringValues = null)
        {
            return new GameDataCatalogFieldRule(
                name,
                GameDataValueKind.String,
                true,
                nonBlank: true,
                stableId: true,
                referenceFamily: referenceFamily,
                allowedStringValues: allowedStringValues);
        }

        private static bool? ValidateRealmRareResourceRelation(
            string realmStableId,
            IReadOnlyDictionary<string, GameDataValue> fields)
        {
            string legacyName;
            int legacyValue;
            string resourceId;
            if (!TryReadRealmIdentity(fields, out legacyName, out legacyValue) ||
                !TryReadString(fields, "rare_resource_id", out resourceId))
            {
                return null;
            }

            return GameDataRealmReferences.IsApprovedRareResourceRelation(
                realmStableId,
                legacyName,
                legacyValue,
                resourceId);
        }

        private static bool? ValidateRealmWorldAssetRelation(
            string realmStableId,
            IReadOnlyDictionary<string, GameDataValue> fields)
        {
            string legacyName;
            int legacyValue;
            string nameReference;
            string descriptionReference;
            string innerRealmId;
            string mainGateId;
            string outerWarzoneId;
            string assetReference;
            if (!TryReadRealmIdentity(fields, out legacyName, out legacyValue) ||
                !TryReadString(fields, "name_ref", out nameReference) ||
                !TryReadString(fields, "description_ref", out descriptionReference) ||
                !TryReadString(fields, "inner_realm_id", out innerRealmId) ||
                !TryReadString(fields, "main_gate_id", out mainGateId) ||
                !TryReadString(fields, "outer_warzone_id", out outerWarzoneId) ||
                !TryReadString(fields, "asset_ref", out assetReference))
            {
                return null;
            }

            return GameDataRealmReferences.IsApprovedWorldAssetRelation(
                realmStableId,
                legacyName,
                legacyValue,
                nameReference,
                descriptionReference,
                innerRealmId,
                mainGateId,
                outerWarzoneId,
                assetReference);
        }

        private static bool? ValidateRealmCapabilityProfileRelation(
            string realmStableId,
            IReadOnlyDictionary<string, GameDataValue> fields)
        {
            IReadOnlyList<string> profileIds;
            if (!TryReadStringArray(fields, "capability_profile_ids", out profileIds))
            {
                return null;
            }

            return GameDataRealmCapabilityProfiles.IsApprovedRealmRelation(
                realmStableId,
                profileIds);
        }

        private static bool? ValidateBuildingProgressionRelation(
            string buildingStableId,
            IReadOnlyDictionary<string, GameDataValue> fields)
        {
            string legacyBuildingId;
            string nameReference;
            long initialLevel;
            long maximumLevel;
            string costProfileStableId;
            string durationProfileStableId;
            string prerequisiteProfileStableId;
            string realmEligibilityProfileStableId;
            if (!TryReadString(
                    fields,
                    "legacy_building_id",
                    out legacyBuildingId) ||
                !TryReadString(fields, "name_ref", out nameReference) ||
                !TryReadInteger(fields, "initial_level", out initialLevel) ||
                !TryReadInteger(fields, "max_level", out maximumLevel) ||
                !TryReadString(
                    fields,
                    "cost_profile_id",
                    out costProfileStableId) ||
                !TryReadString(
                    fields,
                    "duration_profile_id",
                    out durationProfileStableId) ||
                !TryReadString(
                    fields,
                    "prerequisite_profile_id",
                    out prerequisiteProfileStableId) ||
                !TryReadString(
                    fields,
                    "realm_eligibility_profile_id",
                    out realmEligibilityProfileStableId))
            {
                return null;
            }

            if (initialLevel < int.MinValue ||
                initialLevel > int.MaxValue ||
                maximumLevel < int.MinValue ||
                maximumLevel > int.MaxValue)
            {
                return false;
            }

            return GameDataBuildingProgressionRegistry
                .IsApprovedBuildingRelation(
                    buildingStableId,
                    legacyBuildingId,
                    nameReference,
                    (int)initialLevel,
                    (int)maximumLevel,
                    costProfileStableId,
                    durationProfileStableId,
                    prerequisiteProfileStableId,
                    realmEligibilityProfileStableId);
        }

        private static bool TryReadRealmIdentity(
            IReadOnlyDictionary<string, GameDataValue> fields,
            out string legacyName,
            out int legacyValue)
        {
            legacyName = null;
            legacyValue = 0;
            GameDataValue legacyNumericValue;
            if (!TryReadString(fields, "legacy_realm_id", out legacyName) ||
                !fields.TryGetValue("legacy_realm_value", out legacyNumericValue))
            {
                return false;
            }

            var legacyNumber = legacyNumericValue as GameDataNumberValue;
            long exactLegacyValue;
            if (legacyNumber == null ||
                !legacyNumber.TryGetInt64(out exactLegacyValue) ||
                exactLegacyValue < int.MinValue ||
                exactLegacyValue > int.MaxValue)
            {
                return false;
            }

            legacyValue = (int)exactLegacyValue;
            return true;
        }

        private static bool TryReadString(
            IReadOnlyDictionary<string, GameDataValue> fields,
            string fieldName,
            out string value)
        {
            GameDataValue fieldValue;
            var stringValue =
                fields.TryGetValue(fieldName, out fieldValue)
                    ? fieldValue as GameDataStringValue
                    : null;
            value = stringValue == null ? null : stringValue.Value;
            return stringValue != null;
        }

        private static bool TryReadInteger(
            IReadOnlyDictionary<string, GameDataValue> fields,
            string fieldName,
            out long value)
        {
            value = 0L;
            GameDataValue fieldValue;
            var numberValue =
                fields.TryGetValue(fieldName, out fieldValue)
                    ? fieldValue as GameDataNumberValue
                    : null;
            return numberValue != null &&
                   numberValue.TryGetInt64(out value);
        }

        private static bool TryReadStringArray(
            IReadOnlyDictionary<string, GameDataValue> fields,
            string fieldName,
            out IReadOnlyList<string> values)
        {
            GameDataValue fieldValue;
            var arrayValue =
                fields.TryGetValue(fieldName, out fieldValue)
                    ? fieldValue as GameDataArrayValue
                    : null;
            if (arrayValue == null)
            {
                values = null;
                return false;
            }

            var mutableValues = new string[arrayValue.Count];
            for (var index = 0; index < arrayValue.Count; index++)
            {
                var stringValue = arrayValue.Items[index] as GameDataStringValue;
                if (stringValue == null)
                {
                    values = null;
                    return false;
                }

                mutableValues[index] = stringValue.Value;
            }

            values = mutableValues;
            return true;
        }

        private static GameDataCatalogFieldRule LegacyEnum(
            string name,
            params string[] allowedValues)
        {
            return LegacyEnum(
                name,
                (IEnumerable<string>)allowedValues);
        }

        private static GameDataCatalogFieldRule LegacyEnum(
            string name,
            IEnumerable<string> allowedValues)
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
            string referenceFamily = null,
            IEnumerable<string> allowedStringValues = null)
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
                    referenceFamily: referenceFamily,
                    allowedStringValues: allowedStringValues));
        }
    }
}
