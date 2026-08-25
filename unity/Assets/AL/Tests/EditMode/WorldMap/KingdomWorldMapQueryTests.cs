using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.UI.SharedMenu;
using AL.UI.WorldMap;
using AL.World;
using NUnit.Framework;

namespace AL.Tests.EditMode.WorldMap
{
    public sealed class KingdomWorldMapQueryTests
    {
        private static readonly string[] Realms = { "stonehold", "eldergrove", "crownlands", "umbral" };

        [Test]
        public void CanonicalAtlasTwoPointFiveDSetContainsZeroOuterRealmIds()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            HashSet<string> outerIds = CollectOuterRealmIds(snapshot);

            Assert.That(outerIds, Is.Not.Empty, "atlas snapshot must contain outer-realm identities to reject");

            foreach (string realm in Realms)
            {
                KingdomWorldMapQueryResult result = KingdomWorldMapQuery.Enumerate(snapshot, realm);
                Assert.That(KingdomWorldMapQuery.ContainsOuterRealmId(result.RegionIds), Is.False, realm);
                Assert.That(KingdomWorldMapQuery.ContainsOuterRealmId(result.MarkerIds), Is.False, realm);
                CollectionAssert.IsSubsetOf(result.RegionIds, new[] { "zone_inner_" + realm });
                Assert.That(result.MarkerIds.Intersect(outerIds), Is.Empty, realm);
                Assert.That(result.RegionIds.Intersect(outerIds), Is.Empty, realm);
            }
        }

        [Test]
        public void CommittedRealmListsOnlyThatInnerCastleAndAreas()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            KingdomWorldMapQueryResult stonehold = KingdomWorldMapQuery.Enumerate(snapshot, RealmId.Stonehold);

            Assert.That(stonehold.IsSafeZone, Is.True);
            Assert.That(stonehold.OwnershipState, Is.EqualTo(KingdomWorldMapQuery.OwnershipCommittedRealm));
            CollectionAssert.AreEqual(new[] { "zone_inner_stonehold" }, stonehold.RegionIds.ToArray());
            CollectionAssert.AreEqual(
                PrivateKingdomInnerDestinations.EnumerateCastleAndAreas(RealmId.Stonehold).ToArray(),
                stonehold.MarkerIds.ToArray());
            Assert.That(stonehold.Markers.Count, Is.EqualTo(3));
            Assert.That(stonehold.Markers[0].Kind, Is.EqualTo(KingdomWorldMapQuery.KindCastle));
            Assert.That(stonehold.Markers[0].DisplayName, Is.EqualTo(KingdomWorldMapQuery.DisplayCastle));
            Assert.That(stonehold.Markers[1].Kind, Is.EqualTo(KingdomWorldMapQuery.KindArea));
            Assert.That(stonehold.Markers[1].DisplayName, Is.EqualTo(KingdomWorldMapQuery.DisplayArea));
            Assert.That(stonehold.Markers[2].Kind, Is.EqualTo(KingdomWorldMapQuery.KindArea));
            Assert.That(stonehold.Markers[2].DisplayName, Is.EqualTo(KingdomWorldMapQuery.DisplayArea));

