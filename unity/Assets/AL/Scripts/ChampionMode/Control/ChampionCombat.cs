using UnityEngine;
using System;

namespace AL.ChampionMode.Control
{
    public class ChampionCombat : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float _maxHealth = 1000f;
        [SerializeField] private float _maxMana = 100f;
        [SerializeField] private float _manaRegenPerSecond = 7.5f;
        [SerializeField] private float _currentHealth;
        [SerializeField] private float _currentMana;
        [SerializeField] private float _attackPower = 50f;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float CurrentMana => _currentMana;
        public float MaxMana => _maxMana;

        public event Action<float, float> OnHealthChanged;
        public event Action<float, float> OnManaChanged;
        public event Action OnDeath;

        private void Start()
        {
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

        public void TakeDamage(float amount)
        {
            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0)
            {
                Die();
            }
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
            Debug.Log("Champion has fallen!");
            OnDeath?.Invoke();
        }

        public float GetAttackDamage() => _attackPower;
    }
}
