using System;
using AL.Core;
using AL.Data.Catalogs.WorldTerrain;
using UnityEngine;

namespace AL.World
{
    public sealed class FirstSessionAuthoredWorldMarker : MonoBehaviour
    {
        [SerializeField] private RealmId realm;
        [SerializeField] private string sourceCatalog =
            FirstSessionAuthoredAssetCatalog.ResourcesPath;
        [SerializeField] private int importedRendererCount;

        public RealmId Realm => realm;
        public string SourceCatalog => sourceCatalog;
        public int ImportedRendererCount => importedRendererCount;

        internal void Bind(RealmId value, int rendererCount)
        {
            realm = value;
            importedRendererCount = rendererCount;
        }
    }

    /// <summary>
    /// Builds the first-session player-facing capital from a real Unity Terrain,
    /// admitted imported presentation meshes, and explicit primitive collision
    /// proxies. The old atlas-scale primitive builder remains isolated to topology
    /// tests and is not on the production first-session route.
    /// </summary>
    public static class FirstSessionAuthoredWorldBuilder
    {
        public const string RootName = "FirstSessionAuthoredInnerRealm";
        public const string HallName = "AuthoredCovenantHall";
        public const string TerrainName = "NAV_FirstSessionCapitalTerrain";
        public const string LandmarkCollisionRootName = "COL_FirstSessionLandmarkCompound";
        public const string StructuralIdentityPrefix = "RealmStructuralIdentity_";

        public static InnerRealmWorldBuildResult Build(
            InnerRealmWorldLayout layout,
            string walkableRealmId)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            FirstSessionAuthoredAssetCatalog catalog =
                Resources.Load<FirstSessionAuthoredAssetCatalog>(
                    FirstSessionAuthoredAssetCatalog.ResourcesPath);
            if (catalog == null || !catalog.HasRequiredAssets())
            {
                throw new InvalidOperationException(
                    "First-session authored asset catalog is missing or incomplete.");
            }

            InnerRealmSlotLayout walkable = layout.GetWalkableInner(walkableRealmId);
            if (!catalog.TryResolveRealmVisual(walkable.Realm, out FirstSessionRealmVisualAsset realmVisual))
            {
                throw new InvalidOperationException(
                    "No authored structural identity is admitted for " + walkable.Realm + ".");
            }

            FirstSessionTerrainLoadResult terrainLoad =
                FirstSessionTerrainCatalogLoader.Validate(
                    catalog.FirstSessionTerrainCatalog.bytes);
            if (!terrainLoad.IsAccepted)
            {
                string diagnostic = terrainLoad.Diagnostics.Count == 0
                    ? "unknown"
                    : terrainLoad.Diagnostics[0].Fingerprint;
                throw new InvalidOperationException(
                    "First-session terrain catalog is rejected: " + diagnostic);
            }

            var root = new GameObject(RootName).transform;
            Vector3 groundCenter = walkable.CapitalPosition;
            ConfigureAtmosphere(walkable.Realm, realmVisual.PanoramicSky);
            FirstSessionPlayableTerrainBuildResult terrain =
                FirstSessionPlayableTerrainBuilder.Build(
                    root,
                    catalog,
                    terrainLoad.Profile,
                    groundCenter);
            BuildHall(root, catalog, realmVisual, groundCenter, walkable.Realm);
            BuildRealmIdentity(
                root,
                walkable.Realm,
                realmVisual,
                terrainLoad.Profile,
                terrain.CollisionRoot,
                groundCenter);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            var marker = root.gameObject.AddComponent<FirstSessionAuthoredWorldMarker>();
            marker.Bind(walkable.Realm, renderers.Length);
            return new InnerRealmWorldBuildResult(root, layout, walkable);
        }

