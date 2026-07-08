using UnityEngine;

namespace AL.ChampionMode.Camera
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 5.0f;
        [SerializeField] private float _heightOffset = 1.5f;

        [Header("Mouse Settings")]
        [SerializeField] private float _mouseSensitivity = 3f;
        [SerializeField] private float _minPitch = -20f;
        [SerializeField] private float _maxPitch = 60f;

        private float _yaw = 0f;
        private float _pitch = 0f;

        private void Start()
        {
            // Lock and hide cursor for action feel
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // Gather rotation input regardless of target status
            _yaw += Input.GetAxis("Mouse X") * _mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * _mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);

            // Escape key release
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
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

                transform.rotation = rotation;
                transform.position = position;
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
