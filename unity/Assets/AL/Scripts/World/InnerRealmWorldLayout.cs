using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using UnityEngine;

namespace AL.World
{
    public readonly struct InnerRealmRect
    {
        public InnerRealmRect(Vector3 center, float halfExtent)
        {
            Center = center;
            HalfExtent = halfExtent;
        }

        public Vector3 Center { get; }
        public float HalfExtent { get; }
        public float MinX => Center.x - HalfExtent;
        public float MaxX => Center.x + HalfExtent;
        public float MinZ => Center.z - HalfExtent;
        public float MaxZ => Center.z + HalfExtent;

        public bool Contains(Vector3 point)
        {
            return point.x >= MinX && point.x <= MaxX && point.z >= MinZ && point.z <= MaxZ;
        }
    }

    public sealed class InnerRealmSlotLayout
    {
        internal InnerRealmSlotLayout(
            string realmId,
            RealmId realm,
            string innerAtlasZoneId,
            string innerWallId,
            string outerWallId,
            string transitionZoneId,
            string mainGateId,
            string outerWarzoneId,
            string outerAtlasZoneId,
            string ringSlotId,
            Vector2 cornerSign,
            InnerRealmRect innerSafe,
            Vector3 capitalPosition,
            Vector3 outpostAPosition,
            Vector3 outpostBPosition,
            Vector3 gatePosition,
            Vector3 cavePosition,
            Vector3 outerWallCenter)
        {
            RealmId = realmId;
            Realm = realm;
            InnerAtlasZoneId = innerAtlasZoneId;
            InnerWallId = innerWallId;
            OuterWallId = outerWallId;
            TransitionZoneId = transitionZoneId;
            MainGateId = mainGateId;
            OuterWarzoneId = outerWarzoneId;
            OuterAtlasZoneId = outerAtlasZoneId;
            RingSlotId = ringSlotId;
            CornerSign = cornerSign;
            InnerSafe = innerSafe;
            CapitalPosition = capitalPosition;
            OutpostAPosition = outpostAPosition;
            OutpostBPosition = outpostBPosition;
            GatePosition = gatePosition;
            CavePosition = cavePosition;
            OuterWallCenter = outerWallCenter;
            CapitalPoiId = InnerRealmWorldIds.CapitalPoiId(innerAtlasZoneId);
            OutpostAPoiId = InnerRealmWorldIds.OutpostAPoiId(innerAtlasZoneId);
            OutpostBPoiId = InnerRealmWorldIds.OutpostBPoiId(innerAtlasZoneId);
            DragonCaveId = InnerRealmWorldIds.DragonCaveId(innerAtlasZoneId);
        }

        public string RealmId { get; }
        public RealmId Realm { get; }
        public string InnerAtlasZoneId { get; }
        public string InnerWallId { get; }
        public string OuterWallId { get; }
        public string TransitionZoneId { get; }
        public string MainGateId { get; }
        public string OuterWarzoneId { get; }
        public string OuterAtlasZoneId { get; }
        public string RingSlotId { get; }
        public Vector2 CornerSign { get; }
        public InnerRealmRect InnerSafe { get; }
        public Vector3 CapitalPosition { get; }
        public Vector3 OutpostAPosition { get; }
        public Vector3 OutpostBPosition { get; }
        public Vector3 GatePosition { get; }
        public Vector3 CavePosition { get; }
        public Vector3 OuterWallCenter { get; }
        public string CapitalPoiId { get; }
        public string OutpostAPoiId { get; }
        public string OutpostBPoiId { get; }
        public string DragonCaveId { get; }
        public Vector3 WalkableSpawn => CapitalPosition + new Vector3(0f, 1.1f, 0f);
    }

    public sealed class WorldBridgeLayout
    {
        internal WorldBridgeLayout(string id, string connectionType, Vector3 start, Vector3 end, bool sealedEvent)
        {
            Id = id;
            ConnectionType = connectionType;
            Start = start;
            End = end;
            SealedEvent = sealedEvent;
        }

        public string Id { get; }
        public string ConnectionType { get; }
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public bool SealedEvent { get; }
        public Vector3 Midpoint => (Start + End) * 0.5f;
    }

    public sealed class InnerRealmWorldLayout
    {
        public const float ContinentHalfExtent = 160f;
        public const float InnerHalfExtent = 36f;
        public const float InnerInset = 124f;
        public const float GateWidth = 10f;
        public const float WallHeight = 6.2f;
        public const float IsleHeight = 18f;

        // TEMPORARY presentation only. Atlas v001 placement remains unresolved_user_gate.
        private static readonly string[] ProposalRealmOrder =
        {
            "stonehold", "eldergrove", "crownlands", "umbral"
        };

