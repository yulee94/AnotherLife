using UnityEngine;

namespace AL.ChampionMode.Camera
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0, 2, -4);

        [Header("Smoothness")]
        [SerializeField] private float _smoothSpeed = 0.125f;
        [SerializeField] private bool _lookAtTarget = true;

        [Header("Rotation")]
        [SerializeField] private float _rotationSpeed = 5f;

        private void LateUpdate()
        {
            if (_target == null)
            {
                // Auto-find player if target is missing
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _target = player.transform;
                else return;
            }

            HandleMovement();
            if (_lookAtTarget) HandleRotation();
        }

        private void HandleMovement()
        {
            // Calculate desired position relative to target's rotation
            Vector3 desiredPosition = _target.TransformPoint(_offset);

            // Smoothly interpolate to the desired position
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed);
            transform.position = smoothedPosition;
        }

        private void HandleRotation()
        {
            // Smoothly look at the target or maintain offset rotation
            Quaternion targetRotation = Quaternion.LookRotation(_target.position - transform.position + Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
}
