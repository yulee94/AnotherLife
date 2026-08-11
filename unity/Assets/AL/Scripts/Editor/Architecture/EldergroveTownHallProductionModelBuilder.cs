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
    /// Builds the final cumulative Eldergrove Town Hall model from the approved
    /// open-court graybox and authored Eldergrove root construction direction.
    /// </summary>
    public static class EldergroveTownHallProductionModelBuilder
    {
        public const string RuntimeFolder =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "TownHall/Runtime";
        public const string MeshFolder = RuntimeFolder + "/Meshes";
        public const string AtlasPath =
            RuntimeFolder + "/T_Eldergrove_TownHall_Atlas_1024.png";
        public const string AtlasMaterialPath =
            RuntimeFolder + "/MAT_Eldergrove_TownHall_Atlas.mat";
        public const string AccentMaterialPath =
            RuntimeFolder + "/MAT_Eldergrove_TownHall_Accent.mat";
        public const string PrefabPath =
            RuntimeFolder + "/Eldergrove_TownHall_Production.prefab";
        public const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        public const string MotionProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Eldergrove_Atelier_ConstructionProfile.asset";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "EldergroveTownHallProductionModel.unity";
        public const string ModelId =
            "building.eldergrove.townhall.production.v1";

        private const string BuildingId = "TownHall";
        private const int AtlasSize = 1024;
        private const float StrategicBoardScale = 0.09f;

        private static readonly Vector3 SlotEnvelope =
            new Vector3(16f, 13f, 16f);
        private static readonly Vector3 MaximumArtBounds =
            new Vector3(15.2f, 12.6f, 14.2f);
        private static readonly float[] LodTransitions =
            { 0.60f, 0.30f, 0.12f, 0.04f };

        private static readonly Color32[] AtlasColors =
        {
            new Color32(179, 169, 143, 255),
            new Color32(116, 102, 75, 255),
            new Color32(70, 48, 31, 255),
            new Color32(56, 65, 43, 255),
            new Color32(119, 89, 47, 255),
            new Color32(144, 132, 105, 255),
            new Color32(83, 73, 48, 255),
            new Color32(194, 177, 128, 255)
        };

        [MenuItem(
            "Another Life/Architecture/" +
            "Build Eldergrove Town Hall Production Model")]
        public static void Build()
        {
            EnsureFolders();
            Texture2D atlas = CreateAtlas();
            Material atlasMaterial = CreateOrUpdateMaterial(
                AtlasMaterialPath,
                atlas,
                new Color(0.9f, 0.86f, 0.75f),
                Color.black);
            Material accentMaterial = CreateOrUpdateMaterial(
                AccentMaterialPath,
                null,
                new Color(0.42f, 0.62f, 0.24f),
                Color.black);

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
                    "eldergrove-townhall-production",
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
                    $"ELDERGROVE_TOWNHALL_METRIC LOD{lodIndex} " +
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
                        $"ELDERGROVE_TOWNHALL_BOUNDS level={level} " +
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
                new GameObject("Eldergrove_TownHall_Production");
            var lodRoots = new Transform[4];
            var levelObjects = new GameObject[10, 4];
            var partsByLevel = new List<PartSpec>[10];
            for (int level = 1; level <= 10; level++)
            {
                partsByLevel[level - 1] = CreateLevelParts(level);
            }

            try
            {
                CreateAnchor(
                    production.transform,
                    "Entrance",
                    new Vector3(0f, 0f, -5.55f));
                CreateAnchor(
                    production.transform,
                    "CameraFocus",
                    new Vector3(0f, 4.4f, 0f));
                CreateAnchor(
                    production.transform,
                    "Activity_00",
                    new Vector3(2.55f, 0f, -4.55f));
                CreateAnchor(
                    production.transform,
                    "Output_00",
                    new Vector3(-3f, 0f, 4.55f));
                CreateAnchor(
                    production.transform,
                    "Occlusion_Roof",
                    new Vector3(0f, 4.15f, 0f));
                CreateAnchor(
                    production.transform,
                    "Occlusion_Canopies",
                    new Vector3(0f, 3.35f, -3.7f));
                CreateAnchor(
                    production.transform,
                    "Occlusion_Crown",
                    new Vector3(0f, 7.85f, 0f));

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
                selection.center = new Vector3(0f, 5.65f, -0.05f);
                selection.size = new Vector3(14f, 11.3f, 12.8f);

                BoxCollider navigation =
                    production.AddComponent<BoxCollider>();
                navigation.isTrigger = false;
                navigation.center = new Vector3(0f, 0.8f, -0.05f);
                navigation.size = new Vector3(12.7f, 1.6f, 10.7f);

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
                    $"Eldergrove LOD{lodIndex} Level {level} has no mesh.");
            }

            MeshBuildResult result = BuildMesh(
                retained,
                lodIndex,
                $"M_Eldergrove_TownHall_LOD{lodIndex}_L{level:D2}");
            string meshPath =
                $"{MeshFolder}/M_Eldergrove_TownHall_" +
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
                    AddEldergroveLevelOne(parts);
                    break;
                case 2:
                    AddEldergroveLevelTwo(parts);
                    break;
                case 3:
                    AddEldergroveLevelThree(parts);
                    break;
                case 4:
                    AddEldergroveLevelFour(parts);
                    break;
                case 5:
                    AddEldergroveLevelFive(parts);
                    break;
                case 6:
                    AddEldergroveLevelSix(parts);
                    break;
                case 7:
                    AddEldergroveLevelSeven(parts);
                    break;
                case 8:
                    AddEldergroveLevelEight(parts);
                    break;
                case 9:
                    AddEldergroveLevelNine(parts);
                    break;
                case 10:
                    AddEldergroveLevelTen(parts);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
            return parts;
        }

        private static void AddEldergroveLevelOne(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "CivicPlot",
                new Vector3(0f, 0.16f, 0f),
                new Vector3(9.3f, 0.32f, 8f),
                0,
                2,
                true);
            Box(
                parts,
                "CouncilFloor",
                new Vector3(0f, 0.36f, -0.25f),
                new Vector3(6.4f, 0.22f, 5.6f),
                5,
                2);
            Box(
                parts,
                "WestCourtEdge",
                new Vector3(-4.15f, 0.52f, -0.2f),
                new Vector3(0.55f, 0.65f, 6.7f),
                0,
                2);
            Box(
                parts,
                "EastCourtEdge",
                new Vector3(4.15f, 0.52f, -0.2f),
                new Vector3(0.55f, 0.65f, 6.7f),
                0,
                2);
            Box(
                parts,
                "RearCourtEdge",
                new Vector3(0f, 0.52f, 3.4f),
                new Vector3(8.3f, 0.65f, 0.55f),
                0,
                2);
            Box(
                parts,
                "StepUpper",
                new Vector3(0f, 0.44f, -3.76f),
                new Vector3(3f, 0.26f, 0.78f),
                5,
                2);
            Box(
                parts,
                "StepMiddle",
                new Vector3(0f, 0.28f, -4.1f),
                new Vector3(3.55f, 0.2f, 0.76f),
                5,
                1);
            Box(
                parts,
                "StepLower",
                new Vector3(0f, 0.14f, -4.43f),
                new Vector3(4.05f, 0.18f, 0.7f),
                5,
                2);

            Box(
                parts,
                "RearCouncilHall",
                new Vector3(0f, 2.05f, 2.62f),
                new Vector3(7.2f, 2.85f, 1.55f),
                0,
                2,
                true);
            Box(
                parts,
                "RearTimberScreen",
                new Vector3(0f, 2.12f, 1.81f),
                new Vector3(5.7f, 1.75f, 0.16f),
                2,
                1);
            Box(
                parts,
                "WestGalleryBody",
                new Vector3(-3.35f, 1.6f, -0.1f),
                new Vector3(1.22f, 2.05f, 4.55f),
                0,
                2,
                true);
            Box(
                parts,
                "EastGalleryBody",
                new Vector3(3.35f, 1.6f, 0.1f),
                new Vector3(1.22f, 2.05f, 4.2f),
                0,
                2,
                true);
            Box(
                parts,
                "WestGalleryRail",
                new Vector3(-2.66f, 1.5f, -0.25f),
                new Vector3(0.18f, 1.25f, 3.65f),
                4,
                1);
            Box(
                parts,
                "EastGalleryRail",
                new Vector3(2.66f, 1.5f, -0.05f),
                new Vector3(0.18f, 1.25f, 3.35f),
                4,
                1);
            Box(
                parts,
                "WestGalleryRoof",
                new Vector3(-3.34f, 2.8f, -0.05f),
                new Vector3(1.65f, 0.2f, 4.75f),
                2,
                2,
                false,
                Quaternion.Euler(0f, 0f, 10f));
            Box(
                parts,
                "EastGalleryRoof",
                new Vector3(3.34f, 2.8f, 0.12f),
                new Vector3(1.65f, 0.2f, 4.42f),
                2,
                2,
                false,
                Quaternion.Euler(0f, 0f, -10f));

            AddRootArch(
                parts,
                "FrontRootArch",
                new Vector3(-3.55f, 0.58f, -2.25f),
                new Vector3(0f, 5.3f, -2f),
                new Vector3(3.55f, 0.58f, -2.25f));
            AddRootArch(
                parts,
                "WestRootArch",
                new Vector3(-3.55f, 0.58f, -2.25f),
                new Vector3(-2.95f, 5.5f, 0.1f),
                new Vector3(-3.55f, 0.58f, 2.25f));
            AddRootArch(
                parts,
                "EastRootArch",
                new Vector3(3.55f, 0.58f, -2.25f),
                new Vector3(2.95f, 5.5f, 0.1f),
                new Vector3(3.55f, 0.58f, 2.25f));

            foreach (float x in new[] { -3.55f, 3.55f })
            {
                foreach (float z in new[] { -2.25f, 2.25f })
                {
                    Cylinder(
                        parts,
                        $"BaseCollar_{x:0.00}_{z:0.00}",
                        new Vector3(x, 0.58f, z),
                        new Vector3(0.62f, 0.14f, 0.62f),
                        4,
                        1);
                }
            }

            AddRoofPair(
                parts,
                "CouncilCanopy",
                new Vector3(0f, 4.15f, 0f),
                4.45f,
                6.1f,
                18f,
                3,
                true);
            Box(
                parts,
                "CanopyRidge",
                new Vector3(0f, 4.86f, 0f),
                new Vector3(0.26f, 0.24f, 6.3f),
                4,
                2,
                true);
        }

        private static void AddEldergroveLevelTwo(
            ICollection<PartSpec> parts)
        {
            foreach (float x in new[] { -3.55f, 3.55f })
            {
                foreach (float z in new[] { -2.25f, 2.25f })
                {
                    Beam(
                        parts,
                        $"RootFoot_{x:0.00}_{z:0.00}",
                        new Vector3(x, 0.52f, z),
                        new Vector3(
                            x < 0f ? -4.15f : 4.15f,
                            0.18f,
                            z < 0f ? z - 0.55f : z + 0.55f),
                        0.42f,
                        3,
                        2);
                }
            }
            Box(
                parts,
                "WestDrainEdge",
                new Vector3(-4.48f, 0.32f, 0f),
                new Vector3(0.24f, 0.28f, 5.3f),
                4,
                1);
            Box(
                parts,
                "EastDrainEdge",
                new Vector3(4.48f, 0.32f, 0f),
                new Vector3(0.24f, 0.28f, 5.3f),
                4,
                2);
        }

        private static void AddEldergroveLevelThree(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "StewardWingPlinth",
                new Vector3(-5.05f, 0.42f, 0.65f),
                new Vector3(2.1f, 0.5f, 4.45f),
                0,
                2,
                true);
            Box(
                parts,
                "StewardWingBody",
                new Vector3(-5.05f, 1.65f, 0.7f),
                new Vector3(1.82f, 1.95f, 3.95f),
                2,
                2,
                true);
            Box(
                parts,
                "StewardWingFrontPier",
                new Vector3(-5.48f, 1.45f, -1.4f),
                new Vector3(0.42f, 1.9f, 0.5f),
                0,
                1);
            Box(
                parts,
                "StewardWingRearPier",
                new Vector3(-5.48f, 1.45f, 2.76f),
                new Vector3(0.42f, 1.9f, 0.5f),
                0,
                1);
            Box(
                parts,
                "StewardWingRoof",
                new Vector3(-5.05f, 2.83f, 0.7f),
                new Vector3(2.25f, 0.22f, 4.5f),
                2,
                2,
                true,
                Quaternion.Euler(0f, 0f, 11f));
            Box(
                parts,
                "StewardGalleryRail",
                new Vector3(-4.05f, 1.32f, 0.55f),
                new Vector3(0.18f, 1.05f, 3.2f),
                4,
                1);
        }

        private static void AddEldergroveLevelFour(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "EntranceShelter",
                new Vector3(0f, 3.35f, -3.68f),
                new Vector3(3.55f, 0.2f, 1.55f),
                2,
                2,
                true,
                Quaternion.Euler(-8f, 0f, 0f));
            Box(
                parts,
                "ThresholdPostWest",
                new Vector3(-1.48f, 1.8f, -4.02f),
                new Vector3(0.22f, 2.45f, 0.22f),
                2,
                2);
            Box(
                parts,
                "ThresholdPostEast",
                new Vector3(1.48f, 1.8f, -4.02f),
                new Vector3(0.22f, 2.45f, 0.22f),
                2,
                2);
            Box(
                parts,
                "ThresholdBronzeLintel",
                new Vector3(0f, 3.05f, -3.88f),
                new Vector3(3.15f, 0.22f, 0.24f),
                4,
                2);
            Box(
                parts,
                "PublicNoticePlinth",
                new Vector3(2.55f, 0.67f, -4.05f),
                new Vector3(1.05f, 0.82f, 0.68f),
                0,
                1);
            Box(
                parts,
                "PublicLeafWaymark",
                new Vector3(2.55f, 1.02f, -4.405f),
                new Vector3(0.42f, 0.36f, 0.04f),
                0,
                0,
                false,
                Quaternion.identity,
                true);
        }

        private static void AddEldergroveLevelFive(
            ICollection<PartSpec> parts)
        {
            Beam(
                parts,
                "CouncilRidgeWest",
                new Vector3(-2.95f, 5.5f, 0.1f),
                new Vector3(0f, 5.3f, -2f),
                0.34f,
                3,
                2,
                true);
            Beam(
                parts,
                "CouncilRidgeEast",
                new Vector3(0f, 5.3f, -2f),
                new Vector3(2.95f, 5.5f, 0.1f),
                0.34f,
                3,
                2,
                true);
            foreach (Vector3 apex in new[]
                     {
                         new Vector3(0f, 5.3f, -2f),
                         new Vector3(-2.95f, 5.5f, 0.1f),
                         new Vector3(2.95f, 5.5f, 0.1f)
                     })
            {
                Cylinder(
                    parts,
                    $"ApexCollar_{apex.x:0.00}_{apex.z:0.00}",
                    apex,
                    new Vector3(0.38f, 0.12f, 0.38f),
                    4,
                    1,
                    false,
                    Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private static void AddEldergroveLevelSix(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "RecordsWingPlinth",
                new Vector3(5.08f, 0.42f, 0.15f),
                new Vector3(2.2f, 0.5f, 5.15f),
                0,
                2,
                true);
            Box(
                parts,
                "RecordsWingBody",
                new Vector3(5.08f, 1.72f, 0.18f),
                new Vector3(1.88f, 2.05f, 4.65f),
                2,
                2,
                true);
            Box(
                parts,
                "RecordsWingFrontPier",
                new Vector3(5.52f, 1.5f, -2.25f),
                new Vector3(0.44f, 2f, 0.52f),
                0,
                1);
            Box(
                parts,
                "RecordsWingRearPier",
                new Vector3(5.52f, 1.5f, 2.62f),
                new Vector3(0.44f, 2f, 0.52f),
                0,
                1);
            Box(
                parts,
                "RecordsWingRoof",
                new Vector3(5.08f, 2.95f, 0.18f),
                new Vector3(2.34f, 0.22f, 5.24f),
                2,
                2,
                true,
                Quaternion.Euler(0f, 0f, -11f));
            Box(
                parts,
                "RecordsGalleryRail",
                new Vector3(4.04f, 1.36f, 0.05f),
                new Vector3(0.18f, 1.08f, 3.75f),
                4,
                1);
            Box(
                parts,
                "RearCirculationHall",
                new Vector3(0f, 1.78f, 3.66f),
                new Vector3(8.1f, 1.95f, 0.72f),
                2,
                2,
                true);
            Box(
                parts,
                "RearCirculationCap",
                new Vector3(0f, 2.83f, 3.66f),
                new Vector3(8.35f, 0.2f, 1.02f),
                2,
                1);
        }

        private static void AddEldergroveLevelSeven(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "UpperCouncilGallery",
                new Vector3(0f, 4.82f, 2f),
                new Vector3(4.1f, 1.25f, 1.45f),
                2,
                2,
                true);
            AddRoofPair(
                parts,
                "UpperCouncilRoof",
                new Vector3(0f, 5.72f, 2f),
                2.15f,
                1.85f,
                18f,
                1,
                true);
            Box(
                parts,
                "UpperCouncilRidge",
                new Vector3(0f, 6.07f, 2f),
                new Vector3(0.22f, 0.2f, 2f),
                4,
                2,
                true);
        }

        private static void AddEldergroveLevelEight(
            ICollection<PartSpec> parts)
        {
            for (int index = 0; index < 4; index++)
            {
                float x = Mathf.Lerp(-3.25f, 3.25f, index / 3f);
                Box(
                    parts,
                    $"RearWalkPost_{index:D2}",
                    new Vector3(x, 1.45f, 4.2f),
                    new Vector3(0.24f, 2f, 0.24f),
                    3,
                    1);
            }
            Box(
                parts,
                "RearWalkDeck",
                new Vector3(0f, 0.95f, 4.08f),
                new Vector3(7.55f, 0.22f, 1.15f),
                2,
                2,
                true);
            Box(
                parts,
                "RearWalkRoof",
                new Vector3(0f, 2.6f, 4.08f),
                new Vector3(7.85f, 0.2f, 1.35f),
                2,
                2,
                true,
                Quaternion.Euler(7f, 0f, 0f));
            Box(
                parts,
                "RearWalkDrain",
                new Vector3(0f, 2.48f, 4.72f),
                new Vector3(7.78f, 0.16f, 0.16f),
                4,
                1);
        }

        private static void AddEldergroveLevelNine(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "ForecourtEdgeWest",
                new Vector3(-4.25f, 0.26f, -5.02f),
                new Vector3(3.15f, 0.32f, 0.48f),
                0,
                2,
                true);
            Box(
                parts,
                "ForecourtEdgeEast",
                new Vector3(4.25f, 0.26f, -5.02f),
                new Vector3(3.15f, 0.32f, 0.48f),
                0,
                2,
                true);
            Box(
                parts,
                "ApproachCenter",
                new Vector3(0f, 0.12f, -5.18f),
                new Vector3(4.1f, 0.18f, 1.05f),
                5,
                2,
                true);
            Box(
                parts,
                "NoticePierWest",
                new Vector3(-5.08f, 0.82f, -4.88f),
                new Vector3(0.56f, 1.35f, 0.56f),
                0,
                1);
            Box(
                parts,
                "NoticePierEast",
                new Vector3(5.08f, 0.82f, -4.88f),
                new Vector3(0.56f, 1.35f, 0.56f),
                0,
                2);
        }

        private static void AddEldergroveLevelTen(
            ICollection<PartSpec> parts)
        {
            Vector3 frontRing = new Vector3(0f, 7.85f, -2.1f);
            Vector3 westRing = new Vector3(-2.1f, 7.85f, 0f);
            Vector3 eastRing = new Vector3(2.1f, 7.85f, 0f);
            AddBentRootSupport(
                parts,
                "FrontArchCrownRoot",
                new Vector3(0f, 5.3f, -2f),
                new Vector3(0f, 6.7f, -2.28f),
                frontRing);
            AddBentRootSupport(
                parts,
                "WestArchCrownRoot",
                new Vector3(-2.95f, 5.5f, 0.1f),
                new Vector3(-2.62f, 6.72f, -0.12f),
                westRing);
            AddBentRootSupport(
                parts,
                "EastArchCrownRoot",
                new Vector3(2.95f, 5.5f, 0.1f),
                new Vector3(2.62f, 6.72f, -0.12f),
                eastRing);

            const int segmentCount = 12;
            const float radius = 2.1f;
            const float ringHeight = 7.85f;
            for (int index = 0; index < segmentCount; index++)
            {
                float firstAngle =
                    index * Mathf.PI * 2f / segmentCount;
                float secondAngle =
                    (index + 1) * Mathf.PI * 2f / segmentCount;
                Vector3 start = new Vector3(
                    Mathf.Cos(firstAngle) * radius,
                    ringHeight,
                    Mathf.Sin(firstAngle) * radius);
                Vector3 end = new Vector3(
                    Mathf.Cos(secondAngle) * radius,
                    ringHeight,
                    Mathf.Sin(secondAngle) * radius);
                Beam(
                    parts,
                    $"BronzeRingSegment_{index:D2}",
                    start,
                    end,
                    0.13f,
                    4,
                    2,
                    true);
            }

            foreach (Vector3 point in new[]
                     {
                         frontRing,
                         westRing,
                         eastRing
                     })
            {
                Cylinder(
                    parts,
                    $"RingCollar_{point.x:0.0}_{point.z:0.0}",
                    point,
                    new Vector3(0.32f, 0.12f, 0.32f),
                    4,
                    1);
            }
        }

        private static void AddRootArch(
            ICollection<PartSpec> parts,
            string name,
            Vector3 firstBase,
            Vector3 apex,
            Vector3 secondBase)
        {
            Vector3 firstShoulder = Vector3.Lerp(
                firstBase,
                apex,
                0.34f);
            Vector3 firstUpper = Vector3.Lerp(
                firstBase,
                apex,
                0.72f);
            Vector3 secondUpper = Vector3.Lerp(
                secondBase,
                apex,
                0.72f);
            Vector3 secondShoulder = Vector3.Lerp(
                secondBase,
                apex,
                0.34f);

            Beam(
                parts,
                $"{name}_FirstLower",
                firstBase,
                firstShoulder,
                0.72f,
                3,
                2,
                true);
            Beam(
                parts,
                $"{name}_FirstMiddle",
                firstShoulder,
                firstUpper,
                0.64f,
                3,
                2);
            Beam(
                parts,
                $"{name}_FirstCrown",
                firstUpper,
                apex,
                0.54f,
                3,
                2,
                true);
            Beam(
                parts,
                $"{name}_SecondCrown",
                apex,
                secondUpper,
                0.54f,
                3,
                2,
                true);
            Beam(
                parts,
                $"{name}_SecondMiddle",
                secondUpper,
                secondShoulder,
                0.64f,
                3,
                2);
            Beam(
                parts,
                $"{name}_SecondLower",
                secondShoulder,
                secondBase,
                0.72f,
                3,
                2,
                true);
        }

        private static void AddBentRootSupport(
            ICollection<PartSpec> parts,
            string name,
            Vector3 start,
            Vector3 bend,
            Vector3 end)
        {
            Beam(
                parts,
                $"{name}_Lower",
                start,
                bend,
                0.54f,
                3,
                2,
                true);
            Beam(
                parts,
                $"{name}_Upper",
                bend,
                end,
                0.48f,
                3,
                2,
                true);
        }

        private static void AddRoofPair(
            ICollection<PartSpec> parts,
            string prefix,
            Vector3 center,
            float sideWidth,
            float depth,
            float pitch,
            int ribCount,
            bool retainAtFar)
        {
            float halfOffset = sideWidth * 0.47f;
            Box(
                parts,
                $"{prefix}West",
                center + new Vector3(-halfOffset, 0f, 0f),
                new Vector3(sideWidth, 0.28f, depth),
                2,
                2,
                retainAtFar,
                Quaternion.Euler(0f, 0f, pitch));
            Box(
                parts,
                $"{prefix}East",
                center + new Vector3(halfOffset, 0f, 0f),
                new Vector3(sideWidth, 0.28f, depth),
                2,
                2,
                retainAtFar,
                Quaternion.Euler(0f, 0f, -pitch));

            for (int index = 0; index < ribCount; index++)
            {
                float z = ribCount == 1
                    ? center.z
                    : center.z + Mathf.Lerp(
                        -depth * 0.42f,
                        depth * 0.42f,
                        index / (ribCount - 1f));
                Box(
                    parts,
                    $"{prefix}WestRib_{index:D2}",
                    new Vector3(
                        center.x - halfOffset,
                        center.y + 0.16f,
                        z),
                    new Vector3(
                        sideWidth + 0.08f,
                        0.1f,
                        0.13f),
                    4,
                    1,
                    false,
                    Quaternion.Euler(0f, 0f, pitch));
                Box(
                    parts,
                    $"{prefix}EastRib_{index:D2}",
                    new Vector3(
                        center.x + halfOffset,
                        center.y + 0.16f,
                        z),
                    new Vector3(
                        sideWidth + 0.08f,
                        0.1f,
                        0.13f),
                    4,
                    1,
                    false,
                    Quaternion.Euler(0f, 0f, -pitch));
            }
        }

        private static void Beam(
            ICollection<PartSpec> parts,
            string name,
            Vector3 start,
            Vector3 end,
            float thickness,
            int atlasCell,
            int maximumLod,
            bool retainAtFar = false)
        {
            Vector3 direction = end - start;
            Cylinder(
                parts,
                name,
                (start + end) * 0.5f,
                new Vector3(
                    thickness,
                    direction.magnitude,
                    thickness),
                atlasCell,
                maximumLod,
                retainAtFar,
                Quaternion.FromToRotation(
                    Vector3.up,
                    direction.normalized));
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
                name = "T_Eldergrove_TownHall_Atlas_1024",
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
            InstantiatePreview(prefab, 1, new Vector3(-13.5f, 0f, 0f));
            InstantiatePreview(prefab, 6, Vector3.zero);
            InstantiatePreview(prefab, 10, new Vector3(14f, 0f, 0f));

            GameObject ground =
                GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ReviewGround";
            ground.transform.localScale =
                new Vector3(4.6f, 1f, 1.35f);
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
            camera.orthographicSize = 10.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor =
                new Color(0.025f, 0.052f, 0.048f);
            camera.transform.position =
                new Vector3(20f, 14.2f, -33.4f);
            camera.transform.LookAt(new Vector3(0.35f, 3.2f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.84f, 0.59f);
            key.intensity = 1.68f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation =
                Quaternion.Euler(45f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.38f, 0.61f, 0.48f);
            fill.intensity = 0.7f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation =
                Quaternion.Euler(28f, 144f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.2f, 0.22f, 0.17f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void InstantiatePreview(
            GameObject prefab,
            int level,
            Vector3 position)
        {
            var instance =
                (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"TownHall_Level{level:D2}";
            instance.transform.position = position;
            KingdomBuildingLevelModel model =
                instance.GetComponent<KingdomBuildingLevelModel>();
            if (model == null || !model.ApplyConfirmedLevel(level))
            {
                throw new InvalidOperationException(
                    $"Could not preview Eldergrove Level {level}.");
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

        private static Transform CreateAnchor(
            Transform parent,
            string name,
            Vector3 localPosition)
        {
            Transform anchor = CreateGroup(parent, name);
            anchor.localPosition = localPosition;
            return anchor;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Eldergrove",
                "Production");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Eldergrove/Production",
                "TownHall");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
                "TownHall",
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
