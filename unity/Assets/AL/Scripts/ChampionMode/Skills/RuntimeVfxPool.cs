using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
    internal static class CombatEffectOwnership
    {
        [ThreadStatic] private static GameObject _currentOwner;

        public static GameObject CurrentOwner => _currentOwner;

        public static IDisposable Begin(GameObject owner)
        {
            return new OwnershipScope(owner);
        }

        public static void Track(GameObject effect, bool pooled)
        {
            if (effect == null)
            {
                return;
            }

            OwnedCombatEffect ownership = effect.GetComponent<OwnedCombatEffect>();
            if (ownership == null && _currentOwner == null)
            {
                return;
            }

            if (ownership == null)
            {
                ownership = effect.AddComponent<OwnedCombatEffect>();
            }

            ownership.Configure(_currentOwner, pooled);
        }

        public static void Retire(GameObject owner)
        {
            if (owner == null)
            {
                return;
            }

            OwnedCombatEffect[] effects = UnityEngine.Object.FindObjectsByType<OwnedCombatEffect>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < effects.Length; index++)
            {
                OwnedCombatEffect effect = effects[index];
                if (effect == null || effect.Owner != owner)
                {
                    continue;
                }

                bool pooled = effect.Pooled;
                effect.ClearOwner();
                if (pooled)
                {
                    effect.gameObject.SetActive(false);
                }
                else
                {
                    UnityEngine.Object.Destroy(effect.gameObject);
                }
            }
        }

        private sealed class OwnershipScope : IDisposable
        {
            private readonly GameObject _previousOwner;
            private bool _disposed;

            public OwnershipScope(GameObject owner)
            {
                _previousOwner = _currentOwner;
                _currentOwner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _currentOwner = _previousOwner;
            }
        }
    }

    internal sealed class OwnedCombatEffect : MonoBehaviour
    {
        public GameObject Owner { get; private set; }
        public bool Pooled { get; private set; }

        public void Configure(GameObject owner, bool pooled)
        {
            Owner = owner;
            Pooled = pooled;
        }

        public void ClearOwner()
        {
            Owner = null;
        }
    }

    public class RuntimeVfxPool : MonoBehaviour
    {
        private static readonly Dictionary<string, Queue<GameObject>> Pools = new Dictionary<string, Queue<GameObject>>();
        private static readonly Dictionary<string, int> ActiveCounts = new Dictionary<string, int>();
        private static RuntimeVfxPool _instance;

        public static bool TryGet(string key, int maxActive, Func<GameObject> create, out GameObject instance)
        {
            instance = null;
            if (GetActiveCount(key) >= maxActive)
            {
                return false;
            }

            EnsureInstance();
            if (Pools.TryGetValue(key, out var pool) && pool.Count > 0)
            {
                instance = pool.Dequeue();
            }
            else
            {
                instance = create();
            }

            instance.transform.SetParent(null, true);
            instance.SetActive(true);
            CombatEffectOwnership.Track(instance, true);
            ActiveCounts[key] = GetActiveCount(key) + 1;
            return true;
        }

        public static void ReleaseAfter(string key, GameObject instance, float delaySeconds, int maxPoolSize)
        {
            if (instance == null)
            {
                return;
            }

            EnsureInstance();
            _instance.StartCoroutine(_instance.ReleaseRoutine(key, instance, delaySeconds, maxPoolSize));
        }

        private IEnumerator ReleaseRoutine(string key, GameObject instance, float delaySeconds, int maxPoolSize)
        {
            yield return new WaitForSeconds(delaySeconds);

            if (instance == null)
            {
                yield break;
            }

            ActiveCounts[key] = Mathf.Max(0, GetActiveCount(key) - 1);
            instance.GetComponent<OwnedCombatEffect>()?.ClearOwner();
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);

            if (!Pools.TryGetValue(key, out var pool))
            {
                pool = new Queue<GameObject>();
                Pools[key] = pool;
            }

            if (pool.Count < maxPoolSize)
            {
                pool.Enqueue(instance);
            }
            else
            {
                Destroy(instance);
            }
        }

        private static int GetActiveCount(string key)
        {
            return ActiveCounts.TryGetValue(key, out int count) ? count : 0;
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject("RuntimeVfxPool");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _instance = host.AddComponent<RuntimeVfxPool>();
        }
    }
}
