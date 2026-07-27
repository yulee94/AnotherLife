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
    /// Builds the final cumulative Umbral Veilwright production model
    /// from the approved grounded veilwright atelier direction.
    /// </summary>
    public static class UmbralVeilwrightProductionModelBuilder
    {
        public const string RuntimeFolder =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/Runtime";
        public const string MeshFolder = RuntimeFolder + "/Meshes";
        public const string AtlasPath =
            RuntimeFolder + "/T_Umbral_Veilwright_Atlas_1024.png";
        public const string AtlasMaterialPath =
            RuntimeFolder + "/MAT_Umbral_Veilwright_Atlas.mat";
        public const string AccentMaterialPath =
            RuntimeFolder + "/MAT_Umbral_Veilwright_Violet.mat";
        public const string PrefabPath =
            RuntimeFolder + "/Umbral_Veilwright_Production.prefab";
        public const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "UmbralVeilwrightProductionModel.unity";
        public const string ModelId =
            "building.umbral.workshop.production.v1";
        public const string LevelTenCapstoneName =
            "Bound Eclipse Yoke";

        private const string BuildingId = "Workshop";
        private const int AtlasSize = 1024;
        private const float StrategicBoardScale = 0.12f;

        private static readonly Vector3 SlotEnvelope =
            new Vector3(10f, 7.8f, 8f);
        private static readonly Vector3 MaximumArtBounds =
            new Vector3(9.55f, 7.45f, 7.25f);
        private static readonly float[] LodTransitions =
            { 0.60f, 0.30f, 0.12f, 0.04f };

        private static readonly Color32[] AtlasColors =
        {
            new Color32(94, 94, 100, 255),
            new Color32(128, 126, 132, 255),
            new Color32(66, 65, 72, 255),
            new Color32(126, 74, 132, 255),
            new Color32(84, 70, 92, 255),
            new Color32(150, 112, 58, 255),
            new Color32(176, 172, 160, 255),
            new Color32(162, 82, 214, 255)
        };

        [MenuItem(
            "Another Life/Architecture/" +
            "Build Umbral Veilwright Production Model")]
        public static void Build()
        {
            EnsureFolders();
            Texture2D atlas = CreateAtlas();
            Material atlasMaterial = CreateOrUpdateMaterial(
                AtlasMaterialPath,
                atlas,
                new Color(0.62f, 0.61f, 0.65f),
                Color.black);
            Material accentMaterial = CreateOrUpdateMaterial(
                AccentMaterialPath,
                null,
                new Color(0.50f, 0.18f, 0.78f),
                new Color(0.18f, 0.035f, 0.30f));

            GameObject production =
                BuildProductionObject(atlasMaterial, accentMaterial);
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
                    "Unity did not save the Umbral production prefab.");
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
                    "The Umbral production review camera is missing.");
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
                    "umbral-veilwright-production",
                    "render.png"));
        }

        public static void ReportMetricsFromCommandLine()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "The Umbral production prefab is missing.");
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
                    $"UMBRAL_METRIC LOD{lodIndex} " +
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
                    for (int index = 1; index < renderers.Length; index++)
                    {
                        bounds.Encapsulate(renderers[index].bounds);
                    }
                    Debug.Log(
                        $"UMBRAL_BOUNDS level={level} " +
                        $"size={bounds.size} center={bounds.center}");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static GameObject BuildProductionObject(
            Material atlasMaterial,
            Material accentMaterial)
        {
            var production =
                new GameObject("Umbral_Veilwright_Production");
            var lodRoots = new Transform[4];
            var levelObjects = new GameObject[10, 4];
            var partsByLevel = new List<PartSpec>[10];
            for (int level = 1; level <= 10; level++)
            {
                partsByLevel[level - 1] = CreateLevelParts(level);
            }

            try
            {
                for (int lodIndex = 0; lodIndex < 3; lodIndex++)
                {
                    lodRoots[lodIndex] =
                        CreateGroup(production.transform, $"LOD{lodIndex}");
                    for (int level = 1; level <= 10; level++)
                    {
                        levelObjects[level - 1, lodIndex] =
                            CreateLevelObject(
                                lodRoots[lodIndex],
                                partsByLevel[level - 1],
                                level,
                                lodIndex,
                                atlasMaterial,
                                accentMaterial,
                                false);
                    }
                }

                lodRoots[3] =
                    CreateGroup(production.transform, "LOD3");
                CreateFarMilestone(
                    lodRoots[3],
                    partsByLevel,
                    1,
                    1,
                    1,
                    levelObjects,
                    atlasMaterial,
                    accentMaterial);
                CreateFarMilestone(
                    lodRoots[3],
                    partsByLevel,
                    2,
                    6,
                    6,
                    levelObjects,
                    atlasMaterial,
                    accentMaterial);
                CreateFarMilestone(
                    lodRoots[3],
                    partsByLevel,
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
                for (int lodIndex = 0;
                    lodIndex < lodRoots.Length;
                    lodIndex++)
                {
                    lods[lodIndex] = new LOD(
                        LodTransitions[lodIndex],
                        lodRoots[lodIndex]
                            .GetComponentsInChildren<Renderer>(true));
                }
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();

                BoxCollider selection =
                    production.AddComponent<BoxCollider>();
                selection.isTrigger = true;
                selection.center = new Vector3(0f, 3.78f, 0f);
                selection.size = new Vector3(9.8f, 7.56f, 7.4f);

                BoxCollider navigation =
                    production.AddComponent<BoxCollider>();
                navigation.isTrigger = false;
                navigation.center = new Vector3(0f, 0.75f, 0f);
                navigation.size = new Vector3(9.4f, 1.5f, 6.8f);

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
                    deltas[levelIndex] =
                        new KingdomBuildingLevelDelta(
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
        }

        private static void CreateFarMilestone(
            Transform lodRoot,
            IReadOnlyList<List<PartSpec>> partsByLevel,
            int firstLevel,
            int lastLevel,
            int activationLevel,
            GameObject[,] levelObjects,
            Material atlasMaterial,
            Material accentMaterial)
        {
            var parts = new List<PartSpec>();
            for (int level = firstLevel; level <= lastLevel; level++)
            {
                parts.AddRange(partsByLevel[level - 1]);
            }
            levelObjects[activationLevel - 1, 3] =
                CreateLevelObject(
                    lodRoot,
                    parts,
                    activationLevel,
                    3,
                    atlasMaterial,
                    accentMaterial,
                    true);
        }

        private static GameObject CreateLevelObject(
            Transform lodRoot,
            IReadOnlyList<PartSpec> parts,
            int level,
            int lodIndex,
            Material atlasMaterial,
            Material accentMaterial,
            bool farOnly)
        {
            PartSpec[] retained = parts
                .Where(part =>
                    farOnly
                        ? part.RetainAtFar
                        : part.MaximumLod >= lodIndex)
                .ToArray();
            if (retained.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Umbral LOD{lodIndex} Level {level} has no mesh.");
            }

            MeshBuildResult result = BuildMesh(
                retained,
                lodIndex,
                $"M_Umbral_Veilwright_LOD{lodIndex}_L{level:D2}");
            string meshPath =
                $"{MeshFolder}/M_Umbral_Veilwright_" +
                $"LOD{lodIndex}_L{level:D2}.asset";

            var levelObject = new GameObject($"L{level:D2}_Delta");
            levelObject.transform.SetParent(lodRoot, false);
            levelObject.AddComponent<MeshFilter>().sharedMesh =
                PersistMesh(result.Mesh, meshPath);
            MeshRenderer renderer =
                levelObject.AddComponent<MeshRenderer>();
            var materials = new List<Material>();
            if (result.HasStructural)
            {
                materials.Add(atlasMaterial);
            }
            if (result.HasAccent)
            {
                materials.Add(accentMaterial);
            }
            renderer.sharedMaterials = materials.ToArray();
            ConfigureRenderer(renderer, lodIndex);
            return levelObject;
        }

        private static MeshBuildResult BuildMesh(
            IReadOnlyList<PartSpec> parts,
            int lodIndex,
            string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var structuralTriangles = new List<int>();
            var accentTriangles = new List<int>();

            foreach (PartSpec part in parts)
            {
                List<int> triangles = part.IsAccent
                    ? accentTriangles
                    : structuralTriangles;
                if (part.Shape == PartShape.Cylinder)
                {
                    int sides = lodIndex <= 0
                        ? 12
                        : lodIndex == 1
                            ? 8
                            : lodIndex == 2
                                ? 6
                                : 4;
                    AppendCylinder(
                        part,
                        sides,
                        vertices,
                        normals,
                        uvs,
                        triangles);
                }
                else
                {
                    AppendBox(
                        part,
                        vertices,
                        normals,
                        uvs,
                        triangles);
                }
            }

            var mesh = new Mesh
            {
                name = name,
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            int subMeshCount = 0;
            if (structuralTriangles.Count > 0)
            {
                subMeshCount++;
            }
            if (accentTriangles.Count > 0)
            {
                subMeshCount++;
            }
            mesh.subMeshCount = subMeshCount;
            int subMesh = 0;
            if (structuralTriangles.Count > 0)
            {
                mesh.SetTriangles(
                    structuralTriangles,
                    subMesh++,
                    false);
            }
            if (accentTriangles.Count > 0)
            {
                mesh.SetTriangles(
                    accentTriangles,
                    subMesh,
                    false);
            }
            mesh.RecalculateBounds();
            return new MeshBuildResult(
                mesh,
                structuralTriangles.Count > 0,
                accentTriangles.Count > 0);
        }

        private static void AppendBox(
            PartSpec part,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            Vector3 half = part.Size * 0.5f;
            AddFace(part, Vector3.forward, Vector3.right, Vector3.up,
                half.z, half.x, half.y,
                vertices, normals, uvs, triangles);
            AddFace(part, Vector3.back, Vector3.left, Vector3.up,
                half.z, half.x, half.y,
                vertices, normals, uvs, triangles);
            AddFace(part, Vector3.right, Vector3.back, Vector3.up,
                half.x, half.z, half.y,
                vertices, normals, uvs, triangles);
            AddFace(part, Vector3.left, Vector3.forward, Vector3.up,
                half.x, half.z, half.y,
                vertices, normals, uvs, triangles);
            AddFace(part, Vector3.up, Vector3.right, Vector3.back,
                half.y, half.x, half.z,
                vertices, normals, uvs, triangles);
            AddFace(part, Vector3.down, Vector3.right, Vector3.forward,
                half.y, half.x, half.z,
                vertices, normals, uvs, triangles);
        }

        private static void AddFace(
            PartSpec part,
            Vector3 localNormal,
            Vector3 localRight,
            Vector3 localUp,
            float normalExtent,
            float rightExtent,
            float upExtent,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            int start = vertices.Count;
            Vector3 faceCenter = localNormal * normalExtent;
            Vector3[] corners =
            {
                faceCenter - localRight * rightExtent -
                    localUp * upExtent,
                faceCenter - localRight * rightExtent +
                    localUp * upExtent,
                faceCenter + localRight * rightExtent +
                    localUp * upExtent,
                faceCenter + localRight * rightExtent -
                    localUp * upExtent
            };
            Vector2[] faceUvs = AtlasUvs(part.AtlasCell);
            for (int index = 0; index < 4; index++)
            {
                vertices.Add(
                    part.Center + part.Rotation * corners[index]);
                normals.Add(part.Rotation * localNormal);
                uvs.Add(faceUvs[index]);
            }
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
        }

        private static void AppendCylinder(
            PartSpec part,
            int sides,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            float radiusX = part.Size.x * 0.5f;
            float radiusZ = part.Size.z * 0.5f;
            float bottom = -part.Size.y * 0.5f;
            float top = part.Size.y * 0.5f;
            Vector2[] atlasUvs = AtlasUvs(part.AtlasCell);

            for (int side = 0; side < sides; side++)
            {
                float angle0 = side * Mathf.PI * 2f / sides;
                float angle1 = (side + 1) * Mathf.PI * 2f / sides;
                Vector3 lower0 = new Vector3(
                    Mathf.Cos(angle0) * radiusX,
                    bottom,
                    Mathf.Sin(angle0) * radiusZ);
                Vector3 lower1 = new Vector3(
                    Mathf.Cos(angle1) * radiusX,
                    bottom,
                    Mathf.Sin(angle1) * radiusZ);
                Vector3 upper0 =
                    new Vector3(lower0.x, top, lower0.z);
                Vector3 upper1 =
                    new Vector3(lower1.x, top, lower1.z);
                Vector3 normal0 =
                    new Vector3(
                        Mathf.Cos(angle0),
                        0f,
                        Mathf.Sin(angle0)).normalized;
                Vector3 normal1 =
                    new Vector3(
                        Mathf.Cos(angle1),
                        0f,
                        Mathf.Sin(angle1)).normalized;

                int start = vertices.Count;
                AddCylinderVertex(part, lower0, normal0,
                    atlasUvs[0], vertices, normals, uvs);
                AddCylinderVertex(part, upper0, normal0,
                    atlasUvs[1], vertices, normals, uvs);
                AddCylinderVertex(part, upper1, normal1,
                    atlasUvs[2], vertices, normals, uvs);
                AddCylinderVertex(part, lower1, normal1,
                    atlasUvs[3], vertices, normals, uvs);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);

                AddCylinderCap(
                    part,
                    lower0,
                    lower1,
                    bottom,
                    false,
                    atlasUvs,
                    vertices,
                    normals,
                    uvs,
                    triangles);
                AddCylinderCap(
                    part,
                    upper0,
                    upper1,
                    top,
                    true,
                    atlasUvs,
                    vertices,
                    normals,
                    uvs,
                    triangles);
            }
        }

        private static void AddCylinderCap(
            PartSpec part,
            Vector3 edge0,
            Vector3 edge1,
            float y,
            bool top,
            Vector2[] atlasUvs,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            int start = vertices.Count;
            Vector3 normal = top ? Vector3.up : Vector3.down;
            AddCylinderVertex(
                part,
                new Vector3(0f, y, 0f),
                normal,
                (atlasUvs[0] + atlasUvs[2]) * 0.5f,
                vertices,
                normals,
                uvs);
            AddCylinderVertex(
                part,
                edge0,
                normal,
                atlasUvs[0],
                vertices,
                normals,
                uvs);
            AddCylinderVertex(
                part,
                edge1,
                normal,
                atlasUvs[3],
                vertices,
                normals,
                uvs);
            triangles.Add(start);
            if (top)
            {
                triangles.Add(start + 2);
                triangles.Add(start + 1);
            }
            else
            {
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
        }

        private static void AddCylinderVertex(
            PartSpec part,
            Vector3 vertex,
            Vector3 normal,
            Vector2 uv,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs)
        {
            vertices.Add(part.Center + part.Rotation * vertex);
            normals.Add(part.Rotation * normal);
            uvs.Add(uv);
        }

        private static Vector2[] AtlasUvs(int cell)
        {
            int column = cell % 4;
            int row = cell / 4;
            const float cellWidth = 0.25f;
            const float cellHeight = 0.5f;
            const float padding = 0.018f;
            float minU = column * cellWidth + padding;
            float maxU =
                column * cellWidth + cellWidth - padding;
            float minV = row * cellHeight + padding;
            float maxV =
                row * cellHeight + cellHeight - padding;
            return new[]
            {
                new Vector2(minU, minV),
                new Vector2(minU, maxV),
                new Vector2(maxU, maxV),
                new Vector2(maxU, minV)
            };
        }

        private static List<PartSpec> CreateLevelParts(int level)
        {
            var parts = new List<PartSpec>();
            switch (level)
            {
                case 1:
                    AddLevelOne(parts);
                    break;
                case 2:
                    AddLevelTwo(parts);
                    break;
                case 3:
                    AddLevelThree(parts);
                    break;
                case 4:
                    AddLevelFour(parts);
                    break;
                case 5:
                    AddLevelFive(parts);
                    break;
                case 6:
                    AddLevelSix(parts);
                    break;
                case 7:
                    AddLevelSeven(parts);
                    break;
                case 8:
                    AddLevelEight(parts);
                    break;
                case 9:
                    AddLevelNine(parts);
                    break;
                case 10:
                    AddLevelTen(parts);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
            return parts;
        }

        private static void AddLevelOne(List<PartSpec> parts)
        {
            Box(parts, "GraphiteFoundation", new Vector3(0f, 0.18f, 0f),
                new Vector3(6.15f, 0.36f, 5.25f), 0, 2, true);
            Box(parts, "AshInsetFloor", new Vector3(-0.10f, 0.40f, 0f),
                new Vector3(5.45f, 0.16f, 4.55f), 1, 1);
            Box(parts, "ProtectedCentralNegativeSpace",
                new Vector3(0.18f, 0.58f, -0.12f),
                new Vector3(1.38f, 0.08f, 1.62f), 2, 1);
            Box(parts, "BackGraphiteWall", new Vector3(-0.10f, 1.52f, 1.98f),
                new Vector3(5.25f, 2.28f, 0.44f), 1, 2, true);
            Box(parts, "WestGraphiteWall", new Vector3(-2.42f, 1.44f, 0.10f),
                new Vector3(0.44f, 2.12f, 3.60f), 1, 2);
            Box(parts, "EastGraphiteWall", new Vector3(2.28f, 1.38f, 0.02f),
                new Vector3(0.42f, 1.95f, 3.45f), 1, 2);
            Box(parts, "ObliqueEntranceWestJamb",
                new Vector3(-1.18f, 1.50f, -2.18f),
                new Vector3(0.30f, 2.10f, 0.42f), 6, 1,
                false,
                Quaternion.Euler(0f, 0f, -9f));
            Box(parts, "ObliqueEntranceEastJamb",
                new Vector3(1.05f, 1.42f, -2.28f),
                new Vector3(0.30f, 1.92f, 0.42f), 6, 1,
                false,
                Quaternion.Euler(0f, 0f, 7f));
            Box(parts, "EntranceMasonryWestShoulder",
                new Vector3(-1.70f, 1.10f, -2.04f),
                new Vector3(0.46f, 1.34f, 0.56f), 1, 0,
                false,
                Quaternion.Euler(0f, 0f, -5f));
            Box(parts, "EntranceMasonryEastShoulder",
                new Vector3(1.50f, 1.04f, -2.14f),
                new Vector3(0.46f, 1.22f, 0.56f), 1, 0,
                false,
                Quaternion.Euler(0f, 0f, 4f));
            Box(parts, "PointedEntranceLeftLintel",
                new Vector3(-0.52f, 2.66f, -2.26f),
                new Vector3(1.46f, 0.26f, 0.42f), 6, 2, true,
                Quaternion.Euler(0f, 0f, 26f));
            Box(parts, "PointedEntranceRightLintel",
                new Vector3(0.52f, 2.66f, -2.26f),
                new Vector3(1.46f, 0.26f, 0.42f), 6, 2,
                false,
                Quaternion.Euler(0f, 0f, -26f));
            Box(parts, "WestSteepSplitRoof",
                new Vector3(-1.44f, 3.72f, -0.08f),
                new Vector3(2.88f, 0.44f, 4.82f), 2, 2, true,
                Quaternion.Euler(0f, 0f, 27f));
            Box(parts, "EastSteepSplitRoof",
                new Vector3(1.24f, 3.88f, 0.06f),
                new Vector3(2.42f, 0.40f, 4.55f), 4, 2, true,
                Quaternion.Euler(0f, 0f, -32f));
            Box(parts, "OffsetRoofShadowRidge",
                new Vector3(-0.12f, 4.52f, 0f),
                new Vector3(0.44f, 0.44f, 4.88f), 2, 1,
                false,
                Quaternion.Euler(0f, 0f, -3f));
            Box(parts, "WestRoofVisibleSupportStrutFront",
                new Vector3(-2.34f, 2.58f, -1.72f),
                new Vector3(0.18f, 1.78f, 0.18f), 6, 0,
                false,
                Quaternion.Euler(0f, 0f, -20f));
            Box(parts, "WestRoofVisibleSupportStrutRear",
                new Vector3(-2.28f, 2.58f, 1.56f),
                new Vector3(0.18f, 1.72f, 0.18f), 6, 0,
                false,
                Quaternion.Euler(0f, 0f, -20f));
            Box(parts, "EastRoofVisibleSupportStrutFront",
                new Vector3(2.02f, 2.56f, -1.58f),
                new Vector3(0.18f, 1.62f, 0.18f), 6, 0,
                false,
                Quaternion.Euler(0f, 0f, 22f));
            Box(parts, "EastRoofVisibleSupportStrutRear",
                new Vector3(1.96f, 2.56f, 1.46f),
                new Vector3(0.18f, 1.56f, 0.18f), 6, 0,
                false,
                Quaternion.Euler(0f, 0f, 22f));
            Box(parts, "LowOffsetWardChimney",
                new Vector3(-2.12f, 4.50f, 1.32f),
                new Vector3(0.72f, 1.20f, 0.68f), 2, 2, true);
            Box(parts, "WardChimneyCap",
                new Vector3(-2.12f, 5.15f, 1.32f),
                new Vector3(1.02f, 0.20f, 0.96f), 5, 0);
            Box(parts, "DarkglassCalibrationTable",
                new Vector3(0.10f, 1.00f, -0.10f),
                new Vector3(1.92f, 0.58f, 1.34f), 4, 2);
            Box(parts, "GroundedSealingTableFoot",
                new Vector3(0.10f, 0.54f, -0.10f),
                new Vector3(1.42f, 0.34f, 0.92f), 2, 0);
            Box(parts, "LocalizedVioletFocus",
                new Vector3(0.10f, 1.34f, -0.78f),
                new Vector3(0.64f, 0.30f, 0.08f), 7, 2, false,
                Quaternion.identity, true);
        }

        private static void AddLevelTwo(List<PartSpec> parts)
        {
            for (int index = 0; index < 6; index++)
            {
                float x = Mathf.Lerp(-2.40f, 2.20f, index / 5f);
                Box(parts, $"AshWallCourse_{index:D2}",
                    new Vector3(x, 1.96f, 1.72f),
                    new Vector3(0.64f, 0.18f, 0.10f), 6, 0);
            }
            Box(parts, "NorthWestGroundLockCue",
                new Vector3(-2.72f, 0.54f, 2.34f),
                new Vector3(0.36f, 0.34f, 0.36f), 5, 2);
            Box(parts, "NorthEastGroundLockCue",
                new Vector3(2.62f, 0.54f, 2.34f),
                new Vector3(0.36f, 0.34f, 0.36f), 5, 2);
            Box(parts, "SouthWestGroundLockCue",
                new Vector3(-2.72f, 0.54f, -2.34f),
                new Vector3(0.36f, 0.34f, 0.36f), 5, 2);
            Box(parts, "SouthEastGroundLockCue",
                new Vector3(2.62f, 0.54f, -2.34f),
                new Vector3(0.36f, 0.34f, 0.36f), 5, 2);
            Box(parts, "WestAshEdge",
                new Vector3(-2.70f, 1.42f, 0.08f),
                new Vector3(0.12f, 1.82f, 3.22f), 6, 0);
            Box(parts, "EastAshEdge",
                new Vector3(2.52f, 1.36f, 0.02f),
                new Vector3(0.12f, 1.70f, 3.08f), 6, 0);
        }

        private static void AddLevelThree(List<PartSpec> parts)
        {
            Box(parts, "WestAubergineReliquaryFloor",
                new Vector3(-3.38f, 0.24f, -0.18f),
                new Vector3(1.72f, 0.34f, 3.28f), 0, 2);
            Box(parts, "WestReliquaryBack",
                new Vector3(-3.52f, 1.22f, 1.08f),
                new Vector3(1.42f, 1.66f, 0.34f), 3, 2);
            Box(parts, "WestReliquaryOuterWall",
                new Vector3(-4.05f, 1.20f, -0.18f),
                new Vector3(0.34f, 1.62f, 2.58f), 3, 1);
            Box(parts, "WestAubergineShelterRoof",
                new Vector3(-3.44f, 2.24f, -0.12f),
                new Vector3(2.10f, 0.30f, 3.46f), 3, 1,
                false, Quaternion.Euler(0f, 0f, 9f));
            Box(parts, "WestAubergineShelterFace",
                new Vector3(-3.50f, 1.42f, -1.62f),
                new Vector3(1.72f, 0.92f, 0.12f), 3, 0);
            Box(parts, "WestReliquaryPlinth",
                new Vector3(-3.30f, 0.82f, -0.84f),
                new Vector3(0.92f, 0.58f, 0.78f), 5, 0);
            for (int index = 0; index < 5; index++)
            {
                float z = Mathf.Lerp(-1.42f, 0.92f, index / 4f);
                Box(parts, $"WestReliquaryRoofBatten_{index:D2}",
                    new Vector3(-3.36f, 2.38f, z),
                    new Vector3(1.66f, 0.08f, 0.10f), 5, 0,
                    false, Quaternion.Euler(0f, 0f, 9f));
            }
        }

        private static void AddLevelFour(List<PartSpec> parts)
        {
            Box(parts, "FormalObliqueThresholdBase",
                new Vector3(0.02f, 0.42f, -2.72f),
                new Vector3(2.86f, 0.24f, 0.38f), 6, 2);
            Box(parts, "FormalThresholdWestBlade",
                new Vector3(-1.40f, 1.68f, -2.72f),
                new Vector3(0.28f, 2.48f, 0.32f), 6, 0,
                false, Quaternion.Euler(0f, 0f, -13f));
            Box(parts, "FormalThresholdEastBlade",
                new Vector3(1.24f, 1.62f, -2.84f),
                new Vector3(0.28f, 2.36f, 0.32f), 6, 0,
                false, Quaternion.Euler(0f, 0f, 10f));
            Box(parts, "PointedThresholdCrownLeft",
                new Vector3(-0.52f, 3.00f, -2.78f),
                new Vector3(1.54f, 0.22f, 0.32f), 6, 2, true,
                Quaternion.Euler(0f, 0f, 28f));
            Box(parts, "PointedThresholdCrownRight",
                new Vector3(0.52f, 3.00f, -2.78f),
                new Vector3(1.54f, 0.22f, 0.32f), 6, 1,
                false,
                Quaternion.Euler(0f, 0f, -28f));
            Box(parts, "ShadowedSidePassage",
                new Vector3(2.52f, 0.72f, -2.22f),
                new Vector3(1.04f, 0.28f, 1.18f), 2, 1);
        }

        private static void AddLevelFive(List<PartSpec> parts)
        {
            Vector3[] anchors =
            {
                new Vector3(-3.05f, 0.92f, -2.48f),
                new Vector3(3.00f, 0.92f, -2.32f),
                new Vector3(-3.08f, 0.92f, 2.36f),
                new Vector3(2.88f, 0.92f, 2.48f)
            };
            for (int index = 0; index < anchors.Length; index++)
            {
                Box(parts, $"GroundedAnchorPylon_{index:D2}",
                    anchors[index],
                    new Vector3(0.52f, 1.34f, 0.52f), 5, 2,
                    index == 0 || index == 2);
                Box(parts, $"AnchorAshCap_{index:D2}",
                    anchors[index] + Vector3.up * 0.78f,
                    new Vector3(0.68f, 0.22f, 0.68f), 6, 1);
            }
            Box(parts, "NorthWestAnchorAimChannel",
                new Vector3(-1.54f, 0.74f, 0.78f),
                new Vector3(0.12f, 0.10f, 4.06f), 5, 0,
                false, Quaternion.Euler(0f, -42f, 0f));
            Box(parts, "NorthEastAnchorAimChannel",
                new Vector3(1.48f, 0.74f, 0.82f),
                new Vector3(0.12f, 0.10f, 4.00f), 5, 0,
                false, Quaternion.Euler(0f, 42f, 0f));
            Box(parts, "SouthWestAnchorAimChannel",
                new Vector3(-1.50f, 0.75f, -1.46f),
                new Vector3(0.12f, 0.10f, 3.56f), 5, 0,
                false, Quaternion.Euler(0f, 47f, 0f));
            Box(parts, "SouthEastAnchorAimChannel",
                new Vector3(1.48f, 0.75f, -1.42f),
                new Vector3(0.12f, 0.10f, 3.48f), 5, 0,
                false, Quaternion.Euler(0f, -47f, 0f));
            Box(parts, "InwardChannelNorthSouth",
                new Vector3(-0.06f, 0.62f, 0.00f),
                new Vector3(0.12f, 0.08f, 4.78f), 5, 1);
            Box(parts, "InwardChannelEastWest",
                new Vector3(-0.06f, 0.63f, 0.00f),
                new Vector3(5.88f, 0.08f, 0.12f), 5, 1);
            Box(parts, "ChannelToVioletFocus",
                new Vector3(0.06f, 0.70f, -0.56f),
                new Vector3(0.14f, 0.10f, 0.86f), 7, 0);
        }

        private static void AddLevelSix(List<PartSpec> parts)
        {
            Box(parts, "ExpandedRearGalleryFloor",
                new Vector3(-0.22f, 0.24f, 2.92f),
                new Vector3(4.42f, 0.34f, 1.08f), 0, 1);
            Box(parts, "RearGalleryRail",
                new Vector3(-0.22f, 0.94f, 3.26f),
                new Vector3(4.35f, 0.16f, 0.16f), 6, 1);
            Box(parts, "StrongerWestRoofPlane",
                new Vector3(-1.64f, 4.16f, -0.02f),
                new Vector3(3.14f, 0.32f, 4.98f), 2, 2, true,
                Quaternion.Euler(0f, 0f, 29f));
            Box(parts, "StrongerEastRoofPlane",
                new Vector3(1.48f, 4.32f, 0.08f),
                new Vector3(2.58f, 0.30f, 4.62f), 4, 1,
                false,
                Quaternion.Euler(0f, 0f, -34f));
            Box(parts, "WardChimneyRaisedStack",
                new Vector3(-2.12f, 5.62f, 1.32f),
                new Vector3(0.70f, 0.84f, 0.66f), 2, 1);
            Box(parts, "WardChimneyAshBand",
                new Vector3(-2.12f, 5.22f, 1.32f),
                new Vector3(0.96f, 0.16f, 0.92f), 6, 1);
            Box(parts, "WardChimneyTopCap",
                new Vector3(-2.12f, 6.08f, 1.32f),
                new Vector3(1.08f, 0.18f, 1.02f), 5, 1);
        }

        private static void AddLevelSeven(List<PartSpec> parts)
        {
            Box(parts, "EastReliquaryAnnexFloor",
                new Vector3(3.28f, 0.24f, 0.40f),
                new Vector3(1.50f, 0.34f, 3.02f), 0, 2);
            Box(parts, "EastReliquaryOuterWall",
                new Vector3(3.90f, 1.20f, 0.40f),
                new Vector3(0.32f, 1.62f, 2.46f), 1, 1);
            Box(parts, "EastReliquaryTable",
                new Vector3(3.22f, 0.82f, 0.48f),
                new Vector3(0.96f, 0.54f, 0.78f), 5, 1);
            Box(parts, "EastAubergineShelterRoof",
                new Vector3(3.26f, 2.18f, 0.38f),
                new Vector3(1.66f, 0.26f, 2.98f), 3, 1,
                false, Quaternion.Euler(0f, 0f, -8f));
            Box(parts, "EastReliquaryVioletPin",
                new Vector3(3.22f, 1.12f, -0.14f),
                new Vector3(0.34f, 0.20f, 0.07f), 7, 1,
                false, Quaternion.identity, true);
            for (int index = 0; index < 4; index++)
            {
                float z = Mathf.Lerp(-1.15f, 1.5f, index / 3f);
                Box(parts, $"EastReliquaryRoofBatten_{index:D2}",
                    new Vector3(3.26f, 2.34f, z),
                    new Vector3(1.56f, 0.08f, 0.10f), 5, 0,
                    false, Quaternion.Euler(0f, 0f, -8f));
            }
        }

        private static void AddLevelEight(List<PartSpec> parts)
        {
            Box(parts, "RestrainedWestRoofBatten",
                new Vector3(-1.34f, 4.42f, -0.02f),
                new Vector3(3.12f, 0.12f, 4.76f), 6, 2, true,
                Quaternion.Euler(0f, 0f, 20f));
            Box(parts, "RestrainedEastRoofBatten",
                new Vector3(1.04f, 4.54f, 0.08f),
                new Vector3(2.62f, 0.12f, 4.48f), 6, 1,
                false,
                Quaternion.Euler(0f, 0f, -23f));
            Box(parts, "FixedVoidBraceWest",
                new Vector3(-0.66f, 3.02f, -0.18f),
                new Vector3(0.16f, 2.20f, 0.18f), 5, 1,
                false, Quaternion.Euler(0f, 0f, -18f));
            Box(parts, "FixedVoidBraceEast",
                new Vector3(0.70f, 3.02f, -0.18f),
                new Vector3(0.16f, 2.20f, 0.18f), 5, 1,
                false, Quaternion.Euler(0f, 0f, 18f));
            for (int index = 0; index < 5; index++)
            {
                float z = Mathf.Lerp(-1.65f, 1.65f, index / 4f);
                Box(parts, $"AshRoofEdgeMark_{index:D2}",
                    new Vector3(-0.08f, 4.22f, z),
                    new Vector3(0.42f, 0.10f, 0.10f), 6, 0);
            }
        }

        private static void AddLevelNine(List<PartSpec> parts)
        {
            Box(parts, "ServiceApron",
                new Vector3(-0.15f, 0.16f, -3.16f),
                new Vector3(6.82f, 0.28f, 0.72f), 0, 2, true);
            Box(parts, "OuterWestWardPier",
                new Vector3(-4.18f, 1.34f, -2.34f),
                new Vector3(0.42f, 2.32f, 0.48f), 1, 1);
            Box(parts, "OuterEastWardPier",
                new Vector3(4.02f, 1.30f, -2.18f),
                new Vector3(0.42f, 2.24f, 0.48f), 1, 1);
            Box(parts, "OuterWestWardCap",
                new Vector3(-4.18f, 2.62f, -2.34f),
                new Vector3(0.64f, 0.22f, 0.68f), 6, 1);
            Box(parts, "OuterEastWardCap",
                new Vector3(4.02f, 2.52f, -2.18f),
                new Vector3(0.64f, 0.22f, 0.68f), 6, 1);
            for (int index = 0; index < 5; index++)
            {
                float x = Mathf.Lerp(-3.0f, 2.65f, index / 4f);
                Box(parts, $"ApronAshJoint_{index:D2}",
                    new Vector3(x, 0.34f, -3.34f),
                    new Vector3(0.58f, 0.12f, 0.12f), 6, 0);
            }
        }

        private static void AddLevelTen(List<PartSpec> parts)
        {
            Box(parts, LevelTenCapstoneName + " WestSupportedFork",
                new Vector3(-0.70f, 6.18f, -0.06f),
                new Vector3(0.34f, 1.96f, 0.36f), 6, 2, true,
                Quaternion.Euler(0f, 0f, -15f));
            Box(parts, LevelTenCapstoneName + " EastSupportedFork",
                new Vector3(0.82f, 6.08f, -0.08f),
                new Vector3(0.28f, 1.48f, 0.36f), 6, 2, true,
                Quaternion.Euler(0f, 0f, 18f));
            Box(parts, LevelTenCapstoneName + " FixedCrosspiece",
                new Vector3(0.06f, 6.94f, -0.08f),
                new Vector3(1.98f, 0.24f, 0.36f), 6, 2, true);
            Box(parts, LevelTenCapstoneName + " WestGroundedFoot",
                new Vector3(-0.96f, 5.16f, -0.06f),
                new Vector3(0.26f, 1.08f, 0.38f), 6, 0,
                false,
                Quaternion.Euler(0f, 0f, -6f));
            Box(parts, LevelTenCapstoneName + " EastGroundedFoot",
                new Vector3(1.02f, 5.08f, -0.08f),
                new Vector3(0.24f, 0.92f, 0.38f), 6, 0,
                false,
                Quaternion.Euler(0f, 0f, 7f));
            Box(parts, LevelTenCapstoneName + " RoofBaseTie",
                new Vector3(0.04f, 4.62f, -0.08f),
                new Vector3(2.24f, 0.18f, 0.40f), 5, 0);
            Box(parts, "NarrowEmptySlitLeftEdge",
                new Vector3(-0.22f, 6.70f, -0.26f),
                new Vector3(0.08f, 0.74f, 0.10f), 2, 1);
            Box(parts, "NarrowEmptySlitRightEdge",
                new Vector3(0.28f, 6.68f, -0.26f),
                new Vector3(0.08f, 0.70f, 0.10f), 2, 1);
            Box(parts, "ContainedVioletSeal",
                new Vector3(0.03f, 6.64f, -0.32f),
                new Vector3(0.42f, 0.38f, 0.07f), 7, 2, false,
                Quaternion.identity, true);
            Box(parts, "YokeToWestAnchorTie",
                new Vector3(-1.68f, 5.30f, 0.54f),
                new Vector3(0.18f, 1.62f, 0.18f), 5, 2,
                false, Quaternion.Euler(0f, 0f, -22f));
            Box(parts, "YokeToEastAnchorTie",
                new Vector3(1.56f, 5.22f, 0.48f),
                new Vector3(0.18f, 1.48f, 0.18f), 5, 2,
                false, Quaternion.Euler(0f, 0f, 20f));
            Box(parts, "YokeChimneyTie",
                new Vector3(-1.72f, 5.92f, 0.92f),
                new Vector3(0.18f, 1.02f, 0.18f), 5, 1,
                false, Quaternion.Euler(0f, 0f, -10f));
            for (int index = 0; index < 4; index++)
            {
                float y = 0.72f + index * 0.52f;
                Box(parts, $"WestAnchorYokeBand_{index:D2}",
                    new Vector3(-3.24f, y, -2.48f),
                    new Vector3(0.08f, 0.14f, 0.42f), 6, 0);
                Box(parts, $"EastAnchorYokeBand_{index:D2}",
                    new Vector3(3.18f, y, -2.32f),
                    new Vector3(0.08f, 0.14f, 0.42f), 6, 0);
            }
        }

        private static void Box(
            ICollection<PartSpec> parts,
            string name,
            Vector3 center,
            Vector3 size,
            int atlasCell,
            int maximumLod,
            bool retainAtFar = false,
            Quaternion? rotation = null,
            bool isAccent = false)
        {
            parts.Add(new PartSpec(
                name,
                PartShape.Box,
                center,
                size,
                rotation ?? Quaternion.identity,
                atlasCell,
                maximumLod,
                retainAtFar,
                isAccent));
        }

        private static void Cylinder(
            ICollection<PartSpec> parts,
            string name,
            Vector3 center,
            Vector3 size,
            int atlasCell,
            int maximumLod,
            bool retainAtFar = false,
            Quaternion? rotation = null,
            bool isAccent = false)
        {
            parts.Add(new PartSpec(
                name,
                PartShape.Cylinder,
                center,
                size,
                rotation ?? Quaternion.identity,
                atlasCell,
                maximumLod,
                retainAtFar,
                isAccent));
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

        private static Mesh PersistMesh(Mesh generated, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
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
                name = "T_Umbral_Veilwright_Atlas_1024",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[AtlasSize * AtlasSize];
            int cellWidth = AtlasSize / 4;
            int cellHeight = AtlasSize / 2;
            for (int y = 0; y < AtlasSize; y++)
            {
                for (int x = 0; x < AtlasSize; x++)
                {
                    int cell = x / cellWidth +
                        (y / cellHeight) * 4;
                    Color32 baseColor = AtlasColors[cell];
                    int localX = x % cellWidth;
                    int localY = y % cellHeight;
                    int hash =
                        (x * 29 + y * 17 + cell * 61) & 31;
                    int variation = hash - 15;

                    if (cell == 0 || cell == 1 || cell == 6)
                    {
                        bool mortar =
                            localY % 52 < 3 ||
                            (localX +
                                ((localY / 52) % 2) * 31) %
                            64 < 3;
                        if (mortar)
                        {
                            variation += 18;
                        }
                    }
                    else if (cell == 2 || cell == 7)
                    {
                        if (localX % 48 < 3 || localY % 96 < 3)
                        {
                            variation += 15;
                        }
                    }
                    else if (cell == 3 || cell == 4)
                    {
                        if ((localX + localY * 2) % 71 < 3)
                        {
                            variation += 10;
                        }
                    }

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
                    "Unity did not create the Umbral atlas importer.");
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
            material.SetFloat("_Metallic", 0.16f);
            material.SetFloat("_Glossiness", 0.30f);

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
                    !(entry.RealmId == RealmId.Umbral &&
                        string.Equals(
                            entry.BuildingId,
                            BuildingId,
                            StringComparison.Ordinal)))
                .ToList();
            entries.Add(new KingdomBuildingModelEntry(
                ModelId,
                RealmId.Umbral,
                BuildingId,
                prefab,
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
            InstantiatePreview(prefab, 1, new Vector3(-8.5f, 0f, 0f));
            InstantiatePreview(prefab, 6, Vector3.zero);
            InstantiatePreview(prefab, 10, new Vector3(8.8f, 0f, 0f));

            GameObject ground =
                GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ReviewGround";
            ground.transform.localScale =
                new Vector3(3.2f, 1f, 1.15f);
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            Material groundMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/AL/Art/Generated/Architecture/" +
                    "Umbral/Materials/MAT_Umbral_Ground.mat");
            if (groundMaterial != null)
            {
                ground.GetComponent<Renderer>().sharedMaterial =
                    groundMaterial;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor =
                new Color(0.030f, 0.036f, 0.050f);
            camera.transform.position =
                new Vector3(15.2f, 10.8f, -25.4f);
            camera.transform.LookAt(new Vector3(0f, 2.25f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.90f, 0.94f, 1f);
            key.intensity = 2.05f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation =
                Quaternion.Euler(45f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.82f, 0.56f);
            fill.intensity = 0.62f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation =
                Quaternion.Euler(28f, 144f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.22f, 0.24f, 0.29f);
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
                    $"Could not preview Umbral Level {level}.");
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
                "Assets/AL/Art/Generated/Architecture/Umbral",
                "Production");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Umbral/Production",
                "Runtime");
            EnsureFolder(RuntimeFolder, "Meshes");
            EnsureFolder("Assets/AL/ScriptableObjects", "Resources");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private enum PartShape
        {
            Box = 0,
            Cylinder = 1
        }

        private sealed class PartSpec
        {
            public PartSpec(
                string name,
                PartShape shape,
                Vector3 center,
                Vector3 size,
                Quaternion rotation,
                int atlasCell,
                int maximumLod,
                bool retainAtFar,
                bool isAccent)
            {
                Name = name;
                Shape = shape;
                Center = center;
                Size = size;
                Rotation = rotation;
                AtlasCell = Mathf.Clamp(atlasCell, 0, 7);
                MaximumLod = Mathf.Clamp(maximumLod, 0, 2);
                RetainAtFar = retainAtFar;
                IsAccent = isAccent;
            }

            public string Name { get; }
            public PartShape Shape { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }
            public Quaternion Rotation { get; }
            public int AtlasCell { get; }
            public int MaximumLod { get; }
            public bool RetainAtFar { get; }
            public bool IsAccent { get; }
        }

        private sealed class MeshBuildResult
        {
            public MeshBuildResult(
                Mesh mesh,
                bool hasStructural,
                bool hasAccent)
            {
                Mesh = mesh;
                HasStructural = hasStructural;
                HasAccent = hasAccent;
            }

            public Mesh Mesh { get; }
            public bool HasStructural { get; }
            public bool HasAccent { get; }
        }
    }
}
