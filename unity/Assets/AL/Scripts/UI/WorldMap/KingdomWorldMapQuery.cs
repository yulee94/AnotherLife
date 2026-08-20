using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.World;

namespace AL.UI.WorldMap
{
    /// <summary>
    /// 2.5D kingdom WorldMap query. Enumerable set is the committed realm's
    /// inner-realm castle plus Area points only. Outer-gate, warzone, bridge,
    /// Accordant Isle, and cross-realm nodes are never listed. Taps are
    /// preview-only: no travel, no Warzone load, no 3D position change.
    /// </summary>
    public static class KingdomWorldMapQuery
    {
        public const string KindCastle = "castle";
        public const string KindArea = "area";
        public const string OwnershipCommittedRealm = "committed_realm";
        public const string DisplayCastle = "Capital";
        public const string DisplayArea = "Area";
        public const string TapPreviewOnly = "PREVIEW_ONLY";
        public const string TapRejectedOuter = "REJECTED_OUTER";
        public const string TapRejectedUnknown = "REJECTED_UNKNOWN";
        public const string ZoneTypeInnerRealm = "inner_realm";

        public static KingdomWorldMapQueryResult Enumerate(WorldAtlasSnapshot snapshot, RealmId realm)
        {
            return Enumerate(snapshot, InnerRealmWorldLayout.RealmCatalogId(realm));
        }

        public static KingdomWorldMapQueryResult Enumerate(WorldAtlasSnapshot snapshot, string committedRealmId)
        {
            if (snapshot == null || string.IsNullOrEmpty(committedRealmId))
            {
                return KingdomWorldMapQueryResult.Empty;
            }

            var topology = new WorldAtlasTopologyQuery(snapshot);
            if (!topology.TryGetBoundary(committedRealmId, out WorldAtlasBoundary boundary))
            {
                return KingdomWorldMapQueryResult.Empty;
            }

            if (!topology.TryGetZone(boundary.InnerAtlasZoneId, out WorldAtlasZone zone) ||
                zone == null ||
                !string.Equals(zone.ZoneType, ZoneTypeInnerRealm, StringComparison.Ordinal) ||
                !string.Equals(zone.RealmId, committedRealmId, StringComparison.Ordinal) ||
                IsForbiddenId(zone.Id) ||
                IsForbiddenId(boundary.InnerAtlasZoneId))
            {
                return KingdomWorldMapQueryResult.Empty;
            }

            string regionId = boundary.InnerAtlasZoneId;
            var markers = new[]
            {
                new KingdomWorldMapMarker(
                    InnerRealmWorldIds.CapitalPoiId(regionId),
                    regionId,
                    KindCastle,
                    DisplayCastle),
                new KingdomWorldMapMarker(
                    InnerRealmWorldIds.OutpostAPoiId(regionId),
                    regionId,
                    KindArea,
                    DisplayArea),
                new KingdomWorldMapMarker(
                    InnerRealmWorldIds.OutpostBPoiId(regionId),
                    regionId,
                    KindArea,
                    DisplayArea)
            };

            for (int i = 0; i < markers.Length; i++)
            {
                if (IsForbiddenId(markers[i].Id))
                {
                    return KingdomWorldMapQueryResult.Empty;
                }
            }

            return new KingdomWorldMapQueryResult(
                new[] { regionId },
                new[] { markers[0].Id, markers[1].Id, markers[2].Id },
                markers,
                OwnershipCommittedRealm,
                isSafeZone: true);
        }

        public static KingdomWorldMapTapResult Tap(
            WorldAtlasSnapshot snapshot,
            string committedRealmId,
            string markerId)
        {
            KingdomWorldMapQueryResult query = Enumerate(snapshot, committedRealmId);
            if (ContainsId(query.MarkerIds, markerId))
            {
                return KingdomWorldMapTapResult.Preview(markerId);
            }

            if (IsForbiddenId(markerId))
            {
                return KingdomWorldMapTapResult.Rejected(markerId, TapRejectedOuter);
            }

            return KingdomWorldMapTapResult.Rejected(markerId, TapRejectedUnknown);
        }

        public static bool ContainsOuterRealmId(IReadOnlyList<string> ids)
        {
            if (ids == null)
            {
                return false;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (IsForbiddenId(ids[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsForbiddenId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return true;
            }

            if (id.IndexOf("warzone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("outer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("accordant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("bridge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("crossroads", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("sky_castle", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (id.StartsWith("gate_", StringComparison.Ordinal) ||
                id.StartsWith("wall_", StringComparison.Ordinal) ||
                id.StartsWith("endpoint_", StringComparison.Ordinal) ||
                id.StartsWith("ring_slot_", StringComparison.Ordinal) ||
                id.StartsWith("center_slot", StringComparison.Ordinal) ||
                id.StartsWith("adjacency_", StringComparison.Ordinal) ||
                id.StartsWith("boundary_", StringComparison.Ordinal) ||
                id.StartsWith("zone_transition_", StringComparison.Ordinal) ||
                id.StartsWith("zone_warzone_", StringComparison.Ordinal) ||
                id.StartsWith("objective_", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsId(IReadOnlyList<string> ids, string id)
        {
            if (ids == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class KingdomWorldMapMarker
    {
        internal KingdomWorldMapMarker(string id, string regionId, string kind, string displayName)
        {
            Id = id ?? string.Empty;
            RegionId = regionId ?? string.Empty;
            Kind = kind ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public string Id { get; }
        public string RegionId { get; }
        public string Kind { get; }
        public string DisplayName { get; }
    }

    public sealed class KingdomWorldMapQueryResult
    {
        internal static readonly KingdomWorldMapQueryResult Empty = new KingdomWorldMapQueryResult(
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<KingdomWorldMapMarker>(),
            string.Empty,
            isSafeZone: true);

        internal KingdomWorldMapQueryResult(
            IReadOnlyList<string> regionIds,
            IReadOnlyList<string> markerIds,
            IReadOnlyList<KingdomWorldMapMarker> markers,
            string ownershipState,
            bool isSafeZone)
        {
            RegionIds = regionIds ?? Array.Empty<string>();
            MarkerIds = markerIds ?? Array.Empty<string>();
            Markers = markers ?? Array.Empty<KingdomWorldMapMarker>();
            OwnershipState = ownershipState ?? string.Empty;
            IsSafeZone = isSafeZone;
        }

        public IReadOnlyList<string> RegionIds { get; }
        public IReadOnlyList<string> MarkerIds { get; }
        public IReadOnlyList<KingdomWorldMapMarker> Markers { get; }
        public string OwnershipState { get; }
        public bool IsSafeZone { get; }
    }

    public sealed class KingdomWorldMapTapResult
    {
        internal static KingdomWorldMapTapResult Preview(string markerId)
        {
            return new KingdomWorldMapTapResult(
                KingdomWorldMapQuery.TapPreviewOnly,
                markerId,
                isPreview: true);
        }

        internal static KingdomWorldMapTapResult Rejected(string markerId, string status)
        {
            return new KingdomWorldMapTapResult(status, markerId, isPreview: false);
        }

        private KingdomWorldMapTapResult(string status, string markerId, bool isPreview)
        {
            Status = status ?? string.Empty;
            MarkerId = markerId ?? string.Empty;
            IsPreview = isPreview;
        }

        public string Status { get; }
        public string MarkerId { get; }
        public bool IsPreview { get; }
        public bool RequestsTravel => false;
        public bool LoadsWarzone => false;
        public bool ChangesWorldPosition => false;
    }
}
