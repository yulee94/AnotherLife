using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Camera;
using AL.ChampionMode.UI;
using AL.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AL.Tests.PlayMode
{
    public sealed class CameraFollowObstructionTests
    {
        private const float FollowDistance = 8f;
        private const float HeightOffset = 1.5f;
        private const float ChampionDistance = 6.8f;
        private const float ChampionPitch = 14f;
        private const float ChampionPivotHeightRatio = 0.70f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _spawnedAssets = new List<Object>();
        private GameObject _target;
        private GameObject _cameraObject;
        private UnityEngine.Camera _camera;
        private CameraFollow _follow;
        private CharacterController _targetController;
        private bool _originalGameplaySuppressed;
        private CursorLockMode _originalCursorLockMode;
        private bool _originalCursorVisible;
        private bool _originalMenuOpen;
        private bool _originalRecapOpen;
        private float _originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            _originalGameplaySuppressed = GameInput.GameplaySuppressed;
            _originalCursorLockMode = Cursor.lockState;
            _originalCursorVisible = Cursor.visible;
            _originalMenuOpen = ChampionHudCameraGate.MenuOpen;
            _originalRecapOpen = ChampionHudCameraGate.RecapOpen;
            _originalTimeScale = Time.timeScale;
            GameInput.SetGameplaySuppressed(true);
            ChampionHudCameraGate.Reset();
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _spawned.Count - 1; index >= 0; index--)
            {
                if (_spawned[index] != null)
                {
                    Object.Destroy(_spawned[index]);
                }
            }

            _spawned.Clear();
            for (int index = _spawnedAssets.Count - 1; index >= 0; index--)
            {
                if (_spawnedAssets[index] != null)
                {
                    Object.Destroy(_spawnedAssets[index]);
                }
            }

            _spawnedAssets.Clear();
            GameInput.SetGameplaySuppressed(_originalGameplaySuppressed);
            Time.timeScale = _originalTimeScale;
            yield return null;
            Cursor.lockState = _originalCursorLockMode;
            Cursor.visible = _originalCursorVisible;
            ChampionHudCameraGate.MenuOpen = _originalMenuOpen;
            ChampionHudCameraGate.RecapOpen = _originalRecapOpen;
        }

        [UnityTest]
        public IEnumerator NoWorldObstacleKeepsConfiguredDistanceAndIgnoresPlayerAndTriggers()
        {
            CreateRig();

            var playerChild = new GameObject("IgnoredPlayerHierarchyCollider");
            playerChild.transform.SetParent(_target.transform, false);
            playerChild.transform.localPosition = new Vector3(0f, HeightOffset, -2.25f);
            BoxCollider playerCollider = playerChild.AddComponent<BoxCollider>();
            playerCollider.size = new Vector3(2f, 2f, 0.5f);

            BoxCollider trigger = CreateBox(
                "IgnoredCameraTrigger",
                Pivot + Vector3.back * 4f,
                new Vector3(4f, 4f, 0.5f));
            trigger.isTrigger = true;
            Physics.SyncTransforms();

            yield return null;
            yield return null;

            Assert.That(
                Vector3.Distance(_cameraObject.transform.position, DesiredPosition),
                Is.LessThan(0.02f),
                "Player-hierarchy colliders and triggers must not shorten the camera arm.");
            Assert.That(
                Vector3.Distance(Pivot, _cameraObject.transform.position),
                Is.EqualTo(FollowDistance).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator WorldObstaclePullsCameraInKeepsNearClipClearAndRestoresSmoothly()
        {
            CreateRig();
            BoxCollider obstacle = CreateBox(
                "CameraWorldObstacle",
                Pivot + Vector3.back * 4f,
                new Vector3(6f, 6f, 0.5f));
            Physics.SyncTransforms();

            yield return null;

            float pulledDistance = Vector3.Distance(
                Pivot,
                _cameraObject.transform.position);
            Assert.That(pulledDistance, Is.LessThan(4f),
                "An obstruction between pivot and camera must pull the camera in immediately.");
            AssertNearClipClear(obstacle);

            _follow.AddShake(1f, 0.2f);
            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
                AssertNearClipClear(obstacle);
            }

            yield return new WaitForSeconds(0.25f);
            float obstructedDistance = Vector3.Distance(
                Pivot,
                _cameraObject.transform.position);
            AssertNearClipClear(obstacle);

            obstacle.enabled = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.05f);

            float recoveringDistance = Vector3.Distance(
                Pivot,
                _cameraObject.transform.position);
            Assert.That(recoveringDistance, Is.GreaterThan(obstructedDistance),
                "Camera distance must begin restoring after the obstruction clears.");
            Assert.That(recoveringDistance, Is.LessThan(FollowDistance - 0.1f),
                "Obstruction recovery must be smoothed instead of snapping outward.");

            yield return new WaitForSeconds(0.8f);

            Assert.That(
                Vector3.Distance(_cameraObject.transform.position, DesiredPosition),
                Is.LessThan(0.08f),
                "A cleared camera arm must recover to its configured no-obstacle position.");
        }

        [UnityTest]
        public IEnumerator StartingInsideDenseWorldProxyDepenetratesNearClipOutsideGeometry()
        {
            CreateRig();
            BoxCollider enclosure = CreateBox(
                "CameraStartingOverlap",
                Pivot + Vector3.back * (FollowDistance * 0.5f),
                new Vector3(12f, 8f, FollowDistance + 4f));
            Physics.SyncTransforms();

            Assert.That(enclosure.bounds.Contains(Pivot), Is.True,
                "The regression fixture must begin with the target pivot inside world collision.");
            Assert.That(enclosure.bounds.Contains(DesiredPosition), Is.True,
                "The regression fixture must begin with the desired camera candidate inside the same dense proxy.");

            yield return null;

            AssertNearClipClear(enclosure);
            Assert.That(
                Vector3.Dot(
                    _cameraObject.transform.position - Pivot,
                    Vector3.back),
                Is.GreaterThan(0f),
                "Starting-overlap recovery must keep the camera on its intended rear arm.");
        }

        [UnityTest]
        public IEnumerator OrbitZoomAndFollowPreserveMmoCameraContract()
        {
            AssertMmoCameraBindings();
            CreateChampionRig();
            yield return new WaitForSeconds(0.35f);

            Vector3 initialPivot = ChampionPivot;
            Vector3 initialDirection = HorizontalDirection(
                _cameraObject.transform.position - initialPivot);

            yield return new WaitForSeconds(0.12f);

            Vector3 freeCursorDirection = HorizontalDirection(
                _cameraObject.transform.position - ChampionPivot);
            Assert.That(
                Vector3.Angle(initialDirection, freeCursorDirection),
                Is.LessThan(0.5f),
                "Without an orbit intent, a free MMO cursor must not rotate the camera.");

            _follow.ApplyZoomDelta(120f);
            yield return new WaitForSeconds(0.25f);
            Assert.That(
                Vector3.Distance(_cameraObject.transform.position, ChampionPivot),
                Is.LessThan(ChampionDistance - 0.5f),
                "The mouse-wheel intent must provide bounded distance zoom.");

            _follow.ApplyMouseOrbitDelta(new Vector2(220f, -35f));
            yield return new WaitForSeconds(0.30f);

            Vector3 orbitedPivot = ChampionPivot;
            Vector3 orbitedDirection = HorizontalDirection(
                _cameraObject.transform.position - orbitedPivot);
            Assert.That(
                Vector3.Angle(initialDirection, orbitedDirection),
                Is.GreaterThan(10f),
                "The RMB-gated orbit intent must produce a deliberate persistent orbit.");

            float orbitedDistance = Vector3.Distance(
                _cameraObject.transform.position,
                orbitedPivot);
            _target.transform.position += new Vector3(3.25f, 0f, 2.1f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.45f);

            Vector3 followedPivot = ChampionPivot;
            Vector3 followedDirection = HorizontalDirection(
                _cameraObject.transform.position - followedPivot);
            Assert.That(
                Vector3.Angle(orbitedDirection, followedDirection),
                Is.LessThan(1.5f),
                "Following a moving champion must preserve the user's yaw instead of recentering implicitly.");
            Assert.That(
                Vector3.Distance(_cameraObject.transform.position, followedPivot),
                Is.EqualTo(orbitedDistance).Within(0.08f),
                "Follow movement must preserve the selected orbit distance.");
            AssertCameraFramesChampionPivot(followedPivot);
        }

        [UnityTest]
        public IEnumerator RecenterRequestReturnsBehindChampionAndPreservesUpperBodyFraming()
        {
            AssertMmoCameraBindings();
            CreateChampionRig();
            yield return new WaitForSeconds(0.25f);
            _follow.ApplyMouseOrbitDelta(new Vector2(260f, 20f));
            yield return new WaitForSeconds(0.30f);

            _target.transform.rotation = Quaternion.Euler(0f, -55f, 0f);
            Physics.SyncTransforms();
            Vector3 beforeRecenter = HorizontalDirection(
                _cameraObject.transform.position - ChampionPivot);
            Vector3 expectedBehind = -_target.transform.forward;
            Assert.That(
                Vector3.Angle(beforeRecenter, expectedBehind),
                Is.GreaterThan(20f),
                "The fixture must begin off-axis so the recenter assertion is meaningful.");

            _follow.RequestRecenter();
            yield return new WaitForSeconds(0.75f);

            Vector3 pivot = ChampionPivot;
            Vector3 actualBehind = HorizontalDirection(
                _cameraObject.transform.position - pivot);
            Assert.That(
                Vector3.Angle(actualBehind, expectedBehind),
                Is.LessThan(1f),
                "Recenter must return the camera behind the champion's current facing.");
            Assert.That(
                Vector3.Distance(_cameraObject.transform.position, pivot),
                Is.EqualTo(ChampionDistance).Within(0.08f));
            AssertCameraFramesChampionPivot(pivot);
        }

        [UnityTest]
        public IEnumerator LiveTerrainKeepsCameraFootprintOnSupportedSideAtLowPitchAndEdge()
        {
            Terrain terrain = CreateFlatTerrain();
            _target = Track(new GameObject("TerrainSupportedCameraChampion"));
            _target.transform.position = new Vector3(20f, 1.05f, 20f);
            _targetController = _target.AddComponent<CharacterController>();
            _targetController.center = Vector3.zero;
            _targetController.height = 2f;
            _targetController.radius = 0.45f;
            var championController = _target.AddComponent<AL.ChampionMode.Control.ChampionController>();
            Assert.That(
                championController.TryConfigureTerrainSafety(
                    terrain.GetComponent<TerrainCollider>(),
                    _target.transform.position),
                Is.True,
                "The regression camera must share the champion's verified TerrainCollider authority.");

            _cameraObject = Track(new GameObject("TerrainSupportedFollowCamera"));
            _camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            _camera.nearClipPlane = 0.3f;
            _camera.fieldOfView = 42f;
            _camera.aspect = 16f / 9f;
            _cameraObject.transform.position = _target.transform.position +
                                               new Vector3(0f, 2f, -ChampionDistance);
            _follow = _cameraObject.AddComponent<CameraFollow>();
            _follow.ConfigureChampion(_target.transform);
            _follow.ApplyZoomDelta(-10000f);
            _follow.ApplyMouseOrbitDelta(new Vector2(0f, 10000f));
            _follow.AddShake(0.5f, 0.3f);
            Physics.SyncTransforms();

            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                Vector3 cameraPosition = _cameraObject.transform.position;
                float terrainY = terrain.SampleHeight(cameraPosition) +
                                 terrain.transform.position.y;
                float nearClipRadius = CalculateNearClipRadius(_camera);
                Assert.That(
                    cameraPosition.y - terrainY,
                    Is.GreaterThanOrEqualTo(nearClipRadius + 0.07f),
                    "Low-pitch max zoom, smoothing, and combat shake must not put " +
                    "the camera or its near clip below the one-sided terrain renderer.");
            }

            championController.TeleportTo(new Vector3(20f, 1.05f, 0.75f));
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.25f);

            Vector3 edgeCameraPosition = _cameraObject.transform.position;
            float footprintRadius = CalculateNearClipRadius(_camera) + 0.07f;
            Assert.That(
                edgeCameraPosition.z,
                Is.GreaterThanOrEqualTo(
                    terrain.transform.position.z + footprintRadius),
                "A max-distance rear camera must not cross the finite terrain edge " +
                "and reveal the one-sided terrain shell from outside.");
        }

        [UnityTest]
        public IEnumerator TranslatedTerrainKeepsCameraAboveWorldSurfaceAfterMaximumDownwardOrbit()
        {
            // The production MVP terrain places a two-metre base-height heightfield
            // on a Terrain transform translated down by two metres. A zero-origin
            // fixture cannot detect accidental mixing of Terrain-local and world Y.
            Terrain terrain = CreateFlatTerrain(originY: -2f, surfaceY: 0f);
            _target = Track(new GameObject("TranslatedTerrainCameraChampion"));
            _target.transform.position = new Vector3(20f, 1.05f, 20f);
            _targetController = _target.AddComponent<CharacterController>();
            _targetController.center = Vector3.zero;
            _targetController.height = 2f;
            _targetController.radius = 0.45f;
            var championController =
                _target.AddComponent<AL.ChampionMode.Control.ChampionController>();
            Assert.That(
                championController.TryConfigureTerrainSafety(
                    terrain.GetComponent<TerrainCollider>(),
                    _target.transform.position),
                Is.True);

            _cameraObject = Track(new GameObject("TranslatedTerrainFollowCamera"));
            _camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            _camera.nearClipPlane = 0.3f;
            _camera.fieldOfView = 42f;
            _camera.aspect = 16f / 9f;
            _cameraObject.transform.position = _target.transform.position +
                                               new Vector3(0f, 2f, -ChampionDistance);
            _follow = _cameraObject.AddComponent<CameraFollow>();
            _follow.ConfigureChampion(_target.transform);
            _follow.ApplyZoomDelta(-10000f);
            _follow.ApplyMouseOrbitDelta(new Vector2(0f, 10000f));
            Physics.SyncTransforms();

            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                Vector3 cameraPosition = _cameraObject.transform.position;
                float worldSurfaceY = terrain.SampleHeight(cameraPosition) +
                                      terrain.transform.position.y;
                float requiredClearance = CalculateNearClipRadius(_camera) + 0.07f;
                Assert.That(
                    cameraPosition.y - worldSurfaceY,
                    Is.GreaterThanOrEqualTo(requiredClearance),
                    "A translated Terrain must retain its supported-side camera clearance " +
                    "during a maximum downward MMO orbit.");
            }

            Assert.That(
                _cameraObject.transform.position.y,
                Is.GreaterThan(ChampionPivot.y + 0.25f),
                "A grounded MMO camera must keep a shallow downward view instead of " +
                "pinning a negative-pitch arm against the terrain safety plane.");
            Vector3 groundedChampionPoint = new Vector3(
                _target.transform.position.x,
                terrain.SampleHeight(_target.transform.position) +
                terrain.transform.position.y + 0.02f,
                _target.transform.position.z);
            Vector3 groundViewportPoint =
                _camera.WorldToViewportPoint(groundedChampionPoint);
            Assert.That(groundViewportPoint.z, Is.GreaterThan(0f));
            Assert.That(
                groundViewportPoint.y,
                Is.InRange(0.05f, 0.49f),
                "The terrain beneath the champion must remain in the camera frame " +
                "after the same maximum downward orbit used in the packaged repro.");
        }

        private Vector3 Pivot => _target.transform.position + Vector3.up * HeightOffset;

        private Vector3 DesiredPosition => Pivot + Vector3.back * FollowDistance;

        private Vector3 ChampionPivot
        {
            get
            {
                Bounds bounds = _targetController.bounds;
                return new Vector3(
                    bounds.center.x,
                    Mathf.Lerp(
                        bounds.min.y,
                        bounds.max.y,
                        ChampionPivotHeightRatio),
                    bounds.center.z);
            }
        }

        private void CreateRig()
        {
            _target = Track(new GameObject("CameraFollowTestTarget"));
            _cameraObject = Track(new GameObject("CameraFollowTestCamera"));
            _camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            _camera.nearClipPlane = 0.3f;
            _camera.fieldOfView = 60f;
            _camera.aspect = 16f / 9f;
            _cameraObject.transform.position = DesiredPosition;
            _cameraObject.transform.rotation = Quaternion.identity;

            _follow = _cameraObject.AddComponent<CameraFollow>();
            _follow.Configure(
                _target.transform,
                FollowDistance,
                HeightOffset,
                pitch: 0f,
                yaw: 0f);
        }

        private void CreateChampionRig()
        {
            _target = Track(new GameObject("CameraFollowChampionTarget"));
            _target.transform.position = new Vector3(0f, 1.05f, 0f);
            _targetController = _target.AddComponent<CharacterController>();
            _targetController.center = Vector3.zero;
            _targetController.height = 2f;
            _targetController.radius = 0.45f;
            Physics.SyncTransforms();

            _cameraObject = Track(new GameObject("CameraFollowChampionCamera"));
            _camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            _camera.nearClipPlane = 0.3f;
            _camera.fieldOfView = 42f;
            _camera.aspect = 16f / 9f;
            _cameraObject.transform.position = ChampionPivot +
                                               Quaternion.Euler(
                                                   ChampionPitch,
                                                   0f,
                                                   0f) *
                                               Vector3.back * ChampionDistance;

            _follow = _cameraObject.AddComponent<CameraFollow>();
            _follow.ConfigureChampion(_target.transform);
        }

        private static void AssertMmoCameraBindings()
        {
            string[] lookBindings = GameInput.Look.bindings
                .Select(binding => binding.path)
                .ToArray();
            string[] scrollBindings = GameInput.Scroll.bindings
                .Select(binding => binding.path)
                .ToArray();
            string[] recenterBindings = GameInput.CameraRecenter.bindings
                .Select(binding => binding.path)
                .ToArray();

            Assert.That(lookBindings, Does.Contain("<Mouse>/delta"));
            Assert.That(lookBindings, Does.Contain("<Gamepad>/rightStick"));
            Assert.That(scrollBindings, Does.Contain("<Mouse>/scroll/y"));
            Assert.That(recenterBindings, Does.Contain("<Mouse>/middleButton"));
            Assert.That(recenterBindings, Does.Contain("<Gamepad>/rightStickPress"));
        }

        private void AssertCameraFramesChampionPivot(Vector3 pivot)
        {
            float normalizedPivotHeight = Mathf.InverseLerp(
                _targetController.bounds.min.y,
                _targetController.bounds.max.y,
                pivot.y);
            Assert.That(
                normalizedPivotHeight,
                Is.EqualTo(ChampionPivotHeightRatio).Within(0.01f),
                "The camera target must remain on the champion's upper body.");
            Assert.That(
                Vector3.Angle(
                    _cameraObject.transform.forward,
                    (pivot - _cameraObject.transform.position).normalized),
                Is.LessThan(0.05f),
                "Follow smoothing must keep the camera framed on the resolved upper-body pivot.");

            Vector3 viewportPoint = _camera.WorldToViewportPoint(pivot);
            Assert.That(viewportPoint.z, Is.GreaterThan(0f));
            Assert.That(viewportPoint.x, Is.EqualTo(0.5f).Within(0.005f));
            Assert.That(viewportPoint.y, Is.EqualTo(0.5f).Within(0.005f));
        }

        private static Vector3 HorizontalDirection(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.000001f
                ? value.normalized
                : Vector3.zero;
        }

        private BoxCollider CreateBox(
            string name,
            Vector3 position,
            Vector3 size)
        {
            GameObject root = Track(new GameObject(name));
            root.transform.position = position;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private Terrain CreateFlatTerrain(float originY = 0f, float surfaceY = 0f)
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 33,
                size = new Vector3(40f, 4f, 40f)
            };
            float normalizedHeight = Mathf.Clamp01(
                (surfaceY - originY) / terrainData.size.y);
            var heights = new float[33, 33];
            for (int z = 0; z < heights.GetLength(0); z++)
            {
                for (int x = 0; x < heights.GetLength(1); x++)
                {
                    heights[z, x] = normalizedHeight;
                }
            }

            terrainData.SetHeights(0, 0, heights);
            _spawnedAssets.Add(terrainData);
            GameObject terrainObject = Track(
                Terrain.CreateTerrainGameObject(terrainData));
            terrainObject.name = "CameraSupportedSideTerrain";
            terrainObject.transform.position = Vector3.up * originY;
            return terrainObject.GetComponent<Terrain>();
        }

        private void AssertNearClipClear(BoxCollider obstacle)
        {
            Vector3 cameraPosition = _cameraObject.transform.position;
            Vector3 closestPoint = obstacle.ClosestPoint(cameraPosition);
            float clearance = Vector3.Distance(cameraPosition, closestPoint);
            float nearClipRadius = CalculateNearClipRadius(_camera);

            Assert.That(obstacle.bounds.Contains(cameraPosition), Is.False,
                "The camera center entered obstructing geometry.");
            Assert.That(clearance, Is.GreaterThan(nearClipRadius + 0.04f),
                "The camera near-clip footprint lost its safety padding.");
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

        private GameObject Track(GameObject value)
        {
            _spawned.Add(value);
            return value;
        }
    }
}
