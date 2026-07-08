using UnityEngine;
using UnityEngine.UI;
using AL.Core;
using AL.Core.Interfaces;
using System.Collections;
using System.Linq;

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

        [Header("Combat Settings")]
        [SerializeField] private float _attackLungeForce = 2f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 0.5f;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;

        private CharacterController _controller;
        private Vector3 _velocity;
        private bool _isBlocking;
        private bool _isDodging;
        private bool _isAttacking;
        private int _initialEnemyCount;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<CharacterController>();
            }

            _controller.center = new Vector3(0, 1f, 0);
            _controller.height = 2f;
            _controller.radius = 0.5f;
            _controller.stepOffset = 0.3f;

            if (_cameraTransform == null && UnityEngine.Camera.main != null)
                _cameraTransform = UnityEngine.Camera.main.transform;
        }

        private void Start()
        {
            // Record initial enemy count for victory check
            _initialEnemyCount = GameObject.FindObjectsOfType<GameObject>()
                .Count(obj => obj.name.StartsWith("Dummy_"));
        }

        private void Update()
        {
            if (_controller == null || _isDodging) return;

            HandleMovement();
            HandleGravity();
            HandleActions();
        }

        private void HandleMovement()
        {
            if (_isAttacking) return;

            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

            if (direction.magnitude >= 0.1f)
            {
                float targetAngle = 0;
                if (_cameraTransform != null)
                {
                    targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + _cameraTransform.eulerAngles.y;
                }
                else
                {
                    targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                }

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
            if (Input.GetKeyDown(KeyCode.Space)) StartCoroutine(Dodge());
            _isBlocking = Input.GetKey(KeyCode.LeftShift);

            if (Input.GetMouseButtonDown(0) && !_isAttacking)
            {
                StartCoroutine(PerformAttack());
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) UseSkill(0);
        }

        private IEnumerator PerformAttack()
        {
            _isAttacking = true;
            Debug.Log("<color=orange>[Combat] Attacking!</color>");

            // 1. Lunge Forward
            Vector3 lungeDir = transform.forward;
            float lungeTimer = 0f;
            while (lungeTimer < 0.1f)
            {
                _controller.Move(lungeDir * _attackLungeForce * Time.deltaTime * 10f);
                lungeTimer += Time.deltaTime;
                yield return null;
            }

            // 2. Hit Detection
            Vector3 hitCenter = transform.position + transform.forward * 1.5f + Vector3.up;
            Collider[] hitColliders = Physics.OverlapSphere(hitCenter, _attackRange);

            bool hitAnything = false;
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject.name.StartsWith("Dummy_"))
                {
                    hitAnything = true;
                    Debug.Log("<color=red>[Combat] Enemy Defeated!</color>");

                    // Visual Feedback
                    CreateHitVFX(hitCollider.transform.position);

                    Destroy(hitCollider.gameObject);
                    CheckVictory();
                }
            }

            if (!hitAnything) Debug.Log("[Combat] Attack Missed.");

            yield return new WaitForSeconds(_attackCooldown);
            _isAttacking = false;
        }

        private void CreateHitVFX(Vector3 position)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Hit_Flash";
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * 0.5f;
            flash.GetComponent<Renderer>().material.color = Color.white;
            Destroy(flash, 0.2f);
        }

        private void CheckVictory()
        {
            int remaining = GameObject.FindObjectsOfType<GameObject>()
                .Count(obj => obj.name.StartsWith("Dummy_")) - 1; // -1 because current one isn't destroyed yet in this frame

            if (remaining <= 0)
            {
                Debug.Log("<color=gold>[Victory] REALM SECURED!</color>");
                ShowVictoryUI();
            }
        }

        private void ShowVictoryUI()
        {
            GameObject canvasObj = GameObject.Find("DebugUI_Canvas");
            if (canvasObj == null) return;

            GameObject winTextObj = new GameObject("VictoryText");
            winTextObj.transform.SetParent(canvasObj.transform);

            Text text = winTextObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            text.fontSize = 50;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.yellow;
            text.text = "VICTORY\nREALM SECURED";
            text.alignment = TextAnchor.MiddleCenter;

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(600, 200);
        }

        private void UseSkill(int index)
        {
            Debug.Log($"[Champion] Using Skill {index + 1}");
        }

        private IEnumerator Dodge()
        {
            _isDodging = true;
            Vector3 dodgeDir = transform.forward;
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