        private static readonly string[] ProposalRingSlots =
        {
            "ring_slot_01", "ring_slot_02", "ring_slot_03", "ring_slot_04"
        };

        private static readonly Vector2[] CornerSigns =
        {
            new Vector2(-1f, -1f),
            new Vector2(1f, -1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, 1f)
        };

        private InnerRealmWorldLayout(
            string topologyId,
            bool atlasPlacementResolved,
            IList<InnerRealmSlotLayout> inners,
            IList<WorldBridgeLayout> bridges)
        {
            TopologyId = topologyId;
            AtlasPlacementResolved = atlasPlacementResolved;
            PlacementStatus = InnerRealmWorldIds.PlacementProposalStatus;
            Inners = Array.AsReadOnly((inners ?? Array.Empty<InnerRealmSlotLayout>()).ToArraySafe());
            Bridges = Array.AsReadOnly((bridges ?? Array.Empty<WorldBridgeLayout>()).ToArraySafe());
            AccordantIsleCenter = new Vector3(0f, IsleHeight, 0f);
        }

        public string TopologyId { get; }
        public bool AtlasPlacementResolved { get; }
        public string PlacementStatus { get; }
        public IReadOnlyList<InnerRealmSlotLayout> Inners { get; }
        public IReadOnlyList<WorldBridgeLayout> Bridges { get; }
        public Vector3 AccordantIsleCenter { get; }
        public string AccordantIsleZoneId => "zone_accordant_isle";
        public string TemporaryLabel => InnerRealmWorldIds.TemporaryLabel;
        public string ColoredMapNote => InnerRealmWorldIds.ColoredMapMissingNote;

        public static InnerRealmWorldLayout FromSnapshot(WorldAtlasSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var inners = new List<InnerRealmSlotLayout>(4);
            for (int i = 0; i < ProposalRealmOrder.Length; i++)
            {
                string realmId = ProposalRealmOrder[i];
                if (!TryFindBoundary(snapshot, realmId, out WorldAtlasBoundary boundary))
                {
                    throw new InvalidOperationException("Missing atlas boundary for " + realmId);
                }

                inners.Add(BuildInner(boundary, ProposalRingSlots[i], CornerSigns[i]));
            }

            var bridges = new List<WorldBridgeLayout>(12);
            foreach (WorldAtlasBridge bridge in snapshot.Bridges)
            {
                bool center = bridge.NodeAId == "center_slot" || bridge.NodeBId == "center_slot";
                Vector3 a = Endpoint(inners, snapshot, bridge.NodeAId, bridge.Id, center, firstOfPair: true);
                Vector3 b = Endpoint(inners, snapshot, bridge.NodeBId, bridge.Id, center, firstOfPair: false);
                bridges.Add(new WorldBridgeLayout(bridge.Id, bridge.ConnectionType, a, b, center));
            }

            return new InnerRealmWorldLayout(
                snapshot.TopologyId,
                snapshot.PlacementResolved,
                inners,
                bridges);
        }

        public bool TryGetInner(string realmId, out InnerRealmSlotLayout inner)
        {
            if (!string.IsNullOrEmpty(realmId))
            {
                for (int i = 0; i < Inners.Count; i++)
                {
                    if (string.Equals(Inners[i].RealmId, realmId, StringComparison.Ordinal))
                    {
                        inner = Inners[i];
                        return true;
                    }
                }
            }

            inner = null;
            return false;
        }

        public InnerRealmSlotLayout GetWalkableInner(string preferredRealmId)
        {
            if (TryGetInner(preferredRealmId, out InnerRealmSlotLayout preferred))
            {
                return preferred;
            }

            return Inners[0];
        }

        public static string RealmCatalogId(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return "stonehold";
                case RealmId.Eldergrove: return "eldergrove";
                case RealmId.Crownlands: return "crownlands";
                case RealmId.Umbral: return "umbral";
                default: return string.Empty;
            }
        }

        public static RealmId ParseRealm(string realmId)
        {
            if (string.Equals(realmId, "stonehold", StringComparison.Ordinal)) return RealmId.Stonehold;
            if (string.Equals(realmId, "eldergrove", StringComparison.Ordinal)) return RealmId.Eldergrove;
            if (string.Equals(realmId, "crownlands", StringComparison.Ordinal)) return RealmId.Crownlands;
            if (string.Equals(realmId, "umbral", StringComparison.Ordinal)) return RealmId.Umbral;
            return RealmId.None;
        }

