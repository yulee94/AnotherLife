using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using AL.Core;
using AL.ChampionMode.Skills;
using AL.ChampionMode.UI;
using AL.Input;
using AL.Services.Local;
using System;
using System.Collections;
using System.Linq;

namespace AL.ChampionMode.Control
{
    public readonly struct ChampionMovementReceipt
    {
        public ChampionMovementReceipt(
            uint sequence,
            Vector2 requestedInput,
            Vector3 displacement,
            bool wasGrounded,
            bool isGrounded,
            CollisionFlags collisionFlags)
        {
            Sequence = sequence;
            RequestedInput = requestedInput;
            Displacement = displacement;
            WasGrounded = wasGrounded;
            IsGrounded = isGrounded;
            CollisionFlags = collisionFlags;
        }

        public uint Sequence { get; }
        public Vector2 RequestedInput { get; }
        public Vector3 Displacement { get; }
        public bool WasGrounded { get; }
        public bool IsGrounded { get; }
        public CollisionFlags CollisionFlags { get; }
        public float HorizontalDisplacement =>
            new Vector2(Displacement.x, Displacement.z).magnitude;
    }

    /// <summary>
    /// Monotonic evidence that the authoritative local champion motor accepted
    /// a basic-attack command. This is intentionally not a hit/damage receipt.
    /// </summary>
    public readonly struct ChampionBasicAttackReceipt
    {
        public ChampionBasicAttackReceipt(uint sequence)
        {
            Sequence = sequence;
        }

        public uint Sequence { get; }
    }

#if UNITY_EDITOR
    public enum ChampionBasicAttackResolutionKind
    {
        Invalid = 0,
        Miss = 1,
        Hit = 2,
        Defeated = 3
    }

    public readonly struct ChampionBasicAttackContext
    {
        public ChampionBasicAttackContext(
            ChampionController attacker,
            int attackSequence,
            Vector3 hitCenter,
            float hitRadius,
            Collider[] hitColliders,
            RealmId realmId)
        {
            Attacker = attacker;
            AttackSequence = attackSequence;
            HitCenter = hitCenter;
            HitRadius = hitRadius;
            HitColliders = hitColliders ?? System.Array.Empty<Collider>();
            RealmId = realmId;
        }

        public ChampionController Attacker { get; }
        public int AttackSequence { get; }
        public Vector3 HitCenter { get; }
        public float HitRadius { get; }
        public Collider[] HitColliders { get; }
        public RealmId RealmId { get; }
    }

    public readonly struct ChampionBasicAttackResolution
    {
        public ChampionBasicAttackResolution(
            ChampionBasicAttackResolutionKind kind,
            Vector3 impactPosition,
            string combatText)
        {
            Kind = kind;
            ImpactPosition = impactPosition;
            CombatText = combatText ?? string.Empty;
        }

        public ChampionBasicAttackResolutionKind Kind { get; }
        public Vector3 ImpactPosition { get; }
        public string CombatText { get; }
    }

    public interface IChampionBasicAttackResolver
    {
        bool TryResolve(
            ChampionBasicAttackContext context,
            out ChampionBasicAttackResolution resolution);
    }
#endif

    public static class ChampionCombatInputPolicy
    {
        public static bool ShouldSuppressBasicAttack(
            bool pointerOverUi,
            bool attackOriginatesFromMouse)
        {
            return pointerOverUi && attackOriginatesFromMouse;
        }
    }

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
        private ChampionCombat _combat;
        private RealmId _realmId = RealmId.None;
        private float _rotationVelocity;
        private uint _movementSequence;
        private uint _basicAttackRequestSequence;
        private TerrainCollider _requiredTerrainSupport;
        private Vector3 _terrainSafetySpawn;
        private float _terrainSafetyRecoveryY;
        private bool _terrainSafetyConfigured;
        private bool _terrainSafetySupportBlocked;
        private int _terrainSafetyRecoveryCount;

        public ChampionMovementReceipt LastMovementReceipt { get; private set; }
        public ChampionBasicAttackReceipt LastBasicAttackReceipt { get; private set; }
        public bool IsBlocking => _isBlocking;
        public bool TerrainSafetyConfigured => _terrainSafetyConfigured;
        public bool TerrainSafetySupportReady =>
            _terrainSafetyConfigured && HasRequiredTerrainSupport();
        public TerrainCollider TerrainSafetySupport => _requiredTerrainSupport;
        public Vector3 TerrainSafetySpawn => _terrainSafetySpawn;
        public float TerrainSafetyRecoveryY => _terrainSafetyRecoveryY;
        public int TerrainSafetyRecoveryCount => _terrainSafetyRecoveryCount;
        public event Action<ChampionMovementReceipt> MovementApplied;
        public event Action<ChampionBasicAttackReceipt> BasicAttackAccepted;
#if UNITY_EDITOR
        private IChampionBasicAttackResolver _editorBasicAttackResolver;
        private int _editorBasicAttackSequence;

