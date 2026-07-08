using UnityEngine;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace AL.Kingdom.Visuals
{
    public class KingdomVisualizer : MonoBehaviour
    {
        public static event System.Action<string> OnTerritorySelected;

        private const float BoardHalfExtent = 8.8f;

        private CityLayoutEngine _layoutEngine;
        private IRealmService _realmService;
        private IBuildingService _buildingService;
        private ITerritoryService _territoryService;
        private int _lastVisualHash;
        private bool _hasVisualHash;

        private void Start()
        {
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            if (_layoutEngine == null || _realmService == null || _buildingService == null)
            {
                EnsureServices();
            }

            int visualHash = BuildVisualHash();
            if (_hasVisualHash && visualHash == _lastVisualHash)
            {
                return;
            }

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
            _lastVisualHash = BuildVisualHash();
            _hasVisualHash = true;
        }

        private int BuildVisualHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)(_realmService?.CurrentRealmId ?? RealmId.None);

                if (_buildingService != null)
                {
                    foreach (var building in _buildingService.GetAllBuildingStates())
                    {
                        if (building == null)
                        {
                            continue;
                        }

                        hash = hash * 31 + (building.BuildingId == null ? 0 : building.BuildingId.GetHashCode());
                        hash = hash * 31 + building.Level;
                        hash = hash * 31 + (building.IsUpgrading ? 1 : 0);
                    }
                }

                if (_territoryService != null)
                {
                    foreach (var territory in _territoryService.GetTerritories())
                    {
                        if (territory == null)
                        {
                            continue;
                        }

                        hash = hash * 31 + (territory.Id == null ? 0 : territory.Id.GetHashCode());
                        hash = hash * 31 + (int)territory.OwnerRealm;
                        hash = hash * 31 + (territory.IsFortress ? 1 : 0);
                    }
                }

                return hash;
            }
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
            ApplyMaterial(rend, GetTerrainColor(realmId), 0.03f, 0.42f);

            CreateTacticalGrid(realmId);
            CreateRiverAndRavines(realmId);
            CreateCommandPlaza(realmId);
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

        private void CreateTacticalGrid(RealmId realmId)
        {
            var grid = GameObject.Find("Kingdom_TacticalGrid") ?? new GameObject("Kingdom_TacticalGrid");
            ClearChildren(grid.transform);

            Color accent = GetRealmAccent(realmId);
            Color minorLine = Color.Lerp(GetTerrainColor(realmId), Color.black, 0.32f);
            Color majorLine = Color.Lerp(accent, Color.black, 0.42f);

            for (int i = -8; i <= 8; i++)
            {
                bool major = i % 4 == 0;
                float width = major ? 0.055f : 0.026f;
                Color lineColor = major ? majorLine : minorLine;
                CreateSurfaceStrip(grid.transform, "GridNorthSouth", new Vector3(i, 0.052f, 0f), new Vector3(width, 0.025f, 16.2f), lineColor);
                CreateSurfaceStrip(grid.transform, "GridEastWest", new Vector3(0f, 0.054f, i), new Vector3(16.2f, 0.025f, width), lineColor);
            }

            CreateSurfaceStrip(grid.transform, "InnerDistrictPlate", new Vector3(0f, 0.060f, 0f), new Vector3(4.55f, 0.028f, 3.15f), Color.Lerp(GetTerrainColor(realmId), accent, 0.16f));
            CreateRouteSegment(grid.transform, "NorthTradeRoute", new Vector3(0f, 0f, 0.45f), new Vector3(0.35f, 0f, 6.45f), 0.13f, Color.Lerp(accent, Color.black, 0.25f));
            CreateRouteSegment(grid.transform, "SouthTradeRoute", new Vector3(0f, 0f, -0.45f), new Vector3(-0.35f, 0f, -6.45f), 0.13f, Color.Lerp(accent, Color.black, 0.28f));
            CreateRouteSegment(grid.transform, "WestTradeRoute", new Vector3(-0.45f, 0f, 0f), new Vector3(-6.4f, 0f, -0.28f), 0.13f, Color.Lerp(accent, Color.black, 0.29f));
            CreateRouteSegment(grid.transform, "EastTradeRoute", new Vector3(0.45f, 0f, 0f), new Vector3(6.4f, 0f, 0.28f), 0.13f, Color.Lerp(accent, Color.black, 0.29f));
        }

        private void CreateRiverAndRavines(RealmId realmId)
        {
            var water = GameObject.Find("Kingdom_Waterways") ?? new GameObject("Kingdom_Waterways");
            ClearChildren(water.transform);

            Color waterColor = realmId == RealmId.Umbral
                ? new Color(0.20f, 0.18f, 0.34f)
                : new Color(0.06f, 0.24f, 0.32f);
            Color bankColor = Color.Lerp(GetTerrainColor(realmId), Color.black, 0.24f);

            Vector3[] path =
            {
                new Vector3(-7.5f, 0f, 4.9f),
                new Vector3(-5.2f, 0f, 3.4f),
                new Vector3(-3.1f, 0f, 3.0f),
                new Vector3(-0.8f, 0f, 1.6f),
                new Vector3(1.8f, 0f, 1.3f),
                new Vector3(4.2f, 0f, -0.2f),
                new Vector3(7.2f, 0f, -1.6f)
            };

            for (int i = 0; i < path.Length - 1; i++)
            {
                CreateRouteSegment(water.transform, "WaterChannel", path[i], path[i + 1], 0.36f, waterColor, 0.043f);
                CreateRouteSegment(water.transform, "WaterBank", path[i] + Vector3.forward * 0.12f, path[i + 1] + Vector3.forward * 0.12f, 0.08f, bankColor, 0.050f);
            }

            CreateSurfaceStrip(water.transform, "StoneBridge", new Vector3(-0.65f, 0.095f, 1.56f), new Vector3(1.34f, 0.08f, 0.28f), Color.Lerp(bankColor, Color.white, 0.10f), new Vector3(0f, 22f, 0f));
            CreateSurfaceStrip(water.transform, "EastBridge", new Vector3(4.15f, 0.095f, -0.18f), new Vector3(1.18f, 0.08f, 0.24f), Color.Lerp(bankColor, Color.white, 0.08f), new Vector3(0f, 28f, 0f));
        }

        private void CreateCommandPlaza(RealmId realmId)
        {
            var plaza = GameObject.Find("Kingdom_CommandPlaza") ?? new GameObject("Kingdom_CommandPlaza");
            ClearChildren(plaza.transform);

            Color accent = GetRealmAccent(realmId);
            Color stone = Color.Lerp(GetTerrainColor(realmId), Color.white, 0.10f);
            Color hotAccent = Color.Lerp(accent, new Color(1f, 0.88f, 0.46f), 0.38f);

            CreateSurfaceStrip(plaza.transform, "CommandDais", new Vector3(0f, 0.105f, 0f), new Vector3(1.64f, 0.10f, 1.64f), stone, new Vector3(0f, 45f, 0f));
            var core = CreateTerritoryPrimitive(plaza.transform, "RealmCore", PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.28f, 0.35f, 0.28f), hotAccent, 0.1f, 0.65f, hotAccent * 0.28f);
            core.AddComponent<KingdomTacticalPulse>().Configure(hotAccent, Color.Lerp(hotAccent, Color.white, 0.30f), 1.35f);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 0.96f, 0.27f, Mathf.Sin(angle) * 0.96f);
                CreateTerritoryPrimitive(plaza.transform, "PlazaWard", PrimitiveType.Cylinder, position, new Vector3(0.10f, 0.22f, 0.10f), Color.Lerp(accent, Color.black, 0.12f), 0.05f, 0.50f, accent * 0.18f);
            }

            CreatePointLight(plaza.transform, "CommandCoreLight", new Vector3(0f, 1.65f, -0.10f), hotAccent, 1.25f, 4.3f);
        }

        private void CreateBoardFrame(RealmId realmId)
        {
            var frame = GameObject.Find("Kingdom_BoardFrame") ?? new GameObject("Kingdom_BoardFrame");
            ClearChildren(frame.transform);
            Color accent = GetRealmAccent(realmId);

            CreateFramePiece(frame.transform, "NorthFrame", new Vector3(0f, 0.08f, BoardHalfExtent), new Vector3(17.4f, 0.16f, 0.18f), accent);
            CreateFramePiece(frame.transform, "SouthFrame", new Vector3(0f, 0.08f, -BoardHalfExtent), new Vector3(17.4f, 0.16f, 0.18f), accent);
            CreateFramePiece(frame.transform, "EastFrame", new Vector3(BoardHalfExtent, 0.08f, 0f), new Vector3(0.18f, 0.16f, 17.4f), accent);
            CreateFramePiece(frame.transform, "WestFrame", new Vector3(-BoardHalfExtent, 0.08f, 0f), new Vector3(0.18f, 0.16f, 17.4f), accent);

            float corner = BoardHalfExtent - 0.24f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateTerritoryPrimitive(frame.transform, "CornerBastion", PrimitiveType.Cylinder, new Vector3(corner * x, 0.28f, corner * z), new Vector3(0.34f, 0.28f, 0.34f), Color.Lerp(accent, Color.black, 0.08f), 0.06f, 0.48f, accent * 0.12f);
                    CreatePointLight(frame.transform, "CornerBeacon", new Vector3(corner * x, 0.98f, corner * z), Color.Lerp(accent, Color.white, 0.18f), 0.26f, 2.2f);
                }
            }
        }

        private void CreateRealmLandmarks(RealmId realmId)
        {
            var landmarks = GameObject.Find("Kingdom_RealmLandmarks") ?? new GameObject("Kingdom_RealmLandmarks");
            ClearChildren(landmarks.transform);
            Color accent = GetRealmAccent(realmId);
            Color baseColor = Color.Lerp(GetTerrainColor(realmId), Color.black, 0.18f);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 7.4f, 0.25f, Mathf.Sin(angle) * 7.4f);
                CreateTerritoryPrimitive(landmarks.transform, "RealmMarkerBase_" + i, PrimitiveType.Cylinder, new Vector3(position.x, 0.10f, position.z), new Vector3(0.44f, 0.10f, 0.44f), baseColor, 0.02f, 0.30f);
                var marker = CreateTerritoryPrimitive(landmarks.transform, "RealmMarker_" + i, PrimitiveType.Cylinder, position, new Vector3(0.18f, 0.34f + i % 2 * 0.16f, 0.18f), Color.Lerp(accent, Color.white, 0.16f), 0.05f, 0.60f, accent * 0.16f);
                marker.AddComponent<KingdomTacticalPulse>().Configure(Color.Lerp(accent, Color.white, 0.08f), Color.Lerp(accent, Color.white, 0.38f), 0.9f + i * 0.07f);
                CreatePointLight(landmarks.transform, "RealmMarkerLight_" + i, new Vector3(position.x, 0.94f, position.z), accent, 0.24f, 1.75f);
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
                Color routeColor = Color.Lerp(ownerColor, Color.black, territory.OwnerRealm == RealmId.None ? 0.65f : 0.32f);
                CreateRouteSegment(outposts.transform, "OutpostSupplyRoute", Vector3.zero, position, territory.IsFortress ? 0.12f : 0.08f, routeColor, 0.072f);

                var root = new GameObject("Territory_" + territory.Id);
                root.transform.SetParent(outposts.transform, false);
                root.transform.position = position;

                CreateTerritoryPrimitive(root.transform, "OutpostControlRing", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), territory.IsFortress ? new Vector3(0.66f, 0.03f, 0.66f) : new Vector3(0.52f, 0.025f, 0.52f), routeColor, 0.02f, 0.42f, territory.OwnerRealm == RealmId.None ? null : routeColor * 0.10f);
                var baseObject = CreateTerritoryPrimitive(root.transform, "OutpostBase", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), territory.IsFortress ? new Vector3(0.46f, 0.12f, 0.46f) : new Vector3(0.34f, 0.08f, 0.34f), Color.Lerp(ownerColor, Color.black, 0.20f), 0.04f, 0.40f);
                var towerObject = CreateTerritoryPrimitive(root.transform, "OutpostTower", territory.IsFortress ? PrimitiveType.Cylinder : PrimitiveType.Cube, new Vector3(0f, territory.IsFortress ? 0.42f : 0.30f, 0f), territory.IsFortress ? new Vector3(0.22f, 0.46f, 0.22f) : new Vector3(0.30f, 0.32f, 0.30f), ownerColor, 0.05f, 0.52f, territory.OwnerRealm == RealmId.None ? null : ownerColor * 0.12f);
                baseObject.AddComponent<KingdomTerritorySelectable>().Configure(territory.Name, territory.OwnerRealm, territory.BonusType, territory.BonusAmount, territory.IsFortress, Color.Lerp(ownerColor, Color.black, 0.20f), ownerColor);
                towerObject.AddComponent<KingdomTerritorySelectable>().Configure(territory.Name, territory.OwnerRealm, territory.BonusType, territory.BonusAmount, territory.IsFortress, ownerColor, Color.Lerp(ownerColor, Color.white, 0.25f));
                CreateTerritoryPrimitive(root.transform, "OutpostFlag", PrimitiveType.Cube, new Vector3(0.24f, territory.IsFortress ? 0.78f : 0.58f, 0f), new Vector3(0.30f, 0.13f, 0.035f), Color.Lerp(ownerColor, Color.white, 0.25f), 0.02f, 0.44f, territory.OwnerRealm == RealmId.None ? null : ownerColor * 0.08f);
                CreatePointLight(root.transform, "OutpostStatusLight", new Vector3(0f, territory.IsFortress ? 1.12f : 0.88f, -0.08f), Color.Lerp(ownerColor, Color.white, 0.18f), territory.IsFortress ? 0.50f : 0.34f, territory.IsFortress ? 2.4f : 1.8f);

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

        private static GameObject CreateTerritoryPrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Color color, float metallic = 0f, float smoothness = 0.35f, Color? emission = null)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                ApplyMaterial(renderer, color, metallic, smoothness, emission);
            }

            return obj;
        }

        private static void CreateSurfaceStrip(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, Vector3? localEulerAngles = null)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent, false);
            strip.transform.localPosition = localPosition;
            strip.transform.localScale = localScale;
            strip.transform.localRotation = Quaternion.Euler(localEulerAngles ?? Vector3.zero);
            ApplyMaterial(strip.GetComponent<Renderer>(), color, 0.02f, 0.32f);
        }

        private static void CreateRouteSegment(Transform parent, string name, Vector3 start, Vector3 end, float width, Color color, float y = 0.070f)
        {
            Vector3 direction = end - start;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = name;
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = new Vector3((start.x + end.x) * 0.5f, y, (start.z + end.z) * 0.5f);
            segment.transform.localRotation = Quaternion.LookRotation(direction.normalized);
            segment.transform.localScale = new Vector3(width, 0.035f, direction.magnitude);
            ApplyMaterial(segment.GetComponent<Renderer>(), color, 0.03f, 0.48f, color * 0.04f);
        }

        private static void CreatePointLight(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        private static void ApplyMaterial(Renderer renderer, Color color, float metallic = 0f, float smoothness = 0.35f, Color? emission = null)
        {
            if (renderer == null)
            {
                return;
            }

            Material material = renderer.material;
            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
        }

        private static string FormatTerritoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Outpost";
            }

            return name.Length <= 13 ? name : name.Substring(0, 13);
        }

        internal static void RaiseTerritorySelected(string name, RealmId owner, ResourceType bonusType, long bonusAmount, bool isFortress)
        {
            string territoryName = string.IsNullOrWhiteSpace(name) ? "Outpost" : name;
            string ownerLabel = owner == RealmId.None ? "Neutral" : owner.ToString();
            string kind = isFortress ? "Fortress" : "Outpost";
            OnTerritorySelected?.Invoke($"{kind}: {territoryName}. Owner {ownerLabel}. Bonus +{bonusAmount} {bonusType}.");
        }

        private static void CreateFramePiece(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.position = position;
            piece.transform.localScale = scale;
            ApplyMaterial(piece.GetComponent<Renderer>(), Color.Lerp(color, Color.black, 0.15f), 0.05f, 0.55f, color * 0.07f);
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

    public class KingdomTacticalPulse : MonoBehaviour
    {
        private Color _baseColor;
        private Color _pulseColor;
        private float _speed = 1f;
        private Renderer _renderer;

        public void Configure(Color baseColor, Color pulseColor, float speed)
        {
            _baseColor = baseColor;
            _pulseColor = pulseColor;
            _speed = Mathf.Max(0.1f, speed);
            _renderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }

            if (_renderer == null)
            {
                return;
            }

            float pulse = 0.35f + Mathf.PingPong(Time.time * _speed, 1f) * 0.45f;
            _renderer.material.color = Color.Lerp(_baseColor, _pulseColor, pulse);
            if (_renderer.material.HasProperty("_EmissionColor"))
            {
                _renderer.material.SetColor("_EmissionColor", Color.Lerp(_baseColor, _pulseColor, pulse) * 0.18f);
            }
        }
    }

    public class KingdomTerritorySelectable : MonoBehaviour
    {
        private string _name;
        private RealmId _owner;
        private ResourceType _bonusType;
        private long _bonusAmount;
        private bool _isFortress;
        private Color _baseColor;
        private Color _accentColor;
        private Renderer _renderer;
        private float _highlightTimer;

        public void Configure(string name, RealmId owner, ResourceType bonusType, long bonusAmount, bool isFortress, Color baseColor, Color accentColor)
        {
            _name = name;
            _owner = owner;
            _bonusType = bonusType;
            _bonusAmount = bonusAmount;
            _isFortress = isFortress;
            _baseColor = baseColor;
            _accentColor = accentColor;
            _renderer = GetComponent<Renderer>();
        }

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            _highlightTimer = 0.48f;
            KingdomVisualizer.RaiseTerritorySelected(_name, _owner, _bonusType, _bonusAmount, _isFortress);
        }

        private void Update()
        {
            if (_highlightTimer <= 0f)
            {
                SetColor(_baseColor);
                return;
            }

            _highlightTimer -= Time.deltaTime;
            float pulse = Mathf.PingPong(Time.time * 7.5f, 1f);
            SetColor(Color.Lerp(_baseColor, _accentColor, 0.40f + pulse * 0.38f));
        }

        private void SetColor(Color color)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }

            if (_renderer != null)
            {
                _renderer.material.color = color;
            }
        }
    }
}
