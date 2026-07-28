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
    /// Creates deterministic review-only Eldergrove Town Hall anchors for
    /// Levels 1, 6, and 10. The assets prove the open civic court, three
    /// authored structural root arches, cumulative galleries, and the
    /// supported empty crown oculus. They have no gameplay authority.
    /// </summary>
    public static class EldergroveTownHallLevelBlockoutBuilder
    {
        public const string Level01PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "TownHall/Eldergrove_TownHall_Level01_Blockout.prefab";
        public const string Level06PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "TownHall/Eldergrove_TownHall_Level06_Blockout.prefab";
        public const string Level10PrefabPath =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Production/" +
            "TownHall/Eldergrove_TownHall_Level10_Blockout.prefab";
        public const string ScenePath =
            "Assets/AL/Scenes/Prototypes/" +
            "EldergroveTownHallLevelBlockout.unity";

        private const string MaterialFolder =
            "Assets/AL/Art/Generated/Architecture/Eldergrove/Materials";

        [MenuItem(
            "Another Life/Architecture/Build Eldergrove Town Hall Level Blockouts")]
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
                    "The Eldergrove Town Hall review camera is missing.");
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
                    "eldergrove-townhall-level-blockout",
                    "render.png"));
        }

        private static void BuildAndSave(
            MaterialSet materials,
            int level,
            string path)
        {
            var root = new GameObject(
                $"Eldergrove_TownHall_Level{level:D2}_Blockout");
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
            Anchor(root, "CameraFocus", new Vector3(0f, 3.65f, 0f));
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
                new Vector3(0f, 1.35f, 0.15f));
            navigation.localScale = new Vector3(12.8f, 2.7f, 11.2f);
        }

        private static void AddLevel01(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L01_OperationalHall");
            Transform foundation = Group(level, "OpenCivicCourt");
            Block(
                foundation,
                "CivicPlot",
                new Vector3(0f, 0.16f, 0f),
                new Vector3(9.3f, 0.32f, 8f),
                materials.PaleStone);
            Block(
                foundation,
                "CouncilFloor",
                new Vector3(0f, 0.36f, -0.25f),
                new Vector3(6.4f, 0.22f, 5.6f),
                materials.PaleStone);
            Block(
                foundation,
                "WestCourtEdge",
                new Vector3(-4.15f, 0.52f, -0.2f),
                new Vector3(0.55f, 0.65f, 6.7f),
                materials.PaleStone);
            Block(
                foundation,
                "EastCourtEdge",
                new Vector3(4.15f, 0.52f, -0.2f),
                new Vector3(0.55f, 0.65f, 6.7f),
                materials.PaleStone);
            Block(
                foundation,
                "RearCourtEdge",
                new Vector3(0f, 0.52f, 3.4f),
                new Vector3(8.3f, 0.65f, 0.55f),
                materials.PaleStone);
            AddEntranceSteps(foundation, materials);

            Transform galleries = Group(level, "CivicGalleries");
            Block(
                galleries,
                "RearCouncilHall",
                new Vector3(0f, 2.05f, 2.62f),
                new Vector3(7.2f, 2.85f, 1.55f),
                materials.PaleStone);
            Block(
                galleries,
                "RearTimberScreen",
                new Vector3(0f, 2.12f, 1.81f),
                new Vector3(5.7f, 1.75f, 0.16f),
                materials.Timber);
            Block(
                galleries,
                "WestGalleryBody",
                new Vector3(-3.35f, 1.6f, -0.1f),
                new Vector3(1.22f, 2.05f, 4.55f),
                materials.PaleStone);
            Block(
                galleries,
                "EastGalleryBody",
                new Vector3(3.35f, 1.6f, 0.1f),
                new Vector3(1.22f, 2.05f, 4.2f),
                materials.PaleStone);
            Block(
                galleries,
                "WestGalleryRail",
                new Vector3(-2.66f, 1.5f, -0.25f),
                new Vector3(0.18f, 1.25f, 3.65f),
                materials.Timber);
            Block(
                galleries,
                "EastGalleryRail",
                new Vector3(2.66f, 1.5f, -0.05f),
                new Vector3(0.18f, 1.25f, 3.35f),
                materials.Timber);
            Block(
                galleries,
                "WestGalleryRoof",
                new Vector3(-3.34f, 2.8f, -0.05f),
                new Vector3(1.65f, 0.2f, 4.75f),
                materials.Timber,
                Quaternion.Euler(0f, 0f, 10f));
            Block(
                galleries,
                "EastGalleryRoof",
                new Vector3(3.34f, 2.8f, 0.12f),
                new Vector3(1.65f, 0.2f, 4.42f),
                materials.Timber,
                Quaternion.Euler(0f, 0f, -10f));

            Transform rootArches = Group(level, "StructuralRootArches");
            AddRootArch(
                rootArches,
                "RootArch_00",
                new Vector3(-3.55f, 0.58f, -2.25f),
                new Vector3(0f, 5.3f, -2f),
                new Vector3(3.55f, 0.58f, -2.25f),
                materials);
            AddRootArch(
                rootArches,
                "RootArch_01",
                new Vector3(-3.55f, 0.58f, -2.25f),
                new Vector3(-2.95f, 5.5f, 0.1f),
                new Vector3(-3.55f, 0.58f, 2.25f),
                materials);
            AddRootArch(
                rootArches,
                "RootArch_02",
                new Vector3(3.55f, 0.58f, -2.25f),
                new Vector3(2.95f, 5.5f, 0.1f),
                new Vector3(3.55f, 0.58f, 2.25f),
                materials);
            Transform rootCollars = Group(level, "StructuralRootCollars");
            foreach (float x in new[] { -3.55f, 3.55f })
            {
                foreach (float z in new[] { -2.25f, 2.25f })
                {
                    Cylinder(
                        rootCollars,
                        $"BaseCollar_{x:0.00}_{z:0.00}",
                        new Vector3(x, 0.58f, z),
                        new Vector3(0.62f, 0.14f, 0.62f),
                        materials.Bronze);
                }
            }

            Transform roof = Group(level, "CouncilCanopy_Occlusion");
            AddRoofPair(
                roof,
                "CouncilCanopy",
                new Vector3(0f, 4.15f, 0f),
                4.45f,
                6.1f,
                18f,
                3,
                materials.Timber,
                materials.Bronze);
            Block(
                roof,
                "CanopyRidge",
                new Vector3(0f, 4.86f, 0f),
                new Vector3(0.26f, 0.24f, 6.3f),
                materials.Bronze);
        }

        private static void AddEntranceSteps(
            Transform parent,
            MaterialSet materials)
        {
            Block(
                parent,
                "StepUpper",
                new Vector3(0f, 0.44f, -3.76f),
                new Vector3(3f, 0.26f, 0.78f),
                materials.PaleStone);
            Block(
                parent,
                "StepMiddle",
                new Vector3(0f, 0.28f, -4.1f),
                new Vector3(3.55f, 0.2f, 0.76f),
                materials.PaleStone);
            Block(
                parent,
                "StepLower",
                new Vector3(0f, 0.14f, -4.43f),
                new Vector3(4.05f, 0.18f, 0.7f),
                materials.PaleStone);
        }

        private static void AddRootArch(
            Transform parent,
            string name,
            Vector3 firstBase,
            Vector3 apex,
            Vector3 secondBase,
            MaterialSet materials)
        {
            Transform arch = Group(parent, name);
            Vector3 firstShoulder = Vector3.Lerp(firstBase, apex, 0.34f);
            Vector3 firstUpper = Vector3.Lerp(firstBase, apex, 0.72f);
            Vector3 secondUpper = Vector3.Lerp(secondBase, apex, 0.72f);
            Vector3 secondShoulder = Vector3.Lerp(secondBase, apex, 0.34f);

            Beam(
                arch,
                "FirstRootLower",
                firstBase,
                firstShoulder,
                0.72f,
                materials.Bark);
            Beam(
                arch,
                "FirstRootMiddle",
                firstShoulder,
                firstUpper,
                0.64f,
                materials.Bark);
            Beam(
                arch,
                "FirstRootCrown",
                firstUpper,
                apex,
                0.54f,
                materials.Bark);
            Beam(
                arch,
                "SecondRootCrown",
                apex,
                secondUpper,
                0.54f,
                materials.Bark);
            Beam(
                arch,
                "SecondRootMiddle",
                secondUpper,
                secondShoulder,
                0.64f,
                materials.Bark);
            Beam(
                arch,
                "SecondRootLower",
                secondShoulder,
                secondBase,
                0.72f,
                materials.Bark);
        }

        private static void AddLevel02(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L02_Grounded");
            foreach (float x in new[] { -3.55f, 3.55f })
            {
                foreach (float z in new[] { -2.25f, 2.25f })
                {
                    Beam(
                        level,
                        $"RootFoot_{x:0.00}_{z:0.00}",
                        new Vector3(x, 0.52f, z),
                        new Vector3(
                            x < 0f ? -4.15f : 4.15f,
                            0.18f,
                            z < 0f ? z - 0.55f : z + 0.55f),
                        0.42f,
                        materials.Bark);
                }
            }
            Block(
                level,
                "WestDrainEdge",
                new Vector3(-4.48f, 0.32f, 0f),
                new Vector3(0.24f, 0.28f, 5.3f),
                materials.Bronze);
            Block(
                level,
                "EastDrainEdge",
                new Vector3(4.48f, 0.32f, 0f),
                new Vector3(0.24f, 0.28f, 5.3f),
                materials.Bronze);
        }

        private static void AddLevel03(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L03_WorkingWing");
            Block(
                level,
                "StewardWingPlinth",
                new Vector3(-5.05f, 0.42f, 0.65f),
                new Vector3(2.1f, 0.5f, 4.45f),
                materials.PaleStone);
            Block(
                level,
                "StewardWingBody",
                new Vector3(-5.05f, 1.65f, 0.7f),
                new Vector3(1.82f, 1.95f, 3.95f),
                materials.Timber);
            Block(
                level,
                "StewardWingFrontPier",
                new Vector3(-5.48f, 1.45f, -1.4f),
                new Vector3(0.42f, 1.9f, 0.5f),
                materials.PaleStone);
            Block(
                level,
                "StewardWingRearPier",
                new Vector3(-5.48f, 1.45f, 2.76f),
                new Vector3(0.42f, 1.9f, 0.5f),
                materials.PaleStone);
            Block(
                level,
                "StewardWingRoof",
                new Vector3(-5.05f, 2.83f, 0.7f),
                new Vector3(2.25f, 0.22f, 4.5f),
                materials.Timber,
                Quaternion.Euler(0f, 0f, 11f));
            Block(
                level,
                "StewardGalleryRail",
                new Vector3(-4.05f, 1.32f, 0.55f),
                new Vector3(0.18f, 1.05f, 3.2f),
                materials.Bronze);
        }

        private static void AddLevel04(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L04_PublicThreshold");
            Block(
                level,
                "EntranceShelter",
                new Vector3(0f, 3.35f, -3.68f),
                new Vector3(3.55f, 0.2f, 1.55f),
                materials.Timber,
                Quaternion.Euler(-8f, 0f, 0f));
            Block(
                level,
                "ThresholdPostWest",
                new Vector3(-1.48f, 1.8f, -4.02f),
                new Vector3(0.22f, 2.45f, 0.22f),
                materials.Timber);
            Block(
                level,
                "ThresholdPostEast",
                new Vector3(1.48f, 1.8f, -4.02f),
                new Vector3(0.22f, 2.45f, 0.22f),
                materials.Timber);
            Block(
                level,
                "ThresholdBronzeLintel",
                new Vector3(0f, 3.05f, -3.88f),
                new Vector3(3.15f, 0.22f, 0.24f),
                materials.Bronze);
            Block(
                level,
                "PublicNoticePlinth",
                new Vector3(2.55f, 0.67f, -4.05f),
                new Vector3(1.05f, 0.82f, 0.68f),
                materials.PaleStone);
        }

        private static void AddLevel05(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L05_RealmStructure");
            Beam(
                level,
                "CouncilRidgeFront",
                new Vector3(-2.95f, 5.5f, 0.1f),
                new Vector3(0f, 5.3f, -2f),
                0.34f,
                materials.Bark);
            Beam(
                level,
                "CouncilRidgeRear",
                new Vector3(0f, 5.3f, -2f),
                new Vector3(2.95f, 5.5f, 0.1f),
                0.34f,
                materials.Bark);
            foreach (Vector3 apex in new[]
                     {
                         new Vector3(0f, 5.3f, -2f),
                         new Vector3(-2.95f, 5.5f, 0.1f),
                         new Vector3(2.95f, 5.5f, 0.1f)
                     })
            {
                Cylinder(
                    level,
                    $"ApexCollar_{apex.x:0.00}_{apex.z:0.00}",
                    apex,
                    new Vector3(0.38f, 0.12f, 0.38f),
                    materials.Bronze,
                    Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private static void AddLevel06(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L06_DistrictCapacity");
            Block(
                level,
                "RecordsWingPlinth",
                new Vector3(5.08f, 0.42f, 0.15f),
                new Vector3(2.2f, 0.5f, 5.15f),
                materials.PaleStone);
            Block(
                level,
                "RecordsWingBody",
                new Vector3(5.08f, 1.72f, 0.18f),
                new Vector3(1.88f, 2.05f, 4.65f),
                materials.Timber);
            Block(
                level,
                "RecordsWingFrontPier",
                new Vector3(5.52f, 1.5f, -2.25f),
                new Vector3(0.44f, 2f, 0.52f),
                materials.PaleStone);
            Block(
                level,
                "RecordsWingRearPier",
                new Vector3(5.52f, 1.5f, 2.62f),
                new Vector3(0.44f, 2f, 0.52f),
                materials.PaleStone);
            Block(
                level,
                "RecordsWingRoof",
                new Vector3(5.08f, 2.95f, 0.18f),
                new Vector3(2.34f, 0.22f, 5.24f),
                materials.Timber,
                Quaternion.Euler(0f, 0f, -11f));
            Block(
                level,
                "RecordsGalleryRail",
                new Vector3(4.04f, 1.36f, 0.05f),
                new Vector3(0.18f, 1.08f, 3.75f),
                materials.Bronze);
            Block(
                level,
                "RearCirculationHall",
                new Vector3(0f, 1.78f, 3.66f),
                new Vector3(8.1f, 1.95f, 0.72f),
                materials.Timber);
            Block(
                level,
                "RearCirculationCap",
                new Vector3(0f, 2.83f, 3.66f),
                new Vector3(8.35f, 0.2f, 1.02f),
                materials.Timber);
        }

        private static void AddLevel07(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L07_UpperAuthority");
            Block(
                level,
                "UpperCouncilGallery",
                new Vector3(0f, 4.82f, 2f),
                new Vector3(4.1f, 1.25f, 1.45f),
                materials.Timber);
            Transform roof = Group(level, "UpperCouncilRoof_Occlusion");
            AddRoofPair(
                roof,
                "UpperCouncilRoof",
                new Vector3(0f, 5.72f, 2f),
                2.15f,
                1.85f,
                18f,
                1,
                materials.Timber,
                materials.Bronze);
            Block(
                roof,
                "UpperCouncilRidge",
                new Vector3(0f, 6.07f, 2f),
                new Vector3(0.22f, 0.2f, 2f),
                materials.Bronze);
        }

        private static void AddLevel08(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L08_ServiceIntegration");
            for (int index = 0; index < 4; index++)
            {
                float x = Mathf.Lerp(-3.25f, 3.25f, index / 3f);
                Block(
                    level,
                    $"RearWalkPost_{index:D2}",
                    new Vector3(x, 1.45f, 4.2f),
                    new Vector3(0.24f, 2f, 0.24f),
                    materials.Bark);
            }
            Block(
                level,
                "RearWalkDeck",
                new Vector3(0f, 0.95f, 4.08f),
                new Vector3(7.55f, 0.22f, 1.15f),
                materials.Timber);
            Block(
                level,
                "RearWalkRoof",
                new Vector3(0f, 2.6f, 4.08f),
                new Vector3(7.85f, 0.2f, 1.35f),
                materials.Timber,
                Quaternion.Euler(7f, 0f, 0f));
            Block(
                level,
                "RearWalkDrain",
                new Vector3(0f, 2.48f, 4.72f),
                new Vector3(7.78f, 0.16f, 0.16f),
                materials.Bronze);
        }

        private static void AddLevel09(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L09_CivicIntegration");
            Block(
                level,
                "ForecourtEdgeWest",
                new Vector3(-4.25f, 0.26f, -5.02f),
                new Vector3(3.15f, 0.32f, 0.48f),
                materials.PaleStone);
            Block(
                level,
                "ForecourtEdgeEast",
                new Vector3(4.25f, 0.26f, -5.02f),
                new Vector3(3.15f, 0.32f, 0.48f),
                materials.PaleStone);
            Block(
                level,
                "ApproachCenter",
                new Vector3(0f, 0.12f, -5.18f),
                new Vector3(4.1f, 0.18f, 1.05f),
                materials.PaleStone);
            Block(
                level,
                "NoticePierWest",
                new Vector3(-5.08f, 0.82f, -4.88f),
                new Vector3(0.56f, 1.35f, 0.56f),
                materials.PaleStone);
            Block(
                level,
                "NoticePierEast",
                new Vector3(5.08f, 0.82f, -4.88f),
                new Vector3(0.56f, 1.35f, 0.56f),
                materials.PaleStone);
        }

        private static void AddLevel10(
            Transform root,
            MaterialSet materials)
        {
            Transform level = Group(root, "L10_OpenCrownArbor");
            Transform supports = Group(level, "CrownRootSupports");
            Vector3 frontRing = new Vector3(0f, 7.85f, -2.1f);
            Vector3 westRing = new Vector3(-2.1f, 7.85f, 0f);
            Vector3 eastRing = new Vector3(2.1f, 7.85f, 0f);
            AddBentRootSupport(
                supports,
                "FrontArchCrownRoot",
                new Vector3(0f, 5.3f, -2f),
                new Vector3(0f, 6.7f, -2.28f),
                frontRing,
                materials.Bark);
            AddBentRootSupport(
                supports,
                "WestArchCrownRoot",
                new Vector3(-2.95f, 5.5f, 0.1f),
                new Vector3(-2.62f, 6.72f, -0.12f),
                westRing,
                materials.Bark);
            AddBentRootSupport(
                supports,
                "EastArchCrownRoot",
                new Vector3(2.95f, 5.5f, 0.1f),
                new Vector3(2.62f, 6.72f, -0.12f),
                eastRing,
                materials.Bark);

            Transform ring = Group(level, "OpenSkyOculus");
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
                    ring,
                    $"BronzeRingSegment_{index:D2}",
                    start,
                    end,
                    0.13f,
                    materials.Bronze);
            }

            foreach (Vector3 point in new[]
                     {
                         frontRing,
                         westRing,
                         eastRing
                     })
            {
                Cylinder(
                    ring,
                    $"RingCollar_{point.x:0.0}_{point.z:0.0}",
                    point,
                    new Vector3(0.32f, 0.12f, 0.32f),
                    materials.Bronze);
            }
        }

        private static void AddBentRootSupport(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 bend,
            Vector3 end,
            Material material)
        {
            Transform support = Group(parent, name);
            Beam(
                support,
                "LowerRoot",
                start,
                bend,
                0.54f,
                material);
            Beam(
                support,
                "UpperRoot",
                bend,
                end,
                0.48f,
                material);
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
            camera.backgroundColor = new Color(0.03f, 0.055f, 0.058f);
            camera.transform.position = new Vector3(22.5f, 14f, -34f);
            camera.transform.LookAt(new Vector3(0f, 3.55f, 0f));

            var keyObject = new GameObject("KeyLight");
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.82f, 0.58f);
            key.intensity = 1.68f;
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            var fillObject = new GameObject("FillLight");
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.4f, 0.6f, 0.48f);
            fill.intensity = 0.76f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, 142f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.23f, 0.19f);
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
                "Assets/AL/Art/Generated/Architecture/Eldergrove/Production";
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
                PaleStone = RequireMaterial("PaleStone"),
                Bark = RequireMaterial("RootBark"),
                Timber = RequireMaterial("DarkTimber"),
                Bronze = RequireMaterial("WeatheredBronze"),
                Ground = RequireMaterial("Ground")
            };
        }

        private static Material RequireMaterial(string name)
        {
            string path = $"{MaterialFolder}/MAT_Eldergrove_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"The approved Eldergrove material {path} is missing.");
            }
            return material;
        }

        private sealed class MaterialSet
        {
            public Material PaleStone;
            public Material Bark;
            public Material Timber;
            public Material Bronze;
            public Material Ground;
        }
    }
}
