using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AL.Editor.Architecture
{
    /// <summary>
    /// Creates deterministic review-only Crownlands Town Hall anchors for
    /// Levels 1, 6, and 10. The assets prove the broad civic mass, fixed axial
    /// entrance, paired grounded towers, and supported static Concord
    /// Meridian. They have no gameplay authority.
    /// </summary>
    public static class CrownlandsTownHallLevelBlockoutBuilder
    {
        public const string Level01PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Production/" +
            "TownHall/Crownlands_TownHall_Level01_Blockout.prefab";
        public const string Level06PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Production/" +
            "TownHall/Crownlands_TownHall_Level06_Blockout.prefab";
        public const string Level10PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Production/" +
            "TownHall/Crownlands_TownHall_Level10_Blockout.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "CrownlandsTownHallLevelBlockout.unity";

        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Crownlands/Materials";

        [MenuItem(
            "Another Life/Architecture/Build Crownlands Town Hall Level Blockouts")]
        public static void Build()
        {
            EnsureFolders();
            MaterialSet materials = LoadMaterials();
            BuildAndSave(materials, 1, Level01PrefabPath);
            BuildAndSave(materials, 6, Level06PrefabPath);
            BuildAndSave(materials, 10, Level10PrefabPath);
            CreatePreviewScene(materials);
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
                    "The Crownlands Town Hall review camera is missing.");
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
                    "crownlands-townhall-level-blockout",
                    "render.png"));
        }

        private static void BuildAndSave(
            MaterialSet materials,
            int level,
            string path)
        {
            var root = new GameObject(
                $"Crownlands_TownHall_Level{level:D2}_Blockout");
            try
            {
                CreateReviewAnchors(root.transform);
                AddLevel01(root.transform, materials);
                if (level >= 2)
                {
                    AddLevel02(root.transform, materials);
                }
                if (level >= 3)
                {
                    AddLevel03(root.transform, materials);
                }
                if (level >= 4)
                {
                    AddLevel04(root.transform, materials);
                }
                if (level >= 5)
                {
                    AddLevel05(root.transform, materials);
                }
                if (level >= 6)
                {
                    AddLevel06(root.transform, materials);
                }
                if (level >= 7)
                {
                    AddLevel07(root.transform, materials);
                }
                if (level >= 8)
                {
                    AddLevel08(root.transform, materials);
                }
                if (level >= 9)
                {
                    AddLevel09(root.transform, materials);
                }
                if (level >= 10)
                {
                    AddLevel10(root.transform, materials);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateReviewAnchors(Transform root)
        {
            Anchor(root, "Entrance", new Vector3(0f, 0f, -4.65f));
            Anchor(root, "CameraFocus", new Vector3(0f, 3.55f, 0f));
            Anchor(root, "Activity_00", new Vector3(-2.2f, 0f, -3.8f));
            Anchor(root, "Output_00", new Vector3(2.2f, 0f, -3.8f));

            Transform selection = Anchor(
                root,
                "SelectionColliderPreview",
                new Vector3(0f, 6f, 0f));
            selection.localScale = new Vector3(16f, 14f, 15f);

            Transform navigation = Anchor(
                root,
                "NavigationColliderPreview",
                new Vector3(0f, 1.3f, 0.15f));
            navigation.localScale = new Vector3(13f, 2.6f, 11.2f);
        }

        private static void AddLevel01(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L01_OperationalHall");
            Transform foundation = Group(level, "AxialCivicFoundation");
            Block(
                foundation,
                "CivicPlot",
                new Vector3(0f, 0.16f, 0f),
                new Vector3(9.4f, 0.32f, 8.1f),
                materials.Stone);
            Block(
                foundation,
                "CouncilFloor",
                new Vector3(0f, 0.36f, -0.2f),
                new Vector3(6.5f, 0.22f, 5.8f),
                materials.Stone);
            Block(
                foundation,
                "AxialApproach",
                new Vector3(0f, 0.18f, -4.18f),
                new Vector3(3.7f, 0.22f, 1.8f),
                materials.Stone);
            AddEntranceSteps(foundation, materials);

            Transform hall = Group(level, "DisciplinedCivicHall");
            Block(
                hall,
                "CentralHall",
                new Vector3(0f, 2.02f, 0.2f),
                new Vector3(6.25f, 3.3f, 5.45f),
                materials.Stone);
            Block(
                hall,
                "WestCivicBay",
                new Vector3(-3.55f, 1.68f, 0.38f),
                new Vector3(1.5f, 2.65f, 4.7f),
                materials.Stone);
            Block(
                hall,
                "EastCivicBay",
                new Vector3(3.55f, 1.68f, 0.38f),
                new Vector3(1.5f, 2.65f, 4.7f),
                materials.Stone);
            Block(
                hall,
                "ClippedEntranceRecess",
                new Vector3(0f, 1.5f, -2.58f),
                new Vector3(1.95f, 2.25f, 0.18f),
                materials.Slate);

            Transform entrance = Group(hall, "PairedEntrancePiers");
            foreach (float x in new[] { -1.25f, 1.25f })
            {
                Block(
                    entrance,
                    x < 0f ? "WestEntrancePier" : "EastEntrancePier",
                    new Vector3(x, 1.62f, -2.73f),
                    new Vector3(0.58f, 2.6f, 0.68f),
                    materials.Stone);
            }
            Block(
                entrance,
                "CivicLintel",
                new Vector3(0f, 2.98f, -2.73f),
                new Vector3(3.08f, 0.46f, 0.7f),
                materials.Stone);
            Block(
                entrance,
                "UnmarkedCivicPanel",
                new Vector3(0f, 3.48f, -2.58f),
                new Vector3(1.35f, 0.42f, 0.18f),
                materials.Silver);

            Transform roof = Group(level, "CivicRoof_Occlusion");
            AddRoofPair(
                roof,
                "MainCivicRoof",
                new Vector3(0f, 4.05f, 0.25f),
                3.55f,
                5.9f,
                17f,
                3,
                materials.Slate,
                materials.Silver);
            Block(
                roof,
                "MainRoofRidge",
                new Vector3(0f, 4.9f, 0.25f),
                new Vector3(0.28f, 0.24f, 6.1f),
                materials.Silver);
            Block(
                roof,
                "WestBayRoof",
                new Vector3(-3.55f, 3.18f, 0.4f),
                new Vector3(1.82f, 0.25f, 4.95f),
                materials.Slate,
                Quaternion.Euler(0f, 0f, 10f));
            Block(
                roof,
                "EastBayRoof",
                new Vector3(3.55f, 3.18f, 0.4f),
                new Vector3(1.82f, 0.25f, 4.95f),
                materials.Slate,
                Quaternion.Euler(0f, 0f, -10f));

            Transform ribs = Group(level, "FacadeStructuralRibs");
            foreach (float x in new[] { -2.75f, -1.95f, 1.95f, 2.75f })
            {
                Block(
                    ribs,
                    $"FacadeRib_{x:0.00}",
                    new Vector3(x, 2.1f, -2.58f),
                    new Vector3(0.16f, 2.65f, 0.14f),
                    materials.Silver);
            }
        }

        private static void AddEntranceSteps(
            Transform parent,
            MaterialSet materials)
        {
            Block(
                parent,
                "StepUpper",
                new Vector3(0f, 0.42f, -3.55f),
                new Vector3(2.9f, 0.25f, 0.72f),
                materials.Stone);
            Block(
                parent,
                "StepMiddle",
                new Vector3(0f, 0.28f, -3.9f),
                new Vector3(3.4f, 0.2f, 0.76f),
                materials.Stone);
            Block(
                parent,
                "StepLower",
                new Vector3(0f, 0.14f, -4.27f),
                new Vector3(3.9f, 0.18f, 0.78f),
                materials.Stone);
        }

        private static void AddLevel02(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L02_Grounded");
            foreach (Vector3 position in new[]
                     {
                         new Vector3(-4.25f, 0.55f, -3.35f),
                         new Vector3(4.25f, 0.55f, -3.35f),
                         new Vector3(-4.25f, 0.55f, 3.35f),
                         new Vector3(4.25f, 0.55f, 3.35f)
                     })
            {
                Block(
                    level,
                    $"FoundationLock_{position.x:0.00}_{position.z:0.00}",
                    position,
                    new Vector3(0.72f, 0.78f, 0.72f),
                    materials.Stone);
            }
            Block(
                level,
                "WestRetainingEdge",
                new Vector3(-4.48f, 0.35f, 0f),
                new Vector3(0.28f, 0.36f, 6.4f),
                materials.Bronze);
            Block(
                level,
                "EastRetainingEdge",
                new Vector3(4.48f, 0.35f, 0f),
                new Vector3(0.28f, 0.36f, 6.4f),
                materials.Bronze);
        }

        private static void AddLevel03(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L03_WorkingWing");
            AddCivicWing(
                level,
                "RecordsWing",
                -5.1f,
                0.55f,
                materials,
                -9f);
        }

        private static void AddLevel04(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L04_PublicThreshold");
            Block(
                level,
                "PublicCanopy",
                new Vector3(0f, 3.42f, -3.48f),
                new Vector3(4f, 0.22f, 1.55f),
                materials.Slate,
                Quaternion.Euler(-8f, 0f, 0f));
            foreach (float x in new[] { -1.7f, 1.7f })
            {
                Block(
                    level,
                    x < 0f ? "CanopyPostWest" : "CanopyPostEast",
                    new Vector3(x, 1.85f, -3.82f),
                    new Vector3(0.22f, 2.5f, 0.22f),
                    materials.Silver);
            }
            Block(
                level,
                "PublicThresholdLintel",
                new Vector3(0f, 3.15f, -3.74f),
                new Vector3(3.55f, 0.2f, 0.24f),
                materials.Silver);
            Block(
                level,
                "PublicNoticePlinth",
                new Vector3(2.65f, 0.7f, -4.05f),
                new Vector3(1.05f, 0.9f, 0.72f),
                materials.Stone);
        }

        private static void AddLevel05(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L05_RealmStructure");
            Transform meridian = Group(level, "GroundedMeridianLoadPath");
            foreach (float x in new[] { -2.9f, 2.9f })
            {
                Block(
                    meridian,
                    x < 0f ? "WestMeridianRib" : "EastMeridianRib",
                    new Vector3(x, 4.12f, 0.18f),
                    new Vector3(0.22f, 2.25f, 0.24f),
                    materials.Silver);
                Block(
                    meridian,
                    x < 0f ? "WestTowerLoadBase" : "EastTowerLoadBase",
                    new Vector3(x < 0f ? -4.15f : 4.15f, 1f, 0.35f),
                    new Vector3(1.55f, 1.25f, 1.9f),
                    materials.Stone);
            }
            Block(
                meridian,
                "CentralMeridianRidge",
                new Vector3(0f, 5.08f, 0.25f),
                new Vector3(6.15f, 0.2f, 0.24f),
                materials.Silver);
            Block(
                level,
                "WestBalancedGalleryRail",
                new Vector3(-3.95f, 1.42f, -0.15f),
                new Vector3(0.16f, 1.05f, 3.35f),
                materials.Bronze);
            Block(
                level,
                "EastBalancedGalleryRail",
                new Vector3(3.95f, 1.42f, -0.15f),
                new Vector3(0.16f, 1.05f, 3.35f),
                materials.Bronze);
        }

        private static void AddLevel06(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L06_DistrictCapacity");
            AddCivicWing(
                level,
                "StewardWing",
                5.1f,
                0.35f,
                materials,
                9f);

            Transform towers = Group(level, "PairedGroundedCivicTowers");
            AddCivicTower(towers, "WestCivicTower", -4.15f, materials);
            AddCivicTower(towers, "EastCivicTower", 4.15f, materials);
        }

        private static void AddCivicWing(
            Transform parent,
            string name,
            float x,
            float z,
            MaterialSet materials,
            float roofPitch)
        {
            Transform wing = Group(parent, name);
            Block(
                wing,
                "Plinth",
                new Vector3(x, 0.42f, z),
                new Vector3(2.25f, 0.5f, 4.65f),
                materials.Stone);
            Block(
                wing,
                "Body",
                new Vector3(x, 1.68f, z),
                new Vector3(1.9f, 2.05f, 4.15f),
                materials.Stone);
            Block(
                wing,
                "FrontPier",
                new Vector3(x + (x < 0f ? -0.55f : 0.55f), 1.5f, -1.65f),
                new Vector3(0.38f, 1.9f, 0.48f),
                materials.Silver);
            Block(
                wing,
                "RearPier",
                new Vector3(x + (x < 0f ? -0.55f : 0.55f), 1.5f, 2.75f),
                new Vector3(0.38f, 1.9f, 0.48f),
                materials.Silver);
            Block(
                wing,
                "Roof",
                new Vector3(x, 2.9f, z),
                new Vector3(2.28f, 0.24f, 4.7f),
                materials.Slate,
                Quaternion.Euler(0f, 0f, roofPitch));
        }

        private static void AddCivicTower(
            Transform parent,
            string name,
            float x,
            MaterialSet materials)
        {
            Transform tower = Group(parent, name);
            Block(
                tower,
                "LoadPlinth",
                new Vector3(x, 0.78f, 0.35f),
                new Vector3(1.9f, 1.15f, 2.2f),
                materials.Stone);
            Block(
                tower,
                "GroundedBody",
                new Vector3(x, 3.65f, 0.35f),
                new Vector3(1.55f, 4.85f, 1.8f),
                materials.Stone);
            Block(
                tower,
                "UpperCourse",
                new Vector3(x, 5.85f, 0.35f),
                new Vector3(1.72f, 0.42f, 1.98f),
                materials.Silver);
            Block(
                tower,
                "LowCivicRoof",
                new Vector3(x, 6.18f, 0.35f),
                new Vector3(1.88f, 0.28f, 2.08f),
                materials.Slate);
            Block(
                tower,
                "BroadWindowRecess",
                new Vector3(x, 4.05f, -0.58f),
                new Vector3(0.62f, 1.15f, 0.12f),
                materials.Slate);
        }

        private static void AddLevel07(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L07_UpperAuthority");
            Block(
                level,
                "CouncilClerestory",
                new Vector3(0f, 4.62f, 0.72f),
                new Vector3(3.6f, 1.15f, 2.25f),
                materials.Stone);
            AddRoofPair(
                level,
                "UpperCouncilRoof",
                new Vector3(0f, 5.38f, 0.72f),
                2.05f,
                2.55f,
                18f,
                2,
                materials.Slate,
                materials.Silver);
            Block(
                level,
                "UpperCouncilRidge",
                new Vector3(0f, 5.86f, 0.72f),
                new Vector3(0.22f, 0.2f, 2.72f),
                materials.Silver);
        }

        private static void AddLevel08(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L08_ServiceIntegration");
            Block(
                level,
                "RearServiceGallery",
                new Vector3(0f, 1.42f, 3.55f),
                new Vector3(7.1f, 1.45f, 1.25f),
                materials.Stone);
            Block(
                level,
                "RearServiceRoof",
                new Vector3(0f, 2.25f, 3.55f),
                new Vector3(7.45f, 0.22f, 1.55f),
                materials.Slate,
                Quaternion.Euler(8f, 0f, 0f));
            foreach (float x in new[] { -2.9f, 2.9f })
            {
                Block(
                    level,
                    $"RearServicePost_{x:0.00}",
                    new Vector3(x, 1.15f, 4.08f),
                    new Vector3(0.2f, 1.65f, 0.2f),
                    materials.Silver);
                Block(
                    level,
                    $"RearDrain_{x:0.00}",
                    new Vector3(x, 1.2f, 4.28f),
                    new Vector3(0.15f, 1.75f, 0.15f),
                    materials.Bronze);
            }
        }

        private static void AddLevel09(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L09_CivicIntegration");
            Block(
                level,
                "ForecourtEdgeWest",
                new Vector3(-4.25f, 0.28f, -5.02f),
                new Vector3(3.15f, 0.34f, 0.48f),
                materials.Stone);
            Block(
                level,
                "ForecourtEdgeEast",
                new Vector3(4.25f, 0.28f, -5.02f),
                new Vector3(3.15f, 0.34f, 0.48f),
                materials.Stone);
            Block(
                level,
                "ApproachCenter",
                new Vector3(0f, 0.12f, -5.18f),
                new Vector3(4.2f, 0.18f, 1.05f),
                materials.Stone);
            foreach (float x in new[] { -5.1f, 5.1f })
            {
                Block(
                    level,
                    $"NoticePier_{x:0.00}",
                    new Vector3(x, 0.85f, -4.88f),
                    new Vector3(0.58f, 1.4f, 0.58f),
                    materials.Stone);
                Block(
                    level,
                    $"NoticeCap_{x:0.00}",
                    new Vector3(x, 1.62f, -4.88f),
                    new Vector3(0.72f, 0.16f, 0.72f),
                    materials.Bronze);
            }
        }

        private static void AddLevel10(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L10_ConcordMeridian");
            Transform supports = Group(level, "MeridianSupports");
            foreach (float x in new[] { -4.15f, 4.15f })
            {
                Block(
                    supports,
                    x < 0f ? "WestGroundedSupport" : "EastGroundedSupport",
                    new Vector3(x, 6.55f, 0.35f),
                    new Vector3(0.46f, 0.9f, 0.5f),
                    materials.Silver);
            }

            Transform meridian = Group(level, "ConcordMeridian_Occlusion");
            const int segmentCount = 10;
            const float horizontalRadius = 4.15f;
            const float verticalRadius = 1.2f;
            const float centerHeight = 6.65f;
            for (int index = 0; index < segmentCount; index++)
            {
                float firstAngle =
                    Mathf.PI - index * Mathf.PI / segmentCount;
                float secondAngle =
                    Mathf.PI - (index + 1) * Mathf.PI / segmentCount;
                Vector3 start = new Vector3(
                    Mathf.Cos(firstAngle) * horizontalRadius,
                    centerHeight + Mathf.Sin(firstAngle) * verticalRadius,
                    0.35f);
                Vector3 end = new Vector3(
                    Mathf.Cos(secondAngle) * horizontalRadius,
                    centerHeight + Mathf.Sin(secondAngle) * verticalRadius,
                    0.35f);
                Beam(
                    meridian,
                    $"SilverMeridianSegment_{index:D2}",
                    start,
                    end,
                    0.28f,
                    materials.Silver);
            }
            Block(
                meridian,
                "SolidUnmarkedApexDial",
                new Vector3(0f, centerHeight + verticalRadius, 0.35f),
                new Vector3(0.68f, 0.78f, 0.42f),
                materials.Bronze);
        }

        private static void AddRoofPair(
            Transform parent,
            string prefix,
            Vector3 center,
            float sideWidth,
            float depth,
            float pitch,
            int ribCount,
            Material roofMaterial,
            Material ribMaterial)
        {
            float halfOffset = sideWidth * 0.47f;
            Transform westGroup = Group(parent, $"{prefix}WestGroup");
            Block(
                westGroup,
                $"{prefix}West",
                center + new Vector3(-halfOffset, 0f, 0f),
                new Vector3(sideWidth, 0.28f, depth),
                roofMaterial,
                Quaternion.Euler(0f, 0f, pitch));
            Transform eastGroup = Group(parent, $"{prefix}EastGroup");
            Block(
                eastGroup,
                $"{prefix}East",
                center + new Vector3(halfOffset, 0f, 0f),
                new Vector3(sideWidth, 0.28f, depth),
                roofMaterial,
                Quaternion.Euler(0f, 0f, -pitch));

            for (int index = 0; index < ribCount; index++)
            {
                float z = ribCount == 1
                    ? center.z
                    : center.z + Mathf.Lerp(
                        -depth * 0.42f,
                        depth * 0.42f,
                        index / (ribCount - 1f));
                Block(
                    westGroup,
                    $"{prefix}WestRib_{index:D2}",
                    new Vector3(center.x - halfOffset, center.y + 0.16f, z),
                    new Vector3(sideWidth + 0.08f, 0.1f, 0.13f),
                    ribMaterial,
                    Quaternion.Euler(0f, 0f, pitch));
                Block(
                    eastGroup,
                    $"{prefix}EastRib_{index:D2}",
                    new Vector3(center.x + halfOffset, center.y + 0.16f, z),
                    new Vector3(sideWidth + 0.08f, 0.1f, 0.13f),
                    ribMaterial,
                    Quaternion.Euler(0f, 0f, -pitch));
            }
        }

        private static void CreatePreviewScene(MaterialSet materials)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            InstantiateAt(Level01PrefabPath, new Vector3(-12.4f, 0f, 0f));
            InstantiateAt(Level06PrefabPath, Vector3.zero);
            InstantiateAt(Level10PrefabPath, new Vector3(12.4f, 0f, 0f));

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ReviewGround";
            ground.transform.localScale = new Vector3(4.4f, 1f, 1.45f);
            ConfigureRenderer(ground.GetComponent<Renderer>(), materials.Ground);
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
            camera.transform.position = new Vector3(22.5f, 14f, -34f);
            camera.transform.LookAt(new Vector3(0f, 3.55f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.93f, 0.9f, 0.78f);
            key.intensity = 1.62f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.34f, 0.46f, 0.72f);
            fill.intensity = 0.72f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, 142f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.27f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void InstantiateAt(
            string prefabPath,
            Vector3 position)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"The Town Hall level blockout {prefabPath} is missing.");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
        }

        private static Renderer Beam(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float radius,
            Material material)
        {
            Vector3 direction = end - start;
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = (start + end) * 0.5f;
            beam.transform.localRotation = Quaternion.FromToRotation(
                Vector3.up,
                direction.normalized);
            beam.transform.localScale =
                new Vector3(radius, direction.magnitude * 0.5f, radius);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            Renderer renderer = beam.GetComponent<Renderer>();
            ConfigureRenderer(renderer, material);
            return renderer;
        }

        private static Renderer Block(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion? rotation = null)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.transform.localRotation = rotation ?? Quaternion.identity;
            Object.DestroyImmediate(block.GetComponent<Collider>());
            Renderer renderer = block.GetComponent<Renderer>();
            ConfigureRenderer(renderer, material);
            return renderer;
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Transform Anchor(
            Transform parent,
            string name,
            Vector3 position)
        {
            Transform anchor = Group(parent, name);
            anchor.localPosition = position;
            return anchor;
        }

        private static void ConfigureRenderer(
            Renderer renderer,
            Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void WriteFrame(
            Camera camera,
            int width,
            int height,
            string path)
        {
            var target = new RenderTexture(width, height, 24)
            {
                antiAliasing = 4
            };
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
                capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                capture.Apply();
                string outputPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(capture);
                Object.DestroyImmediate(target);
            }
        }

        private static void EnsureFolders()
        {
            const string production =
                "Assets/AL/Art/Generated/Architecture/Crownlands/Production";
            const string townHall = production + "/TownHall";
            if (!AssetDatabase.IsValidFolder(townHall))
            {
                AssetDatabase.CreateFolder(production, "TownHall");
            }
        }

        private static MaterialSet LoadMaterials()
        {
            return new MaterialSet
            {
                Stone = RequireMaterial("Stone"),
                Slate = RequireMaterial("BlueSlate"),
                Silver = RequireMaterial("Silver"),
                Bronze = RequireMaterial("Bronze"),
                Ground = RequireMaterial("Ground")
            };
        }

        private static Material RequireMaterial(string name)
        {
            string path =
                $"{MaterialFolder}/MAT_Crownlands_Stormwright_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"Required Crownlands material is missing: {path}");
            }
            return material;
        }

        private sealed class MaterialSet
        {
            public Material Stone;
            public Material Slate;
            public Material Silver;
            public Material Bronze;
            public Material Ground;
        }
    }
}