        private static void BuildHall(
            Transform root,
            FirstSessionAuthoredAssetCatalog catalog,
            FirstSessionRealmVisualAsset realmVisual,
            Vector3 center,
            RealmId realm)
        {
            GameObject hall = UnityEngine.Object.Instantiate(catalog.CovenantHallPrefab, root);
            hall.name = HallName;
            hall.transform.position = new Vector3(center.x, 0f, center.z);
            hall.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Material floorMaterial = CreateHallMaterial(
                catalog.FloorMaterial,
                "AuthoredHallFloor_" + realm,
                RealmStone(realm),
                0.12f,
                0.28f);
            Material wallMaterial = CreateHallMaterial(
                catalog.WallMaterial,
                "AuthoredHallWall_" + realm,
                Color.Lerp(new Color(0.12f, 0.13f, 0.16f), RealmStone(realm), 0.22f),
                0.18f,
                0.34f);
            Material trimMaterial = CreateHallMaterial(
                catalog.TrimMaterial,
                "AuthoredHallTrim_" + realm,
                Color.Lerp(new Color(0.34f, 0.24f, 0.12f), RealmAccent(realm), 0.28f),
                0.72f,
                0.58f);

            Transform floor = FindRequired(hall.transform, "FloorModule");
            Transform wall = FindRequired(hall.transform, "WallModule");
            Transform innerCorner = FindRequired(hall.transform, "InnerCornerModule");
            Transform outerCorner = FindRequired(hall.transform, "OuterCornerModule");
            Transform doorway = FindRequired(hall.transform, "DoorwayModule");
            Transform beam = FindRequired(hall.transform, "CeilingBeamModule");
            Transform trim = FindRequired(hall.transform, "TrimModule");
            Transform brazier = FindRequired(hall.transform, "BrazierProp");
            Transform banner = FindRequired(hall.transform, "BannerStandProp");
            Transform clutter = FindRequired(hall.transform, "CrateBarrelProp");
            Transform floorCenter = Clone(floor, hall.transform, "FloorModule_Center");
            Transform floorDais = Clone(floor, hall.transform, "FloorModule_Dais");

            // The premium realm landmark now carries the bounded architectural mass.
            // Keep only a compact authored covenant threshold/dressing kit in front of it.
            wall.gameObject.SetActive(false);
            innerCorner.gameObject.SetActive(false);
            outerCorner.gameObject.SetActive(false);
            doorway.gameObject.SetActive(false);
            beam.gameObject.SetActive(false);
            trim.gameObject.SetActive(false);
            clutter.gameObject.SetActive(false);

            Place(floor, center + new Vector3(0f, 0f, -4.8f), 0f, true);
            Place(floorCenter, center + new Vector3(0f, 0f, 2f), 0f, true);
            Place(floorDais, center + new Vector3(0f, 0f, 8.8f), 0f, true);
            WidenThreshold(floor);
            WidenThreshold(floorCenter);
            WidenThreshold(floorDais);
            Place(brazier, center + new Vector3(-3.8f, 0f, 2.4f), 0f, true);
            Place(Clone(brazier, hall.transform, "BrazierProp_Right"), center + new Vector3(3.8f, 0f, 2.4f), 0f, true);
            Place(banner, center + new Vector3(-4.5f, 0f, 4.1f), 0f, true);

            AssignMaterial(floor, floorMaterial);
            AssignMaterial(wall, wallMaterial);
            AssignMaterial(innerCorner, wallMaterial);
            AssignMaterial(outerCorner, wallMaterial);
            AssignMaterial(doorway, wallMaterial);
            AssignMaterial(beam, trimMaterial);
            AssignMaterial(trim, trimMaterial);
            AssignMaterial(brazier, trimMaterial);
            AssignMaterial(banner, floorMaterial);
            AssignMaterial(clutter, wallMaterial);
            AssignMaterialsByName(hall.transform, floorMaterial, wallMaterial, trimMaterial);
            Material premiumFloorMaterial = CreatePremiumFloorMaterial(catalog, realm);
            AssignMaterial(floor, premiumFloorMaterial);
            AssignMaterial(floorCenter, premiumFloorMaterial);
            AssignMaterial(floorDais, premiumFloorMaterial);

            Renderer[] thresholdRenderers = hall.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < thresholdRenderers.Length; index++)
            {
                thresholdRenderers[index].enabled = false;
            }
            SetRenderersEnabled(floor, true);
            SetRenderersEnabled(floorCenter, true);
            SetRenderersEnabled(floorDais, true);
        }

        private static void SetRenderersEnabled(Transform target, bool enabled)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }

        private static Material CreatePremiumFloorMaterial(
            FirstSessionAuthoredAssetCatalog catalog,
            RealmId realm)
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = realm + "_PremiumThreshold_PBR",
                color = Color.Lerp(Color.white, RealmStone(realm), 0.08f),
                mainTexture = catalog.PremiumFloorBaseColor
            };
            material.SetTexture("_BumpMap", catalog.PremiumFloorNormal);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", catalog.PremiumFloorMetallic);
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetFloat("_Metallic", 0.12f);
            material.SetFloat("_Glossiness", 0.28f);
            material.SetFloat("_GlossMapScale", 0.72f);
            return material;
        }

        private static void BuildRealmIdentity(
            Transform root,
            RealmId realm,
            FirstSessionRealmVisualAsset realmVisual,
            FirstSessionTerrainProfile terrainProfile,
            Transform collisionRoot,
            Vector3 center)
        {
            var identity = new GameObject(StructuralIdentityPrefix + realm).transform;
            identity.SetParent(root, false);

            GameObject landmark = UnityEngine.Object.Instantiate(
                realmVisual.PremiumLandmarkPrefab,
                identity);
            landmark.name = realm + "_PremiumCapitalHall";
            landmark.transform.position = center;
            landmark.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            landmark.transform.localScale = Vector3.one;
            ScaleAndGround(landmark, center.y, LandmarkExtent(realm));
            FirstSessionPlayableTerrainBuilder.AlignLandmarkAndBuildCollision(
                landmark,
                collisionRoot,
                terrainProfile,
                center);
            ApplyPremiumRealmMaterial(landmark, realmVisual, realm);
        }

        private static float LandmarkExtent(RealmId realm)
        {
            return realm == RealmId.Eldergrove ? 28f : 17.5f;
        }

        private static void ConfigureAtmosphere(RealmId realm, Texture2D panoramicSky)
        {
            Color accent = RealmAccent(realm);
            Color horizon = Color.Lerp(new Color(0.14f, 0.17f, 0.22f), accent, 0.12f);
            Color ground = Color.Lerp(horizon, new Color(0.08f, 0.10f, 0.14f), 0.24f);
            Shader skyShader = Shader.Find("Skybox/Panoramic");
            if (skyShader != null)
            {
                var sky = new Material(skyShader)
                {
                    name = realm + "_FirstSessionPanoramicSky"
                };
                sky.SetTexture("_MainTex", panoramicSky);
                sky.SetColor("_Tint", Color.Lerp(Color.white, accent, 0.06f));
                sky.SetFloat("_Exposure", realm == RealmId.Crownlands ? 0.50f : 0.72f);
                sky.SetFloat("_Rotation", PanoramicRotation(realm));
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.24f, 0.29f, 0.38f);
            RenderSettings.ambientEquatorColor = horizon;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogDensity = 0.018f;
        }

        private static float PanoramicRotation(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return 32f;
                case RealmId.Eldergrove:
                    return 18f;
                case RealmId.Crownlands:
                    return 8f;
                case RealmId.Umbral:
                    return 24f;
                default:
                    return 0f;
            }
        }

        private static void WidenThreshold(Transform target)
        {
            target.localScale = new Vector3(
                target.localScale.x * 1.75f,
                target.localScale.y,
                target.localScale.z);
        }

        private static void ApplyPremiumRealmMaterial(
            GameObject landmark,
            FirstSessionRealmVisualAsset visual,
            RealmId realm)
        {
            Renderer[] renderers = landmark.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material source = renderers[rendererIndex].sharedMaterial;
                var material = source != null
                    ? new Material(source)
                    : new Material(Shader.Find("Standard"));
                material.name = realm + "_PremiumCapitalHall_PBR";
                material.color = Color.white;
                material.mainTexture = visual.PremiumBaseColor;
                material.SetTexture("_BumpMap", visual.PremiumNormal);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", visual.PremiumMetallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetFloat("_Metallic", 0.2f);
                material.SetFloat("_Glossiness", 0.34f);
                material.SetFloat("_GlossMapScale", 0.82f);
                material.SetTexture("_EmissionMap", visual.PremiumEmission);
                material.SetColor("_EmissionColor", RealmAccent(realm) * 0.4f);
                material.EnableKeyword("_EMISSION");
                renderers[rendererIndex].sharedMaterial = material;
                renderers[rendererIndex].shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[rendererIndex].receiveShadows = true;
            }
        }

        private static Transform Clone(Transform source, Transform parent, string name)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, parent);
            clone.name = name;
            return clone.transform;
        }

        private static void Place(
            Transform target,
            Vector3 position,
            float yaw,
            bool ground)
        {
            target.position = position;
            target.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (ground)
            {
                Ground(target, position.y);
            }
        }

        private static void Ground(Transform target, float groundY)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            target.position += Vector3.up * (groundY - bounds.min.y);
        }

        private static Material CreateHallMaterial(
            Material source,
            string name,
            Color color,
            float metallic,
            float smoothness)
        {
            var material = new Material(source)
            {
                name = name,
                color = color
            };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static void AssignMaterialsByName(
            Transform hall,
            Material floor,
            Material wall,
            Material trim)
        {
            Renderer[] renderers = hall.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                string name = renderers[index].transform.name;
                Material material = name.IndexOf("Floor", StringComparison.Ordinal) >= 0 ||
                                    name.IndexOf("Banner", StringComparison.Ordinal) >= 0
                    ? floor
                    : name.IndexOf("Beam", StringComparison.Ordinal) >= 0 ||
                      name.IndexOf("Trim", StringComparison.Ordinal) >= 0 ||
                      name.IndexOf("Brazier", StringComparison.Ordinal) >= 0
                        ? trim
                        : wall;
                Material[] slots = renderers[index].sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    slots[slot] = material;
                }

                renderers[index].sharedMaterials = slots;
                renderers[index].shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[index].receiveShadows = true;
            }
        }

        private static Color RealmStone(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return new Color(0.24f, 0.18f, 0.14f);
                case RealmId.Eldergrove:
                    return new Color(0.13f, 0.22f, 0.17f);
                case RealmId.Crownlands:
                    return new Color(0.16f, 0.19f, 0.27f);
                case RealmId.Umbral:
                    return new Color(0.18f, 0.13f, 0.22f);
                default:
                    return new Color(0.18f, 0.18f, 0.18f);
            }
        }

        private static Color RealmAccent(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return new Color(0.78f, 0.34f, 0.14f);
                case RealmId.Eldergrove:
                    return new Color(0.31f, 0.64f, 0.38f);
                case RealmId.Crownlands:
                    return new Color(0.38f, 0.55f, 0.92f);
                case RealmId.Umbral:
                    return new Color(0.59f, 0.28f, 0.78f);
                default:
                    return Color.gray;
            }
        }

        private static void ScaleAndGround(
            GameObject structure,
            float groundY,
            float targetHorizontalExtent)
        {
            Bounds bounds = CalculateBounds(structure);
            float horizontalExtent = Mathf.Max(bounds.size.x, bounds.size.z);
            if (horizontalExtent > 0.01f)
            {
                float scale = Mathf.Clamp(
                    targetHorizontalExtent / horizontalExtent,
                    0.2f,
                    100f);
                structure.transform.localScale = Vector3.one * scale;
                bounds = CalculateBounds(structure);
            }

            structure.transform.position += Vector3.up * (groundY - bounds.min.y);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static Transform FindRequired(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (string.Equals(transforms[index].name, name, StringComparison.Ordinal))
                {
                    return transforms[index];
                }
            }

            throw new InvalidOperationException("Authored hall module missing: " + name);
        }

        private static void AssignMaterial(Transform target, Material material)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = material;
                }

                renderers[rendererIndex].sharedMaterials = materials;
                renderers[rendererIndex].shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[rendererIndex].receiveShadows = true;
            }
        }
    }
}
