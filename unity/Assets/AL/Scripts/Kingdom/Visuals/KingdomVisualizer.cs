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
        private ITerritoryService _territoryService;

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
            try
            {
                _territoryService = ServiceLocator.Get<ITerritoryService>();
            }
            catch (System.Exception)
            {
                _territoryService = null;
            }
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
            CreateTerritoryOutposts();
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

        private void CreateTerritoryOutposts()
        {
            var outposts = GameObject.Find("Kingdom_TerritoryOutposts") ?? new GameObject("Kingdom_TerritoryOutposts");
            ClearChildren(outposts.transform);
            if (_territoryService == null)
            {
                return;
            }

            int index = 0;
            foreach (var territory in _territoryService.GetTerritories())
            {
                if (territory == null)
                {
                    continue;
                }

                float angle = index * Mathf.PI * 2f / 5f + Mathf.PI / 5f;
                float radius = territory.IsFortress ? 7.95f : 7.10f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.16f, Mathf.Sin(angle) * radius);
                Color ownerColor = GetRealmAccent(territory.OwnerRealm);
                var root = new GameObject("Territory_" + territory.Id);
                root.transform.SetParent(outposts.transform, false);
                root.transform.position = position;

                CreateTerritoryPrimitive(root.transform, "OutpostBase", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), territory.IsFortress ? new Vector3(0.46f, 0.12f, 0.46f) : new Vector3(0.34f, 0.08f, 0.34f), Color.Lerp(ownerColor, Color.black, 0.20f));
                CreateTerritoryPrimitive(root.transform, "OutpostTower", territory.IsFortress ? PrimitiveType.Cylinder : PrimitiveType.Cube, new Vector3(0f, territory.IsFortress ? 0.40f : 0.28f, 0f), territory.IsFortress ? new Vector3(0.22f, 0.46f, 0.22f) : new Vector3(0.30f, 0.32f, 0.30f), ownerColor);
                CreateTerritoryPrimitive(root.transform, "OutpostFlag", PrimitiveType.Cube, new Vector3(0.24f, territory.IsFortress ? 0.76f : 0.56f, 0f), new Vector3(0.30f, 0.13f, 0.035f), Color.Lerp(ownerColor, Color.white, 0.25f));

                var labelObject = new GameObject("OutpostLabel");
                labelObject.transform.SetParent(root.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, territory.IsFortress ? 1.08f : 0.86f, -0.14f);
                labelObject.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
                var label = labelObject.AddComponent<TextMesh>();
                label.text = $"{FormatTerritoryName(territory.Name)}\n{territory.BonusType}+{territory.BonusAmount}";
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 44;
                label.characterSize = 0.050f;
                label.color = Color.Lerp(ownerColor, Color.white, 0.38f);

                index++;
            }
        }

        private static GameObject CreateTerritoryPrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return obj;
        }

        private static string FormatTerritoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Outpost";
            }

            return name.Length <= 13 ? name : name.Substring(0, 13);
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
