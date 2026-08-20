using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using AL.ChampionMode.UI;
using AL.Input;
using AL.Core;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace AL.ChampionMode.Camera
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 5.0f;
        [SerializeField] private float _heightOffset = 1.5f;
        [SerializeField] private float _followSmoothTime = 0.08f;

        [Header("Mouse Settings")]
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _zoomSensitivity = 4.5f;
        [SerializeField] private float _minPitch = -20f;
        [SerializeField] private float _maxPitch = 60f;
        [SerializeField] private float _minDistance = 5.2f;
        [SerializeField] private float _maxDistance = 12.5f;

        [Header("Touch Settings")]
        [SerializeField] private float _touchOrbitSensitivity = 0.12f;
        [SerializeField] private float _touchZoomSensitivity = 0.012f;
        [SerializeField] private float _touchOrbitScreenMinX = 0.42f;

        private float _yaw = 0f;
        private float _pitch = 0f;
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
        private UnityEngine.Camera _camera;

        public void Configure(Transform target, float distance, float heightOffset, float pitch, float yaw)
        {
            _target = target;
            _distance = Mathf.Max(1.5f, distance);
            _minDistance = Mathf.Min(_minDistance, _distance);
            _maxDistance = Mathf.Max(_maxDistance, _distance);
            _heightOffset = heightOffset;
            _pitch = Mathf.Clamp(pitch, _minPitch, _maxPitch);
            _yaw = yaw;
        }

        public void AddShake(float strength, float duration)
        {
            _shakeStrength = Mathf.Max(_shakeStrength, Mathf.Max(0f, strength));
            _shakeDuration = Mathf.Max(_shakeDuration, Mathf.Max(0.01f, duration));
            _shakeTime = _shakeDuration;
        }

        public void SetCinematicShot(Vector3 position, Vector3 lookAt, float fieldOfView, float smoothTime)
        {
            EnsureCamera();
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
        }

        public void SetInspectionMode(bool enabled)
        {
            if (_inspectionMode == enabled)
            {
                return;
            }

            _inspectionMode = enabled;
            if (enabled)
            {
                ClearCinematicShot();
                _storedDistance = _distance;
                _storedHeightOffset = _heightOffset;
                _storedPitch = _pitch;
                _distance = 4.4f;
                _heightOffset = 1.55f;
                _pitch = Mathf.Clamp(8f, _minPitch, _maxPitch);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                _distance = Mathf.Max(_minDistance, _storedDistance);
                _heightOffset = _storedHeightOffset;
                _pitch = Mathf.Clamp(_storedPitch, _minPitch, _maxPitch);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Start()
        {
            EnsureCamera();
            // Lock and hide cursor for action feel
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (_cinematicMode)
            {
                return;
            }

            HandleMouseInput();
            HandleTouchInput();
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
        }

        private void HandleMouseInput()
        {
            if (GameInput.TouchCount > 0)
            {
                return;
            }

            bool canOrbit = !_inspectionMode || (Mouse.current != null && Mouse.current.rightButton.isPressed);
            if (canOrbit && !ChampionHudCameraGate.ShouldIgnoreLook())
            {
                Vector2 look = GameInput.ReadLook();
                _yaw += look.x * _mouseSensitivity;
                _pitch -= look.y * _mouseSensitivity;
            }

            if (ChampionHudCameraGate.ShouldIgnoreLook())
            {
                return;
            }

            float wheel = GameInput.ReadScroll();
            if (Mathf.Abs(wheel) > 0.001f)
            {
                _distance -= wheel * _zoomSensitivity;
            }
        }

        private void HandleTouchInput()
        {
            if (GameInput.TouchCount == 0)
            {
                _lastPinchDistance = -1f;
                return;
            }

            if (GameInput.TouchCount >= 2)
            {
                EnhancedTouch first = GameInput.GetTouch(0);
                EnhancedTouch second = GameInput.GetTouch(1);
                float pinchDistance = Vector2.Distance(first.screenPosition, second.screenPosition);
                if (_lastPinchDistance > 0f)
                {
                    _distance -= (pinchDistance - _lastPinchDistance) * _touchZoomSensitivity;
                }

                _lastPinchDistance = pinchDistance;
                return;
            }

            _lastPinchDistance = -1f;
            EnhancedTouch touch = GameInput.GetTouch(0);
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Moved || touch.screenPosition.x < Screen.width * _touchOrbitScreenMinX || IsTouchOverUi(touch))
            {
                return;
            }

            _yaw += touch.delta.x * _touchOrbitSensitivity;
            _pitch -= touch.delta.y * _touchOrbitSensitivity;
        }

        private static bool IsTouchOverUi(EnhancedTouch touch)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.touchId);
        }

        private void LateUpdate()
        {
            if (_cinematicMode)
            {
                UpdateCinematicCamera();
                return;
            }

            // 1. Bulletproof Type-Based Discovery
            if (_target == null)
            {
                var controller = FindObjectOfType<AL.ChampionMode.Control.ChampionController>();
                if (controller != null)
                {
                    _target = controller.transform;
                    GameDebug.Log("<color=green>[Camera] Type-based lock achieved on ChampionController.</color>");
                }
            }

            // 2. Force Positional Logic
            if (_target != null)
            {
                Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
                Vector3 negDistance = new Vector3(0.0f, 0.0f, -_distance);
                Vector3 position = (rotation * negDistance) + _target.position + new Vector3(0, _heightOffset, 0);
                Vector3 shakeOffset = Vector3.zero;
                if (_shakeTime > 0f)
                {
                    float shakePercent = _shakeTime / Mathf.Max(0.01f, _shakeDuration);
                    shakeOffset = Random.insideUnitSphere * (_shakeStrength * shakePercent);
                    _shakeTime = Mathf.Max(0f, _shakeTime - Time.unscaledDeltaTime);
                }

                transform.rotation = rotation;
                transform.position = Vector3.SmoothDamp(transform.position, position, ref _positionVelocity, _followSmoothTime) + shakeOffset;
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
    }
}
