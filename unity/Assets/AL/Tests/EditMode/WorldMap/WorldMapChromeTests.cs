using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Data.Catalogs.WorldAtlas;
using AL.UI.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.WorldMap
{
    public sealed class WorldMapChromeTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            WorldMapSession.ResetStatics();
            AL.Input.GameInput.SetGameplaySuppressed(false);
        }

        [TearDown]
        public void TearDown()
        {
            WorldMapSession.ResetStatics();
            AL.Input.GameInput.SetGameplaySuppressed(false);
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
        public void PresentationShowsFourCornerInnersWithoutPlayableWarzone()
        {
            WorldMapPresentation presentation = LoadPresentation();

            Assert.That(presentation.TopologyId, Is.EqualTo("topology_launch_world_ring_v001"));
            Assert.That(presentation.AtlasPlacementResolved, Is.False);
            Assert.That(presentation.PlacementStatus, Is.EqualTo(WorldMapIds.PlacementProposalStatus));
            Assert.That(presentation.DrawsPlayableWarzone, Is.False);
            Assert.That(presentation.Inners.Count, Is.EqualTo(4));
            Assert.That(presentation.AccordantIsle.Id, Is.EqualTo(WorldMapIds.AccordantIsleZoneId));
            Assert.That(presentation.AccordantIsle.Label, Is.EqualTo(WorldMapIds.DisplayAccordantIsle));

            string[] expectedZones =
            {
                "zone_inner_stonehold",
                "zone_inner_eldergrove",
                "zone_inner_crownlands",
                "zone_inner_umbral"
            };
            CollectionAssert.AreEquivalent(expectedZones, presentation.Inners.Select(inner => inner.InnerAtlasZoneId).ToArray());

            WorldMapInnerRealm stonehold = presentation.Inners.First(inner => inner.RealmId == "stonehold");
            WorldMapInnerRealm eldergrove = presentation.Inners.First(inner => inner.RealmId == "eldergrove");
            WorldMapInnerRealm crownlands = presentation.Inners.First(inner => inner.RealmId == "crownlands");
            WorldMapInnerRealm umbral = presentation.Inners.First(inner => inner.RealmId == "umbral");

            Assert.That(stonehold.Capital.Uv.X, Is.LessThan(0.5f));
            Assert.That(stonehold.Capital.Uv.Y, Is.LessThan(0.5f));
            Assert.That(eldergrove.Capital.Uv.X, Is.LessThan(0.5f));
            Assert.That(eldergrove.Capital.Uv.Y, Is.GreaterThan(0.5f));
            Assert.That(crownlands.Capital.Uv.X, Is.GreaterThan(0.5f));
            Assert.That(crownlands.Capital.Uv.Y, Is.GreaterThan(0.5f));
            Assert.That(umbral.Capital.Uv.X, Is.GreaterThan(0.5f));
            Assert.That(umbral.Capital.Uv.Y, Is.LessThan(0.5f));

            foreach (WorldMapSettlement settlement in presentation.VisibleSettlements())
            {
                Assert.That(presentation.ContainsWarzoneDestination(settlement.Id), Is.False, settlement.Id);
                Assert.That(
                    new[] { WorldMapIds.DisplayCapital, WorldMapIds.DisplayOutpostA, WorldMapIds.DisplayOutpostB },
                    Does.Contain(settlement.Label));
            }
        }

        [Test]
        public void OverlayOpensAndClosesFromSessionAndSharedMenuEntry()
        {
            WorldAtlasSnapshot snapshot = LoadSnapshot();
            WorldMapOverlay overlay = WorldMapOverlay.Ensure(snapshot);
            _spawned.Add(overlay.gameObject);

            GameObject mapVeil = FindDeep(overlay.transform, "WorldMap_Veil");
            GameObject menuVeil = FindDeep(overlay.transform, "SharedMenu_Veil");
            Assert.That(mapVeil, Is.Not.Null);
            Assert.That(menuVeil, Is.Not.Null);
            Assert.That(WorldMapSession.IsMapOpen, Is.False);
            Assert.That(mapVeil.activeSelf, Is.False);
            Assert.That(menuVeil.activeSelf, Is.False);

            WorldMapSession.OpenSharedMenu();
            Assert.That(WorldMapSession.IsMenuOpen, Is.True);
            Assert.That(menuVeil.activeSelf, Is.True);
            Assert.That(FindDeep(overlay.transform, WorldMapIds.MenuModuleWorldMap), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, WorldMapIds.MenuModuleKingdom).GetComponent<Button>().interactable, Is.False);

            WorldMapSession.OpenMapFromSharedMenu();
            Assert.That(WorldMapSession.IsMapOpen, Is.True);
            Assert.That(mapVeil.activeSelf, Is.True);
            Assert.That(menuVeil.activeSelf, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.True);

            Assert.That(FindDeep(overlay.transform, "zone_inner_stonehold"), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, "wall_stonehold_inner"), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, WorldMapIds.CapitalPoiId("zone_inner_stonehold")), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, WorldMapIds.AccordantIsleZoneId), Is.Not.Null);
            Assert.That(FindDeep(overlay.transform, "zone_warzone_stonehold_gate"), Is.Null);
            Assert.That(FindDeep(overlay.transform, "bridge_ring_01_02_01"), Is.Null);

            string dump = DumpText(overlay.transform);
            Assert.That(dump, Does.Contain(WorldMapIds.DisplayCapital));
            Assert.That(dump, Does.Contain(WorldMapIds.DisplayOutpostA));
            Assert.That(dump, Does.Contain("Stonehold"));
            Assert.That(dump, Does.Not.Contain("Crownspire"));
            Assert.That(dump, Does.Not.Contain("Stormwright"));
            Assert.That(dump, Does.Not.Contain("KingdomSceneController"));

            WorldMapSession.CloseMap();
            Assert.That(WorldMapSession.IsMapOpen, Is.False);
            Assert.That(mapVeil.activeSelf, Is.False);
            Assert.That(AL.Input.GameInput.GameplaySuppressed, Is.False);
        }

        [Test]
        public void HostRecognizes3DScenesOnlyAndWritesContactSheet()
        {
            Assert.That(WorldMapHost.IsWorldMapScene("ChampionArena"), Is.True);
            Assert.That(WorldMapHost.IsWorldMapScene("InnerRealmWorld"), Is.True);
            Assert.That(WorldMapHost.IsWorldMapScene("Kingdom"), Is.False);
            Assert.That(WorldMapHost.IsWorldMapScene("Boot"), Is.False);

            string path = Path.Combine(Application.dataPath, "../Logs/t_9d7be35a-world-map.png");
            string written = WorldMapContactSheet.WritePng(LoadPresentation(), Path.GetFullPath(path));
            Assert.That(File.Exists(written), Is.True);
            Assert.That(new FileInfo(written).Length, Is.GreaterThan(8 * 1024));
        }

        private static WorldMapPresentation LoadPresentation()
        {
            return WorldMapPresentation.FromSnapshot(LoadSnapshot());
        }

        private static WorldAtlasSnapshot LoadSnapshot()
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_world_atlas_narrative_catalog.json"));
            WorldAtlasLoadResult result = WorldAtlasTopologyLoader.Validate(bytes);
            Assert.That(result.IsAccepted, Is.True, string.Join("\n", result.Diagnostics.Select(item => item.Fingerprint)));
            return result.Snapshot;
        }

        private static GameObject FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string DumpText(Transform root)
        {
            var parts = new List<string>();
            Collect(root, parts);
            return string.Join("\n", parts);
        }

        private static void Collect(Transform node, List<string> parts)
        {
            parts.Add(node.name);
            Text[] texts = node.GetComponents<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                parts.Add(texts[i].text);
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Collect(node.GetChild(i), parts);
            }
        }
    }
}
