using UnityEngine;
using System;
using AL.Core;

namespace AL.ChampionMode.Control
{
    /// <summary>
    /// Authoritative result for one incoming champion-damage request. Requested,
    /// mitigated, and actually applied damage stay distinct so combat feedback
    /// never reports pre-mitigation or overkill damage as health loss.
    /// </summary>
    public readonly struct ChampionDamageReceipt
    {
        public ChampionDamageReceipt(
            uint sequence,
            bool accepted,
            float requestedDamage,
            float appliedDamage,
            float mitigatedDamage,
            bool wasDefending,
            float defendMitigation,
            float remainingHealth,
            bool wasFatal,
            string diagnosticCode)
        {
            Sequence = sequence;
            Accepted = accepted;
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            MitigatedDamage = mitigatedDamage;
            WasDefending = wasDefending;
            DefendMitigation = defendMitigation;
            RemainingHealth = remainingHealth;
            WasFatal = wasFatal;
            DiagnosticCode = diagnosticCode ?? string.Empty;
        }

        public uint Sequence { get; }
        public bool Accepted { get; }
        public float RequestedDamage { get; }
        public float AppliedDamage { get; }
        public float MitigatedDamage { get; }
        public bool WasDefending { get; }
        public float DefendMitigation { get; }
        public float RemainingHealth { get; }
        public bool WasFatal { get; }
        public string DiagnosticCode { get; }
        public bool WasMitigated => Accepted && MitigatedDamage > Mathf.Epsilon;
    }

    public class ChampionCombat : MonoBehaviour
    {
        public const string DamageAcceptedCode = "AL-CHAMPION-DAMAGE-ACCEPTED";
        public const string DamageRejectedCode = "AL-CHAMPION-DAMAGE-REJECTED";
        public const string DefendMitigationUnavailableCode =
            "AL-CHAMPION-DEFEND-MITIGATION-UNAVAILABLE";

        [Header("Stats")]
        [SerializeField] private float _maxHealth = 1000f;
        [SerializeField] private float _maxMana = 100f;
        [SerializeField] private float _manaRegenPerSecond = 7.5f;
        [SerializeField] private float _currentHealth;
        [SerializeField] private float _currentMana;
        [SerializeField] private float _attackPower = 50f;
        private bool _isDead;
        private bool _defendMitigationReady;
        private float _defendMitigation;
        private uint _damageSequence;
        private ChampionController _controller;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float CurrentMana => _currentMana;
        public float MaxMana => _maxMana;
        public bool IsDead => _isDead;
        public bool DefendMitigationReady => _defendMitigationReady;
        public float DefendMitigation => _defendMitigation;
        public ChampionDamageReceipt LastDamageReceipt { get; private set; }

        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnManaChanged;
        public event Action OnDeath;
        public event Action<ChampionDamageReceipt> DamageResolved;

        private void Start()
        {
            _isDead = false;
            _currentHealth = _maxHealth;
            _currentMana = _maxMana;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            OnManaChanged?.Invoke(_currentMana, _maxMana);
        }

        private void Update()
        {
            if (_currentHealth <= 0f || _currentMana >= _maxMana)
            {
                return;
            }

            RestoreMana(_manaRegenPerSecond * Time.deltaTime);
        }

        public ChampionDamageReceipt TakeDamage(float amount)
        {
            _controller ??= GetComponent<ChampionController>();
            return ApplyIncomingDamage(
                amount,
                _controller != null && _controller.IsBlocking);
        }

        public ChampionDamageReceipt ApplyIncomingDamage(
            float requestedDamage,
            bool isDefending)
        {
            if (_isDead ||
                requestedDamage <= 0f ||
                float.IsNaN(requestedDamage) ||
                float.IsInfinity(requestedDamage))
            {
                return PublishDamageReceipt(
                    false,
                    requestedDamage,
                    0f,
                    0f,
                    isDefending,
                    0f,
                    false,
                    DamageRejectedCode);
            }

            var defendAuthorityAvailable = !isDefending || _defendMitigationReady;
            var mitigation = isDefending && defendAuthorityAvailable
                ? _defendMitigation
                : 0f;
            var damageAfterMitigation =
                Mathf.Max(0f, requestedDamage * (1f - mitigation));
            var mitigatedDamage =
                Mathf.Max(0f, requestedDamage - damageAfterMitigation);
            var healthBefore = _currentHealth;
            _currentHealth = Mathf.Max(0f, _currentHealth - damageAfterMitigation);
            var appliedDamage = Mathf.Max(0f, healthBefore - _currentHealth);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            var wasFatal = _currentHealth <= 0f;
            ChampionDamageReceipt receipt = PublishDamageReceipt(
                true,
                requestedDamage,
                appliedDamage,
                mitigatedDamage,
                isDefending,
                mitigation,
                wasFatal,
                defendAuthorityAvailable
                    ? DamageAcceptedCode
                    : DefendMitigationUnavailableCode);
            if (wasFatal)
            {
                Die();
            }

            return receipt;
        }

        public bool TryConfigureDefendMitigation(float mitigation)
        {
            if (float.IsNaN(mitigation) ||
                float.IsInfinity(mitigation) ||
                mitigation < 0f ||
                mitigation > 1f)
            {
                return false;
            }

            if (_defendMitigationReady)
            {
                return Mathf.Approximately(_defendMitigation, mitigation);
            }

            _defendMitigation = mitigation;
            _defendMitigationReady = true;
            return true;
        }

        private ChampionDamageReceipt PublishDamageReceipt(
            bool accepted,
            float requestedDamage,
            float appliedDamage,
            float mitigatedDamage,
            bool wasDefending,
            float defendMitigation,
            bool wasFatal,
            string diagnosticCode)
        {
            _damageSequence++;
            if (_damageSequence == 0)
            {
                _damageSequence = 1;
            }

            LastDamageReceipt = new ChampionDamageReceipt(
                _damageSequence,
                accepted,
                requestedDamage,
                appliedDamage,
                mitigatedDamage,
                wasDefending,
                defendMitigation,
                _currentHealth,
                wasFatal,
                diagnosticCode);
            DamageResolved?.Invoke(LastDamageReceipt);
            return LastDamageReceipt;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || _currentHealth <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public bool TrySpendMana(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (_currentMana < amount)
            {
                return false;
            }

            _currentMana -= amount;
            OnManaChanged?.Invoke(_currentMana, _maxMana);
            return true;
        }

        public void RestoreMana(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentMana = Mathf.Min(_maxMana, _currentMana + amount);
            OnManaChanged?.Invoke(_currentMana, _maxMana);
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            GameDebug.Log("Champion has fallen!");
            OnDeath?.Invoke();
        }

        public bool TryRevive(float healthFraction)
        {
            if (!_isDead)
            {
                return false;
            }

            _isDead = false;
            float fraction = Mathf.Clamp01(healthFraction);
            _currentHealth = Mathf.Max(1f, _maxHealth * fraction);
            if (_currentHealth > _maxHealth)
            {
                _currentHealth = _maxHealth;
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            return true;
        }

        public float GetAttackDamage() => _attackPower;

        public bool ApplyCatalogStats(float maxHealth, float maxMana, float attackPower)
        {
            if (!IsFinitePositive(maxHealth) ||
                !IsFiniteNonNegative(maxMana) ||
                !IsFinitePositive(attackPower))
            {
                return false;
            }

            _maxHealth = maxHealth;
            _maxMana = maxMana;
            _attackPower = attackPower;
            _isDead = false;
            _currentHealth = _maxHealth;
            _currentMana = _maxMana;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            OnManaChanged?.Invoke(_currentMana, _maxMana);
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
