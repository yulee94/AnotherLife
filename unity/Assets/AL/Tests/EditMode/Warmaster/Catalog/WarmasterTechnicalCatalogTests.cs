using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using AL.Data.Catalogs;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Warmaster.Catalog
{
    public sealed class WarmasterTechnicalCatalogTests
    {
        private const string SetId = "prototype_true_warmaster";

        [Test]
        public void PackagedCatalogPublishesOneSetAndTenPieces()
        {
            WarmasterTechnicalCatalogLoadResult result = LoadPackaged();

            Assert.That(result.Status, Is.EqualTo(GameDataCatalogLoadStatus.LoadedPackaged));
            Assert.That(result.Catalog, Is.Not.Null);
            Assert.That(result.Catalog.Sets, Has.Count.EqualTo(1));
            Assert.That(result.Catalog.Pieces, Has.Count.EqualTo(10));

            var resolver = new WarmasterTechnicalCatalogResolver(result);
            WarmasterTechnicalCatalogQueryResult set = resolver.Resolve(SetId);
            Assert.That(set.Status, Is.EqualTo(WarmasterTechnicalCatalogQueryStatus.Found));
            Assert.That(set.Kind, Is.EqualTo(WarmasterTechnicalDefinitionKind.Set));
            Assert.That(set.Set, Is.SameAs(result.Catalog.Sets[0]));
        }

        [Test]
        public void UnapprovedContentReferenceFailsClosed()
        {
            byte[] artifact = ReadPackagedBytes(ArtifactPath);
            byte[] changed = Utf8.GetBytes(Utf8.GetString(artifact).Replace(
                "warmaster.piece.oathbound_helm",
                "warmaster.piece.unapproved"));

            WarmasterTechnicalCatalogLoadResult result = Load(
                SourceWith(changed, ManifestFor(changed)));

            Assert.That(result.Status, Is.EqualTo(GameDataCatalogLoadStatus.InvalidRecord));
            Assert.That(result.Catalog, Is.Null);

            byte[] wrongApprovedMapping = Utf8.GetBytes(Utf8.GetString(artifact).Replace(
                "warmaster.piece.oathbound_helm.name",
                "warmaster.piece.vanguard_pauldrons.name"));
            WarmasterTechnicalCatalogLoadResult mappingResult = Load(
                SourceWith(wrongApprovedMapping, ManifestFor(wrongApprovedMapping)));
            Assert.That(mappingResult.Status, Is.EqualTo(GameDataCatalogLoadStatus.InvalidRecord));
            Assert.That(mappingResult.Catalog, Is.Null);
        }

        [Test]
        public void MissingManifestAndRequiredArtifactFailClosed()
        {
            WarmasterTechnicalCatalogLoadResult missingManifest = Load(
                new ImmediateCatalogSource());
            Assert.That(
                missingManifest.Status,
                Is.EqualTo(GameDataCatalogLoadStatus.MissingManifest));
            Assert.That(missingManifest.Catalog, Is.Null);

            var missingArtifactSource = new ImmediateCatalogSource().Add(
                WarmasterTechnicalCatalogContract.ManifestRelativePath,
                ReadPackagedBytes(WarmasterTechnicalCatalogContract.ManifestRelativePath));
            WarmasterTechnicalCatalogLoadResult missingArtifact = Load(missingArtifactSource);
            Assert.That(
                missingArtifact.Status,
                Is.EqualTo(GameDataCatalogLoadStatus.MissingArtifact));
            Assert.That(missingArtifact.Catalog, Is.Null);
        }

        [Test]
        public void RawHashMismatchFailsClosed()
        {
            byte[] changed = Utf8.GetBytes(Utf8.GetString(ReadPackagedBytes(ArtifactPath)).Replace(
                "warmaster.piece.oathbound_helm",
                "warmaster.piece.unapproved"));
            WarmasterTechnicalCatalogLoadResult result = Load(SourceWith(
                changed,
                ReadPackagedBytes(WarmasterTechnicalCatalogContract.ManifestRelativePath)));

            Assert.That(result.Status, Is.EqualTo(GameDataCatalogLoadStatus.HashMismatch));
            Assert.That(result.Catalog, Is.Null);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Does.Contain("AL-GDC-HASH-MISMATCH"));
        }

        [Test]
        public void UnsupportedVersionAndUnknownFieldsFailClosed()
        {
            byte[] unsupported = Utf8.GetBytes(Utf8.GetString(ReadPackagedBytes(ArtifactPath)).Replace(
                "\"schemaVersion\": 1",
                "\"schemaVersion\": 2"));
            string unsupportedManifest = Utf8.GetString(ManifestFor(unsupported)).Replace(
                "\"relativePath\": \"" + ArtifactPath + "\",\n      \"schemaVersion\": 1",
                "\"relativePath\": \"" + ArtifactPath + "\",\n      \"schemaVersion\": 2");
            WarmasterTechnicalCatalogLoadResult versionResult = Load(SourceWith(
                unsupported,
                Utf8.GetBytes(unsupportedManifest)));
            Assert.That(
                versionResult.Status,
                Is.EqualTo(GameDataCatalogLoadStatus.UnsupportedVersion));
            Assert.That(versionResult.Catalog, Is.Null);

            byte[] unknownField = Utf8.GetBytes(Utf8.GetString(ReadPackagedBytes(ArtifactPath)).Replace(
                "\"kind\": \"set\",",
                "\"kind\": \"set\",\n      \"price\": 1,"));
            WarmasterTechnicalCatalogLoadResult fieldResult = Load(SourceWith(
                unknownField,
                ManifestFor(unknownField)));
            Assert.That(fieldResult.Status, Is.EqualTo(GameDataCatalogLoadStatus.InvalidRecord));
            Assert.That(fieldResult.Catalog, Is.Null);
        }

        [Test]
        public void DuplicateRecordAndMembershipDriftFailClosed()
        {
            byte[] duplicateRecord = Utf8.GetBytes(Utf8.GetString(ReadPackagedBytes(ArtifactPath)).Replace(
                "\"id\": \"warmaster_piece_10\"",
                "\"id\": \"warmaster_piece_09\""));
            WarmasterTechnicalCatalogLoadResult duplicateResult = Load(SourceWith(
                duplicateRecord,
                ManifestFor(duplicateRecord)));
            Assert.That(
                duplicateResult.Status,
                Is.EqualTo(GameDataCatalogLoadStatus.InvalidRecord));
            Assert.That(duplicateResult.Catalog, Is.Null);

            byte[] duplicateMembership = Utf8.GetBytes(Utf8.GetString(ReadPackagedBytes(ArtifactPath)).Replace(
                "        \"warmaster_piece_09\",\n        \"warmaster_piece_10\"\n      ]",
                "        \"warmaster_piece_09\",\n        \"warmaster_piece_09\"\n      ]"));
            WarmasterTechnicalCatalogLoadResult membershipResult = Load(SourceWith(
                duplicateMembership,
                ManifestFor(duplicateMembership)));
            Assert.That(
                membershipResult.Status,
                Is.EqualTo(GameDataCatalogLoadStatus.InvalidRecord));
            Assert.That(membershipResult.Catalog, Is.Null);
        }

        [Test]
        public void AliasRowsFailClosed()
        {
            byte[] changed = Utf8.GetBytes(Utf8.GetString(ReadPackagedBytes(ArtifactPath)).Replace(
                "\"aliases\": []",
                "\"aliases\": [{\"legacyId\":\"Old Warmaster\",\"canonicalId\":\"prototype_true_warmaster\",\"introducedVersion\":1,\"retirementVersion\":null,\"migrationIssue\":\"#171\"}]"));

            WarmasterTechnicalCatalogLoadResult result = Load(SourceWith(
                changed,
                ManifestFor(changed)));

            Assert.That(result.Status, Is.EqualTo(GameDataCatalogLoadStatus.InvalidRecord));
            Assert.That(result.Catalog, Is.Null);
        }

        [Test]
        public void ResolverReturnsKnownPieceAndExplicitUnknownOutcomes()
        {
            WarmasterTechnicalCatalogLoadResult ready = LoadPackaged();
            var resolver = new WarmasterTechnicalCatalogResolver(ready);

            WarmasterTechnicalCatalogQueryResult piece = resolver.Resolve("warmaster_piece_01");
            Assert.That(piece.Status, Is.EqualTo(WarmasterTechnicalCatalogQueryStatus.Found));
            Assert.That(piece.Kind, Is.EqualTo(WarmasterTechnicalDefinitionKind.Piece));
            Assert.That(piece.Piece.Id, Is.EqualTo("warmaster_piece_01"));
            Assert.That(piece.Set, Is.Null);

            WarmasterTechnicalCatalogQueryResult unknown = resolver.Resolve("warmaster_piece_11");
            Assert.That(unknown.Status,
                Is.EqualTo(WarmasterTechnicalCatalogQueryStatus.UnknownDefinition));
            Assert.That(unknown.Kind, Is.EqualTo(WarmasterTechnicalDefinitionKind.None));
            Assert.That(unknown.Set, Is.Null);
            Assert.That(unknown.Piece, Is.Null);
            Assert.That(resolver.Resolve(null).Status,
                Is.EqualTo(WarmasterTechnicalCatalogQueryStatus.UnknownDefinition));
            Assert.That(resolver.Resolve(string.Empty).Status,
                Is.EqualTo(WarmasterTechnicalCatalogQueryStatus.UnknownDefinition));

            WarmasterTechnicalCatalogLoadResult unavailable = Load(new ImmediateCatalogSource());
            WarmasterTechnicalCatalogQueryResult unavailableQuery =
                new WarmasterTechnicalCatalogResolver(unavailable).Resolve("warmaster_piece_01");
            Assert.That(unavailableQuery.Status,
                Is.EqualTo(WarmasterTechnicalCatalogQueryStatus.CatalogUnavailable));
        }

        [Test]
        public void SnapshotDefinitionsAndCollectionsAreImmutable()
        {
            WarmasterTechnicalCatalogSnapshot catalog = LoadPackaged().Catalog;

            Assert.Throws<NotSupportedException>(() =>
                ((IList<WarmasterTechnicalPieceDefinition>)catalog.Pieces).RemoveAt(0));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)catalog.Sets[0].PieceIds).RemoveAt(0));
            Assert.That(
                typeof(WarmasterTechnicalCatalogSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(property => property.CanWrite),
                Is.False);
            Assert.That(
                typeof(WarmasterTechnicalPieceDefinition).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Any(property => property.CanWrite),
                Is.False);
        }

        [Test]
        public void PackagedReferencesMatchApprovedContentAndContainNoBalanceAuthority()
        {
            WarmasterTechnicalCatalogSnapshot catalog = LoadPackaged().Catalog;
            string content = Utf8.GetString(ReadPackagedBytes("al_warmaster_content_catalog.json"));
            foreach (WarmasterTechnicalSetDefinition definition in catalog.Sets)
            {
                Assert.That(content, Does.Contain(
                    "\"displayNameKey\": \"" + definition.NameReference + "\""));
                Assert.That(content, Does.Contain(
                    "\"summaryKey\": \"" + definition.SummaryReference + "\""));
            }
            foreach (WarmasterTechnicalPieceDefinition definition in catalog.Pieces)
            {
                Assert.That(content, Does.Contain(
                    "\"displayNameKey\": \"" + definition.NameReference + "\""));
                Assert.That(content, Does.Contain(
                    "\"summaryKey\": \"" + definition.SummaryReference + "\""));
            }

            string technical = Utf8.GetString(ReadPackagedBytes(ArtifactPath)).ToLowerInvariant();
            foreach (string forbidden in new[]
                     {
                         "price", "cost", "currency", "entitlement", "progression",
                         "unlock_policy", "equip_policy", "asset_ref", "prefab"
                     })
            {
                Assert.That(technical, Does.Not.Contain(forbidden));
            }
        }

        [Test]
        public void ManifestPinsArtifactHashAndProductionServiceRemainsUnwired()
        {
            byte[] artifact = ReadPackagedBytes(ArtifactPath);
            string expectedHash = GameDataCatalogValidator.ComputeSha256(artifact);
            string manifest = Utf8.GetString(ReadPackagedBytes(
                WarmasterTechnicalCatalogContract.ManifestRelativePath));
            WarmasterTechnicalCatalogSnapshot catalog = LoadPackaged().Catalog;

            Assert.That(manifest, Does.Contain("\"sha256\": \"" + expectedHash + "\""));
            Assert.That(catalog.CatalogSha256, Is.EqualTo(expectedHash));
            Assert.That(catalog.CatalogId, Is.EqualTo("warmaster_technical_v1"));
            Assert.That(catalog.CatalogSetId, Is.EqualTo("warmaster_technical_set_v1"));
            Assert.That(
                catalog.SourceRevision,
                Is.EqualTo("12576d06b27559e92203eba5b6253d1dba19a2ea"));

            string localService = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Services",
                "Local",
                "LocalWarmasterService.cs"));
            Assert.That(localService, Does.Not.Contain("WarmasterTechnicalCatalog"));
        }

        private static WarmasterTechnicalCatalogLoadResult LoadPackaged()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData"));
            var source = new DirectFileGameDataCatalogSource(root);
            var loader = new WarmasterTechnicalCatalogLoader();
            WarmasterTechnicalCatalogLoadResult result = null;
            using (IGameDataCatalogLoadOperation operation = loader.BeginLoad(
                       source,
                       GameDataCatalogSourceKind.Packaged,
                       value => result = value))
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                while ((!operation.IsCompleted || result == null) &&
                       DateTime.UtcNow < deadline)
                {
                    operation.Tick();
                    Thread.Sleep(1);
                }

                Assert.That(operation.IsCompleted, Is.True, "Warmaster catalog load timed out.");
            }

            Assert.That(result, Is.Not.Null);
            Assert.That(
                result.IsSuccess,
                Is.True,
                result == null
                    ? "Warmaster load returned no typed result."
                    : string.Join("; ", result.Diagnostics.Select(item =>
                        item.Code + ": " + item.TechnicalMessage)));
            return result;
        }

        private const string ArtifactPath = "al_warmaster_technical_catalog.json";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private static WarmasterTechnicalCatalogLoadResult Load(
            IGameDataCatalogSource source)
        {
            var loader = new WarmasterTechnicalCatalogLoader();
            WarmasterTechnicalCatalogLoadResult result = null;
            using (IGameDataCatalogLoadOperation operation = loader.BeginLoad(
                       source,
                       GameDataCatalogSourceKind.Packaged,
                       value => result = value))
            {
                for (var index = 0; index < 20 && !operation.IsCompleted; index++)
                {
                    operation.Tick();
                }

                Assert.That(operation.IsCompleted, Is.True);
            }

            Assert.That(result, Is.Not.Null);
            return result;
        }

        private static ImmediateCatalogSource SourceWith(
            byte[] artifact,
            byte[] manifest)
        {
            return new ImmediateCatalogSource()
                .Add(WarmasterTechnicalCatalogContract.ManifestRelativePath, manifest)
                .Add(ArtifactPath, artifact);
        }

        private static byte[] ManifestFor(byte[] artifact)
        {
            byte[] canonicalArtifact = ReadPackagedBytes(ArtifactPath);
            string canonicalHash = GameDataCatalogValidator.ComputeSha256(canonicalArtifact);
            string changedHash = GameDataCatalogValidator.ComputeSha256(artifact);
            return Utf8.GetBytes(Utf8.GetString(ReadPackagedBytes(
                WarmasterTechnicalCatalogContract.ManifestRelativePath)).Replace(
                canonicalHash,
                changedHash));
        }

        private static byte[] ReadPackagedBytes(string relativePath)
        {
            return File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                relativePath));
        }

        private sealed class ImmediateCatalogSource : IGameDataCatalogSource
        {
            private readonly Dictionary<string, byte[]> entries =
                new Dictionary<string, byte[]>(StringComparer.Ordinal);

            public ImmediateCatalogSource Add(string relativePath, byte[] bytes)
            {
                entries.Add(relativePath, (byte[])bytes.Clone());
                return this;
            }

            public IGameDataCatalogReadOperation Read(
                GameDataCatalogReadRequest request,
                Action<GameDataCatalogReadResult> completed)
            {
                byte[] bytes;
                GameDataCatalogReadStatus status = entries.TryGetValue(
                    request.RelativePath,
                    out bytes)
                    ? GameDataCatalogReadStatus.Succeeded
                    : GameDataCatalogReadStatus.NotFound;
                completed(new GameDataCatalogReadResult(
                    status,
                    request.RelativePath,
                    bytes,
                    status == GameDataCatalogReadStatus.Succeeded
                        ? string.Empty
                        : "fixture_missing"));
                return new ImmediateReadOperation();
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
}
