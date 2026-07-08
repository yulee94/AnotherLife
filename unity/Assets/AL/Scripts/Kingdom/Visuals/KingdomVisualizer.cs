using UnityEngine;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using System.Collections.Generic;

namespace AL.Kingdom.Visuals
{
    public class KingdomVisualizer : MonoBehaviour
    {
        private CityLayoutEngine _layoutEngine;
        private IRealmService _realmService;
        private IBuildingService _buildingService;

        private void Start()
        {
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            InitializeKingdom();
        }

        private void EnsureServices()
        {
            _layoutEngine = gameObject.AddComponent<CityLayoutEngine>();
            _realmService = ServiceLocator.Get<IRealmService>();
            _buildingService = ServiceLocator.Get<IBuildingService>();
        }

        public void InitializeKingdom()
        {
            if (_layoutEngine == null || _realmService == null || _buildingService == null)
            {
                EnsureServices();
            }

            RealmId realmId = _realmService.CurrentRealmId;
            Debug.Log($"Visualizing Kingdom for Realm: {realmId}");

            SetupTerrain(realmId);

            var buildings = new List<BuildingState>(_buildingService.GetAllBuildingStates());
            if (buildings.Count == 0)
            {
                // Ensure base buildings exist for the visualization
                buildings.Add(_buildingService.GetBuildingState("TownHall"));
                buildings.Add(_buildingService.GetBuildingState("Farm"));
                buildings.Add(_buildingService.GetBuildingState("Barracks"));
            }

            _layoutEngine.AutoPlaceBuildings(realmId, buildings);
        }

        private void SetupTerrain(RealmId realmId)
        {
            GameObject floor = GameObject.Find("World_Floor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "World_Floor";
                floor.transform.localScale = new Vector3(12, 1, 12);
            }

            Renderer rend = floor.GetComponent<Renderer>();
            rend.material.color = GetTerrainColor(realmId);

            CreateBoardFrame(realmId);
            CreateRealmLandmarks(realmId);
        }

        private static Color GetTerrainColor(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.27f, 0.29f, 0.31f),
                RealmId.Eldergrove => new Color(0.10f, 0.34f, 0.18f),
                RealmId.Crownlands => new Color(0.34f, 0.38f, 0.20f),
                RealmId.Umbral => new Color(0.16f, 0.08f, 0.23f),
                _ => new Color(0.22f, 0.25f, 0.27f)
            };
        }

        private void CreateBoardFrame(RealmId realmId)
        {
            var frame = GameObject.Find("Kingdom_BoardFrame") ?? new GameObject("Kingdom_BoardFrame");
            ClearChildren(frame.transform);
            Color accent = GetRealmAccent(realmId);

            CreateFramePiece(frame.transform, "NorthFrame", new Vector3(0f, 0.08f, 8.8f), new Vector3(17.4f, 0.16f, 0.18f), accent);
            CreateFramePiece(frame.transform, "SouthFrame", new Vector3(0f, 0.08f, -8.8f), new Vector3(17.4f, 0.16f, 0.18f), accent);
            CreateFramePiece(frame.transform, "EastFrame", new Vector3(8.8f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 17.4f), accent);
            CreateFramePiece(frame.transform, "WestFrame", new Vector3(-8.8f, 0.08f, 0f), new Vector3(0.18f, 0.16f, 17.4f), accent);
        }

        private void CreateRealmLandmarks(RealmId realmId)
        {
            var landmarks = GameObject.Find("Kingdom_RealmLandmarks") ?? new GameObject("Kingdom_RealmLandmarks");
            ClearChildren(landmarks.transform);
            Color accent = GetRealmAccent(realmId);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 7.4f, 0.25f, Mathf.Sin(angle) * 7.4f);
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "RealmMarker_" + i;
                marker.transform.SetParent(landmarks.transform, false);
                marker.transform.position = position;
                marker.transform.localScale = new Vector3(0.24f, 0.34f + i % 2 * 0.16f, 0.24f);
                marker.GetComponent<Renderer>().material.color = Color.Lerp(accent, Color.white, 0.16f);
            }
        }

        private static void CreateFramePiece(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.position = position;
            piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().material.color = Color.Lerp(color, Color.black, 0.15f);
        }

        private static Color GetRealmAccent(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.82f, 0.58f, 0.28f),
                RealmId.Eldergrove => new Color(0.22f, 0.78f, 0.38f),
                RealmId.Crownlands => new Color(0.92f, 0.72f, 0.24f),
                RealmId.Umbral => new Color(0.68f, 0.24f, 0.92f),
                _ => new Color(0.62f, 0.72f, 0.82f)
            };
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
