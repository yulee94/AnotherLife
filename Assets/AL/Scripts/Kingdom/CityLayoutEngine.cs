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

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            // Simple Isometric Conversion
            float x = (gridPos.x - gridPos.y) * TileSize * 0.5f;
            float z = (gridPos.x + gridPos.y) * TileSize * 0.25f;
            return new Vector3(x, 0, z);
        }

        public void AutoPlaceBuildings(RealmId realmId, List<BuildingState> buildings)
        {
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

                // Trigger Visual Spawn
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

            GameObject buildingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buildingObj.name = $"Building_{state.BuildingId}";
            buildingObj.transform.position = GridToWorld(pos) + Vector3.up * 0.5f;
            buildingObj.transform.localScale = new Vector3(TileSize * 0.8f, 1f, TileSize * 0.8f);

            // Add a simple label or color based on ID
            var renderer = buildingObj.GetComponent<Renderer>();
            renderer.material.color = Color.Lerp(Color.grey, Color.white, state.Level / 10f);
        }

        private void ClearExistingBuildingVisuals()
        {
            var existingBuildings = GameObject.FindGameObjectsWithTag("Untagged");
            foreach (var building in existingBuildings)
            {
                if (building != null && building.name.StartsWith("Building_"))
                {
                    Destroy(building);
                }
            }
        }
    }
}