            foreach (KingdomWorldMapMarker marker in stonehold.Markers)
            {
                Assert.That(marker.DisplayName, Does.Not.Contain("Outpost"));
                Assert.That(marker.DisplayName, Does.Not.Contain("Warzone"));
                Assert.That(marker.Id, Does.Not.Contain("crownlands"));
                Assert.That(marker.Id, Does.Not.Contain("eldergrove"));
                Assert.That(marker.Id, Does.Not.Contain("umbral"));
            }
        }

        [Test]
        public void AnyAtlasSnapshotTwoPointFiveDEnumerableExcludesEveryOuterIdentity()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            string[] poison =
            {
                "zone_accordant_isle",
                "zone_crossroads_bridges",
                "zone_sky_castle_marker",
                "zone_warzone_stonehold_gate",
                "zone_warzone_eldergrove_gate",
                "zone_warzone_crownlands_gate",
                "zone_warzone_umbral_gate",
                "warzone_stonehold",
                "gate_stonehold_faultline",
                "gate_eldergrove_greenveil",
                "gate_crownlands_meridian",
                "gate_umbral_ashvein",
                "bridge_ring_01_02_01",
                "bridge_ring_01_02_02",
                "bridge_center_ring_01_01",
                "center_slot",
                "ring_slot_01"
            };

            foreach (string realm in Realms)
            {
                KingdomWorldMapQueryResult result = KingdomWorldMapQuery.Enumerate(snapshot, realm);
                IEnumerable<string> listed = result.RegionIds.Concat(result.MarkerIds);
                foreach (string id in poison)
                {
                    Assert.That(listed, Does.Not.Contain(id), realm + " listed " + id);
                }

                foreach (string other in Realms)
                {
                    if (other == realm)
                    {
                        continue;
                    }

                    Assert.That(listed, Does.Not.Contain("zone_inner_" + other), realm);
                    Assert.That(listed, Does.Not.Contain("poi_zone_inner_" + other + "_capital"), realm);
                }

                Assert.That(result.MarkerIds.Count, Is.EqualTo(3), realm);
                Assert.That(result.Markers.Count(marker => marker.Kind == KingdomWorldMapQuery.KindCastle), Is.EqualTo(1));
                Assert.That(result.Markers.Count(marker => marker.Kind == KingdomWorldMapQuery.KindArea), Is.EqualTo(2));
            }
        }

        [Test]
        public void TappingAnAreaIsPreviewOnlyAndNeverLoadsWarzoneOrMoves()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            KingdomWorldMapQueryResult listed = KingdomWorldMapQuery.Enumerate(snapshot, "umbral");

            KingdomWorldMapTapResult tap = KingdomWorldMapQuery.Tap(snapshot, "umbral", listed.MarkerIds[1]);

            Assert.That(tap.Status, Is.EqualTo(KingdomWorldMapQuery.TapPreviewOnly));
            Assert.That(tap.IsPreview, Is.True);
            Assert.That(tap.RequestsTravel, Is.False);
            Assert.That(tap.LoadsWarzone, Is.False);
            Assert.That(tap.ChangesWorldPosition, Is.False);
            Assert.That(tap.MarkerId, Is.EqualTo(listed.MarkerIds[1]));
        }

        [Test]
        public void TappingOuterGateWarzoneBridgeOrIsleIsRejectedWithoutTravel()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            string[] forbidden =
            {
                "zone_warzone_stonehold_gate",
                "warzone_center_unplayable",
                "bridge_ring_02_03_01",
                "zone_accordant_isle",
                "gate_crownlands_meridian",
                "zone_crossroads_bridges"
            };

            foreach (string id in forbidden)
            {
                KingdomWorldMapTapResult tap = KingdomWorldMapQuery.Tap(snapshot, "stonehold", id);
                Assert.That(tap.Status, Is.EqualTo(KingdomWorldMapQuery.TapRejectedOuter), id);
                Assert.That(tap.IsPreview, Is.False, id);
                Assert.That(tap.RequestsTravel, Is.False, id);
                Assert.That(tap.LoadsWarzone, Is.False, id);
                Assert.That(tap.ChangesWorldPosition, Is.False, id);
            }
        }

        [Test]
        public void MissingSnapshotOrRealmYieldsEmptySet()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            Assert.That(KingdomWorldMapQuery.Enumerate(null, "stonehold").MarkerIds, Is.Empty);
            Assert.That(KingdomWorldMapQuery.Enumerate(snapshot, string.Empty).MarkerIds, Is.Empty);
            Assert.That(KingdomWorldMapQuery.Enumerate(snapshot, "shared").MarkerIds, Is.Empty);
            Assert.That(KingdomWorldMapQuery.Enumerate(snapshot, RealmId.None).MarkerIds, Is.Empty);
        }

        private static HashSet<string> CollectOuterRealmIds(WorldAtlasSnapshot snapshot)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldAtlasNode node in snapshot.Nodes)
            {
                ids.Add(node.Id);
                if (!string.IsNullOrEmpty(node.AtlasZoneId))
                {
                    ids.Add(node.AtlasZoneId);
                }
            }

            foreach (WorldAtlasAdjacency adjacency in snapshot.Adjacencies)
            {
                ids.Add(adjacency.Id);
            }

            foreach (WorldAtlasBridge bridge in snapshot.Bridges)
            {
                ids.Add(bridge.Id);
                ids.Add(bridge.NodeAId);
                ids.Add(bridge.NodeBId);
                ids.Add(bridge.EndpointAId);
                ids.Add(bridge.EndpointBId);
            }

            foreach (WorldAtlasEndpoint endpoint in snapshot.Endpoints)
            {
                ids.Add(endpoint.Id);
            }

            foreach (WorldAtlasBoundary boundary in snapshot.Boundaries)
            {
                ids.Add(boundary.Id);
                ids.Add(boundary.MainGateId);
                ids.Add(boundary.OuterWallId);
                ids.Add(boundary.OuterWarzoneId);
                ids.Add(boundary.OuterAtlasZoneId);
                ids.Add(boundary.TransitionZoneId);
            }

            foreach (WorldAtlasZone zone in snapshot.Zones)
            {
                if (!string.Equals(zone.ZoneType, KingdomWorldMapQuery.ZoneTypeInnerRealm, StringComparison.Ordinal))
                {
                    ids.Add(zone.Id);
                }
            }

            foreach (WorldAtlasObjective objective in snapshot.Objectives)
            {
                ids.Add(objective.Id);
            }

            ids.RemoveWhere(id => string.IsNullOrEmpty(id));
            return ids;
        }
    }
}
