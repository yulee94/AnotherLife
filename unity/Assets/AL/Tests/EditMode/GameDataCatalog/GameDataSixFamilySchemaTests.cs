using System;
using System.Linq;
using System.Threading;
using AL.Data.Catalogs;
using NUnit.Framework;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataSixFamilySchemaTests
    {
        [Test]
        public void RegistryDeclaresExactBindingOrderAndRequiredNonemptySet()
        {
            CollectionAssert.AreEqual(
                new[] { "realms", "buildings", "research", "troops", "champions", "skills" },
                GameDataSixFamilySchemas.FamilyOrder.ToArray());

            var registry = GameDataSixFamilySchemas.CreateRegistry();
            CollectionAssert.AreEqual(
                new[] { "buildings", "champions", "realms", "research", "skills", "troops" },
                registry.Schemas.Select(schema => schema.Family).ToArray(),
                "The common registry intentionally exposes its ordinal family-key order.");

            Assert.AreEqual(6, registry.Schemas.Count);
            foreach (var schema in registry.Schemas)
            {
                Assert.False(schema.AllowEmptyRecords, schema.Family);
                Assert.True(schema.SupportsVersion(GameDataSixFamilySchemas.SchemaVersion), schema.Family);
                Assert.False(schema.SupportsVersion(0), schema.Family);
                Assert.False(schema.SupportsVersion(GameDataSixFamilySchemas.SchemaVersion + 1), schema.Family);
            }
        }

        [Test]
        public void SchemasExposeExactRequiredProductionFields()
        {
            var registry = GameDataSixFamilySchemas.CreateRegistry();

            AssertExactRequiredFields(
                Family(registry, "realms"),
                "asset_ref",
                "capability_profile_ids",
                "description_ref",
                "legacy_realm_id",
                "legacy_realm_value",
                "name_ref",
                "rare_resource_id");
            AssertExactRequiredFields(
                Family(registry, "buildings"),
                "asset_ref",
                "cost_profile_id",
                "duration_profile_id",
                "max_level",
                "name_ref",
                "production_profile_ids");
            AssertExactRequiredFields(
                Family(registry, "research"),
                "cost_profile_id",
                "duration_profile_id",
                "effect_ids",
                "max_level",
                "name_ref",
                "prerequisite_research_ids");
            AssertExactRequiredFields(
                Family(registry, "troops"),
                "asset_ref",
                "base_attack",
                "base_defense",
                "legacy_troop_type",
                "legacy_troop_value",
                "name_ref",
                "training_profile_id");
            AssertExactRequiredFields(
                Family(registry, "champions"),
                "base_skill_ids",
                "class_family_id",
                "model_asset_ref",
                "name_ref",
                "portrait_asset_ref",
                "realm_id",
                "stat_profile_id");
            AssertExactRequiredFields(
                Family(registry, "skills"),
                "audio_asset_ref",
                "behavior_profile_id",
                "cast_time_seconds",
                "cooldown_seconds",
                "mana_cost",
                "name_ref",
                "power",
                "presentation_profile_id",
                "range_meters",
                "target_type",
                "vfx_asset_ref");
        }

        [Test]
        public void EnumDomainsAndCrossFamilyReferencesAreExact()
        {
            var registry = GameDataSixFamilySchemas.CreateRegistry();

            CollectionAssert.AreEquivalent(
                new[] { "Stonehold", "Eldergrove", "Crownlands", "Umbral" },
                Field(registry, "realms", "legacy_realm_id").AllowedStringValues);
            CollectionAssert.AreEquivalent(
                new[] { "Infantry", "Cavalry", "Ranged", "Siege" },
                Field(registry, "troops", "legacy_troop_type").AllowedStringValues);
            CollectionAssert.AreEquivalent(
                new[] { "warrior", "mage", "ranger", "assassin" },
                Field(registry, "champions", "class_family_id").AllowedStringValues);
            CollectionAssert.AreEquivalent(
                new[] { "single", "aoe", "self", "ally", "enemy" },
                Field(registry, "skills", "target_type").AllowedStringValues);

            Assert.AreEqual("realms", Field(registry, "champions", "realm_id").ReferenceFamily);
            Assert.AreEqual(
                "skills",
                Field(registry, "champions", "base_skill_ids").ItemRule.ReferenceFamily);
            Assert.AreEqual(
                "research",
                Field(registry, "research", "prerequisite_research_ids").ItemRule.ReferenceFamily);
        }

        [Test]
        public void StableIdAndReferenceRulesDoNotConflateContentOrAssetAddresses()
        {
            var registry = GameDataSixFamilySchemas.CreateRegistry();

            AssertStableString(Field(registry, "realms", "rare_resource_id"));
            AssertStableString(Field(registry, "buildings", "cost_profile_id"));
            AssertStableString(Field(registry, "buildings", "duration_profile_id"));
            AssertStableString(Field(registry, "research", "cost_profile_id"));
            AssertStableString(Field(registry, "research", "duration_profile_id"));
            AssertStableString(Field(registry, "troops", "training_profile_id"));
            AssertStableString(Field(registry, "champions", "realm_id"));
            AssertStableString(Field(registry, "champions", "class_family_id"));
            AssertStableString(Field(registry, "champions", "stat_profile_id"));
            AssertStableString(Field(registry, "skills", "behavior_profile_id"));
            AssertStableString(Field(registry, "skills", "presentation_profile_id"));
            AssertStableString(Field(registry, "skills", "target_type"));

            AssertStableArray(Field(registry, "realms", "capability_profile_ids"));
            AssertStableArray(Field(registry, "buildings", "production_profile_ids"));
            AssertStableArray(Field(registry, "research", "effect_ids"));
            AssertStableArray(Field(registry, "research", "prerequisite_research_ids"));
            AssertStableArray(Field(registry, "champions", "base_skill_ids"));

            AssertNonStableAddress(Field(registry, "realms", "name_ref"));
            AssertNonStableAddress(Field(registry, "realms", "description_ref"));
            AssertNonStableAddress(Field(registry, "realms", "asset_ref"));
            AssertNonStableAddress(Field(registry, "champions", "portrait_asset_ref"));
            AssertNonStableAddress(Field(registry, "champions", "model_asset_ref"));
            AssertNonStableAddress(Field(registry, "skills", "vfx_asset_ref"));
            AssertNonStableAddress(Field(registry, "skills", "audio_asset_ref"));
        }

        [Test]
        public void NumericAndCollectionBoundsAreExplicitAndStorageSafe()
        {
            var registry = GameDataSixFamilySchemas.CreateRegistry();

            AssertIntegerBounds(Field(registry, "realms", "legacy_realm_value"), 1, 4);
            AssertIntegerBounds(
                Field(registry, "buildings", "max_level"),
                1,
                GameDataSixFamilySchemas.MaximumCatalogInteger);
            AssertIntegerBounds(
                Field(registry, "research", "max_level"),
                1,
                GameDataSixFamilySchemas.MaximumCatalogInteger);
            AssertIntegerBounds(Field(registry, "troops", "legacy_troop_value"), 0, 3);
            AssertIntegerBounds(
                Field(registry, "troops", "base_attack"),
                0,
                GameDataSixFamilySchemas.MaximumCatalogInteger);
            AssertIntegerBounds(
                Field(registry, "troops", "base_defense"),
                0,
                GameDataSixFamilySchemas.MaximumCatalogInteger);

            foreach (var fieldName in new[]
                     {
                         "cooldown_seconds", "power", "mana_cost", "cast_time_seconds", "range_meters"
                     })
            {
                var rule = Field(registry, "skills", fieldName);
                Assert.AreEqual(GameDataValueKind.Number, rule.Kind, fieldName);
                Assert.False(rule.IntegerOnly, fieldName);
                Assert.AreEqual(0d, rule.MinimumNumber, fieldName);
                Assert.AreEqual(GameDataSixFamilySchemas.MaximumCatalogFloat, rule.MaximumNumber, fieldName);
            }

            AssertArrayBounds(
                Field(registry, "realms", "capability_profile_ids"),
                1,
                GameDataSixFamilySchemas.MaximumProfileReferences);
            AssertArrayBounds(
                Field(registry, "buildings", "production_profile_ids"),
                1,
                GameDataSixFamilySchemas.MaximumProfileReferences);
            AssertArrayBounds(
                Field(registry, "research", "effect_ids"),
                1,
                GameDataSixFamilySchemas.MaximumProfileReferences);
            AssertArrayBounds(
                Field(registry, "research", "prerequisite_research_ids"),
                0,
                GameDataSixFamilySchemas.MaximumPrerequisiteReferences);
            AssertArrayBounds(
                Field(registry, "champions", "base_skill_ids"),
                1,
                GameDataSixFamilySchemas.MaximumChampionSkillReferences);
        }

        [Test]
        public void RepresentativeSixFamilySetValidatesAndExactAliasesRemainObservable()
        {
            var artifacts = ValidArtifacts();
            var result = Validate(artifacts);

            Assert.AreEqual(GameDataCatalogLoadStatus.LoadedPackaged, result.Status);
            Assert.NotNull(result.Snapshot);
            Assert.AreEqual(6, result.Snapshot.Families.Count);

            var store = Load(artifacts);
            Assert.AreEqual(
                GameDataQueryStatus.AliasResolved,
                store.QueryRecord("buildings", "LegacyBuilding").Status);
            Assert.AreEqual(
                GameDataQueryStatus.AliasResolved,
                store.QueryRecord("research", "Legacy Research").Status);
            Assert.AreEqual(
                GameDataQueryStatus.UnknownId,
                store.QueryRecord("buildings", "legacybuilding").Status,
                "Aliases are exact and must not be resolved through case folding or fuzzy normalization.");
        }

        [Test]
        public void EmptyRequiredTroopAndChampionFamiliesFailClosed()
        {
            foreach (var family in new[] { "troops", "champions" })
            {
                var artifact = CatalogFixture.FamilyArtifact(
                    family,
                    family + "_phase_c2_empty",
                    "Catalogs/" + family + ".phase_c2_empty.json",
                    true,
                    "0.1.0",
                    "[]",
                    "[]",
                    string.Empty);

                var result = Validate(artifact);
                Assert.AreEqual(GameDataCatalogLoadStatus.InvalidRecord, result.Status, family);
                Assert.IsNull(result.Snapshot, family);
                CollectionAssert.Contains(
                    result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                    "AL-GDC-RECORD-COUNT",
                    family);
            }
        }

        [Test]
        public void MissingRequiredAddressAndInvalidLegacyRealmMappingFailClosed()
        {
            var validRealm = ValidArtifacts().Single(artifact => artifact.Family == "realms");
            var blankAsset = CatalogFixture.MutateArtifact(
                validRealm,
                "\"asset_ref\":\"Assets/Fixture/Realm\"",
                "\"asset_ref\":\"\"");
            var invalidName = CatalogFixture.MutateArtifact(
                validRealm,
                "\"legacy_realm_id\":\"Stonehold\"",
                "\"legacy_realm_id\":\"None\"");
            var invalidValue = CatalogFixture.MutateArtifact(
                validRealm,
                "\"legacy_realm_value\":1",
                "\"legacy_realm_value\":0");

            foreach (var artifact in new[] { blankAsset, invalidName, invalidValue })
            {
                var result = Validate(artifact);
                Assert.AreEqual(GameDataCatalogLoadStatus.InvalidRecord, result.Status);
                Assert.IsNull(result.Snapshot);
            }
        }

        [Test]
        public void MissingChampionAndResearchReferencesRejectTheWholeSet()
        {
            var artifacts = ValidArtifacts();
            var champion = artifacts.Single(artifact => artifact.Family == "champions");
            var missingRealm = CatalogFixture.MutateArtifact(
                champion,
                "\"realm_id\":\"realm_fixture\"",
                "\"realm_id\":\"missing_realm\"");
            var championSet = artifacts
                .Select(artifact => artifact.Family == "champions" ? missingRealm : artifact)
                .ToArray();
            var championResult = Validate(championSet);
            Assert.AreEqual(GameDataCatalogLoadStatus.CrossReferenceFailure, championResult.Status);
            Assert.IsNull(championResult.Snapshot);

            var research = artifacts.Single(artifact => artifact.Family == "research");
            var missingPrerequisite = CatalogFixture.MutateArtifact(
                research,
                "\"prerequisite_research_ids\":[]",
                "\"prerequisite_research_ids\":[\"missing_research\"]");
            var researchSet = artifacts
                .Select(artifact => artifact.Family == "research" ? missingPrerequisite : artifact)
                .ToArray();
            var researchResult = Validate(researchSet);
            Assert.AreEqual(GameDataCatalogLoadStatus.CrossReferenceFailure, researchResult.Status);
            Assert.IsNull(researchResult.Snapshot);
        }

        [Test]
        public void SchemaAssemblyHasNoUnityEngineDependency()
        {
            var schemaType = typeof(GameDataSixFamilySchemas);
            Assert.True(schemaType.IsAbstract && schemaType.IsSealed, "The schema registry must remain static.");
            Assert.AreSame(typeof(GameDataCatalogSchemaRegistry).Assembly, schemaType.Assembly);
            Assert.False(
                schemaType.Assembly.GetReferencedAssemblies()
                    .Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                "The pure catalog assembly must not acquire a UnityEngine dependency.");
        }

        private static GameDataCatalogFamilySchema Family(
            GameDataCatalogSchemaRegistry registry,
            string family)
        {
            GameDataCatalogFamilySchema schema;
            Assert.True(registry.TryGet(family, out schema), "Missing family schema: " + family);
            return schema;
        }

        private static GameDataCatalogFieldRule Field(
            GameDataCatalogSchemaRegistry registry,
            string family,
            string field)
        {
            var schema = Family(registry, family);
            var matches = schema.Fields.Where(rule => rule.Name == field).ToArray();
            Assert.AreEqual(1, matches.Length, family + "." + field);
            return matches[0];
        }

        private static void AssertExactRequiredFields(
            GameDataCatalogFamilySchema schema,
            params string[] expectedFields)
        {
            CollectionAssert.AreEqual(
                expectedFields.OrderBy(field => field, StringComparer.Ordinal).ToArray(),
                schema.Fields.Select(field => field.Name).ToArray(),
                schema.Family);
            Assert.True(schema.Fields.All(field => field.Required), schema.Family);
            Assert.True(schema.Fields.All(field => !field.AllowNull), schema.Family);
        }

        private static void AssertStableString(GameDataCatalogFieldRule rule)
        {
            Assert.AreEqual(GameDataValueKind.String, rule.Kind, rule.Name);
            Assert.True(rule.NonBlank, rule.Name);
            Assert.True(rule.StableId, rule.Name);
        }

        private static void AssertStableArray(GameDataCatalogFieldRule rule)
        {
            Assert.AreEqual(GameDataValueKind.Array, rule.Kind, rule.Name);
            Assert.NotNull(rule.ItemRule, rule.Name);
            Assert.AreEqual(GameDataValueKind.String, rule.ItemRule.Kind, rule.Name);
            Assert.True(rule.ItemRule.NonBlank, rule.Name);
            Assert.True(rule.ItemRule.StableId, rule.Name);
        }

        private static void AssertNonStableAddress(GameDataCatalogFieldRule rule)
        {
            Assert.AreEqual(GameDataValueKind.String, rule.Kind, rule.Name);
            Assert.True(rule.NonBlank, rule.Name);
            Assert.False(rule.StableId, rule.Name);
        }

        private static void AssertIntegerBounds(
            GameDataCatalogFieldRule rule,
            double minimum,
            double maximum)
        {
            Assert.AreEqual(GameDataValueKind.Number, rule.Kind, rule.Name);
            Assert.True(rule.IntegerOnly, rule.Name);
            Assert.AreEqual(minimum, rule.MinimumNumber, rule.Name);
            Assert.AreEqual(maximum, rule.MaximumNumber, rule.Name);
        }

        private static void AssertArrayBounds(
            GameDataCatalogFieldRule rule,
            int minimum,
            int maximum)
        {
            Assert.AreEqual(GameDataValueKind.Array, rule.Kind, rule.Name);
            Assert.AreEqual(minimum, rule.MinimumItems, rule.Name);
            Assert.AreEqual(maximum, rule.MaximumItems, rule.Name);
        }

        private static CatalogFixture.Artifact[] ValidArtifacts()
        {
            return new[]
            {
                CatalogFixture.FamilyArtifact(
                    "realms",
                    "realms_phase_c2_fixture",
                    "Catalogs/realms.phase_c2_fixture.json",
                    true,
                    "0.1.0",
                    "[{\"id\":\"realm_fixture\",\"legacy_realm_id\":\"Stonehold\",\"legacy_realm_value\":1,\"name_ref\":\"fixture.realm.name\",\"description_ref\":\"fixture.realm.description\",\"rare_resource_id\":\"deep_ore\",\"capability_profile_ids\":[\"resilience\"],\"asset_ref\":\"Assets/Fixture/Realm\"}]",
                    "[]",
                    string.Empty),
                CatalogFixture.FamilyArtifact(
                    "buildings",
                    "buildings_phase_c2_fixture",
                    "Catalogs/buildings.phase_c2_fixture.json",
                    true,
                    "0.1.0",
                    "[{\"id\":\"building_fixture\",\"name_ref\":\"fixture.building.name\",\"max_level\":10,\"production_profile_ids\":[\"resource_output\"],\"cost_profile_id\":\"building_cost\",\"duration_profile_id\":\"building_duration\",\"asset_ref\":\"Assets/Fixture/Building\"}]",
                    "[{\"legacyId\":\"LegacyBuilding\",\"canonicalId\":\"building_fixture\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#165\"}]",
                    string.Empty),
                CatalogFixture.FamilyArtifact(
                    "research",
                    "research_phase_c2_fixture",
                    "Catalogs/research.phase_c2_fixture.json",
                    true,
                    "0.1.0",
                    "[{\"id\":\"research_fixture\",\"name_ref\":\"fixture.research.name\",\"max_level\":1,\"cost_profile_id\":\"research_cost\",\"duration_profile_id\":\"research_duration\",\"effect_ids\":[\"attack_bonus\"],\"prerequisite_research_ids\":[]}]",
                    "[{\"legacyId\":\"Legacy Research\",\"canonicalId\":\"research_fixture\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#165\"}]",
                    string.Empty),
                CatalogFixture.FamilyArtifact(
                    "troops",
                    "troops_phase_c2_fixture",
                    "Catalogs/troops.phase_c2_fixture.json",
                    true,
                    "0.1.0",
                    "[{\"id\":\"troop_fixture\",\"legacy_troop_type\":\"Infantry\",\"legacy_troop_value\":0,\"name_ref\":\"fixture.troop.name\",\"base_attack\":1,\"base_defense\":1,\"training_profile_id\":\"basic_training\",\"asset_ref\":\"Assets/Fixture/Troop\"}]",
                    "[]",
                    string.Empty),
                CatalogFixture.FamilyArtifact(
                    "champions",
                    "champions_phase_c2_fixture",
                    "Catalogs/champions.phase_c2_fixture.json",
                    true,
                    "0.1.0",
                    "[{\"id\":\"champion_fixture\",\"name_ref\":\"fixture.champion.name\",\"realm_id\":\"realm_fixture\",\"class_family_id\":\"warrior\",\"portrait_asset_ref\":\"Assets/Fixture/ChampionPortrait\",\"model_asset_ref\":\"Assets/Fixture/ChampionModel\",\"base_skill_ids\":[\"skill_fixture\"],\"stat_profile_id\":\"champion_stats\"}]",
                    "[]",
                    string.Empty),
                CatalogFixture.FamilyArtifact(
                    "skills",
                    "skills_phase_c2_fixture",
                    "Catalogs/skills.phase_c2_fixture.json",
                    true,
                    "0.1.0",
                    "[{\"id\":\"skill_fixture\",\"name_ref\":\"fixture.skill.name\",\"behavior_profile_id\":\"fixture_behavior\",\"presentation_profile_id\":\"fixture_presentation\",\"target_type\":\"single\",\"cooldown_seconds\":1,\"power\":1,\"mana_cost\":1,\"cast_time_seconds\":0,\"range_meters\":1,\"vfx_asset_ref\":\"Assets/Fixture/Vfx\",\"audio_asset_ref\":\"Assets/Fixture/Audio\"}]",
                    "[]",
                    string.Empty)
            };
        }

        private static GameDataCatalogLoadResult Validate(params CatalogFixture.Artifact[] artifacts)
        {
            var manifestResult = GameDataCatalogValidator.ValidateManifest(
                CatalogFixture.Manifest(artifacts),
                CatalogFixture.Policy());
            Assert.True(
                manifestResult.IsAccepted,
                string.Join("\n", manifestResult.Diagnostics.Select(diagnostic => diagnostic.Fingerprint)));

            return GameDataCatalogValidator.ValidateCatalogSet(
                manifestResult.Manifest,
                artifacts.Select(artifact => new GameDataCatalogArtifactInput(
                    artifact.Path,
                    GameDataCatalogReadStatus.Succeeded,
                    artifact.Bytes,
                    string.Empty)),
                GameDataSixFamilySchemas.CreateRegistry(),
                CatalogFixture.Policy(),
                GameDataCatalogSourceKind.Packaged,
                new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 23, 0, 0, 1, TimeSpan.Zero));
        }

        private static GameDataCatalogStore Load(params CatalogFixture.Artifact[] artifacts)
        {
            var store = new GameDataCatalogStore();
            var loader = new GameDataCatalogLoader(
                CatalogFixture.Policy(),
                GameDataSixFamilySchemas.CreateRegistry());
            var operation = store.BeginLoad(
                loader,
                CatalogFixture.Source(artifacts),
                CatalogFixture.ManifestPath,
                GameDataCatalogSourceKind.Packaged);
            Assert.True(
                SpinWait.SpinUntil(() => operation.IsCompleted, 5000),
                "The bounded six-family fixture should complete within five seconds.");
            Assert.AreEqual(GameDataCatalogLifecycleStatus.Ready, store.State.Status);
            return store;
        }
    }
}
