using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using UnityEngine;

namespace AL.RealmWar.World
{
    public class WorldObjectiveMarkerSpawner : MonoBehaviour
    {
        [SerializeField] private int _maxMarkers = 8;
        [SerializeField] private float _ringRadius = 18f;
        [SerializeField] private float _markerHeight = 0.9f;
        [SerializeField] private bool _faceMainCamera = true;

        private readonly List<GameObject> _spawnedMarkers = new List<GameObject>();
        private readonly List<Transform> _labels = new List<Transform>();

        public void Configure(RealmId viewerRealm, int maxMarkers = 8)
        {
            _maxMarkers = Mathf.Max(1, maxMarkers);
            SpawnForRealm(viewerRealm);
        }

        public void SpawnForRealm(RealmId viewerRealm)
        {
            ClearMarkers();

            try
            {
                var atlas = ServiceLocator.Get<IWorldAtlasService>();
                var objectives = new List<WorldObjectiveData>(atlas.GetObjectivesForRealm(viewerRealm));
                objectives.Sort((left, right) => right.PassiveCreditWeight.CompareTo(left.PassiveCreditWeight));

                int count = Mathf.Min(_maxMarkers, objectives.Count);
                for (int index = 0; index < count; index++)
                {
                    SpawnMarker(objectives[index], index, count);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"World objective markers unavailable: {ex.Message}");
            }
        }

        private void LateUpdate()
        {
            if (!_faceMainCamera || Camera.main == null)
            {
                return;
            }

            foreach (var label in _labels)
            {
                if (label != null)
                {
                    label.rotation = Quaternion.LookRotation(label.position - Camera.main.transform.position);
                }
            }
        }

        private void OnDestroy()
        {
            ClearMarkers();
        }

