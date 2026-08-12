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
    /// Builds the final cumulative Crownlands Stormwright production model
    /// from the approved civic stormwright atelier direction.
    /// </summary>
    public static class CrownlandsStormwrightProductionModelBuilder
    {
        public const string RuntimeFolder =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Production/Runtime";
        public const string MeshFolder = RuntimeFolder + "/Meshes";
        public const string AtlasPath =
            RuntimeFolder + "/T_Crownlands_Stormwright_Atlas_1024.png";
        public const string AtlasMaterialPath =
            RuntimeFolder + "/MAT_Crownlands_Stormwright_Atlas.mat";
        public const string AccentMaterialPath =
            RuntimeFolder + "/MAT_Crownlands_Stormwright_Indigo.mat";
        public const string PrefabPath =
            RuntimeFolder + "/Crownlands_Stormwright_Production.prefab";
        public const string CatalogPath =
            "Assets/AL/ScriptableObjects/Resources/" +
            "KingdomBuildingModelCatalog.asset";
        public const string MotionProfilePath =
            "Assets/AL/Art/Generated/Architecture/Profiles/" +
            "Crownlands_Stormwright_ConstructionProfile.asset";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "CrownlandsStormwrightProductionModel.unity";
        public const string ModelId =
            "building.crownlands.workshop.production.v1";
        public const string LevelTenCapstoneName =
            "Meridian Crown Lantern";

        private const string BuildingId = "Workshop";
        private const int AtlasSize = 1024;
        private const float StrategicBoardScale = 0.12f;

        private static readonly Vector3 SlotEnvelope =
            new Vector3(10f, 7.8f, 8f);
        private static readonly Vector3 MaximumArtBounds =
            new Vector3(9.65f, 7.55f, 7.2f);
        private static readonly float[] LodTransitions =
            { 0.60f, 0.30f, 0.12f, 0.04f };

        private static readonly Color32[] AtlasColors =
        {
            new Color32(160, 164, 170, 255),
            new Color32(198, 202, 210, 255),
            new Color32(91, 104, 128, 255),
            new Color32(43, 66, 128, 255),
            new Color32(26, 43, 91, 255),
            new Color32(118, 94, 44, 255),
            new Color32(216, 220, 226, 255),
            new Color32(70, 118, 210, 255)
        };

        [MenuItem(
            "Another Life/Architecture/" +
            "Build Crownlands Stormwright Production Model")]
        public static void Build()
        {
            EnsureFolders();
            Texture2D atlas = CreateAtlas();
            Material atlasMaterial = CreateOrUpdateMaterial(
                AtlasMaterialPath,
                atlas,
                new Color(0.84f, 0.86f, 0.91f),
                Color.black);
            Material accentMaterial = CreateOrUpdateMaterial(
                AccentMaterialPath,
                null,
                new Color(0.18f, 0.34f, 0.92f),
                new Color(0.035f, 0.08f, 0.32f));

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
                    "Unity did not save the Crownlands production prefab.");
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
                    "The Crownlands production review camera is missing.");
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
                    "crownlands-stormwright-production",
                    "render.png"));
        }

        public static void ReportMetricsFromCommandLine()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "The Crownlands production prefab is missing.");
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
                    $"CROWNLANDS_METRIC LOD{lodIndex} " +
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
                        $"CROWNLANDS_BOUNDS level={level} " +
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
                new GameObject("Crownlands_Stormwright_Production");
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
                    $"Crownlands LOD{lodIndex} Level {level} has no mesh.");
            }

            MeshBuildResult result = BuildMesh(
                retained,
                lodIndex,
                $"M_Crownlands_Stormwright_LOD{lodIndex}_L{level:D2}");
            string meshPath =
                $"{MeshFolder}/M_Crownlands_Stormwright_" +
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
            Box(parts, "CivicFoundation", new Vector3(0f, 0.18f, 0f),
                new Vector3(6.3f, 0.36f, 5.0f), 0, 2, true);
            Box(parts, "RaisedStoneFloor", new Vector3(0f, 0.40f, 0f),
                new Vector3(5.55f, 0.16f, 4.45f), 1, 1);
            Box(parts, "GroundChannelNorthSouth",
                new Vector3(0f, 0.54f, 0f),
                new Vector3(0.13f, 0.08f, 4.15f), 5, 1);
            Box(parts, "GroundChannelEastWest",
                new Vector3(0f, 0.55f, 0f),
                new Vector3(4.75f, 0.08f, 0.13f), 5, 1);
            Box(parts, "BackPaleWall", new Vector3(0f, 1.56f, 1.92f),
                new Vector3(5.35f, 2.35f, 0.44f), 1, 2, true);
            Box(parts, "WestPaleWall", new Vector3(-2.48f, 1.48f, 0.06f),
                new Vector3(0.44f, 2.18f, 3.65f), 1, 2);
            Box(parts, "EastPaleWall", new Vector3(2.48f, 1.48f, 0.06f),
                new Vector3(0.44f, 2.18f, 3.65f), 1, 2);
            Box(parts, "WestFrontPier", new Vector3(-2.12f, 1.76f, -1.88f),
                new Vector3(0.78f, 3.12f, 0.78f), 1, 2, true);
            Box(parts, "EastFrontPier", new Vector3(2.12f, 1.76f, -1.88f),
                new Vector3(0.78f, 3.12f, 0.78f), 1, 2, true);
            Box(parts, "WestPierSilverCap",
                new Vector3(-2.12f, 3.40f, -1.88f),
                new Vector3(0.96f, 0.26f, 0.96f), 6, 1);
            Box(parts, "EastPierSilverCap",
                new Vector3(2.12f, 3.40f, -1.88f),
                new Vector3(0.96f, 0.26f, 0.96f), 6, 1);
            Box(parts, "BroadSilverArchWest",
                new Vector3(-1.80f, 1.66f, -2.05f),
                new Vector3(0.32f, 1.52f, 0.42f), 6, 2, true);
            Box(parts, "BroadSilverArchEast",
                new Vector3(1.80f, 1.66f, -2.05f),
                new Vector3(0.32f, 1.52f, 0.42f), 6, 2, true);
            for (int index = 0; index < 9; index++)
            {
                float angle = Mathf.Lerp(20f, 160f, index / 8f);
                float radians = angle * Mathf.Deg2Rad;
                Box(parts, $"BroadSilverArchSegment_{index:D2}",
                    new Vector3(
                        Mathf.Cos(radians) * 1.80f,
                        1.72f + Mathf.Sin(radians) * 1.55f,
                        -2.05f),
                    new Vector3(0.74f, 0.27f, 0.42f),
                    6,
                    2,
                    index == 4,
                    Quaternion.Euler(0f, 0f, angle + 90f));
            }
            Box(parts, "RoofWestBlueStep", new Vector3(-1.35f, 3.72f, 0f),
                new Vector3(3.35f, 0.34f, 4.65f), 3, 2, true,
                Quaternion.Euler(0f, 0f, 10f));
            Box(parts, "RoofEastBlueStep", new Vector3(1.35f, 3.72f, 0f),
                new Vector3(3.35f, 0.34f, 4.65f), 3, 2, true,
                Quaternion.Euler(0f, 0f, -10f));
            Box(parts, "RoofWestSilverEave",
                new Vector3(-1.55f, 3.58f, -0.02f),
                new Vector3(3.45f, 0.12f, 4.82f), 6, 1);
            Box(parts, "RoofEastSilverEave",
                new Vector3(1.55f, 3.58f, -0.02f),
                new Vector3(3.45f, 0.12f, 4.82f), 6, 1);
            Box(parts, "RoofWestUpperBlueStep",
                new Vector3(-0.76f, 4.03f, 0f),
                new Vector3(2.15f, 0.24f, 4.34f), 4, 1, true,
                Quaternion.Euler(0f, 0f, 7f));
            Box(parts, "RoofEastUpperBlueStep",
                new Vector3(0.76f, 4.03f, 0f),
                new Vector3(2.15f, 0.24f, 4.34f), 4, 1, true,
                Quaternion.Euler(0f, 0f, -7f));
            Box(parts, "BlueRoofRidge", new Vector3(0f, 4.02f, 0f),
                new Vector3(0.48f, 0.38f, 4.82f), 4, 2);
            Cylinder(parts, "CompactLanternBase",
                new Vector3(0f, 4.38f, 0f),
                new Vector3(0.92f, 0.22f, 0.92f), 6, 2);
            Cylinder(parts, "CompactLanternDrum",
                new Vector3(0f, 4.76f, 0f),
                new Vector3(0.58f, 0.58f, 0.58f), 6, 2, true);
            Cylinder(parts, "CompactBlueLanternCap",
                new Vector3(0f, 5.08f, 0f),
                new Vector3(0.76f, 0.18f, 0.76f), 3, 2, true);
            Cylinder(parts, "CalibrationFloorRing",
                new Vector3(0f, 0.61f, -0.20f),
                new Vector3(1.86f, 0.16f, 1.86f), 6, 2, true);
            Cylinder(parts, "CalibrationFloorCore",
                new Vector3(0f, 0.71f, -0.20f),
                new Vector3(1.24f, 0.10f, 1.24f), 4, 2);
            Cylinder(parts, "CalibrationAxis",
                new Vector3(0f, 0.89f, -0.20f),
                new Vector3(0.42f, 0.42f, 0.42f), 5, 1);
            Box(parts, "CalibrationEngine",
                new Vector3(0f, 1.02f, 0.25f),
                new Vector3(1.22f, 1.00f, 1.08f), 5, 1);
            Box(parts, "ContainedIndigoCore",
                new Vector3(0f, 1.18f, -0.36f),
                new Vector3(0.54f, 0.34f, 0.08f), 7, 2, false,
                Quaternion.identity, true);
        }

        private static void AddLevelTwo(List<PartSpec> parts)
        {
            Box(parts, "WestRearPier",
                new Vector3(-2.62f, 1.50f, 1.88f),
                new Vector3(0.62f, 2.55f, 0.62f), 1, 2);
            Box(parts, "EastRearPier",
                new Vector3(2.62f, 1.50f, 1.88f),
                new Vector3(0.62f, 2.55f, 0.62f), 1, 2);
            Box(parts, "WestRearPierCap",
                new Vector3(-2.62f, 2.88f, 1.88f),
                new Vector3(0.78f, 0.22f, 0.78f), 6, 1);
            Box(parts, "EastRearPierCap",
                new Vector3(2.62f, 2.88f, 1.88f),
                new Vector3(0.78f, 0.22f, 0.78f), 6, 1);
            Box(parts, "WestWallConductor",
                new Vector3(-2.75f, 1.72f, 0f),
                new Vector3(0.12f, 2.20f, 3.20f), 5, 1);
            Box(parts, "EastWallConductor",
                new Vector3(2.75f, 1.72f, 0f),
                new Vector3(0.12f, 2.20f, 3.20f), 5, 1);
            for (int index = 0; index < 6; index++)
            {
                float z = Mathf.Lerp(-1.55f, 1.55f, index / 5f);
                Box(parts, $"GroundingSocket_{index:D2}",
                    new Vector3(
                        index % 2 == 0 ? -2.92f : 2.92f,
                        1.25f,
                        z),
                    new Vector3(0.16f, 0.30f, 0.38f), 6, 0);
            }
        }

        private static void AddLevelThree(List<PartSpec> parts)
        {
            Box(parts, "EastInstrumentBayFloor",
                new Vector3(3.38f, 0.25f, -0.30f),
                new Vector3(1.55f, 0.36f, 3.05f), 0, 2);
            Box(parts, "EastInstrumentBayWall",
                new Vector3(3.95f, 1.28f, -0.22f),
                new Vector3(0.34f, 1.75f, 2.55f), 1, 2);
            Box(parts, "EastInstrumentBayBench",
                new Vector3(3.32f, 0.95f, -0.25f),
                new Vector3(1.10f, 0.24f, 1.55f), 5, 1);
            Box(parts, "EastBayBlueAwning",
                new Vector3(3.38f, 2.26f, -0.25f),
                new Vector3(1.70f, 0.26f, 3.05f), 3, 2,
                false,
                Quaternion.Euler(0f, 0f, -7f));
            Cylinder(parts, "EastInstrumentDial",
                new Vector3(3.30f, 1.48f, -1.08f),
                new Vector3(0.62f, 0.16f, 0.62f), 6, 1,
                false, Quaternion.Euler(90f, 0f, 0f));
            for (int index = 0; index < 5; index++)
            {
                float z = Mathf.Lerp(-1.55f, 0.85f, index / 4f);
                Box(parts, $"EastAwningRib_{index:D2}",
                    new Vector3(3.38f, 2.42f, z),
                    new Vector3(1.66f, 0.08f, 0.10f), 6, 0,
                    false, Quaternion.Euler(0f, 0f, -7f));
            }
        }

        private static void AddLevelFour(List<PartSpec> parts)
        {
            Box(parts, "OuterSilverPortalWest",
                new Vector3(-1.35f, 1.58f, -2.52f),
                new Vector3(0.32f, 2.28f, 0.34f), 6, 2);
            Box(parts, "OuterSilverPortalEast",
                new Vector3(1.35f, 1.58f, -2.52f),
                new Vector3(0.32f, 2.28f, 0.34f), 6, 2);
            Box(parts, "OuterSilverPortalLintel",
                new Vector3(0f, 2.82f, -2.52f),
                new Vector3(3.08f, 0.34f, 0.36f), 6, 2);
            Box(parts, "RoyalKeystone",
                new Vector3(0f, 3.04f, -2.72f),
                new Vector3(0.42f, 0.56f, 0.12f), 5, 1);
            for (int index = 0; index < 5; index++)
            {
                float x = Mathf.Lerp(-1.85f, 1.85f, index / 4f);
                Box(parts, $"PortalInsulator_{index:D2}",
                    new Vector3(x, 2.86f, -2.70f),
                    new Vector3(0.12f, 0.12f, 0.08f), 7, 0);
            }
        }

        private static void AddLevelFive(List<PartSpec> parts)
        {
            Box(parts, "CivicApproachApron",
                new Vector3(-0.25f, 0.16f, -2.97f),
                new Vector3(6.95f, 0.28f, 0.78f), 0, 2, true);
            Box(parts, "WestCalibrationBench",
                new Vector3(-3.05f, 0.92f, -0.78f),
                new Vector3(1.28f, 0.28f, 1.62f), 5, 1);
            Box(parts, "WestBenchCanopyPostA",
                new Vector3(-3.62f, 1.38f, -1.48f),
                new Vector3(0.20f, 1.72f, 0.20f), 6, 1);
            Box(parts, "WestBenchCanopyPostB",
                new Vector3(-2.50f, 1.38f, -1.48f),
                new Vector3(0.20f, 1.72f, 0.20f), 6, 1);
            Box(parts, "WestBenchCanopy",
                new Vector3(-3.06f, 2.26f, -0.78f),
                new Vector3(1.45f, 0.22f, 1.78f), 3, 2,
                false, Quaternion.Euler(0f, 0f, 6f));
            for (int index = 0; index < 6; index++)
            {
                float x = Mathf.Lerp(-3.0f, 2.5f, index / 5f);
                Box(parts, $"ApronInlay_{index:D2}",
                    new Vector3(x, 0.33f, -3.22f),
                    new Vector3(0.68f, 0.12f, 0.18f), 6, 0);
            }
        }

        private static void AddLevelSix(List<PartSpec> parts)
        {
            Cylinder(parts, "AdvancedLanternPedestal",
                new Vector3(0f, 5.28f, 0f),
                new Vector3(1.20f, 0.30f, 1.20f), 6, 2);
            Cylinder(parts, "AdvancedLanternDrum",
                new Vector3(0f, 5.76f, 0f),
                new Vector3(0.82f, 0.76f, 0.82f), 6, 2, true);
            Cylinder(parts, "AdvancedBlueLanternCap",
                new Vector3(0f, 6.24f, 0f),
                new Vector3(1.08f, 0.20f, 1.08f), 3, 2, true);
            Box(parts, "RearServicePlatform",
                new Vector3(0f, 0.25f, 2.72f),
                new Vector3(4.15f, 0.34f, 0.98f), 0, 2);
            Box(parts, "RearConductorRail",
                new Vector3(0f, 0.92f, 3.05f),
                new Vector3(4.10f, 0.16f, 0.16f), 5, 1);
            for (int index = 0; index < 4; index++)
            {
                float x = Mathf.Lerp(-1.8f, 1.8f, index / 3f);
                Box(parts, $"RearRailInsulator_{index:D2}",
                    new Vector3(x, 0.68f, 3.05f),
                    new Vector3(0.14f, 0.82f, 0.14f), 6, 0);
            }
        }

        private static void AddLevelSeven(List<PartSpec> parts)
        {
            Box(parts, "WestInstrumentBayFloor",
                new Vector3(-3.40f, 0.24f, 0.38f),
                new Vector3(1.55f, 0.36f, 3.05f), 0, 2);
            Box(parts, "WestInstrumentBayWall",
                new Vector3(-3.98f, 1.28f, 0.38f),
                new Vector3(0.34f, 1.75f, 2.55f), 1, 2);
            Box(parts, "WestInstrumentBayBench",
                new Vector3(-3.30f, 0.95f, 0.42f),
                new Vector3(1.10f, 0.24f, 1.55f), 5, 1);
            Box(parts, "WestBayBlueAwning",
                new Vector3(-3.38f, 2.26f, 0.38f),
                new Vector3(1.70f, 0.26f, 3.05f), 3, 2,
                false,
                Quaternion.Euler(0f, 0f, 7f));
            Cylinder(parts, "WestInstrumentDial",
                new Vector3(-3.30f, 1.48f, 1.25f),
                new Vector3(0.62f, 0.16f, 0.62f), 6, 1,
                false, Quaternion.Euler(90f, 0f, 0f));
            for (int index = 0; index < 4; index++)
            {
                float z = Mathf.Lerp(-1.15f, 1.5f, index / 3f);
                Box(parts, $"WestAwningRib_{index:D2}",
                    new Vector3(-3.38f, 2.42f, z),
                    new Vector3(1.66f, 0.08f, 0.10f), 6, 0,
                    false, Quaternion.Euler(0f, 0f, 7f));
            }
        }

        private static void AddLevelEight(List<PartSpec> parts)
        {
            Box(parts, "RoofUpperWestStep",
                new Vector3(-1.02f, 4.20f, -0.02f),
                new Vector3(2.55f, 0.25f, 4.38f), 4, 2, true,
                Quaternion.Euler(0f, 0f, 8f));
            Box(parts, "RoofUpperEastStep",
                new Vector3(1.02f, 4.20f, -0.02f),
                new Vector3(2.55f, 0.25f, 4.38f), 4, 2, true,
                Quaternion.Euler(0f, 0f, -8f));
            Box(parts, "RoofSilverSpine",
                new Vector3(0f, 4.48f, -0.02f),
                new Vector3(0.46f, 0.28f, 4.64f), 6, 2);
            for (int index = 0; index < 5; index++)
            {
                float z = Mathf.Lerp(-1.65f, 1.65f, index / 4f);
                Cylinder(parts, $"RoofCeramicIsolator_{index:D2}",
                    new Vector3(0f, 3.98f, z),
                    new Vector3(0.24f, 0.18f, 0.24f), 7, 0);
            }
        }

        private static void AddLevelNine(List<PartSpec> parts)
        {
            Box(parts, "SouthCivicBalustrade",
                new Vector3(0f, 0.88f, -3.05f),
                new Vector3(6.75f, 0.24f, 0.20f), 6, 2);
            Box(parts, "WestWeatherShutter",
                new Vector3(-2.88f, 1.72f, -0.72f),
                new Vector3(0.18f, 1.35f, 1.10f), 3, 1);
            Box(parts, "EastWeatherShutter",
                new Vector3(2.88f, 1.72f, -0.72f),
                new Vector3(0.18f, 1.35f, 1.10f), 3, 1);
            Box(parts, "CentralConductorReturn",
                new Vector3(0f, 2.66f, 1.95f),
                new Vector3(0.18f, 2.05f, 0.18f), 5, 2);
            Box(parts, "RearReturnSocket",
                new Vector3(0f, 1.54f, 2.72f),
                new Vector3(1.05f, 0.38f, 0.32f), 6, 1);
            for (int index = 0; index < 5; index++)
            {
                float x = Mathf.Lerp(-3.05f, 3.05f, index / 4f);
                Box(parts, $"BalustradeFinial_{index:D2}",
                    new Vector3(x, 1.13f, -3.05f),
                    new Vector3(0.16f, 0.42f, 0.16f), 6, 0);
            }
        }

        private static void AddLevelTen(List<PartSpec> parts)
        {
            Cylinder(parts, LevelTenCapstoneName + " Base",
                new Vector3(0f, 6.08f, 0f),
                new Vector3(1.58f, 0.26f, 1.58f), 6, 2);
            Cylinder(parts, LevelTenCapstoneName + " Tall Body",
                new Vector3(0f, 6.68f, 0f),
                new Vector3(1.02f, 1.18f, 1.02f), 6, 2, true);
            Cylinder(parts, LevelTenCapstoneName + " Blue Crown",
                new Vector3(0f, 7.30f, 0f),
                new Vector3(1.32f, 0.22f, 1.32f), 3, 2, true);
            Box(parts, "ContainedIndigoCalibrationAperture",
                new Vector3(0f, 6.68f, -0.56f),
                new Vector3(0.48f, 0.38f, 0.07f), 7, 2, false,
                Quaternion.identity, true);
            Box(parts, "WestMeridianRib",
                new Vector3(-0.72f, 6.62f, 0f),
                new Vector3(0.16f, 1.28f, 0.18f), 6, 2,
                false,
                Quaternion.Euler(0f, 0f, -12f));
            Box(parts, "EastMeridianRib",
                new Vector3(0.72f, 6.62f, 0f),
                new Vector3(0.16f, 1.28f, 0.18f), 6, 2,
                false,
                Quaternion.Euler(0f, 0f, 12f));
            Box(parts, "NorthMeridianRib",
                new Vector3(0f, 6.62f, 0.72f),
                new Vector3(0.18f, 1.28f, 0.16f), 6, 2,
                false,
                Quaternion.Euler(12f, 0f, 0f));
            Box(parts, "SouthMeridianRib",
                new Vector3(0f, 6.62f, -0.72f),
                new Vector3(0.18f, 1.28f, 0.16f), 6, 2,
                false,
                Quaternion.Euler(-12f, 0f, 0f));
            for (int index = 0; index < 8; index++)
            {
                float angle = index * 45f;
                float radians = angle * Mathf.Deg2Rad;
                Box(parts, $"MeridianCrownFaceSegment_{index:D2}",
                    new Vector3(
                        Mathf.Cos(radians) * 0.76f,
                        6.68f + Mathf.Sin(radians) * 0.76f,
                        -0.62f),
                    new Vector3(0.52f, 0.12f, 0.13f),
                    6,
                    1,
                    index == 2,
                    Quaternion.Euler(0f, 0f, angle + 90f));
            }
            Box(parts, "WestConductorPylon",
                new Vector3(-4.48f, 1.68f, 2.46f),
                new Vector3(0.46f, 2.95f, 0.54f), 1, 2, true);
            Box(parts, "EastConductorPylon",
                new Vector3(4.48f, 1.68f, 2.46f),
                new Vector3(0.46f, 2.95f, 0.54f), 1, 2, true);
            Box(parts, "WestConductorFinial",
                new Vector3(-4.48f, 3.25f, 2.46f),
                new Vector3(0.66f, 0.30f, 0.74f), 6, 1);
            Box(parts, "EastConductorFinial",
                new Vector3(4.48f, 3.25f, 2.46f),
                new Vector3(0.66f, 0.30f, 0.74f), 6, 1);
            for (int index = 0; index < 4; index++)
            {
                float y = 0.72f + index * 0.52f;
                Box(parts, $"WestPylonSilverBand_{index:D2}",
                    new Vector3(-4.72f, y, 2.46f),
                    new Vector3(0.08f, 0.16f, 0.58f), 6, 0);
                Box(parts, $"EastPylonSilverBand_{index:D2}",
                    new Vector3(4.72f, y, 2.46f),
                    new Vector3(0.08f, 0.16f, 0.58f), 6, 0);
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
                name = "T_Crownlands_Stormwright_Atlas_1024",
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
                    "Unity did not create the Crownlands atlas importer.");
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
                    "The Crownlands realm motion profile is missing or invalid.");
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
                    !(entry.RealmId == RealmId.Crownlands &&
                        string.Equals(
                            entry.BuildingId,
                            BuildingId,
                            StringComparison.Ordinal)))
                .ToList();
            entries.Add(new KingdomBuildingModelEntry(
                ModelId,
                RealmId.Crownlands,
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
                    "Crownlands/Materials/MAT_Crownlands_Ground.mat");
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
                new Color(0.008f, 0.014f, 0.028f);
            camera.transform.position =
                new Vector3(15.2f, 10.8f, -25.4f);
            camera.transform.LookAt(new Vector3(0f, 2.25f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.82f, 0.88f, 1f);
            key.intensity = 1.35f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation =
                Quaternion.Euler(45f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.95f, 0.72f, 0.44f);
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation =
                Quaternion.Euler(28f, 144f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.12f, 0.14f, 0.18f);
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
                    $"Could not preview Crownlands Level {level}.");
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
                "Assets/AL/Art/Generated/Architecture/Crownlands",
                "Production");
            EnsureFolder(
                "Assets/AL/Art/Generated/Architecture/Crownlands/Production",
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
