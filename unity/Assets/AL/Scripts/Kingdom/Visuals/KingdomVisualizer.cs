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
            _layoutEngine = gameObject.AddComponent<CityLayoutEngine>();
            _realmService = ServiceLocator.Get<IRealmService>();
            _buildingService = ServiceLocator.Get<IBuildingService>();

            InitializeKingdom();
        }

        public void InitializeKingdom()
        {
            RealmId realmId = _realmService.CurrentRealmId;
            Debug.Log($"Visualizing Kingdom for Realm: {realmId}");

            // 1. Setup Terrain/Background
            SetupTerrain(realmId);

            // 2. Place Buildings
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
                floor.transform.localScale = new Vector3(10, 1, 10);
            }

            Renderer rend = floor.GetComponent<Renderer>();
            rend.material.color = realmId switch
            {
                RealmId.Stonehold => new Color(0.4f, 0.4f, 0.4f), // Stone Grey
                RealmId.Eldergrove => new Color(0.1f, 0.5f, 0.1f), // Deep Forest Green
                RealmId.Crownlands => new Color(0.6f, 0.5f, 0.2f), // Royal Gold/Grass
                RealmId.Umbral => new Color(0.2f, 0.1f, 0.3f), // Volcanic Purple
                _ => Color.grey
            };
        }
    }
}
