using UnityEngine;
using System.Collections;

namespace AL.ChampionMode.AI
{
    public class BossDummyAI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _attackRange = 5f;
        [SerializeField] private float _attackCooldown = 3f;
        [SerializeField] private float _enrageThreshold = 0.3f;

        private Transform _player;
        private bool _isAttacking;
        private float _healthPercent = 1.0f;

        private void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;

            StartCoroutine(BehaviorLoop());
        }

        private IEnumerator BehaviorLoop()
        {
            while (true)
            {
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
            // Highlight area / VFX
            yield return new WaitForSeconds(1.5f);

            Debug.Log("BOSS: SLAM!");
            // Check for hits in radius

            yield return new WaitForSeconds(_attackCooldown);
            _isAttacking = false;
        }

        public void UpdateHealth(float current, float max)
        {
            _healthPercent = current / max;
            if (_healthPercent <= _enrageThreshold)
            {
                Debug.Log("BOSS: ENRAGED!");
                _attackCooldown *= 0.5f;
            }
        }
    }
}