        public int EditorBasicAttackSequence => _editorBasicAttackSequence;

        public bool TryBindEditorBasicAttackResolver(
            IChampionBasicAttackResolver resolver)
        {
            if (resolver == null || _editorBasicAttackResolver != null)
            {
                return false;
            }

            _editorBasicAttackResolver = resolver;
            return true;
        }

        public bool TryUnbindEditorBasicAttackResolver(
            IChampionBasicAttackResolver resolver)
        {
            if (resolver == null ||
                !ReferenceEquals(_editorBasicAttackResolver, resolver))
            {
                return false;
            }

            _editorBasicAttackResolver = null;
            return true;
        }
#endif

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<CharacterController>();
            }

            // Presentation/spawn authority owns the capsule pivot and dimensions.
            // Production uses a body-center root while the isolated onboarding
            // harness uses a foot root; the motor must preserve either explicit
            // contract instead of silently rewriting serialized collision geometry.
            _controller.minMoveDistance = 0f;

            if (_cameraTransform == null && UnityEngine.Camera.main != null)
                _cameraTransform = UnityEngine.Camera.main.transform;

            _combat = GetComponent<ChampionCombat>() ?? gameObject.AddComponent<ChampionCombat>();
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

            Vector3 frameStart = transform.position;
            bool wasGrounded = _controller.isGrounded;
            Vector2 requestedInput = Vector2.zero;
            CollisionFlags collisionFlags = CollisionFlags.None;

            if (_terrainSafetyConfigured && !HasRequiredTerrainSupport())
            {
                if (!_terrainSafetySupportBlocked)
                {
                    RecoverToTerrainSafetySpawn();
                }
                _terrainSafetySupportBlocked = true;
                _velocity = Vector3.zero;
                PublishMovementReceipt(
                    requestedInput,
                    frameStart,
                    wasGrounded,
                    collisionFlags);
                return;
            }

            _terrainSafetySupportBlocked = false;
            if (TryRecoverTerrainSafety())
            {
                PublishMovementReceipt(
                    requestedInput,
                    frameStart,
                    wasGrounded,
                    CollisionFlags.Below);
                return;
            }

            if (_controlsLocked)
            {
                collisionFlags = ApplyMovement(Vector2.zero);
                PublishMovementReceipt(
                    requestedInput,
                    frameStart,
                    wasGrounded,
                    collisionFlags);
                return;
            }

            requestedInput = ReadMoveInput();
            collisionFlags = ApplyMovement(requestedInput);
            if (TryRecoverTerrainSafety())
            {
                collisionFlags |= CollisionFlags.Below;
            }
            PublishMovementReceipt(
                requestedInput,
                frameStart,
                wasGrounded,
                collisionFlags);
            HandleActions();
        }

        private Vector2 ReadMoveInput()
        {
            Vector2 move = GameInput.ReadMove();
            float horizontal = Mathf.Abs(_externalMoveInput.x) > 0.01f
                ? _externalMoveInput.x
                : move.x;
            float vertical = Mathf.Abs(_externalMoveInput.y) > 0.01f
                ? _externalMoveInput.y
                : move.y;
            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private CollisionFlags ApplyMovement(Vector2 requestedInput)
        {
            Vector3 planarVelocity = Vector3.zero;
            if (!_isAttacking && requestedInput.magnitude >= 0.1f)
            {
                RefreshCameraTransform();
                Vector3 direction = new Vector3(
                    requestedInput.x,
                    0f,
                    requestedInput.y);
                float inputMagnitude = Mathf.Clamp01(direction.magnitude);
                direction.Normalize();

                float targetAngle = 0;
                if (_cameraTransform != null)
                {
                    targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + _cameraTransform.eulerAngles.y;
                }
                else
                {
                    targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                }

                float angle = Mathf.SmoothDampAngle(
                    transform.eulerAngles.y,
                    targetAngle,
                    ref _rotationVelocity,
                    1f / Mathf.Max(0.01f, _rotationSpeed));
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                planarVelocity = moveDir.normalized * (_moveSpeed * inputMagnitude);
            }

            if (_controller.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f;
            }

            _velocity.y += _gravity * Time.deltaTime;
            Vector3 frameVelocity = planarVelocity + Vector3.up * _velocity.y;
            return MoveWithinTerrainSupport(frameVelocity * Time.deltaTime);
        }

        private CollisionFlags MoveWithinTerrainSupport(Vector3 displacement)
        {
            if (_controller == null || !_controller.enabled)
            {
                return CollisionFlags.None;
            }

            if (!_terrainSafetyConfigured)
            {
                return _controller.Move(displacement);
            }

            // Action coroutines run outside Update, so they must share the same
            // TerrainCollider authority as ordinary movement. Failing closed here
            // prevents a lunge or dodge from moving while support is unavailable.
            if (!HasRequiredTerrainSupport())
            {
                return CollisionFlags.None;
            }

            Vector3 center = transform.TransformPoint(_controller.center);
            Bounds supportBounds = _requiredTerrainSupport.bounds;
            float horizontalScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.z));
            float supportInset =
                (_controller.radius + _controller.skinWidth) * horizontalScale;
            float minimumX = supportBounds.min.x + supportInset;
            float maximumX = supportBounds.max.x - supportInset;
            float minimumZ = supportBounds.min.z + supportInset;
            float maximumZ = supportBounds.max.z - supportInset;

            Vector3 candidateCenter = center + displacement;
            if (minimumX <= maximumX)
            {
                displacement.x +=
                    Mathf.Clamp(candidateCenter.x, minimumX, maximumX) -
                    candidateCenter.x;
            }
            else
            {
                displacement.x = 0f;
            }

            if (minimumZ <= maximumZ)
            {
                displacement.z +=
                    Mathf.Clamp(candidateCenter.z, minimumZ, maximumZ) -
                    candidateCenter.z;
            }
            else
            {
                displacement.z = 0f;
            }

            return _controller.Move(displacement);
        }

        private void PublishMovementReceipt(
            Vector2 requestedInput,
            Vector3 frameStart,
            bool wasGrounded,
            CollisionFlags collisionFlags)
        {
            _movementSequence++;
            bool isGrounded = _controller.isGrounded ||
                              (collisionFlags & CollisionFlags.Below) != 0;
            LastMovementReceipt = new ChampionMovementReceipt(
                _movementSequence,
                requestedInput,
                transform.position - frameStart,
                wasGrounded,
                isGrounded,
                collisionFlags);
            MovementApplied?.Invoke(LastMovementReceipt);
        }

        private void HandleActions()
        {
            if (_controlsLocked || _realmId == RealmId.None)
            {
                return;
            }

            if (GameInput.DodgePressed()) StartCoroutine(Dodge());
            _isBlocking = _touchBlockHeld || GameInput.BlockHeld();

            if (GameInput.AttackPressed() &&
                !ChampionCombatInputPolicy.ShouldSuppressBasicAttack(
                    ChampionHudCameraGate.IsPointerOverUi(),
                    GameInput.Attack.activeControl?.device is Mouse))
            {
                RequestBasicAttack();
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
#if UNITY_EDITOR
            _editorBasicAttackSequence++;
            int editorAttackSequence = _editorBasicAttackSequence;
#endif
            GameDebug.Log("<color=orange>[Combat] Attacking!</color>");
            RuntimeCombatAudio.PlayBasicAttack();

            // 1. Lunge Forward
            Vector3 lungeDir = transform.forward;
            float lungeTimer = 0f;
            while (lungeTimer < 0.1f && !_controlsLocked)
            {
                MoveWithinTerrainSupport(
                    lungeDir * _attackLungeForce * Time.deltaTime * 10f);
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
#if UNITY_EDITOR
            bool editorResolverBound = _editorBasicAttackResolver != null;
            bool editorResolverAllowsMissFeedback = false;
            if (editorResolverBound)
            {
                bool resolved = false;
                ChampionBasicAttackResolution resolution = default;
                try
                {
                    resolved = _editorBasicAttackResolver.TryResolve(
                        new ChampionBasicAttackContext(
                            this,
                            editorAttackSequence,
                            hitCenter,
                            _attackRange,
                            hitColliders,
                            realmId),
                        out resolution);
                }
                catch (System.Exception exception)
                {
                    Debug.LogError(
                        "[AL-FIRST-USER-ATTACK-RESOLVER-FAILED] " +
                        exception.GetType().Name);
                }

                if (!resolved || resolution.Kind == ChampionBasicAttackResolutionKind.Invalid)
                {
                    Debug.LogError(
                        "[AL-FIRST-USER-ATTACK-RESOLVER-FAILED] " +
                        "The isolated resolver did not return an exact result.");
                }
                else if (resolution.Kind == ChampionBasicAttackResolutionKind.Miss)
                {
                    editorResolverAllowsMissFeedback = true;
                }
                else
                {
                    hitAnything = true;
                    bool defeated =
                        resolution.Kind == ChampionBasicAttackResolutionKind.Defeated;
                    GameDebug.Log(defeated
                        ? "<color=red>[Combat] Enemy Defeated!</color>"
                        : "<color=red>[Combat] Enemy Hit!</color>");
                    CreateHitVFX(resolution.ImpactPosition);
                    SkillEffectFactory.SpawnFloatingCombatText(
                        resolution.ImpactPosition + Vector3.up * 1.45f,
                        string.IsNullOrEmpty(resolution.CombatText)
                            ? defeated ? "KO" : "HIT"
                            : resolution.CombatText,
                        new Color(1f, 0.78f, 0.22f),
                        0.26f,
                        0.8f);
                    SkillEffectFactory.ShakeCamera(0.10f, 0.10f);
                    SkillEffectFactory.RequestHitPause(0.035f, 0.14f);
                    RuntimeCombatAudio.PlayImpact();
                }
            }

            if (!editorResolverBound)
            {
#endif
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
                        // hotspot: ChampionController.cs — basic-attack damage must stay catalog-backed.
                        float catalogDamage = ResolveCatalogAttackDamage();
                        if (catalogDamage > 0f)
                        {
                            boss.TakeDamage(catalogDamage);
                        }

                        RuntimeCombatAudio.PlayImpact();
                    }
                }
            }
#if UNITY_EDITOR
            }
