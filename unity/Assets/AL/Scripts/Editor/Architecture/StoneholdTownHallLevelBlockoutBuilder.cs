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
    /// Creates deterministic review-only Stonehold Town Hall anchors for
    /// Levels 1, 6, and 10. The assets prove cumulative civic silhouette,
    /// fixed spatial identity, roof pitch, and capstone support. They are not
    /// live kingdom prefabs and contain no gameplay or animation authority.
    /// </summary>
    public static class StoneholdTownHallLevelBlockoutBuilder
    {
        public const string Level01PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall/Stonehold_TownHall_Level01_Blockout.prefab";
        public const string Level06PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall/Stonehold_TownHall_Level06_Blockout.prefab";
        public const string Level10PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall/Stonehold_TownHall_Level10_Blockout.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "StoneholdTownHallLevelBlockout.unity";

        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Materials";
        private const string TownHallFolder =
            "Assets/AL/Art/Generated/Architecture/Stonehold/Production/" +
            "TownHall";

        [MenuItem(
            "Another Life/Architecture/Build Stonehold Town Hall Level Blockouts")]
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
                    "The Stonehold Town Hall review camera is missing.");
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
                    "stonehold-townhall-level-blockout",
                    "render.png"));
        }

        private static void BuildAndSave(
            MaterialSet materials,
            int level,
            string path)
        {
            var root = new GameObject(
                $"Stonehold_TownHall_Level{level:D2}_Blockout");
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
            Anchor(root, "CameraFocus", new Vector3(0f, 3.7f, 0f));
            Anchor(root, "Activity_00", new Vector3(-2.2f, 0f, -3.9f));
            Anchor(root, "Output_00", new Vector3(2.2f, 0f, -3.9f));

            Transform selection = Anchor(
                root,
                "SelectionColliderPreview",
                new Vector3(0f, 5.4f, 0f));
            selection.localScale = new Vector3(14.4f, 10.8f, 13.2f);

            Transform navigation = Anchor(
                root,
                "NavigationColliderPreview",
                new Vector3(0f, 1.4f, 0.15f));
            navigation.localScale = new Vector3(12.8f, 2.8f, 11.2f);
        }

        private static void AddLevel01(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L01_OperationalHall");
            Transform foundation = Group(level, "Foundation");
            Block(
                foundation,
                "CivicPlot",
                new Vector3(0f, 0.18f, 0f),
                new Vector3(9.35f, 0.36f, 8f),
                materials.Basalt);
            Block(
                foundation,
                "FoundationCourse",
                new Vector3(0f, 0.48f, 0.05f),
                new Vector3(8.75f, 0.42f, 7.45f),
                materials.Stone);
            Block(
                foundation,
                "WestRetainingCourse",
                new Vector3(-4.12f, 0.72f, 0.35f),
                new Vector3(0.55f, 0.72f, 6.65f),
                materials.Basalt);
            Block(
                foundation,
                "EastRetainingCourse",
                new Vector3(4.12f, 0.72f, 0.35f),
                new Vector3(0.55f, 0.72f, 6.65f),
                materials.Basalt);

            Transform shell = Group(level, "CouncilShell");
            Block(
                shell,
                "BackWall",
                new Vector3(0f, 2.5f, 3.28f),
                new Vector3(8.15f, 3.65f, 0.55f),
                materials.Stone);
            Block(
                shell,
                "WestWall",
                new Vector3(-3.8f, 2.45f, 0f),
                new Vector3(0.65f, 3.55f, 6.45f),
                materials.Stone);
            Block(
                shell,
                "EastWall",
                new Vector3(3.8f, 2.45f, 0f),
                new Vector3(0.65f, 3.55f, 6.45f),
                materials.Stone);
            Block(
                shell,
                "FrontWallWest",
                new Vector3(-2.55f, 2.42f, -3.22f),
                new Vector3(2.45f, 3.5f, 0.58f),
                materials.Stone);
            Block(
                shell,
                "FrontWallEast",
                new Vector3(2.55f, 2.42f, -3.22f),
                new Vector3(2.45f, 3.5f, 0.58f),
                materials.Stone);
            Block(
                shell,
                "FrontPierWest",
                new Vector3(-3.75f, 2.05f, -3.2f),
                new Vector3(0.82f, 3.15f, 0.92f),
                materials.Basalt);
            Block(
                shell,
                "FrontPierEast",
                new Vector3(3.75f, 2.05f, -3.2f),
                new Vector3(0.82f, 3.15f, 0.92f),
                materials.Basalt);

            Transform entrance = Group(level, "PublicEntrance");
            Block(
                entrance,
                "DoorRecess",
                new Vector3(0f, 2f, -3.54f),
                new Vector3(2.25f, 2.95f, 0.16f),
                materials.Iron);
            Block(
                entrance,
                "EntranceJambWest",
                new Vector3(-1.28f, 2.12f, -3.57f),
                new Vector3(0.48f, 3.15f, 0.68f),
                materials.Basalt);
            Block(
                entrance,
                "EntranceJambEast",
                new Vector3(1.28f, 2.12f, -3.57f),
                new Vector3(0.48f, 3.15f, 0.68f),
                materials.Basalt);
            Block(
                entrance,
                "ClippedShoulderWest",
                new Vector3(-0.78f, 3.72f, -3.57f),
                new Vector3(1.05f, 0.5f, 0.7f),
                materials.Basalt,
                Quaternion.Euler(0f, 0f, -30f));
            Block(
                entrance,
                "ClippedShoulderEast",
                new Vector3(0.78f, 3.72f, -3.57f),
                new Vector3(1.05f, 0.5f, 0.7f),
                materials.Basalt,
                Quaternion.Euler(0f, 0f, 30f));
            Block(
                entrance,
                "ClippedArchCrown",
                new Vector3(0f, 3.98f, -3.57f),
                new Vector3(1.15f, 0.5f, 0.7f),
                materials.Basalt);
            Block(
                entrance,
                "ContainedAmberSlit",
                new Vector3(0f, 2.05f, -3.65f),
                new Vector3(0.24f, 1.55f, 0.08f),
                materials.Amber);
            AddEntranceSteps(entrance, materials);

            Transform roof = Group(level, "Roof_Occlusion");
            AddRoofPair(
                roof,
                "Roof",
                new Vector3(0f, 5.25f, 0f),
                4.9f,
                7.5f,
                20f,
                5,
                materials.Iron,
                materials.Basalt);
            Block(
                roof,
                "RidgeLock",
                new Vector3(0f, 6.1f, 0f),
                new Vector3(0.34f, 0.32f, 7.72f),
                materials.Iron);
            Block(
                roof,
                "WestEaveDrain",
                new Vector3(-4.58f, 4.45f, 0f),
                new Vector3(0.18f, 0.2f, 7.55f),
                materials.Iron);
            Block(
                roof,
                "EastEaveDrain",
                new Vector3(4.58f, 4.45f, 0f),
                new Vector3(0.18f, 0.2f, 7.55f),
                materials.Iron);
        }

        private static void AddEntranceSteps(
            Transform parent,
            MaterialSet materials)
        {
            Block(
                parent,
                "StepUpper",
                new Vector3(0f, 0.48f, -3.78f),
                new Vector3(3.1f, 0.28f, 0.85f),
                materials.Stone);
            Block(
                parent,
                "StepMiddle",
                new Vector3(0f, 0.3f, -4.14f),
                new Vector3(3.65f, 0.22f, 0.8f),
                materials.Stone);
            Block(
                parent,
                "StepLower",
                new Vector3(0f, 0.15f, -4.48f),
                new Vector3(4.15f, 0.18f, 0.72f),
                materials.Basalt);
        }

        private static void AddLevel02(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L02_Grounded");
            foreach (float x in new[] { -4.32f, 4.32f })
            {
                foreach (float z in new[] { -2.65f, 2.65f })
                {
                    Block(
                        level,
                        $"CornerLock_{Side(x)}_{Side(z)}",
                        new Vector3(x, 0.9f, z),
                        new Vector3(0.75f, 1.15f, 1.05f),
                        materials.Basalt);
                }
            }
            Block(
                level,
                "WestFoundationLock",
                new Vector3(-4.48f, 0.48f, 0f),
                new Vector3(0.38f, 0.5f, 4.5f),
                materials.Iron);
            Block(
                level,
                "EastFoundationLock",
                new Vector3(4.48f, 0.48f, 0f),
                new Vector3(0.38f, 0.5f, 4.5f),
                materials.Iron);
        }

        private static void AddLevel03(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L03_WorkingWing");
            Block(
                level,
                "RecordsWingPlinth",
                new Vector3(-5.05f, 0.42f, 0.7f),
                new Vector3(2.2f, 0.5f, 4.75f),
                materials.Basalt);
            Block(
                level,
                "RecordsWingBody",
                new Vector3(-5.05f, 1.85f, 0.78f),
                new Vector3(1.9f, 2.35f, 4.2f),
                materials.Stone);
            Block(
                level,
                "RecordsWingFrontPier",
                new Vector3(-5.45f, 1.65f, -1.52f),
                new Vector3(0.55f, 2.2f, 0.62f),
                materials.Basalt);
            Block(
                level,
                "RecordsWingRearPier",
                new Vector3(-5.45f, 1.65f, 3.02f),
                new Vector3(0.55f, 2.2f, 0.62f),
                materials.Basalt);
            Block(
                level,
                "RecordsWingRoof",
                new Vector3(-5.05f, 3.24f, 0.75f),
                new Vector3(2.45f, 0.28f, 4.85f),
                materials.Iron,
                Quaternion.Euler(0f, 0f, 12f));
            Block(
                level,
                "RecordsWingRoofLock",
                new Vector3(-4.05f, 3.43f, 0.75f),
                new Vector3(0.18f, 0.2f, 4.92f),
                materials.Basalt,
                Quaternion.Euler(0f, 0f, 12f));
        }

        private static void AddLevel04(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L04_PublicThreshold");
            Block(
                level,
                "ThresholdCanopy",
                new Vector3(0f, 3.5f, -4f),
                new Vector3(3.75f, 0.24f, 1.65f),
                materials.Iron,
                Quaternion.Euler(-8f, 0f, 0f));
            Block(
                level,
                "CanopyPostWest",
                new Vector3(-1.55f, 1.95f, -4.45f),
                new Vector3(0.24f, 2.65f, 0.24f),
                materials.Iron);
            Block(
                level,
                "CanopyPostEast",
                new Vector3(1.55f, 1.95f, -4.45f),
                new Vector3(0.24f, 2.65f, 0.24f),
                materials.Iron);
            Block(
                level,
                "ThresholdLintel",
                new Vector3(0f, 3.23f, -4.22f),
                new Vector3(3.25f, 0.32f, 0.32f),
                materials.Basalt);
            Block(
                level,
                "PublicNoticePlinth",
                new Vector3(2.55f, 0.68f, -4.18f),
                new Vector3(1.1f, 0.85f, 0.7f),
                materials.Stone);
        }

        private static void AddLevel05(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L05_RealmStructure");
            Block(
                level,
                "ButtressTowerWest",
                new Vector3(-4.15f, 2.25f, -1.45f),
                new Vector3(1.15f, 3.65f, 1.45f),
                materials.Basalt);
            Block(
                level,
                "ButtressTowerEast",
                new Vector3(4.15f, 2.25f, -1.45f),
                new Vector3(1.15f, 3.65f, 1.45f),
                materials.Basalt);
            Block(
                level,
                "ButtressCapWest",
                new Vector3(-4.15f, 4.18f, -1.45f),
                new Vector3(1.45f, 0.35f, 1.75f),
                materials.Stone);
            Block(
                level,
                "ButtressCapEast",
                new Vector3(4.15f, 4.18f, -1.45f),
                new Vector3(1.45f, 0.35f, 1.75f),
                materials.Stone);
            Block(
                level,
                "ContinuousCivicLintel",
                new Vector3(0f, 4.36f, -2.98f),
                new Vector3(8.6f, 0.42f, 0.58f),
                materials.Iron);
            Block(
                level,
                "LintelLockWest",
                new Vector3(-2.8f, 4.35f, -3.3f),
                new Vector3(0.3f, 0.68f, 0.24f),
                materials.Basalt);
            Block(
                level,
                "LintelLockEast",
                new Vector3(2.8f, 4.35f, -3.3f),
                new Vector3(0.3f, 0.68f, 0.24f),
                materials.Basalt);
        }

        private static void AddLevel06(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L06_DistrictCapacity");
            Block(
                level,
                "AssemblyWingPlinth",
                new Vector3(5.12f, 0.42f, 0.25f),
                new Vector3(2.28f, 0.5f, 5.55f),
                materials.Basalt);
            Block(
                level,
                "AssemblyWingBody",
                new Vector3(5.12f, 1.92f, 0.28f),
                new Vector3(1.95f, 2.45f, 5.05f),
                materials.Stone);
            Block(
                level,
                "AssemblyWingFrontPier",
                new Vector3(5.55f, 1.7f, -2.42f),
                new Vector3(0.58f, 2.3f, 0.62f),
                materials.Basalt);
            Block(
                level,
                "AssemblyWingRearPier",
                new Vector3(5.55f, 1.7f, 2.96f),
                new Vector3(0.58f, 2.3f, 0.62f),
                materials.Basalt);
            Block(
                level,
                "AssemblyWingRoof",
                new Vector3(5.12f, 3.38f, 0.28f),
                new Vector3(2.52f, 0.3f, 5.72f),
                materials.Iron,
                Quaternion.Euler(0f, 0f, -12f));
            Block(
                level,
                "AssemblyWingRoofLock",
                new Vector3(4.08f, 3.58f, 0.28f),
                new Vector3(0.18f, 0.2f, 5.78f),
                materials.Basalt,
                Quaternion.Euler(0f, 0f, -12f));
            Block(
                level,
                "RearServiceGallery",
                new Vector3(0.15f, 2.02f, 3.72f),
                new Vector3(8.3f, 2.25f, 0.72f),
                materials.Timber);
            Block(
                level,
                "RearGalleryCap",
                new Vector3(0.15f, 3.25f, 3.72f),
                new Vector3(8.65f, 0.24f, 1.05f),
                materials.Iron);
            Block(
                level,
                "UpperCouncilCourse",
                new Vector3(0f, 5.1f, -0.55f),
                new Vector3(4.45f, 1.5f, 3.45f),
                materials.Stone);
            Transform upperRoof = Group(level, "UpperCouncilRoof_Occlusion");
            AddRoofPair(
                upperRoof,
                "UpperCouncilRoof",
                new Vector3(0f, 6.12f, -0.55f),
                2.45f,
                3.72f,
                18f,
                2,
                materials.Iron,
                materials.Basalt);
            Block(
                upperRoof,
                "UpperCouncilRidge",
                new Vector3(0f, 6.5f, -0.55f),
                new Vector3(0.26f, 0.25f, 3.88f),
                materials.Iron);
        }

        private static void AddLevel07(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L07_UpperAuthority");
            Block(
                level,
                "UpperCouncilWall",
                new Vector3(0f, 5.12f, -2.78f),
                new Vector3(4.25f, 1.55f, 0.5f),
                materials.Basalt);
            Block(
                level,
                "UpperShoulderWest",
                new Vector3(-1.35f, 5.88f, -2.8f),
                new Vector3(1.8f, 0.38f, 0.6f),
                materials.Stone,
                Quaternion.Euler(0f, 0f, -18f));
            Block(
                level,
                "UpperShoulderEast",
                new Vector3(1.35f, 5.88f, -2.8f),
                new Vector3(1.8f, 0.38f, 0.6f),
                materials.Stone,
                Quaternion.Euler(0f, 0f, 18f));
            Block(
                level,
                "UpperAuthorityBand",
                new Vector3(0f, 5.28f, -3.08f),
                new Vector3(4.05f, 0.26f, 0.22f),
                materials.Iron);
            Block(
                level,
                "RidgeAuthoritySpine",
                new Vector3(0f, 6.35f, -0.2f),
                new Vector3(0.48f, 0.38f, 6.2f),
                materials.Basalt);
        }

        private static void AddLevel08(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L08_ServiceIntegration");
            for (int index = 0; index < 4; index++)
            {
                float x = Mathf.Lerp(-3.45f, 3.45f, index / 3f);
                Block(
                    level,
                    $"RearGalleryPost_{index:D2}",
                    new Vector3(x, 1.55f, 4.25f),
                    new Vector3(0.28f, 2.25f, 0.28f),
                    materials.Basalt);
            }
            Block(
                level,
                "RearCirculationDeck",
                new Vector3(0f, 1.08f, 4.1f),
                new Vector3(7.8f, 0.24f, 1.3f),
                materials.Timber);
            Block(
                level,
                "RearCirculationRoof",
                new Vector3(0f, 2.85f, 4.1f),
                new Vector3(8.2f, 0.22f, 1.48f),
                materials.Iron,
                Quaternion.Euler(7f, 0f, 0f));
            Block(
                level,
                "RearDrain",
                new Vector3(0f, 2.72f, 4.82f),
                new Vector3(8.1f, 0.18f, 0.18f),
                materials.Iron);
            Block(
                level,
                "ServiceStore",
                new Vector3(-2.7f, 0.78f, 4.25f),
                new Vector3(1.5f, 0.82f, 0.82f),
                materials.Basalt);
        }

        private static void AddLevel09(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L09_CivicIntegration");
            Block(
                level,
                "ForecourtEdgeWest",
                new Vector3(-4.3f, 0.28f, -5.05f),
                new Vector3(3.25f, 0.34f, 0.5f),
                materials.Basalt);
            Block(
                level,
                "ForecourtEdgeEast",
                new Vector3(4.3f, 0.28f, -5.05f),
                new Vector3(3.25f, 0.34f, 0.5f),
                materials.Basalt);
            Block(
                level,
                "ApproachCenter",
                new Vector3(0f, 0.12f, -5.2f),
                new Vector3(4.2f, 0.18f, 1.1f),
                materials.Stone);
            Block(
                level,
                "NoticePierWest",
                new Vector3(-5.15f, 0.85f, -4.9f),
                new Vector3(0.62f, 1.45f, 0.62f),
                materials.Stone);
            Block(
                level,
                "NoticePierEast",
                new Vector3(5.15f, 0.85f, -4.9f),
                new Vector3(0.62f, 1.45f, 0.62f),
                materials.Stone);
            Block(
                level,
                "NoticeBandWest",
                new Vector3(-5.15f, 1.25f, -5.24f),
                new Vector3(0.82f, 0.22f, 0.18f),
                materials.Iron);
            Block(
                level,
                "NoticeBandEast",
                new Vector3(5.15f, 1.25f, -5.24f),
                new Vector3(0.82f, 0.22f, 0.18f),
                materials.Iron);
        }

        private static void AddLevel10(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L10_OathstoneCrown");
            Transform crown = Group(level, "Crown_Occlusion");
            Block(
                crown,
                "CrownLoadBase",
                new Vector3(0f, 6.55f, -0.25f),
                new Vector3(5f, 1.2f, 3.45f),
                materials.Basalt);
            Block(
                crown,
                "CrownSecondCourse",
                new Vector3(0f, 7.45f, -0.35f),
                new Vector3(4.1f, 1.45f, 2.9f),
                materials.Stone);
            Block(
                crown,
                "CrownBand",
                new Vector3(0f, 7.65f, -1.82f),
                new Vector3(4.2f, 0.32f, 0.22f),
                materials.Iron);
            foreach (float x in new[] { -1.22f, 1.22f })
            {
                foreach (float z in new[] { -0.82f, 0.25f })
                {
                    Block(
                        crown,
                        $"BelfryPier_{Side(x)}_{Side(z)}",
                        new Vector3(x, 8.75f, z),
                        new Vector3(0.42f, 1.35f, 0.42f),
                        materials.Basalt);
                }
            }
            Block(
                crown,
                "BelfryHeader",
                new Vector3(0f, 9.45f, -0.28f),
                new Vector3(3.35f, 0.4f, 1.95f),
                materials.Stone);
            AddRoofPair(
                crown,
                "CrownRoof",
                new Vector3(0f, 9.78f, -0.28f),
                2.25f,
                3f,
                22f,
                2,
                materials.Iron,
                materials.Basalt);
            Block(
                crown,
                "CrownRidge",
                new Vector3(0f, 10.2f, -0.28f),
                new Vector3(0.3f, 0.25f, 3.15f),
                materials.Iron);
            Cylinder(
                crown,
                "FixedIronOathPlate",
                new Vector3(0f, 7.45f, -1.84f),
                new Vector3(1.05f, 0.12f, 1.05f),
                materials.Iron,
                Quaternion.Euler(90f, 0f, 0f));
            Cylinder(
                crown,
                "OathPlateBoss",
                new Vector3(0f, 7.45f, -1.98f),
                new Vector3(0.2f, 0.1f, 0.2f),
                materials.Stone,
                Quaternion.Euler(90f, 0f, 0f));
            Block(
                crown,
                "ContainedCrownSlit",
                new Vector3(0f, 8.78f, -1.05f),
                new Vector3(0.22f, 0.62f, 0.08f),
                materials.Amber);
            Block(
                crown,
                "RoofLoadBracketWest",
                new Vector3(-2.08f, 6.02f, -0.25f),
                new Vector3(0.45f, 1.75f, 1.8f),
                materials.Iron,
                Quaternion.Euler(0f, 0f, -28f));
            Block(
                crown,
                "RoofLoadBracketEast",
                new Vector3(2.08f, 6.02f, -0.25f),
                new Vector3(0.45f, 1.75f, 1.8f),
                materials.Iron,
                Quaternion.Euler(0f, 0f, 28f));
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
                new Vector3(sideWidth, 0.3f, depth),
                roofMaterial,
                Quaternion.Euler(0f, 0f, pitch));
            Transform eastGroup = Group(parent, $"{prefix}EastGroup");
            Block(
                eastGroup,
                $"{prefix}East",
                center + new Vector3(halfOffset, 0f, 0f),
                new Vector3(sideWidth, 0.3f, depth),
                roofMaterial,
                Quaternion.Euler(0f, 0f, -pitch));

            for (int index = 0; index < ribCount; index++)
            {
                float z = ribCount == 1
                    ? center.z
                    : center.z + Mathf.Lerp(
                        -depth * 0.43f,
                        depth * 0.43f,
                        index / (ribCount - 1f));
                Block(
                    westGroup,
                    $"{prefix}WestRib_{index:D2}",
                    new Vector3(center.x - halfOffset, center.y + 0.18f, z),
                    new Vector3(sideWidth + 0.08f, 0.11f, 0.14f),
                    ribMaterial,
                    Quaternion.Euler(0f, 0f, pitch));
                Block(
                    eastGroup,
                    $"{prefix}EastRib_{index:D2}",
                    new Vector3(center.x + halfOffset, center.y + 0.18f, z),
                    new Vector3(sideWidth + 0.08f, 0.11f, 0.14f),
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
            camera.orthographicSize = 9.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.067f);
            camera.transform.position = new Vector3(22.5f, 14f, -34f);
            camera.transform.LookAt(new Vector3(0f, 3.65f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.78f, 0.55f);
            key.intensity = 1.65f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.42f, 0.58f, 0.68f);
            fill.intensity = 0.72f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, 142f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.2f);
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

        private static Renderer Cylinder(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion? rotation = null)
        {
            GameObject cylinder =
                GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localScale = scale;
            cylinder.transform.localRotation = rotation ?? Quaternion.identity;
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            Renderer renderer = cylinder.GetComponent<Renderer>();
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

        private static string Side(float value)
        {
            return value < 0f ? "West" : "East";
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
                "Assets/AL/Art/Generated/Architecture/Stonehold/Production";
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
                Basalt = RequireMaterial("Basalt"),
                Stone = RequireMaterial("Stone"),
                Iron = RequireMaterial("DarkIron"),
                Timber = RequireMaterial("Timber"),
                Amber = CreateReviewAmberMaterial(),
                Ground = RequireMaterial("Ground")
            };
        }

        private static Material CreateReviewAmberMaterial()
        {
            string path =
                $"{TownHallFolder}/MAT_Stonehold_TownHall_ReviewAmber.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard") ??
                    Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = new Color(0.72f, 0.28f, 0.025f);
            material.enableInstancing = true;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(
                    "_EmissionColor",
                    new Color(1.25f, 0.32f, 0.025f));
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material RequireMaterial(string name)
        {
            string path = $"{MaterialFolder}/MAT_Stonehold_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"The approved Stonehold material {path} is missing.");
            }
            return material;
        }

        private sealed class MaterialSet
        {
            public Material Basalt;
            public Material Stone;
            public Material Iron;
            public Material Timber;
            public Material Amber;
            public Material Ground;
        }
    }
}
