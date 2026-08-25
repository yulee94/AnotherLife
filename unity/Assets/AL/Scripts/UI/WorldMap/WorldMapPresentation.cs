using System;
using System.Collections.Generic;
using System.Linq;
using AL.Data.Catalogs.WorldAtlas;
using UnityEngine;

namespace AL.UI.WorldMap
{
    public readonly struct WorldMapUv
    {
        public WorldMapUv(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
        public Vector2 AsVector => new Vector2(X, Y);
    }

    public sealed class WorldMapSettlement
    {
        internal WorldMapSettlement(string id, string label, string kind, WorldMapUv uv)
        {
            Id = id;
            Label = label;
            Kind = kind;
            Uv = uv;
        }

        public string Id { get; }
        public string Label { get; }
        public string Kind { get; }
        public WorldMapUv Uv { get; }
    }

    public sealed class WorldMapInnerRealm
    {
        internal WorldMapInnerRealm(
            string realmId,
            string innerAtlasZoneId,
            string innerWallId,
            string displayName,
            WorldMapUv capitalUv,
            WorldMapUv outpostAUv,
            WorldMapUv outpostBUv,
            WorldMapUv wallFrom,
            WorldMapUv wallTo)
        {
            RealmId = realmId;
            InnerAtlasZoneId = innerAtlasZoneId;
            InnerWallId = innerWallId;
            DisplayName = displayName;
            Capital = new WorldMapSettlement(
                WorldMapIds.CapitalPoiId(innerAtlasZoneId),
                WorldMapIds.DisplayCapital,
                "capital",
                capitalUv);
            OutpostA = new WorldMapSettlement(
                WorldMapIds.OutpostAPoiId(innerAtlasZoneId),
                WorldMapIds.DisplayOutpostA,
                "outpost_a",
                outpostAUv);
            OutpostB = new WorldMapSettlement(
                WorldMapIds.OutpostBPoiId(innerAtlasZoneId),
                WorldMapIds.DisplayOutpostB,
                "outpost_b",
                outpostBUv);
            WallFrom = wallFrom;
            WallTo = wallTo;
        }

        public string RealmId { get; }
        public string InnerAtlasZoneId { get; }
        public string InnerWallId { get; }
        public string DisplayName { get; }
        public WorldMapSettlement Capital { get; }
        public WorldMapSettlement OutpostA { get; }
        public WorldMapSettlement OutpostB { get; }
        public WorldMapUv WallFrom { get; }
        public WorldMapUv WallTo { get; }
    }

    /// <summary>
    /// Open-map chrome topology. Capitals sit at the four corners. Walls are dividing
    /// lines, not circular rings. Outer warzone is omitted (not playable this card).
    /// Compass assignment is a presentation proposal — Atlas v001 leaves slots unresolved.
    /// </summary>
    public sealed class WorldMapPresentation
    {
        // v001 reference map proposal (NW Eldergrove, NE Crownlands, SW Stonehold, SE Umbral).
        private static readonly string[] ProposalRealmOrder =
        {
            "stonehold", "eldergrove", "crownlands", "umbral"
        };

        private static readonly WorldMapUv[] ProposalCorners =
        {
            new WorldMapUv(0.13f, 0.13f),
            new WorldMapUv(0.13f, 0.87f),
            new WorldMapUv(0.87f, 0.87f),
            new WorldMapUv(0.87f, 0.13f)
        };

        private WorldMapPresentation(
            string topologyId,
            bool atlasPlacementResolved,
            IList<WorldMapInnerRealm> inners,
            WorldMapSettlement accordantIsle)
        {
            TopologyId = topologyId;
            AtlasPlacementResolved = atlasPlacementResolved;
            PlacementStatus = WorldMapIds.PlacementProposalStatus;
            Inners = Array.AsReadOnly((inners ?? Array.Empty<WorldMapInnerRealm>()).ToArray());
            AccordantIsle = accordantIsle;
            DrawsPlayableWarzone = false;
            ColoredMapNote = WorldMapIds.ColoredMapMissingNote;
        }

        public string TopologyId { get; }
        public bool AtlasPlacementResolved { get; }
        public string PlacementStatus { get; }
        public IReadOnlyList<WorldMapInnerRealm> Inners { get; }
        public WorldMapSettlement AccordantIsle { get; }
        public bool DrawsPlayableWarzone { get; }
        public string ColoredMapNote { get; }
        public string TemporaryLabel => WorldMapIds.TemporaryLabel;

        public static WorldMapPresentation FromSnapshot(WorldAtlasSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var query = new WorldAtlasTopologyQuery(snapshot);
            var inners = new List<WorldMapInnerRealm>(4);
            for (int i = 0; i < ProposalRealmOrder.Length; i++)
            {
                string realmId = ProposalRealmOrder[i];
                if (!query.TryGetBoundary(realmId, out WorldAtlasBoundary boundary))
                {
                    throw new InvalidOperationException("Missing atlas boundary for " + realmId);
                }

                inners.Add(BuildInner(boundary, ProposalCorners[i]));
            }

            return new WorldMapPresentation(
                snapshot.TopologyId,
                snapshot.PlacementResolved,
                inners,
                new WorldMapSettlement(
                    WorldMapIds.AccordantIsleZoneId,
                    WorldMapIds.DisplayAccordantIsle,
                    "isle",
                    new WorldMapUv(0.5f, 0.5f)));
        }

        public IReadOnlyList<WorldMapSettlement> VisibleSettlements()
        {
            var list = new List<WorldMapSettlement>(12);
            for (int i = 0; i < Inners.Count; i++)
            {
                list.Add(Inners[i].Capital);
                list.Add(Inners[i].OutpostA);
                list.Add(Inners[i].OutpostB);
            }

            return list;
        }

        public bool ContainsWarzoneDestination(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return id.IndexOf("warzone", StringComparison.Ordinal) >= 0 ||
                   id.IndexOf("bridge_", StringComparison.Ordinal) >= 0;
        }

        private static WorldMapInnerRealm BuildInner(WorldAtlasBoundary boundary, WorldMapUv corner)
        {
            float towardCenterX = 0.5f - corner.X;
            float towardCenterY = 0.5f - corner.Y;
            float lateralX = -towardCenterY;
            float lateralY = towardCenterX;

            WorldMapUv capital = Offset(corner, towardCenterX * -0.04f, towardCenterY * -0.04f);
            WorldMapUv outpostA = Offset(corner, towardCenterX * 0.07f + lateralX * 0.09f, towardCenterY * 0.07f + lateralY * 0.09f);
            WorldMapUv outpostB = Offset(corner, towardCenterX * 0.07f - lateralX * 0.09f, towardCenterY * 0.07f - lateralY * 0.09f);
            WorldMapUv wallMid = Offset(corner, towardCenterX * 0.22f, towardCenterY * 0.22f);
            WorldMapUv wallFrom = Offset(wallMid, lateralX * 0.14f, lateralY * 0.14f);
            WorldMapUv wallTo = Offset(wallMid, -lateralX * 0.14f, -lateralY * 0.14f);

            return new WorldMapInnerRealm(
                boundary.RealmId,
                boundary.InnerAtlasZoneId,
                boundary.InnerWallId,
                WorldMapIds.RealmDisplayName(boundary.RealmId),
                capital,
                outpostA,
                outpostB,
                wallFrom,
                wallTo);
        }

        private static WorldMapUv Offset(WorldMapUv origin, float dx, float dy)
        {
            return new WorldMapUv(
                Mathf.Clamp01(origin.X + dx),
                Mathf.Clamp01(origin.Y + dy));
        }
    }
}
