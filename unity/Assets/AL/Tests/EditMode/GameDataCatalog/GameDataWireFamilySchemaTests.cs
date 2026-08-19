using System;
using System.IO;
using System.Linq;
using System.Text;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataWireFamilySchemaTests
    {
        private static readonly string[] SkipFamilies =
        {
            "character_customization_content",
            "notification_content",
            "notification_production",
            "quest_preview",
            "realm_gem_wishgate",
            "relationship_authority",
            "warmaster",
            "world_atlas_narrative",
            "world_event",
            "six_family",
            "canonical_contracts"
        };

        [Test]
        public void RegistryDeclaresExactWireFamiliesAndRejectsSkipSet()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "realm_specialized",
                    "character_customization",
                    "skill_weather"
                },
                GameDataWireFamilySchemas.FamilyOrder.ToArray());

            var registry = GameDataWireFamilySchemas.CreateRegistry();
            CollectionAssert.AreEqual(
                new[]
                {
                    "character_customization",
                    "realm_specialized",
                    "skill_weather"
                },
                registry.Schemas.Select(schema => schema.Family).ToArray(),
                "The common registry exposes schemas in ordinal family-key order.");

            Assert.AreEqual(3, registry.Schemas.Count);
            foreach (var schema in registry.Schemas)
            {
                Assert.False(schema.AllowEmptyRecords, schema.Family);
                Assert.True(
                    schema.SupportsVersion(GameDataWireFamilySchemas.SchemaVersion),
                    schema.Family);
                Assert.False(schema.SupportsVersion(0), schema.Family);
                Assert.False(
                    schema.SupportsVersion(GameDataWireFamilySchemas.SchemaVersion + 1),
                    schema.Family);
            }

            foreach (var family in SkipFamilies)
            {
                GameDataCatalogFamilySchema unused;
                Assert.False(registry.TryGet(family, out unused), family);
            }
        }

        [Test]
        public void ProductionRegistryIsSixFamilyPlusWireAndNotFourteen()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "realms",
                    "buildings",
                    "research",
                    "troops",
                    "champions",
                    "skills",
                    "realm_specialized",
                    "character_customization",
                    "skill_weather"
                },
                GameDataProductionCatalogSchemas.FamilyOrder.ToArray());

            var registry = GameDataProductionCatalogSchemas.CreateRegistry();
            Assert.AreEqual(9, registry.Schemas.Count);
            Assert.AreEqual(6, GameDataSixFamilySchemas.CreateRegistry().Schemas.Count);

            foreach (var family in SkipFamilies)
            {
                GameDataCatalogFamilySchema unused;
                Assert.False(registry.TryGet(family, out unused), family);
            }
        }

        [Test]
        public void OnlyKindIsRequiredOnHeterogeneousRecords()
        {
            var registry = GameDataWireFamilySchemas.CreateRegistry();
            foreach (var schema in registry.Schemas)
            {
                var required = schema.Fields.Where(field => field.Required).Select(field => field.Name).ToArray();
                CollectionAssert.AreEqual(new[] { "kind" }, required, schema.Family);
            }
        }

        [Test]
        public void KindDomainsMatchFlattenedDiscriminators()
        {
            var registry = GameDataWireFamilySchemas.CreateRegistry();
            CollectionAssert.AreEquivalent(
                GameDataWireFamilySchemas.RealmSpecializedKinds,
                Field(registry, "realm_specialized", "kind").AllowedStringValues);
            CollectionAssert.AreEquivalent(
                GameDataWireFamilySchemas.CharacterCustomizationKinds,
                Field(registry, "character_customization", "kind").AllowedStringValues);
            CollectionAssert.AreEquivalent(
                GameDataWireFamilySchemas.SkillWeatherKinds,
                Field(registry, "skill_weather", "kind").AllowedStringValues);
        }

        [Test]
        public void SchemasExposeTheFlattenedFieldUnion()
        {
            var registry = GameDataWireFamilySchemas.CreateRegistry();
            AssertExactFields(
                Family(registry, "realm_specialized"),
                "account_lock_summary",
                "adjective",
                "capital_id",
                "committed_meaning",
                "committed_profile_state",
                "consumer",
                "continuity_hooks",
                "cross_realm_creation_policy",
                "display_name",
                "dragon_id",
                "handoff_status",
                "inner_realm_id",
                "key",
                "kind",
                "language_id",
                "legacy_runtime_id",
                "lore",
                "main_gate_id",
                "naming_conventions",
                "narrative_warning_key",
                "non_goals_for_this_catalog",
                "outer_warzone_id",
                "palette",
                "parse_on_launch",
                "people_name",
                "realm_change_policy",
                "realm_gem_ids",
                "realm_ids",
                "realm_lock_scope",
                "required_validation",
                "selection_mode",
                "selection_warning_meaning",
                "shared_storage_policy",
                "sigil",
                "sort_order",
                "source_mode",
                "source_packet_id",
                "starting_hooks",
                "sub_character_policy",
                "text",
                "uncommitted_meaning",
                "uncommitted_profile_state");
            AssertExactFields(
                Family(registry, "character_customization"),
                "accent_color",
                "armor_style_id",
                "body_preset_id",
                "cape_enabled",
                "customization_focus",
                "display_name",
                "eye_color",
                "face_mark_id",
                "far_representation",
                "hair_color",
                "hair_style_id",
                "helmet_enabled",
                "hero_triangles",
                "kind",
                "legacy_id",
                "low_triangles",
                "material_keys",
                "medium_triangles",
                "offhand_style_id",
                "primary_color",
                "rgb",
                "scale",
                "skin_color",
                "slot",
                "summary",
                "weapon_style_id");
            AssertExactFields(
                Family(registry, "skill_weather"),
                "bot_damage_multiplier",
                "cast_time_seconds",
                "color",
                "colors",
                "cooldown_seconds",
                "display_name",
                "emission_rate_multiplier",
                "fall_speed",
                "horizontal_drift",
                "key",
                "kind",
                "lighting",
                "lightning",
                "mana_cost",
                "max_particles",
                "noise_frequency",
                "noise_strength",
                "particle_end_color",
                "particle_lifetime",
                "particle_size",
                "particle_start_color",
                "particles",
                "power",
                "radius",
                "range_meters",
                "realm",
                "role",
                "slot",
                "use",
                "vfx_key",
                "wind");
        }

        [Test]
        public void RepresentativeWireFixturesValidateAndResolveAliases()
        {
            var result = Validate(ValidArtifacts());
            Assert.AreEqual(GameDataCatalogLoadStatus.LoadedPackaged, result.Status);
            Assert.NotNull(result.Snapshot);
            Assert.AreEqual(3, result.Snapshot.Families.Count);

            var store = Load(ValidArtifacts());
            Assert.AreEqual(
                GameDataQueryStatus.AliasResolved,
                store.QueryRecord("realm_specialized", "Crownlands").Status);
            Assert.AreEqual(
                GameDataQueryStatus.AliasResolved,
                store.QueryRecord("skill_weather", "Realm Strike").Status);
            Assert.AreEqual(
                GameDataQueryStatus.Found,
                store.QueryRecord("character_customization", "body_preset_average").Status);
        }

        [Test]
        public void EmptyRequiredWireFamiliesFailClosed()
        {
            foreach (var family in GameDataWireFamilySchemas.FamilyOrder)
            {
                var artifact = CatalogFixture.FamilyArtifact(
                    family,
                    family + "_v1",
                    "Catalogs/" + family + ".empty.json",
                    true,
                    "1.0.0",
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
        public void UnknownRecordFieldAndUnknownKindFailClosed()
        {
            var validSkill = ValidArtifacts().Single(artifact => artifact.Family == "skill_weather");
            var unknownField = CatalogFixture.MutateArtifact(
                validSkill,
                "\"role\":\"melee_damage\"",
                "\"role\":\"melee_damage\",\"unexpected_wrapper\":true");
            var unknownKind = CatalogFixture.MutateArtifact(
                validSkill,
                "\"kind\":\"skill_loadout\"",
                "\"kind\":\"unused_fourteen\"");

            foreach (var artifact in new[] { unknownField, unknownKind })
            {
                var result = Validate(artifact);
                Assert.AreEqual(GameDataCatalogLoadStatus.InvalidRecord, result.Status);
                Assert.IsNull(result.Snapshot);
            }
        }

        [Test]
        public void PackagedSkillWeatherEnvelopeLoadsThroughWireRegistry()
        {
            AssertPackagedFamily("skill_weather", 13);
        }

        [Test]
        public void PackagedRealmSpecializedEnvelopeLoadsThroughWireRegistry()
        {
            AssertPackagedFamily("realm_specialized", 13);
        }

        [Test]
        public void PackagedCharacterCustomizationEnvelopeLoadsThroughWireRegistry()
        {
            AssertPackagedFamily("character_customization", 114);
        }

        [Test]
        public void SchemaAssemblyHasNoUnityEngineDependency()
        {
            var schemaType = typeof(GameDataWireFamilySchemas);
            Assert.True(schemaType.IsAbstract && schemaType.IsSealed);
            Assert.AreSame(typeof(GameDataCatalogSchemaRegistry).Assembly, schemaType.Assembly);
            Assert.AreSame(
                typeof(GameDataProductionCatalogSchemas).Assembly,
                schemaType.Assembly);
            Assert.False(
                schemaType.Assembly.GetReferencedAssemblies()
                    .Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)));
        }

        private static CatalogFixture.Artifact[] ValidArtifacts()
        {
            return new[]
            {
                CatalogFixture.FamilyArtifact(
                    "realm_specialized",
                    "realm_specialized_v1",
                    "Catalogs/realm_specialized.fixture.json",
                    true,
                    "1.0.0",
                    "[{\"id\":\"crownlands\",\"kind\":\"realm\",\"legacy_runtime_id\":\"Crownlands\",\"display_name\":\"Crownlands\",\"sort_order\":0}]",
                    "[{\"legacyId\":\"Crownlands\",\"canonicalId\":\"crownlands\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]",
                    string.Empty),
                CatalogFixture.FamilyArtifact(
                    "character_customization",
                    "character_customization_v1",
                    "Catalogs/character_customization.fixture.json",
                    true,
                    "1.0.0",
                    "[{\"id\":\"body_preset_average\",\"kind\":\"body_preset\",\"legacy_id\":\"average\",\"display_name\":\"Average\",\"scale\":[1.0,1.0,1.0]}]",
                    "[]",
                    string.Empty),
                CatalogFixture.FamilyArtifact(
                    "skill_weather",
                    "skill_weather_v1",
                    "Catalogs/skill_weather.fixture.json",
                    true,
                    "1.0.0",
                    "[{\"id\":\"realm_strike\",\"kind\":\"skill_loadout\",\"slot\":0,\"display_name\":\"Realm Strike\",\"role\":\"melee_damage\",\"vfx_key\":\"realm_slash\",\"cooldown_seconds\":4.0,\"mana_cost\":20.0,\"cast_time_seconds\":0.05,\"range_meters\":2.6,\"power\":150.0,\"bot_damage_multiplier\":0.72}]",
                    "[{\"legacyId\":\"Realm Strike\",\"canonicalId\":\"realm_strike\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]",
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
                GameDataWireFamilySchemas.CreateRegistry(),
                CatalogFixture.Policy(),
                GameDataCatalogSourceKind.Packaged,
                new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 19, 0, 0, 1, TimeSpan.Zero));
        }

        private static GameDataCatalogStore Load(params CatalogFixture.Artifact[] artifacts)
        {
            var store = new GameDataCatalogStore();
            var loader = new GameDataCatalogLoader(
                CatalogFixture.Policy(),
                GameDataWireFamilySchemas.CreateRegistry());
            var operation = store.BeginLoad(
                loader,
                CatalogFixture.Source(artifacts),
                CatalogFixture.ManifestPath,
                GameDataCatalogSourceKind.Packaged);
            Assert.True(
                System.Threading.SpinWait.SpinUntil(() => operation.IsCompleted, 5000),
                "The bounded wire-family fixture should complete within five seconds.");
            Assert.AreEqual(GameDataCatalogLifecycleStatus.Ready, store.State.Status);
            return store;
        }

        private static void AssertPackagedFamily(string family, int expectedRecords)
        {
            var artifact = PackagedArtifact(family);
            var result = ValidatePackaged(artifact);
            Assert.AreEqual(GameDataCatalogLoadStatus.LoadedPackaged, result.Status, family);
            Assert.NotNull(result.Snapshot, family);
            GameDataFamilyCatalogSnapshot snapshot;
            Assert.True(result.Snapshot.FamiliesById.TryGetValue(family, out snapshot), family);
            Assert.AreEqual(expectedRecords, snapshot.Records.Count, family);
        }

        private static CatalogFixture.Artifact PackagedArtifact(string family)
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    family + ".v1.json"));
            Assert.True(File.Exists(path), path);
            return new CatalogFixture.Artifact(
                family,
                family + "_v1",
                family + ".v1.json",
                true,
                "1.0.0",
                File.ReadAllBytes(path));
        }

        private static GameDataCatalogLoadResult ValidatePackaged(CatalogFixture.Artifact artifact)
        {
            var manifestBytes = PackagedManifest(artifact);
            var manifestResult = GameDataCatalogValidator.ValidateManifest(
                manifestBytes,
                CatalogFixture.Policy());
            Assert.True(
                manifestResult.IsAccepted,
                string.Join("\n", manifestResult.Diagnostics.Select(diagnostic => diagnostic.Fingerprint)));

            return GameDataCatalogValidator.ValidateCatalogSet(
                manifestResult.Manifest,
                new[]
                {
                    new GameDataCatalogArtifactInput(
                        artifact.Path,
                        GameDataCatalogReadStatus.Succeeded,
                        artifact.Bytes,
                        string.Empty)
                },
                GameDataWireFamilySchemas.CreateRegistry(),
                CatalogFixture.Policy(),
                GameDataCatalogSourceKind.Packaged,
                new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 19, 0, 0, 1, TimeSpan.Zero));
        }

        private static byte[] PackagedManifest(CatalogFixture.Artifact artifact)
        {
            const string sourceRevision = "t_d4892ee5";
            var json =
                "{\n" +
                "  \"gameId\":\"another-life\",\n" +
                "  \"catalogSetId\":\"catalog_set_wire_packaged\",\n" +
                "  \"schemaVersion\":1,\n" +
                "  \"contentVersion\":\"1.0.0\",\n" +
                "  \"minimumRuntimeCatalogVersion\":1,\n" +
                "  \"sourceRevision\":\"" + sourceRevision + "\",\n" +
                "  \"artifacts\":[\n" +
                "    {\"family\":\"" + artifact.Family +
                "\",\"catalogId\":\"" + artifact.CatalogId +
                "\",\"relativePath\":\"" + artifact.Path +
                "\",\"schemaVersion\":1,\"contentVersion\":\"" + artifact.ContentVersion +
                "\",\"required\":true,\"sha256\":\"" + artifact.Sha256 +
                "\",\"mediaType\":\"application/json\",\"sourceMode\":\"authored\",\"sourceRevision\":\"" +
                sourceRevision +
                "\"}\n" +
                "  ]\n" +
                "}\n";
            return new UTF8Encoding(false, true).GetBytes(json);
        }

        private static GameDataCatalogFamilySchema Family(
            GameDataCatalogSchemaRegistry registry,
            string family)
        {
            GameDataCatalogFamilySchema schema;
            Assert.True(registry.TryGet(family, out schema), family);
            return schema;
        }

        private static GameDataCatalogFieldRule Field(
            GameDataCatalogSchemaRegistry registry,
            string family,
            string fieldName)
        {
            var schema = Family(registry, family);
            foreach (var field in schema.Fields)
            {
                if (string.Equals(field.Name, fieldName, StringComparison.Ordinal))
                {
                    return field;
                }
            }

            Assert.Fail(family + "." + fieldName + " is missing.");
            return null;
        }

        private static void AssertExactFields(
            GameDataCatalogFamilySchema schema,
            params string[] expected)
        {
            CollectionAssert.AreEqual(
                expected.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                schema.Fields.Select(field => field.Name).ToArray(),
                schema.Family);
        }
    }
}
