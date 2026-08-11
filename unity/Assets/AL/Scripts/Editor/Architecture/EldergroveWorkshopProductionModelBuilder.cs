using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Kingdom.Visuals.Architecture;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AL.Editor.Architecture
{
    /// <summary>
    /// Converts the approved Eldergrove Workshop progression blockout into
    /// the packaged, cumulative production model used by the live kingdom.
    /// </summary>
    public static class EldergroveWorkshopProductionModelBuilder
    {
        public const string SourcePrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "Eldergrove_Workshop_Level10_Blockout.prefab";
        public const string RuntimeFolder =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/Runtime";
        public const string MeshFolder = RuntimeFolder + "/Meshes";
        public const string AtlasPath =
            RuntimeFolder + "/T_Eldergrove_Workshop_Atlas_1024.png";
        public const string AtlasMaterialPath =
            RuntimeFolder + "/MAT_Eldergrove_Workshop_Atlas.mat";
        public const string AccentMaterialPath =
            RuntimeFolder + "/MAT_Eldergrove_Workshop_Accent.mat";
        public const string PrefabPath =
            RuntimeFolder + "/Eldergrove_Workshop_Production.prefab";
        public const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        public const string MotionProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Eldergrove_Atelier_ConstructionProfile.asset";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "EldergroveWorkshopProductionModel.unity";
        public const string ModelId =
            "building.eldergrove.workshop.production.v1";

        private const string BuildingId = "Workshop";
        private const int AtlasSize = 1024;
        private const float StrategicBoardScale = 0.12f;

        private static readonly Vector3 SlotEnvelope =
            new Vector3(10f, 6.8f, 8f);
        private static readonly Vector3 MaximumArtBounds =
            new Vector3(9.2f, 6.8f, 7f);
        private static readonly float[] LodTransitions =
            { 0.60f, 0.30f, 0.12f, 0.04f };

        private static readonly string[] LevelGroupNames =
        {
            "L01_Foundational",
            "L02_Reinforced",
            "L03_Expanded",
            "L04_Established",
            "L05_DistrictAnchor",
            "L06_Advanced",
            "L07_Signature",
            "L08_Masterwork",
            "L09_Prestige",
            "L10_Landmark"
        };

        private static readonly Color32[] AtlasColors =
        {
            new Color32(174, 160, 132, 255),
            new Color32(76, 58, 39, 255),
            new Color32(70, 48, 31, 255),
            new Color32(119, 89, 47, 255),
            new Color32(63, 98, 54, 255),
            new Color32(52, 112, 65, 255),
            new Color32(69, 70, 54, 255),
            new Color32(111, 105, 79, 255)
        };

        [MenuItem("Another Life/Architecture/Build Eldergrove Workshop Production Model")]
        public static void Build()
        {
            EnsureFolders();
            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"The approved Level 10 source is missing: {SourcePrefabPath}");
            }

            Texture2D atlas = CreateAtlas();
            Material atlasMaterial = CreateOrUpdateMaterial(
                AtlasMaterialPath,
                atlas,
                new Color(0.84f, 0.82f, 0.72f),
                Color.black);
            Material accentMaterial = CreateOrUpdateMaterial(
                AccentMaterialPath,
                null,
                new Color(0.22f, 0.67f, 0.31f),
                new Color(0.08f, 0.36f, 0.12f));

            GameObject production =
                BuildProductionObject(source, atlasMaterial, accentMaterial);
            GameObject prefab;
            try
            {
                prefab = PrefabUtility.SaveAsPrefabAsset(
                    production,
                    PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(production);
            }

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Unity did not save the Eldergrove production prefab.");
            }

            CreateOrUpdateCatalog(prefab);
            CreatePreviewScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        public static void RenderFromCommandLine()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "The Eldergrove production review camera is missing.");
            }

            WriteFrame(
                camera,
                1536,
                768,
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "..",
                    ".omx",
                    "state",
                    "eldergrove-workshop-production",
                    "render.png"));
        }

        public static void ReportMetricsFromCommandLine()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "The Eldergrove production prefab is missing.");
            }

            for (int lodIndex = 0; lodIndex < 4; lodIndex++)
            {
                Transform root = prefab.transform.Find($"LOD{lodIndex}");
                int triangles = root == null
                    ? 0
                    : root.GetComponentsInChildren<MeshFilter>(true)
                        .Sum(filter => TriangleCount(filter.sharedMesh));
                int renderers = root == null
                    ? 0
                    : root.GetComponentsInChildren<Renderer>(true).Length;
                Debug.Log(
                    $"ELDERGROVE_METRIC LOD{lodIndex} " +
                    $"triangles={triangles} renderers={renderers}");
            }

            foreach (int level in new[] { 1, 6, 10 })
            {
                GameObject instance = Object.Instantiate(prefab);
                try
                {
                    instance.GetComponent<KingdomBuildingLevelModel>()
                        .ApplyConfirmedLevel(level);
                    Renderer[] renderers = instance.transform
                        .Find("LOD0")
                        .GetComponentsInChildren<Renderer>(false);
                    Bounds bounds = renderers[0].bounds;
                    for (int index = 1;
                        index < renderers.Length;
                        index++)
                    {
                        bounds.Encapsulate(renderers[index].bounds);
                    }
                    Debug.Log(
                        $"ELDERGROVE_BOUNDS level={level} " +
                        $"size={bounds.size} center={bounds.center}");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static int TriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            int triangles = 0;
            for (int subMesh = 0;
                subMesh < mesh.subMeshCount;
                subMesh++)
            {
                triangles += (int)mesh.GetIndexCount(subMesh) / 3;
            }
            return triangles;
        }

        private static GameObject BuildProductionObject(
            GameObject sourcePrefab,
            Material atlasMaterial,
            Material accentMaterial)
        {
            GameObject source = Object.Instantiate(sourcePrefab);
            source.name = "Eldergrove_Workshop_Production_Source";
            source.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            source.transform.localScale = Vector3.one;

            var production =
                new GameObject("Eldergrove_Workshop_Production");
            var lodRoots = new Transform[4];
            var levelObjects =
                new GameObject[10, 4];

            try
            {
                for (int lodIndex = 0; lodIndex < 3; lodIndex++)
                {
                    lodRoots[lodIndex] =
                        CreateGroup(production.transform, $"LOD{lodIndex}");
                    for (int levelIndex = 0;
                        levelIndex < LevelGroupNames.Length;
                        levelIndex++)
                    {
                        Transform levelGroup =
                            source.transform.Find(LevelGroupNames[levelIndex]);
                        if (levelGroup == null)
                        {
                            throw new InvalidOperationException(
                                $"Missing approved source group " +
                                $"{LevelGroupNames[levelIndex]}.");
                        }

                        levelObjects[levelIndex, lodIndex] =
                            CreateCombinedLevelObject(
                                production.transform,
                                lodRoots[lodIndex],
                                new[] { levelGroup },
                                levelIndex + 1,
                                lodIndex,
                                atlasMaterial,
                                accentMaterial);
                    }
                }

                lodRoots[3] = CreateGroup(production.transform, "LOD3");
                CreateLod3Milestone(
                    production.transform,
                    source.transform,
                    lodRoots[3],
                    1,
                    1,
                    1,
                    levelObjects,
                    atlasMaterial,
                    accentMaterial);
                CreateLod3Milestone(
                    production.transform,
                    source.transform,
                    lodRoots[3],
                    2,
                    6,
                    6,
                    levelObjects,
                    atlasMaterial,
                    accentMaterial);
                CreateLod3Milestone(
                    production.transform,
                    source.transform,
                    lodRoots[3],
                    7,
                    10,
                    10,
                    levelObjects,
                    atlasMaterial,
                    accentMaterial);

                LODGroup lodGroup = production.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.animateCrossFading = false;
                var lods = new LOD[4];
                for (int lodIndex = 0; lodIndex < lodRoots.Length; lodIndex++)
                {
                    Renderer[] renderers = lodRoots[lodIndex]
                        .GetComponentsInChildren<Renderer>(true);
                    lods[lodIndex] = new LOD(
                        LodTransitions[lodIndex],
                        renderers);
                }
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();

                BoxCollider selection =
                    production.AddComponent<BoxCollider>();
                selection.isTrigger = true;
                selection.center = new Vector3(0f, 3.4f, 0f);
                selection.size = new Vector3(9.4f, 6.8f, 7.2f);

                BoxCollider navigation =
                    production.AddComponent<BoxCollider>();
                navigation.isTrigger = false;
                navigation.center = new Vector3(0f, 0.7f, 0f);
                navigation.size = new Vector3(8.6f, 1.4f, 6.2f);

                var deltas = new KingdomBuildingLevelDelta[10];
                for (int levelIndex = 0;
                    levelIndex < deltas.Length;
                    levelIndex++)
                {
                    var objects = new GameObject[4];
                    for (int lodIndex = 0; lodIndex < 4; lodIndex++)
                    {
                        objects[lodIndex] =
                            levelObjects[levelIndex, lodIndex];
                    }
                    deltas[levelIndex] = new KingdomBuildingLevelDelta(
                        levelIndex + 1,
                        objects);
                }

                var levelModel =
                    production.AddComponent<KingdomBuildingLevelModel>();
                levelModel.Configure(
                    ModelId,
                    BuildingId,
                    SlotEnvelope,
                    MaximumArtBounds,
                    deltas,
                    lodGroup,
                    selection,
                    navigation);
                levelModel.ApplyConfirmedLevel(10);

                return production;
            }
            catch
            {
                Object.DestroyImmediate(production);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static void CreateLod3Milestone(
            Transform productionRoot,
            Transform sourceRoot,
            Transform lodRoot,
            int firstLevel,
            int lastLevel,
            int activationLevel,
            GameObject[,] levelObjects,
            Material atlasMaterial,
            Material accentMaterial)
        {
            var groups = new List<Transform>();
            for (int level = firstLevel; level <= lastLevel; level++)
            {
                Transform group =
                    sourceRoot.Find(LevelGroupNames[level - 1]);
                if (group == null)
                {
                    throw new InvalidOperationException(
                        $"Missing approved source group " +
                        $"{LevelGroupNames[level - 1]}.");
                }
                groups.Add(group);
            }

            levelObjects[activationLevel - 1, 3] =
                CreateCombinedLevelObject(
                    productionRoot,
                    lodRoot,
                    groups,
                    activationLevel,
                    3,
                    atlasMaterial,
                    accentMaterial);
        }

        private static GameObject CreateCombinedLevelObject(
            Transform productionRoot,
            Transform lodRoot,
            IEnumerable<Transform> sourceGroups,
            int level,
            int lodIndex,
            Material atlasMaterial,
            Material accentMaterial)
        {
            var structural = new List<MeshSource>();
            var accent = new List<MeshSource>();
            foreach (Transform group in sourceGroups)
            {
                foreach (MeshFilter filter in
                    group.GetComponentsInChildren<MeshFilter>(true))
                {
                    MeshRenderer renderer =
                        filter.GetComponent<MeshRenderer>();
                    if (renderer == null ||
                        filter.sharedMesh == null ||
                        !ShouldInclude(filter, lodIndex))
                    {
                        continue;
                    }

                    Material sourceMaterial = renderer.sharedMaterial;
                    var meshSource = new MeshSource(
                        filter,
                        sourceMaterial,
                        ResolveAtlasCell(sourceMaterial));
                    if (IsAccent(sourceMaterial))
                    {
                        accent.Add(meshSource);
                    }
                    else
                    {
                        structural.Add(meshSource);
                    }
                }
            }

            if (structural.Count == 0 && accent.Count == 0)
            {
                throw new InvalidOperationException(
                    $"LOD{lodIndex} level {level} contains no retained mesh.");
            }

            var levelObject = new GameObject($"L{level:D2}_Delta");
            levelObject.transform.SetParent(lodRoot, false);
            var filterComponent = levelObject.AddComponent<MeshFilter>();
            var rendererComponent =
                levelObject.AddComponent<MeshRenderer>();

            string meshPath =
                $"{MeshFolder}/M_Eldergrove_Workshop_" +
                $"LOD{lodIndex}_L{level:D2}.asset";
            Mesh combined = CombineCategories(
                productionRoot,
                structural,
                accent,
                $"M_Eldergrove_Workshop_LOD{lodIndex}_L{level:D2}");
            filterComponent.sharedMesh =
                PersistMesh(combined, meshPath);

            var materials = new List<Material>();
            if (structural.Count > 0)
            {
                materials.Add(atlasMaterial);
            }
            if (accent.Count > 0)
            {
                materials.Add(accentMaterial);
            }
            rendererComponent.sharedMaterials = materials.ToArray();
            ConfigureRenderer(rendererComponent, lodIndex);
            return levelObject;
        }

        private static Mesh CombineCategories(
            Transform productionRoot,
            IReadOnlyList<MeshSource> structural,
            IReadOnlyList<MeshSource> accent,
            string name)
        {
            var categoryMeshes = new List<Mesh>();
            try
            {
                Mesh structuralMesh = CombineSources(
                    productionRoot,
                    structural,
                    name + "_Structural");
                if (structuralMesh != null)
                {
                    categoryMeshes.Add(structuralMesh);
                }

                Mesh accentMesh = CombineSources(
                    productionRoot,
                    accent,
                    name + "_Accent");
                if (accentMesh != null)
                {
                    categoryMeshes.Add(accentMesh);
                }

                var final = new Mesh
                {
                    name = name,
                    indexFormat = IndexFormat.UInt32
                };
                var combines = categoryMeshes
                    .Select(mesh => new CombineInstance
                    {
                        mesh = mesh,
                        transform = Matrix4x4.identity
                    })
                    .ToArray();
                final.CombineMeshes(
                    combines,
                    false,
                    true,
                    false);
                final.RecalculateBounds();
                return final;
            }
            finally
            {
                foreach (Mesh mesh in categoryMeshes)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        private static Mesh CombineSources(
            Transform productionRoot,
            IReadOnlyList<MeshSource> sources,
            string name)
        {
            if (sources.Count == 0)
            {
                return null;
            }

            var temporaryMeshes = new List<Mesh>();
            try
            {
                var combines = new CombineInstance[sources.Count];
                for (int index = 0; index < sources.Count; index++)
                {
                    MeshSource source = sources[index];
                    Mesh remapped = CreateLodMesh(
                        source.Filter.sharedMesh,
                        GetLodIndexFromName(name));
                    remapped.name =
                        source.Filter.sharedMesh.name + "_Atlas";
                    RemapUvs(remapped, source.AtlasCell);
                    temporaryMeshes.Add(remapped);
                    combines[index] = new CombineInstance
                    {
                        mesh = remapped,
                        transform =
                            productionRoot.worldToLocalMatrix *
                            source.Filter.transform.localToWorldMatrix
                    };
                }

                var combined = new Mesh
                {
                    name = name,
                    indexFormat = IndexFormat.UInt32
                };
                combined.CombineMeshes(combines, true, true, false);
                combined.RecalculateBounds();
                return combined;
            }
            finally
            {
                foreach (Mesh mesh in temporaryMeshes)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        private static int GetLodIndexFromName(string name)
        {
            int marker = name.IndexOf("_LOD", StringComparison.Ordinal);
            if (marker < 0 || marker + 4 >= name.Length)
            {
                return 0;
            }

            char digit = name[marker + 4];
            return char.IsDigit(digit) ? digit - '0' : 0;
        }

        private static Mesh CreateLodMesh(
            Mesh source,
            int lodIndex)
        {
            if (lodIndex >= 3)
            {
                return CreateBoundsProxy(source.bounds);
            }

            if (source.name.IndexOf(
                    "Cylinder",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (lodIndex == 1)
                {
                    return CreateCylinder(source.bounds, 10);
                }
                if (lodIndex == 2)
                {
                    return CreateCylinder(source.bounds, 6);
                }
            }

            return Object.Instantiate(source);
        }

        private static Mesh CreateCylinder(Bounds bounds, int sides)
        {
            var vertices = new Vector3[sides * 2 + 2];
            var uv = new Vector2[vertices.Length];
            float bottom = bounds.min.y;
            float top = bounds.max.y;
            vertices[sides * 2] =
                new Vector3(bounds.center.x, bottom, bounds.center.z);
            vertices[sides * 2 + 1] =
                new Vector3(bounds.center.x, top, bounds.center.z);
            uv[sides * 2] = new Vector2(0.5f, 0.5f);
            uv[sides * 2 + 1] = new Vector2(0.5f, 0.5f);

            for (int index = 0; index < sides; index++)
            {
                float angle = index * Mathf.PI * 2f / sides;
                float x = bounds.center.x +
                    Mathf.Cos(angle) * bounds.extents.x;
                float z = bounds.center.z +
                    Mathf.Sin(angle) * bounds.extents.z;
                vertices[index] = new Vector3(x, bottom, z);
                vertices[index + sides] = new Vector3(x, top, z);
                uv[index] =
                    new Vector2(index / (float)sides, 0f);
                uv[index + sides] =
                    new Vector2(index / (float)sides, 1f);
            }

            var triangles = new int[sides * 12];
            int triangleIndex = 0;
            for (int index = 0; index < sides; index++)
            {
                int next = (index + 1) % sides;
                triangles[triangleIndex++] = index;
                triangles[triangleIndex++] = index + sides;
                triangles[triangleIndex++] = next + sides;
                triangles[triangleIndex++] = index;
                triangles[triangleIndex++] = next + sides;
                triangles[triangleIndex++] = next;

                triangles[triangleIndex++] = sides * 2;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = index;
                triangles[triangleIndex++] = sides * 2 + 1;
                triangles[triangleIndex++] = index + sides;
                triangles[triangleIndex++] = next + sides;
            }

            var mesh = new Mesh
            {
                name = $"Cylinder_{sides}",
                vertices = vertices,
                triangles = triangles,
                uv = uv
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateBoundsProxy(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            var vertices = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z)
            };
            var triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                1, 6, 5, 1, 2, 6,
                5, 7, 4, 5, 6, 7,
                4, 3, 0, 4, 7, 3,
                3, 6, 2, 3, 7, 6,
                4, 1, 5, 4, 0, 1
            };
            var mesh = new Mesh
            {
                name = "BoundsProxy",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void RemapUvs(Mesh mesh, int cell)
        {
            Vector2[] sourceUvs = mesh.uv;
            if (sourceUvs == null || sourceUvs.Length != mesh.vertexCount)
            {
                sourceUvs = new Vector2[mesh.vertexCount];
            }

            int column = cell % 4;
            int row = cell / 4;
            const float padding = 0.018f;
            float cellWidth = 0.25f;
            float cellHeight = 0.5f;
            var remapped = new Vector2[sourceUvs.Length];
            for (int index = 0; index < sourceUvs.Length; index++)
            {
                float u = Mathf.Repeat(sourceUvs[index].x, 1f);
                float v = Mathf.Repeat(sourceUvs[index].y, 1f);
                remapped[index] = new Vector2(
                    column * cellWidth +
                        Mathf.Lerp(padding, cellWidth - padding, u),
                    row * cellHeight +
                        Mathf.Lerp(padding, cellHeight - padding, v));
            }
            mesh.uv = remapped;
        }

        private static bool ShouldInclude(
            MeshFilter filter,
            int lodIndex)
        {
            if (lodIndex == 0)
            {
                return true;
            }

            Vector3 size = filter.GetComponent<Renderer>().bounds.size;
            float maximumDimension =
                Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            string name = filter.gameObject.name;

            if (lodIndex == 1)
            {
                return maximumDimension >= 0.36f ||
                    IsProtectedSilhouette(name);
            }
            if (lodIndex == 2)
            {
                return maximumDimension >= 0.92f ||
                    IsProtectedSilhouette(name);
            }

            return maximumDimension >= 1.72f ||
                IsFarSilhouette(name);
        }

        private static bool IsProtectedSilhouette(string name)
        {
            return ContainsAny(
                name,
                "Plinth",
                "Masonry",
                "Roof",
                "Root",
                "Annex",
                "Bay",
                "Entrance",
                "Canopy",
                "GrowthFrame",
                "LanternCore",
                "LanternBase",
                "CrownRoof",
                "FinalRootLock");
        }

        private static bool IsFarSilhouette(string name)
        {
            return ContainsAny(
                name,
                "StonePlinth",
                "Masonry",
                "RoofWest",
                "RoofEast",
                "RootBase",
                "RootVault",
                "AnnexPlinth",
                "AnnexRoof",
                "UpperGraftBay",
                "SecondaryRoof",
                "GrowthFrameCrown",
                "RepairArcadeRoof",
                "LogisticsBay",
                "LogisticsRoof",
                "CrownRoof",
                "LanternCore",
                "LanternBase",
                "FinalRootLock");
        }

        private static bool ContainsAny(
            string value,
            params string[] fragments)
        {
            return fragments.Any(
                fragment => value.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsAccent(Material material)
        {
            string name = material == null ? string.Empty : material.name;
            return ContainsAny(
                name,
                "LivingSap",
                "BasinWater",
                "Water");
        }

        private static int ResolveAtlasCell(Material material)
        {
            string name = material == null ? string.Empty : material.name;
            if (ContainsAny(name, "PaleStone"))
            {
                return 0;
            }
            if (ContainsAny(name, "RootBark"))
            {
                return 1;
            }
            if (ContainsAny(name, "DarkTimber"))
            {
                return 2;
            }
            if (ContainsAny(name, "WeatheredBronze"))
            {
                return 3;
            }
            if (ContainsAny(name, "LeafRoof"))
            {
                return 4;
            }
            if (ContainsAny(name, "LivingLeaf", "Moss"))
            {
                return 5;
            }
            if (ContainsAny(name, "Ground"))
            {
                return 6;
            }
            return 7;
        }

        private static Mesh PersistMesh(Mesh generated, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                generated.UploadMeshData(true);
                EditorUtility.SetDirty(generated);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            existing.UploadMeshData(true);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Texture2D CreateAtlas()
        {
            var texture = new Texture2D(
                AtlasSize,
                AtlasSize,
                TextureFormat.RGB24,
                true,
                false)
            {
                name = "T_Eldergrove_Workshop_Atlas_1024",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[AtlasSize * AtlasSize];
            for (int y = 0; y < AtlasSize; y++)
            {
                for (int x = 0; x < AtlasSize; x++)
                {
                    int cell = x / (AtlasSize / 4) +
                        (y / (AtlasSize / 2)) * 4;
                    Color32 baseColor = AtlasColors[cell];
                    int hash = (x * 31 + y * 17 + cell * 47) & 15;
                    int variation = hash - 7;
                    pixels[y * AtlasSize + x] = new Color32(
                        ClampByte(baseColor.r + variation),
                        ClampByte(baseColor.g + variation),
                        ClampByte(baseColor.b + variation),
                        255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);

            string absolutePath = Path.GetFullPath(AtlasPath);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(
                AtlasPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var importer =
                AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity did not create the Eldergrove atlas importer.");
            }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = AtlasSize;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        }

        private static byte ClampByte(int value)
        {
            return (byte)Mathf.Clamp(value, 0, 255);
        }

        private static Material CreateOrUpdateMaterial(
            string path,
            Texture texture,
            Color color,
            Color emission)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The built-in Standard shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            material.mainTexture = texture;
            material.color = color;
            material.enableInstancing = true;
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.renderQueue = -1;
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetFloat("_Metallic", 0.12f);
            material.SetFloat("_Glossiness", 0.34f);

            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureRenderer(
            MeshRenderer renderer,
            int lodIndex)
        {
            renderer.shadowCastingMode = lodIndex <= 1
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            renderer.receiveShadows = lodIndex <= 2;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void CreateOrUpdateCatalog(GameObject prefab)
        {
            ArchitectureConstructionAnimationProfile motionProfile =
                AssetDatabase.LoadAssetAtPath<
                    ArchitectureConstructionAnimationProfile>(
                        MotionProfilePath);
            if (motionProfile == null || !motionProfile.IsConfigured)
            {
                throw new InvalidOperationException(
                    "The Eldergrove realm motion profile is missing or invalid.");
            }

            KingdomBuildingModelCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    KingdomBuildingModelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<
                        KingdomBuildingModelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = catalog.Entries
                .Where(entry =>
                    entry != null &&
                    !(entry.RealmId == RealmId.Eldergrove &&
                        string.Equals(
                            entry.BuildingId,
                            BuildingId,
                            StringComparison.Ordinal)))
                .ToList();
            entries.Add(new KingdomBuildingModelEntry(
                ModelId,
                RealmId.Eldergrove,
                BuildingId,
                prefab,
                motionProfile,
                StrategicBoardScale,
                1,
                10));
            catalog.Configure(entries.ToArray());
            EditorUtility.SetDirty(catalog);
        }

        private static void CreatePreviewScene(GameObject prefab)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            InstantiatePreview(prefab, 1, new Vector3(-8.3f, 0f, 0f));
            InstantiatePreview(prefab, 6, Vector3.zero);
            InstantiatePreview(prefab, 10, new Vector3(8.7f, 0f, 0f));

            GameObject ground =
                GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ReviewGround";
            ground.transform.localScale =
                new Vector3(3.1f, 1f, 1.05f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            Material groundMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/AL/Art/Generated/Architecture/" +
                    "Eldergrove/Materials/MAT_Eldergrove_Ground.mat");
            if (groundMaterial != null)
            {
                ground.GetComponent<Renderer>().sharedMaterial =
                    groundMaterial;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor =
                new Color(0.025f, 0.052f, 0.068f);
            camera.transform.position =
                new Vector3(14.8f, 10.6f, -24.8f);
            camera.transform.LookAt(new Vector3(0f, 2.25f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.82f, 0.56f);
            key.intensity = 1.72f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation =
                Quaternion.Euler(46f, -32f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.42f, 0.58f, 0.4f);
            fill.intensity = 0.62f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation =
                Quaternion.Euler(28f, 146f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.19f, 0.2f, 0.145f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void InstantiatePreview(
            GameObject prefab,
            int level,
            Vector3 position)
        {
            var instance =
                (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"Workshop_Level{level:D2}";
            instance.transform.position = position;
            KingdomBuildingLevelModel model =
                instance.GetComponent<KingdomBuildingLevelModel>();
            if (model == null || !model.ApplyConfirmedLevel(level))
            {
                throw new InvalidOperationException(
                    $"Could not preview Eldergrove level {level}.");
            }
        }

        private static void WriteFrame(
            Camera camera,
            int width,
            int height,
            string path)
        {
            var target = new RenderTexture(width, height, 24);
            var capture = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(
                    new Rect(0f, 0f, width, height),
                    0,
                    0);
                capture.Apply();
                string outputPath = Path.GetFullPath(path);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(
                    outputPath,
                    capture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(target);
            }
        }

        private static Transform CreateGroup(
            Transform parent,
            string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Eldergrove/" +
                "Production",
                "Runtime");
            EnsureFolder(RuntimeFolder, "Meshes");
            EnsureFolder(
                "Assets/AL/ScriptableObjects",
                "Resources");
        }

        private static void EnsureFolder(
            string parent,
            string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private sealed class MeshSource
        {
            public MeshSource(
                MeshFilter filter,
                Material material,
                int atlasCell)
            {
                Filter = filter;
                Material = material;
                AtlasCell = atlasCell;
            }

            public MeshFilter Filter { get; }
            public Material Material { get; }
            public int AtlasCell { get; }
        }
    }
}
