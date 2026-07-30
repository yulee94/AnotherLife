using System;
using System.IO;
using System.Linq;
using System.Text;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.GameDataCatalog
{
    public sealed class RealmShadowArtifactTests
    {
        private const string CatalogId = "realms_phase_c9a_shadow_v1";
        private const string ContentVersion = "0.1.0-shadow.1";
        private const string SourceRevision =
            "game-data-phase-c-six-family-technical-source-2026-07-29-v003";
        private const string ArtifactRelativePath =
            "PhaseC/Shadow/realm-family-shadow-v001.json";
        private const string ArtifactSha256 =
            "265160f0c20b10293a69572fbcc4703ad81add498b20dfb727c353e050b0eccb";

        [Test]
        public void CommittedArtifactValidatesThroughCommonRealmSchemaWithoutAliases()
        {
            var artifactBytes = ReadArtifactBytes();
            Assert.AreEqual(
                ArtifactSha256,
                GameDataCatalogValidator.ComputeSha256(artifactBytes));

            var result = Validate(artifactBytes);

            Assert.AreEqual(
                GameDataCatalogLoadStatus.LoadedPackaged,
                result.Status,
                Diagnostics(result));
            Assert.NotNull(result.Snapshot);
            Assert.AreEqual(1, result.Snapshot.Families.Count);

            var family = result.Snapshot.Families[0];
            Assert.AreEqual("realms", family.Family);
            Assert.AreEqual(CatalogId, family.CatalogId);
            Assert.AreEqual(ContentVersion, family.ContentVersion);
            Assert.AreEqual(SourceRevision, family.SourceRevision);
            Assert.AreEqual(4, family.Records.Count);
            Assert.AreEqual(0, family.Aliases.Count);

            foreach (var reference in GameDataRealmReferences.Entries)
            {
                var query = result.Snapshot.QueryRecord(
                    "realms",
                    reference.StableId);
                Assert.AreEqual(
                    GameDataQueryStatus.Found,
                    query.Status,
                    reference.StableId);
                Assert.AreEqual(
                    reference.LegacyRealmName,
                    StringField(query.Record, "legacy_realm_id"));
                Assert.AreEqual(
                    reference.LegacyRealmValue,
                    IntegerField(query.Record, "legacy_realm_value"));
                Assert.AreEqual(
                    reference.NameReference,
                    StringField(query.Record, "name_ref"));
                Assert.AreEqual(
                    reference.DescriptionReference,
                    StringField(query.Record, "description_ref"));
                Assert.AreEqual(
                    reference.InnerRealmId,
                    StringField(query.Record, "inner_realm_id"));
                Assert.AreEqual(
                    reference.MainGateId,
                    StringField(query.Record, "main_gate_id"));
                Assert.AreEqual(
                    reference.OuterWarzoneId,
                    StringField(query.Record, "outer_warzone_id"));
                Assert.AreEqual(
                    reference.RareResourceStableId,
                    StringField(query.Record, "rare_resource_id"));
                Assert.AreEqual(
                    reference.AssetReference,
                    StringField(query.Record, "asset_ref"));

                GameDataRealmCapabilityProfile profile;
                Assert.True(
                    GameDataRealmCapabilityProfiles.TryGetByRealmStableId(
                        reference.StableId,
                        out profile),
                    reference.StableId);
                CollectionAssert.AreEqual(
                    new[] { profile.StableId },
                    StringArrayField(query.Record, "capability_profile_ids"),
                    reference.StableId);
            }

            foreach (var invalidId in new[]
                     {
                         string.Empty,
                         "Crownlands",
                         " crownlands",
                         "crownlands ",
                         "unknown_realm"
                     })
            {
                Assert.AreEqual(
                    GameDataQueryStatus.UnknownId,
                    result.Snapshot.QueryRecord("realms", invalidId).Status,
                    invalidId);
            }
        }

        [Test]
        public void CrossRealmMutationsProduceStableOrderedDiagnostics()
        {
            var source = Encoding.UTF8.GetString(ReadArtifactBytes());
            var changed = source
                .Replace(
                    "\"rare_resource_id\": \"royal_sigil\"",
                    "\"rare_resource_id\": \"deep_ore\"")
                .Replace(
                    "\"battle_realm_crownlands\"",
                    "\"battle_realm_stonehold\"")
                .Replace(
                    "S_ArcaneAxis_Crownlands_Flat_256_v001.png",
                    "S_ArcaneAxis_Stonehold_Flat_256_v001.png");
            Assert.AreNotEqual(source, changed);

            var result = Validate(Encoding.UTF8.GetBytes(changed));

            Assert.AreEqual(GameDataCatalogLoadStatus.InvalidRecord, result.Status);
            Assert.IsNull(result.Snapshot);
            CollectionAssert.AreEqual(
                new[]
                {
                    "AL-GDC-REALM-WORLD-ASSET-REFERENCE",
                    "AL-GDC-REALM-CAPABILITY-PROFILE-REFERENCE",
                    "AL-GDC-REALM-RARE-RESOURCE-REFERENCE"
                },
                result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                Diagnostics(result));
            CollectionAssert.AreEqual(
                new[]
                {
                    "$.records[0].asset_ref",
                    "$.records[0].capability_profile_ids",
                    "$.records[0].rare_resource_id"
                },
                result.Diagnostics
                    .Select(diagnostic => diagnostic.FieldPath)
                    .ToArray());
        }

        private static GameDataCatalogLoadResult Validate(byte[] artifactBytes)
        {
            var policy = new GameDataCatalogValidationPolicy(
                GameDataCatalogContract.DefaultGameId);
            var manifestResult = GameDataCatalogValidator.ValidateManifest(
                ManifestBytes(artifactBytes),
                policy);
            Assert.True(
                manifestResult.IsAccepted,
                string.Join(
                    "\n",
                    manifestResult.Diagnostics.Select(
                        diagnostic => diagnostic.Fingerprint)));

            return GameDataCatalogValidator.ValidateCatalogSet(
                manifestResult.Manifest,
                new[]
                {
                    new GameDataCatalogArtifactInput(
                        ArtifactRelativePath,
                        GameDataCatalogReadStatus.Succeeded,
                        artifactBytes,
                        string.Empty)
                },
                GameDataSixFamilySchemas.CreateRegistry(),
                policy,
                GameDataCatalogSourceKind.Packaged,
                new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 30, 0, 0, 1, TimeSpan.Zero));
        }

        private static byte[] ManifestBytes(byte[] artifactBytes)
        {
            var hash = GameDataCatalogValidator.ComputeSha256(artifactBytes);
            var json =
                "{\n" +
                "  \"gameId\":\"another-life\",\n" +
                "  \"catalogSetId\":\"realm_shadow_validation_set\",\n" +
                "  \"schemaVersion\":1,\n" +
                "  \"contentVersion\":\"" + ContentVersion + "\",\n" +
                "  \"minimumRuntimeCatalogVersion\":1,\n" +
                "  \"sourceRevision\":\"" + SourceRevision + "\",\n" +
                "  \"artifacts\":[\n" +
                "    {\"family\":\"realms\",\"catalogId\":\"" + CatalogId +
                "\",\"relativePath\":\"" + ArtifactRelativePath +
                "\",\"schemaVersion\":1,\"contentVersion\":\"" +
                ContentVersion +
                "\",\"required\":true,\"sha256\":\"" + hash +
                "\",\"mediaType\":\"application/json\",\"sourceMode\":" +
                "\"generated\",\"sourceRevision\":\"" + SourceRevision + "\"}\n" +
                "  ]\n" +
                "}\n";
            return Encoding.UTF8.GetBytes(json);
        }

        private static byte[] ReadArtifactBytes()
        {
            var artifactPath = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "GameDataCatalog",
                    ArtifactRelativePath));
            Assert.True(File.Exists(artifactPath), artifactPath);
            return File.ReadAllBytes(artifactPath);
        }

        private static string StringField(
            GameDataCatalogRecord record,
            string fieldName)
        {
            GameDataValue value;
            Assert.True(record.TryGetField(fieldName, out value), fieldName);
            var stringValue = value as GameDataStringValue;
            Assert.NotNull(stringValue, fieldName);
            return stringValue.Value;
        }

        private static long IntegerField(
            GameDataCatalogRecord record,
            string fieldName)
        {
            GameDataValue value;
            Assert.True(record.TryGetField(fieldName, out value), fieldName);
            var numberValue = value as GameDataNumberValue;
            Assert.NotNull(numberValue, fieldName);
            long result;
            Assert.True(numberValue.TryGetInt64(out result), fieldName);
            return result;
        }

        private static string[] StringArrayField(
            GameDataCatalogRecord record,
            string fieldName)
        {
            GameDataValue value;
            Assert.True(record.TryGetField(fieldName, out value), fieldName);
            var arrayValue = value as GameDataArrayValue;
            Assert.NotNull(arrayValue, fieldName);
            return arrayValue.Items
                .Select(item => ((GameDataStringValue)item).Value)
                .ToArray();
        }

        private static string Diagnostics(GameDataCatalogLoadResult result)
        {
            return string.Join(
                "\n",
                result.Diagnostics.Select(
                    diagnostic => diagnostic.Fingerprint));
        }
    }
}
