using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode;
using AL.Core.Scenes;
using AL.Data.Catalogs.WorldAtlas;
using AL.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.World
{
    public sealed class FirstSessionInnerRealmSpawnTests
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
        public void FourRealmsResolveDistinctInnerCapitalSpawnIds()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            FirstSessionInnerRealmSpawn[] spawns = FirstSessionInnerRealmSpawn.ResolveAllFour(snapshot);

            Assert.That(spawns.Length, Is.EqualTo(4));
            CollectionAssert.AreEqual(
                new[] { "stonehold", "eldergrove", "crownlands", "umbral" },
                spawns.Select(spawn => spawn.RealmId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    FirstSessionInnerRealmSpawn.StoneholdZoneId,
                    FirstSessionInnerRealmSpawn.EldergroveZoneId,
                    FirstSessionInnerRealmSpawn.CrownlandsZoneId,
                    FirstSessionInnerRealmSpawn.UmbralZoneId
                },
                spawns.Select(spawn => spawn.InnerAtlasZoneId).ToArray());
            CollectionAssert.AllItemsAreUnique(spawns.Select(spawn => spawn.InnerAtlasZoneId).ToArray());
            CollectionAssert.AllItemsAreUnique(spawns.Select(spawn => spawn.CapitalPoiId).ToArray());
            CollectionAssert.AllItemsAreUnique(spawns.Select(spawn => spawn.Position).ToArray());

            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(snapshot);
            foreach (FirstSessionInnerRealmSpawn spawn in spawns)
            {
                Assert.That(FirstSessionInnerRealmSpawn.IsForbiddenDestination("ChampionArena", spawn.InnerAtlasZoneId), Is.False);
                Assert.That(FirstSessionInnerRealmSpawn.IsForbiddenDestination("Kingdom", spawn.InnerAtlasZoneId), Is.True);
                Assert.That(FirstSessionInnerRealmSpawn.IsForbiddenDestination("ChampionArena", FirstSessionInnerRealmSpawn.AccordantIsleZoneId), Is.True);
                Assert.That(FirstSessionInnerRealmSpawn.IsForbiddenDestination("ChampionArena", FirstSessionInnerRealmSpawn.WarzoneCenterId), Is.True);
                Assert.That(FirstSessionInnerRealmSpawn.IsForbiddenDestination("ChampionArena", "zone_outer_stonehold"), Is.True);
                Assert.That(spawn.CapitalPoiId, Is.EqualTo(InnerRealmWorldIds.CapitalPoiId(spawn.InnerAtlasZoneId)));
                Assert.That(spawn.TemporaryLabel, Is.EqualTo(InnerRealmWorldIds.TemporaryLabel));
                Assert.That(spawn.DisplayCapital, Is.EqualTo("Capital"));
                Assert.That(spawn.PlacementStatus, Is.EqualTo(InnerRealmWorldIds.PlacementProposalStatus));
                Assert.That(spawn.ReportLine, Does.Contain(spawn.InnerAtlasZoneId));
                Assert.That(spawn.ReportLine, Does.Contain(spawn.CapitalPoiId));
                Assert.That(layout.TryGetInner(spawn.RealmId, out InnerRealmSlotLayout inner), Is.True);
                Assert.That(spawn.IsInsideInnerSafe(inner), Is.True, spawn.InnerAtlasZoneId);
                Assert.That(Vector3.Distance(new Vector3(spawn.Position.x, 0f, spawn.Position.z), new Vector3(inner.CapitalPosition.x, 0f, inner.CapitalPosition.z)), Is.LessThan(2.5f));
                Assert.That(new Vector3(spawn.Position.x, 0f, spawn.Position.z).magnitude, Is.GreaterThan(80f), "must not spawn at world origin / warzone");
            }
        }

        [Test]
        public void GreyboxBuildPlacesEachRealmOnItsUnnamedCapital()
        {
            WorldAtlasSnapshot snapshot = FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot();
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(snapshot);
            string[] realms = { "stonehold", "eldergrove", "crownlands", "umbral" };
            string[] identityMarkers =
            {
                "stonehold_basalt_spire",
                "eldergrove_trunk_0",
                "crownlands_grain_row_0",
                "umbral_void_pit"
            };

            for (int i = 0; i < realms.Length; i++)
            {
                FirstSessionInnerRealmSpawn spawn = FirstSessionInnerRealmSpawn.Resolve(realms[i], snapshot);
                InnerRealmWorldBuildResult built = InnerRealmWorldGreyboxBuilder.Build(layout, realms[i]);
                _spawned.Add(built.Root.gameObject);

                Assert.That(built.Root.name, Does.Contain(InnerRealmWorldIds.TemporaryLabel));
                Assert.That(built.WalkableInner.InnerAtlasZoneId, Is.EqualTo(spawn.InnerAtlasZoneId));
                Assert.That(built.PlayerSpawn, Is.EqualTo(spawn.Position));
                Assert.That(GameObject.Find(spawn.InnerAtlasZoneId), Is.Not.Null);
                Assert.That(GameObject.Find(spawn.CapitalPoiId), Is.Not.Null);
                Assert.That(GameObject.Find(identityMarkers[i]), Is.Not.Null);
                Assert.That(GameObject.Find(FirstSessionInnerRealmSpawn.AccordantIsleZoneId), Is.Not.Null);
                Assert.That(GameObject.Find(FirstSessionInnerRealmSpawn.WarzoneCenterId), Is.Not.Null);
                Assert.That(built.WalkableInner.InnerSafe.Contains(new Vector3(built.PlayerSpawn.x, 0f, built.PlayerSpawn.z)), Is.True);

                Object.DestroyImmediate(built.Root.gameObject);
                _spawned.RemoveAt(_spawned.Count - 1);
            }
        }

        [Test]
        public void FirstSessionStaysOffKingdomAndNamesTheGreybox()
        {
            Assert.AreEqual("ChampionArena", FirstSessionChampionStart.DestinationSceneName);
            Assert.AreNotEqual(FirstSessionInnerRealmSpawn.KingdomSceneName, FirstSessionChampionStart.DestinationSceneName);
            Assert.That(FirstSessionChampionStart.EnvironmentRootName, Is.EqualTo("InnerRealmWorld_TEMPORARY"));
            Assert.That(FirstSessionChampionStart.TemporaryPlaqueCopy, Does.Contain("TEMPORARY"));
            Assert.That(FirstSessionChampionStart.TemporaryPlaqueCopy, Does.Contain("Capital"));
            Assert.That(FirstSessionChampionStart.LandingFeedCopy, Does.Contain("TEMPORARY"));
            Assert.That(FirstSessionChampionStart.LandingFeedCopy, Does.Not.Contain("citadel"));

            Assert.IsTrue(
                ProductionSceneDescriptor.TryGetById(
                    ProductionSceneDescriptor.CharacterCreationSceneId,
                    out ProductionSceneRecord create));
            SceneTransition arena = null;
            foreach (SceneTransition transition in create.TransitionTargets)
            {
                if (transition.TargetSceneId == ProductionSceneDescriptor.ChampionArenaSceneId)
                {
                    arena = transition;
                    break;
                }
            }

            Assert.NotNull(arena);
            Assert.AreEqual("ChampionArena", arena.SerializedValue);
            Assert.AreNotEqual("Kingdom", arena.SerializedValue);
        }
    }
}
