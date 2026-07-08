using AL.ChampionMode.Skills;
using UnityEngine;

namespace AL.ChampionMode.AI
{
    public class BotChampionAI : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 2.4f;
        [SerializeField] private float _attackRange = 1.8f;
        [SerializeField] private float _attackCooldown = 1.4f;

        private Transform _target;
        private float _nextAttackTime;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _target = player.transform;
            }
        }

        private void Update()
        {
            if (_target == null)
            {
                return;
            }

            Vector3 toTarget = _target.position - transform.position;
            toTarget.y = 0;
            float distance = toTarget.magnitude;

            if (distance > _attackRange)
            {
                Vector3 direction = toTarget.normalized;
                transform.position += direction * _moveSpeed * Time.deltaTime;
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
                }
            }
            else if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _attackCooldown;
                SkillEffectFactory.SpawnCurseMark(transform.position + Vector3.up);
                var combat = _target.GetComponent<AL.ChampionMode.Control.ChampionCombat>();
                combat?.TakeDamage(18f);
            }
        }
    }
}

