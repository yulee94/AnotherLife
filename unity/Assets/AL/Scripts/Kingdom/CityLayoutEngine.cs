using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using AL.Core;

namespace AL.Kingdom
{
    public class CityLayoutEngine : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float TileSize = 1.0f;
        public Vector2Int GridSize = new Vector2Int(20, 20);

        public static event System.Action<string> OnBuildingSelected;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicProperty = Shader.PropertyToID("_Metallic");
        private static readonly int GlossinessProperty = Shader.PropertyToID("_Glossiness");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
        private static Material _sharedEmissiveMaterial;

        private readonly Dictionary<Vector2Int, KingdomBuildingPresentation> _occupiedTiles =
            new Dictionary<Vector2Int, KingdomBuildingPresentation>();
        private Transform _visualRoot;
        private RealmId _activeRealmId;

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            // Simple Isometric Conversion
            float x = (gridPos.x - gridPos.y) * TileSize * 0.5f;
            float z = (gridPos.x + gridPos.y) * TileSize * 0.25f;
            return new Vector3(x, 0, z);
        }

        public void AutoPlaceBuildings(
            RealmId realmId,
            IReadOnlyList<KingdomBuildingPresentation> buildings)
        {
            _activeRealmId = realmId;
            _occupiedTiles.Clear();
            ClearExistingBuildingVisuals();

            if (buildings == null)
            {
                return;
            }

            foreach (KingdomBuildingPresentation building in buildings)
            {
                if (building?.Slot == null || building.Slot.RealmId != realmId)
                {
                    continue;
                }

                Vector2Int pos = building.Slot.GridPosition;
                if (_occupiedTiles.ContainsKey(pos))
                {
                    Debug.LogError(
                        $"Duplicate kingdom slot coordinate {pos} in {KingdomBuildingLayoutCatalog.LayoutVersion}.");
                    continue;
                }

                _occupiedTiles.Add(pos, building);

                SpawnRoadVisual(pos);
                SpawnBuildingVisual(building, pos);
            }
        }

        private void SpawnBuildingVisual(
            KingdomBuildingPresentation presentation,
            Vector2Int pos)
        {
            if (presentation?.Slot == null)
            {
                return;
            }

            Transform root = EnsureVisualRoot();
            Vector3 worldPosition = GridToWorld(pos);
            int level = presentation.ConfirmedLevel;
            float height = Mathf.Clamp(0.72f + level * 0.08f, 0.72f, 1.85f);
            Color bodyColor = presentation.Status == KingdomBuildingPresentationStatus.InvalidState
                ? new Color(0.56f, 0.16f, 0.18f)
                : GetBuildingBodyColor(presentation.BuildingId);
            Color accentColor = GetRealmAccent(_activeRealmId);

            var buildingRoot = new GameObject($"Building_{presentation.Slot.SlotId}");
            buildingRoot.transform.SetParent(root, false);
            buildingRoot.transform.position = worldPosition;
            buildingRoot.transform.rotation = Quaternion.Euler(
                0f,
                presentation.Slot.RotationQuarterTurns * 90f,
                0f);

            CreateDistrictFootprint(buildingRoot.transform, presentation, bodyColor, accentColor);
            if (!presentation.IsBuilt)
            {
                CreateUnbuiltPlot(
                    buildingRoot.transform,
                    presentation,
                    bodyColor,
                    accentColor);
                CreateLevelBadge(
                    buildingRoot.transform,
                    presentation,
                    0.18f,
                    accentColor);
                return;
            }

            var baseObject = CreatePrimitive(buildingRoot.transform, "Base", PrimitiveType.Cube, new Vector3(0f, height * 0.5f, 0f), new Vector3(TileSize * 0.88f, height, TileSize * 0.88f), bodyColor);
            baseObject.AddComponent<KingdomBuildingSelectable>().Configure(
                presentation.BuildingId,
                level,
                bodyColor,
                accentColor,
                presentation.IsUpgrading,
                GetUpgradeRemainingSeconds(presentation),
                presentation.DiagnosticCode);
            CreatePrimitive(buildingRoot.transform, "Trim", PrimitiveType.Cube, new Vector3(0f, height + 0.04f, 0f), new Vector3(TileSize * 0.98f, 0.10f, TileSize * 0.98f), accentColor, null, 0.04f, 0.58f, accentColor * 0.06f);
            CreateWindowDetails(buildingRoot.transform, height, accentColor, presentation.BuildingId);
            CreateBuildingBanners(buildingRoot.transform, height, accentColor, presentation.BuildingId);

            if (presentation.BuildingId.Contains("Hall"))
            {
                CreateTownHallDetails(buildingRoot.transform, height, bodyColor, accentColor);
            }
            else if (presentation.BuildingId.Contains("Barracks"))
            {
                CreateBarracksDetails(buildingRoot.transform, height, bodyColor, accentColor);
            }
            else if (presentation.BuildingId.Contains("Farm"))
            {
                CreateFarmDetails(buildingRoot.transform, height, bodyColor, accentColor);
            }
            else if (presentation.BuildingId.Contains("Lumber"))
            {
                CreateLumberDetails(buildingRoot.transform, height, bodyColor, accentColor);
            }
            else if (presentation.BuildingId.Contains("Mana"))
            {
                CreateManaShrineDetails(buildingRoot.transform, height, bodyColor, accentColor);
            }
            else if (presentation.BuildingId.Contains("Gold"))
            {
                CreateGoldMineDetails(buildingRoot.transform, height, bodyColor, accentColor);
            }
            else if (presentation.BuildingId.Contains("Mine") ||
                     presentation.BuildingId.Contains("Quarry"))
            {
                CreateMineDetails(buildingRoot.transform, height, bodyColor, accentColor);
            }
            else
            {
                CreatePrimitive(buildingRoot.transform, "Roof", PrimitiveType.Cube, new Vector3(0f, height + 0.18f, 0f), new Vector3(TileSize * 0.68f, 0.22f, TileSize * 0.68f), Color.Lerp(bodyColor, accentColor, 0.35f), new Vector3(0f, 45f, 0f), 0.02f, 0.46f);
            }

            if (presentation.IsUpgrading)
            {
                CreateUpgradeIndicator(
                    buildingRoot.transform,
                    height,
                    accentColor,
                    GetUpgradeRemainingSeconds(presentation));
            }

            CreateLevelBadge(buildingRoot.transform, presentation, height, accentColor);
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
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, flatDirection);
            Color roadColor = new Color(0.10f, 0.083f, 0.060f, 1f);
            Color edgeColor = Color.Lerp(GetRealmAccent(_activeRealmId), Color.black, 0.40f);

            var road = CreatePrimitive(root, "CityRoad", PrimitiveType.Cube, midpoint, new Vector3(0.16f, 0.038f, length), roadColor, null, 0.02f, 0.42f);
            road.transform.rotation = Quaternion.LookRotation(flatDirection);

            var leftEdge = CreatePrimitive(root, "CityRoadEdge", PrimitiveType.Cube, midpoint + right * 0.105f + Vector3.up * 0.010f, new Vector3(0.030f, 0.028f, length), edgeColor, null, 0.03f, 0.52f, edgeColor * 0.04f);
            leftEdge.transform.rotation = road.transform.rotation;
            var rightEdge = CreatePrimitive(root, "CityRoadEdge", PrimitiveType.Cube, midpoint - right * 0.105f + Vector3.up * 0.010f, new Vector3(0.030f, 0.028f, length), edgeColor, null, 0.03f, 0.52f, edgeColor * 0.04f);
            rightEdge.transform.rotation = road.transform.rotation;
        }

        private void CreateDistrictFootprint(
            Transform parent,
            KingdomBuildingPresentation presentation,
            Color bodyColor,
            Color accentColor)
        {
            Color plateColor = Color.Lerp(bodyColor, Color.black, 0.34f);
            CreatePrimitive(parent, "DistrictPlate", PrimitiveType.Cube, new Vector3(0f, 0.035f, 0f), new Vector3(TileSize * 1.24f, 0.055f, TileSize * 1.24f), plateColor, new Vector3(0f, 45f, 0f), 0.02f, 0.40f);
            CreatePrimitive(parent, "DistrictInlay", PrimitiveType.Cube, new Vector3(0f, 0.070f, 0f), new Vector3(TileSize * 0.98f, 0.030f, TileSize * 0.98f), Color.Lerp(bodyColor, accentColor, 0.18f), new Vector3(0f, 45f, 0f), 0.02f, 0.48f, accentColor * 0.025f);

            if (presentation.IsUpgrading)
            {
                CreatePrimitive(parent, "UpgradeWorksiteGlow", PrimitiveType.Cylinder, new Vector3(0f, 0.095f, 0f), new Vector3(TileSize * 0.52f, 0.020f, TileSize * 0.52f), Color.Lerp(accentColor, new Color(1f, 0.82f, 0.32f), 0.50f), null, 0.02f, 0.60f, accentColor * 0.15f);
            }
        }

        private void CreateUnbuiltPlot(
            Transform parent,
            KingdomBuildingPresentation presentation,
            Color bodyColor,
            Color accentColor)
        {
            bool invalid =
                presentation.Status == KingdomBuildingPresentationStatus.InvalidState;
            Color markerColor = invalid
                ? new Color(0.92f, 0.24f, 0.22f)
                : Color.Lerp(bodyColor, accentColor, 0.32f);

            var siteMarker = CreatePrimitive(
                parent,
                invalid ? "InvalidSiteMarker" : "ReservedSiteMarker",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(TileSize * 0.24f, 0.08f, TileSize * 0.24f),
                markerColor,
                null,
                0.02f,
                0.48f,
                markerColor * (invalid ? 0.14f : 0.04f));
            siteMarker.AddComponent<KingdomBuildingSelectable>().Configure(
                presentation.BuildingId,
                presentation.ConfirmedLevel,
                markerColor,
                accentColor,
                presentation.IsUpgrading,
                GetUpgradeRemainingSeconds(presentation),
                presentation.DiagnosticCode);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                Vector3 position = new Vector3(
                    Mathf.Cos(angle) * TileSize * 0.42f,
                    0.13f,
                    Mathf.Sin(angle) * TileSize * 0.42f);
                CreatePrimitive(
                    parent,
                    "SiteBoundaryPost",
                    PrimitiveType.Cube,
                    position,
                    new Vector3(0.055f, 0.20f, 0.055f),
                    markerColor,
                    null,
                    0.02f,
                    0.38f);
            }

            if (presentation.IsUpgrading)
            {
                CreateUpgradeIndicator(
                    parent,
                    0.42f,
                    accentColor,
                    GetUpgradeRemainingSeconds(presentation));
            }
        }

        private void CreateWindowDetails(Transform parent, float height, Color accentColor, string buildingId)
        {
            Color glass = buildingId.Contains("Mana")
                ? new Color(0.34f, 0.56f, 0.94f)
                : Color.Lerp(accentColor, Color.white, 0.26f);
            int rows = Mathf.Clamp(Mathf.RoundToInt(height * 1.4f), 1, 3);

            for (int row = 0; row < rows; row++)
            {
                float y = 0.34f + row * 0.36f;
                CreatePrimitive(parent, "WindowNorth", PrimitiveType.Cube, new Vector3(-0.22f, y, -0.452f), new Vector3(0.13f, 0.10f, 0.018f), glass, null, 0f, 0.62f, glass * 0.08f);
                CreatePrimitive(parent, "WindowNorth", PrimitiveType.Cube, new Vector3(0.22f, y, -0.452f), new Vector3(0.13f, 0.10f, 0.018f), glass, null, 0f, 0.62f, glass * 0.08f);
                CreatePrimitive(parent, "WindowEast", PrimitiveType.Cube, new Vector3(0.452f, y, 0f), new Vector3(0.018f, 0.10f, 0.15f), glass, null, 0f, 0.62f, glass * 0.08f);
            }
        }

        private void CreateBuildingBanners(Transform parent, float height, Color accentColor, string buildingId)
        {
            if (buildingId.Contains("Farm") || buildingId.Contains("Lumber"))
            {
                return;
            }

            Color banner = Color.Lerp(accentColor, Color.black, 0.08f);
            CreatePrimitive(parent, "BannerLeft", PrimitiveType.Cube, new Vector3(-0.50f, height * 0.58f, -0.12f), new Vector3(0.035f, 0.36f, 0.12f), banner, null, 0.01f, 0.40f, banner * 0.05f);
            CreatePrimitive(parent, "BannerRight", PrimitiveType.Cube, new Vector3(0.50f, height * 0.58f, -0.12f), new Vector3(0.035f, 0.36f, 0.12f), banner, null, 0.01f, 0.40f, banner * 0.05f);
        }

        private void CreateTownHallDetails(Transform parent, float height, Color bodyColor, Color accentColor)
        {
            CreatePrimitive(parent, "UpperKeep", PrimitiveType.Cube, new Vector3(0f, height + 0.24f, 0f), new Vector3(0.58f, 0.36f, 0.58f), Color.Lerp(bodyColor, accentColor, 0.20f), null, 0.04f, 0.50f);
            CreatePrimitive(parent, "CommandCrown", PrimitiveType.Cube, new Vector3(0f, height + 0.52f, 0f), new Vector3(0.72f, 0.12f, 0.72f), accentColor, new Vector3(0f, 45f, 0f), 0.06f, 0.62f, accentColor * 0.08f);
            CreatePrimitive(parent, "Spire", PrimitiveType.Cylinder, new Vector3(0f, height + 0.90f, 0f), new Vector3(0.16f, 0.42f, 0.16f), Color.Lerp(accentColor, Color.white, 0.12f), null, 0.08f, 0.70f, accentColor * 0.18f);
            CreatePointLight(parent, "HallCommandLight", new Vector3(0f, height + 1.20f, -0.12f), Color.Lerp(accentColor, Color.white, 0.14f), 0.38f, 1.80f);
        }

        private void CreateBarracksDetails(Transform parent, float height, Color bodyColor, Color accentColor)
        {
            CreatePrimitive(parent, "ArmoryRoof", PrimitiveType.Cube, new Vector3(0f, height + 0.20f, 0f), new Vector3(0.84f, 0.20f, 0.54f), Color.Lerp(bodyColor, accentColor, 0.24f), new Vector3(0f, 45f, 0f), 0.03f, 0.48f);
            CreatePrimitive(parent, "TrainingYard", PrimitiveType.Cube, new Vector3(0.64f, 0.105f, 0.28f), new Vector3(0.56f, 0.045f, 0.42f), Color.Lerp(bodyColor, Color.black, 0.24f), null, 0.02f, 0.32f);
            CreatePrimitive(parent, "WeaponRack", PrimitiveType.Cube, new Vector3(0.64f, 0.38f, 0.28f), new Vector3(0.08f, 0.48f, 0.08f), Color.Lerp(accentColor, Color.white, 0.18f), new Vector3(0f, 0f, 18f), 0.04f, 0.48f, accentColor * 0.05f);
            CreatePrimitive(parent, "WeaponRackCross", PrimitiveType.Cube, new Vector3(0.64f, 0.52f, 0.28f), new Vector3(0.42f, 0.05f, 0.05f), Color.Lerp(accentColor, Color.white, 0.18f), new Vector3(0f, 0f, 18f), 0.04f, 0.48f);
        }

        private void CreateFarmDetails(Transform parent, float height, Color bodyColor, Color accentColor)
        {
            Color field = new Color(0.34f, 0.48f, 0.18f);
            for (int i = 0; i < 3; i++)
            {
                CreatePrimitive(parent, "FieldRow", PrimitiveType.Cube, new Vector3(-0.66f + i * 0.16f, 0.10f, 0.46f), new Vector3(0.08f, 0.050f, 0.62f), Color.Lerp(field, accentColor, i * 0.06f), new Vector3(0f, 0f, 0f), 0.01f, 0.32f);
            }

            CreatePrimitive(parent, "Granary", PrimitiveType.Cylinder, new Vector3(0.52f, 0.40f, 0.34f), new Vector3(0.18f, 0.34f, 0.18f), Color.Lerp(bodyColor, Color.white, 0.10f), null, 0.02f, 0.38f);
            CreatePrimitive(parent, "GranaryCap", PrimitiveType.Cube, new Vector3(0.52f, 0.78f, 0.34f), new Vector3(0.34f, 0.10f, 0.34f), Color.Lerp(bodyColor, accentColor, 0.22f), new Vector3(0f, 45f, 0f), 0.02f, 0.40f);
            CreatePrimitive(parent, "Canopy", PrimitiveType.Sphere, new Vector3(0.06f, height + 0.22f, 0.02f), new Vector3(0.42f, 0.23f, 0.42f), Color.Lerp(bodyColor, accentColor, 0.22f), null, 0.01f, 0.36f);
        }

        private void CreateLumberDetails(Transform parent, float height, Color bodyColor, Color accentColor)
        {
            CreatePrimitive(parent, "MillRoof", PrimitiveType.Cube, new Vector3(0f, height + 0.18f, 0f), new Vector3(0.74f, 0.22f, 0.74f), Color.Lerp(bodyColor, accentColor, 0.28f), new Vector3(0f, 45f, 0f), 0.02f, 0.42f);
            for (int i = 0; i < 3; i++)
            {
                CreatePrimitive(parent, "LogStack", PrimitiveType.Cylinder, new Vector3(-0.62f + i * 0.16f, 0.20f, 0.42f), new Vector3(0.08f, 0.30f, 0.08f), new Color(0.34f, 0.22f, 0.12f), new Vector3(90f, 0f, 0f), 0.01f, 0.38f);
            }

            CreatePrimitive(parent, "SawFrame", PrimitiveType.Cube, new Vector3(0.54f, 0.46f, -0.26f), new Vector3(0.08f, 0.50f, 0.08f), Color.Lerp(accentColor, Color.white, 0.08f), null, 0.03f, 0.45f);
            CreatePrimitive(parent, "SawBlade", PrimitiveType.Cylinder, new Vector3(0.54f, 0.58f, -0.26f), new Vector3(0.20f, 0.035f, 0.20f), Color.Lerp(Color.gray, Color.white, 0.22f), new Vector3(90f, 0f, 0f), 0.16f, 0.70f);
        }

        private void CreateManaShrineDetails(Transform parent, float height, Color bodyColor, Color accentColor)
        {
            Color crystal = new Color(0.36f, 0.68f, 1f);
            CreatePrimitive(parent, "ShrineCrown", PrimitiveType.Cylinder, new Vector3(0f, height + 0.18f, 0f), new Vector3(0.42f, 0.12f, 0.42f), Color.Lerp(bodyColor, accentColor, 0.36f), null, 0.04f, 0.60f, accentColor * 0.08f);
            CreatePrimitive(parent, "ManaCrystal", PrimitiveType.Cube, new Vector3(0f, height + 0.58f, 0f), new Vector3(0.24f, 0.52f, 0.24f), crystal, new Vector3(0f, 45f, 0f), 0.02f, 0.84f, crystal * 0.35f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                CreatePrimitive(parent, "ManaPylon", PrimitiveType.Cylinder, new Vector3(Mathf.Cos(angle) * 0.52f, height + 0.30f, Mathf.Sin(angle) * 0.52f), new Vector3(0.06f, 0.28f, 0.06f), Color.Lerp(accentColor, crystal, 0.34f), null, 0.04f, 0.64f, crystal * 0.12f);
            }

            CreatePointLight(parent, "ManaShrineLight", new Vector3(0f, height + 0.92f, -0.10f), crystal, 0.58f, 2.10f);
        }

        private void CreateGoldMineDetails(Transform parent, float height, Color bodyColor, Color accentColor)
        {
            Color gold = new Color(1f, 0.68f, 0.20f);
            CreatePrimitive(parent, "VaultRoof", PrimitiveType.Cube, new Vector3(0f, height + 0.18f, 0f), new Vector3(0.78f, 0.20f, 0.78f), Color.Lerp(bodyColor, gold, 0.28f), new Vector3(0f, 45f, 0f), 0.08f, 0.66f, gold * 0.08f);
            CreatePrimitive(parent, "VaultDoor", PrimitiveType.Cube, new Vector3(0f, 0.46f, -0.465f), new Vector3(0.32f, 0.42f, 0.026f), Color.Lerp(gold, Color.black, 0.10f), null, 0.12f, 0.70f, gold * 0.08f);
            CreatePrimitive(parent, "GoldOreLeft", PrimitiveType.Sphere, new Vector3(-0.48f, 0.20f, 0.42f), new Vector3(0.16f, 0.11f, 0.16f), gold, null, 0.08f, 0.62f, gold * 0.12f);
            CreatePrimitive(parent, "GoldOreRight", PrimitiveType.Sphere, new Vector3(0.48f, 0.20f, 0.42f), new Vector3(0.16f, 0.11f, 0.16f), gold, null, 0.08f, 0.62f, gold * 0.12f);
            CreatePointLight(parent, "GoldMineLight", new Vector3(0f, height + 0.65f, -0.16f), gold, 0.30f, 1.45f);
        }

        private void CreateMineDetails(Transform parent, float height, Color bodyColor, Color accentColor)
        {
            CreatePrimitive(parent, "CranePost", PrimitiveType.Cube, new Vector3(-0.28f, height + 0.18f, 0f), new Vector3(0.08f, 0.58f, 0.08f), Color.Lerp(bodyColor, Color.white, 0.10f), null, 0.03f, 0.44f);
            CreatePrimitive(parent, "CraneArm", PrimitiveType.Cube, new Vector3(0.12f, height + 0.48f, 0f), new Vector3(0.74f, 0.08f, 0.08f), Color.Lerp(bodyColor, Color.white, 0.18f), new Vector3(0f, 0f, 10f), 0.04f, 0.52f);
            CreatePrimitive(parent, "OrePile", PrimitiveType.Sphere, new Vector3(0.48f, 0.20f, 0.44f), new Vector3(0.22f, 0.13f, 0.20f), Color.Lerp(bodyColor, accentColor, 0.22f), null, 0.03f, 0.48f, accentColor * 0.05f);
            CreatePrimitive(parent, "MineShaft", PrimitiveType.Cube, new Vector3(-0.50f, 0.32f, -0.34f), new Vector3(0.30f, 0.36f, 0.12f), Color.Lerp(bodyColor, Color.black, 0.30f), null, 0.02f, 0.36f);
        }

        private void CreateLevelBadge(
            Transform parent,
            KingdomBuildingPresentation presentation,
            float height,
            Color accentColor)
        {
            int remainingSeconds = GetUpgradeRemainingSeconds(presentation);
            bool invalid =
                presentation.Status == KingdomBuildingPresentationStatus.InvalidState;
            Color badgeAccent = invalid ? new Color(0.92f, 0.24f, 0.22f) : accentColor;
            Color plateColor = Color.Lerp(badgeAccent, Color.black, 0.26f);
            CreatePrimitive(parent, "LevelPlate", PrimitiveType.Cube, new Vector3(0f, height + 0.78f, -0.205f), new Vector3(0.96f, 0.38f, 0.035f), plateColor, new Vector3(55f, 0f, 0f), 0.03f, 0.56f, accentColor * 0.08f);
            CreatePrimitive(parent, "LevelPlatePin", PrimitiveType.Cube, new Vector3(0f, height + 0.54f, -0.15f), new Vector3(0.08f, 0.20f, 0.030f), Color.Lerp(plateColor, Color.white, 0.16f), new Vector3(55f, 0f, 0f), 0.04f, 0.54f);
            var labelObject = new GameObject("LevelLabel");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, height + 0.78f, -0.18f);
            labelObject.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = invalid
                ? $"{GetShortBuildingName(presentation.BuildingId)}\nDATA!"
                : presentation.IsUpgrading
                    ? $"{GetShortBuildingName(presentation.BuildingId)}\nUP {remainingSeconds}s"
                    : presentation.ConfirmedLevel == 0
                        ? $"{GetShortBuildingName(presentation.BuildingId)}\nUNBUILT"
                        : $"{GetShortBuildingName(presentation.BuildingId)}\nLv {presentation.ConfirmedLevel}";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.055f;
            label.color = Color.Lerp(badgeAccent, Color.white, 0.50f);
        }

        private void CreateUpgradeIndicator(Transform parent, float height, Color accentColor, int remainingSeconds)
        {
            Color progressColor = Color.Lerp(accentColor, new Color(1f, 0.84f, 0.32f), 0.45f);
            CreatePrimitive(parent, "UpgradeBaseRing", PrimitiveType.Cylinder, new Vector3(0f, 0.055f, 0f), new Vector3(0.72f, 0.035f, 0.72f), progressColor, null, 0.03f, 0.66f, progressColor * 0.18f);
            CreatePrimitive(parent, "UpgradeBeam", PrimitiveType.Cylinder, new Vector3(0f, height + 0.34f, 0f), new Vector3(0.08f, 0.36f, 0.08f), progressColor, null, 0.04f, 0.74f, progressColor * 0.26f);
            CreatePointLight(parent, "UpgradeWorkLight", new Vector3(0f, height + 0.76f, -0.12f), progressColor, 0.45f, 1.75f);

            int tickCount = Mathf.Clamp(remainingSeconds <= 0 ? 4 : 4 + remainingSeconds / 5, 4, 8);
            for (int i = 0; i < tickCount; i++)
            {
                float angle = i * Mathf.PI * 2f / tickCount;
                Vector3 local = new Vector3(Mathf.Cos(angle) * 0.56f, 0.14f, Mathf.Sin(angle) * 0.56f);
                CreatePrimitive(parent, "UpgradeTick", PrimitiveType.Cube, local, new Vector3(0.08f, 0.08f, 0.20f), progressColor, new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), 0.03f, 0.66f, progressColor * 0.12f);
            }
        }

        private GameObject CreatePrimitive(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Color color, Vector3? localEulerAngles = null, float metallic = 0f, float smoothness = 0.35f, Color? emission = null)
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
                ApplyMaterial(renderer, color, metallic, smoothness, emission);
            }

            return obj;
        }

        private void CreatePointLight(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range)
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

            Material sharedMaterial = renderer.sharedMaterial;
            if (emission.HasValue)
            {
                sharedMaterial = GetSharedEmissiveMaterial(sharedMaterial);
                renderer.sharedMaterial = sharedMaterial;
            }

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            if (sharedMaterial != null && sharedMaterial.HasProperty(ColorProperty))
            {
                properties.SetColor(ColorProperty, color);
            }

            if (sharedMaterial != null && sharedMaterial.HasProperty(BaseColorProperty))
            {
                properties.SetColor(BaseColorProperty, color);
            }

            if (sharedMaterial != null && sharedMaterial.HasProperty(MetallicProperty))
            {
                properties.SetFloat(MetallicProperty, metallic);
            }

            if (sharedMaterial != null && sharedMaterial.HasProperty(GlossinessProperty))
            {
                properties.SetFloat(GlossinessProperty, smoothness);
            }

            if (emission.HasValue &&
                sharedMaterial != null &&
                sharedMaterial.HasProperty(EmissionColorProperty))
            {
                properties.SetColor(EmissionColorProperty, emission.Value);
            }

            renderer.SetPropertyBlock(properties);
        }

        private static Material GetSharedEmissiveMaterial(Material fallback)
        {
            if (_sharedEmissiveMaterial != null)
            {
                return _sharedEmissiveMaterial;
            }

            Shader shader = Shader.Find("Standard");
            _sharedEmissiveMaterial = shader != null
                ? new Material(shader)
                : fallback != null
                    ? new Material(fallback)
                    : null;
            if (_sharedEmissiveMaterial != null)
            {
                _sharedEmissiveMaterial.name = "Kingdom Shared Emissive";
                _sharedEmissiveMaterial.hideFlags = HideFlags.HideAndDontSave;
                _sharedEmissiveMaterial.EnableKeyword("_EMISSION");
            }

            return _sharedEmissiveMaterial;
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
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                return "District";
            }

            return buildingId
                .Replace("TownHall", "Hall")
                .Replace("LumberMill", "Lumber")
                .Replace("GoldMine", "Gold")
                .Replace("ManaShrine", "Mana");
        }

        internal static void RaiseBuildingSelected(
            string buildingId,
            int level,
            bool isUpgrading,
            int remainingSeconds,
            string diagnosticCode)
        {
            bool invalid = !string.IsNullOrWhiteSpace(diagnosticCode);
            string status = invalid
                ? "DATA UNAVAILABLE"
                : isUpgrading
                    ? $"UPGRADING / {remainingSeconds}s"
                    : level == 0
                        ? "UNBUILT"
                        : "READY";
            string district = GetShortBuildingName(buildingId).ToUpperInvariant();
            string recommendation = invalid
                ? $"State rejected ({diagnosticCode}); gameplay data was not changed."
                : level == 0
                    ? "Reserved plot. Construction requires an approved gameplay order."
                    : GetBuildingRecommendation(buildingId, isUpgrading);
            OnBuildingSelected?.Invoke(
                $"DISTRICT LOCK: {district} Lv {level} | {GetBuildingRole(buildingId)} | {status}. {recommendation}");
        }

        private static string GetBuildingRole(string buildingId)
        {
            if (buildingId == null)
            {
                buildingId = string.Empty;
            }

            if (buildingId.Contains("Hall"))
            {
                return "Realm authority";
            }

            if (buildingId.Contains("Barracks"))
            {
                return "Force projection";
            }

            if (buildingId.Contains("Farm"))
            {
                return "Food security";
            }

            if (buildingId.Contains("Lumber"))
            {
                return "Construction supply";
            }

            if (buildingId.Contains("Mana"))
            {
                return "Arcane reserves";
            }

            if (buildingId.Contains("Gold"))
            {
                return "Treasury pressure";
            }

            if (buildingId.Contains("Mine") || buildingId.Contains("Quarry"))
            {
                return "Material pipeline";
            }

            return "Civil district";
        }

        private static string GetBuildingRecommendation(string buildingId, bool isUpgrading)
        {
            if (buildingId == null)
            {
                buildingId = string.Empty;
            }

            if (isUpgrading)
            {
                return "Hold resources until the timer clears, then chain the next upgrade or pivot into research.";
            }

            if (buildingId.Contains("Barracks"))
            {
                return "Train infantry or ranged troops before running the battle sim.";
            }

            if (buildingId.Contains("Hall"))
            {
                return "Upgrade here when the rest of the city is stable; it defines your growth ceiling.";
            }

            if (buildingId.Contains("Mana"))
            {
                return "Bank mana before skill-heavy champion progression.";
            }

            if (buildingId.Contains("Gold"))
            {
                return "Use treasury growth to sustain Warmaster purchases.";
            }

            return "Upgrade when the command deck shows enough spare resources.";
        }

        private static int GetUpgradeRemainingSeconds(
            KingdomBuildingPresentation presentation)
        {
            if (presentation == null || !presentation.IsUpgrading)
            {
                return 0;
            }

            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long remaining = presentation.UpgradeCompleteTimestamp - now;
            return remaining <= 0
                ? 0
                : remaining >= int.MaxValue
                    ? int.MaxValue
                    : (int)remaining;
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

    public class KingdomBuildingSelectable : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        private string _buildingId;
        private int _level;
        private bool _isUpgrading;
        private int _remainingSeconds;
        private string _diagnosticCode;
        private Color _baseColor;
        private Color _accentColor;
        private Renderer _renderer;
        private Vector3 _baseScale;
        private float _highlightTimer;
        private bool _hovered;

        public void Configure(
            string buildingId,
            int level,
            Color baseColor,
            Color accentColor,
            bool isUpgrading,
            int remainingSeconds,
            string diagnosticCode)
        {
            _buildingId = buildingId;
            _level = level;
            _isUpgrading = isUpgrading;
            _remainingSeconds = remainingSeconds;
            _diagnosticCode = diagnosticCode ?? string.Empty;
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

            _highlightTimer = 0.42f;
            SpawnSelectionPulse();
            CityLayoutEngine.RaiseBuildingSelected(
                _buildingId,
                _level,
                _isUpgrading,
                _remainingSeconds,
                _diagnosticCode);
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
                if (_isUpgrading)
                {
                    float upgradePulse = Mathf.PingPong(Time.time * 2.8f, 1f);
                    SetColor(Color.Lerp(_baseColor, _accentColor, 0.18f + upgradePulse * 0.22f));
                }
                else if (_hovered)
                {
                    SetColor(Color.Lerp(_baseColor, _accentColor, 0.24f));
                }
                else
                {
                    SetColor(_baseColor);
                }

                SetScale(_hovered ? 1.035f : 1f);
                return;
            }

            _highlightTimer -= Time.deltaTime;
            float pulse = Mathf.PingPong(Time.time * 7f, 1f);
            SetColor(Color.Lerp(_baseColor, _accentColor, 0.45f + pulse * 0.35f));
            SetScale(1.055f + pulse * 0.045f);
        }

        private void SetColor(Color color)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }

            if (_renderer != null)
            {
                Material sharedMaterial = _renderer.sharedMaterial;
                var properties = new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(properties);
                if (sharedMaterial != null && sharedMaterial.HasProperty(ColorProperty))
                {
                    properties.SetColor(ColorProperty, color);
                }

                if (sharedMaterial != null && sharedMaterial.HasProperty(BaseColorProperty))
                {
                    properties.SetColor(BaseColorProperty, color);
                }

                _renderer.SetPropertyBlock(properties);
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
            pulse.name = "DistrictSelectionPulse";
            pulse.transform.SetParent(transform.parent, false);
            pulse.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            pulse.transform.localScale = new Vector3(1.18f, 0.018f, 1.18f);
            var collider = pulse.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            pulse.AddComponent<KingdomSelectionPulse>().Configure(_accentColor, 0.58f, 1.42f);
            KingdomSelectionBeacon.Spawn(transform.parent, _accentColor, false);
        }
    }

    public class KingdomSelectionPulse : MonoBehaviour
    {
        private Renderer _renderer;
        private Color _color;
        private float _age;
        private float _duration = 0.55f;
        private float _targetScale = 1.35f;
        private Vector3 _startScale;

        public void Configure(Color color, float duration, float targetScale)
        {
            _color = color;
            _duration = Mathf.Max(0.1f, duration);
            _targetScale = Mathf.Max(1f, targetScale);
            _startScale = transform.localScale;
            _renderer = GetComponent<Renderer>();

            if (_renderer != null)
            {
                var shader = Shader.Find("Standard");
                var material = shader != null ? new Material(shader) : new Material(_renderer.material);
                material.color = Color.Lerp(color, Color.white, 0.16f);
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.45f);
                }

                _renderer.material = material;
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _duration);
            transform.localScale = Vector3.Lerp(_startScale, _startScale * _targetScale, t);

            if (_renderer != null)
            {
                _renderer.material.color = Color.Lerp(Color.Lerp(_color, Color.white, 0.20f), _color, t);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    public class KingdomSelectionBeacon : MonoBehaviour
    {
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Transform> _ticks = new List<Transform>();
        private readonly List<float> _tickAngles = new List<float>();
        private Color _color;
        private Transform _beam;
        private Transform _cap;
        private Vector3 _capStartScale;
        private float _age;
        private float _duration = 0.72f;
        private float _radius = 0.64f;
        private float _beamStartHeight = 0.58f;

        public static void Spawn(Transform parent, Color color, bool large)
        {
            if (parent == null)
            {
                return;
            }

            var beacon = new GameObject(large ? "FortressSelectionBeacon" : "DistrictSelectionBeacon");
            beacon.transform.SetParent(parent, false);
            beacon.transform.localPosition = Vector3.zero;
            beacon.AddComponent<KingdomSelectionBeacon>().Configure(color, large);
        }

        public void Configure(Color color, bool large)
        {
            _color = color;
            _duration = large ? 0.84f : 0.72f;
            _radius = large ? 0.86f : 0.66f;
            _beamStartHeight = large ? 0.72f : 0.58f;
            _capStartScale = large ? new Vector3(0.18f, 0.11f, 0.18f) : new Vector3(0.15f, 0.10f, 0.15f);
            CreateVisuals(large);
        }

        private void CreateVisuals(bool large)
        {
            _beam = CreateBeaconPrimitive("SelectionBeam", PrimitiveType.Cylinder, new Vector3(0f, large ? 0.78f : 0.66f, 0f), new Vector3(0.055f, _beamStartHeight, 0.055f), Color.Lerp(_color, Color.white, 0.18f), _color * 0.28f).transform;
            _cap = CreateBeaconPrimitive("SelectionCap", PrimitiveType.Sphere, new Vector3(0f, large ? 1.52f : 1.24f, 0f), _capStartScale, Color.Lerp(_color, Color.white, 0.34f), _color * 0.42f).transform;

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * _radius, 0.17f, Mathf.Sin(angle) * _radius);
                var tick = CreateBeaconPrimitive("SelectionTick_" + i, PrimitiveType.Cube, position, new Vector3(0.26f, 0.035f, 0.070f), Color.Lerp(_color, Color.white, 0.24f), _color * 0.20f).transform;
                tick.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                _ticks.Add(tick);
                _tickAngles.Add(angle);
            }
        }

        private GameObject CreateBeaconPrimitive(string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Color color, Color emission)
        {
            var obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard");
                var material = shader != null ? new Material(shader) : new Material(renderer.material);
                material.color = color;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission);
                }

                renderer.material = material;
                _renderers.Add(renderer);
                _materials.Add(material);
            }

            return obj;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _duration);
            float pulse = 0.82f + Mathf.Sin(Time.time * 14f) * 0.12f;

            if (_beam != null)
            {
                _beam.localScale = new Vector3(0.055f * pulse, Mathf.Lerp(_beamStartHeight, 0.22f, t), 0.055f * pulse);
            }

            if (_cap != null)
            {
                _cap.localPosition += Vector3.up * (Time.deltaTime * 0.22f);
                _cap.localScale = _capStartScale * Mathf.Lerp(1f, 0.55f, t);
            }

            for (int i = 0; i < _ticks.Count; i++)
            {
                float angle = _tickAngles[i];
                float radius = _radius + t * 0.28f;
                _ticks[i].localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.17f + t * 0.035f, Mathf.Sin(angle) * radius);
                _ticks[i].localScale = new Vector3(Mathf.Lerp(0.26f, 0.42f, t), 0.035f, 0.070f);
            }

            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Color color = Color.Lerp(Color.Lerp(_color, Color.white, 0.32f), _color, t);
                renderer.material.color = color;
                if (renderer.material.HasProperty("_EmissionColor"))
                {
                    renderer.material.SetColor("_EmissionColor", _color * Mathf.Lerp(0.36f, 0.04f, t));
                }
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            foreach (var material in _materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }
    }
}
