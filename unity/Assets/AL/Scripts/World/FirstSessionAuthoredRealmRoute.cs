using System;
using UnityEngine;

namespace AL.World
{
    public enum FirstSessionRouteTarget
    {
        PlayerSpawn = 0,
        CaptainValerius = 1,
        GuardianTrial = 2,
        CovenantSite = 3,
        LordshipDestination = 4
    }

    /// <summary>
    /// Typed anchor map carried by each catalog-backed first-session realm prefab.
    /// Runtime quest systems resolve physical destinations from this authored asset
    /// instead of creating player-relative temporary markers.
    /// </summary>
    public sealed class FirstSessionAuthoredRealmRoute : MonoBehaviour
    {
        public const string LandscapeName = "AuthoredRealmLandscape";
        public const string QuestRoadName = "AuthoredQuestRoad";
        public const string SpawnPlazaName = "AuthoredSpawnPlaza";
        public const string PlayerSpawnAnchorName = "RouteAnchor_PlayerSpawn";
        public const string CaptainValeriusAnchorName = "RouteAnchor_CaptainValerius";
        public const string GuardianTrialAnchorName = "RouteAnchor_GuardianTrial";
        public const string CovenantSiteAnchorName = "RouteAnchor_CovenantSite";
        public const string LordshipDestinationAnchorName = "RouteAnchor_LordshipDestination";
        public const string WaypointPrefix = "RouteWaypoint_";

        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform captainValerius;
        [SerializeField] private Transform guardianTrial;
        [SerializeField] private Transform covenantSite;
        [SerializeField] private Transform lordshipDestination;
        [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();

        public Transform PlayerSpawn => playerSpawn;
        public Transform CaptainValerius => captainValerius;
        public Transform GuardianTrial => guardianTrial;
        public Transform CovenantSite => covenantSite;
        public Transform LordshipDestination => lordshipDestination;
        public Transform[] Waypoints => waypoints;

        public void Bind(
            Transform playerSpawnAnchor,
            Transform captainValeriusAnchor,
            Transform guardianTrialAnchor,
            Transform covenantSiteAnchor,
            Transform lordshipDestinationAnchor,
            Transform[] routeWaypoints)
        {
            playerSpawn = playerSpawnAnchor;
            captainValerius = captainValeriusAnchor;
            guardianTrial = guardianTrialAnchor;
            covenantSite = covenantSiteAnchor;
            lordshipDestination = lordshipDestinationAnchor;
            waypoints = routeWaypoints ?? Array.Empty<Transform>();
        }

        public bool TryGetAnchor(FirstSessionRouteTarget target, out Transform anchor)
        {
            switch (target)
            {
                case FirstSessionRouteTarget.PlayerSpawn:
                    anchor = playerSpawn;
                    break;
                case FirstSessionRouteTarget.CaptainValerius:
                    anchor = captainValerius;
                    break;
                case FirstSessionRouteTarget.GuardianTrial:
                    anchor = guardianTrial;
                    break;
                case FirstSessionRouteTarget.CovenantSite:
                    anchor = covenantSite;
                    break;
                case FirstSessionRouteTarget.LordshipDestination:
                    anchor = lordshipDestination;
                    break;
                default:
                    anchor = null;
                    break;
            }

            return anchor != null;
        }

        public bool HasCompleteRoute()
        {
            return playerSpawn != null && captainValerius != null &&
                   guardianTrial != null && covenantSite != null &&
                   lordshipDestination != null && waypoints != null &&
                   waypoints.Length >= 5;
        }

        public bool TryGetNextWaypoint(
            Vector3 currentPosition,
            Transform destination,
            out Transform waypoint)
        {
            waypoint = null;
            if (!HasCompleteRoute() || destination == null)
            {
                return false;
            }

            int destinationIndex = 0;
            float destinationDistance = float.MaxValue;
            for (int index = 0; index < waypoints.Length; index++)
            {
                float distance = HorizontalSqrDistance(
                    waypoints[index].position,
                    destination.position);
                if (distance < destinationDistance)
                {
                    destinationDistance = distance;
                    destinationIndex = index;
                }
            }

            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index <= destinationIndex; index++)
            {
                float distance = HorizontalSqrDistance(
                    waypoints[index].position,
                    currentPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            }

            if (nearestIndex >= destinationIndex)
            {
                waypoint = waypoints[nearestIndex];
                return HorizontalSqrDistance(currentPosition, waypoint.position) > 0.25f;
            }

            Vector3 segmentStart = waypoints[nearestIndex].position;
            Vector3 segmentEnd = waypoints[nearestIndex + 1].position;
            segmentStart.y = 0f;
            segmentEnd.y = 0f;
            Vector3 current = currentPosition;
            current.y = 0f;
            Vector3 segment = segmentEnd - segmentStart;
            float projection = Vector3.Dot(current - segmentStart, segment) /
                               segment.sqrMagnitude;
            Vector3 closest = segmentStart + segment * Mathf.Clamp01(projection);
            bool onCurrentSegment = projection >= 0f &&
                                    (current - closest).sqrMagnitude <= 16f;
            waypoint = onCurrentSegment
                ? waypoints[nearestIndex + 1]
                : waypoints[nearestIndex];
            return HorizontalSqrDistance(currentPosition, waypoint.position) > 0.25f;
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }
    }
}
