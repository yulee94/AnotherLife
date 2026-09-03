using System;
using AL.ChampionMode.Presentation;
using AL.ChampionMode.Skills;
using UnityEngine;

namespace AL.ChampionMode.Control
{
    public sealed class ChampionCrowdControl : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)]
        private float _controlResistance;

        private CrowdControlState _state;
        private SkillCaster _skillCaster;
        private ChampionController _controller;
        private ChampionActionPresentation _actionPresentation;

        public event Action<CrowdControlApplication> ControlApplied;
        public event Action<CrowdControlApplication> ControlRejected;

        public bool IsReady => _state != null;
        public float Resolve => _state != null ? _state.Resolve : 0f;
        public bool IsHardControlImmune =>
            _state != null && _state.IsHardControlImmune;
        public bool IsSoftControlWardActive =>
            _state != null && _state.IsSoftControlWardActive;
        public bool BlocksMovement => _state != null && _state.BlocksMovement;
        public bool BlocksJump => _state != null && _state.BlocksJump;
        public bool BlocksSkillCasting =>
            _state != null && _state.BlocksSkillCasting;
        public bool BlocksBasicAttack =>
            _state != null && _state.BlocksBasicAttack;

        private void Awake()
        {
            _skillCaster = GetComponent<SkillCaster>();
            _controller = GetComponent<ChampionController>();
            _actionPresentation =
                GetComponent<ChampionActionPresentation>() ??
                gameObject.AddComponent<ChampionActionPresentation>();
            if (CombatControlCatalog.TryLoad(out CombatControlProfile profile))
            {
                float resistance = _controlResistance > 0f
                    ? _controlResistance
                    : profile.DefaultControlResistance;
                _state = new CrowdControlState(profile, resistance);
            }
            else
            {
                Debug.LogError(
                    "[ChampionCrowdControl] Missing valid combat-control catalog profile.");
            }
        }

        private void Update()
        {
            if (_state == null)
            {
                return;
            }

            bool wasRooted = _state.IsActive(CrowdControlKind.Root);
            bool wasSilenced = _state.IsActive(CrowdControlKind.Silence);
            bool wasStunned = _state.IsActive(CrowdControlKind.Stun);
            bool wasKnockedDown = _state.IsActive(CrowdControlKind.Knockdown);
            _state.Advance(Time.deltaTime);

            if (wasKnockedDown && !_state.IsActive(CrowdControlKind.Knockdown))
            {
                _actionPresentation?.Emit(
                    ChampionActionKind.Control,
                    ChampionActionPhase.GetUp,
                    actionId: CrowdControlKind.Knockdown.ToString());
            }
            else if ((wasStunned && !_state.IsActive(CrowdControlKind.Stun)) ||
                     (wasSilenced && !_state.IsActive(CrowdControlKind.Silence)) ||
                     (wasRooted && !_state.IsActive(CrowdControlKind.Root)))
            {
                _actionPresentation?.Emit(
                    ChampionActionKind.Control,
                    ChampionActionPhase.Recovery);
            }
        }

        public CrowdControlApplication Apply(
            CrowdControlKind kind,
            float baseDurationSeconds,
            float severity)
        {
            if (_state == null)
            {
                var unavailable = new CrowdControlApplication(
                    CrowdControlApplicationStatus.Invalid,
                    kind,
                    0f);
                ControlRejected?.Invoke(unavailable);
                return unavailable;
            }

            CrowdControlApplication result = _state.Apply(
                kind,
                baseDurationSeconds,
                severity);
            if (result.Status != CrowdControlApplicationStatus.Applied)
            {
                ControlRejected?.Invoke(result);
                return result;
            }

            if (kind == CrowdControlKind.Silence ||
                CrowdControlState.IsHardControl(kind))
            {
                _skillCaster ??= GetComponent<SkillCaster>();
                _skillCaster?.CancelCurrentSkill();
            }

            if (CrowdControlState.IsHardControl(kind))
            {
                _controller ??= GetComponent<ChampionController>();
                _controller?.InterruptCurrentActions();
            }

            _actionPresentation?.Emit(
                ChampionActionKind.Control,
                PresentationPhase(kind),
                actionId: kind.ToString());
            ControlApplied?.Invoke(result);
            return result;
        }

        public void CleanseSoftControl(float wardSeconds)
        {
            _state?.CleanseSoftControl(wardSeconds);
        }

        public bool IsActive(CrowdControlKind kind)
        {
            return _state != null && _state.IsActive(kind);
        }

        public float GetRemainingSeconds(CrowdControlKind kind)
        {
            return _state != null ? _state.GetRemainingSeconds(kind) : 0f;
        }

        private static ChampionActionPhase PresentationPhase(CrowdControlKind kind)
        {
            switch (kind)
            {
                case CrowdControlKind.Root:
                    return ChampionActionPhase.Rooted;
                case CrowdControlKind.Silence:
                    return ChampionActionPhase.Silenced;
                case CrowdControlKind.Stun:
                    return ChampionActionPhase.Stunned;
                case CrowdControlKind.Knockdown:
                    return ChampionActionPhase.Knockdown;
                default:
                    return ChampionActionPhase.Idle;
            }
        }
    }
}
