using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Kingdom.Visuals.Architecture;
using UnityEngine;

namespace AL.Kingdom.Visuals
{
    /// <summary>
    /// Product-facing private-kingdom presentation. The city is composed from
    /// the shipped realm Town Hall and Workshop model families; save data owns
    /// only the central Town Hall build state while the surrounding structures
    /// are non-interactive architectural set dressing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrivateKingdomCityPresenter : MonoBehaviour
    {
        public const string RootName = "PrivateKingdom_City";
        public const string TownHallRootName = "PrivateKingdom_TownHall";
        public const string ArchitectureRootName = "PrivateKingdom_Architecture";
        public const string GroundRootName = "PrivateKingdom_Ground";
        public const int EligibleDistrictCount = 4;
        public const int MinimumDuplicatesPerEligibleBuilding = 2;
        public const int MaximumDuplicatesPerEligibleBuilding = 3;
        public const int SetDressingBuildingCount = EligibleDistrictCount * MaximumDuplicatesPerEligibleBuilding;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        private static readonly Vector3[] CrownlandsDistrictCenters =
        {
            new(-4.8f, 0.05f, 0f), new(0f, 0.05f, 4.8f),
            new(4.8f, 0.05f, 0f), new(0f, 0.05f, -4.8f)
        };

        private static readonly Vector3[] StoneholdDistrictCenters =
        {
            new(-4.8f, 0.35f, 0f), new(0f, 0.85f, 4.8f),
            new(4.8f, 0.35f, 0f), new(0f, 0.55f, -4.8f)
        };

        private static readonly Vector3[] EldergroveDistrictCenters =
        {
            new(-4.8f, 0.10f, 0f), new(0f, 0.18f, 4.8f),
            new(4.8f, 0.10f, 0f), new(0f, 0.12f, -4.8f)
        };

        private static readonly Vector3[] UmbralDistrictCenters =
        {
            new(-4.8f, 0.28f, 0f), new(0f, 0.52f, 4.8f),
            new(4.8f, 0.28f, 0f), new(0f, 0.40f, -4.8f)
        };

        private readonly List<Material> _ownedMaterials = new();
        private IRealmService _realmService;
        private IBuildingService _buildingService;
        private KingdomBuildingModelCatalog _modelCatalog;
        private Transform _cityRoot;
        private int _lastPresentationHash;
        private bool _hasPresentationHash;
        private bool _ownsAmbientSettings;
        private UnityEngine.Rendering.AmbientMode _previousAmbientMode;
        private Color _previousAmbientLight;

        public RealmId PresentedRealm { get; private set; }
        public bool TownHallConstructed { get; private set; }
        public int ArchitectureInstanceCount { get; private set; }
        public int DuplicateCountPerEligibleBuilding { get; private set; }
        public string LastDiagnostic { get; private set; } = string.Empty;

        public void Refresh()
        {
            if (!TryResolveAuthority(out RealmId realm, out IReadOnlyList<KingdomBuildingPresentation> buildings))
            {
                ClearCity();
                return;
            }

            KingdomBuildingPresentation townHall = buildings.FirstOrDefault(
                item => item != null && string.Equals(item.BuildingId, "TownHall", StringComparison.Ordinal));
            int presentationHash = BuildPresentationHash(realm, townHall);
            if (_hasPresentationHash && presentationHash == _lastPresentationHash && _cityRoot != null)
            {
                return;
            }

            Rebuild(realm, townHall);
            _lastPresentationHash = presentationHash;
            _hasPresentationHash = true;
        }

        private bool TryResolveAuthority(
            out RealmId realm,
            out IReadOnlyList<KingdomBuildingPresentation> buildings)
        {
            realm = RealmId.None;
            buildings = Array.Empty<KingdomBuildingPresentation>();
            LastDiagnostic = string.Empty;

            try
            {
                _realmService ??= ServiceLocator.Get<IRealmService>();
                _buildingService ??= ServiceLocator.Get<IBuildingService>();
                _modelCatalog ??= KingdomBuildingModelCatalog.LoadDefault();
                realm = _realmService.CurrentRealmId;
                if (realm == RealmId.None)
                {
                    LastDiagnostic = "PRIVATE_KINGDOM_REALM_UNAVAILABLE";
                    return false;
                }

                string catalogDiagnostic = string.Empty;
                if (_modelCatalog == null || !_modelCatalog.Validate(out catalogDiagnostic))
                {
                    LastDiagnostic = string.IsNullOrWhiteSpace(catalogDiagnostic)
                        ? "PRIVATE_KINGDOM_MODEL_CATALOG_UNAVAILABLE"
                        : catalogDiagnostic;
                    return false;
                }

                buildings = KingdomBuildingPresentationResolver.Resolve(
                    realm,
                    _buildingService.GetAllBuildingStates());
                return true;
            }
            catch (Exception exception)
            {
                LastDiagnostic = "PRIVATE_KINGDOM_AUTHORITY_UNAVAILABLE:" + exception.GetType().Name;
                return false;
            }
        }

        private void Rebuild(RealmId realm, KingdomBuildingPresentation townHall)
        {
            ClearCity();
            PresentedRealm = realm;
            TownHallConstructed = townHall != null &&
                townHall.Status == KingdomBuildingPresentationStatus.Built &&
                townHall.ConfirmedLevel > 0;

            _cityRoot = new GameObject(RootName).transform;
            _cityRoot.SetParent(transform, false);

            Color accent = RealmAccent(realm);
            Color ground = RealmGround(realm);
            CreateGround(_cityRoot, realm, ground, accent);
            CreateArchitecture(_cityRoot, realm, townHall, accent);
            CreateLighting(_cityRoot, accent);
        }

        private void CreateArchitecture(
            Transform parent,
            RealmId realm,
            KingdomBuildingPresentation townHall,
            Color accent)
        {
            Transform architecture = new GameObject(ArchitectureRootName).transform;
            architecture.SetParent(parent, false);
            ArchitectureInstanceCount = 0;

            int confirmedTownHallLevel = TownHallConstructed
                ? Mathf.Clamp(townHall.ConfirmedLevel, 1, 10)
                : 1;
            GameObject hall = InstantiateModel(
                architecture,
                realm,
                "TownHall",
                confirmedTownHallLevel,
                Vector3.zero,
                Quaternion.identity,
                TownHallConstructed ? 4.0f : 3.45f,
                TownHallRootName);
            if (hall != null)
            {
                ArchitectureInstanceCount++;
                if (!TownHallConstructed)
                {
                    ApplyPreviewTone(hall, Color.Lerp(RealmGround(realm), accent, 0.18f));
                    hall.name = TownHallRootName + "_ConstructionPreview";
                }
                else
                {
                    CreateTownHallBeacon(hall.transform, accent);
                }
            }

            Vector3[] layout = LayoutFor(realm);
            DuplicateCountPerEligibleBuilding = confirmedTownHallLevel >= 5
                ? MaximumDuplicatesPerEligibleBuilding
                : MinimumDuplicatesPerEligibleBuilding;
            KingdomBuildingSlotDefinition[] eligibleSlots = KingdomBuildingLayoutCatalog
                .GetSlots(realm)
                .Where(slot => !string.Equals(slot.BuildingId, "TownHall", StringComparison.Ordinal))
                .Take(EligibleDistrictCount)
                .ToArray();
            int[] levels = { 3, 5, 7, 4, 6, 8, 5, 4 };
            for (int districtIndex = 0; districtIndex < layout.Length; districtIndex++)
            {
                Vector3 center = layout[districtIndex];
                Vector3 radial = new Vector3(center.x, 0f, center.z).normalized;
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                string eligibleBuildingId = districtIndex < eligibleSlots.Length
                    ? eligibleSlots[districtIndex].BuildingId
                    : "District" + (districtIndex + 1);
                for (int copyIndex = 0; copyIndex < DuplicateCountPerEligibleBuilding; copyIndex++)
                {
                    float centeredIndex = copyIndex - (DuplicateCountPerEligibleBuilding - 1) * 0.5f;
                    Vector3 position = center + tangent * centeredIndex * 1.55f;
                    int flatIndex = districtIndex * DuplicateCountPerEligibleBuilding + copyIndex;
                    float yaw = YawFor(realm, flatIndex, position);
                    float scale = 2.35f + (districtIndex % 2) * 0.24f;
                    GameObject district = InstantiateModel(
                        architecture,
                        realm,
                        "Workshop",
                        levels[flatIndex % levels.Length],
                        position,
                        Quaternion.Euler(0f, yaw, 0f),
                        scale,
                        $"PrivateKingdom_{eligibleBuildingId}_{copyIndex + 1:00}");
                    if (district == null)
                    {
                        continue;
                    }

                    DisableColliders(district);
                    ArchitectureInstanceCount++;
                }
            }
        }

        private GameObject InstantiateModel(
            Transform parent,
            RealmId realm,
            string buildingId,
            int level,
            Vector3 localPosition,
            Quaternion localRotation,
            float scaleMultiplier,
            string instanceName)
        {
            if (!_modelCatalog.TryGetEntry(realm, buildingId, out KingdomBuildingModelEntry entry) ||
                entry == null || !entry.IsConfigured || !entry.SupportsLevel(level))
            {
                LastDiagnostic = $"PRIVATE_KINGDOM_MODEL_UNAVAILABLE:{realm}:{buildingId}:{level}";
                return null;
            }

            GameObject instance = Instantiate(entry.Prefab, parent, false);
            instance.name = instanceName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one * entry.StrategicBoardScale * scaleMultiplier;
            KingdomBuildingLevelModel levelModel = instance.GetComponent<KingdomBuildingLevelModel>();
            if (levelModel == null || !levelModel.ApplyConfirmedLevel(level))
            {
                DestroySafely(instance);
                LastDiagnostic = $"PRIVATE_KINGDOM_LEVEL_UNAVAILABLE:{realm}:{buildingId}:{level}";
                return null;
            }

            return instance;
        }

        private void CreateGround(Transform parent, RealmId realm, Color ground, Color accent)
        {
            Transform root = new GameObject(GroundRootName).transform;
            root.SetParent(parent, false);

            Material groundMaterial = CreateMaterial(
                $"PrivateKingdom_{realm}_Ground",
                ground,
                metallic: realm == RealmId.Stonehold ? 0.18f : 0.04f,
                smoothness: realm == RealmId.Umbral ? 0.52f : 0.30f);
            CreateDiscMesh(root, "PrivateKingdom_Courtyard", 8.7f, groundMaterial);

            Material roadMaterial = CreateMaterial(
                $"PrivateKingdom_{realm}_Road",
                Color.Lerp(ground, accent, 0.07f),
                0.08f,
                0.38f);
            foreach (Vector3 destination in LayoutFor(realm))
            {
                CreateRibbonMesh(root, Vector3.zero, destination, 0.12f, roadMaterial);
            }

            Material ringMaterial = CreateMaterial(
                $"PrivateKingdom_{realm}_Inlay",
                Color.Lerp(accent, Color.white, 0.08f),
                0.26f,
                0.64f,
                accent * 0.06f);
            CreateRingMesh(root, "PrivateKingdom_HeraldicInlay", 2.0f, 2.15f, ringMaterial);
        }

        private static void CreateDiscMesh(
            Transform parent,
            string name,
            float radius,
            Material material)
        {
            const int segments = 48;
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = new Vector3(0f, 0f, 0f);
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                float edge = radius * (index % 2 == 0 ? 1f : 0.985f);
                vertices[index + 1] = new Vector3(Mathf.Cos(angle) * edge, 0f, Mathf.Sin(angle) * edge);
                uv[index + 1] = new Vector2(
                    vertices[index + 1].x / (radius * 2f) + 0.5f,
                    vertices[index + 1].z / (radius * 2f) + 0.5f);
                int triangle = index * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = (index + 1) % segments + 1;
                triangles[triangle + 2] = index + 1;
            }

            CreateMeshObject(parent, name, vertices, uv, triangles, material, 0f);
        }

