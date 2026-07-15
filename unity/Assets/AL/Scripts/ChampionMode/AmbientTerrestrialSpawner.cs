using AL.Core;
using UnityEngine;

namespace AL.ChampionMode
{
    public class AmbientTerrestrialSpawner : MonoBehaviour
    {
        [SerializeField] private int _spawnCount = 10;
        [SerializeField] private float _innerRadius = 13.8f;
        [SerializeField] private float _outerRadius = 18.4f;
        [SerializeField] private RealmId _realmId = RealmId.Crownlands;

        private bool _spawned;

        public void Configure(RealmId realmId, int spawnCount, float innerRadius = 13.8f, float outerRadius = 18.4f)
        {
            _realmId = realmId == RealmId.None ? RealmId.Crownlands : realmId;
            _spawnCount = Mathf.Clamp(spawnCount, 0, 24);
            _innerRadius = Mathf.Max(4f, innerRadius);
            _outerRadius = Mathf.Max(_innerRadius + 1f, outerRadius);
            Spawn();
        }

        private void Spawn()
        {
            if (_spawned || _spawnCount <= 0)
            {
                return;
            }

            _spawned = true;
            Color realmColor = GetRealmColor(_realmId);
            Color shadow = new Color(0.025f, 0.022f, 0.028f, 0.78f);
            for (int i = 0; i < _spawnCount; i++)
            {
                float normalized = i / (float)Mathf.Max(1, _spawnCount);
                float angle = normalized * Mathf.PI * 2f + (i % 3) * 0.27f;
                float radius = Mathf.Lerp(_innerRadius, _outerRadius, (i * 0.37f) % 1f);
                Vector3 anchor = new Vector3(Mathf.Cos(angle) * radius, 0.18f, Mathf.Sin(angle) * radius);
                CreateTerrestrial(i, anchor, angle, realmColor, shadow);
            }
        }

        private void CreateTerrestrial(int index, Vector3 anchor, float angle, Color realmColor, Color shadow)
        {
            var root = new GameObject("AmbientTerrestrial_" + index.ToString("00"));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = anchor;
            root.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);

            int variant = index % 4;
            Color body = Color.Lerp(new Color(0.12f, 0.13f, 0.14f), realmColor, 0.28f + variant * 0.06f);
            Color crest = Color.Lerp(realmColor, Color.white, 0.18f + variant * 0.05f);
            float scale = 0.86f + (index % 5) * 0.06f;

            CreatePrimitive(root.transform, "GroundShadow", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0f), new Vector3(0.72f, 0.015f, 0.42f) * scale, Vector3.zero, shadow, true, 0f, 0.20f);
            switch (variant)
            {
                case 0:
                    CreateSkitter(root.transform, body, crest, scale);
                    break;
                case 1:
                    CreateHornback(root.transform, body, crest, scale);
                    break;
                case 2:
                    CreateSpineRunner(root.transform, body, crest, scale);
                    break;
                default:
                    CreateWatcher(root.transform, body, crest, scale);
                    break;
            }

