using UnityEngine;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.ChampionMode.Control
{
    [RequireComponent(typeof(CharacterController))]
    public class ChampionController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _rotationSpeed = 10f;
        [SerializeField] private float _dodgeDistance = 4f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;

        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isBlocking;
        private bool _isDodging;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_cameraTransform == null && Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (_isDodging) return;

            HandleMovement();
            HandleGravity();
            HandleActions();
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // Mobile joystick logic would feed into these axes
            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

            if (direction.magnitude >= 0.1f)
            {
                // Calculate movement direction relative to camera
                float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + _cameraTransform.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _rotationSpeed, 0.1f);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                _controller.Move(moveDir.normalized * _moveSpeed * Time.deltaTime);
            }
        }

        private void HandleGravity()
        {
            if (_controller.isGrounded && _velocity.y < 0)
                _velocity.y = -2f;

            _velocity.y += _gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleActions()
        {
            // Debug Keyboard Controls
            if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(Dodge());
            _isBlocking = Input.GetKey(KeyCode.LeftShift);

            if (Input.GetMouseButtonDown(0)) Attack();

            // Skills 1-4
            if (Input.GetKeyDown(KeyCode.Alpha1)) UseSkill(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) UseSkill(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) UseSkill(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) UseSkill(3);
        }

        private void Attack()
        {
            if (_isBlocking) return;
            Debug.Log("Performing Basic Attack");
            // Play animation, trigger hitboxes
        }

        private void UseSkill(int index)
        {
            Debug.Log($"Using Skill {index + 1}");
        }

        private System.Collections.IEnumerator Dodge()
        {
            _isDodging = true;
            Vector3 dodgeDir = transform.forward; // Or based on input
            float timer = 0f;
            float duration = 0.2f;

            while (timer < duration)
            {
                _controller.Move(dodgeDir * (_dodgeDistance / duration) * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            _isDodging = false;
        }
    }
}