        private static void CreateRingMesh(
            Transform parent,
            string name,
            float innerRadius,
            float outerRadius,
            Material material)
        {
            const int segments = 48;
            var vertices = new Vector3[segments * 2];
            var uv = new Vector2[segments * 2];
            var triangles = new int[segments * 6];
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[index * 2] = direction * innerRadius;
                vertices[index * 2 + 1] = direction * outerRadius;
                uv[index * 2] = new Vector2(index / (float)segments, 0f);
                uv[index * 2 + 1] = new Vector2(index / (float)segments, 1f);

                int next = (index + 1) % segments;
                int triangle = index * 6;
                triangles[triangle] = index * 2;
                triangles[triangle + 1] = next * 2 + 1;
                triangles[triangle + 2] = index * 2 + 1;
                triangles[triangle + 3] = index * 2;
                triangles[triangle + 4] = next * 2;
                triangles[triangle + 5] = next * 2 + 1;
            }

            CreateMeshObject(parent, name, vertices, uv, triangles, material, 0.035f);
        }

        private static void CreateRibbonMesh(
            Transform parent,
            Vector3 start,
            Vector3 end,
            float halfWidth,
            Material material)
        {
            Vector3 direction = end - start;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, direction.normalized) * halfWidth;
            Vector3[] vertices =
            {
                start - perpendicular,
                start + perpendicular,
                end - perpendicular,
                end + perpendicular
            };
            Vector2[] uv =
            {
                Vector2.zero, Vector2.right, Vector2.up, Vector2.one
            };
            int[] triangles = { 0, 2, 1, 2, 3, 1 };
            CreateMeshObject(
                parent,
                "PrivateKingdom_Avenue",
                vertices,
                uv,
                triangles,
                material,
                0.025f);
        }

        private static void CreateMeshObject(
            Transform parent,
            string name,
            Vector3[] vertices,
            Vector2[] uv,
            int[] triangles,
            Material material,
            float height)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = Vector3.up * height;
            var mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private Material CreateMaterial(
            string name,
            Color color,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader) { name = name };
            if (material.HasProperty(BaseColorProperty))
            {
                material.SetColor(BaseColorProperty, color);
            }
            if (material.HasProperty(ColorProperty))
            {
                material.SetColor(ColorProperty, color);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            _ownedMaterials.Add(material);
            return material;
        }

        private static void ApplyPreviewTone(GameObject root, Color color)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material shared = renderer.sharedMaterial;
                if (shared == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                if (shared.HasProperty(ColorProperty))
                {
                    block.SetColor(ColorProperty, color);
                }
                if (shared.HasProperty(BaseColorProperty))
                {
                    block.SetColor(BaseColorProperty, color);
                }
                renderer.SetPropertyBlock(block);
            }
        }

        private static void DisableColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static void CreateTownHallBeacon(Transform parent, Color accent)
        {
            var lightObject = new GameObject("PrivateKingdom_TownHallBeacon");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0f, 7.8f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.Lerp(accent, Color.white, 0.22f);
            light.intensity = 1.8f;
            light.range = 10f;
        }

        private void CreateLighting(Transform parent, Color accent)
        {
            if (!_ownsAmbientSettings)
            {
                _previousAmbientMode = RenderSettings.ambientMode;
                _previousAmbientLight = RenderSettings.ambientLight;
                _ownsAmbientSettings = true;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.23f, 0.24f, 0.27f);

            var sunObject = new GameObject("PrivateKingdom_CitadelSun");
            sunObject.transform.SetParent(parent, false);
            sunObject.transform.localRotation = Quaternion.Euler(48f, -34f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.Lerp(accent, Color.white, 0.72f);
            sun.intensity = 1.65f;
            sun.shadows = LightShadows.Soft;

            var fillObject = new GameObject("PrivateKingdom_CitadelFill");
            fillObject.transform.SetParent(parent, false);
            fillObject.transform.localRotation = Quaternion.Euler(58f, 142f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.48f, 0.62f, 0.92f);
            fill.intensity = 0.52f;
            fill.shadows = LightShadows.None;

            var lightObject = new GameObject("PrivateKingdom_RealmLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(-3.6f, 5.6f, -2.4f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.Lerp(accent, Color.white, 0.28f);
            light.intensity = 1.35f;
            light.range = 14f;
        }

        private static Vector3[] LayoutFor(RealmId realm) => realm switch
        {
            RealmId.Stonehold => StoneholdDistrictCenters,
            RealmId.Eldergrove => EldergroveDistrictCenters,
            RealmId.Crownlands => CrownlandsDistrictCenters,
            RealmId.Umbral => UmbralDistrictCenters,
            _ => CrownlandsDistrictCenters
        };

        private static float YawFor(RealmId realm, int index, Vector3 position)
        {
            if (realm == RealmId.Crownlands)
            {
                return index < 4 ? 180f : 0f;
            }
            if (realm == RealmId.Stonehold)
            {
                return Mathf.Round(Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg / 45f) * 45f;
            }
            if (realm == RealmId.Eldergrove)
            {
                return Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg + index * 7f;
            }

            return index * 47f + 18f;
        }

        private static Color RealmAccent(RealmId realm) => realm switch
        {
            RealmId.Stonehold => new Color(0.88f, 0.48f, 0.18f),
            RealmId.Eldergrove => new Color(0.34f, 0.86f, 0.45f),
            RealmId.Crownlands => new Color(0.95f, 0.72f, 0.22f),
            RealmId.Umbral => new Color(0.78f, 0.20f, 0.34f),
            _ => new Color(0.62f, 0.72f, 0.82f)
        };

        private static Color RealmGround(RealmId realm) => realm switch
        {
            RealmId.Stonehold => new Color(0.23f, 0.19f, 0.15f),
            RealmId.Eldergrove => new Color(0.09f, 0.24f, 0.12f),
            RealmId.Crownlands => new Color(0.12f, 0.17f, 0.27f),
            RealmId.Umbral => new Color(0.12f, 0.07f, 0.16f),
            _ => new Color(0.13f, 0.15f, 0.18f)
        };

        private static int BuildPresentationHash(
            RealmId realm,
            KingdomBuildingPresentation townHall)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)realm;
                hash = hash * 31 + (townHall?.ConfirmedLevel ?? 0);
                hash = hash * 31 + (townHall?.IsUpgrading == true ? 1 : 0);
                hash = hash * 31 + (int)(townHall?.Status ?? KingdomBuildingPresentationStatus.Unbuilt);
                return hash;
            }
        }

        private void ClearCity()
        {
            if (_cityRoot != null)
            {
                DestroySafely(_cityRoot.gameObject);
                _cityRoot = null;
            }

            GameObject stale = GameObject.Find(RootName);
            if (stale != null && stale.transform.parent == transform)
            {
                DestroySafely(stale);
            }

            foreach (Material material in _ownedMaterials)
            {
                if (material != null)
                {
                    DestroySafely(material);
                }
            }
            _ownedMaterials.Clear();
            ArchitectureInstanceCount = 0;
            DuplicateCountPerEligibleBuilding = 0;
            if (_ownsAmbientSettings)
            {
                RenderSettings.ambientMode = _previousAmbientMode;
                RenderSettings.ambientLight = _previousAmbientLight;
                _ownsAmbientSettings = false;
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            ClearCity();
        }
    }
}
