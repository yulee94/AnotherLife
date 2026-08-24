using UnityEngine;
using AL.Core;
using AL.Core.Interfaces;
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
        private PrivateKingdomCityPresenter _privateCityPresenter;
        private int _lastVisualHash;
        private bool _hasVisualHash;

        private void Start()
        {
            RefreshVisuals();
        }

        public void RefreshVisuals()
        {
            EnsurePrivateCityPresenter().Refresh();
            return;

#pragma warning disable CS0162
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
#pragma warning restore CS0162
        }

        private PrivateKingdomCityPresenter EnsurePrivateCityPresenter()
        {
            if (_privateCityPresenter == null)
            {
                _privateCityPresenter = GetComponent<PrivateKingdomCityPresenter>();
                if (_privateCityPresenter == null)
                {
                    _privateCityPresenter = gameObject.AddComponent<PrivateKingdomCityPresenter>();
                }
            }

            return _privateCityPresenter;
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
            EnsurePrivateCityPresenter().Refresh();
            return;

#pragma warning disable CS0162
            if (_layoutEngine == null || _realmService == null || _buildingService == null)
            {
                EnsureServices();
            }

            RealmId realmId = _realmService.CurrentRealmId;
            Debug.Log($"Visualizing Kingdom for Realm: {realmId}");

            SetupTerrain(realmId);

            IReadOnlyList<KingdomBuildingPresentation> buildings =
                KingdomBuildingPresentationResolver.Resolve(
                    realmId,
                    _buildingService.GetAllBuildingStates());

            _layoutEngine.AutoPlaceBuildings(realmId, buildings);
            CreateTerritoryOutposts();
            CreateAmbientBoardLife(realmId);
            CreateBoardAtmosphere(realmId);
            _lastVisualHash = BuildVisualHash();
            _hasVisualHash = true;
#pragma warning restore CS0162
        }

        private int BuildVisualHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)(_realmService?.CurrentRealmId ?? RealmId.None);

                if (_buildingService != null)
                {
                    IReadOnlyList<KingdomBuildingPresentation> buildings =
                        KingdomBuildingPresentationResolver.Resolve(
                            _realmService?.CurrentRealmId ?? RealmId.None,
                            _buildingService.GetAllBuildingStates());
                    foreach (KingdomBuildingPresentation building in buildings)
                    {
                        if (building == null)
                        {
                            continue;
                        }

                        hash = hash * 31 + building.Slot.SlotId.GetHashCode();
                        hash = hash * 31 + (building.BuildingId == null ? 0 : building.BuildingId.GetHashCode());
                        hash = hash * 31 + building.ConfirmedLevel;
                        hash = hash * 31 + (building.IsUpgrading ? 1 : 0);
                        hash = hash * 31 + (int)building.Status;
                        hash = hash * 31 + building.UpgradeCompleteTimestamp.GetHashCode();
                        hash = hash * 31 + building.DiagnosticCode.GetHashCode();
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
            CreateCommandSignalNetwork(realmId);
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

        private void CreateCommandSignalNetwork(RealmId realmId)
        {
            var network = GameObject.Find("Kingdom_CommandSignalNetwork") ?? new GameObject("Kingdom_CommandSignalNetwork");
            ClearChildren(network.transform);

            Color accent = GetRealmAccent(realmId);
            Color hotAccent = Color.Lerp(accent, Color.white, 0.30f);
            Color dimAccent = Color.Lerp(accent, Color.black, 0.36f);

            CreateCommandRingTicks(network.transform, accent, hotAccent);
            CreateSignalBeaconLine(network.transform, "NorthCommandSignal", Vector3.zero, new Vector3(0.35f, 0f, 6.45f), 7, dimAccent, hotAccent, 0.0f);
            CreateSignalBeaconLine(network.transform, "SouthCommandSignal", Vector3.zero, new Vector3(-0.35f, 0f, -6.45f), 7, dimAccent, hotAccent, 0.18f);
            CreateSignalBeaconLine(network.transform, "WestCommandSignal", Vector3.zero, new Vector3(-6.40f, 0f, -0.28f), 7, dimAccent, hotAccent, 0.36f);
            CreateSignalBeaconLine(network.transform, "EastCommandSignal", Vector3.zero, new Vector3(6.40f, 0f, 0.28f), 7, dimAccent, hotAccent, 0.54f);

            CreatePointLight(network.transform, "SignalNetworkLight", new Vector3(0f, 0.95f, 0f), hotAccent, 0.36f, 4.7f);
        }

        private static void CreateCommandRingTicks(Transform parent, Color accent, Color hotAccent)
        {
            const int tickCount = 14;
            for (int i = 0; i < tickCount; i++)
            {
                float angle = i * Mathf.PI * 2f / tickCount;
                float radius = i % 2 == 0 ? 1.34f : 1.56f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.132f, Mathf.Sin(angle) * radius);
                var tick = CreateTerritoryPrimitive(parent, "CommandRingTick_" + i, PrimitiveType.Cube, position, new Vector3(0.055f, 0.018f, i % 2 == 0 ? 0.30f : 0.20f), Color.Lerp(accent, Color.black, 0.24f), 0.02f, 0.50f, accent * 0.08f);
                tick.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                RemoveCollider(tick);
                tick.AddComponent<KingdomTacticalPulse>().Configure(Color.Lerp(accent, Color.black, 0.24f), hotAccent, 0.54f + i * 0.035f);
            }
        }

        private static void CreateSignalBeaconLine(Transform parent, string name, Vector3 start, Vector3 end, int count, Color baseColor, Color pulseColor, float phase)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            for (int i = 1; i <= count; i++)
            {
                float t = i / (count + 1f);
                Vector3 position = Vector3.Lerp(start, end, t);
                position.y = 0.125f;
                float scale = Mathf.Lerp(0.060f, 0.090f, Mathf.PingPong(t + phase, 1f));
                var beacon = CreateTerritoryPrimitive(root.transform, "SignalBeacon_" + i, PrimitiveType.Cylinder, position, new Vector3(scale, 0.018f, scale), Color.Lerp(baseColor, pulseColor, 0.16f + t * 0.16f), 0.02f, 0.55f, pulseColor * 0.10f);
                RemoveCollider(beacon);
                beacon.AddComponent<KingdomTacticalPulse>().Configure(Color.Lerp(baseColor, pulseColor, 0.10f), pulseColor, 0.82f + phase + i * 0.045f);
            }
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

                var controlRing = CreateTerritoryPrimitive(root.transform, "OutpostControlRing", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), territory.IsFortress ? new Vector3(0.66f, 0.03f, 0.66f) : new Vector3(0.52f, 0.025f, 0.52f), routeColor, 0.02f, 0.42f, territory.OwnerRealm == RealmId.None ? null : routeColor * 0.10f);
                if (territory.OwnerRealm != RealmId.None || territory.IsFortress)
                {
                    controlRing.AddComponent<KingdomTacticalPulse>().Configure(routeColor, Color.Lerp(routeColor, Color.white, 0.22f), territory.IsFortress ? 1.08f : 0.78f);
                }

                var baseObject = CreateTerritoryPrimitive(root.transform, "OutpostBase", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), territory.IsFortress ? new Vector3(0.46f, 0.12f, 0.46f) : new Vector3(0.34f, 0.08f, 0.34f), Color.Lerp(ownerColor, Color.black, 0.20f), 0.04f, 0.40f);
                var towerObject = CreateTerritoryPrimitive(root.transform, "OutpostTower", territory.IsFortress ? PrimitiveType.Cylinder : PrimitiveType.Cube, new Vector3(0f, territory.IsFortress ? 0.42f : 0.30f, 0f), territory.IsFortress ? new Vector3(0.22f, 0.46f, 0.22f) : new Vector3(0.30f, 0.32f, 0.30f), ownerColor, 0.05f, 0.52f, territory.OwnerRealm == RealmId.None ? null : ownerColor * 0.12f);
                baseObject.AddComponent<KingdomTerritorySelectable>().Configure(territory.Name, territory.OwnerRealm, territory.BonusType, territory.BonusAmount, territory.IsFortress, Color.Lerp(ownerColor, Color.black, 0.20f), ownerColor);
                towerObject.AddComponent<KingdomTerritorySelectable>().Configure(territory.Name, territory.OwnerRealm, territory.BonusType, territory.BonusAmount, territory.IsFortress, ownerColor, Color.Lerp(ownerColor, Color.white, 0.25f));
                CreateOutpostGarrisonMarkers(root.transform, ownerColor, routeColor, territory.IsFortress, territory.OwnerRealm == RealmId.None);
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

        private void CreateAmbientBoardLife(RealmId realmId)
        {
            var ambient = GameObject.Find("Kingdom_AmbientBoardLife") ?? new GameObject("Kingdom_AmbientBoardLife");
            ClearChildren(ambient.transform);

            Color accent = GetRealmAccent(realmId);
            Color supply = Color.Lerp(accent, new Color(1f, 0.82f, 0.46f), 0.32f);
            Color patrol = Color.Lerp(accent, new Color(0.52f, 0.72f, 1f), 0.28f);
            Color shadow = Color.Lerp(GetTerrainColor(realmId), Color.black, 0.46f);

            Vector3[] tradeLoop =
            {
                new Vector3(-6.45f, 0.18f, -0.30f),
                new Vector3(-2.10f, 0.18f, -0.12f),
                new Vector3(-0.40f, 0.18f, 0.36f),
                new Vector3(2.35f, 0.18f, 0.18f),
                new Vector3(6.45f, 0.18f, 0.32f),
                new Vector3(2.20f, 0.18f, -0.42f),
                new Vector3(-0.35f, 0.18f, -0.56f),
                new Vector3(-2.65f, 0.18f, -0.42f)
            };

            Vector3[] northSouth =
            {
                new Vector3(0f, 0.19f, -6.35f),
                new Vector3(-0.35f, 0.19f, -2.20f),
                new Vector3(0.18f, 0.19f, 0.52f),
                new Vector3(0.36f, 0.19f, 6.35f)
            };

            Vector3[] patrolLoop =
            {
                new Vector3(-4.95f, 0.20f, 4.25f),
                new Vector3(-1.60f, 0.20f, 3.25f),
                new Vector3(2.05f, 0.20f, 3.15f),
                new Vector3(5.10f, 0.20f, 1.45f),
                new Vector3(4.62f, 0.20f, -2.10f),
                new Vector3(1.18f, 0.20f, -3.45f),
                new Vector3(-3.10f, 0.20f, -3.16f),
                new Vector3(-5.26f, 0.20f, -0.86f)
            };

            for (int i = 0; i < 5; i++)
            {
                CreateAmbientToken(ambient.transform, "SupplyRunner_" + i, tradeLoop, 0.55f + i * 0.035f, i / 5f, supply, shadow, false);
            }

            for (int i = 0; i < 3; i++)
            {
                CreateAmbientToken(ambient.transform, "CommandCourier_" + i, northSouth, 0.48f + i * 0.04f, i / 3f, Color.Lerp(accent, Color.white, 0.26f), shadow, true);
            }

            for (int i = 0; i < 4; i++)
            {
                CreateAmbientToken(ambient.transform, "GatePatrol_" + i, patrolLoop, 0.38f + i * 0.025f, i / 4f, patrol, shadow, false);
            }
        }

        private void CreateBoardAtmosphere(RealmId realmId)
        {
            var atmosphere = GameObject.Find("Kingdom_BoardAtmosphere") ?? new GameObject("Kingdom_BoardAtmosphere");
            ClearChildren(atmosphere.transform);

            Color accent = GetRealmAccent(realmId);
            Color terrain = GetTerrainColor(realmId);
            Color fog = GetRealmFogColor(realmId);
            Color particleStart = GetRealmWeatherParticleStart(realmId);
            Color particleEnd = GetRealmWeatherParticleEnd(realmId);
            Color shadow = new Color(0.012f, 0.016f, 0.022f, realmId == RealmId.Crownlands ? 0.15f : 0.19f);
            Color mist = Color.Lerp(fog, accent, 0.18f);
            mist.a = realmId == RealmId.Umbral ? 0.20f : 0.16f;

            CreateAtmosphereBand(atmosphere.transform, "CloudShadow_WestFront", new Vector3(-6.8f, 0.162f, -3.7f), new Vector3(7.4f, 0.024f, 1.14f), shadow, new Vector3(0f, 24f, 0f), new Vector3(14.2f, 0f, 2.8f), 0.024f, 0.10f, 0.23f, 0.16f);
            CreateAtmosphereBand(atmosphere.transform, "CloudShadow_EastFront", new Vector3(5.9f, 0.166f, 2.9f), new Vector3(6.4f, 0.022f, 0.92f), shadow, new Vector3(0f, -18f, 0f), new Vector3(-12.6f, 0f, -2.2f), 0.019f, 0.42f, 0.08f, 0.19f);
            CreateAtmosphereBand(atmosphere.transform, "CloudShadow_CitadelSweep", new Vector3(-1.4f, 0.170f, 5.6f), new Vector3(8.8f, 0.020f, 0.72f), Color.Lerp(shadow, terrain, 0.28f), new Vector3(0f, 8f, 0f), new Vector3(5.8f, 0f, -11.4f), 0.015f, 0.70f, 0.06f, 0.15f);

            CreateAtmosphereBand(atmosphere.transform, "LowMist_NorthWall", new Vector3(-2.4f, 0.245f, 7.75f), new Vector3(7.8f, 0.032f, 0.40f), mist, new Vector3(0f, -9f, 0f), new Vector3(5.6f, 0f, -0.40f), 0.018f, 0.10f, 0.08f, mist.a);
            CreateAtmosphereBand(atmosphere.transform, "LowMist_SouthGate", new Vector3(3.1f, 0.250f, -7.55f), new Vector3(6.9f, 0.032f, 0.44f), Color.Lerp(mist, Color.white, 0.06f), new Vector3(0f, 12f, 0f), new Vector3(-6.1f, 0f, 0.44f), 0.016f, 0.34f, 0.07f, mist.a * 0.92f);
            CreateAtmosphereBand(atmosphere.transform, "RiverHaze_GoldTrace", new Vector3(2.15f, 0.205f, 0.64f), new Vector3(5.1f, 0.024f, 0.24f), Color.Lerp(accent, Color.white, 0.15f), new Vector3(0f, 27f, 0f), new Vector3(1.2f, 0f, -1.7f), 0.030f, 0.64f, 0.025f, 0.09f);

            ParticleSystem weatherParticles = CreateAtmosphereParticleSystem(
                atmosphere.transform,
                "RealmWeather_Motes",
                new Vector3(0f, 1.88f, 0f),
                particleStart,
                particleEnd,
                new Vector3(17.6f, 2.6f, 17.6f),
                Mathf.Lerp(54f, 96f, realmId == RealmId.Umbral ? 1f : 0.45f),
                realmId == RealmId.Stonehold ? 0.070f : 0.045f,
                realmId == RealmId.Umbral ? 0.80f : 0.54f,
                false);

            ParticleSystem groundMist = CreateAtmosphereParticleSystem(
                atmosphere.transform,
                "RealmWeather_GroundMist",
                new Vector3(0f, 0.38f, 0f),
                Color.Lerp(fog, terrain, 0.16f),
                Color.Lerp(fog, accent, 0.12f),
                new Vector3(18.2f, 0.45f, 18.2f),
                46f,
                0.82f,
                0.13f,
                true);

            var lightObject = new GameObject("Atmosphere_BreathingKey");
            lightObject.transform.SetParent(atmosphere.transform, false);
            lightObject.transform.localPosition = new Vector3(-2.6f, 3.4f, -1.8f);
            lightObject.transform.localRotation = Quaternion.Euler(58f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.Lerp(accent, Color.white, 0.16f);
            light.intensity = realmId == RealmId.Umbral ? 0.72f : 0.52f;
            light.range = 8.4f;

            atmosphere.AddComponent<KingdomBoardAtmosphere>().Configure(realmId, light, weatherParticles, groundMist, accent);
        }

        private static void CreateAmbientToken(Transform parent, string name, Vector3[] path, float speed, float offset, Color color, Color shadow, bool tallBanner)
        {
            var token = new GameObject(name);
            token.transform.SetParent(parent, false);

            CreateTerritoryPrimitive(token.transform, "TokenShadow", PrimitiveType.Cylinder, new Vector3(0f, -0.045f, 0f), new Vector3(0.16f, 0.010f, 0.16f), shadow, 0f, 0.18f);
            CreateTerritoryPrimitive(token.transform, "TokenBody", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.075f, tallBanner ? 0.18f : 0.13f, 0.075f), color, 0.03f, 0.48f, color * 0.08f);
            CreateTerritoryPrimitive(token.transform, "TokenHead", PrimitiveType.Sphere, new Vector3(0f, tallBanner ? 0.22f : 0.16f, 0f), new Vector3(0.070f, 0.052f, 0.070f), Color.Lerp(color, Color.white, 0.20f), 0.02f, 0.42f);
            CreateTerritoryPrimitive(token.transform, "TokenSignal", PrimitiveType.Cube, new Vector3(0.070f, tallBanner ? 0.28f : 0.21f, -0.014f), new Vector3(0.030f, tallBanner ? 0.22f : 0.15f, 0.020f), Color.Lerp(color, Color.white, 0.36f), 0.02f, 0.55f, color * 0.10f);

            token.AddComponent<KingdomAmbientPathWalker>().Configure(path, speed, offset, 0.032f, color);
        }

        private static void CreateAtmosphereBand(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, Vector3 localEulerAngles, Vector3 travel, float speed, float phase, float minAlpha, float maxAlpha)
        {
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = name;
            band.transform.SetParent(parent, false);
            band.transform.localPosition = localPosition;
            band.transform.localRotation = Quaternion.Euler(localEulerAngles);
            band.transform.localScale = localScale;

            var collider = band.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            color.a = maxAlpha;
            var renderer = band.GetComponent<Renderer>();
            ApplyTransparentMaterial(renderer, color, 0f, 0.18f, null);
            band.AddComponent<KingdomAtmosphereBand>().Configure(localPosition, travel, speed, phase, color, minAlpha, maxAlpha, renderer);
        }

        private static ParticleSystem CreateAtmosphereParticleSystem(Transform parent, string name, Vector3 localPosition, Color startColor, Color endColor, Vector3 shapeScale, float emissionRate, float particleSize, float driftSpeed, bool groundLayer)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = localPosition;
            var particles = particleObject.AddComponent<ParticleSystem>();

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = groundLayer
                ? new ParticleSystem.MinMaxCurve(5.5f, 9.0f)
                : new ParticleSystem.MinMaxCurve(3.8f, 7.2f);
            main.startSpeed = groundLayer
                ? new ParticleSystem.MinMaxCurve(0.04f, 0.16f)
                : new ParticleSystem.MinMaxCurve(0.18f, Mathf.Max(0.22f, driftSpeed));
            main.startSize = groundLayer
                ? new ParticleSystem.MinMaxCurve(particleSize * 0.75f, particleSize * 1.42f)
                : new ParticleSystem.MinMaxCurve(particleSize * 0.70f, particleSize * 1.36f);
            main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
            main.maxParticles = groundLayer ? 72 : 128;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(2f, emissionRate);

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = shapeScale;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = groundLayer
                ? new ParticleSystem.MinMaxCurve(-0.12f, 0.12f)
                : new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
            velocity.y = groundLayer
                ? new ParticleSystem.MinMaxCurve(-0.02f, 0.055f)
                : new ParticleSystem.MinMaxCurve(-0.18f, -0.04f);
            velocity.z = groundLayer
                ? new ParticleSystem.MinMaxCurve(-0.10f, 0.10f)
                : new ParticleSystem.MinMaxCurve(-driftSpeed * 0.45f, driftSpeed * 0.45f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = groundLayer ? 0.18f : 0.34f;
            noise.frequency = groundLayer ? 0.035f : 0.075f;
            noise.scrollSpeed = groundLayer ? 0.08f : 0.22f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                renderer.sortingOrder = groundLayer ? -2 : 1;
            }

            particles.Play();
            return particles;
        }

        private static void CreateOutpostGarrisonMarkers(Transform root, Color ownerColor, Color routeColor, bool isFortress, bool isNeutral)
        {
            int markerCount = isFortress ? 4 : 3;
            float radius = isFortress ? 0.62f : 0.48f;
            Color markerColor = isNeutral
                ? Color.Lerp(routeColor, new Color(0.72f, 0.78f, 0.84f), 0.35f)
                : Color.Lerp(ownerColor, Color.white, 0.18f);

            for (int i = 0; i < markerCount; i++)
            {
                float angle = i * Mathf.PI * 2f / markerCount + Mathf.PI * 0.16f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.19f, Mathf.Sin(angle) * radius);
                var marker = CreateTerritoryPrimitive(root, "OutpostGarrisonPip", PrimitiveType.Cylinder, position, new Vector3(0.052f, isFortress ? 0.14f : 0.10f, 0.052f), markerColor, 0.04f, 0.58f, isNeutral ? null : markerColor * 0.13f);
                if (!isNeutral)
                {
                    marker.AddComponent<KingdomTacticalPulse>().Configure(markerColor, Color.Lerp(markerColor, Color.white, 0.35f), 0.72f + i * 0.08f);
                }
            }

            Color chevronColor = isNeutral ? Color.Lerp(routeColor, Color.white, 0.12f) : Color.Lerp(ownerColor, Color.white, 0.24f);
            CreateSurfaceStrip(root, "OutpostChevronA", new Vector3(-0.15f, 0.075f, -0.58f), new Vector3(0.28f, 0.022f, 0.055f), chevronColor, new Vector3(0f, 22f, 0f));
            CreateSurfaceStrip(root, "OutpostChevronB", new Vector3(0.15f, 0.075f, -0.58f), new Vector3(0.28f, 0.022f, 0.055f), chevronColor, new Vector3(0f, -22f, 0f));
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

        private static void RemoveCollider(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
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

        private static void ApplyTransparentMaterial(Renderer renderer, Color color, float metallic = 0f, float smoothness = 0.35f, Color? emission = null)
        {
            ApplyMaterial(renderer, color, metallic, smoothness, emission);
            if (renderer == null)
            {
                return;
            }

            Material material = renderer.material;
            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
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
            string ownerLabel = owner == RealmId.None ? "NEUTRAL" : owner.ToString().ToUpperInvariant();
            string kind = isFortress ? "FORTRESS" : "OUTPOST";
            OnTerritorySelected?.Invoke($"{kind} LOCK: {territoryName} | {ownerLabel} CONTROL | Yield +{bonusAmount} {bonusType}. {GetTerritoryRecommendation(owner, isFortress)}");
        }

        private static string GetTerritoryRecommendation(RealmId owner, bool isFortress)
        {
            if (owner == RealmId.None)
            {
                return isFortress
                    ? "High-value neutral fortress; capture only after building troop depth."
                    : "Prime capture target when your army can absorb border losses.";
            }

            return isFortress
                ? "Fortress is a strategic anchor; keep troop production ahead of rivals."
                : "Owned route is contributing income; defend it before pushing deeper.";
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

        private static Color GetRealmFogColor(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.56f, 0.66f, 0.74f, 0.22f),
                RealmId.Eldergrove => new Color(0.20f, 0.44f, 0.29f, 0.18f),
                RealmId.Crownlands => new Color(0.29f, 0.33f, 0.43f, 0.16f),
                RealmId.Umbral => new Color(0.15f, 0.07f, 0.10f, 0.24f),
                _ => new Color(0.26f, 0.30f, 0.34f, 0.18f)
            };
        }

        private static Color GetRealmWeatherParticleStart(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.82f, 0.93f, 1f, 0.52f),
                RealmId.Eldergrove => new Color(0.46f, 0.98f, 0.66f, 0.38f),
                RealmId.Crownlands => new Color(0.56f, 0.64f, 0.86f, 0.34f),
                RealmId.Umbral => new Color(0.30f, 0.17f, 0.17f, 0.56f),
                _ => new Color(0.62f, 0.68f, 0.74f, 0.32f)
            };
        }

        private static Color GetRealmWeatherParticleEnd(RealmId realmId)
        {
            return realmId switch
            {
                RealmId.Stonehold => new Color(0.96f, 0.99f, 1f, 0.18f),
                RealmId.Eldergrove => new Color(1f, 0.90f, 0.42f, 0.18f),
                RealmId.Crownlands => new Color(0.94f, 0.78f, 0.40f, 0.12f),
                RealmId.Umbral => new Color(0.96f, 0.16f, 0.08f, 0.18f),
                _ => new Color(0.78f, 0.84f, 0.90f, 0.12f)
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

    public class KingdomAtmosphereBand : MonoBehaviour
    {
        private Vector3 _basePosition;
        private Vector3 _travel;
        private float _speed = 0.02f;
        private float _phase;
        private float _minAlpha;
        private float _maxAlpha;
        private Color _baseColor;
        private Renderer _renderer;

        public void Configure(Vector3 basePosition, Vector3 travel, float speed, float phase, Color baseColor, float minAlpha, float maxAlpha, Renderer renderer)
        {
            _basePosition = basePosition;
            _travel = travel;
            _speed = Mathf.Max(0.001f, speed);
            _phase = phase;
            _baseColor = baseColor;
            _minAlpha = Mathf.Clamp01(minAlpha);
            _maxAlpha = Mathf.Clamp01(maxAlpha);
            _renderer = renderer;
        }

        private void Update()
        {
            float sweep = Mathf.PingPong(Time.time * _speed + _phase, 1f);
            float fade = 0.5f + Mathf.Sin((Time.time * _speed * 5.2f + _phase) * Mathf.PI * 2f) * 0.5f;
            transform.localPosition = _basePosition + _travel * (sweep - 0.5f);

            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }

            if (_renderer == null)
            {
                return;
            }

            Color color = _baseColor;
            color.a = Mathf.Lerp(_minAlpha, _maxAlpha, fade);
            _renderer.material.color = color;
        }
    }

    public class KingdomBoardAtmosphere : MonoBehaviour
    {
        private RealmId _realmId;
        private Light _breathingLight;
        private ParticleSystem _weatherParticles;
        private ParticleSystem _groundMist;
        private Color _accentColor;
        private float _baseLightIntensity;

        public void Configure(RealmId realmId, Light breathingLight, ParticleSystem weatherParticles, ParticleSystem groundMist, Color accentColor)
        {
            _realmId = realmId;
            _breathingLight = breathingLight;
            _weatherParticles = weatherParticles;
            _groundMist = groundMist;
            _accentColor = accentColor;
            _baseLightIntensity = breathingLight != null ? breathingLight.intensity : 0f;
        }

        private void Update()
        {
            float time = Time.time;
            float breath = 0.5f + Mathf.Sin(time * 0.72f) * 0.5f;
            float front = 0.5f + Mathf.Sin(time * 0.31f + (int)_realmId) * 0.5f;

            if (_breathingLight != null)
            {
                _breathingLight.intensity = _baseLightIntensity * Mathf.Lerp(0.78f, 1.26f, breath);
                _breathingLight.color = Color.Lerp(_accentColor, Color.white, 0.10f + front * 0.10f);
            }

            SetEmissionMultiplier(_weatherParticles, Mathf.Lerp(0.84f, 1.24f, front));
            SetEmissionMultiplier(_groundMist, Mathf.Lerp(0.72f, 1.12f, breath));
        }

        private static void SetEmissionMultiplier(ParticleSystem particles, float multiplier)
        {
            if (particles == null)
            {
                return;
            }

            var emission = particles.emission;
            emission.rateOverTimeMultiplier = Mathf.Max(0.05f, multiplier);
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

    public class KingdomAmbientPathWalker : MonoBehaviour
    {
        private Vector3[] _path;
        private float _speed = 0.5f;
        private float _progress;
        private float _bobHeight = 0.03f;
        private float _pathLength;
        private Color _accentColor;
        private Renderer[] _renderers;

        public void Configure(Vector3[] path, float speed, float offset, float bobHeight, Color accentColor)
        {
            _path = path;
            _speed = Mathf.Max(0.05f, speed);
            _progress = Mathf.Repeat(offset, 1f);
            _bobHeight = Mathf.Max(0f, bobHeight);
            _accentColor = accentColor;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _pathLength = CalculatePathLength(path);
            UpdateTransform(true);
        }

        private void Update()
        {
            if (_path == null || _path.Length < 2)
            {
                return;
            }

            float length = Mathf.Max(0.1f, _pathLength);
            _progress = Mathf.Repeat(_progress + Time.deltaTime * _speed / length, 1f);
            UpdateTransform(false);
            UpdatePulse();
        }

        private void UpdateTransform(bool immediate)
        {
            Vector3 position = EvaluateLoop(_path, _progress, out Vector3 direction);
            position.y += Mathf.Sin(Time.time * 3.6f + _progress * Mathf.PI * 2f) * _bobHeight;
            transform.localPosition = position;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.localRotation = immediate ? rotation : Quaternion.Slerp(transform.localRotation, rotation, Time.deltaTime * 7.5f);
            }
        }

        private void UpdatePulse()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            float pulse = 0.55f + Mathf.Sin(Time.time * 4.1f + _progress * Mathf.PI * 2f) * 0.18f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null || renderer.gameObject.name.Contains("Shadow"))
                {
                    continue;
                }

                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", _accentColor * Mathf.Max(0f, pulse * 0.12f));
                }
            }
        }

        private static float CalculatePathLength(Vector3[] path)
        {
            if (path == null || path.Length < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 current = path[i];
                Vector3 next = path[(i + 1) % path.Length];
                length += Vector3.Distance(current, next);
            }

            return length;
        }

        private static Vector3 EvaluateLoop(Vector3[] path, float progress, out Vector3 direction)
        {
            direction = Vector3.forward;
            if (path == null || path.Length == 0)
            {
                return Vector3.zero;
            }

            if (path.Length == 1)
            {
                return path[0];
            }

            float totalLength = Mathf.Max(0.1f, CalculatePathLength(path));
            float distance = Mathf.Repeat(progress, 1f) * totalLength;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 start = path[i];
                Vector3 end = path[(i + 1) % path.Length];
                float segmentLength = Vector3.Distance(start, end);
                if (segmentLength <= 0.001f)
                {
                    continue;
                }

                if (distance <= segmentLength)
                {
                    float t = distance / segmentLength;
                    direction = end - start;
                    return Vector3.Lerp(start, end, t);
                }

                distance -= segmentLength;
            }

            direction = path[0] - path[path.Length - 1];
            return path[0];
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
        private Vector3 _baseScale;
        private float _highlightTimer;
        private bool _hovered;

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
            _baseScale = transform.localScale;
        }

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            _highlightTimer = 0.48f;
            SpawnSelectionPulse();
            KingdomVisualizer.RaiseTerritorySelected(_name, _owner, _bonusType, _bonusAmount, _isFortress);
        }

        private void OnMouseEnter()
        {
            _hovered = true;
        }

        private void OnMouseExit()
        {
            _hovered = false;
        }

        private void Update()
        {
            if (_highlightTimer <= 0f)
            {
                SetColor(_hovered ? Color.Lerp(_baseColor, _accentColor, 0.28f) : _baseColor);
                SetScale(_hovered ? 1.045f : 1f);
                return;
            }

            _highlightTimer -= Time.deltaTime;
            float pulse = Mathf.PingPong(Time.time * 7.5f, 1f);
            SetColor(Color.Lerp(_baseColor, _accentColor, 0.40f + pulse * 0.38f));
            SetScale(1.060f + pulse * 0.050f);
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

        private void SetScale(float multiplier)
        {
            if (_baseScale == Vector3.zero)
            {
                _baseScale = transform.localScale;
            }

            transform.localScale = _baseScale * multiplier;
        }

        private void SpawnSelectionPulse()
        {
            if (transform.parent == null)
            {
                return;
            }

            var pulse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pulse.name = "OutpostSelectionPulse";
            pulse.transform.SetParent(transform.parent, false);
            pulse.transform.localPosition = new Vector3(0f, 0.115f, 0f);
            float ringScale = _isFortress ? 0.92f : 0.72f;
            pulse.transform.localScale = new Vector3(ringScale, 0.016f, ringScale);
            var collider = pulse.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            pulse.AddComponent<AL.Kingdom.KingdomSelectionPulse>().Configure(_accentColor, 0.62f, _isFortress ? 1.34f : 1.42f);
            AL.Kingdom.KingdomSelectionBeacon.Spawn(transform.parent, _accentColor, _isFortress);
        }
    }
}
