using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;

namespace AL.ChampionMode.AI
{
    public class BossDummyAI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _maxHealth = 1200f;
        [SerializeField] private float _attackRange = 5f;
        [SerializeField] private float _attackCooldown = 3f;
        [SerializeField] private float _enrageThreshold = 0.3f;
        [SerializeField] private float _timedEnrageSeconds = 90f;
        [SerializeField] private BossDefinition _bossDefinition;
        [SerializeField] private string _bossId = "boss_dummy";
        [SerializeField] private string _bossName = "Boss Dummy";
        [SerializeField] private int _warzoneCreditReward = 500;
        [SerializeField] private List<EquipmentDefinition> _possibleLoot = new List<EquipmentDefinition>();

        [Header("Break Bar")]
        [SerializeField] private float _breakBarMax = 100f;
        [SerializeField] private float _breakRecoverPerSecond = 4f;
        [SerializeField] private float _brokenDuration = 3f;
        [SerializeField] private float _brokenDamageMultiplier = 1.25f;

        private Transform _player;
        private bool _isAttacking;
        private bool _isDead;
        private bool _isBroken;
        private bool _phase70;
        private bool _phase40;
        private bool _phase15;
        private bool _enraged;
        private float _currentHealth;
        private float _currentBreak;
        private float _healthPercent = 1.0f;
        private float _fightStartTime;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float CurrentBreak => _currentBreak;
        public float MaxBreak => _breakBarMax;
        public bool IsBroken => _isBroken;

        private void Start()
        {
            ApplyBossDefinition();
            _currentHealth = _maxHealth;
            _currentBreak = _breakBarMax;
            _fightStartTime = Time.time;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;

            StartCoroutine(BehaviorLoop());
        }

        private void ApplyBossDefinition()
        {
            if (_bossDefinition == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_bossDefinition.Id))
            {
                _bossId = _bossDefinition.Id;
            }

            if (!string.IsNullOrWhiteSpace(_bossDefinition.BossName))
            {
                _bossName = _bossDefinition.BossName;
            }

            if (_bossDefinition.Health > 0)
            {
                _maxHealth = _bossDefinition.Health;
            }

            if (_bossDefinition.PossibleLoot != null && _bossDefinition.PossibleLoot.Count > 0)
            {
                _possibleLoot = _bossDefinition.PossibleLoot;
            }
        }

        private IEnumerator BehaviorLoop()
        {
            while (true)
            {
                if (_isDead)
                {
                    yield break;
                }

                TickTimedEnrage();
                TickBreakRecovery();

                if (_isBroken)
                {
                    yield return null;
                    continue;
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

            if (_isDead || _isBroken)
            {
                _isAttacking = false;
                yield break;
            }

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
                TriggerEnrage("low health");
            }
        }

        public void TakeDamage(float amount)
        {
            if (_isDead)
            {
                return;
            }

            float finalAmount = _isBroken ? amount * _brokenDamageMultiplier : amount;
            _currentHealth = Mathf.Max(0f, _currentHealth - finalAmount);
            ApplyBreakDamage(amount);
            UpdateHealth(_currentHealth, _maxHealth);
            SkillEffectFactory.SpawnForgeBurst(transform.position + Vector3.up);
            Debug.Log($"BOSS: Took {finalAmount} damage. HP {_currentHealth}/{_maxHealth}. Break {_currentBreak}/{_breakBarMax}");

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        private void ApplyBreakDamage(float sourceDamage)
        {
            if (_isBroken || sourceDamage <= 0f)
            {
                return;
            }

            float breakDamage = Mathf.Clamp(sourceDamage * 0.18f, 8f, 30f);
            _currentBreak = Mathf.Max(0f, _currentBreak - breakDamage);
            if (_currentBreak <= 0f)
            {
                StartCoroutine(BreakRoutine());
            }
        }

        private void TickBreakRecovery()
        {
            if (_isBroken || _currentBreak >= _breakBarMax)
            {
                return;
            }

            _currentBreak = Mathf.Min(_breakBarMax, _currentBreak + _breakRecoverPerSecond * Time.deltaTime);
        }

        private IEnumerator BreakRoutine()
        {
            _isBroken = true;
            _isAttacking = false;
            Debug.Log("BOSS: BREAK! Damage window opened.");
            SkillEffectFactory.SpawnBossTelegraph(transform.position, 2.25f, _brokenDuration);

            yield return new WaitForSeconds(_brokenDuration);

            if (_isDead)
            {
                yield break;
            }

            _currentBreak = _breakBarMax;
            _isBroken = false;
            Debug.Log("BOSS: Break recovered.");
        }

        private void TickTimedEnrage()
        {
            if (_enraged || Time.time - _fightStartTime < _timedEnrageSeconds)
            {
                return;
            }

            TriggerEnrage("timer");
        }

        private void TriggerEnrage(string reason)
        {
            if (_enraged)
            {
                return;
            }

            _enraged = true;
            Debug.Log($"BOSS: ENRAGED by {reason}!");
            _attackCooldown *= 0.5f;
            _attackRange += 0.5f;
            SkillEffectFactory.SpawnCurseMark(transform.position + Vector3.up);
        }

        private void Die()
        {
            _isDead = true;
            Debug.Log("BOSS: Defeated.");

            try
            {
                ServiceLocator.Get<IBossLootService>().RollLoot(CreateLootRequest());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Boss loot service unavailable. Falling back to simple reward. {ex.Message}");
                GrantFallbackLoot();
            }

            Destroy(gameObject);
        }

        private BossLootRequest CreateLootRequest()
        {
            return new BossLootRequest
            {
                BossId = _bossId,
                BossName = _bossName,
                WarzoneCreditReward = _warzoneCreditReward,
                RandomSeed = unchecked(_bossId.GetHashCode() ^ Mathf.RoundToInt(Time.time * 1000f)),
                LootTable = _possibleLoot ?? new List<EquipmentDefinition>()
            };
        }

        private void GrantFallbackLoot()
        {
            try
            {
                ServiceLocator.Get<IWarzoneCreditService>().AddCredits(_warzoneCreditReward);
                ServiceLocator.Get<INotificationService>().ShowMessage($"Anonymous player has acquired Ember Crown Shard from {_bossName}.");
            }
            catch (Exception)
            {
                // Services are optional in isolated tests.
            }
        }
    }
}
