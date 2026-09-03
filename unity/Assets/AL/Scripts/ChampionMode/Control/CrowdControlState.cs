using System;
using System.Collections.Generic;
using UnityEngine;

namespace AL.ChampionMode.Control
{
    public enum CrowdControlKind
    {
        None = 0,
        Root = 1,
        Silence = 2,
        Stun = 3,
        Knockdown = 4
    }

    public enum CrowdControlApplicationStatus
    {
        Invalid = 0,
        Applied = 1,
        Resisted = 2,
        Immune = 3
    }

    public readonly struct CrowdControlApplication
    {
        public CrowdControlApplication(
            CrowdControlApplicationStatus status,
            CrowdControlKind kind,
            float appliedDurationSeconds)
        {
            Status = status;
            Kind = kind;
            AppliedDurationSeconds = appliedDurationSeconds;
        }

        public CrowdControlApplicationStatus Status { get; }
        public CrowdControlKind Kind { get; }
        public float AppliedDurationSeconds { get; }
    }

    public sealed class CrowdControlState
    {
        private readonly CombatControlProfile _profile;
        private readonly Dictionary<CrowdControlKind, float> _remainingByKind =
            new Dictionary<CrowdControlKind, float>();
        private readonly float _resistance;
        private float _resolve;
        private float _hardControlImmunityRemaining;
        private float _softControlWardRemaining;
        private float _secondsSinceLastApplication;

        public CrowdControlState(
            CombatControlProfile profile,
            float controlResistance)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _resistance = Mathf.Clamp01(controlResistance);
        }

        public float Resolve => _resolve;
        public bool IsHardControlImmune => _hardControlImmunityRemaining > 0f;
        public bool IsSoftControlWardActive => _softControlWardRemaining > 0f;
        public bool BlocksMovement =>
            IsActive(CrowdControlKind.Root) ||
            IsActive(CrowdControlKind.Stun) ||
            IsActive(CrowdControlKind.Knockdown);
        public bool BlocksJump => BlocksMovement;
        public bool BlocksSkillCasting =>
            IsActive(CrowdControlKind.Silence) ||
            IsActive(CrowdControlKind.Stun) ||
            IsActive(CrowdControlKind.Knockdown);
        public bool BlocksBasicAttack =>
            IsActive(CrowdControlKind.Stun) ||
            IsActive(CrowdControlKind.Knockdown);
        public bool InterruptsAllActions => BlocksBasicAttack;

        public CrowdControlApplication Apply(
            CrowdControlKind kind,
            float baseDurationSeconds,
            float severity)
        {
            if (kind == CrowdControlKind.None ||
                baseDurationSeconds <= 0f ||
                severity <= 0f ||
                float.IsNaN(baseDurationSeconds) ||
                float.IsInfinity(baseDurationSeconds) ||
                float.IsNaN(severity) ||
                float.IsInfinity(severity))
            {
                return new CrowdControlApplication(
                    CrowdControlApplicationStatus.Invalid,
                    kind,
                    0f);
            }

            if ((IsHardControl(kind) && IsHardControlImmune) ||
                (IsSoftControl(kind) && IsSoftControlWardActive))
            {
                return new CrowdControlApplication(
                    CrowdControlApplicationStatus.Immune,
                    kind,
                    0f);
            }

            float resolveMultiplier = Mathf.Max(
                _profile.ResolveMinimumDurationMultiplier,
                1f - _resolve / 100f);
            float duration = baseDurationSeconds *
                             (1f - _resistance) *
                             resolveMultiplier;
            if (IsHardControl(kind))
            {
                duration = Mathf.Min(duration, _profile.HardControlMaximumSeconds);
            }

            if (duration <= 0f)
            {
                return new CrowdControlApplication(
                    CrowdControlApplicationStatus.Resisted,
                    kind,
                    0f);
            }

            if (_remainingByKind.TryGetValue(kind, out float remaining))
            {
                _remainingByKind[kind] = Mathf.Max(remaining, duration);
            }
            else
            {
                _remainingByKind.Add(kind, duration);
            }

            float priorResolve = _resolve;
            _resolve = Mathf.Clamp(
                _resolve +
                _profile.ResolveGainPerSecond * Mathf.Max(0f, severity) * duration,
                0f,
                100f);
            _secondsSinceLastApplication = 0f;
            if (priorResolve < 100f && _resolve >= 100f)
            {
                _hardControlImmunityRemaining =
                    _profile.HardControlImmunitySeconds;
            }

            return new CrowdControlApplication(
                CrowdControlApplicationStatus.Applied,
                kind,
                duration);
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f ||
                float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                return;
            }

            var activeKinds = new List<CrowdControlKind>(_remainingByKind.Keys);
            for (int index = 0; index < activeKinds.Count; index++)
            {
                CrowdControlKind kind = activeKinds[index];
                float remaining = _remainingByKind[kind] - deltaSeconds;
                if (remaining > 0f)
                {
                    _remainingByKind[kind] = remaining;
                }
                else
                {
                    _remainingByKind.Remove(kind);
                }
            }

            _hardControlImmunityRemaining = Mathf.Max(
                0f,
                _hardControlImmunityRemaining - deltaSeconds);
            _softControlWardRemaining = Mathf.Max(
                0f,
                _softControlWardRemaining - deltaSeconds);

            float previousSinceApplication = _secondsSinceLastApplication;
            _secondsSinceLastApplication += deltaSeconds;
            float previousDecayTime = Mathf.Max(
                0f,
                previousSinceApplication - _profile.ResolveDecayDelaySeconds);
            float currentDecayTime = Mathf.Max(
                0f,
                _secondsSinceLastApplication - _profile.ResolveDecayDelaySeconds);
            float decayDuration = currentDecayTime - previousDecayTime;
            if (decayDuration > 0f)
            {
                _resolve = Mathf.Max(
                    0f,
                    _resolve - _profile.ResolveDecayPerSecond * decayDuration);
            }
        }

        public void CleanseSoftControl(float wardSeconds)
        {
            _remainingByKind.Remove(CrowdControlKind.Root);
            _remainingByKind.Remove(CrowdControlKind.Silence);
            if (wardSeconds > 0f &&
                !float.IsNaN(wardSeconds) &&
                !float.IsInfinity(wardSeconds))
            {
                _softControlWardRemaining = Mathf.Max(
                    _softControlWardRemaining,
                    wardSeconds);
            }
        }

        public bool IsActive(CrowdControlKind kind)
        {
            return _remainingByKind.TryGetValue(kind, out float remaining) &&
                   remaining > 0f;
        }

        public float GetRemainingSeconds(CrowdControlKind kind)
        {
            return _remainingByKind.TryGetValue(kind, out float remaining)
                ? Mathf.Max(0f, remaining)
                : 0f;
        }

        public static bool IsHardControl(CrowdControlKind kind)
        {
            return kind == CrowdControlKind.Stun ||
                   kind == CrowdControlKind.Knockdown;
        }

        public static bool IsSoftControl(CrowdControlKind kind)
        {
            return kind == CrowdControlKind.Root ||
                   kind == CrowdControlKind.Silence;
        }
    }
}
