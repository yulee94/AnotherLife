using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using AL.ChampionMode.Control;
using AL.ChampionMode.UI;
using AL.Input;
using AL.Core;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using IDisposable = System.IDisposable;

namespace AL.ChampionMode.Camera
{
    public class CameraFollow : MonoBehaviour
    {
        private const int ObstructionHitCapacity = 32;
        private const int ObstructionOverlapSearchSteps = 8;
        private const int ObstructionOverlapRefinementSteps = 8;
        private const float ObstructionSurfaceEpsilon = 0.01f;
        private const float TerrainSupportedSideEpsilon = 0.03f;
        private const float TerrainOrbitMinimumPitch = 3f;
        private const float LegacyMouseSensitivityScale = 0.04f;

        [Header("Champion Follow Settings (Provisional)")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 6.8f;
        [Tooltip("Fallback pivot offset when the target has no CharacterController.")]
        [SerializeField] private float _heightOffset = 0.45f;
        [Tooltip("Upper-body aim point as a fraction of the champion controller bounds.")]
        [SerializeField, Range(0.5f, 0.9f)] private float _targetPivotHeightRatio = 0.70f;
        [SerializeField] private float _followSmoothTime = 0.08f;

        [Header("Obstruction Settings (Provisional)")]
        [Tooltip("Standard world raycast layers are included by default. Player hierarchy colliders and triggers are ignored at runtime.")]
        [SerializeField] private LayerMask _obstructionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float _obstructionSphereRadius = 0.20f;
        [SerializeField] private float _nearClipPadding = 0.08f;
        [SerializeField] private float _obstructionRestoreSmoothTime = 0.18f;

        [Header("Orbit Settings (Provisional)")]
        [Tooltip("Mouse orbit degrees per pixel. Values from the legacy scale are normalized at runtime.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _mouseSensitivity = 0.12f;
        [Tooltip("Gamepad right-stick orbit rate in degrees per second.")]
        [SerializeField] private float _gamepadOrbitDegreesPerSecond = 120f;
        [Tooltip("World-space zoom distance per raw mouse-wheel unit.")]
        [SerializeField] private float _zoomSensitivity = 0.008f;
        [SerializeField] private float _pitch = 14f;
        [SerializeField] private float _yaw;
        [SerializeField] private float _minPitch = -10f;
        [SerializeField] private float _maxPitch = 45f;
        [SerializeField] private float _minDistance = 3.2f;
        [SerializeField] private float _maxDistance = 10.5f;

        [Header("Recenter Settings (Provisional)")]
        [SerializeField] private float _recenterSmoothTime = 0.12f;

        [Header("Touch Settings")]
        [SerializeField] private float _touchOrbitSensitivity = 0.12f;
        [SerializeField] private float _touchZoomSensitivity = 0.012f;
        [SerializeField] private float _touchOrbitScreenMinX = 0.42f;

        private float _lastPinchDistance = -1f;
        private float _shakeTime;
        private float _shakeDuration;
        private float _shakeStrength;
        private bool _inspectionMode;
        private bool _cinematicMode;
        private float _storedDistance;
        private float _storedHeightOffset;
        private float _storedPitch;
        private Vector3 _positionVelocity;
        private Vector3 _cinematicTargetPosition;
        private Vector3 _cinematicLookAt;
        private Vector3 _cinematicPositionVelocity;
        private float _cinematicFieldOfView;
        private float _cinematicFovVelocity;
        private float _cinematicSmoothTime = 0.16f;
        private float _defaultFieldOfView = 42f;
        private float _focusHeightOffset = 1f;
        private UnityEngine.Camera _camera;
        private IDisposable _inspectionCursorOwnership;
        private IDisposable _manualCursorOwnership;
        private int _presentationSuspensionCount;
        private int _presentationSuspensionGeneration;
        private readonly RaycastHit[] _obstructionHits =
            new RaycastHit[ObstructionHitCapacity];
        private readonly Collider[] _obstructionOverlaps =
            new Collider[ObstructionHitCapacity];
        private bool _recoveringFromObstruction;
        private CharacterController _targetController;
        private ChampionController _targetChampionController;
        private bool _useCharacterControllerPivot = true;
        private bool _mouseOrbitActive;
        private bool _ignoreNextMouseDelta;
        private Vector2 _cursorPositionBeforeOrbit;
        private bool _recenterActive;
        private float _recenterPitch;
        private float _recenterYawVelocity;
        private float _recenterPitchVelocity;

        public void Configure(Transform target, float distance, float heightOffset, float pitch, float yaw)
        {
            // Preserve the explicit-pivot contract for existing callers. New champion
            // routes should use ConfigureChampion so framing derives from the collider.
            _useCharacterControllerPivot = false;
            ApplyConfiguration(target, distance, heightOffset, pitch, yaw);
        }

        public void ConfigureChampion(Transform target)
        {
            _useCharacterControllerPivot = true;
            float initialYaw = target != null ? target.eulerAngles.y : _yaw;
            ApplyConfiguration(
                target,
                _distance,
                _heightOffset,
                _pitch,
                initialYaw);
        }

        public void RequestRecenter()
        {
            if (_target == null)
            {
                return;
            }

            _recenterActive = true;
            _recenterYawVelocity = 0f;
            _recenterPitchVelocity = 0f;
        }

        /// <summary>
        /// Applies a raw mouse orbit delta. The input layer owns RMB/UI gating; this
        /// method owns the persistent yaw/pitch camera response.
        /// </summary>
        public void ApplyMouseOrbitDelta(Vector2 lookDelta)
        {
            if (lookDelta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            float degreesPerPixel = ResolveMouseDegreesPerPixel();
            _yaw += lookDelta.x * degreesPerPixel;
            _pitch = Mathf.Clamp(
                _pitch - lookDelta.y * degreesPerPixel,
                ResolveMinimumPitch(),
                _maxPitch);
            CancelRecenter();
        }

        /// <summary>
        /// Applies the existing raw wheel convention and clamps to the configured
        /// camera-distance envelope.
        /// </summary>
        public void ApplyZoomDelta(float scrollDelta)
        {
            if (Mathf.Abs(scrollDelta) <= 0.001f)
            {
                return;
            }

            _distance = Mathf.Clamp(
                _distance - scrollDelta * _zoomSensitivity,
                _minDistance,
                _maxDistance);
        }

        private void ApplyConfiguration(
            Transform target,
            float distance,
            float heightOffset,
            float pitch,
            float yaw)
        {
            BindTarget(target);
            _distance = Mathf.Max(1.5f, distance);
            _minDistance = Mathf.Min(_minDistance, _distance);
            _maxDistance = Mathf.Max(_maxDistance, _distance);
            _heightOffset = heightOffset;
            _pitch = Mathf.Clamp(pitch, ResolveMinimumPitch(), _maxPitch);
            _yaw = yaw;
            _recenterPitch = _pitch;
            _recenterActive = false;
            _recenterYawVelocity = 0f;
            _recenterPitchVelocity = 0f;
            _positionVelocity = Vector3.zero;
            _recoveringFromObstruction = false;
            UpdateFocusHeightOffset();
        }

        public void AddShake(float strength, float duration)
        {
            _shakeStrength = Mathf.Max(_shakeStrength, Mathf.Max(0f, strength));
            _shakeDuration = Mathf.Max(_shakeDuration, Mathf.Max(0.01f, duration));
            _shakeTime = _shakeDuration;
        }

        public IDisposable AcquirePresentationSuspension(string owner)
        {
            _presentationSuspensionCount++;
            return new PresentationSuspensionToken(
                this,
                _presentationSuspensionGeneration);
        }

        public void SnapToTarget()
        {
            if (!TryCalculateDesiredPose(out Vector3 position, out Quaternion rotation))
            {
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
            _positionVelocity = Vector3.zero;
        }

        public void SetCinematicShot(Vector3 position, Vector3 lookAt, float fieldOfView, float smoothTime)
        {
            EnsureCamera();
            EndMouseOrbit(restoreCursorPosition: false);
            _cinematicMode = true;
            _cinematicTargetPosition = position;
            _cinematicLookAt = lookAt;
            _cinematicFieldOfView = Mathf.Clamp(fieldOfView, 28f, 62f);
            _cinematicSmoothTime = Mathf.Clamp(smoothTime, 0.04f, 0.40f);
        }

        public void ClearCinematicShot()
        {
            EnsureCamera();
            _cinematicMode = false;
            _cinematicPositionVelocity = Vector3.zero;
            _positionVelocity = Vector3.zero;
            if (_camera != null)
            {
                _camera.fieldOfView = _defaultFieldOfView;
            }

            EnsureFreeCursor();
        }

        public void SetInspectionMode(bool enabled)
        {
            if (_inspectionMode == enabled)
            {
                return;
            }

            if (enabled)
            {
                _inspectionCursorOwnership ??=
                    ChampionHudCameraGate.AcquireCursorOwnership("camera-inspection");
            }

            EndMouseOrbit(restoreCursorPosition: true);
            CancelRecenter();
            _inspectionMode = enabled;
            if (enabled)
            {
                ClearCinematicShot();
                _storedDistance = _distance;
                _storedHeightOffset = _heightOffset;
                _storedPitch = _pitch;
                _distance = 4.4f;
                _heightOffset = 1.55f;
                _pitch = Mathf.Clamp(8f, ResolveMinimumPitch(), _maxPitch);
            }
            else
            {
                _distance = Mathf.Max(_minDistance, _storedDistance);
                _heightOffset = _storedHeightOffset;
                _pitch = Mathf.Clamp(
                    _storedPitch,
                    ResolveMinimumPitch(),
                    _maxPitch);
                _inspectionCursorOwnership?.Dispose();
                _inspectionCursorOwnership = null;
            }
        }

        private void Start()
        {
            EnsureCamera();
            _recenterPitch = Mathf.Clamp(
                _pitch,
                ResolveMinimumPitch(),
                _maxPitch);
            EnsureFreeCursor();
        }

        private void OnDisable()
        {
            _presentationSuspensionCount = 0;
            _presentationSuspensionGeneration =
                unchecked(_presentationSuspensionGeneration + 1);
            EndMouseOrbit(restoreCursorPosition: false);
            if (_inspectionMode)
            {
                SetInspectionMode(false);
            }

            _manualCursorOwnership?.Dispose();
            _manualCursorOwnership = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                EndMouseOrbit(restoreCursorPosition: false);
            }
        }

        private void Update()
        {
            if (_presentationSuspensionCount > 0)
            {
                return;
            }

            ApplyCursorModeToggle(GameInput.CursorModePressed());
            ChampionHudCameraGate.ReapplyCursorState();

            if (_cinematicMode)
            {
                EndMouseOrbit(restoreCursorPosition: false);
                return;
            }

            int touchCount = GameInput.TouchCount;
            bool manualOrbit = HandlePointerAndGamepadInput(touchCount);
            manualOrbit |= HandleTouchInput(touchCount);
            if (manualOrbit)
            {
                CancelRecenter();
            }
            else if (CanAcceptRecenterInput() && GameInput.CameraRecenterPressed())
            {
                RequestRecenter();
            }

            UpdateRecenter();
            _pitch = Mathf.Clamp(_pitch, ResolveMinimumPitch(), _maxPitch);
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
        }

        public void ApplyCursorModeToggle(bool pressed)
        {
            if (!pressed)
            {
                return;
            }

            if (_manualCursorOwnership == null)
            {
                _manualCursorOwnership =
                    ChampionHudCameraGate.AcquireCursorOwnership("camera-manual");
                return;
            }

            _manualCursorOwnership.Dispose();
            _manualCursorOwnership = null;
        }

        private bool ShouldIgnoreManualCameraInput() =>
            ChampionHudCameraGate.BlocksLook ||
            GameInput.GameplaySuppressed;

        private bool HandlePointerAndGamepadInput(int touchCount)
        {
            if (touchCount > 0)
            {
                EndMouseOrbit(restoreCursorPosition: true);
                return false;
            }

            bool inputBlocked = ShouldIgnoreManualCameraInput();
            Mouse mouse = Mouse.current;
            if (_mouseOrbitActive &&
                (inputBlocked || mouse == null || !mouse.rightButton.isPressed))
            {
                EndMouseOrbit(restoreCursorPosition: true);
            }

            if (!_mouseOrbitActive &&
                !inputBlocked &&
                mouse != null &&
                mouse.rightButton.wasPressedThisFrame &&
                !ChampionHudCameraGate.IsPointerOverUi())
            {
                BeginMouseOrbit(mouse);
            }

            if (!_mouseOrbitActive)
            {
                EnsureFreeCursor();
            }

            bool appliedOrbit = false;
            if (!inputBlocked)
            {
                Vector2 look = GameInput.ReadLook();
                bool gamepadLook = GameInput.Look.activeControl != null &&
                                   GameInput.Look.activeControl.device is Gamepad;
                if (gamepadLook && look.sqrMagnitude > 0.000001f)
                {
                    float degreesThisFrame = Mathf.Max(
                        0f,
                        _gamepadOrbitDegreesPerSecond) * Time.unscaledDeltaTime;
                    _yaw += look.x * degreesThisFrame;
                    _pitch -= look.y * degreesThisFrame;
                    appliedOrbit = true;
                }
                else if (_mouseOrbitActive)
                {
                    if (_ignoreNextMouseDelta)
                    {
                        _ignoreNextMouseDelta = false;
                    }
                    else if (look.sqrMagnitude > 0.000001f)
                    {
                        ApplyMouseOrbitDelta(look);
                        appliedOrbit = true;
                    }
                }
            }

            if (!inputBlocked && !ChampionHudCameraGate.IsPointerOverUi())
            {
                float wheel = GameInput.ReadScroll();
                if (Mathf.Abs(wheel) > 0.001f)
                {
                    ApplyZoomDelta(wheel);
                }
            }

            return appliedOrbit;
        }

        private bool HandleTouchInput(int touchCount)
        {
            if (touchCount == 0 || ShouldIgnoreManualCameraInput())
            {
                _lastPinchDistance = -1f;
                return false;
            }

            if (touchCount >= 2)
            {
                EnhancedTouch first = GameInput.GetTouch(0);
                EnhancedTouch second = GameInput.GetTouch(1);
                float pinchDistance = Vector2.Distance(first.screenPosition, second.screenPosition);
                if (_lastPinchDistance > 0f)
                {
                    _distance -= (pinchDistance - _lastPinchDistance) * _touchZoomSensitivity;
                }

                _lastPinchDistance = pinchDistance;
                return false;
            }

            _lastPinchDistance = -1f;
            EnhancedTouch touch = GameInput.GetTouch(0);
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Moved || touch.screenPosition.x < Screen.width * _touchOrbitScreenMinX || IsTouchOverUi(touch))
            {
                return false;
            }

            _yaw += touch.delta.x * _touchOrbitSensitivity;
            _pitch -= touch.delta.y * _touchOrbitSensitivity;
            return true;
        }

        private void BeginMouseOrbit(Mouse mouse)
        {
            _cursorPositionBeforeOrbit = mouse.position.ReadValue();
            _mouseOrbitActive = true;
            _ignoreNextMouseDelta = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void EndMouseOrbit(bool restoreCursorPosition)
        {
            bool wasOrbiting = _mouseOrbitActive;
            _mouseOrbitActive = false;
            _ignoreNextMouseDelta = false;
            EnsureFreeCursor();

            Mouse mouse = Mouse.current;
            if (wasOrbiting && restoreCursorPosition && mouse != null)
            {
                mouse.WarpCursorPosition(_cursorPositionBeforeOrbit);
            }
        }

        private static void EnsureFreeCursor()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }

            bool shouldShowCursor = Mouse.current != null;
            if (Cursor.visible != shouldShowCursor)
            {
                Cursor.visible = shouldShowCursor;
            }
        }

        private float ResolveMouseDegreesPerPixel()
        {
            float serializedSensitivity = Mathf.Max(0f, _mouseSensitivity);
            return serializedSensitivity > 0.5f
                ? serializedSensitivity * LegacyMouseSensitivityScale
                : serializedSensitivity;
        }

        private float ResolveMinimumPitch()
        {
            // A free camera can deliberately look upward, but a grounded champion
            // camera must retain a shallow downward view of its physical support.
            // Letting the arm point below the upper-body pivot forces long zooms
            // into one-sided terrain and produces the familiar MMO "grey void".
            return _targetChampionController != null &&
                   _targetChampionController.TerrainSafetySupportReady
                ? Mathf.Max(_minPitch, TerrainOrbitMinimumPitch)
                : _minPitch;
        }

        private bool CanAcceptRecenterInput()
        {
            if (ChampionHudCameraGate.BlocksLook || GameInput.GameplaySuppressed)
            {
                return false;
            }

            var activeControl = GameInput.CameraRecenter.activeControl;
            return !(activeControl != null && activeControl.device is Mouse) ||
                   !ChampionHudCameraGate.IsPointerOverUi();
        }

        private void CancelRecenter()
        {
            _recenterActive = false;
            _recenterYawVelocity = 0f;
            _recenterPitchVelocity = 0f;
        }

        private void UpdateRecenter()
        {
            if (!_recenterActive || _target == null)
            {
                return;
            }

            float targetYaw = _target.eulerAngles.y;
            float smoothTime = Mathf.Max(0.001f, _recenterSmoothTime);
            _yaw = Mathf.SmoothDampAngle(
                _yaw,
                targetYaw,
                ref _recenterYawVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            _pitch = Mathf.SmoothDampAngle(
                _pitch,
                _recenterPitch,
                ref _recenterPitchVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            bool yawAligned = Mathf.Abs(Mathf.DeltaAngle(_yaw, targetYaw)) < 0.15f;
            bool pitchAligned = Mathf.Abs(Mathf.DeltaAngle(_pitch, _recenterPitch)) < 0.15f;
            if (!yawAligned || !pitchAligned)
            {
                return;
            }

            _yaw = targetYaw;
            _pitch = _recenterPitch;
            CancelRecenter();
        }

        private static bool IsTouchOverUi(EnhancedTouch touch)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId);
        }

        private void LateUpdate()
        {
            if (_presentationSuspensionCount > 0)
            {
                return;
            }

            if (_cinematicMode)
            {
                UpdateCinematicCamera();
                return;
            }

            // Fallback for legacy/demo routes that do not configure a target.
            if (_target == null)
            {
                var controller = FindObjectOfType<AL.ChampionMode.Control.ChampionController>();
                if (controller != null)
                {
                    BindTarget(controller.transform);
                    GameDebug.Log("<color=green>[Camera] Type-based lock achieved on ChampionController.</color>");
                }
            }

            if (_target != null)
            {
                Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
                Vector3 negDistance = new Vector3(0.0f, 0.0f, -_distance);
                Vector3 pivot = ResolveTargetPivot();
                Vector3 desiredPosition = (rotation * negDistance) + pivot;
                Vector3 collisionSafePosition = ResolveObstructedPosition(
                    pivot,
                    desiredPosition,
                    out bool obstructed);
                Vector3 shakeOffset = Vector3.zero;
                if (_shakeTime > 0f)
                {
                    float shakePercent = _shakeTime / Mathf.Max(0.01f, _shakeDuration);
                    shakeOffset = Random.insideUnitSphere * (_shakeStrength * shakePercent);
                    _shakeTime = Mathf.Max(0f, _shakeTime - Time.unscaledDeltaTime);
                }

                Vector3 followedPosition;
                if (obstructed)
                {
                    // Collision truth wins immediately when geometry enters the camera
                    // path. Only the outward recovery is smoothed.
                    followedPosition = collisionSafePosition;
                    _positionVelocity = Vector3.zero;
                    _recoveringFromObstruction = true;
                }
                else
                {
                    float smoothTime = _recoveringFromObstruction
                        ? Mathf.Max(_followSmoothTime, _obstructionRestoreSmoothTime)
                        : _followSmoothTime;
                    followedPosition = Vector3.SmoothDamp(
                        transform.position,
                        desiredPosition,
                        ref _positionVelocity,
                        Mathf.Max(0.001f, smoothTime));

                    if (_recoveringFromObstruction &&
                        (followedPosition - desiredPosition).sqrMagnitude <= 0.0004f)
                    {
                        followedPosition = desiredPosition;
                        _positionVelocity = Vector3.zero;
                        _recoveringFromObstruction = false;
                    }
                }

                // Resolve again after shake and follow smoothing so neither can push the
                // near clip plane back through geometry.
                Vector3 finalPosition = ResolveObstructedPosition(
                    pivot,
                    followedPosition + shakeOffset,
                    out bool finalObstructed);
                finalPosition = ResolveTerrainSupportedPosition(
                    finalPosition,
                    out bool terrainAdjusted);
                if (finalObstructed || terrainAdjusted)
                {
                    _recoveringFromObstruction = true;
                    _positionVelocity = Vector3.zero;
                }

                transform.position = finalPosition;
                Vector3 lookDirection = ResolveOpticalFocusPoint(pivot) - finalPosition;
                transform.rotation = lookDirection.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                    : rotation;
            }
            else
            {
                // Visual feedback that the camera is searching
                transform.LookAt(Vector3.zero);
                if (Time.frameCount % 60 == 0)
                {
                    Debug.LogError("CAMERA ERROR: Still cannot find a Champion in the scene! Ensure WorldBuilder spawned the hero.");
                }
            }
        }

        private void BindTarget(Transform target)
        {
            _target = target;
            _targetController = target != null
                ? target.GetComponent<CharacterController>()
                : null;
            _targetChampionController = target != null
                ? target.GetComponent<ChampionController>()
                : null;
        }

        private Vector3 ResolveTerrainSupportedPosition(
            Vector3 candidatePosition,
            out bool adjusted)
        {
            adjusted = false;
            TerrainCollider terrainCollider = _targetChampionController != null
                ? _targetChampionController.TerrainSafetySupport
                : null;
            if (terrainCollider == null ||
                !terrainCollider.enabled ||
                !terrainCollider.gameObject.activeInHierarchy)
            {
                return candidatePosition;
            }

            Terrain terrain = terrainCollider.GetComponent<Terrain>();
            TerrainData terrainData = terrain != null
                ? terrain.terrainData
                : terrainCollider.terrainData;
            if (terrainData == null)
            {
                return candidatePosition;
            }

            Vector3 origin = terrainCollider.transform.position;
            Vector3 size = terrainData.size;
            if (size.x <= 0f ||
                size.z <= 0f)
            {
                return candidatePosition;
            }

            float footprintRadius = ResolveObstructionRadius();
            float minimumX = origin.x + footprintRadius;
            float maximumX = origin.x + size.x - footprintRadius;
            float minimumZ = origin.z + footprintRadius;
            float maximumZ = origin.z + size.z - footprintRadius;
            if (minimumX > maximumX || minimumZ > maximumZ)
            {
                return candidatePosition;
            }

            float supportedX = Mathf.Clamp(
                candidatePosition.x,
                minimumX,
                maximumX);
            float supportedZ = Mathf.Clamp(
                candidatePosition.z,
                minimumZ,
                maximumZ);
            if (!Mathf.Approximately(candidatePosition.x, supportedX) ||
                !Mathf.Approximately(candidatePosition.z, supportedZ))
            {
                candidatePosition.x = supportedX;
                candidatePosition.z = supportedZ;
                adjusted = true;
            }

            int holesResolution = terrainData.holesResolution;
            if (holesResolution > 0)
            {
                float normalizedX = Mathf.Clamp01(
                    (candidatePosition.x - origin.x) / size.x);
                float normalizedZ = Mathf.Clamp01(
                    (candidatePosition.z - origin.z) / size.z);
                int holeX = Mathf.Min(
                    Mathf.FloorToInt(normalizedX * holesResolution),
                    holesResolution - 1);
                int holeZ = Mathf.Min(
                    Mathf.FloorToInt(normalizedZ * holesResolution),
                    holesResolution - 1);
                if (terrainData.IsHole(holeX, holeZ))
                {
                    return candidatePosition;
                }
            }

            float surfaceY = terrain != null
                ? terrain.SampleHeight(candidatePosition) + origin.y
                : origin.y + terrainData.GetInterpolatedHeight(
                    Mathf.Clamp01((candidatePosition.x - origin.x) / size.x),
                    Mathf.Clamp01((candidatePosition.z - origin.z) / size.z));
            float minimumCameraY = surfaceY +
                                   footprintRadius +
                                   TerrainSupportedSideEpsilon;
            if (candidatePosition.y >= minimumCameraY)
            {
                return candidatePosition;
            }

            candidatePosition.y = minimumCameraY;
            adjusted = true;
            return candidatePosition;
        }

        private Vector3 ResolveTargetPivot()
        {
            if (_target == null)
            {
                return Vector3.zero;
            }

            if (_useCharacterControllerPivot &&
                _targetController != null &&
                _targetController.enabled &&
                _targetController.gameObject.activeInHierarchy)
            {
                Bounds bounds = _targetController.bounds;
                if (bounds.size.sqrMagnitude > 0.000001f)
                {
                    float heightRatio = Mathf.Clamp(
                        _targetPivotHeightRatio,
                        0.5f,
                        0.9f);
                    return new Vector3(
                        bounds.center.x,
                        Mathf.Lerp(bounds.min.y, bounds.max.y, heightRatio),
                        bounds.center.z);
                }
            }

            return _target.position + Vector3.up * _heightOffset;
        }

        private Vector3 ResolveTargetFocusPoint()
        {
            return _target != null
                ? _target.position + Vector3.up * _focusHeightOffset
                : Vector3.zero;
        }

        private Vector3 ResolveOpticalFocusPoint(Vector3 resolvedPivot)
        {
            return _useCharacterControllerPivot
                ? resolvedPivot
                : ResolveTargetFocusPoint();
        }

        private Vector3 ResolveObstructedPosition(
            Vector3 pivot,
            Vector3 candidatePosition,
            out bool obstructed)
        {
            Vector3 offset = candidatePosition - pivot;
            float distance = offset.magnitude;
            if (distance <= 0.001f || _obstructionMask.value == 0)
            {
                obstructed = false;
                return candidatePosition;
            }

            Vector3 direction = offset / distance;
            float obstructionRadius = ResolveObstructionRadius();
            int hitCount = Physics.SphereCastNonAlloc(
                pivot,
                obstructionRadius,
                direction,
                _obstructionHits,
                distance,
                _obstructionMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = distance;
            obstructed = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _obstructionHits[index];
                if (ShouldIgnoreObstruction(hit.collider))
                {
                    continue;
                }

                if (!obstructed || hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    obstructed = true;
                }
            }

            Vector3 resolvedPosition = candidatePosition;
            if (obstructed)
            {
                float safeDistance = Mathf.Max(
                    0f,
                    nearestDistance - ObstructionSurfaceEpsilon);
                resolvedPosition = pivot + direction * safeDistance;
            }

            resolvedPosition = ResolveStartingOverlap(
                pivot,
                resolvedPosition,
                direction,
                obstructionRadius,
                out bool depenetrated);
            obstructed |= depenetrated;
            return resolvedPosition;
        }

        private Vector3 ResolveStartingOverlap(
            Vector3 pivot,
            Vector3 candidatePosition,
            Vector3 armDirection,
            float worldRadius,
            out bool depenetrated)
        {
            if (!HasObstructionOverlap(candidatePosition, worldRadius))
            {
                depenetrated = false;
                return candidatePosition;
            }

            depenetrated = true;
            float candidateDistance = Vector3.Distance(pivot, candidatePosition);
            if (!HasObstructionOverlap(pivot, worldRadius))
            {
                float safeDistance = 0f;
                float blockedDistance = candidateDistance;
                for (int step = 0;
                     step < ObstructionOverlapRefinementSteps;
                     step++)
                {
                    float midpoint = (safeDistance + blockedDistance) * 0.5f;
                    if (HasObstructionOverlap(
                            pivot + armDirection * midpoint,
                            worldRadius))
                    {
                        blockedDistance = midpoint;
                    }
                    else
                    {
                        safeDistance = midpoint;
                    }
                }

                return pivot + armDirection * Mathf.Max(
                    0f,
                    safeDistance - ObstructionSurfaceEpsilon);
            }

            // Physics casts do not report a collider that already overlaps their
            // origin. Preserve the authored rear arm and search outward for the
            // nearest clear sphere before refining back to its boundary.
            float maximumExtension =
                Mathf.Max(_maxDistance, candidateDistance) + worldRadius * 2f;
            float blockedExtension = 0f;
            for (int step = 1; step <= ObstructionOverlapSearchSteps; step++)
            {
                float clearExtension = maximumExtension * step /
                                       ObstructionOverlapSearchSteps;
                Vector3 probePosition = candidatePosition +
                                        armDirection * clearExtension;
                if (HasObstructionOverlap(probePosition, worldRadius))
                {
                    blockedExtension = clearExtension;
                    continue;
                }

                for (int refinement = 0;
                     refinement < ObstructionOverlapRefinementSteps;
                     refinement++)
                {
                    float midpoint = (blockedExtension + clearExtension) * 0.5f;
                    if (HasObstructionOverlap(
                            candidatePosition + armDirection * midpoint,
                            worldRadius))
                    {
                        blockedExtension = midpoint;
                    }
                    else
                    {
                        clearExtension = midpoint;
                    }
                }

                return candidatePosition + armDirection *
                       (clearExtension + ObstructionSurfaceEpsilon);
            }

            if (!HasObstructionOverlap(transform.position, worldRadius))
            {
                return transform.position;
            }

            return candidatePosition;
        }

        private bool HasObstructionOverlap(Vector3 position, float worldRadius)
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(
                position,
                worldRadius,
                _obstructionOverlaps,
                _obstructionMask,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < overlapCount; index++)
            {
                if (!ShouldIgnoreObstruction(_obstructionOverlaps[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private float ResolveObstructionRadius()
        {
            EnsureCamera();
            float cameraNearClipRadius = 0f;
            if (_camera != null)
            {
                float nearClip = Mathf.Max(0.01f, _camera.nearClipPlane);
                float halfHeight = Mathf.Tan(
                    _camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * nearClip;
                float halfWidth = halfHeight * Mathf.Max(0.1f, _camera.aspect);
                cameraNearClipRadius = Mathf.Sqrt(
                    halfWidth * halfWidth +
                    halfHeight * halfHeight +
                    nearClip * nearClip);
            }

            return Mathf.Max(
                0.01f,
                Mathf.Max(_obstructionSphereRadius, cameraNearClipRadius) +
                Mathf.Max(0f, _nearClipPadding));
        }

        private bool ShouldIgnoreObstruction(Collider collider)
        {
            if (collider == null)
            {
                return true;
            }

            Transform hitTransform = collider.transform;
            if (_target != null &&
                (hitTransform == _target || hitTransform.IsChildOf(_target)))
            {
                return true;
            }

            return hitTransform == transform || hitTransform.IsChildOf(transform);
        }

        private void UpdateCinematicCamera()
        {
            EnsureCamera();

            transform.position = Vector3.SmoothDamp(transform.position, _cinematicTargetPosition, ref _cinematicPositionVelocity, _cinematicSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            Vector3 lookDirection = _cinematicLookAt - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.unscaledDeltaTime * 7.5f);
            }

            if (_camera != null)
            {
                _camera.fieldOfView = Mathf.SmoothDamp(_camera.fieldOfView, _cinematicFieldOfView, ref _cinematicFovVelocity, _cinematicSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            }
        }

        private void EnsureCamera()
        {
            if (_camera == null)
            {
                _camera = GetComponent<UnityEngine.Camera>();
                if (_camera != null)
                {
                    _defaultFieldOfView = _camera.fieldOfView;
                }
            }
        }

        private bool TryCalculateDesiredPose(
            out Vector3 position,
            out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            if (_target == null)
            {
                return false;
            }

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = ResolveTargetPivot();
            position =
                orbit * new Vector3(0f, 0f, -_distance) +
                pivot;
            Vector3 focus = ResolveOpticalFocusPoint(pivot);
            Vector3 lookDirection = focus - position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }

            return true;
        }

        private void ReleasePresentationSuspension(int generation)
        {
            if (generation != _presentationSuspensionGeneration ||
                _presentationSuspensionCount <= 0)
            {
                return;
            }

            _presentationSuspensionCount--;
        }

        private sealed class PresentationSuspensionToken : IDisposable
        {
            private CameraFollow _owner;
            private readonly int _generation;

            public PresentationSuspensionToken(CameraFollow owner, int generation)
            {
                _owner = owner;
                _generation = generation;
            }

            public void Dispose()
            {
                CameraFollow owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.ReleasePresentationSuspension(_generation);
            }
        }

        private void UpdateFocusHeightOffset()
        {
            _focusHeightOffset = 1f;
            if (_target == null)
            {
                return;
            }

            Renderer[] renderers = _target.GetComponentsInChildren<Renderer>(false);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                _focusHeightOffset = bounds.center.y - _target.position.y;
            }
        }
    }
}
