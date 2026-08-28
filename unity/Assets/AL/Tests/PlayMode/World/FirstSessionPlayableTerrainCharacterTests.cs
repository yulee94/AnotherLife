using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Camera;
using AL.ChampionMode.Control;
using AL.ChampionMode.Presentation;
using AL.Core;
using AL.Data.Catalogs.WorldTerrain;
using AL.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode.World
{
    public sealed class FirstSessionPlayableTerrainCharacterTests
    {
        private const float SettleObservationSeconds = 1.6f;
        private const float MovementObservationSeconds = 0.2f;
        private const float MaximumRootSettleDistance = 0.25f;
        private const float MinimumHorizontalMovement = 0.35f;
        private const float SupportTolerance = 0.02f;

        private readonly List<Material> _fixtureMaterials = new List<Material>();
        private GameObject _worldRoot;
        private GameObject _championRoot;
        private Material _originalSkybox;
        private AmbientMode _originalAmbientMode;
        private Color _originalAmbientSkyColor;
        private Color _originalAmbientEquatorColor;
        private Color _originalAmbientGroundColor;
        private float _originalAmbientIntensity;
        private bool _originalFog;
        private FogMode _originalFogMode;
        private Color _originalFogColor;
        private float _originalFogDensity;
        private float _originalTimeScale;
        private CursorLockMode _originalCursorLockMode;
        private bool _originalCursorVisible;

        [SetUp]
        public void SetUp()
        {
            _originalSkybox = RenderSettings.skybox;
            _originalAmbientMode = RenderSettings.ambientMode;
            _originalAmbientSkyColor = RenderSettings.ambientSkyColor;
            _originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            _originalAmbientGroundColor = RenderSettings.ambientGroundColor;
            _originalAmbientIntensity = RenderSettings.ambientIntensity;
            _originalFog = RenderSettings.fog;
            _originalFogMode = RenderSettings.fogMode;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalTimeScale = Time.timeScale;
            _originalCursorLockMode = Cursor.lockState;
            _originalCursorVisible = Cursor.visible;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyCurrentFixture();
            Time.timeScale = _originalTimeScale;
            Cursor.lockState = _originalCursorLockMode;
            Cursor.visible = _originalCursorVisible;
        }

        [UnityTest]
        public IEnumerator EveryRealmChampionSettlesAndTraversesPhysicalUnityTerrain()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(
                FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot());

            foreach (RealmId realm in Realms())
            {
                InnerRealmWorldBuildResult built = FirstSessionAuthoredWorldBuilder.Build(
                    layout,
                    realm.ToString().ToLowerInvariant());
                _worldRoot = built.Root.gameObject;
                TrackFixtureMaterials();

                _championRoot = ChampionPresentationBinder.CreateChampionRoot(
                    built.PlayerSpawn);
                ChampionController controller =
                    _championRoot.AddComponent<ChampionController>();
                controller.ConfigureRealmContext(realm);
                yield return null;

                Collider[] solidRootColliders = _championRoot
                    .GetComponents<Collider>()
                    .Where(collider => collider.enabled && !collider.isTrigger)
                    .ToArray();
                Assert.That(solidRootColliders, Has.Length.EqualTo(1),
                    realm + " champion must have one solid movement collider.");
                Assert.That(solidRootColliders[0], Is.SameAs(controller.GetComponent<CharacterController>()),
                    realm + " champion movement collision must be owned by CharacterController.");
                Assert.That(_championRoot.GetComponent<CapsuleCollider>(), Is.Null,
                    realm + " champion root may not retain a competing CapsuleCollider.");

                Transform supportTransform = built.Root.Find(
                    FirstSessionAuthoredWorldBuilder.TerrainName);
                Assert.That(supportTransform, Is.Not.Null,
                    realm + " authored world has no walkable Unity Terrain.");
                TerrainCollider support =
                    supportTransform.GetComponent<TerrainCollider>();
                Assert.That(support, Is.Not.Null,
                    realm + " authored world has no TerrainCollider support.");
                Assert.That(support.isTrigger, Is.False,
                    realm + " walkable support must be solid.");

                Vector3 spawn = built.PlayerSpawn;
                float lowestRootY = _championRoot.transform.position.y;
                float settleStarted = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - settleStarted < SettleObservationSeconds)
                {
                    yield return null;
                    lowestRootY = Mathf.Min(lowestRootY, _championRoot.transform.position.y);
                    AssertSupported(realm, controller, support, "settling");
                }

                Assert.That(
                    Time.realtimeSinceStartup - settleStarted,
                    Is.GreaterThanOrEqualTo(1.5f),
                    realm + " settle observation ended too early.\n" +
                    MovementDiagnostics(controller, support));
                Assert.That(
                    lowestRootY,
                    Is.GreaterThanOrEqualTo(spawn.y - MaximumRootSettleDistance),
                    realm + " champion root dropped materially while settling.\n" +
                    MovementDiagnostics(controller, support));

                ChampionMovementReceipt settleReceipt = controller.LastMovementReceipt;
                Assert.That(settleReceipt.Sequence, Is.GreaterThan(0u),
                    realm + " controller published no movement receipt while settling.\n" +
                    MovementDiagnostics(controller, support));
                Assert.That(settleReceipt.IsGrounded, Is.True,
                    realm + " production movement receipt did not confirm ground contact.\n" +
                    MovementDiagnostics(controller, support));

                Vector3 movementStart = _championRoot.transform.position;
                uint settledSequence = settleReceipt.Sequence;
                controller.SetExternalMoveInput(Vector2.up);
                float movementStarted = Time.realtimeSinceStartup;
                try
                {
                    while (Time.realtimeSinceStartup - movementStarted < MovementObservationSeconds)
                    {
                        yield return null;
                        AssertSupported(realm, controller, support, "moving");
                    }
                }
                finally
                {
                    controller.SetExternalMoveInput(Vector2.zero);
                }

                Vector3 displacement = _championRoot.transform.position - movementStart;
                displacement.y = 0f;
                Assert.That(displacement.magnitude, Is.GreaterThan(MinimumHorizontalMovement),
                    realm + " external movement input produced no meaningful traversal.\n" +
                    MovementDiagnostics(controller, support));
                AssertSupported(realm, controller, support, "after movement");
                ChampionMovementReceipt movementReceipt = controller.LastMovementReceipt;
                Assert.That(movementReceipt.Sequence, Is.GreaterThan(settledSequence),
                    realm + " controller published no receipt for external movement.\n" +
                    MovementDiagnostics(controller, support));
                Assert.That(movementReceipt.RequestedInput.y, Is.GreaterThan(0.9f),
                    realm + " controller receipt did not preserve external movement input.\n" +
                    MovementDiagnostics(controller, support));
                Assert.That(movementReceipt.HorizontalDisplacement, Is.GreaterThan(0f),
                    realm + " final movement receipt contained no horizontal displacement.\n" +
                    MovementDiagnostics(controller, support));
                Assert.That(movementReceipt.IsGrounded, Is.True,
                    realm + " production movement receipt lost ground contact after movement.\n" +
                    MovementDiagnostics(controller, support));

                yield return DestroyCurrentFixture();
            }
        }

        [UnityTest]
        public IEnumerator TerrainColliderSupportsSustainedTraversalBeyondFormerFloorBounds()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(
                FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot());
            InnerRealmWorldBuildResult built = FirstSessionAuthoredWorldBuilder.Build(
                layout,
                "crownlands");
            _worldRoot = built.Root.gameObject;
            TrackFixtureMaterials();

            Transform terrainTransform = built.Root.Find(
                FirstSessionAuthoredWorldBuilder.TerrainName);
            Terrain terrain = terrainTransform.GetComponent<Terrain>();
            TerrainCollider terrainCollider =
                terrainTransform.GetComponent<TerrainCollider>();
            FirstSessionTerrainRuntimeMarker marker =
                terrainTransform.GetComponent<FirstSessionTerrainRuntimeMarker>();
            FirstSessionAuthoredAssetCatalog artCatalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            FirstSessionTerrainLoadResult terrainLoad =
                FirstSessionTerrainCatalogLoader.Validate(
                    artCatalog.FirstSessionTerrainCatalog.bytes);
            Assert.That(terrainLoad.IsAccepted, Is.True);
            Assert.That(marker.ProfileId, Is.EqualTo(terrainLoad.Profile.Id));

            _championRoot = new GameObject("TerrainTraversalProbe");
            CharacterController mover =
                _championRoot.AddComponent<CharacterController>();
            mover.height = 2f;
            mover.radius = 0.45f;
            mover.center = Vector3.zero;
            mover.stepOffset = 0.3f;
            mover.minMoveDistance = 0f;

            Vector3[] directions =
            {
                Vector3.left,
                Vector3.right,
                Vector3.back,
                new Vector3(-1f, 0f, -1f).normalized
            };
            const float traversalSeconds = 3f;
            const float traversalSpeed = 6f;
            foreach (Vector3 direction in directions)
            {
                mover.enabled = false;
                _championRoot.transform.position = built.PlayerSpawn;
                mover.enabled = true;
                Physics.SyncTransforms();
                yield return null;

                Vector3 start = _championRoot.transform.position;
                float verticalVelocity = -2f;
                float started = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - started < traversalSeconds)
                {
                    float delta = Mathf.Min(Time.deltaTime, 0.05f);
                    verticalVelocity += Physics.gravity.y * delta;
                    CollisionFlags flags = mover.Move(
                        direction * traversalSpeed * delta +
                        Vector3.up * verticalVelocity * delta);
                    if ((flags & CollisionFlags.Below) != 0)
                    {
                        verticalVelocity = -2f;
                    }

                    float surfaceY = SampleWorldHeight(
                        terrain,
                        _championRoot.transform.position);
                    float feetY = _championRoot.transform.position.y -
                                   mover.height * 0.5f;
                    Assert.That(
                        feetY,
                        Is.InRange(surfaceY - 0.08f, surfaceY + 0.18f),
                        "Traversal probe lost the TerrainCollider while moving " +
                        direction + ".");
                    Assert.That(
                        terrainCollider.Raycast(
                            new Ray(
                                _championRoot.transform.position + Vector3.up * 4f,
                                Vector3.down),
                            out _,
                            12f),
                        Is.True,
                        "TerrainCollider no longer exists below the probe while moving " +
                        direction + ".");
                    yield return null;
                }

                Vector3 horizontal = _championRoot.transform.position - start;
                horizontal.y = 0f;
                Assert.That(
                    horizontal.magnitude,
                    Is.GreaterThanOrEqualTo(15f),
                    direction +
                    " traversal did not cross the former decorative-floor footprint.");
            }
        }

        [UnityTest]
        public IEnumerator TerrainSafetySweepsHighSpeedDescentAndRecoversEscapedChampion()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(
                FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot());
            InnerRealmWorldBuildResult built = FirstSessionAuthoredWorldBuilder.Build(
                layout,
                "crownlands");
            _worldRoot = built.Root.gameObject;
            TrackFixtureMaterials();

            Transform terrainTransform = built.Root.Find(
                FirstSessionAuthoredWorldBuilder.TerrainName);
            Terrain terrain = terrainTransform.GetComponent<Terrain>();
            TerrainCollider support = terrainTransform.GetComponent<TerrainCollider>();
            _championRoot = ChampionPresentationBinder.CreateChampionRoot(
                built.PlayerSpawn);
            ChampionController controller =
                _championRoot.AddComponent<ChampionController>();
            CharacterController movement =
                _championRoot.GetComponent<CharacterController>();
            Physics.SyncTransforms();

            Assert.That(
                controller.TryConfigureTerrainSafety(support, built.PlayerSpawn),
                Is.True,
                "The production motor must bind to TerrainCollider authority.");
            Assert.That(controller.TerrainSafetyConfigured, Is.True);
            Assert.That(controller.TerrainSafetySupportReady, Is.True);

            controller.TeleportTo(built.PlayerSpawn + Vector3.up * 24f);
            Physics.SyncTransforms();
            CollisionFlags descent = movement.Move(Vector3.down * 64f);
            Physics.SyncTransforms();
            float surfaceY = SampleWorldHeight(terrain, _championRoot.transform.position);
            float feetY = _championRoot.transform.position.y + movement.center.y -
                           movement.height * 0.5f;
            Assert.That(
                (descent & CollisionFlags.Below) != 0,
                Is.True,
                "A single high-speed descent sweep crossed the TerrainCollider.");
            Assert.That(
                feetY,
                Is.GreaterThanOrEqualTo(surfaceY - 0.02f),
                "The high-speed descent left the capsule below physical terrain.");

            controller.TeleportTo(new Vector3(
                built.PlayerSpawn.x,
                controller.TerrainSafetyRecoveryY - 1f,
                built.PlayerSpawn.z));
            yield return null;
            Assert.That(controller.TerrainSafetyRecoveryCount, Is.EqualTo(1));
            Assert.That(
                Vector3.Distance(
                    _championRoot.transform.position,
                    controller.TerrainSafetySpawn),
                Is.LessThan(0.02f),
                "Below-world recovery did not restore the verified grounded spawn.");

            Vector3 blockedPosition = _championRoot.transform.position;
            support.enabled = false;
            controller.SetExternalMoveInput(Vector2.up);
            yield return null;
            controller.SetExternalMoveInput(Vector2.zero);
            Assert.That(controller.TerrainSafetySupportReady, Is.False);
            Assert.That(
                Vector3.Distance(_championRoot.transform.position, blockedPosition),
                Is.LessThan(0.001f),
                "The motor continued moving after physical ground authority disappeared.");
            support.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            Assert.That(controller.TerrainSafetySupportReady, Is.True);
        }

        [UnityTest]
        public IEnumerator GeneratedRimSlopeKeepsCapsuleSupportedAcrossHeightfieldSamples()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(
                FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot());
            InnerRealmWorldBuildResult built = FirstSessionAuthoredWorldBuilder.Build(
                layout,
                "stonehold");
            _worldRoot = built.Root.gameObject;
            TrackFixtureMaterials();

            Transform terrainTransform = built.Root.Find(
                FirstSessionAuthoredWorldBuilder.TerrainName);
            Terrain terrain = terrainTransform.GetComponent<Terrain>();
            TerrainCollider support = terrainTransform.GetComponent<TerrainCollider>();
            _championRoot = new GameObject("TerrainRimSlopeProbe");
            CharacterController mover =
                _championRoot.AddComponent<CharacterController>();
            mover.height = 2f;
            mover.radius = 0.45f;
            mover.center = Vector3.zero;
            mover.stepOffset = 0.3f;
            mover.slopeLimit = 45f;
            mover.minMoveDistance = 0f;

            Vector3 direction = Vector3.forward;
            Vector3 start = built.WalkableInner.CapitalPosition + direction * 35f;
            start.y = SampleWorldHeight(terrain, start) + mover.height * 0.5f + 0.1f;
            _championRoot.transform.position = start;
            Physics.SyncTransforms();
            yield return null;

            float startSurfaceY = SampleWorldHeight(terrain, start);
            float verticalVelocity = -2f;
            float started = Time.realtimeSinceStartup;
            const float traversalSeconds = 4f;
            const float traversalSpeed = 5f;
            while (Time.realtimeSinceStartup - started < traversalSeconds)
            {
                float delta = Mathf.Min(Time.deltaTime, 0.05f);
                verticalVelocity += Physics.gravity.y * delta;
                CollisionFlags flags = mover.Move(
                    direction * traversalSpeed * delta +
                    Vector3.up * verticalVelocity * delta);
                if ((flags & CollisionFlags.Below) != 0)
                {
                    verticalVelocity = -2f;
                }

                float surfaceY = SampleWorldHeight(
                    terrain,
                    _championRoot.transform.position);
                float feetY = _championRoot.transform.position.y -
                               mover.height * 0.5f;
                Assert.That(
                    feetY,
                    Is.InRange(surfaceY - 0.08f, surfaceY + 0.20f),
                    "The capsule separated from the rising TerrainCollider rim.");
                Assert.That(
                    support.Raycast(
                        new Ray(
                            _championRoot.transform.position + Vector3.up * 4f,
                            Vector3.down),
                        out _,
                        12f),
                    Is.True,
                    "Physical terrain disappeared beneath the rim traversal probe.");
                yield return null;
            }

            Vector3 horizontal = _championRoot.transform.position - start;
            horizontal.y = 0f;
            float endSurfaceY = SampleWorldHeight(
                terrain,
                _championRoot.transform.position);
            Assert.That(horizontal.magnitude, Is.GreaterThanOrEqualTo(15f));
            Assert.That(
                endSurfaceY - startSurfaceY,
                Is.GreaterThan(0.35f),
                "The test never reached the generated heightfield slope.");
        }

        [UnityTest]
        public IEnumerator SealedCrownlandsLandmarkBlocksSustainedChampionAndCameraApproach()
        {
            InnerRealmWorldLayout layout = InnerRealmWorldLayout.FromSnapshot(
                FirstSessionInnerRealmSpawn.LoadCanonicalSnapshot());
            InnerRealmWorldBuildResult built = FirstSessionAuthoredWorldBuilder.Build(
                layout,
                "crownlands");
            _worldRoot = built.Root.gameObject;
            TrackFixtureMaterials();

            Transform landmark = built.Root.Find(
                FirstSessionAuthoredWorldBuilder.StructuralIdentityPrefix +
                RealmId.Crownlands);
            Assert.That(landmark, Is.Not.Null);
            Bounds visibleBounds = CalculateRendererBounds(landmark.gameObject);
            Transform collisionRoot = built.Root
                .GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(candidate =>
                    candidate.name ==
                    FirstSessionAuthoredWorldBuilder.LandmarkCollisionRootName);
            Assert.That(collisionRoot, Is.Not.Null);
            BoxCollider sealedFront = collisionRoot
                .GetComponentsInChildren<BoxCollider>(true)
                .Single(collider => collider.name == "COL_Landmark_Front");
            Assert.That(
                sealedFront.bounds.size.x,
                Is.EqualTo(visibleBounds.size.x).Within(0.02f),
                "The visually closed Crownlands hall must have one full-width front proxy.");

            _championRoot = new GameObject("SealedLandmarkApproachChampion");
            CharacterController mover =
                _championRoot.AddComponent<CharacterController>();
            mover.height = 2f;
            mover.radius = 0.45f;
            mover.center = Vector3.zero;
            mover.stepOffset = 0.3f;
            mover.minMoveDistance = 0f;
            _championRoot.transform.position = new Vector3(
                visibleBounds.center.x,
                built.WalkableInner.CapitalPosition.y + mover.height * 0.5f,
                visibleBounds.min.z - 6f);

            var cameraObject = new GameObject("SealedLandmarkApproachCamera");
            cameraObject.transform.SetParent(_worldRoot.transform, false);
            UnityEngine.Camera camera =
                cameraObject.AddComponent<UnityEngine.Camera>();
            camera.nearClipPlane = 0.3f;
            camera.fieldOfView = 42f;
            camera.aspect = 16f / 9f;
            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            follow.ConfigureChampion(_championRoot.transform);

            Physics.SyncTransforms();
            yield return null;

            Vector3 start = _championRoot.transform.position;
            float verticalVelocity = -2f;
            float started = Time.realtimeSinceStartup;
            const float approachSeconds = 2.25f;
            const float approachSpeed = 6f;
            while (Time.realtimeSinceStartup - started < approachSeconds)
            {
                float delta = Mathf.Min(Time.deltaTime, 0.05f);
                verticalVelocity += Physics.gravity.y * delta;
                CollisionFlags flags = mover.Move(
                    Vector3.forward * approachSpeed * delta +
                    Vector3.up * verticalVelocity * delta);
                if ((flags & CollisionFlags.Below) != 0)
                {
                    verticalVelocity = -2f;
                }

                Vector3 controllerCenter =
                    _championRoot.transform.TransformPoint(mover.center);
                Assert.That(
                    controllerCenter.z + mover.radius,
                    Is.LessThanOrEqualTo(visibleBounds.min.z + 0.03f),
                    "Sustained forward input crossed the visually sealed landmark front.");
                Assert.That(
                    visibleBounds.Contains(cameraObject.transform.position),
                    Is.False,
                    "Camera center entered the dense imported landmark bounds.");
                float cameraClearance = Vector3.Distance(
                    cameraObject.transform.position,
                    visibleBounds.ClosestPoint(cameraObject.transform.position));
                Assert.That(
                    cameraClearance,
                    Is.GreaterThan(CalculateNearClipRadius(camera) + 0.04f),
                    "Camera near clip entered the dense imported landmark bounds.");
                yield return null;
            }

            Vector3 horizontalDisplacement =
                _championRoot.transform.position - start;
            horizontalDisplacement.y = 0f;
            Assert.That(
                horizontalDisplacement.magnitude,
                Is.GreaterThan(4f),
                "The regression probe never reached the landmark collision.");
            Vector3 finalCenter =
                _championRoot.transform.TransformPoint(mover.center);
            Assert.That(
                finalCenter.z + mover.radius,
                Is.EqualTo(sealedFront.bounds.min.z).Within(0.12f),
                "The champion must settle against the solid front proxy instead of entering the renderer.");
        }

        private void TrackFixtureMaterials()
        {
            _fixtureMaterials.AddRange(_worldRoot
                .GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Where(IsRuntimeFixtureMaterial)
                .Distinct());
            Material generatedSkybox = RenderSettings.skybox;
            if (generatedSkybox != null && generatedSkybox != _originalSkybox &&
                IsRuntimeFixtureMaterial(generatedSkybox) &&
                !_fixtureMaterials.Contains(generatedSkybox))
            {
                _fixtureMaterials.Add(generatedSkybox);
            }
        }

        private static bool IsRuntimeFixtureMaterial(Material material)
        {
#if UNITY_EDITOR
            return !EditorUtility.IsPersistent(material);
#else
            return true;
#endif
        }

        private IEnumerator DestroyCurrentFixture()
        {
            GameObject champion = _championRoot;
            GameObject world = _worldRoot;
            _championRoot = null;
            _worldRoot = null;

            RestoreAtmosphere();
            if (champion != null)
            {
                Object.Destroy(champion);
            }

            if (world != null)
            {
                Object.Destroy(world);
            }

            for (int index = 0; index < _fixtureMaterials.Count; index++)
            {
                if (_fixtureMaterials[index] != null)
                {
                    Object.Destroy(_fixtureMaterials[index]);
                }
            }

            _fixtureMaterials.Clear();
            yield return null;

            Assert.That(champion == null, Is.True,
                "Champion fixture leaked into the next realm iteration.");
            Assert.That(world == null, Is.True,
                "Authored-world fixture leaked into the next realm iteration.");
        }

        private void RestoreAtmosphere()
        {
            RenderSettings.skybox = _originalSkybox;
            RenderSettings.ambientMode = _originalAmbientMode;
            RenderSettings.ambientSkyColor = _originalAmbientSkyColor;
            RenderSettings.ambientEquatorColor = _originalAmbientEquatorColor;
            RenderSettings.ambientGroundColor = _originalAmbientGroundColor;
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
            RenderSettings.fog = _originalFog;
            RenderSettings.fogMode = _originalFogMode;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
        }

        private static void AssertSupported(
            RealmId realm,
            ChampionController controller,
            TerrainCollider support,
            string phase)
        {
            CharacterController movementCollider = controller.GetComponent<CharacterController>();
            Vector3 center = controller.transform.TransformPoint(movementCollider.center);
            Terrain terrain = support.GetComponent<Terrain>();
            Vector3 terrainMinimum = support.transform.position;
            Vector3 terrainMaximum = terrainMinimum + terrain.terrainData.size;
            float radius = movementCollider.radius;
            string diagnostics = MovementDiagnostics(controller, support);
            Assert.That(center.x - radius, Is.GreaterThanOrEqualTo(terrainMinimum.x - SupportTolerance),
                realm + " champion left support on -X while " + phase + ".\n" + diagnostics);
            Assert.That(center.x + radius, Is.LessThanOrEqualTo(terrainMaximum.x + SupportTolerance),
                realm + " champion left support on +X while " + phase + ".\n" + diagnostics);
            Assert.That(center.z - radius, Is.GreaterThanOrEqualTo(terrainMinimum.z - SupportTolerance),
                realm + " champion left support on -Z while " + phase + ".\n" + diagnostics);
            Assert.That(center.z + radius, Is.LessThanOrEqualTo(terrainMaximum.z + SupportTolerance),
                realm + " champion left support on +Z while " + phase + ".\n" + diagnostics);

            float feetY = center.y - movementCollider.height * 0.5f;
            float surfaceY = SampleWorldHeight(terrain, center);
            Assert.That(feetY, Is.InRange(surfaceY - 0.08f, surfaceY + 0.18f),
                realm + " champion feet left the support surface while " + phase + ".\n" +
                diagnostics);
        }

        private static string MovementDiagnostics(
            ChampionController controller,
            TerrainCollider support)
        {
            CharacterController movementCollider = controller.GetComponent<CharacterController>();
            ChampionMovementReceipt receipt = controller.LastMovementReceipt;
            Vector3 root = controller.transform.position;
            Vector3 center = controller.transform.TransformPoint(movementCollider.center);
            float feetY = center.y - movementCollider.height * 0.5f;
            Terrain terrain = support.GetComponent<Terrain>();
            Vector3 supportMinimum = support.transform.position;
            Vector3 supportMaximum = supportMinimum + terrain.terrainData.size;
            float surfaceY = SampleWorldHeight(terrain, center);
            return "sequence=" + receipt.Sequence +
                   ", requested=" + receipt.RequestedInput +
                   ", receiptDisplacement=" + receipt.Displacement +
                   ", receiptWasGrounded=" + receipt.WasGrounded +
                   ", receiptIsGrounded=" + receipt.IsGrounded +
                   ", collisionFlags=" + receipt.CollisionFlags +
                   ", rawIsGrounded=" + movementCollider.isGrounded +
                   ", root=" + root +
                   ", feetY=" + feetY +
                   ", surfaceY=" + surfaceY +
                   ", supportMin=" + supportMinimum +
                   ", supportMax=" + supportMaximum;
        }

        private static float SampleWorldHeight(Terrain terrain, Vector3 worldPosition)
        {
            return terrain.transform.position.y + terrain.SampleHeight(worldPosition);
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static float CalculateNearClipRadius(UnityEngine.Camera camera)
        {
            float halfHeight = Mathf.Tan(
                camera.fieldOfView * 0.5f * Mathf.Deg2Rad) *
                camera.nearClipPlane;
            float halfWidth = halfHeight * camera.aspect;
            return Mathf.Sqrt(
                halfWidth * halfWidth +
                halfHeight * halfHeight +
                camera.nearClipPlane * camera.nearClipPlane);
        }

        private static RealmId[] Realms()
        {
            return new[]
            {
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Crownlands,
                RealmId.Umbral
            };
        }
    }
}
