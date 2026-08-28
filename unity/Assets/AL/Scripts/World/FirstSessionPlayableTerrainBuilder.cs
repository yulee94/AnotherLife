using System;
using AL.Data.Catalogs.WorldTerrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace AL.World
{
    public sealed class FirstSessionTerrainRuntimeMarker : MonoBehaviour
    {
        [SerializeField] private string profileId;
        [SerializeField] private string replacementSocketId;
        [SerializeField] private string futureBakeContract;
        [SerializeField] private int generationSeed;

        public string ProfileId => profileId;
        public string ReplacementSocketId => replacementSocketId;
        public string FutureBakeContract => futureBakeContract;
        public int GenerationSeed => generationSeed;

        internal void Bind(FirstSessionTerrainProfile profile)
        {
            profileId = profile.Id;
            replacementSocketId = profile.ReplacementSocketId;
            futureBakeContract = profile.FutureBakeContract;
            generationSeed = profile.Generation.Seed;
        }
    }

    /// <summary>
    /// Owns UnityEngine.Objects generated for the runtime MVP terrain. Destroying
    /// the world root releases the TerrainData, TerrainLayer, and tiny grid texture.
    /// </summary>
    [ExecuteAlways]
    public sealed class FirstSessionTerrainRuntimeResources : MonoBehaviour
    {
        private TerrainData terrainData;
        private TerrainLayer terrainLayer;
        private Texture2D gridTexture;

        internal void Bind(
            TerrainData generatedTerrainData,
            TerrainLayer generatedTerrainLayer,
            Texture2D generatedGridTexture)
        {
            terrainData = generatedTerrainData;
            terrainLayer = generatedTerrainLayer;
            gridTexture = generatedGridTexture;
        }

        private void OnDestroy()
        {
            Terrain terrain = GetComponent<Terrain>();
            if (terrain != null)
            {
                terrain.terrainData = null;
            }

            TerrainCollider collider = GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = null;
            }

            if (terrainData != null)
            {
                terrainData.terrainLayers = Array.Empty<TerrainLayer>();
            }

            if (terrainLayer != null)
            {
                terrainLayer.diffuseTexture = null;
                terrainLayer.normalMapTexture = null;
            }

            Release(gridTexture);
            Release(terrainLayer);
            Release(terrainData);
            gridTexture = null;
            terrainLayer = null;
            terrainData = null;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }

    internal sealed class FirstSessionPlayableTerrainBuildResult
    {
        internal FirstSessionPlayableTerrainBuildResult(
            Terrain terrain,
            TerrainCollider terrainCollider,
            Transform collisionRoot)
        {
            Terrain = terrain;
            TerrainCollider = terrainCollider;
            CollisionRoot = collisionRoot;
        }

        internal Terrain Terrain { get; }
        internal TerrainCollider TerrainCollider { get; }
        internal Transform CollisionRoot { get; }
    }

    internal static class FirstSessionPlayableTerrainBuilder
    {
        internal static FirstSessionPlayableTerrainBuildResult Build(
            Transform root,
            FirstSessionAuthoredAssetCatalog artCatalog,
            FirstSessionTerrainProfile profile,
            Vector3 capitalGround)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (artCatalog == null) throw new ArgumentNullException(nameof(artCatalog));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            FirstSessionTerrainDimensions dimensions = profile.Dimensions;
            FirstSessionTerrainGeneration generation = profile.Generation;
            FirstSessionTerrainSurface surface = profile.Surface;

            var terrainData = new TerrainData
            {
                name = profile.Id + "_RuntimeTerrainData",
                hideFlags = HideFlags.DontSave,
                heightmapResolution = dimensions.HeightmapResolution,
                alphamapResolution = dimensions.AlphamapResolution,
                baseMapResolution = dimensions.BaseMapResolution,
                size = new Vector3(
                    dimensions.SizeXMeters,
                    dimensions.HeightMeters,
                    dimensions.SizeZMeters)
            };
            terrainData.SetHeights(
                0,
                0,
                BuildHeightfield(profile));

            Texture2D gridTexture = BuildGridTexture(surface);
            var terrainLayer = new TerrainLayer
            {
                name = profile.Id + "_DebugGridLayer",
                hideFlags = HideFlags.DontSave,
                diffuseTexture = gridTexture,
                normalMapTexture = artCatalog.PremiumFloorNormal,
                normalScale = surface.NormalScale,
                metallic = surface.Metallic,
                smoothness = surface.Smoothness,
                tileSize = new Vector2(surface.TileSizeMeters, surface.TileSizeMeters),
                tileOffset = Vector2.zero
            };
            terrainData.terrainLayers = new[] { terrainLayer };
            terrainData.SetAlphamaps(
                0,
                0,
                BuildSingleLayerAlphamap(dimensions.AlphamapResolution));

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = FirstSessionAuthoredWorldBuilder.TerrainName;
            terrainObject.transform.SetParent(root, true);
            terrainObject.transform.position = new Vector3(
                capitalGround.x - dimensions.SizeXMeters * 0.5f,
                capitalGround.y - generation.BaseHeightMeters,
                capitalGround.z - dimensions.SizeZMeters * 0.5f);

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.allowAutoConnect = false;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = surface.HeightmapPixelError;
            terrain.basemapDistance = surface.BaseMapDistanceMeters;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;

            TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
            {
                terrainCollider = terrainObject.AddComponent<TerrainCollider>();
            }
            terrainCollider.terrainData = terrainData;

            terrainObject.AddComponent<FirstSessionTerrainRuntimeResources>()
                .Bind(terrainData, terrainLayer, gridTexture);
            terrainObject.AddComponent<FirstSessionTerrainRuntimeMarker>().Bind(profile);

            var replacementSocket = new GameObject(profile.ReplacementSocketId).transform;
            replacementSocket.SetParent(root, false);
            replacementSocket.position = capitalGround;

            var collisionRoot = new GameObject(
                profile.Navigation.CollisionCollectionName).transform;
            collisionRoot.SetParent(root, false);
            BuildTerrainBoundary(collisionRoot, profile, capitalGround);
            return new FirstSessionPlayableTerrainBuildResult(
                terrain,
                terrainCollider,
                collisionRoot);
        }

        internal static Bounds AlignLandmarkAndBuildCollision(
            GameObject landmark,
            Transform collisionRoot,
            FirstSessionTerrainProfile profile,
            Vector3 capitalGround)
        {
            if (landmark == null) throw new ArgumentNullException(nameof(landmark));
            Bounds visibleBounds = CalculateBounds(landmark);
            float desiredFront = capitalGround.z +
                                 profile.Placement.LandmarkFrontOffsetMeters;
            landmark.transform.position += Vector3.forward *
                                           (desiredFront - visibleBounds.min.z);
            visibleBounds = CalculateBounds(landmark);
            BuildLandmarkCompoundCollision(
                collisionRoot,
                visibleBounds,
                profile.Collision);
            return visibleBounds;
        }

        private static float[,] BuildHeightfield(FirstSessionTerrainProfile profile)
        {
            FirstSessionTerrainDimensions dimensions = profile.Dimensions;
            FirstSessionTerrainGeneration generation = profile.Generation;
            int resolution = dimensions.HeightmapResolution;
            var heights = new float[resolution, resolution];
            float minimumHalfExtent = Mathf.Min(
                dimensions.SizeXMeters,
                dimensions.SizeZMeters) * 0.5f;
            float rimSpan = minimumHalfExtent - generation.SafeCourtyardRadiusMeters;
            float seedPhase = (generation.Seed % 4096) / 4096f * Mathf.PI * 2f;

            for (int z = 0; z < resolution; z++)
            {
                float normalizedZ = z / (float)(resolution - 1);
                float localZ = (normalizedZ - 0.5f) * dimensions.SizeZMeters;
                for (int x = 0; x < resolution; x++)
                {
                    float normalizedX = x / (float)(resolution - 1);
                    float localX = (normalizedX - 0.5f) * dimensions.SizeXMeters;
                    float distance = Mathf.Sqrt(localX * localX + localZ * localZ);
                    float rim = Mathf.Clamp01(
                        (distance - generation.SafeCourtyardRadiusMeters) / rimSpan);
                    float smoothRim = rim * rim * (3f - 2f * rim);
                    float wave = Mathf.Sin(
                                     normalizedX * generation.NoiseCycles *
                                     Mathf.PI * 2f + seedPhase) *
                                 Mathf.Cos(
                                     normalizedZ * generation.NoiseCycles *
                                     Mathf.PI * 1.73f - seedPhase * 0.67f);
                    float heightMeters = generation.BaseHeightMeters + smoothRim *
                        (generation.RimRiseMeters +
                         generation.NoiseAmplitudeMeters * wave);
                    heights[z, x] = Mathf.Clamp01(
                        heightMeters / dimensions.HeightMeters);
                }
            }

            return heights;
        }

        private static float[,,] BuildSingleLayerAlphamap(int resolution)
        {
            var alphamap = new float[resolution, resolution, 1];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    alphamap[y, x, 0] = 1f;
                }
            }

            return alphamap;
        }

        private static Texture2D BuildGridTexture(FirstSessionTerrainSurface surface)
        {
            if (!ColorUtility.TryParseHtmlString(surface.BaseColor, out Color baseColor) ||
                !ColorUtility.TryParseHtmlString(
                    surface.AlternateColor,
                    out Color alternateColor) ||
                !ColorUtility.TryParseHtmlString(surface.GridColor, out Color gridColor))
            {
                throw new InvalidOperationException(
                    "Validated terrain profile contains an invalid grid color.");
            }

            int resolution = surface.TextureResolution;
            int cellPixels = resolution / surface.CheckerCellsPerTile;
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    bool grid = x % cellPixels < surface.GridLinePixels ||
                                y % cellPixels < surface.GridLinePixels;
                    bool alternate = ((x / cellPixels) + (y / cellPixels)) % 2 != 0;
                    pixels[y * resolution + x] = grid
                        ? gridColor
                        : alternate ? alternateColor : baseColor;
                }
            }

            var texture = new Texture2D(
                resolution,
                resolution,
                TextureFormat.RGBA32,
                true,
                false)
            {
                name = "FirstSessionTerrain_DebugGrid_" + resolution,
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 2
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static void BuildTerrainBoundary(
            Transform collisionRoot,
            FirstSessionTerrainProfile profile,
            Vector3 capitalGround)
        {
            FirstSessionTerrainDimensions dimensions = profile.Dimensions;
            FirstSessionTerrainGeneration generation = profile.Generation;
            FirstSessionTerrainCollision collision = profile.Collision;
            float halfX = dimensions.SizeXMeters * 0.5f -
                          collision.BoundaryInsetMeters;
            float halfZ = dimensions.SizeZMeters * 0.5f -
                          collision.BoundaryInsetMeters;
            float thickness = collision.BoundaryThicknessMeters;
            float bottom = capitalGround.y - 0.5f;
            float totalHeight = collision.BoundaryHeightMeters +
                                generation.RimRiseMeters +
                                generation.NoiseAmplitudeMeters + 0.5f;
            float centerY = bottom + totalHeight * 0.5f;

            CreateBoxCollider(
                collisionRoot,
                "COL_FirstSessionTerrainBoundary_North",
                new Vector3(capitalGround.x, centerY, capitalGround.z + halfZ),
                new Vector3(dimensions.SizeXMeters, totalHeight, thickness));
            CreateBoxCollider(
                collisionRoot,
                "COL_FirstSessionTerrainBoundary_South",
                new Vector3(capitalGround.x, centerY, capitalGround.z - halfZ),
                new Vector3(dimensions.SizeXMeters, totalHeight, thickness));
            CreateBoxCollider(
                collisionRoot,
                "COL_FirstSessionTerrainBoundary_East",
                new Vector3(capitalGround.x + halfX, centerY, capitalGround.z),
                new Vector3(thickness, totalHeight, dimensions.SizeZMeters));
            CreateBoxCollider(
                collisionRoot,
                "COL_FirstSessionTerrainBoundary_West",
                new Vector3(capitalGround.x - halfX, centerY, capitalGround.z),
                new Vector3(thickness, totalHeight, dimensions.SizeZMeters));
        }

        private static void BuildLandmarkCompoundCollision(
            Transform collisionRoot,
            Bounds visibleBounds,
            FirstSessionTerrainCollision collision)
        {
            var compound = new GameObject(
                FirstSessionAuthoredWorldBuilder.LandmarkCollisionRootName).transform;
            compound.SetParent(collisionRoot, false);
            float thickness = collision.LandmarkProxyThicknessMeters;
            float height = Mathf.Clamp(
                visibleBounds.size.y * collision.LandmarkHeightFraction,
                collision.LandmarkMinimumHeightMeters,
                collision.LandmarkMaximumHeightMeters);
            float centerY = visibleBounds.min.y + height * 0.5f;
            float entranceWidth = Mathf.Min(
                collision.LandmarkEntranceWidthMeters,
                visibleBounds.size.x);
            float segmentWidth = (visibleBounds.size.x - entranceWidth) * 0.5f;
            float lateralThickness = segmentWidth > Mathf.Epsilon
                ? Mathf.Min(thickness, segmentWidth)
                : thickness;

            CreateBoxCollider(
                compound,
                "COL_Landmark_Left",
                new Vector3(visibleBounds.min.x, centerY, visibleBounds.center.z),
                new Vector3(lateralThickness, height, visibleBounds.size.z));
            CreateBoxCollider(
                compound,
                "COL_Landmark_Right",
                new Vector3(visibleBounds.max.x, centerY, visibleBounds.center.z),
                new Vector3(lateralThickness, height, visibleBounds.size.z));
            CreateBoxCollider(
                compound,
                "COL_Landmark_Back",
                new Vector3(visibleBounds.center.x, centerY, visibleBounds.max.z),
                new Vector3(visibleBounds.size.x, height, thickness));

            if (entranceWidth <= Mathf.Epsilon)
            {
                CreateBoxCollider(
                    compound,
                    "COL_Landmark_Front",
                    new Vector3(
                        visibleBounds.center.x,
                        centerY,
                        visibleBounds.min.z),
                    new Vector3(visibleBounds.size.x, height, thickness));
                return;
            }

            if (segmentWidth > Mathf.Epsilon)
            {
                CreateBoxCollider(
                    compound,
                    "COL_Landmark_FrontLeft",
                    new Vector3(
                        visibleBounds.min.x + segmentWidth * 0.5f,
                        centerY,
                        visibleBounds.min.z),
                    new Vector3(segmentWidth, height, thickness));
                CreateBoxCollider(
                    compound,
                    "COL_Landmark_FrontRight",
                    new Vector3(
                        visibleBounds.max.x - segmentWidth * 0.5f,
                        centerY,
                        visibleBounds.min.z),
                    new Vector3(segmentWidth, height, thickness));
            }
        }

        private static BoxCollider CreateBoxCollider(
            Transform parent,
            string name,
            Vector3 worldCenter,
            Vector3 worldSize)
        {
            var proxy = new GameObject(name);
            proxy.transform.SetParent(parent, false);
            proxy.transform.position = worldCenter;
            BoxCollider collider = proxy.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = worldSize;
            return collider;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "Authored landmark has no visible bounds: " + root.name);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }
    }
}
