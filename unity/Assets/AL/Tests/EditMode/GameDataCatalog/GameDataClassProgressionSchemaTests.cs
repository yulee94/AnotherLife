using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataClassProgressionSchemaTests
    {
        [Test]
        public void RegistryDeclaresExactAtomicIdentityFamiliesAndBounds()
        {
            CollectionAssert.AreEqual(
                new[]
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
                },
                GameDataClassProgressionSchemas.FamilyOrder.ToArray());

            var registry = GameDataClassProgressionSchemas.CreateRegistry();
            CollectionAssert.AreEqual(
                new[]
                {
                    "class_families",
                    "class_mastery_trials",
                    "class_milestone_skills",
                    "class_resources",
                    "class_skill_branches",
                    "class_skill_trees",
                    "class_sources",
                    "class_warmaster_identities",
                    "playable_classes"
                },
                registry.Schemas.Select(schema => schema.Family).ToArray());
            Assert.AreEqual(9, registry.Schemas.Count);
            Assert.True(registry.Schemas.All(schema => !schema.AllowEmptyRecords));
            Assert.True(registry.Schemas.All(schema =>
                schema.SupportsVersion(GameDataClassProgressionSchemas.SchemaVersion)));

            AssertArrayBounds(
                Field(registry, "class_skill_trees", "branch_ids"),
                GameDataClassProgressionSchemas.BranchesPerClass,
                GameDataClassProgressionSchemas.BranchesPerClass);
            AssertArrayBounds(
                Field(registry, "class_skill_trees", "milestone_skill_ids"),
                GameDataClassProgressionSchemas.MilestonesPerClass,
                GameDataClassProgressionSchemas.MilestonesPerClass);
            AssertArrayBounds(
                Field(registry, "class_skill_trees", "milestone_levels"),
                GameDataClassProgressionSchemas.MilestonesPerClass,
                GameDataClassProgressionSchemas.MilestonesPerClass);
            AssertArrayBounds(
                Field(registry, "class_warmaster_identities", "piece_slot_ids"),
                GameDataClassProgressionSchemas.WarmasterPieceSlots,
                GameDataClassProgressionSchemas.WarmasterPieceSlots);

            Assert.AreEqual(
                GameDataClassProgressionSchemas.ActiveSkillSlots,
                Field(registry, "class_skill_trees", "active_slot_count").MinimumNumber);
            Assert.AreEqual(
                GameDataClassProgressionSchemas.ActiveSkillSlots,
                Field(registry, "class_skill_trees", "active_slot_count").MaximumNumber);
            Assert.AreEqual(
                GameDataClassProgressionSchemas.LaunchLevelCap,
                Field(registry, "class_mastery_trials", "minimum_level").MinimumNumber);
            Assert.AreEqual(
                GameDataClassProgressionSchemas.LaunchLevelCap,
                Field(registry, "class_warmaster_identities", "minimum_level").MaximumNumber);
        }

        [Test]
        public void SchemaOmitsExecutableBalanceAndPresentationAuthority()
        {
            var forbidden = new[]
            {
                "behavior_profile_id",
                "presentation_profile_id",
                "cooldown_seconds",
                "mana_cost",
                "power",
                "cast_time_seconds",
                "range_meters",
                "point_cost",
                "price",
                "warzone_point_threshold",
                "asset_ref"
            };
            var registry = GameDataClassProgressionSchemas.CreateRegistry();
            var fieldNames = registry.Schemas
                .SelectMany(schema => schema.Fields)
                .Select(field => field.Name)
                .ToArray();

            foreach (var forbiddenField in forbidden)
            {
                CollectionAssert.DoesNotContain(fieldNames, forbiddenField);
            }

            Assert.False(
                typeof(GameDataClassProgressionSchemas).Assembly.GetReferencedAssemblies()
                    .Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                "The class contracts must remain in the no-engine catalog assembly.");
            Assert.AreEqual(
                6,
                GameDataSixFamilySchemas.CreateRegistry().Schemas.Count,
                "The existing six-family v1 registry must remain unchanged.");
        }

        [Test]
        public void AcceptedNarrativePacketCompilesToIdentityOnlySnapshot()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var generic = ValidateGeneric(artifacts);
            Assert.AreEqual(GameDataCatalogLoadStatus.LoadedPackaged, generic.Status);
            Assert.NotNull(generic.Snapshot);
            Assert.AreEqual(
                GameDataClassProgressionSchemas.SourceProjectionSha256,
                ClassProgressionCatalogValidator.ComputeCanonicalSourceProjectionSha256(
                    generic.Snapshot));

            var result = ClassProgressionCatalogValidator.Validate(generic.Snapshot);
            Assert.True(
                result.IsAccepted,
                string.Join("\n", result.Diagnostics.Select(item => item.Fingerprint)));
            Assert.AreEqual(
                ClassProgressionCatalogValidationStatus.AcceptedIdentitySpine,
                result.Status);
            Assert.NotNull(result.Snapshot);
            Assert.False(result.IsProductionReady);
            Assert.False(result.Snapshot.IsProductionReady);
            Assert.AreEqual(
                ClassProgressionProductionReadiness.IdentitySpineOnly,
                result.Snapshot.ProductionReadiness);
            Assert.AreEqual(6, result.Snapshot.ProductionBlockerIds.Count);

            Assert.AreEqual(4, result.Snapshot.Families.Count);
            Assert.AreEqual(16, result.Snapshot.Classes.Count);
            Assert.AreEqual(16, result.Snapshot.Resources.Count);
            Assert.AreEqual(16, result.Snapshot.Trees.Count);
            Assert.AreEqual(48, result.Snapshot.Branches.Count);
            Assert.AreEqual(80, result.Snapshot.MilestoneSkills.Count);
            Assert.AreEqual(16, result.Snapshot.MasteryTrials.Count);
            Assert.AreEqual(16, result.Snapshot.WarmasterIdentities.Count);

            Assert.AreEqual(
                GameDataQueryStatus.Found,
                result.Snapshot.QueryClass("class_druid").Status);
            Assert.AreEqual(
                GameDataQueryStatus.AliasResolved,
                result.Snapshot.QueryMasteryTrial("SQ_Vanguard").Status);
            Assert.AreEqual(
                "class_trial_vanguard_frontline_eternity",
                result.Snapshot.QueryMasteryTrial("SQ_Vanguard").CanonicalId);
            Assert.AreEqual(
                GameDataQueryStatus.UnknownId,
                result.Snapshot.QueryClass("vanguard").Status,
                "The Forge visual label must not resolve as a class.");
            Assert.AreEqual(
                GameDataQueryStatus.UnknownId,
                result.Snapshot.QueryClass("Cursor").Status,
                "Legacy visual labels must not resolve as classes.");
            Assert.AreEqual(
                GameDataQueryStatus.UnknownId,
                result.Snapshot.QueryMilestoneSkill("realm_strike").Status,
                "Prototype skills must not satisfy class milestone identity.");
        }

        [Test]
        public void ExplicitLateEnumMappingsCannotBeDerivedByOrdinalRange()
        {
            var result = ValidateSemantic(ClassProgressionFixture.ValidArtifacts());
            Assert.True(result.IsAccepted);

            Assert.AreEqual(
                "family_warrior",
                StringField(result.Snapshot.QueryClass("class_paladin"), "family_id"));
            Assert.AreEqual(
                "family_mage",
                StringField(result.Snapshot.QueryClass("class_necromancer"), "family_id"));
            Assert.AreEqual(
                "family_assassin",
                StringField(result.Snapshot.QueryClass("class_slayer"), "family_id"));
            Assert.AreEqual(
                "family_ranger",
                StringField(result.Snapshot.QueryClass("class_druid"), "family_id"));
        }

        [Test]
        public void PacketHashAndExplicitFamilyMappingDriftFailClosed()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var source = artifacts.Single(item => item.Family == "class_sources");
            var classes = artifacts.Single(item => item.Family == "playable_classes");
            var badSource = CatalogFixture.MutateArtifact(
                source,
                GameDataClassProgressionSchemas.PacketSha256,
                new string('0', 64));
            var badClasses = ClassProgressionFixture.MutateRecord(
                classes,
                "class_paladin",
                "\"family_id\":\"family_warrior\"",
                "\"family_id\":\"family_mage\"");

            AssertSemanticFailure(
                Replace(artifacts, badSource),
                "AL-GDC-CLS-PACKET-HASH");
            AssertSemanticFailure(
                Replace(artifacts, badClasses),
                "AL-GDC-CLS-CLASS-FAMILY");
        }

        [Test]
        public void TreeMilestoneAndSupportPolicyDriftFailClosed()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var trees = artifacts.Single(item => item.Family == "class_skill_trees");
            var milestones = artifacts.Single(item => item.Family == "class_milestone_skills");
            var classes = artifacts.Single(item => item.Family == "playable_classes");

            var duplicateBranch = ClassProgressionFixture.MutateRecord(
                trees,
                "skill_tree_vanguard_general",
                "\"skill_branch_vanguard_war_banner\"",
                "\"skill_branch_vanguard_linebreaker\"");
            var wrongMilestone = ClassProgressionFixture.MutateRecord(
                milestones,
                "skill_vanguard_rallying_standard",
                "\"milestone_level\":20",
                "\"milestone_level\":21");
            var wrongHealer = ClassProgressionFixture.MutateRecord(
                classes,
                "class_druid",
                "\"primary_role_id\":\"healer\"",
                "\"primary_role_id\":\"damage\"");
            var extraPrimaryHealer = ClassProgressionFixture.MutateRecord(
                classes,
                "class_paladin",
                "\"primary_role_id\":\"protector_support\"",
                "\"primary_role_id\":\"healer\"");
            var extraSecondaryHealer = ClassProgressionFixture.MutateRecord(
                classes,
                "class_vanguard",
                "\"off_tank\"",
                "\"healer\"");

            AssertSemanticFailure(
                Replace(artifacts, duplicateBranch),
                "AL-GDC-CLS-BRANCH-DUPLICATE");
            AssertSemanticFailure(
                Replace(artifacts, wrongMilestone),
                "AL-GDC-CLS-MILESTONE-LEVEL");
            AssertSemanticFailure(
                Replace(artifacts, wrongHealer),
                "AL-GDC-CLS-HEALER-POLICY");
            AssertSemanticFailure(
                Replace(artifacts, extraPrimaryHealer),
                "AL-GDC-CLS-HEALER-POLICY");
            AssertSemanticFailure(
                Replace(artifacts, extraSecondaryHealer),
                "AL-GDC-CLS-HEALER-POLICY");
        }

        [Test]
        public void MasteryAndWarmasterBoundaryDriftFailClosed()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var trials = artifacts.Single(item => item.Family == "class_mastery_trials");
            var warmasters = artifacts.Single(item => item.Family == "class_warmaster_identities");
            var families = artifacts.Single(item => item.Family == "class_families");

            var gatingTrial = ClassProgressionFixture.MutateRecord(
                trials,
                "class_trial_vanguard_frontline_eternity",
                "\"gates_capstone\":false",
                "\"gates_capstone\":true");
            var duplicateWarmasterSlot = ClassProgressionFixture.MutateRecord(
                warmasters,
                "warmaster_set_vanguard_regalia_first_line",
                "\"class_relic\"]",
                "\"weapon\"]");
            var forgeAlias = CatalogFixture.MutateArtifact(
                families,
                "\"aliases\":[]",
                "\"aliases\":[{\"legacyId\":\"vanguard\",\"canonicalId\":\"family_warrior\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#184\"}]");

            AssertSemanticFailure(
                Replace(artifacts, gatingTrial),
                "AL-GDC-CLS-TRIAL-BOUNDARY");
            AssertSemanticFailure(
                Replace(artifacts, duplicateWarmasterSlot),
                "AL-GDC-CLS-WARMASTER-PIECES");
            AssertSemanticFailure(
                Replace(artifacts, forgeAlias),
                "AL-GDC-CLS-UNAUTHORIZED-ALIAS");
        }

        [Test]
        public void PartialPacketNeverPublishesAndDiagnosticsAreDeterministic()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var partial = artifacts
                .Where(item => item.Family != "class_warmaster_identities")
                .ToArray();
            var first = ValidateGeneric(partial);
            var second = ValidateGeneric(partial);

            Assert.False(first.IsSuccess);
            Assert.IsNull(first.Snapshot);
            Assert.AreEqual(
                GameDataCatalogLoadStatus.CrossReferenceFailure,
                first.Status);
            CollectionAssert.Contains(
                first.Diagnostics.Select(item => item.Code).ToArray(),
                "AL-GDC-REFERENCE-MISSING");
            CollectionAssert.AreEqual(
                first.Diagnostics.Select(item => item.Fingerprint).ToArray(),
                second.Diagnostics.Select(item => item.Fingerprint).ToArray());
        }

        [Test]
        public void AcceptedSnapshotIsImmutableAfterInputMutation()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var result = ValidateSemantic(artifacts);
            Assert.True(result.IsAccepted);

            Array.Clear(artifacts[0].Bytes, 0, artifacts[0].Bytes.Length);
            Assert.AreEqual(
                GameDataQueryStatus.Found,
                result.Snapshot.QueryClass("class_vanguard").Status);
            var mutableView = result.Snapshot.Classes as IList<GameDataCatalogRecord>;
            Assert.NotNull(mutableView);
            Assert.Throws<NotSupportedException>(() => mutableView.Add(null));
        }

        [Test]
        public void EnvelopeRequiresEveryArtifactAndValidatedSourceRevision()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var source = artifacts.Single(item => item.Family == "class_sources");
            var optionalSource = new CatalogFixture.Artifact(
                source.Family,
                source.CatalogId,
                source.Path,
                false,
                source.ContentVersion,
                source.Bytes);
            AssertSemanticFailure(
                Replace(artifacts, optionalSource),
                "AL-GDC-CLS-ARTIFACT-REQUIRED");

            const string wrongRevision = "class-source-revision-drift";
            var wrongRevisionArtifacts = artifacts
                .Select(item => CatalogFixture.MutateArtifact(
                    item,
                    "\"sourceRevision\":\"" +
                    GameDataClassProgressionSchemas.ValidatedRevision +
                    "\"",
                    "\"sourceRevision\":\"" + wrongRevision + "\""))
                .ToArray();
            var generic = ValidateGenericWithRevision(
                wrongRevision,
                wrongRevisionArtifacts);
            Assert.True(generic.IsSuccess);
            var result = ClassProgressionCatalogValidator.Validate(generic.Snapshot);
            Assert.False(result.IsAccepted);
            CollectionAssert.Contains(
                result.Diagnostics.Select(item => item.Code).ToArray(),
                "AL-GDC-CLS-SOURCE-REVISION");
        }

        [Test]
        public void CanonicalProjectionRejectsCoherentRenameAndAuthoredProseDrift()
        {
            var artifacts = ClassProgressionFixture.ValidArtifacts();
            var classes = artifacts.Single(item => item.Family == "playable_classes");
            var resources = artifacts.Single(item => item.Family == "class_resources");
            const string oldResourceId = "class_resource_vanguard_command";
            const string newResourceId = "class_resource_vanguard_command_renamed";

            var renamedClass = ClassProgressionFixture.MutateRecord(
                classes,
                "class_vanguard",
                oldResourceId,
                newResourceId);
            var renamedResource = ClassProgressionFixture.MutateRecord(
                resources,
                oldResourceId,
                oldResourceId,
                newResourceId);
            renamedResource = ClassProgressionFixture.MutateRecord(
                renamedResource,
                newResourceId,
                "class_resource.vanguard.command.name",
                "class_resource.vanguard.command_renamed.name");
            var rewrittenClass = ClassProgressionFixture.MutateRecord(
                classes,
                "class_vanguard",
                "\"identity_source_text\":\"A frontline commander",
                "\"identity_source_text\":\"A rewritten commander");

            AssertSemanticFailure(
                Replace(artifacts, renamedClass, renamedResource),
                "AL-GDC-CLS-SOURCE-PROJECTION");
            AssertSemanticFailure(
                Replace(artifacts, rewrittenClass),
                "AL-GDC-CLS-SOURCE-PROJECTION");
        }

        private static GameDataCatalogFamilySchema Family(
            GameDataCatalogSchemaRegistry registry,
            string family)
        {
            GameDataCatalogFamilySchema schema;
            Assert.True(registry.TryGet(family, out schema), "Missing schema: " + family);
            return schema;
        }

        private static GameDataCatalogFieldRule Field(
            GameDataCatalogSchemaRegistry registry,
            string family,
            string field)
        {
            return Family(registry, family).Fields.Single(item => item.Name == field);
        }

        private static void AssertArrayBounds(
            GameDataCatalogFieldRule field,
            int minimum,
            int maximum)
        {
            Assert.AreEqual(GameDataValueKind.Array, field.Kind, field.Name);
            Assert.AreEqual(minimum, field.MinimumItems, field.Name);
            Assert.AreEqual(maximum, field.MaximumItems, field.Name);
        }

        private static ClassProgressionCatalogValidationResult ValidateSemantic(
            params CatalogFixture.Artifact[] artifacts)
        {
            var generic = ValidateGeneric(artifacts);
            Assert.True(
                generic.IsSuccess,
                string.Join("\n", generic.Diagnostics.Select(item => item.Fingerprint)));
            return ClassProgressionCatalogValidator.Validate(generic.Snapshot);
        }

        private static GameDataCatalogLoadResult ValidateGeneric(
            params CatalogFixture.Artifact[] artifacts)
        {
            return ValidateGenericWithRevision(
                GameDataClassProgressionSchemas.ValidatedRevision,
                artifacts);
        }

        private static GameDataCatalogLoadResult ValidateGenericWithRevision(
            string sourceRevision,
            params CatalogFixture.Artifact[] artifacts)
        {
            var manifest = GameDataCatalogValidator.ValidateManifest(
                ClassProgressionFixture.Manifest(
                    artifacts,
                    sourceRevision),
                CatalogFixture.Policy());
            Assert.True(
                manifest.IsAccepted,
                string.Join("\n", manifest.Diagnostics.Select(item => item.Fingerprint)));
            return GameDataCatalogValidator.ValidateCatalogSet(
                manifest.Manifest,
                artifacts.Select(artifact => new GameDataCatalogArtifactInput(
                    artifact.Path,
                    GameDataCatalogReadStatus.Succeeded,
                    artifact.Bytes,
                    string.Empty)),
                GameDataClassProgressionSchemas.CreateRegistry(),
                CatalogFixture.Policy(),
                GameDataCatalogSourceKind.Packaged,
                new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 24, 0, 0, 1, TimeSpan.Zero));
        }

        private static void AssertSemanticFailure(
            CatalogFixture.Artifact[] artifacts,
            string expectedCode)
        {
            var result = ValidateSemantic(artifacts);
            Assert.False(result.IsAccepted);
            Assert.IsNull(result.Snapshot);
            CollectionAssert.Contains(
                result.Diagnostics.Select(item => item.Code).ToArray(),
                expectedCode,
                string.Join("\n", result.Diagnostics.Select(item => item.Fingerprint)));
        }

        private static CatalogFixture.Artifact[] Replace(
            IEnumerable<CatalogFixture.Artifact> artifacts,
            params CatalogFixture.Artifact[] replacements)
        {
            var replacementsByFamily = replacements.ToDictionary(
                item => item.Family,
                StringComparer.Ordinal);
            return artifacts
                .Select(item =>
                {
                    CatalogFixture.Artifact replacement;
                    return replacementsByFamily.TryGetValue(item.Family, out replacement)
                        ? replacement
                        : item;
                })
                .ToArray();
        }

        private static string StringField(GameDataCatalogQueryResult query, string field)
        {
            Assert.True(query.HasRecord);
            GameDataValue value;
            Assert.True(query.Record.TryGetField(field, out value));
            return ((GameDataStringValue)value).Value;
        }

        private static class ClassProgressionFixture
        {
            internal static CatalogFixture.Artifact[] ValidArtifacts()
            {
                var source = ReadSource();
                var sourceRecords = new List<SourceRecord>();
                var familyRecords = new List<FamilyRecord>();
                var classRecords = new List<ClassRecord>();
                var resourceRecords = new List<ResourceRecord>();
                var treeRecords = new List<TreeRecord>();
                var branchRecords = new List<BranchRecord>();
                var milestoneRecords = new List<MilestoneRecord>();
                var trialRecords = new List<TrialRecord>();
                var warmasterRecords = new List<WarmasterRecord>();

                sourceRecords.Add(new SourceRecord
                {
                    id = GameDataClassProgressionSchemas.SourceRecordId,
                    packet_id = source.Packet.packetId,
                    packet_version = source.Packet.packetVersion,
                    packet_sha256 = source.PacketSha256,
                    source_projection_sha256 =
                        GameDataClassProgressionSchemas.SourceProjectionSha256,
                    authored_revision = GameDataClassProgressionSchemas.AuthoredRevision,
                    validated_revision = GameDataClassProgressionSchemas.ValidatedRevision,
                    component_ids = source.Packet.components.Select(item => item.componentId).ToArray(),
                    component_family_ids = source.Packet.components.Select(item => item.familyId).ToArray(),
                    component_paths = source.Packet.components.Select(item => item.path).ToArray(),
                    component_sha256s = source.Packet.components.Select(item => item.sha256).ToArray(),
                    content_scope = GameDataClassProgressionSchemas.IdentityScope,
                    production_eligible = false
                });

                var classOrder = source.Packet.classIndex.ToDictionary(
                    item => item.id,
                    item => item.order,
                    StringComparer.Ordinal);
                for (var componentIndex = 0; componentIndex < source.Components.Length; componentIndex++)
                {
                    var component = source.Components[componentIndex];
                    var family = component.family;
                    familyRecords.Add(new FamilyRecord
                    {
                        id = family.id,
                        source_id = GameDataClassProgressionSchemas.SourceRecordId,
                        source_component_id = component.componentId,
                        source_order = componentIndex,
                        legacy_enum_name = family.legacyEnum.name,
                        legacy_enum_value = family.legacyEnum.value,
                        name_ref = family.name.key,
                        name_text = family.name.text,
                        identity_source_text = family.identity,
                        realm_ids = family.realmAvailability
                            .Select(value => value.ToLowerInvariant())
                            .ToArray(),
                        class_ids = family.classIds
                    });

                    for (var familyOrder = 0; familyOrder < family.classes.Length; familyOrder++)
                    {
                        var sourceClass = family.classes[familyOrder];
                        var classToken = sourceClass.id.Substring("class_".Length);
                        var treeId = "skill_tree_" + classToken + "_general";
                        classRecords.Add(new ClassRecord
                        {
                            id = sourceClass.id,
                            source_id = GameDataClassProgressionSchemas.SourceRecordId,
                            source_component_id = component.componentId,
                            family_id = family.id,
                            source_order = classOrder[sourceClass.id],
                            family_order = familyOrder,
                            legacy_subclass_name = sourceClass.legacySubclass.name,
                            legacy_subclass_value = sourceClass.legacySubclass.value,
                            name_ref = sourceClass.name.key,
                            name_text = sourceClass.name.text,
                            identity_source_text = sourceClass.identity,
                            primary_role_id = sourceClass.roles.primary,
                            secondary_role_ids = sourceClass.roles.secondary,
                            contribution_ids = sourceClass.roles.contribution,
                            equipment_armor_id = sourceClass.equipmentIdentity.armor,
                            equipment_main_hand_ids = sourceClass.equipmentIdentity.mainHand,
                            equipment_off_hand_ids = sourceClass.equipmentIdentity.offHand,
                            silhouette_source_text = sourceClass.equipmentIdentity.silhouette,
                            resource_id = sourceClass.resource.id,
                            tree_id = treeId,
                            mastery_trial_id = sourceClass.masteryTrial.id,
                            warmaster_set_id = sourceClass.warmaster.setId
                        });
                        resourceRecords.Add(new ResourceRecord
                        {
                            id = sourceClass.resource.id,
                            source_id = GameDataClassProgressionSchemas.SourceRecordId,
                            source_component_id = component.componentId,
                            class_id = sourceClass.id,
                            name_ref = sourceClass.resource.name.key,
                            name_text = sourceClass.resource.name.text,
                            gain_source_text = sourceClass.resource.gain,
                            spend_source_text = sourceClass.resource.spend
                        });

                        var branchIds = sourceClass.branches.Select(item => item.id).ToArray();
                        var milestoneIds = sourceClass.milestones.Select(item => item.skillId).ToArray();
                        var milestoneLevels = sourceClass.milestones.Select(item => item.level).ToArray();
                        treeRecords.Add(new TreeRecord
                        {
                            id = treeId,
                            source_id = GameDataClassProgressionSchemas.SourceRecordId,
                            source_component_id = component.componentId,
                            class_id = sourceClass.id,
                            visible_level = GameDataClassProgressionSchemas.VisibleFromLevel,
                            branch_policy = "non_exclusive",
                            branch_ids = branchIds,
                            milestone_skill_ids = milestoneIds,
                            milestone_levels = milestoneLevels,
                            capstone_skill_id = milestoneIds[milestoneIds.Length - 1],
                            active_slot_count = GameDataClassProgressionSchemas.ActiveSkillSlots,
                            completeness = GameDataClassProgressionSchemas.IdentityScope,
                            production_eligible = false
                        });
                        for (var branchIndex = 0; branchIndex < sourceClass.branches.Length; branchIndex++)
                        {
                            var branch = sourceClass.branches[branchIndex];
                            branchRecords.Add(new BranchRecord
                            {
                                id = branch.id,
                                source_id = GameDataClassProgressionSchemas.SourceRecordId,
                                source_component_id = component.componentId,
                                class_id = sourceClass.id,
                                tree_id = treeId,
                                branch_order = branchIndex,
                                name_ref = branch.name.key,
                                name_text = branch.name.text,
                                identity_source_text = branch.identity
                            });
                        }

                        for (var milestoneIndex = 0;
                             milestoneIndex < sourceClass.milestones.Length;
                             milestoneIndex++)
                        {
                            var milestone = sourceClass.milestones[milestoneIndex];
                            milestoneRecords.Add(new MilestoneRecord
                            {
                                id = milestone.skillId,
                                source_id = GameDataClassProgressionSchemas.SourceRecordId,
                                source_component_id = component.componentId,
                                class_id = sourceClass.id,
                                tree_id = treeId,
                                milestone_level = milestone.level,
                                name_ref = milestone.name.key,
                                name_text = milestone.name.text,
                                identity_source_text = milestone.identity,
                                identity_scope = "class_milestone",
                                production_eligible = false
                            });
                        }

                        trialRecords.Add(new TrialRecord
                        {
                            id = sourceClass.masteryTrial.id,
                            source_id = GameDataClassProgressionSchemas.SourceRecordId,
                            source_component_id = component.componentId,
                            class_id = sourceClass.id,
                            name_ref = sourceClass.masteryTrial.name.key,
                            name_text = sourceClass.masteryTrial.name.text,
                            summary_source_text = sourceClass.masteryTrial.summary,
                            availability_source_text = sourceClass.masteryTrial.availability,
                            boundary_source_text = sourceClass.masteryTrial.boundary,
                            minimum_level = GameDataClassProgressionSchemas.LaunchLevelCap,
                            is_optional = true,
                            is_recoverable = true,
                            is_critical_path = false,
                            gates_capstone = false,
                            gates_warmaster = false
                        });
                        warmasterRecords.Add(new WarmasterRecord
                        {
                            id = sourceClass.warmaster.setId,
                            source_id = GameDataClassProgressionSchemas.SourceRecordId,
                            source_component_id = component.componentId,
                            class_id = sourceClass.id,
                            title_id = sourceClass.warmaster.titleId,
                            title_name_ref = sourceClass.warmaster.title.key,
                            title_name_text = sourceClass.warmaster.title.text,
                            set_name_ref = sourceClass.warmaster.setName.key,
                            set_name_text = sourceClass.warmaster.setName.text,
                            relic_id = sourceClass.warmaster.relicId,
                            relic_name_ref = sourceClass.warmaster.relicName.key,
                            relic_name_text = sourceClass.warmaster.relicName.text,
                            ultimate_skill_id = sourceClass.warmaster.ultimateSkillId,
                            ultimate_name_ref = sourceClass.warmaster.ultimateName.key,
                            ultimate_name_text = sourceClass.warmaster.ultimateName.text,
                            identity_source_text = sourceClass.warmaster.identity,
                            counterplay_source_text = sourceClass.warmaster.counterplay,
                            piece_slot_ids = sourceClass.warmaster.pieceSlots,
                            minimum_level = GameDataClassProgressionSchemas.LaunchLevelCap,
                            requires_realm_contract = true,
                            requires_committed_warzone_points = true,
                            requires_complete_set = true,
                            active_slot_policy = "standard_four_slot",
                            production_eligible = false
                        });
                    }
                }

                var version = GameDataClassProgressionSchemas.PacketVersion;
                return new[]
                {
                    Artifact("class_sources", version, ToJsonArray(sourceRecords), "[]"),
                    Artifact("class_families", version, ToJsonArray(familyRecords), "[]"),
                    Artifact("playable_classes", version, ToJsonArray(classRecords), "[]"),
                    Artifact("class_resources", version, ToJsonArray(resourceRecords), "[]"),
                    Artifact("class_skill_trees", version, ToJsonArray(treeRecords), "[]"),
                    Artifact("class_skill_branches", version, ToJsonArray(branchRecords), "[]"),
                    Artifact("class_milestone_skills", version, ToJsonArray(milestoneRecords), "[]"),
                    Artifact(
                        "class_mastery_trials",
                        version,
                        ToJsonArray(trialRecords),
                        TrialAliases(source.Components)),
                    Artifact(
                        "class_warmaster_identities",
                        version,
                        ToJsonArray(warmasterRecords),
                        "[]")
                };
            }

            internal static CatalogFixture.Artifact MutateRecord(
                CatalogFixture.Artifact artifact,
                string recordId,
                string oldValue,
                string newValue)
            {
                var text = CatalogFixture.Text(artifact.Bytes);
                var marker = "\"id\":\"" + recordId + "\"";
                var start = text.IndexOf(marker, StringComparison.Ordinal);
                Assert.GreaterOrEqual(start, 0, "Missing fixture record: " + recordId);
                var next = text.IndexOf("},{\"id\":", start, StringComparison.Ordinal);
                var end = next >= 0 ? next + 1 : text.IndexOf("],", start, StringComparison.Ordinal);
                Assert.Greater(end, start, "Could not bound fixture record: " + recordId);
                var record = text.Substring(start, end - start);
                var changed = record.Replace(oldValue, newValue);
                Assert.AreNotEqual(record, changed, "The record mutation must change exact bytes.");
                var mutated = text.Substring(0, start) + changed + text.Substring(end);
                return new CatalogFixture.Artifact(
                    artifact.Family,
                    artifact.CatalogId,
                    artifact.Path,
                    artifact.Required,
                    artifact.ContentVersion,
                    CatalogFixture.Bytes(mutated));
            }

            private static CatalogFixture.Artifact Artifact(
                string family,
                string version,
                string records,
                string aliases)
            {
                var catalogId = family + "_identity_v001";
                var path = "Catalogs/" + family + ".identity_v001.json";
                var json =
                    "{\n" +
                    "  \"gameId\":\"another-life\",\n" +
                    "  \"catalogId\":\"" + catalogId + "\",\n" +
                    "  \"family\":\"" + family + "\",\n" +
                    "  \"schemaVersion\":1,\n" +
                    "  \"contentVersion\":\"" + version + "\",\n" +
                    "  \"sourceRevision\":\"" +
                    GameDataClassProgressionSchemas.ValidatedRevision +
                    "\",\n" +
                    "  \"records\":" + records + ",\n" +
                    "  \"aliases\":" + aliases + "\n" +
                    "}\n";
                return new CatalogFixture.Artifact(
                    family,
                    catalogId,
                    path,
                    true,
                    version,
                    CatalogFixture.Bytes(json));
            }

            internal static byte[] Manifest(
                IEnumerable<CatalogFixture.Artifact> artifacts,
                string sourceRevision)
            {
                var rows = artifacts.Select(artifact =>
                    "    {\"family\":\"" + artifact.Family +
                    "\",\"catalogId\":\"" + artifact.CatalogId +
                    "\",\"relativePath\":\"" + artifact.Path.Replace("\\", "\\\\") +
                    "\",\"schemaVersion\":1,\"contentVersion\":\"" +
                    artifact.ContentVersion +
                    "\",\"required\":" + (artifact.Required ? "true" : "false") +
                    ",\"sha256\":\"" + artifact.Sha256 +
                    "\",\"mediaType\":\"application/json\",\"sourceMode\":\"" +
                    GameDataClassProgressionSchemas.CatalogSourceMode +
                    "\",\"sourceRevision\":\"" + sourceRevision + "\"}");
                var json =
                    "{\n" +
                    "  \"gameId\":\"another-life\",\n" +
                    "  \"catalogSetId\":\"" +
                    GameDataClassProgressionSchemas.CatalogSetId +
                    "\",\n" +
                    "  \"schemaVersion\":1,\n" +
                    "  \"contentVersion\":\"" +
                    GameDataClassProgressionSchemas.PacketVersion +
                    "\",\n" +
                    "  \"minimumRuntimeCatalogVersion\":1,\n" +
                    "  \"sourceRevision\":\"" + sourceRevision + "\",\n" +
                    "  \"artifacts\":[\n" +
                    string.Join(",\n", rows) +
                    "\n  ]\n" +
                    "}\n";
                return CatalogFixture.Bytes(json);
            }

            private static string ToJsonArray<T>(IEnumerable<T> records)
            {
                return "[" + string.Join(
                    ",",
                    records.Select(record => JsonUtility.ToJson(record))) + "]";
            }

            private static string TrialAliases(IEnumerable<FamilyComponentDto> components)
            {
                var rows = components
                    .SelectMany(component => component.family.classes)
                    .Select(sourceClass =>
                        "{\"legacyId\":\"" + sourceClass.masteryTrial.legacyAlias +
                        "\",\"canonicalId\":\"" + sourceClass.masteryTrial.id +
                        "\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#14\"}");
                return "[" + string.Join(",", rows) + "]";
            }

            private static SourceBundle ReadSource()
            {
                var repositoryRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", ".."));
                var packetPath = Path.Combine(
                    repositoryRoot,
                    "unity",
                    "Docs",
                    "Narrative",
                    "Classes",
                    "ANOTHERLIFE_CLASS_IDENTITY_SKILL_TREES.packet.json");
                var packetBytes = ReadCanonicalSourceBytes(packetPath);
                var packet = JsonUtility.FromJson<PacketDto>(
                    Encoding.UTF8.GetString(packetBytes));
                var packetSha256 = GameDataCatalogValidator.ComputeSha256(packetBytes);
                Assert.AreEqual(GameDataClassProgressionSchemas.PacketId, packet.packetId);
                Assert.AreEqual(GameDataClassProgressionSchemas.PacketVersion, packet.packetVersion);
                Assert.AreEqual(GameDataClassProgressionSchemas.PacketSha256, packetSha256);
                Assert.AreEqual(GameDataClassProgressionSchemas.ExpectedFamilyCount, packet.components.Length);
                Assert.AreEqual(GameDataClassProgressionSchemas.ExpectedClassCount, packet.classIndex.Length);

                var components = new List<FamilyComponentDto>();
                for (var index = 0; index < packet.components.Length; index++)
                {
                    var manifest = packet.components[index];
                    var componentPath = Path.Combine(
                        repositoryRoot,
                        manifest.path.Replace('/', Path.DirectorySeparatorChar));
                    var bytes = ReadCanonicalSourceBytes(componentPath);
                    Assert.AreEqual(
                        manifest.sha256,
                        GameDataCatalogValidator.ComputeSha256(bytes),
                        manifest.path);
                    var component = JsonUtility.FromJson<FamilyComponentDto>(
                        Encoding.UTF8.GetString(bytes));
                    Assert.AreEqual(manifest.componentId, component.componentId);
                    Assert.AreEqual(manifest.familyId, component.family.id);
                    Assert.AreEqual(packet.packetVersion, component.parentPacketVersion);
                    components.Add(component);
                }

                return new SourceBundle(packet, packetSha256, components.ToArray());
            }

            private static byte[] ReadCanonicalSourceBytes(string path)
            {
                var text = File.ReadAllText(path, new UTF8Encoding(false, true));
                var normalized = text
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");
                return new UTF8Encoding(false).GetBytes(normalized);
            }

            private sealed class SourceBundle
            {
                public SourceBundle(
                    PacketDto packet,
                    string packetSha256,
                    FamilyComponentDto[] components)
                {
                    Packet = packet;
                    PacketSha256 = packetSha256;
                    Components = components;
                }

                public PacketDto Packet { get; }
                public string PacketSha256 { get; }
                public FamilyComponentDto[] Components { get; }
            }
        }

        [Serializable]
        private sealed class PacketDto
        {
            public string packetVersion;
            public string packetId;
            public ComponentManifestDto[] components;
            public ClassIndexDto[] classIndex;
        }

        [Serializable]
        private sealed class ComponentManifestDto
        {
            public string componentId;
            public string familyId;
            public string path;
            public string sha256;
        }

        [Serializable]
        private sealed class ClassIndexDto
        {
            public int order;
            public string id;
        }

        [Serializable]
        private sealed class FamilyComponentDto
        {
            public string parentPacketVersion;
            public string componentId;
            public SourceFamilyDto family;
        }

        [Serializable]
        private sealed class SourceFamilyDto
        {
            public string id;
            public LegacyEnumDto legacyEnum;
            public NameDto name;
            public string identity;
            public string[] realmAvailability;
            public string[] classIds;
            public SourceClassDto[] classes;
        }

        [Serializable]
        private sealed class SourceClassDto
        {
            public string id;
            public LegacyEnumDto legacySubclass;
            public NameDto name;
            public string identity;
            public RolesDto roles;
            public EquipmentDto equipmentIdentity;
            public ResourceDto resource;
            public BranchDto[] branches;
            public MilestoneDto[] milestones;
            public MasteryTrialDto masteryTrial;
            public WarmasterDto warmaster;
        }

        [Serializable]
        private sealed class LegacyEnumDto
        {
            public string name;
            public int value;
        }

        [Serializable]
        private sealed class NameDto
        {
            public string key;
            public string text;
        }

        [Serializable]
        private sealed class RolesDto
        {
            public string primary;
            public string[] secondary;
            public string[] contribution;
        }

        [Serializable]
        private sealed class EquipmentDto
        {
            public string armor;
            public string[] mainHand;
            public string[] offHand;
            public string silhouette;
        }

        [Serializable]
        private sealed class ResourceDto
        {
            public string id;
            public NameDto name;
            public string gain;
            public string spend;
        }

        [Serializable]
        private sealed class BranchDto
        {
            public string id;
            public NameDto name;
            public string identity;
        }

        [Serializable]
        private sealed class MilestoneDto
        {
            public int level;
            public string skillId;
            public NameDto name;
            public string identity;
        }

        [Serializable]
        private sealed class MasteryTrialDto
        {
            public string id;
            public string legacyAlias;
            public NameDto name;
            public string summary;
            public string availability;
            public string boundary;
        }

        [Serializable]
        private sealed class WarmasterDto
        {
            public string titleId;
            public NameDto title;
            public string setId;
            public NameDto setName;
            public string relicId;
            public NameDto relicName;
            public string ultimateSkillId;
            public NameDto ultimateName;
            public string identity;
            public string counterplay;
            public string[] pieceSlots;
        }

        [Serializable]
        private sealed class SourceRecord
        {
            public string id;
            public string packet_id;
            public string packet_version;
            public string packet_sha256;
            public string source_projection_sha256;
            public string authored_revision;
            public string validated_revision;
            public string[] component_ids;
            public string[] component_family_ids;
            public string[] component_paths;
            public string[] component_sha256s;
            public string content_scope;
            public bool production_eligible;
        }

        [Serializable]
        private sealed class FamilyRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public int source_order;
            public string legacy_enum_name;
            public int legacy_enum_value;
            public string name_ref;
            public string name_text;
            public string identity_source_text;
            public string[] realm_ids;
            public string[] class_ids;
        }

        [Serializable]
        private sealed class ClassRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public string family_id;
            public int source_order;
            public int family_order;
            public string legacy_subclass_name;
            public int legacy_subclass_value;
            public string name_ref;
            public string name_text;
            public string identity_source_text;
            public string primary_role_id;
            public string[] secondary_role_ids;
            public string[] contribution_ids;
            public string equipment_armor_id;
            public string[] equipment_main_hand_ids;
            public string[] equipment_off_hand_ids;
            public string silhouette_source_text;
            public string resource_id;
            public string tree_id;
            public string mastery_trial_id;
            public string warmaster_set_id;
        }

        [Serializable]
        private sealed class ResourceRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public string class_id;
            public string name_ref;
            public string name_text;
            public string gain_source_text;
            public string spend_source_text;
        }

        [Serializable]
        private sealed class TreeRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public string class_id;
            public int visible_level;
            public string branch_policy;
            public string[] branch_ids;
            public string[] milestone_skill_ids;
            public int[] milestone_levels;
            public string capstone_skill_id;
            public int active_slot_count;
            public string completeness;
            public bool production_eligible;
        }

        [Serializable]
        private sealed class BranchRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public string class_id;
            public string tree_id;
            public int branch_order;
            public string name_ref;
            public string name_text;
            public string identity_source_text;
        }

        [Serializable]
        private sealed class MilestoneRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public string class_id;
            public string tree_id;
            public int milestone_level;
            public string name_ref;
            public string name_text;
            public string identity_source_text;
            public string identity_scope;
            public bool production_eligible;
        }

        [Serializable]
        private sealed class TrialRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public string class_id;
            public string name_ref;
            public string name_text;
            public string summary_source_text;
            public string availability_source_text;
            public string boundary_source_text;
            public int minimum_level;
            public bool is_optional;
            public bool is_recoverable;
            public bool is_critical_path;
            public bool gates_capstone;
            public bool gates_warmaster;
        }

        [Serializable]
        private sealed class WarmasterRecord
        {
            public string id;
            public string source_id;
            public string source_component_id;
            public string class_id;
            public string title_id;
            public string title_name_ref;
            public string title_name_text;
            public string set_name_ref;
            public string set_name_text;
            public string relic_id;
            public string relic_name_ref;
            public string relic_name_text;
            public string ultimate_skill_id;
            public string ultimate_name_ref;
            public string ultimate_name_text;
            public string identity_source_text;
            public string counterplay_source_text;
            public string[] piece_slot_ids;
            public int minimum_level;
            public bool requires_realm_contract;
            public bool requires_committed_warzone_points;
            public bool requires_complete_set;
            public string active_slot_policy;
            public bool production_eligible;
        }
    }
}
