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
        private UnityEngine.Camera _camera;

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
                WorldAtlasServiceQueryResult<IReadOnlyList<WorldObjectiveData>> result =
                    atlas.GetObjectivesForRealm(viewerRealm);
                if (!result.IsAvailable || result.Value == null)
                {
                    string diagnostic = result.Diagnostics.Count == 0
                        ? result.Status.ToString()
                        : result.Diagnostics[0].Code;
                    Debug.LogWarning($"World objective markers unavailable: {diagnostic}");
                    return;
                }

                var objectives = new List<WorldObjectiveData>(result.Value);

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
            if (!_faceMainCamera)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                return;
            }

            foreach (var label in _labels)
            {
                if (label != null)
                {
                    label.rotation = Quaternion.LookRotation(label.position - _camera.transform.position);
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

            CreatePrimitivePart(marker.transform, "Base", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.4f, 0.18f, 1.4f), realmColor);
            CreatePrimitivePart(marker.transform, "Pylon", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f), new Vector3(0.22f, 1.9f, 0.22f), Color.Lerp(realmColor, Color.black, 0.35f));
            CreatePrimitivePart(marker.transform, "ObjectiveCore", PrimitiveType.Sphere, new Vector3(0f, 2.1f, 0f), new Vector3(0.58f, 0.58f, 0.58f), resourceColor);
            CreatePrimitivePart(marker.transform, "Banner", PrimitiveType.Cube, new Vector3(0.46f, 1.42f, 0f), new Vector3(0.82f, 0.46f, 0.08f), realmColor);

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

        private static void CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
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
}
