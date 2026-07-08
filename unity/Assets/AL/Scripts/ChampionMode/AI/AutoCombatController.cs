using AL.ChampionMode.Control;
using AL.Core;
using UnityEngine;

namespace AL.ChampionMode.AI
{
    public class AutoCombatController : MonoBehaviour
    {
        [SerializeField] private AutoMode _mode = AutoMode.Manual;
        [SerializeField] private float _targetScanInterval = 0.5f;
        [SerializeField] private float _reactionDelay = 0.35f;
        [SerializeField] private float _desiredRange = 2.0f;

        private ChampionController _controller;
        private Transform _target;
        private float _nextScanTime;
        private float _nextDecisionTime;

        public AutoMode Mode => _mode;

        private void Awake()
        {
            _controller = GetComponent<ChampionController>();
        }

        private void Update()
        {
            if (_controller == null || _mode == AutoMode.Manual)
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
            TickAssistOrAuto();
        }

        public void SetMode(AutoMode mode)
        {
            _mode = mode;
            if (_mode == AutoMode.Manual)
            {
                _controller?.SetExternalMoveInput(Vector2.zero);
            }

            Debug.Log($"Auto mode set to {_mode}");
        }

        private void TickAssistOrAuto()
        {
            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (_mode == AutoMode.FullAuto && distance > _desiredRange)
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

            if (_mode != AutoMode.Manual && Random.value > 0.55f)
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
            return Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f ||
                   Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f ||
                   Input.GetMouseButtonDown(0) ||
                   Input.GetKeyDown(KeyCode.Space) ||
                   Input.GetKeyDown(KeyCode.LeftShift);
        }
    }
}
