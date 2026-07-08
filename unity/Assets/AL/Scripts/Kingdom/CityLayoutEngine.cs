using UnityEngine;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;
using AL.Data.Runtime;

namespace AL.Kingdom
{
    public class CityLayoutEngine : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float TileSize = 1.0f;
        public Vector2Int GridSize = new Vector2Int(20, 20);

        private Dictionary<Vector2Int, BuildingState> _occupiedTiles = new Dictionary<Vector2Int, BuildingState>();
        private Transform _visualRoot;
        private RealmId _activeRealmId;

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            // Simple Isometric Conversion
            float x = (gridPos.x - gridPos.y) * TileSize * 0.5f;
            float z = (gridPos.x + gridPos.y) * TileSize * 0.25f;
            return new Vector3(x, 0, z);
        }

        public void AutoPlaceBuildings(RealmId realmId, List<BuildingState> buildings)
        {
            _activeRealmId = realmId;
            _occupiedTiles.Clear();
            ClearExistingBuildingVisuals();
            int index = 0;

            foreach (var b in buildings)
            {
                if (b == null || string.IsNullOrWhiteSpace(b.BuildingId))
                {
                    continue;
                }

                Vector2Int pos = CalculateRealmPosition(realmId, index++);
                _occupiedTiles[pos] = b;

                SpawnRoadVisual(pos);
                SpawnBuildingVisual(b, pos);
            }
        }

        private Vector2Int CalculateRealmPosition(RealmId realmId, int index)
        {
            return realmId switch
            {
                RealmId.Stonehold => CalculateCircular(index),
                RealmId.Eldergrove => CalculateOrganic(index),
                RealmId.Crownlands => CalculateGrid(index),
                RealmId.Umbral => CalculateDivergent(index),
                _ => new Vector2Int(index, index)
            };
        }

        private Vector2Int CalculateCircular(int i)
        {
            float angle = i * (Mathf.PI * 2 / 15);
            float radius = (i < 5) ? 2 : (i < 10 ? 5 : 8);
            return new Vector2Int((int)(Mathf.Cos(angle) * radius), (int)(Mathf.Sin(angle) * radius));
        }

        private Vector2Int CalculateOrganic(int i)
        {
            Random.InitState(i * 123);
            return new Vector2Int(Random.Range(-10, 10), Random.Range(-10, 10));
        }

        private Vector2Int CalculateGrid(int i)
        {
            return new Vector2Int((i % 4) * 3, (i / 4) * 3);
        }

        private Vector2Int CalculateDivergent(int i)
        {
            return new Vector2Int(i * 2, (i % 2 == 0) ? i : -i);
        }

        private void SpawnBuildingVisual(BuildingState state, Vector2Int pos)
        {
            if (state == null)
            {
                return;
            }

            Transform root = EnsureVisualRoot();
            Vector3 worldPosition = GridToWorld(pos);
            float height = Mathf.Clamp(0.72f + state.Level * 0.08f, 0.72f, 1.85f);
            Color bodyColor = GetBuildingBodyColor(state.BuildingId);
            Color accentColor = GetRealmAccent(_activeRealmId);

            var buildingRoot = new GameObject($"Building_{state.BuildingId}");
            buildingRoot.transform.SetParent(root, false);
            buildingRoot.transform.position = worldPosition;

            CreatePrimitive(buildingRoot.transform, "Base", PrimitiveType.Cube, new Vector3(0f, height * 0.5f, 0f), new Vector3(TileSize * 0.88f, height, TileSize * 0.88f), bodyColor);
            CreatePrimitive(buildingRoot.transform, "Trim", PrimitiveType.Cube, new Vector3(0f, height + 0.04f, 0f), new Vector3(TileSize * 0.98f, 0.10f, TileSize * 0.98f), accentColor);

            if (state.BuildingId.Contains("Hall") || state.BuildingId.Contains("Barracks"))
            {
                CreatePrimitive(buildingRoot.transform, "Spire", PrimitiveType.Cylinder, new Vector3(0f, height + 0.42f, 0f), new Vector3(0.22f, 0.38f, 0.22f), Color.Lerp(accentColor, Color.white, 0.10f));
            }
            else if (state.BuildingId.Contains("Farm") || state.BuildingId.Contains("Lumber"))
            {
                CreatePrimitive(buildingRoot.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0.06f, height + 0.22f, 0.02f), new Vector3(0.46f, 0.25f, 0.46f), Color.Lerp(bodyColor, accentColor, 0.22f));
            }
            else if (state.BuildingId.Contains("Mine") || state.BuildingId.Contains("Quarry"))
            {
                CreatePrimitive(buildingRoot.transform, "CraneArm", PrimitiveType.Cube, new Vector3(0.12f, height + 0.18f, 0f), new Vector3(0.72f, 0.08f, 0.08f), Color.Lerp(bodyColor, Color.white, 0.18f), new Vector3(0f, 0f, 14f));
            }
            else
            {
                CreatePrimitive(buildingRoot.transform, "Roof", PrimitiveType.Cube, new Vector3(0f, height + 0.18f, 0f), new Vector3(TileSize * 0.68f, 0.22f, TileSize * 0.68f), Color.Lerp(bodyColor, accentColor, 0.35f), new Vector3(0f, 45f, 0f));
            }

            CreateLevelBadge(buildingRoot.transform, state, height, accentColor);
        }

        private void SpawnRoadVisual(Vector2Int pos)
        {
            Vector3 end = GridToWorld(pos);
            end.y = 0.035f;
            if (end.sqrMagnitude < 0.05f)
            {
                return;
            }

            Transform root = EnsureVisualRoot();
            Vector3 start = Vector3.zero;
            Vector3 midpoint = (start + end) * 0.5f;
            Vector3 direction = end - start;
            float length = direction.magnitude;
            var road = CreatePrimitive(root, "CityRoad", PrimitiveType.Cube, midpoint, new Vector3(0.10f, 0.035f, length), new Color(0.09f, 0.075f, 0.055f, 0.95f));
            road.transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        private void CreateLevelBadge(Transform parent, BuildingState state, float height, Color accentColor)
        {
            var labelObject = new GameObject("LevelLabel");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, height + 0.78f, -0.18f);
            labelObject.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = $"{GetShortBuildingName(state.BuildingId)}\nLv {state.Level}";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.055f;
            label.color = Color.Lerp(accentColor, Color.white, 0.34f);
        }

        private GameObject CreatePrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Color color, Vector3? localEulerAngles = null)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            obj.transform.localRotation = Quaternion.Euler(localEulerAngles ?? Vector3.zero);
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return obj;
        }

        private Transform EnsureVisualRoot()
        {
            if (_visualRoot != null)
            {
                return _visualRoot;
            }

            var existing = GameObject.Find("Kingdom_CityBoard");
            _visualRoot = existing != null ? existing.transform : new GameObject("Kingdom_CityBoard").transform;
            return _visualRoot;
        }

        private static Color GetBuildingBodyColor(string buildingId)
        {
            if (buildingId.Contains("Farm"))
            {
                return new Color(0.30f, 0.46f, 0.20f);
            }

            if (buildingId.Contains("Lumber"))
            {
                return new Color(0.27f, 0.36f, 0.22f);
            }

            if (buildingId.Contains("Quarry") || buildingId.Contains("Mine"))
            {
                return new Color(0.30f, 0.31f, 0.34f);
            }

            if (buildingId.Contains("Gold"))
            {
                return new Color(0.54f, 0.42f, 0.18f);
            }

            if (buildingId.Contains("Mana"))
            {
                return new Color(0.22f, 0.22f, 0.48f);
            }

            if (buildingId.Contains("Barracks"))
            {
                return new Color(0.43f, 0.20f, 0.17f);
            }

            return new Color(0.36f, 0.36f, 0.39f);
        }

        private static Color GetRealmAccent(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.84f, 0.58f, 0.28f),
                RealmId.Eldergrove => new Color(0.22f, 0.78f, 0.38f),
                RealmId.Crownlands => new Color(0.92f, 0.72f, 0.24f),
                RealmId.Umbral => new Color(0.68f, 0.24f, 0.92f),
                _ => new Color(0.62f, 0.72f, 0.82f)
            };
        }

        private static string GetShortBuildingName(string buildingId)
        {
            return buildingId
                .Replace("TownHall", "Hall")
                .Replace("LumberMill", "Lumber")
                .Replace("GoldMine", "Gold")
                .Replace("ManaShrine", "Mana");
        }

        private void ClearExistingBuildingVisuals()
        {
            Transform root = EnsureVisualRoot();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }
    }
}
