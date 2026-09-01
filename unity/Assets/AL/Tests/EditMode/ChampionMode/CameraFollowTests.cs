using System.Reflection;
using IDisposable = System.IDisposable;
using AL.ChampionMode.Camera;
using AL.ChampionMode.UI;
using AL.Input;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class CameraFollowTests
    {
        private GameObject _targetObject;
        private GameObject _cameraObject;

        [TearDown]
        public void TearDown()
        {
            ChampionHudCameraGate.Reset();
            if (_cameraObject != null)
            {
                Object.DestroyImmediate(_cameraObject);
            }

            if (_targetObject != null)
            {
                Object.DestroyImmediate(_targetObject);
            }
        }

        [Test]
        public void SnapToTargetImmediatelyProducesReadableThirdPersonPose()
        {
            _targetObject = new GameObject("Player_Champion");
            _targetObject.transform.position = new Vector3(4f, 1.1f, -3f);
            GameObject authoredVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            authoredVisual.name = "AuthoredChampionVisual";
            authoredVisual.transform.SetParent(_targetObject.transform, false);
            authoredVisual.transform.localPosition = Vector3.down * 0.18f;
            authoredVisual.transform.localScale = new Vector3(0.8f, 1.72f, 0.8f);

            _cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 38f;
            camera.aspect = 16f / 9f;
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();
            follow.Configure(_targetObject.transform, 6.8f, 0.85f, 12f, 0f);

            follow.SnapToTarget();

            Bounds bounds = authoredVisual.GetComponent<Renderer>().bounds;
            Vector3 center = camera.WorldToViewportPoint(bounds.center);
            float bottom = camera.WorldToViewportPoint(
                bounds.center - Vector3.up * bounds.extents.y).y;
            float top = camera.WorldToViewportPoint(
                bounds.center + Vector3.up * bounds.extents.y).y;
            Vector3 horizontalForward = Vector3.ProjectOnPlane(
                camera.transform.forward,
                Vector3.up).normalized;
            float opticalAngle = Vector3.Angle(horizontalForward, camera.transform.forward);

            Assert.That(center.x, Is.InRange(0.47f, 0.53f));
            Assert.That(center.y, Is.InRange(0.47f, 0.55f));
            Assert.That(center.z, Is.GreaterThan(0f));
            Assert.That(top - bottom, Is.InRange(0.30f, 0.44f));
            Assert.That(opticalAngle, Is.InRange(18f, 26f));
        }

        [Test]
        public void FollowPoseCentersAuthoredChampionFocusWithoutChangingBoomPosition()
        {
            _targetObject = new GameObject("Player_Champion");
            _targetObject.transform.position = new Vector3(4f, 1.1f, -3f);

            _cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();
            const float distance = 8.6f;
            const float boomHeight = 2.65f;
            const float pitch = 25f;
            follow.Configure(_targetObject.transform, distance, boomHeight, pitch, 0f);

            Quaternion orbit = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 expectedBoomPosition =
                _targetObject.transform.position +
                Vector3.up * boomHeight +
                orbit * new Vector3(0f, 0f, -distance);
            _cameraObject.transform.position = expectedBoomPosition;

            InvokeLateUpdate(follow);

            Assert.That(
                Vector3.Distance(_cameraObject.transform.position, expectedBoomPosition),
                Is.LessThan(0.001f),
                "Centering must not introduce an unexpected camera-position jump.");
            Vector3 authoredChampionFocus = _targetObject.transform.position + Vector3.up;
            Vector3 viewport = camera.WorldToViewportPoint(authoredChampionFocus);
            Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewport.z, Is.GreaterThan(0f));
        }

        [Test]
        public void FollowPoseCentersEnabledAuthoredRendererBounds()
        {
            _targetObject = new GameObject("Player_Champion");
            _targetObject.transform.position = new Vector3(4f, 1.1f, -3f);
            GameObject authoredVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            authoredVisual.name = "AuthoredChampionVisual";
            authoredVisual.transform.SetParent(_targetObject.transform, false);
            authoredVisual.transform.localPosition = Vector3.down * 1.08f;
            authoredVisual.transform.localScale = new Vector3(0.8f, 1.72f, 0.8f);

            _cameraObject = new GameObject("Main Camera");
            UnityEngine.Camera camera = _cameraObject.AddComponent<UnityEngine.Camera>();
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();
            const float distance = 8.6f;
            const float boomHeight = 2.65f;
            const float pitch = 25f;
            follow.Configure(_targetObject.transform, distance, boomHeight, pitch, 0f);

            Quaternion orbit = Quaternion.Euler(pitch, 0f, 0f);
            Vector3 expectedBoomPosition =
                _targetObject.transform.position +
                Vector3.up * boomHeight +
                orbit * new Vector3(0f, 0f, -distance);
            _cameraObject.transform.position = expectedBoomPosition;

            InvokeLateUpdate(follow);

            Vector3 viewport = camera.WorldToViewportPoint(
                authoredVisual.GetComponent<Renderer>().bounds.center);
            Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewport.z, Is.GreaterThan(0f));
        }

        [Test]
        public void DefaultMouseSensitivityBoundsHundredPixelMotionToFifteenDegrees()
        {
            _cameraObject = new GameObject("Main Camera");
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();
            FieldInfo field = typeof(CameraFollow).GetField(
                "_mouseSensitivity",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(field);
            float degreesPerPixel = (float)field.GetValue(follow);
            Assert.That(degreesPerPixel * 100f, Is.LessThanOrEqualTo(15f));
        }

        [Test]
        public void TouchCameraInputUsesTheSameModalSuppressionPolicyAsMouseInput()
        {
            ChampionHudCameraGate.Reset();
            _cameraObject = new GameObject("Main Camera");
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();
            MethodInfo policy = typeof(CameraFollow).GetMethod(
                "ShouldIgnoreManualCameraInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(policy, Is.Not.Null,
                "Mouse and EnhancedTouch input need one shared suppression policy.");

            using (GameInput.AcquireGameplaySuppression("camera-touch-test"))
            {
                Assert.That((bool)policy.Invoke(follow, null), Is.True);
            }
            using (ChampionHudCameraGate.AcquireCursorOwnership("camera-touch-test"))
            {
                Assert.That((bool)policy.Invoke(follow, null), Is.True);
            }
        }

        [Test]
        public void ControlPressTogglesCursorModeAndNoPressLeavesItUnchanged()
        {
            ChampionHudCameraGate.Reset();
            _cameraObject = new GameObject("Main Camera");
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();

            follow.ApplyCursorModeToggle(false);
            Assert.IsFalse(ChampionHudCameraGate.CursorModeOpen);

            follow.ApplyCursorModeToggle(true);
            Assert.IsTrue(ChampionHudCameraGate.CursorModeOpen);

            follow.ApplyCursorModeToggle(false);
            Assert.IsTrue(ChampionHudCameraGate.CursorModeOpen);

            follow.ApplyCursorModeToggle(true);
            Assert.IsFalse(ChampionHudCameraGate.CursorModeOpen);
        }

        [Test]
        public void DisablingCameraFollowReleasesItsManualCursorOwnership()
        {
            ChampionHudCameraGate.Reset();
            _cameraObject = new GameObject("Main Camera");
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();
            follow.ApplyCursorModeToggle(true);
            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.True);

            InvokeOnDisable(follow);

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        [Test]
        public void CameraUpdateDoesNotPromoteTokenOwnershipIntoManualCursorMode()
        {
            ChampionHudCameraGate.Reset();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            GameInput.SetCursorModeSuppressed(false);
            CursorLockMode priorLockState = Cursor.lockState;
            bool priorVisibility = Cursor.visible;
            _cameraObject = new GameObject("Main Camera");
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();
            IDisposable owner = ChampionHudCameraGate.AcquireCursorOwnership("world-map");

            InvokeUpdate(follow);
            owner.Dispose();

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(priorLockState));
            Assert.That(Cursor.visible, Is.EqualTo(priorVisibility));
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        [Test]
        public void InspectionAndManualCursorInterleavingRestoresOriginalBaseline()
        {
            ChampionHudCameraGate.Reset();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            GameInput.SetCursorModeSuppressed(false);
            CursorLockMode priorLockState = Cursor.lockState;
            bool priorVisibility = Cursor.visible;
            _cameraObject = new GameObject("Inspection Camera");
            CameraFollow follow = _cameraObject.AddComponent<CameraFollow>();

            follow.SetInspectionMode(true);
            ChampionHudCameraGate.SetCursorMode(true);
            follow.SetInspectionMode(false);
            ChampionHudCameraGate.SetCursorMode(false);

            Assert.That(ChampionHudCameraGate.CursorModeOpen, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(priorLockState));
            Assert.That(Cursor.visible, Is.EqualTo(priorVisibility));
            Assert.That(GameInput.CursorModeSuppressed, Is.False);
        }

        private static void InvokeLateUpdate(CameraFollow follow)
        {
            MethodInfo lateUpdate = typeof(CameraFollow).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(lateUpdate);
            lateUpdate.Invoke(follow, null);
        }

        private static void InvokeUpdate(CameraFollow follow)
        {
            MethodInfo update = typeof(CameraFollow).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(update);
            update.Invoke(follow, null);
        }

        private static void InvokeOnDisable(CameraFollow follow)
        {
            MethodInfo onDisable = typeof(CameraFollow).GetMethod(
                "OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onDisable);
            onDisable.Invoke(follow, null);
        }
    }
}
