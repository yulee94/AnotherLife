using UnityEngine;
using UnityEngine.EventSystems;

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
        private float _storedDistance;
        private float _storedHeightOffset;
        private float _storedPitch;
        private Vector3 _positionVelocity;

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

        public void SetInspectionMode(bool enabled)
        {
            if (_inspectionMode == enabled)
            {
                return;
            }

            _inspectionMode = enabled;
            if (enabled)
            {
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
            // Lock and hide cursor for action feel
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleMouseInput();
            HandleTouchInput();
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);

            // Escape key release
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMouseInput()
        {
            if (Input.touchCount > 0)
            {
                return;
            }

            bool canOrbit = !_inspectionMode || Input.GetMouseButton(1);
            if (canOrbit)
            {
                _yaw += Input.GetAxis("Mouse X") * _mouseSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * _mouseSensitivity;
            }

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f)
            {
                _distance -= wheel * _zoomSensitivity;
            }
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 0)
            {
                _lastPinchDistance = -1f;
                return;
            }

            if (Input.touchCount >= 2)
            {
                Touch first = Input.GetTouch(0);
                Touch second = Input.GetTouch(1);
                float pinchDistance = Vector2.Distance(first.position, second.position);
                if (_lastPinchDistance > 0f)
                {
                    _distance -= (pinchDistance - _lastPinchDistance) * _touchZoomSensitivity;
                }

                _lastPinchDistance = pinchDistance;
                return;
            }

            _lastPinchDistance = -1f;
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Moved || touch.position.x < Screen.width * _touchOrbitScreenMinX || IsTouchOverUi(touch))
            {
                return;
            }

            _yaw += touch.deltaPosition.x * _touchOrbitSensitivity;
            _pitch -= touch.deltaPosition.y * _touchOrbitSensitivity;
        }

        private static bool IsTouchOverUi(Touch touch)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
        }

        private void LateUpdate()
        {
            // 1. Bulletproof Type-Based Discovery
            if (_target == null)
            {
                var controller = FindObjectOfType<AL.ChampionMode.Control.ChampionController>();
                if (controller != null)
                {
                    _target = controller.transform;
                    Debug.Log("<color=green>[Camera] Type-based lock achieved on ChampionController.</color>");
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
    }
}
