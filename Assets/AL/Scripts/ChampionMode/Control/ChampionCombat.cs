using UnityEngine;
using System;

namespace AL.ChampionMode.Control
{
    public class ChampionCombat : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float _maxHealth = 1000f;
        [SerializeField] private float _currentHealth;
        [SerializeField] private float _attackPower = 50f;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDeath;

        private void Start()
        {
            _currentHealth = _maxHealth;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
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

        private void Die()
        {
            Debug.Log("Champion has fallen!");
            OnDeath?.Invoke();
        }

        public float GetAttackDamage() => _attackPower;
    }
}