#endif

            if (!hitAnything
#if UNITY_EDITOR
                && (!editorResolverBound || editorResolverAllowsMissFeedback)
#endif
                )
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
                MoveWithinTerrainSupport(
                    dodgeDir * (_dodgeDistance / duration) * Time.deltaTime);
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

        public bool RequestBasicAttack()
        {
            if (_controlsLocked ||
                _isAttacking ||
                _realmId == RealmId.None ||
                !isActiveAndEnabled)
            {
                return false;
            }

            StartCoroutine(PerformAttack());
            _basicAttackRequestSequence++;
            if (_basicAttackRequestSequence == 0)
            {
                _basicAttackRequestSequence = 1;
            }

            LastBasicAttackReceipt =
                new ChampionBasicAttackReceipt(_basicAttackRequestSequence);
            BasicAttackAccepted?.Invoke(LastBasicAttackReceipt);
            return true;
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

            if (normalized == RealmId.None)
            {
                _realmId = RealmId.None;
                _skillCaster?.ConfigureRealmContext(RealmId.None);
                return;
            }

            float defendMitigation;
            string diagnosticCode;
            if (!SixFamilyRuntimeCatalog.TryResolveDefendMitigation(
                    normalized,
                    out defendMitigation,
                    out diagnosticCode))
            {
                Debug.LogError(
                    diagnosticCode +
                    ": champion defend authority could not resolve realm " +
                    normalized + ".");
                _realmId = RealmId.None;
                _skillCaster?.ConfigureRealmContext(RealmId.None);
                return;
            }

            _combat ??= GetComponent<ChampionCombat>();
            if (_combat == null ||
                !_combat.TryConfigureDefendMitigation(defendMitigation))
            {
                Debug.LogError(
                    SixFamilyRuntimeCatalog.DefendMitigationInvalidCode +
                    ": champion combat rejected realm " +
                    normalized +
                    " defend authority.");
                _realmId = RealmId.None;
                _skillCaster?.ConfigureRealmContext(RealmId.None);
                return;
            }

            _realmId = normalized;
            _skillCaster?.ConfigureRealmContext(normalized);
        }

        /// <summary>
        /// Binds the first-session motor to an actual Unity TerrainCollider. The
        /// renderer is never accepted as physical authority: if the collider is
        /// unloaded or disabled, movement fails closed; if the champion escapes
        /// its horizontal bounds or drops below it, the motor restores a verified
        /// grounded spawn on the next update.
        /// </summary>
        public bool TryConfigureTerrainSafety(
            TerrainCollider terrainSupport,
            Vector3 requestedSpawn)
        {
            if (_controller == null ||
                terrainSupport == null ||
                !terrainSupport.enabled ||
                !terrainSupport.gameObject.activeInHierarchy ||
                terrainSupport.isTrigger ||
                terrainSupport.terrainData == null ||
                !IsFinite(requestedSpawn))
            {
                return false;
            }

            // The runtime terrain and champion are created in the same startup
            // frame. Synchronize once before proving the spawn against physics;
            // this is not a per-frame movement cost.
            Physics.SyncTransforms();
            Bounds supportBounds = terrainSupport.bounds;
            if (requestedSpawn.x < supportBounds.min.x ||
                requestedSpawn.x > supportBounds.max.x ||
                requestedSpawn.z < supportBounds.min.z ||
                requestedSpawn.z > supportBounds.max.z)
            {
                return false;
            }

            float verticalScale = Mathf.Abs(transform.lossyScale.y);
            float horizontalScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.z));
            if (verticalScale <= Mathf.Epsilon || horizontalScale <= Mathf.Epsilon)
            {
                return false;
            }

            float worldHeight = _controller.height * verticalScale;
            float rayOriginY = supportBounds.max.y + worldHeight + 1f;
            float rayDistance = supportBounds.size.y + worldHeight * 4f + 2f;
            var ray = new Ray(
                new Vector3(requestedSpawn.x, rayOriginY, requestedSpawn.z),
                Vector3.down);
            if (!terrainSupport.Raycast(ray, out RaycastHit hit, rayDistance))
            {
                return false;
            }

            float centerOffsetY = _controller.center.y *
                                  transform.lossyScale.y;
            float footClearance = Mathf.Max(_controller.skinWidth, 0.05f);
            _requiredTerrainSupport = terrainSupport;
            _terrainSafetySpawn = new Vector3(
                requestedSpawn.x,
                hit.point.y - centerOffsetY + worldHeight * 0.5f + footClearance,
                requestedSpawn.z);
            _terrainSafetyRecoveryY = supportBounds.min.y -
                                      Mathf.Max(worldHeight * 2f, 1f);
            _terrainSafetyConfigured = true;
            _terrainSafetySupportBlocked = false;
            return true;
        }

        private bool HasRequiredTerrainSupport()
        {
            return _requiredTerrainSupport != null &&
                   _requiredTerrainSupport.enabled &&
                   _requiredTerrainSupport.gameObject.activeInHierarchy &&
                   !_requiredTerrainSupport.isTrigger &&
                   _requiredTerrainSupport.terrainData != null;
        }

        private bool TryRecoverTerrainSafety()
        {
            if (!_terrainSafetyConfigured || _terrainSafetySupportBlocked)
            {
                return false;
            }

            Vector3 position = transform.position;
            Bounds bounds = _requiredTerrainSupport.bounds;
            float radius = _controller.radius *
                           Mathf.Max(
                               Mathf.Abs(transform.lossyScale.x),
                               Mathf.Abs(transform.lossyScale.z));
            bool outsideHorizontalSupport =
                position.x + radius < bounds.min.x ||
                position.x - radius > bounds.max.x ||
                position.z + radius < bounds.min.z ||
                position.z - radius > bounds.max.z;
            if (IsFinite(position) &&
                position.y >= _terrainSafetyRecoveryY &&
                !outsideHorizontalSupport)
            {
                return false;
            }

            RecoverToTerrainSafetySpawn();
            return true;
        }

        private void RecoverToTerrainSafetySpawn()
        {
            TeleportTo(_terrainSafetySpawn);
            _terrainSafetyRecoveryCount++;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
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
            _rotationVelocity = 0f;

            if (isLocked)
            {
                StopAllCoroutines();
                _isAttacking = false;
                _isDodging = false;
                _skillCaster?.CancelCurrentSkill();
            }
        }

        private float ResolveCatalogAttackDamage()
        {
            _combat ??= GetComponent<ChampionCombat>();
            return _combat != null ? _combat.GetAttackDamage() : 0f;
        }

        public void TeleportTo(Vector3 position)
        {
            bool wasEnabled = _controller != null && _controller.enabled;
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            transform.position = position;
            _velocity = Vector3.zero;
            _rotationVelocity = 0f;

            if (_controller != null)
            {
                _controller.enabled = wasEnabled;
            }
        }

    }
}
