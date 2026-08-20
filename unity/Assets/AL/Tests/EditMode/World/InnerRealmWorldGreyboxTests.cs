using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldAtlas;
using AL.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.World
{
    public sealed class InnerRealmWorldGreyboxTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void CanonicalAtlasBuildsFourInnerRealmsWithUnnamedSettlements()
        {
            InnerRealmWorldLayout layout = LoadLayout();

            Assert.That(layout.TopologyId, Is.EqualTo("topology_launch_world_ring_v001"));
            Assert.That(layout.AtlasPlacementResolved, Is.False);
            Assert.That(layout.PlacementStatus, Is.EqualTo(InnerRealmWorldIds.PlacementProposalStatus));
            Assert.That(layout.Inners.Count, Is.EqualTo(4));
            Assert.That(layout.Bridges.Count, Is.EqualTo(12));
            Assert.That(layout.Bridges.Count(bridge => bridge.SealedEvent), Is.EqualTo(4));
            Assert.That(layout.AccordantIsleZoneId, Is.EqualTo("zone_accordant_isle"));
            Assert.That(layout.ColoredMapNote, Does.Contain("colored_specifics_map_missing"));

            string[] expectedZones =
            {
                "zone_inner_stonehold",
                "zone_inner_eldergrove",
                "zone_inner_crownlands",
                "zone_inner_umbral"
            };
            CollectionAssert.AreEquivalent(expectedZones, layout.Inners.Select(inner => inner.InnerAtlasZoneId).ToArray());

            foreach (InnerRealmSlotLayout inner in layout.Inners)
            {
                Assert.That(inner.InnerWallId, Is.EqualTo("wall_" + inner.RealmId + "_inner"));
                Assert.That(inner.MainGateId, Does.StartWith("gate_" + inner.RealmId + "_"));
                Assert.That(inner.CapitalPoiId, Is.EqualTo(InnerRealmWorldIds.CapitalPoiId(inner.InnerAtlasZoneId)));
                Assert.That(inner.OutpostAPoiId, Is.EqualTo(InnerRealmWorldIds.OutpostAPoiId(inner.InnerAtlasZoneId)));
                Assert.That(inner.OutpostBPoiId, Is.EqualTo(InnerRealmWorldIds.OutpostBPoiId(inner.InnerAtlasZoneId)));
                Assert.That(inner.InnerSafe.Contains(inner.CapitalPosition), Is.True, inner.InnerAtlasZoneId);
                Assert.That(inner.InnerSafe.Contains(inner.OutpostAPosition), Is.True);
                Assert.That(inner.InnerSafe.Contains(inner.OutpostBPosition), Is.True);
                Assert.That(inner.InnerSafe.Contains(new Vector3(inner.WalkableSpawn.x, 0f, inner.WalkableSpawn.z)), Is.True);
            }
        }

        [Test]
        public void DualAdjacentBridgesAndOneSealedSpokeUseAtlasIds()
        {
            InnerRealmWorldLayout layout = LoadLayout();
            string[] expected =
            {
                "bridge_ring_01_02_01",
                "bridge_ring_01_02_02",
                "bridge_ring_02_03_01",
                "bridge_ring_02_03_02",
                "bridge_ring_03_04_01",
                "bridge_ring_03_04_02",
                "bridge_ring_01_04_01",
                "bridge_ring_01_04_02",
                "bridge_center_ring_01_01",
                "bridge_center_ring_02_01",
                "bridge_center_ring_03_01",
                "bridge_center_ring_04_01"
            };

            CollectionAssert.AreEquivalent(expected, layout.Bridges.Select(bridge => bridge.Id).ToArray());
            foreach (WorldBridgeLayout spoke in layout.Bridges.Where(bridge => bridge.SealedEvent))
            {
                Assert.That(spoke.Id, Does.StartWith("bridge_center_"));
            }
        }

        [Test]
        public void GreyboxSpawnsVisibleCityAndInnerWallForWalkableRealm()
        {
            InnerRealmWorldLayout layout = LoadLayout();
            InnerRealmWorldBuildResult built = InnerRealmWorldGreyboxBuilder.Build(layout, "stonehold");
            _spawned.Add(built.Root.gameObject);

            Assert.That(built.Root.name, Does.Contain(InnerRealmWorldIds.TemporaryLabel));
            Assert.That(built.WalkableInner.InnerAtlasZoneId, Is.EqualTo("zone_inner_stonehold"));
            Assert.That(GameObject.Find("zone_inner_stonehold"), Is.Not.Null);
            Assert.That(GameObject.Find("wall_stonehold_inner"), Is.Not.Null);
            Assert.That(GameObject.Find("gate_stonehold_faultline"), Is.Not.Null);
            Assert.That(GameObject.Find(built.WalkableInner.CapitalPoiId), Is.Not.Null);
            Assert.That(GameObject.Find(built.WalkableInner.OutpostAPoiId), Is.Not.Null);
            Assert.That(GameObject.Find(built.WalkableInner.OutpostBPoiId), Is.Not.Null);
            Assert.That(GameObject.Find("zone_accordant_isle"), Is.Not.Null);
            Assert.That(GameObject.Find("bridge_ring_01_02_01"), Is.Not.Null);
            Assert.That(GameObject.Find("bridge_center_ring_01_01_seal"), Is.Not.Null);

            Collider wall = GameObject.Find("wall_stonehold_inner_north").GetComponent<Collider>();
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.enabled, Is.True);

            string hierarchy = DumpNames(built.Root);
            Assert.That(hierarchy, Does.Contain(InnerRealmWorldIds.DisplayCapital()));
            Assert.That(hierarchy, Does.Contain(InnerRealmWorldIds.DisplayOutpostA()));
            Assert.That(hierarchy, Does.Not.Contain("Stormwright"));
            Assert.That(hierarchy, Does.Not.Contain("Deep Forge"));
            Assert.That(hierarchy, Does.Not.Contain("World Tree"));
        }

        [Test]
        public void StructuralIdentityIsNotAHueSwap()
        {
            InnerRealmWorldLayout layout = LoadLayout();
            InnerRealmWorldBuildResult built = InnerRealmWorldGreyboxBuilder.Build(layout, "eldergrove");
            _spawned.Add(built.Root.gameObject);

            Assert.That(GameObject.Find("stonehold_basalt_spire"), Is.Not.Null);
            Assert.That(GameObject.Find("eldergrove_trunk_0"), Is.Not.Null);
            Assert.That(GameObject.Find("crownlands_grain_row_0"), Is.Not.Null);
            Assert.That(GameObject.Find("umbral_void_pit"), Is.Not.Null);
            Assert.That(built.WalkableInner.InnerSafe.Contains(new Vector3(built.PlayerSpawn.x, 0f, built.PlayerSpawn.z)), Is.True);
        }

        private static InnerRealmWorldLayout LoadLayout()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);
            Assert.That(result.IsAccepted, Is.True, string.Join("\n", result.Diagnostics.Select(item => item.Fingerprint)));
            return InnerRealmWorldLayout.FromSnapshot(result.Snapshot);
        }

        private static string DumpNames(Transform root)
        {
            var names = new List<string>();
            Collect(root, names);
            return string.Join("\n", names);
        }

        private static void Collect(Transform node, List<string> names)
        {
            names.Add(node.name);
            TextMesh[] labels = node.GetComponents<TextMesh>();
            for (int i = 0; i < labels.Length; i++)
            {
                names.Add(labels[i].text);
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Collect(node.GetChild(i), names);
            }
        }
    }
}
