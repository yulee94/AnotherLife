using AL.ChampionMode.Control;
using AL.ChampionMode.Skills;
using AL.Core;
using UnityEngine;

namespace AL.ChampionMode.AI
{
    public class BotChampionAI : MonoBehaviour
    {
        [Header("Realm")]
        [SerializeField] private RealmId _realmId = RealmId.Crownlands;
        [SerializeField] private RealmId _playerRealm = RealmId.Crownlands;

        [Header("Stats")]
        [SerializeField] private float _maxHealth = 260f;
        [SerializeField] private float _attackDamage = 24f;

        [Header("Behavior")]
        [SerializeField] private float _moveSpeed = 2.4f;
        [SerializeField] private float _attackRange = 1.8f;
        [SerializeField] private float _attackCooldown = 1.4f;
        [SerializeField] private float _aggroRange = 24f;
        [SerializeField] private float _targetRefreshSeconds = 1.1f;

        private Transform _target;
        private BotChampionAI _targetBot;
        private ChampionCombat _targetPlayerCombat;
        private Transform _fallbackObjective;
        private Vector3 _arenaCenter;
        private float _arenaRadius = 24f;
        private float _currentHealth;
        private float _nextAttackTime;
        private float _nextTargetRefreshTime;
        private bool _isDead;

        public RealmId RealmId => _realmId;
        public bool IsAlive => !_isDead && gameObject.activeInHierarchy;

        private void Awake()
        {
            _currentHealth = _maxHealth;
            _arenaCenter = Vector3.zero;
        }

        private void Start()
        {
            if (_fallbackObjective == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _fallbackObjective = player.transform;
                }
            }

            ChooseTarget(true);
        }

        public void Configure(RealmId realmId, RealmId playerRealm, Transform fallbackObjective, Vector3 arenaCenter, float arenaRadius, float moveSpeedScale)
        {
            _realmId = realmId;
            _playerRealm = playerRealm == RealmId.None ? RealmId.Crownlands : playerRealm;
            _fallbackObjective = fallbackObjective;
            _arenaCenter = arenaCenter;
            _arenaRadius = Mathf.Max(8f, arenaRadius);
            _moveSpeed *= Mathf.Max(0.5f, moveSpeedScale);
            _attackCooldown += Random.Range(-0.25f, 0.35f);
            _targetRefreshSeconds += Random.Range(-0.2f, 0.35f);
            _currentHealth = _maxHealth;
            _isDead = false;
            ChooseTarget(true);
        }

        public void TakeDamage(float amount, RealmId attackerRealm)
        {
            if (_isDead || amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            if (_currentHealth <= 0f)
            {
                Die(attackerRealm);
            }
            else if (Random.value > 0.65f)
            {
                SkillEffectFactory.SpawnRealmImpact(transform.position + Vector3.up, attackerRealm);
            }
        }

        private void Update()
        {
            if (_isDead)
            {
                return;
            }

            if (Time.time >= _nextTargetRefreshTime)
            {
                ChooseTarget(false);
            }

            if (_target == null)
            {
                PatrolObjective();
                return;
            }

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;

            if (distance > _attackRange || !IsTargetAlive())
            {
                MoveToward(distance > 0.01f ? toTarget.normalized : Vector3.forward);
            }
            else if (Time.time >= _nextAttackTime)
            {
                AttackTarget();
            }
        }

        private void ChooseTarget(bool force)
        {
            if (!force && _target != null && IsTargetAlive())
            {
                _nextTargetRefreshTime = Time.time + _targetRefreshSeconds;
                return;
            }

            _target = null;
            _targetBot = null;
            _targetPlayerCombat = null;
            _nextTargetRefreshTime = Time.time + _targetRefreshSeconds;

            float bestScore = float.MaxValue;
            var bots = FindObjectsOfType<BotChampionAI>();
            foreach (var bot in bots)
            {
                if (bot == null || bot == this || !bot.IsAlive || bot.RealmId == _realmId)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, bot.transform.position);
                if (distance > _aggroRange)
                {
                    continue;
                }

                float score = distance + Random.Range(0f, 4f);
                if (score < bestScore)
                {
                    bestScore = score;
                    _target = bot.transform;
                    _targetBot = bot;
                    _targetPlayerCombat = null;
                }
            }

            if (_realmId == _playerRealm)
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return;
            }

            float playerDistance = Vector3.Distance(transform.position, player.transform.position);
            float playerScore = playerDistance + Random.Range(3f, 9f);
            if (playerDistance <= _aggroRange && playerScore < bestScore)
            {
                _target = player.transform;
                _targetBot = null;
                _targetPlayerCombat = player.GetComponent<ChampionCombat>();
            }
        }

        private bool IsTargetAlive()
        {
            if (_targetBot != null)
            {
                return _targetBot.IsAlive;
            }

            if (_targetPlayerCombat != null)
            {
                return _targetPlayerCombat.CurrentHealth > 0f;
            }

            return _target != null;
        }

        private void AttackTarget()
        {
            _nextAttackTime = Time.time + Mathf.Max(0.25f, _attackCooldown + Random.Range(-0.2f, 0.3f));
            SkillEffectFactory.SpawnRealmImpact(transform.position + Vector3.up, _realmId);

            if (_targetBot != null)
            {
                _targetBot.TakeDamage(_attackDamage, _realmId);
                return;
            }

            _targetPlayerCombat?.TakeDamage(_attackDamage * 0.65f);
        }

        private void PatrolObjective()
        {
            Vector3 objective = _fallbackObjective != null ? _fallbackObjective.position : _arenaCenter;
            float angle = Time.time * 0.35f + GetInstanceID() * 0.01f;
            Vector3 patrolPoint = objective + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (_arenaRadius * 0.35f);
            Vector3 toPatrol = patrolPoint - transform.position;
            toPatrol.y = 0f;
            MoveToward(toPatrol.sqrMagnitude > 0.01f ? toPatrol.normalized : Vector3.forward);
        }

        private void MoveToward(Vector3 direction)
        {
            if (direction == Vector3.zero)
            {
                return;
            }

            transform.position += direction * _moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
        }

        private void Die(RealmId attackerRealm)
        {
            _isDead = true;
            SkillEffectFactory.SpawnRealmImpact(transform.position + Vector3.up, attackerRealm);
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            Destroy(gameObject, 1.2f);
        }
    }
}
