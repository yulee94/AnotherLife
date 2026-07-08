using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AL.ChampionMode.Skills
{
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
