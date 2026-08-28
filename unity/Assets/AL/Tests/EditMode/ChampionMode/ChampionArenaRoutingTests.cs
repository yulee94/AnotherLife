using System.Linq;
using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.World;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionArenaRoutingTests
    {
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void AtlasObjectiveMarkersStayOffTheFirstSessionQuestRoute(
            bool firstSessionLanding,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                ChampionArenaSceneController.ShouldCreateWorldObjectiveMarkers(
                    firstSessionLanding));
        }

        [TestCase(ProofOfWorthPhase.OmenOffered, FirstSessionRouteTarget.CaptainValerius)]
        [TestCase(ProofOfWorthPhase.OmenTalk, FirstSessionRouteTarget.CaptainValerius)]
        [TestCase(ProofOfWorthPhase.OmenArena, FirstSessionRouteTarget.GuardianTrial)]
        [TestCase(ProofOfWorthPhase.OmenReport, FirstSessionRouteTarget.CaptainValerius)]
        [TestCase(ProofOfWorthPhase.C1MeetGuide, FirstSessionRouteTarget.CaptainValerius)]
        [TestCase(ProofOfWorthPhase.C1RestoreCovenant, FirstSessionRouteTarget.CovenantSite)]
        [TestCase(ProofOfWorthPhase.C1FaceGuardian, FirstSessionRouteTarget.GuardianTrial)]
        [TestCase(ProofOfWorthPhase.C1AcceptMark, FirstSessionRouteTarget.LordshipDestination)]
        public void EveryActiveFirstSessionPhaseResolvesAnAuthoredPhysicalDestination(
            ProofOfWorthPhase phase,
            FirstSessionRouteTarget expected)
        {
            Assert.That(
                ProofOfWorthDirector.TryResolveRouteTarget(phase, out FirstSessionRouteTarget target),
                Is.True);
            Assert.That(target, Is.EqualTo(expected));
        }

        [Test]
        public void AuthoredRouteReturnsOrderedWaypointsWithoutLongOrOffRoadSegments()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            Assert.That(
                catalog.TryResolveFirstSessionRealm(
                    AL.Core.RealmId.Crownlands,
                    out GameObject prefab),
                Is.True);
            GameObject realm = Object.Instantiate(prefab);
            try
            {
                FirstSessionAuthoredRealmRoute route =
                    realm.GetComponent<FirstSessionAuthoredRealmRoute>();
                Vector3 current = route.PlayerSpawn.position;
                int steps = 0;
                while (Vector3.Distance(current, route.LordshipDestination.position) > 2f)
                {
                    Assert.That(
                        route.TryGetNextWaypoint(
                            current,
                            route.LordshipDestination,
                            out Transform next),
                        Is.True);
                    Assert.That(next.position.z, Is.GreaterThan(current.z));
                    Assert.That(Vector3.Distance(current, next.position), Is.LessThanOrEqualTo(16f));
                    Assert.That(Mathf.Abs(next.position.x), Is.LessThanOrEqualTo(6f));
                    current = next.position;
                    Assert.That(++steps, Is.LessThanOrEqualTo(8));
                }
            }
            finally
            {
                Object.DestroyImmediate(realm);
            }
        }

        [Test]
        public void AuthoredRouteDoesNotReverseWhenChampionIsBetweenWaypoints()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            Assert.That(
                catalog.TryResolveFirstSessionRealm(
                    AL.Core.RealmId.Crownlands,
                    out GameObject prefab),
                Is.True);
            GameObject realm = Object.Instantiate(prefab);
            try
            {
                FirstSessionAuthoredRealmRoute route =
                    realm.GetComponent<FirstSessionAuthoredRealmRoute>();
                Vector3 between = route.PlayerSpawn.position + Vector3.forward * 4.1f;
                Assert.That(
                    route.TryGetNextWaypoint(
                        between,
                        route.GuardianTrial,
                        out Transform next),
                    Is.True);
                Assert.That(next.name, Is.EqualTo("RouteWaypoint_01"));
                Assert.That(next.position.z, Is.GreaterThan(between.z));
            }
            finally
            {
                Object.DestroyImmediate(realm);
            }
        }

        [Test]
        public void FirstSessionObjectiveBeaconIsVisibleButNeverBlocksTheAuthoredRoad()
        {
            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            Assert.That(
                catalog.TryResolveFirstSessionRealm(
                    AL.Core.RealmId.Crownlands,
                    out GameObject prefab),
                Is.True);
            GameObject realm = Object.Instantiate(prefab);
            var player = new GameObject("RouteBeaconPlayer");
            var directorObject = new GameObject("RouteBeaconDirector");
            try
            {
                FirstSessionAuthoredRealmRoute route =
                    realm.GetComponent<FirstSessionAuthoredRealmRoute>();
                player.transform.position = route.PlayerSpawn.position;
                ProofOfWorthDirector director =
                    directorObject.AddComponent<ProofOfWorthDirector>();
                director.EnsureReady(null, player.transform, AL.Core.RealmId.Crownlands);

                GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
                Assert.That(markerRoot, Is.Not.Null);
                Assert.That(markerRoot.GetComponentInChildren<LineRenderer>(true), Is.Not.Null);
                Assert.That(markerRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
                LineRenderer beam = markerRoot.GetComponentsInChildren<LineRenderer>(true)
                    .Single(line => line.name == "ObjectiveBeam");
                Assert.That(beam.GetPosition(0).y, Is.GreaterThanOrEqualTo(2.4f));
                Assert.That(beam.GetPosition(1).y, Is.GreaterThan(beam.GetPosition(0).y));
            }
            finally
            {
                ProofOfWorthDirector.ResetForTests();
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(directorObject);
                Object.DestroyImmediate(realm);
                GameObject markerRoot = GameObject.Find(ProofOfWorthDirector.MarkerRootName);
                if (markerRoot != null)
                {
                    Object.DestroyImmediate(markerRoot);
                }
            }
        }
    }
}
