using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AL.RealmGems;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class RealmGemWishgateRuntimeCatalogTests
    {
        private const string ValidAuthorityId = "another-life.realm-gem-wishgate.production";
        private const string ValidSourceHash = "899892f08ecb330359586f3d2ba767c193e4e6c9c91f380cb5c35ccec9a057ce";

        [SetUp]
        public void SetUp()
        {
            RealmGemWishgateRuntimeCatalog.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            RealmGemWishgateRuntimeCatalog.ResetForTests();
        }

        [Test]
        public void PackagedProductionCatalogActivatesThroughRuntimePath()
        {
            string path = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                RealmGemWishgateRuntimeCatalog.RelativePath);

            RealmGemWishgateCatalogLoadResult result =
                RealmGemWishgateRuntimeCatalog.Parse(File.ReadAllText(path));

            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            Assert.That(result.Snapshot.CatalogId, Is.EqualTo("al_realm_gem_wishgate_runtime_catalog"));
            Assert.That(result.Snapshot.AuthorityId, Is.EqualTo(ValidAuthorityId));
            Assert.That(result.Snapshot.SourceSha256, Is.EqualTo(ValidSourceHash));
            Assert.That(result.Snapshot.RealmGems.Select(gem => gem.Id), Is.Ordered);
            Assert.That(result.Snapshot.RealmGems.Count, Is.EqualTo(8));
            Assert.That(result.Snapshot.Wishgate.Id, Is.EqualTo("wishgate_eightfold_concordance"));
            Assert.That(result.Snapshot.Wishgate.EligibilityAuthorityAvailable, Is.False);
            Assert.That(result.Snapshot.Wishgate.RewardAuthorityAvailable, Is.False);

            RealmGemWishgateRuntimeCatalog.MarkLoadingForTests(1);
            RealmGemWishgateRuntimeCatalog.PublishForTests(1, result);

            Assert.That(RealmGemWishgateRuntimeCatalog.Status, Is.EqualTo(RealmGemWishgateCatalogRuntimeStatus.Ready));
            Assert.That(RealmGemWishgateRuntimeCatalog.Current, Is.SameAs(result.Snapshot));
        }

        [TestCase("authorityId", "unrecognized.authority", "AL-RGW-CATALOG-AUTHORITY")]
        [TestCase("sourceSha256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "AL-RGW-CATALOG-SOURCE")]
        [TestCase("authorityVersion", "0", "AL-RGW-CATALOG-AUTHORITY")]
        [TestCase("contentVersion", "0", "AL-RGW-CATALOG-VERSION")]
        public void MissingStaleOrUnrecognizedAuthorityFailsClosed(
            string field,
            string replacement,
            string expectedCode)
        {
            string json = ValidCatalogJson();
            string original = field == "authorityVersion" || field == "contentVersion"
                ? $"\"{field}\": 1"
                : $"\"{field}\": \"{(field == "authorityId" ? ValidAuthorityId : ValidSourceHash)}\"";
            string changed = field == "authorityVersion" || field == "contentVersion"
                ? $"\"{field}\": {replacement}"
                : $"\"{field}\": \"{replacement}\"";

            RealmGemWishgateCatalogLoadResult result =
                RealmGemWishgateRuntimeCatalog.ParseForValidationTests(
                    json.Replace(original, changed));

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.TechnicalCode, Is.EqualTo(expectedCode));
        }

        [Test]
        public void PackagedArtifactTamperingFailsBeforeFieldValidation()
        {
            string tampered = ValidCatalogJson().Replace(
                "gem_crownlands_sun",
                "gem_crownlands_tampered");

            RealmGemWishgateCatalogLoadResult result =
                RealmGemWishgateRuntimeCatalog.Parse(tampered);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.TechnicalCode, Is.EqualTo("AL-RGW-CATALOG-ARTIFACT-HASH"));
        }

        [Test]
        public void InvalidCatalogPublicationClearsPreviouslyReadyAuthority()
        {
            RealmGemWishgateCatalogLoadResult valid =
                RealmGemWishgateRuntimeCatalog.Parse(ValidCatalogJson());
            RealmGemWishgateRuntimeCatalog.MarkLoadingForTests(1);
            RealmGemWishgateRuntimeCatalog.PublishForTests(1, valid);
            Assert.That(RealmGemWishgateRuntimeCatalog.Current, Is.Not.Null);

            RealmGemWishgateRuntimeCatalog.MarkLoadingForTests(2);
            RealmGemWishgateRuntimeCatalog.PublishForTests(
                2,
                RealmGemWishgateRuntimeCatalog.ParseForValidationTests("{}"));

            Assert.That(RealmGemWishgateRuntimeCatalog.Status, Is.EqualTo(RealmGemWishgateCatalogRuntimeStatus.Unavailable));
            Assert.That(RealmGemWishgateRuntimeCatalog.Current, Is.Null);
        }

        [Test]
        public void CatalogRejectsMissingDuplicateAndMismatchedRequiredGemFields()
        {
            string valid = ValidCatalogJson();

            AssertRejected(
                valid.Replace("\"gemIndex\": 1", "\"gemIndex\": 0"),
                "AL-RGW-CATALOG-GEM");
            AssertRejected(
                valid.Replace("gem_crownlands_oath", "gem_crownlands_sun"),
                "AL-RGW-CATALOG-GEM");
            AssertRejected(
                valid.Replace("\"realmId\": \"crownlands\", \"runtimeRealmId\": \"Crownlands\"",
                    "\"realmId\": \"crownlands\", \"runtimeRealmId\": \"Umbral\""),
                "AL-RGW-CATALOG-GEM");
        }

        [Test]
        public void RuntimeCodeDoesNotCrossIntoRealmGemWishgateSourceOnlyFiles()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "AL", "Scripts");
            Assert.That(Directory.Exists(scriptsRoot), Is.True, "Runtime scripts directory is missing.");
            string[] scriptPaths = Directory.GetFiles(
                scriptsRoot,
                "*.cs",
                SearchOption.AllDirectories);
            string[] forbiddenSourceReferences =
            {
                "al_realm_gem_wishgate_content_catalog.json",
                "Realm_Gem_Wishgate_Source_Handoff.md",
                "docs/narrative/gamedata"
            };

            Assert.That(scriptPaths, Is.Not.Empty, "Runtime boundary scan found no C# scripts.");
            foreach (string scriptPath in scriptPaths)
            {
                string runtimeSource = File.ReadAllText(scriptPath)
                    .ToLowerInvariant();
                runtimeSource = Regex.Replace(runtimeSource, @"[\\/]+", "/");
                foreach (string forbiddenReference in forbiddenSourceReferences)
                {
                    Assert.That(
                        runtimeSource,
                        Does.Not.Contain(forbiddenReference.ToLowerInvariant()),
                        $"Runtime script crossed the source-only boundary via " +
                        $"'{forbiddenReference}': {scriptPath}");
                }
            }
        }

        private static void AssertRejected(string json, string code)
        {
            RealmGemWishgateCatalogLoadResult result =
                RealmGemWishgateRuntimeCatalog.ParseForValidationTests(json);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.TechnicalCode, Is.EqualTo(code));
        }

        private static string ValidCatalogJson()
        {
            string path = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                RealmGemWishgateRuntimeCatalog.RelativePath);
            return File.ReadAllText(path);
        }
    }
}