            var motion = root.AddComponent<AmbientTerrestrialMotion>();
            float pathRadius = 0.55f + (index % 4) * 0.18f;
            motion.Configure(anchor, pathRadius, 0.16f + (index % 5) * 0.035f, index * 0.19f, crest);
        }

        private void CreateSkitter(Transform root, Color body, Color crest, float scale)
        {
            CreatePrimitive(root, "Skitter_Body", PrimitiveType.Sphere, new Vector3(0f, 0.10f, 0f), new Vector3(0.46f, 0.20f, 0.78f) * scale, Vector3.zero, body, true, 0.02f, 0.46f);
            CreatePrimitive(root, "Skitter_Head", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.48f * scale), new Vector3(0.30f, 0.18f, 0.26f) * scale, Vector3.zero, body, true, 0.02f, 0.48f);
            CreatePrimitive(root, "Skitter_Crest", PrimitiveType.Cube, new Vector3(0f, 0.30f, 0.10f), new Vector3(0.08f, 0.22f, 0.92f) * scale, new Vector3(12f, 0f, 0f), crest, true, 0f, 0.78f);
            CreateLegs(root, body, scale, 3, 0.42f);
        }

        private void CreateHornback(Transform root, Color body, Color crest, float scale)
        {
            CreatePrimitive(root, "Hornback_Body", PrimitiveType.Capsule, new Vector3(0f, 0.24f, 0f), new Vector3(0.42f, 0.42f, 0.60f) * scale, new Vector3(90f, 0f, 0f), body, true, 0.04f, 0.42f);
            CreatePrimitive(root, "Hornback_Head", PrimitiveType.Sphere, new Vector3(0f, 0.24f, 0.54f * scale), new Vector3(0.28f, 0.24f, 0.28f) * scale, Vector3.zero, body, true, 0.04f, 0.44f);
            CreatePrimitive(root, "Hornback_Horn_L", PrimitiveType.Cube, new Vector3(-0.14f * scale, 0.36f, 0.74f * scale), new Vector3(0.055f, 0.12f, 0.32f) * scale, new Vector3(24f, -18f, 0f), crest, true, 0.02f, 0.80f);
            CreatePrimitive(root, "Hornback_Horn_R", PrimitiveType.Cube, new Vector3(0.14f * scale, 0.36f, 0.74f * scale), new Vector3(0.055f, 0.12f, 0.32f) * scale, new Vector3(24f, 18f, 0f), crest, true, 0.02f, 0.80f);
            CreatePrimitive(root, "Hornback_BackPlate", PrimitiveType.Cube, new Vector3(0f, 0.48f, -0.05f), new Vector3(0.18f, 0.11f, 0.88f) * scale, Vector3.zero, crest, true, 0.04f, 0.70f);
            CreateLegs(root, body, scale, 2, 0.34f);
        }

        private void CreateSpineRunner(Transform root, Color body, Color crest, float scale)
        {
            CreatePrimitive(root, "Runner_Body", PrimitiveType.Capsule, new Vector3(0f, 0.22f, 0f), new Vector3(0.30f, 0.34f, 0.72f) * scale, new Vector3(90f, 0f, 0f), body, true, 0.02f, 0.50f);
            CreatePrimitive(root, "Runner_Tail", PrimitiveType.Cube, new Vector3(0f, 0.18f, -0.58f * scale), new Vector3(0.08f, 0.08f, 0.66f) * scale, new Vector3(-10f, 0f, 0f), body, true, 0.02f, 0.42f);
            for (int i = 0; i < 5; i++)
            {
                float z = -0.30f + i * 0.16f;
                CreatePrimitive(root, "Runner_Spine_" + i, PrimitiveType.Cube, new Vector3(0f, 0.44f + i % 2 * 0.03f, z * scale), new Vector3(0.045f, 0.20f, 0.055f) * scale, new Vector3(0f, 0f, i % 2 == 0 ? 12f : -12f), crest, true, 0f, 0.76f);
            }
            CreateLegs(root, body, scale, 2, 0.30f);
        }

        private void CreateWatcher(Transform root, Color body, Color crest, float scale)
        {
            CreatePrimitive(root, "Watcher_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.30f, 0.18f, 0.30f) * scale, Vector3.zero, body, true, 0.02f, 0.46f);
            CreatePrimitive(root, "Watcher_Neck", PrimitiveType.Cylinder, new Vector3(0f, 0.44f, 0f), new Vector3(0.12f, 0.36f, 0.12f) * scale, Vector3.zero, body, true, 0.02f, 0.48f);
            CreatePrimitive(root, "Watcher_Eye", PrimitiveType.Sphere, new Vector3(0f, 0.82f, 0.16f * scale), new Vector3(0.18f, 0.14f, 0.12f) * scale, Vector3.zero, crest, true, 0f, 0.86f);
            CreatePrimitive(root, "Watcher_Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.82f, 0.16f * scale), new Vector3(0.32f, 0.012f, 0.32f) * scale, new Vector3(90f, 0f, 0f), crest, true, 0f, 0.82f);
        }

        private void CreateLegs(Transform root, Color body, float scale, int pairs, float zSpan)
        {
            for (int i = 0; i < pairs; i++)
            {
                float z = pairs <= 1 ? 0f : Mathf.Lerp(-zSpan, zSpan, i / (float)(pairs - 1));
                for (int side = -1; side <= 1; side += 2)
                {
                    CreatePrimitive(root, "Leg_" + i + "_" + side, PrimitiveType.Cube, new Vector3(side * 0.32f * scale, 0.02f, z * scale), new Vector3(0.34f, 0.045f, 0.055f) * scale, new Vector3(0f, 0f, side * 18f), body, true, 0.02f, 0.34f);
                }
            }
        }

        private static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color, bool removeCollider, float metallic, float smoothness)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localRotation = Quaternion.Euler(localEulerAngles);
            obj.transform.localScale = localScale;
            if (removeCollider)
            {
                var collider = obj.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }
            }

            ApplyMaterial(obj, color, metallic, smoothness);
            return obj;
        }

        private static void ApplyMaterial(GameObject obj, Color color, float metallic, float smoothness)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var shader = Shader.Find("Standard");
            var material = shader != null ? new Material(shader) : new Material(renderer.material);
            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (smoothness > 0.68f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.45f);
            }

            renderer.material = material;
        }

        private static Color GetRealmColor(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.88f, 0.54f, 0.22f),
                RealmId.Eldergrove => new Color(0.22f, 0.88f, 0.42f),
                RealmId.Crownlands => new Color(0.28f, 0.54f, 1f),
                RealmId.Umbral => new Color(0.82f, 0.16f, 1f),
                _ => new Color(0.76f, 0.82f, 0.88f)
            };
        }
    }

    internal sealed class AmbientTerrestrialMotion : MonoBehaviour
    {
        private Vector3 _anchor;
        private float _pathRadius = 0.7f;
        private float _speed = 0.2f;
        private float _offset;
        private Color _accent;
        private Renderer[] _renderers;

        public void Configure(Vector3 anchor, float pathRadius, float speed, float offset, Color accent)
        {
            _anchor = anchor;
            _pathRadius = Mathf.Max(0.1f, pathRadius);
            _speed = Mathf.Max(0.02f, speed);
            _offset = offset;
            _accent = accent;
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Update()
        {
            float t = Time.time * _speed + _offset;
            Vector3 localOffset = new Vector3(Mathf.Cos(t * Mathf.PI * 2f) * _pathRadius, Mathf.Sin(t * 5.2f) * 0.035f, Mathf.Sin(t * Mathf.PI * 2f) * _pathRadius * 0.58f);
            Vector3 nextOffset = new Vector3(Mathf.Cos((t + 0.02f) * Mathf.PI * 2f) * _pathRadius, 0f, Mathf.Sin((t + 0.02f) * Mathf.PI * 2f) * _pathRadius * 0.58f);
            transform.localPosition = _anchor + localOffset;
            Vector3 direction = nextOffset - localOffset;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(direction.normalized), Time.deltaTime * 4.5f);
            }

            PulseAccent(t);
        }

        private void PulseAccent(float t)
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            float pulse = 0.18f + (Mathf.Sin(t * 8.0f) + 1f) * 0.11f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || renderer.gameObject.name.Contains("Shadow") || !renderer.material.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                renderer.material.SetColor("_EmissionColor", _accent * pulse);
            }
        }
    }
}
