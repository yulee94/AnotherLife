using UnityEngine;
using UnityEngine.UI;
using AL.Core;
using AL.ChampionMode.Skills;
using AL.Input;
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
        private bool _controlsLocked;
        private bool _touchBlockHeld;
        private int _initialEnemyCount;
        private Vector2 _externalMoveInput;
        private SkillCaster _skillCaster;
        private RealmId _realmId = RealmId.None;

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

            _skillCaster = GetComponent<SkillCaster>() ?? gameObject.AddComponent<SkillCaster>();
        }

        private void Start()
        {
            RefreshCameraTransform();

            // Record initial enemy count for victory check
            _initialEnemyCount = GameObject.FindObjectsOfType<GameObject>()
                .Count(obj => obj.name.StartsWith("Dummy_"));
        }

        private void Update()
        {
            if (_controller == null || _isDodging) return;

            if (_controlsLocked)
            {
                HandleGravity();
                return;
            }

            HandleMovement();
            HandleGravity();
            HandleActions();
        }

        private void HandleMovement()
        {
            if (_isAttacking) return;
            RefreshCameraTransform();

            Vector2 move = GameInput.ReadMove();
            float horizontal = Mathf.Abs(_externalMoveInput.x) > 0.01f ? _externalMoveInput.x : move.x;
            float vertical = Mathf.Abs(_externalMoveInput.y) > 0.01f ? _externalMoveInput.y : move.y;

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
            if (_controlsLocked || _realmId == RealmId.None)
            {
                return;
            }

            if (GameInput.DodgePressed()) StartCoroutine(Dodge());
            _isBlocking = _touchBlockHeld || GameInput.BlockHeld();

            if (GameInput.AttackPressed() && !_isAttacking)
            {
                StartCoroutine(PerformAttack());
            }

            if (GameInput.SkillPressed(0)) RequestSkill(0);
            if (GameInput.SkillPressed(1)) RequestSkill(1);
            if (GameInput.SkillPressed(2)) RequestSkill(2);
            if (GameInput.SkillPressed(3)) RequestSkill(3);
        }

        private IEnumerator PerformAttack()
        {
            if (_controlsLocked || _realmId == RealmId.None)
            {
                yield break;
            }

            _isAttacking = true;
            GameDebug.Log("<color=orange>[Combat] Attacking!</color>");
            RuntimeCombatAudio.PlayBasicAttack();

            // 1. Lunge Forward
            Vector3 lungeDir = transform.forward;
            float lungeTimer = 0f;
            while (lungeTimer < 0.1f && !_controlsLocked)
            {
                _controller.Move(lungeDir * _attackLungeForce * Time.deltaTime * 10f);
                lungeTimer += Time.deltaTime;
                yield return null;
            }

            if (_controlsLocked)
            {
                _isAttacking = false;
                yield break;
            }

            // 2. Hit Detection
            Vector3 hitCenter = transform.position + transform.forward * 1.5f + Vector3.up;
            Collider[] hitColliders = Physics.OverlapSphere(hitCenter, _attackRange);
            RealmId realmId = _realmId;

            bool hitAnything = false;
            bool hitBoss = false;
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.gameObject.name.StartsWith("Dummy_"))
                {
                    hitAnything = true;
                    GameDebug.Log("<color=red>[Combat] Enemy Defeated!</color>");

                    // Visual Feedback
                    CreateHitVFX(hitCollider.transform.position);
                    SkillEffectFactory.SpawnFloatingCombatText(hitCollider.transform.position + Vector3.up * 1.45f, "KO", new Color(1f, 0.78f, 0.22f), 0.26f, 0.8f);
                    SkillEffectFactory.ShakeCamera(0.10f, 0.10f);
                    SkillEffectFactory.RequestHitPause(0.035f, 0.14f);
                    RuntimeCombatAudio.PlayImpact();

                    Destroy(hitCollider.gameObject);
                    CheckVictory(1);
                }
                else
                {
                    var boss = hitCollider.GetComponentInParent<AL.ChampionMode.AI.BossDummyAI>();
                    if (boss != null && !hitBoss)
                    {
                        hitBoss = true;
                        hitAnything = true;
                        boss.TakeDamage(125f);
                        RuntimeCombatAudio.PlayImpact();
                    }
                }
            }

            if (!hitAnything)
            {
                GameDebug.Log("[Combat] Attack Missed.");
                Vector3 whiffCenter = transform.position + transform.forward * 1.35f;
                SkillEffectFactory.SpawnBasicAttackWhiff(whiffCenter, transform.forward, realmId);
                SkillEffectFactory.SpawnFloatingCombatText(transform.position + Vector3.up * 1.55f + transform.forward * 0.65f, "MISS", new Color(0.68f, 0.76f, 0.86f), 0.20f, 0.55f);
                SkillEffectFactory.ShakeCamera(0.035f, 0.055f);
            }

            yield return new WaitForSeconds(_attackCooldown);
            _isAttacking = false;
        }

        private void CreateHitVFX(Vector3 position)
        {
            SkillEffectFactory.SpawnRealmImpact(position, _realmId);
        }

        public void CheckVictory(int pendingDestroyedDummies = 0)
        {
            int remaining = GameObject.FindObjectsOfType<GameObject>()
                .Count(obj => obj.name.StartsWith("Dummy_")) - pendingDestroyedDummies;

            if (remaining <= 0)
            {
                GameDebug.Log("<color=gold>[Victory] REALM SECURED!</color>");
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
            GameDebug.Log($"[Champion] Using Skill {index + 1}");
            _skillCaster?.TryCastSkill(index);
        }

        private void RefreshCameraTransform()
        {
            if (_cameraTransform == null && UnityEngine.Camera.main != null)
            {
                _cameraTransform = UnityEngine.Camera.main.transform;
            }
        }

        private IEnumerator Dodge()
        {
            if (_controlsLocked || _realmId == RealmId.None)
            {
                yield break;
            }

            _isDodging = true;
            _skillCaster?.CancelCurrentSkill();
            SkillEffectFactory.SpawnDodgeTrail(transform.position + Vector3.up * 0.25f, transform.forward, _realmId);
            RuntimeCombatAudio.PlayDodge();
            Vector3 dodgeDir = transform.forward;
            float timer = 0f;
            float duration = 0.2f;

            while (timer < duration && !_controlsLocked)
            {
                _controller.Move(dodgeDir * (_dodgeDistance / duration) * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            _isDodging = false;
        }

        public void SetExternalMoveInput(Vector2 input)
        {
            if (_controlsLocked)
            {
                _externalMoveInput = Vector2.zero;
                return;
            }

            _externalMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void RequestBasicAttack()
        {
            if (!_controlsLocked && !_isAttacking && _realmId != RealmId.None)
            {
                StartCoroutine(PerformAttack());
            }
        }

        public void RequestDodge()
        {
            if (!_controlsLocked && !_isDodging && _realmId != RealmId.None)
            {
                StartCoroutine(Dodge());
            }
        }

        public void RequestSkill(int index)
        {
            if (_controlsLocked || _realmId == RealmId.None)
            {
                return;
            }

            UseSkill(index);
        }

        public void ConfigureRealmContext(RealmId realmId)
        {
            RealmId normalized = ChampionRealmContext.Normalize(realmId);
            if (_realmId != RealmId.None && normalized != _realmId)
            {
                return;
            }

            _realmId = normalized;
            _skillCaster?.ConfigureRealmContext(normalized);
        }

        public void SetBlocking(bool isBlocking)
        {
            if (_controlsLocked || _realmId == RealmId.None)
            {
                _touchBlockHeld = false;
                _isBlocking = false;
                return;
            }

            _touchBlockHeld = isBlocking;
            _isBlocking = _touchBlockHeld || GameInput.BlockHeld();
        }

        public void SetControlLocked(bool isLocked)
        {
            _controlsLocked = isLocked;
            _externalMoveInput = Vector2.zero;
            _touchBlockHeld = false;
            _isBlocking = false;
            _velocity = Vector3.zero;

            if (isLocked)
            {
                StopAllCoroutines();
                _isAttacking = false;
                _isDodging = false;
                _skillCaster?.CancelCurrentSkill();
            }
        }

    }
}
