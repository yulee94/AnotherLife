using UnityEngine;
using System.Collections;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.ChampionMode.AI
{
    public class BossDummyAI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _maxHealth = 1200f;
        [SerializeField] private float _attackRange = 5f;
        [SerializeField] private float _attackCooldown = 3f;
        [SerializeField] private float _enrageThreshold = 0.3f;

        private Transform _player;
        private bool _isAttacking;
        private bool _isDead;
        private bool _phase70;
        private bool _phase40;
        private bool _phase15;
        private bool _enraged;
        private float _currentHealth;
        private float _healthPercent = 1.0f;

        private void Start()
        {
            _currentHealth = _maxHealth;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;

            StartCoroutine(BehaviorLoop());
        }

        private IEnumerator BehaviorLoop()
        {
            while (true)
            {
                if (_isDead)
                {
                    yield break;
                }

                if (_player != null)
                {
                    float distance = Vector3.Distance(transform.position, _player.position);

                    if (distance <= _attackRange && !_isAttacking)
                    {
                        yield return StartCoroutine(PerformTelegraphedAttack());
                    }
                    else
                    {
                        // Rotate towards player
                        Vector3 dir = (_player.position - transform.position).normalized;
                        dir.y = 0;
                        if (dir != Vector3.zero)
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 2f);
                    }
                }
                yield return null;
            }
        }

        private IEnumerator PerformTelegraphedAttack()
        {
            _isAttacking = true;
            Debug.Log("BOSS: Telegraphing Slam Attack...");
            if (_player != null)
            {
                SkillEffectFactory.SpawnBossTelegraph(_player.position, _attackRange, 1.5f);
            }
            yield return new WaitForSeconds(1.5f);

            Debug.Log("BOSS: SLAM!");
            if (_player != null && Vector3.Distance(transform.position, _player.position) <= _attackRange)
            {
                var combat = _player.GetComponent<AL.ChampionMode.Control.ChampionCombat>();
                combat?.TakeDamage(80f);
            }

            yield return new WaitForSeconds(_attackCooldown);
            _isAttacking = false;
        }

        public void UpdateHealth(float current, float max)
        {
            _healthPercent = current / max;
            if (!_phase70 && _healthPercent <= 0.70f)
            {
                _phase70 = true;
                Debug.Log("BOSS: Phase 2. Wider telegraphs.");
                _attackRange += 1f;
            }

            if (!_phase40 && _healthPercent <= 0.40f)
            {
                _phase40 = true;
                Debug.Log("BOSS: Phase 3. Faster attacks.");
                _attackCooldown *= 0.75f;
            }

            if (!_phase15 && _healthPercent <= 0.15f)
            {
                _phase15 = true;
                Debug.Log("BOSS: Final phase.");
                SkillEffectFactory.SpawnCurseMark(transform.position + Vector3.up);
            }

            if (!_enraged && _healthPercent <= _enrageThreshold)
            {
                _enraged = true;
                Debug.Log("BOSS: ENRAGED!");
                _attackCooldown *= 0.5f;
            }
        }

        public void TakeDamage(float amount)
        {
            if (_isDead)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            UpdateHealth(_currentHealth, _maxHealth);
            SkillEffectFactory.SpawnForgeBurst(transform.position + Vector3.up);
            Debug.Log($"BOSS: Took {amount} damage. HP {_currentHealth}/{_maxHealth}");

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            _isDead = true;
            Debug.Log("BOSS: Defeated.");

            try
            {
                ServiceLocator.Get<IWarzoneCreditService>().AddCredits(500);
                ServiceLocator.Get<INotificationService>().ShowMessage("Anonymous player has acquired Ember Crown Shard from Boss Dummy.");
            }
            catch (System.Exception)
            {
                // Services are optional in isolated tests.
            }

            Destroy(gameObject);
        }
    }
}
