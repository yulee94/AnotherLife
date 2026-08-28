using System.Collections;
using AL.ChampionMode.Control;
using AL.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode.World
{
    public sealed class ChampionTerrainBoundedActionMovementTests
    {
        private GameObject _terrainObject;
        private TerrainData _terrainData;
        private GameObject _championObject;
        private GameObject _audioListenerObject;
        private float _originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            if (Object.FindObjectOfType<AudioListener>() == null)
            {
                _audioListenerObject = new GameObject("ActionMovementTestAudioListener");
                _audioListenerObject.AddComponent<AudioListener>();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = _originalTimeScale;
            if (_championObject != null)
            {
                Object.Destroy(_championObject);
            }

            if (_audioListenerObject != null)
            {
                Object.Destroy(_audioListenerObject);
            }

            if (_terrainObject != null)
            {
                Object.Destroy(_terrainObject);
            }

            if (_terrainData != null)
            {
                Object.Destroy(_terrainData);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AttackLungeThenDodgeNearTerrainEdgeNeverLeavesPhysicalSupport()
        {
            TerrainCollider support = CreateFiniteTerrain();
            ChampionController controller = CreateChampion();
            CharacterController movement =
                controller.GetComponent<CharacterController>();
            Vector3 verifiedSpawn = GroundedRootPosition(
                support,
                movement,
                Vector3.zero);

            Assert.That(
                controller.TryConfigureTerrainSafety(support, verifiedSpawn),
                Is.True,
                "The action-movement regression requires verified TerrainCollider authority.");

            float combinedActionDistance = 4.5f;
            Vector3 nearEdge = GroundedRootPosition(
                support,
                movement,
                new Vector3(
                    support.bounds.max.x - combinedActionDistance,
                    0f,
                    support.bounds.center.z));
            controller.TeleportTo(nearEdge);
            controller.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Physics.SyncTransforms();
            yield return null;

            int recoveriesBeforeActions = controller.TerrainSafetyRecoveryCount;
            Assert.That(controller.RequestBasicAttack(), Is.True);
            yield return new WaitForSeconds(0.15f);
            controller.RequestDodge();

            bool observedUnsupportedPosition = false;
            float maximumCenterX = float.NegativeInfinity;
            float observationStarted = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - observationStarted < 0.40f)
            {
                yield return null;
                Vector3 center = controller.transform.TransformPoint(movement.center);
                maximumCenterX = Mathf.Max(maximumCenterX, center.x);
                bool capsuleInsideHorizontalSupport =
                    center.x + movement.radius <= support.bounds.max.x + 0.02f;
                bool terrainBelow = support.Raycast(
                    new Ray(center + Vector3.up * 4f, Vector3.down),
                    out _,
                    10f);
                observedUnsupportedPosition |=
                    !capsuleInsideHorizontalSupport || !terrainBelow;
            }

            Assert.That(
                observedUnsupportedPosition,
                Is.False,
                "Attack lunge plus dodge escaped the finite TerrainCollider before recovery. " +
                "maximumCenterX=" + maximumCenterX +
                ", terrainMaxX=" + support.bounds.max.x +
                ", radius=" + movement.radius + ".");
            Assert.That(
                controller.TerrainSafetyRecoveryCount,
                Is.EqualTo(recoveriesBeforeActions),
                "Action movement must be constrained at the physical edge, not repaired by respawn.");
        }

        [UnityTest]
        public IEnumerator AttackAndDodgeTowardEveryEdgeAndCornerStayOnTerrain()
        {
            TerrainCollider support = CreateFiniteTerrain();
            ChampionController controller = CreateChampion();
            CharacterController movement =
                controller.GetComponent<CharacterController>();
            Vector3 verifiedSpawn = GroundedRootPosition(
                support,
                movement,
                Vector3.zero);
            Assert.That(
                controller.TryConfigureTerrainSafety(support, verifiedSpawn),
                Is.True);

            Vector3[] directions =
            {
                Vector3.right,
                Vector3.left,
                Vector3.forward,
                Vector3.back,
                new Vector3(1f, 0f, 1f).normalized,
                new Vector3(1f, 0f, -1f).normalized,
                new Vector3(-1f, 0f, 1f).normalized,
                new Vector3(-1f, 0f, -1f).normalized
            };

            const float actionApproachDistance = 4.5f;
            for (int directionIndex = 0;
                 directionIndex < directions.Length;
                 directionIndex++)
            {
                Vector3 direction = directions[directionIndex];
                Vector3 horizontalStart = support.bounds.center;
                if (direction.x > 0.01f)
                {
                    horizontalStart.x = support.bounds.max.x - actionApproachDistance;
                }
                else if (direction.x < -0.01f)
                {
                    horizontalStart.x = support.bounds.min.x + actionApproachDistance;
                }

                if (direction.z > 0.01f)
                {
                    horizontalStart.z = support.bounds.max.z - actionApproachDistance;
                }
                else if (direction.z < -0.01f)
                {
                    horizontalStart.z = support.bounds.min.z + actionApproachDistance;
                }

                controller.TeleportTo(GroundedRootPosition(
                    support,
                    movement,
                    horizontalStart));
                controller.transform.rotation = Quaternion.LookRotation(direction);
                Physics.SyncTransforms();
                yield return null;

                int recoveriesBeforeActions =
                    controller.TerrainSafetyRecoveryCount;
                Assert.That(
                    controller.RequestBasicAttack(),
                    Is.True,
                    "Attack was not ready for boundary direction " + direction + ".");
                yield return new WaitForSeconds(0.15f);
                controller.RequestDodge();

                bool escapedSupport = false;
                float observationStarted = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - observationStarted < 0.45f)
                {
                    yield return null;
                    Vector3 center = controller.transform.TransformPoint(
                        movement.center);
                    bool capsuleInside =
                        center.x - movement.radius >= support.bounds.min.x - 0.02f &&
                        center.x + movement.radius <= support.bounds.max.x + 0.02f &&
                        center.z - movement.radius >= support.bounds.min.z - 0.02f &&
                        center.z + movement.radius <= support.bounds.max.z + 0.02f;
                    bool terrainBelow = support.Raycast(
                        new Ray(center + Vector3.up * 4f, Vector3.down),
                        out _,
                        10f);
                    escapedSupport |= !capsuleInside || !terrainBelow;
                }

                Assert.That(
                    escapedSupport,
                    Is.False,
                    "Attack+dodge escaped terrain toward " + direction + ".");
                Assert.That(
                    controller.TerrainSafetyRecoveryCount,
                    Is.EqualTo(recoveriesBeforeActions),
                    "Boundary containment toward " + direction +
                    " must not depend on recovery teleportation.");
                yield return new WaitForSeconds(0.1f);
            }
        }

        private TerrainCollider CreateFiniteTerrain()
        {
            const int resolution = 33;
            _terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                size = new Vector3(12f, 2f, 12f)
            };
            _terrainData.SetHeights(0, 0, new float[resolution, resolution]);
            _terrainObject = Terrain.CreateTerrainGameObject(_terrainData);
            _terrainObject.name = "FiniteActionMovementTerrain";
            _terrainObject.transform.position = new Vector3(-6f, 0f, -6f);
            TerrainCollider support = _terrainObject.GetComponent<TerrainCollider>();
            support.terrainData = _terrainData;
            return support;
        }

        private ChampionController CreateChampion()
        {
            _championObject = new GameObject("TerrainBoundedActionChampion");
            CharacterController movement =
                _championObject.AddComponent<CharacterController>();
            movement.center = Vector3.zero;
            movement.height = 2f;
            movement.radius = 0.45f;
            movement.stepOffset = 0.3f;
            movement.minMoveDistance = 0f;
            ChampionController controller =
                _championObject.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            return controller;
        }

        private static Vector3 GroundedRootPosition(
            TerrainCollider support,
            CharacterController movement,
            Vector3 horizontalPosition)
        {
            float rayOriginY = support.bounds.max.y + movement.height + 1f;
            var ray = new Ray(
                new Vector3(horizontalPosition.x, rayOriginY, horizontalPosition.z),
                Vector3.down);
            Assert.That(support.Raycast(ray, out RaycastHit hit, 10f), Is.True);
            return new Vector3(
                horizontalPosition.x,
                hit.point.y - movement.center.y + movement.height * 0.5f +
                Mathf.Max(movement.skinWidth, 0.05f),
                horizontalPosition.z);
        }
    }
}
