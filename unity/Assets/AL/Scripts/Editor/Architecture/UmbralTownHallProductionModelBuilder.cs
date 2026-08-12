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
    /// Builds the final cumulative Umbral Town Hall model from the approved
    /// offset protected civic-hall graybox and Umbral Veilwright motion
    /// grammar.
    /// </summary>
    public static class UmbralTownHallProductionModelBuilder
    {
        public const string RuntimeFolder =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Runtime";
        public const string MeshFolder = RuntimeFolder + "/Meshes";
        public const string AtlasPath =
            RuntimeFolder + "/T_Umbral_TownHall_Atlas_1024.png";
        public const string AtlasMaterialPath =
            RuntimeFolder + "/MAT_Umbral_TownHall_Atlas.mat";
        public const string AccentMaterialPath =
            RuntimeFolder + "/MAT_Umbral_TownHall_Accent.mat";
        public const string PrefabPath =
            RuntimeFolder + "/Umbral_TownHall_Production.prefab";
        public const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        public const string MotionProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Umbral_Veilwright_ConstructionProfile.asset";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "UmbralTownHallProductionModel.unity";
        public const string ModelId =
            "building.umbral.townhall.production.v1";

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
            new Color32(75, 76, 83, 255),
            new Color32(50, 51, 58, 255),
            new Color32(38, 35, 40, 255),
            new Color32(19, 19, 26, 255),
            new Color32(145, 103, 61, 255),
            new Color32(98, 94, 96, 255),
            new Color32(45, 44, 53, 255),
            new Color32(52, 45, 76, 255)
        };

        [MenuItem(
            "Another Life/Architecture/" +
            "Build Umbral Town Hall Production Model")]
        public static void Build()
        {
            EnsureFolders();
            Texture2D atlas = CreateAtlas();
            Material atlasMaterial = CreateOrUpdateMaterial(
                AtlasMaterialPath,
                atlas,
                new Color(0.84f, 0.84f, 0.9f),
                Color.black);
            Material accentMaterial = CreateOrUpdateMaterial(
                AccentMaterialPath,
                null,
                new Color(0.29f, 0.22f, 0.42f),
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
                    "umbral-townhall-production",
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
                    $"UMBRAL_TOWNHALL_METRIC LOD{lodIndex} " +
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
                        $"UMBRAL_TOWNHALL_BOUNDS level={level} " +
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
                new GameObject("Umbral_TownHall_Production");
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
                    new Vector3(1.05f, 0f, -4.65f));
                CreateAnchor(
                    production.transform,
                    "CameraFocus",
                    new Vector3(0.2f, 3.55f, 0.1f));
                CreateAnchor(
                    production.transform,
                    "Activity_00",
                    new Vector3(-2.35f, 0f, -3.85f));
                CreateAnchor(
                    production.transform,
                    "Output_00",
                    new Vector3(2.65f, 0f, -3.65f));
                CreateAnchor(
                    production.transform,
                    "Occlusion_Roof",
                    new Vector3(-0.35f, 4.08f, 0.3f));
                CreateAnchor(
                    production.transform,
                    "Occlusion_Canopies",
                    new Vector3(1.05f, 3.4f, -3.55f));
                CreateAnchor(
                    production.transform,
                    "Occlusion_Crown",
                    new Vector3(0.3f, 6.45f, 0.72f));

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
                selection.center = new Vector3(0.15f, 3.5f, -0.3f);
                selection.size = new Vector3(13f, 7f, 12f);

                BoxCollider navigation =
                    production.AddComponent<BoxCollider>();
                navigation.isTrigger = false;
                navigation.center = new Vector3(0.15f, 0.9f, 0.1f);
                navigation.size = new Vector3(12.4f, 1.8f, 10.8f);

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
                $"M_Umbral_TownHall_LOD{lodIndex}_L{level:D2}");
            string meshPath =
                $"{MeshFolder}/M_Umbral_TownHall_" +
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
                indexFormat = IndexFormat.UInt16
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
                    AddUmbralLevelOne(parts);
                    break;
                case 2:
                    AddUmbralLevelTwo(parts);
                    break;
                case 3:
                    AddUmbralLevelThree(parts);
                    break;
                case 4:
                    AddUmbralLevelFour(parts);
                    break;
                case 5:
                    AddUmbralLevelFive(parts);
                    break;
                case 6:
                    AddUmbralLevelSix(parts);
                    break;
                case 7:
                    AddUmbralLevelSeven(parts);
                    break;
                case 8:
                    AddUmbralLevelEight(parts);
                    break;
                case 9:
                    AddUmbralLevelNine(parts);
                    break;
                case 10:
                    AddUmbralLevelTen(parts);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level));
            }
            return parts;
        }

        private static void AddUmbralLevelOne(
            ICollection<PartSpec> parts)
        {
            Quaternion entranceRotation = Quaternion.Euler(0f, -7f, 0f);
            Box(
                parts,
                "CivicPlot",
                new Vector3(0f, 0.16f, 0f),
                new Vector3(9.5f, 0.32f, 8.15f),
                0,
                2,
                true);
            Box(
                parts,
                "ProtectedCouncilFloor",
                new Vector3(-0.25f, 0.36f, 0.15f),
                new Vector3(7.1f, 0.22f, 5.9f),
                5,
                2);
            Box(
                parts,
                "ObliquePublicApproach",
                new Vector3(1.05f, 0.18f, -3.95f),
                new Vector3(3.5f, 0.22f, 1.55f),
                5,
                2,
                true,
                entranceRotation);
            Box(
                parts,
                "StepUpper",
                new Vector3(1.05f, 0.42f, -3.55f),
                new Vector3(2.75f, 0.25f, 0.72f),
                5,
                2,
                false,
                entranceRotation);
            Box(
                parts,
                "StepMiddle",
                new Vector3(1.05f, 0.28f, -3.92f),
                new Vector3(3.2f, 0.2f, 0.76f),
                5,
                1,
                false,
                entranceRotation);
            Box(
                parts,
                "StepLower",
                new Vector3(1.05f, 0.14f, -4.3f),
                new Vector3(3.65f, 0.18f, 0.78f),
                5,
                2,
                false,
                entranceRotation);

            Box(
                parts,
                "WestCouncilMass",
                new Vector3(-1.25f, 2.08f, 0.3f),
                new Vector3(5.35f, 3.4f, 5.75f),
                0,
                2,
                true);
            Box(
                parts,
                "EastRecordsMass",
                new Vector3(2.45f, 1.72f, -0.2f),
                new Vector3(2.85f, 2.68f, 4.65f),
                1,
                2,
                true);
            Box(
                parts,
                "EntranceRecess",
                new Vector3(1.05f, 1.52f, -2.62f),
                new Vector3(2.05f, 2.28f, 0.2f),
                3,
                1,
                false,
                entranceRotation);
            Box(
                parts,
                "EntrancePierWest",
                new Vector3(0f, 1.68f, -2.78f),
                new Vector3(0.5f, 2.65f, 0.62f),
                1,
                2,
                true,
                entranceRotation);
            Box(
                parts,
                "EntrancePierEast",
                new Vector3(2.1f, 1.68f, -2.78f),
                new Vector3(0.5f, 2.65f, 0.62f),
                1,
                2,
                true,
                entranceRotation);
            Box(
                parts,
                "EntranceLintel",
                new Vector3(1.05f, 3.05f, -2.78f),
                new Vector3(2.65f, 0.42f, 0.66f),
                4,
                2,
                true,
                entranceRotation);
            Box(
                parts,
                "DarkglassCivicInset",
                new Vector3(2.48f, 2.05f, -2.56f),
                new Vector3(0.52f, 0.82f, 0.16f),
                7,
                1,
                false,
                Quaternion.identity,
                true);

            AddRoofPair(
                parts,
                "WestCouncilRoof",
                new Vector3(-1.25f, 4.08f, 0.3f),
                3f,
                6.05f,
                18f,
                2,
                true);
            Box(
                parts,
                "WestCouncilRidge",
                new Vector3(-1.25f, 4.89f, 0.3f),
                new Vector3(0.22f, 0.2f, 6.2f),
                4,
                2,
                true);
            AddRoofPair(
                parts,
                "EastRecordsRoof",
                new Vector3(2.45f, 3.32f, -0.2f),
                1.75f,
                4.9f,
                15f,
                1,
                true);
            Box(
                parts,
                "EastRecordsRidge",
                new Vector3(2.45f, 3.79f, -0.2f),
                new Vector3(0.18f, 0.18f, 5.05f),
                4,
                2,
                true);

            foreach (float x in new[] { -3.45f, -2.25f, 0.45f, 3.55f })
            {
                Box(
                    parts,
                    $"AshGalleryPost_{x:0.00}",
                    new Vector3(x, 1.7f, 2.72f),
                    new Vector3(0.18f, 2.15f, 0.18f),
                    2,
                    1);
            }
        }

        private static void AddUmbralLevelTwo(
            ICollection<PartSpec> parts)
        {
            Vector3[] positions = BoundaryPierPositions();
            for (int index = 0; index < positions.Length; index++)
            {
                Box(
                    parts,
                    $"FoundationLock_{index:D2}",
                    positions[index] + new Vector3(0f, 0.55f, 0f),
                    new Vector3(0.76f, 0.82f, 0.76f),
                    1,
                    2);
            }
            Box(
                parts,
                "WestRetainingEdge",
                new Vector3(-4.5f, 0.36f, 0.05f),
                new Vector3(0.3f, 0.38f, 6.45f),
                1,
                1);
            Box(
                parts,
                "EastRetainingEdge",
                new Vector3(4.65f, 0.36f, -0.1f),
                new Vector3(0.3f, 0.38f, 6.25f),
                1,
                2);
        }

        private static void AddUmbralLevelThree(
            ICollection<PartSpec> parts)
        {
            AddCivicGallery(
                parts,
                "RecordsGallery",
                -4.45f,
                0.55f,
                4.5f,
                -9f);
        }

        private static void AddUmbralLevelFour(
            ICollection<PartSpec> parts)
        {
            Quaternion entranceRotation = Quaternion.Euler(0f, -7f, 0f);
            Box(
                parts,
                "PublicThresholdCanopy",
                new Vector3(1.05f, 3.4f, -3.55f),
                new Vector3(3.9f, 0.22f, 1.48f),
                2,
                2,
                true,
                Quaternion.Euler(-7f, -7f, 0f));
            Box(
                parts,
                "PublicPostWest",
                new Vector3(-0.6f, 1.85f, -3.88f),
                new Vector3(0.22f, 2.45f, 0.22f),
                4,
                2);
            Box(
                parts,
                "PublicPostEast",
                new Vector3(2.7f, 1.85f, -3.88f),
                new Vector3(0.22f, 2.45f, 0.22f),
                4,
                2);
            Box(
                parts,
                "PublicThresholdBrace",
                new Vector3(1.05f, 3.12f, -3.82f),
                new Vector3(3.45f, 0.2f, 0.22f),
                4,
                2,
                false,
                entranceRotation);
            Box(
                parts,
                "PublicNoticePlinth",
                new Vector3(-1.85f, 0.72f, -4.05f),
                new Vector3(1f, 0.95f, 0.7f),
                1,
                1);
        }

        private static void AddUmbralLevelFive(
            ICollection<PartSpec> parts)
        {
            Vector3[] positions = BoundaryPierPositions();
            for (int index = 0; index < positions.Length; index++)
            {
                AddBoundaryPier(parts, index, positions[index]);
            }

            Beam(
                parts,
                "BoundaryBraceFrontWest",
                positions[0] + new Vector3(0f, 4.62f, 0f),
                new Vector3(-2.8f, 4.48f, -1.82f),
                0.18f,
                2,
                2,
                true);
            Beam(
                parts,
                "BoundaryBraceFrontEast",
                positions[1] + new Vector3(0f, 4.62f, 0f),
                new Vector3(3.35f, 3.82f, -1.72f),
                0.18f,
                2,
                2,
                true);
            Beam(
                parts,
                "BoundaryBraceRearWest",
                positions[2] + new Vector3(0f, 4.62f, 0f),
                new Vector3(-2.72f, 4.48f, 2.08f),
                0.18f,
                2,
                2,
                true);
            Beam(
                parts,
                "BoundaryBraceRearEast",
                positions[3] + new Vector3(0f, 4.62f, 0f),
                new Vector3(3.42f, 3.82f, 1.92f),
                0.18f,
                2,
                2,
                true);
        }

        private static void AddUmbralLevelSix(
            ICollection<PartSpec> parts)
        {
            AddCivicGallery(
                parts,
                "StewardGallery",
                4.7f,
                0.2f,
                4.15f,
                9f);
        }

        private static void AddUmbralLevelSeven(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "UpperCouncilClerestory",
                new Vector3(-0.35f, 4.65f, 0.72f),
                new Vector3(3.55f, 1.05f, 2.35f),
                0,
                2,
                true);
            AddRoofPair(
                parts,
                "UpperCouncilRoof",
                new Vector3(-0.35f, 5.36f, 0.72f),
                2.05f,
                2.58f,
                17f,
                2,
                true);
            Box(
                parts,
                "UpperCouncilRidge",
                new Vector3(-0.35f, 5.91f, 0.72f),
                new Vector3(0.2f, 0.18f, 2.72f),
                4,
                2,
                true);
        }

        private static void AddUmbralLevelEight(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "RearServiceGallery",
                new Vector3(0.15f, 1.38f, 3.55f),
                new Vector3(7.15f, 1.4f, 1.18f),
                1,
                2,
                true);
            Box(
                parts,
                "RearServiceRoof",
                new Vector3(0.15f, 2.18f, 3.55f),
                new Vector3(7.5f, 0.22f, 1.5f),
                2,
                2,
                true,
                Quaternion.Euler(8f, 0f, 0f));
            foreach (float x in new[] { -2.75f, 3.05f })
            {
                Box(
                    parts,
                    $"RearServicePost_{x:0.0}",
                    new Vector3(x, 1.12f, 4.02f),
                    new Vector3(0.2f, 1.6f, 0.2f),
                    4,
                    1);
                Box(
                    parts,
                    $"RearServiceDrain_{x:0.0}",
                    new Vector3(x, 1.18f, 4.22f),
                    new Vector3(0.14f, 1.72f, 0.14f),
                    4,
                    1);
            }
        }

        private static void AddUmbralLevelNine(
            ICollection<PartSpec> parts)
        {
            Box(
                parts,
                "ForecourtEdgeWest",
                new Vector3(-3.6f, 0.28f, -5f),
                new Vector3(3.2f, 0.34f, 0.48f),
                1,
                2,
                true,
                Quaternion.Euler(0f, -4f, 0f));
            Box(
                parts,
                "ForecourtEdgeEast",
                new Vector3(4.45f, 0.28f, -4.82f),
                new Vector3(3.1f, 0.34f, 0.48f),
                1,
                2,
                true,
                Quaternion.Euler(0f, 6f, 0f));
            Box(
                parts,
                "ApproachCenter",
                new Vector3(1.05f, 0.12f, -5.15f),
                new Vector3(4.05f, 0.18f, 1.02f),
                5,
                2,
                true,
                Quaternion.Euler(0f, -7f, 0f));
            Box(
                parts,
                "NoticePierWest",
                new Vector3(-5f, 0.82f, -4.72f),
                new Vector3(0.58f, 1.35f, 0.58f),
                1,
                1);
            Box(
                parts,
                "NoticePierEast",
                new Vector3(5.45f, 0.82f, -4.72f),
                new Vector3(0.58f, 1.35f, 0.58f),
                1,
                2);
            foreach (float x in new[] { -5f, 5.45f })
            {
                Box(
                    parts,
                    $"NoticePierCap_{x:0.0}",
                    new Vector3(x, 1.55f, -4.72f),
                    new Vector3(0.72f, 0.16f, 0.72f),
                    4,
                    1);
            }
        }

        private static void AddUmbralLevelTen(
            ICollection<PartSpec> parts)
        {
            Vector3[] positions = BoundaryPierPositions();
            Beam(
                parts,
                "FrontWestLoadRail",
                positions[0] + new Vector3(0f, 4.82f, 0f),
                new Vector3(-2.45f, 5.98f, 0.1f),
                0.2f,
                2,
                2,
                true);
            Beam(
                parts,
                "FrontEastLoadRail",
                positions[1] + new Vector3(0f, 4.82f, 0f),
                new Vector3(2.75f, 5.98f, 0.1f),
                0.2f,
                2,
                2,
                true);
            Beam(
                parts,
                "RearWestLoadRail",
                positions[2] + new Vector3(0f, 4.82f, 0f),
                new Vector3(-2.2f, 6.1f, 1.35f),
                0.2f,
                2,
                2,
                true);
            Beam(
                parts,
                "RearEastLoadRail",
                positions[3] + new Vector3(0f, 4.82f, 0f),
                new Vector3(3f, 6.1f, 1.35f),
                0.2f,
                2,
                2,
                true);

            AddYokeFrame(parts, "FrontYoke", 0.15f, 0.1f, 5.98f);
            AddYokeFrame(parts, "RearYoke", 0.4f, 1.35f, 6.1f);

            Beam(
                parts,
                "WestUpperYokeConnector",
                new Vector3(-2.45f, 6.62f, 0.1f),
                new Vector3(-2.2f, 6.74f, 1.35f),
                0.14f,
                4,
                2,
                true);
            Beam(
                parts,
                "EastUpperYokeConnector",
                new Vector3(2.75f, 6.62f, 0.1f),
                new Vector3(3f, 6.74f, 1.35f),
                0.14f,
                4,
                2,
                true);
            Beam(
                parts,
                "WestLowerYokeConnector",
                new Vector3(-2.45f, 5.98f, 0.1f),
                new Vector3(-2.2f, 6.1f, 1.35f),
                0.12f,
                2,
                2,
                true);
            Beam(
                parts,
                "EastLowerYokeConnector",
                new Vector3(2.75f, 5.98f, 0.1f),
                new Vector3(3f, 6.1f, 1.35f),
                0.12f,
                2,
                2,
                true);
        }

        private static void AddCivicGallery(
            ICollection<PartSpec> parts,
            string prefix,
            float x,
            float z,
            float depth,
            float roofPitch)
        {
            Box(
                parts,
                $"{prefix}Plinth",
                new Vector3(x, 0.42f, z),
                new Vector3(2.15f, 0.5f, depth + 0.45f),
                1,
                2,
                true);
            Box(
                parts,
                $"{prefix}Body",
                new Vector3(x, 1.6f, z),
                new Vector3(1.82f, 1.85f, depth),
                0,
                2,
                true);
            Box(
                parts,
                $"{prefix}Roof",
                new Vector3(x, 2.72f, z),
                new Vector3(2.22f, 0.24f, depth + 0.5f),
                2,
                2,
                true,
                Quaternion.Euler(0f, 0f, roofPitch));
            foreach (float xOffset in new[] { -0.7f, 0.7f })
            {
                foreach (float zOffset in new[] { -1.5f, 1.5f })
                {
                    Box(
                        parts,
                        $"{prefix}Post_{xOffset:0.0}_{zOffset:0.0}",
                        new Vector3(
                            x + xOffset,
                            1.48f,
                            z + zOffset),
                        new Vector3(0.25f, 1.85f, 0.25f),
                        2,
                        1);
                }
            }
        }

        private static Vector3[] BoundaryPierPositions()
        {
            return new[]
            {
                new Vector3(-4.35f, 0f, -2.75f),
                new Vector3(4.65f, 0f, -2.75f),
                new Vector3(-4.05f, 0f, 3.08f),
                new Vector3(4.95f, 0f, 3.08f)
            };
        }

        private static void AddBoundaryPier(
            ICollection<PartSpec> parts,
            int index,
            Vector3 position)
        {
            Box(
                parts,
                $"BoundaryPierBase_{index:D2}",
                position + new Vector3(0f, 0.78f, 0f),
                new Vector3(1.25f, 1.15f, 1.25f),
                1,
                2,
                true);
            Box(
                parts,
                $"BoundaryPierBody_{index:D2}",
                position + new Vector3(0f, 2.82f, 0f),
                new Vector3(0.9f, 3.45f, 0.9f),
                0,
                2,
                true);
            Box(
                parts,
                $"BoundaryPierCap_{index:D2}",
                position + new Vector3(0f, 4.68f, 0f),
                new Vector3(1.08f, 0.3f, 1.08f),
                4,
                2,
                true);
        }

        private static void AddYokeFrame(
            ICollection<PartSpec> parts,
            string prefix,
            float centerX,
            float z,
            float lowerHeight)
        {
            Box(
                parts,
                $"{prefix}LowerBar",
                new Vector3(centerX, lowerHeight, z),
                new Vector3(5.2f, 0.28f, 0.34f),
                2,
                2,
                true);
            Box(
                parts,
                $"{prefix}UpperBar",
                new Vector3(centerX + 0.12f, lowerHeight + 0.64f, z),
                new Vector3(5.05f, 0.28f, 0.34f),
                2,
                2,
                true,
                Quaternion.Euler(0f, 0f, 1f));
            Box(
                parts,
                $"{prefix}SlitBoundaryWest",
                new Vector3(centerX - 0.42f, lowerHeight + 0.32f, z),
                new Vector3(0.2f, 0.38f, 0.38f),
                4,
                2,
                true);
            Box(
                parts,
                $"{prefix}SlitBoundaryEast",
                new Vector3(centerX + 0.42f, lowerHeight + 0.32f, z),
                new Vector3(0.2f, 0.38f, 0.38f),
                4,
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
                name = "T_Umbral_TownHall_Atlas_1024",
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
            ArchitectureConstructionAnimationProfile motionProfile =
                AssetDatabase.LoadAssetAtPath<
                    ArchitectureConstructionAnimationProfile>(
                        MotionProfilePath);
            if (motionProfile == null || !motionProfile.IsConfigured)
            {
                throw new InvalidOperationException(
                    "The Umbral realm motion profile is missing or invalid.");
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
            InstantiatePreview(prefab, 1, new Vector3(-13f, 0f, 0f));
            InstantiatePreview(prefab, 6, Vector3.zero);
            InstantiatePreview(prefab, 10, new Vector3(13f, 0f, 0f));

            GameObject ground =
                GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ReviewGround";
            ground.transform.localScale =
                new Vector3(4.6f, 1f, 1.35f);
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
            camera.orthographicSize = 9.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor =
                new Color(0.018f, 0.022f, 0.034f);
            camera.transform.position =
                new Vector3(22.5f, 14f, -34f);
            camera.transform.LookAt(new Vector3(0.25f, 3.15f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.82f, 0.87f, 1f);
            key.intensity = 1.78f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation =
                Quaternion.Euler(45f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.68f, 0.42f);
            fill.intensity = 0.78f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation =
                Quaternion.Euler(28f, 144f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.24f, 0.23f, 0.29f);
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
                "Assets/AL/Art/Generated/Architecture/Umbral",
                "Production");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Umbral/Production",
                "TownHall");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
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
