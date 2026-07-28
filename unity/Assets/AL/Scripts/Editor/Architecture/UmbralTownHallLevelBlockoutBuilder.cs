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
    /// Creates deterministic review-only Umbral Town Hall anchors for Levels
    /// 1, 6, and 10. The assets prove the offset protected civic masses,
    /// stable public entrance, four grounded boundary piers, and supported
    /// static Veiled Accord Yoke. They have no gameplay authority.
    /// </summary>
    public static class UmbralTownHallLevelBlockoutBuilder
    {
        public const string Level01PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Umbral_TownHall_Level01_Blockout.prefab";
        public const string Level06PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Umbral_TownHall_Level06_Blockout.prefab";
        public const string Level10PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Umbral/Production/" +
            "TownHall/Umbral_TownHall_Level10_Blockout.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "UmbralTownHallLevelBlockout.unity";

        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Umbral/Materials";

        [MenuItem(
            "Another Life/Architecture/Build Umbral Town Hall Level Blockouts")]
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
                    "The Umbral Town Hall review camera is missing.");
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
                    "umbral-townhall-level-blockout",
                    "render.png"));
        }

        private static void BuildAndSave(
            MaterialSet materials,
            int level,
            string path)
        {
            var root = new GameObject(
                $"Umbral_TownHall_Level{level:D2}_Blockout");
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
            Anchor(root, "Entrance", new Vector3(1.05f, 0f, -4.65f));
            Anchor(root, "CameraFocus", new Vector3(0.2f, 3.55f, 0.1f));
            Anchor(root, "Activity_00", new Vector3(-2.35f, 0f, -3.85f));
            Anchor(root, "Output_00", new Vector3(2.65f, 0f, -3.65f));

            Transform selection = Anchor(
                root,
                "SelectionColliderPreview",
                new Vector3(0f, 6f, 0f));
            selection.localScale = new Vector3(16f, 14f, 15f);

            Transform navigation = Anchor(
                root,
                "NavigationColliderPreview",
                new Vector3(0.15f, 1.35f, 0.1f));
            navigation.localScale = new Vector3(13f, 2.7f, 11.4f);
        }

        private static void AddLevel01(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L01_OperationalHall");
            Transform foundation = Group(level, "ProtectedCivicFoundation");
            Block(
                foundation,
                "CivicPlot",
                new Vector3(0f, 0.16f, 0f),
                new Vector3(9.5f, 0.32f, 8.15f),
                materials.Stone);
            Block(
                foundation,
                "ProtectedCouncilFloor",
                new Vector3(-0.25f, 0.36f, 0.15f),
                new Vector3(7.1f, 0.22f, 5.9f),
                materials.Stone);
            Block(
                foundation,
                "ObliquePublicApproach",
                new Vector3(1.05f, 0.18f, -3.95f),
                new Vector3(3.5f, 0.22f, 1.55f),
                materials.Stone,
                Quaternion.Euler(0f, -7f, 0f));
            AddEntranceSteps(foundation, materials);

            Transform hall = Group(level, "OffsetProtectedCivicMasses");
            Block(
                hall,
                "WestCouncilMass",
                new Vector3(-1.25f, 2.08f, 0.3f),
                new Vector3(5.35f, 3.4f, 5.75f),
                materials.Stone);
            Block(
                hall,
                "EastRecordsMass",
                new Vector3(2.45f, 1.72f, -0.2f),
                new Vector3(2.85f, 2.68f, 4.65f),
                materials.Stone);
            Block(
                hall,
                "ObliqueEntranceRecess",
                new Vector3(1.05f, 1.52f, -2.62f),
                new Vector3(2.05f, 2.28f, 0.2f),
                materials.Obsidian,
                Quaternion.Euler(0f, -7f, 0f));

            Transform entrance = Group(hall, "ProtectedPublicEntrance");
            foreach (float x in new[] { 0f, 2.1f })
            {
                Block(
                    entrance,
                    x < 1f ? "WestEntrancePier" : "EastEntrancePier",
                    new Vector3(x, 1.68f, -2.78f),
                    new Vector3(0.5f, 2.65f, 0.62f),
                    materials.Timber,
                    Quaternion.Euler(0f, -7f, 0f));
            }
            Block(
                entrance,
                "PublicEntranceLintel",
                new Vector3(1.05f, 3.05f, -2.78f),
                new Vector3(2.65f, 0.42f, 0.66f),
                materials.Brass,
                Quaternion.Euler(0f, -7f, 0f));
            Block(
                entrance,
                "ContainedDarkglassCivicInset",
                new Vector3(2.48f, 2.05f, -2.56f),
                new Vector3(0.52f, 0.82f, 0.16f),
                materials.Darkglass);

            Transform roof = Group(level, "SplitCivicRoof_Occlusion");
            AddSplitRoof(
                roof,
                "WestCouncilRoof",
                new Vector3(-1.25f, 4.08f, 0.3f),
                3f,
                6.05f,
                18f,
                materials.Timber,
                materials.Brass);
            AddSplitRoof(
                roof,
                "EastRecordsRoof",
                new Vector3(2.45f, 3.32f, -0.2f),
                1.75f,
                4.9f,
                15f,
                materials.Timber,
                materials.Brass);

            Transform gallery = Group(level, "AshTimberCouncilGallery");
            foreach (float x in new[] { -3.45f, -2.25f, 0.45f, 3.55f })
            {
                Block(
                    gallery,
                    $"GalleryPost_{x:0.00}",
                    new Vector3(x, 1.7f, 2.72f),
                    new Vector3(0.18f, 2.15f, 0.18f),
                    materials.Timber);
            }
        }

        private static void AddEntranceSteps(
            Transform parent,
            MaterialSet materials)
        {
            Quaternion rotation = Quaternion.Euler(0f, -7f, 0f);
            Block(
                parent,
                "StepUpper",
                new Vector3(1.05f, 0.42f, -3.55f),
                new Vector3(2.75f, 0.25f, 0.72f),
                materials.Stone,
                rotation);
            Block(
                parent,
                "StepMiddle",
                new Vector3(1.05f, 0.28f, -3.92f),
                new Vector3(3.2f, 0.2f, 0.76f),
                materials.Stone,
                rotation);
            Block(
                parent,
                "StepLower",
                new Vector3(1.05f, 0.14f, -4.3f),
                new Vector3(3.65f, 0.18f, 0.78f),
                materials.Stone,
                rotation);
        }

        private static void AddLevel02(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L02_Grounded");
            foreach (Vector3 position in BoundaryPierPositions())
            {
                Block(
                    level,
                    $"FoundationLock_{position.x:0.00}_{position.z:0.00}",
                    new Vector3(position.x, 0.55f, position.z),
                    new Vector3(0.76f, 0.82f, 0.76f),
                    materials.Stone);
            }
            Block(
                level,
                "WestRetainingEdge",
                new Vector3(-4.5f, 0.36f, 0.05f),
                new Vector3(0.3f, 0.38f, 6.45f),
                materials.Brass);
            Block(
                level,
                "EastRetainingEdge",
                new Vector3(4.65f, 0.36f, -0.1f),
                new Vector3(0.3f, 0.38f, 6.25f),
                materials.Brass);
        }

        private static void AddLevel03(
            Transform root,
            MaterialSet materials)
        {
            AddCivicGallery(
                Group(root, "L03_WorkingWing"),
                "RecordsGallery",
                -4.45f,
                0.55f,
                4.5f,
                -8f,
                materials);
        }

        private static void AddLevel04(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L04_PublicThreshold");
            Block(
                level,
                "ShelteredPublicCanopy",
                new Vector3(1.05f, 3.4f, -3.55f),
                new Vector3(3.9f, 0.22f, 1.48f),
                materials.Timber,
                Quaternion.Euler(-7f, -7f, 0f));
            foreach (float x in new[] { -0.6f, 2.7f })
            {
                Block(
                    level,
                    x < 0f ? "CanopyPostWest" : "CanopyPostEast",
                    new Vector3(x, 1.85f, -3.88f),
                    new Vector3(0.22f, 2.45f, 0.22f),
                    materials.Timber);
            }
            Block(
                level,
                "PublicThresholdBrace",
                new Vector3(1.05f, 3.12f, -3.82f),
                new Vector3(3.45f, 0.2f, 0.22f),
                materials.Brass,
                Quaternion.Euler(0f, -7f, 0f));
            Block(
                level,
                "CivicNoticePlinth",
                new Vector3(-1.85f, 0.72f, -4.05f),
                new Vector3(1f, 0.95f, 0.7f),
                materials.Stone);
        }

        private static void AddLevel05(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L05_RealmStructure");
            Transform piers = Group(level, "FourGroundedBoundaryPiers");
            Vector3[] positions = BoundaryPierPositions();
            for (int index = 0; index < positions.Length; index++)
            {
                AddBoundaryPier(
                    piers,
                    $"BoundaryPier_{index + 1:D2}",
                    positions[index],
                    materials);
            }

            Transform shelter = Group(level, "PhysicalShelterLoadPath");
            Beam(
                shelter,
                "FrontWestPierToRoofBrace",
                new Vector3(positions[0].x, 4.62f, positions[0].z),
                new Vector3(-2.8f, 4.48f, -1.82f),
                0.18f,
                materials.Timber);
            Beam(
                shelter,
                "FrontEastPierToRoofBrace",
                new Vector3(positions[1].x, 4.62f, positions[1].z),
                new Vector3(3.35f, 3.82f, -1.72f),
                0.18f,
                materials.Timber);
            Beam(
                shelter,
                "RearWestPierToRoofBrace",
                new Vector3(positions[2].x, 4.62f, positions[2].z),
                new Vector3(-2.72f, 4.48f, 2.08f),
                0.18f,
                materials.Timber);
            Beam(
                shelter,
                "RearEastPierToRoofBrace",
                new Vector3(positions[3].x, 4.62f, positions[3].z),
                new Vector3(3.42f, 3.82f, 1.92f),
                0.18f,
                materials.Timber);
        }

        private static void AddBoundaryPier(
            Transform parent,
            string name,
            Vector3 position,
            MaterialSet materials)
        {
            Transform pier = Group(parent, name);
            Block(
                pier,
                "GroundedBase",
                new Vector3(position.x, 0.78f, position.z),
                new Vector3(1.25f, 1.15f, 1.25f),
                materials.Stone);
            Block(
                pier,
                "ThickPierBody",
                new Vector3(position.x, 2.82f, position.z),
                new Vector3(0.9f, 3.45f, 0.9f),
                materials.Stone);
            Block(
                pier,
                "BrassLoadCap",
                new Vector3(position.x, 4.68f, position.z),
                new Vector3(1.08f, 0.3f, 1.08f),
                materials.Brass);
        }

        private static void AddLevel06(
            Transform root,
            MaterialSet materials)
        {
            AddCivicGallery(
                Group(root, "L06_DistrictCapacity"),
                "StewardGallery",
                4.7f,
                0.2f,
                4.15f,
                9f,
                materials);
        }

        private static void AddCivicGallery(
            Transform parent,
            string name,
            float x,
            float z,
            float depth,
            float roofPitch,
            MaterialSet materials)
        {
            Transform gallery = Group(parent, name);
            Block(
                gallery,
                "GalleryPlinth",
                new Vector3(x, 0.42f, z),
                new Vector3(2.15f, 0.5f, depth + 0.45f),
                materials.Stone);
            Block(
                gallery,
                "GalleryBody",
                new Vector3(x, 1.6f, z),
                new Vector3(1.82f, 1.85f, depth),
                materials.Stone);
            Block(
                gallery,
                "GalleryRoof",
                new Vector3(x, 2.72f, z),
                new Vector3(2.22f, 0.24f, depth + 0.5f),
                materials.Timber,
                Quaternion.Euler(0f, 0f, roofPitch));
            foreach (float zOffset in new[] { -1.5f, 1.5f })
            {
                Block(
                    gallery,
                    zOffset < 0f ? "FrontGalleryPost" : "RearGalleryPost",
                    new Vector3(
                        x + (x < 0f ? -0.7f : 0.7f),
                        1.48f,
                        z + zOffset),
                    new Vector3(0.25f, 1.85f, 0.25f),
                    materials.Timber);
            }
        }

        private static void AddLevel07(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L07_UpperAuthority");
            Block(
                level,
                "ProtectedCouncilClerestory",
                new Vector3(-0.35f, 4.65f, 0.72f),
                new Vector3(3.55f, 1.05f, 2.35f),
                materials.Stone);
            AddSplitRoof(
                level,
                "UpperCouncilRoof",
                new Vector3(-0.35f, 5.36f, 0.72f),
                2.05f,
                2.58f,
                17f,
                materials.Timber,
                materials.Brass);
        }

        private static void AddLevel08(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L08_ServiceIntegration");
            Block(
                level,
                "RearServiceGallery",
                new Vector3(0.15f, 1.38f, 3.55f),
                new Vector3(7.15f, 1.4f, 1.18f),
                materials.Stone);
            Block(
                level,
                "RearServiceRoof",
                new Vector3(0.15f, 2.18f, 3.55f),
                new Vector3(7.5f, 0.22f, 1.5f),
                materials.Timber,
                Quaternion.Euler(8f, 0f, 0f));
            foreach (float x in new[] { -2.75f, 3.05f })
            {
                Block(
                    level,
                    $"RearServicePost_{x:0.00}",
                    new Vector3(x, 1.12f, 4.02f),
                    new Vector3(0.2f, 1.6f, 0.2f),
                    materials.Timber);
                Block(
                    level,
                    $"RearDrain_{x:0.00}",
                    new Vector3(x, 1.18f, 4.22f),
                    new Vector3(0.14f, 1.72f, 0.14f),
                    materials.Brass);
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
                new Vector3(-3.6f, 0.28f, -5f),
                new Vector3(3.2f, 0.34f, 0.48f),
                materials.Stone,
                Quaternion.Euler(0f, -4f, 0f));
            Block(
                level,
                "ForecourtEdgeEast",
                new Vector3(4.45f, 0.28f, -4.82f),
                new Vector3(3.1f, 0.34f, 0.48f),
                materials.Stone,
                Quaternion.Euler(0f, 6f, 0f));
            Block(
                level,
                "ApproachCenter",
                new Vector3(1.05f, 0.12f, -5.15f),
                new Vector3(4.05f, 0.18f, 1.02f),
                materials.Stone,
                Quaternion.Euler(0f, -7f, 0f));
            foreach (float x in new[] { -5f, 5.45f })
            {
                Block(
                    level,
                    $"NoticePier_{x:0.00}",
                    new Vector3(x, 0.82f, -4.72f),
                    new Vector3(0.58f, 1.35f, 0.58f),
                    materials.Stone);
                Block(
                    level,
                    $"NoticeCap_{x:0.00}",
                    new Vector3(x, 1.55f, -4.72f),
                    new Vector3(0.72f, 0.16f, 0.72f),
                    materials.Brass);
            }
        }

        private static void AddLevel10(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L10_VeiledAccordYoke");
            Vector3[] positions = BoundaryPierPositions();
            Transform loadRails = Group(level, "FourYokeLoadRails");
            Beam(
                loadRails,
                "BoundaryPier01ToFrontWestFrame",
                new Vector3(positions[0].x, 4.82f, positions[0].z),
                new Vector3(-2.45f, 5.98f, 0.1f),
                0.2f,
                materials.Timber);
            Beam(
                loadRails,
                "BoundaryPier02ToFrontEastFrame",
                new Vector3(positions[1].x, 4.82f, positions[1].z),
                new Vector3(2.75f, 5.98f, 0.1f),
                0.2f,
                materials.Timber);
            Beam(
                loadRails,
                "BoundaryPier03ToRearWestFrame",
                new Vector3(positions[2].x, 4.82f, positions[2].z),
                new Vector3(-2.2f, 6.1f, 1.35f),
                0.2f,
                materials.Timber);
            Beam(
                loadRails,
                "BoundaryPier04ToRearEastFrame",
                new Vector3(positions[3].x, 4.82f, positions[3].z),
                new Vector3(3f, 6.1f, 1.35f),
                0.2f,
                materials.Timber);

            Transform yoke = Group(level, "VeiledAccordYoke_Occlusion");
            AddYokeFrame(
                yoke,
                "FrontCrossframe",
                0.1f,
                0.15f,
                5.98f,
                materials);
            AddYokeFrame(
                yoke,
                "RearCrossframe",
                1.35f,
                0.4f,
                6.1f,
                materials);

            Transform connectors = Group(yoke, "FixedFrameConnectors");
            Beam(
                connectors,
                "WestUpperConnector",
                new Vector3(-2.45f, 6.62f, 0.1f),
                new Vector3(-2.2f, 6.74f, 1.35f),
                0.14f,
                materials.Brass);
            Beam(
                connectors,
                "EastUpperConnector",
                new Vector3(2.75f, 6.62f, 0.1f),
                new Vector3(3f, 6.74f, 1.35f),
                0.14f,
                materials.Brass);
            Beam(
                connectors,
                "WestLowerConnector",
                new Vector3(-2.45f, 5.98f, 0.1f),
                new Vector3(-2.2f, 6.1f, 1.35f),
                0.12f,
                materials.Timber);
            Beam(
                connectors,
                "EastLowerConnector",
                new Vector3(2.75f, 5.98f, 0.1f),
                new Vector3(3f, 6.1f, 1.35f),
                0.12f,
                materials.Timber);
        }

        private static void AddYokeFrame(
            Transform parent,
            string name,
            float z,
            float centerX,
            float lowerHeight,
            MaterialSet materials)
        {
            Transform frame = Group(parent, name);
            Block(
                frame,
                "LowerFixedCrossbar",
                new Vector3(centerX, lowerHeight, z),
                new Vector3(5.2f, 0.28f, 0.34f),
                materials.Timber);
            Block(
                frame,
                "UpperFixedCrossbar",
                new Vector3(centerX + 0.12f, lowerHeight + 0.64f, z),
                new Vector3(5.05f, 0.28f, 0.34f),
                materials.Timber,
                Quaternion.Euler(0f, 0f, 1f));
            Block(
                frame,
                "CouncilSlitBoundaryWest",
                new Vector3(centerX - 0.42f, lowerHeight + 0.32f, z),
                new Vector3(0.2f, 0.38f, 0.38f),
                materials.Brass);
            Block(
                frame,
                "CouncilSlitBoundaryEast",
                new Vector3(centerX + 0.42f, lowerHeight + 0.32f, z),
                new Vector3(0.2f, 0.38f, 0.38f),
                materials.Brass);
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

        private static void AddSplitRoof(
            Transform parent,
            string prefix,
            Vector3 center,
            float sideWidth,
            float depth,
            float pitch,
            Material roofMaterial,
            Material ridgeMaterial)
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
            Block(
                parent,
                $"{prefix}Ridge",
                center + new Vector3(0f, sideWidth * 0.27f, 0f),
                new Vector3(0.22f, 0.2f, depth + 0.12f),
                ridgeMaterial);
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
            camera.orthographicSize = 9.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.022f, 0.034f);
            camera.transform.position = new Vector3(22.5f, 14f, -34f);
            camera.transform.LookAt(new Vector3(0f, 3.5f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.82f, 0.87f, 1f);
            key.intensity = 1.78f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(1f, 0.68f, 0.42f);
            fill.intensity = 0.78f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, 142f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.23f, 0.29f);
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
                "Assets/AL/Art/Generated/Architecture/Umbral/Production";
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
                Stone = RequireMaterial("GraphiteStone"),
                Timber = RequireMaterial("AshTimber"),
                Brass = RequireMaterial("TarnishedBrass"),
                Obsidian = RequireMaterial("Obsidian"),
                Darkglass = RequireMaterial("Aubergine"),
                Ground = RequireMaterial("Ground")
            };
        }

        private static Material RequireMaterial(string name)
        {
            string path = $"{MaterialFolder}/MAT_Umbral_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"Required Umbral material is missing: {path}");
            }
            return material;
        }

        private sealed class MaterialSet
        {
            public Material Stone;
            public Material Timber;
            public Material Brass;
            public Material Obsidian;
            public Material Darkglass;
            public Material Ground;
        }
    }
}
