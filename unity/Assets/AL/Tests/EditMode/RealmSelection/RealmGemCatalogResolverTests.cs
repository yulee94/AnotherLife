using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmGemCatalogResolverTests
    {
        private RealmCatalogSnapshot authority;

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            RealmCatalogLoadResult result = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            authority = result.Snapshot;
        }

        [Test]
        public void CanonicalAuthorityResolvesEightDeterministicOneBasedSlots()
        {
            RealmGemCatalogBuildResult result = RealmGemCatalogResolver.Build(authority);

            Assert.That(result.Status, Is.EqualTo(RealmGemCatalogStatus.Ready));
            Assert.That(result.TechnicalCode, Is.EqualTo(RealmGemCatalogResolver.ReadyCode));
            Assert.That(result.Snapshot.SourceVersion, Is.EqualTo(RealmCatalogRuntime.SupportedVersion));
            Assert.That(result.Snapshot.Entries, Has.Count.EqualTo(8));
            CollectionAssert.AreEqual(
                new[]
                {
                    "gem_crownlands_sun:1",
                    "gem_crownlands_oath:2",
                    "gem_stonehold_forge:1",
                    "gem_stonehold_depth:2",
                    "gem_eldergrove_root:1",
                    "gem_eldergrove_moon:2",
                    "gem_umbral_veil:1",
                    "gem_umbral_ember:2"
                },
                result.Snapshot.Entries
                    .Select(entry => entry.Id + ":" + entry.SaveSlotIndex)
                    .ToArray());
        }

        [Test]
        public void RuntimeProjectionMatchesNarrativeSourceWithoutConsumingIt()
        {
            RealmGemCatalogBuildResult result = RealmGemCatalogResolver.Build(authority);
            string sourcePath = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "al_realm_gem_wishgate_content_catalog.json");
            var source = JsonUtility.FromJson<RealmGemContentSource>(
                File.ReadAllText(sourcePath));

            Assert.That(source, Is.Not.Null);
            Assert.That(source.realmGems, Has.Length.EqualTo(8));
            CollectionAssert.AreEqual(
                source.realmGems.Select(gem => gem.id).ToArray(),
                result.Snapshot.Entries.Select(entry => entry.Id).ToArray());
            CollectionAssert.AreEqual(
                source.realmGems.Select(gem => gem.realmId).ToArray(),
                result.Snapshot.Entries.Select(entry => entry.HomeRealmId).ToArray());
        }

        [Test]
        public void QueriesReturnTypedFoundInvalidAndUnknownResults()
        {
            RealmGemCatalogSnapshot snapshot = RealmGemCatalogResolver.Build(authority).Snapshot;

            RealmGemQueryResult found = snapshot.Resolve("gem_stonehold_depth");
            RealmGemQueryResult blank = snapshot.Resolve(" ");
            RealmGemQueryResult malformed = snapshot.Resolve("Gem_Stonehold_Depth");
            RealmGemQueryResult unknown = snapshot.Resolve("gem_stonehold_future");

            Assert.That(found.Status, Is.EqualTo(RealmGemQueryStatus.Found));
            Assert.That(found.TechnicalCode, Is.EqualTo(RealmGemCatalogResolver.FoundCode));
            Assert.That(found.Entry.HomeRealm, Is.EqualTo(RealmId.Stonehold));
            Assert.That(found.Entry.SaveSlotIndex, Is.EqualTo(2));
            Assert.That(blank.Status, Is.EqualTo(RealmGemQueryStatus.InvalidId));
            Assert.That(malformed.Status, Is.EqualTo(RealmGemQueryStatus.InvalidId));
            Assert.That(unknown.Status, Is.EqualTo(RealmGemQueryStatus.UnknownId));
            Assert.That(unknown.Entry, Is.Null);
        }

        [Test]
        public void MissingAuthorityFailsClosedWithoutFallbackEntries()
        {
            RealmGemCatalogBuildResult result = RealmGemCatalogResolver.Build(null);

            Assert.That(result.Status, Is.EqualTo(RealmGemCatalogStatus.AuthorityUnavailable));
            Assert.That(result.TechnicalCode, Is.EqualTo(RealmGemCatalogResolver.AuthorityUnavailableCode));
            Assert.That(result.Snapshot, Is.Null);
        }

        [Test]
        public void PublishedRealmAndGemCollectionsAreReadOnlyCopies()
        {
            RealmGemCatalogSnapshot snapshot = RealmGemCatalogResolver.Build(authority).Snapshot;
            var realms = (IList<RealmCatalogEntry>)authority.Realms;
            var gemIds = (IList<string>)authority.Realms[0].RealmGemIds;
            var gems = (IList<RealmGemCatalogEntry>)snapshot.Entries;

            Assert.That(realms.IsReadOnly, Is.True);
            Assert.That(gemIds.IsReadOnly, Is.True);
            Assert.That(gems.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => gemIds[0] = "gem_tampered");
            Assert.Throws<NotSupportedException>(() => gems.RemoveAt(0));
            Assert.That(snapshot.Resolve("gem_crownlands_sun").IsFound, Is.True);
        }

        [Serializable]
        private sealed class RealmGemContentSource
        {
            public RealmGemContentRow[] realmGems = Array.Empty<RealmGemContentRow>();
        }

        [Serializable]
        private sealed class RealmGemContentRow
        {
            public string id = string.Empty;
            public string realmId = string.Empty;
        }
    }
}
