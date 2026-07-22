using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using AL.Data.Catalogs;
using NUnit.Framework;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class GameDataCatalogFoundationTests
    {
        [Test]
        public void ValidRequiredSetPublishesOneImmutableSnapshotWithObservableAliasAndReference()
        {
            var set = CatalogFixture.ValidSet();
            var source = CatalogFixture.Source(set);
            var originalSkillBytes = (byte[])set[1].Bytes.Clone();
            source.Add(set[1].Path, originalSkillBytes);
            for (var index = 0; index < originalSkillBytes.Length; index++)
            {
                originalSkillBytes[index] = (byte)'x';
            }

            var store = CatalogFixture.Load(source);

            Assert.AreEqual(GameDataCatalogLifecycleStatus.Ready, store.State.Status);
            Assert.AreEqual(1, store.Snapshot.Revision);
            Assert.AreEqual("catalog_set_test", store.Snapshot.CatalogSetId);
            Assert.AreEqual("test-revision", store.Snapshot.SourceRevision);
            CollectionAssert.AreEqual(
                new[] { "champions", "skills" },
                store.Snapshot.Families.Select(item => item.Family).ToArray(),
                "Manifest family order must be retained.");

            var champion = store.QueryRecord("champions", "warden");
            Assert.AreEqual(GameDataQueryStatus.Found, champion.Status);
            Assert.AreEqual("champions_v1", champion.CatalogId);
            GameDataValue referenceValue;
            Assert.True(champion.Record.TryGetField("skill_id", out referenceValue));
            Assert.AreEqual("ember", ((GameDataStringValue)referenceValue).Value);

            var alias = store.QueryRecord("skills", "Old Ember");
            Assert.AreEqual(GameDataQueryStatus.AliasResolved, alias.Status);
            Assert.AreEqual("ember", alias.CanonicalId);
            Assert.AreEqual("skills_v1", alias.CatalogId);
            Assert.AreEqual("1.0.0", alias.ContentVersion);
            Assert.AreEqual("test-revision", alias.SourceRevision);
            Assert.AreEqual("AL-GDC-QUERY-ALIAS-RESOLVED", alias.Diagnostics.Single().Code);

            var unknown = store.QueryRecord("skills", "missing");
            Assert.AreEqual(GameDataQueryStatus.UnknownId, unknown.Status);
            Assert.AreEqual("skills_v1", unknown.CatalogId, "Known-family misses retain catalog provenance.");

            var first = store.QueryRecord("skills", "ember");
            var second = store.QueryRecord("skills", "ember");
            Assert.AreSame(first.Record, second.Record, "Pure queries reuse the immutable record.");
            GameDataValue tagsValue;
            Assert.True(first.Record.TryGetField("tags", out tagsValue));
            var tags = (GameDataArrayValue)tagsValue;
            var nongenericItems = (IList)tags.Items;
            Assert.True(nongenericItems.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => nongenericItems.Add(GameDataNullValue.Instance));
            var nongenericFields = (IDictionary)first.Record.Fields;
            Assert.True(nongenericFields.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => nongenericFields.Add("tamper", GameDataNullValue.Instance));
        }

        [Test]
        public void OptionalNotFoundIsExplicitButInvalidOptionalAndUndeclaredFamiliesAreNotGaps()
        {
            var skill = CatalogFixture.SkillArtifact();
            var optional = CatalogFixture.CosmeticsArtifact(required: false);
            var source = new InMemoryGameDataCatalogSource()
                .Add(CatalogFixture.ManifestPath, CatalogFixture.Manifest(skill, optional))
                .Add(skill.Path, skill.Bytes);

            var store = CatalogFixture.Load(source);
            Assert.AreEqual(GameDataCatalogLifecycleStatus.ReadyWithOptionalGaps, store.State.Status);
            CollectionAssert.AreEqual(new[] { "cosmetics" }, store.Snapshot.MissingOptionalFamilies);
            var optionalQuery = store.QueryRecord("cosmetics", "anything");
            Assert.AreEqual(GameDataQueryStatus.OptionalAbsent, optionalQuery.Status);
            Assert.AreEqual("cosmetics_v1", optionalQuery.CatalogId);
            Assert.AreEqual("1.0.0", optionalQuery.ContentVersion);
            Assert.AreEqual(GameDataQueryStatus.CatalogUnavailable, store.QueryRecord("world", "anything").Status);

            var failedOptionalSource = new InMemoryGameDataCatalogSource()
                .Add(CatalogFixture.ManifestPath, CatalogFixture.Manifest(skill, optional))
                .Add(skill.Path, skill.Bytes)
                .AddReadFailure(optional.Path, "fixture_read_failure");
            var failedStore = CatalogFixture.Load(failedOptionalSource);
            Assert.IsNull(failedStore.Snapshot);
            Assert.AreEqual(GameDataCatalogLoadStatus.ReadFailed, failedStore.LastLoadResult.Status);
            StringAssert.Contains("fixture_read_failure", failedStore.LastLoadResult.Diagnostics[0].TechnicalMessage);
        }

        [Test]
        public void RequiredMissingAndHashMismatchPublishNothing()
        {
            var skill = CatalogFixture.SkillArtifact();
            var missingSource = new InMemoryGameDataCatalogSource()
                .Add(CatalogFixture.ManifestPath, CatalogFixture.Manifest(skill));
            var missingStore = CatalogFixture.Load(missingSource);
            Assert.IsNull(missingStore.Snapshot);
            Assert.AreEqual(GameDataCatalogLoadStatus.MissingArtifact, missingStore.LastLoadResult.Status);

            var changed = CatalogFixture.Bytes(Encoding.UTF8.GetString(skill.Bytes).Replace("\"power\":5", "\"power\":6"));
            var mismatchSource = new InMemoryGameDataCatalogSource()
                .Add(CatalogFixture.ManifestPath, CatalogFixture.Manifest(skill))
                .Add(skill.Path, changed);
            var mismatchStore = CatalogFixture.Load(mismatchSource);
            Assert.IsNull(mismatchStore.Snapshot);
            Assert.AreEqual(GameDataCatalogLoadStatus.HashMismatch, mismatchStore.LastLoadResult.Status);
            CollectionAssert.Contains(
                mismatchStore.LastLoadResult.Diagnostics.Select(item => item.Code).ToArray(),
                "AL-GDC-HASH-MISMATCH");
        }

        [Test]
        public void ManifestStrictnessRejectsIdentityVersionPathTypeAndSelectionDrift()
        {
            var skill = CatalogFixture.SkillArtifact();
            var valid = CatalogFixture.Text(CatalogFixture.Manifest(skill));
            var cases = new Dictionary<string, string>
            {
                { "game case", valid.Replace("\"another-life\"", "\"Another-Life\"") },
                { "unsupported schema", valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2") },
                { "future runtime", valid.Replace("\"minimumRuntimeCatalogVersion\":1", "\"minimumRuntimeCatalogVersion\":2") },
                { "required string", valid.Replace("\"required\":true", "\"required\":\"true\"") },
                { "blank catalog id", valid.Replace("\"catalogId\":\"skills_v1\"", "\"catalogId\":\"\"") },
                { "traversal", valid.Replace(skill.Path, "../skills.json") },
                { "absolute", valid.Replace(skill.Path, "C:/skills.json") },
                { "backslash", valid.Replace(skill.Path, "Catalogs\\\\skills.v1.json") },
                { "uppercase hash", valid.Replace(skill.Sha256, skill.Sha256.ToUpperInvariant()) },
                { "media type", valid.Replace("\"mediaType\":\"application/json\"", "\"mediaType\":\"text/json\"") },
                { "unknown root", valid.Replace("{\n", "{\n  \"unexpected\":true,\n") }
            };

            foreach (var item in cases)
            {
                var result = GameDataCatalogValidator.ValidateManifest(
                    CatalogFixture.Bytes(item.Value),
                    CatalogFixture.Policy());
                Assert.False(result.IsAccepted, item.Key);
                Assert.IsNull(result.Manifest, item.Key);
                Assert.That(result.Diagnostics.Count, Is.GreaterThan(0), item.Key);
            }

            var duplicateSelection = CatalogFixture.Manifest(skill, skill);
            var duplicateResult = GameDataCatalogValidator.ValidateManifest(duplicateSelection, CatalogFixture.Policy());
            Assert.False(duplicateResult.IsAccepted);
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "AL-GDC-FAMILY-DUPLICATE",
                    "AL-GDC-CATALOG-ID-DUPLICATE",
                    "AL-GDC-ARTIFACT-PATH-DUPLICATE"
                },
                duplicateResult.Diagnostics.Select(item => item.Code).ToArray());
        }

        [Test]
        public void StrictJsonRejectsEmptyBomInvalidUtf8DuplicatePropertiesAndTrailingContent()
        {
            var skill = CatalogFixture.SkillArtifact();
            var valid = CatalogFixture.Manifest(skill);
            var cases = new List<byte[]>
            {
                new byte[0],
                new byte[] { 0xef, 0xbb, 0xbf }.Concat(valid).ToArray(),
                new byte[] { 0xff },
                CatalogFixture.Bytes("{\"gameId\":\"another-life\",\"gameId\":\"another-life\"}"),
                valid.Concat(CatalogFixture.Bytes("{}" )).ToArray()
            };

            foreach (var bytes in cases)
            {
                var result = GameDataCatalogValidator.ValidateManifest(bytes, CatalogFixture.Policy());
                Assert.AreEqual(GameDataCatalogLoadStatus.MalformedJson, result.Status);
                Assert.IsNull(result.Manifest);
                Assert.That(result.Diagnostics.Single().Code, Does.StartWith(GameDataCatalogContract.DiagnosticPrefix));
            }
        }

        [Test]
        public void EnvelopeAndNestedRecordFieldsAreStrictAtEveryDepth()
        {
            var strictCases = new[]
            {
                CatalogFixture.SkillArtifact(records: "[{\"id\":\"ember\",\"power\":5,\"tags\":[\"fire\"],\"unknown\":true}]"),
                CatalogFixture.SkillArtifact(records: "[{\"id\":\"ember\",\"id\":\"ember\",\"power\":5,\"tags\":[\"fire\"]}]"),
                CatalogFixture.SkillArtifact(records: "[{\"id\":\"ember\",\"power\":5}]"),
                CatalogFixture.SkillArtifact(records: "[{\"id\":\"ember\",\"power\":101,\"tags\":[\"fire\"]}]"),
                CatalogFixture.SkillArtifact(records: "[{\"id\":\"ember\",\"power\":1e999,\"tags\":[\"fire\"]}]"),
                CatalogFixture.ChampionArtifact(records: "[{\"id\":\"warden\",\"skill_id\":\"ember\",\"stats\":{\"level\":1,\"role\":\"tank\",\"extra\":0}}]"),
                CatalogFixture.ChampionArtifact(records: "[{\"id\":\"warden\",\"skill_id\":\"ember\",\"stats\":{\"level\":1,\"role\":\"mage\"}}]"),
                CatalogFixture.SkillArtifact(extraRoot: ",\n  \"unexpected\":true")
            };

            foreach (var artifact in strictCases)
            {
                var source = new InMemoryGameDataCatalogSource()
                    .Add(CatalogFixture.ManifestPath, CatalogFixture.Manifest(artifact))
                    .Add(artifact.Path, artifact.Bytes);
                var store = CatalogFixture.Load(source);
                Assert.IsNull(store.Snapshot, artifact.Path + " should fail closed");
                Assert.That(
                    store.LastLoadResult.Status,
                    Is.EqualTo(GameDataCatalogLoadStatus.InvalidRecord)
                        .Or.EqualTo(GameDataCatalogLoadStatus.InvalidEnvelope)
                        .Or.EqualTo(GameDataCatalogLoadStatus.MalformedJson));
            }
        }

        [Test]
        public void EnvelopeIdentityVersionAndRegisteredFamilyMustMatchManifestExactly()
        {
            var baseline = CatalogFixture.SkillArtifact();
            var cases = new[]
            {
                new[] { "\"gameId\":\"another-life\"", "\"gameId\":\"another-game\"", "AL-GDC-GAME-ID" },
                new[] { "\"catalogId\":\"skills_v1\"", "\"catalogId\":\"skills_other\"", "AL-GDC-CATALOG-ID-MISMATCH" },
                new[] { "\"family\":\"skills\"", "\"family\":\"champions\"", "AL-GDC-FAMILY-MISMATCH" },
                new[] { "\"schemaVersion\":1", "\"schemaVersion\":2", "AL-GDC-FAMILY-VERSION-UNSUPPORTED" },
                new[] { "\"contentVersion\":\"1.0.0\"", "\"contentVersion\":\"1.0.1\"", "AL-GDC-CONTENT-VERSION-MISMATCH" },
                new[] { "\"sourceRevision\":\"test-revision\"", "\"sourceRevision\":\"other-revision\"", "AL-GDC-SOURCE-REVISION-MISMATCH" }
            };

            foreach (var item in cases)
            {
                var artifact = CatalogFixture.MutateArtifact(baseline, item[0], item[1]);
                var store = CatalogFixture.Load(CatalogFixture.Source(new[] { artifact }));
                Assert.IsNull(store.Snapshot, item[2]);
                CollectionAssert.Contains(
                    store.LastLoadResult.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                    item[2]);
            }

            var unknown = CatalogFixture.FamilyArtifact(
                "unknown_family",
                "unknown_family_v1",
                "Catalogs/unknown_family.v1.json",
                true,
                "1.0.0",
                "[]",
                "[]",
                string.Empty);
            var unsupportedStore = CatalogFixture.Load(CatalogFixture.Source(new[] { unknown }));
            Assert.IsNull(unsupportedStore.Snapshot);
            Assert.AreEqual(GameDataCatalogLoadStatus.UnsupportedVersion, unsupportedStore.LastLoadResult.Status);
            CollectionAssert.Contains(
                unsupportedStore.LastLoadResult.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                "AL-GDC-FAMILY-UNSUPPORTED");
        }

        [TestCase("")]
        [TestCase(" Upper")]
        [TestCase("Upper")]
        [TestCase("1alpha")]
        [TestCase("alpha-beta")]
        [TestCase("alpha__beta")]
        [TestCase("alpha_")]
        [TestCase("álpha")]
        public void CanonicalRecordIdsAreExactLowerSnakeCase(string invalidId)
        {
            var escaped = invalidId.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var skill = CatalogFixture.SkillArtifact(
                records: "[{\"id\":\"" + escaped + "\",\"power\":5,\"tags\":[\"fire\"]}]");
            var store = CatalogFixture.Load(CatalogFixture.Source(new[] { skill }));
            Assert.IsNull(store.Snapshot);
            CollectionAssert.Contains(
                store.LastLoadResult.Diagnostics.Select(item => item.Code).ToArray(),
                "AL-GDC-RECORD-ID");
        }

        [Test]
        public void NullAndDuplicateRecordsFailWithOrderedTypedDiagnostics()
        {
            var skill = CatalogFixture.SkillArtifact(
                records: "[null,{\"id\":\"ember\",\"power\":5,\"tags\":[\"fire\"]},{\"id\":\"ember\",\"power\":6,\"tags\":[\"fire\"]}]");
            var first = CatalogFixture.Validate(skill);
            var second = CatalogFixture.Validate(skill);

            Assert.AreEqual(GameDataCatalogLoadStatus.InvalidRecord, first.Status);
            Assert.IsNull(first.Snapshot);
            CollectionAssert.AreEqual(
                first.Diagnostics.Select(item => item.Fingerprint).ToArray(),
                second.Diagnostics.Select(item => item.Fingerprint).ToArray());
            Assert.That(first.Diagnostics.Count, Is.LessThanOrEqualTo(GameDataCatalogContract.MaximumDiagnostics));
            CollectionAssert.Contains(first.Diagnostics.Select(item => item.Code).ToArray(), "AL-GDC-RECORD-ID-DUPLICATE");
            Assert.True(first.Diagnostics.All(item => item.BlocksCatalogSet));
        }

        [Test]
        public void DiagnosticLimitIsBoundedSentinelAndOrderingIsRepeatable()
        {
            var records = "[" + string.Join(",", Enumerable.Repeat("null", 300)) + "]";
            var artifact = CatalogFixture.SkillArtifact(records: records);
            var first = CatalogFixture.Validate(artifact);
            var second = CatalogFixture.Validate(artifact);

            Assert.AreEqual(GameDataCatalogContract.MaximumDiagnostics, first.Diagnostics.Count);
            Assert.AreEqual(1, first.Diagnostics.Count(item => item.Code == "AL-GDC-DIAGNOSTIC-LIMIT"));
            CollectionAssert.AreEqual(
                first.Diagnostics.Select(item => item.Fingerprint).ToArray(),
                second.Diagnostics.Select(item => item.Fingerprint).ToArray());
        }

        [Test]
        public void Sha256UsesExactRawBytesAndKnownLowercaseVector()
        {
            Assert.AreEqual(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                GameDataCatalogValidator.ComputeSha256(Encoding.ASCII.GetBytes("abc")));
            Assert.AreNotEqual(
                GameDataCatalogValidator.ComputeSha256(Encoding.UTF8.GetBytes("abc")),
                GameDataCatalogValidator.ComputeSha256(Encoding.UTF8.GetBytes("abc\n")));
        }

        [Test]
        public void ArtifactFailureCodesAreSanitizedBeforeDiagnostics()
        {
            var artifact = CatalogFixture.SkillArtifact();
            var manifestValidation = GameDataCatalogValidator.ValidateManifest(
                CatalogFixture.Manifest(artifact),
                CatalogFixture.Policy());
            Assert.True(manifestValidation.IsAccepted);
            var input = new GameDataCatalogArtifactInput(
                artifact.Path,
                GameDataCatalogReadStatus.ReadFailed,
                null,
                @"C:\Users\private\catalog.json");
            Assert.AreEqual("read_failed", input.FailureCode);

            var result = GameDataCatalogValidator.ValidateCatalogSet(
                manifestValidation.Manifest,
                new[] { input },
                CatalogFixture.Schemas(),
                CatalogFixture.Policy(),
                GameDataCatalogSourceKind.Packaged,
                new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 22, 0, 0, 1, TimeSpan.Zero));
            Assert.AreEqual(GameDataCatalogLoadStatus.ReadFailed, result.Status);
            Assert.That(result.Diagnostics.Single().TechnicalMessage, Does.Contain("read_failed"));
            Assert.That(result.Diagnostics.Single().TechnicalMessage, Does.Not.Contain("C:\\Users"));
        }

        [Test]
        public void AliasMetadataIsImmutableAndExactResolutionNeverNormalizes()
        {
            var skill = CatalogFixture.SkillArtifact(
                aliases: "[{\"legacyId\":\"Old Ember\",\"canonicalId\":\"ember\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]");
            var store = CatalogFixture.Load(CatalogFixture.Source(new[] { skill }));
            Assert.AreEqual(GameDataQueryStatus.AliasResolved, store.QueryRecord("skills", "Old Ember").Status);
            Assert.AreEqual(GameDataQueryStatus.UnknownId, store.QueryRecord("skills", "old ember").Status);
            Assert.AreEqual(GameDataQueryStatus.UnknownId, store.QueryRecord("skills", " Old Ember ").Status);

            var metadata = store.Snapshot.Families.Single().Aliases.Single();
            Assert.AreEqual("Old Ember", metadata.LegacyId);
            Assert.AreEqual("ember", metadata.CanonicalId);
            Assert.AreEqual(1, metadata.IntroducedVersion);
            Assert.IsNull(metadata.RetirementVersion);
            Assert.AreEqual("#183", metadata.MigrationIssue);
            var aliases = (IList)store.Snapshot.Families.Single().Aliases;
            Assert.True(aliases.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => aliases.Add(metadata));
        }

        [TestCase("shadow")]
        [TestCase("duplicate")]
        [TestCase("chain")]
        [TestCase("cycle")]
        [TestCase("missing")]
        public void AliasShadowDuplicateChainCycleAndMissingTargetFailClosed(string kind)
        {
            string records;
            string aliases;
            switch (kind)
            {
                case "shadow":
                    records = "[{\"id\":\"ember\",\"power\":5,\"tags\":[\"fire\"]},{\"id\":\"later\",\"power\":6,\"tags\":[\"ice\"]}]";
                    aliases = "[{\"legacyId\":\"later\",\"canonicalId\":\"ember\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]";
                    break;
                case "duplicate":
                    records = CatalogFixture.DefaultSkillRecords;
                    aliases = "[{\"legacyId\":\"old\",\"canonicalId\":\"ember\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"},{\"legacyId\":\"old\",\"canonicalId\":\"ember\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]";
                    break;
                case "chain":
                    records = CatalogFixture.DefaultSkillRecords;
                    aliases = "[{\"legacyId\":\"old_a\",\"canonicalId\":\"old_b\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"},{\"legacyId\":\"old_b\",\"canonicalId\":\"ember\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]";
                    break;
                case "cycle":
                    records = CatalogFixture.DefaultSkillRecords;
                    aliases = "[{\"legacyId\":\"old_a\",\"canonicalId\":\"old_b\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"},{\"legacyId\":\"old_b\",\"canonicalId\":\"old_a\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]";
                    break;
                default:
                    records = CatalogFixture.DefaultSkillRecords;
                    aliases = "[{\"legacyId\":\"old\",\"canonicalId\":\"absent\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]";
                    break;
            }

            var store = CatalogFixture.Load(CatalogFixture.Source(new[]
            {
                CatalogFixture.SkillArtifact(records: records, aliases: aliases)
            }));
            Assert.IsNull(store.Snapshot);
            Assert.That(store.LastLoadResult.Diagnostics.Any(item => item.Code.Contains("ALIAS")), Is.True);
        }

        [Test]
        public void CrossFamilyReferencesResolveAgainstCompleteCandidateAndMissingTargetBlocksSet()
        {
            var valid = CatalogFixture.Load(CatalogFixture.Source(CatalogFixture.ValidSet()));
            Assert.AreEqual(GameDataCatalogLifecycleStatus.Ready, valid.State.Status);

            var champion = CatalogFixture.ChampionArtifact(
                records: "[{\"id\":\"warden\",\"skill_id\":\"absent\",\"stats\":{\"level\":1,\"role\":\"tank\"}}]");
            var skill = CatalogFixture.SkillArtifact();
            var invalid = CatalogFixture.Load(CatalogFixture.Source(new[] { champion, skill }));
            Assert.IsNull(invalid.Snapshot);
            Assert.AreEqual(GameDataCatalogLoadStatus.CrossReferenceFailure, invalid.LastLoadResult.Status);
            var diagnostic = invalid.LastLoadResult.Diagnostics.Single(item => item.Code == "AL-GDC-REFERENCE-MISSING");
            Assert.AreEqual("champions", diagnostic.Family);
            Assert.AreEqual("warden", diagnostic.RecordId);
            Assert.AreEqual("$.records[0].skill_id", diagnostic.FieldPath);
        }

        [Test]
        public void FailedReloadPreservesSnapshotIdentityAndSuccessfulReloadSwapsRevisionAtomically()
        {
            var originalSet = CatalogFixture.ValidSet();
            var store = CatalogFixture.Load(CatalogFixture.Source(originalSet));
            var original = store.Snapshot;
            Assert.AreEqual(5d, CatalogFixture.Number(store.QueryRecord("skills", "ember"), "power"));

            var badSkill = CatalogFixture.SkillArtifact();
            var badSource = new InMemoryGameDataCatalogSource()
                .Add(CatalogFixture.ManifestPath, CatalogFixture.Manifest(badSkill))
                .Add(badSkill.Path, CatalogFixture.Bytes(CatalogFixture.Text(badSkill.Bytes).Replace("\"power\":5", "\"power\":6")));
            store.BeginLoad(CatalogFixture.Loader(), badSource, CatalogFixture.ManifestPath, GameDataCatalogSourceKind.Packaged);
            Assert.AreSame(original, store.Snapshot);
            Assert.AreEqual(1, store.Snapshot.Revision);
            Assert.AreEqual(GameDataCatalogLoadStatus.HashMismatch, store.LastLoadResult.Status);
            Assert.AreEqual(5d, CatalogFixture.Number(store.QueryRecord("skills", "ember"), "power"));

            var updatedSkill = CatalogFixture.SkillArtifact(
                contentVersion: "1.0.1",
                records: "[{\"id\":\"ember\",\"power\":9,\"tags\":[\"fire\"]}]");
            var updatedChampion = CatalogFixture.ChampionArtifact(
                contentVersion: "1.0.1",
                records: "[{\"id\":\"warden\",\"skill_id\":\"ember\",\"stats\":{\"level\":9,\"role\":\"tank\"}}]");
            var updatedSet = new[] { updatedChampion, updatedSkill };
            var deferred = new DeferredSource();
            store.BeginLoad(
                CatalogFixture.Loader(),
                deferred,
                CatalogFixture.ManifestPath,
                GameDataCatalogSourceKind.Packaged);

            Assert.AreSame(original, store.Snapshot);
            Assert.True(store.State.IsLoading);
            deferred.CompleteLate(
                0,
                GameDataCatalogReadStatus.Succeeded,
                CatalogFixture.Manifest(updatedSet, "1.0.1"));
            deferred.CompleteLate(1, GameDataCatalogReadStatus.Succeeded, updatedChampion.Bytes);
            Assert.AreSame(original, store.Snapshot, "A partial candidate must not mix with the published revision.");
            Assert.AreEqual(1, store.Snapshot.Revision);
            Assert.AreEqual(5d, CatalogFixture.Number(store.QueryRecord("skills", "ember"), "power"));
            Assert.True(store.Snapshot.Families.All(family => family.ContentVersion == "1.0.0"));

            deferred.CompleteLate(2, GameDataCatalogReadStatus.Succeeded, updatedSkill.Bytes);
            Assert.AreNotSame(original, store.Snapshot);
            Assert.AreEqual(2, store.Snapshot.Revision);
            Assert.AreEqual(9d, CatalogFixture.Number(store.QueryRecord("skills", "ember"), "power"));
            Assert.AreEqual("1.0.1", store.Snapshot.ContentVersion);
            Assert.True(store.Snapshot.Families.All(family => family.ContentVersion == "1.0.1"));
        }

        [Test]
        public void QueryLifecycleDistinguishesPendingUnavailableInvalidAndUnsupported()
        {
            var store = new GameDataCatalogStore();
            Assert.AreEqual(GameDataQueryStatus.CatalogPending, store.QueryRecord("skills", "ember").Status);

            var missingManifest = new InMemoryGameDataCatalogSource();
            store.BeginLoad(CatalogFixture.Loader(), missingManifest, CatalogFixture.ManifestPath, GameDataCatalogSourceKind.Packaged);
            Assert.AreEqual(GameDataQueryStatus.CatalogUnavailable, store.QueryRecord("skills", "ember").Status);

            var skill = CatalogFixture.SkillArtifact();
            var invalidManifest = CatalogFixture.Text(CatalogFixture.Manifest(skill)).Replace("\"another-life\"", "\"wrong\"");
            var invalidSource = new InMemoryGameDataCatalogSource()
                .Add(CatalogFixture.ManifestPath, CatalogFixture.Bytes(invalidManifest));
            store.BeginLoad(CatalogFixture.Loader(), invalidSource, CatalogFixture.ManifestPath, GameDataCatalogSourceKind.Packaged);
            Assert.AreEqual(GameDataQueryStatus.CatalogInvalid, store.QueryRecord("skills", "ember").Status);

            var unsupportedManifest = CatalogFixture.Text(CatalogFixture.Manifest(skill)).Replace("\"schemaVersion\":1", "\"schemaVersion\":2");
            var unsupportedSource = new InMemoryGameDataCatalogSource()
                .Add(CatalogFixture.ManifestPath, CatalogFixture.Bytes(unsupportedManifest));
            store.BeginLoad(CatalogFixture.Loader(), unsupportedSource, CatalogFixture.ManifestPath, GameDataCatalogSourceKind.Packaged);
            Assert.AreEqual(GameDataQueryStatus.UnsupportedVersion, store.QueryRecord("skills", "ember").Status);
        }

        [Test]
        public void DevelopmentFallbackIsNeverReportedAsPackagedReady()
        {
            var store = new GameDataCatalogStore();
            store.BeginLoad(
                CatalogFixture.Loader(),
                CatalogFixture.Source(CatalogFixture.ValidSet()),
                CatalogFixture.ManifestPath,
                GameDataCatalogSourceKind.DevelopmentFallback);
            Assert.AreEqual(GameDataCatalogLifecycleStatus.DevelopmentFallback, store.State.Status);
            Assert.AreEqual(GameDataCatalogLoadStatus.LoadedDevelopmentFallback, store.LastLoadResult.Status);
            Assert.That(store.LastLoadResult.Diagnostics.Count, Is.EqualTo(2));
            Assert.True(store.LastLoadResult.Diagnostics.All(item => item.Code == "AL-GDC-DEVELOPMENT-FALLBACK"));
        }

        [Test]
        public void UndefinedSourceKindCannotPublish()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CatalogFixture.Loader().BeginLoad(
                    CatalogFixture.Source(CatalogFixture.ValidSet()),
                    CatalogFixture.ManifestPath,
                    (GameDataCatalogSourceKind)999,
                    _ => { }));

            var store = CatalogFixture.Load(CatalogFixture.Source(CatalogFixture.ValidSet()));
            var accepted = store.Snapshot;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                store.BeginLoad(
                    CatalogFixture.Loader(),
                    CatalogFixture.Source(CatalogFixture.ValidSet()),
                    CatalogFixture.ManifestPath,
                    (GameDataCatalogSourceKind)999));
            Assert.AreSame(accepted, store.Snapshot);
            Assert.False(store.State.IsLoading, "Invalid startup arguments must not strand the store in Loading.");
        }

        [Test]
        public void DelegateSeamWaitsForQueuedCallbackAndTerminalClaimsRejectLateSuccess()
        {
            var request = new GameDataCatalogReadRequest(
                CatalogFixture.ManifestPath,
                GameDataCatalogContract.MaximumManifestBytes,
                TimeSpan.FromSeconds(5));
            Action<GameDataCatalogReadStatus, byte[], string> queuedCompletion = null;
            var queuedResults = new List<GameDataCatalogReadResult>();
            var queuedSource = new DelegateGameDataCatalogSource((_, completion) =>
            {
                queuedCompletion = completion;
                return new ImmediateReadOperation();
            });

            var queuedOperation = queuedSource.Read(request, queuedResults.Add);
            Assert.False(queuedOperation.IsCompleted, "A completed platform handle does not prove its queued callback is missing.");
            queuedCompletion(GameDataCatalogReadStatus.Succeeded, CatalogFixture.Bytes("{}"), string.Empty);
            Assert.True(queuedOperation.IsCompleted);
            Assert.AreEqual(GameDataCatalogReadStatus.Succeeded, queuedResults.Single().Status);
            queuedOperation.Dispose();

            Action<GameDataCatalogReadStatus, byte[], string> cancelledCompletion = null;
            var cancelledResults = new List<GameDataCatalogReadResult>();
            var cancelledHandle = new DeferredReadOperation();
            var cancelledSource = new DelegateGameDataCatalogSource((_, completion) =>
            {
                cancelledCompletion = completion;
                return cancelledHandle;
            });
            var cancelledOperation = cancelledSource.Read(request, cancelledResults.Add);
            cancelledOperation.Cancel();
            cancelledCompletion(GameDataCatalogReadStatus.Succeeded, CatalogFixture.Bytes("{}"), string.Empty);
            Assert.True(cancelledOperation.IsCancelled);
            Assert.AreEqual(1, cancelledHandle.CancelCount);
            Assert.AreEqual(GameDataCatalogReadStatus.Cancelled, cancelledResults.Single().Status);
            cancelledOperation.Dispose();
            Assert.AreEqual(1, cancelledHandle.DisposeCount);

            Action<GameDataCatalogReadStatus, byte[], string> disposedCompletion = null;
            var disposedResults = new List<GameDataCatalogReadResult>();
            var disposedHandle = new DeferredReadOperation();
            var disposedSource = new DelegateGameDataCatalogSource((_, completion) =>
            {
                disposedCompletion = completion;
                return disposedHandle;
            });
            var disposedOperation = disposedSource.Read(request, disposedResults.Add);
            disposedOperation.Dispose();
            disposedCompletion(GameDataCatalogReadStatus.Succeeded, CatalogFixture.Bytes("{}"), string.Empty);
            Assert.True(disposedOperation.IsCompleted);
            Assert.False(disposedOperation.IsCancelled);
            Assert.AreEqual(1, disposedHandle.DisposeCount);
            Assert.AreEqual(GameDataCatalogReadStatus.Disposed, disposedResults.Single().Status);
        }

        [Test]
        public void CancellationTimeoutDisposalAndLateCallbacksNeverPublish()
        {
            var set = CatalogFixture.ValidSet();
            var manifest = CatalogFixture.Manifest(set);

            var cancelSource = new DeferredSource();
            var cancelStore = new GameDataCatalogStore();
            cancelStore.BeginLoad(CatalogFixture.Loader(), cancelSource, CatalogFixture.ManifestPath, GameDataCatalogSourceKind.Packaged);
            Assert.AreEqual(GameDataQueryStatus.CatalogPending, cancelStore.QueryRecord("skills", "ember").Status);
            cancelStore.CancelActiveLoad();
            Assert.AreEqual(GameDataCatalogLoadStatus.Cancelled, cancelStore.LastLoadResult.Status);
            cancelSource.CompleteLate(0, GameDataCatalogReadStatus.Succeeded, manifest);
            Assert.IsNull(cancelStore.Snapshot);

            var clock = new ManualClock();
            var timeoutSource = new DeferredSource();
            var timeoutStore = new GameDataCatalogStore();
            timeoutStore.BeginLoad(
                CatalogFixture.Loader(clock, TimeSpan.FromSeconds(5)),
                timeoutSource,
                CatalogFixture.ManifestPath,
                GameDataCatalogSourceKind.Packaged);
            clock.Advance(TimeSpan.FromSeconds(6));
            timeoutStore.Tick();
            Assert.AreEqual(GameDataCatalogLoadStatus.TimedOut, timeoutStore.LastLoadResult.Status);
            timeoutSource.CompleteLate(0, GameDataCatalogReadStatus.Succeeded, manifest);
            Assert.IsNull(timeoutStore.Snapshot);

            var disposeSource = new DeferredSource();
            var disposeStore = new GameDataCatalogStore();
            disposeStore.BeginLoad(CatalogFixture.Loader(), disposeSource, CatalogFixture.ManifestPath, GameDataCatalogSourceKind.Packaged);
            disposeStore.Dispose();
            disposeSource.CompleteLate(0, GameDataCatalogReadStatus.Succeeded, manifest);
            Assert.AreEqual(GameDataCatalogLifecycleStatus.Disposed, disposeStore.State.Status);
            Assert.IsNull(disposeStore.Snapshot);
            Assert.AreEqual(GameDataQueryStatus.CatalogUnavailable, disposeStore.QueryRecord("skills", "ember").Status);
        }

        [Test]
        public void SupersededDeferredLoadCannotReplaceNewerAcceptedSnapshot()
        {
            var deferred = new DeferredSource();
            var store = new GameDataCatalogStore();
            store.BeginLoad(CatalogFixture.Loader(), deferred, CatalogFixture.ManifestPath, GameDataCatalogSourceKind.Packaged);

            var acceptedSet = CatalogFixture.ValidSet();
            store.BeginLoad(
                CatalogFixture.Loader(),
                CatalogFixture.Source(acceptedSet),
                CatalogFixture.ManifestPath,
                GameDataCatalogSourceKind.Packaged);
            var accepted = store.Snapshot;
            Assert.NotNull(accepted);
            deferred.CompleteLate(0, GameDataCatalogReadStatus.Succeeded, CatalogFixture.Manifest(acceptedSet));
            Assert.AreSame(accepted, store.Snapshot);
            Assert.AreEqual(1, store.Snapshot.Revision);
        }

        [Test]
        public void DirectFileAndDelegatePlatformSeamsPublishIdenticalFingerprints()
        {
            var set = CatalogFixture.ValidSet();
            var manifest = CatalogFixture.Manifest(set);
            var root = Path.Combine(Path.GetTempPath(), "al-gdc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllBytes(Path.Combine(root, CatalogFixture.ManifestPath), manifest);
                foreach (var artifact in set)
                {
                    var fullPath = Path.Combine(root, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    File.WriteAllBytes(fullPath, artifact.Bytes);
                }

                var directStore = CatalogFixture.Load(new DirectFileGameDataCatalogSource(root));
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    { CatalogFixture.ManifestPath, manifest }
                };
                foreach (var artifact in set) files.Add(artifact.Path, artifact.Bytes);

                var platform = new DelegateGameDataCatalogSource((request, completed) =>
                {
                    byte[] bytes;
                    if (files.TryGetValue(request.RelativePath, out bytes))
                    {
                        completed(GameDataCatalogReadStatus.Succeeded, bytes, string.Empty);
                    }
                    else
                    {
                        completed(GameDataCatalogReadStatus.NotFound, null, "not_found");
                    }
                    return new ImmediateReadOperation();
                });
                var platformStore = CatalogFixture.Load(platform);

                Assert.AreEqual(directStore.LastLoadResult.Status, platformStore.LastLoadResult.Status);
                Assert.AreEqual(directStore.Snapshot.ManifestSha256, platformStore.Snapshot.ManifestSha256);
                CollectionAssert.AreEqual(
                    directStore.Snapshot.Families.Select(item => item.Sha256).ToArray(),
                    platformStore.Snapshot.Families.Select(item => item.Sha256).ToArray());
                Assert.AreEqual(
                    CatalogFixture.Number(directStore.QueryRecord("skills", "ember"), "power"),
                    CatalogFixture.Number(platformStore.QueryRecord("skills", "ember"), "power"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void CatalogAssemblyHasNoUnityObjectAuthorityOrMutablePublicArrays()
        {
            var assembly = typeof(GameDataCatalogStore).Assembly;
            Assert.AreEqual("AL.GameDataCatalog", assembly.GetName().Name);
            foreach (var type in assembly.GetExportedTypes())
            {
                Assert.False(
                    type.GetProperties().Any(property => property.PropertyType.IsArray),
                    type.FullName + " exposes a mutable public array.");
                Assert.False(
                    type.GetProperties().Any(property => property.CanWrite),
                    type.FullName + " exposes a public property setter.");
                Assert.False(
                    type.GetFields().Any(field => field.FieldType.IsArray),
                    type.FullName + " exposes a mutable public array field.");
            }
        }

        private sealed class ManualClock : IGameDataCatalogClock
        {
            private long ticks;
            private readonly DateTimeOffset start = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);

            public long Timestamp => ticks;
            public DateTimeOffset UtcNow => start.AddTicks(ticks);
            public TimeSpan ElapsedSince(long timestamp) => TimeSpan.FromTicks(ticks - timestamp);

            public void Advance(TimeSpan amount)
            {
                ticks += amount.Ticks;
            }
        }

        private sealed class DeferredSource : IGameDataCatalogSource
        {
            private readonly List<Pending> reads = new List<Pending>();

            public IGameDataCatalogReadOperation Read(
                GameDataCatalogReadRequest request,
                Action<GameDataCatalogReadResult> completed)
            {
                var operation = new DeferredReadOperation();
                reads.Add(new Pending(request, completed, operation));
                return operation;
            }

            public void CompleteLate(int index, GameDataCatalogReadStatus status, byte[] bytes)
            {
                var pending = reads[index];
                pending.Completed(new GameDataCatalogReadResult(
                    status,
                    pending.Request.RelativePath,
                    bytes,
                    status == GameDataCatalogReadStatus.Succeeded ? string.Empty : "fixture_failure"));
            }

            private sealed class Pending
            {
                public Pending(
                    GameDataCatalogReadRequest request,
                    Action<GameDataCatalogReadResult> completed,
                    DeferredReadOperation operation)
                {
                    Request = request;
                    Completed = completed;
                    Operation = operation;
                }

                public GameDataCatalogReadRequest Request { get; }
                public Action<GameDataCatalogReadResult> Completed { get; }
                public DeferredReadOperation Operation { get; }
            }
        }

        private sealed class DeferredReadOperation : IGameDataCatalogReadOperation
        {
            public bool IsCompleted { get; private set; }
            public bool IsCancelled { get; private set; }
            public int CancelCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Cancel()
            {
                CancelCount++;
                IsCancelled = true;
                IsCompleted = true;
            }

            public void Dispose()
            {
                DisposeCount++;
                IsCompleted = true;
            }
        }

        private sealed class ImmediateReadOperation : IGameDataCatalogReadOperation
        {
            public bool IsCompleted => true;
            public bool IsCancelled => false;
            public void Cancel() { }
            public void Dispose() { }
        }
    }

    internal static class CatalogFixture
    {
        internal const string ManifestPath = "catalog-set.json";
        internal const string DefaultSkillRecords = "[{\"id\":\"ember\",\"power\":5,\"tags\":[\"fire\"]}]";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static GameDataCatalogValidationPolicy Policy()
        {
            return new GameDataCatalogValidationPolicy(GameDataCatalogContract.DefaultGameId);
        }

        internal static GameDataCatalogSchemaRegistry Schemas()
        {
            var skillSchema = new GameDataCatalogFamilySchema(
                "skills",
                new[] { 1 },
                new[]
                {
                    new GameDataCatalogFieldRule(
                        "power",
                        GameDataValueKind.Number,
                        true,
                        integerOnly: true,
                        minimumNumber: 0,
                        maximumNumber: 100),
                    new GameDataCatalogFieldRule(
                        "tags",
                        GameDataValueKind.Array,
                        true,
                        minimumItems: 1,
                        maximumItems: 8,
                        itemRule: new GameDataCatalogFieldRule(
                            "$item",
                            GameDataValueKind.String,
                            true,
                            nonBlank: true,
                            stableId: true))
                });
            var championSchema = new GameDataCatalogFamilySchema(
                "champions",
                new[] { 1 },
                new[]
                {
                    new GameDataCatalogFieldRule(
                        "skill_id",
                        GameDataValueKind.String,
                        true,
                        nonBlank: true,
                        stableId: true,
                        referenceFamily: "skills"),
                    new GameDataCatalogFieldRule(
                        "stats",
                        GameDataValueKind.Object,
                        true,
                        objectFields: new[]
                        {
                            new GameDataCatalogFieldRule(
                                "level",
                                GameDataValueKind.Number,
                                true,
                                integerOnly: true,
                                minimumNumber: 1,
                                maximumNumber: 100),
                            new GameDataCatalogFieldRule(
                                "role",
                                GameDataValueKind.String,
                                true,
                                nonBlank: true,
                                allowedStringValues: new[] { "tank", "striker" })
                        })
                });
            var cosmeticsSchema = new GameDataCatalogFamilySchema(
                "cosmetics",
                new[] { 1 },
                new[]
                {
                    new GameDataCatalogFieldRule("asset_id", GameDataValueKind.String, true, nonBlank: true, stableId: true)
                },
                allowEmptyRecords: true);
            return new GameDataCatalogSchemaRegistry(new[] { skillSchema, championSchema, cosmeticsSchema });
        }

        internal static GameDataCatalogLoader Loader(
            IGameDataCatalogClock clock = null,
            TimeSpan? timeout = null)
        {
            return new GameDataCatalogLoader(Policy(), Schemas(), clock, timeout);
        }

        internal static Artifact SkillArtifact(
            bool required = true,
            string contentVersion = "1.0.0",
            string records = null,
            string aliases = null,
            string extraRoot = "")
        {
            return FamilyArtifact(
                "skills",
                "skills_v1",
                "Catalogs/skills.v1.json",
                required,
                contentVersion,
                records ?? DefaultSkillRecords,
                aliases ?? "[{\"legacyId\":\"Old Ember\",\"canonicalId\":\"ember\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#183\"}]",
                extraRoot);
        }

        internal static Artifact ChampionArtifact(
            bool required = true,
            string contentVersion = "1.0.0",
            string records = null,
            string aliases = "[]",
            string extraRoot = "")
        {
            return FamilyArtifact(
                "champions",
                "champions_v1",
                "Catalogs/champions.v1.json",
                required,
                contentVersion,
                records ?? "[{\"id\":\"warden\",\"skill_id\":\"ember\",\"stats\":{\"level\":1,\"role\":\"tank\"}}]",
                aliases,
                extraRoot);
        }

        internal static Artifact CosmeticsArtifact(bool required)
        {
            return FamilyArtifact(
                "cosmetics",
                "cosmetics_v1",
                "Catalogs/cosmetics.v1.json",
                required,
                "1.0.0",
                "[]",
                "[]",
                string.Empty);
        }

        internal static Artifact[] ValidSet()
        {
            return new[] { ChampionArtifact(), SkillArtifact() };
        }

        internal static Artifact FamilyArtifact(
            string family,
            string catalogId,
            string path,
            bool required,
            string contentVersion,
            string records,
            string aliases,
            string extraRoot)
        {
            var json =
                "{\n" +
                "  \"gameId\":\"another-life\",\n" +
                "  \"catalogId\":\"" + catalogId + "\",\n" +
                "  \"family\":\"" + family + "\",\n" +
                "  \"schemaVersion\":1,\n" +
                "  \"contentVersion\":\"" + contentVersion + "\",\n" +
                "  \"sourceRevision\":\"test-revision\",\n" +
                "  \"records\":" + records + ",\n" +
                "  \"aliases\":" + aliases + extraRoot + "\n" +
                "}\n";
            return new Artifact(family, catalogId, path, required, contentVersion, Bytes(json));
        }

        internal static byte[] Manifest(IEnumerable<Artifact> artifacts, string contentVersion = "1.0.0")
        {
            var rows = artifacts.Select(artifact =>
                "    {\"family\":\"" + artifact.Family +
                "\",\"catalogId\":\"" + artifact.CatalogId +
                "\",\"relativePath\":\"" + artifact.Path.Replace("\\", "\\\\") +
                "\",\"schemaVersion\":1,\"contentVersion\":\"" + artifact.ContentVersion +
                "\",\"required\":" + (artifact.Required ? "true" : "false") +
                ",\"sha256\":\"" + artifact.Sha256 +
                "\",\"mediaType\":\"application/json\",\"sourceMode\":\"authored\",\"sourceRevision\":\"test-revision\"}");
            var json =
                "{\n" +
                "  \"gameId\":\"another-life\",\n" +
                "  \"catalogSetId\":\"catalog_set_test\",\n" +
                "  \"schemaVersion\":1,\n" +
                "  \"contentVersion\":\"" + contentVersion + "\",\n" +
                "  \"minimumRuntimeCatalogVersion\":1,\n" +
                "  \"sourceRevision\":\"test-revision\",\n" +
                "  \"artifacts\":[\n" + string.Join(",\n", rows) + "\n  ]\n" +
                "}\n";
            return Bytes(json);
        }

        internal static byte[] Manifest(params Artifact[] artifacts)
        {
            return Manifest((IEnumerable<Artifact>)artifacts);
        }

        internal static InMemoryGameDataCatalogSource Source(
            IEnumerable<Artifact> artifacts,
            string manifestContentVersion = "1.0.0")
        {
            var materialized = artifacts.ToArray();
            var source = new InMemoryGameDataCatalogSource()
                .Add(ManifestPath, Manifest(materialized, manifestContentVersion));
            foreach (var artifact in materialized) source.Add(artifact.Path, artifact.Bytes);
            return source;
        }

        internal static Artifact MutateArtifact(Artifact artifact, string oldValue, string newValue)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            var original = Text(artifact.Bytes);
            var changed = original.Replace(oldValue, newValue);
            Assert.AreNotEqual(original, changed, "The requested artifact mutation must change exact bytes.");
            return new Artifact(
                artifact.Family,
                artifact.CatalogId,
                artifact.Path,
                artifact.Required,
                artifact.ContentVersion,
                Bytes(changed));
        }

        internal static GameDataCatalogStore Load(IGameDataCatalogSource source)
        {
            var store = new GameDataCatalogStore();
            var operation = store.BeginLoad(Loader(), source, ManifestPath, GameDataCatalogSourceKind.Packaged);
            Assert.True(
                SpinWait.SpinUntil(() => operation.IsCompleted, 5000),
                "The bounded fixture source should complete within five seconds.");
            return store;
        }

        internal static GameDataCatalogLoadResult Validate(Artifact artifact)
        {
            var manifestBytes = Manifest(artifact);
            var manifest = GameDataCatalogValidator.ValidateManifest(manifestBytes, Policy());
            Assert.True(manifest.IsAccepted, string.Join("\n", manifest.Diagnostics.Select(item => item.Fingerprint)));
            return GameDataCatalogValidator.ValidateCatalogSet(
                manifest.Manifest,
                new[]
                {
                    new GameDataCatalogArtifactInput(
                        artifact.Path,
                        GameDataCatalogReadStatus.Succeeded,
                        artifact.Bytes,
                        string.Empty)
                },
                Schemas(),
                Policy(),
                GameDataCatalogSourceKind.Packaged,
                new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 22, 0, 0, 1, TimeSpan.Zero));
        }

        internal static double Number(GameDataCatalogQueryResult query, string field)
        {
            Assert.True(query.HasRecord);
            GameDataValue value;
            Assert.True(query.Record.TryGetField(field, out value));
            return ((GameDataNumberValue)value).Value;
        }

        internal static byte[] Bytes(string value)
        {
            return Utf8.GetBytes(value);
        }

        internal static string Text(byte[] value)
        {
            return Utf8.GetString(value);
        }

        internal sealed class Artifact
        {
            public Artifact(
                string family,
                string catalogId,
                string path,
                bool required,
                string contentVersion,
                byte[] bytes)
            {
                Family = family;
                CatalogId = catalogId;
                Path = path;
                Required = required;
                ContentVersion = contentVersion;
                Bytes = (byte[])bytes.Clone();
                Sha256 = GameDataCatalogValidator.ComputeSha256(Bytes);
            }

            public string Family { get; }
            public string CatalogId { get; }
            public string Path { get; }
            public bool Required { get; }
            public string ContentVersion { get; }
            public byte[] Bytes { get; }
            public string Sha256 { get; }
        }
    }
}
