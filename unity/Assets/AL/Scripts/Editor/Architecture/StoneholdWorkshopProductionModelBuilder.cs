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
    /// Builds the final cumulative Stonehold Workshop model from the approved
    /// modular workshop and rigid-construction direction.
    /// </summary>
    public static class StoneholdWorkshopProductionModelBuilder
    {
        public const string RuntimeFolder =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/Runtime";
        public const string MeshFolder = RuntimeFolder + "/Meshes";
        public const string AtlasPath =
            RuntimeFolder + "/T_Stonehold_Workshop_Atlas_1024.png";
        public const string AtlasMaterialPath =
            RuntimeFolder + "/MAT_Stonehold_Workshop_Atlas.mat";
        public const string AccentMaterialPath =
            RuntimeFolder + "/MAT_Stonehold_Workshop_Accent.mat";
        public const string PrefabPath =
            RuntimeFolder + "/Stonehold_Workshop_Production.prefab";
        public const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        public const string MotionProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Stonehold_Workshop_ConstructionProfile.asset";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "StoneholdWorkshopProductionModel.unity";
        public const string ModelId =
            "building.stonehold.workshop.production.v1";

        private const string BuildingId = "Workshop";
        private const int AtlasSize = 1024;
        private const float StrategicBoardScale = 0.12f;

        private static readonly Vector3 SlotEnvelope =
            new Vector3(10f, 6.8f, 8f);
        private static readonly Vector3 MaximumArtBounds =
            new Vector3(9.2f, 6.6f, 6.8f);
        private static readonly float[] LodTransitions =
            { 0.60f, 0.30f, 0.12f, 0.04f };

        private static readonly Color32[] AtlasColors =
        {
            new Color32(55, 52, 49, 255),
            new Color32(86, 81, 74, 255),
            new Color32(38, 37, 36, 255),
            new Color32(91, 53, 30, 255),
            new Color32(77, 36, 22, 255),
            new Color32(42, 39, 36, 255),
            new Color32(130, 119, 101, 255),
            new Color32(118, 70, 35, 255)
        };

        [MenuItem(
            "Another Life/Architecture/" +
            "Build Stonehold Workshop Production Model")]
        public static void Build()
        {
            EnsureFolders();
            Texture2D atlas = CreateAtlas();
            Material atlasMaterial = CreateOrUpdateMaterial(
                AtlasMaterialPath,
                atlas,
                new Color(0.82f, 0.78f, 0.72f),
                Color.black);
            Material accentMaterial = CreateOrUpdateMaterial(
                AccentMaterialPath,
                null,
                new Color(0.88f, 0.24f, 0.045f),
                new Color(0.72f, 0.095f, 0.012f));

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
                    "Unity did not save the Stonehold production prefab.");
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
                    "The Stonehold production review camera is missing.");
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
                    "stonehold-workshop-production",
                    "render.png"));
        }

        public static void ReportMetricsFromCommandLine()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "The Stonehold production prefab is missing.");
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
                    $"STONEHOLD_METRIC LOD{lodIndex} " +
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
                        $"STONEHOLD_BOUNDS level={level} " +
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
                new GameObject("Stonehold_Workshop_Production");
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
                selection.center = new Vector3(0f, 3.3f, 0f);
                selection.size = new Vector3(9.4f, 6.6f, 7.2f);

                BoxCollider navigation =
                    production.AddComponent<BoxCollider>();
                navigation.isTrigger = false;
                navigation.center = new Vector3(0f, 0.75f, 0f);
                navigation.size = new Vector3(9.0f, 1.5f, 6.4f);

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
                    $"Stonehold LOD{lodIndex} Level {level} has no mesh.");
            }

            MeshBuildResult result = BuildMesh(
                retained,
                lodIndex,
                $"M_Stonehold_Workshop_LOD{lodIndex}_L{level:D2}");
            string meshPath =
                $"{MeshFolder}/M_Stonehold_Workshop_" +
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
            Box(parts, "Foundation", new Vector3(0f, 0.18f, 0f),
                new Vector3(6.4f, 0.36f, 5.4f), 0, 2, true);
            Box(parts, "Floor", new Vector3(0f, 0.39f, 0f),
                new Vector3(5.9f, 0.16f, 4.9f), 1, 1);
            Box(parts, "BackWall", new Vector3(0f, 1.68f, 2.08f),
                new Vector3(5.65f, 2.55f, 0.48f), 1, 2, true);
            Box(parts, "WestWall", new Vector3(-2.58f, 1.62f, 0.2f),
                new Vector3(0.5f, 2.42f, 3.85f), 1, 2, true);
            Box(parts, "EastWall", new Vector3(2.58f, 1.62f, 0.2f),
                new Vector3(0.5f, 2.42f, 3.85f), 1, 2, true);
            Box(parts, "FrontWestPier",
                new Vector3(-2.08f, 1.58f, -2.02f),
                new Vector3(1.45f, 2.35f, 0.52f), 1, 2);
            Box(parts, "FrontEastPier",
                new Vector3(2.08f, 1.58f, -2.02f),
                new Vector3(1.45f, 2.35f, 0.52f), 1, 2);
            Box(parts, "EntranceWestJamb",
                new Vector3(-0.92f, 1.42f, -2.18f),
                new Vector3(0.42f, 1.95f, 0.55f), 0, 2);
            Box(parts, "EntranceEastJamb",
                new Vector3(0.92f, 1.42f, -2.18f),
                new Vector3(0.42f, 1.95f, 0.55f), 0, 2);
            Box(parts, "EntranceLintel",
                new Vector3(0f, 2.48f, -2.18f),
                new Vector3(2.25f, 0.42f, 0.58f), 2, 2);
            Box(parts, "RoofWest", new Vector3(-1.36f, 3.05f, 0.05f),
                new Vector3(3.35f, 0.38f, 4.85f), 2, 2, true,
                Quaternion.Euler(0f, 0f, 14f));
            Box(parts, "RoofEast", new Vector3(1.36f, 3.05f, 0.05f),
                new Vector3(3.35f, 0.38f, 4.85f), 2, 2, true,
                Quaternion.Euler(0f, 0f, -14f));
            Box(parts, "RoofRidge", new Vector3(0f, 3.48f, 0.05f),
                new Vector3(0.46f, 0.46f, 4.98f), 0, 2, true);
            Box(parts, "ChimneyBody", new Vector3(1.72f, 4.02f, 1.08f),
                new Vector3(0.92f, 2.05f, 0.92f), 1, 2, true);
            Box(parts, "ChimneyCap", new Vector3(1.72f, 5.10f, 1.08f),
                new Vector3(1.18f, 0.20f, 1.18f), 2, 1);
            Box(parts, "ForgeBody", new Vector3(1.42f, 0.98f, 0.72f),
                new Vector3(1.45f, 1.05f, 1.35f), 0, 1);
            Box(parts, "ForgeCore", new Vector3(1.42f, 1.08f, -0.01f),
                new Vector3(0.72f, 0.38f, 0.08f), 0, 2, false,
                Quaternion.identity, true);
            Box(parts, "Workbench", new Vector3(-1.25f, 0.80f, 0.82f),
                new Vector3(1.75f, 0.22f, 0.78f), 3, 0);
            Box(parts, "AnvilBase", new Vector3(-0.75f, 0.72f, -0.45f),
                new Vector3(0.52f, 0.65f, 0.42f), 2, 0);
            Box(parts, "AnvilTop", new Vector3(-0.75f, 1.10f, -0.45f),
                new Vector3(0.9f, 0.20f, 0.38f), 2, 1);

            for (int index = 0; index < 7; index++)
            {
                float z = Mathf.Lerp(-2.0f, 2.0f, index / 6f);
                Box(parts, $"RoofBandWest_{index:D2}",
                    new Vector3(-1.36f, 3.27f, z),
                    new Vector3(3.32f, 0.10f, 0.12f), 0,
                    index % 2 == 0 ? 1 : 0, false,
                    Quaternion.Euler(0f, 0f, 14f));
                Box(parts, $"RoofBandEast_{index:D2}",
                    new Vector3(1.36f, 3.27f, z),
                    new Vector3(3.32f, 0.10f, 0.12f), 0,
                    index % 2 == 0 ? 1 : 0, false,
                    Quaternion.Euler(0f, 0f, -14f));
            }

            for (int index = 0; index < 5; index++)
            {
                float x = Mathf.Lerp(-2.25f, 2.25f, index / 4f);
                Box(parts, $"BackCourse_{index:D2}",
                    new Vector3(x, 1.72f, 1.81f),
                    new Vector3(0.82f, 0.34f, 0.08f), 6, 0);
            }
            for (int index = 0; index < 4; index++)
            {
                float x = Mathf.Lerp(-2.65f, 2.65f, index / 3f);
                Box(parts, $"FoundationLock_{index:D2}",
                    new Vector3(x, 0.40f, -2.46f),
                    new Vector3(0.30f, 0.42f, 0.14f), 2, 1);
            }
        }

        private static void AddLevelTwo(List<PartSpec> parts)
        {
            Box(parts, "WestButtressBase",
                new Vector3(-3.25f, 0.72f, 0.82f),
                new Vector3(0.62f, 1.42f, 1.20f), 0, 2, true);
            Box(parts, "EastButtressBase",
                new Vector3(3.25f, 0.72f, 0.82f),
                new Vector3(0.62f, 1.42f, 1.20f), 0, 2, true);
            Box(parts, "WestButtressCap",
                new Vector3(-3.18f, 1.55f, 0.82f),
                new Vector3(0.48f, 0.34f, 0.92f), 1, 1);
            Box(parts, "EastButtressCap",
                new Vector3(3.18f, 1.55f, 0.82f),
                new Vector3(0.48f, 0.34f, 0.92f), 1, 1);
            Box(parts, "WestPressureBand",
                new Vector3(-2.89f, 1.60f, 0.1f),
                new Vector3(0.16f, 2.28f, 2.85f), 2, 1);
            Box(parts, "EastPressureBand",
                new Vector3(2.89f, 1.60f, 0.1f),
                new Vector3(0.16f, 2.28f, 2.85f), 2, 1);
            for (int index = 0; index < 6; index++)
            {
                float z = Mathf.Lerp(-1.55f, 1.55f, index / 5f);
                Box(parts, $"SideLock_{index:D2}",
                    new Vector3(
                        index % 2 == 0 ? -2.9f : 2.9f,
                        1.25f,
                        z),
                    new Vector3(0.18f, 0.32f, 0.42f), 7, 0);
            }
        }

        private static void AddLevelThree(List<PartSpec> parts)
        {
            Box(parts, "EastAnnexFoundation",
                new Vector3(3.43f, 0.22f, -0.45f),
                new Vector3(1.75f, 0.42f, 3.55f), 0, 2, true);
            Box(parts, "EastAnnexBack",
                new Vector3(3.55f, 1.30f, 1.04f),
                new Vector3(1.45f, 1.75f, 0.38f), 1, 2);
            Box(parts, "EastAnnexSide",
                new Vector3(4.10f, 1.22f, -0.45f),
                new Vector3(0.36f, 1.62f, 2.65f), 1, 2, true);
            Box(parts, "EastAnnexRoof",
                new Vector3(3.42f, 2.25f, -0.35f),
                new Vector3(1.85f, 0.30f, 3.35f), 2, 2, true,
                Quaternion.Euler(0f, 0f, -8f));
            Box(parts, "EastAnnexPostFront",
                new Vector3(4.02f, 1.0f, -1.67f),
                new Vector3(0.28f, 1.8f, 0.28f), 3, 1);
            Box(parts, "EastAnnexPostRear",
                new Vector3(4.02f, 1.0f, 0.83f),
                new Vector3(0.28f, 1.8f, 0.28f), 3, 1);
            for (int index = 0; index < 5; index++)
            {
                float z = Mathf.Lerp(-1.55f, 0.85f, index / 4f);
                Box(parts, $"AnnexRoofBand_{index:D2}",
                    new Vector3(3.42f, 2.42f, z),
                    new Vector3(1.8f, 0.08f, 0.12f), 0, 0,
                    false, Quaternion.Euler(0f, 0f, -8f));
            }
        }

        private static void AddLevelFour(List<PartSpec> parts)
        {
            Box(parts, "OuterPortalWest",
                new Vector3(-1.25f, 1.48f, -2.50f),
                new Vector3(0.42f, 2.15f, 0.34f), 0, 2);
            Box(parts, "OuterPortalEast",
                new Vector3(1.25f, 1.48f, -2.50f),
                new Vector3(0.42f, 2.15f, 0.34f), 0, 2);
            Box(parts, "OuterPortalLintel",
                new Vector3(0f, 2.62f, -2.50f),
                new Vector3(2.85f, 0.42f, 0.36f), 2, 2, true);
            Box(parts, "PortalKeystone",
                new Vector3(0f, 2.72f, -2.73f),
                new Vector3(0.46f, 0.48f, 0.14f), 7, 1);
            for (int index = 0; index < 5; index++)
            {
                float x = Mathf.Lerp(-1.85f, 1.85f, index / 4f);
                Box(parts, $"LintelRivet_{index:D2}",
                    new Vector3(x, 2.64f, -2.70f),
                    new Vector3(0.12f, 0.12f, 0.08f), 7, 0);
            }
        }

        private static void AddLevelFive(List<PartSpec> parts)
        {
            Box(parts, "DistrictApron",
                new Vector3(-0.25f, 0.16f, -2.97f),
                new Vector3(6.85f, 0.28f, 0.85f), 0, 2, true);
            Box(parts, "HoistPostWest",
                new Vector3(-2.55f, 1.68f, -2.82f),
                new Vector3(0.28f, 3.0f, 0.28f), 3, 2);
            Box(parts, "HoistPostEast",
                new Vector3(-1.12f, 1.68f, -2.82f),
                new Vector3(0.28f, 3.0f, 0.28f), 3, 1);
            Box(parts, "HoistBeam",
                new Vector3(-1.83f, 3.10f, -2.82f),
                new Vector3(1.85f, 0.28f, 0.28f), 3, 2);
            Cylinder(parts, "HoistDrum",
                new Vector3(-1.83f, 2.72f, -2.82f),
                new Vector3(0.42f, 0.75f, 0.42f), 2, 0,
                false, Quaternion.Euler(0f, 0f, 90f));
            for (int index = 0; index < 6; index++)
            {
                float x = Mathf.Lerp(-3.0f, 2.5f, index / 5f);
                Box(parts, $"ApronCourse_{index:D2}",
                    new Vector3(x, 0.33f, -3.22f),
                    new Vector3(0.72f, 0.16f, 0.22f), 6, 0);
            }
        }

        private static void AddLevelSix(List<PartSpec> parts)
        {
            Box(parts, "ChimneyUpper",
                new Vector3(1.72f, 5.56f, 1.08f),
                new Vector3(1.02f, 0.92f, 1.02f), 1, 2, true);
            Box(parts, "ChimneyUpperBand",
                new Vector3(1.72f, 5.18f, 1.08f),
                new Vector3(1.28f, 0.18f, 1.28f), 2, 1);
            Box(parts, "ChimneyUpperCap",
                new Vector3(1.72f, 6.03f, 1.08f),
                new Vector3(1.42f, 0.20f, 1.42f), 2, 2, true);
            Box(parts, "RearServicePlatform",
                new Vector3(-1.25f, 0.25f, 2.72f),
                new Vector3(3.25f, 0.35f, 1.05f), 0, 2);
            Box(parts, "RearServiceRail",
                new Vector3(-1.25f, 0.92f, 3.05f),
                new Vector3(3.15f, 0.18f, 0.18f), 2, 1);
            for (int index = 0; index < 4; index++)
            {
                float x = Mathf.Lerp(-2.55f, 0.05f, index / 3f);
                Box(parts, $"RearRailPost_{index:D2}",
                    new Vector3(x, 0.68f, 3.05f),
                    new Vector3(0.16f, 0.85f, 0.16f), 2, 0);
            }
        }

        private static void AddLevelSeven(List<PartSpec> parts)
        {
            Box(parts, "WestStorageFoundation",
                new Vector3(-3.45f, 0.20f, 0.1f),
                new Vector3(1.65f, 0.38f, 3.45f), 0, 2);
            Box(parts, "WestStorageWall",
                new Vector3(-4.05f, 1.25f, 0.2f),
                new Vector3(0.38f, 1.75f, 2.75f), 1, 2);
            Box(parts, "WestStorageRoof",
                new Vector3(-3.42f, 2.22f, 0.18f),
                new Vector3(1.82f, 0.30f, 3.42f), 2, 2, true,
                Quaternion.Euler(0f, 0f, 8f));
            Box(parts, "WestStorageFrontPost",
                new Vector3(-4.0f, 1.0f, -1.24f),
                new Vector3(0.28f, 1.8f, 0.28f), 3, 1);
            Box(parts, "WestStorageRearPost",
                new Vector3(-4.0f, 1.0f, 1.58f),
                new Vector3(0.28f, 1.8f, 0.28f), 3, 1);
            for (int index = 0; index < 4; index++)
            {
                float z = Mathf.Lerp(-1.15f, 1.5f, index / 3f);
                Box(parts, $"StorageBand_{index:D2}",
                    new Vector3(-3.42f, 2.38f, z),
                    new Vector3(1.8f, 0.08f, 0.12f), 0, 0,
                    false, Quaternion.Euler(0f, 0f, 8f));
            }
        }

        private static void AddLevelEight(List<PartSpec> parts)
        {
            Box(parts, "RoofCrownSpine",
                new Vector3(0f, 3.78f, 0.05f),
                new Vector3(0.62f, 0.32f, 5.25f), 0, 2, true);
            Box(parts, "RoofCrownCrossFront",
                new Vector3(0f, 3.66f, -1.92f),
                new Vector3(5.95f, 0.22f, 0.22f), 2, 2);
            Box(parts, "RoofCrownCrossRear",
                new Vector3(0f, 3.66f, 1.92f),
                new Vector3(5.95f, 0.22f, 0.22f), 2, 1);
            for (int index = 0; index < 5; index++)
            {
                float z = Mathf.Lerp(-1.65f, 1.65f, index / 4f);
                Cylinder(parts, $"RoofLock_{index:D2}",
                    new Vector3(0f, 3.98f, z),
                    new Vector3(0.26f, 0.20f, 0.26f), 7, 0);
            }
        }

        private static void AddLevelNine(List<PartSpec> parts)
        {
            Box(parts, "LoadingQuay",
                new Vector3(-3.72f, 0.20f, -2.23f),
                new Vector3(1.62f, 0.36f, 2.15f), 0, 2, true);
            Box(parts, "LoadingCanopy",
                new Vector3(-3.62f, 2.42f, -2.05f),
                new Vector3(1.85f, 0.26f, 2.35f), 2, 2,
                false, Quaternion.Euler(0f, 0f, 7f));
            Box(parts, "LoadingPostWest",
                new Vector3(-4.35f, 1.25f, -2.75f),
                new Vector3(0.26f, 2.25f, 0.26f), 3, 1);
            Box(parts, "LoadingPostEast",
                new Vector3(-2.92f, 1.25f, -2.75f),
                new Vector3(0.26f, 2.25f, 0.26f), 3, 1);
            Box(parts, "OreRack",
                new Vector3(-3.65f, 0.75f, -1.75f),
                new Vector3(1.25f, 0.85f, 0.58f), 2, 1);
            for (int index = 0; index < 5; index++)
            {
                float x = Mathf.Lerp(-4.25f, -3.05f, index / 4f);
                Box(parts, $"QuayLock_{index:D2}",
                    new Vector3(x, 0.42f, -3.18f),
                    new Vector3(0.14f, 0.32f, 0.12f), 7, 0);
            }
        }

        private static void AddLevelTen(List<PartSpec> parts)
        {
            Box(parts, "AnvilCrownBase",
                new Vector3(1.72f, 6.16f, 1.08f),
                new Vector3(1.62f, 0.24f, 1.62f), 0, 2, true);
            Box(parts, "AnvilCrownWest",
                new Vector3(1.35f, 6.38f, 1.08f),
                new Vector3(1.18f, 0.20f, 1.34f), 2, 2);
            Box(parts, "AnvilCrownEast",
                new Vector3(2.09f, 6.38f, 1.08f),
                new Vector3(1.18f, 0.20f, 1.34f), 2, 2);
            Box(parts, "CrownEmberSlit",
                new Vector3(1.72f, 6.10f, 0.24f),
                new Vector3(0.58f, 0.16f, 0.07f), 0, 2, false,
                Quaternion.identity, true);
            Box(parts, "WestLandmarkLock",
                new Vector3(-4.25f, 1.48f, 2.45f),
                new Vector3(0.48f, 2.55f, 0.62f), 0, 2, true);
            Box(parts, "EastLandmarkLock",
                new Vector3(4.25f, 1.48f, 2.45f),
                new Vector3(0.48f, 2.55f, 0.62f), 0, 2, true);
            Box(parts, "WestLandmarkCap",
                new Vector3(-4.25f, 2.83f, 2.45f),
                new Vector3(0.68f, 0.22f, 0.82f), 2, 1);
            Box(parts, "EastLandmarkCap",
                new Vector3(4.25f, 2.83f, 2.45f),
                new Vector3(0.68f, 0.22f, 0.82f), 2, 1);
            for (int index = 0; index < 4; index++)
            {
                float y = 0.72f + index * 0.52f;
                Box(parts, $"WestLockBand_{index:D2}",
                    new Vector3(-4.55f, y, 2.45f),
                    new Vector3(0.08f, 0.16f, 0.68f), 7, 0);
                Box(parts, $"EastLockBand_{index:D2}",
                    new Vector3(4.55f, y, 2.45f),
                    new Vector3(0.08f, 0.16f, 0.68f), 7, 0);
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
                name = "T_Stonehold_Workshop_Atlas_1024",
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
                    "Unity did not create the Stonehold atlas importer.");
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
                    "The Stonehold realm motion profile is missing or invalid.");
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
                    !(entry.RealmId == RealmId.Stonehold &&
                        string.Equals(
                            entry.BuildingId,
                            BuildingId,
                            StringComparison.Ordinal)))
                .ToList();
            entries.Add(new KingdomBuildingModelEntry(
                ModelId,
                RealmId.Stonehold,
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
                    "Stonehold/Materials/MAT_Stonehold_Ground.mat");
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
                new Color(0.025f, 0.021f, 0.018f);
            camera.transform.position =
                new Vector3(15.2f, 10.8f, -25.4f);
            camera.transform.LookAt(new Vector3(0f, 2.25f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.76f, 0.52f);
            key.intensity = 1.62f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation =
                Quaternion.Euler(45f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.34f, 0.42f, 0.48f);
            fill.intensity = 0.48f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation =
                Quaternion.Euler(28f, 144f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.17f, 0.15f, 0.135f);
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
                    $"Could not preview Stonehold Level {level}.");
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
                "Assets/AL/Art/Generated/Architecture/Stonehold",
                "Production");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Stonehold/Production",
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
