using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AL.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmGemWishgateCatalogResolverTests
    {
        private const string CatalogRelativePath =
            "AL/StreamingAssets/GameData/al_realm_gem_wishgate_content_catalog.json";

        [Test]
        public void CanonicalSourcePublishesImmutableRealmGemAndWishEmphasisAuthority()
        {
            byte[] sourceBytes = ReadCatalogBytes();
            RealmGemWishgateCatalogLoadResult result = RealmGemWishgateCatalogResolver.Load(
                sourceBytes,
                LoadRealmGemAuthority());

            Assert.That(result.Status, Is.EqualTo(RealmGemWishgateCatalogLoadStatus.Ready));
            Assert.That(result.TechnicalCode, Is.EqualTo(RealmGemWishgateCatalogResolver.ReadyCode));
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(result.Snapshot.SourceVersion, Is.EqualTo("0.1.0"));
            Assert.That(result.Snapshot.SourceSha256, Is.EqualTo(
                "942699cb3c39ebea243c381bd5cadf78ab85aef177902ed09aad2b60897a086b"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "gem_crownlands_sun",
                    "gem_crownlands_oath",
                    "gem_stonehold_forge",
                    "gem_stonehold_depth",
                    "gem_eldergrove_root",
                    "gem_eldergrove_moon",
                    "gem_umbral_veil",
                    "gem_umbral_ember"
                },
                result.Snapshot.RealmGems.Select(entry => entry.Id).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "wish_emphasis_bridges",
                    "wish_emphasis_vigil",
                    "wish_emphasis_renewal"
                },
                result.Snapshot.WishEmphases.Select(entry => entry.Id).ToArray());
            Assert.That(result.Snapshot.Wishgate.Id, Is.EqualTo("wishgate_eightfold_concordance"));
            Assert.That(result.Snapshot.ResolveRealmGem("gem_stonehold_depth").Status,
                Is.EqualTo(RealmGemWishgateQueryStatus.Found));
            Assert.That(result.Snapshot.ResolveWishEmphasis("wish_emphasis_renewal").Status,
                Is.EqualTo(RealmGemWishgateQueryStatus.Found));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<RealmGemWishgateCatalogEntry>)result.Snapshot.RealmGems).RemoveAt(0));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<WishEmphasisCatalogEntry>)result.Snapshot.WishEmphases).Add(
                    result.Snapshot.WishEmphases[0]));
        }

        [Test]
        public void MissingSourceAndRealmAuthorityFailClosedWithDistinctResults()
        {
            RealmGemWishgateCatalogLoadResult missingSource =
                RealmGemWishgateCatalogResolver.Load(null, LoadRealmGemAuthority());
            RealmGemWishgateCatalogLoadResult missingRealmAuthority =
                RealmGemWishgateCatalogResolver.Load(ReadCatalogBytes(), null);

            AssertRejected(
                missingSource,
                RealmGemWishgateCatalogLoadStatus.SourceUnavailable);
            AssertRejected(
                missingRealmAuthority,
                RealmGemWishgateCatalogLoadStatus.RealmAuthorityUnavailable);
        }

        [TestCase("\"version\": \"0.1.0\"", "\"version\": \"0.2.0\"",
            RealmGemWishgateCatalogLoadStatus.FutureVersion)]
        [TestCase("\"version\": \"0.1.0\"", "\"version\": \"0.0.9\"",
            RealmGemWishgateCatalogLoadStatus.UnsupportedVersion)]
        [TestCase(
            "\"catalogId\": \"al_realm_gem_wishgate_content_catalog\"",
            "\"catalogId\": \"al_realm_gem_wishgate_content_catalog_x\"",
            RealmGemWishgateCatalogLoadStatus.IdentityMismatch)]
        public void VersionAndIdentityDriftReturnExplicitFailures(
            string search,
            string replacement,
            RealmGemWishgateCatalogLoadStatus expected)
        {
            AssertRejected(Load(MutateFirst(search, replacement)), expected);
        }

        [Test]
        public void DuplicateJsonMembersReturnExplicitFailure()
        {
            AssertRejected(
                Load(MutateFirst(
                    "\"version\": \"0.1.0\"",
                    "\"version\": \"0.1.0\",\n  \"version\": \"0.1.0\"")),
                RealmGemWishgateCatalogLoadStatus.DuplicateMember);
        }

        [Test]
        public void DuplicateCatalogIdsReturnExplicitFailure()
        {
            AssertRejected(
                Load(MutateFirst(
                    "\"id\": \"gem_crownlands_oath\"",
                    "\"id\": \"gem_crownlands_sun\"")),
                RealmGemWishgateCatalogLoadStatus.DuplicateId);
        }

        [Test]
        public void UnknownRealmGemReturnsExplicitFailure()
        {
            AssertRejected(
                Load(MutateFirst(
                    "\"id\": \"gem_crownlands_oath\"",
                    "\"id\": \"gem_crownlands_future\"")),
                RealmGemWishgateCatalogLoadStatus.UnknownRealmGem);
        }

        [Test]
        public void RealmGemHomeRealmDriftReturnsExplicitFailure()
        {
            AssertRejected(
                Load(MutateFirst(
                    "\"realmId\": \"crownlands\"",
                    "\"realmId\": \"stonehold\"")),
                RealmGemWishgateCatalogLoadStatus.RealmAuthorityMismatch);
        }

        [Test]
        public void SourceContentDriftReturnsHashFailureWithoutPublishing()
        {
            AssertRejected(
                Load(MutateFirst("\"text\": \"Sun Gem\"", "\"text\": \"Sun Gem!\"")),
                RealmGemWishgateCatalogLoadStatus.SourceHashMismatch);
        }

        [Test]
        public void NarrativeAuthorityDriftFailsSemanticValidationBeforeHashValidation()
        {
            AssertRejected(
                Load(MutateFirst(
                    "\"primaryMode\": \"codex_narrative_content\"",
                    "\"primaryMode\": \"runtime_fallback\"")),
                RealmGemWishgateCatalogLoadStatus.InvalidSource);
        }

        [Test]
        public void QueriesRejectMalformedAndUnknownIdsWithoutFallback()
        {
            RealmGemWishgateCatalogSnapshot snapshot = Load(ReadCatalogBytes()).Snapshot;

            Assert.That(snapshot.ResolveRealmGem("Gem_Stonehold_Depth").Status,
                Is.EqualTo(RealmGemWishgateQueryStatus.InvalidId));
            Assert.That(snapshot.ResolveRealmGem("gem_stonehold_future").Status,
                Is.EqualTo(RealmGemWishgateQueryStatus.UnknownId));
            Assert.That(snapshot.ResolveWishEmphasis(" ").Status,
                Is.EqualTo(RealmGemWishgateQueryStatus.InvalidId));
            Assert.That(snapshot.ResolveWishEmphasis("wish_emphasis_future").Status,
                Is.EqualTo(RealmGemWishgateQueryStatus.UnknownId));
        }

        [Test]
        public void CallerByteMutationCannotChangePublishedSnapshot()
        {
            byte[] bytes = ReadCatalogBytes();
            RealmGemWishgateCatalogSnapshot snapshot = Load(bytes).Snapshot;

            Array.Clear(bytes, 0, bytes.Length);

            Assert.That(snapshot.ResolveRealmGem("gem_crownlands_sun").IsFound, Is.True);
            Assert.That(snapshot.ResolveWishEmphasis("wish_emphasis_bridges").IsFound, Is.True);
        }

        [Test]
        public void CatalogAuthorityDoesNotImplementWishgateMutationAuthority()
        {
            Assert.That(
                typeof(IWishgateTransactionAuthority).IsAssignableFrom(
                    typeof(RealmGemWishgateCatalogSnapshot)),
                Is.False);
            Assert.That(
                typeof(IWishgateTransactionAuthority).IsAssignableFrom(
                    typeof(RealmGemWishgateCatalogResolver)),
                Is.False);
        }

        [Test]
        public void FailureTechnicalCodesAreStableAndHyphenated()
        {
            Assert.That(
                RealmGemWishgateCatalogResolver.Load(null, LoadRealmGemAuthority()).TechnicalCode,
                Is.EqualTo("AL-REALM-GEM-WISHGATE-CATALOG-SOURCE-UNAVAILABLE"));
            Assert.That(
                Load(MutateFirst(
                    "\"version\": \"0.1.0\"",
                    "\"version\": \"0.2.0\"")).TechnicalCode,
                Is.EqualTo("AL-REALM-GEM-WISHGATE-CATALOG-FUTURE-VERSION"));
            Assert.That(
                Load(MutateFirst(
                    "\"id\": \"gem_crownlands_oath\"",
                    "\"id\": \"gem_crownlands_future\"")).TechnicalCode,
                Is.EqualTo("AL-REALM-GEM-WISHGATE-CATALOG-UNKNOWN-REALM-GEM"));
        }

        private static RealmGemWishgateCatalogLoadResult Load(byte[] bytes)
        {
            return RealmGemWishgateCatalogResolver.Load(bytes, LoadRealmGemAuthority());
        }

        private static byte[] MutateFirst(string search, string replacement)
        {
            string source = Encoding.UTF8.GetString(ReadCatalogBytes());
            int index = source.IndexOf(search, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Fixture text was not found: " + search);
            return Encoding.UTF8.GetBytes(
                source.Substring(0, index) +
                replacement +
                source.Substring(index + search.Length));
        }

        private static void AssertRejected(
            RealmGemWishgateCatalogLoadResult result,
            RealmGemWishgateCatalogLoadStatus expected)
        {
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.TechnicalCode, Is.Not.Empty);
            Assert.That(result.IsReady, Is.False);
        }

        private static byte[] ReadCatalogBytes()
        {
            return File.ReadAllBytes(Path.Combine(Application.dataPath, CatalogRelativePath));
        }

        private static RealmGemCatalogSnapshot LoadRealmGemAuthority()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult realmResult = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.That(realmResult.IsSuccess, Is.True, realmResult.TechnicalCode);
            RealmGemCatalogBuildResult gemResult = RealmGemCatalogResolver.Build(realmResult.Snapshot);
            Assert.That(gemResult.IsReady, Is.True, gemResult.TechnicalCode);
            return gemResult.Snapshot;
        }
    }
}
