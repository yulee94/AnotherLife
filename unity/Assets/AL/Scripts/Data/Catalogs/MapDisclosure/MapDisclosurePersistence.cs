using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Data.Catalogs.MapDisclosure
{
    [Serializable]
    public sealed class MapDisclosurePersistentState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public long AuthorityEpoch;
        public long AuthorityRevision;
        public string CatalogVersion = string.Empty;
        public string CatalogSha256 = string.Empty;
        public string StateDigest = string.Empty;
        public List<string> DiscoveredFeatureIds = new List<string>();
        public List<string> VisibleRouteIds = new List<string>();
        public List<string> VisibleObjectiveIds = new List<string>();
        public List<string> VisibleAllegianceMarkerIds = new List<string>();
    }

    public static class MapDisclosurePersistence
    {
        public static MapDisclosurePersistentState Capture(
            MapDisclosureProjection projection)
        {
            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }
            if (!projection.HasAuthority || projection.RequiresRefresh)
            {
                throw new InvalidOperationException(
                    "Only a current authoritative map projection can be persisted.");
            }

            return new MapDisclosurePersistentState
            {
                Version = MapDisclosurePersistentState.CurrentVersion,
                AuthorityEpoch = projection.AuthorityEpoch,
                AuthorityRevision = projection.AuthorityRevision,
                CatalogVersion = projection.CatalogVersion,
                CatalogSha256 = projection.CatalogSha256,
                StateDigest = projection.StateDigest,
                DiscoveredFeatureIds = projection.DiscoveredFeatureIds.ToList(),
                VisibleRouteIds = projection.VisibleRouteGrants.ToList(),
                VisibleObjectiveIds = projection.VisibleObjectiveGrants.ToList(),
                VisibleAllegianceMarkerIds =
                    projection.VisibleAllegianceGrants.ToList()
            };
        }

        public static MapDisclosureProjection Restore(
            MapDisclosurePersistentState state,
            MapDisclosureCatalogSnapshot catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (state == null ||
                state.Version != MapDisclosurePersistentState.CurrentVersion ||
                state.AuthorityEpoch <= 0 ||
                state.AuthorityRevision <= 0 ||
                !string.Equals(
                    state.CatalogVersion,
                    catalog.Version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    state.CatalogSha256,
                    catalog.SourceSha256,
                    StringComparison.Ordinal))
            {
                return MapDisclosureProjection.AwaitingAuthority(catalog);
            }

            MapDisclosureAuthoritySnapshot authority =
                MapDisclosureAuthoritySnapshot.Create(
                    catalog.Authority.SnapshotStateVersion,
                    state.AuthorityEpoch,
                    state.AuthorityRevision,
                    state.CatalogVersion,
                    state.CatalogSha256,
                    state.DiscoveredFeatureIds,
                    state.VisibleRouteIds,
                    state.VisibleObjectiveIds,
                    state.VisibleAllegianceMarkerIds);
            if (!string.Equals(
                    authority.StateDigest,
                    state.StateDigest,
                    StringComparison.Ordinal))
            {
                return MapDisclosureProjection.AwaitingAuthority(catalog);
            }

            return MapDisclosureProjection.Restored(authority, catalog);
        }
    }
}