        private void SpawnMarker(WorldObjectiveData objective, int index, int count)
        {
            float angle = count <= 1 ? 0f : index * Mathf.PI * 2f / count;
            Vector3 position = new Vector3(Mathf.Cos(angle) * _ringRadius, _markerHeight, Mathf.Sin(angle) * _ringRadius);

            var marker = new GameObject("WorldObjectiveMarker_" + SafeId(objective.Id, index));
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = position;
            marker.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            _spawnedMarkers.Add(marker);

            Color realmColor = GetRealmColor(objective.OwnerRealm);
            Color resourceColor = GetResourceColor(objective.RareResourceReward);
            float weight = Mathf.Clamp01(objective.PassiveCreditWeight <= 0f ? 0.18f : objective.PassiveCreditWeight);
            float beaconScale = Mathf.Lerp(0.88f, 1.18f, weight);
            Color trimColor = Color.Lerp(resourceColor, Color.white, objective.IsWarzoneObjective ? 0.34f : 0.18f);

            CreatePrimitivePart(marker.transform, "Base", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.4f, 0.18f, 1.4f) * beaconScale, realmColor, 0.08f, 0.42f);
            CreatePrimitivePart(marker.transform, "BaseGlow", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0f), new Vector3(1.08f, 0.035f, 1.08f) * beaconScale, resourceColor, 0f, 0.86f, 0.32f);
            CreatePrimitivePart(marker.transform, "Pylon", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f), new Vector3(0.22f, 1.9f, 0.22f) * beaconScale, Color.Lerp(realmColor, Color.black, 0.35f), 0.16f, 0.50f);
            CreatePrimitivePart(marker.transform, "ObjectiveCore", PrimitiveType.Sphere, new Vector3(0f, 2.1f, 0f), new Vector3(0.58f, 0.58f, 0.58f) * Mathf.Lerp(0.95f, 1.18f, weight), resourceColor, 0f, 0.88f, 0.58f);
            CreatePrimitivePart(marker.transform, "CoreHalo", PrimitiveType.Cylinder, new Vector3(0f, 2.1f, 0f), new Vector3(0.88f, 0.018f, 0.88f) * beaconScale, new Vector3(90f, 0f, 0f), trimColor, 0f, 0.90f, 0.36f);
            CreatePrimitivePart(marker.transform, "Banner", PrimitiveType.Cube, new Vector3(0.46f, 1.42f, 0f), new Vector3(0.82f, 0.46f, 0.08f) * beaconScale, realmColor, 0.04f, 0.46f);
            CreatePrimitivePart(marker.transform, "BannerMark", PrimitiveType.Cube, new Vector3(0.49f, 1.42f, 0f), new Vector3(0.50f, 0.060f, 0.088f) * beaconScale, trimColor, 0f, 0.76f, 0.20f);
            CreateMarkerNotches(marker.transform, resourceColor, trimColor, beaconScale, objective.IsWarzoneObjective);
            if (objective.IsWarzoneObjective)
            {
                CreateWarzoneMarkerAccents(marker.transform, realmColor, trimColor, beaconScale);
            }

            var pulse = marker.AddComponent<WorldObjectiveMarkerPulse>();
            pulse.Configure(resourceColor, trimColor, 0.34f + weight * 0.42f);

            CreateLabel(marker.transform, objective);
        }

        private void CreateLabel(Transform parent, WorldObjectiveData objective)
        {
            var labelObject = new GameObject("ObjectiveLabel");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 2.85f, 0f);

            var text = labelObject.AddComponent<TextMesh>();
            text.text = $"{SafeText(objective.DisplayName, objective.Id)}\n{SafeText(objective.ObjectiveType, "Objective")}";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 26;
            text.characterSize = 0.11f;
            text.color = Color.white;

            _labels.Add(labelObject.transform);
        }

        private static void CreateMarkerNotches(Transform parent, Color resourceColor, Color trimColor, float scale, bool isWarzoneObjective)
        {
            int count = isWarzoneObjective ? 10 : 6;
            float radius = isWarzoneObjective ? 0.92f : 0.76f;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.22f, Mathf.Sin(angle) * radius);
                Vector3 euler = new Vector3(0f, -angle * Mathf.Rad2Deg, 0f);
                CreatePrimitivePart(parent, "BaseRune_" + i, PrimitiveType.Cube, position, new Vector3(0.18f, 0.035f, 0.05f) * scale, euler, i % 2 == 0 ? resourceColor : trimColor, 0f, 0.78f, 0.18f);
            }
        }

        private static void CreateWarzoneMarkerAccents(Transform parent, Color realmColor, Color trimColor, float scale)
        {
            CreatePrimitivePart(parent, "WarzoneSpire_L", PrimitiveType.Cube, new Vector3(-0.28f, 1.88f, -0.12f), new Vector3(0.080f, 0.82f, 0.080f) * scale, new Vector3(0f, 0f, -14f), trimColor, 0.10f, 0.72f, 0.24f);
            CreatePrimitivePart(parent, "WarzoneSpire_R", PrimitiveType.Cube, new Vector3(0.28f, 1.88f, -0.12f), new Vector3(0.080f, 0.82f, 0.080f) * scale, new Vector3(0f, 0f, 14f), trimColor, 0.10f, 0.72f, 0.24f);
            CreatePrimitivePart(parent, "WarzoneCrossbar", PrimitiveType.Cube, new Vector3(0f, 1.74f, -0.16f), new Vector3(0.78f, 0.060f, 0.060f) * scale, Vector3.zero, Color.Lerp(realmColor, trimColor, 0.45f), 0.12f, 0.68f, 0.16f);
        }

        private static void CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
        {
            CreatePrimitivePart(parent, name, type, localPosition, localScale, Vector3.zero, color, 0f, 0.40f);
        }

        private static void CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color, float metallic, float smoothness, float emissionStrength = 0f)
        {
            CreatePrimitivePart(parent, name, type, localPosition, localScale, Vector3.zero, color, metallic, smoothness, emissionStrength);
        }

        private static void CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Color color, float metallic, float smoothness, float emissionStrength = 0f)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
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

                if (emissionStrength > 0f && material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * emissionStrength);
                }

                renderer.material = material;
            }
        }

        private void ClearMarkers()
        {
            foreach (var marker in _spawnedMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }

            _spawnedMarkers.Clear();
            _labels.Clear();
        }

        private static string SafeId(string value, int fallbackIndex)
        {
            return string.IsNullOrWhiteSpace(value) ? fallbackIndex.ToString() : value;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static Color GetRealmColor(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                    return new Color(0.48f, 0.44f, 0.38f);
                case RealmId.Eldergrove:
                    return new Color(0.22f, 0.64f, 0.28f);
                case RealmId.Crownlands:
                    return new Color(0.20f, 0.34f, 0.84f);
                case RealmId.Umbral:
                    return new Color(0.30f, 0.12f, 0.44f);
                default:
                    return new Color(0.54f, 0.48f, 0.36f);
            }
        }

        private static Color GetResourceColor(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.DeepOre:
                    return new Color(0.74f, 0.70f, 0.64f);
                case ResourceType.WorldSap:
                    return new Color(0.42f, 0.95f, 0.54f);
                case ResourceType.RoyalSigil:
                    return new Color(0.94f, 0.78f, 0.24f);
                case ResourceType.DarkCrystal:
                    return new Color(0.66f, 0.18f, 0.92f);
                case ResourceType.Gold:
                    return new Color(1.0f, 0.72f, 0.18f);
                default:
                    return new Color(0.72f, 0.78f, 0.84f);
            }
        }
    }

    internal sealed class WorldObjectiveMarkerPulse : MonoBehaviour
    {
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private Color _primary;
        private Color _secondary;
        private float _intensity = 0.4f;
        private float _seed;

        public void Configure(Color primary, Color secondary, float intensity)
        {
            _primary = primary;
            _secondary = secondary;
            _intensity = Mathf.Clamp01(intensity);
            _seed = transform.GetSiblingIndex() * 0.37f;
            Rebind();
        }

        private void OnEnable()
        {
            Rebind();
        }

        private void Rebind()
        {
            _renderers.Clear();
            GetComponentsInChildren(true, _renderers);
        }

        private void Update()
        {
            float pulse = 0.45f + Mathf.Sin(Time.time * Mathf.Lerp(1.4f, 3.2f, _intensity) + _seed) * 0.18f;
            transform.localScale = Vector3.one * (1f + pulse * 0.035f);
            for (int i = 0; i < _renderers.Count; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || !renderer.material.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                Color color = i % 2 == 0 ? _primary : _secondary;
                renderer.material.SetColor("_EmissionColor", color * Mathf.Lerp(0.08f, 0.62f, _intensity) * pulse);
            }
        }
    }
}
