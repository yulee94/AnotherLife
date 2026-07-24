using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AL.Data.Catalogs
{
    /// <summary>
    /// Semantic validation layered after the strict generic catalog validator.
    /// A successful result publishes only an immutable identity spine and is never
    /// evidence that combat, balance, persistence, or runtime activation is ready.
    /// </summary>
    public static class ClassProgressionCatalogValidator
    {
        private const int ExpectedLocalizedNameCount = 244;

        private static readonly string[] ExpectedFamilyInventory =
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
        };

        private static readonly string[] ExpectedRealmIds =
        {
            "stonehold",
            "eldergrove",
            "crownlands",
            "umbral"
        };

        private static readonly long[] ExpectedMilestoneLevels =
        {
            10,
            20,
            30,
            40,
            50
        };

        private static readonly string[] ExpectedWarmasterPieceSlots =
        {
            "weapon",
            "helm",
            "chest",
            "gloves",
            "boots",
            "cape",
            "ring",
            "amulet",
            "mount_armor",
            "class_relic"
        };

        private static readonly ExpectedFamily[] ExpectedFamilies =
        {
            new ExpectedFamily(
                "family_warrior",
                "Warrior",
                0,
                "CLASS_FAMILY_WARRIOR",
                "unity/Docs/Narrative/Classes/Families/ANOTHERLIFE_CLASSES.00-warrior.json",
                "f7198c0a64f815c467a417f5bc7c04fccb1dc55b4ce874d86d57ee94dd89875e",
                new[]
                {
                    "class_vanguard",
                    "class_guardian",
                    "class_berserker",
                    "class_paladin"
                }),
            new ExpectedFamily(
                "family_mage",
                "Mage",
                1,
                "CLASS_FAMILY_MAGE",
                "unity/Docs/Narrative/Classes/Families/ANOTHERLIFE_CLASSES.01-mage.json",
                "3379253bd321658476347bbec987f30551d15a6c74d05187d764552a57d36637",
                new[]
                {
                    "class_pyromancer",
                    "class_cryomancer",
                    "class_archmage",
                    "class_necromancer"
                }),
            new ExpectedFamily(
                "family_ranger",
                "Ranger",
                2,
                "CLASS_FAMILY_RANGER",
                "unity/Docs/Narrative/Classes/Families/ANOTHERLIFE_CLASSES.02-ranger.json",
                "1acfed117da4ecd7c2a92d6347e433254516f464e9a64f8cbeefcaea6d664792",
                new[]
                {
                    "class_sharpshooter",
                    "class_stalker",
                    "class_beastmaster",
                    "class_druid"
                }),
            new ExpectedFamily(
                "family_assassin",
                "Assassin",
                3,
                "CLASS_FAMILY_ASSASSIN",
                "unity/Docs/Narrative/Classes/Families/ANOTHERLIFE_CLASSES.03-assassin.json",
                "7a368c0937a37ad9498057b487f36f42d36eb501e857f52a3b03cadbd2274fe9",
                new[]
                {
                    "class_shadowblade",
                    "class_infiltrator",
                    "class_nightstalker",
                    "class_slayer"
                })
        };

        private static readonly ExpectedClass[] ExpectedClasses =
        {
            new ExpectedClass("class_vanguard", "Vanguard", 1, "family_warrior", 0),
            new ExpectedClass("class_guardian", "Guardian", 2, "family_warrior", 1),
            new ExpectedClass("class_berserker", "Berserker", 3, "family_warrior", 2),
            new ExpectedClass("class_pyromancer", "Pyromancer", 4, "family_mage", 0),
            new ExpectedClass("class_cryomancer", "Cryomancer", 5, "family_mage", 1),
            new ExpectedClass("class_archmage", "Archmage", 6, "family_mage", 2),
            new ExpectedClass("class_sharpshooter", "Sharpshooter", 7, "family_ranger", 0),
            new ExpectedClass("class_stalker", "Stalker", 8, "family_ranger", 1),
            new ExpectedClass("class_beastmaster", "Beastmaster", 9, "family_ranger", 2),
            new ExpectedClass("class_shadowblade", "Shadowblade", 10, "family_assassin", 0),
            new ExpectedClass("class_infiltrator", "Infiltrator", 11, "family_assassin", 1),
            new ExpectedClass("class_nightstalker", "Nightstalker", 12, "family_assassin", 2),
            new ExpectedClass("class_paladin", "Paladin", 13, "family_warrior", 3),
            new ExpectedClass("class_necromancer", "Necromancer", 14, "family_mage", 3),
            new ExpectedClass("class_slayer", "Slayer", 15, "family_assassin", 3),
            new ExpectedClass("class_druid", "Druid", 16, "family_ranger", 3)
        };

        public static ClassProgressionCatalogValidationResult Validate(
            GameDataCatalogSetSnapshot catalogSet)
        {
            var diagnostics = new List<GameDataCatalogDiagnostic>();
            if (catalogSet == null)
            {
                Add(
                    diagnostics,
                    "CLS-CATALOG-UNAVAILABLE",
                    string.Empty,
                    string.Empty,
                    "$",
                    "No generically validated catalog set was supplied.",
                    "Load one complete class-progression catalog set before semantic validation.");
                return new ClassProgressionCatalogValidationResult(
                    ClassProgressionCatalogValidationStatus.CatalogUnavailable,
                    null,
                    diagnostics);
            }

            if (catalogSet.SchemaVersion != GameDataClassProgressionSchemas.SchemaVersion)
            {
                Add(
                    diagnostics,
                    "CLS-SCHEMA-VERSION",
                    string.Empty,
                    string.Empty,
                    "$.schemaVersion",
                    "The catalog-set schema version is not supported by this class contract.",
                    "Compile the packet with the supported class-progression schema.");
                return new ClassProgressionCatalogValidationResult(
                    ClassProgressionCatalogValidationStatus.UnsupportedVersion,
                    null,
                    diagnostics);
            }

            ValidateSetIdentity(catalogSet, diagnostics);
            ValidateFamilyInventory(catalogSet, diagnostics);
            ValidateAliasBoundary(catalogSet, diagnostics);

            var localizationKeys = new List<string>();
            ValidateSource(catalogSet, diagnostics);
            ValidateFamilies(catalogSet, localizationKeys, diagnostics);
            ValidateClasses(catalogSet, localizationKeys, diagnostics);
            ValidateOwnedProgressionRecords(catalogSet, localizationKeys, diagnostics);
            ValidateLocalizationInventory(localizationKeys, diagnostics);
            ValidateSourceProjection(catalogSet, diagnostics);

            if (diagnostics.Count > 0)
            {
                return new ClassProgressionCatalogValidationResult(
                    ClassProgressionCatalogValidationStatus.CatalogInvalid,
                    null,
                    diagnostics);
            }

            return new ClassProgressionCatalogValidationResult(
                ClassProgressionCatalogValidationStatus.AcceptedIdentitySpine,
                new ClassProgressionIdentitySnapshot(catalogSet),
                new GameDataCatalogDiagnostic[0]);
        }

        private static void ValidateSetIdentity(
            GameDataCatalogSetSnapshot catalogSet,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            CheckEqual(
                catalogSet.GameId,
                GameDataCatalogContract.DefaultGameId,
                diagnostics,
                "CLS-GAME-ID",
                string.Empty,
                string.Empty,
                "$.gameId",
                "The class catalog must target the canonical game ID.");
            CheckEqual(
                catalogSet.ContentVersion,
                GameDataClassProgressionSchemas.PacketVersion,
                diagnostics,
                "CLS-PACKET-VERSION",
                string.Empty,
                string.Empty,
                "$.contentVersion",
                "The class catalog content version must equal the accepted packet version.");
            CheckEqual(
                catalogSet.CatalogSetId,
                GameDataClassProgressionSchemas.CatalogSetId,
                diagnostics,
                "CLS-CATALOG-SET-ID",
                string.Empty,
                string.Empty,
                "$.catalogSetId",
                "The class catalog-set ID must match the reviewed compiler contract.");
            CheckEqual(
                catalogSet.SourceRevision,
                GameDataClassProgressionSchemas.ValidatedRevision,
                diagnostics,
                "CLS-SOURCE-REVISION",
                string.Empty,
                string.Empty,
                "$.sourceRevision",
                "The catalog source revision must pin the validated narrative source.");

            if (catalogSet.SourceKind != GameDataCatalogSourceKind.Packaged)
            {
                Add(
                    diagnostics,
                    "CLS-SOURCE-KIND",
                    string.Empty,
                    string.Empty,
                    "$.sourceKind",
                    "A development fallback cannot become accepted class identity authority.",
                    "Validate exact packaged artifacts from the accepted packet.");
            }

            if (catalogSet.Artifacts.Count != ExpectedFamilyInventory.Length)
            {
                Add(
                    diagnostics,
                    "CLS-ARTIFACT-DESCRIPTOR",
                    string.Empty,
                    string.Empty,
                    "$.artifacts",
                    "The manifest must declare exactly the nine required class artifacts.",
                    "Regenerate the complete reviewed manifest.");
            }

            var descriptorCount = Math.Min(
                catalogSet.Artifacts.Count,
                ExpectedFamilyInventory.Length);
            for (var index = 0; index < descriptorCount; index++)
            {
                var descriptor = catalogSet.Artifacts[index];
                var expectedFamily = ExpectedFamilyInventory[index];
                var path = "$.artifacts[" +
                           index.ToString(CultureInfo.InvariantCulture) +
                           "]";
                CheckEqual(
                    descriptor.Family,
                    expectedFamily,
                    diagnostics,
                    "CLS-ARTIFACT-DESCRIPTOR",
                    expectedFamily,
                    string.Empty,
                    path + ".family",
                    "Artifact binding order must match the reviewed class manifest.");
                CheckEqual(
                    descriptor.CatalogId,
                    expectedFamily + "_identity_v001",
                    diagnostics,
                    "CLS-ARTIFACT-DESCRIPTOR",
                    expectedFamily,
                    string.Empty,
                    path + ".catalogId",
                    "Artifact catalog IDs must use the reviewed deterministic identity.");
                CheckEqual(
                    descriptor.RelativePath,
                    "Catalogs/" + expectedFamily + ".identity_v001.json",
                    diagnostics,
                    "CLS-ARTIFACT-DESCRIPTOR",
                    expectedFamily,
                    string.Empty,
                    path + ".relativePath",
                    "Artifact paths must use the reviewed deterministic location.");
                CheckEqual(
                    descriptor.SchemaVersion,
                    GameDataClassProgressionSchemas.SchemaVersion,
                    diagnostics,
                    "CLS-ARTIFACT-DESCRIPTOR",
                    expectedFamily,
                    string.Empty,
                    path + ".schemaVersion",
                    "Artifact schema versions must match the class contract.");
                CheckEqual(
                    descriptor.ContentVersion,
                    GameDataClassProgressionSchemas.PacketVersion,
                    diagnostics,
                    "CLS-ARTIFACT-DESCRIPTOR",
                    expectedFamily,
                    string.Empty,
                    path + ".contentVersion",
                    "Artifact content versions must match the accepted packet.");
                CheckEqual(
                    descriptor.SourceMode,
                    GameDataClassProgressionSchemas.CatalogSourceMode,
                    diagnostics,
                    "CLS-ARTIFACT-DESCRIPTOR",
                    expectedFamily,
                    string.Empty,
                    path + ".sourceMode",
                    "Every class artifact must declare authored source mode.");
                CheckEqual(
                    descriptor.SourceRevision,
                    GameDataClassProgressionSchemas.ValidatedRevision,
                    diagnostics,
                    "CLS-SOURCE-REVISION",
                    expectedFamily,
                    string.Empty,
                    path + ".sourceRevision",
                    "Every artifact must pin the validated narrative revision.");
                if (!descriptor.Required)
                {
                    Add(
                        diagnostics,
                        "CLS-ARTIFACT-REQUIRED",
                        expectedFamily,
                        string.Empty,
                        path + ".required",
                        "All nine class-progression artifacts are mandatory.",
                        "Mark every artifact required and reject partial publication.");
                }

                var family = GetFamily(catalogSet, expectedFamily);
                if (family != null)
                {
                    CheckEqual(
                        family.CatalogId,
                        expectedFamily + "_identity_v001",
                        diagnostics,
                        "CLS-ARTIFACT-DESCRIPTOR",
                        expectedFamily,
                        string.Empty,
                        "$.catalogId",
                        "Loaded family identity must match its manifest descriptor.");
                    CheckEqual(
                        family.SourceRevision,
                        GameDataClassProgressionSchemas.ValidatedRevision,
                        diagnostics,
                        "CLS-SOURCE-REVISION",
                        expectedFamily,
                        string.Empty,
                        "$.sourceRevision",
                        "Loaded family source revision must match the validated narrative source.");
                }
            }
        }

        private static void ValidateFamilyInventory(
            GameDataCatalogSetSnapshot catalogSet,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var actualOrder = catalogSet.Families.Select(family => family.Family).ToArray();
            if (!actualOrder.SequenceEqual(ExpectedFamilyInventory, StringComparer.Ordinal))
            {
                Add(
                    diagnostics,
                    "CLS-FAMILY-INVENTORY",
                    string.Empty,
                    string.Empty,
                    "$.artifacts",
                    "The class catalog must contain the exact nine required families in binding order.",
                    "Recompile one complete atomic class-progression catalog set.");
            }

            CheckFamilyCount(
                catalogSet,
                "class_sources",
                GameDataClassProgressionSchemas.ExpectedSourceCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "class_families",
                GameDataClassProgressionSchemas.ExpectedFamilyCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "playable_classes",
                GameDataClassProgressionSchemas.ExpectedClassCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "class_resources",
                GameDataClassProgressionSchemas.ExpectedResourceCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "class_skill_trees",
                GameDataClassProgressionSchemas.ExpectedTreeCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "class_skill_branches",
                GameDataClassProgressionSchemas.ExpectedBranchCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "class_milestone_skills",
                GameDataClassProgressionSchemas.ExpectedMilestoneCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "class_mastery_trials",
                GameDataClassProgressionSchemas.ExpectedMasteryTrialCount,
                diagnostics);
            CheckFamilyCount(
                catalogSet,
                "class_warmaster_identities",
                GameDataClassProgressionSchemas.ExpectedWarmasterCount,
                diagnostics);
        }

        private static void ValidateAliasBoundary(
            GameDataCatalogSetSnapshot catalogSet,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            for (var index = 0; index < catalogSet.Families.Count; index++)
            {
                var family = catalogSet.Families[index];
                if (string.Equals(family.Family, "class_mastery_trials", StringComparison.Ordinal))
                {
                    continue;
                }

                if (family.Aliases.Count != 0)
                {
                    Add(
                        diagnostics,
                        "CLS-UNAUTHORIZED-ALIAS",
                        family.Family,
                        string.Empty,
                        "$.aliases",
                        "Only exact SQ_* mastery-trial aliases are authorized.",
                        "Remove class, Forge, visual, prototype-skill, or Warmaster aliases.");
                }
            }
        }

        private static void ValidateSource(
            GameDataCatalogSetSnapshot catalogSet,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var family = GetFamily(catalogSet, "class_sources");
            if (family == null)
            {
                return;
            }

            if (!HasExactRecordIds(
                    family,
                    new[] { GameDataClassProgressionSchemas.SourceRecordId }))
            {
                Add(
                    diagnostics,
                    "CLS-SOURCE-INVENTORY",
                    family.Family,
                    string.Empty,
                    "$.records",
                    "Exactly one canonical source record is required.",
                    "Compile provenance from the accepted packet without substitutes.");
                return;
            }

            var record = family.RecordsById[GameDataClassProgressionSchemas.SourceRecordId];
            CheckEqual(
                ReadString(record, "packet_id", family.Family, diagnostics),
                GameDataClassProgressionSchemas.PacketId,
                diagnostics,
                "CLS-PACKET-ID",
                family.Family,
                record.Id,
                "$.packet_id",
                "The packet ID does not match the accepted source.");
            CheckEqual(
                ReadString(record, "packet_version", family.Family, diagnostics),
                GameDataClassProgressionSchemas.PacketVersion,
                diagnostics,
                "CLS-PACKET-VERSION",
                family.Family,
                record.Id,
                "$.packet_version",
                "The packet version does not match the accepted source.");
            CheckEqual(
                ReadString(record, "packet_sha256", family.Family, diagnostics),
                GameDataClassProgressionSchemas.PacketSha256,
                diagnostics,
                "CLS-PACKET-HASH",
                family.Family,
                record.Id,
                "$.packet_sha256",
                "The packet SHA-256 does not match the accepted source.");
            CheckEqual(
                ReadString(record, "source_projection_sha256", family.Family, diagnostics),
                GameDataClassProgressionSchemas.SourceProjectionSha256,
                diagnostics,
                "CLS-SOURCE-PROJECTION",
                family.Family,
                record.Id,
                "$.source_projection_sha256",
                "The declared canonical projection does not match the accepted source.");
            CheckEqual(
                ReadString(record, "authored_revision", family.Family, diagnostics),
                GameDataClassProgressionSchemas.AuthoredRevision,
                diagnostics,
                "CLS-AUTHORED-REVISION",
                family.Family,
                record.Id,
                "$.authored_revision",
                "The authored packet revision does not match the accepted provenance.");
            CheckEqual(
                ReadString(record, "validated_revision", family.Family, diagnostics),
                GameDataClassProgressionSchemas.ValidatedRevision,
                diagnostics,
                "CLS-VALIDATED-REVISION",
                family.Family,
                record.Id,
                "$.validated_revision",
                "The source-validation revision does not match the accepted provenance.");
            CheckEqual(
                ReadString(record, "content_scope", family.Family, diagnostics),
                GameDataClassProgressionSchemas.IdentityScope,
                diagnostics,
                "CLS-SOURCE-SCOPE",
                family.Family,
                record.Id,
                "$.content_scope",
                "The first class catalog is limited to the identity spine.");
            CheckBoolean(
                record,
                "production_eligible",
                false,
                family.Family,
                "CLS-PRODUCTION-ELIGIBILITY",
                diagnostics);

            CheckStringArray(
                ReadStringArray(record, "component_ids", family.Family, diagnostics),
                ExpectedFamilies.Select(item => item.ComponentId).ToArray(),
                diagnostics,
                "CLS-COMPONENT-IDS",
                family.Family,
                record.Id,
                "$.component_ids",
                "The component ID inventory or order does not match the accepted packet.");
            CheckStringArray(
                ReadStringArray(record, "component_family_ids", family.Family, diagnostics),
                ExpectedFamilies.Select(item => item.Id).ToArray(),
                diagnostics,
                "CLS-COMPONENT-FAMILIES",
                family.Family,
                record.Id,
                "$.component_family_ids",
                "The component-to-family mapping does not match the accepted packet.");
            CheckStringArray(
                ReadStringArray(record, "component_paths", family.Family, diagnostics),
                ExpectedFamilies.Select(item => item.Path).ToArray(),
                diagnostics,
                "CLS-COMPONENT-PATHS",
                family.Family,
                record.Id,
                "$.component_paths",
                "The component path inventory or order does not match the accepted packet.");
            CheckStringArray(
                ReadStringArray(record, "component_sha256s", family.Family, diagnostics),
                ExpectedFamilies.Select(item => item.Sha256).ToArray(),
                diagnostics,
                "CLS-COMPONENT-HASHES",
                family.Family,
                record.Id,
                "$.component_sha256s",
                "The component hash inventory or order does not match the accepted packet.");
        }

        private static void ValidateFamilies(
            GameDataCatalogSetSnapshot catalogSet,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var family = GetFamily(catalogSet, "class_families");
            if (family == null)
            {
                return;
            }

            if (!HasExactRecordIds(family, ExpectedFamilies.Select(item => item.Id)))
            {
                Add(
                    diagnostics,
                    "CLS-ROSTER-FAMILIES",
                    family.Family,
                    string.Empty,
                    "$.records",
                    "The exact Warrior, Mage, Ranger, and Assassin family IDs are required.",
                    "Restore the accepted four-family inventory.");
            }

            for (var index = 0; index < ExpectedFamilies.Length; index++)
            {
                var expected = ExpectedFamilies[index];
                GameDataCatalogRecord record;
                if (!family.RecordsById.TryGetValue(expected.Id, out record))
                {
                    continue;
                }

                CheckCommonSource(record, family.Family, expected.ComponentId, diagnostics);
                CheckEqual(
                    ReadInt(record, "source_order", family.Family, diagnostics),
                    expected.Order,
                    diagnostics,
                    "CLS-FAMILY-ORDER",
                    family.Family,
                    record.Id,
                    "$.source_order",
                    "Family source order must match the accepted packet.");
                CheckEqual(
                    ReadString(record, "legacy_enum_name", family.Family, diagnostics),
                    expected.EnumName,
                    diagnostics,
                    "CLS-FAMILY-ENUM",
                    family.Family,
                    record.Id,
                    "$.legacy_enum_name",
                    "Family legacy-enum evidence must match exactly.");
                CheckEqual(
                    ReadInt(record, "legacy_enum_value", family.Family, diagnostics),
                    expected.Order,
                    diagnostics,
                    "CLS-FAMILY-ENUM",
                    family.Family,
                    record.Id,
                    "$.legacy_enum_value",
                    "Family legacy-enum evidence must match exactly.");
                ValidateName(
                    record,
                    "name_ref",
                    "class_family." + Token(expected.Id, "family_") + ".name",
                    family.Family,
                    localizationKeys,
                    diagnostics);
                CheckEqual(
                    ReadString(record, "name_text", family.Family, diagnostics),
                    expected.EnumName,
                    diagnostics,
                    "CLS-FAMILY-NAME",
                    family.Family,
                    record.Id,
                    "$.name_text",
                    "Family English source text must match the accepted packet.");
                CheckStringArray(
                    ReadStringArray(record, "realm_ids", family.Family, diagnostics),
                    ExpectedRealmIds,
                    diagnostics,
                    "CLS-REALM-AVAILABILITY",
                    family.Family,
                    record.Id,
                    "$.realm_ids",
                    "Every family must be available in all four launch realms.");
                CheckStringArray(
                    ReadStringArray(record, "class_ids", family.Family, diagnostics),
                    expected.ClassIds,
                    diagnostics,
                    "CLS-FAMILY-MAPPING",
                    family.Family,
                    record.Id,
                    "$.class_ids",
                    "Family membership must use the accepted explicit four-class mapping.");
            }
        }

        private static void ValidateClasses(
            GameDataCatalogSetSnapshot catalogSet,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var family = GetFamily(catalogSet, "playable_classes");
            if (family == null)
            {
                return;
            }

            if (!HasExactRecordIds(family, ExpectedClasses.Select(item => item.Id)))
            {
                Add(
                    diagnostics,
                    "CLS-ROSTER-CLASSES",
                    family.Family,
                    string.Empty,
                    "$.records",
                    "The exact accepted 16-class roster is required.",
                    "Restore every canonical class exactly once.");
            }

            for (var index = 0; index < ExpectedClasses.Length; index++)
            {
                var expected = ExpectedClasses[index];
                GameDataCatalogRecord record;
                if (!family.RecordsById.TryGetValue(expected.Id, out record))
                {
                    continue;
                }

                var expectedFamily = FindExpectedFamily(expected.FamilyId);
                CheckCommonSource(record, family.Family, expectedFamily.ComponentId, diagnostics);
                CheckEqual(
                    ReadString(record, "family_id", family.Family, diagnostics),
                    expected.FamilyId,
                    diagnostics,
                    "CLS-CLASS-FAMILY",
                    family.Family,
                    record.Id,
                    "$.family_id",
                    "Class ownership must use the explicit accepted family mapping.");
                CheckEqual(
                    ReadInt(record, "source_order", family.Family, diagnostics),
                    index,
                    diagnostics,
                    "CLS-CLASS-ORDER",
                    family.Family,
                    record.Id,
                    "$.source_order",
                    "Class source order must match the accepted legacy roster order.");
                CheckEqual(
                    ReadInt(record, "family_order", family.Family, diagnostics),
                    expected.FamilyOrder,
                    diagnostics,
                    "CLS-CLASS-FAMILY-ORDER",
                    family.Family,
                    record.Id,
                    "$.family_order",
                    "Class family-local order must match the accepted component.");
                CheckEqual(
                    ReadString(record, "legacy_subclass_name", family.Family, diagnostics),
                    expected.EnumName,
                    diagnostics,
                    "CLS-CLASS-ENUM",
                    family.Family,
                    record.Id,
                    "$.legacy_subclass_name",
                    "Class legacy-enum name must match exactly.");
                CheckEqual(
                    ReadInt(record, "legacy_subclass_value", family.Family, diagnostics),
                    expected.EnumValue,
                    diagnostics,
                    "CLS-CLASS-ENUM",
                    family.Family,
                    record.Id,
                    "$.legacy_subclass_value",
                    "Class legacy-enum value must match exactly.");

                var classToken = Token(record.Id, "class_");
                ValidateName(
                    record,
                    "name_ref",
                    "class." + classToken + ".name",
                    family.Family,
                    localizationKeys,
                    diagnostics);
                CheckEqual(
                    ReadString(record, "name_text", family.Family, diagnostics),
                    expected.EnumName,
                    diagnostics,
                    "CLS-CLASS-NAME",
                    family.Family,
                    record.Id,
                    "$.name_text",
                    "Class English source text must match the accepted packet.");
                CheckOwnedPrefix(
                    ReadString(record, "resource_id", family.Family, diagnostics),
                    "class_resource_" + classToken + "_",
                    diagnostics,
                    "CLS-RESOURCE-OWNER",
                    family.Family,
                    record.Id,
                    "$.resource_id");
                CheckEqual(
                    ReadString(record, "tree_id", family.Family, diagnostics),
                    "skill_tree_" + classToken + "_general",
                    diagnostics,
                    "CLS-TREE-ID",
                    family.Family,
                    record.Id,
                    "$.tree_id",
                    "Each class must reference its deterministic general-tree ID.");
                CheckOwnedPrefix(
                    ReadString(record, "mastery_trial_id", family.Family, diagnostics),
                    "class_trial_" + classToken + "_",
                    diagnostics,
                    "CLS-TRIAL-OWNER",
                    family.Family,
                    record.Id,
                    "$.mastery_trial_id");
                CheckOwnedPrefix(
                    ReadString(record, "warmaster_set_id", family.Family, diagnostics),
                    "warmaster_set_" + classToken + "_",
                    diagnostics,
                    "CLS-WARMASTER-OWNER",
                    family.Family,
                    record.Id,
                    "$.warmaster_set_id");

                CheckUniqueArray(
                    ReadStringArray(record, "secondary_role_ids", family.Family, diagnostics),
                    diagnostics,
                    "CLS-ROLE-DUPLICATE",
                    family.Family,
                    record.Id,
                    "$.secondary_role_ids");
                CheckUniqueArray(
                    ReadStringArray(record, "contribution_ids", family.Family, diagnostics),
                    diagnostics,
                    "CLS-CONTRIBUTION-DUPLICATE",
                    family.Family,
                    record.Id,
                    "$.contribution_ids");
                CheckUniqueArray(
                    ReadStringArray(record, "equipment_main_hand_ids", family.Family, diagnostics),
                    diagnostics,
                    "CLS-EQUIPMENT-DUPLICATE",
                    family.Family,
                    record.Id,
                    "$.equipment_main_hand_ids");
                CheckUniqueArray(
                    ReadStringArray(record, "equipment_off_hand_ids", family.Family, diagnostics),
                    diagnostics,
                    "CLS-EQUIPMENT-DUPLICATE",
                    family.Family,
                    record.Id,
                    "$.equipment_off_hand_ids");
            }

            ValidateSupportPolicy(family, diagnostics);
        }

        private static void ValidateSupportPolicy(
            GameDataFamilyCatalogSnapshot classes,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            for (var index = 0; index < ExpectedClasses.Length; index++)
            {
                var expected = ExpectedClasses[index];
                GameDataCatalogRecord record;
                if (!classes.RecordsById.TryGetValue(expected.Id, out record))
                {
                    continue;
                }

                var primary = ReadString(
                    record,
                    "primary_role_id",
                    classes.Family,
                    diagnostics);
                var secondary = ReadStringArray(
                    record,
                    "secondary_role_ids",
                    classes.Family,
                    diagnostics);
                var isPrimaryHealer =
                    string.Equals(primary, "healer", StringComparison.Ordinal);
                var isSecondaryHealer =
                    secondary.Contains("healer", StringComparer.Ordinal);
                var shouldBePrimaryHealer =
                    string.Equals(record.Id, "class_druid", StringComparison.Ordinal);
                var shouldBeSecondaryHealer =
                    string.Equals(record.Id, "class_paladin", StringComparison.Ordinal);

                if (isPrimaryHealer != shouldBePrimaryHealer)
                {
                    Add(
                        diagnostics,
                        "CLS-HEALER-POLICY",
                        classes.Family,
                        record.Id,
                        "$.primary_role_id",
                        "Druid is the only accepted primary healer.",
                        "Restore the exact primary-healer ownership from the accepted packet.");
                }

                if (isSecondaryHealer != shouldBeSecondaryHealer)
                {
                    Add(
                        diagnostics,
                        "CLS-HEALER-POLICY",
                        classes.Family,
                        record.Id,
                        "$.secondary_role_ids",
                        "Paladin is the only accepted secondary healer.",
                        "Restore the exact secondary-healer ownership from the accepted packet.");
                }
            }
        }

        private static void ValidateOwnedProgressionRecords(
            GameDataCatalogSetSnapshot catalogSet,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var classes = GetFamily(catalogSet, "playable_classes");
            var resources = GetFamily(catalogSet, "class_resources");
            var trees = GetFamily(catalogSet, "class_skill_trees");
            var branches = GetFamily(catalogSet, "class_skill_branches");
            var milestones = GetFamily(catalogSet, "class_milestone_skills");
            var trials = GetFamily(catalogSet, "class_mastery_trials");
            var warmasters = GetFamily(catalogSet, "class_warmaster_identities");
            if (classes == null ||
                resources == null ||
                trees == null ||
                branches == null ||
                milestones == null ||
                trials == null ||
                warmasters == null)
            {
                return;
            }

            var referencedResources = new List<string>();
            var referencedTrees = new List<string>();
            var referencedBranches = new List<string>();
            var referencedMilestones = new List<string>();
            var referencedTrials = new List<string>();
            var referencedWarmasters = new List<string>();

            for (var classIndex = 0; classIndex < ExpectedClasses.Length; classIndex++)
            {
                var expectedClass = ExpectedClasses[classIndex];
                GameDataCatalogRecord classRecord;
                if (!classes.RecordsById.TryGetValue(expectedClass.Id, out classRecord))
                {
                    continue;
                }

                var componentId = FindExpectedFamily(expectedClass.FamilyId).ComponentId;
                var classToken = Token(classRecord.Id, "class_");
                var resourceId = ReadString(classRecord, "resource_id", classes.Family, diagnostics);
                var treeId = ReadString(classRecord, "tree_id", classes.Family, diagnostics);
                var trialId = ReadString(classRecord, "mastery_trial_id", classes.Family, diagnostics);
                var warmasterId = ReadString(classRecord, "warmaster_set_id", classes.Family, diagnostics);
                referencedResources.Add(resourceId);
                referencedTrees.Add(treeId);
                referencedTrials.Add(trialId);
                referencedWarmasters.Add(warmasterId);

                ValidateResource(
                    resources,
                    resourceId,
                    classRecord.Id,
                    classToken,
                    componentId,
                    localizationKeys,
                    diagnostics);
                ValidateTree(
                    trees,
                    branches,
                    milestones,
                    treeId,
                    classRecord.Id,
                    classToken,
                    componentId,
                    referencedBranches,
                    referencedMilestones,
                    localizationKeys,
                    diagnostics);
                ValidateTrial(
                    trials,
                    trialId,
                    classRecord.Id,
                    classToken,
                    expectedClass.EnumName,
                    componentId,
                    localizationKeys,
                    diagnostics);
                ValidateWarmaster(
                    warmasters,
                    warmasterId,
                    classRecord.Id,
                    classToken,
                    componentId,
                    localizationKeys,
                    diagnostics);
            }

            CheckReferencedInventory(resources, referencedResources, "CLS-RESOURCE-INVENTORY", diagnostics);
            CheckReferencedInventory(trees, referencedTrees, "CLS-TREE-INVENTORY", diagnostics);
            CheckReferencedInventory(branches, referencedBranches, "CLS-BRANCH-INVENTORY", diagnostics);
            CheckReferencedInventory(milestones, referencedMilestones, "CLS-MILESTONE-INVENTORY", diagnostics);
            CheckReferencedInventory(trials, referencedTrials, "CLS-TRIAL-INVENTORY", diagnostics);
            CheckReferencedInventory(warmasters, referencedWarmasters, "CLS-WARMASTER-INVENTORY", diagnostics);
            ValidateTrialAliases(trials, diagnostics);
        }

        private static void ValidateResource(
            GameDataFamilyCatalogSnapshot family,
            string resourceId,
            string classId,
            string classToken,
            string componentId,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataCatalogRecord record;
            if (!family.RecordsById.TryGetValue(resourceId, out record))
            {
                return;
            }

            CheckCommonSource(record, family.Family, componentId, diagnostics);
            CheckEqual(
                ReadString(record, "class_id", family.Family, diagnostics),
                classId,
                diagnostics,
                "CLS-RESOURCE-RECIPROCITY",
                family.Family,
                record.Id,
                "$.class_id",
                "A class resource must point back to its owning class.");
            CheckOwnedPrefix(
                record.Id,
                "class_resource_" + classToken + "_",
                diagnostics,
                "CLS-RESOURCE-OWNER",
                family.Family,
                record.Id,
                "$.id");
            ValidateName(
                record,
                "name_ref",
                NameKey(record.Id, "class_resource_", "class_resource."),
                family.Family,
                localizationKeys,
                diagnostics);
        }

        private static void ValidateTree(
            GameDataFamilyCatalogSnapshot trees,
            GameDataFamilyCatalogSnapshot branches,
            GameDataFamilyCatalogSnapshot milestones,
            string treeId,
            string classId,
            string classToken,
            string componentId,
            List<string> referencedBranches,
            List<string> referencedMilestones,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataCatalogRecord tree;
            if (!trees.RecordsById.TryGetValue(treeId, out tree))
            {
                return;
            }

            CheckCommonSource(tree, trees.Family, componentId, diagnostics);
            CheckEqual(
                tree.Id,
                "skill_tree_" + classToken + "_general",
                diagnostics,
                "CLS-TREE-ID",
                trees.Family,
                tree.Id,
                "$.id",
                "The general-tree ID must be deterministic and class-owned.");
            CheckEqual(
                ReadString(tree, "class_id", trees.Family, diagnostics),
                classId,
                diagnostics,
                "CLS-TREE-RECIPROCITY",
                trees.Family,
                tree.Id,
                "$.class_id",
                "A class tree must point back to its owning class.");
            CheckEqual(
                ReadInt(tree, "visible_level", trees.Family, diagnostics),
                GameDataClassProgressionSchemas.VisibleFromLevel,
                diagnostics,
                "CLS-TREE-POLICY",
                trees.Family,
                tree.Id,
                "$.visible_level",
                "The general tree is visible from level 1.");
            CheckEqual(
                ReadString(tree, "branch_policy", trees.Family, diagnostics),
                "non_exclusive",
                diagnostics,
                "CLS-TREE-POLICY",
                trees.Family,
                tree.Id,
                "$.branch_policy",
                "All three identity branches are non-exclusive.");
            CheckEqual(
                ReadInt(tree, "active_slot_count", trees.Family, diagnostics),
                GameDataClassProgressionSchemas.ActiveSkillSlots,
                diagnostics,
                "CLS-LOADOUT-POLICY",
                trees.Family,
                tree.Id,
                "$.active_slot_count",
                "The initial active loadout contains exactly four slots.");
            CheckEqual(
                ReadString(tree, "completeness", trees.Family, diagnostics),
                GameDataClassProgressionSchemas.IdentityScope,
                diagnostics,
                "CLS-TREE-COMPLETENESS",
                trees.Family,
                tree.Id,
                "$.completeness",
                "The packet defines an identity spine, not a complete playable tree.");
            CheckBoolean(
                tree,
                "production_eligible",
                false,
                trees.Family,
                "CLS-PRODUCTION-ELIGIBILITY",
                diagnostics);

            var branchIds = ReadStringArray(tree, "branch_ids", trees.Family, diagnostics);
            var milestoneIds = ReadStringArray(
                tree,
                "milestone_skill_ids",
                trees.Family,
                diagnostics);
            var milestoneLevels = ReadIntArray(
                tree,
                "milestone_levels",
                trees.Family,
                diagnostics);
            CheckUniqueArray(
                branchIds,
                diagnostics,
                "CLS-BRANCH-DUPLICATE",
                trees.Family,
                tree.Id,
                "$.branch_ids");
            CheckUniqueArray(
                milestoneIds,
                diagnostics,
                "CLS-MILESTONE-DUPLICATE",
                trees.Family,
                tree.Id,
                "$.milestone_skill_ids");
            CheckIntArray(
                milestoneLevels,
                ExpectedMilestoneLevels,
                diagnostics,
                "CLS-MILESTONE-LEVELS",
                trees.Family,
                tree.Id,
                "$.milestone_levels",
                "Milestone levels must be exactly 10, 20, 30, 40, and 50.");

            for (var index = 0; index < branchIds.Count; index++)
            {
                referencedBranches.Add(branchIds[index]);
                GameDataCatalogRecord branch;
                if (!branches.RecordsById.TryGetValue(branchIds[index], out branch))
                {
                    continue;
                }

                CheckCommonSource(branch, branches.Family, componentId, diagnostics);
                CheckOwnedPrefix(
                    branch.Id,
                    "skill_branch_" + classToken + "_",
                    diagnostics,
                    "CLS-BRANCH-OWNER",
                    branches.Family,
                    branch.Id,
                    "$.id");
                CheckEqual(
                    ReadString(branch, "class_id", branches.Family, diagnostics),
                    classId,
                    diagnostics,
                    "CLS-BRANCH-RECIPROCITY",
                    branches.Family,
                    branch.Id,
                    "$.class_id",
                    "A branch must point back to its owning class.");
                CheckEqual(
                    ReadString(branch, "tree_id", branches.Family, diagnostics),
                    tree.Id,
                    diagnostics,
                    "CLS-BRANCH-RECIPROCITY",
                    branches.Family,
                    branch.Id,
                    "$.tree_id",
                    "A branch must point back to its owning tree.");
                CheckEqual(
                    ReadInt(branch, "branch_order", branches.Family, diagnostics),
                    index,
                    diagnostics,
                    "CLS-BRANCH-ORDER",
                    branches.Family,
                    branch.Id,
                    "$.branch_order",
                    "Branch order must agree with the owning tree.");
                ValidateName(
                    branch,
                    "name_ref",
                    NameKey(branch.Id, "skill_branch_", "skill_branch."),
                    branches.Family,
                    localizationKeys,
                    diagnostics);
            }

            for (var index = 0; index < milestoneIds.Count; index++)
            {
                referencedMilestones.Add(milestoneIds[index]);
                GameDataCatalogRecord milestone;
                if (!milestones.RecordsById.TryGetValue(milestoneIds[index], out milestone))
                {
                    continue;
                }

                CheckCommonSource(milestone, milestones.Family, componentId, diagnostics);
                CheckOwnedPrefix(
                    milestone.Id,
                    "skill_" + classToken + "_",
                    diagnostics,
                    "CLS-MILESTONE-OWNER",
                    milestones.Family,
                    milestone.Id,
                    "$.id");
                CheckEqual(
                    ReadString(milestone, "class_id", milestones.Family, diagnostics),
                    classId,
                    diagnostics,
                    "CLS-MILESTONE-RECIPROCITY",
                    milestones.Family,
                    milestone.Id,
                    "$.class_id",
                    "A milestone must point back to its owning class.");
                CheckEqual(
                    ReadString(milestone, "tree_id", milestones.Family, diagnostics),
                    tree.Id,
                    diagnostics,
                    "CLS-MILESTONE-RECIPROCITY",
                    milestones.Family,
                    milestone.Id,
                    "$.tree_id",
                    "A milestone must point back to its owning tree.");
                if (index < ExpectedMilestoneLevels.Length)
                {
                    CheckEqual(
                        ReadInt(milestone, "milestone_level", milestones.Family, diagnostics),
                        ExpectedMilestoneLevels[index],
                        diagnostics,
                        "CLS-MILESTONE-LEVEL",
                        milestones.Family,
                        milestone.Id,
                        "$.milestone_level",
                        "Milestone level must agree with its ordered tree anchor.");
                }
                CheckEqual(
                    ReadString(milestone, "identity_scope", milestones.Family, diagnostics),
                    "class_milestone",
                    diagnostics,
                    "CLS-MILESTONE-SCOPE",
                    milestones.Family,
                    milestone.Id,
                    "$.identity_scope",
                    "Milestones are class-owned identity anchors, not executable definitions.");
                CheckBoolean(
                    milestone,
                    "production_eligible",
                    false,
                    milestones.Family,
                    "CLS-PRODUCTION-ELIGIBILITY",
                    diagnostics);
                ValidateName(
                    milestone,
                    "name_ref",
                    NameKey(milestone.Id, "skill_", "skill."),
                    milestones.Family,
                    localizationKeys,
                    diagnostics);
            }

            if (milestoneIds.Count > 0)
            {
                CheckEqual(
                    ReadString(tree, "capstone_skill_id", trees.Family, diagnostics),
                    milestoneIds[milestoneIds.Count - 1],
                    diagnostics,
                    "CLS-CAPSTONE-BOUNDARY",
                    trees.Family,
                    tree.Id,
                    "$.capstone_skill_id",
                    "The ordinary level-50 milestone is the class capstone.");
            }
        }

        private static void ValidateTrial(
            GameDataFamilyCatalogSnapshot family,
            string trialId,
            string classId,
            string classToken,
            string legacyClassName,
            string componentId,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataCatalogRecord trial;
            if (!family.RecordsById.TryGetValue(trialId, out trial))
            {
                return;
            }

            CheckCommonSource(trial, family.Family, componentId, diagnostics);
            CheckOwnedPrefix(
                trial.Id,
                "class_trial_" + classToken + "_",
                diagnostics,
                "CLS-TRIAL-OWNER",
                family.Family,
                trial.Id,
                "$.id");
            CheckEqual(
                ReadString(trial, "class_id", family.Family, diagnostics),
                classId,
                diagnostics,
                "CLS-TRIAL-RECIPROCITY",
                family.Family,
                trial.Id,
                "$.class_id",
                "A mastery trial must point back to its owning class.");
            ValidateName(
                trial,
                "name_ref",
                NameKey(trial.Id, "class_trial_", "class_trial."),
                family.Family,
                localizationKeys,
                diagnostics);
            CheckEqual(
                ReadInt(trial, "minimum_level", family.Family, diagnostics),
                GameDataClassProgressionSchemas.LaunchLevelCap,
                diagnostics,
                "CLS-TRIAL-LEVEL",
                family.Family,
                trial.Id,
                "$.minimum_level",
                "The mastery trial becomes available upon reaching the launch level cap.");
            CheckBoolean(
                trial,
                "is_optional",
                true,
                family.Family,
                "CLS-TRIAL-BOUNDARY",
                diagnostics);
            CheckBoolean(
                trial,
                "is_recoverable",
                true,
                family.Family,
                "CLS-TRIAL-BOUNDARY",
                diagnostics);
            CheckBoolean(
                trial,
                "is_critical_path",
                false,
                family.Family,
                "CLS-TRIAL-BOUNDARY",
                diagnostics);
            CheckBoolean(
                trial,
                "gates_capstone",
                false,
                family.Family,
                "CLS-TRIAL-BOUNDARY",
                diagnostics);
            CheckBoolean(
                trial,
                "gates_warmaster",
                false,
                family.Family,
                "CLS-TRIAL-BOUNDARY",
                diagnostics);

            GameDataCatalogAlias alias;
            if (!family.AliasesByLegacyId.TryGetValue("SQ_" + legacyClassName, out alias) ||
                !string.Equals(alias.CanonicalId, trial.Id, StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    "CLS-TRIAL-ALIAS",
                    family.Family,
                    trial.Id,
                    "$.aliases",
                    "The exact legacy SQ_* alias must resolve only to this mastery trial.",
                    "Restore the packet-authored mastery-trial alias mapping.");
            }
        }

        private static void ValidateWarmaster(
            GameDataFamilyCatalogSnapshot family,
            string setId,
            string classId,
            string classToken,
            string componentId,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataCatalogRecord record;
            if (!family.RecordsById.TryGetValue(setId, out record))
            {
                return;
            }

            CheckCommonSource(record, family.Family, componentId, diagnostics);
            CheckOwnedPrefix(
                record.Id,
                "warmaster_set_" + classToken + "_",
                diagnostics,
                "CLS-WARMASTER-OWNER",
                family.Family,
                record.Id,
                "$.id");
            CheckEqual(
                ReadString(record, "class_id", family.Family, diagnostics),
                classId,
                diagnostics,
                "CLS-WARMASTER-RECIPROCITY",
                family.Family,
                record.Id,
                "$.class_id",
                "A Warmaster identity must point back to its owning class.");

            var titleId = ReadString(record, "title_id", family.Family, diagnostics);
            var relicId = ReadString(record, "relic_id", family.Family, diagnostics);
            var ultimateId = ReadString(record, "ultimate_skill_id", family.Family, diagnostics);
            CheckOwnedPrefix(
                titleId,
                "warmaster_title_" + classToken + "_",
                diagnostics,
                "CLS-WARMASTER-OWNER",
                family.Family,
                record.Id,
                "$.title_id");
            CheckOwnedPrefix(
                relicId,
                "warmaster_relic_" + classToken + "_",
                diagnostics,
                "CLS-WARMASTER-OWNER",
                family.Family,
                record.Id,
                "$.relic_id");
            CheckOwnedPrefix(
                ultimateId,
                "skill_" + classToken + "_true_warmaster_",
                diagnostics,
                "CLS-WARMASTER-OWNER",
                family.Family,
                record.Id,
                "$.ultimate_skill_id");

            ValidateName(
                record,
                "title_name_ref",
                NameKey(titleId, "warmaster_title_", "warmaster_title."),
                family.Family,
                localizationKeys,
                diagnostics);
            ValidateName(
                record,
                "set_name_ref",
                NameKey(record.Id, "warmaster_set_", "warmaster_set."),
                family.Family,
                localizationKeys,
                diagnostics);
            ValidateName(
                record,
                "relic_name_ref",
                NameKey(relicId, "warmaster_relic_", "warmaster_relic."),
                family.Family,
                localizationKeys,
                diagnostics);
            ValidateName(
                record,
                "ultimate_name_ref",
                "skill." +
                classToken +
                ".true_warmaster." +
                Token(
                    ultimateId,
                    "skill_" + classToken + "_true_warmaster_") +
                ".name",
                family.Family,
                localizationKeys,
                diagnostics);
            CheckStringArray(
                ReadStringArray(record, "piece_slot_ids", family.Family, diagnostics),
                ExpectedWarmasterPieceSlots,
                diagnostics,
                "CLS-WARMASTER-PIECES",
                family.Family,
                record.Id,
                "$.piece_slot_ids",
                "Warmaster identity requires the exact ten distinct class-set slots.");
            CheckEqual(
                ReadInt(record, "minimum_level", family.Family, diagnostics),
                GameDataClassProgressionSchemas.LaunchLevelCap,
                diagnostics,
                "CLS-WARMASTER-LEVEL",
                family.Family,
                record.Id,
                "$.minimum_level",
                "True Warmaster eligibility begins at level 50, never level 51.");
            CheckBoolean(
                record,
                "requires_realm_contract",
                true,
                family.Family,
                "CLS-WARMASTER-ELIGIBILITY",
                diagnostics);
            CheckBoolean(
                record,
                "requires_committed_warzone_points",
                true,
                family.Family,
                "CLS-WARMASTER-ELIGIBILITY",
                diagnostics);
            CheckBoolean(
                record,
                "requires_complete_set",
                true,
                family.Family,
                "CLS-WARMASTER-ELIGIBILITY",
                diagnostics);
            CheckEqual(
                ReadString(record, "active_slot_policy", family.Family, diagnostics),
                "standard_four_slot",
                diagnostics,
                "CLS-WARMASTER-SLOT",
                family.Family,
                record.Id,
                "$.active_slot_policy",
                "A True Warmaster skill uses the same four-slot loadout surface.");
            CheckBoolean(
                record,
                "production_eligible",
                false,
                family.Family,
                "CLS-PRODUCTION-ELIGIBILITY",
                diagnostics);
        }

        private static void ValidateTrialAliases(
            GameDataFamilyCatalogSnapshot family,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var expected = ExpectedClasses
                .Select(item => "SQ_" + item.EnumName)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var actual = family.Aliases
                .Select(item => item.LegacyId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                Add(
                    diagnostics,
                    "CLS-TRIAL-ALIAS-INVENTORY",
                    family.Family,
                    string.Empty,
                    "$.aliases",
                    "The mastery family must contain exactly the sixteen preserved SQ_* aliases.",
                    "Remove visual aliases and restore every exact legacy mastery alias.");
            }

            for (var index = 0; index < family.Aliases.Count; index++)
            {
                var alias = family.Aliases[index];
                if (alias.IntroducedVersion != 1 ||
                    alias.RetirementVersion.HasValue ||
                    !string.Equals(alias.MigrationIssue, "#14", StringComparison.Ordinal))
                {
                    Add(
                        diagnostics,
                        "CLS-TRIAL-ALIAS-METADATA",
                        family.Family,
                        alias.CanonicalId,
                        "$.aliases",
                        "Mastery-trial alias migration metadata must remain explicit and unretired.",
                        "Restore introducedVersion 1, null retirement, and the #14 provenance marker.");
                }
            }
        }

        private static void ValidateLocalizationInventory(
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var unique = new HashSet<string>(localizationKeys, StringComparer.Ordinal);
            if (localizationKeys.Count != ExpectedLocalizedNameCount ||
                unique.Count != ExpectedLocalizedNameCount)
            {
                Add(
                    diagnostics,
                    "CLS-LOCALIZATION-INVENTORY",
                    string.Empty,
                    string.Empty,
                    "$.name_ref",
                    "The identity spine must expose exactly 244 unique owner-derived name references.",
                    "Restore all family, class, resource, branch, milestone, trial, and Warmaster name keys.");
            }
        }

        private static void ValidateSourceProjection(
            GameDataCatalogSetSnapshot catalogSet,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var actual = ComputeCanonicalSourceProjectionSha256(catalogSet);
            CheckEqual(
                actual,
                GameDataClassProgressionSchemas.SourceProjectionSha256,
                diagnostics,
                "CLS-SOURCE-PROJECTION",
                "class_sources",
                GameDataClassProgressionSchemas.SourceRecordId,
                "$.records",
                "The canonical projection of every class record and alias does not match the accepted packet.");
        }

        public static string ComputeCanonicalSourceProjectionSha256(
            GameDataCatalogSetSnapshot catalogSet)
        {
            if (catalogSet == null)
            {
                throw new ArgumentNullException(nameof(catalogSet));
            }

            var builder = new StringBuilder(128 * 1024);
            AppendProjectionToken(builder, "class_progression_projection_v1");
            for (var familyIndex = 0;
                 familyIndex < ExpectedFamilyInventory.Length;
                 familyIndex++)
            {
                var familyId = ExpectedFamilyInventory[familyIndex];
                AppendProjectionToken(builder, familyId);
                var family = GetFamily(catalogSet, familyId);
                if (family == null)
                {
                    AppendProjectionToken(builder, "<missing-family>");
                    continue;
                }

                builder.Append(family.Records.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(';');
                for (var recordIndex = 0; recordIndex < family.Records.Count; recordIndex++)
                {
                    var record = family.Records[recordIndex];
                    AppendProjectionToken(builder, record.Id);
                    foreach (var field in record.Fields)
                    {
                        if (string.Equals(familyId, "class_sources", StringComparison.Ordinal) &&
                            string.Equals(
                                field.Key,
                                "source_projection_sha256",
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        AppendProjectionToken(builder, field.Key);
                        AppendProjectionValue(builder, field.Value);
                    }
                }

                builder.Append(family.Aliases.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(';');
                for (var aliasIndex = 0; aliasIndex < family.Aliases.Count; aliasIndex++)
                {
                    var alias = family.Aliases[aliasIndex];
                    AppendProjectionToken(builder, alias.LegacyId);
                    AppendProjectionToken(builder, alias.CanonicalId);
                    builder.Append(alias.IntroducedVersion.ToString(CultureInfo.InvariantCulture));
                    builder.Append(';');
                    if (alias.RetirementVersion.HasValue)
                    {
                        builder.Append(
                            alias.RetirementVersion.Value.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append('~');
                    }
                    builder.Append(';');
                    AppendProjectionToken(builder, alias.MigrationIssue);
                }
            }

            return GameDataCatalogValidator.ComputeSha256(
                new UTF8Encoding(false, true).GetBytes(builder.ToString()));
        }

        private static void AppendProjectionValue(
            StringBuilder builder,
            GameDataValue value)
        {
            if (value == null)
            {
                builder.Append("X;");
                return;
            }

            builder.Append(((int)value.Kind).ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            var stringValue = value as GameDataStringValue;
            if (stringValue != null)
            {
                AppendProjectionToken(builder, stringValue.Value);
                return;
            }

            var numberValue = value as GameDataNumberValue;
            if (numberValue != null)
            {
                AppendProjectionToken(builder, numberValue.RawValue);
                return;
            }

            var booleanValue = value as GameDataBooleanValue;
            if (booleanValue != null)
            {
                builder.Append(booleanValue.Value ? "1;" : "0;");
                return;
            }

            var arrayValue = value as GameDataArrayValue;
            if (arrayValue != null)
            {
                builder.Append(arrayValue.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(';');
                for (var index = 0; index < arrayValue.Count; index++)
                {
                    AppendProjectionValue(builder, arrayValue.Items[index]);
                }
                return;
            }

            var objectValue = value as GameDataObjectValue;
            if (objectValue != null)
            {
                builder.Append(objectValue.Properties.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(';');
                foreach (var field in objectValue.Properties)
                {
                    AppendProjectionToken(builder, field.Key);
                    AppendProjectionValue(builder, field.Value);
                }
                return;
            }

            builder.Append("N;");
        }

        private static void AppendProjectionToken(StringBuilder builder, string value)
        {
            var safeValue = value ?? string.Empty;
            builder.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(safeValue);
            builder.Append(';');
        }

        private static void CheckCommonSource(
            GameDataCatalogRecord record,
            string family,
            string componentId,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            CheckEqual(
                ReadString(record, "source_id", family, diagnostics),
                GameDataClassProgressionSchemas.SourceRecordId,
                diagnostics,
                "CLS-SOURCE-REFERENCE",
                family,
                record.Id,
                "$.source_id",
                "Every class identity record must reference the accepted packet source.");
            CheckEqual(
                ReadString(record, "source_component_id", family, diagnostics),
                componentId,
                diagnostics,
                "CLS-COMPONENT-REFERENCE",
                family,
                record.Id,
                "$.source_component_id",
                "Every class identity record must reference its owning family component.");
        }

        private static void CheckFamilyCount(
            GameDataCatalogSetSnapshot catalogSet,
            string familyId,
            int expectedCount,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var family = GetFamily(catalogSet, familyId);
            if (family == null)
            {
                return;
            }

            if (family.Records.Count != expectedCount)
            {
                Add(
                    diagnostics,
                    "CLS-RECORD-COUNT",
                    familyId,
                    string.Empty,
                    "$.records",
                    "The family record count does not match the accepted identity inventory.",
                    "Compile the complete packet without partial or extra records.");
            }
        }

        private static void CheckReferencedInventory(
            GameDataFamilyCatalogSnapshot family,
            IEnumerable<string> referencedIds,
            string code,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var expected = referencedIds
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var actual = family.Records
                .Select(item => item.Id)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                Add(
                    diagnostics,
                    code,
                    family.Family,
                    string.Empty,
                    "$.records",
                    "Owned records must be referenced exactly once with no orphan or shared identity.",
                    "Restore reciprocal one-to-one class ownership.");
            }
        }

        private static bool HasExactRecordIds(
            GameDataFamilyCatalogSnapshot family,
            IEnumerable<string> expectedIds)
        {
            var expected = expectedIds.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            var actual = family.Records.Select(item => item.Id).ToArray();
            return actual.SequenceEqual(expected, StringComparer.Ordinal);
        }

        private static GameDataFamilyCatalogSnapshot GetFamily(
            GameDataCatalogSetSnapshot catalogSet,
            string family)
        {
            GameDataFamilyCatalogSnapshot result;
            return catalogSet.FamiliesById.TryGetValue(family, out result) ? result : null;
        }

        private static ExpectedFamily FindExpectedFamily(string familyId)
        {
            for (var index = 0; index < ExpectedFamilies.Length; index++)
            {
                if (string.Equals(ExpectedFamilies[index].Id, familyId, StringComparison.Ordinal))
                {
                    return ExpectedFamilies[index];
                }
            }

            throw new InvalidOperationException("Unknown expected class family: " + familyId);
        }

        private static string ReadString(
            GameDataCatalogRecord record,
            string field,
            string family,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataValue value;
            var typed = record.TryGetField(field, out value) ? value as GameDataStringValue : null;
            if (typed != null)
            {
                return typed.Value;
            }

            AddFieldShape(diagnostics, family, record.Id, field, "string");
            return string.Empty;
        }

        private static long ReadInt(
            GameDataCatalogRecord record,
            string field,
            string family,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataValue value;
            var typed = record.TryGetField(field, out value) ? value as GameDataNumberValue : null;
            long result;
            if (typed != null && typed.TryGetInt64(out result))
            {
                return result;
            }

            AddFieldShape(diagnostics, family, record.Id, field, "integer");
            return long.MinValue;
        }

        private static bool ReadBoolean(
            GameDataCatalogRecord record,
            string field,
            string family,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataValue value;
            var typed = record.TryGetField(field, out value) ? value as GameDataBooleanValue : null;
            if (typed != null)
            {
                return typed.Value;
            }

            AddFieldShape(diagnostics, family, record.Id, field, "boolean");
            return false;
        }

        private static IReadOnlyList<string> ReadStringArray(
            GameDataCatalogRecord record,
            string field,
            string family,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataValue value;
            var array = record.TryGetField(field, out value) ? value as GameDataArrayValue : null;
            if (array == null)
            {
                AddFieldShape(diagnostics, family, record.Id, field, "string array");
                return new string[0];
            }

            var result = new List<string>();
            for (var index = 0; index < array.Count; index++)
            {
                var item = array.Items[index] as GameDataStringValue;
                if (item == null)
                {
                    AddFieldShape(diagnostics, family, record.Id, field, "string array");
                    return new string[0];
                }

                result.Add(item.Value);
            }

            return result;
        }

        private static IReadOnlyList<long> ReadIntArray(
            GameDataCatalogRecord record,
            string field,
            string family,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            GameDataValue value;
            var array = record.TryGetField(field, out value) ? value as GameDataArrayValue : null;
            if (array == null)
            {
                AddFieldShape(diagnostics, family, record.Id, field, "integer array");
                return new long[0];
            }

            var result = new List<long>();
            for (var index = 0; index < array.Count; index++)
            {
                var item = array.Items[index] as GameDataNumberValue;
                long parsed;
                if (item == null || !item.TryGetInt64(out parsed))
                {
                    AddFieldShape(diagnostics, family, record.Id, field, "integer array");
                    return new long[0];
                }

                result.Add(parsed);
            }

            return result;
        }

        private static void AddFieldShape(
            List<GameDataCatalogDiagnostic> diagnostics,
            string family,
            string recordId,
            string field,
            string expected)
        {
            Add(
                diagnostics,
                "CLS-FIELD-SHAPE",
                family,
                recordId,
                "$." + field,
                "Semantic validation expected a " + expected + " field.",
                "Validate this artifact with GameDataClassProgressionSchemas before semantic validation.");
        }

        private static void ValidateName(
            GameDataCatalogRecord record,
            string field,
            string expected,
            string family,
            List<string> localizationKeys,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var actual = ReadString(record, field, family, diagnostics);
            localizationKeys.Add(actual);
            CheckEqual(
                actual,
                expected,
                diagnostics,
                "CLS-LOCALIZATION-OWNER",
                family,
                record.Id,
                "$." + field,
                "The localization key must be derived from the owning stable ID.");
        }

        private static void CheckBoolean(
            GameDataCatalogRecord record,
            string field,
            bool expected,
            string family,
            string code,
            List<GameDataCatalogDiagnostic> diagnostics)
        {
            var actual = ReadBoolean(record, field, family, diagnostics);
            if (actual != expected)
            {
                Add(
                    diagnostics,
                    code,
                    family,
                    record.Id,
                    "$." + field,
                    "The boolean policy does not match the accepted class boundary.",
                    "Restore the packet-authored identity-only policy.");
            }
        }

        private static void CheckOwnedPrefix(
            string value,
            string expectedPrefix,
            List<GameDataCatalogDiagnostic> diagnostics,
            string code,
            string family,
            string recordId,
            string path)
        {
            if (!value.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    code,
                    family,
                    recordId,
                    path,
                    "The stable ID is not owned by the referenced class.",
                    "Restore the accepted class-owned stable-ID namespace.");
            }
        }

        private static void CheckUniqueArray(
            IReadOnlyList<string> values,
            List<GameDataCatalogDiagnostic> diagnostics,
            string code,
            string family,
            string recordId,
            string path)
        {
            var unique = new HashSet<string>(values, StringComparer.Ordinal);
            if (unique.Count != values.Count)
            {
                Add(
                    diagnostics,
                    code,
                    family,
                    recordId,
                    path,
                    "Array values must be exact and unique.",
                    "Remove duplicate identity references.");
            }
        }

        private static void CheckStringArray(
            IReadOnlyList<string> actual,
            IReadOnlyList<string> expected,
            List<GameDataCatalogDiagnostic> diagnostics,
            string code,
            string family,
            string recordId,
            string path,
            string message)
        {
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                Add(
                    diagnostics,
                    code,
                    family,
                    recordId,
                    path,
                    message,
                    "Restore the exact accepted values and deterministic order.");
            }
        }

        private static void CheckIntArray(
            IReadOnlyList<long> actual,
            IReadOnlyList<long> expected,
            List<GameDataCatalogDiagnostic> diagnostics,
            string code,
            string family,
            string recordId,
            string path,
            string message)
        {
            if (!actual.SequenceEqual(expected))
            {
                Add(
                    diagnostics,
                    code,
                    family,
                    recordId,
                    path,
                    message,
                    "Restore the exact accepted values and deterministic order.");
            }
        }

        private static void CheckEqual(
            string actual,
            string expected,
            List<GameDataCatalogDiagnostic> diagnostics,
            string code,
            string family,
            string recordId,
            string path,
            string message)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                Add(
                    diagnostics,
                    code,
                    family,
                    recordId,
                    path,
                    message,
                    "Restore the exact accepted value.");
            }
        }

        private static void CheckEqual(
            long actual,
            long expected,
            List<GameDataCatalogDiagnostic> diagnostics,
            string code,
            string family,
            string recordId,
            string path,
            string message)
        {
            if (actual != expected)
            {
                Add(
                    diagnostics,
                    code,
                    family,
                    recordId,
                    path,
                    message,
                    "Restore the exact accepted value.");
            }
        }

        private static string Token(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.Ordinal)
                ? value.Substring(prefix.Length)
                : string.Empty;
        }

        private static string NameKey(string value, string prefix, string keyPrefix)
        {
            var token = Token(value, prefix);
            var ownerBoundary = token.IndexOf('_');
            return ownerBoundary < 0
                ? keyPrefix + token + ".name"
                : keyPrefix +
                  token.Substring(0, ownerBoundary) +
                  "." +
                  token.Substring(ownerBoundary + 1) +
                  ".name";
        }

        private static void Add(
            List<GameDataCatalogDiagnostic> diagnostics,
            string code,
            string family,
            string recordId,
            string fieldPath,
            string technicalMessage,
            string action)
        {
            if (diagnostics.Count >= GameDataCatalogContract.MaximumDiagnostics)
            {
                return;
            }

            diagnostics.Add(new GameDataCatalogDiagnostic(
                code,
                GameDataDiagnosticSeverity.Error,
                string.IsNullOrEmpty(family) ? "class_progression" : family,
                family,
                recordId,
                fieldPath,
                "catalog.class_progression.invalid",
                technicalMessage,
                action,
                true,
                true,
                -1,
                -1));
        }

        private sealed class ExpectedFamily
        {
            public ExpectedFamily(
                string id,
                string enumName,
                int order,
                string componentId,
                string path,
                string sha256,
                string[] classIds)
            {
                Id = id;
                EnumName = enumName;
                Order = order;
                ComponentId = componentId;
                Path = path;
                Sha256 = sha256;
                ClassIds = classIds;
            }

            public string Id { get; }
            public string EnumName { get; }
            public int Order { get; }
            public string ComponentId { get; }
            public string Path { get; }
            public string Sha256 { get; }
            public string[] ClassIds { get; }
        }

        private sealed class ExpectedClass
        {
            public ExpectedClass(
                string id,
                string enumName,
                int enumValue,
                string familyId,
                int familyOrder)
            {
                Id = id;
                EnumName = enumName;
                EnumValue = enumValue;
                FamilyId = familyId;
                FamilyOrder = familyOrder;
            }

            public string Id { get; }
            public string EnumName { get; }
            public int EnumValue { get; }
            public string FamilyId { get; }
            public int FamilyOrder { get; }
        }
    }
}
