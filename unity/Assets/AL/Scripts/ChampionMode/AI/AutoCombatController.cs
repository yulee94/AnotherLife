using AL.ChampionMode.Control;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Input;
using AL.UI.QuestHud;
using UnityEngine;

namespace AL.ChampionMode.AI
{
    public class AutoCombatController : MonoBehaviour
    {
        public const float ManualOverrideSeconds = 1.25f;

        [SerializeField] private AutoMode _mode = AutoMode.Manual;
        [SerializeField] private float _targetScanInterval = 0.5f;
        [SerializeField] private float _reactionDelay = 0.35f;
        [SerializeField] private float _desiredRange = 2.0f;

        private ChampionController _controller;
        private Transform _target;
        private Transform _questTarget;
        private bool _questTargetArmed;
        private float _nextScanTime;
        private float _nextDecisionTime;
        private float _manualOverrideUntil;

        public AutoMode Mode => _mode;
        public Transform QuestTarget => _questTarget;

        private void Awake()
        {
            _controller = GetComponent<ChampionController>();
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            if (GameInput.GameplaySuppressed || ChampionHudCameraGate.BlocksGameplay)
            {
                _controller.SetExternalMoveInput(Vector2.zero);
                return;
            }

            if (_questTargetArmed &&
                (!QuestHudAutoQuest.Enabled || _questTarget == null))
            {
                _controller.SetExternalMoveInput(Vector2.zero);
                if (_questTarget == null)
                {
                    _target = null;
                    _questTarget = null;
                    _questTargetArmed = false;
                }
            }

            bool questDriven = QuestHudAutoQuest.Enabled && _questTarget != null;
            if (questDriven)
            {
                if (HasManualOverrideInput())
                {
                    NotifyManualOverrideAt(Time.unscaledTime);
                    return;
                }

                if (!CanDriveQuestTargetAt(Time.unscaledTime))
                {
                    _controller.SetExternalMoveInput(Vector2.zero);
                    return;
                }

                _target = _questTarget;
                if (Time.time >= _nextDecisionTime)
                {
                    _nextDecisionTime = Time.time + _reactionDelay;
                    TickAssistOrAuto(AutoMode.FullAuto);
                }

                return;
            }

            if (_mode == AutoMode.Manual)
            {
                return;
            }

            if (HasManualOverrideInput())
            {
                SetMode(AutoMode.Manual);
                return;
            }

            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + _targetScanInterval;
                _target = FindNearestTarget();
            }

            if (_target == null || Time.time < _nextDecisionTime)
            {
                return;
            }

            _nextDecisionTime = Time.time + _reactionDelay;
            TickAssistOrAuto(_mode);
        }

        private void OnDisable()
        {
            _controller?.SetExternalMoveInput(Vector2.zero);
        }

        private void OnDestroy()
        {
            _controller?.SetExternalMoveInput(Vector2.zero);
        }

        public bool TryAssignQuestTarget(
            ChampionArenaSceneController arena,
            Transform target)
        {
            BossDummyAI boss = target == null ? null : target.GetComponent<BossDummyAI>();
            if (arena == null ||
                target == null ||
                !ReferenceEquals(arena.GuardianTrialTarget, target) ||
                target == transform ||
                !target.gameObject.activeInHierarchy ||
                boss == null ||
                boss.IsDead)
            {
                return false;
            }

            _questTarget = target;
            _questTargetArmed = true;
            return true;
        }

        public void ClearQuestTarget()
        {
            _questTarget = null;
            _target = null;
            _questTargetArmed = false;
            _controller?.SetExternalMoveInput(Vector2.zero);
        }

        public void NotifyManualOverrideAt(float unscaledTime)
        {
            _manualOverrideUntil = unscaledTime + ManualOverrideSeconds;
            _controller?.SetExternalMoveInput(Vector2.zero);
        }

        public bool CanDriveQuestTargetAt(float unscaledTime)
        {
            if (!QuestHudAutoQuest.Enabled ||
                _questTarget == null ||
                !_questTarget.gameObject.activeInHierarchy ||
                unscaledTime < _manualOverrideUntil)
            {
                return false;
            }

            BossDummyAI boss = _questTarget.GetComponent<BossDummyAI>();
            return boss != null && !boss.IsDead;
        }

        public void SetMode(AutoMode mode)
        {
            _mode = mode;
            if (_mode == AutoMode.Manual)
            {
                _controller?.SetExternalMoveInput(Vector2.zero);
            }

            GameDebug.Log($"Auto mode set to {_mode}");
        }

        private void TickAssistOrAuto(AutoMode mode)
        {
            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (mode == AutoMode.FullAuto && distance > _desiredRange)
            {
                Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);
                _controller.SetExternalMoveInput(new Vector2(localDirection.x, localDirection.z));
            }
            else
            {
                _controller.SetExternalMoveInput(Vector2.zero);
            }

            if (distance <= _desiredRange + 0.5f)
            {
                _controller.RequestBasicAttack();
            }

            if (mode != AutoMode.Manual && Random.value > 0.55f)
            {
                _controller.RequestSkill(Random.Range(0, 4));
            }
        }

        private Transform FindNearestTarget()
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            Transform best = null;
            float bestDistance = float.MaxValue;

            foreach (var obj in allObjects)
            {
                if (obj == null || obj == gameObject)
                {
                    continue;
                }

                bool isTarget = obj.name.StartsWith("Dummy_") || obj.name.StartsWith("BossDummy");
                if (!isTarget)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = obj.transform;
                }
            }

            return best;
        }

        private static bool HasManualOverrideInput()
        {
            Vector2 move = GameInput.ReadMove();
            return Mathf.Abs(move.x) > 0.1f ||
                   Mathf.Abs(move.y) > 0.1f ||
                   GameInput.AttackPressed() ||
                   GameInput.DodgePressed() ||
                   GameInput.BlockPressed();
        }
    }
}