        private static InnerRealmSlotLayout BuildInner(
            WorldAtlasBoundary boundary,
            string ringSlotId,
            Vector2 sign)
        {
            Vector3 center = new Vector3(sign.x * InnerInset, 0f, sign.y * InnerInset);
            var safe = new InnerRealmRect(center, InnerHalfExtent);
            Vector3 towardCorner = new Vector3(sign.x, 0f, sign.y);
            Vector3 towardCenter = -towardCorner;
            Vector3 capital = center + towardCorner * 14f;
            Vector3 lateral = new Vector3(-sign.y, 0f, sign.x);
            Vector3 outpostA = center + lateral * 18f + towardCorner * 4f;
            Vector3 outpostB = center - lateral * 18f + towardCorner * 4f;
            Vector3 gate = center + towardCenter * InnerHalfExtent;
            Vector3 cave = capital + Vector3.down * 2.4f + towardCorner * 4f;
            Vector3 outer = center + towardCenter * (InnerHalfExtent + 18f);
            return new InnerRealmSlotLayout(
                boundary.RealmId,
                ParseRealm(boundary.RealmId),
                boundary.InnerAtlasZoneId,
                boundary.InnerWallId,
                boundary.OuterWallId,
                boundary.TransitionZoneId,
                boundary.MainGateId,
                boundary.OuterWarzoneId,
                boundary.OuterAtlasZoneId,
                ringSlotId,
                sign,
                safe,
                capital,
                outpostA,
                outpostB,
                gate,
                cave,
                outer);
        }

        private static bool TryFindBoundary(
            WorldAtlasSnapshot snapshot,
            string realmId,
            out WorldAtlasBoundary boundary)
        {
            for (int i = 0; i < snapshot.Boundaries.Count; i++)
            {
                if (string.Equals(snapshot.Boundaries[i].RealmId, realmId, StringComparison.Ordinal))
                {
                    boundary = snapshot.Boundaries[i];
                    return true;
                }
            }

            boundary = null;
            return false;
        }

        private static Vector3 Endpoint(
            IList<InnerRealmSlotLayout> inners,
            WorldAtlasSnapshot snapshot,
            string nodeId,
            string bridgeId,
            bool centerBridge,
            bool firstOfPair)
        {
            if (nodeId == "center_slot")
            {
                return new Vector3(0f, 2.2f, 0f);
            }

            InnerRealmSlotLayout slot = FindByRing(inners, nodeId);
            if (slot == null)
            {
                return Vector3.zero;
            }

            if (centerBridge)
            {
                return slot.GatePosition;
            }

            Vector3 along = AdjacentOffset(slot, snapshot, nodeId, bridgeId, firstOfPair);
            return slot.OuterWallCenter + along;
        }

        private static Vector3 AdjacentOffset(
            InnerRealmSlotLayout slot,
            WorldAtlasSnapshot snapshot,
            string nodeId,
            string bridgeId,
            bool firstOfPair)
        {
            float lane = bridgeId.EndsWith("_02", StringComparison.Ordinal) ? 10f : -10f;
            WorldAtlasBridge bridge = null;
            for (int i = 0; i < snapshot.Bridges.Count; i++)
            {
                if (snapshot.Bridges[i].Id == bridgeId)
                {
                    bridge = snapshot.Bridges[i];
                    break;
                }
            }

            string other = bridge == null
                ? nodeId
                : (bridge.NodeAId == nodeId ? bridge.NodeBId : bridge.NodeAId);
            Vector2 otherSign = SignForRing(other);
            Vector3 delta = new Vector3(otherSign.x - slot.CornerSign.x, 0f, otherSign.y - slot.CornerSign.y);
            if (delta.sqrMagnitude < 0.01f)
            {
                return Vector3.zero;
            }

            delta.Normalize();
            Vector3 perp = Vector3.Cross(Vector3.up, delta).normalized;
            return delta * 8f + perp * lane * (firstOfPair ? 1f : 1f);
        }

        private static InnerRealmSlotLayout FindByRing(IList<InnerRealmSlotLayout> inners, string ringSlotId)
        {
            for (int i = 0; i < inners.Count; i++)
            {
                if (inners[i].RingSlotId == ringSlotId)
                {
                    return inners[i];
                }
            }

            return null;
        }

        private static Vector2 SignForRing(string ringSlotId)
        {
            for (int i = 0; i < ProposalRingSlots.Length; i++)
            {
                if (ProposalRingSlots[i] == ringSlotId)
                {
                    return CornerSigns[i];
                }
            }

            return Vector2.zero;
        }
    }

    internal static class InnerRealmWorldLayoutListExtensions
    {
        internal static T[] ToArraySafe<T>(this IList<T> values)
        {
            if (values == null)
            {
                return Array.Empty<T>();
            }

            var copy = new T[values.Count];
            values.CopyTo(copy, 0);
            return copy;
        }
    }
}
