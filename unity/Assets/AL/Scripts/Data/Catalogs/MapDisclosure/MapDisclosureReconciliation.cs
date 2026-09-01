using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Data.Catalogs.MapDisclosure
{
    public enum MapDisclosureReconcileDisposition
    {
        Accepted = 0,
        Duplicate = 1,
        StaleIgnored = 2,
        ConflictSuppressed = 3,
        CatalogMismatchSuppressed = 4,
        InvalidSuppressed = 5
    }

    public sealed class MapDisclosureAuthoritySnapshot
    {
        private MapDisclosureAuthoritySnapshot(
            int stateVersion,
            long authorityEpoch,
            long authorityRevision,
            string catalogVersion,
            string catalogSha256,
            IReadOnlyList<string> discoveredFeatureIds,
            IReadOnlyList<string> visibleRouteIds,
            IReadOnlyList<string> visibleObjectiveIds,
            IReadOnlyList<string> visibleAllegianceMarkerIds)
        {
            StateVersion = stateVersion;
            AuthorityEpoch = authorityEpoch;
            AuthorityRevision = authorityRevision;
            CatalogVersion = catalogVersion ?? string.Empty;
            CatalogSha256 = catalogSha256 ?? string.Empty;
            DiscoveredFeatureIds = discoveredFeatureIds;
            VisibleRouteIds = visibleRouteIds;
            VisibleObjectiveIds = visibleObjectiveIds;
            VisibleAllegianceMarkerIds = visibleAllegianceMarkerIds;
            StateDigest = ComputeDigest(this);
        }

        public int StateVersion { get; }
        public long AuthorityEpoch { get; }
        public long AuthorityRevision { get; }
        public string CatalogVersion { get; }
        public string CatalogSha256 { get; }
        public IReadOnlyList<string> DiscoveredFeatureIds { get; }
        public IReadOnlyList<string> VisibleRouteIds { get; }
        public IReadOnlyList<string> VisibleObjectiveIds { get; }
        public IReadOnlyList<string> VisibleAllegianceMarkerIds { get; }
        public string StateDigest { get; }

        public static MapDisclosureAuthoritySnapshot Create(
            int stateVersion,
            long authorityEpoch,
            long authorityRevision,
            string catalogVersion,
            string catalogSha256,
            IEnumerable<string> discoveredFeatureIds,
            IEnumerable<string> visibleRouteIds,
            IEnumerable<string> visibleObjectiveIds,
            IEnumerable<string> visibleAllegianceMarkerIds)
        {
            return new MapDisclosureAuthoritySnapshot(
                stateVersion,
                authorityEpoch,
                authorityRevision,
                catalogVersion,
                catalogSha256,
                Normalize(discoveredFeatureIds),
                Normalize(visibleRouteIds),
                Normalize(visibleObjectiveIds),
                Normalize(visibleAllegianceMarkerIds));
        }

        private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
        {
            return Array.AsReadOnly(
                (values ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray());
        }

        private static string ComputeDigest(MapDisclosureAuthoritySnapshot snapshot)
        {
            string payload = string.Join(
                "\n",
                snapshot.StateVersion.ToString(),
                snapshot.AuthorityEpoch.ToString(),
                snapshot.AuthorityRevision.ToString(),
                snapshot.CatalogVersion,
                snapshot.CatalogSha256,
                string.Join("\u001f", snapshot.DiscoveredFeatureIds),
                string.Join("\u001f", snapshot.VisibleRouteIds),
                string.Join("\u001f", snapshot.VisibleObjectiveIds),
                string.Join("\u001f", snapshot.VisibleAllegianceMarkerIds));
            using (SHA256 sha256 = SHA256.Create())
            {
                return string.Concat(
                    sha256.ComputeHash(Encoding.UTF8.GetBytes(payload))
                        .Select(value => value.ToString("x2")));
            }
        }
    }

    public sealed class MapDisclosureProjection
    {
        private MapDisclosureProjection(
            string catalogVersion,
            string catalogSha256,
            bool hasAuthority,
            bool requiresRefresh,
            long authorityEpoch,
            long authorityRevision,
            string stateDigest,
            IReadOnlyList<string> discoveredFeatureIds,
            IReadOnlyList<string> visibleRouteGrants,
            IReadOnlyList<string> visibleObjectiveGrants,
            IReadOnlyList<string> visibleAllegianceGrants,
            IReadOnlyList<string> visibleFeatureIds,
            IReadOnlyList<string> visibleRouteIds,
            IReadOnlyList<string> visibleObjectiveIds,
            IReadOnlyList<string> visibleAllegianceMarkerIds)
        {
            CatalogVersion = catalogVersion ?? string.Empty;
            CatalogSha256 = catalogSha256 ?? string.Empty;
            HasAuthority = hasAuthority;
            RequiresRefresh = requiresRefresh;
            AuthorityEpoch = authorityEpoch;
            AuthorityRevision = authorityRevision;
            StateDigest = stateDigest ?? string.Empty;
            DiscoveredFeatureIds = discoveredFeatureIds;
            VisibleRouteGrants = visibleRouteGrants;
            VisibleObjectiveGrants = visibleObjectiveGrants;
            VisibleAllegianceGrants = visibleAllegianceGrants;
            VisibleFeatureIds = visibleFeatureIds;
            VisibleRouteIds = visibleRouteIds;
            VisibleObjectiveIds = visibleObjectiveIds;
            VisibleAllegianceMarkerIds = visibleAllegianceMarkerIds;
        }

        public string CatalogVersion { get; }
        public string CatalogSha256 { get; }
        public bool HasAuthority { get; }
        public bool RequiresRefresh { get; }
        public long AuthorityEpoch { get; }
        public long AuthorityRevision { get; }
        public string StateDigest { get; }
        public IReadOnlyList<string> VisibleFeatureIds { get; }
        public IReadOnlyList<string> VisibleRouteIds { get; }
        public IReadOnlyList<string> VisibleObjectiveIds { get; }
        public IReadOnlyList<string> VisibleAllegianceMarkerIds { get; }

        internal IReadOnlyList<string> DiscoveredFeatureIds { get; }
        internal IReadOnlyList<string> VisibleRouteGrants { get; }
        internal IReadOnlyList<string> VisibleObjectiveGrants { get; }
        internal IReadOnlyList<string> VisibleAllegianceGrants { get; }

        public static MapDisclosureProjection AwaitingAuthority(
            MapDisclosureCatalogSnapshot catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            IReadOnlyList<string> empty = Array.AsReadOnly(Array.Empty<string>());
            return new MapDisclosureProjection(
                catalog.Version,
                catalog.SourceSha256,
                false,
                true,
                0,
                0,
                string.Empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty);
        }

        internal static MapDisclosureProjection FromAuthority(
            MapDisclosureAuthoritySnapshot authority,
            MapDisclosureCatalogSnapshot catalog,
            bool requiresRefresh)
        {
            var discovered = new HashSet<string>(
                authority.DiscoveredFeatureIds.Where(catalog.ContainsFeature),
                StringComparer.Ordinal);
            var routes = new HashSet<string>(
                authority.VisibleRouteIds.Where(catalog.ContainsRoute),
                StringComparer.Ordinal);
            var objectives = new HashSet<string>(
                authority.VisibleObjectiveIds.Where(catalog.ContainsObjective),
                StringComparer.Ordinal);
            var allegiances = new HashSet<string>(
                authority.VisibleAllegianceMarkerIds.Where(
                    catalog.ContainsAllegianceMarker),
                StringComparer.Ordinal);

            string[] visibleFeatures = catalog.Features
                .Where(feature =>
                {
                    if (!catalog.TryGetVisibilityRule(
                            feature.VisibilityRuleId,
                            out MapDisclosureVisibilityRule rule))
                    {
                        return false;
                    }
                    return string.Equals(rule.Mode, "always", StringComparison.Ordinal) ||
                           string.Equals(rule.Mode, "discovered", StringComparison.Ordinal) &&
                           discovered.Contains(feature.Id);
                })
                .Select(feature => feature.Id)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return new MapDisclosureProjection(
                catalog.Version,
                catalog.SourceSha256,
                true,
                requiresRefresh,
                authority.AuthorityEpoch,
                authority.AuthorityRevision,
                authority.StateDigest,
                Freeze(discovered),
                Freeze(routes),
                Freeze(objectives),
                Freeze(allegiances),
                Array.AsReadOnly(visibleFeatures),
                Freeze(routes),
                Freeze(objectives),
                Freeze(allegiances));
        }

        internal static MapDisclosureProjection Suppressed(
            MapDisclosureCatalogSnapshot catalog,
            MapDisclosureProjection current)
        {
            IReadOnlyList<string> empty = Array.AsReadOnly(Array.Empty<string>());
            return new MapDisclosureProjection(
                catalog.Version,
                catalog.SourceSha256,
                false,
                true,
                current.AuthorityEpoch,
                current.AuthorityRevision,
                string.Empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty);
        }

        internal static MapDisclosureProjection Restored(
            MapDisclosureAuthoritySnapshot authority,
            MapDisclosureCatalogSnapshot catalog)
        {
            IReadOnlyList<string> discovered = Freeze(
                authority.DiscoveredFeatureIds.Where(catalog.ContainsFeature));
            IReadOnlyList<string> routes = Freeze(
                authority.VisibleRouteIds.Where(catalog.ContainsRoute));
            IReadOnlyList<string> objectives = Freeze(
                authority.VisibleObjectiveIds.Where(catalog.ContainsObjective));
            IReadOnlyList<string> allegiances = Freeze(
                authority.VisibleAllegianceMarkerIds.Where(
                    catalog.ContainsAllegianceMarker));
            IReadOnlyList<string> empty = Array.AsReadOnly(Array.Empty<string>());
            return new MapDisclosureProjection(
                catalog.Version,
                catalog.SourceSha256,
                false,
                true,
                authority.AuthorityEpoch,
                authority.AuthorityRevision,
                authority.StateDigest,
                discovered,
                routes,
                objectives,
                allegiances,
                empty,
                empty,
                empty,
                empty);
        }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
        {
            return Array.AsReadOnly(
                values.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class MapDisclosureReconcileResult
    {
        internal MapDisclosureReconcileResult(
            MapDisclosureReconcileDisposition disposition,
            MapDisclosureProjection projection,
            string message)
        {
            Disposition = disposition;
            Projection = projection;
            Message = message ?? string.Empty;
        }

        public MapDisclosureReconcileDisposition Disposition { get; }
        public MapDisclosureProjection Projection { get; }
        public string Message { get; }
    }

    public static class MapDisclosureReconciler
    {
        public static MapDisclosureReconcileResult ApplyAuthoritative(
            MapDisclosureProjection current,
            MapDisclosureAuthoritySnapshot incoming,
            MapDisclosureCatalogSnapshot catalog)
        {
            if (current == null || incoming == null || catalog == null)
            {
                throw new ArgumentNullException(
                    current == null
                        ? nameof(current)
                        : incoming == null ? nameof(incoming) : nameof(catalog));
            }
            if (!string.Equals(
                    current.CatalogVersion,
                    catalog.Version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    current.CatalogSha256,
                    catalog.SourceSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    incoming.CatalogVersion,
                    catalog.Version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    incoming.CatalogSha256,
                    catalog.SourceSha256,
                    StringComparison.Ordinal))
            {
                return new MapDisclosureReconcileResult(
                    MapDisclosureReconcileDisposition.CatalogMismatchSuppressed,
                    MapDisclosureProjection.Suppressed(
                        catalog,
                        current),
                    "AL-MAP-DISCLOSURE-CATALOG-MISMATCH");
            }
            if (incoming.StateVersion != catalog.Authority.SnapshotStateVersion ||
                incoming.AuthorityEpoch <= 0 ||
                incoming.AuthorityRevision <= 0 ||
                incoming.DiscoveredFeatureIds.Any(
                    value => !catalog.ContainsFeature(value)) ||
                incoming.VisibleRouteIds.Any(
                    value => !catalog.ContainsRoute(value)) ||
                incoming.VisibleObjectiveIds.Any(
                    value => !catalog.ContainsObjective(value)) ||
                incoming.VisibleAllegianceMarkerIds.Any(
                    value => !catalog.ContainsAllegianceMarker(value)))
            {
                return new MapDisclosureReconcileResult(
                    MapDisclosureReconcileDisposition.InvalidSuppressed,
                    MapDisclosureProjection.Suppressed(
                        catalog,
                        current),
                    "AL-MAP-DISCLOSURE-AUTHORITY-INVALID");
            }
            if (current.AuthorityEpoch > 0 &&
                (current.AuthorityEpoch > incoming.AuthorityEpoch ||
                 current.AuthorityEpoch == incoming.AuthorityEpoch &&
                 current.AuthorityRevision > incoming.AuthorityRevision))
            {
                return new MapDisclosureReconcileResult(
                    MapDisclosureReconcileDisposition.StaleIgnored,
                    current,
                    "AL-MAP-DISCLOSURE-STALE-SNAPSHOT");
            }
            bool hasComparableState =
                current.HasAuthority || !string.IsNullOrEmpty(current.StateDigest);
            if (hasComparableState &&
                current.AuthorityEpoch == incoming.AuthorityEpoch &&
                current.AuthorityRevision == incoming.AuthorityRevision &&
                string.Equals(
                    current.StateDigest,
                    incoming.StateDigest,
                    StringComparison.Ordinal))
            {
                return current.HasAuthority
                    ? new MapDisclosureReconcileResult(
                        MapDisclosureReconcileDisposition.Duplicate,
                        current,
                        string.Empty)
                    : Accepted(incoming, catalog);
            }
            if (hasComparableState &&
                current.AuthorityEpoch == incoming.AuthorityEpoch &&
                current.AuthorityRevision == incoming.AuthorityRevision &&
                !string.Equals(
                    current.StateDigest,
                    incoming.StateDigest,
                    StringComparison.Ordinal))
            {
                MapDisclosureAuthoritySnapshot intersection =
                    MapDisclosureAuthoritySnapshot.Create(
                        incoming.StateVersion,
                        incoming.AuthorityEpoch,
                        incoming.AuthorityRevision,
                        incoming.CatalogVersion,
                        incoming.CatalogSha256,
                        Intersect(
                            current.DiscoveredFeatureIds,
                            incoming.DiscoveredFeatureIds),
                        Intersect(
                            current.VisibleRouteGrants,
                            incoming.VisibleRouteIds),
                        Intersect(
                            current.VisibleObjectiveGrants,
                            incoming.VisibleObjectiveIds),
                        Intersect(
                            current.VisibleAllegianceGrants,
                            incoming.VisibleAllegianceMarkerIds));
                return new MapDisclosureReconcileResult(
                    MapDisclosureReconcileDisposition.ConflictSuppressed,
                    MapDisclosureProjection.FromAuthority(
                        intersection,
                        catalog,
                        true),
                    "AL-MAP-DISCLOSURE-EQUAL-REVISION-CONFLICT");
            }
            return Accepted(incoming, catalog);
        }

        private static MapDisclosureReconcileResult Accepted(
            MapDisclosureAuthoritySnapshot authority,
            MapDisclosureCatalogSnapshot catalog)
        {
            return new MapDisclosureReconcileResult(
                MapDisclosureReconcileDisposition.Accepted,
                MapDisclosureProjection.FromAuthority(authority, catalog, false),
                string.Empty);
        }

        private static IEnumerable<string> Intersect(
            IEnumerable<string> left,
            IEnumerable<string> right)
        {
            var values = new HashSet<string>(left, StringComparer.Ordinal);
            values.IntersectWith(right);
            return values.OrderBy(value => value, StringComparer.Ordinal);
        }

    }
}
