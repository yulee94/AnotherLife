using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Data.Catalogs.WorldAtlas;
using AL.UI.DesignSystem;
using AL.UI.WorldMap;
using AL.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.WorldMap
{
    public sealed class MainQuestMapMarkerTests
    {
        private WorldAtlasSnapshot _snapshot;
        private MainQuestMapMarkerCatalog _catalog;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            MainQuestMapSession.ResetForTests();
            ProgressiveMapSession.ResetForTests();
            WorldMapSession.ResetStatics();
            _snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            _catalog = MainQuestMapMarkerCatalog.LoadCanonical();
        }

        [TearDown]
        public void TearDown()
        {
            MainQuestMapSession.ResetForTests();
            ProgressiveMapSession.ResetForTests();
            WorldMapSession.ResetStatics();
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
        public void CurrentObjectiveIdResolvesToExactlyOneInnerRealmMarker()
        {
            IReadOnlyList<MainQuestMapMarker> markers =
                MainQuestMapMarkerResolver.ResolveCurrent(
                    _snapshot,
                    _catalog,
                    ProofOfWorthIds.OmenTalkObjectiveId,
                    RealmId.Crownlands,
                    ProofOfWorthCopy.OmenTalkObjective);

            Assert.That(markers.Count, Is.EqualTo(1));
            MainQuestMapMarker marker = markers.Single();
            Assert.That(marker.ObjectiveId, Is.EqualTo(ProofOfWorthIds.OmenTalkObjectiveId));
            Assert.That(marker.ZoneId, Is.EqualTo("zone_inner_crownlands"));
            Assert.That(marker.MarkerId, Is.EqualTo(InnerRealmWorldIds.CapitalPoiId(marker.ZoneId)));
            Assert.That(marker.WhatToDo, Is.EqualTo(ProofOfWorthCopy.OmenTalkObjective));
            Assert.That(marker.IsInnerRealm, Is.True);
        }

        [Test]
        public void EveryCatalogObjectiveResolvesInsideCommittedRealmSafeBounds()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(_snapshot);
            foreach (RealmId realm in new[]
                     {
                         RealmId.Stonehold,
                         RealmId.Eldergrove,
                         RealmId.Crownlands,
                         RealmId.Umbral
                     })
            {
                string realmId = InnerRealmWorldLayout.RealmCatalogId(realm);
                Assert.That(layout.TryGetInner(realmId, out InnerRealmSlotLayout inner), Is.True);

                foreach (string objectiveId in _catalog.ObjectiveIds)
                {
                    IReadOnlyList<MainQuestMapMarker> markers =
                        MainQuestMapMarkerResolver.ResolveCurrent(
                            _snapshot,
                            _catalog,
                            objectiveId,
                            realm,
                            "Do the current main-quest step.");

                    Assert.That(markers.Count, Is.EqualTo(1), objectiveId + " / " + realm);
                    MainQuestMapMarker marker = markers[0];
                    Assert.That(marker.ZoneId, Is.EqualTo(inner.InnerAtlasZoneId));
                    Assert.That(inner.InnerSafe.Contains(marker.WorldPosition), Is.True, marker.MarkerId);
                    Assert.That(KingdomWorldMapQuery.IsForbiddenId(marker.MarkerId), Is.False, marker.MarkerId);
                }
            }
        }

        [Test]
        public void UnknownAndOuterRealmObjectiveIdsNeverResolve()
        {
            string[] rejected =
            {
                "OBJ_ENTER_WARZONE",
                "zone_warzone_stonehold_gate",
                "gate_stonehold_faultline",
                "bridge_ring_01_02_01",
                "zone_accordant_isle",
                string.Empty
            };

            foreach (string objectiveId in rejected)
            {
                IReadOnlyList<MainQuestMapMarker> markers =
                    MainQuestMapMarkerResolver.ResolveCurrent(
                        _snapshot,
                        _catalog,
                        objectiveId,
                        RealmId.Stonehold,
                        "Do not expose outer destinations.");

                Assert.That(markers, Is.Empty, objectiveId);
            }
        }

        [Test]
        public void CatalogRejectsWrongAuthoritiesAndObjectiveSetDrift()
        {
            MethodInfo parse = typeof(MainQuestMapMarkerCatalog).GetMethod(
                "Parse",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(parse, Is.Not.Null, "Runtime catalog validation must be testable independently of disk I/O.");

            string path = Path.Combine(
                Application.dataPath,
                "AL/StreamingAssets/GameData/al_main_quest_map_marker_catalog.json");
            string canonical = File.ReadAllText(path);
            string wrongAuthority = canonical.Replace(
                "\"al_world_atlas_narrative_catalog\"",
                "\"wrong_world_atlas\"");
            string driftedObjectives = canonical.Replace(
                "\"OBJ_C1_ACCEPT_MARK\"",
                "\"OBJ_NOT_PROOF_OF_WORTH\"");

            Assert.That(
                () => InvokeParse(parse, wrongAuthority),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(
                () => InvokeParse(parse, driftedObjectives),
                Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void PackagedPlayerFallbackUsesStreamingAssetsGameData()
        {
            MethodInfo resolve = typeof(MainQuestMapMarkerCatalog).GetMethod(
                "ResolveFallbackPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null, "Packaged-player path selection must be testable.");

            string path = (string)resolve.Invoke(
                null,
                new object[]
                {
                    "C:/Build/AnotherLife_Data",
                    "C:/Build/AnotherLife_Data/StreamingAssets",
                    false
                });
            Assert.That(
                path.Replace('\\', '/'),
                Is.EqualTo("C:/Build/AnotherLife_Data/StreamingAssets/GameData/" + MainQuestMapMarkerCatalog.FileName));
        }

        [Test]
        public void MinimapShowsPlayerCurrentQuestAndOnlyCommittedInnerRealmPois()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(_snapshot);
            Assert.That(layout.TryGetInner("stonehold", out InnerRealmSlotLayout inner), Is.True);
            var player = new GameObject("PlayerForMinimap");
            player.transform.position = inner.CapitalPosition;
            _spawned.Add(player);

            MainQuestMapSession.Publish(
                ProofOfWorthIds.OmenArenaObjectiveId,
                RealmId.Stonehold,
                ProofOfWorthCopy.OmenArenaObjective);
            InnerRealmMinimapOverlay minimap =
                InnerRealmMinimapOverlay.Ensure(_snapshot, player.transform);
            _spawned.Add(minimap.gameObject);

            CollectionAssert.AreEquivalent(
                KingdomWorldMapQuery.Enumerate(_snapshot, RealmId.Stonehold).MarkerIds,
                minimap.VisibleMarkerIds);
            Assert.That(minimap.CurrentQuestMarkers.Count, Is.EqualTo(1));
            Assert.That(minimap.CurrentQuestMarkers[0].MarkerId, Is.EqualTo(inner.OutpostAPoiId));
            Assert.That(FindDeep(minimap.transform, InnerRealmMinimapOverlay.PlayerMarkerName), Is.Not.Null);
            Assert.That(FindDeep(minimap.transform, "MinimapQuestMarker_" + inner.OutpostAPoiId), Is.Not.Null);

            string dump = Dump(minimap.transform);
            Assert.That(dump, Does.Contain("Capital"));
            Assert.That(dump, Does.Contain("Area I"));
            Assert.That(dump, Does.Contain("Area II"));
            Assert.That(dump, Does.Contain("YOU"));
            Assert.That(dump, Does.Contain("MAIN QUEST"));
            Assert.That(dump, Does.Not.Contain("warzone"));
            Assert.That(dump, Does.Not.Contain("zone_inner_eldergrove"));
            Assert.That(dump, Does.Not.Contain("zone_inner_crownlands"));
            Assert.That(dump, Does.Not.Contain("zone_inner_umbral"));
        }

        [Test]
        public void ReducedEffectsKeepObjectiveStaticAndLargeTextScalesMinimapChrome()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(_snapshot);
            Assert.That(layout.TryGetInner("stonehold", out InnerRealmSlotLayout inner), Is.True);
            var player = new GameObject("AccessibleMinimapPlayer");
            player.transform.position = inner.CapitalPosition;
            _spawned.Add(player);
            ProgressiveMapSession.ConfigureAccessibility(
                new MapAccessibilityProfile(
                    new UiAccessibilitySettings(2f, true, true, true),
                    highContrast: true));
            MainQuestMapSession.Publish(
                ProofOfWorthIds.OmenArenaObjectiveId,
                RealmId.Stonehold,
                ProofOfWorthCopy.OmenArenaObjective);

            InnerRealmMinimapOverlay minimap =
                InnerRealmMinimapOverlay.Ensure(_snapshot, player.transform);
            _spawned.Add(minimap.gameObject);
            GameObject objective = FindDeep(
                minimap.transform,
                "MinimapQuestMarker_" + inner.OutpostAPoiId);
            Assert.That(objective, Is.Not.Null);
            MethodInfo updatePulse = typeof(InnerRealmMinimapOverlay).GetMethod(
                "UpdateQuestPulse",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updatePulse, Is.Not.Null);

            updatePulse.Invoke(minimap, null);

            Assert.That(objective.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(objective.GetComponent<Image>().color.a, Is.EqualTo(1f));
            Text title = FindDeep(minimap.transform, "MinimapTitle").GetComponent<Text>();
            Assert.That(title.fontSize, Is.EqualTo(32));
            Assert.That(title.GetComponent<UiScalableText>(), Is.Not.Null);
        }

        [Test]
        public void MinimapYouLabelFollowsMovingPlayerMarker()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(_snapshot);
            Assert.That(layout.TryGetInner("stonehold", out InnerRealmSlotLayout inner), Is.True);
            var player = new GameObject("MovingPlayerForMinimap");
            player.transform.position = inner.CapitalPosition;
            _spawned.Add(player);

            MainQuestMapSession.Publish(
                ProofOfWorthIds.OmenArenaObjectiveId,
                RealmId.Stonehold,
                ProofOfWorthCopy.OmenArenaObjective);
            InnerRealmMinimapOverlay minimap =
                InnerRealmMinimapOverlay.Ensure(_snapshot, player.transform);
            _spawned.Add(minimap.gameObject);

            player.transform.position = inner.OutpostBPosition;
            MethodInfo updatePlayer = typeof(InnerRealmMinimapOverlay).GetMethod(
                "UpdatePlayerMarker",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updatePlayer, Is.Not.Null);
            updatePlayer.Invoke(minimap, null);

            RectTransform playerMarker =
                FindDeep(minimap.transform, InnerRealmMinimapOverlay.PlayerMarkerName)
                    .GetComponent<RectTransform>();
            RectTransform playerLabel =
                FindDeep(minimap.transform, "MinimapPlayerLabel")
                    .GetComponent<RectTransform>();
            Assert.That(playerLabel.anchorMin, Is.EqualTo(playerMarker.anchorMin));
            Assert.That(playerLabel.anchorMax, Is.EqualTo(playerMarker.anchorMax));
        }

        [Test]
        public void FullWorldMapShowsCurrentMarkerAndWhatToDoLabel()
        {
            MainQuestMapSession.Publish(
                ProofOfWorthIds.RestoreCovenantObjectiveId,
                RealmId.Eldergrove,
                ProofOfWorthCopy.C1RestoreCovenant);
            WorldMapOverlay overlay = EnsureStandaloneOverlay(_snapshot);
            _spawned.Add(overlay.gameObject);
            WorldMapSession.OpenMap();

            IReadOnlyList<MainQuestMapMarker> markers =
                MainQuestMapMarkerResolver.ResolveCurrent(
                    _snapshot,
                    _catalog,
                    ProofOfWorthIds.RestoreCovenantObjectiveId,
                    RealmId.Eldergrove,
                    ProofOfWorthCopy.C1RestoreCovenant);
            Assert.That(markers.Count, Is.EqualTo(1));
            Assert.That(
                FindDeep(overlay.transform, "WorldMapQuestMarker_" + markers[0].MarkerId),
                Is.Not.Null);
            Assert.That(Dump(overlay.transform), Does.Contain(ProofOfWorthCopy.C1RestoreCovenant));
        }

        private static WorldMapOverlay EnsureStandaloneOverlay(WorldAtlasSnapshot snapshot)
        {
            WorldMapOverlay overlay = WorldMapOverlay.Ensure(snapshot);
            MethodInfo activate = typeof(WorldMapOverlay).GetMethod(
                "SetPresentationAuthority",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activate, Is.Not.Null);
            activate.Invoke(overlay, new object[] { true });
            return overlay;
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

        private static MainQuestMapMarkerCatalog InvokeParse(MethodInfo parse, string payload)
        {
            try
            {
                return (MainQuestMapMarkerCatalog)parse.Invoke(null, new object[] { payload, "test" });
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }

        private static string Dump(Transform root)
        {
            var values = new List<string> { root.name };
            Text[] labels = root.GetComponents<Text>();
            for (int i = 0; i < labels.Length; i++)
            {
                values.Add(labels[i].text);
            }

            for (int i = 0; i < root.childCount; i++)
            {
                values.Add(Dump(root.GetChild(i)));
            }

            return string.Join("\n", values);
        }
    }
}
