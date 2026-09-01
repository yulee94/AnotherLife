using System.IO;
using System.Linq;
using System.Text;
using AL.Data.Catalogs.MapDisclosure;
using AL.Data.Catalogs.WorldAtlas;
using AL.Data.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.WorldMap
{
    public sealed class MapDisclosureContractTests
    {
        private static byte[] CanonicalBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_map_disclosure_catalog.json"));
        }

        private static byte[] AtlasBytes()
        {
            return File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
        }

        [Test]
        public void CanonicalCatalogLoadsWithServerAuthorityAndCompleteContentFamilies()
        {
            MapDisclosureLoadResult result =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes());

            Assert.That(
                result.Status,
                Is.EqualTo(MapDisclosureLoadStatus.Accepted),
                string.Join("\n", result.Diagnostics.Select(value => value.Fingerprint)));
            Assert.That(result.Snapshot, Is.Not.Null);
            Assert.That(result.Snapshot.Authority.Owner, Is.EqualTo("server"));
            Assert.That(
                result.Snapshot.Authority.EqualRevisionConflictPolicy,
                Is.EqualTo("intersect_visible_identifiers_and_require_refresh"));
            Assert.That(result.Snapshot.VisibilityRules.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.Features.Count, Is.EqualTo(11));
            Assert.That(result.Snapshot.Routes.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.Objectives.Count, Is.EqualTo(5));
            Assert.That(result.Snapshot.AllegianceMarkers.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.RealmGlyphs.Count, Is.EqualTo(4));
            Assert.That(result.Snapshot.SourceSha256, Has.Length.EqualTo(64));
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void AuthoritativeDiscoveryRevealsOnlyAlwaysAndDiscoveredFeatures()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureProjection awaiting =
                MapDisclosureProjection.AwaitingAuthority(catalog);
            MapDisclosureAuthoritySnapshot authority =
                MapDisclosureAuthoritySnapshot.Create(
                    catalog.Authority.SnapshotStateVersion,
                    1,
                    7,
                    catalog.Version,
                    catalog.SourceSha256,
                    new[] { "feature_inner_crownlands" },
                    new string[0],
                    new string[0],
                    new string[0]);

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    awaiting,
                    authority,
                    catalog);

            Assert.That(
                result.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.Accepted));
            CollectionAssert.AreEqual(
                new[] { "feature_accordant_isle", "feature_inner_crownlands" },
                result.Projection.VisibleFeatureIds);
            Assert.That(
                result.Projection.VisibleFeatureIds,
                Does.Not.Contain("feature_inner_stonehold"));
        }

        [Test]
        public void OlderAuthorityRevisionCannotReplaceCurrentDisclosure()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureProjection current =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    Snapshot(catalog, 2, 7, "feature_inner_crownlands"),
                    catalog).Projection;

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    current,
                    Snapshot(catalog, 2, 6, "feature_inner_stonehold"),
                    catalog);

            Assert.That(
                result.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.StaleIgnored));
            Assert.That(result.Projection.StateDigest, Is.EqualTo(current.StateDigest));
            Assert.That(
                result.Projection.VisibleFeatureIds,
                Does.Contain("feature_inner_crownlands"));
            Assert.That(
                result.Projection.VisibleFeatureIds,
                Does.Not.Contain("feature_inner_stonehold"));
        }

        [Test]
        public void DuplicateAuthoritySnapshotKeepsCurrentProjection()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureAuthoritySnapshot authority =
                Snapshot(catalog, 2, 8, "feature_inner_crownlands");
            MapDisclosureProjection current =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    authority,
                    catalog).Projection;

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    current,
                    authority,
                    catalog);

            Assert.That(
                result.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.Duplicate));
            Assert.That(result.Projection, Is.SameAs(current));
        }

        [Test]
        public void MissingLegacyDisclosureStateRestoresAsFailClosedAwaitingAuthority()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;

            MapDisclosureProjection restored =
                MapDisclosurePersistence.Restore(null, catalog);

            Assert.That(restored.HasAuthority, Is.False);
            Assert.That(restored.RequiresRefresh, Is.True);
            Assert.That(restored.AuthorityRevision, Is.Zero);
            Assert.That(restored.VisibleFeatureIds, Is.Empty);
            Assert.That(restored.VisibleRouteIds, Is.Empty);
            Assert.That(restored.VisibleObjectiveIds, Is.Empty);
            Assert.That(restored.VisibleAllegianceMarkerIds, Is.Empty);
        }

        [Test]
        public void PersistedReconnectStateStaysHiddenUntilNewerAuthorityConverges()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureProjection prior =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    Snapshot(catalog, 3, 7, "feature_inner_crownlands"),
                    catalog).Projection;
            MapDisclosurePersistentState saved =
                MapDisclosurePersistence.Capture(prior);

            MapDisclosureProjection restored =
                MapDisclosurePersistence.Restore(saved, catalog);

            Assert.That(restored.HasAuthority, Is.False);
            Assert.That(restored.RequiresRefresh, Is.True);
            Assert.That(restored.AuthorityEpoch, Is.EqualTo(3));
            Assert.That(restored.AuthorityRevision, Is.EqualTo(7));
            Assert.That(restored.VisibleFeatureIds, Is.Empty);

            MapDisclosureReconcileResult refreshed =
                MapDisclosureReconciler.ApplyAuthoritative(
                    restored,
                    Snapshot(catalog, 3, 8, "feature_inner_stonehold"),
                    catalog);
            Assert.That(
                refreshed.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.Accepted));
            Assert.That(
                refreshed.Projection.VisibleFeatureIds,
                Does.Contain("feature_inner_stonehold"));
            Assert.That(
                refreshed.Projection.VisibleFeatureIds,
                Does.Not.Contain("feature_inner_crownlands"));
        }

        [Test]
        public void PersistedEqualRevisionConflictRemainsSuppressed()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureProjection authoritative =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    Snapshot(
                        catalog,
                        3,
                        7,
                        "feature_inner_crownlands"),
                    catalog).Projection;
            MapDisclosureProjection restored =
                MapDisclosurePersistence.Restore(
                    MapDisclosurePersistence.Capture(authoritative),
                    catalog);

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    restored,
                    Snapshot(
                        catalog,
                        3,
                        7,
                        "feature_inner_stonehold"),
                    catalog);

            Assert.That(
                result.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.ConflictSuppressed));
            Assert.That(result.Projection.RequiresRefresh, Is.True);
            Assert.That(
                result.Projection.VisibleFeatureIds,
                Does.Not.Contain("feature_inner_crownlands"));
            Assert.That(
                result.Projection.VisibleFeatureIds,
                Does.Not.Contain("feature_inner_stonehold"));
        }

        [Test]
        public void EqualRevisionConflictIntersectsVisibilityUntilNewerAuthorityConverges()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureProjection current =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    SnapshotWithGrants(
                        catalog,
                        4,
                        9,
                        new[]
                        {
                            "feature_inner_crownlands",
                            "feature_inner_stonehold"
                        },
                        new[]
                        {
                            "route_crownlands_to_accordant",
                            "route_stonehold_to_accordant"
                        },
                        new[]
                        {
                            "map_objective_realm_main_gate_defense",
                            "map_objective_crossroads_control"
                        },
                        new[]
                        {
                            "allegiance_crownlands",
                            "allegiance_stonehold"
                        }),
                    catalog).Projection;
            MapDisclosureAuthoritySnapshot conflicting = SnapshotWithGrants(
                catalog,
                4,
                9,
                new[] { "feature_inner_stonehold", "feature_inner_eldergrove" },
                new[]
                {
                    "route_stonehold_to_accordant",
                    "route_eldergrove_to_accordant"
                },
                new[]
                {
                    "map_objective_realm_main_gate_defense",
                    "map_objective_eight_gem_custody"
                },
                new[] { "allegiance_stonehold", "allegiance_eldergrove" });

            MapDisclosureReconcileResult conflict =
                MapDisclosureReconciler.ApplyAuthoritative(
                    current,
                    conflicting,
                    catalog);

            Assert.That(
                conflict.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.ConflictSuppressed));
            Assert.That(conflict.Projection.RequiresRefresh, Is.True);
            CollectionAssert.AreEqual(
                new[] { "feature_accordant_isle", "feature_inner_stonehold" },
                conflict.Projection.VisibleFeatureIds);
            CollectionAssert.AreEqual(
                new[] { "route_stonehold_to_accordant" },
                conflict.Projection.VisibleRouteIds);
            CollectionAssert.AreEqual(
                new[] { "map_objective_realm_main_gate_defense" },
                conflict.Projection.VisibleObjectiveIds);
            CollectionAssert.AreEqual(
                new[] { "allegiance_stonehold" },
                conflict.Projection.VisibleAllegianceMarkerIds);

            MapDisclosureReconcileResult converged =
                MapDisclosureReconciler.ApplyAuthoritative(
                    conflict.Projection,
                    Snapshot(catalog, 4, 10, "feature_inner_eldergrove"),
                    catalog);
            Assert.That(
                converged.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.Accepted));
            Assert.That(converged.Projection.RequiresRefresh, Is.False);
            Assert.That(
                converged.Projection.VisibleFeatureIds,
                Does.Contain("feature_inner_eldergrove"));
            Assert.That(
                converged.Projection.VisibleFeatureIds,
                Does.Not.Contain("feature_inner_stonehold"));
        }

        [Test]
        public void CatalogFingerprintMismatchSuppressesAllDisclosure()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureProjection current =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    Snapshot(catalog, 5, 1, "feature_inner_crownlands"),
                    catalog).Projection;
            MapDisclosureAuthoritySnapshot mismatched =
                MapDisclosureAuthoritySnapshot.Create(
                    catalog.Authority.SnapshotStateVersion,
                    5,
                    2,
                    catalog.Version,
                    new string('0', 64),
                    new[] { "feature_inner_stonehold" },
                    new[] { "route_stonehold_to_accordant" },
                    new[] { "map_objective_crossroads_control" },
                    new[] { "allegiance_stonehold" });

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    current,
                    mismatched,
                    catalog);

            Assert.That(
                result.Disposition,
                Is.EqualTo(
                    MapDisclosureReconcileDisposition.CatalogMismatchSuppressed));
            Assert.That(result.Projection.HasAuthority, Is.False);
            Assert.That(result.Projection.RequiresRefresh, Is.True);
            Assert.That(result.Projection.VisibleFeatureIds, Is.Empty);
            Assert.That(result.Projection.VisibleRouteIds, Is.Empty);
            Assert.That(result.Projection.VisibleObjectiveIds, Is.Empty);
            Assert.That(result.Projection.VisibleAllegianceMarkerIds, Is.Empty);
        }

        [Test]
        public void UnsupportedSnapshotStateVersionSuppressesAllDisclosure()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureAuthoritySnapshot unsupported =
                MapDisclosureAuthoritySnapshot.Create(
                    catalog.Authority.SnapshotStateVersion + 1,
                    6,
                    1,
                    catalog.Version,
                    catalog.SourceSha256,
                    new[] { "feature_inner_crownlands" },
                    new string[0],
                    new string[0],
                    new string[0]);

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    unsupported,
                    catalog);

            Assert.That(
                result.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.InvalidSuppressed));
            Assert.That(result.Projection.HasAuthority, Is.False);
            Assert.That(result.Projection.RequiresRefresh, Is.True);
            Assert.That(result.Projection.VisibleFeatureIds, Is.Empty);
        }

        [Test]
        public void UnknownAuthorityIdentifierSuppressesSnapshot()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureAuthoritySnapshot incoming =
                MapDisclosureAuthoritySnapshot.Create(
                    catalog.Authority.SnapshotStateVersion,
                    5,
                    1,
                    catalog.Version,
                    catalog.SourceSha256,
                    new[]
                    {
                        "feature_inner_crownlands",
                        "feature_server_only_secret"
                    },
                    new string[0],
                    new string[0],
                    new string[0]);

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    MapDisclosureProjection.AwaitingAuthority(catalog),
                    incoming,
                    catalog);

            Assert.That(
                result.Disposition,
                Is.EqualTo(MapDisclosureReconcileDisposition.InvalidSuppressed));
            Assert.That(result.Projection.VisibleFeatureIds, Is.Empty);
        }

        [Test]
        public void InvalidHighEpochDoesNotPoisonLaterValidAuthority()
        {
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            MapDisclosureProjection awaiting =
                MapDisclosureProjection.AwaitingAuthority(catalog);
            MapDisclosureAuthoritySnapshot invalid =
                MapDisclosureAuthoritySnapshot.Create(
                    catalog.Authority.SnapshotStateVersion + 1,
                    999,
                    1,
                    catalog.Version,
                    catalog.SourceSha256,
                    new[] { "feature_inner_stonehold" },
                    new string[0],
                    new string[0],
                    new string[0]);
            MapDisclosureProjection suppressed =
                MapDisclosureReconciler.ApplyAuthoritative(
                    awaiting,
                    invalid,
                    catalog).Projection;

            MapDisclosureReconcileResult result =
                MapDisclosureReconciler.ApplyAuthoritative(
                    suppressed,
                    Snapshot(
                        catalog,
                        1,
                        1,
                        "feature_inner_crownlands"),
                    catalog);

            Assert.That(result.Disposition, Is.EqualTo(MapDisclosureReconcileDisposition.Accepted));
            Assert.That(
                result.Projection.VisibleFeatureIds,
                Does.Contain("feature_inner_crownlands"));
        }

        [Test]
        public void SaveModelWithoutDisclosureExtensionUsesLegacyDefault()
        {
            var legacySave = new SaveGameData();
            MapDisclosureCatalogSnapshot catalog =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;

            Assert.That(legacySave.MapDisclosure, Is.Null);
            MapDisclosureProjection restored =
                MapDisclosurePersistence.Restore(
                    legacySave.MapDisclosure,
                    catalog);
            Assert.That(restored.HasAuthority, Is.False);
            Assert.That(restored.VisibleFeatureIds, Is.Empty);
        }

        [Test]
        public void CanonicalSourceReferencesResolveAgainstWorldAtlas()
        {
            MapDisclosureCatalogSnapshot disclosure =
                MapDisclosureCatalogLoader.Validate(CanonicalBytes()).Snapshot;
            WorldAtlasSnapshot atlas =
                WorldAtlasTopologyLoader.Validate(AtlasBytes()).Snapshot;
            string[] zoneIds = atlas.Zones.Select(value => value.Id).ToArray();
            string[] objectiveIds =
                atlas.Objectives.Select(value => value.Id).ToArray();

            Assert.That(
                disclosure.Features.Select(value => value.SourceId),
                Is.SubsetOf(zoneIds));
            Assert.That(
                disclosure.Objectives.Select(value => value.SourceObjectiveId),
                Is.SubsetOf(objectiveIds));
        }

        [Test]
        public void DuplicateGlyphIdsAreRejectedWithoutThrowing()
        {
            string payload = Encoding.UTF8.GetString(CanonicalBytes()).Replace(
                "\"id\": \"realm_glyph_stonehold\"",
                "\"id\": \"realm_glyph_crownlands\"");
            MapDisclosureLoadResult result = null;

            Assert.DoesNotThrow(() =>
                result = MapDisclosureCatalogLoader.Validate(
                    Encoding.UTF8.GetBytes(payload)));
            Assert.That(result.Status, Is.EqualTo(MapDisclosureLoadStatus.Rejected));
            Assert.That(
                result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-MAP-DISCLOSURE-ID-INVALID"));
        }

        [Test]
        public void UnsupportedAuthorityPolicyIsRejected()
        {
            string payload = Encoding.UTF8.GetString(CanonicalBytes()).Replace(
                "intersect_visible_identifiers_and_require_refresh",
                "last_write_wins");

            MapDisclosureLoadResult result =
                MapDisclosureCatalogLoader.Validate(
                    Encoding.UTF8.GetBytes(payload));

            Assert.That(result.Status, Is.EqualTo(MapDisclosureLoadStatus.Rejected));
            Assert.That(
                result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-MAP-DISCLOSURE-AUTHORITY-INVALID"));
        }

        [Test]
        public void UnsupportedVisibilityModeIsRejected()
        {
            string payload = Encoding.UTF8.GetString(CanonicalBytes()).Replace(
                "\"mode\": \"active_objective\"",
                "\"mode\": \"client_guess\"");

            MapDisclosureLoadResult result =
                MapDisclosureCatalogLoader.Validate(
                    Encoding.UTF8.GetBytes(payload));

            Assert.That(result.Status, Is.EqualTo(MapDisclosureLoadStatus.Rejected));
            Assert.That(
                result.Diagnostics.Select(value => value.Code),
                Does.Contain("AL-MAP-DISCLOSURE-VISIBILITY-MODE-INVALID"));
        }

        [TestCase(
            "\"idFormat\": \"lowercase_snake_case\",",
            "\"idFormat\": \"lowercase_snake_case\", \"rogue\": true,",
            "AL-MAP-DISCLOSURE-SCHEMA-INVALID")]
        [TestCase(
            "\"sourceType\": \"atlas_zone\"",
            "\"sourceType\": \"client_guess\"",
            "AL-MAP-DISCLOSURE-SOURCE-TYPE-INVALID")]
        [TestCase(
            "\"featureIds\": [\"feature_inner_crownlands\", \"feature_warzone_crownlands_gate\"",
            "\"featureIds\": [\"feature_inner_crownlands\", \"feature_inner_crownlands\"",
            "AL-MAP-DISCLOSURE-ROUTE-INVALID")]
        [TestCase(
            "\"snapshotStateVersion\": 1",
            "\"snapshotStateVersion\": 2",
            "AL-MAP-DISCLOSURE-AUTHORITY-INVALID")]
        [TestCase(
            "feature_inner_crownlands",
            "1feature_inner_crownlands",
            "AL-MAP-DISCLOSURE-ID-INVALID")]
        [TestCase(
            "zone_inner_crownlands",
            "1zone_inner_crownlands",
            "AL-MAP-DISCLOSURE-ID-INVALID")]
        [TestCase(
            "\"nonColorShape\": \"shield\"",
            "\"nonColorShape\": \"Broken Shape\"",
            "AL-MAP-DISCLOSURE-ID-INVALID")]
        [TestCase(
            "\"visibilityRuleId\": \"visibility_active_objective\"",
            "\"visibilityRuleId\": \"visibility_discovered\"",
            "AL-MAP-DISCLOSURE-VISIBILITY-MODE-INVALID")]
        public void RuntimeLoaderRejectsSchemaInvalidPayload(
            string original,
            string replacement,
            string expectedCode)
        {
            string payload = Encoding.UTF8.GetString(CanonicalBytes()).Replace(
                original,
                replacement);

            MapDisclosureLoadResult result =
                MapDisclosureCatalogLoader.Validate(
                    Encoding.UTF8.GetBytes(payload));

            Assert.That(result.Status, Is.EqualTo(MapDisclosureLoadStatus.Rejected));
            Assert.That(
                result.Diagnostics.Select(value => value.Code),
                Does.Contain(expectedCode));
        }

        private static MapDisclosureAuthoritySnapshot Snapshot(
            MapDisclosureCatalogSnapshot catalog,
            long epoch,
            long revision,
            params string[] discoveredFeatureIds)
        {
            return MapDisclosureAuthoritySnapshot.Create(
                catalog.Authority.SnapshotStateVersion,
                epoch,
                revision,
                catalog.Version,
                catalog.SourceSha256,
                discoveredFeatureIds,
                new string[0],
                new string[0],
                new string[0]);
        }

        private static MapDisclosureAuthoritySnapshot SnapshotWithGrants(
            MapDisclosureCatalogSnapshot catalog,
            long epoch,
            long revision,
            string[] discoveredFeatureIds,
            string[] visibleRouteIds,
            string[] visibleObjectiveIds,
            string[] visibleAllegianceMarkerIds)
        {
            return MapDisclosureAuthoritySnapshot.Create(
                catalog.Authority.SnapshotStateVersion,
                epoch,
                revision,
                catalog.Version,
                catalog.SourceSha256,
                discoveredFeatureIds,
                visibleRouteIds,
                visibleObjectiveIds,
                visibleAllegianceMarkerIds);
        }
    }
}
